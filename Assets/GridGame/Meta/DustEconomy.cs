using System;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Aetherite Dust (§25.18.1, GG-T569/623-625): earned in play and bought with gold, refined by the Aetherist at Camp
    /// into <see cref="ShardText"/>'s Aetherite Shards — the only way Dust becomes Shards. Every rate here is the one
    /// tuning spot (GG-T625, "tune so Shards stay valuable"): buying Dust and refining it must never beat
    /// <see cref="DiamondExchange"/>'s own gold-to-Shard rate (<see cref="DiamondExchange.GoldPerDiamond"/>, 6 g), so the
    /// gold path to a Shard stays the direct exchange and Dust stays the free-play path — slower, but free.
    /// </summary>
    public static class DustEconomy
    {
        public const string DustId = "mat_aetherite_dust";
        public const string DustName = "Mana Dust"; // GG-T675; was "Aetherite Dust" (id kept)

        /// <summary>Dust every battle victory pays (GG-T623), on top of gold and loot — a slow, repeatable trickle.</summary>
        public const int DustPerVictory = 1;
        /// <summary>Extra Dust a stage's first clear pays, stacked on <see cref="DustPerVictory"/> — new ground pays
        /// more than a repeat hunt.</summary>
        public const int FirstClearDustBonus = 4;

        /// <summary>Mana Dust each whole <see cref="BattleConfig.ManaPointsPerOrb"/> points left in the party mana pool at the
        /// end of a won stage settle back into (GG-T676, GG-T1927 §3.1.0). 1 per 100 matches <see cref="DustPerVictory"/>'s scale, so a
        /// player who ends a stage with a full pool earns a little more than a first clear's bonus, a real choice against
        /// casting, while staying far from a Shard's worth (<see cref="DustPerShard"/>) per battle.</summary>
        public const int DustPerOrbLeft = 1;

        /// <summary>Mana Dust <paramref name="manaLeft"/> leftover pool points settle into: floor(manaLeft / 100) x
        /// <see cref="DustPerOrbLeft"/>; a part-100 remainder pays nothing.</summary>
        public static int SettleMana(int manaLeft) => Math.Max(0, manaLeft) / BattleConfig.ManaPointsPerOrb * DustPerOrbLeft;

        /// <summary>Gold for one Dust at the Aetherist's counter (GG-T623).</summary>
        public const int GoldPerDust = 2;
        /// <summary>Dust the Aetherist refines into one Shard (GG-T624). Buying a refine's worth of Dust costs
        /// <see cref="GoldPerDust"/>×this in gold — kept well above <see cref="DiamondExchange.GoldPerDiamond"/> so the
        /// Dust counter is never the cheap way to a Shard (GG-T625).</summary>
        public const int DustPerShard = 10;

        public static int Have(SaveData s) => s?.Count(DustId) ?? 0;

        /// <summary>Gold worth of <paramref name="dust"/> Dust at the counter.</summary>
        public static int GoldFor(int dust) => Math.Max(0, dust) * GoldPerDust;

        public static int Cap(ContentDb c) => c?.TryItem(DustId)?.MaxStack ?? int.MaxValue;

        /// <summary>Room left in the bag for Dust.</summary>
        public static int Room(ContentDb c, SaveData s) => Math.Max(0, Cap(c) - Have(s));

        /// <summary>The most Dust this save's gold can buy right now (and the bag can hold).</summary>
        public static int MaxBuy(ContentDb c, SaveData s) => s == null ? 0 : Math.Min(Room(c, s), Math.Max(0, s.Gold) / GoldPerDust);

        /// <summary>Exchanges gold for <paramref name="dust"/> Dust, instantly, at <see cref="GoldPerDust"/>.</summary>
        public static Result Buy(ContentDb c, SaveData s, int dust)
        {
            if (s == null) return Result.Fail("No save");
            if (dust <= 0) return Result.Fail("Pick an amount");
            if (Room(c, s) < dust) return Result.Fail($"Can't hold that much {DustName}");
            int gold = GoldFor(dust);
            if (s.Gold < gold) return Result.Fail($"Not enough gold: {gold:N0} G for {dust:N0} {DustName}");
            s.Gold -= gold;
            s.AddItem(DustId, dust);
            return Result.Success($"Exchanged {gold:N0} G for {dust:N0} {DustName}.");
        }

        /// <summary>Shards <paramref name="dust"/> Dust refines into, floored to a whole Shard.</summary>
        public static int ShardsFor(int dust) => Math.Max(0, dust) / DustPerShard;

        /// <summary>The most Shards this save's Dust can refine right now (and the bag can hold).</summary>
        public static int MaxRefine(ContentDb c, SaveData s) =>
            s == null ? 0 : Math.Min(ShardsFor(Have(s)), DiamondExchange.Room(c, s));

        /// <summary>Refines <paramref name="shards"/> Shards' worth of Dust (<see cref="DustPerShard"/> each) at the
        /// Aetherist's counter. Dust under a whole Shard's worth stays banked for next time. Takes nothing on failure.</summary>
        public static Result Refine(ContentDb c, SaveData s, int shards)
        {
            if (s == null) return Result.Fail("No save");
            if (shards <= 0) return Result.Fail("Pick an amount");
            int dust = shards * DustPerShard;
            if (Have(s) < dust) return Result.Fail($"Not enough {DustName}: {dust - Have(s):N0} more needed");
            if (DiamondExchange.Room(c, s) < shards) return Result.Fail($"Can't hold that many {ShardText.Plural}");
            s.AddItem(DustId, -dust);
            DiamondExchange.Add(s, shards); // GG-T702: into the account wallet once the save was moved
            return Result.Success($"Refined {dust:N0} {DustName} into {ShardText.Of(shards)}.");
        }
    }
}
