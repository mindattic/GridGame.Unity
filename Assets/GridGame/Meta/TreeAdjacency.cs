using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Archetype adjacency for cross-tree specialization (§25.6.2.2, GG-T135), layered on GG-T51's home-tree threshold
    /// (<see cref="TalentConfig"/>): which trees are neighbours, how far apart two trees are, and what that distance does
    /// to the off-tree premium. Pure data over tree ids — no <c>ContentDb</c>, no <c>HeroSave</c> — so <see cref="Talents"/>
    /// asks it with the ids of the trees a hero already holds (home + opened) and the tree it wants to open.
    ///
    /// <para>The board is a ring in role-kinship order (GG-T79's ring, with GG-T84's Rogue between Warrior and Ranger):
    /// Guardian - Warrior - Rogue - Ranger - Alchemist - Mage - Cleric - (back to Guardian). A tree's distance is measured
    /// from the <b>nearest tree the hero already holds</b>, so opening a stepping-stone tree brings the next one closer:
    /// a Warrior can't open Mage directly (3 steps), but Warrior → Guardian (plate, 1 step) puts Mage 2 steps away
    /// (via Cleric) — the fireball-casting barbarian in plate — and Warrior → Guardian → Cleric puts it 1 step away.</para>
    ///
    /// <para>GG-T979: a hero on a family tree plays on the family board instead: <see cref="FamilyRing"/>, the families of
    /// families.json in their <see cref="FamilyDef.Ring"/> order (Vanguard - Blade - Brawn - Shadow - Wild - Machina - Arcane -
    /// Occult - Grace - back to Vanguard). Every walk takes the board as an optional <c>ring</c> (null = the role
    /// <see cref="Ring"/>); <see cref="ExtraEdges"/>, <see cref="MaxOpenDistance"/> and <see cref="PremiumPerExtraStep"/>
    /// apply on either board. <see cref="Talents"/> picks the board with <see cref="BoardFor"/>.</para>
    ///
    /// <para>Static fields rather than consts, like <see cref="TalentConfig"/>, so a balance pass or a test can retune them.</para>
    /// </summary>
    public static class TreeAdjacency
    {
        /// <summary>The board, as a ring of tree ids: each tree neighbours the one before and after it (the last wraps to the first).</summary>
        public static string[] Ring = { "guardian", "warrior", "rogue", "ranger", "alchemist", "mage", "cleric" };

        /// <summary>Extra neighbour pairs beyond the ring (chords). Empty by default; a balance pass can add a bridge here.</summary>
        public static List<(string A, string B)> ExtraEdges = new List<(string A, string B)>();

        /// <summary>The farthest a tree may be (steps from the nearest held tree) and still be opened directly. Farther trees
        /// must be routed to: open a tree in between first.</summary>
        public static int MaxOpenDistance = 2;

        /// <summary>Extra skill points per step beyond the first, on top of GG-T51's flat <see cref="TalentConfig.OffTreePremium"/>.
        /// A neighbour (1 step) costs exactly the flat premium; 2 steps costs one more per node.</summary>
        public static int PremiumPerExtraStep = 1;

        /// <summary>Distance returned for a tree that isn't on the board, or isn't connected to any held tree.</summary>
        public const int Unreachable = int.MaxValue;

        /// <summary>GG-T979: the family board, built from families.json: every family id in <see cref="FamilyDef.Ring"/>
        /// order (ties by id). Empty when the content has no families.</summary>
        public static string[] FamilyRing(ContentDb c) =>
            c?.Families == null ? Array.Empty<string>()
                : c.Families.Where(f => f != null && !string.IsNullOrEmpty(f.Id))
                    .OrderBy(f => f.Ring).ThenBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(f => f.Id).ToArray();

        /// <summary>GG-T979: the board a hero whose home tree is <paramref name="homeTreeId"/> plays on: the
        /// <see cref="FamilyRing"/> when that tree is a family's, else the role <see cref="Ring"/>.</summary>
        public static string[] BoardFor(ContentDb c, string homeTreeId) =>
            c != null && !string.IsNullOrEmpty(homeTreeId) && c.TryFamily(homeTreeId) != null ? FamilyRing(c) : Ring;

        /// <summary>The trees one step from <paramref name="treeId"/> on <paramref name="ring"/> (null = <see cref="Ring"/>).</summary>
        public static IEnumerable<string> Neighbours(string treeId, string[] ring = null)
        {
            if (string.IsNullOrEmpty(treeId)) yield break;
            ring = ring ?? Ring ?? Array.Empty<string>();
            int i = Array.FindIndex(ring, r => Same(r, treeId));
            if (i >= 0 && ring.Length > 1)
            {
                yield return ring[(i + ring.Length - 1) % ring.Length];
                if (ring.Length > 2) yield return ring[(i + 1) % ring.Length];
            }
            foreach (var (a, b) in ExtraEdges ?? new List<(string A, string B)>())
            {
                if (Same(a, treeId)) yield return b;
                else if (Same(b, treeId)) yield return a;
            }
        }

        public static bool Adjacent(string a, string b, string[] ring = null) => !Same(a, b) && Neighbours(a, ring).Any(n => Same(n, b));

        /// <summary>Fewest steps from <paramref name="from"/> to <paramref name="to"/> (0 for the same tree), or <see cref="Unreachable"/>.</summary>
        public static int Distance(string from, string to, string[] ring = null) => DistanceFrom(new[] { from }, to, ring);

        /// <summary>Fewest steps from the nearest of <paramref name="held"/> to <paramref name="target"/> (a breadth-first walk
        /// that may pass through trees the hero doesn't hold), or <see cref="Unreachable"/>. 0 when the target is itself held.</summary>
        public static int DistanceFrom(IEnumerable<string> held, string target, string[] ring = null)
        {
            if (string.IsNullOrEmpty(target) || held == null) return Unreachable;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var frontier = new List<string>();
            foreach (var h in held)
                if (!string.IsNullOrEmpty(h) && seen.Add(h)) frontier.Add(h);
            for (int d = 0; frontier.Count > 0; d++)
            {
                if (frontier.Any(f => Same(f, target))) return d;
                var next = new List<string>();
                foreach (var f in frontier)
                    foreach (var n in Neighbours(f, ring))
                        if (seen.Add(n)) next.Add(n);
                frontier = next;
            }
            return Unreachable;
        }

        /// <summary>Extra points per off-tree node a tree <paramref name="distance"/> steps away adds to GG-T51's flat
        /// premium: 0 for a neighbour (or the home tree), <see cref="PremiumPerExtraStep"/> per step beyond the first.</summary>
        public static int ExtraPremium(int distance) =>
            distance <= 1 || distance == Unreachable ? 0 : (distance - 1) * Math.Max(0, PremiumPerExtraStep);

        /// <summary>The distance surcharge for a node in <paramref name="target"/>, measured from the nearest <i>other</i>
        /// tree in <paramref name="held"/> (so an already-opened target is priced by its link to the rest, not 0).</summary>
        public static int ExtraPremium(IEnumerable<string> held, string target, string[] ring = null)
        {
            int d = DistanceFrom((held ?? Enumerable.Empty<string>()).Where(h => !Same(h, target)), target, ring);
            return ExtraPremium(d);
        }

        /// <summary>Why <paramref name="target"/> can't be opened from the held trees because it's too far — naming the
        /// tree(s) to route through — or null when it's within <see cref="MaxOpenDistance"/> (or held already, or not on the
        /// board at all: an unknown tree is left to GG-T51's slot rule rather than locked out here).</summary>
        public static string WhyTooFar(IEnumerable<string> held, string target, Func<string, string> name = null, string[] ring = null)
        {
            var heldList = (held ?? Enumerable.Empty<string>()).Where(h => !string.IsNullOrEmpty(h)).ToList();
            if (heldList.Count == 0 || string.IsNullOrEmpty(target)) return null;
            int d = DistanceFrom(heldList, target, ring);
            if (d == Unreachable || d <= MaxOpenDistance) return null;
            name ??= id => id;
            var via = Stepping(heldList, target, ring).Select(name).ToList();
            string route = via.Count == 0 ? "a tree in between" : string.Join(" or ", via);
            return $"{name(target)} is {d} steps away: open {route} first";
        }

        /// <summary>The unheld trees one step from a held tree that bring <paramref name="target"/> closer — where to route.</summary>
        public static List<string> Stepping(IEnumerable<string> held, string target, string[] ring = null)
        {
            var heldList = (held ?? Enumerable.Empty<string>()).ToList();
            int d = DistanceFrom(heldList, target, ring);
            if (d == Unreachable || d <= 1) return new List<string>();
            return heldList.SelectMany(h => Neighbours(h, ring))
                .Where(n => !heldList.Any(h => Same(h, n)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(n => Distance(n, target, ring) < d)
                .ToList();
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
