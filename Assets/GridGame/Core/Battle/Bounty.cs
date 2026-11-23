using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>A marked bounty target for one battle (§22.4, GG-T431): a tougher, titled variant of an existing enemy class
    /// that joins wave <see cref="Wave"/> (or the next wave with room). Built by the meta layer (<c>Bounties.MarksFor</c>)
    /// from the bounties the player has taken for this stage; never authored on a stage.</summary>
    public sealed class BountyMark
    {
        public string BountyId;
        public string ClassId;
        /// <summary>Its name in battle, e.g. "Grimjaw the Mauler".</summary>
        public string Title;
        /// <summary>0-based wave it joins.</summary>
        public int Wave;
        /// <summary>An optional extra trait: one of the rule-breaking bosses' rules (§14.9).</summary>
        public RuleBreak RuleBreak;
        /// <summary>Set once it is on the board, so it spawns once.</summary>
        public bool Spawned;
        /// <summary>Set when it falls for good; the meta layer marks the bounty done from this.</summary>
        public bool Slain;
        public BountyMark Clone() => (BountyMark)MemberwiseClone();
    }

    /// <summary>A bounty target arrived (<see cref="Slain"/> false) or fell (true) — §22.4, GG-T431. Played with the skull
    /// marker, a banner and its own cue.</summary>
    public sealed class BountyTargetEvent : BattleEvent
    {
        public int UnitId; public string BountyId; public string Title; public bool Slain;
        public override string ToString() => $"Bounty {(Slain ? "slain" : "arrived")} {Title} ({UnitId})";
    }
}
