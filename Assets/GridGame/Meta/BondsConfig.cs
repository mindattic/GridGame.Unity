namespace GridGame.Meta
{
    /// <summary>Every tuning number of Trust and cohesion in one place (bible §25.20), read by <see cref="Bonds"/> and the
    /// battle payoffs, so each can be dialed without touching the rules.</summary>
    public static class BondsConfig
    {
        /// <summary>Trust and cohesion both run 0..Max.</summary>
        public const int Min = 0;
        public const int Max = 100;

        /// <summary>Where tiers 0-5 begin. A bond never drops below the floor of the highest tier it has reached.</summary>
        public static readonly int[] TierFloors = { 0, 10, 25, 45, 70, 100 };
        public static int MaxTier => TierFloors.Length - 1;

        /// <summary>A dialogue choice moves a bond by this much at most either way, once per save.</summary>
        public const int ChoiceMin = -10;
        public const int ChoiceMax = 10;

        // ── Join-route seeds: Trust a hero arrives with ──────────────────────────────
        public const int StartingSeed = 0;
        /// <summary>A story recruit (§25.17) arrives at tier 1.</summary>
        public const int StoryRecruitSeed = 10;
        /// <summary>A wanderer won at full battle Rapport (§25.11.2).</summary>
        public const int RapportSeed = 15;
        /// <summary>A Summon Circle hire, a freed guest or a talk-recruitable (§25.10, §25.11, §25.11.1).</summary>
        public const int HireSeed = 5;

        // ── Battle credits ─────────────────────────────────────────────────────────
        /// <summary>Trust for each hero who fought in a won battle.</summary>
        public const int VictoryTrust = 1;
        /// <summary>Cohesion for each hero pincer two heroes were both part of.</summary>
        public const int PincerCredit = 1;
        /// <summary>Cohesion each time one hero healed, shielded or buffed the other.</summary>
        public const int SupportCredit = 1;
        /// <summary>Most cohesion one pair earns from a single battle.</summary>
        public const int PairCreditCapPerBattle = 5;

        // ── Battle payoffs ─────────────────────────────────────────────────────────
        public const float CritPerTrustTier = 0.02f;
        public const float EvasionPerTrustTier = 0.02f;
        /// <summary>Trust tier that grants Resolve: once per battle, survive a lethal blow at <see cref="ResolveHp"/>.</summary>
        public const int ResolveTier = 5;
        public const int ResolveHp = 1;
        /// <summary>Bonded pincer partners (the two ends) deal this much more damage per cohesion tier.</summary>
        public const float PincerDamagePerCohesionTier = GridGame.Core.Battle.BattleConfig.PincerDamagePerCohesionTier;
        /// <summary>From this cohesion tier a supporter's reach is +<see cref="BondedReachBonus"/> toward its partner.</summary>
        public const int BondedReachTier = 3;
        public const int BondedReachBonus = 1;
        /// <summary>From this cohesion tier heals, shields and buffs one partner gives the other are +<see cref="BondedSupportBonus"/>.</summary>
        public const int BondedSupportTier = 4;
        public const float BondedSupportBonus = 0.10f;
        /// <summary>Cohesion tier that unlocks a pair's authored Duo support.</summary>
        public const int DuoTier = 5;

        // ── Story payoffs ──────────────────────────────────────────────────────────
        /// <summary>Trust tiers that play a hero's Trust scene.</summary>
        public static readonly int[] TrustSceneTiers = { 1, 3, 5 };
        /// <summary>Cohesion tiers that play a pair scene.</summary>
        public static readonly int[] PairSceneTiers = { 2, 4 };
    }
}
