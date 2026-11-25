using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>A hero's skill hexagon as the game reads it (GG-T1057, docs/design/skill-hexagon/PLAN.md): the nodes it owns
    /// and the stats its owned passives give it. A hero with no <see cref="HeroSave.HexNodes"/> yet (null or empty: a new
    /// or older save) stands on its class Start path from skillhex.json. Buying lives in <see cref="SkillHexProgress"/>.</summary>
    public static class HeroBoard
    {
        private static readonly ConditionalWeakTable<SkillHexDef, SkillHexBoard> Boards = new ConditionalWeakTable<SkillHexDef, SkillHexBoard>();

        /// <summary>The loaded board, built once per <see cref="SkillHexDef"/>; null when the content has none.</summary>
        public static SkillHexBoard Board(ContentDb c) =>
            c?.SkillHex == null ? null : Boards.GetValue(c.SkillHex, def => new SkillHexBoard(def));

        /// <summary>The nodes this hero owns: its saved ones, or its class Start path while it has none saved.</summary>
        public static SkillHexOwned Owned(ContentDb c, HeroSave h)
        {
            if (h == null) return new SkillHexOwned();
            if (h.HexNodes != null && h.HexNodes.Count > 0) return SkillHexProgress.Owned(h);
            var board = Board(c);
            return board == null ? new SkillHexOwned() : SkillHexOwned.Starting(board, h.ClassId);
        }

        /// <summary>The stats this hero's owned passives add up to (VIT, STR, AGI, SPD, WIS, INT; unknown ids ignored).
        /// All zero when there is no board.</summary>
        public static Stats BoardStats(ContentDb c, HeroSave h)
        {
            var s = new Stats();
            var board = Board(c);
            if (board == null || h == null) return s;
            foreach (var kv in board.StatTotals(Owned(c, h).Nodes))
            {
                switch (kv.Key?.ToUpperInvariant())
                {
                    case "VIT": s.Vitality += kv.Value; break;
                    case "STR": s.Strength += kv.Value; break;
                    case "AGI": s.Agility += kv.Value; break;
                    case "SPD": s.Speed += kv.Value; break;
                    case "WIS": s.Wisdom += kv.Value; break;
                    case "INT": s.Intelligence += kv.Value; break;
                }
            }
            return s;
        }

        /// <summary>The abilities this hero owns, in order: its <see cref="HeroSave.Known"/> list, then any ability on a board
        /// node it owns that isn't known yet (its class Start path before it has joined; buying a node already teaches it,
        /// <see cref="SkillHexProgress"/>). These are what the Hero screen's 5-slot bar picks from (GG-T1043).</summary>
        public static List<string> OwnedAbilities(ContentDb c, HeroSave h)
        {
            var ids = new List<string>();
            if (h == null) return ids;
            foreach (var id in h.Known) if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
            var board = Board(c);
            if (board == null) return ids;
            foreach (var pt in Owned(c, h).Nodes)
            {
                var n = board.Node(pt);
                if (n != null && n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability) && !ids.Contains(n.Ability))
                    ids.Add(n.Ability);
            }
            return ids;
        }

        /// <summary>Whether <paramref name="abilityId"/> is one of <see cref="OwnedAbilities"/>.</summary>
        public static bool OwnsAbility(ContentDb c, HeroSave h, string abilityId) =>
            h != null && !string.IsNullOrEmpty(abilityId) && (h.Known.Contains(abilityId) || OwnedAbilities(c, h).Contains(abilityId));

        /// <summary>Whether <paramref name="s"/> adds nothing.</summary>
        public static bool IsZero(Stats s) =>
            s == null || (s.Strength == 0f && s.Vitality == 0f && s.Agility == 0f && s.Speed == 0f
                          && s.Intelligence == 0f && s.Wisdom == 0f && s.Luck == 0f);
    }
}
