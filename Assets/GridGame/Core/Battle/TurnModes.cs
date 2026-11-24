using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// The turn modes a player may pick (bible §2.13, GG-T774): Settings' "Turn mode" row and New Game's lock offer only
    /// <see cref="Available"/>. A mode whose rules aren't built yet throws in <see cref="TurnRules"/>, so it is not listed;
    /// each mode's feature ticket appends it here (and its <c>glossary:turn_mode_*</c> entry) when it lands. A mode's name
    /// and one-line explanation are its glossary entry (Content/glossary.json, words in Content/Text via the Prose tools).
    /// </summary>
    public static class TurnModes
    {
        /// <summary>The modes that are built, in the order the pickers cycle them. PartyWindow first (the default).</summary>
        public static readonly IReadOnlyList<TurnMode> Available = new[] { TurnMode.PartyWindow };

        /// <summary>Whether <paramref name="mode"/> can be played and picked (the one check the pickers use).</summary>
        public static bool IsBuilt(TurnMode mode)
        {
            for (int i = 0; i < Available.Count; i++) if (Available[i] == mode) return true;
            return false;
        }

        /// <summary>The available mode after <paramref name="mode"/>, wrapping; one that isn't built goes to the first.</summary>
        public static TurnMode Next(TurnMode mode)
        {
            for (int i = 0; i < Available.Count; i++)
                if (Available[i] == mode) return Available[(i + 1) % Available.Count];
            return Available[0];
        }

        /// <summary>The glossary entry that names and explains <paramref name="mode"/>.</summary>
        public static string GlossaryId(TurnMode mode) => "turn_mode_" + Snake(mode.ToString());

        /// <summary>The mode's display name: its glossary term, else the enum name.</summary>
        public static string Name(ContentDb db, TurnMode mode) => db?.TryTerm(GlossaryId(mode))?.Term ?? mode.ToString();

        /// <summary>The mode's one-line explanation (its glossary definition, term brackets removed); "" when it has none.</summary>
        public static string Describe(ContentDb db, TurnMode mode)
        {
            var text = db?.TryTerm(GlossaryId(mode))?.Text;
            return string.IsNullOrEmpty(text) ? "" : Glossary.Plain(text);
        }

        private static string Snake(string pascal)
        {
            var sb = new System.Text.StringBuilder(pascal.Length + 4);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
