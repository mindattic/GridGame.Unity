using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>GG-T2127: the Commander's pronouns, picked at the Founding (<see cref="SaveData.CommanderPronouns"/>).</summary>
    public enum CommanderPronouns
    {
        TheyThem = 0,
        SheHer = 1,
        HeHim = 2,
    }

    /// <summary>The Commander (§25.20, GG-T1575): the player, who speaks in dialogue as the speaker class
    /// <see cref="Token"/>. The name matches the {commander} text token (GG-T1342). Not a hero: no Trust, no bonds.</summary>
    public static class Commander
    {
        /// <summary>The dialogue speaker class that binds to the Commander.</summary>
        public const string Token = ContentLoader.CommanderToken;
        /// <summary>The text token story text uses for the Commander's name (GG-T1342).</summary>
        public const string TextToken = "{commander}";
        /// <summary>The name when neither the save nor the profile gives one.</summary>
        public const string Fallback = "Commander";
        /// <summary>The Commander's Portraits/ file (GG-T1425): the female look, the default until the Founding's pick
        /// (GG-T2127). A mannequin placeholder until the real art lands (GG-T1427).</summary>
        public static readonly string Portrait = AssetNames.Of("commander");
        /// <summary>GG-T2127: the male look's Portraits/ file.</summary>
        public static readonly string MalePortrait = AssetNames.Of("commander_male");

        /// <summary>The save's <see cref="SaveData.CommanderName"/>, else <paramref name="profileName"/>, else <see cref="Fallback"/>.</summary>
        public static string Name(SaveData s, string profileName = null) =>
            !string.IsNullOrWhiteSpace(s?.CommanderName) ? s.CommanderName
            : !string.IsNullOrWhiteSpace(profileName) ? profileName.Trim() : Fallback;

        /// <summary>GG-T2127: the Portraits/ file of <paramref name="look"/>.</summary>
        public static string PortraitOf(HeroVariant look) => look == HeroVariant.Male ? MalePortrait : Portrait;

        /// <summary>GG-T2127: the Portraits/ file of the save's <see cref="SaveData.CommanderLook"/> (the female look with no save).</summary>
        public static string PortraitOf(SaveData s) => PortraitOf(s?.CommanderLook ?? HeroVariant.Female);

        /// <summary>GG-T2127: how the Founding's pick shows <paramref name="p"/> ("they/them").</summary>
        public static string Label(CommanderPronouns p) =>
            p == CommanderPronouns.SheHer ? "she/her" : p == CommanderPronouns.HeHim ? "he/him" : "they/them";

        /// <summary>GG-T2127: the word pronoun token <paramref name="token"/> (no braces, <see cref="ProseText.PronounTokens"/>,
        /// lower case or capitalized) says for <paramref name="p"/>, capitalized like the token; null for no pronoun token.</summary>
        public static string Pronoun(CommanderPronouns p, string token)
        {
            if (!ProseText.IsPronounToken(token)) return null;
            string[] forms = p == CommanderPronouns.SheHer ? new[] { "she", "her", "her", "hers", "herself" }
                : p == CommanderPronouns.HeHim ? new[] { "he", "him", "his", "his", "himself" }
                : ProseText.PronounTokens;
            string word = forms[System.Array.IndexOf(ProseText.PronounTokens, char.ToLowerInvariant(token[0]) + token.Substring(1))];
            return char.IsUpper(token[0]) ? char.ToUpperInvariant(word[0]) + word.Substring(1) : word;
        }

        /// <summary><paramref name="text"/> with every {commander} token replaced by <see cref="Name"/> and, GG-T2127, every
        /// pronoun token ({they}, {Their}...) by the save's <see cref="SaveData.CommanderPronouns"/>.</summary>
        public static string Fill(string text, SaveData s, string profileName = null)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            if (text.Contains(TextToken)) text = text.Replace(TextToken, Name(s, profileName));
            var p = s?.CommanderPronouns ?? CommanderPronouns.TheyThem;
            foreach (var t in ProseText.PronounTokens)
                foreach (var token in new[] { t, char.ToUpperInvariant(t[0]) + t.Substring(1) })
                    if (text.Contains("{" + token + "}")) text = text.Replace("{" + token + "}", Pronoun(p, token));
            return text;
        }
    }
}
