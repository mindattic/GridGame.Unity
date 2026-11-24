namespace GridGame.Core.Battle
{
    /// <summary>
    /// The turn mode (bible §2.13, GG-T762/GG-T767): the rule for which heroes may act and when. Copied from
    /// <see cref="BattleSetup.TurnMode"/> to <see cref="BattleState.TurnMode"/> and recorded with the battle
    /// (<see cref="BattleRecorder"/>, <see cref="CommandCodec.ReadTurnMode"/>) so a replay reproduces it. Only the
    /// TurnRules seam may read it. Append new values at the end; the recorded name, not the number, is what persists.
    /// </summary>
    public enum TurnMode
    {
        /// <summary>Today's rule and the default (§2.13.0): every able hero acts once per party window.</summary>
        PartyWindow = 0,
        /// <summary>⬜ (§2.13.1, GG-T779): one hero per party turn, any hero.</summary>
        AnyHeroEveryTurn = 1,
        /// <summary>⬜ (§2.13.2, GG-T792): one hero per party turn, each once per round.</summary>
        UnmovedThisRound = 2,
        /// <summary>⬜ (§2.13.3, GG-T805): each hero has its own icon on the timeline.</summary>
        HeroTimeline = 3,
    }
}
