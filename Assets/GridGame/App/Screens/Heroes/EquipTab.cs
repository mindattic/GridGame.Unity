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
    /// <summary>The Hero screen's Equip tab (§25.4, GG-T435).</summary>
    internal sealed class EquipTab : HeroTab
    {
        private EquipmentSlot slot = EquipmentSlot.Weapon;
        private string previewId;
        public override string Id => "equip";
        public override void OnHeroChanged() => previewId = null;
        /// <summary>GG-T2061: the Auto-equip tip only while it has something to equip.</summary>
        public override string TipStation => Hero != null && AutoEquip.Plan(C, S, Hero).Count > 0 ? Id : null;
        public override void BuildBody(VisualElement body)
        {
            var h = Hero;
            // GG-T2061: an upgrade not shown before opens picked and previewed, so the compare reads it at once; then it's seen.
            if (previewId == null && !h.Domesticated)
            {
                var fresh = AutoEquip.Plan(C, S, h);
                int i = fresh.FindIndex(p => !S.SeenGear.Contains(p.Item.Id));
                if (i >= 0)
                {
                    slot = fresh[i].Slot;
                    previewId = fresh[i].Item.Id;
                    AutoEquip.MarkSeen(C, S, h);
                    App.Persist();
                }
            }
            bool MasteryFor(EquipmentSlot sl) => sl == EquipmentSlot.Weapon ? h.WeaponMastery > 1f : h.Armor.TryGetValue(sl, out var piece) && piece.Mastery > 1f;
            // GG-T452: the eleven slots used to be three stacked windows; with the stat compare under them they pushed the
            // bag below the stage on a phone, so nothing could be equipped. They now share one scrolling window, which
            // splits the leftover height with the bag (both grow), and scrolls the picked slot into view.
            // GG-T1873: slots, the stat compare and the bag are one scroll region filling the leftover height, no nested scroll windows.
            var slotWin = Ui.Panel(body, null, true, "equip-slots");
            var slotList = Ui.List(slotWin);
            // GG-T1972 (UX review): the eleven slots as a paper-doll grid of thumbnails, Weapon, armor, then jewelry (§24.2);
            // tapping one picks it and lists the bag for it below.
            var slotGrid = Ui.El("kit-pillar equip-grid", slotList.contentContainer);
            foreach (var sl in AutoEquip.Slots)
            {
                var s2 = sl;
                var it = C.TryItem(Equipment.Equipped(h, sl));
                bool mastery = MasteryFor(sl);
                bool enchanted = h.Armor.TryGetValue(sl, out var worn) && ArmorEnchanting.PassiveOf(C, worn.Enchant) != null; // GG-T179
                var tile = Ui.El("kit-tile equip-tile", slotGrid);
                tile.name = "equip-slot-" + sl; // GG-T448: so a specific slot can be selected by automation without clicking its display text
                tile.KindIcon(RowKind.Item, it != null ? Art.ItemIcon(it.Id) : null); // GG-T718: the worn piece, else the Item emblem
                Ui.Text(ItemUi.SlotName(sl), "kit-tile-label", tile);
                var valueRow = it != null && (mastery || enchanted) ? Ui.El("icon-line", tile) : tile;
                if (it != null && mastery) valueRow.Add(RowIcon.SetIcon("cur-masterwork", RowIcon.InlineClass));
                var value = Ui.Text(it == null ? "Empty" : it.Name, "kit-tile-value", valueRow);
                if (it != null && enchanted) valueRow.Add(RowIcon.SetIcon("itemcat-enchanted", RowIcon.InlineClass));
                value.name = "value";
                if (it != null) value.AddToClassList(Ui.RarityClass(it.Rarity));
                else value.AddToClassList("dim");
                tile.EnableInClassList("equip-tile--selected", sl == slot);
                Ui.Tappable(tile, () => { slot = s2; previewId = null; Refresh(); }, "ui.select");
            }

            if (!h.Domesticated)
            {
                var plan = AutoEquip.Plan(C, S, h);
                var auto = Ui.Actions(slotList.contentContainer);
                auto.name = "equip-auto";
                var autoBtn = Ui.Button(plan.Count == 0 ? "Best gear on" : $"Auto-equip best ({plan.Count})",
                    () => Done(AutoEquip.Apply(C, S, h), cue: "ui.equip"), null, auto, confirmTap: true);
                autoBtn.name = "equip-auto-best";
                Ui.SetEnabled(autoBtn, plan.Count > 0);
                foreach (var p in plan)
                    Ui.Caption($"{ItemUi.SlotName(p.Slot)}: {p.Item.Name}", slotList.contentContainer, "gold");
            }

            var cmp = Ui.Panel(slotList.contentContainer, null, false, "equip-compare");
            var curId = Equipment.Equipped(h, slot);
            var thenId = previewId ?? curId;
            var (now, then) = Equipment.Compare(C, h, slot, thenId);
            var grid = Ui.El("row", cmp);
            grid.style.flexWrap = Wrap.Wrap;
            void Cell(string n, float a, float b) { var c = Ui.El("row", grid); c.style.width = Length.Percent(25); Ui.Caption(n, c, "dim").style.width = Size.Column; Ui.Caption(Ui.Delta(a, b), c); }
            Cell("STR", now.Strength, then.Strength); Cell("VIT", now.Vitality, then.Vitality); Cell("AGI", now.Agility, then.Agility); Cell("SPD", now.Speed, then.Speed);
            Cell("INT", now.Intelligence, then.Intelligence); Cell("WIS", now.Wisdom, then.Wisdom); Cell("LCK", now.Luck, then.Luck);
            var curItem = C.TryItem(curId);
            var thenItem = C.TryItem(thenId);
            if (!string.IsNullOrEmpty(curItem?.PassiveName) || (!string.IsNullOrEmpty(thenItem?.PassiveName) && thenId != curId))
            {
                if (!string.IsNullOrEmpty(curItem?.PassiveName))
                    GlossaryText.Create($"Now: {curItem.PassiveName} — {curItem.PassiveDescription}", "kit-text dim", cmp);
                if (!string.IsNullOrEmpty(thenItem?.PassiveName) && thenId != curId)
                    GlossaryText.Create($"Then: {thenItem.PassiveName} — {thenItem.PassiveDescription}", "kit-text gold", cmp);
            }
            // GG-T179: the worn piece's armor enchant goes with it if it's swapped out.
            if (h.Armor.TryGetValue(slot, out var cur) && ArmorEnchanting.PassiveOf(C, cur.Enchant) is AbilityDef ench)
                GlossaryText.Create($"Enchanted: {ench.Name} — {ench.Description}", "kit-text gold", RowIcon.Line("itemcat-enchanted", cmp));
            // GG-T182: set progress for the set(s) this slot's current and previewed pieces belong to — "2/5 → 3/5"
            // when the preview would change it, with each bonus tier lit once it's reached.
            if (Array.IndexOf(BattleEngine.ArmorSlots, slot) >= 0)
            {
                var nowSets = Equipment.WornSets(C, h);
                var thenSets = Equipment.WornSetsWith(C, h, slot, thenId);
                var shown = new[] { C.TryArmorSetOf(curId), C.TryArmorSetOf(thenId) }.Where(x => x != null).Distinct();
                foreach (var set in shown)
                {
                    var preview = thenSets.FirstOrDefault(p => p.Set == set) ?? new SetProgress { Set = set };
                    int nowWorn = nowSets.FirstOrDefault(p => p.Set == set)?.Worn ?? 0;
                    ItemUi.SetLines(cmp, preview, nowWorn, compact: true);
                }
            }

            var list = slotList.contentContainer;
            Ui.Section($"{ItemUi.SlotName(slot)} — your bag", list).name = "equip-bag";
            if (Equipment.Equipped(h, slot) != null)
                Ui.MenuRow("Remove", "", () => Done(Equipment.Unequip(C, S, h, slot), cue: "ui.equip"), list).KindIcon(RowKind.Close);
            // GG-T52: the bag listing is shape-only (Equipment.Candidates with no hero) so a piece the hero
            // owns but isn't proficient in still shows up — locked, with the lock icon and the passive that would
            // unlock it — instead of silently disappearing as if it were never picked up.
            var cands = Equipment.Candidates(C, S, slot);
            if (cands.Count == 0) Ui.Empty("Nothing in your bag fits this slot. Forge gear at the Blacksmith.", list);
            foreach (var it in cands)
            {
                var item = it;
                var gate = Equipment.ProficiencyGate(C, h, item);
                var value = gate ?? $"×{S.Count(item.Id)}";
                var row = ItemUi.Row(item, value, () =>
                {
                    if (previewId == item.Id) { previewId = null; Done(Equipment.Equip(C, S, h, slot, item.Id), cue: "ui.equip"); }
                    else { previewId = item.Id; Refresh(); App.Toast("Tap again to equip"); }
                }, list, gate == null, Equipment.IsMasterwork(S, item.Id),
                rowName: (gate == null ? "equip-ok-" : "equip-locked-") + item.Id);
                if (gate != null) RowIcon.Prefix(row.Q<Label>("value"), "chrome-locked");
                if (item.Id == previewId) Ui.Select(row);
            }
        }
    }
}
