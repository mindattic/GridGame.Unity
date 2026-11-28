using GridGame.Meta;
using MindAttic.Media.Capture;
using MindAttic.Media.Capture.Unity;
using MindAttic.Telemetry;
using MindAttic.Telemetry.Unity;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// GG-T61: the hooks <see cref="EndlessStability"/> uses to keep a `-demo -endless` run alive for hundreds of
    /// encounters in one process — ending an encounter that ran past its turn / real-time cap as "stalled" instead of
    /// letting it hang the loop, and tearing a finished battle down completely once the next one is on screen.
    /// </summary>
    public sealed partial class BattleScreen
    {
        /// <summary>A beat is animating or the bot's simulated drag is in flight (the battle is busy, not frozen).</summary>
        internal bool StabilityBusy => playing || simulatingDrag;

        /// <summary>This battle has finished (or been ended) and accepts no more commands.</summary>
        internal bool StabilityEnded => ended;

        /// <summary>
        /// Ends a battle that never resolved as <c>"stalled"</c>: no further command is accepted (the same `ended`
        /// gate EndBattle/Retreat use, so an in-flight drag or playback can't commit or route afterwards), the battle
        /// record is saved, and the telemetry session closes with a "stalled" event and outcome. The caller routes on
        /// (the endless loop goes straight to the next encounter). Returns false if the battle had already ended.
        /// </summary>
        internal bool EndStalled(string reason, float realSeconds)
        {
            if (ended || State == null) return false;
            ended = true;
            AutoPlay = false;
            int heroes = 0, foes = 0;
            foreach (var u in State.Units)
                if (u.Alive) { if (u.IsHero) heroes++; else foes++; }
            CaptureUnity.Raise("battle-end", new CaptureData { ["stage"] = State.Stage.Id, ["result"] = "stalled" });
            SaveRecord();
            TelemetryUnity.RecordEvent("stalled", new TelemetryData
            {
                ["reason"] = reason, ["seconds"] = Mathf.Round(realSeconds),
                ["actions"] = State.Results.ActionsTaken, ["ticks"] = State.Results.TicksElapsed,
            });
            TelemetryUnity.Finish(new
            {
                stage = State.Stage.Id,
                result = "stalled",
                reason,
                seconds = Mathf.Round(realSeconds),
                waveReached = State.Wave + 1,
                actions = State.Results.ActionsTaken,
                ticks = State.Results.TicksElapsed,
                coins = State.Results.Coins,
                xp = State.Results.Xp,
                heroesStanding = heroes,
                enemiesStanding = foes,
            });
            return true;
        }

        /// <summary>The report the endless loop records for a stalled encounter (nothing is committed or awarded).</summary>
        internal BattleReport StalledReport() => new BattleReport
        {
            StageId = State.Stage.Id, StageName = State.Stage.Name, StageTheme = State.Stage.Theme,
        };

        /// <summary>
        /// Called once this battle is off screen for good (the next encounter is showing): drops everything it could
        /// still be holding and clears the global pause/hold flags in case it was torn down mid-beat (GG-T586):
        /// <list type="bullet">
        /// <item>its long-lived coroutines — the event playback (a stalled battle can be ended mid-beat, and its
        /// playback would otherwise keep stepping through events, playing their sounds over the next encounter), the
        /// bot's simulated drag and the banner marquee — are stopped;</item>
        /// <item>the long-press ticker is cancelled and the prefab FX layer disposed (its camera rig GameObject,
        /// RenderTexture, material and pooled particle instances; OnHidden does this too, and it is idempotent);</item>
        /// <item>its Overlay-hosted pieces (pause veil, billboard, mana-tag host) are removed and the per-battle
        /// caches (cast ghosts, unplayed casts, mint tags) emptied;</item>
        /// <item>its whole visual tree (board, FX layer, cut-in cards, timeline icons) is detached and cleared, which
        /// also stops every scheduled pulse on those elements.</item>
        /// </list>
        /// Short Tween coroutines still running finish on detached elements within a second or two; with `ended` set
        /// nothing can commit or route. The SFX pool is stopped by the caller (<see cref="Audio.StopSfx"/>).
        /// </summary>
        internal void TearDownAfterEncounter()
        {
            ended = true;
            AutoPlay = false;
            autoBot = null;
            perfectBot = null;
            StopRoutine(ref playRoutine);
            StopRoutine(ref dragRoutine);
            StopRoutine(ref bannerScrollRoutine);
            playing = simulatingDrag = pressing = dragging = false;
            holdTicker?.Pause();
            holdTicker = null;
            DisposePrefabFx();
            if (pauseVeil != null) { pauseVeil.RemoveFromHierarchy(); pauseVeil = null; }
            billboard?.RemoveFromHierarchy();
            billboard = null;
            castGhosts.Clear();
            unplayedCasts.Clear();
            Tween.Paused = false;
            if (Root != null)
            {
                Root.RemoveFromHierarchy();
                Root.Clear();
            }
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine != null && App != null) App.StopCoroutine(routine);
            routine = null;
        }
    }
}
