using GridGame.Core.Content;

namespace GridGame.Meta
{
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
        /// <summary>The Commander's Portraits/ file (GG-T1425): the female look until the front door's pick (GG-T1258)
        /// chooses between it and commander_male. A mannequin placeholder until the real art lands (GG-T1427).</summary>
        public static readonly string Portrait = AssetNames.Of("commander");

        /// <summary>The save's <see cref="SaveData.CommanderName"/>, else <paramref name="profileName"/>, else <see cref="Fallback"/>.</summary>
        public static string Name(SaveData s, string profileName = null) =>
            !string.IsNullOrWhiteSpace(s?.CommanderName) ? s.CommanderName
            : !string.IsNullOrWhiteSpace(profileName) ? profileName.Trim() : Fallback;

        /// <summary><paramref name="text"/> with every {commander} token replaced by <see cref="Name"/>.</summary>
        public static string Fill(string text, SaveData s, string profileName = null) =>
            string.IsNullOrEmpty(text) || !text.Contains(TextToken) ? text : text.Replace(TextToken, Name(s, profileName));
    }
}
