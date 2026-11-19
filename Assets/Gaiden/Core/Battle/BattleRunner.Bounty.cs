using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>Bounty targets (§22.4, GG-T431): the marked variant joins its wave, fights tougher, and is credited when it
    /// falls for good.</summary>
    internal sealed partial class BattleRunner
    {
        /// <summary>Spawns every bounty target due by wave <paramref name="index"/> that isn't on the board yet. One that
        /// finds no room waits for the next wave. Returns the ids placed, for the arrival events.</summary>
        private List<int> SpawnBounties(int index, WaveStartedEvent started)
        {
            List<int> placed = null;
            foreach (var m in S.Bounties)
            {
                if (m.Spawned || m.Wave > index) continue;
                var def = C.TryClass(m.ClassId);
                if (def == null) { m.Spawned = true; continue; }
                int level = (S.Stage.IsTest ? 1 : S.RecommendedLevel) + S.NewGamePlus * BattleConfig.NgPlusLevelStep
                            + DifficultyTable.At(S.Difficulty).EnemyLevelOffset + BattleConfig.BountyLevelBonus;
                var u = SpawnEnemy(def, level, -1, -1);
                if (u == null) continue;
                m.Spawned = true;
                MarkBounty(u, m);
                started.Spawned.Add(u.Id);
                (placed ??= new List<int>()).Add(u.Id);
            }
            return placed;
        }

        /// <summary>Makes <paramref name="u"/> the bounty target: its title, tougher stats (Speed untouched) and HP, and its
        /// extra trait.</summary>
        private static void MarkBounty(Unit u, BountyMark m)
        {
            u.BountyId = m.BountyId;
            u.Title = string.IsNullOrEmpty(m.Title) ? null : m.Title;
            float speed = u.LevelStats.Speed;
            u.LevelStats = u.LevelStats * BattleConfig.BountyStatScale;
            u.LevelStats.Speed = speed;
            u.MaxHp = u.Hp = Math.Max(1, (int)Math.Round(u.MaxHp * BattleConfig.BountyHpScale));
            if (m.RuleBreak != RuleBreak.None) ArmRuleBreak(u, m.RuleBreak);
        }

        private void AnnounceBounties(List<int> placed)
        {
            if (placed == null) return;
            foreach (var id in placed)
                if (S.UnitById(id) is Unit u)
                    Emit(new BountyTargetEvent { UnitId = u.Id, BountyId = u.BountyId, Title = u.Name });
        }

        /// <summary>A bounty target fell for good (after any Undying rise): its mark is slain.</summary>
        private void CreditBounty(Unit u)
        {
            if (u.BountyId == null || u.Team != Team.Enemy) return;
            foreach (var m in S.Bounties)
                if (m.BountyId == u.BountyId) m.Slain = true;
            Emit(new BountyTargetEvent { UnitId = u.Id, BountyId = u.BountyId, Title = u.Name, Slain = true });
        }
    }
}
