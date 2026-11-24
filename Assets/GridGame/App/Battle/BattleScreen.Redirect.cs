using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Redirect Flow on the board (§7, GG-T887): a pale jade chi stream runs from the cleansed ally into the foe
    /// beside the Monk, one bead down it per debuff carried (in each status's own colour), with a "Redirect" callout over
    /// the ally. Each moved status then gets its own CombatText from the events that follow: struck through as it leaves
    /// the ally (BuffRemovedEvent, Reason "redirected") and dropping in over the foe as it lands (BuffAppliedEvent).</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color ChiColor = new Color(0.55f, 1f, 0.82f);

        private IEnumerator RedirectAnim(DebuffsRedirectedEvent e)
        {
            var a = State.UnitById(e.FromId);
            var b = State.UnitById(e.ToId);
            if (a == null || b == null) yield break;
            App.Audio.Sfx("battle.cast", 0.7f, 1.2f);
            Vector2 from = ViewCenter(a), to = ViewCenter(b);
            Text.Show(from, CombatTextKind.Callout, "Redirect", 0f, ChiColor);
            if (ReduceMotion)
            {
                board.Lines.Add(from, to, ChiColor.WithA(0.85f), 6f);
                yield return Tween.Wait(Timing.Quick);
                board.Lines.ClearLines();
                yield break;
            }
            int n = Mathf.Max(1, e.BuffIds.Count);
            var normal = new Vector2(-(to - from).y, (to - from).x).normalized;
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                float reach = Mathf.Min(1f, k * 2f);
                float fade = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
                // The stream: a few short segments that sway about the straight line, so it reads as a current, not a beam.
                const int segs = 8;
                var prev = from;
                for (int i = 1; i <= segs; i++)
                {
                    float t = reach * i / segs;
                    var p = Vector2.Lerp(from, to, t) + normal * (6f * Mathf.Sin(t * Mathf.PI * 3f + k * 12f) * (1f - t * 0.5f));
                    board.Lines.Add(prev, p, ChiColor.WithA(0.75f * fade), 7f);
                    board.Lines.Add(prev, p, Palette.White.WithA(0.4f * fade), 2.5f);
                    prev = p;
                }
                // One bead per status carried, staggered down the stream.
                for (int i = 0; i < e.BuffIds.Count; i++)
                {
                    float start = 0.15f + 0.45f * i / n;
                    float bk = Mathf.Clamp01((k - start) / 0.35f);
                    if (bk <= 0f || bk >= 1f) continue;
                    var def = App.Content.TryBuff(e.BuffIds[i]);
                    var c = def != null ? StatusColor(def) : ChiColor;
                    board.Lines.Dot(Vector2.Lerp(from, to, bk), 12f, c.WithA(fade));
                }
                board.Lines.Dot(to, 10f + 6f * k, ChiColor.WithA(0.6f * fade));
            });
            board.Lines.ClearLines();
        }
    }
}
