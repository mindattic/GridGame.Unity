using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The tutorial drills (GG-T1374, GG-T1626): campaign.json's Tutorial stages, each a stage with Lessons. A drill plays
    /// on its own fixed seed as a tutorial battle, so its lessons may stage their situations. It is practice: nothing is
    /// earned, spent or worn (no XP, gold, loot, item use or durability wear; wounds stay as they were) and campaign
    /// progress never moves. The Bestiary still counts the foes seen and defeated, and a win marks the drill cleared
    /// (<see cref="SaveData.ClearedDrills"/>).
    /// </summary>
    public static class Drills
    {
        public static bool IsDrill(StageDef stage) => stage?.Lessons != null && stage.Lessons.Count > 0;

        /// <summary>GG-T2075: the drills a new game plays, in order: campaign.json's Prologue battles, else its Tutorial drills.</summary>
        public static List<string> NewGameOrder(ContentDb c)
        {
            var prologue = c?.Campaign?.Prologue;
            return prologue != null && prologue.Count > 0 ? prologue : c?.Campaign?.Tutorial ?? new List<string>();
        }

        /// <summary>GG-T2075: the drill list <paramref name="stageId"/> sits in: the Prologue for a prologue battle, else the
        /// Tutorial (Play a drill's list).</summary>
        public static List<string> OrderOf(ContentDb c, string stageId)
        {
            var prologue = c?.Campaign?.Prologue;
            return prologue != null && prologue.Exists(id => string.Equals(id, stageId, StringComparison.OrdinalIgnoreCase))
                ? prologue : c?.Campaign?.Tutorial ?? new List<string>();
        }

        public static bool IsCleared(SaveData s, string stageId) => s?.ClearedDrills != null && stageId != null && s.ClearedDrills.Contains(stageId);

        /// <summary>GG-T1398: the save flag Skip tutorial in a drill leaves: the drills were skipped, not cleared.</summary>
        public const string SkippedFlag = "drills:skipped";

        public static bool Skipped(SaveData s) => s?.Flags != null && s.Flags.Contains(SkippedFlag);

        /// <summary>GG-T1398: the drills are skipped as a set; the onboarding moves on to Camp's first visit.</summary>
        public static void Skip(SaveData s)
        {
            if (s == null) return;
            s.Flags.Add(SkippedFlag);
            Onboarding.Begin(s, OnboardingStep.HubIntro);
        }

        /// <summary>GG-T1959: the lesson that teaches the turn clock; its drill is The Clock.</summary>
        public const string ClockLesson = "hold";

        /// <summary>GG-T1959: a drill that comes before The Clock in its list's order (Tutorial, or GG-T2075 Prologue) plays
        /// untimed, so the clock waits even while a hero is held until the drill that teaches it.</summary>
        public static bool BeforeClock(ContentDb c, StageDef stage)
        {
            if (!IsDrill(stage) || c == null) return false;
            var order = OrderOf(c, stage.Id);
            int at = order.FindIndex(id => string.Equals(id, stage.Id, StringComparison.OrdinalIgnoreCase));
            int clock = order.FindIndex(id => c.TryStage(id)?.Lessons?.Any(l => l.Id == ClockLesson) == true);
            return at >= 0 && clock >= 0 && at < clock;
        }

        /// <summary>Marks every drill cleared: a save that finished the tutorial, and Test Mode.</summary>
        public static void MarkAllCleared(ContentDb c, SaveData s)
        {
            s.ClearedDrills ??= new HashSet<string>();
            foreach (var id in (c?.Campaign?.Tutorial ?? Enumerable.Empty<string>()).Concat(c?.Campaign?.Prologue ?? Enumerable.Empty<string>()))
                if (id != null) s.ClearedDrills.Add(id);
        }

        /// <summary>Commits a finished drill: only the Bestiary and, on a win, the drill's clear.</summary>
        public static BattleReport Commit(ContentDb c, SaveData s, BattleState battle)
        {
            var r = battle.Results;
            var rep = Report(battle);
            foreach (var id in r.Seen) BattleFactory.Entry(s, id).Seen = true;
            foreach (var kv in r.Kills) BattleFactory.Entry(s, kv.Key).Defeated += kv.Value;
            if (rep.Victory)
            {
                s.ClearedDrills ??= new HashSet<string>();
                rep.FirstClear = s.ClearedDrills.Add(battle.Stage.Id);
            }
            Codex.Sync(c, s);
            s.UpdatedUtc = DateTime.UtcNow;
            return rep;
        }

        /// <summary>GG-T1647: a finished drill's report, written nowhere (a practice drill from Settings leaves the save as it was).</summary>
        public static BattleReport Report(BattleState battle)
        {
            var r = battle.Results;
            return new BattleReport
            {
                Victory = battle.Phase == BattlePhase.Victory,
                Escaped = battle.Phase == BattlePhase.Escaped,
                Retreated = battle.Phase == BattlePhase.Retreated,
                StageId = battle.Stage.Id,
                StageName = battle.Stage.Name,
                StageTheme = battle.Stage.Theme,
                Difficulty = battle.Difficulty,
                BestComboRank = r.BestComboRank,
                BestComboScore = r.BestComboScore,
                PartyTurns = r.PartyTurns,
                Actions = r.ActionsTaken,
                Rounds = r.Rounds,
                HeroTurns = r.HeroTurns,
            };
        }
    }
}
