using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Station progression (§24.6.1, GG-T35): craft/brew/repair/salvage levels up the
    // station that did the work, unlocking the next tier of its own recipe ladder.
    // Recipe tiers only: the gold-bought facility levels of §25.12.2 (GG-T432) are a second, separate gate on
    // material tier (result rarity), checked by Crafting.CanMake; caravan stat upgrades never move station XP.
    // ─────────────────────────────────────────────────────────────────────────────

    public enum StationKind { Blacksmith, Alchemist }

    public static class StationProgress
    {
        public static readonly string[] LevelNames = { "Apprentice", "Journeyman", "Master" };
        /// <summary>Total XP needed to *reach* each level (index 0 = Apprentice, free). Tuned in <see cref="VendorTiers.RankXp"/> (GG-T538).</summary>
        private static int[] Thresholds => VendorTiers.RankXp;

        public static string Id(StationKind k) => k == StationKind.Blacksmith ? "blacksmith" : "alchemist";

        public static int Xp(SaveData s, StationKind k) => s.StationXp.TryGetValue(Id(k), out var n) ? n : 0;

        public static int Level(SaveData s, StationKind k)
        {
            int xp = Xp(s, k);
            int lvl = 1;
            for (int i = 1; i < Thresholds.Length; i++) if (xp >= Thresholds[i]) lvl = i + 1;
            return lvl;
        }

        public static string LevelName(SaveData s, StationKind k) => LevelNames[Level(s, k) - 1];
        public static string TierName(int tier) => LevelNames[Math.Max(1, Math.Min(tier, LevelNames.Length)) - 1];

        public static void Gain(SaveData s, StationKind k, int amount)
        {
            if (amount <= 0) return;
            s.StationXp[Id(k)] = Xp(s, k) + amount;
        }

        public static bool IsUnlocked(SaveData s, StationKind k, int tier) => Level(s, k) >= tier;

        /// <summary>The header's compact form: "Apprentice 40/150 XP", or just the rank once maxed.</summary>
        public static string ShortText(SaveData s, StationKind k)
        {
            int lvl = Level(s, k);
            return lvl >= LevelNames.Length ? LevelNames[lvl - 1] : $"{LevelNames[lvl - 1]} {Xp(s, k)}/{Thresholds[lvl]} XP";
        }

        /// <summary>"Journeyman — 40/450 XP to Master", or "Master — every tier unlocked" when maxed.</summary>
        public static string HeaderText(SaveData s, StationKind k)
        {
            int lvl = Level(s, k);
            if (lvl >= LevelNames.Length) return $"{LevelNames[lvl - 1]} — every recipe tier unlocked";
            return $"{LevelNames[lvl - 1]} — {Xp(s, k)}/{Thresholds[lvl]} XP to {LevelNames[lvl]}";
        }
    }
}
