using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>What a party is short of, as the board bot reads it (GG-T1047).</summary>
    public enum PartyNeed { Heal, Cleanse, Damage, Area, Support, Control }

    /// <summary>
    /// The bots' skill-hexagon spender (GG-T1047, docs/design/skill-hexagon/PLAN.md step 7), used by the camp routine (the
    /// MetaBot: balance soaks, auto-manage, every HeroBot campaign): each roster hero joins the board on its class Start
    /// path, picks a target ability by what the party lacks (<see cref="Needs"/>) over the planner cost to reach it, and walks
    /// the board's cheapest route there one node at a time with its banked skill points, banking the rest for the next
    /// visit. A reached target picks the next one. Replaces BotStatSpend and the talent-tree auto-spend.
    /// Deterministic per seed: the same save and seed buy the same nodes; the seed only breaks ties between equal picks.
    /// </summary>
    public static class BoardBot
    {
        /// <summary>How much each need weighs while the party has none of it; each ability already covering it halves
        /// the pull again (weight / (1 + have)).</summary>
        public static readonly IReadOnlyDictionary<PartyNeed, float> Importance = new Dictionary<PartyNeed, float>
        {
            [PartyNeed.Heal] = 3f, [PartyNeed.Damage] = 2f, [PartyNeed.Area] = 2f,
            [PartyNeed.Cleanse] = 1.5f, [PartyNeed.Support] = 1.5f, [PartyNeed.Control] = 1.5f,
        };

        /// <summary>Value every ability has to its owner, so a party with every need met still grows.</summary>
        public const float BaseValue = 0.5f;

        /// <summary>Text in a camp log line that records a board purchase, the same marker the talent spend wrote.</summary>
        public const string Marker = " SP on ";

        private static readonly (int Q, int R) NoTarget = (int.MinValue, int.MinValue);

        /// <summary>The needs one ability covers.</summary>
        public static IEnumerable<PartyNeed> Covers(AbilityDef a)
        {
            if (a == null) yield break;
            bool foes = a.Filter == TargetFilter.EnemyOnly || a.Shape == TargetShape.AllEnemies;
            if (a.BaseHeal > 0) yield return PartyNeed.Heal;
            if (a.RemovesDebuffs) yield return PartyNeed.Cleanse;
            if (a.BaseDamage > 0)
            {
                yield return PartyNeed.Damage;
                if (a.Shape != TargetShape.SingleActor && a.Shape != TargetShape.Self && a.Shape != TargetShape.SingleTile)
                    yield return PartyNeed.Area;
            }
            if (!string.IsNullOrEmpty(a.ApplyBuff) && !foes) yield return PartyNeed.Support;
            if ((!string.IsNullOrEmpty(a.ApplyBuff) && foes) || a.Knockback > 0 || a.Pull > 0 || a.Pushback > 0f)
                yield return PartyNeed.Control;
        }

        /// <summary>How many of the party's known abilities cover each need (every need present, zero when uncovered).</summary>
        public static Dictionary<PartyNeed, int> Coverage(ContentDb c, SaveData s)
        {
            var have = Enum.GetValues(typeof(PartyNeed)).Cast<PartyNeed>().ToDictionary(n => n, n => 0);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in s?.Party ?? new List<string>())
            {
                var h = s.Hero(id);
                if (h == null) continue;
                var known = (h.Known ?? new List<string>()).ToList();
                var board = HeroBoard.Board(c);
                if (board != null) known.AddRange(board.OwnedAbilities(HeroBoard.Owned(c, h).Nodes));
                foreach (var ab in known)
                    if (ab != null && seen.Add(ab))
                        foreach (var need in Covers(c.TryAbility(ab))) have[need]++;
            }
            return have;
        }

        /// <summary>The weight of each need for the party right now: <see cref="Importance"/> / (1 + abilities covering it).</summary>
        public static Dictionary<PartyNeed, float> Needs(ContentDb c, SaveData s) =>
            Coverage(c, s).ToDictionary(kv => kv.Key, kv => Importance[kv.Key] / (1 + kv.Value));

        /// <summary>What an ability is worth to a party with these <paramref name="needs"/>.</summary>
        public static float Value(AbilityDef a, IReadOnlyDictionary<PartyNeed, float> needs) =>
            a == null ? 0f : BaseValue + Covers(a).Distinct().Sum(n => needs.TryGetValue(n, out var w) ? w : 0f);

        /// <summary>The ability node this hero should head for next, or null when every reachable ability is owned (or there
        /// is no board): the best <see cref="Value"/> per planner cost to reach it, ties by the seeded hash, then (Q, R).</summary>
        public static (int Q, int R)? PickTarget(ContentDb c, SaveData s, HeroSave h, int seed, IReadOnlyDictionary<PartyNeed, float> needs = null)
        {
            var board = HeroBoard.Board(c);
            if (board == null || h == null) return null;
            needs = needs ?? Needs(c, s);
            var owned = HeroBoard.Owned(c, h).Nodes;
            if (owned.Count == 0) return null;
            var wanted = Wanted(c, board, owned, needs);
            var dist = Distances(board, owned, wanted);
            (int Q, int R)? best = null;
            float bestScore = float.MinValue; uint bestTie = 0;
            foreach (var n in board.Def.Nodes)
            {
                if (n == null || n.Kind != SkillHexNodeKind.Ability || string.IsNullOrEmpty(n.Ability)) continue;
                var pt = (n.Q, n.R);
                if (owned.Contains(pt) || !board.Contains(pt) || (h.Known != null && h.Known.Contains(n.Ability))) continue;
                var a = c.TryAbility(n.Ability);
                if (a == null) continue;
                int reach = int.MaxValue;
                foreach (var nb in board.Neighbors(pt))
                {
                    if (owned.Contains(nb)) { reach = 0; break; }
                    if (dist.TryGetValue(nb, out var d) && d < reach) reach = d;
                }
                if (reach == int.MaxValue) continue;
                float score = Value(a, needs) / (1f + reach + board.Costs.Ability);
                uint tie = Hash(seed, h.ClassId, n.Ability);
                if (best == null || score > bestScore + 1e-6f
                    || (Math.Abs(score - bestScore) <= 1e-6f && (tie > bestTie || (tie == bestTie && pt.CompareTo(best.Value) < 0))))
                {
                    best = pt; bestScore = score; bestTie = tie;
                }
            }
            return best;
        }

        /// <summary>Spends every roster hero's banked skill points on the board (party heroes first, then the rest, in roster
        /// order); a hero not on the board yet joins it first (free). Returns one log line per purchase.</summary>
        public static List<string> Spend(ContentDb c, SaveData s, int seed)
        {
            var log = new List<string>();
            var board = HeroBoard.Board(c);
            if (board == null || s == null) return log;
            var order = s.Party.Select(s.Hero).Where(h => h != null)
                .Concat(s.Roster.Where(h => h != null && !s.Party.Contains(h.ClassId))).ToList();
            foreach (var h in order)
            {
                string name = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
                if ((h.HexNodes == null || h.HexNodes.Count == 0) && board.StartOf(h.ClassId).Count > 0)
                {
                    var j = SkillHexProgress.Join(c, h);
                    if (j.Ok) log.Add($"{name} joined the skill hexagon");
                }
                if (h.HexNodes == null || h.HexNodes.Count == 0) continue;
                // Bounded by the board: every pass buys one node or stops.
                for (int guard = 0; guard < 1000 && h.SkillPoints > 0; guard++)
                {
                    var target = PickTarget(c, s, h, seed);
                    if (target == null) break;
                    var owned = SkillHexProgress.Owned(h).Nodes;
                    var route = board.CheapestRoute(owned, target.Value, Wanted(c, board, owned, Needs(c, s)));
                    if (route == null || route.Count == 0) break;
                    var step = route[0];
                    int price = board.Price(step);
                    if (price > h.SkillPoints) break;
                    var r = SkillHexProgress.Buy(c, h, step);
                    if (!r.Ok) break;
                    var node = board.Node(step);
                    // GG-T1077: an ability node is how the camp routine learns now, so its line carries the learned marker too.
                    if (node?.Kind == SkillHexNodeKind.Ability)
                        log.Add($"{name}{CampRoutine.LearnedMarker}{c.TryAbility(node.Ability)?.Name ?? node.Ability} for {price}{Marker}the skill hexagon");
                    else
                        log.Add($"{name} spent {price}{Marker}{string.Join(" ", (node?.Stats ?? new Dictionary<string, int>()).Select(kv => $"{kv.Key} +{kv.Value}"))} (skill hexagon)");
                }
            }
            return log;
        }

        /// <summary>A seed for <see cref="Spend"/> from the routine's rng without drawing from it (so the forge rolls a seeded
        /// soak relies on stay put), or from the save's name when there is none.</summary>
        public static int SeedFor(SaveData s, Core.Rng rng) =>
            rng != null ? (int)rng.Clone().NextUInt() : (int)Hash(0, s?.Name ?? "", "");

        /// <summary>Unowned ability nodes covering a need the party has none of: routes may pass through these at the ability
        /// price instead of the planner's pass-through penalty.</summary>
        private static HashSet<(int Q, int R)> Wanted(ContentDb c, SkillHexBoard board, IReadOnlyCollection<(int Q, int R)> owned,
            IReadOnlyDictionary<PartyNeed, float> needs)
        {
            var wanted = new HashSet<(int Q, int R)>();
            foreach (var n in board.Def.Nodes)
            {
                if (n == null || n.Kind != SkillHexNodeKind.Ability || string.IsNullOrEmpty(n.Ability)) continue;
                var pt = (n.Q, n.R);
                if (owned.Contains(pt)) continue;
                if (Covers(c.TryAbility(n.Ability)).Any(need => needs.TryGetValue(need, out var w) && w >= Importance[need] - 1e-6f))
                    wanted.Add(pt);
            }
            return wanted;
        }

        /// <summary>Planner distance from the owned set to every reachable takeable node (by <see cref="SkillHexBoard.StepWeight"/>
        /// with no one target, so every unwanted ability is a pass-through).</summary>
        private static Dictionary<(int Q, int R), int> Distances(SkillHexBoard board, IReadOnlyCollection<(int Q, int R)> owned,
            ICollection<(int Q, int R)> wanted)
        {
            var dist = new Dictionary<(int Q, int R), int>();
            var open = new SortedSet<(int D, int Q, int R)>();
            foreach (var pt in owned)
                if (board.Contains(pt) && dist.TryAdd(pt, 0)) open.Add((0, pt.Q, pt.R));
            while (open.Count > 0)
            {
                var cur = open.Min;
                open.Remove(cur);
                foreach (var nb in board.Neighbors((cur.Q, cur.R)))
                {
                    if (owned.Contains(nb) || !board.IsTakeable(nb)) continue;
                    int d = cur.D + board.StepWeight(nb, NoTarget, wanted);
                    if (dist.TryGetValue(nb, out var old))
                    {
                        if (d >= old) continue;
                        open.Remove((old, nb.Q, nb.R));
                    }
                    dist[nb] = d;
                    open.Add((d, nb.Q, nb.R));
                }
            }
            return dist;
        }

        /// <summary>FNV-1a over the seed and two strings: stable across runs and platforms.</summary>
        private static uint Hash(int seed, string a, string b)
        {
            uint h = 2166136261u;
            void Mix(uint v) { h ^= v; h *= 16777619u; }
            Mix((uint)seed);
            foreach (var ch in a ?? "") Mix(ch);
            Mix(0x1Fu);
            foreach (var ch in b ?? "") Mix(ch);
            return h;
        }
    }
}
