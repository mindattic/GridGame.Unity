using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>
    /// The Crucible (§22.10, GG-T95): a quick, endless survival battle fought from camp inside a normal saved
    /// campaign, for XP and loot without moving the story along. It is one generated <see cref="StageDef"/> with
    /// <see cref="MaxWaves"/> waves, so the normal BattleEngine wave loop runs it (wounds, mana and consumables
    /// carry wave to wave because it is one battle) until the party falls or leaves. Each wave is one enemy family
    /// (ClassDef.DropTable) the save has already met or reached in the campaign, at a level that starts from the
    /// party's and rises each wave, built from that family's authored encounter groups (GG-T184). Unlike any other non-victory end, a wipe or retreat here keeps everything
    /// earned (§22.2's exception, AMENDMENTS 2026-10-19), and the run never touches campaign progress, unlocks
    /// or the expedition.
    /// </summary>
    public static class Crucible
    {
        public const string IdPrefix = "crucible-";
        // The horde ramp's numbers live in BattleConfig (GG-T430); these names are kept for callers and tests.
        /// <summary>Waves in one run. Effectively endless: levels, foe counts and elites climb every wave.</summary>
        public const int MaxWaves = BattleConfig.CrucibleMaxWaves;
        /// <summary>Enemy levels gained per wave cleared.</summary>
        public const int LevelStep = BattleConfig.CrucibleLevelStep;
        public const int FirstWaveEnemies = BattleConfig.CrucibleFirstWaveEnemies;
        public const int MaxWaveEnemies = BattleConfig.CrucibleMaxWaveEnemies;
        /// <summary>Every this many waves, one more enemy joins each wave (up to <see cref="MaxWaveEnemies"/>).</summary>
        public const int WavesPerExtraEnemy = BattleConfig.CrucibleWavesPerExtraEnemy;

        public static bool IsStage(string stageId) => stageId != null && stageId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>The party's level for the Crucible's first wave: the rounded mean of the active party's levels.</summary>
        public static int PartyLevel(SaveData s) => Progression.PartyLevel(s);

        public static int WaveLevel(int startLevel, int wave) => startLevel + wave * LevelStep;

        public static int WaveEnemies(int wave) => Math.Min(MaxWaveEnemies, FirstWaveEnemies + wave / WavesPerExtraEnemy);

        /// <summary>Elites in wave <paramref name="wave"/> (0-based): none before <see cref="BattleConfig.CrucibleFirstEliteWave"/>,
        /// then one more every <see cref="BattleConfig.CrucibleWavesPerElite"/> waves, capped (and never more than the wave's foes).</summary>
        public static int WaveElites(int wave) =>
            wave < BattleConfig.CrucibleFirstEliteWave ? 0
            : Math.Min(Math.Min(BattleConfig.CrucibleMaxElites, WaveEnemies(wave)), 1 + (wave - BattleConfig.CrucibleFirstEliteWave) / BattleConfig.CrucibleWavesPerElite);

        /// <summary>Gold banked for clearing wave <paramref name="wave"/> (0-based), kept however the run ends.</summary>
        public static int WaveGold(int wave) => BattleConfig.CrucibleWaveGoldBase + BattleConfig.CrucibleWaveGoldPerWave * (wave + 1);

        /// <summary>Not mid-expedition: the Crucible is fought from a settled camp, and its full-HP reset would
        /// otherwise heal the party between an expedition's battles for free (§22.5).</summary>
        public static bool CanLaunch(ContentDb c, SaveData s, out string why)
        {
            why = null;
            if (s == null) { why = "No save loaded"; return false; }
            if (s.Expedition != null) { why = "Closed on an expedition"; return false; }
            if (!s.Party.Any(id => s.Hero(id) != null)) { why = "No party"; return false; }
            if (Families(c, s).Count == 0) { why = "No foes met yet"; return false; }
            return true;
        }

        /// <summary>The next run's seed: fixed by the save (its creation time and runs fought), so the same save
        /// replays the same gauntlet until it fights it, and every run after differs.</summary>
        public static int SeedFor(SaveData s)
        {
            unchecked
            {
                long t = s.CreatedUtc.Ticks;
                int h = (int)(t ^ (t >> 32));
                return h * 31 + (s.Crucible?.Runs ?? 0) * 7919 + 1;
            }
        }

        /// <summary>The campaign stages the save has reached: everything cleared plus the next one up.</summary>
        public static List<StageDef> StagesSoFar(ContentDb c, SaveData s)
        {
            var all = c.CampaignStages();
            if (all.Count == 0) return all;
            int last = Math.Min(all.Count - 1, Math.Max(0, s.HighestClearedStage + 1));
            return all.Take(last + 1).ToList();
        }

        /// <summary>Enemy families (grouped by ClassDef.DropTable) the run may draw from: every ordinary enemy the save
        /// has met (Bestiary) or that fights in the campaign so far. Bosses and multi-tile foes stay in their stages.</summary>
        public static List<List<ClassDef>> Families(ContentDb c, SaveData s)
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in s.Bestiary) if (kv.Value != null && (kv.Value.Seen || BestiaryKnowledge.Tier(kv.Value) > BestiaryKnowledge.None)) known.Add(kv.Key);
            foreach (var st in StagesSoFar(c, s))
                foreach (var w in st.Waves)
                    foreach (var e in w.Enemies) known.Add(e.Class);
            return known.Select(c.TryClass)
                .Where(d => d != null && d.Team == Team.Enemy && !string.IsNullOrEmpty(d.DropTable) && d.BossPhases.Count == 0
                            && !d.Tags.Contains("Boss") && (d.Footprint?.W ?? 1) <= 1 && (d.Footprint?.H ?? 1) <= 1)
                .GroupBy(d => d.DropTable, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.OrderBy(d => d.Id, StringComparer.Ordinal).ToList())
                .ToList();
        }

        /// <summary>Builds the run's stage: deterministic for (content, save state, seed).</summary>
        public static StageDef Generate(ContentDb c, SaveData s, int seed)
        {
            var rng = new Rng(seed);
            var families = Families(c, s);
            if (families.Count == 0) throw new InvalidOperationException("Crucible: the save has met no enemy family yet");
            var arenas = StagesSoFar(c, s);
            var arena = arenas.Count > 0 ? rng.Pick(arenas) : null;
            int start = PartyLevel(s);
            var stage = new StageDef
            {
                Id = IdPrefix + unchecked((uint)seed).ToString("x8"),
                Name = "The Crucible",
                Theme = arena?.Theme,
                Background = arena?.Background,
                Music = arena?.Music,
                IsTest = false,
                LevelOverride = start,
                Blocks = arena?.Blocks.Select(b => new Coord(b.X, b.Y)).ToList() ?? new List<Coord>(),
            };
            var order = new List<List<ClassDef>>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < MaxWaves; i++)
            {
                // Walk a fresh shuffle of every family, never the same family twice in a row (when there are two+).
                if (order.Count == 0)
                {
                    order = new List<List<ClassDef>>(families);
                    for (int k = order.Count - 1; k > 0; k--) { int j = rng.Range(0, k + 1); (order[k], order[j]) = (order[j], order[k]); }
                    var prev = stage.Waves.Count > 0 ? c.TryClass(stage.Waves[stage.Waves.Count - 1].Enemies[0].Class)?.DropTable : null;
                    if (order.Count > 1 && order[0][0].DropTable == prev) (order[0], order[order.Count - 1]) = (order[order.Count - 1], order[0]);
                }
                var pool = order[0];
                order.RemoveAt(0);
                var wave = new WaveDef();
                int level = WaveLevel(start, i), count = WaveEnemies(i);
                // GG-T184: the wave is one of the family's authored groups (mix + formation), each group used once
                // before any repeats; a family with none, or none that big, tops up with random foes as before.
                var group = EncounterCatalog.Pick(c, rng, pool[0].DropTable, count, level, used);
                if (group != null) { wave.Group = group.Id; wave.Enemies.AddRange(EncounterCatalog.Spawns(group, level)); }
                while (wave.Enemies.Count < count) wave.Enemies.Add(new SpawnDef { Class = rng.Pick(pool).Id, Level = level });
                // GG-T430: the horde ramps — the last few foes of a later wave are elites, a few levels above it.
                int elites = WaveElites(i);
                for (int k = 0; k < elites; k++) wave.Enemies[wave.Enemies.Count - 1 - k].Level = level + BattleConfig.CrucibleEliteLevelBonus;
                wave.ClearCoins = WaveGold(i);
                stage.Waves.Add(wave);
            }
            return stage;
        }

        /// <summary>Generates the save's next run and registers it in the in-memory content (never on disk) so the
        /// battle and results screens can look it up by id like any stage. Earlier runs' stages are pruned.</summary>
        public static StageDef Prepare(ContentDb c, SaveData s)
        {
            foreach (var old in c.Stages.Keys.Where(IsStage).ToList()) c.Stages.Remove(old);
            var stage = Generate(c, s, SeedFor(s));
            c.Stages[stage.Id] = stage;
            return stage;
        }

        /// <summary>Waves fully cleared: all of them on the (unlikely) clean sweep, else the index the party fell on.</summary>
        public static int WavesSurvived(BattleState battle) =>
            battle.Phase == BattlePhase.Victory ? battle.Stage.Waves.Count : Math.Max(0, battle.Wave);

        /// <summary>
        /// Commits a finished Crucible run (called through <see cref="BattleFactory.Commit"/>). However it ended —
        /// cleared, wiped, retreated or escaped — the save keeps ALL of it: gold, item drops, XP and level-ups,
        /// kills, bounty progress and gear wear. The party then recovers to full HP like after a defeat. Campaign
        /// progress (cleared stages, the frontier, unlocks, the last stage) and the expedition are never touched.
        /// </summary>
        public static BattleReport Commit(ContentDb c, SaveData s, BattleState battle)
        {
            var r = battle.Results;
            BattleEngine.SnapshotGear(battle); // a wipe/retreat never wrote end-of-battle wear; the run keeps it
            var rep = new BattleReport
            {
                Victory = battle.Phase == BattlePhase.Victory,
                Escaped = battle.Phase == BattlePhase.Escaped,
                Retreated = battle.Phase == BattlePhase.Retreated,
                StageId = battle.Stage.Id,
                StageName = battle.Stage.Name,
                StageTheme = battle.Stage.Theme,
                Crucible = true,
            };
            int slotsBefore = Progression.UnlockedSlots(s);
            Bargains.Rotate(s);

            foreach (var kv in r.ItemsConsumed) s.AddItem(kv.Key, -kv.Value);
            foreach (var id in r.Seen) BattleFactory.Entry(s, id).Seen = true;

            rep.Gold = r.Coins;
            s.Gold += r.Coins;
            s.LifetimeCoins += r.Coins;
            BattleFactory.GrantItems(c, s, rep, r.Items);
            foreach (var kv in r.Kills) BattleFactory.Entry(s, kv.Key).Defeated += kv.Value;

            var heroes = battle.Units.Where(u => u.IsHero && !u.IsGuest && !u.IsVisitor).ToList();
            rep.XpEach = heroes.Count > 0 ? r.Xp : 0;
            foreach (var u in heroes)
            {
                var h = s.Hero(u.Def.Id);
                if (h == null) continue;
                int before = Progression.Level(h);
                h.TotalXp += Domestication.XpFor(h, rep.XpEach);
                int after = Progression.Level(h);
                if (after > before) rep.LevelUps.Add((h.ClassId, before, after));
                h.SkillPoints += Formulas.SkillPointsForLevelUp(before, after);
                BattleFactory.SyncGear(s, h, u, r);
            }
            BattleFactory.KeepBrokenFragments(s, r);
            foreach (var id in s.Party) { var h = s.Hero(id); if (h != null) h.Hp = 0; } // everyone staggers out and recovers
            rep.BountyProgress = Bounties.RecordKills(c, s, r.Kills);

            rep.CrucibleWaves = WavesSurvived(battle);
            s.Crucible ??= new CrucibleSave();
            s.Crucible.Runs++;
            s.Crucible.TotalWaves += rep.CrucibleWaves;
            if (rep.CrucibleWaves > s.Crucible.Best) { s.Crucible.Best = rep.CrucibleWaves; rep.CrucibleNewBest = true; }
            rep.CrucibleBest = s.Crucible.Best;
            if (rep.Gold > s.Crucible.BestGold) s.Crucible.BestGold = rep.Gold;

            rep.NewSlots = Progression.UnlockedSlots(s) - slotsBefore;
            s.UpdatedUtc = DateTime.UtcNow;
            return rep;
        }
    }
}
