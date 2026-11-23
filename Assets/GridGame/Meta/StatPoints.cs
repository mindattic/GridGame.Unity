using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>
    /// Level-up stat points (§15.1, GG-T591, from GG-T168): every level past 1 earns one free point the player puts into
    /// any of the 8 stats (<see cref="StatGrantDef.Names"/>). A pool of its own, apart from <see cref="HeroSave.SkillPoints"/>
    /// (the skill-tree currency, which quests, bounties and dialogue also pay): see the BIBLE's GG-T591 amendment for why.
    /// Earned points are derived from the hero's level (<see cref="Formulas.StatPointsForLevel"/>), never stored, so every
    /// way a level can move (battle, hard-won XP, Crucible, a recruit or guest arriving above level 1, a debug level set) pays
    /// them with no grant at each site, and an older save needs no backfill. Only the picks are saved
    /// (<see cref="HeroSave.StatPicks"/>); a pick is permanent, the hero's build is the history of what they chose.
    /// </summary>
    public static class StatPoints
    {
        /// <summary>Points this hero has earned so far: one per level past 1.</summary>
        public static int Earned(HeroSave h) => h == null ? 0 : Formulas.StatPointsForLevel(Progression.Level(h));

        /// <summary>Points already put into stats.</summary>
        public static int Spent(HeroSave h) => h?.StatPicks == null ? 0 : h.StatPicks.Values.Sum(v => Math.Max(0, v));

        /// <summary>Points earned and not yet put into a stat.</summary>
        public static int Unspent(HeroSave h) => Math.Max(0, Earned(h) - Spent(h));

        /// <summary>Points (not stat amount) this hero has put into <paramref name="stat"/> (a <see cref="StatGrantDef.Names"/> entry).</summary>
        public static int In(HeroSave h, string stat) =>
            h?.StatPicks != null && stat != null && h.StatPicks.TryGetValue(stat, out var n) ? Math.Max(0, n) : 0;

        /// <summary>Puts one unspent point into <paramref name="stat"/>.</summary>
        public static Result Spend(HeroSave h, string stat)
        {
            if (h == null) return Result.Fail("No such hero");
            if (!StatGrantDef.IsStatName(stat)) return Result.Fail($"Unknown stat '{stat}'");
            if (Unspent(h) <= 0) return Result.Fail("No stat points to spend");
            h.StatPicks ??= new Dictionary<string, int>();
            h.StatPicks[stat] = In(h, stat) + 1;
            return Result.Success($"+{Formulas.StatPointValue} {stat}");
        }

        /// <summary>The picks as points per stat (not stat amount): what <see cref="AbilityDef.Requires"/> is measured against
        /// (GG-T592, §25.6.5). Unknown keys (a hand-edited save) are ignored.</summary>
        public static Stats Picks(HeroSave h)
        {
            var s = new Stats();
            if (h?.StatPicks == null) return s;
            foreach (var kv in h.StatPicks)
                if (kv.Value > 0 && StatGrantDef.IsStatName(kv.Key))
                    s = s + new StatGrantDef { Stat = kv.Key, Amount = kv.Value }.ToStats();
            return s;
        }

        /// <summary>Every pick as one <see cref="Stats"/> block, added to the hero's level stats in battle
        /// (<see cref="Core.Battle.HeroSpec.StatBonus"/>). Unknown keys (a hand-edited save) are ignored.</summary>
        public static Stats Bonus(HeroSave h)
        {
            var s = new Stats();
            if (h?.StatPicks == null) return s;
            foreach (var kv in h.StatPicks)
                if (kv.Value > 0 && StatGrantDef.IsStatName(kv.Key))
                    s = s + new StatGrantDef { Stat = kv.Key, Amount = kv.Value * Formulas.StatPointValue }.ToStats();
            return s;
        }

        /// <summary>The stat the camp routine (GG-T54's bot) puts a point into: the class's own strongest growth stat, so a
        /// bot hero stays a pure build of its class. Ties go to the earlier <see cref="StatGrantDef.Names"/> entry.</summary>
        public static string BotPick(ContentDb c, HeroSave h)
        {
            var g = c?.TryClass(h?.ClassId)?.Growth;
            if (g == null) return StatGrantDef.Names[0];
            float[] v = { g.Strength, g.Vitality, g.Agility, g.Speed, g.Stamina, g.Intelligence, g.Wisdom, g.Luck };
            int best = 0;
            for (int i = 1; i < v.Length; i++) if (v[i] > v[best]) best = i;
            return StatGrantDef.Names[best];
        }

        /// <summary>The camp routine's spend: every unspent point into <see cref="BotPick"/>. Returns one line per hero that spent.</summary>
        public static List<string> AutoSpend(ContentDb c, SaveData s)
        {
            var log = new List<string>();
            foreach (var h in s.Roster)
            {
                int n = Unspent(h);
                if (h == null || n <= 0) continue;
                string stat = BotPick(c, h);
                for (int i = 0; i < n; i++) if (!Spend(h, stat).Ok) break;
                log.Add($"{c.TryClass(h.ClassId)?.Name ?? h.ClassId} put {n} stat point{(n == 1 ? "" : "s")} into {stat}");
            }
            return log;
        }
    }
}
