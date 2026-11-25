using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>A hero's place on the skill hexagon (GG-T1031, docs/design/skill-hexagon/PLAN.md): the nodes it owns
    /// (<see cref="HeroSave.HexNodes"/>), bought with its <see cref="HeroSave.SkillPoints"/> (1 per level, owner
    /// 2026-11-24; every node costs its board price, 1 today). Owning an ability node teaches the ability, so it can go on
    /// the bar. The path is the gate: an ability's Requires do not apply here.</summary>
    public static class SkillHexProgress
    {
        /// <summary>The loaded board, or null when the content has none.</summary>
        public static SkillHexBoard Board(ContentDb c) => c?.SkillHex == null ? null : new SkillHexBoard(c.SkillHex);

        public static string Key((int Q, int R) pt) => $"{pt.Q},{pt.R}";

        public static bool TryParse(string key, out (int Q, int R) pt)
        {
            pt = default;
            var parts = key?.Split(',');
            if (parts == null || parts.Length != 2 || !int.TryParse(parts[0], out var q) || !int.TryParse(parts[1], out var r)) return false;
            pt = (q, r);
            return true;
        }

        /// <summary>The nodes this hero owns (unparsable entries skipped).</summary>
        public static SkillHexOwned Owned(HeroSave h)
        {
            var pts = new List<(int Q, int R)>();
            foreach (var k in h?.HexNodes ?? new List<string>())
                if (TryParse(k, out var pt)) pts.Add(pt);
            return new SkillHexOwned(pts);
        }

        /// <summary>Nodes this hero can buy next, sorted by (Q, R). With nothing owned yet, its class Start path is offered
        /// as one whole: buy it with <see cref="Join"/>.</summary>
        public static List<(int Q, int R)> Frontier(ContentDb c, HeroSave h)
        {
            var board = Board(c);
            if (board == null || h == null) return new List<(int, int)>();
            return board.Frontier(Owned(h).Nodes);
        }

        /// <summary>Gives a hero that owns nothing yet its class Start path, free (and learns every ability on it).</summary>
        public static Result Join(ContentDb c, HeroSave h)
        {
            var board = Board(c);
            if (board == null || h == null) return Result.Fail("No skill hexagon");
            if (h.HexNodes == null) h.HexNodes = new List<string>();
            if (h.HexNodes.Count > 0) return Result.Fail("Already on the board");
            var start = board.StartOf(h.ClassId);
            if (start.Count == 0) return Result.Fail("This hero has no start path");
            foreach (var pt in start.OrderBy(p => p)) Own(c, board, h, pt);
            return Result.Success("Joined the board");
        }

        /// <summary>Buys one node next to one the hero owns, for its price in skill points. Owning an ability node
        /// teaches the ability (its Requires don't gate it).</summary>
        public static Result Buy(ContentDb c, HeroSave h, (int Q, int R) pt)
        {
            var board = Board(c);
            if (board == null || h == null) return Result.Fail("No skill hexagon");
            var owned = Owned(h);
            if (owned.Owns(pt)) return Result.Fail("Already owned");
            if (owned.Count == 0) return Result.Fail("Not on the board yet");
            if (!board.CanTake(owned.Nodes, pt)) return Result.Fail("Not next to a node you own");
            int price = board.Price(pt);
            if (h.SkillPoints < price) return Result.Fail("Not enough skill points");
            h.SkillPoints -= price;
            var n = Own(c, board, h, pt);
            return Result.Success(n?.Kind == SkillHexNodeKind.Ability ? $"Learned {c.TryAbility(n.Ability)?.Name ?? n.Ability}" : "Bought");
        }

        /// <summary>Buys the cheapest route to <paramref name="target"/> when the hero can pay for all of it; nothing changes
        /// otherwise.</summary>
        public static Result BuyRoute(ContentDb c, HeroSave h, (int Q, int R) target)
        {
            var board = Board(c);
            if (board == null || h == null) return Result.Fail("No skill hexagon");
            var owned = Owned(h);
            var route = board.CheapestRoute(owned.Nodes, target);
            if (route == null) return Result.Fail("Can't reach that");
            if (route.Count == 0) return Result.Fail("Already owned");
            int cost = board.SpCost(route, owned.Nodes);
            if (h.SkillPoints < cost) return Result.Fail($"Needs {cost} skill points ({h.SkillPoints} now)");
            foreach (var pt in route)
            {
                var r = Buy(c, h, pt);
                if (!r.Ok) return r;
            }
            return Result.Success($"Bought {route.Count} node{(route.Count == 1 ? "" : "s")}");
        }

        private static SkillHexNodeDef Own(ContentDb c, SkillHexBoard board, HeroSave h, (int Q, int R) pt)
        {
            var key = Key(pt);
            if (!h.HexNodes.Contains(key)) h.HexNodes.Add(key);
            var n = board.Node(pt);
            if (n != null && n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability) && !h.Known.Contains(n.Ability))
                h.Known.Add(n.Ability);
            return n;
        }
    }
}
