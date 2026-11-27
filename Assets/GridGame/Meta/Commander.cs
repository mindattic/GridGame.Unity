using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>The Commander (§25.20, GG-T1575): the player, who speaks in dialogue as the speaker class
    /// <see cref="Token"/>. The name matches the {commander} text token (GG-T1342). Not a hero: no Trust, no bonds.</summary>
    public static class Commander
    {
        /// <summary>The dialogue speaker class that binds to the Commander.</summary>
        public const string Token = ContentLoader.CommanderToken;
        /// <summary>A placeholder until the Commander canon (GG-T1256) names them.</summary>
        public const string Name = "the Commander";
        /// <summary>The Commander's Portraits/ file (GG-T1425): the female look until the front door's pick (GG-T1258)
        /// chooses between it and commander_male. A mannequin placeholder until the real art lands (GG-T1427).</summary>
        public static readonly string Portrait = AssetNames.Of("commander");
    }
}
