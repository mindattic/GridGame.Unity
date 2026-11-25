using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// GG-T593 (§25.6.5, from GG-T168): how the camp routine (GG-T54's bot) spends level-up stat points now that abilities
    /// are gated by <see cref="AbilityDef.Requires"/>. A point goes toward the next ability on the hero's own class list it
    /// is locked out of (<see cref="Target"/>), into the stat that ability is furthest short on, so a bot hero builds toward
    /// unlocks the way a player reading the Learn screen's "needs INT 8" would, instead of piling every point into one growth
    /// stat and never meeting a hybrid's second threshold. Once nothing on its class list is locked any more, points fall
    /// back to <see cref="StatPoints.BotPick"/> (the class's strongest growth stat). Kit moves and the guided first skill are
    /// waived from <c>Requires</c> (<see cref="Abilities.RequiresWaived"/>), so they are never a target.
    /// </summary>
    public static class BotStatSpend
    {
        /// <summary>The ability the bot builds this hero toward: of its own class list's hero-usable abilities it doesn't
        /// know and is locked out of (<see cref="Abilities.Unmet"/>), an on-build one first (every stat it is short on is one
        /// the class's own kit asks for, <see cref="KitAffinity"/>, so a Barbarian climbs STR to Last Man Swinging before
        /// detouring into Mug's LCK), then the fewest points away, then the pricier (higher-tier) one, then class-list order.
        /// Null for a tamed monster (no Learn screen) or when nothing on the list is locked.</summary>
        public static AbilityDef Target(ContentDb c, SaveData s, HeroSave h)
        {
            if (c == null || h == null || h.Domesticated) return null;
            var def = c.TryClass(h.ClassId);
            if (def == null) return null;
            var affinity = KitAffinity(c, def);
            return def.Abilities.Distinct().Select((id, i) => (a: c.TryAbility(id), i))
                .Where(x => x.a != null && Abilities.IsHeroUsable(x.a) && !h.Known.Contains(x.a.Id))
                .Select(x => (x.a, x.i, unmet: Abilities.Unmet(c, s, h, x.a)))
                .Where(x => x.unmet.Count > 0)
                .OrderByDescending(x => x.unmet.All(u => Get(affinity, u.Stat) > 0f))
                .ThenBy(x => x.unmet.Sum(u => u.Min - u.Have))
                .ThenByDescending(x => Abilities.Price(x.a))
                .ThenBy(x => x.i)
                .Select(x => x.a).FirstOrDefault();
        }

        /// <summary>The stats a class's own kit (<see cref="Progression.KitAbilities"/>) is built on: the sum of its kit
        /// moves' <see cref="AbilityDef.Requires"/> (Barbarian: STR 21; Paladin: VIT 11, WIS 7, STR 2). The kit itself is
        /// waived from them, but they name the class's build, which is what the bot's points should follow.</summary>
        public static Stats KitAffinity(ContentDb c, ClassDef def)
        {
            var sum = new Stats();
            foreach (var id in Progression.KitAbilities(def))
                if (c.TryAbility(id)?.Requires is Stats r) sum = sum + r;
            return sum;
        }

        /// <summary>The stat the next point goes into: the stat <see cref="Target"/> is furthest short on (ties: the one the
        /// kit leans on more, then the class's stronger growth stat, then the usual STR … LCK order), else
        /// <see cref="StatPoints.BotPick"/>.</summary>
        public static string Pick(ContentDb c, SaveData s, HeroSave h)
        {
            var target = Target(c, s, h);
            if (target == null) return StatPoints.BotPick(c, h);
            var def = c.TryClass(h.ClassId);
            var affinity = KitAffinity(c, def);
            return Abilities.Unmet(c, s, h, target)
                .OrderByDescending(u => u.Min - u.Have)
                .ThenByDescending(u => Get(affinity, u.Stat))
                .ThenByDescending(u => Get(def?.Growth, u.Stat))
                .First().Stat;
        }

        /// <summary>Spend every roster hero's unspent stat points, one at a time by <see cref="Pick"/> (each point can
        /// change the target). One log line per hero that spent: "Paladin put 3 stat points into Vitality 2, Wisdom 1".</summary>
        public static List<string> Spend(ContentDb c, SaveData s)
        {
            var log = new List<string>();
            foreach (var h in s.Roster)
            {
                if (h == null) continue;
                int n = StatPoints.Unspent(h);
                if (n <= 0) continue;
                var tally = new List<(string Stat, int Count)>();
                int spent = 0;
                for (int i = 0; i < n; i++)
                {
                    string stat = Pick(c, s, h);
                    if (!StatPoints.Spend(h, stat).Ok) break;
                    spent++;
                    int at = tally.FindIndex(t => t.Stat == stat);
                    if (at < 0) tally.Add((stat, 1)); else tally[at] = (stat, tally[at].Count + 1);
                }
                if (spent == 0) continue;
                string into = tally.Count == 1 ? tally[0].Stat : string.Join(", ", tally.Select(t => $"{t.Stat} {t.Count}"));
                log.Add($"{c.TryClass(h.ClassId)?.Name ?? h.ClassId} put {spent} stat point{(spent == 1 ? "" : "s")} into {into}");
            }
            return log;
        }

        private static float Get(Stats g, string stat)
        {
            if (g == null) return 0f;
            switch (stat)
            {
                case "Strength": return g.Strength;
                case "Vitality": return g.Vitality;
                case "Agility": return g.Agility;
                case "Speed": return g.Speed;
                case "Intelligence": return g.Intelligence;
                case "Wisdom": return g.Wisdom;
                case "Luck": return g.Luck;
                default: return 0f;
            }
        }
    }
}
