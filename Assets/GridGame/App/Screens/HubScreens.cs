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

        public static string Glyph(ItemDef i)
        {
            switch (i.Type)
            {
                case ItemType.Equipment: return i.Slot == EquipmentSlot.Weapon ? "†" : Array.IndexOf(BattleEngine.ArmorSlots, i.Slot) >= 0 ? "◆" : "✧";
                case ItemType.Consumable: return "✚";
                case ItemType.CraftingMaterial: return "◈";
                case ItemType.RecipeScroll: case ItemType.Tome: return "❖";
                default: return "✦";
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
            if (i.BattleStartManaOrbs > 0) parts.Add($"+{i.BattleStartManaOrbs} orbs at battle start");
            if (i.BankCapacityBonus > 0) parts.Add($"+{i.BankCapacityBonus} orb slots");
            if (i.PincerDamageBonus > 0) parts.Add(FormattableString.Invariant($"Pincer +{i.PincerDamageBonus:0%}"));
            if (i.EvasionBonus > 0) parts.Add($"Evasion +{i.EvasionBonus:0}"); // GG-T297
            if (i.RegenPerTurn > 0) parts.Add($"Regen {i.RegenPerTurn}/turn");
            if (i.StartsProtected) parts.Add("Starts battles Protected");
            if (i.GrantsOrbs > 0) parts.Add($"+{i.GrantsOrbs} orbs");
            if (i.Revives) parts.Add("Revives a fallen ally");
            if (i.Escape) parts.Add("Escape from battle");
            if (i.CuresAll) parts.Add("Cures all ailments");
            else if (i.CuresBuff != null) parts.Add($"Cures {i.CuresBuff}");
            foreach (var kv in i.Resistances) parts.Add(FormattableString.Invariant($"{kv.Key} ×{kv.Value:0.##}"));
            if (!string.IsNullOrEmpty(i.PassiveName)) parts.Add($"{i.PassiveName}: {i.PassiveDescription}");
            return string.Join("   ", parts);
        }

        /// <summary>Standard item row: glyph · rarity-colored name · right value. <paramref name="masterwork"/> prefixes a ★ (GG-T46).
        /// <paramref name="nameOverride"/> replaces the item's own name in the label (GG-T198: lets a recipe row show
        /// "Result — key inputs" instead of the bare item name, e.g. when two recipes share a result).</summary>
        public static VisualElement Row(ItemDef i, string value, Action onClick, VisualElement parent, bool enabled = true, bool masterwork = false, string nameOverride = null, string rowName = null)
        {
            string label = nameOverride ?? i.Name;
            if (masterwork) label = "★ " + label;
            var row = Ui.MenuRow(label, value, onClick, parent, null, enabled);
            row.KindIcon(RowKind.Item, Art.ItemIcon(i.Id)); // GG-T717 (§26.7): its pixel icon, else the Item emblem
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
        /// then one line per bonus tier, ✔ and green once that many pieces are worn.</summary>
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
            foreach (var b in set.Bonuses) Ui.Para(Tier(b), parent, worn >= b.Pieces ? "good" : "dim");
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
        private static readonly string[] Tabs = { "All", "Items", "Gear", "Materials" };

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(Tabs, tab, i => { tab = i; selected = null; Refresh(); }, body);
            var list = Ui.ListPanel(body);
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
                var row = ItemUi.Row(it, $"×{n}", () => { selected = it; selectedBroken = false; ShowDetail(detail, it, false); }, list, true, Equipment.IsMasterwork(S, it.Id));
                if (selected == it && !selectedBroken) Ui.Select(row);
            }
            foreach (var (item, n) in broken)
            {
                var it = item;
                var row = Ui.MenuRow($"Broken {it.Name}", $"×{n}", () => { selected = it; selectedBroken = true; ShowDetail(detail, it, true); }, list);
                row.KindIcon(RowKind.Item, Art.ItemIcon(it.Id)); // GG-T717
                row.Q<Label>("label").AddToClassList("dim");
                if (selected == it && selectedBroken) Ui.Select(row);
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
                teach.style.marginLeft = Space.S0;
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
                face.style.width = face.style.height = Size.IconS; face.style.marginRight = Space.S8;
                b.style.flexGrow = 1; b.style.flexShrink = 1; b.style.flexBasis = Length.Percent(25); b.style.minWidth = 0;
                b.name = "use-on-" + id;
            }
        }
    }

    // ── Status ────────────────────────────────────────────────────────────────────

    /// <summary>The Hero screen's Status tab (§25.0, GG-T435): the per-hero character sheet that was the Status screen.</summary>
    internal sealed class StatusTab : HeroTab
    {
        public override string Id => "status";

        public override void BuildBody(VisualElement body)
        {
            var h = Hero;
            var def = C.Class(h.ClassId);
            var u = Progression.Preview(C, h);

            // GG-T454 (the menu kit): one scrolling sheet, the build summary as the window's caption, then the portrait and
            // numbers, gear, lore and the build sections under kit section headers. It scrolls inside itself, so a long
            // Known list or lots of gear never pushes anything off the screen (GG-T48).
            var sheet = Ui.ListPanel(body, null, "status-sheet");
            // GG-T48: the build summary — class, tree investment and role, all read off the numbers below.
            Ui.Para(Progression.BuildSummary(C, h), sheet, "gold");
            var top = Ui.El("row", sheet);
            top.style.alignItems = Align.FlexStart;
            var artCol = Ui.El("col", top);
            artCol.style.alignItems = Align.Center;
            var art = Ui.El("", artCol);
            art.style.width = Size.PortraitW; art.style.height = Size.ArtH;
            art.style.backgroundImage = new StyleBackground(Art.PortraitFigure(h.ClassId));
            art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            // GG-T532: a class with male art can switch looks here any time (the same picker as on joining).
            if (!h.Domesticated && Art.HasMaleArt(h.ClassId))
                RowIcon.IconButton("Change look", RowKind.Hero, () => VariantPicker.Change(h, Refresh), "btn--small", artCol).name = "hero-look";
            var stats = Ui.El("col grow", top);
            stats.style.marginLeft = Space.S24; stats.style.flexShrink = 1; stats.style.minWidth = 0;
            int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp;
            Ui.RoleBadge(Ui.RoleOf(h, def), stats).style.alignSelf = Align.FlexStart;
            Ui.Para(Ui.RoleBlurb(Ui.RoleOf(h, def)), stats, "dim");
            GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, def.Support), "kit-text gold", stats).name = "hero-support"; // GG-T298
            Ui.Heading($"HP  {hp} / {u.MaxHp}", stats);
            Ui.Gauge(hp / (float)u.MaxHp, stats);
            var (lvl, into) = GridGame.Core.Rules.Formulas.LevelFromTotalXp(h.TotalXp);
            Ui.Heading($"Level {lvl}", stats, "gold");
            Ui.Caption($"Next: {GridGame.Core.Rules.Formulas.XpToNext(lvl) - into} XP  ·  Skill points: {h.SkillPoints}", stats, "dim");

            // GG-T48: base (pre-gear) vs. gear-bought vs. total, instead of just the effective total.
            // GG-T1043: the skill hexagon's owned passives get their own Board column (they were folded into Base).
            var board = HeroBoard.BoardStats(C, h);
            var withBoard = u.LevelStats; // the preview's level stats already carry the board bonus (BattleFactory.SpecFor)
            var baseStats = withBoard + board * -1f;
            var eff = u.Effective;
            var statHead = Ui.El("row", stats);
            statHead.name = "status-stat-head";
            Ui.Caption("", statHead).style.width = Size.Column;
            Ui.Caption("Base", statHead, "dim").style.width = Size.ColumnS;
            Ui.Caption("Board", statHead, "dim").style.width = Size.ColumnS;
            Ui.Caption("Gear", statHead, "dim").style.width = Size.ColumnS;
            Ui.Caption("Total", statHead, "dim").style.width = Size.Column;
            void Delta(VisualElement r, float d) =>
                Ui.Caption(Math.Abs(d) < 0.05f ? "—" : $"{(d > 0 ? "+" : "")}{d:0}", r, d > 0.05f ? "good" : d < -0.05f ? "bad" : "dim").style.width = Size.ColumnS;
            void Stat(string n, float b, float bb, float t)
            {
                var r = Ui.El("row", stats);
                Ui.Caption(n, r, "dim").style.width = Size.Column;
                Ui.Caption($"{b:0}", r, "dim").style.width = Size.ColumnS;
                Delta(r, bb - b);
                Delta(r, t - bb);
                Ui.Name($"{t:0}", r).style.width = Size.Column;
            }
            Stat("STR", baseStats.Strength, withBoard.Strength, eff.Strength); Stat("VIT", baseStats.Vitality, withBoard.Vitality, eff.Vitality);
            Stat("AGI", baseStats.Agility, withBoard.Agility, eff.Agility); Stat("SPD", baseStats.Speed, withBoard.Speed, eff.Speed);
            Stat("INT", baseStats.Intelligence, withBoard.Intelligence, eff.Intelligence); Stat("WIS", baseStats.Wisdom, withBoard.Wisdom, eff.Wisdom);
            Stat("LCK", baseStats.Luck, withBoard.Luck, eff.Luck);

            BondsUi.TrustAndBonds(C, S, h.ClassId, sheet); // GG-T1299

            Ui.Section("Gear", sheet);
            void G(string label, string id, int dur = -1, int repairs = 0, float mastery = 1f, string enchant = null)
            {
                var it = C.TryItem(id);
                string name = it == null ? "—" : (mastery > 1f ? "★ " : "") + it.Name +
                    (dur >= 0 && it.Durability > 0 ? $"  ({dur}/{Repair.EffectiveMax(it, repairs, mastery)})" : "");
                // GG-T179: an enchanted armor piece names the passive it grants.
                var passive = it != null ? ArmorEnchanting.PassiveOf(C, enchant) : null;
                if (passive != null) name += $"  ✦ {passive.Name}";
                Ui.Field(label, name, sheet, it != null ? Ui.RarityClass(it.Rarity) : "dim");
            }
            G("Weapon", h.WeaponId, h.WeaponDurability, h.WeaponRepairs, h.WeaponMastery);
            foreach (var slot in BattleEngine.ArmorSlots)
            {
                h.Armor.TryGetValue(slot, out var piece);
                G(slot.ToString(), piece?.ItemId, piece?.Durability ?? -1, piece?.Repairs ?? 0, piece?.Mastery ?? 1f, piece?.Enchant);
            }
            foreach (var slot in Equipment.JewelrySlots)
                G(ItemUi.SlotName(slot), h.Jewelry.TryGetValue(slot, out var jid) ? jid : null);
            Ui.Section("Lore", sheet);
            GlossaryText.Create(def.Lore ?? def.Description ?? "", "kit-text dim", sheet);

            // GG-T182: armor set progress — every set this hero wears a piece of, with each bonus tier lit once reached.
            var setsBlock = Ui.El("col", sheet);
            setsBlock.name = "status-armor-sets";
            Ui.Section("Armor sets", setsBlock);
            var worn = Equipment.WornSets(C, h);
            if (worn.Count == 0) Ui.Para("No set pieces worn. Two or five pieces of one named set unlock its bonuses.", setsBlock, "dim");
            foreach (var p in worn) ItemUi.SetLines(setsBlock, p, p.Worn);

            var abilitiesBlock = Ui.El("col", sheet);
            abilitiesBlock.name = "status-known-abilities";
            var ownedAbilities = HeroBoard.OwnedAbilities(C, h); // GG-T1043: known, or on an owned skill hexagon node
            Ui.Section($"Known abilities ({ownedAbilities.Count})", abilitiesBlock);
            if (ownedAbilities.Count == 0) Ui.Para("None learned yet.", abilitiesBlock, "dim");
            foreach (var id in ownedAbilities)
            {
                var a = C.TryAbility(id);
                if (a == null) continue;
                var row = Ui.MenuRow(a.Name, a.Kind == AbilityKind.Passive ? "Passive" : a.Kind.ToString(), null, abilitiesBlock, Art.AbilityIcon(a));
                InspectCard.Attach(row, b => InspectCard.Ability(b, a, wearer: C.TryClass(h.ClassId)), tap: true); // GG-T450: tap or hold to read it
            }

            var profBlock = Ui.El("col", sheet);
            profBlock.name = "status-proficiencies";
            Ui.Section("Proficiencies", profBlock);
            var profs = Progression.KnownProficiencies(C, h);
            if (profs.Count == 0) Ui.Para("None yet — unlocked through skill-tree passives (Heavy Armor Proficiency, Martial Weapons, Arcane Focus).", profBlock, "dim");
            foreach (var p in profs) Ui.Para("✔ " + p, profBlock, "good");
        }
    }

    // ── Trust and bonds (§25.20, GG-T1299) ────────────────────────────────────────

    /// <summary>Trust and cohesion read on the Hero screen: the Status tab's Trust meter and top bonds, and the Party tab's
    /// cohesion grid, so every bond reads without a debug tool.</summary>
    internal static class BondsUi
    {
        private const int TopBonds = 3;

        /// <summary>The Status tab's Trust block (value, tier pips, the next tier's payoff) and the hero's three strongest
        /// bonds. Nothing for a hero without Trust (a domesticated monster, a mercenary).</summary>
        public static void TrustAndBonds(ContentDb c, SaveData s, string classId, VisualElement parent)
        {
            if (!Bonds.HasTrust(s, classId)) return;
            var block = Ui.El("col", parent);
            block.name = "status-trust";
            Ui.Section("Trust", block);
            int trust = Bonds.Trust(s, classId), tier = Bonds.TrustTier(s, classId);
            var head = Ui.El("row", block);
            head.style.alignItems = Align.Center;
            Ui.Caption($"Tier {tier}  ·  {trust} / {BondsConfig.Max}", head, "gold").style.marginRight = Space.S16;
            TierPips(tier, head);
            var meter = Ui.Gauge(trust / (float)BondsConfig.Max, block).Q("fill");
            meter.RemoveFromClassList("gauge-fill--mid"); meter.RemoveFromClassList("gauge-fill--low"); // low Trust is not a warning
            Ui.Para(NextTrustPayoff(tier), block, "dim").name = "status-trust-next";

            var bondsBlock = Ui.El("col", parent);
            bondsBlock.name = "status-bonds";
            Ui.Section("Bonds", bondsBlock);
            var top = s.Roster.Select(x => x.ClassId)
                .Where(id => Bonds.Key(id) != Bonds.Key(classId) && Bonds.HasCohesion(s, id))
                .Select(id => (id, value: Bonds.Cohesion(s, classId, id)))
                .Where(p => p.value > 0)
                .OrderByDescending(p => p.value).ThenBy(p => p.id)
                .Take(TopBonds).ToList();
            if (top.Count == 0) { Ui.Para("No bonds yet. Heroes grow closer pincering together and healing, shielding or buffing each other.", bondsBlock, "dim"); return; }
            var row = Ui.El("row", bondsBlock);
            row.style.alignItems = Align.FlexStart;
            foreach (var (id, value) in top)
            {
                var col = Ui.El("col", row);
                col.name = "status-bond-" + id;
                col.style.flexGrow = 1; col.style.flexBasis = 0; col.style.minWidth = 0;
                col.style.alignItems = Align.Center;
                Ui.Thumb(id, col, Kit.Avatar);
                var name = Ui.Marquee(Ui.Caption(c.TryClass(id)?.Name ?? id, col), grow: false);
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                Ui.Caption($"Tier {Bonds.Tier(value)}", col, Bonds.Tier(value) > 0 ? "gold" : "dim");
            }
        }

        /// <summary>What the next Trust tier pays, in one line (the top tier says what it holds).</summary>
        private static string NextTrustPayoff(int tier)
        {
            string perTier = $"+{BondsConfig.CritPerTrustTier * 100f:0}% crit and +{BondsConfig.EvasionPerTrustTier * 100f:0}% evasion";
            if (tier >= BondsConfig.MaxTier)
                return $"Highest tier: {perTier} a tier, and Resolve (survive one lethal blow a battle).";
            int next = tier + 1;
            var parts = new List<string> { perTier };
            if (next == BondsConfig.ResolveTier) parts.Add("Resolve (survive one lethal blow a battle)");
            if (Array.IndexOf(BondsConfig.TrustSceneTiers, next) >= 0) parts.Add("a Trust scene");
            return $"Next: tier {next} at {Bonds.Floor(next)} Trust: {string.Join(", ", parts)}.";
        }

        /// <summary>One pip per tier, the reached ones lit gold.</summary>
        private static void TierPips(int tier, VisualElement parent)
        {
            var row = Ui.El("row", parent);
            row.pickingMode = PickingMode.Ignore;
            for (int t = 1; t <= BondsConfig.MaxTier; t++)
            {
                var p = Ui.Text("", "pip", row);
                p.style.backgroundColor = t <= tier ? Palette.Gold : Palette.Ink.WithA(0.6f);
                if (t > tier) p.style.opacity = 0.5f;
            }
        }

        /// <summary>The Party tab's cohesion grid: the party down the side and across the top, each cell the pair's cohesion
        /// tier (gold from the Duo tier). Nothing for a party of fewer than two heroes with cohesion.</summary>
        public static void CohesionGrid(ContentDb c, SaveData s, VisualElement body)
        {
            var ids = s?.Party?.Where(id => Bonds.HasCohesion(s, id)).ToList();
            if (ids == null || ids.Count < 2) return;
            var panel = Ui.Panel(body, "Party cohesion", false, "party-cohesion");
            VisualElement Cell(VisualElement row)
            {
                var cell = Ui.El("col", row);
                cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.minWidth = 0;
                cell.style.alignItems = Align.Center; cell.style.justifyContent = Justify.Center;
                cell.style.height = Size.IconM + Space.S8;
                return cell;
            }
            var header = Ui.El("row", panel);
            Cell(header);
            foreach (var id in ids) Ui.Thumb(id, Cell(header), Size.IconM);
            foreach (var a in ids)
            {
                var row = Ui.El("row", panel);
                row.name = "party-cohesion-" + a;
                Ui.Thumb(a, Cell(row), Size.IconM);
                foreach (var b in ids)
                {
                    var cell = Cell(row);
                    if (a == b) { Ui.Caption("—", cell, "dim"); continue; }
                    int t = Bonds.CohesionTier(s, a, b);
                    Ui.Caption(t.ToString(), cell, t >= BondsConfig.DuoTier ? "gold" : t > 0 ? "good" : "dim");
                }
            }
            Ui.Caption($"Cohesion tier of each pair, 0 to {BondsConfig.MaxTier}", panel, "dim");
        }
    }

    // ── Equip ─────────────────────────────────────────────────────────────────────

    /// <summary>The Hero screen's Equip tab (§25.4, GG-T435).</summary>
    internal sealed class EquipTab : HeroTab
    {
        private EquipmentSlot slot = EquipmentSlot.Weapon;
        private string previewId;
        public override string Id => "equip";
        public override void OnHeroChanged() => previewId = null;
        public override void BuildBody(VisualElement body)
        {
            var h = Hero;
            bool MasteryFor(EquipmentSlot sl) => sl == EquipmentSlot.Weapon ? h.WeaponMastery > 1f : h.Armor.TryGetValue(sl, out var piece) && piece.Mastery > 1f;
            // GG-T452: the eleven slots used to be three stacked windows; with the stat compare under them they pushed the
            // bag below the stage on a phone, so nothing could be equipped. They now share one scrolling window, which
            // splits the leftover height with the bag (both grow), and scrolls the picked slot into view.
            // GG-T454: both are kit list panels, which split the leftover height evenly.
            Ui.ListPanel(body, null, out var slotList, "equip-slots");
            VisualElement pickedRow = null;
            void SlotGroup(string title, IEnumerable<EquipmentSlot> slots)
            {
                Ui.Section(title, slotList.contentContainer);
                foreach (var sl in slots)
                {
                    var s2 = sl;
                    var it = C.TryItem(Equipment.Equipped(h, sl));
                    bool mastery = MasteryFor(sl);
                    bool enchanted = h.Armor.TryGetValue(sl, out var worn) && ArmorEnchanting.PassiveOf(C, worn.Enchant) != null; // GG-T179
                    var row = Ui.MenuRow(ItemUi.SlotName(sl), it == null ? "—" : (mastery ? "★ " : "") + it.Name + (enchanted ? " ✦" : ""), () => { slot = s2; previewId = null; Refresh(); }, slotList.contentContainer);
                    row.AddToClassList("menu-row--compact"); // GG-T760: the Equip list's own shorter rows, smaller text
                    row.KindIcon(RowKind.Item, it != null ? Art.ItemIcon(it.Id) : null); // GG-T718: the worn piece, else the Item emblem
                    row.name = "equip-slot-" + sl; // GG-T448: so a specific slot can be selected by automation without clicking its display text
                    if (it != null) row.Q<Label>("value").AddToClassList(Ui.RarityClass(it.Rarity));
                    if (sl == slot) { Ui.Select(row); pickedRow = row; }
                }
            }
            // §24.2 equip screen layout (GG-T89): Weapon, then the five armor pieces, then the five jewelry pieces.
            SlotGroup("Weapon", new[] { EquipmentSlot.Weapon });
            SlotGroup("Armor", BattleEngine.ArmorSlots);
            SlotGroup("Jewelry", Equipment.JewelrySlots);
            if (pickedRow != null) slotList.schedule.Execute(() => slotList.ScrollTo(pickedRow)).ExecuteLater(30);

            var cmp = Ui.Panel(body, null, false, "equip-compare");
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
                GlossaryText.Create($"✦ Enchanted: {ench.Name} — {ench.Description}", "kit-text gold", cmp);
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

            // GG-T452: splits the leftover height evenly with the slot list above (both kit list panels).
            var list = Ui.ListPanel(body, $"{ItemUi.SlotName(slot)} — your bag", "equip-bag");
            if (Equipment.Equipped(h, slot) != null)
                Ui.MenuRow("Remove", "", () => Done(Equipment.Unequip(C, S, h, slot), cue: "ui.equip"), list).KindIcon(RowKind.Close);
            // GG-T52: the bag listing is shape-only (Equipment.Candidates with no hero) so a piece the hero
            // owns but isn't proficient in still shows up — locked, with a 🔒 and the passive that would
            // unlock it — instead of silently disappearing as if it were never picked up.
            var cands = Equipment.Candidates(C, S, slot);
            if (cands.Count == 0) Ui.Empty("Nothing in your bag fits this slot. Forge gear at the Blacksmith.", list);
            foreach (var it in cands)
            {
                var item = it;
                var gate = Equipment.ProficiencyGate(C, h, item);
                var value = gate != null ? $"🔒 {gate}" : $"×{S.Count(item.Id)}";
                var row = ItemUi.Row(item, value, () =>
                {
                    if (previewId == item.Id) { previewId = null; Done(Equipment.Equip(C, S, h, slot, item.Id), cue: "ui.equip"); }
                    else { previewId = item.Id; Refresh(); App.Toast("Tap again to equip"); }
                }, list, gate == null, Equipment.IsMasterwork(S, item.Id),
                rowName: (gate == null ? "equip-ok-" : "equip-locked-") + item.Id);
                if (item.Id == previewId) Ui.Select(row);
            }
        }
    }

    // ── Abilities: learn + slot the bar ───────────────────────────────────────────

    /// <summary>The Hero screen's Abilities tab (§25.6, GG-T435): the battle bar over the Known / Items / Passives /
    /// Locked sub-tabs, plus Board — the skill hexagon (GG-T1079: the gold Learn sub-tab is gone; abilities come from the board).</summary>
    internal sealed class AbilitiesTab : HeroTab
    {
        private int slot;
        private int tab = -1;
        private readonly SkillHexTab talents = new SkillHexTab();
        /// <summary>The Board sub-tab's index (after Known, Items, Passives, Locked): the skill hexagon (GG-T1056),
        /// in place of the old Talents skill trees; its id stays "talents".</summary>
        public const int TalentsSub = 4;
        private static readonly string[] SubTabs = { "Known", "Items", "Passives", "Locked", "Board" };

        public AbilitiesTab(int subTab = -1, (int Q, int R)? boardNode = null) { tab = subTab; talents.Select(boardNode); }
        public override void OnHeroChanged() => talents.OnHeroChanged();
        /// <summary>"talents" while the Talents sub-tab is up, so `-start talents` and the harness's goto check still match.</summary>
        public override string Id => tab == TalentsSub ? talents.Id : "abilities";
        /// <summary>GG-T180/GG-T1086: while onboarding step 7 is ahead, the Board sub-tab (where the guided lesson sits)
        /// carries the training tip; the other sub-tabs keep the plain "abilities" tip.</summary>
        public override string TipStation => tab == TalentsSub ? (Training.Step(S) ? Training.TipStation : null) : "abilities";

        /// <summary>GG-T180 (§25.0.9), GG-T1086: during step 7 the tab opens on the Board, on the hero the guided first skill
        /// is for — decided before the hero picker draws, so the picker marks that hero. GG-T1407: an open Board marks its
        /// hero's ready ability seen, so its "New ability ready" badge is already gone when the picker draws.</summary>
        public override void Prepare()
        {
            if (tab < 0)
            {
                var plan = Training.First(C, S);
                tab = plan != null ? TalentsSub : 0;
                if (plan != null) HeroId = plan.Hero.ClassId;
            }
            if (tab == TalentsSub && NextAbility.MarkSeen(C, Hero)) App.Persist();
        }

        public override void BuildBody(VisualElement body)
        {
            Prepare();
            if (tab == TalentsSub)
            {
                Ui.Tabs(SubTabs, tab, i => { tab = i; Refresh(); }, body).name = "abilities-tabs";
                var plan = Training.First(C, S);
                if (plan != null && plan.Hero == Hero) GuidedLesson(plan, body); // GG-T1086: above the hexagon
                talents.Host = Host;
                talents.BuildBody(body);
                return;
            }
            var h = Hero;
            int unlocked = Progression.UnlockedSlots(S);
            var barW = Ui.Panel(body, "Battle bar", false, "abilities-bar");
            var bar = Ui.El("row", barW);
            // Edge = row padding + cell margin = gap between cells = 24 (POL-C8 equal gutters; POL-C3 24 px between targets).
            bar.style.paddingLeft = bar.style.paddingRight = Space.TargetHalfGap;
            for (int i = 0; i < Progression.MaxBarSlots; i++)
            {
                int idx = i;
                var cell = Ui.El("col center", bar);
                // Every bar slot shares the row with even gutters, however many slots or however wide the stage (POL-C8).
                cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.minHeight = Size.Cell; // GG-T1145: min, not fixed: a long name wraps to 3 lines and grows the cell instead of spilling onto the Supporter line
                cell.style.marginLeft = cell.style.marginRight = Space.TargetHalfGap;
                cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 14;
                cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = i == slot ? 6 : 3;
                var bc = i == slot ? Palette.Gold : Palette.Text.WithA(0.7f);
                cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = bc;
                cell.style.backgroundColor = Palette.Ink.WithA(0.9f);
                if (i >= unlocked) { Ui.Caption("Locked", cell, "dim"); cell.style.opacity = 0.5f; continue; }
                var entry = h.Bar[i];
                var label = EntryName(entry);
                var icon = EntryIcon(entry);
                if (icon != null) { var ic = Ui.El("menu-icon", cell); ic.style.backgroundImage = new StyleBackground(icon); ic.style.marginRight = Space.S0; }
                else cell.KindIcon(RowKind.Ability).style.marginRight = Space.S0; // GG-T750 (§26.7): an empty slot shows the Ability emblem
                var t = Ui.Caption(label ?? "Empty", cell, label == null ? "dim" : null);
                t.style.unityTextAlign = TextAnchor.MiddleCenter; t.style.whiteSpace = WhiteSpace.Normal;
                t.style.maxWidth = Length.Percent(100); t.style.minWidth = 0; t.style.flexShrink = 1;
                cell.AddToClassList("card-tap");
                InspectCard.Attach(cell, b => InspectEntry(b, entry));
                Ui.Tappable(cell, () => { slot = idx; Refresh(); });
            }
            // GG-T298: what this hero does when it joins a pincer as a supporter.
            var support = GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, C.Class(h.ClassId).Support), "kit-text gold", barW);
            support.name = "abilities-support";
            Ui.Para("Pick a slot, then choose what goes in it — or move it along the bar.", barW, "dim");
            var tools = Ui.Actions(barW);
            var left = RowIcon.IconButton("Move", RowKind.Back, () => { var r = Abilities.Swap(S, h, slot, slot - 1); if (r.Ok) slot--; Done(r); }, "btn--small", tools);
            left.name = "bar-move-left";
            Ui.SetEnabled(left, slot > 0 && slot < unlocked);
            var right = RowIcon.IconButton("Move", RowKind.Enter, () => { var r = Abilities.Swap(S, h, slot, slot + 1); if (r.Ok) slot++; Done(r); }, "btn--small", tools);
            right.name = "bar-move-right";
            Ui.SetEnabled(right, slot + 1 < unlocked);

            Ui.Tabs(SubTabs, tab, i => { tab = i; Refresh(); }, body).name = "abilities-tabs";
            var list = Ui.ListPanel(body);
            if (tab == PassivesTab) { Passives(h, list); return; }
            if (tab == LockedTab) { Locked(h, list); return; }
            if (slot < unlocked && h.Bar[slot] != null) Ui.MenuRow("Clear slot", "", () => Done(Abilities.Assign(C, S, h, slot, null)), list).KindIcon(RowKind.Close);
            if (tab == 0)
            {
                int ownedOrbs = ManaOrbs.Owned(C, S); // GG-T677: a spell costing more orbs than you hold can't be cast yet
                // GG-T1043: the bar picks from the abilities the hero owns (known, or on its owned skill hexagon nodes) —
                // only the ones it can fire (skills and spells); passives work from the Passives tab, not a slot.
                foreach (var id in HeroBoard.OwnedAbilities(C, h))
                {
                    var a = C.TryAbility(id);
                    if (a == null || !Abilities.IsHeroUsable(a)) continue;
                    bool shortOrbs = a.Kind == AbilityKind.Spell && a.Cost > ownedOrbs;
                    string value = shortOrbs ? $"{KindLabel(a)} · needs {a.Cost} orbs, you have {ownedOrbs}" : KindLabel(a);
                    var row = Ui.MenuRow(a.Name, value, () => Done(Abilities.Assign(C, S, h, slot, a.Id)), list, Art.AbilityIcon(a), slot < unlocked);
                    if (shortOrbs) row.Q<Label>("value")?.AddToClassList("bad");
                    if (a.Cost > 0) Ui.CostPips(a.Cost, row, have: ownedOrbs);
                    InspectCard.Attach(row, b => InspectCard.Ability(b, a, wearer: C.TryClass(h.ClassId)));
                    Describe(a, list);
                }
            }
            else if (tab == 1)
            {
                var items = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null && (i.Type == ItemType.Consumable || i.Slot == EquipmentSlot.Weapon)).ToList();
                if (items.Count == 0) Ui.Empty("No battle items. Buy potions from the Merchant.", list);
                foreach (var it in items)
                {
                    var item = it;
                    bool weapon = item.Slot == EquipmentSlot.Weapon;
                    ItemUi.Row(item, weapon ? "swap in battle" : $"×{S.Count(item.Id)}", () => Done(Abilities.Assign(C, S, h, slot, (weapon ? "weapon:" : "item:") + item.Id)), list, slot < unlocked);
                }
            }
        }

        private const int PassivesTab = 2;
        private const int LockedTab = 3;

        /// <summary>A skill this hero lacks and where it comes from (GG-T1081: the talent-tree sources are gone).</summary>
        private sealed class LockedSkill
        {
            public AbilityDef Ability;
            public bool Kit;
            public List<string> Sources = new List<string>();
        }

        private List<LockedSkill> LockedSkills(HeroSave h)
        {
            var result = new List<LockedSkill>();
            var def = C.TryClass(h?.ClassId);
            if (def == null || h.Domesticated) return result;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string id, bool kit)
            {
                var a = C.TryAbility(id);
                if (a == null || h.Known.Contains(a.Id) || PassiveLoadout.IsInnate(C, h, a.Id) || !seen.Add(a.Id)) return;
                var sources = new List<string>();
                foreach (var tome in C.Items.Values.Where(i => i.Type == ItemType.Tome && string.Equals(i.TeachesAbility, a.Id, StringComparison.OrdinalIgnoreCase)))
                    sources.Add($"{tome.Name}: {string.Join(", ", TomeFoundAt(tome.Id).DefaultIfEmpty("not found anywhere yet"))}");
                var boardPt = SkillHexProgress.Board(C)?.FindAbility(a.Id);
                if (boardPt != null) sources.Add($"Skill board - {SkillHexProgress.Board(C).Price(boardPt.Value)} SP");
                if (sources.Count == 0) sources.Add("Not taught anywhere yet");
                result.Add(new LockedSkill { Ability = a, Kit = kit, Sources = sources });
            }
            foreach (var id in Progression.KitAbilities(C, def)) Add(id, true);
            foreach (var id in def.Abilities) Add(id, false);
            foreach (var item in C.Items.Values.Where(i => i.Type == ItemType.Tome && i.TeachesAbility != null).OrderBy(i => i.Id, StringComparer.Ordinal))
            {
                var a = C.TryAbility(item.TeachesAbility);
                if (a != null && RewardCatalog.AllowedFor(a, def)) Add(a.Id, false);
            }
            return result;
        }

        /// <summary>Where an item turns up: a quest or bounty reward, a theme's first clear, the Merchant, a foe's drops or a chest.</summary>
        private List<string> TomeFoundAt(string itemId)
        {
            var found = new List<string>();
            foreach (var q in C.QuestOrder) if (q.RewardItem == itemId) found.Add($"quest {q.Name}");
            foreach (var b in C.Bounties.Values.OrderBy(b => b.Id, StringComparer.Ordinal)) if (b.RewardItem == itemId) found.Add($"bounty {b.Name}");
            foreach (var t in C.Campaign.Themes) if (t.MilestoneReward == itemId) found.Add($"clearing {t.Name}");
            if (C.TryItem(itemId)?.SoldByVendor == true) found.Add("the Merchant");
            var tables = new HashSet<string>(C.DropTables.Values.Where(t => t.Entries.Any(e => e.Item == itemId)).Select(t => t.Id), StringComparer.OrdinalIgnoreCase);
            var foes = C.Classes.Values.Where(d => d.Team == Team.Enemy && d.DropTable != null && tables.Contains(d.DropTable))
                .Select(d => d.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (foes.Count > 0) found.Add("dropped by " + string.Join(", ", foes.Take(3)) + (foes.Count > 3 ? $" and {foes.Count - 3} more" : ""));
            foreach (var chest in C.Items.Values.Where(i => i.Type == ItemType.Chest && i.OpensTable != null && tables.Contains(i.OpensTable)).OrderBy(i => i.Id, StringComparer.Ordinal))
                found.Add($"a {chest.Name}");
            return found;
        }

        /// <summary>The Locked tab (GG-T388): every skill this hero doesn't know yet that belongs to it — its own kit moves,
        /// the rest of its class list, its home tree and the passives a Tome could teach — each with where it comes from
        /// (the trainer's price, the tree node and its points, the Tome and where that Tome is found).</summary>
        private void Locked(HeroSave h, VisualElement list)
        {
            var locked = LockedSkills(h);
            Ui.Para("A hero starts with two moves. The rest are bought from the trainer (Learn) or the skill board, or discovered as Tomes.", list, "dim");
            if (locked.Count == 0) Ui.Empty("Nothing left to learn.", list);
            foreach (var l in locked)
            {
                var ab = l.Ability;
                var row = Ui.MenuRow(ab.Name, l.Kit ? "Own kit" : Abilities.IsHeroUsable(ab) ? KindLabel(ab) : "Passive", null, list, Art.AbilityIcon(ab));
                row.name = "locked-" + ab.Id;
                if (l.Kit) row.Q<Label>("value")?.AddToClassList("gold");
                InspectCard.Attach(row, b => InspectCard.Ability(b, ab, wearer: C.TryClass(h.ClassId)), tap: true); // GG-T450: a read-only row reads on a tap
                foreach (var src in l.Sources) Ui.Note("· " + src, list, null);
                Describe(ab, list);
            }
        }

        /// <summary>The Passives tab (GG-T301, §25.6.4): learned passives (Tomes, skill-tree grants) with an On/Off switch,
        /// at most <see cref="PassiveLoadout.Slots"/> on; the class kit's innate ones, always on; and any Tome in the bag
        /// this hero could read.</summary>
        private void Passives(HeroSave h, VisualElement list)
        {
            var learned = PassiveLoadout.Learned(C, h);
            var on = PassiveLoadout.Active(C, h);
            int slots = PassiveLoadout.Slots(h), next = PassiveLoadout.NextSlotLevel(h);
            var head = Ui.Section($"Learned passives on: {on.Count}/{slots}" + (next > 0 ? $"   ·   another slot at Lv {next}" : ""), list);
            head.name = "passive-slots";
            Ui.Para("Tap one to switch it on or off. Tomes come from quests, bounties, chests, theme clears and rare drops.", list, "dim");
            if (learned.Count == 0) Ui.Empty("No learned passives yet.", list);
            foreach (var a in learned)
            {
                var ab = a;
                bool active = on.Contains(a.Id);
                var row = Ui.MenuRow(a.Name, active ? "On" : "Off", () => Done(PassiveLoadout.Toggle(C, h, ab.Id)), list, Art.AbilityIcon(a));
                row.name = "passive-" + a.Id;
                row.Q<Label>("value").AddToClassList(active ? "good" : "dim");
                InspectCard.Attach(row, b => InspectCard.Ability(b, ab, wearer: C.TryClass(h.ClassId)));
                Sentence(a, list);
            }
            var innate = PassiveLoadout.Innate(C, h);
            if (innate.Count > 0)
            {
                Ui.Section("Class passives — always on", list);
                foreach (var a in innate)
                {
                    var ab = a;
                    var row = Ui.MenuRow(a.Name, "Innate", null, list, Art.AbilityIcon(a));
                    InspectCard.Attach(row, b => InspectCard.Ability(b, ab, wearer: C.TryClass(h.ClassId)), tap: true); // GG-T450
                    Sentence(a, list);
                }
            }
            var tomes = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key))
                .Where(i => i != null && i.Type == ItemType.Tome && Tomes.WhyNot(C, S, i.Id, h.ClassId) == null).ToList();
            if (tomes.Count > 0) Ui.Section("Tomes in the bag", list);
            foreach (var t in tomes)
            {
                var tome = t;
                var row = ItemUi.Row(t, "Read", () =>
                {
                    var r = Tomes.Teach(C, S, tome.Id, h.ClassId);
                    App.Toast(r.Message, 2.4f);
                    App.Audio.Sfx(r.Ok ? "battle.powerup" : "ui.error");
                    if (r.Ok) App.Persist();
                    Refresh();
                }, list);
                row.name = "read-" + t.Id;
                TomeUi.Preview(list, t);
            }
        }

        private void Sentence(AbilityDef a, VisualElement list) => GlossaryText.Create(TriggerText.Describe(C, a), "kit-text dim", list).AddToClassList("kit-note"); // on the box, not each word

        /// <summary>The guided first skill (GG-T180, §25.0.9) heads the Board sub-tab (GG-T1086): what it does, that its board
        /// node is free (no skill points) and which bar slot it lands in, with a Learn button (the camp tip's target) that
        /// buys the node and slots the skill in one tap (<see cref="Training.Teach"/>).</summary>
        private void GuidedLesson(Training.Plan plan, VisualElement list)
        {
            var card = Ui.Window(null, list);
            card.name = "guided-lesson";
            var head = Ui.El("row", card);
            head.style.alignItems = Align.Center;
            var icon = Art.AbilityIcon(plan.Skill);
            if (icon != null) { var ic = Ui.El("menu-icon", head); ic.style.backgroundImage = new StyleBackground(icon); }
            Ui.Heading($"{plan.Skill.Name} — {KindLabel(plan.Skill)}", head, "grow");
            GlossaryText.Create(plan.Skill.Description ?? "", "kit-text dim", card);
            string hero = C.TryClass(plan.Hero.ClassId)?.Name ?? plan.Hero.ClassId;
            Ui.Para($"First lesson free — the {hero} takes this skill board node at no skill point cost, and it goes onto the battle bar" + (plan.Slot >= 0 ? $", slot {plan.Slot + 1}." : "."), card, "gold");
            var btn = RowIcon.IconButton("Learn", RowKind.Confirm, () => LearnDone(Training.Teach(C, S), plan.Hero, plan.Skill.Id), "btn--primary", Ui.Actions(card));
            btn.name = CampTips.CraftButton;
        }

        /// <summary>After learning, select the bar slot the skill went into (the guided first one is slotted automatically).</summary>
        private void LearnDone(Result r, HeroSave h, string abilityId)
        {
            if (r.Ok)
            {
                int at = h.Bar.IndexOf(abilityId);
                if (at >= 0) { slot = at; tab = 0; }
            }
            Done(r);
        }

        private void Describe(AbilityDef a, VisualElement list) => GlossaryText.Create(a.Description ?? "", "kit-text dim", list).AddToClassList("kit-note"); // on the box, not each word

        /// <summary>The hold-to-read card for a bar entry: an ability, a battle item or a swap weapon.</summary>
        private bool InspectEntry(VisualElement body, string entry)
        {
            if (entry == null) return false;
            if (entry.StartsWith("item:") || entry.StartsWith("weapon:"))
                return InspectCard.Item(body, C.TryItem(entry.Substring(entry.IndexOf(':') + 1)));
            return InspectCard.Ability(body, C.TryAbility(entry));
        }

        private static string KindLabel(AbilityDef a) => a.Kind == AbilityKind.Skill ? (a.CooldownTurns > 0 ? $"Skill · CD {a.CooldownTurns}" : "Skill") : a.CastTime > 0 ? FormattableString.Invariant($"{a.CastTime:0.#}s") : "Instant";

        private string EntryName(string entry)
        {
            if (entry == null) return null;
            if (entry.StartsWith("item:")) { var it = C.TryItem(entry.Substring(5)); return it == null ? null : $"{it.Name} ×{S.Count(it.Id)}"; }  // GG-T711: one line, shrunk to fit the bar cell by TextFit; it wraps (GG-T701) only below the type floor
            if (entry.StartsWith("weapon:")) { var w = C.TryItem(entry.Substring(7)); return w == null ? null : $"⭾ {w.Name}"; }
            return C.TryAbility(entry)?.Name;
        }

        private Texture2D EntryIcon(string entry)
        {
            if (entry == null) return null;
            if (entry.StartsWith("item:")) return Art.ItemIcon(entry.Substring(5));
            if (entry.StartsWith("weapon:")) return Art.ItemIcon(entry.Substring(7));
            return Art.AbilityIcon(C.TryAbility(entry));
        }
    }

    // ── Party ─────────────────────────────────────────────────────────────────────

    /// <summary>The Hero screen's Party tab (§25.5, GG-T435): every roster hero as a card; tap to swap in or out.</summary>
    internal sealed class PartyTab : HeroTab
    {
        public override string Id => "party";
        public override bool ShowsPicker => false;

        public override void BuildBody(VisualElement body)
        {
            PartyBoards.Build(C, S, body); // GG-T1044: each party hero's board and bar side by side
            BondsUi.CohesionGrid(C, S, body); // GG-T1299
            var list = Ui.ListPanel(body, $"Active party {S.Party.Count}/{Progression.MaxPartySize} — tap to swap", "party-list");
            foreach (var h in S.Roster.OrderByDescending(x => S.Party.Contains(x.ClassId)).ThenBy(x => x.ClassId))
            {
                var hero = h;
                var u = Progression.Preview(C, h);
                bool active = S.Party.Contains(h.ClassId);
                var card = Ui.Card(list, h.ClassId, null, out var col);
                card.EnableInClassList("card--selected", active);
                var nameRow = Ui.El("row", col);
                Ui.Name(C.Class(h.ClassId).Name, nameRow);
                Ui.RoleBadge(Ui.RoleOf(h, C.Class(h.ClassId)), nameRow);
                MercenaryCamp.Tag(S, h.ClassId, nameRow);
                Ui.Caption($"Lv {u.Level}   HP {u.MaxHp}   {(active ? "<color=#78EC96>Active</color>" : "Reserve")}", col);
                GlossaryText.Create(C.Class(h.ClassId).Card ?? "", "kit-text dim", col);
                card.AddToClassList("card-tap");
                Ui.Tappable(card, () => Done(Party.Toggle(S, hero.ClassId)), "ui.select");
            }
        }
    }

    // ── Merchant (buy / sell with carts, §25.1) ──────────────────────────────────

    public sealed class ShopScreen : HubPage
    {
        private int tab;
        private string bargainSelected;
        private int buyCategory = -1;
        private readonly Dictionary<string, int> buyCart = new Dictionary<string, int>();
        private readonly Dictionary<string, int> sellCart = new Dictionary<string, int>();
        private ScrollView cartScroll;
        private float keepScroll;
        public override string Id => "shop";
        protected override string Title => "Merchant";
        protected override string Npc => CampNpcs.Merchant;

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(new[] { "Buy", "Sell", "Bargain" }, tab, i => { tab = i; bargainSelected = null; Refresh(); }, body);
            if (tab == 2) { BuildBargain(body); return; }
            bool selling = tab == 1;
            // GG-T538 (§25.0.10): the shelf grows with progress; a fresh Merchant shows the first stock tier only.
            var items = selling ? Shop.Sellable(C, S) : Shop.Stock(C, S).Where(i => Features.Offers(i.Id)).ToList();
            if (!selling)
            {
                // GG-T1161 (§25.1): one category at a time (about 110 rows in one list was a wall); the first non-empty one opens.
                var cats = items.Select(Shop.SectionOf).Distinct().OrderBy(c => c).ToList();
                if (cats.Count > 0)
                {
                    if (!cats.Contains(buyCategory)) buyCategory = cats[0];
                    Ui.Tabs(cats.Select(c => Shop.Sections[c]).ToArray(), cats.IndexOf(buyCategory), i => { buyCategory = cats[i]; keepScroll = 0f; Refresh(); }, body);
                    items = items.Where(i => Shop.SectionOf(i) == buyCategory).ToList();
                }
            }
            var list = Ui.ListPanel(body, null, out cartScroll);
            // A −/+ tap rebuilds the page: keep the list where the player had scrolled it, not back at the top.
            float restore = keepScroll; keepScroll = 0f;
            var sv = cartScroll;
            if (restore > 0f) sv.RegisterCallback<GeometryChangedEvent>(_ => { if (restore > 0f) { sv.scrollOffset = new Vector2(0f, restore); restore = 0f; } });
            var cart = selling ? sellCart : buyCart;
            if (!selling && Shop.NextStock(C, S) is var next && next.tier > 0)
            {
                // The next stock tier, signposted at the head of the list: "More stock after The Lantern Road III · +14 items".
                string when = VendorTiers.OpensHint(C, next.tier);
                var more = Ui.MenuRow($"More stock {when}", $"+{next.count} items", () => { App.Toast($"The Merchant restocks {when}"); }, list, null, false);
                more.KindIcon(RowKind.Locked); // GG-T750 (§26.7): stock not open yet
                more.name = "shop-next";
            }
            if (items.Count == 0) Ui.Empty(selling ? "Nothing to sell." : "Sold out.", list);
            var detail = Ui.DetailPanel(body);
            ItemUi.Detail(detail, null);
            int section = -1;
            foreach (var it in items)
            {
                var item = it;
                if (!selling && Shop.SectionOf(item) != section)
                {
                    section = Shop.SectionOf(item);
                    Ui.Section(Shop.Sections[section], list).name = "shop-section-" + Shop.Sections[section].ToLowerInvariant();
                }
                cart.TryGetValue(item.Id, out var q);
                int price = selling ? Shop.SellPrice(item) : item.BaseCost;
                int max = selling ? S.Count(item.Id) : Shop.MaxBuy(S, item);
                // The value is the price, with "own 2" in small type under it once you have some (GG-T454). GG-T515: one
                // line "250 G · own 15" was wide enough, beside the −/0/+ counter, to squeeze names onto three hyphenated
                // lines ("Bram- blehide Grips") and clip its own count; stacked, the value column is only as wide as the price.
                int own = S.Count(item.Id);
                var row = ItemUi.Row(item, $"{price} G", () => ItemUi.Detail(detail, item, C), list);
                if (own > 0)
                {
                    var v = row.Q<Label>("value");
                    var stack = Ui.El("col", row);
                    stack.name = "shop-value";
                    stack.style.flexShrink = 0f;
                    stack.style.alignItems = Align.FlexEnd;
                    stack.style.justifyContent = Justify.Center;
                    row.Insert(row.IndexOf(v), stack);
                    stack.Add(v);
                    var ownLabel = Ui.Text($"own {own}", "small dim", stack);
                    ownLabel.name = "shop-own";
                    ownLabel.style.whiteSpace = WhiteSpace.NoWrap;
                }
                var minus = Ui.Button("−", () => { Step(cart, item.Id, -1, max); }, "btn--small btn--icon", row);
                var n = Ui.Name(q.ToString(), row);
                n.style.minWidth = Size.ColumnS; n.style.flexShrink = 0f; n.style.unityTextAlign = TextAnchor.MiddleCenter; // grows with Text size (GG-T495)
                var plus = Ui.Button("+", () => { Step(cart, item.Id, +1, max); }, "btn--small btn--icon", row);
                plus.name = "cart-plus-" + item.Id;
                Ui.SetEnabled(minus, q > 0);
                Ui.SetEnabled(plus, q < max);
                // GG-T520: under a gear row, which party heroes can equip it and whom it upgrades.
                if (!selling && Shop.GearFitLine(C, S, item, out bool upgrade) is string fit)
                    Ui.Note(fit, list, upgrade ? "good" : "dim").name = "shop-fit-" + item.Id;
            }
            var fr = Ui.Footer(body, "shop-footer");
            int total = Shop.CartTotal(C, cart, selling);
            Ui.Heading(selling ? $"Receive: {Ui.Gold(total)}" : $"Pay: {Ui.Gold(total)}", fr, "grow " + (!selling && total > S.Gold ? "bad" : "gold"));
            var commit = RowIcon.IconButton(selling ? "Sell" : "Buy", RowKind.Confirm, () =>
            {
                var r = selling ? Shop.Sell(C, S, cart) : Shop.Buy(C, S, cart);
                if (r.Ok) cart.Clear();
                Done(r);
            }, "btn--primary", fr);
            commit.name = "shop-commit";
            Ui.SetEnabled(commit, total > 0 && (selling || total <= S.Gold));
        }

        private void Step(Dictionary<string, int> cart, string id, int d, int max)
        {
            cart.TryGetValue(id, out var q);
            q = Mathf.Clamp(q + d, 0, max);
            if (q == 0) cart.Remove(id); else cart[id] = q;
            keepScroll = cartScroll != null ? cartScroll.scrollOffset.y : 0f;
            Refresh();
        }

        // ── "Used / Bargain" shelf: rotating worn weapons/armor at a discount (US-246) ──

        private void BuildBargain(VisualElement body)
        {
            var list = Ui.ListPanel(body);
            var detail = Ui.DetailPanel(body);
            var offers = Bargains.Shelf(C, S);
            if (offers.Count == 0) Ui.Empty("The shelf is bare. Check back after your next battle.", list);
            foreach (var o in offers)
            {
                var offer = o;
                var row = ItemUi.Row(offer.Item, $"{offer.Price} G", () => { bargainSelected = offer.Item.Id; Refresh(); }, list);
                if (bargainSelected == offer.Item.Id) Ui.Select(row);
            }
            var sel = offers.FirstOrDefault(o => o.Item.Id == bargainSelected);
            if (sel != null) ShowBargain(detail, sel);
            else Ui.Empty("Pick a piece to see how worn it is.", detail);
        }

        private void ShowBargain(VisualElement w, BargainOffer offer)
        {
            ItemUi.Detail(w, offer.Item, C);
            Ui.Caption($"Durability {offer.Durability}/{offer.Item.Durability}", w, "dim");
            Ui.Gauge((float)offer.Durability / offer.Item.Durability, w, 320);
            Ui.Para("Used gear: cheap now, but it can shatter mid-battle before you get a chance to repair it.", w, "warn");
            bool owned = S.Count(offer.Item.Id) >= offer.Item.MaxStack;
            var btn = RowIcon.IconButton(owned ? "Already owned" : $"Buy for {Ui.Gold(offer.Price)}", RowKind.Confirm, () =>
            {
                bargainSelected = null;
                Done(Bargains.Buy(C, S, offer.Item.Id));
            }, "btn--primary", Ui.Actions(w));
            Ui.SetEnabled(btn, !owned && S.Gold >= offer.Price);
        }
    }

    // ── Blacksmith: forge / upgrade / salvage / repair (§25.2) ───────────────────

    public sealed class BlacksmithScreen : HubPage
    {
        private const int RepairTab = 3;
        /// <summary>-1 until first built: then Repair if the workshop is still closed or his camp tip is due (GG-T175), else Forge.</summary>
        private int tab = -1;
        private RecipeDef selected;
        public override string Id => "blacksmith";
        protected override string Title => "Blacksmith";
        protected override string Backdrop => "cave";
        protected override string Subtitle => $"{Facilities.Current(C, S, CraftStation.Forge)} · {StationProgress.ShortText(S, StationKind.Blacksmith)}";
        protected override string Npc => CampNpcs.Blacksmith;
        /// <summary>The Forge tab has its own tip, the first forge (GG-T177), then the first armor once the weapon is made
        /// (GG-T178); Repair keeps the Blacksmith's.</summary>
        protected override string TipStation => tab != 0 ? Id : Forging.ArmorStep(S) ? Forging.ArmorTipStation : Forging.TipStation;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T175 (§25.0.4): he joins to mend worn gear, so Repair is his only open tab at first; Forge, Upgrade and
            // Salvage are dimmed and say when they arrive.
            bool workshop = CampUnlocks.SmithWorkshopOpen(S);
            if (tab < 0) tab = workshop && (CampTips.Pending(S, Id) == null || !Repair.Pieces(C, S).Any()) ? 0 : RepairTab;
            if (!workshop) tab = RepairTab;
            var bar = Ui.Tabs(new[] { "Forge", "Upgrade", "Salvage", "Repair" }, tab, i =>
            {
                if (!workshop && i != RepairTab) { App.Toast(CampUnlocks.SmithWorkshopHint(App.Content)); App.Audio.Sfx("ui.error"); return; }
                tab = i; selected = null; Refresh();
            }, body);
            if (!workshop)
                for (int i = 0; i < bar.childCount; i++)
                    if (i != RepairTab) { bar[i].style.opacity = 0.4f; bar[i].AddToClassList("tab--locked"); }
            var list = Ui.ListPanel(body);
            var detail = Ui.DetailPanel(body);
            // GG-T177 (§25.0.6): until the first forge is made, the Forge opens on the starter weapon its tip walks.
            // GG-T178 (§25.0.7): once it's made (the starter still selected), it moves on to the first armor piece.
            if (tab == 0 && (selected == null || (Forging.IsStarter(selected) && Forging.Done(S))))
                selected = Forging.First(C, S)?.Recipe ?? Forging.FirstArmor(C, S)?.Recipe ?? selected;
            if (tab <= 1) RecipeList(list, detail, C, S, CraftStation.Forge, tab == 1, selected, r => selected = r, this);
            else if (tab == 2) SalvageList(list, detail);
            else RepairList(list, detail);
        }

        /// <summary>The station's recipe rows over its detail pane. <paramref name="onPick"/> keeps the page's own selection in
        /// step with the row the player taps, so a Craft/Brew (which rebuilds the page) stays on that recipe — and the list
        /// scrolls back to it — instead of jumping to the first or guided one.</summary>
        internal static void RecipeList(VisualElement list, VisualElement detail, ContentDb c, SaveData s, CraftStation station, bool upgrades, RecipeDef selected, Action<RecipeDef> onPick, HubPage page)
        {
            var recipes = Crafting.Recipes(c, station).Where(r => (r.BaseItem != null) == upgrades).ToList();
            if (station == CraftStation.Enchant) recipes = Crafting.Recipes(c, station).ToList();
            // Recipe discovery (§24.6.2, GG-T36): undiscovered recipes are hidden, except a "???" tease
            // for ones the player is exactly one input away from.
            // Armor-set recipes open after the first forge (GG-T178, §25.0.7).
            // GG-T538 (§25.0.10): the Forge and Alchemy lists also grow with progress (VendorTiers.RecipeOpen).
            var visible = recipes.Where(r => Forging.ArmorOpen(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && (RecipeDiscovery.IsDiscovered(s, r) || RecipeDiscovery.OneAwayMissing(s, r) != null)).ToList();
            // GG-T432 (§25.12.2): the station's next facility level, bought right here (also a card on the Caravan screen).
            if (Facilities.Next(c, s, station) is CaravanLevel nextFacility)
            {
                var build = Ui.MenuRow($"Build {nextFacility.Name}", Ui.Gold(nextFacility.Cost), () => ConfirmFacility(c, s, station, page), list);
                build.name = "facility-build";
                build.KindIcon(RowKind.Station); // GG-T746 (§26.7)
            }
            // GG-T538: the station's next rank, in forges or brews ("Journeyman at 150 XP: 40/150, about 8 more brews").
            if (VendorTiers.Gated(recipes.FirstOrDefault()) && VendorTiers.NextRankLine(s, Crafting.StationFor(station)) is string rankLine)
                Ui.Note(rankLine, list).name = "rank-next";
            if (visible.Count == 0) Ui.Empty("No recipes.", list);
            RecipeDef sel = selected;
            // GG-T198: two discovered recipes can share a Result (the old Slime Gel brew and the new Empty Bottle
            // brew, GG-T176) and otherwise look identical in the list apart from price. When that happens, name
            // each row's key inputs so a player can tell them apart without opening both.
            var resultCounts = visible.Where(x => RecipeDiscovery.IsDiscovered(s, x)).GroupBy(x => x.Result)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var r in visible)
            {
                var rec = r;
                VisualElement row;
                if (RecipeDiscovery.IsDiscovered(s, r))
                {
                    var result = c.Item(r.Result);
                    bool tierLocked = !Crafting.IsUnlocked(s, r), facilityLocked = !Facilities.Allows(c, s, r);
                    bool locked = tierLocked || facilityLocked;
                    bool can = !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold;
                    string nameOverride = resultCounts[r.Result] > 1 ? $"{result.Name} — {InputSummary(c, r)}" : null;
                    string lockLabel = tierLocked ? $"🔒 {StationProgress.TierName(r.Tier)}" : $"🔒 {Facilities.NeededName(c, r)}";
                    row = ItemUi.Row(result, locked ? lockLabel : $"{r.Gold} G", () => { onPick?.Invoke(rec); ShowRecipe(detail, c, s, rec, page); }, list, true, false, nameOverride);
                    row.name = "recipe-" + r.Id;
                    if (!can) row.style.opacity = 0.55f;
                }
                else
                {
                    var missing = RecipeDiscovery.OneAwayMissing(s, r);
                    var missingItem = c.TryItem(missing.Item);
                    row = Ui.MenuRow("???", $"Missing {missingItem?.Name ?? missing.Item}", () => { onPick?.Invoke(rec); ShowRecipe(detail, c, s, rec, page); }, list);
                    row.KindIcon(RowKind.Item); // GG-T717: an undiscovered result shows the Item emblem
                    row.style.opacity = 0.75f;
                }
                if (sel == r)
                {
                    Ui.Select(row);
                    var picked = row;
                    var scroll = list.GetFirstAncestorOfType<ScrollView>();
                    bool scrolled = false;
                    if (scroll != null)
                        scroll.RegisterCallback<GeometryChangedEvent>(_ => { if (!scrolled && picked.layout.height > 0) { scrolled = true; scroll.ScrollTo(picked); } });
                }
            }
            if (VendorTiers.Gated(recipes.FirstOrDefault())) ComingLater(list, detail, c, s, station, recipes, visible);
            if (sel != null && visible.Contains(sel)) ShowRecipe(detail, c, s, sel, page);
            else if (sel != null && !VendorTiers.RecipeOpen(c, s, sel)) ShowLocked(detail, c, s, sel, LockReason(c, s, sel));
            else if (sel != null) ShowRecipe(detail, c, s, sel, page);
            else Ui.Empty("Pick a recipe to see what it needs.", detail);
        }

        /// <summary>GG-T538 (§25.0.10): the next tier, shown locked at the foot of the list. Up to
        /// <see cref="VendorTiers.PreviewRows"/> recipes from the next rank ("🔒 Journeyman") and from the next progress tier
        /// ("🔒 The Lantern Road III"), with a count of the rest, so the player sees what is coming and what opens it.</summary>
        private static void ComingLater(VisualElement list, VisualElement detail, ContentDb c, SaveData s, CraftStation station, List<RecipeDef> recipes, List<RecipeDef> visible)
        {
            var kind = Crafting.StationFor(station);
            int rank = StationProgress.Level(s, kind), tier = VendorTiers.ProgressTier(c, s);
            var shown = new HashSet<RecipeDef>(visible);
            // Next rank: open on the road, hidden today only because the station's rank is short.
            var byRank = recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && r.Tier == rank + 1).ToList();
            // Next progress tier: the first tier above this save's with anything for this list.
            int next = recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && !VendorTiers.RecipeOpen(c, s, r))
                .Select(r => VendorTiers.RecipeTier(c, r)).Where(t => t > tier).DefaultIfEmpty(0).Min();
            var byRoad = next == 0 ? new List<RecipeDef>() : recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && !VendorTiers.RecipeOpen(c, s, r) && VendorTiers.RecipeTier(c, r) == next)
                .OrderBy(r => r.Tier).ThenBy(r => r.Gold).ToList();
            if (byRank.Count == 0 && byRoad.Count == 0) return;
            Ui.Section("Coming later", list).name = "recipes-later";
            void Rows(List<RecipeDef> set, string value, string more)
            {
                foreach (var r in set.Take(VendorTiers.PreviewRows))
                {
                    var rec = r;
                    var row = ItemUi.Row(c.Item(r.Result), value, () => ShowLocked(detail, c, s, rec, LockReason(c, s, rec)), list, true, false);
                    row.name = "recipe-locked-" + r.Id;
                    row.style.opacity = 0.55f;
                }
                if (set.Count > VendorTiers.PreviewRows) Ui.Note($"+{set.Count - VendorTiers.PreviewRows} more {more}", list);
            }
            if (byRank.Count > 0) Rows(byRank, $"🔒 {StationProgress.TierName(rank + 1)}", $"at {StationProgress.TierName(rank + 1)}");
            if (byRoad.Count > 0)
            {
                string after = VendorTiers.OpensHint(c, next);
                Rows(byRoad, "🔒 " + after.Substring("after ".Length), after);
            }
        }

        /// <summary>Why a previewed recipe is locked: "Opens after The Lantern Road III", "Needs a Journeyman blacksmith (40/150 XP)", or both.</summary>
        private static string LockReason(ContentDb c, SaveData s, RecipeDef r)
        {
            var kind = Crafting.StationFor(r.Station);
            var parts = new List<string>();
            if (!VendorTiers.RecipeOpen(c, s, r)) parts.Add("Opens " + VendorTiers.OpensHint(c, VendorTiers.RecipeTier(c, r)));
            if (!Crafting.IsUnlocked(s, r))
                parts.Add($"Needs a {StationProgress.TierName(r.Tier)} {StationProgress.Id(kind)} (you are {StationProgress.LevelName(s, kind)}, {StationProgress.Xp(s, kind)} XP)");
            return string.Join(". ", parts);
        }

        /// <summary>A previewed recipe's detail: what it makes, why it is locked, and what goes in (GG-T538).</summary>
        private static void ShowLocked(VisualElement w, ContentDb c, SaveData s, RecipeDef r, string reason)
        {
            w.Clear();
            ItemUi.Detail(w, c.Item(r.Result), c);
            Ui.Para("🔒 " + reason, w, "bad");
            Ui.Section("Needs", w);
            if (r.BaseItem != null) Ui.Text($"{c.TryItem(r.BaseItem)?.Name ?? r.BaseItem} ×1", "kit-text dim", w);
            foreach (var ic in r.Inputs) Ui.Text($"{c.TryItem(ic.Item)?.Name ?? ic.Item} ×{ic.Count}", "kit-text dim", w);
            Ui.Para(Ui.Gold(r.Gold), w, "dim");
        }

        /// <summary>"Slime Gel" or "Empty Bottle + Wolf Pelt + Tattered Cloth" — the recipe's own inputs, joined,
        /// used to tell apart two recipes that share a Result (GG-T198).</summary>
        private static string InputSummary(ContentDb c, RecipeDef r) =>
            string.Join(" + ", r.Inputs.Select(ic => c.TryItem(ic.Item)?.Name ?? ic.Item));

        /// <summary>Asks, then builds the station's next facility level (GG-T432): a coin cue, then the forge's clang or
        /// the still's glug as the new equipment goes in.</summary>
        private static void ConfirmFacility(ContentDb c, SaveData s, CraftStation station, HubPage page)
        {
            var next = Facilities.Next(c, s, station);
            if (next == null) return;
            if (s.Gold < next.Cost) { GameApp.I.Toast($"{next.Name} costs {Ui.Gold(next.Cost)}"); GameApp.I.Audio.Sfx("ui.error"); return; }
            Confirm.Show($"Build the {next.Name} for {Ui.Gold(next.Cost)}? {next.Text}.", "Build", () =>
            {
                var res = Facilities.Build(c, s, station);
                GameApp.I.Toast(res.Message);
                GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                if (res.Ok)
                {
                    GameApp.I.Audio.Sfx(station == CraftStation.Forge ? "battle.clang" : "battle.glug");
                    GameApp.I.Persist();
                }
                page.Refresh();
            });
        }

        internal static void ShowRecipe(VisualElement w, ContentDb c, SaveData s, RecipeDef r, HubPage page)
        {
            w.Clear();
            if (!RecipeDiscovery.IsDiscovered(s, r))
            {
                Ui.Heading("??? — Undiscovered Recipe", w, "dim");
                Ui.Para("You sense you're close. Find the missing material to learn how this is made.", w, "dim");
                var undiscoveredNeed = w;
                if (r.BaseItem != null)
                {
                    var b = c.TryItem(r.BaseItem);
                    int have = s.Count(r.BaseItem);
                    Ui.Text($"{(have >= 1 ? "✔" : "✘")} {b?.Name ?? r.BaseItem} ×1   (have {have})", "kit-text " + (have >= 1 ? "good" : "bad"), undiscoveredNeed);
                }
                foreach (var ic in r.Inputs)
                {
                    var it = c.TryItem(ic.Item);
                    int have = s.Count(ic.Item);
                    Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), undiscoveredNeed);
                }
                return;
            }
            var result = c.Item(r.Result);
            ItemUi.Detail(w, result, c);
            bool locked = !Crafting.CanMake(c, s, r);
            if (!Crafting.IsUnlocked(s, r))
            {
                var kind = Crafting.StationFor(r.Station);
                Ui.Para($"🔒 Requires {StationProgress.TierName(r.Tier)} ({StationProgress.Id(kind)} is {StationProgress.LevelName(s, kind)})", w, "bad");
            }
            if (!Facilities.Allows(c, s, r))
            {
                // GG-T432: shown locked, never hidden — the facility that works this material is named, with its price.
                Ui.Para($"🔒 Needs {Facilities.NeededName(c, r)} (you have the {Facilities.Current(c, s, r.Station)})", w, "bad");
                if (Facilities.Next(c, s, r.Station) is CaravanLevel next)
                {
                    var b = RowIcon.IconButton($"Build {next.Name} · {Ui.Gold(next.Cost)}", RowKind.Confirm, () => ConfirmFacility(c, s, r.Station, page), "btn--small", Ui.Actions(w));
                    b.name = "facility-build-detail";
                    Ui.SetEnabled(b, s.Gold >= next.Cost);
                }
            }
            Ui.Section("Needs", w);
            var need = w;
            if (r.BaseItem != null)
            {
                var b = c.TryItem(r.BaseItem);
                int have = s.Count(r.BaseItem);
                Ui.Text($"{(have >= 1 ? "✔" : "✘")} {b?.Name ?? r.BaseItem} ×1   (have {have})", "kit-text " + (have >= 1 ? "good" : "bad"), need);
            }
            foreach (var ic in r.Inputs)
            {
                var it = c.TryItem(ic.Item);
                int have = s.Count(ic.Item);
                Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), need);
            }
            Ui.Para($"{(s.Gold >= r.Gold ? "✔" : "✘")} {Ui.Gold(r.Gold)}", need, s.Gold >= r.Gold ? "good" : "bad");
            if (r.Station == CraftStation.Forge)
                Ui.Para($"★ {OddsText.Percent(Crafting.MasterworkChance(c, s))} chance of a Masterwork result", need, "gold");
            if (Forging.IsGuided(s, r) && Forging.SuitedHero(c, s, result) is HeroSave suited)
                Ui.Para($"First forge: free, and the {c.TryClass(suited.ClassId)?.Name ?? suited.ClassId} equips it", need, "gold");
            if (Forging.IsGuidedArmor(c, s, r) && Forging.SuitedWearer(c, s, result) is HeroSave wearer)
                Ui.Para($"First armor: the {c.TryClass(wearer.ClassId)?.Name ?? wearer.ClassId} wears it", need, "gold");
            string verb = r.Station == CraftStation.Alchemy ? "Brew" : r.Station == CraftStation.Enchant ? "Enchant" : "Craft";
            var btn = RowIcon.IconButton(verb, RowKind.Confirm, () =>
            {
                // The guided first forge (GG-T177) and first armor (GG-T178) also equip what they make on the hero it suits.
                var res = Forging.Forge(c, s, r);
                GameApp.I.Toast(res.Message);
                GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                if (res.Ok) GameApp.I.Persist();
                page.Refresh();
            }, "btn--primary", Ui.Actions(w));
            btn.name = "craft";
            Ui.SetEnabled(btn, !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold);
            LuckyCraftUi.Section(w, c, s, r, !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold, page.Refresh);

            if (r.PotencyResult != null)
            {
                var greater = c.Item(r.PotencyResult);
                Ui.Section($"With a rarer reagent: {greater.Name}", w);
                var pneed = w;
                foreach (var ic in r.PotencyInputs)
                {
                    var it = c.TryItem(ic.Item);
                    int have = s.Count(ic.Item);
                    Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), pneed);
                }
                var pbtn = RowIcon.IconButton($"{verb} {greater.Name}", RowKind.Confirm, () =>
                {
                    var res = Crafting.Craft(c, s, r, potency: true);
                    GameApp.I.Toast(res.Message);
                    GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                    if (res.Ok) GameApp.I.Persist();
                    page.Refresh();
                }, null, Ui.Actions(w));
                Ui.SetEnabled(pbtn, !locked && Crafting.HasPotencyInputs(s, r) && s.Gold >= r.Gold);
            }
        }

        private void SalvageList(VisualElement list, VisualElement detail)
        {
            var gear = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null && i.Type == ItemType.Equipment).ToList();
            var broken = S.Broken.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null).ToList();
            if (gear.Count == 0 && broken.Count == 0) Ui.Empty("No spare gear in your bag.", list);
            Ui.Para("Break spare gear into materials — worn gear yields less; the worst copy goes first. Broken gear can only be salvaged.", detail, "dim");
            foreach (var it in gear)
            {
                var item = it;
                var yield = Crafting.SalvagePreview(C, S, item.Id);
                string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
                ItemUi.Row(item, $"×{S.Count(item.Id)}", () => Confirm.Show($"Salvage {item.Name} into {y}?", "Salvage", () => Done(Crafting.Salvage(C, S, item.Id))), list, yield.Count > 0, Equipment.IsMasterwork(S, item.Id));
            }
            foreach (var it in broken)
            {
                var item = it;
                var yield = Crafting.SalvageBrokenPreview(C, S, item.Id);
                string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
                var row = Ui.MenuRow($"Broken {item.Name}", $"×{S.BrokenCount(item.Id)}",
                    () => Confirm.Show($"Salvage Broken {item.Name} into {y}?", "Salvage", () => Done(Crafting.SalvageBroken(C, S, item.Id))), list, null, yield.Count > 0);
                row.KindIcon(RowKind.Item, Art.ItemIcon(item.Id)); // GG-T717
                row.Q<Label>("label").AddToClassList("dim");
            }
        }

        /// <summary>The Repair tab's Repair all button (GG-T544). Not "repair-…": the Blacksmith's tip rings the first
        /// control whose name starts with that.</summary>
        public const string RepairAllButton = "smith-repair-all";

        /// <summary>How much wear the Repair tab's price hint quotes (GG-T544): "3 G per 10 wear, 5 G after this repair".</summary>
        private const int HintWear = 10;

        private void RepairList(VisualElement list, VisualElement detail)
        {
            bool salvage = CampUnlocks.SmithWorkshopOpen(S); // "Salvage instead" waits for the Salvage tab (GG-T175)
            int threshold = (int)Math.Round(CampRoutine.RepairBelowFraction * 100);
            // GG-T544: one threshold for the camp routine and the player. Each repair compounds the piece's next price, so
            // the tab says so, Repair all mends only pieces worn below the threshold, and the rest say "Cheaper to wait".
            Ui.Para($"Each repair lowers the piece's max durability by 1 and makes its next repair cost 1.6× as much, so mend gear once it's below {threshold}%, not after every scratch."
                    + (salvage ? " An uneconomical repair offers salvage instead." : ""), detail, "dim");
            var due = Repair.DuePieces(C, S);
            int dueCost = due.Sum(p => Repair.Cost(C, S, p));
            var all = RowIcon.IconButton(due.Count == 0 ? "Nothing needs repair yet" : $"Repair all ({due.Count}) · {Ui.Gold(dueCost)}", RowKind.Confirm,
                () => Done(Repair.RepairAll(C, S), cue: "camp.repair"), "btn--primary", Ui.Actions(detail));
            all.name = RepairAllButton;
            Ui.SetEnabled(all, due.Any(p => Repair.Cost(C, S, p) <= S.Gold));
            bool any = false;
            foreach (var p in Repair.Pieces(C, S))
            {
                any = true;
                var piece = p;
                int cost = Repair.Cost(C, S, p);
                bool bad = cost > 0 && Repair.IsUneconomical(p.Item, p.Durability, p.Repairs, p.Mastery);
                string name = (p.Mastery > 1f ? "★ " : "") + p.Item.Name;
                var row = Ui.MenuRow($"{C.Class(p.Hero.ClassId).Name} — {name}  {p.Durability}/{p.Max}", cost > 0 ? Ui.Gold(cost) : "OK",
                    () => Done(piece.Slot == EquipmentSlot.Weapon ? Repair.RepairWeapon(C, S, piece.Hero) : Repair.RepairArmor(C, S, piece.Hero, piece.Slot), cue: "camp.repair"),
                    list, null, cost > 0);
                row.name = p.RowName;
                row.KindIcon(RowKind.Item, Art.ItemIcon(p.Item.Id)); // GG-T746 (§26.7): the worn piece's icon, else the Item emblem
                if (bad) row.Q<Label>("value").AddToClassList("warn");
                if (cost > 0) RepairPriceNote(list, p);
                if (bad && salvage) SalvageInsteadRow(list, p.Hero, p.Slot, p.Item, p.Durability, cost);
            }
            if (!any) Ui.Empty("Nobody has gear that wears out. Equip forged weapons first.", list);
        }

        /// <summary>GG-T544: the line under a worn piece's repair row — "Cheaper to wait" while it's above the threshold, then
        /// how the price grows: what 10 points of wear cost now, and after this repair (1.6x per repair, BIBLE §24.5).</summary>
        private void RepairPriceNote(VisualElement list, Repair.WornPiece p)
        {
            int now = Repair.WearCost(C, S, p.Item, HintWear, p.Repairs, p.Mastery);
            int next = Repair.WearCost(C, S, p.Item, HintWear, p.Repairs + 1, p.Mastery);
            bool wait = !Repair.Due(p.Item, p.Durability, p.Repairs, p.Mastery);
            var note = Ui.Note((wait ? $"Cheaper to wait ({Mathf.RoundToInt(p.Fraction * 100)}%) · " : "")
                               + $"{Ui.Gold(now)} per {HintWear} wear, {Ui.Gold(next)} after this repair", list);
            note.name = $"wear-note-{p.Hero.ClassId}-{p.Slot}"; // not "repair-…" (the tip rings that prefix)
        }

        /// <summary>The "repair is a ripoff — salvage it instead" offer, shown right under an uneconomical
        /// repair row so both outcomes (repair cost vs. salvage yield) sit side by side (GG-T42).</summary>
        private void SalvageInsteadRow(VisualElement list, HeroSave hero, EquipmentSlot slot, ItemDef item, int durability, int repairCost)
        {
            var yield = Crafting.SalvageYield(C, item, durability);
            string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
            var row = Ui.MenuRow("Salvage instead", y, () =>
                Confirm.Show($"Repair costs {Ui.Gold(repairCost)}. Scrap the {item.Name} instead for {y}? This destroys it.", "Salvage",
                    () => Done(Crafting.SalvageEquipped(C, S, hero, slot))), list);
            row.KindIcon(RowKind.Item, Art.ItemIcon(item.Id)); // GG-T746 (§26.7): the piece it scraps
            row.AddToClassList("kit-note"); // a sub-row of the repair above, indented under its label
            row.Q<Label>("value").AddToClassList("dim");
        }
    }

    // ── Alchemist: brew (§25.3) ───────────────────────────────────────────────────
    // GG-T119: enchanting moved to the Enchantress's own station, and resting is the Innkeeper's (the old Heal tab
    // was the Inn under another name, which would have let the Alchemist open it before the Innkeeper joined).

    public sealed class AlchemistScreen : HubPage
    {
        private RecipeDef selected;
        public override string Id => "alchemist";
        protected override string Title => "Alchemist";
        protected override string Backdrop => "swamp";
        protected override string Subtitle => $"{Facilities.Current(C, S, CraftStation.Alchemy)} · {StationProgress.HeaderText(S, StationKind.Alchemist)}";
        protected override string Npc => CampNpcs.Alchemist;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T176 (§25.0.5): on the first visit his camp tip walks the first brew, so that bottle recipe opens selected.
            if (selected == null && CampTips.Pending(S, Id) != null) selected = Brewing.FirstBrew(C, S);
            StationBody(body, CraftStation.Alchemy, false, selected, r => selected = r, this);
        }

        /// <summary>A recipe list over a detail pane — the Alchemist's Brew and the Enchantress's Enchant share it.</summary>
        internal static void StationBody(VisualElement body, CraftStation station, bool upgrades, RecipeDef selected, Action<RecipeDef> onPick, HubPage page)
        {
            var list = Ui.ListPanel(body);
            var detail = Ui.DetailPanel(body);
            BlacksmithScreen.RecipeList(list, detail, GameApp.I.Content, GameApp.I.Save, station, upgrades, selected, onPick, page);
        }
    }

    // ── Enchantress: enchant (§25.3.4, GG-T119) ──────────────────────────────────

    /// <summary>The Enchantress's station: the Enchant recipes that used to be the Alchemist's second tab. Every
    /// enchant is Master tier and she has no ladder of her own to climb, so her recipes still unlock with the
    /// Alchemist's station level (her reagents are his brews); her header says how far off that is.</summary>
    public sealed class EnchantressScreen : HubPage
    {
        private RecipeDef selected;
        /// <summary>0 = Armor (GG-T179: bind a passive into a worn piece), 1 = Weapons (the elemental weapon enchants).</summary>
        private int tab;
        private ArmorEnchanting.Target piece;
        private ArmorEnchantDef enchant;
        public override string Id => "enchantress";
        protected override string Title => "Enchantress";
        protected override string Backdrop => "swamp";
        protected override string Subtitle => "Unlocks with the Alchemist: " + StationProgress.HeaderText(S, StationKind.Alchemist);
        protected override string Npc => CampNpcs.Enchantress;

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(new[] { "Armor", "Weapons" }, tab, i => { tab = i; Refresh(); }, body);
            if (tab == 1) { AlchemistScreen.StationBody(body, CraftStation.Enchant, true, selected, r => selected = r, this); return; }
            ArmorBody(body);
        }

        /// <summary>Armor enchants (GG-T179, §25.0.8): pick a worn piece, then an enchant; Enchant binds its passive into
        /// the piece. Until the first enchant is done, both start on what her camp tip walks (<see cref="ArmorEnchanting.First"/>).</summary>
        private void ArmorBody(VisualElement body)
        {
            var pieces = ArmorEnchanting.Pieces(C, S);
            var plan = ArmorEnchanting.First(C, S);
            if (plan != null && piece == null && enchant == null) { piece = plan.Target; enchant = plan.Enchant; }
            piece = piece != null ? pieces.FirstOrDefault(p => p.Hero == piece.Hero && p.Slot == piece.Slot) : null;
            piece ??= pieces.FirstOrDefault();

            // GG-T454: worn armor and enchants are two kit list panels sharing the height, over the detail pane; with no
            // armor worn, the first is just its empty line and the enchants take the room.
            var plist = pieces.Count > 0 ? Ui.ListPanel(body, "Worn armor", "enchant-pieces") : Ui.Panel(body, "Worn armor", false, "enchant-pieces");
            if (pieces.Count == 0)
                Ui.Empty("Nobody wears armor yet. Forge a piece at the Blacksmith, then bring it here.", plist);
            foreach (var t in pieces)
            {
                var target = t;
                string hero = C.TryClass(t.Hero.ClassId)?.Name ?? t.Hero.ClassId;
                var bound = ArmorEnchanting.PassiveOf(C, t.Piece.Enchant);
                var row = ItemUi.Row(t.Item, bound != null ? $"{hero} ✦ {bound.Name}" : hero, () => { piece = target; Refresh(); }, plist, true);
                row.name = t.RowName;
                if (piece != null && piece.Hero == t.Hero && piece.Slot == t.Slot) Ui.Select(row);
            }

            var elist = Ui.ListPanel(body, "Enchants", "enchant-list");
            foreach (var e in ArmorEnchanting.All(C))
            {
                var ench = e;
                bool locked = !ArmorEnchanting.IsUnlocked(S, e);
                bool can = !locked && ArmorEnchanting.HasInputs(S, e) && ArmorEnchanting.CanAfford(C, S, e);
                var passive = C.TryAbility(e.Passive);
                var row = Ui.MenuRow(e.Name, locked ? $"🔒 {StationProgress.TierName(e.Tier)}" : Ui.Gold(ArmorEnchanting.Gold(C, S, e)),
                    () => { enchant = ench; Refresh(); }, elist);
                row.KindIcon(RowKind.Ability, passive != null ? Art.AbilityIcon(passive) : null); // GG-T746 (§26.7), else the Ability emblem
                row.name = ArmorEnchanting.EnchantRowPrefix + e.Id;
                if (!can) row.style.opacity = 0.55f;
                if (enchant == e) Ui.Select(row);
            }

            var detail = Ui.DetailPanel(body);
            if (enchant == null) { Ui.Empty("Pick an enchant to see what it binds.", detail); return; }
            var p = C.TryAbility(enchant.Passive);
            Ui.Heading($"{enchant.Name} — {p?.Name ?? enchant.Passive}", detail);
            GlossaryText.Create(p?.Description ?? "", "kit-text dim", detail);
            Ui.Section("Needs", detail);
            foreach (var ic in enchant.Inputs)
            {
                int have = S.Count(ic.Item);
                Ui.Para($"{(have >= ic.Count ? "✔" : "✘")} {C.TryItem(ic.Item)?.Name ?? ic.Item} ×{ic.Count}   (have {have})", detail, have >= ic.Count ? "good" : "bad");
            }
            int gold = ArmorEnchanting.Gold(C, S, enchant);
            Ui.Para($"{(S.Gold >= gold ? "✔" : "✘")} {Ui.Gold(gold)}" + (gold == 0 && enchant.Gold > 0 ? "  (first rune free)" : gold > 0 && gold < enchant.Gold ? "  (Enchanter's Wagon)" : ""), detail, S.Gold >= gold ? "good" : "bad");
            bool unlocked = ArmorEnchanting.IsUnlocked(S, enchant);
            if (!unlocked)
                Ui.Para($"🔒 Requires {StationProgress.TierName(enchant.Tier)} (alchemist is {StationProgress.LevelName(S, StationKind.Alchemist)})", detail, "bad");
            if (piece != null)
            {
                string hero = C.TryClass(piece.Hero.ClassId)?.Name ?? piece.Hero.ClassId;
                var old = C.TryArmorEnchant(piece.Piece.Enchant);
                Ui.Para($"Into the {hero}'s {piece.Item.Name}" + (old != null && old != enchant ? $" (replaces {old.Name})" : ""), detail, "gold");
            }
            var btn = RowIcon.IconButton("Enchant", RowKind.Confirm, () => Done(ArmorEnchanting.Enchant(C, S, piece?.Hero, piece?.Slot ?? EquipmentSlot.Head, enchant)), "btn--primary", Ui.Actions(detail));
            btn.name = CampTips.CraftButton;
            Ui.SetEnabled(btn, piece != null && unlocked && ArmorEnchanting.HasInputs(S, enchant) && ArmorEnchanting.CanAfford(C, S, enchant) && piece.Piece.Enchant != enchant.Id);
        }
    }

    // ── Caravan upgrades (§25.12) ────────────────────────────────────────────────

    public sealed class CaravanScreen : HubPage
    {
        public override string Id => "caravan";
        protected override string Title => "Caravan";
        protected override string Backdrop => "desert";

        protected override void BuildBody(VisualElement body)
        {
            var head = Ui.Panel(body);
            Ui.Para("The caravan travels with the party — its wagons are the shops between battles. Upgrade it for the long road.", head);
            BarkLine(C, S, head);
            var list = Ui.ListPanel(body);
            foreach (var def in C.Caravan.Values.Where(x => Features.OffersCaravan(x.Id)))
            {
                var d = def;
                int lv = CaravanUpgrades.Level(S, d.Id);
                var next = CaravanUpgrades.NextLevel(C, S, d.Id);
                var card = Ui.Card(list, null, null, out var col, "caravan-" + d.Id);
                // GG-T746 (§26.7): the upgrade's pixel icon, else the Station emblem, leads the card.
                var ic = card.KindIcon(RowKind.Station, Art.ItemIcon(d.Icon));
                ic.style.width = ic.style.height = Kit.Thumb;
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var top = Ui.El("row", col);
                Ui.Name(d.Name, top, "grow");
                Ui.Caption($"Lv {lv}/{d.Levels.Count}", top, "gold");
                if (!string.IsNullOrEmpty(d.Crew)) Ui.Caption("Crew: " + CaravanUpgrades.CrewName(d), col, "gold");
                var crewBlurb = ContentDb.Blurb(C.CrewBlurbs, d.Id); // GG-T374: Prose "character:<crew>"
                if (!string.IsNullOrEmpty(crewBlurb)) Ui.Para(crewBlurb, col, "dim");
                Ui.Para(d.Description, col, "dim");
                if (lv > 0) Ui.Para("Now: " + d.Levels[lv - 1].Text, col, "good");
                if (next != null) Ui.Para("Next: " + next.Text, col);
                var b = RowIcon.IconButton(next == null ? "Maxed" : Ui.Gold(next.Cost), RowKind.Confirm, () => Confirm.Show($"Buy {d.Name} ({next.Text}) for {Ui.Gold(next.Cost)}?", "Buy", () => Done(CaravanUpgrades.Buy(C, S, d.Id), cue: "camp.caravan")), "btn--small", Ui.Actions(col));
                b.name = "caravan-buy-" + d.Id;
                Ui.SetEnabled(b, next != null && S.Gold >= next.Cost);
            }
        }

        private static int barkTurn = Environment.TickCount & 0xFFFF;

        /// <summary>One rotating crew bark (GG-T75, §25.12.1): a quoted line and who said it — flavor only, nothing to
        /// tap or answer. Each call advances the rotation, so every rebuild of Camp or the Caravan shows the next line.</summary>
        public static VisualElement BarkLine(ContentDb c, SaveData s, VisualElement parent)
        {
            var (crew, line) = CaravanUpgrades.Bark(c, s, barkTurn++);
            if (line == null) return null;
            var box = Ui.El("col", parent);
            box.name = "caravan-bark";
            Ui.Para($"\"{line}\"", box, "gold");
            Ui.Caption("— " + CaravanUpgrades.CrewName(crew), box, "dim");
            return box;
        }
    }

    // ── Summon Circle (§25.10) ───────────────────────────────────────────────────

    public sealed class SummonScreen : HubPage
    {
        public override string Id => "summon";
        protected override string Title => "Summon";
        protected override string Backdrop => "cityruins";

        /// <summary>The open tab: one per open banner (<see cref="Gacha.Open"/>), then Recruit.</summary>
        private int tab;

        /// <summary>The open Rates list's height in design px: about ten rows, the rest scrolls.</summary>
        private const float OddsListMaxHeight = 560;

        protected override void BuildBody(VisualElement body)
        {
            bool busy = Paywall.Pending(S, PaywallKind.Summon) != null;
            int starter = SummonTickets.Starter(S); // GG-T1604: the first visit's Tickets
            if (starter > 0) { App.Persist(); App.Toast($"The circle gives you {TicketText.Of(starter)} to begin with."); }
            var banners = Gacha.Open(C, DateTime.UtcNow);
            var names = banners.Select(TabName).Append("Recruit").ToArray();
            if (tab >= names.Length) tab = 0;
            Ui.Tabs(names, tab, i => { tab = i; Refresh(); }, body).name = "summon-tabs";
            if (tab < banners.Count) Banner(body, banners[tab], busy);
            else Recruit(body, busy);
        }

        private string TabName(BannerDef b) => b.Kind == BannerKind.Standard
            ? "Standard"
            : string.Join(" & ", b.Featured.Select(id => C.TryClass(id)?.Name ?? id));

        /// <summary>"3d 4h", "5h 12m", "9m 30s": the time left on a Featured banner.</summary>
        internal static string Left(TimeSpan t) =>
            t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h"
            : t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m"
            : $"{Math.Max(0, t.Minutes)}m {Math.Max(0, t.Seconds)}s";

        /// <summary>A banner's tab (§25.10.3, GG-T1428): the featured portraits and the countdown to its close, the pity
        /// meters, the Rates list, the wallet, and the x1 and x<see cref="BannerDef.MultiCount"/> pulls naming what they take.</summary>
        private void Banner(VisualElement body, BannerDef b, bool busy)
        {
            var w = Ui.Panel(body, b.Name, false, "summon-banner-" + b.Id);
            var head = Ui.El("row", w);
            foreach (var id in b.Featured.Where(id => C.TryClass(id) != null)) Ui.Thumb(id, head, Kit.Thumb).name = "summon-featured-" + id;
            var col = Ui.El("col grow", head);
            int pool = Gacha.Pool(C, S, b).Count;
            Ui.Para(b.Kind == BannerKind.Featured
                ? $"{TabName(b)} answers {OddsText.Percent(b.FeaturedShare)} of the pulls here; the rest are any of {pool} heroes."
                : $"Any of {pool} heroes may answer, each as likely as the next. A hero you already have comes as an {Gacha.EchoName}.", col, "dim");
            var closes = Gacha.Closes(C, b, DateTime.UtcNow);
            var ends = Ui.Caption(closes is DateTime at ? "Ends in " + Left(at - DateTime.UtcNow) : "Always open", col, "gold");
            ends.name = "summon-ends";
            if (closes is DateTime close)
                ends.schedule.Execute(() =>
                {
                    var left = close - DateTime.UtcNow;
                    if (left <= TimeSpan.Zero) { Refresh(); return; }
                    ends.text = "Ends in " + Left(left);
                }).Every(1000);
            Pity(w, b);
            RatesRow(w, b);

            int diamonds = DiamondExchange.Have(S), tickets = SummonTickets.Have(S);
            var wallet = Ui.Actions(w);
            var img = wallet.KindIcon(RowKind.Diamond, Art.ItemIcon(Crafting.DiamondId)); // GG-T745 (§26.7)
            img.style.width = img.style.height = Size.IconS;
            img.style.marginLeft = Space.TargetHalfGap;
            img.style.marginRight = 0;
            Ui.Name($"{ShardText.ShortPlural}: {Ui.Count(diamonds)} · {TicketText.Plural}: {Ui.Count(tickets)}", wallet, "grow").style.marginLeft = Space.S8;
            var row = Ui.Actions(w);
            PullButton(row, b, 1, busy, "btn--small btn--primary").name = "summon-draw";
            PullButton(row, b, b.MultiCount, busy, "btn--small").name = "summon-draw-multi";
            if (Gacha.Price(S, b, 1).shards > 0 && diamonds < b.PullShards)
            {
                Ui.Para($"Short of {ShardText.ShortPlural}, the rest is paid in gold at the exchange rate.", w, "dim");
                RowIcon.IconButton(ShardText.Store, RowKind.Diamond, DiamondStores.Open, "btn--small", Ui.Actions(w)).name = "summon-diamond-store"; // GG-T750
            }
        }

        /// <summary>The pity meters: pulls toward a guaranteed new hero and, on a Featured banner, a guaranteed featured one.</summary>
        private void Pity(VisualElement w, BannerDef b)
        {
            var box = Ui.El("col", w);
            box.name = "summon-pity";
            if (b.NewHeroPity > 0) Meter(box, "New hero", Gacha.NewHeroMiss(S, b.Kind), b.NewHeroPity, "summon-pity-new");
            if (b.Kind == BannerKind.Featured && b.FeaturedPity > 0) Meter(box, "Featured", S.Summon?.FeaturedMiss ?? 0, b.FeaturedPity, "summon-pity-featured");
            var due = Gacha.DueLine(Gacha.Due(S, b, Gacha.Pool(C, S, b)));
            if (due != null) Ui.Para(due, box, "good");
        }

        private static void Meter(VisualElement parent, string label, int miss, int pity, string name)
        {
            var row = Ui.El("row", parent);
            row.name = name;
            Ui.Caption(label, row);
            var g = Ui.Gauge(miss / (float)pity, row);
            g.style.flexGrow = 1;
            g.style.marginLeft = g.style.marginRight = Space.S8;
            int left = Math.Max(1, pity - miss);
            Ui.Caption(left == 1 ? "Next pull" : $"Within {left} pulls", row, "dim");
        }

        /// <summary>What a pull takes, as its button names it: "1 Summon Ticket", "1,000 Shards", "40 Shards + 360 G".</summary>
        private string PayText(int tickets, int shards)
        {
            var parts = new List<string>();
            if (tickets > 0) parts.Add(TicketText.Of(tickets));
            if (shards > 0)
            {
                var (qd, qg) = DiamondExchange.Quote(S, shards);
                if (qd > 0) parts.Add(ShardText.ShortOf(qd));
                if (qg > 0) parts.Add(Ui.Gold(qg));
            }
            return string.Join(" + ", parts);
        }

        private Button PullButton(VisualElement row, BannerDef b, int count, bool busy, string classes)
        {
            var (tickets, shards) = Gacha.Price(S, b, count);
            string pay = PayText(tickets, shards);
            var btn = RowIcon.IconButton($"x{count} · {pay}", RowKind.Confirm, () =>
            {
                var can = Gacha.CanPull(C, S, b, count);
                if (!can.Ok)
                {
                    if (shards > 0 && !DiamondExchange.CanPay(S, shards).Ok) DiamondStores.OfferStore(can.Message);
                    else { App.Toast(can.Message); App.Audio.Sfx("ui.error"); }
                    return;
                }
                Confirm.Show($"Pay {pay} for {(count == 1 ? "a pull" : $"{count} pulls")} on {b.Name}? {RatesLine(b, Gacha.Odds(C, S, b))}.", "Summon", () => Pull(b, count));
            }, classes, row);
            Ui.SetEnabled(btn, !busy && !S.TestMode && Gacha.Pool(C, S, b).Count > 0);
            return btn;
        }

        /// <summary>Pays and starts the pull, then reveals every result in pull order (<see cref="SummonReveal"/>) and
        /// offers the new heroes' looks.</summary>
        private void Pull(BannerDef b, int count)
        {
            List<PullRecord> pulls = null;
            TimedJobView.Begin(now =>
            {
                var r = Gacha.Pull(C, S, b, count, now, out var job);
                if (r.Ok) pulls = S.Summon.History.Take(count).Reverse().ToList();
                return (r, job);
            }, count == 1 ? "The circle reaches out…" : $"The circle reaches out {count} times…", r =>
            {
                if (!r.Ok || pulls == null) { Refresh(); return; }
                SummonReveal.Show(pulls, b, () => VariantPicker.ShowPending(Refresh)); // GG-T532: then pick their look
            });
        }

        /// <summary>The next pull's odds in a line: the pity due, the featured share, or the even chance of each hero.</summary>
        private string RatesLine(BannerDef b, List<DrawOdds> odds)
        {
            var due = Gacha.DueLine(Gacha.Due(S, b, Gacha.Pool(C, S, b)));
            if (due != null) return due.TrimEnd('.');
            if (odds.Count == 0) return "No one is left to answer";
            if (b.Kind == BannerKind.Featured && b.Featured.Count > 0)
                return $"Featured: {OddsText.Percent(b.FeaturedShare)} together; each other hero {odds[odds.Count - 1].Label}";
            return $"Each of these {odds.Count} heroes: {odds[0].Label}";
        }

        /// <summary>The banner's odds, disclosed before purchase (GG-T503): one line (<c>summon-odds</c>) and a <b>Rates</b>
        /// button (<c>summon-odds-btn</c>) that opens the full list (<c>summon-odds-list</c>, a row <c>summon-odds-{classId}</c>
        /// per hero with its chance), all read from <see cref="Gacha.Odds"/>, the list the pull rolls from.</summary>
        private void RatesRow(VisualElement w, BannerDef b)
        {
            var odds = Gacha.Odds(C, S, b);
            var row = Ui.Actions(w);
            var line = Ui.Name(RatesLine(b, odds), row, "gold grow");
            line.name = "summon-odds";
            line.style.whiteSpace = WhiteSpace.Normal;
            // A bounded scroller: every row laid out in full pushed the pull buttons off a page that doesn't scroll (GG-T494).
            var list = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            list.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            list.style.maxHeight = OddsListMaxHeight;
            w.Add(list);
            list.name = "summon-odds-list";
            list.style.display = DisplayStyle.None;
            foreach (var o in odds)
            {
                var def = C.TryClass(o.ClassId);
                var r = Ui.El("row", list);
                r.name = $"summon-odds-{o.ClassId}";
                bool featured = b.Featured.Contains(o.ClassId, StringComparer.OrdinalIgnoreCase);
                Ui.Caption((def != null ? $"{def.Name} · {def.Role}" : o.ClassId) + (featured ? " · Featured" : ""), r, featured ? "gold grow" : "grow");
                Ui.Caption(o.Label, r, "dim");
            }
            Ui.Caption($"A hero you already have comes as an {Gacha.EchoName} (+{Gacha.EchoSp} SP).", list, "dim");
            Button toggle = null;
            toggle = RowIcon.IconButton("Rates", RowKind.Menu, () =>
            {
                bool open = list.style.display == DisplayStyle.None;
                list.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                toggle.Q<Label>().text = open ? "Hide rates" : "Rates"; // GG-T750: the IconButton's label, not Button.text
            }, "btn--small", row);
            toggle.name = "summon-odds-btn";
        }

        /// <summary>The Recruit tab: the gold chosen summon, any hero from the pool by name.</summary>
        private void Recruit(VisualElement body, bool busy)
        {
            int price = Summon.Price(C, S);
            var tag = Paywall.Tag(PaywallKind.Summon, S);
            var list = Ui.ListPanel(body, "Heroes who answer", "summon-pool");
            Ui.Para($"Summon a hero to your side ({tag}). The circle takes a moment to answer.", list, "dim");
            foreach (var d in Summon.Pool(C, S).Where(d => Features.Offers(d.Id)))
            {
                var def = d;
                bool owned = S.Hero(d.Id) != null;
                Ui.Card(list, d.Id, d.Name, out var col);
                GlossaryText.Create(d.Card ?? d.Description ?? "", "kit-text dim", col);
                var b = RowIcon.IconButton(owned ? "Joined" : "Recruit", RowKind.Confirm, () => Confirm.Show($"Summon {def.Name} for {Ui.Gold(price)}?", "Summon", () =>
                    TimedJobView.Begin(now => (Summon.BeginRecruit(C, S, def.Id, now, out var job), job), $"Summoning {def.Name}…", _ => VariantPicker.ShowPending(Refresh))), "btn--small", Ui.Actions(col)); // GG-T532: then pick their look
                Ui.SetEnabled(b, !owned && !busy && S.Gold >= price);
            }
        }
    }

    /// <summary>A banner pull's reveal (GG-T343, GG-T1429): each result in pull order plays over time — the portrait swells
    /// out of the light, a <b>New</b> or <b>Echo</b> badge lands with its own cue, and a featured hero gets a gold flash
    /// and a bigger fanfare. A tap finishes the playing result, the next tap moves on. <b>Skip</b> (multi-pulls) and the
    /// end of a multi-pull open a summary grid of every result; Continue closes it. Under Reduce Motion each result
    /// fades in at full size with no swell, pop or flash. A new hero whose pull fizzled (refunded) isn't shown.</summary>
    internal static class SummonReveal
    {
        private const float PortraitSize = 420;

        public static void Show(IReadOnlyList<PullRecord> pulls, BannerDef banner, Action onClose)
        {
            var app = GameApp.I;
            var shown = pulls.Where(p => app.Content.TryClass(p.ClassId) != null && (p.Result != PullResult.New || app.Save.Hero(p.ClassId) != null)).ToList();
            if (shown.Count == 0) { onClose?.Invoke(); return; }
            bool calm = app.Settings.ReduceMotion;
            var dim = Palette.Ink.WithA(0.9f);
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = "summon-reveal";
            veil.pickingMode = PickingMode.Position;
            // The reveal's name and role are bare text, not a window: the plain veil left the Summon Circle's own text
            // showing through them. Dim the page almost out so the hero carries the moment.
            veil.style.backgroundColor = dim;
            var stage = Ui.El("col center", veil);
            stage.name = "summon-reveal-stage";
            int at = -1;
            bool ready = false, summary = false;
            Coroutine playing = null;
            Action settle = null;
            Button skip = null;
            if (shown.Count > 1)
            {
                skip = Ui.Button("Skip", () => Summary(), "btn--small", veil);
                skip.name = "summon-reveal-skip";
                skip.style.position = Position.Absolute;
                skip.style.top = skip.style.right = Space.S24;
                skip.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            }

            veil.RegisterCallback<ClickEvent>(_ =>
            {
                if (summary) return;
                if (!ready) { Stop(); settle?.Invoke(); ready = true; return; }
                Next();
            });
            Next();

            void Stop()
            {
                if (playing != null) app.StopCoroutine(playing);
                playing = null;
            }

            void Close()
            {
                Stop();
                veil.RemoveFromHierarchy();
                onClose?.Invoke();
            }

            void Next()
            {
                if (++at >= shown.Count) { if (shown.Count > 1) Summary(); else Close(); return; }
                ready = false;
                stage.Clear();
                veil.style.backgroundColor = dim;
                var p = shown[at];
                var def = app.Content.TryClass(p.ClassId);
                bool featured = IsFeatured(banner, p);
                var thumb = Ui.Thumb(def.Id, stage, PortraitSize);
                thumb.name = "summon-reveal-hero";
                var badge = Ui.Text(Badge(p, featured), "h2 shadow " + (p.Result == PullResult.New ? "gold" : "good"), stage);
                badge.name = "summon-reveal-badge";
                badge.style.marginTop = Space.S16;
                var name = Ui.Text(def.Name, "title shadow" + (featured ? " gold" : ""), stage);
                name.style.marginTop = Space.S8;
                var role = Ui.Text(Line(def, p), "h2 shadow", stage);
                var tap = Ui.Text(shown.Count > 1 ? $"{at + 1} of {shown.Count} · Tap to continue" : "Tap to continue", "small dim", stage);
                tap.name = "summon-reveal-tap";
                tap.style.marginTop = Space.S48;
                settle = () =>
                {
                    thumb.style.scale = new Scale(Vector2.one);
                    thumb.style.opacity = badge.style.opacity = name.style.opacity = role.style.opacity = tap.style.opacity = 1f;
                    badge.style.scale = name.style.scale = new Scale(Vector2.one);
                    veil.style.backgroundColor = dim;
                };
                thumb.style.scale = new Scale(calm ? Vector2.one : Vector2.zero);
                thumb.style.opacity = calm ? 0f : 1f;
                badge.style.opacity = name.style.opacity = role.style.opacity = tap.style.opacity = 0f;
                playing = app.StartCoroutine(Play(p, featured, thumb, badge, name, role, tap));
            }

            System.Collections.IEnumerator Play(PullRecord p, bool featured, VisualElement thumb, Label badge, Label name, Label role, Label tap)
            {
                bool isNew = p.Result == PullResult.New;
                app.Audio.Sfx(isNew ? "camp.summon" : "ui.select", isNew ? 0.9f : 0.7f);
                if (calm) yield return Tween.Run(Timing.Quick, k => thumb.style.opacity = k);
                else
                {
                    float peak = featured ? 1.2f : 1.12f;
                    yield return Tween.Run(featured ? Timing.Emphasis : Timing.Standard,
                        k => thumb.style.scale = new Scale(Vector2.one * (k < 0.7f ? k / 0.7f * peak : peak - (peak - 1f) * (k - 0.7f) / 0.3f)), Tween.EaseOut);
                }
                thumb.style.scale = new Scale(Vector2.one);
                if (featured)
                {
                    app.Audio.Sfx("battle.victory");
                    app.Audio.Sfx("battle.warcry", 0.6f);
                    if (!calm) yield return Tween.Run(Timing.Dramatic, k => veil.style.backgroundColor = Color.Lerp(Palette.Gold.WithA(0.55f), dim, k), Tween.EaseOut);
                }
                else app.Audio.Sfx(isNew ? "battle.powerup" : "ui.diamond", isNew ? 0.7f : 0.6f);
                yield return Tween.Run(Timing.Quick, k => badge.style.opacity = name.style.opacity = role.style.opacity = k);
                if (!calm) yield return Tween.Pop(isNew ? name : badge, featured ? 1.3f : 1.15f, featured ? Timing.Standard : 0.2f);
                tap.style.opacity = 1f;
                playing = null;
                ready = true;
            }

            void Summary()
            {
                if (summary) return;
                summary = true;
                Stop();
                stage.Clear();
                veil.style.backgroundColor = dim;
                skip?.RemoveFromHierarchy();
                stage.name = "summon-reveal-summary";
                Ui.Text("The circle answered", "h2 shadow gold", stage).style.marginBottom = Space.S16;
                var grid = Ui.El("row", stage);
                grid.style.flexWrap = Wrap.Wrap;
                grid.style.justifyContent = Justify.Center;
                for (int i = 0; i < shown.Count; i++)
                {
                    var p = shown[i];
                    bool featured = IsFeatured(banner, p);
                    var cell = Ui.El("col center", grid);
                    cell.name = "summon-summary-" + i;
                    cell.style.marginLeft = cell.style.marginRight = cell.style.marginBottom = Space.S8;
                    var t = Ui.Thumb(p.ClassId, cell, Kit.Thumb);
                    if (featured)
                    {
                        t.style.borderTopWidth = t.style.borderBottomWidth = t.style.borderLeftWidth = t.style.borderRightWidth = 3;
                        t.style.borderTopColor = t.style.borderBottomColor = t.style.borderLeftColor = t.style.borderRightColor = Palette.Gold;
                    }
                    Ui.Caption(app.Content.TryClass(p.ClassId)?.Name ?? p.ClassId, cell, featured ? "gold" : null);
                    Ui.Caption(Badge(p, featured), cell, p.Result == PullResult.New ? "gold" : "dim");
                }
                var done = Ui.Button("Continue", Close, "btn--primary", Ui.Actions(stage));
                done.name = "summon-reveal-done";
                done.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                if (!calm) app.StartCoroutine(Tween.Fade(grid, 0f, 1f, Timing.Standard));
            }
        }

        private static bool IsFeatured(BannerDef banner, PullRecord p) =>
            banner?.Kind == BannerKind.Featured && banner.Featured.Contains(p.ClassId, StringComparer.OrdinalIgnoreCase);

        /// <summary>"New", "New · Featured", "Echo +1 SP", "+5 Ticket fragments".</summary>
        private static string Badge(PullRecord p, bool featured) => p.Result switch
        {
            PullResult.New => featured ? "New · Featured" : "New",
            PullResult.Echo => $"{Gacha.EchoName} +{Gacha.EchoSp} SP",
            _ => $"+{p.Fragments} {TicketText.Fragments}",
        };

        private static string Line(ClassDef def, PullRecord p) =>
            p.Result == PullResult.New ? $"{def.Role} · answers the circle" : $"{def.Role} · answers again";
    }

    // ── The Crucible (§22.10) ────────────────────────────────────────────────────

    public sealed class CrucibleScreen : HubPage
    {
        public override string Id => "crucible";
        protected override string Title => "Crucible";
        protected override string Backdrop => HubScreen.ThemeFor(C, S);

        /// <summary>The Camp row's value: the save's best run in <paramref name="mode"/> (the mode the next run plays,
        /// GG-T945: records are kept per turn mode), or an invitation before the first one.</summary>
        public static string RecordText(SaveData s, TurnMode mode) => CampMenu.CrucibleRecord(s, mode);

        protected override void BuildBody(VisualElement body)
        {
            // GG-T454 (the menu kit): the rules and the record in one scrolling list, the team under it, and the primary action
            // in the footer at the bottom of the screen, where the thumb is (POL-C4); nothing can push Enter off a phone (GG-T439).
            var list = Ui.ListPanel(body, "The Crucible", "crucible-intro");
            Ui.Para("A horde that never ends: wave after wave, each one harder, until your team falls or you leave. Wounds, mana and items carry from wave to wave, with no camp between. " +
                    "Every wave you clear banks gold, and every coin, drop and point of experience earned is <b>kept</b>, even in defeat. The campaign doesn't move.", list);
            int start = Crucible.PartyLevel(S);
            Ui.Field("First wave", $"Lv {start}, +{Crucible.LevelStep} each wave", list);
            Ui.Field("Horde", $"{Crucible.FirstWaveEnemies}→{Crucible.MaxWaveEnemies} foes, elites from wave {BattleConfig.CrucibleFirstEliteWave + 1}", list);
            Ui.Field("Pay", $"{Ui.Gold(Crucible.WaveGold(0))}, +{BattleConfig.CrucibleWaveGoldPerWave} G a wave", list, "gold");
            var fams = Crucible.Families(C, S);
            Ui.Field("Foes", $"{fams.Count} famil{(fams.Count == 1 ? "y" : "ies")} met: " + string.Join(", ", fams.Select(f => f[0].Name)), list);
            var rec = Ui.El("col", list);
            rec.name = "crucible-record";
            Ui.Section("Record", rec);
            bool ran = S.Crucible.Runs > 0;
            var mode = TurnModeSetting.Resolve(S, App.Settings); // GG-T945: the record of the mode the next run plays
            Ui.Field("Best", ran ? $"{S.Crucible.BestFor(mode)} waves · {Ui.Gold(S.Crucible.BestGoldFor(mode))}" : "None yet", rec, "gold");
            Ui.Field("Runs", ran ? $"{S.Crucible.Runs} · {S.Crucible.TotalWaves} waves" : "0", rec);

            // GG-T430: you pick one team before the run (the active party, up to the party-size cap) and it fights
            // every wave; "Change team" swaps it on the Party page and comes straight back here.
            var team = Ui.Panel(body, $"Your team {S.Party.Count}/{Progression.MaxPartySize}", false, "crucible-team");
            // GG-T658: the faces share the row evenly (each cell grows from the avatar's width), with the edge equal to
            // the gap (row padding + cell margins = 24 each), instead of piling up at the left edge (POL-C8).
            var faces = Ui.El("row", team);
            faces.style.flexWrap = Wrap.Wrap;
            faces.style.paddingLeft = faces.style.paddingRight = Space.TargetHalfGap;
            foreach (var id in S.Party)
            {
                var h = S.Hero(id);
                if (h == null) continue;
                var cell = Ui.El("col", faces);
                cell.style.alignItems = Align.Center;
                cell.style.flexGrow = 1; cell.style.flexShrink = 0; cell.style.flexBasis = Kit.Avatar;
                cell.style.marginLeft = cell.style.marginRight = Space.TargetHalfGap;
                Ui.Thumb(id, cell, Kit.Avatar).style.marginRight = Space.S0;
                Ui.Caption($"Lv {Progression.Level(h)}", cell);
            }
            RowIcon.IconButton("Change team", RowKind.Hero, () => App.Router.Push(new PartyScreen()), "btn--small", Ui.Actions(team)).name = "crucible-team-change"; // GG-T750

            bool can = Crucible.CanLaunch(C, S, out var why);
            var foot = Ui.Footer(body, "crucible-footer");
            var costCol = Ui.El("col grow", foot);
            if (!can) Ui.Para(why, costCol, "bad");
            Ui.Caption($"Costs {RestedPool.LaunchCost} Rested", costCol, "dim");
            var enter = RowIcon.IconButton("Enter the Crucible", RowKind.Enter, () => CrucibleLauncher.Launch(false), "btn--primary", foot);
            enter.name = "crucible-enter";
            Ui.SetEnabled(enter, can);
        }
    }

    // ── Bounty board (§22.4, GG-T431) ──────────────────────────────────────────────

    /// <summary>Hunts for marked targets: collect what was slain (with a claim cue), track what is hunted, and take a fresh
    /// poster. Each poster shows the target's portrait under the bounty skull, its title, its kind, the clue naming its
    /// stage, and the reward.</summary>
    public sealed class BountyScreen : HubPage
    {
        public override string Id => "bounties";
        protected override string Title => "Bounties";
        /// <summary>The Tome a just-claimed bounty paid (GG-T301), offered for teaching at the top of the board.</summary>
        private string claimedTome;

        protected override void BuildBody(VisualElement body)
        {
            if (C.TryItem(claimedTome) is ItemDef paid && S.Count(paid.Id) > 0)
            {
                var tw = Ui.Panel(body, $"Reward: {paid.Name}", false, "bounty-tome");
                TomeUi.Offer(tw, paid, r => { if (r.Ok) claimedTome = null; });
            }
            var list = Ui.ListPanel(body, "Wanted", "bounty-board");
            Ui.Para("Hunt a marked foe: play the stage its clue names, kill the one wearing the skull, then collect here.", list, "dim");
            var board = Bounties.Board(C, S);
            if (board.Count == 0) Ui.Empty("No one is posted yet. Clear more stages.", list);
            foreach (var b in board) Poster(b, list);
        }

        private void Poster(BountyDef b, VisualElement list)
        {
            var bounty = b;
            var target = C.TryClass(b.TargetClass);
            var reward = C.TryItem(b.RewardItem);
            bool slain = Bounties.IsSlain(S, b.Id), hunting = Bounties.IsTaken(S, b.Id);
            var card = Ui.Card(list, null, b.Name, out var col, "poster-" + b.Id);
            var face = Ui.El(null, null);
            card.Insert(0, face);
            face.style.position = Position.Relative;
            if (target != null) Ui.Thumb(target.Id, face, Kit.Thumb);
            var skull = BountySkull.Make(Size.IconS, face);
            skull.style.position = Position.Absolute;
            skull.style.right = Space.S16; skull.style.top = -Space.S4;
            Ui.Caption($"{target?.Name ?? b.TargetClass} · tougher variant", col, "dim");
            Ui.Para(Bounties.Clue(C, b), col, "gold");
            int sp = Bounties.SkillPointsPaid(b);
            Ui.Para($"Reward {Ui.Gold(Bounties.RewardGold(C, b))}{(reward != null ? " + " + reward.Name : "")}{(sp > 0 ? $" + {sp} SP each" : "")}", col, "dim");
            TomeUi.Preview(col, reward); // GG-T301: what a Tome reward teaches
            var acts = Ui.Actions(col);
            if (slain)
            {
                var claim = RowIcon.IconButton("Claim", RowKind.Confirm, () => // GG-T750 (§26.7)
                {
                    claimedTome = reward?.Type == ItemType.Tome ? reward.Id : null;
                    var r = Bounties.Claim(C, S, bounty.Id);
                    Done(r);
                    if (r.Ok) App.Audio.Sfx("battle.pickup"); // the claim cue: the purse fills
                }, "btn--primary btn--small", acts);
                claim.name = "bounty-claim-" + b.Id;
            }
            else if (hunting)
            {
                var stop = RowIcon.IconButton("Hunting", RowKind.Close, () => Confirm.Show($"Stop hunting {bounty.Name}?", "Stop", () => Done(Bounties.Abandon(C, S, bounty.Id))), "btn--small", acts);
                stop.name = "bounty-abandon-" + b.Id;
            }
            else
            {
                var take = RowIcon.IconButton("Take", RowKind.Confirm, () => Done(Bounties.Take(C, S, bounty.Id)), "btn--small", acts);
                take.name = "bounty-" + b.Id;
            }
        }
    }

    // ── Bestiary ─────────────────────────────────────────────────────────────────

    public sealed class BestiaryScreen : HubPage
    {
        private string selected;
        public override string Id => "bestiary";
        protected override string Title => "Bestiary";

        /// <summary>The creature the detail shows (GG-T925: the Codex opens a linked foe here).</summary>
        internal string Selected { get => selected; set => selected = value; }

        protected override void BuildBody(VisualElement body) => BuildGrid(body);

        /// <summary>The grid and detail into <paramref name="body"/>: this screen's own body, and the Codex's Foes tab
        /// (GG-T925), which embeds it rather than copying it. <paramref name="onPick"/> hears each creature opened;
        /// <paramref name="isNew"/> puts the New badge on a seen creature's card until it is opened.</summary>
        internal void BuildGrid(VisualElement body, Action<string> onPick = null, Func<string, bool> isNew = null)
        {
            var enemies = C.Classes.Values.Where(c => c.Team == Team.Enemy).OrderBy(c => c.Name).ToList();
            int seen = enemies.Count(e => S.Bestiary.TryGetValue(e.Id, out var b) && b.Seen);
            var grid = Ui.ListPanel(body, $"Seen {seen}/{enemies.Count}", out var scroll, "bestiary-grid");
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            var detail = Ui.DetailPanel(body);
            foreach (var e in enemies)
            {
                var def = e;
                S.Bestiary.TryGetValue(e.Id, out var entry);
                var t = Ui.Thumb(e.Id, grid, 160, thumbOnly: true); // GG-T1210: 100+ cards, baked thumbs only
                t.name = "beast-" + e.Id;
                if (entry == null || !entry.Seen) { t.Q(Ui.ThumbArt).style.unityBackgroundImageTintColor = new Color(0, 0, 0, 0.92f); }
                else if (isNew != null && isNew(e.Id)) RowIcon.Badge(t, RowKind.New);
                t.AddToClassList("card-tap");
                Ui.Tappable(t, () => { selected = def.Id; Show(detail, def, entry); RowIcon.ClearBadge(t); onPick?.Invoke(def.Id); });
            }
            grid.style.flexDirection = FlexDirection.Column;
            grid.style.flexWrap = Wrap.NoWrap;
            var cells = grid.Children().ToList();
            int laidCols = -1;
            // Listen on the viewport, not the ScrollView: the vertical scroller appears only after the cells fill it,
            // shrinking the viewport without resizing the ScrollView, so a ScrollView listener never re-lays the grid.
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                // The content viewport (inside the ScrollView's border, padding and scroller) is what the rows really get.
                laidCols = EvenGrid(grid, cells, scroll.contentViewport.layout.width, 160f, Space.S24, laidCols);
            });
            if (selected != null && C.TryClass(selected) is ClassDef sel) { Show(detail, sel, S.Bestiary.TryGetValue(selected, out var se) ? se : null); onPick?.Invoke(selected); }
            else Ui.Empty("Tap a creature.", detail);
        }

        /// <summary>Fits as many cells of about <paramref name="target"/> px as the width allows, then stretches them so the
        /// left, right and between-cell gutters are all exactly <paramref name="gap"/> (POL-C1/C8): no ragged right edge.
        /// Cells go into explicit row containers (never wrap), so every row has exactly the same count and edges.
        /// Returns the column count laid out; a call with the same count only resizes.</summary>
        private static int EvenGrid(VisualElement grid, List<VisualElement> cells, float width, float target, float gap, int laidCols)
        {
            float w = width - grid.resolvedStyle.paddingLeft - grid.resolvedStyle.paddingRight;
            if (float.IsNaN(w) || w <= target) return laidCols;
            int cols = Mathf.Max(1, Mathf.FloorToInt((w - gap) / (target + gap)));
            // Exact (unfloored) size: flooring dumped the remainder on the right edge, a ragged gutter (POL-C8).
            float size = (w - gap * (cols + 1)) / cols - 0.01f;
            if (cols != laidCols)
            {
                grid.Clear();
                VisualElement row = null;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (i % cols == 0)
                    {
                        row = new VisualElement { name = "grid-row" };
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.paddingLeft = row.style.paddingRight = gap / 2f;
                        row.style.marginTop = i == 0 ? gap / 2f : 0;
                        grid.Add(row);
                    }
                    row.Add(cells[i]);
                }
                // Pad a short last row with invisible fillers so its cells stay the same size as every other row's.
                for (int i = cells.Count % cols; i > 0 && i < cols; i++)
                {
                    var filler = new VisualElement { name = "grid-filler", pickingMode = PickingMode.Ignore };
                    filler.style.visibility = Visibility.Hidden;
                    filler.style.flexBasis = 0; filler.style.flexGrow = 1; filler.style.flexShrink = 1;
                    filler.style.marginLeft = filler.style.marginRight = gap / 2f;
                    row.Add(filler);
                }
            }
            foreach (var t in cells)
            {
                // Flex shares the row exactly (equal grow, zero basis): a fixed width computed here drifted wider than the row
                // by the cells' own borders and min sizes, leaving a ragged right edge. Height follows the computed size.
                t.style.width = StyleKeyword.Auto; t.style.minWidth = 0; t.style.maxWidth = StyleKeyword.None;
                t.style.flexBasis = 0; t.style.flexGrow = 1; t.style.flexShrink = 1;
                t.style.height = size;
                t.style.marginLeft = t.style.marginRight = t.style.marginTop = t.style.marginBottom = gap / 2f;
            }
            return cols;
        }

        /// <summary>The detail panel (§25.11, GG-T290): name and description once Seen, then each knowledge row in the order
        /// defeats unlock it — Speed, HP and stats, elements, abilities — with a locked row reading "???" and a hint
        /// naming how many more defeats the next one takes.</summary>
        private void Show(VisualElement w, ClassDef d, BestiaryEntry e)
        {
            w.Clear();
            if (e == null || !e.Seen) { Ui.Heading("??? — not yet encountered", w, "dim"); return; }
            int tier = BestiaryKnowledge.Tier(e);
            var top = Ui.El("row", w);
            Ui.Heading(d.Name, top, "grow");
            Ui.Caption($"Defeated {e.Defeated}", top, "gold");
            if (!string.IsNullOrEmpty(d.Description)) GlossaryText.Create(d.Description, "kit-text", w);
            if (!string.IsNullOrEmpty(d.Lore) && d.Lore != d.Description) GlossaryText.Create(d.Lore, "kit-text dim", w);
            if (d.Heavy) GlossaryText.Create(InspectCard.HeavyLine, "kit-text gold", w).name = "beast-heavy"; // GG-T892: its keyword and counters, once seen
            if (d.DropTable != null && C.DropTables.TryGetValue(d.DropTable, out var drops) && drops.Entries.Count > 0)
            {
                var names = drops.Entries.OrderByDescending(x => x.Chance).Select(de => C.TryItem(de.Item)).Where(it => it != null)
                    .Select(it => $"<color=#{ColorUtility.ToHtmlStringRGB(Palette.Rarity(it.Rarity))}>{it.Name}</color>");
                Ui.Field("Drops", string.Join(",  ", names), w, null, "beast-drops").Q<Label>("value").enableRichText = true;
            }
            Fact(w, "beast-speed", "Speed", tier >= BestiaryKnowledge.Speed ? $"SPD {d.BaseStats.Speed:0}" : null);
            Fact(w, "beast-stats", "Stats", tier >= BestiaryKnowledge.Stats
                ? $"HP {Formulas.MaxHp(d.BaseStats, 1)} at Lv 1  ·  " + Ui.StatLine(Formulas.StatsAtLevel(d, 1)).Replace("\n", "  ") + $"  ·  EVA {Formulas.Evasion(Formulas.StatsAtLevel(d, 1)):0}%" : null);
            Fact(w, "beast-elements", "Elements", tier >= BestiaryKnowledge.Elements ? BestiaryKnowledge.ElementLine(d) : null);
            Fact(w, "beast-abilities", "Abilities", tier >= BestiaryKnowledge.Abilities ? "" : null);
            if (tier >= BestiaryKnowledge.Abilities)
            {
                var kit = Ui.KitTraits(C, d);
                if (kit.Length > 0) GlossaryText.Create(kit, "kit-text gold", w);
                // What it does standing in a pincer chain (GG-T307, §1.2.3): every foe has its own support, read at this tier.
                GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, d.Support, named: true), "kit-text gold", w).name = "beast-support";
                // Its abilities as chips: hold one to read it whole (GG-T252).
                var ids = d.Abilities.Concat(string.IsNullOrEmpty(d.ChargeAbility) ? Enumerable.Empty<string>() : new[] { d.ChargeAbility }).Distinct();
                var chips = Ui.El("row", w);
                chips.style.flexWrap = Wrap.Wrap;
                foreach (var id in ids)
                {
                    if (!(C.TryAbility(id) is AbilityDef ab) || string.IsNullOrEmpty(ab.Name)) continue;
                    var chip = Ui.El("row", chips);
                    chip.name = "beast-ability-" + ab.Id;
                    chip.style.alignItems = Align.Center;
                    chip.style.minHeight = Kit.Row;
                    chip.style.marginRight = Space.S24;
                    var icon = Art.AbilityIcon(ab);
                    if (icon != null) { var ic = Ui.El("menu-icon", chip); ic.style.backgroundImage = new StyleBackground(icon); }
                    Ui.Name(ab.Name, chip);
                    InspectCard.Attach(chip, b => InspectCard.Ability(b, ab), tap: true); // GG-T450: chips have no other tap
                }
                Ui.Para(GridGame.Core.Rules.EnemyPlanner.Describe(d.Targeting != Targeting.Auto ? d.Targeting : d.BaseStats.Intelligence >= 14 ? Targeting.Weakest : d.Tags.Contains("Sentient") ? Targeting.Threat : Targeting.Closest), w, "warn");
                Ui.Para(GridGame.Core.Rules.EnemyPlanner.DescribeBrain(d.Brain), w);
            }
            var hint = BestiaryKnowledge.Hint(e);
            if (hint != null) Ui.Para(hint, w, "dim").name = "beast-hint";
        }

        /// <summary>One knowledge row: its label, then the value, or "???" while <paramref name="value"/> is still locked (null).</summary>
        private static void Fact(VisualElement w, string name, string label, string value)
        {
            Ui.Field(label, value ?? "???", w, value == null ? "dim" : null, name);
        }
    }

    // ── Settings (§31) ───────────────────────────────────────────────────────────

    public sealed class SettingsScreen : HubPage
    {
        public override string Id => "settings";
        protected override string Title => "Settings";
        /// <summary>The active profile (GG-T99), in the header (GG-T454).</summary>
        protected override string Subtitle => $"Profile: {App.Profile?.Name}";
        protected override string SubtitleName => "settings-profile";

        /// <summary>The difficulty dial (§22.9, GG-T309): − / level / + (each a 132-px target), then one line naming what
        /// the level does and its reward multiplier. Saved at once; the next battle is built at the new level.</summary>
        private void DifficultyControl(SettingsSave st, VisualElement parent)
        {
            st.Difficulty ??= new DifficultySettings();
            int level = DifficultyTable.Clamp(st.Difficulty.Level);
            void Set(int to)
            {
                st.Difficulty.Level = Math.Max(DifficultyTable.Min, Math.Min(DifficultyTable.Max, to));
                App.SaveSettings(); Refresh();
            }
            var row = Ui.El("row", parent);
            row.name = "settings-difficulty";
            row.style.alignItems = Align.Center;
            row.style.minHeight = Kit.Row;
            row.style.paddingLeft = Kit.Indent; // the label lines up with the menu rows' labels
            row.KindIcon(RowKind.Settings); // GG-T750 (§26.7): lines up with the setting rows' icons
            Ui.Text("Difficulty", "menu-label grow", row);
            var minus = Ui.Button("−", () => Set(level - 1), "btn--small btn--icon", row);
            minus.name = "difficulty-down";
            var n = Ui.Name(level.ToString(), row, "gold");
            n.name = "difficulty-level";
            // At least a column wide, wider when Text size grows the number (GG-T495: a fixed width ellipsized "10" at 130%).
            n.style.minWidth = Size.ColumnS; n.style.flexShrink = 0f; n.style.unityTextAlign = TextAnchor.MiddleCenter;
            var plus = Ui.Button("+", () => Set(level + 1), "btn--small btn--icon", row);
            plus.name = "difficulty-up";
            Ui.SetEnabled(minus, level > DifficultyTable.Min);
            Ui.SetEnabled(plus, level < DifficultyTable.Max);
            Ui.Note(DifficultyTable.Describe(level) + " Applies from the next battle.", parent).name = "difficulty-text";
        }

        /// <summary>The turn mode (§2.13, GG-T774): the profile's <see cref="SettingsSave.TurnMode"/>, cycled like Battle
        /// speed through <see cref="TurnModes.Available"/> (only modes that are built), then one line explaining it. A save
        /// New Game locked to a mode keeps playing that one, and the line says so; changing the mode here releases that lock
        /// so the save follows Settings again (GG-T1216: switching modes to test them is never blocked).</summary>
        private void TurnModeControl(SettingsSave st, VisualElement parent)
        {
            var c = App.Content;
            var mode = TurnModes.IsBuilt(st.TurnMode) ? st.TurnMode : TurnModes.Available[0];
            var row = Ui.MenuRow("Turn mode", TurnModes.Name(c, mode), () =>
            {
                st.TurnMode = TurnModes.Next(mode);
                App.SaveSettings();
                if (App.Save?.TurnModeLock != null) { TurnModeSetting.Lock(App.Save, null); App.Persist(); }
                Refresh();
            }, parent);
            row.name = "settings-turn-mode";
            row.KindIcon(RowKind.Settings);
            string text = TurnModes.Describe(c, mode);
            if (App.Save?.TurnModeLock is TurnMode locked)
                text += $" This save is locked to {TurnModes.Name(c, locked)} until you change it here.";
            Ui.Note(text, parent).name = "settings-turn-mode-text";
        }

        /// <summary>A parent passed the gate this visit, so the limit cycles freely until Settings closes.</summary>
        private bool limitUnlocked;

        /// <summary>The monthly spending limit (§25.18.1, GG-T1559): the wallet's limit and this month's spend, cycled through
        /// <see cref="SpendLimits.Choices"/> behind the parental gate unless the wallet answered adult.</summary>
        private void SpendingLimitRow(AccountWallet wallet, VisualElement parent)
        {
            void Cycle()
            {
                wallet.MonthlyLimitCents = SpendLimits.Next(wallet.MonthlyLimitCents);
                DiamondExchange.WalletDirty = true;
                App.PersistWallet();
                Refresh();
            }
            string spent = SpendLimits.Money(SpendLimits.SpentCents(wallet, DateTime.UtcNow));
            var row = Ui.MenuRow("Spending limit", $"{SpendLimits.Describe(wallet)} · {spent} this month", () =>
            {
                if (limitUnlocked || !SpendLimits.LimitGated(wallet)) Cycle();
                else SpendGate.Parental(() => { limitUnlocked = true; Cycle(); });
            }, parent);
            row.name = "settings-spending-limit";
            row.KindIcon(RowKind.Settings);
        }

        /// <summary>Delete all game data (§15, GG-T1580): two questions, then <see cref="GameApp.DeleteAllData"/> erases this
        /// device's profiles, slots, settings and wallet, and the cloud copies, and the app starts over at the title.</summary>
        private void DeleteAllDataRow(VisualElement parent)
        {
            var row = Ui.MenuRow("Delete all game data", "Erase", () =>
                Confirm.Show("Delete all game data? Every profile, save slot and setting on this device is erased, and so are their cloud copies. This can't be undone.", "Continue", () =>
                    Confirm.Show("Purchased Shards, owned products and cosmetics are lost too, and are not refunded. Delete everything?", "Delete all", () =>
                        App.DeleteAllData(ok => App.Toast(ok ? "All game data deleted" : "Some game data couldn't be deleted")))), parent);
            row.name = "settings-delete-all-data";
            row.KindIcon(RowKind.Settings);
        }

        protected override void BuildBody(VisualElement body)
        {
            // The active profile's settings (GG-T99, §15.4): one set for every save slot, kept in its settings.json.
            var st = App.Settings;
            // GG-T454: one scrolling list (the rows, then How to play), so the last rows are never cut off a short phone.
            // GG-T1223: grouped into Gameplay, Graphics, Display, Sound, Accessibility and Data sections.
            var w = Ui.ListPanel(body, null, "settings-list");
            // GG-T750 (§26.7): every setting row leads with the Settings gear, the save rows with the Save disk.
            VisualElement Row(string label, string value, Action set, string name = null)
            {
                var row = Ui.MenuRow(label, value, () => { set(); App.ApplySettings(); App.SaveSettings(); Refresh(); }, w).KindIcon(RowKind.Settings);
                if (name != null) row.name = name;
                return row;
            }
            void Toggle(string label, bool value, Action<bool> set, string name = null) => Row(label, value ? "ON" : "OFF", () => set(!value), name);
            void Cycle(string label, float value, float[] opts, string fmt, Action<float> set, string name = null)
            {
                int i = Array.FindIndex(opts, o => Mathf.Abs(o - value) < 0.01f);
                Row(label, string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, value), () => set(opts[(i + 1) % opts.Length]), name);
            }
            void CycleInt(string label, int value, int[] opts, Func<int, string> text, Action<int> set, string name)
            {
                int i = Array.IndexOf(opts, value);
                Row(label, text(value), () => set(opts[(i + 1) % opts.Length]), name);
            }
            void Section(string title) => Ui.Section(title, w).name = "settings-section-" + title.ToLowerInvariant();
            void CycleFont()
            {
                var names = Fonts.Presets.Select(p => p.Name).ToArray();
                int i = Array.IndexOf(names, st.FontName);
                st.FontName = names[(i + 1) % names.Length];
                App.ApplySettings(); App.SaveSettings(); Refresh();
            }
            // GG-T1222: Window mode and Resolution only apply to a desktop player, Thumb margin only to a phone.
            bool mobile = Application.isMobilePlatform;
            bool desktop = !mobile && Application.platform != RuntimePlatform.WebGLPlayer;

            Section("Gameplay");
            TurnModeControl(st, w); // GG-T774 (§2.13); first row so the modes can be picked from the title screen (GG-T1216)
            DifficultyControl(st, w); // GG-T309: the 1..10 dial
            Cycle("Battle speed", st.BattleSpeed, new[] { 1f, 1.5f, 2f, 3f }, "×{0:0.#}", v => st.BattleSpeed = v);
            Toggle("Drag timer (4 s)", st.DragTimer, v => st.DragTimer = v);
            Toggle("Confirm before acting", st.ConfirmActions, v => st.ConfirmActions = v);
            Toggle("Pincer close-ups", st.PincerCutIns, v => st.PincerCutIns = v);

            // GG-T1221: quality (-1 = the project's own), frame-rate cap, vsync, shake and effects amount.
            Section("Graphics");
            var qualities = QualitySettings.names;
            CycleInt("Quality", st.QualityLevel >= 0 && st.QualityLevel < qualities.Length ? st.QualityLevel : -1,
                Enumerable.Range(-1, qualities.Length + 1).ToArray(), q => q < 0 ? "Default" : qualities[q], v => st.QualityLevel = v, "settings-quality");
            CycleInt("Frame rate cap", GameSettings.ClampFrameRateCap(st.FrameRateCap), new[] { 30, 60, -1 },
                f => f < 0 ? "Uncapped" : $"{f} fps", v => st.FrameRateCap = v, "settings-frame-cap");
            if (!mobile) Toggle("VSync", st.VSync, v => st.VSync = v, "settings-vsync"); // GG-T1240: mobile ignores vsync
            Cycle("Screen shake", st.ScreenShake, new[] { 0f, 0.5f, 1f }, "{0:0%}", v => st.ScreenShake = v, "settings-screen-shake");
            Cycle("Effects", st.VfxAmount, new[] { 0f, 0.5f, 1f }, "{0:0%}", v => st.VfxAmount = v, "settings-effects");

            Section("Display");
            if (desktop)
            {
                CycleInt("Window mode", GameSettings.ClampFullScreen(st.FullScreen), new[] { -1, 0, 1 },
                    m => m < 0 ? "As launched" : m == 1 ? "Fullscreen" : "Windowed", v => st.FullScreen = v, "settings-window-mode");
                var sizes = new List<(int w, int h)> { (0, 0) };
                foreach (var r in UnityEngine.Screen.resolutions)
                    if (!sizes.Contains((r.width, r.height))) sizes.Add((r.width, r.height));
                // GG-T1238: a saved size missing from this monitor's list (other monitor, hand edit) is still applied by
                // ApplyDisplay, so list it rather than show "As launched" for it.
                var saved = (st.ResolutionWidth, st.ResolutionHeight);
                if (saved.Item1 > 0 && saved.Item2 > 0 && !sizes.Contains(saved)) sizes.Add(saved);
                int at = Math.Max(0, sizes.IndexOf(saved));
                Row("Resolution", at == 0 ? "As launched" : $"{sizes[at].w}×{sizes[at].h}", () =>
                {
                    var next = sizes[(at + 1) % sizes.Count];
                    st.ResolutionWidth = next.w; st.ResolutionHeight = next.h;
                }, "settings-resolution");
            }
            if (mobile)
                CycleInt("Thumb margin", GameSettings.ClampThumbMargin(st.ThumbMargin), new[] { 0, 24, 48, 72, GameSettings.MaxThumbMargin },
                    m => m == 0 ? "OFF" : $"{m} px", v => st.ThumbMargin = v, "settings-thumb-margin");
            CycleInt("UI scale", GameSettings.ClampUiScale(st.UiScale), new[] { 80, 90, 100 }, s => $"{s}%", v => st.UiScale = v, "settings-ui-scale");

            Section("Sound");
            Cycle("Master volume", st.MasterVolume, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, "{0:0%}", v => st.MasterVolume = v, "settings-master-volume");
            Cycle("Music volume", st.MusicVolume, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, "{0:0%}", v => st.MusicVolume = v);
            Cycle("Sound volume", st.SfxVolume, new[] { 0f, 0.25f, 0.5f, 0.7f, 0.85f, 1f }, "{0:0%}", v => st.SfxVolume = v);
            Toggle("Mute music", st.MuteMusic, v => st.MuteMusic = v);
            Toggle("Mute sound", st.MuteSfx, v => st.MuteSfx = v);
            Toggle("Mute when unfocused", st.MuteWhenUnfocused, v => st.MuteWhenUnfocused = v, "settings-mute-unfocused");

            Section("Accessibility");
            // GG-T495: text size, applied live to every screen (TextSize classes on the UI root).
            Row("Text size", $"{TextSize.Clamp(st.TextScale)}%", () => st.TextScale = TextSize.Next(st.TextScale), "settings-text-size");
            Toggle("Colorblind palette", st.ColorblindMode, v => st.ColorblindMode = v);
            Toggle("Reduce motion", st.ReduceMotion, v => st.ReduceMotion = v);
            // GG-T392: the look presets are developer dials, hidden while Features.LookPresets is off (GameApp.ApplySettings
            // then draws with the default theme, font and word wrap).
            if (Features.LookPresets)
            {
                // GG-T407: every window theme (presets, the player's own, and the editor for gradient, texture and frame).
                var windows = Ui.MenuRow("Windows", Art.ThemeName, () => App.Router.Push(new WindowEditorScreen()), w);
                windows.name = "settings-windows";
                windows.KindIcon(RowKind.Settings);
                Ui.MenuRow("Font", st.FontName, CycleFont, w).KindIcon(RowKind.Settings);
                // GG-T305: how wrapped text breaks, applied live to every screen (TextFit).
                Row("Text wrapping", st.Hyphenate ? "Hyphenate" : "Word wrap", () => st.Hyphenate = !st.Hyphenate, "settings-text-wrap");
            }

            Section("Data");
            // GG-T607 (§15.2): move a slot to another device as a copyable code, no cloud account needed.
            var transfer = Ui.MenuRow("Transfer save", "Copy code", () => App.Router.Push(new SaveExportScreen(App.Slot >= 0 ? App.Slot : Math.Max(0, App.Saves?.Root == null ? 0 : SaveFiles.MostRecent(App.Saves.Root)))), w);
            transfer.name = "settings-transfer-save";
            transfer.KindIcon(RowKind.Save);
            var importRow = Ui.MenuRow("Import save", "Paste code", () => App.Router.Push(new SaveImportScreen()), w);
            importRow.name = "settings-import-save";
            importRow.KindIcon(RowKind.Save);
            if (Features.DiamondStore && DiamondExchange.Wallet != null) SpendingLimitRow(DiamondExchange.Wallet, w);
            DeleteAllDataRow(w);
            // GG-T1330: version, legal links and third-party notices.
            var about = Ui.MenuRow("About", "", () => App.Router.Push(new AboutScreen()), w);
            about.name = "settings-about";
            about.KindIcon(RowKind.Settings);
            var help = Ui.El("col", w);
            help.name = "settings-help";
            Ui.Section("How to play", help);
            var t = Ui.Para(
                "• Drag a hero tile by tile. Anyone in the way swaps into the tile you left.\n" +
                "• Trap enemies in a straight line between two heroes to PINCER them. Allies next to the attackers join in.\n" +
                "• Moving never hurts. Only pincers and spells deal damage.\n" +
                "• Time moves only when you act. Icons flow left; whoever reaches the left edge acts, and speed decides who gets there first.\n" +
                "• Pincer an enemy to push its icon back on the timeline.\n" +
                "• Pincers fill your mana orbs. Spend them on spells; skills and items cost a turn.\n" +
                "• Tap a hero, a slot, a target, then Cast. Spells charge on the timeline before they land.\n" +
                "• Roles need each other: Tanks Taunt so enemies leave your casters alone, Healers mend (WIS), Mages hit hardest with spells (INT), Strikers swing Cleave and War Cry (STR).\n" +
                "• Fallen enemies sometimes drop a pickup. Walk a hero over it before it fades.\n" +
                "• GUARD skips to the next enemy turn and shields everyone.", help);
            if (S != null)
            {
                // GG-T539: one button brings back every lesson: the first battle's (on The Tithe Moors I), each battle system's
                // the next time it comes up, and every Camp and vendor lesson. It also turns lessons back on after Skip tutorial.
                var replay = RowIcon.IconButton("Replay tutorials", RowKind.NewGamePlus, () => { Coach.ResetAll(S); Done(Result.Success("Every lesson will show again")); }, "btn--small", Ui.Actions(help));
                replay.name = "replay-camp-tips";
            }
        }
    }

    // ── Aetherist: refine Dust into Shards (§25.18.1, GG-T570/624) ──────────────────

    /// <summary>The Aetherist's counter (GG-T624): buy Aetherite Dust with gold, refine it into Aetherite Shards. The
    /// only station whose two actions are both currency exchanges — no recipes, no gear.</summary>
    public sealed class AetheristScreen : HubPage
    {
        public override string Id => "aetherist";
        protected override string Title => "Manasmith";
        protected override string Backdrop => "desert";
        protected override string Npc => CampNpcs.Aetherist;

        private int buyAmount = 10;
        private int refineAmount = 1;
        private static readonly int[] Steps = { -10, -1, 1, 10 };

        protected override void BuildBody(VisualElement body)
        {
            int dust = DustEconomy.Have(S);
            int shards = DiamondExchange.Have(S);
            int maxBuy = DustEconomy.MaxBuy(C, S);
            int maxRefine = DustEconomy.MaxRefine(C, S);
            buyAmount = Math.Max(1, buyAmount);
            refineAmount = Math.Max(1, refineAmount);

            var bal = Ui.Panel(body, null, false, "aetherist-balance");
            Ui.Field(DustEconomy.DustName, dust.ToString("N0"), bal, null, "aetherist-dust-count");
            Ui.Field(ShardText.Plural, ShardText.ShortOf(shards), bal, null, "aetherist-shard-count");
            var greet = ShopBark(C, "aetherist/greet");
            if (greet != null) Ui.Para(greet, bal, "dim");

            var buy = Ui.Panel(body, "Buy Dust with gold", false, "aetherist-buy-panel");
            var pick = Ui.Actions(buy);
            pick.style.alignItems = Align.Center;
            foreach (var step in Steps)
            {
                int d = step;
                if (d > 0) continue;
                Ui.Button($"−{-d}", () => { buyAmount = Math.Max(1, buyAmount + d); Refresh(); }, "btn--small", pick).name = $"aetherist-buy-minus{-d}";
            }
            Ui.Name($"{buyAmount:N0}", pick, "gold").name = "aetherist-buy-amount";
            foreach (var step in Steps)
            {
                int d = step;
                if (d < 0) continue;
                Ui.Button($"+{d}", () => { buyAmount += d; Refresh(); }, "btn--small", pick).name = $"aetherist-buy-plus{d}";
            }
            var maxBuyBtn = Ui.Button("Max", () => { buyAmount = Math.Max(1, maxBuy); Refresh(); }, "btn--small", pick);
            maxBuyBtn.name = "aetherist-buy-max";
            Ui.SetEnabled(maxBuyBtn, maxBuy > 0);
            Ui.Field("Costs", Ui.Gold(DustEconomy.GoldFor(buyAmount)), buy, S.Gold >= DustEconomy.GoldFor(buyAmount) ? null : "bad", "aetherist-buy-quote");
            var buyBtn = RowIcon.IconButton($"Buy {buyAmount:N0} {DustEconomy.DustName}", RowKind.Confirm, () => Done(DustEconomy.Buy(C, S, buyAmount), true, "ui.buy"), "btn--primary", buy);
            buyBtn.name = "aetherist-buy";
            Ui.SetEnabled(buyBtn, buyAmount <= maxBuy);

            var refine = Ui.Panel(body, "Refine into " + ShardText.Plural, false, "aetherist-refine-panel");
            var rpick = Ui.Actions(refine);
            rpick.style.alignItems = Align.Center;
            foreach (var step in Steps)
            {
                int d = step;
                if (d > 0) continue;
                Ui.Button($"−{-d}", () => { refineAmount = Math.Max(1, refineAmount + d); Refresh(); }, "btn--small", rpick).name = $"aetherist-refine-minus{-d}";
            }
            Ui.Name($"{refineAmount:N0}", rpick, "gold").name = "aetherist-refine-amount";
            foreach (var step in Steps)
            {
                int d = step;
                if (d < 0) continue;
                Ui.Button($"+{d}", () => { refineAmount += d; Refresh(); }, "btn--small", rpick).name = $"aetherist-refine-plus{d}";
            }
            var maxRefineBtn = Ui.Button("Max", () => { refineAmount = Math.Max(1, maxRefine); Refresh(); }, "btn--small", rpick);
            maxRefineBtn.name = "aetherist-refine-max";
            Ui.SetEnabled(maxRefineBtn, maxRefine > 0);
            Ui.Field("Needs", $"{refineAmount * DustEconomy.DustPerShard:N0} {DustEconomy.DustName}", refine,
                dust >= refineAmount * DustEconomy.DustPerShard ? null : "bad", "aetherist-refine-quote");
            var refineBtn = RowIcon.IconButton($"Refine {ShardText.ShortOf(refineAmount)}", RowKind.Confirm, () =>
            {
                string occasion = dust < maxRefine * DustEconomy.DustPerShard ? "aetherist/short" : "aetherist/refine";
                var r = DustEconomy.Refine(C, S, refineAmount);
                var bark = r.Ok ? ShopBark(C, occasion) : null;
                Done(r, true, "ui.diamond");
                if (bark != null) App.Toast(bark, 2.6f);
            }, "btn--primary", refine);
            refineBtn.name = "aetherist-refine";
            Ui.SetEnabled(refineBtn, refineAmount <= maxRefine);

            // GG-T677: the Manasmith reseals Broken Mana Orbs found as loot into owned orbs (one more orb to fill in battle).
            int broken = S.Count(ManaOrbs.BrokenOrbId);
            var orbs = Ui.Panel(body, "Mana Orbs", false, "aetherist-orb-panel");
            Ui.Field("Held", $"{ManaOrbs.Owned(C, S)} of {GridGame.Core.Battle.BattleConfig.MaxOrbCapacity}", orbs, null, "aetherist-orb-count");
            Ui.Field("Broken Mana Orbs", broken.ToString("N0"), orbs, null, "aetherist-broken-count");
            Ui.Field("Restoring one costs", $"{ManaOrbs.RestoreDust:N0} {DustEconomy.DustName} + {Ui.Gold(ManaOrbs.RestoreGold)}", orbs, null, "aetherist-restore-quote");
            var can = ManaOrbs.CanRestore(C, S);
            var restoreBtn = RowIcon.IconButton("Restore a Mana Orb", RowKind.Confirm, () => Done(ManaOrbs.Restore(C, S), true, "ui.diamond"), "btn--primary", orbs);
            restoreBtn.name = "aetherist-restore";
            Ui.SetEnabled(restoreBtn, can.Ok);
            if (!can.Ok && broken > 0) Ui.Para(can.Message, orbs, "dim");

            Ui.Note($"{DustEconomy.DustPerShard} {DustEconomy.DustName} refines into 1 {ShardText.Short}. {DustEconomy.DustName} costs {DustEconomy.GoldPerDust} G a piece here — " +
                $"buying {ShardText.Plural} outright at the {ShardText.Store} is the better rate; Dust is the free-play path, earned in battle.", body);
        }

        /// <summary>A random line for this shop occasion, or null with nothing authored for it yet.</summary>
        private static string ShopBark(ContentDb c, string key) =>
            c != null && c.ShopBarks.TryGetValue(key, out var lines) && lines.Count > 0 ? lines[UnityEngine.Random.Range(0, lines.Count)] : null;
    }
}
