using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GridGame.Core.Content
{
    /// <summary>The skill hexagon (skillhex.json, GG-T1022, docs/design/skill-hexagon/PLAN.md): the one board every hero is
    /// built on. Six stat corners, six family wedges between them, a hexagonal lattice of <see cref="Radius"/> in axial
    /// coordinates where every point with (Q - R) mod 3 == 0 is an ability slot and every other point a stat passive, and
    /// every hero as a starting path on it. The schema is docs/design/skill-hexagon/skillhex.schema.json; the rules are
    /// tools/skillhex/validate.py's, ported to <see cref="SkillHexContent.Validate"/>. Nothing in the game reads it yet
    /// (GG-T1023 adds the runtime).</summary>
    public sealed class SkillHexDef
    {
        /// <summary>The editor hint at the top of the file; ignored.</summary>
        [JsonProperty("$schema")] public string Schema;
        /// <summary>Bumped whenever a change needs a save migration (moved or removed nodes).</summary>
        public int Version;
        /// <summary>Lattice radius N: every point with |Q|, |R| and |Q+R| at most N exists (N=16 gives 817 points).</summary>
        public int Radius;
        /// <summary>The six identity stats, clockwise from the top corner.</summary>
        public List<SkillHexStatDef> Stats = new List<SkillHexStatDef>();
        /// <summary>Family k is the wedge from corner k (From) to corner k+1 (To).</summary>
        public List<SkillHexFamilyDef> Families = new List<SkillHexFamilyDef>();
        public SkillHexCostsDef Costs = new SkillHexCostsDef();
        /// <summary>Every lattice point exactly once.</summary>
        public List<SkillHexNodeDef> Nodes = new List<SkillHexNodeDef>();
        public List<SkillHexHeroDef> Heroes = new List<SkillHexHeroDef>();

        /// <summary>Whether (q, r) is an ability slot: (Q - R) mod 3 == 0.</summary>
        public static bool IsSlot(int q, int r) => ((q - r) % 3 + 3) % 3 == 0;

        /// <summary>Whether (q, r) lies on a lattice of radius n.</summary>
        public static bool OnLattice(int q, int r, int n) => Math.Abs(q) <= n && Math.Abs(r) <= n && Math.Abs(q + r) <= n;

        /// <summary>The six axial neighbour directions.</summary>
        public static readonly (int Q, int R)[] Directions = { (1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1) };
    }

    public sealed class SkillHexStatDef
    {
        /// <summary>One of <see cref="SkillHexContent.StatIds"/>.</summary>
        public string Id;
        /// <summary>#rrggbb.</summary>
        public string Color;
    }

    public sealed class SkillHexFamilyDef
    {
        public string Id;
        public string Name;
        public string From;
        public string To;
    }

    public sealed class SkillHexCostsDef
    {
        /// <summary>Skill points to take a passive node.</summary>
        public int Passive;
        /// <summary>Skill points to take an ability node.</summary>
        public int Ability;
        /// <summary>Route weight the planner and bots give to walking through an ability you did not aim for.</summary>
        public int PlannerPassThroughAbility;
    }

    /// <summary>None means the file left Kind out; Validate reports it.</summary>
    public enum SkillHexNodeKind { None, Ability, Passive, Open }

    public sealed class SkillHexNodeDef
    {
        public int Q;
        public int R;
        public SkillHexNodeKind Kind;
        /// <summary>abilities.json id; Kind Ability only.</summary>
        public string Ability;
        /// <summary>Passive bonus, e.g. {"VIT": 2}; Kind Passive only.</summary>
        public Dictionary<string, int> Stats;
        /// <summary>Designer note, e.g. what an Open slot is reserved for.</summary>
        public string Note;
    }

    /// <summary>None means the file left Status out; Validate reports it.</summary>
    public enum SkillHexHeroStatus { None, Placed, Cut, New }

    public sealed class SkillHexHeroDef
    {
        /// <summary>classes.json id; null only for a <see cref="SkillHexHeroStatus.New"/> slot waiting for a hero.</summary>
        public string Class;
        public string Name;
        /// <summary>4-point stat recipe that places the hero on the stat hexagon; null only for a Cut hero.</summary>
        public Dictionary<string, int> Recipe;
        public SkillHexHeroStatus Status;
        /// <summary>abilities.json id of the move only this hero starts with.</summary>
        public string Signature;
        /// <summary>Lattice points [Q, R] this hero owns when it joins: a connected path through its starting moves.</summary>
        public List<int[]> Start = new List<int[]>();
        public string Note;
    }

    /// <summary>tools/skillhex/validate.py's checks on <see cref="ContentDb.SkillHex"/> (GG-T1022). Problems start with
    /// "skillhex:". The script's warnings (a Signature shared with another class's kit, a Signature far from its hero's
    /// spot) are not ported: Validate has no warning channel, and the script stays the place to see them.</summary>
    public static class SkillHexContent
    {
        public static readonly string[] StatIds = { "VIT", "STR", "AGI", "SPD", "WIS", "INT" };
        public const int RecipePoints = 4;

        public static void Validate(ContentDb db, SkillHexDef hex, List<string> p)
        {
            if (hex == null) return;
            void Err(string m) => p.Add("skillhex: " + m);

            if (hex.Version < 1) Err($"Version {hex.Version} must be at least 1");
            if (hex.Costs == null) Err("Costs is missing");
            else if (hex.Costs.Passive < 0 || hex.Costs.Ability < 0 || hex.Costs.PlannerPassThroughAbility < 0) Err("Costs can't be negative");

            var stats = ValidateStatsFamilies(hex, Err);
            var statSet = new HashSet<string>(stats.Count > 0 ? stats : StatIds.ToList(), StringComparer.Ordinal);
            var placed = ValidateNodes(db, hex, statSet, Err, out var points);
            ValidateHeroes(db, hex, points, placed, Err);
        }

        private static List<string> ValidateStatsFamilies(SkillHexDef hex, Action<string> err)
        {
            var stats = (hex.Stats ?? new List<SkillHexStatDef>()).Where(s => s != null).Select(s => s.Id).ToList();
            if (stats.Count != 6) err($"Stats: expected 6, found {stats.Count}");
            var dupStats = stats.GroupBy(s => s ?? "").Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupStats.Count > 0) err($"Stats: duplicate ids {string.Join(", ", dupStats)}");
            foreach (var s in stats)
                if (!StatIds.Contains(s)) err($"Stats: unknown stat '{s}' (use {string.Join("/", StatIds)})");
            var fams = (hex.Families ?? new List<SkillHexFamilyDef>()).Where(f => f != null).ToList();
            if (fams.Count != 6) err($"Families: expected 6, found {fams.Count}");
            var dupFams = fams.GroupBy(f => f.Id ?? "").Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupFams.Count > 0) err($"Families: duplicate ids {string.Join(", ", dupFams)}");
            if (stats.Count == 6)
                for (int k = 0; k < Math.Min(6, fams.Count); k++)
                {
                    var f = fams[k];
                    string from = stats[k], to = stats[(k + 1) % 6];
                    if (f.From != from || f.To != to)
                        err($"Families[{k}] {f.Id}: runs {f.From}->{f.To}, corner order needs {from}->{to}");
                }
            return stats;
        }

        /// <summary>Returns where each ability id is placed (in-lattice Ability nodes); <paramref name="points"/> is the
        /// lattice, empty when the Radius is bad.</summary>
        private static Dictionary<string, List<(int, int)>> ValidateNodes(ContentDb db, SkillHexDef hex, HashSet<string> stats,
            Action<string> err, out HashSet<(int, int)> points)
        {
            var placed = new Dictionary<string, List<(int, int)>>(StringComparer.OrdinalIgnoreCase);
            points = new HashSet<(int, int)>();
            int n = hex.Radius;
            if (n < 2) { err($"Radius: expected an integer >= 2, found {n}"); return placed; }
            for (int q = -n; q <= n; q++)
                for (int r = -n; r <= n; r++)
                    if (Math.Abs(q + r) <= n) points.Add((q, r));

            var seen = new Dictionary<(int, int), int>();
            var nodes = hex.Nodes ?? new List<SkillHexNodeDef>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null) { err($"Nodes[{i}]: null entry"); continue; }
                var pt = (node.Q, node.R);
                var w = $"node {Fmt(pt)}";
                seen[pt] = seen.TryGetValue(pt, out var c) ? c + 1 : 1;
                if (!points.Contains(pt)) { err($"{w}: outside the radius-{n} lattice"); continue; }
                var kind = node.Kind;
                bool slot = SkillHexDef.IsSlot(node.Q, node.R);
                if ((kind == SkillHexNodeKind.Ability || kind == SkillHexNodeKind.Open) && !slot)
                    err($"{w}: {kind} on a passive point ((Q-R) mod 3 != 0)");
                else if (kind == SkillHexNodeKind.Passive && slot)
                    err($"{w}: Passive on an ability slot ((Q-R) mod 3 == 0)");
                else if (kind == SkillHexNodeKind.None)
                    err($"{w}: needs a Kind (Ability, Passive or Open)");

                if (kind == SkillHexNodeKind.Ability)
                {
                    var aid = node.Ability;
                    if (string.IsNullOrEmpty(aid)) err($"{w}: Ability node without an Ability id");
                    else
                    {
                        if (!placed.TryGetValue(aid, out var at)) placed[aid] = at = new List<(int, int)>();
                        at.Add(pt);
                        if (db.AbilityAliases.TryGetValue(aid, out var into))
                            err($"{w}: ability '{aid}' is an alias stub (MergedInto '{into}'); place '{into}' instead");
                        else if (!db.Abilities.ContainsKey(aid))
                            err($"{w}: ability '{aid}' is not in abilities.json");
                    }
                }
                else if (node.Ability != null) err($"{w}: Ability id on a {kind} node");

                if (kind == SkillHexNodeKind.Passive)
                {
                    if (node.Stats == null || node.Stats.Count == 0) err($"{w}: Passive without Stats");
                    else
                        foreach (var kv in node.Stats)
                        {
                            if (!stats.Contains(kv.Key)) err($"{w}: passive stat '{kv.Key}' is not one of the six Stats");
                            if (kv.Value < 1) err($"{w}: passive {kv.Key} value {kv.Value} must be a positive integer");
                        }
                }
                else if (node.Stats != null) err($"{w}: Stats on a {kind} node");
            }
            foreach (var kv in seen.OrderBy(k => k.Key))
                if (kv.Value > 1) err($"node {Fmt(kv.Key)}: listed {kv.Value} times");
            var missing = points.Where(pt => !seen.ContainsKey(pt)).OrderBy(pt => pt).ToList();
            if (missing.Count > 0)
            {
                var more = missing.Count > 20 ? $" (+{missing.Count - 20} more)" : "";
                err($"{missing.Count} lattice point(s) missing: {string.Join(" ", missing.Take(20).Select(Fmt))}{more}");
            }
            foreach (var kv in placed.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                if (kv.Value.Count > 1) err($"ability '{kv.Key}' placed {kv.Value.Count} times: {string.Join(" ", kv.Value.Select(Fmt))}");
            return placed;
        }

        private static void ValidateHeroes(ContentDb db, SkillHexDef hex, HashSet<(int, int)> points,
            Dictionary<string, List<(int, int)>> placed, Action<string> err)
        {
            var heroes = (hex.Heroes ?? new List<SkillHexHeroDef>()).Where(h => h != null).ToList();
            foreach (var g in heroes.Where(h => !string.IsNullOrEmpty(h.Class)).GroupBy(h => h.Class, StringComparer.OrdinalIgnoreCase))
                if (g.Count() > 1) err($"hero class '{g.Key}' listed {g.Count()} times");
            var sigs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in heroes)
            {
                var status = h.Status;
                var w = $"hero '{h.Class ?? h.Name ?? "?"}'";
                if (status == SkillHexHeroStatus.None) err($"{w}: needs a Status (Placed, Cut or New)");
                if (h.Class == null)
                {
                    if (status != SkillHexHeroStatus.New) err($"{w}: Class is null but Status is {status} (only New heroes may lack a class)");
                }
                else if (!db.Classes.ContainsKey(h.Class)) err($"{w}: class '{h.Class}' is not in classes.json");

                if (h.Recipe == null)
                {
                    if (status != SkillHexHeroStatus.Cut) err($"{w}: {status} hero without a Recipe");
                }
                else
                {
                    int total = h.Recipe.Values.Sum();
                    if (total != RecipePoints) err($"{w}: Recipe sums to {total}, not {RecipePoints}");
                    foreach (var kv in h.Recipe)
                    {
                        if (!StatIds.Contains(kv.Key)) err($"{w}: Recipe stat '{kv.Key}' is not one of the six Stats");
                        if (kv.Value < 1 || kv.Value > RecipePoints) err($"{w}: Recipe {kv.Key} value {kv.Value} must be 1..{RecipePoints}");
                    }
                }

                var cells = new HashSet<(int, int)>();
                foreach (var c in h.Start ?? new List<int[]>())
                {
                    if (c == null || c.Length != 2) { err($"{w}: bad Start entry (needs [Q, R])"); continue; }
                    cells.Add((c[0], c[1]));
                }
                var off = cells.Where(pt => !points.Contains(pt)).OrderBy(pt => pt).ToList();
                if (off.Count > 0) err($"{w}: Start points outside the lattice: {string.Join(" ", off.Select(Fmt))}");
                if (cells.Count == 0 && status != SkillHexHeroStatus.New) err($"{w}: {status} hero with an empty Start");
                var stray = Unreached(new HashSet<(int, int)>(cells.Where(points.Contains)));
                if (stray.Count > 0) err($"{w}: Start is not connected; cut off from the rest: {string.Join(" ", stray.Select(Fmt))}");

                var sig = h.Signature;
                if (sig == null)
                {
                    if (status != SkillHexHeroStatus.New) err($"{w}: {status} hero without a Signature");
                    continue;
                }
                if (!sigs.TryGetValue(sig, out var owners)) sigs[sig] = owners = new List<string>();
                owners.Add(h.Class ?? h.Name);
                if (!db.Abilities.ContainsKey(sig) && !db.AbilityAliases.ContainsKey(sig))
                    err($"{w}: Signature '{sig}' is not in abilities.json");
                if (!placed.TryGetValue(sig, out var at) || at.Count == 0)
                    err($"{w}: Signature '{sig}' is not placed on the board");
                else if (!at.Any(cells.Contains))
                    err($"{w}: Start does not hold its Signature '{sig}' at {Fmt(at[0])}");
            }
            foreach (var kv in sigs.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                if (kv.Value.Count > 1) err($"Signature '{kv.Key}' shared by {string.Join(", ", kv.Value)}");
        }

        /// <summary>The cells not reached from the first one through lattice adjacency (empty when connected).</summary>
        public static List<(int, int)> Unreached(HashSet<(int, int)> cells)
        {
            if (cells.Count == 0) return new List<(int, int)>();
            var start = cells.First();
            var seen = new HashSet<(int, int)> { start };
            var stack = new Stack<(int, int)>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var (q, r) = stack.Pop();
                foreach (var d in SkillHexDef.Directions)
                {
                    var nb = (q + d.Q, r + d.R);
                    if (cells.Contains(nb) && seen.Add(nb)) stack.Push(nb);
                }
            }
            return cells.Where(c => !seen.Contains(c)).OrderBy(c => c).ToList();
        }

        private static string Fmt((int, int) pt) => $"[{pt.Item1}, {pt.Item2}]";
    }
}
