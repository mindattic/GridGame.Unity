using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>
    /// GG-T191 soak instrument for the held-turn timeline (§2.0), the Guard mana bank (§2.5, §3.1.9) and class support
    /// effects (§1.2.3): plays the campaign with bots and tabulates average turn time used, Guard frequency, mana from
    /// Guard vs pincers (and the other sources), support heal / buff / haste / cleanse totals and win rate per stage,
    /// plus an all-stages row. Every tuning number it reports on lives in <see cref="BattleConfig"/>. Asserts only that
    /// nothing stalls. GG-T595: with GRIDGAME_SOAK_OUT set to a folder, each bot's table is also written there as
    /// <c>held-turn-&lt;label&gt;.json</c> (raw sums and derived rates) for the GG-T596 tuning pass. GG-T913: the
    /// turn-mode comparison metrics of docs/TURN_MODES.md (battle length, decisions per player turn, hero actions per enemy
    /// turn, downtime, pincers and chains, forced vs chosen Guards, slot uses by kind, bot refusals, combo results) for
    /// the mode GRIDGAME_TURN_MODE names (<see cref="TestKit.SoakTurnMode"/>), also written as
    /// <c>turn-mode-&lt;mode&gt;-&lt;label&gt;.json</c>. GG-T778 (tools/turn_modes.py): GRIDGAME_SOAK_STAGES=<id>[,<id>...] tallies
    /// only those stages (the campaign is still played in order up to the last of them, so each is met at its campaign
    /// level), GRIDGAME_SOAK_SEEDS=<n>[,<n>...] replaces the default seeds 1..3, and GRIDGAME_DIFFICULTY=1..10 plays that
    /// difficulty (unset keeps the raw, neutral rules this report always used).
    /// </summary>
    public class HeldTurnSoakReport
    {
        private sealed class Row
        {
            public int Runs, Wins, Moves, Guards, Forced;
            public float HeldSeconds, WindowSeconds;
            public readonly Dictionary<string, int> Mana = new Dictionary<string, int>();
            public int HealFromSupport, HealTotal, SupportBuffs, SupportHaste, SupportCleanse, SupportActions;
            // GG-T913 turn-mode metrics.
            public int HeroActions, EnemyTurns, Refusals, Pincers, Chains, GuardsForced, GuardsChosen;
            public long BattleTicks, EnemyOnlyTicks;
            public int EnemyStretches, DowntimeMax, DowntimeSum;
            public int DecisionSamples; public long HeroesSum, LegalActionsSum;
            public readonly Dictionary<string, int> Uses = new Dictionary<string, int>();
            public int ComboScoreSum, ComboBestScore; public ComboRank ComboBestRank;
            public readonly Dictionary<string, int> ComboAttacker = new Dictionary<string, int>();
            public readonly Dictionary<string, int> ComboSupporter = new Dictionary<string, int>();

            public void Add(Row o)
            {
                Runs += o.Runs; Wins += o.Wins; Moves += o.Moves; Guards += o.Guards; Forced += o.Forced;
                HeldSeconds += o.HeldSeconds; WindowSeconds += o.WindowSeconds;
                foreach (var kv in o.Mana) Mana[kv.Key] = (Mana.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
                HealFromSupport += o.HealFromSupport; HealTotal += o.HealTotal; SupportBuffs += o.SupportBuffs;
                SupportHaste += o.SupportHaste; SupportCleanse += o.SupportCleanse; SupportActions += o.SupportActions;
                HeroActions += o.HeroActions; EnemyTurns += o.EnemyTurns; Refusals += o.Refusals; Pincers += o.Pincers; Chains += o.Chains;
                GuardsForced += o.GuardsForced; GuardsChosen += o.GuardsChosen; BattleTicks += o.BattleTicks; EnemyOnlyTicks += o.EnemyOnlyTicks;
                EnemyStretches += o.EnemyStretches; DowntimeMax = System.Math.Max(DowntimeMax, o.DowntimeMax); DowntimeSum += o.DowntimeSum;
                DecisionSamples += o.DecisionSamples; HeroesSum += o.HeroesSum; LegalActionsSum += o.LegalActionsSum;
                Bump(Uses, o.Uses); Bump(ComboAttacker, o.ComboAttacker); Bump(ComboSupporter, o.ComboSupporter);
                ComboScoreSum += o.ComboScoreSum; ComboBestScore = System.Math.Max(ComboBestScore, o.ComboBestScore);
                if (o.ComboBestRank > ComboBestRank) ComboBestRank = o.ComboBestRank;
            }

            public static void Bump(Dictionary<string, int> into, Dictionary<string, int> from)
            {
                foreach (var kv in from) into[kv.Key] = (into.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            }

            public float BattleSeconds => (float)BattleTicks / BattleConfig.TicksPerSecond;
            public float EnemyOnlySeconds => (float)EnemyOnlyTicks / BattleConfig.TicksPerSecond;
            public float PerRun(float v) => Runs > 0 ? v / Runs : 0f;
            public float HeroesPerTurn => DecisionSamples > 0 ? (float)HeroesSum / DecisionSamples : 0f;
            public float LegalActionsPerTurn => DecisionSamples > 0 ? (float)LegalActionsSum / DecisionSamples : 0f;
            public float HeroActionsPerEnemyTurn => EnemyTurns > 0 ? (float)HeroActions / EnemyTurns : 0f;
            public float EnemyOnlySecondsPerStretch => EnemyStretches > 0 ? EnemyOnlySeconds / EnemyStretches : 0f;
            public int UsesOf(AbilityKind k) => Uses.TryGetValue(k.ToString(), out var v) ? v : 0;

            public int Turns => System.Math.Max(1, Moves + Guards);
            public float WinPct => Runs > 0 ? 100f * Wins / Runs : 0f;
            public float UsedPct => WindowSeconds > 0f ? 100f * HeldSeconds / WindowSeconds : 0f;
            public float AvgHeldSeconds => Moves > 0 ? HeldSeconds / Moves : 0f;
            public float GuardPct => 100f * Guards / Turns;
            public float ForcedPct => 100f * Forced / Turns;
            public int ManaOf(string reason) => Mana.TryGetValue(reason, out var v) ? v : 0;
            public int ManaOther => Mana.Where(m => m.Key != "guard" && m.Key != "pincer").Sum(m => m.Value);
        }

        /// <summary>Counts hero-side play only: the events of one command include the enemy turns that follow it, and a
        /// foe's support or a heal on a foe is not the party's. A heal counts as support healing only when its source is
        /// the supporter of the last Heal support action, before the next support action or pincer (PerformSupport emits
        /// the action, then each heal; recruit/trust events may sit between them).</summary>
        private static void Tally(Row row, BattleCommand cmd, List<BattleEvent> events, BattleState state, float window)
        {
            if (cmd is MoveCommand mv)
            {
                row.Moves++;
                row.HeldSeconds += window < 0f ? mv.HeldSeconds : System.Math.Min(mv.HeldSeconds, window);
                row.WindowSeconds += window < 0f ? mv.HeldSeconds : window;
            }
            else if (cmd is ShieldCommand) row.Guards++;
            var heroes = new HashSet<int>(state.Units.Where(u => u.IsHero).Select(u => u.Id));
            int supportHealer = -1;
            foreach (var e in events)
            {
                switch (e)
                {
                    case TurnTimeUpEvent _: row.Forced++; supportHealer = -1; break;
                    case PincerFormedEvent _: supportHealer = -1; break;
                    case ManaChargedEvent mc: row.Mana[mc.Reason ?? "?"] = row.ManaOf(mc.Reason ?? "?") + mc.Amount; break;
                    case SupportActionEvent sa:
                        supportHealer = -1;
                        if (!heroes.Contains(sa.SupporterId)) break;
                        row.SupportActions++;
                        if (sa.Effect == SupportEffect.Heal) supportHealer = sa.SupporterId;
                        else if (sa.Effect == SupportEffect.Haste) row.SupportHaste += sa.Targets.Count;
                        else if (sa.Effect == SupportEffect.Cleanse) row.SupportCleanse += sa.Targets.Count;
                        else if (sa.Effect == SupportEffect.Buff) row.SupportBuffs += sa.Targets.Count;
                        break;
                    case HealedEvent h:
                        if (!heroes.Contains(h.TargetId)) break;
                        row.HealTotal += h.Amount;
                        if (supportHealer >= 0 && h.SourceId == supportHealer) row.HealFromSupport += h.Amount;
                        break;
                }
            }
        }

        /// <summary>GG-T913: per-battle state the turn-mode metrics carry from one observe call to the next.</summary>
        private sealed class ModeTracker
        {
            public bool TooLittleTime;                 // BattleEngine.TooLittleTime before the next command (Guard forced vs chosen)
            public readonly Dictionary<(int Hero, int Slot), AbilityKind> SlotKinds = new Dictionary<(int Hero, int Slot), AbilityKind>();
            public int EnemyRun, LongestEnemyRun;      // enemy TurnStarted events since the last accepted hero action
        }

        /// <summary>
        /// GG-T913 (docs/TURN_MODES.md metrics): one command's events. Accepted = its events hold a PlayerControlEvent or
        /// BattleEndedEvent (BotRunner's own test); anything else is a bot refusal. A hero action is an accepted Move, UseSlot
        /// or Talk; an enemy turn a TurnStartedEvent whose unit is not a hero; enemy-only time the TimelineAdvanced ticks
        /// after the command's first enemy TurnStarted up to the PlayerControlEvent that hands control back.
        /// </summary>
        private static void TallyMode(Row row, ModeTracker t, BattleCommand cmd, List<BattleEvent> events, BattleState state)
        {
            bool accepted = events.Exists(e => e is PlayerControlEvent || e is BattleEndedEvent);
            if (!accepted) { row.Refusals++; return; }
            if (cmd is MoveCommand || cmd is UseSlotCommand || cmd is TalkCommand)
            {
                row.HeroActions++;
                t.EnemyRun = 0;
            }
            if (cmd is UseSlotCommand use)
            {
                var kind = t.SlotKinds.TryGetValue((use.HeroId, use.Slot), out var k) ? k.ToString() : "?";
                row.Uses[kind] = (row.Uses.TryGetValue(kind, out var n) ? n : 0) + 1;
            }
            else if (cmd is ShieldCommand)
            {
                if (t.TooLittleTime) row.GuardsForced++; else row.GuardsChosen++;
            }
            bool enemyStretch = false;
            foreach (var e in events)
            {
                switch (e)
                {
                    case TurnStartedEvent ts:
                        var u = state.UnitById(ts.UnitId);
                        if (u != null && u.IsHero) break;
                        row.EnemyTurns++;
                        t.EnemyRun++;
                        if (t.EnemyRun > t.LongestEnemyRun) t.LongestEnemyRun = t.EnemyRun;
                        if (!enemyStretch) { enemyStretch = true; row.EnemyStretches++; }
                        break;
                    case TimelineAdvancedEvent ta:
                        if (enemyStretch) row.EnemyOnlyTicks += ta.Ticks;
                        break;
                    case PlayerControlEvent _:
                        enemyStretch = false;
                        break;
                    case PincerFormedEvent pf:
                        if (pf.Team != Team.Hero) break;
                        row.Pincers++;
                        if (pf.ChainIndex > 0) row.Chains++;
                        break;
                }
            }
        }

        /// <summary>
        /// GG-T913: what the state offers before the next command. At the start of a player turn (PlayerInput, no hero has
        /// acted) samples the decisions: heroes able to act, and their legal actions - walkable tiles within
        /// <see cref="BattleEngine.MaxDragSteps"/> (Manhattan) when the hero can move, plus per Ready slot 1 for an Auto
        /// ability else its valid targets. Also remembers whether a Guard now would be forced and each slot's kind.
        /// </summary>
        private static void Observe(Row row, ModeTracker t, BattleState s)
        {
            t.TooLittleTime = !s.IsOver && s.Phase == BattlePhase.PlayerInput && BattleEngine.TooLittleTime(s);
            t.SlotKinds.Clear();
            foreach (var h in s.Units)
            {
                if (!h.IsHero) continue;
                for (int slot = 0; slot < h.Bar.Count; slot++)
                {
                    var a = BattleEngine.SlotAbility(s, h, slot);
                    if (a != null) t.SlotKinds[(h.Id, slot)] = a.Kind;
                }
            }
            if (s.IsOver || s.Phase != BattlePhase.PlayerInput) return;
            if (s.Units.Any(u => u.Alive && u.IsCommandable && TurnRules.HasActed(s, u))) return;
            var heroes = BattleEngine.HeroesLeftToAct(s);
            int legal = 0;
            int steps = BattleEngine.MaxDragSteps(s);
            foreach (var h in heroes)
            {
                if (BattleEngine.CanMove(s, h.Id))
                    for (int y = 0; y < BattleConfig.Rows; y++)
                        for (int x = 0; x < BattleConfig.Cols; x++)
                        {
                            var c = new Coord(x, y);
                            int d = System.Math.Abs(c.X - h.Pos.X) + System.Math.Abs(c.Y - h.Pos.Y);
                            if (d > 0 && d <= steps && s.Walkable(c)) legal++;
                        }
                for (int slot = 0; slot < h.Bar.Count; slot++)
                {
                    if (BattleEngine.GetSlotStatus(s, h.Id, slot) != SlotStatus.Ready) continue;
                    var a = BattleEngine.SlotAbility(s, h, slot);
                    legal += a != null && a.Mode == TargetMode.Auto ? 1 : BattleEngine.ValidTargets(s, h.Id, slot).Count;
                }
            }
            row.DecisionSamples++;
            row.HeroesSum += heroes.Count;
            row.LegalActionsSum += legal;
        }

        /// <summary>GG-T913: end-of-battle totals - battle seconds, the longest enemy-turn run and the combo results.</summary>
        private static void Finish(Row row, ModeTracker t, BattleState st)
        {
            row.BattleTicks += st.Clock;
            row.DowntimeMax = System.Math.Max(row.DowntimeMax, t.LongestEnemyRun);
            row.DowntimeSum += t.LongestEnemyRun;
            var r = st.Results;
            if (r == null) return;
            row.ComboScoreSum += r.BestComboScore;
            row.ComboBestScore = System.Math.Max(row.ComboBestScore, r.BestComboScore);
            if (r.BestComboRank > row.ComboBestRank) row.ComboBestRank = r.BestComboRank;
            Row.Bump(row.ComboAttacker, r.ComboAttackerPoints);
            Row.Bump(row.ComboSupporter, r.ComboSupporterPoints);
        }

        [TestCase(0.0f, "greedy")]
        [TestCase(0.5f, "casual")]
        public void Held_turn_guard_and_support_soak(float randomness, string label)
        {
            var c = Real;
            var rows = new Dictionary<string, Row>();
            var seedList = SoakSeeds();
            int seeds = seedList.Count;
            var onlyStages = SoakStages();
            var difficultyEnv = System.Environment.GetEnvironmentVariable("GRIDGAME_DIFFICULTY");
            int difficulty = string.IsNullOrWhiteSpace(difficultyEnv) ? DifficultyTable.Neutral : BotDifficulty.FromEnv(); // GG-T778
            foreach (int seed in seedList)
            {
                var s = Progression.NewGame(c, "Soak", Progression.StartingRoster);
                CampNpcs.JoinAll(s);
                int battleSeed = seed * 1000;
                var campaign = c.CampaignStages();
                int lastWanted = onlyStages == null ? campaign.Count - 1 : campaign.FindLastIndex(st => onlyStages.Contains(st.Id));
                for (int si = 0; si <= lastWanted; si++)
                {
                    var stage = campaign[si];
                    bool tally = onlyStages == null || onlyStages.Contains(stage.Id);
                    bool cleared = false;
                    for (int attempt = 0; attempt < 4 && !cleared; attempt++)
                    {
                        DialogueRunner.PlayPendingBeatsHeadless(c, s, stage.Id);
                        CampRoutine.Run(c, s);
                        var setup = BattleFactory.Build(c, s, stage.Id, battleSeed++, difficulty, SoakTurnMode); // GG-T777: GRIDGAME_TURN_MODE
                        Row row;
                        if (!tally) row = new Row(); // GG-T778: played to reach the listed stages, not reported
                        else if (!rows.TryGetValue(stage.Id, out row)) rows[stage.Id] = row = new Row();
                        row.Runs++;
                        float window = -1f;
                        var tracker = new ModeTracker();
                        var st = BotRunner.Play(c, setup, new HeroBot(battleSeed, randomness), 900, (cmd, ev, state) =>
                        {
                            bool accepted = ev.Exists(e => e is PlayerControlEvent || e is BattleEndedEvent);
                            if (cmd != null) { Tally(row, cmd, ev, state, window); TallyMode(row, tracker, cmd, ev, state); }
                            window = BattleEngine.TurnWindowSeconds(state); // the window of the turn the NEXT command plays
                            if (cmd == null || accepted) Observe(row, tracker, state); // a refusal leaves the state as it was
                        });
                        Finish(row, tracker, st);
                        if (st.Phase == BattlePhase.Victory) { row.Wins++; cleared = true; }
                        BattleFactory.Commit(c, s, st);
                    }
                    if (!cleared) break;
                }
            }
            var all = new Row();
            foreach (var r in rows.Values) all.Add(r);

            var sb = new StringBuilder();
            sb.AppendLine($"=== held-turn soak, {label} bot (randomness {randomness}), {seeds} seeds, difficulty {difficulty} ===");
            sb.AppendLine("stage                  win%  turn-used%  held-s  guard%  forced%  mana(guard/pincer/other)  heal(sup/all)  buffs haste cleanse");
            foreach (var kv in rows) sb.AppendLine(Line(kv.Key, kv.Value));
            sb.AppendLine(Line("ALL", all));
            var mode = SoakTurnMode;
            sb.AppendLine($"--- turn-mode metrics ({mode}) ---");
            sb.AppendLine("stage                  win%  hero-acts/run enemy-turns/run  secs/run  heroes x legal/turn  acts/enemy  downtime(max/avg) enemy-s/stretch  pincers chains  guard(forced/chosen)  skill/spell/item  refusals  combo(best/avg)");
            foreach (var kv in rows) sb.AppendLine(ModeLine(kv.Key, kv.Value));
            sb.AppendLine(ModeLine("ALL", all));
            TestContext.Out.WriteLine(sb.ToString());

            var outDir = System.Environment.GetEnvironmentVariable("GRIDGAME_SOAK_OUT");
            if (!string.IsNullOrEmpty(outDir))
            {
                System.IO.Directory.CreateDirectory(outDir);
                var json = new StringBuilder("{");
                json.Append($"\"label\":\"{label}\",\"randomness\":{F(randomness)},\"seeds\":{seeds},\"all\":{Json(all)},\"stages\":{{");
                json.Append(string.Join(",", rows.Select(kv => $"\"{kv.Key}\":{Json(kv.Value)}"))).Append("}}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, $"held-turn-{label}.json"), json.ToString());
                var modeJson = new StringBuilder("{");
                modeJson.Append($"\"mode\":\"{mode}\",\"label\":\"{label}\",\"randomness\":{F(randomness)},\"seeds\":{seeds},\"seedList\":[{string.Join(",", seedList)}],\"difficulty\":{difficulty},\"all\":{ModeJson(all)},\"stages\":{{");
                modeJson.Append(string.Join(",", rows.Select(kv => $"\"{kv.Key}\":{ModeJson(kv.Value)}"))).Append("}}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, $"turn-mode-{mode}-{label}.json"), modeJson.ToString());
            }
            Assert.That(rows, Is.Not.Empty);
        }

        /// <summary>GG-T778: GRIDGAME_SOAK_SEEDS=<n>[,<n>...] (default 1,2,3).</summary>
        private static List<int> SoakSeeds()
        {
            var v = System.Environment.GetEnvironmentVariable("GRIDGAME_SOAK_SEEDS");
            if (string.IsNullOrWhiteSpace(v)) return new List<int> { 1, 2, 3 };
            return v.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                .Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToList();
        }

        /// <summary>GG-T778: GRIDGAME_SOAK_STAGES=<id>[,<id>...], or null for every campaign stage. An id that is not a
        /// campaign stage throws, so a typo never silently measures nothing.</summary>
        private static HashSet<string> SoakStages()
        {
            var v = System.Environment.GetEnvironmentVariable("GRIDGAME_SOAK_STAGES");
            if (string.IsNullOrWhiteSpace(v)) return null;
            var set = new HashSet<string>(v.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries));
            var known = new HashSet<string>(Real.CampaignStages().Select(st => st.Id));
            var bad = set.Where(id => !known.Contains(id)).ToList();
            if (bad.Count > 0) throw new System.ArgumentException("GRIDGAME_SOAK_STAGES: not campaign stages: " + string.Join(", ", bad));
            return set;
        }

        private static string Line(string name, Row r) =>
            $"{name,-22} {r.WinPct,4:F0}%  {r.UsedPct,9:F0}%  {r.AvgHeldSeconds,6:F2}  {r.GuardPct,5:F0}%  {r.ForcedPct,6:F0}%  {r.ManaOf("guard"),7}/{r.ManaOf("pincer")}/{r.ManaOther}  {r.HealFromSupport,6}/{r.HealTotal}  {r.SupportBuffs,5} {r.SupportHaste,5} {r.SupportCleanse,7}";

        private static string ModeLine(string name, Row r) =>
            $"{name,-22} {r.WinPct,4:F0}%  {r.PerRun(r.HeroActions),13:F1} {r.PerRun(r.EnemyTurns),15:F1}  {r.PerRun(r.BattleSeconds),8:F1}  {r.HeroesPerTurn,6:F2} x {r.LegalActionsPerTurn,10:F1}  {r.HeroActionsPerEnemyTurn,10:F2}  {r.DowntimeMax,8}/{r.PerRun(r.DowntimeSum),-7:F1} {r.EnemyOnlySecondsPerStretch,15:F2}  {r.Pincers,7} {r.Chains,6}  {r.GuardsForced,10}/{r.GuardsChosen,-9}  {r.UsesOf(AbilityKind.Skill),5}/{r.UsesOf(AbilityKind.Spell)}/{r.UsesOf(AbilityKind.Item)}  {r.Refusals,8}  {r.ComboBestRank}/{r.PerRun(r.ComboScoreSum):F0}";

        private static string Obj(Dictionary<string, int> d) =>
            "{" + string.Join(",", d.OrderBy(kv => kv.Key).Select(kv => $"\"{kv.Key}\":{kv.Value}")) + "}";

        /// <summary>GG-T913: one row of the turn-mode comparison (raw sums plus derived rates), docs/TURN_MODES.md order.</summary>
        private static string ModeJson(Row r) =>
            "{" + $"\"runs\":{r.Runs},\"wins\":{r.Wins},\"winPct\":{F(r.WinPct)}," +
            $"\"heroActions\":{r.HeroActions},\"enemyTurns\":{r.EnemyTurns},\"battleSeconds\":{F(r.BattleSeconds)}," +
            $"\"heroActionsPerRun\":{F(r.PerRun(r.HeroActions))},\"enemyTurnsPerRun\":{F(r.PerRun(r.EnemyTurns))},\"secondsPerRun\":{F(r.PerRun(r.BattleSeconds))}," +
            $"\"decisionSamples\":{r.DecisionSamples},\"heroesPerTurn\":{F(r.HeroesPerTurn)},\"legalActionsPerTurn\":{F(r.LegalActionsPerTurn)}," +
            $"\"heroActionsPerEnemyTurn\":{F(r.HeroActionsPerEnemyTurn)}," +
            $"\"downtimeMax\":{r.DowntimeMax},\"downtimeAvg\":{F(r.PerRun(r.DowntimeSum))},\"enemyOnlySeconds\":{F(r.EnemyOnlySeconds)}," +
            $"\"enemyStretches\":{r.EnemyStretches},\"enemyOnlySecondsPerStretch\":{F(r.EnemyOnlySecondsPerStretch)}," +
            $"\"pincers\":{r.Pincers},\"chains\":{r.Chains},\"guardsForced\":{r.GuardsForced},\"guardsChosen\":{r.GuardsChosen}," +
            $"\"uses\":{Obj(r.Uses)},\"mana\":{Obj(r.Mana)},\"refusals\":{r.Refusals}," +
            $"\"comboBestRank\":\"{r.ComboBestRank}\",\"comboBestScore\":{r.ComboBestScore},\"comboScoreAvg\":{F(r.PerRun(r.ComboScoreSum))}," +
            $"\"comboAttackerPoints\":{Obj(r.ComboAttacker)},\"comboSupporterPoints\":{Obj(r.ComboSupporter)}" + "}";

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Json(Row r) =>
            "{" + $"\"runs\":{r.Runs},\"wins\":{r.Wins},\"moves\":{r.Moves},\"guards\":{r.Guards},\"forced\":{r.Forced}," +
            $"\"heldSeconds\":{F(r.HeldSeconds)},\"windowSeconds\":{F(r.WindowSeconds)}," +
            $"\"winPct\":{F(r.WinPct)},\"turnUsedPct\":{F(r.UsedPct)},\"avgHeldSeconds\":{F(r.AvgHeldSeconds)},\"guardPct\":{F(r.GuardPct)},\"forcedPct\":{F(r.ForcedPct)}," +
            $"\"mana\":{{{string.Join(",", r.Mana.OrderBy(kv => kv.Key).Select(kv => $"\"{kv.Key}\":{kv.Value}"))}}}," +
            $"\"supportActions\":{r.SupportActions},\"healFromSupport\":{r.HealFromSupport},\"healTotal\":{r.HealTotal}," +
            $"\"supportBuffs\":{r.SupportBuffs},\"supportHaste\":{r.SupportHaste},\"supportCleanse\":{r.SupportCleanse}" + "}";
    }
}
