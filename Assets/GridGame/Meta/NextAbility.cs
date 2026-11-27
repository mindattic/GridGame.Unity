using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>A hero's next ability on the skill hexagon (GG-T1404): the nearest ability node it doesn't own whose ability
    /// it doesn't know, by <see cref="SkillHexBoard.CheapestRoute"/> from <see cref="HeroBoard.Owned"/> (its class Start path
    /// before it has joined). Nearest is the fewest SP, then the shortest route, then (Q, R). The level-up fanfare and the
    /// results show it per hero; their Learn button opens the Board with the route preselected.</summary>
    public static class NextAbility
    {
        public sealed class Pick
        {
            public AbilityDef Ability;
            public (int Q, int R) Node;
            public List<(int Q, int R)> Route;
            public int Sp;
        }

        /// <summary>The hero's next ability, or null (no board, a domesticated monster, nothing left to reach).</summary>
        public static Pick For(ContentDb c, HeroSave h)
        {
            var board = HeroBoard.Board(c);
            if (board == null || h == null || h.Domesticated) return null;
            var owned = HeroBoard.Owned(c, h).Nodes;
            if (owned.Count == 0) return null;
            var have = new HashSet<string>(board.OwnedAbilities(owned));
            if (h.Known != null) have.UnionWith(h.Known);
            Pick best = null;
            foreach (var n in board.Def.Nodes ?? new List<SkillHexNodeDef>())
            {
                if (n == null || n.Kind != SkillHexNodeKind.Ability || string.IsNullOrEmpty(n.Ability) || have.Contains(n.Ability)) continue;
                var pt = (n.Q, n.R);
                if (owned.Contains(pt)) continue;
                var a = c.TryAbility(n.Ability);
                if (a == null) continue;
                var route = board.CheapestRoute(owned, pt);
                if (route == null || route.Count == 0) continue;
                int sp = board.SpCost(route, owned);
                if (best == null || sp < best.Sp || (sp == best.Sp && (route.Count < best.Route.Count
                    || (route.Count == best.Route.Count && pt.CompareTo(best.Node) < 0))))
                    best = new Pick { Ability = a, Node = pt, Route = route, Sp = sp };
            }
            return best;
        }

        /// <summary>"Next ability: X, N SP", or null when there is none.</summary>
        public static string Line(ContentDb c, HeroSave h)
        {
            var p = For(c, h);
            return p == null ? null : $"Next ability: {p.Ability.Name}, {p.Sp} SP";
        }

        /// <summary>The badge's words on the Heroes hub tile and a roster card (GG-T1407).</summary>
        public const string ReadyLabel = "New ability ready";

        /// <summary>The hero's banked SP covers the route to its next ability (GG-T1407).</summary>
        public static bool Affordable(ContentDb c, SaveData s, string heroId) => AffordablePick(c, s?.Hero(heroId)) != null;

        /// <summary>"New ability ready" (GG-T1407): <see cref="Affordable"/>, and the player hasn't opened the hero's Board
        /// since this ability became the one in reach (<see cref="HeroSave.AbilityReadySeen"/>).</summary>
        public static bool Ready(ContentDb c, SaveData s, string heroId)
        {
            var h = s?.Hero(heroId);
            var p = AffordablePick(c, h);
            return p != null && p.Ability.Id != h.AbilityReadySeen;
        }

        /// <summary>Any party hero is <see cref="Ready"/>: the Heroes nav tab and hub tile badge.</summary>
        public static bool ReadyInParty(ContentDb c, SaveData s) => s?.Party != null && s.Party.Any(id => Ready(c, s, id));

        /// <summary>The hero's Board is open: its ready ability is seen and its badge clears. True when that changed the save.</summary>
        public static bool MarkSeen(ContentDb c, HeroSave h)
        {
            var p = AffordablePick(c, h);
            if (p == null || p.Ability.Id == h.AbilityReadySeen) return false;
            h.AbilityReadySeen = p.Ability.Id;
            return true;
        }

        private static Pick AffordablePick(ContentDb c, HeroSave h)
        {
            if (h == null || h.SkillPoints <= 0 || h.Domesticated) return null;
            var p = For(c, h);
            return p != null && p.Sp <= h.SkillPoints ? p : null;
        }
    }
}
