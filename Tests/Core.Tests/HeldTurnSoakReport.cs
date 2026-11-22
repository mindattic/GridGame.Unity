using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
    /// nothing stalls. GG-T595: with GAIDEN_SOAK_OUT set to a folder, each bot's table is also written there as
    /// <c>held-turn-&lt;label&gt;.json</c> (raw sums and derived rates) for the GG-T596 tuning pass.
    /// </summary>
    public class HeldTurnSoakReport
    {
        private sealed class Row
        {
            public int Runs, Wins, Moves, Guards, Forced;
            public float HeldSeconds, WindowSeconds;
            public readonly Dictionary<string, int> Mana = new Dictionary<string, int>();
            public int HealFromSupport, HealTotal, SupportBuffs, SupportHaste, SupportCleanse, SupportActions;

            public void Add(Row o)
            {
                Runs += o.Runs; Wins += o.Wins; Moves += o.Moves; Guards += o.Guards; Forced += o.Forced;
                HeldSeconds += o.HeldSeconds; WindowSeconds += o.WindowSeconds;
                foreach (var kv in o.Mana) Mana[kv.Key] = (Mana.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
                HealFromSupport += o.HealFromSupport; HealTotal += o.HealTotal; SupportBuffs += o.SupportBuffs;
                SupportHaste += o.SupportHaste; SupportCleanse += o.SupportCleanse; SupportActions += o.SupportActions;
            }

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

        [TestCase(0.0f, "greedy")]
        [TestCase(0.5f, "casual")]
        public void Held_turn_guard_and_support_soak(float randomness, string label)
        {
            var c = Real;
            var rows = new Dictionary<string, Row>();
            const int seeds = 3;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var s = Progression.NewGame(c, "Soak", Progression.StartingRoster);
                CampNpcs.JoinAll(s);
                int battleSeed = seed * 1000;
                foreach (var stage in c.CampaignStages())
                {
                    bool cleared = false;
                    for (int attempt = 0; attempt < 4 && !cleared; attempt++)
                    {
                        DialogueRunner.PlayPendingBeatsHeadless(c, s, stage.Id);
                        CampRoutine.Run(c, s);
                        var setup = BattleFactory.Build(c, s, stage.Id, battleSeed++);
                        if (!rows.TryGetValue(stage.Id, out var row)) rows[stage.Id] = row = new Row();
                        row.Runs++;
                        float window = -1f;
                        var st = BotRunner.Play(c, setup, new HeroBot(battleSeed, randomness), 900, (cmd, ev, state) =>
                        {
                            if (cmd != null) Tally(row, cmd, ev, state, window);
                            window = BattleEngine.TurnWindowSeconds(state); // the window of the turn the NEXT command plays
                        });
                        if (st.Phase == BattlePhase.Victory) { row.Wins++; cleared = true; }
                        BattleFactory.Commit(c, s, st);
                    }
                    if (!cleared) break;
                }
            }
            var all = new Row();
            foreach (var r in rows.Values) all.Add(r);

            var sb = new StringBuilder();
            sb.AppendLine($"=== held-turn soak, {label} bot (randomness {randomness}), {seeds} seeds ===");
            sb.AppendLine("stage                  win%  turn-used%  held-s  guard%  forced%  mana(guard/pincer/other)  heal(sup/all)  buffs haste cleanse");
            foreach (var kv in rows) sb.AppendLine(Line(kv.Key, kv.Value));
            sb.AppendLine(Line("ALL", all));
            TestContext.Out.WriteLine(sb.ToString());

            var outDir = System.Environment.GetEnvironmentVariable("GAIDEN_SOAK_OUT");
            if (!string.IsNullOrEmpty(outDir))
            {
                System.IO.Directory.CreateDirectory(outDir);
                var json = new StringBuilder("{");
                json.Append($"\"label\":\"{label}\",\"randomness\":{F(randomness)},\"seeds\":{seeds},\"all\":{Json(all)},\"stages\":{{");
                json.Append(string.Join(",", rows.Select(kv => $"\"{kv.Key}\":{Json(kv.Value)}"))).Append("}}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, $"held-turn-{label}.json"), json.ToString());
            }
            Assert.That(rows, Is.Not.Empty);
        }

        private static string Line(string name, Row r) =>
            $"{name,-22} {r.WinPct,4:F0}%  {r.UsedPct,9:F0}%  {r.AvgHeldSeconds,6:F2}  {r.GuardPct,5:F0}%  {r.ForcedPct,6:F0}%  {r.ManaOf("guard"),7}/{r.ManaOf("pincer")}/{r.ManaOther}  {r.HealFromSupport,6}/{r.HealTotal}  {r.SupportBuffs,5} {r.SupportHaste,5} {r.SupportCleanse,7}";

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
