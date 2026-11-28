namespace GridGame.Core.Battle
{
    /// <summary>
    /// Temporary shim over the per-spell mana meters (bible §3.1.0, GG-T1807); deleted by GG-T1829 once App, Meta, the bot
    /// and the tests read the meters. Bound to a <see cref="BattleState"/> (as <see cref="BattleState.Bank"/> always is),
    /// <see cref="Count"/> is the number of full spell meters, <see cref="Add"/> is a whole grant of N x
    /// <see cref="BattleConfig.ManaPointsPerOrb"/> points to every meter and <see cref="Spend"/> a siphon of the same.
    /// Unbound (built on its own), it is the old capped orb counter.
    /// </summary>
    public sealed class ManaBank
    {
        public const int DefaultCapacity = BattleConfig.ManaBankCapacity;

        private BattleState owner;
        private int count;

        public int Capacity { get; }
        public int Count => owner != null ? owner.ReadySpellCount : count;
        public bool IsFull => owner != null ? owner.AllSpellsFull : count >= Capacity;

        public ManaBank(int capacity = DefaultCapacity) { Capacity = capacity; }

        internal ManaBank Bind(BattleState state) { owner = state; return this; }

        public ManaBank Clone() => new ManaBank(Capacity) { count = count };

        /// <summary>Bound: grants <paramref name="n"/> orbs' worth of points to every meter and returns n when any landed.
        /// Unbound: adds up to n orbs and returns how many landed (the rest overflowed).</summary>
        public int Add(int n = 1)
        {
            if (owner != null) return n > 0 && BattleEngine.GainMana(owner, n * BattleConfig.ManaPointsPerOrb, "orb") != null ? n : 0;
            int landed = System.Math.Max(0, System.Math.Min(n, Capacity - count));
            count += landed;
            return landed;
        }

        public bool CanAfford(int cost) => cost <= Count;

        /// <summary>Bound: removes <paramref name="cost"/> orbs' worth of points from every meter. Unbound: removes cost
        /// orbs; false (and nothing spent) if unaffordable.</summary>
        public bool Spend(int cost)
        {
            if (cost < 0 || cost > Count) return false;
            if (owner != null) { BattleEngine.GainMana(owner, -cost * BattleConfig.ManaPointsPerOrb, "orb"); return true; }
            count -= cost;
            return true;
        }

        public override string ToString() => $"{Count}/{Capacity}";
    }
}
