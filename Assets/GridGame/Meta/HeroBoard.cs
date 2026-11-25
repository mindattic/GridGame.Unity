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

        /// <summary>Whether <paramref name="s"/> adds nothing.</summary>
        public static bool IsZero(Stats s) =>
            s == null || (s.Strength == 0f && s.Vitality == 0f && s.Agility == 0f && s.Speed == 0f
                          && s.Intelligence == 0f && s.Wisdom == 0f && s.Luck == 0f);
    }
}
