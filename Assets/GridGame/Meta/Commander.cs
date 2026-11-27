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
        /// <summary>No portrait yet (the screen shows a silhouette) until the GG-T1256 canon fills it.</summary>
        public const string Portrait = null;
    }
}
