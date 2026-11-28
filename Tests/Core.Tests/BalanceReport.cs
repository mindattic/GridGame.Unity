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
    /// Balance instrument (bible §0.3 pacing targets): plays the campaign in order with natural progression —
    /// XP, carried wounds, inn rests when affordable, retries on defeat — for bots of different skill, and
    /// reports win rate, actions per battle and HP left. Asserts only the guard rails (no stalls; a strong
    /// bot can finish the campaign); the table is the tuning dashboard.
    /// </summary>
    public class BalanceReport
    {

        private sealed class Row { public int Runs, Wins, Actions, HpPct; }

        /// <summary>GG-T448: where a run's gold went — battle pay ("earned") against each camp phase's spend — summed over
        /// the seeds of one table row, plus the purse after each stage.</summary>
        private Dictionary<string, int> ledger = new Dictionary<string, int>();
        private Dictionary<string, int> ledgerBank = new Dictionary<string, int>();
        private static void Add(Dictionary<string, int> d, string k, int v) { d.TryGetValue(k, out var had); d[k] = had + v; }
        /// <summary>GG-T540: the same ledger cut to the main campaign (through campaign.json's Ending stage) and per theme
        /// (region) of it — the scope of the repair-share target in BIBLE §25.0.11.</summary>
        private Dictionary<string, int> mainLedger = new Dictionary<string, int>();
        private Dictionary<string, Dictionary<string, int>> themeLedger = new Dictionary<string, Dictionary<string, int>>();

        private Dictionary<string, Row> Campaign(float randomness, int seed, int maxAttemptsPerStage, out bool finished, bool manageCamp = false) =>
            Campaign(randomness, seed, maxAttemptsPerStage, out finished, out _, manageCamp, Progression.StartingRoster, true);

        /// <param name="story">Play each stage's pending story beats first (headless, first choice) so the story
        /// recruits join the small starting party the way they do in the game (GG-T118).</param>
        private Dictionary<string, Row> Campaign(float randomness, int seed, int maxAttemptsPerStage, out bool finished, out SaveData save, bool manageCamp, IReadOnlyList<string> startingParty, bool story)
        {
            var c = Real;
            var s = save = Progression.NewGame(c, "Sim", startingParty);
            // GG-T119: Camp NPCs join at story beats. A no-story row is the pre-story yardstick, so it has every
            // service from the start, as every save did before; story rows meet them through the headless beats.
            if (!story) CampNpcs.JoinAll(s);
            var rows = new Dictionary<string, Row>();
            var stages = c.CampaignStages();
            // "Finished" is the main campaign, through campaign.json's Ending stage (GG-T446): the owner decided
            // 2026-11-17 that the postgame after it is an intended grind a non-hunting bot isn't expected to finish.
            // The bot still plays on into the postgame so its rows stay measured.
            string endingStage = c.Campaign.Ending?.Stage;
            finished = endingStage == null;
            int battleSeed = seed * 1000;
            for (int si = 0; si < stages.Count; si++)
            {
                var stage = stages[si];
                bool cleared = false;
                for (int attempt = 0; attempt < maxAttemptsPerStage && !cleared; attempt++)
                {
                    if (story) DialogueRunner.PlayPendingBeatsHeadless(c, s, stage.Id);
                    var visit = new Dictionary<string, int>();
                    if (manageCamp)
                    {
                        // GG-T276: a seeded Masterwork roll per campaign seed, stage and try. Unseeded, the forge rolled off the
                        // clock, so the same content gave a different ledger (1/4 vs 3/4 finished) every run.
                        var camp = CampRoutine.Run(c, s, visit, new GridGame.Core.Rng(unchecked(seed * 100003 + si * 16 + attempt)));
                        // GG-T540: how often the routine reaches for the Blacksmith, not just what it paid him.
                        Add(visit, "#repairs", camp.Count(m => m.StartsWith("Repaired ")));
                        Add(visit, "#salvages", camp.Count(m => m.StartsWith("Salvaged ")));
                    }
                    else if (CampNpcs.Joined(s, CampNpcs.Innkeeper) && s.Roster.Any(h => Inn.MissingHp(c, s, h) > 0)) { TravelRules.MakeCamp(s); CampRestService.Rest(s, out _); } // GG-T1790: free camp rest
                    var setup = BattleFactory.Build(c, s, stage.Id, battleSeed++, BotDifficulty.FromEnv()); // GG-T309
                    var st = BotRunner.Play(c, setup, new HeroBot(battleSeed, randomness), 3000); // the first battle is a 20-wave gauntlet; GG-T311: forced Guards count
                    if (!rows.TryGetValue(stage.Id, out var row)) rows[stage.Id] = row = new Row();
                    row.Runs++;
                    row.Actions += st.Results.ActionsTaken;
                    var heroes = st.Units.Where(u => u.IsHero).ToList();
                    row.HpPct += (int)(100f * heroes.Sum(u => u.Alive ? u.Hp : 0) / heroes.Sum(u => u.MaxHp));
                    if (st.Phase == BattlePhase.Victory) { row.Wins++; cleared = true; }
                    int goldBefore = s.Gold;
                    Add(visit, "#breaks", st.Results.BrokenWeapons.Values.Sum(l => l.Count) + st.Results.BrokenArmors.Values.Sum(l => l.Count));
                    var report = BattleFactory.Commit(c, s, st);
                    Add(visit, "earned", s.Gold - goldBefore);
                    Add(visit, "#battles", 1);
                    Add(visit, "#dust", report.DustEarned); // GG-T676: all Mana Dust, of which #orbdust came from unspent orbs
                    Add(visit, "#orbdust", report.OrbDust);
                    foreach (var kv in visit)
                    {
                        Add(ledger, kv.Key, kv.Value);
                        if (finished) continue; // GG-T540: the main campaign ends at the Ending stage; the postgame is out of scope
                        Add(mainLedger, kv.Key, kv.Value);
                        string theme = c.Campaign.Themes.FirstOrDefault(t => t.Stages.Contains(stage.Id))?.Id ?? "?";
                        if (!themeLedger.TryGetValue(theme, out var tl)) themeLedger[theme] = tl = new Dictionary<string, int>();
                        Add(tl, kv.Key, kv.Value);
                    }
                }
                ledgerBank[stage.Id] = (ledgerBank.TryGetValue(stage.Id, out var b) ? b : 0) + s.Gold;
                if (cleared && string.Equals(stage.Id, endingStage, System.StringComparison.OrdinalIgnoreCase)) finished = true;
                if (!cleared) break;
            }
            // GG-T119: every Camp NPC is met on the road, early enough that a finished run has them all.
            if (finished)
                Assert.That(CampNpcs.All.Where(n => !CampNpcs.Joined(s, n)), Is.Empty, "a finished campaign should have met every Camp NPC");
            return rows;
        }
        /// <summary>The pre-GG-T118 starting five (the default trio plus the two story recruits' preferred classes) —
        /// the baseline the small-start rows are balanced against.</summary>
        private static readonly string[] FiveStart = Progression.StartingRoster.Concat(new[] { "redmage", "engineer" }).ToArray();

        /// <summary>Every real new game starts small (GG-T118) and grows at story beats, so the default rows play
        /// the trio with story recruits. "no-story" keeps the trio short (GG-T87's trio-start row) so the camp
        /// routine's Summon Circle step always has an open slot to fill; "five-start" is the old full-party start,
        /// kept as the yardstick for the early-stage rebalance.</summary>
        [TestCase(0.0f, "greedy", false, 3, true)]
        [TestCase(0.5f, "casual", false, 3, true)]
        [TestCase(0.85f, "button-masher", false, 3, true)]
        [TestCase(0.0f, "greedy+camp", true, 3, true)]
        [TestCase(0.5f, "casual+camp", true, 3, true)]
        [TestCase(0.0f, "greedy+camp trio no-story", true, 3, false)]
        [TestCase(0.0f, "greedy five-start (pre-GG-T118)", false, 5, false)]
        public void Campaign_pacing_table(float randomness, string label, bool manageCamp, int startingHeroes, bool story)
        {
            var total = new Dictionary<string, Row>();
            ledger = new Dictionary<string, int>(); ledgerBank = new Dictionary<string, int>();
            mainLedger = new Dictionary<string, int>(); themeLedger = new Dictionary<string, Dictionary<string, int>>();
            int finishedRuns = 0, recruits = 0, bountiesClaimed = 0, bountySp = 0, roster = 0, spSpent = 0, spBanked = 0;
            const int seeds = 4;
            var party = FiveStart.Take(startingHeroes).ToList();
            for (int seed = 1; seed <= seeds; seed++)
            {
                var rows = Campaign(randomness, seed, 4, out bool finished, out var save, manageCamp, party, story);
                recruits += save.Recruits;
                roster += save.Roster.Count;
                bountiesClaimed += save.Bounty.Claimed.Count;
                bountySp += Bounties.SkillPointsClaimed(Real, save); // GG-T134: skill points paid each party hero
                // GG-T54: the skill-point sink — what the camp routine put into trees vs. what's still banked at the end.
                {
                    var hexBoard = SkillHexProgress.Board(Real); // SP actually paid: owned nodes outside the free class Start path
                    spSpent += save.Roster.Sum(h => hexBoard == null ? 0
                        : SkillHexProgress.Owned(h).Nodes.Where(pt => !hexBoard.StartOf(h.ClassId).Contains(pt)).Sum(hexBoard.Price));
                }
                spBanked += save.Roster.Sum(h => h.SkillPoints);
                if (finished) finishedRuns++;
                foreach (var kv in rows)
                {
                    if (!total.TryGetValue(kv.Key, out var t)) total[kv.Key] = t = new Row();
                    t.Runs += kv.Value.Runs; t.Wins += kv.Value.Wins; t.Actions += kv.Value.Actions; t.HpPct += kv.Value.HpPct;
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine($"=== {label} bot (randomness {randomness}) — campaign finished {finishedRuns}/{seeds} — summon recruits {recruits}, roster at the end {roster / (float)seeds:F1}, bounties claimed {bountiesClaimed} (+{bountySp / (float)seeds:F1} SP each per run), roster SP spent {spSpent / (float)seeds:F1} / banked {spBanked / (float)seeds:F1} per run ===");
            sb.AppendLine("stage                  win%   actions   hp-left%");
            foreach (var kv in total)
                sb.AppendLine($"{kv.Key,-22} {100 * kv.Value.Wins / kv.Value.Runs,4}%   {kv.Value.Actions / (float)kv.Value.Runs,7:F1}   {kv.Value.HpPct / kv.Value.Runs,6}%");
            if (manageCamp)
            {
                sb.AppendLine("gold per run: " + string.Join(", ", ledger.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / seeds}")));
                sb.AppendLine("purse after stage: " + string.Join(", ", ledgerBank.Select(kv => $"{kv.Key} {kv.Value / seeds}")));
            }
            TestContext.Out.WriteLine(sb.ToString());
            // GG-T311: at the designed difficulty a party that never spends its gear, gold or skill points no longer
            // finishes (gaps too short to act in cost it turns); a managed camp still must.
            if (randomness == 0f && manageCamp) Assert.That(finishedRuns >= 1, Is.True, "a greedy bot that manages its camp should be able to finish the campaign");
            // GG-T87: the camp routine must actually drive the Summon Circle (given an open slot) as part of organic
            // progression. GG-T431: bots skip the Bounty Board (hunts are the player's own), so no bounty is claimed.
            Assert.That(bountiesClaimed, Is.Zero, "the camp routine never works the bounty board");
            if (manageCamp && !story && startingHeroes < Progression.MaxPartySize) Assert.That(recruits > 0, Is.True, "the camp routine never recruited into an open party slot");
            // GG-T54: a managed campaign actually spends the skill points it earns.
            if (manageCamp && finishedRuns > 0) Assert.That(spSpent > 0, Is.True, "the camp routine never spent a skill point over a played-out campaign");
        }

        /// <summary>GG-T540: the economy instrument behind <c>tools/polish.py --balance</c>. Plays the managed-camp rows
        /// (greedy and casual, trio start with story recruits, 4 seeds each) and writes where the main campaign's gold went —
        /// battle pay against each camp phase, per run and per theme, plus repair/salvage/break counts — as JSON to
        /// GRIDGAME_ECONOMY_OUT. Ignored without that variable, so the regular suite doesn't play these campaigns twice.</summary>
        /// <summary>GG-T276: campaign seeds per economy row (GRIDGAME_ECONOMY_SEEDS, default 4); more seeds average out a single seed's luck.</summary>
        private static readonly int EconomySeeds =
            int.TryParse(System.Environment.GetEnvironmentVariable("GRIDGAME_ECONOMY_SEEDS"), out var es) && es > 0 ? es : 4;

        [Test]
        public void Economy_ledger()
        {
            var outPath = System.Environment.GetEnvironmentVariable("GRIDGAME_ECONOMY_OUT");
            if (string.IsNullOrEmpty(outPath)) Assert.Ignore("set GRIDGAME_ECONOMY_OUT (tools/polish.py --balance does)");
            int seeds = EconomySeeds;
            var json = new StringBuilder("{");
            string Obj(Dictionary<string, int> d) => "{" + string.Join(",", d.Select(kv => $"\"{kv.Key}\":{kv.Value / (float)seeds:F1}")) + "}";
            var rows = new[] { (0.0f, "greedy+camp"), (0.5f, "casual+camp") };
            for (int i = 0; i < rows.Length; i++)
            {
                var (randomness, label) = rows[i];
                ledger = new Dictionary<string, int>(); ledgerBank = new Dictionary<string, int>();
                mainLedger = new Dictionary<string, int>(); themeLedger = new Dictionary<string, Dictionary<string, int>>();
                int finishedRuns = 0;
                for (int seed = 1; seed <= seeds; seed++)
                {
                    Campaign(randomness, seed, 4, out bool finished, out _, true, Progression.StartingRoster, true);
                    if (finished) finishedRuns++;
                }
                json.Append(i > 0 ? "," : "").Append($"\"{label}\":{{\"finished\":{finishedRuns},\"seeds\":{seeds},\"main\":{Obj(mainLedger)},\"themes\":{{");
                json.Append(string.Join(",", themeLedger.Select(kv => $"\"{kv.Key}\":{Obj(kv.Value)}"))).Append("}}");
                TestContext.Out.WriteLine($"{label}: main campaign gold per run: " + string.Join(", ", mainLedger.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / seeds}")));
            }
            json.Append("}");
            System.IO.File.WriteAllText(outPath, json.ToString());
        }
    }
}
