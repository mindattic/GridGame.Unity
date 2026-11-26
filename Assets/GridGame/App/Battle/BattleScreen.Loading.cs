using System;
using System.Collections;
using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1205: the stage load. The router runs <see cref="Prepare"/> behind its fade before the battle is first mounted
    /// (campaign, Crucible, automation and `-demo -endless` battles alike, all built as a BattleScreen): it builds the
    /// battle, releases the last stage's art, unloads what nothing draws and collects, then preloads this stage's
    /// <see cref="StageTextureSet"/> into <see cref="ArtScope.Stage"/> a few textures a frame under a progress bar. The
    /// scope is released when the battle leaves; nothing is swapped while it plays.
    /// </summary>
    public sealed partial class BattleScreen
    {
        /// <summary>Textures preloaded per frame.</summary>
        private const int PreloadChunk = 6;
        /// <summary>The battle holding <see cref="ArtScope.Stage"/> (only it releases the scope when it leaves).</summary>
        private static BattleScreen stageOwner;

        private bool built;
        private BattleSetup setup;
        private List<BattleEvent> openingEvents;

        public override IEnumerator Prepare(Action<float> progress)
        {
            if (State != null) yield break;
            CreateState();
            progress(0.05f);
            stageOwner = this;
            yield return Art.ReleaseAndCollect(ArtScope.Stage);
            progress(0.25f);
            Art.BeginScope(ArtScope.Stage);
            var chunks = Chunks(StageTextureSet.For(App.Content, setup));
            for (int i = 0; i < chunks.Count; i++)
            {
                Art.Preload(chunks[i]);
                progress(0.25f + 0.75f * (i + 1) / chunks.Count);
                yield return null;
            }
            // GG-T1207: from here on, a Stage load the set missed is warned about while this battle is the mounted screen.
            Art.WatchStageLoads(() => stageOwner == this && App.Router != null && ReferenceEquals(App.Router.Current, this));
        }

        /// <summary>Builds the setup, the engine state and the recorder (once); Build starts the telemetry and playback.</summary>
        private void CreateState()
        {
            // GG-T539: Settings › How to play › Replay tutorials brings the lessons back on the first stage too.
            bool tutorial = setupOverride == null && !InCrucible && App.Save != null && !App.Save.TutorialDone && (App.Save.HighestClearedStage < 0 || IsFirstStage)
                && !CommandLine.Has("-autobattle") && !DemoMode.Active && !TestMode.Active;
            // A Crucible run is seeded by the save (§22.10) so the same save replays the same gauntlet; the tutorial battle
            // always plays the same (GG-T542), so its lessons' set pieces land the same way every time.
            int seed = tutorial ? TutorialSeed : InCrucible && App.Save != null ? Crucible.SeedFor(App.Save) : Environment.TickCount;
            setup = setupOverride ?? BattleFactory.Build(App.Content, App.Save, stageId, seed, App.Settings?.Difficulty?.Level ?? DifficultyTable.Default,
                App.Settings?.TurnMode ?? TurnMode.PartyWindow); // §2.13 (GG-T773): the save's lock, else the profile's
            setup.Tutorial = tutorial;
            if (setupOverride == null && !tutorial) Wanderers.Apply(App.Content, App.Save, setup); // GG-T342: maybe a wanderer to win over
            if (setupOverride == null && DevNextWanderer != null) { setup.Wanderer = new WandererSpec { Class = DevNextWanderer, Wave = 0 }; DevNextWanderer = null; }
            // GG-T532: a wanderer shows either version of its class (when the male art exists), the same for this battle's
            // replay and its talk after; the player still picks when they join.
            HeroLooks.ClearShown();
            if (setup.Wanderer?.Class is string wc && App.Save?.Hero(wc) == null && Art.HasMaleArt(wc))
                HeroLooks.Show(wc, HeroVariants.Roll(setup.Seed));
            openingEvents = new List<BattleEvent>();
            State = BattleEngine.Create(App.Content, setup, openingEvents);
            recorder = new BattleRecorder(State, setup, "player", openingEvents);
        }

        /// <summary>Drops this battle's stage art when it leaves (the next stage's load unloads and collects it).</summary>
        private void ReleaseStageArt()
        {
            ReleaseRecruitArt(); // GG-T1206: a recruit dialogue cut short by the battle leaving
            if (stageOwner != this) return;
            stageOwner = null;
            Art.WatchStageLoads(null); // GG-T1207
            Art.ReleaseScope(ArtScope.Stage);
        }

        /// <summary><paramref name="set"/> split into sets of at most <see cref="PreloadChunk"/> entries (the theme's tile and
        /// backdrops count as one).</summary>
        private static List<StageTextureSet> Chunks(StageTextureSet set)
        {
            var parts = new List<StageTextureSet>();
            StageTextureSet cur = null;
            int n = 0;
            void Add(Action<StageTextureSet> put)
            {
                if (cur == null || n >= PreloadChunk) { cur = new StageTextureSet(); parts.Add(cur); n = 0; }
                put(cur);
                n++;
            }
            foreach (var c in set.Classes) Add(s => s.Classes.Add(c));
            foreach (var a in set.Abilities) Add(s => s.Abilities.Add(a));
            foreach (var i in set.Items) Add(s => s.Items.Add(i));
            foreach (var st in set.Statuses) Add(s => s.Statuses.Add(st));
            if (!string.IsNullOrEmpty(set.Theme))
                Add(s => { s.Theme = set.Theme; foreach (var b in set.BackdropIndexes) s.BackdropIndexes.Add(b); });
            return parts;
        }
    }
}
