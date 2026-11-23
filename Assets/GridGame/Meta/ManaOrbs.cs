using System;
using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Owned Mana Orbs (GG-T677, BIBLE §3.1.4.2). Mana Orbs are scavenged Halcyon batteries that capture Mana Dust so it
    /// can be spent in combat; how many a save owns is how many the battle's row can fill (BattleSetup.OrbCapacity).
    /// Owner 2026-11-22: "going from one Mana Orb to two Mana Orbs doubles your magic casting abilities". Every source is
    /// finite, so the most a save can ever own is fixed by content (ContentLoader refuses more than
    /// <see cref="BattleConfig.MaxOrbCapacity"/>):
    /// <list type="bullet">
    /// <item>the starting orb, <see cref="BattleConfig.StartOwnedOrbs"/>;</item>
    /// <item>one per <see cref="ThemeDef.MilestoneOrb"/> theme's first full clear (boss and milestone rewards);</item>
    /// <item>one per stage of a Halcyon ruin (<see cref="ThemeDef.OrbFindChance"/>), found on a victory there;</item>
    /// <item>up to <see cref="BattleConfig.MaxBrokenOrbs"/> Broken Mana Orbs dropped as loot, which the Manasmith restores.</item>
    /// </list>
    /// </summary>
    public static class ManaOrbs
    {
        /// <summary>A rare loot item: a cracked Halcyon battery the Manasmith can restore into an owned orb.</summary>
        public const string BrokenOrbId = "mat_broken_mana_orb";
        /// <summary>Chance a campaign victory turns up a Broken Mana Orb while fewer than <see cref="BattleConfig.MaxBrokenOrbs"/>
        /// have dropped: about two over a straight playthrough's ~50 victories.</summary>
        public const float BrokenOrbChance = 0.04f;
        /// <summary>The Manasmith's price to restore one Broken Mana Orb: Mana Dust (the refinery's own material) plus gold.</summary>
        public const int RestoreDust = 20, RestoreGold = 100;

        /// <summary>Orbs this save owns, 1..<see cref="BattleConfig.MaxOrbCapacity"/>. A save not yet migrated (0) reads
        /// as <see cref="Derived"/>, so a balance snapshot or an old file never fights with zero orbs.</summary>
        public static int Owned(ContentDb c, SaveData s)
        {
            if (s == null) return BattleConfig.StartOwnedOrbs;
            int n = s.OwnedOrbs > 0 ? s.OwnedOrbs : Derived(c, s);
            return Math.Max(1, Math.Min(BattleConfig.MaxOrbCapacity, n));
        }

        /// <summary>The guaranteed orbs a save's progress has earned: the starting orb, one per orb milestone already paid,
        /// and any Halcyon finds recorded. Used to migrate a save from before owned orbs (GG-T677).</summary>
        public static int Derived(ContentDb c, SaveData s)
        {
            int n = BattleConfig.StartOwnedOrbs;
            if (c?.Campaign?.Themes != null && s?.Milestones != null)
                foreach (var t in c.Campaign.Themes)
                    if (t.MilestoneOrb && s.Milestones.Contains(t.Id)) n++;
            n += s?.OrbFinds?.Count ?? 0;
            return Math.Min(BattleConfig.MaxOrbCapacity, n);
        }

        /// <summary>Adds one owned orb (never past the cap) and records why on the report. Returns whether it was added.</summary>
        public static bool Grant(ContentDb c, SaveData s, BattleReport rep, string why)
        {
            if (s == null) return false;
            int have = Owned(c, s);
            if (have >= BattleConfig.MaxOrbCapacity) return false;
            s.OwnedOrbs = have + 1;
            if (rep != null) { rep.OrbsGained++; rep.OrbsGainedFrom.Add(why); }
            return true;
        }

        /// <summary>A won campaign stage's orb rewards: the milestone orb (when <paramref name="milestone"/> is an orb
        /// milestone theme just paid), a Halcyon find, and a Broken Mana Orb drop. Rolls are seeded by
        /// <see cref="SaveData.OrbRolls"/>, so a save replays them identically.</summary>
        public static void AfterVictory(ContentDb c, SaveData s, BattleReport rep, string stageId, ThemeDef milestone)
        {
            if (c == null || s == null || rep == null) return;
            s.OrbFinds ??= new HashSet<string>();
            if (milestone != null && milestone.MilestoneOrb) Grant(c, s, rep, $"{milestone.Name} cleared");

            var theme = c.ThemeOf(stageId);
            if (theme != null && theme.OrbFindChance > 0f && !s.OrbFinds.Contains(stageId))
            {
                var rng = Roll(s, stageId);
                if (rng.Chance(theme.OrbFindChance) && Grant(c, s, rep, $"found in the ruins of {theme.Name}"))
                    s.OrbFinds.Add(stageId);
            }

            if (s.BrokenOrbsDropped < BattleConfig.MaxBrokenOrbs && Roll(s, stageId + "#broken").Chance(BrokenOrbChance))
            {
                s.BrokenOrbsDropped++;
                s.AddItem(BrokenOrbId, 1);
                rep.Items.TryGetValue(BrokenOrbId, out var had);
                rep.Items[BrokenOrbId] = had + 1;
                rep.BrokenOrbFound = true;
            }
            rep.OrbsOwned = Owned(c, s);
        }

        private static Rng Roll(SaveData s, string key)
        {
            var rng = new Rng(unchecked(s.OrbRolls * 7919 + 15485863 + StableHash(key)));
            s.OrbRolls++;
            return rng;
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                int h = 17;
                foreach (var ch in text ?? "") h = h * 31 + ch;
                return h;
            }
        }

        /// <summary>Whether the Manasmith can restore a Broken Mana Orb right now, and why not when not.</summary>
        public static Result CanRestore(ContentDb c, SaveData s)
        {
            if (s == null) return Result.Fail("No save");
            if (s.Count(BrokenOrbId) <= 0) return Result.Fail("No Broken Mana Orb to restore");
            if (Owned(c, s) >= BattleConfig.MaxOrbCapacity) return Result.Fail("You hold every Mana Orb there is");
            if (DustEconomy.Have(s) < RestoreDust) return Result.Fail($"Not enough {DustEconomy.DustName}: {RestoreDust - DustEconomy.Have(s):N0} more needed");
            if (s.Gold < RestoreGold) return Result.Fail($"Not enough gold: {RestoreGold:N0} G needed");
            return Result.Success("");
        }

        /// <summary>The Manasmith restores one Broken Mana Orb into an owned orb for <see cref="RestoreDust"/> Mana Dust
        /// and <see cref="RestoreGold"/> gold. Takes nothing on failure.</summary>
        public static Result Restore(ContentDb c, SaveData s)
        {
            var can = CanRestore(c, s);
            if (!can.Ok) return can;
            s.AddItem(BrokenOrbId, -1);
            s.AddItem(DustEconomy.DustId, -RestoreDust);
            s.Gold -= RestoreGold;
            s.OwnedOrbs = Owned(c, s) + 1;
            return Result.Success($"The Manasmith restored a Mana Orb. You now hold {s.OwnedOrbs}.");
        }
    }
}
