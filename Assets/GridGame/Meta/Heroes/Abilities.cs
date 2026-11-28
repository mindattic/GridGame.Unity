using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Abilities: learn + slot (§25.6)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Abilities
    {
        /// <summary>What a hero could learn: its class list plus the general spell catalog (hero spells only). GG-T1079: no
        /// longer sold for gold (abilities come from the skill board); kept as the catalog the ability audits check.
        /// GG-T1098: the family-tree paths are retired, so every class lists its <see cref="ClassDef.Abilities"/> and the whole catalog.</summary>
        public static List<AbilityDef> Learnable(ContentDb c, HeroSave h)
        {
            var def = c.TryClass(h.ClassId);
            var set = new List<AbilityDef>();
            if (h.Domesticated) return set; // a tamed monster fights with its own instincts, never a trainer's skills (GG-T303)
            var own = def == null ? new List<string>() : def.Abilities;
            foreach (var id in own)
            {
                var a = c.TryAbility(id);
                if (a != null && IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a)) set.Add(a);
            }
            foreach (var a in c.Abilities.Values)
                if (IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a) &&
                    (a.Kind == AbilityKind.Spell || a.Universal))
                    set.Add(a);
            // GG-T388: the hero's own kit moves first (the cheapest, and what it was built to do), then by price.
            var kit = Progression.KitAbilities(def);
            return set.OrderBy(a => kit.Contains(a.Id) ? 0 : 1).ThenBy(a => Price(a)).ThenBy(a => a.Name).ToList();
        }

        /// <summary>Boss-phase abilities only an enemy fires (an <c>OnEnterAbility</c>): free, so never learnable.</summary>
        private static readonly HashSet<string> EnemyOnly = new HashSet<string> { "enrage", "chrono_rewind", "lich_soul_harvest", "stampede_rush", "hierarch_open_reservoir" };

        public static bool IsHeroUsable(AbilityDef a) => a.Kind != AbilityKind.Passive &&
            (a.Kind == AbilityKind.Spell || a.Kind == AbilityKind.Skill) && !a.Id.StartsWith("charge_") && !EnemyOnly.Contains(a.Id);

        /// <summary>GG-T215: priced off <see cref="BattleConfig"/>'s training constants — a spell by its orb cost,
        /// a skill by its CooldownTurns (skills carry no orb cost, so cooldown is the power proxy), replacing the
        /// old flat 300G-for-every-skill price the RELEASE_SCOPE watch-list flagged.</summary>
        public static int Price(AbilityDef a) => a.Kind == AbilityKind.Skill
            ? BattleConfig.SkillTrainBasePrice + BattleConfig.SkillTrainPricePerCooldown * a.CooldownTurns
            : BattleConfig.SpellTrainBasePrice + BattleConfig.SpellTrainPricePerOrb * a.Cost;

        /// <summary>Is <paramref name="a"/> one of this hero's own kit moves (its class's default loadout)? Kit moves are waived
        /// from their Requires (<see cref="RequiresWaived"/>).</summary>
        public static bool IsKitMove(ContentDb c, HeroSave h, AbilityDef a)
        {
            if (a == null || h == null) return false;
            var def = c.TryClass(h.ClassId);
            return Progression.KitAbilities(def).Contains(a.Id);
        }

        /// <summary>Every hero-usable ability some hero could learn (GG-T1079: the gold Learn screen is gone): any spell, any universal skill, and
        /// every skill on a hero class's list (the union of <see cref="Learnable"/> over the hero classes).</summary>
        public static List<AbilityDef> LearnableByAnyHero(ContentDb c)
        {
            var onHeroLists = new HashSet<string>();
            foreach (var cls in c.Classes.Values)
                if (cls.Team == Team.Hero)
                    foreach (var id in cls.Abilities) onHeroLists.Add(id);
            return c.Abilities.Values
                .Where(a => IsHeroUsable(a) && (a.Kind == AbilityKind.Spell || a.Universal || onHeroLists.Contains(a.Id)))
                .OrderBy(a => a.Id).ToList();
        }

        /// <summary>
        /// GG-T183 reskin audit: the mechanic an ability plays, with its name, numbers (damage, heal, cost, cooldown,
        /// hasten amount, chance) and art stripped away. Two abilities with the same key are the same move with a
        /// different coat of paint. The key is: how it is paid and timed (skill on a cooldown, spell cast on the
        /// timeline, instant spell), its footprint (shape, radius where it changes the footprint, auto/pick, who it
        /// filters, adjacent-only), and each effect it carries — damage (Fire and Lightning separately, since they
        /// thaw/steam and shock/blind), heal or drain, the status it applies or clears, steal, orbs, revive,
        /// bonus-vs-tag, the movement it causes (teleport, knockback, pull, swap, step back) and the timeline change
        /// (hasten, delay). Other elements are resistance-only, so they count as the same mechanic.
        /// </summary>
        public static string MechanicKey(AbilityDef a)
        {
            string pay = a.Kind == AbilityKind.Spell ? (a.CastTime > 0f ? "cast" : "instant") : "skill";
            bool area = a.Shape == TargetShape.Square || a.Shape == TargetShape.Diamond || a.Shape == TargetShape.Cross;
            bool self = a.Shape == TargetShape.Self;
            bool all = a.Shape == TargetShape.AllAllies || a.Shape == TargetShape.AllEnemies;
            var k = new List<string>
            {
                pay,
                a.Shape + (area ? a.Radius.ToString() : ""),
                self || all ? "-" : a.Mode.ToString(),
                self || all ? "-" : a.Filter.ToString(),
                a.AdjacentOnly ? "adj" : "",
            };
            if (a.BaseDamage > 0) k.Add("dmg" + (a.DamageType == DamageType.Fire || a.DamageType == DamageType.Lightning ? ":" + a.DamageType : ""));
            if (a.BaseHeal > 0) k.Add(a.Filter == TargetFilter.EnemyOnly && !self ? "drain" : "heal");
            if (a.ApplyBuff != null) k.Add("buff:" + a.ApplyBuff);
            if (a.RemovesDebuffs) k.Add("cleanse");
            if (a.RemovesBuff != null) k.Add("cure:" + a.RemovesBuff);
            if (a.StealsMana) k.Add("steal");
            if (a.DrainsOrbs > 0) k.Add("siphon"); // GG-T332
            if (a.Tame > 0f) k.Add("tame"); // GG-T303
            if (a.GrantsOrbs > 0) k.Add("orbs");
            if (a.Revives) k.Add("revive");
            if (a.Escape) k.Add("escape");
            if (a.BonusVsTag != null) k.Add("vs:" + a.BonusVsTag);
            if (a.Vaults) k.Add("vault"); // GG-T868
            else if (a.IsTeleport) k.Add("teleport");
            if (a.Knockback > 0) k.Add("knockback");
            if (a.Pull > 0) k.Add("pull");
            if (a.Magnetizes > 0) k.Add("magnetize"); // GG-T878
            if (a.Vanishes) k.Add("vanish"); // GG-T861
            if (a.SwapsPlaces) k.Add("swap");
            if (a.StepBack > 0) k.Add("stepback");
            if (a.HastenU > 0f) k.Add("hasten");
            if (a.Pushback > 0f) k.Add("pushback");
            if (a.SwapsTurns) k.Add("countermarch"); // GG-T336
            if (a.Echoes) k.Add("echo"); // GG-T388
            if (a.RedirectsDebuffs) k.Add("redirect"); // GG-T887
            if (a.Afterimage > 0) k.Add("afterimage"); // GG-T867
            if (a.StealsBoon) k.Add("swipeboon"); // GG-T866
            return string.Join("|", k);
        }

        /// <summary>Does the ability do anything at all when it resolves? (GG-T183 found four that did nothing.)</summary>
        public static bool HasEffect(AbilityDef a) =>
            a.BaseDamage > 0 || a.BaseHeal > 0 || a.ApplyBuff != null || a.RemovesDebuffs || a.RemovesBuff != null || a.StealsMana || a.DrainsOrbs > 0 ||
            a.GrantsOrbs > 0 || a.Revives || a.Escape || a.IsTeleport || a.Knockback > 0 || a.Pull > 0 || a.Magnetizes > 0 || a.Vanishes || a.SwapsPlaces ||
            a.StepBack > 0 || a.HastenU > 0f || a.Pushback > 0f || a.Tame > 0f || a.SwapsTurns || a.Echoes || a.RedirectsDebuffs || a.StealsBoon || a.Afterimage > 0; // GG-T867: an Afterimage dashes; GG-T866: a Swipe Boon steals a boon; GG-T887: a Redirect Flow moves debuffs; GG-T388: an Echo repeats a move; GG-T303: a Tame turns a monster; GG-T336: a Countermarch reorders the foes

        /// <summary>GG-T592 (§25.6.5): is <paramref name="a"/> waived from its <see cref="AbilityDef.Requires"/> for this hero?
        /// A hero's own kit moves are (what its class was built to do: a fresh Red Mage is never locked out of its own
        /// Fireball), and so is the guided first skill
        /// (GG-T180) — GG-T1086: no longer, it is taught on the skill board by <see cref="Training.Teach"/>, and dropping it
        /// spares the Learn tab a Training.First route search per row.</summary>
        public static bool RequiresWaived(ContentDb c, SaveData s, HeroSave h, AbilityDef a) => IsKitMove(c, h, a);

        /// <summary>GG-T592: the stats this hero falls short on for <paramref name="a"/> (a display hint only, GG-T1031), measured against
        /// its skill hexagon stat totals (<see cref="HeroBoard.BoardStats"/>, GG-T1058; was its spent level-up stat points).
        /// Empty when it can be bought now (met or waived).</summary>
        public static List<(string Abbrev, string Stat, float Min, float Have)> Unmet(ContentDb c, SaveData s, HeroSave h, AbilityDef a)
        {
            if (a == null || h == null || !a.HasRequires || RequiresWaived(c, s, h, a)) return new List<(string, string, float, float)>();
            return a.UnmetRequires(HeroBoard.BoardStats(c, h));
        }

        /// <summary>A locked Learn entry's reason: "needs INT 8 (have 3)", or "needs VIT 5, WIS 4 (have 2, 0)". Null when met.</summary>
        public static string NeedsText(List<(string Abbrev, string Stat, float Min, float Have)> unmet) =>
            unmet == null || unmet.Count == 0 ? null
                : $"needs {string.Join(", ", unmet.Select(u => $"{u.Abbrev} {u.Min:0}"))} (have {string.Join(", ", unmet.Select(u => $"{u.Have:0}"))})";

        /// <summary>Reorder the bar (owner direction 2026-10-11: organise your abilities): swap two unlocked slots.</summary>
        public static Result Swap(SaveData s, HeroSave h, int a, int b)
        {
            int unlocked = Progression.UnlockedSlots(s);
            if (a < 0 || b < 0 || a >= unlocked || b >= unlocked) return Result.Fail("Slot locked");
            if (a == b) return Result.Fail("Same slot");
            while (h.Bar.Count < Progression.MaxBarSlots) h.Bar.Add(null);
            var t = h.Bar[a]; h.Bar[a] = h.Bar[b]; h.Bar[b] = t;
            return Result.Success("Moved");
        }

        public static Result Assign(ContentDb c, SaveData s, HeroSave h, int slot, string entry)
        {
            if (slot < 0 || slot >= Progression.MaxBarSlots) return Result.Fail("No such slot");
            if (h != null && h.Domesticated && entry != null) return Result.Fail("A tamed monster fights on instinct"); // GG-T303: no bar
            if (slot >= Progression.UnlockedSlots(s)) return Result.Fail("Slot locked");
            if (entry != null)
            {
                if (entry.StartsWith("item:"))
                {
                    var item = c.TryItem(entry.Substring(5));
                    if (item == null || item.Type != ItemType.Consumable) return Result.Fail("Not usable in battle");
                }
                else if (entry.StartsWith("weapon:"))
                {
                    var w = c.TryItem(entry.Substring(7));
                    if (w == null || w.Slot != EquipmentSlot.Weapon) return Result.Fail("Not a weapon");
                    if (s.Count(w.Id) < 1) return Result.Fail("You need a spare one in your bag");
                }
                else if (!HeroBoard.OwnsAbility(c, h, entry)) return Result.Fail("Not learned"); // known, or on an owned board node (GG-T1043)
                int dup = h.Bar.IndexOf(entry);
                if (dup >= 0 && dup != slot) h.Bar[dup] = null;
            }
            h.Bar[slot] = entry;
            return Result.Success();
        }
    }
}
