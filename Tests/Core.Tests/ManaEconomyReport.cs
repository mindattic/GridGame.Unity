using System.Collections.Generic;
using System.Linq;
using System.Text;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>
    /// Mana economy instrument (§3.1.0, GG-T247, GG-T1915): bots play every campaign stage once per seed with natural progression (a loss moves on; hard-won XP keeps) and report how full
    /// the party pool runs — share of player turns at capacity, grant points lost past capacity, average pool, when the bar
    /// first fills, where granted points come from and which spells spend them. Explicit: run on purpose, it is the tuning dashboard.
    /// </summary>
    [Explicit("measurement instrument (GG-T247); run with --filter ManaEconomyReport")]
    public class ManaEconomyReport
    {
        private sealed class Tally
        {
            public int Battles, Wins, Turns, TurnsAtCap, TurnsAfford2, PoolSum, Landed, Wasted, Casts, Spent, BattlesFull, FirstFullTurnSum;
            /// <summary>Per-battle shares summed, so one long losing grind can't swamp the pooled turn counts.</summary>
            public float AtCapShareSum, Afford2ShareSum;
            public readonly Dictionary<string, int> BySource = new Dictionary<string, int>();
            public readonly Dictionary<string, int> SpendBy = new Dictionary<string, int>();
        }

        /// <summary>The price of a 2-cost spell: the pool can pay for a mid spell.</summary>
        private const int Afford2 = 2 * BattleConfig.ManaPointsPerOrb;

        [Test]
        public void Mana_economy_table()
        {
            var c = Real;
            var sb = new StringBuilder();
            foreach (var (label, randomness) in new[] { ("greedy", 0f), ("casual", 0.5f) })
            {
                var t = new Tally();
                int seeds = int.TryParse(System.Environment.GetEnvironmentVariable("GRIDGAME_MANA_SEEDS"), out var ns) ? ns : 3;
                for (int seed = 1; seed <= seeds; seed++) PlayCampaign(c, randomness, seed, t);
                int n = System.Math.Max(1, t.Turns), b = System.Math.Max(1, t.Battles);
                sb.AppendLine($"── {label}: {t.Battles} battles, {t.Wins} won, every stage played once per seed, {t.Turns} player turns");
                sb.AppendLine($"   per battle: pool at capacity {100f * t.AtCapShareSum / b:0.0}% of its turns · ≥{Afford2} points {100f * t.Afford2ShareSum / b:0.0}%");
                sb.AppendLine($"   pool at capacity {100f * t.TurnsAtCap / n:0.0}% of turns · ≥{Afford2} points {100f * t.TurnsAfford2 / n:0.0}% · avg pool {(float)t.PoolSum / n:0.0}/{BattleConfig.PartyManaCapacity}");
                sb.AppendLine($"   grant points landed {t.Landed} · lost past capacity {t.Wasted} ({100f * t.Wasted / System.Math.Max(1, t.Landed + t.Wasted):0.0}%) · {t.Casts} casts spent {t.Spent}");
                sb.AppendLine($"   battles that filled the bar {100f * t.BattlesFull / System.Math.Max(1, t.Battles):0.0}% · first full at turn {(t.BattlesFull > 0 ? (float)t.FirstFullTurnSum / t.BattlesFull : 0):0.0} (avg)");
                sb.AppendLine("   sources: " + string.Join(", ", t.BySource.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
                sb.AppendLine("   spenders: " + string.Join(", ", t.SpendBy.OrderByDescending(kv => kv.Value).Take(15).Select(kv => $"{kv.Key} {kv.Value}")));
            }
            TestContext.Out.WriteLine(sb.ToString());
        }

        private static void PlayCampaign(GridGame.Core.Content.ContentDb c, float randomness, int seed, Tally t)
        {
            var s = Progression.NewGame(c, "Mana", Progression.StartingRoster);
            int battleSeed = seed * 1000;
            foreach (var stage in c.CampaignStages())
            {
                bool cleared = false;
                for (int attempt = 0; attempt < 1 && !cleared; attempt++)
                {
                    DialogueRunner.PlayPendingBeatsHeadless(c, s, stage.Id);
                    CampRoutine.Run(c, s);
                    var setup = BattleFactory.Build(c, s, stage.Id, battleSeed++);
                    int turn = 0, firstFull = -1, atCap = 0, afford2 = 0;
                    var st = BotRunner.Play(c, setup, new HeroBot(battleSeed, randomness), 900, (cmd, ev, state) =>
                    {
                        for (int i = 0; i < ev.Count; i++)
                        {
                            switch (ev[i])
                            {
                                case ManaGainedEvent o when o.Kind == ManaGainKind.Grant:
                                    t.Landed += o.Amount; t.Wasted += o.Wasted;
                                    t.BySource[o.Reason] = (t.BySource.TryGetValue(o.Reason, out var v) ? v : 0) + o.Amount;
                                    break;
                                case SpellManaSpentEvent sp:
                                    // A spell pays at command time, before its CastQueued/AbilityResolved in the same list.
                                    t.Casts++; t.Spent += sp.Points;
                                    var key = SpellAfter(ev, i) ?? "?";
                                    t.SpendBy[key] = t.SpendBy.TryGetValue(key, out var w) ? w + 1 : 1;
                                    break;
                                case PlayerControlEvent _:
                                    turn++; t.Turns++;
                                    t.PoolSum += state.PartyMana;
                                    if (state.PartyManaFull) { t.TurnsAtCap++; atCap++; if (firstFull < 0) firstFull = turn; }
                                    if (state.PartyMana >= Afford2) { t.TurnsAfford2++; afford2++; }
                                    break;
                            }
                        }
                    });
                    t.Battles++;
                    if (turn > 0) { t.AtCapShareSum += atCap / (float)turn; t.Afford2ShareSum += afford2 / (float)turn; }
                    if (firstFull >= 0) { t.BattlesFull++; t.FirstFullTurnSum += firstFull; }
                    if (st.Phase == BattlePhase.Victory) { cleared = true; t.Wins++; }
                    BattleFactory.Commit(c, s, st);
                }
            }
        }

        private static string SpellAfter(List<BattleEvent> ev, int from)
        {
            for (int j = from + 1; j < ev.Count; j++)
            {
                if (ev[j] is CastQueuedEvent q) return q.AbilityId;
                if (ev[j] is AbilityResolvedEvent r) return r.AbilityId;
            }
            return null;
        }
    }
}
