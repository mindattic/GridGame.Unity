using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    internal static class ItemUi
    {
        public static string TypeLabel(ItemDef i)
        {
            switch (i.Type)
            {
                case ItemType.Equipment: return i.Slot == EquipmentSlot.Weapon ? "Weapon" : Array.IndexOf(BattleEngine.ArmorSlots, i.Slot) >= 0 ? "Armor" : "Jewelry";
                case ItemType.Consumable: return "Item";
                case ItemType.CraftingMaterial: return "Material";
                case ItemType.RecipeScroll: return "Recipe Scroll";
                case ItemType.Tome: return "Tome";
                case ItemType.Chest: return "Chest";
                default: return "Key Item";
            }
        }

        public static string SlotName(EquipmentSlot s) =>
            s == EquipmentSlot.Earring1 ? "Earring I" : s == EquipmentSlot.Earring2 ? "Earring II" :
            s == EquipmentSlot.Ring1 ? "Ring I" : s == EquipmentSlot.Ring2 ? "Ring II" : s.ToString();

        /// <summary>The item's category set icon id (<see cref="Art.UiIcon"/>, docs/ICONS.md <c>itemcat-*</c>).</summary>
        public static string CategoryIconId(ItemDef i)
        {
            switch (i.Type)
            {
                case ItemType.Equipment: return i.Slot == EquipmentSlot.Weapon ? "itemcat-weapon" : Array.IndexOf(BattleEngine.ArmorSlots, i.Slot) >= 0 ? "itemcat-armor" : "itemcat-accessory";
                case ItemType.Consumable: return "itemcat-consumable";
                case ItemType.CraftingMaterial: return "itemcat-material";
                case ItemType.RecipeScroll: case ItemType.Tome: return "itemcat-recipe";
                default: return "itemcat-enchanted";
            }
        }

        public static string StatsText(ItemDef i)
        {
            var parts = new List<string>();
            var s = i.Stats;
            void P(string n, float v) { if (Math.Abs(v) > 0.01f) parts.Add($"{n} {(v > 0 ? "+" : "")}{v:0}"); }
            P("STR", s.Strength); P("VIT", s.Vitality); P("AGI", s.Agility); P("SPD", s.Speed);
            P("INT", s.Intelligence); P("WIS", s.Wisdom); P("LCK", s.Luck);
            if (i.BaseDamage > 0) parts.Add($"ATK +{i.BaseDamage}");
            if (i.BaseHealing > 0) parts.Add($"Heals {i.BaseHealing}");
            if (i.Durability > 0) parts.Add($"Durability {i.Durability}");
            if (i.BattleStartManaOrbs > 0) parts.Add($"+{i.BattleStartManaOrbs * 100} mana at battle start");
            if (i.PincerDamageBonus > 0) parts.Add(FormattableString.Invariant($"Pincer +{i.PincerDamageBonus:0%}"));
            if (i.EvasionBonus > 0) parts.Add($"Evasion +{i.EvasionBonus:0}"); // GG-T297
            if (i.RegenPerTurn > 0) parts.Add($"Regen {i.RegenPerTurn}/turn");
            if (i.StartsProtected) parts.Add("Starts battles Protected");
            if (i.GrantsOrbs > 0) parts.Add($"+{i.GrantsOrbs * 100} mana");
            if (i.Revives) parts.Add("Revives a fallen ally");
            if (i.Escape) parts.Add("Escape from battle");
            if (i.CuresAll) parts.Add("Cures all ailments");
            else if (i.CuresBuff != null) parts.Add($"Cures {i.CuresBuff}");
            foreach (var kv in i.Resistances) parts.Add(FormattableString.Invariant($"{kv.Key} ×{kv.Value:0.##}"));
            if (!string.IsNullOrEmpty(i.PassiveName)) parts.Add($"{i.PassiveName}: {i.PassiveDescription}");
            return string.Join("   ", parts);
        }

        /// <summary>Standard item row: icon · rarity-colored name · right value. <paramref name="masterwork"/> prefixes the masterwork icon (GG-T46).
        /// <paramref name="nameOverride"/> replaces the item's own name in the label (GG-T198: lets a recipe row show
        /// "Result — key inputs" instead of the bare item name, e.g. when two recipes share a result).</summary>
        public static VisualElement Row(ItemDef i, string value, Action onClick, VisualElement parent, bool enabled = true, bool masterwork = false, string nameOverride = null, string rowName = null)
        {
            string label = nameOverride ?? i.Name;
            var row = Ui.MenuRow(label, value, onClick, parent, null, enabled);
            row.KindIcon(RowKind.Item, Art.ItemIcon(i.Id)); // GG-T717 (§26.7): its pixel icon, else the Item emblem
            if (masterwork) RowIcon.Prefix(row.Q<Label>("label"), "cur-masterwork");
            if (rowName != null) row.name = rowName; // GG-T448: an automation id for screens (e.g. Equip) that otherwise have none
            row.Q<Label>("label").AddToClassList(Ui.RarityClass(i.Rarity));
            // Hold any item row to read the whole item — stats, set, slot — without selecting it (GG-T252); a row whose
            // tap does nothing else reads on a tap too (GG-T450).
            InspectCard.Attach(row, b => { Detail(b, i, GameApp.I?.Content); return true; }, tap: onClick == null);
            return row;
        }

        public static void Detail(VisualElement w, ItemDef i, ContentDb c = null)
        {
            w.Clear();
            if (i == null) { Ui.Empty("Select an item.", w); return; }
            // The kit's detail pane (GG-T454): heading + one caption of meta, then body sentences.
            var top = Ui.El("row", w);
            Ui.Heading(i.Name, top, "grow " + Ui.RarityClass(i.Rarity));
            Ui.Caption($"{i.Rarity} {TypeLabel(i)}", top, "dim");
            GlossaryText.Create(string.IsNullOrEmpty(i.Description) ? "—" : i.Description, "kit-text", w);
            var st = StatsText(i);
            if (st.Length > 0) GlossaryText.Create(st, "kit-text gold", w);
            // GG-T301: a Tome names the passive it teaches as its generated rule sentence.
            if (RewardCatalog.TaughtBy(c, i) is AbilityDef taught) GlossaryText.Create(TomeUi.Sentence(c, taught), "kit-text gold", w);
            var set = c?.TryArmorSetOf(i.Id);
            if (set != null)
            {
                Ui.Section($"{set.Name} set · {set.Weight} · Lv {set.Level}", w);
                foreach (var b in set.Bonuses) Ui.Para($"{b.Pieces} pieces: {ArmorSets.Describe(b)}", w, "dim");
            }
        }

        /// <summary>Armor set progress lines (GG-T182): "Wayfarer's set 2/5" (or "2/5 → 3/5" for an Equip preview)
        /// then one line per bonus tier, with the cleared icon and green once that many pieces are worn.</summary>
        /// <remarks><paramref name="compact"/> folds the tiers onto one wrapped line so the Equip screen's bag list
        /// keeps its room (GG-T193).</remarks>
        public static void SetLines(VisualElement parent, SetProgress progress, int nowWorn, bool compact = false)
        {
            int worn = progress.Worn;
            var set = progress.Set;
            string count = nowWorn == worn ? $"{worn}/5" : $"{nowWorn}/5 → {worn}/5";
            string tone = worn > nowWorn ? "good" : worn < nowWorn ? "bad" : "gold";
            string Tier(SetBonusDef b) => $"{(worn >= b.Pieces ? "✔ " : "")}{b.Pieces} pieces: {ArmorSets.Describe(b)}";
            if (compact)
            {
                Ui.Para($"{set.Name} set  {count}   ·   " + string.Join("   ·   ", set.Bonuses.Select(Tier)), parent, tone);
                return;
            }
            Ui.Para($"{set.Name} set  {count}", parent, tone);
            foreach (var b in set.Bonuses) Ui.Para($"{b.Pieces} pieces: {ArmorSets.Describe(b)}", worn >= b.Pieces ? RowIcon.Line("chrome-cleared", parent) : parent, worn >= b.Pieces ? "good" : "dim");
        }
    }

    // ── Items (inventory) ─────────────────────────────────────────────────────────

    public sealed class ItemsScreen : HubPage
    {
        private int tab;
        private ItemDef selected;
        private bool selectedBroken;
        public override string Id => "items";
        protected override string Title => "Items";
        private static readonly string[] Tabs = { "All", "Consumables", "Gear", "Materials" };

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(Tabs, tab, i => { tab = i; selected = null; Refresh(); }, body);
            // Rows keep their own height at the top of the panel, the spare height left below them (GG-T1882).
            var list = Ui.List(Ui.Panel(body, fill: true)).contentContainer;
            var items = S.Inventory.Where(kv => kv.Value > 0).Select(kv => (item: C.TryItem(kv.Key), n: kv.Value)).Where(x => x.item != null)
                .Where(x => tab == 0 || (tab == 1 && (x.item.Type == ItemType.Consumable || x.item.Type == ItemType.Tome || x.item.Type == ItemType.Chest)) || (tab == 2 && x.item.Type == ItemType.Equipment) || (tab == 3 && x.item.Type == ItemType.CraftingMaterial))
                .OrderBy(x => x.item.Type).ThenByDescending(x => x.item.Rarity).ThenBy(x => x.item.Name).ToList();
            // Broken gear fragments (GG-T41): shown alongside gear, but can't be equipped or repaired — salvage only.
            var broken = (tab == 0 || tab == 2)
                ? S.Broken.Where(kv => kv.Value > 0).Select(kv => (item: C.TryItem(kv.Key), n: kv.Value)).Where(x => x.item != null)
                    .OrderByDescending(x => x.item.Rarity).ThenBy(x => x.item.Name).ToList()
                : new List<(ItemDef item, int n)>();
            var detail = Ui.DetailPanel(body);
            if (items.Count == 0 && broken.Count == 0) Ui.Empty("Nothing here yet. Win battles to find loot.", list);
            foreach (var (item, n) in items)
            {
                var it = item;
                var row = ItemUi.Row(it, $"×{n}", () => { }, list, true, Equipment.IsMasterwork(S, it.Id));
                Pick(row, detail, it, false);
            }
            foreach (var (item, n) in broken)
            {
                var it = item;
                var row = Ui.MenuRow($"Broken {it.Name}", $"×{n}", null, list);
                row.KindIcon(RowKind.Item, Art.ItemIcon(it.Id)); // GG-T717
                row.Q<Label>("label").AddToClassList("dim");
                Pick(row, detail, it, true);
            }
            var fallback = selected;
            bool fallbackBroken = selectedBroken;
            if (fallback == null)
            {
                fallback = items.Select(x => x.item).FirstOrDefault();
                fallbackBroken = false;
                if (fallback == null) { fallback = broken.Select(x => x.item).FirstOrDefault(); fallbackBroken = fallback != null; }
            }
            ShowDetail(detail, fallback, fallbackBroken);
            if (selected == null && list.childCount > 0 && (items.Count > 0 || broken.Count > 0)) Ui.Select(list[0]);
        }

        /// <summary>A 132-px kit row (GG-T2019): the first tap selects it and shows its detail at once; the Use and Learn
        /// buttons in the detail are the confirm (GG-T1942), so a second tap on the row runs nothing more.</summary>
        private void Pick(VisualElement row, VisualElement detail, ItemDef it, bool broken)
        {
            row.AddToClassList("menu-row--compact");
            row.RegisterCallback<ClickEvent>(_ => { selected = it; selectedBroken = broken; ShowDetail(detail, it, broken); });
            if (selected == it && selectedBroken == broken) Ui.Select(row);
        }

        private void ShowDetail(VisualElement detail, ItemDef it, bool broken)
        {
            ItemUi.Detail(detail, it, C);
            if (broken)
            {
                Ui.Para("Broken. Can't be equipped or repaired — salvage it at the Blacksmith for materials.", detail, "bad");
                return;
            }
            if (it != null && it.Type == ItemType.RecipeScroll)
            {
                RowIcon.IconButton("Learn recipe", RowKind.Confirm, () => { selected = it; Done(RecipeDiscovery.Learn(C, S, it.Id)); }, "btn--small btn--primary", Ui.Actions(detail));
                return;
            }
            // GG-T301: read a Tome to a hero, or open a Chest.
            if (it != null && it.Type == ItemType.Tome)
            {
                var teach = TomeUi.Offer(detail, it, r => { if (r.Ok) { selected = S.Count(it.Id) > 0 ? it : null; Refresh(); } }, false);
                Ui.Margin(teach, left: Space.S0);
                return;
            }
            if (it != null && it.Type == ItemType.Chest)
            {
                TomeUi.OpenButton(Ui.Actions(detail), it, (r, haul) => { selected = r.Ok && haul != null ? C.TryItem(haul.Items.Keys.FirstOrDefault(k => C.TryItem(k)?.Type == ItemType.Tome) ?? it.Id) : it; if (selected != null && S.Count(selected.Id) <= 0) selected = null; Refresh(); });
                return;
            }
            if (!Camp.UsableAtCamp(it)) return;
            var row = Ui.Actions(detail);
            if (Camp.HealsWholeParty(it))
            {
                RowIcon.IconButton("Use on party", RowKind.Confirm, () => Done(Camp.Use(C, S, it.Id, null)), "btn--small btn--primary", row);
                return;
            }
            // One button per party hero; a party of six (GG-T291) wraps into equal thirds instead of running off the window.
            foreach (var id in S.Party)
            {
                var hero = id;
                var b = RowIcon.IconButton(C.TryClass(id)?.Name ?? id, RowKind.Hero, () => { selected = it; Done(Camp.Use(C, S, it.Id, hero)); }, "btn--small", row);
                var face = b.KindIcon(RowKind.Hero, id); // GG-T750 (§26.7): the hero's own thumbnail, else the Hero emblem
                face.style.width = face.style.height = Size.IconS; Ui.Margin(face, right: Space.S8);
                b.style.flexGrow = 1; b.style.flexShrink = 1; b.style.flexBasis = Length.Percent(25); b.style.minWidth = 0;
                b.name = "use-on-" + id;
            }
        }
    }
}
