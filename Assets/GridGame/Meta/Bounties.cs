using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The bounty board (§22.4, GG-T431): hunts for marked targets. Each bounty is a tougher, titled variant of an existing
    /// enemy class roaming one campaign stage, with a clue naming it ("Seen wandering the Tithe Moors II"). Take it, play
    /// that stage, and it joins one of the waves wearing a skull; kill it and its reward waits at the board. The board shows
    /// <see cref="BoardSize"/> bounties: what is waiting to be collected, what is being hunted, then fresh offers in an
    /// order seeded by the save that turns over every <see cref="RefreshBattles"/> stage battles. Bots never use it.
    /// Replaces the kill-N contracts (retired: they duplicated the side quests' Defeat objectives).
    /// </summary>
    public static class Bounties
    {
        /// <summary>Bounties on the board at once (waiting, hunted and offered together).</summary>
        public const int BoardSize = 3;
        /// <summary>Stage battles between turnovers of the open offers.</summary>
        public const int RefreshBattles = 3;
        /// <summary>Gold for a bounty: base + per-stage × the campaign index of the stage it roams.</summary>
        public const int RewardGoldBase = 150;
        public const int RewardGoldPerStage = 50;

        public static BountyDef Def(ContentDb c, string id) => id != null && c.Bounties.TryGetValue(id, out var b) ? b : null;

        public static bool IsTaken(SaveData s, string id) => s.Bounty.Taken.Contains(id);
        public static bool IsSlain(SaveData s, string id) => s.Bounty.Slain.Contains(id);

        /// <summary>The campaign index of the stage a bounty roams (-1 if it isn't on the campaign).</summary>
        public static int StageIndex(ContentDb c, BountyDef b) =>
            c.CampaignStages().FindIndex(x => string.Equals(x.Id, b.Stage, StringComparison.OrdinalIgnoreCase));

        public static int RewardGold(ContentDb c, BountyDef b) => RewardGoldBase + RewardGoldPerStage * Math.Max(0, StageIndex(c, b));

        /// <summary>Skill points each active-party hero gains when this bounty is claimed (GG-T134); never negative.</summary>
        public static int SkillPointsPaid(BountyDef b) => b == null ? 0 : Math.Max(0, b.RewardSkillPoints);

        /// <summary>Skill points the claimed bounties have paid each party hero over this save (GG-T134).</summary>
        public static int SkillPointsClaimed(ContentDb c, SaveData s) => s.Bounty.Claimed.Sum(id => SkillPointsPaid(Def(c, id)));

        /// <summary>The stage's display name as it reads mid-sentence ("the Tithe Moors II").</summary>
        public static string StageName(ContentDb c, BountyDef b)
        {
            string name = c.Stages.TryGetValue(b.Stage ?? "", out var st) ? st.Name : b.Stage;
            return name != null && name.StartsWith("The ", StringComparison.Ordinal) ? "the " + name.Substring(4) : name;
        }

        /// <summary>The clue on the poster: where it was seen.</summary>
        public static string Clue(ContentDb c, BountyDef b) => $"Seen wandering {StageName(c, b)}";

        /// <summary>The board, in order: slain (collect), hunted, then fresh offers up to <see cref="BoardSize"/>. Fresh
        /// offers are unclaimed bounties on stages already unlocked, in an order seeded by the save and the turnover.</summary>
        public static List<BountyDef> Board(ContentDb c, SaveData s)
        {
            var board = new List<BountyDef>();
            foreach (var id in s.Bounty.Slain) if (Def(c, id) is BountyDef b && !board.Contains(b)) board.Add(b);
            foreach (var id in s.Bounty.Taken) if (Def(c, id) is BountyDef b && !board.Contains(b)) board.Add(b);
            var open = c.Bounties.Values
                .Where(b => !s.Bounty.Claimed.Contains(b.Id) && !board.Contains(b))
                .Where(b => { int i = StageIndex(c, b); return i >= 0 && Progression.IsStageUnlocked(s, i); })
                .OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
            var rng = new Random(Seed(s));
            for (int i = open.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (open[i], open[j]) = (open[j], open[i]); }
            board.AddRange(open.Take(Math.Max(0, BoardSize - board.Count)));
            return board;
        }

        /// <summary>Fresh offers only (the board minus what is hunted or waiting).</summary>
        public static List<BountyDef> Offers(ContentDb c, SaveData s) =>
            Board(c, s).Where(b => !IsTaken(s, b.Id) && !IsSlain(s, b.Id)).ToList();

        /// <summary>Bounties being hunted.</summary>
        public static List<BountyDef> Hunting(ContentDb c, SaveData s) =>
            s.Bounty.Taken.Select(id => Def(c, id)).Where(b => b != null).ToList();

        /// <summary>Targets slain whose reward waits at the board.</summary>
        public static List<BountyDef> Claimable(ContentDb c, SaveData s) =>
            s.Bounty.Slain.Select(id => Def(c, id)).Where(b => b != null).ToList();

        /// <summary>Hunted bounties roaming <paramref name="stageId"/> (the stage select marks those stages).</summary>
        public static List<BountyDef> OnStage(ContentDb c, SaveData s, string stageId) =>
            Hunting(c, s).Where(b => string.Equals(b.Stage, stageId, StringComparison.OrdinalIgnoreCase)).ToList();

        public static Result Take(ContentDb c, SaveData s, string id)
        {
            var b = Def(c, id);
            if (b == null || !Offers(c, s).Contains(b)) return Result.Fail("Not on the board");
            s.Bounty.Taken.Add(b.Id);
            s.Bounty.Used = true;
            return Result.Success($"Hunting {b.Name}");
        }

        /// <summary>Gives up a hunt; the bounty goes back to the pool and may post again.</summary>
        public static Result Abandon(ContentDb c, SaveData s, string id)
        {
            if (id == null || !s.Bounty.Taken.Remove(id)) return Result.Fail("Not hunting that one");
            return Result.Success($"Stopped hunting {Def(c, id)?.Name ?? id}");
        }

        /// <summary>The marked targets a battle on this stage carries: one per hunted bounty that roams it.</summary>
        public static List<BountyMark> MarksFor(ContentDb c, SaveData s, StageDef stage)
        {
            if (stage == null || stage.Waves.Count == 0) return new List<BountyMark>();
            return OnStage(c, s, stage.Id).Select(b => new BountyMark
            {
                BountyId = b.Id, ClassId = b.TargetClass, Title = b.Name, RuleBreak = b.RuleBreak,
                Wave = Math.Min(Math.Max(0, b.Wave), stage.Waves.Count - 1),
            }).ToList();
        }

        /// <summary>After a stage battle (<see cref="BattleFactory.Commit"/>), however it ended: each target that fell is
        /// slain (its reward waits at the board), and the open offers turn over every <see cref="RefreshBattles"/>
        /// battles. Returns the results-screen line, or null.</summary>
        public static string RecordBattle(ContentDb c, SaveData s, BattleState battle)
        {
            string line = null;
            foreach (var m in battle.Bounties)
            {
                if (!m.Slain || !s.Bounty.Taken.Remove(m.BountyId)) continue;
                if (!s.Bounty.Slain.Contains(m.BountyId)) s.Bounty.Slain.Add(m.BountyId);
                string text = $"{m.Title} slain! Collect at the board";
                line = line == null ? text : line + "\n" + text;
            }
            if (++s.Bounty.Battles >= RefreshBattles)
            {
                s.Bounty.Battles = 0;
                s.Bounty.Refresh++;
            }
            return line;
        }

        /// <summary>Collects a slain target's reward: gold, its item, and any skill points for every party hero.</summary>
        public static Result Claim(ContentDb c, SaveData s, string id)
        {
            var b = Def(c, id);
            if (b == null || !s.Bounty.Slain.Contains(b.Id)) return Result.Fail("Not slain yet");
            int gold = RewardGold(c, b);
            s.Gold += gold;
            if (b.RewardItem != null) s.AddItem(b.RewardItem, 1);
            int sp = SkillPointsPaid(b);
            if (sp > 0)
                foreach (var hid in s.Party)
                {
                    var h = s.Hero(hid);
                    if (h != null) h.SkillPoints += sp;
                }
            s.Bounty.Slain.Remove(b.Id);
            s.Bounty.Claimed.Add(b.Id);
            return Result.Success(sp > 0 ? $"Claimed {gold}g + {sp} SP each" : $"Claimed {gold}g");
        }

        private static int Seed(SaveData s) =>
            unchecked((int)((uint)s.CreatedUtc.Ticks * 2246822519u ^ (uint)s.Bounty.Refresh * 3266489917u ^ 0x5B0u));
    }
}
