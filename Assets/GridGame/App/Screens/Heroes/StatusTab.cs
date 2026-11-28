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
            var art = Ui.El("thumb", artCol);
            art.name = "hero-thumb";
            art.style.width = art.style.height = Size.PortraitW;
            Ui.PortraitThumb(art, h.ClassId);
            Ui.Tappable(art, () => ArtPreview.Open(h.ClassId), null); // GG-T1804: the full art is one tap away
            // GG-T532: a class with male art can switch looks here any time (the same picker as on joining).
            if (!h.Domesticated && Art.HasMaleArt(h.ClassId))
            {
                var look = RowIcon.IconButton("Change look", RowKind.Hero, () => VariantPicker.Change(h, Refresh), "btn--small", artCol);
                look.name = "hero-look";
                Ui.Margin(look, top: Space.S24);
            }
            var stats = Ui.El("col grow", top);
            Ui.Margin(stats, left: Space.S24); stats.style.flexShrink = 1; stats.style.minWidth = 0;
            int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp;
            Ui.RoleBadge(Ui.RoleOf(h, def), stats).style.alignSelf = Align.FlexStart;
            Ui.Para(Ui.RoleBlurb(Ui.RoleOf(h, def)), stats, "dim");
            GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, def.Support), "kit-text gold", stats).name = "hero-support"; // GG-T298
            Ui.Heading($"HP  {hp} / {u.MaxHp}", stats);
            Ui.Gauge(hp / (float)u.MaxHp, stats);
            var (lvl, into) = GridGame.Core.Rules.Formulas.LevelFromTotalXp(h.TotalXp);
            Ui.Heading($"Level {lvl}", stats, "gold");
            Ui.Caption($"Next: {GridGame.Core.Rules.Formulas.XpToNext(lvl) - into} XP", stats, "dim");
            Ui.Caption($"Skill points: {h.SkillPoints}", stats, "dim");

            // GG-T48: base (pre-gear) vs. gear-bought vs. total, instead of just the effective total.
            // GG-T1043: the skill hexagon's owned passives get their own Board column (they were folded into Base).
            var board = HeroBoard.BoardStats(C, h);
            var withBoard = u.LevelStats; // the preview's level stats already carry the board bonus (BattleFactory.SpecFor)
            var baseStats = withBoard + board * -1f;
            var eff = u.Effective;
            // GG-T1971: "Talents" is the widest heading, so its column is wider and the number columns give it the room
            // (the row stays as wide as four ColumnL * 1.25 columns at the phone caption size, GG-T1861).
            float statCol = Size.ColumnL * 1.15f, talentCol = Size.ColumnL * 1.5f;
            var statHead = Ui.El("row", stats);
            statHead.name = "status-stat-head";
            Ui.Caption("", statHead).style.width = Size.Column;
            Ui.Caption("Base", statHead, "dim").style.width = statCol;
            Ui.Caption("Talents", statHead, "dim").style.width = talentCol;
            Ui.Caption("Gear", statHead, "dim").style.width = statCol;
            Ui.Caption("Total", statHead, "dim").style.width = statCol;
            void Delta(VisualElement r, float d, float w) =>
                Ui.Caption(Math.Abs(d) < 0.05f ? "—" : $"{(d > 0 ? "+" : "")}{d:0}", r, d > 0.05f ? "good" : d < -0.05f ? "bad" : "dim").style.width = w;
            void Stat(string n, float b, float bb, float t)
            {
                var r = Ui.El("row", stats);
                Ui.Caption(n, r, "dim").style.width = Size.Column;
                Ui.Caption($"{b:0}", r, "dim").style.width = statCol;
                Delta(r, bb - b, talentCol);
                Delta(r, t - bb, statCol);
                Ui.Name($"{t:0}", r).style.width = statCol;
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
            // GG-T1826: a unit's description and lore live here and in the Bestiary, not in battle.
            if (!string.IsNullOrEmpty(def.Description) || !string.IsNullOrEmpty(def.Lore))
            {
                Ui.Section("Lore", sheet);
                if (!string.IsNullOrEmpty(def.Description)) GlossaryText.Create(def.Description, "kit-text", sheet).name = "status-description";
                if (!string.IsNullOrEmpty(def.Lore) && def.Lore != def.Description) GlossaryText.Create(def.Lore, "kit-text dim", sheet).name = "status-lore";
            }

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
                var row = Ui.MenuRow(a.Name, a.Kind == AbilityKind.Passive ? "Passive" : a.Kind.ToString(), null, abilitiesBlock);
                row.KindIcon(RowKind.Ability, Art.AbilityIcon(a));
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
}
