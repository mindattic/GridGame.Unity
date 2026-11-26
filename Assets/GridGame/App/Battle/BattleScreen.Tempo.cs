using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Party tempo readout (GG-T268, BIBLE §2.11): "Tempo +2" under the timeline, the track tinted cool
    /// while foes are slowed and warm while the party is slowed.</summary>
    public sealed partial class BattleScreen
    {
        private Label tempoLabel;
        private int shownTempo;
        private const float TempoLabelWidth = 90f;

        private void EnsureTempoLabel()
        {
            if (tempoLabel != null && tempoLabel.parent != null) return;
            tempoLabel = Ui.Text("", "tiny shadow", timelineFrame);
            tempoLabel.name = "timeline-tempo";
            tempoLabel.pickingMode = PickingMode.Ignore;
            tempoLabel.style.position = Position.Absolute;
            tempoLabel.style.left = XOfU(1f) - TempoLabelWidth + IconSpan / 2f; // over the track's far end, so the round readout owns the acting end (GG-T1158)
            tempoLabel.style.width = TempoLabelWidth;
            tempoLabel.style.unityTextAlign = TextAnchor.UpperRight;
            tempoLabel.style.whiteSpace = WhiteSpace.NoWrap;
            tempoLabel.style.top = Space.S0;
            Fonts.UseHeavy(tempoLabel);
        }

        private static Color TempoColor(int net) => net > 0 ? Palette.Haste : net < 0 ? Palette.Bad : Palette.Text;

        /// <summary>Snap the readout and tint to the state without animating (screen build, resync).</summary>
        private void SyncTempo()
        {
            if (timelineFrame == null || State == null) return;
            EnsureTempoLabel();
            shownTempo = State.NetTempo;
            shownTempoMultiplier = BattleEngine.EnemyTempoMultiplier(State);
            ApplyTempoLook(shownTempo, 1f);
        }

        /// <summary>The foes' tempo rate the bar is drawn at; a Slow scales on-bar foe icons by old/new (GG-T1084).</summary>
        private float shownTempoMultiplier = 1f;

        /// <summary>
        /// GG-T1084: a Slow (party tempo up) pushes every on-bar foe turn icon later at once: its u (time left) scales by
        /// old/new enemy tempo multiplier. Each icon syncs to its new u and spin-slides back with PushSlide (GG-T1071) like
        /// any other pushback. Hero icons, casts and tray icons hold still.
        /// </summary>
        private void TempoPushback(float oldMult, float newMult)
        {
            var (hold, moved) = SlowShift(oldMult, newMult);
            if (moved.Count == 0) return;
            SyncTimeline(hold);
            App.Audio.Sfx("battle.slide", 0.7f, 0.75f);
            foreach (var m in moved) App.StartCoroutine(PushSlide(m.id, m.from, m.to, 0.45f));
        }

        /// <summary>The bar a Slow from <paramref name="oldMult"/> to <paramref name="newMult"/> leaves (every u) and the
        /// on-bar foe turn icons it moves.</summary>
        private (Dictionary<int, float> hold, List<(int id, float from, float to)> moved) SlowShift(float oldMult, float newMult)
        {
            var moved = new List<(int id, float from, float to)>();
            if (lastAt == null || State == null || newMult <= 0f || newMult >= oldMult) return (null, moved);
            float ratio = oldMult / newMult;
            var hold = new Dictionary<int, float>(lastAt);
            foreach (var e in State.Timeline)
            {
                if (e.Kind != TimelineKind.Turn || inTray.Contains(e.Id) || !lastAt.TryGetValue(e.Id, out var u)) continue;
                var owner = State.UnitById(e.OwnerId);
                if (owner == null || owner.IsHero) continue;
                float to = Mathf.Clamp01(u * ratio);
                if (to - u < 0.002f) continue;
                hold[e.Id] = to;
                moved.Add((e.Id, u, to));
            }
            return (hold, moved);
        }

        /// <summary>
        /// GG-T1094 dev hook (automation 'devpushback'): plays the bar's pushback look without touching the state.
        /// kind=push tosses the first on-bar foe turn icon back by <paramref name="shift"/> of the bar (as PushAnim does);
        /// kind=slow runs the Slow pushback from tempo ×1 to ×1/(1+shift) (as TempoAnim does). <paramref name="k"/> in
        /// [0, 1] instead freezes the slide at that progress (no tween), so a screenshot shows one exact spin angle; k &lt; 0
        /// plays it live. kind=clear drops frozen slides and resyncs the bar to the state. Returns the icons moved.
        /// </summary>
        public object DevPushback(string kind, float shift, float k)
        {
            if (lastAt == null || State == null) return null;
            if (kind == "clear") { pushSlides.Clear(); SyncTimeline(null); return new { cleared = true }; }
            Dictionary<int, float> hold;
            List<(int id, float from, float to)> moved;
            if (kind == "slow") (hold, moved) = SlowShift(1f, 1f / (1f + Mathf.Max(0.05f, shift)));
            else
            {
                hold = new Dictionary<int, float>(lastAt);
                moved = new List<(int id, float from, float to)>();
                var foe = State.Timeline.Where(e => e.Kind == TimelineKind.Turn && !inTray.Contains(e.Id) && lastAt.ContainsKey(e.Id))
                    .FirstOrDefault(e => State.UnitById(e.OwnerId)?.IsHero == false);
                if (foe != null)
                {
                    float u = lastAt[foe.Id], to = Mathf.Clamp01(u + Mathf.Max(0.05f, shift));
                    hold[foe.Id] = to;
                    moved.Add((foe.Id, u, to));
                }
            }
            if (moved.Count == 0) return new { moved = 0 };
            SyncTimeline(hold);
            foreach (var m in moved)
            {
                if (k < 0f) App.StartCoroutine(PushSlide(m.id, m.from, m.to, kind == "slow" ? 0.45f : 0.38f));
                else pushSlides[m.id] = (m.from, Mathf.Clamp01(k), PushTurns(m.from, m.to));
            }
            if (k >= 0f) LayoutIcons(lastAt);
            return new
            {
                moved = moved.Count,
                icons = moved.Select(m => new { id = m.id, from = m.from, to = m.to, turns = PushTurns(m.from, m.to),
                    deg = k < 0f ? -1f : PushTurns(m.from, m.to) * 360f * PushEase(Mathf.Clamp01(k)),
                    u = k < 0f ? -1f : Mathf.LerpUnclamped(m.from, m.to, PushEase(Mathf.Clamp01(k))) }).ToList(),
            };
        }

        private void ApplyTempoLook(int net, float k)
        {
            tempoLabel.text = net == 0 ? "" : net > 0 ? $"Tempo +{net}" : $"Tempo −{-net}";
            tempoLabel.style.color = TempoColor(net);
            tempoLabel.style.opacity = net == 0 ? 0f : k;
            var baseTrack = Palette.Text.WithA(0.85f);
            if (timelineBar != null) timelineBar.style.backgroundColor = net == 0 ? baseTrack : Color.Lerp(baseTrack, TempoColor(net), 0.55f * k);
        }

        private IEnumerator TempoAnim(TempoChangedEvent t)
        {
            EnsureTempoLabel();
            int from = shownTempo;
            shownTempo = t.Net;
            float fromMult = shownTempoMultiplier;
            shownTempoMultiplier = t.Multiplier;
            TempoPushback(fromMult, t.Multiplier); // GG-T1084: foes slowed slide back now
            if (t.Net == from) yield break;
            bool up = t.Net > from;
            App.Audio.Sfx(up ? "battle.powerup" : "status.debuff", 0.7f, up ? 1.25f : 0.8f);
            Feed(t.Net > 0 ? $"Tempo +{t.Net}: foes slowed (×{t.Multiplier:0.00})"
                 : t.Net < 0 ? $"Tempo −{-t.Net}: foes hastened (×{t.Multiplier:0.00})"
                 : "Tempo back to normal", TempoColor(t.Net));
            tempoLabel.style.scale = new Scale(Vector2.one * 1.3f);
            yield return Tween.Run(Timing.Standard, k =>
            {
                ApplyTempoLook(t.Net, t.Net == 0 ? 1f - k : k);
                tempoLabel.style.scale = new Scale(Vector2.one * Mathf.Lerp(1.3f, 1f, k));
            }, Tween.EaseOut);
            ApplyTempoLook(t.Net, 1f);
        }
    }
}
