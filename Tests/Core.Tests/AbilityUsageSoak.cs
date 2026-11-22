using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>
    /// GG-T246: a measurement soak, not a gate. With GAIDEN_ABILITY_RECORDS=&lt;dir&gt; it plays every hero class (in
    /// rotating parties of five, default bars, at each stage's recommended level) across the main campaign and writes
    /// one battle record per fight; `python tools/records.py --abilities &lt;dir&gt;` turns them into the per-ability
    /// usefulness table (docs/research/AbilityUsage.md). Unset, it does nothing.
    /// </summary>
    public class AbilityUsageSoak
    {
        [Test]
        public void Ability_usage_soak_across_every_hero_class()
        {
            var dir = Environment.GetEnvironmentVariable("GAIDEN_ABILITY_RECORDS");
            if (string.IsNullOrEmpty(dir)) { TestContext.Out.WriteLine("GAIDEN_ABILITY_RECORDS not set — nothing written"); return; }
            int rounds = int.TryParse(Environment.GetEnvironmentVariable("GAIDEN_ABILITY_ROUNDS"), out var n) ? n : 2;
            // GG-T255: GAIDEN_ABILITY_BOT=casual plays the casual bot (randomness 0.5), and GAIDEN_ABILITY_PER_STAGE=N
            // fights every stage N times with rotating parties, so POL-G1/G2/G7 get per-stage win rates.
            bool casual = Environment.GetEnvironmentVariable("GAIDEN_ABILITY_BOT") == "casual";
            int perStage = int.TryParse(Environment.GetEnvironmentVariable("GAIDEN_ABILITY_PER_STAGE"), out var ps) ? ps : 0;
            string source = casual ? "bot:casual" : "bot:greedy";
            // GG-T292: GAIDEN_TUNE="PincerPushbackSeconds=0.5;PushbackMaxSeconds=2" overrides BattleConfig's static tuning
            // fields for this soak only (a tuning sweep without a rebuild per value); restored when it ends.
            var restore = new List<(System.Reflection.FieldInfo f, object v)>();
            foreach (var kv in (Environment.GetEnvironmentVariable("GAIDEN_TUNE") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = kv.Split('=');
                var f = typeof(BattleConfig).GetField(parts[0].Trim(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                Assert.That(f != null && !f.IsLiteral && !f.IsInitOnly, $"GAIDEN_TUNE: {parts[0]} is not a tunable BattleConfig field");
                restore.Add((f, f.GetValue(null)));
                f.SetValue(null, Convert.ChangeType(parts[1].Trim(), f.FieldType, System.Globalization.CultureInfo.InvariantCulture));
            }
            // GAIDEN_PUSHBACK_SCALE=1.5 scales every ability's Pushback seconds (capped at PushbackMaxSeconds) for this soak.
            var pushes = Real.Abilities.Values.Where(a => a.Pushback > 0f).ToDictionary(a => a, a => a.Pushback);
            if (float.TryParse(Environment.GetEnvironmentVariable("GAIDEN_PUSHBACK_SCALE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale))
                foreach (var a in pushes.Keys) a.Pushback = Math.Min(BattleConfig.PushbackMaxSeconds, a.Pushback * scale);
            try { Soak(dir, rounds, casual, perStage, source); }
            finally
            {
                foreach (var (f, v) in restore) f.SetValue(null, v);
                foreach (var kv in pushes) kv.Key.Pushback = kv.Value;
            }
        }

        /// <summary>The party a campaign player fields at <paramref name="stage"/>: the starting three plus every JoinHero
        /// story beat that plays before it (a StageCleared beat for an earlier stage, a ThemeStart beat for its theme or
        /// an earlier one), capped at <see cref="Progression.MaxPartySize"/> (GG-T291/GG-T292).</summary>
        internal static int CampaignPartySize(ContentDb c, StageDef stage)
        {
            var order = c.CampaignStages().Select(st => st.Id).ToList();
            int at = order.IndexOf(stage.Id), joins = 0;
            foreach (var d in c.Dialogues.Values)
            {
                if (!d.Nodes.Any(n => n.Effects.Any(e => e.Type == DialogueEffectType.JoinHero))) continue;
                int when = d.Trigger.Kind == DialogueTriggerKind.StageCleared ? order.IndexOf(d.Trigger.Id) + 1
                         : d.Trigger.Kind == DialogueTriggerKind.ThemeStart ? FirstStageOf(c, d.Trigger.Id, order) : -1;
                if (when >= 0 && when <= at) joins++;
            }
            return Math.Min(Progression.MaxPartySize, Progression.StartingPartySize + joins);
        }

        /// <summary>The main campaign, in order: every campaign stage up to the postgame (whose stages carry a LevelOverride).
        /// GG-T344: was the first 15, which silently dropped Halcyon once The Still made the main campaign 18 stages.</summary>
        internal static List<StageDef> MainStages(ContentDb c) => c.CampaignStages().TakeWhile(st => st.LevelOverride == null).ToList();

        private static int FirstStageOf(ContentDb c, string theme, List<string> order)
        {
            var t = c.Campaign.Themes.FirstOrDefault(x => x.Id == theme);
            return t == null || t.Stages.Count == 0 ? -1 : order.IndexOf(t.Stages[0]);
        }

        /// <summary>GG-T276: the save a campaign player brings to each stage — gear forged and bought, abilities trained,
        /// talents spent, potions stocked — taken from one greedy bot managing its camp through the main campaign in order
        /// (story beats, then <see cref="CampRoutine"/>, then the fight, up to four tries a stage). Snapshot just before the
        /// stage's first try. A bare new hero is only what a player has at the first stage (no Forge or Merchant yet).</summary>
        internal static Dictionary<string, SaveData> CampaignSnapshots(ContentDb c, List<StageDef> stages, int difficulty)
        {
            var snaps = new Dictionary<string, SaveData>();
            var s = Progression.NewGame(c, "Snapshot");
            int seed = 7000;
            for (int si = 0; si < stages.Count; si++)
            {
                var stage = stages[si];
                bool cleared = false;
                for (int attempt = 0; attempt < 4 && !cleared; attempt++)
                {
                    DialogueRunner.PlayPendingBeatsHeadless(c, s, stage.Id);
                    // A seeded Masterwork roll per stage and try: unseeded, the camp's forging rolled off the clock, so the
                    // same content gave a different snapshot (and win rate) every run.
                    CampRoutine.Run(c, s, rng: new Rng(9000 + si * 4 + attempt));
                    if (attempt == 0) snaps[stage.Id] = SaveFiles.Deserialize(SaveFiles.Serialize(s));
                    var setup = BattleFactory.Build(c, s, stage.Id, seed++, difficulty);
                    var st = BotRunner.Play(c, setup, new HeroBot(seed, 0f), 3000);
                    cleared = st.Phase == BattlePhase.Victory;
                    BattleFactory.Commit(c, s, st);
                }
                if (!cleared) break;
            }
            return snaps;
        }

        /// <summary>GG-T276: <see cref="CampaignSnapshots"/> frozen on disk (artifacts/balance-snapshots/d&lt;difficulty&gt;,
        /// git-ignored), so a tune to one stage changes only that stage's fights and every later stage is judged against
        /// the same party (regenerated, any upstream change reshuffles the campaign's loot and so every later stage's
        /// gear). GAIDEN_ABILITY_SNAPSHOT=regen rebuilds them, to see a landed tune's knock-on once; the manifest records
        /// when and from which content they were made, and a content mismatch only warns.</summary>
        internal static Dictionary<string, SaveData> FrozenSnapshots(ContentDb c, List<StageDef> stages, int difficulty)
        {
            var dir = Path.Combine(RepoRoot, "artifacts", "balance-snapshots", $"d{difficulty}");
            var manifestPath = Path.Combine(dir, "manifest.json");
            string hash = ContentHash();
            bool regen = Environment.GetEnvironmentVariable("GAIDEN_ABILITY_SNAPSHOT") == "regen";
            if (!regen && File.Exists(manifestPath))
            {
                var manifest = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(manifestPath));
                var covers = new HashSet<string>(manifest["covers"]?.ToObject<List<string>>() ?? new List<string>());
                if (stages.All(st => covers.Contains(st.Id)))
                {
                    if ((string)manifest["contentHash"] != hash)
                        TestContext.Out.WriteLine($"balance snapshots ({manifest["createdUtc"]}) predate the current content; GAIDEN_ABILITY_SNAPSHOT=regen rebuilds them");
                    var loaded = new Dictionary<string, SaveData>();
                    foreach (var st in stages)
                    {
                        var f = Path.Combine(dir, st.Id + ".json");
                        if (File.Exists(f)) loaded[st.Id] = SaveFiles.Deserialize(File.ReadAllText(f));
                    }
                    TestContext.Out.WriteLine($"balance snapshots reused from {dir} ({manifest["createdUtc"]})");
                    return loaded;
                }
            }
            var snaps = CampaignSnapshots(c, stages, difficulty);
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.json")) File.Delete(f);
            foreach (var kv in snaps) File.WriteAllText(Path.Combine(dir, kv.Key + ".json"), SaveFiles.Serialize(kv.Value));
            File.WriteAllText(manifestPath, new Newtonsoft.Json.Linq.JObject
            {
                ["createdUtc"] = DateTime.UtcNow.ToString("o"),
                ["contentHash"] = hash,
                ["covers"] = new Newtonsoft.Json.Linq.JArray(stages.Select(st => st.Id)),
            }.ToString());
            TestContext.Out.WriteLine($"balance snapshots written to {dir}");
            return snaps;
        }

        /// <summary>A short hash of every content JSON file, to tell whether frozen snapshots predate the content.</summary>
        private static string ContentHash()
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            foreach (var f in Directory.GetFiles(ContentDir, "*.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                var bytes = File.ReadAllBytes(f);
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash).Substring(0, 16);
        }

        /// <summary>The soak's save for one fight: the stage's campaign snapshot with <paramref name="party"/> fielded — a
        /// hero the campaign already has keeps its gear, kit and talents; a rotating joiner comes in fresh.</summary>
        private static SaveData FromSnapshot(ContentDb c, SaveData snap, List<string> party)
        {
            var s = SaveFiles.Deserialize(SaveFiles.Serialize(snap));
            s.Party.Clear();
            foreach (var id in party)
            {
                if (s.Roster.All(h => !string.Equals(h.ClassId, id, StringComparison.OrdinalIgnoreCase))) s.Roster.Add(Progression.NewHero(c, id));
                s.Party.Add(id);
            }
            return s;
        }

        private static void Soak(string dir, int rounds, bool casual, int perStage, string source)
        {
            Directory.CreateDirectory(dir);
            var c = Real;
            var heroes = c.Classes.Values.Where(k => k.Team == Team.Hero).Select(k => k.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            var stages = MainStages(c);
            var plan = new List<(List<string> party, StageDef stage)>();
            // GG-T276: GAIDEN_ABILITY_GEAR=campaign (with GAIDEN_ABILITY_PARTY=campaign) fights each per-stage battle from
            // the save a camp-managing campaign arrives with, instead of bare new heroes (polish.py's default since 20c776fc),
            // frozen on disk between runs (FrozenSnapshots).
            Dictionary<string, SaveData> snaps = null;
            if (perStage > 0)
            {
                // GG-T292: GAIDEN_ABILITY_PARTY=campaign fields the party the campaign has at that stage (the starting three,
                // plus a rotating hero for each story-beat join before it, up to the cap of six) instead of a flat five.
                bool campaignParty = Environment.GetEnvironmentVariable("GAIDEN_ABILITY_PARTY") == "campaign";
                if (campaignParty && Environment.GetEnvironmentVariable("GAIDEN_ABILITY_GEAR") == "campaign")
                {
                    var only = Environment.GetEnvironmentVariable("GAIDEN_ABILITY_STAGE")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    int last = only == null ? stages.Count - 1 : stages.FindLastIndex(st => only.Contains(st.Id));
                    snaps = FrozenSnapshots(c, stages.Take(last + 1).ToList(), BotDifficulty.FromEnv());
                }
                // GG-T333: GAIDEN_ABILITY_WITH=<hero> fields that hero in every campaign party in place of
                // GAIDEN_ABILITY_REPLACE (default the starting Cleric); the rotation never draws it, so the same seeds and
                // joiners compare one hero against another of its role.
                var with = Environment.GetEnvironmentVariable("GAIDEN_ABILITY_WITH");
                var replace = Environment.GetEnvironmentVariable("GAIDEN_ABILITY_REPLACE") ?? "cleric";
                for (int si = 0; si < stages.Count; si++)
                    for (int k = 0; k < perStage; k++)
                    {
                        int start = (si * perStage + k) * 5;
                        if (!campaignParty) { plan.Add((Enumerable.Range(0, 5).Select(i => heroes[(start + i) % heroes.Count]).Distinct().ToList(), stages[si])); continue; }
                        // The campaign's own start (Tank, Striker, Healer) plus rotating joiners up to the stage's party size.
                        var p = Progression.StartingRoster.ToList();
                        for (int i = 0; p.Count < CampaignPartySize(c, stages[si]); i++)
                        {
                            var h = heroes[(start + i) % heroes.Count];
                            if (!p.Contains(h) && (string.IsNullOrEmpty(with) || (h != with && h != replace))) p.Add(h);
                        }
                        if (!string.IsNullOrEmpty(with)) p[p.IndexOf(replace)] = with;
                        plan.Add((p, stages[si]));
                    }
            }
            else
            {
                int b = 0;
                for (int round = 0; round < rounds; round++)
                    for (int start = 0; start < heroes.Count; start += 5, b++)
                        plan.Add((Enumerable.Range(0, 5).Select(i => heroes[(start + i + round * 2) % heroes.Count]).Distinct().ToList(), stages[b % stages.Count]));
            }
            // GAIDEN_ABILITY_STAGE=<id>[,<id>...] fights only those stages (GG-T344: a comma list).
            var onlyStages = Environment.GetEnvironmentVariable("GAIDEN_ABILITY_STAGE")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int difficulty = BotDifficulty.FromEnv(); // GG-T309: GAIDEN_DIFFICULTY=1..10 (default 10, the designed game)
            int battle = 0;
            foreach (var (party, stage) in plan)
                {
                    // GAIDEN_ABILITY_STAGE=<id> fights only that stage, with the same parties and seeds as the full run (GG-T274).
                    if (onlyStages != null && !onlyStages.Contains(stage.Id)) { battle++; continue; }
                    int seed = 1000 + battle;
                    var save = snaps != null && snaps.TryGetValue(stage.Id, out var snap) ? FromSnapshot(c, snap, party) : Progression.NewGame(c, "Usage", party);
                    // GG-T292: GAIDEN_LEVEL_OFFSET=-3 fights under-levelled, a stress test that shows how much skill matters.
                    int level = Math.Max(1, Progression.StageRecommendedLevel(c, save, stage) + (int.TryParse(Environment.GetEnvironmentVariable("GAIDEN_LEVEL_OFFSET"), out var lo) ? lo : 0));
                    // A campaign hero keeps a higher level it earned; everyone else is set to the stage's level.
                    foreach (var h in save.Roster) h.TotalXp = snaps != null ? Math.Max(h.TotalXp, Formulas.TotalXpForLevel(level)) : Formulas.TotalXpForLevel(level);
                    var setup = BattleFactory.Build(c, save, stage.Id, seed, difficulty);
                    var opening = new List<BattleEvent>();
                    var s = BattleEngine.Create(c, setup, opening);
                    var rec = new BattleRecorder(s, setup, source, opening);
                    var bot = new HeroBot(seed, casual ? 0.5f : 0f) { IgnoreBar = Environment.GetEnvironmentVariable("GAIDEN_BOT_BAR") == "off" };
                    // GG-T311: GAIDEN_BOT_THINK=<seconds> and GAIDEN_BOT_QUICK=<factor> sweep the bot's human drag model.
                    if (float.TryParse(Environment.GetEnvironmentVariable("GAIDEN_BOT_THINK"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var think)) bot.ThinkSeconds = think;
                    if (float.TryParse(Environment.GetEnvironmentVariable("GAIDEN_BOT_QUICK"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var quick)) bot.QuickThinkFactor = quick;
                    int refused = 0;
                    for (int i = 0; i < 3000 && !s.IsOver; i++) // GG-T308: a command is one hero action now, several a window
                    {
                        var ev = rec.Apply(s, bot.Decide(s));
                        refused = ev.Exists(e => e is PlayerControlEvent || e is BattleEndedEvent) ? 0 : refused + 1;
                        if (refused > 5) break;
                    }
                    File.WriteAllText(Path.Combine(dir, $"{(casual ? "casual" : "greedy")}_{stage.Id}_{battle:000}.json"), rec.Finish(s).ToString(Newtonsoft.Json.Formatting.None));
                    TestContext.Out.WriteLine($"{battle:000} {stage.Id} [{string.Join(",", party)}]: {s.Phase} wave {s.Wave + 1}");
                    battle++;
                }
        }
    }
}
