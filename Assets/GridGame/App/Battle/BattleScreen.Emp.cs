using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>EMP Pulse on the board (§7, GG-T879): a crackle of static, then three cyan rings ripple out from the picked
    /// tile across the 3x3 it covers while sparks jitter along their edge, before the jams ("Jammed!"/"Interrupted!"), the
    /// Silenced cues and any mine it sets off play. Reduce Motion keeps one fading ring.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color EmpColor = Palette.Vfx.Emp;

        private IEnumerator EmpPulseVfx(AbilityResolvedEvent ar)
        {
            Vector2 at = BoardView.CenterOf(ar.Anchor);
            float reach = BoardView.Tile * 1.6f;
            // Static: a sharp lightning crackle, then two quieter, higher snaps.
            App.Audio.Sfx("element.lightning", 0.8f, 1.6f);
            App.StartCoroutine(DelayedSfx("element.lightning", 0.09f, 2.1f));
            App.StartCoroutine(DelayedSfx("battle.fizzle", 0.18f, 1.4f));
            if (ReduceMotion)
            {
                yield return Tween.Run(Timing.Quick, k =>
                {
                    board.Lines.Rings.Clear();
                    board.Lines.Ring(at, reach * 0.8f, EmpColor.WithA(0.8f * (1f - k)), 8f);
                });
                board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
                yield break;
            }
            const int sparks = 12;
            yield return Tween.Run(Timing.Emphasis, k =>
            {
                board.Lines.Rings.Clear();
                board.Lines.ClearLines();
                // Three rings ripple outward one after another, thinning as they go.
                for (int r = 0; r < 3; r++)
                {
                    float kk = Mathf.Clamp01(k * 1.5f - r * 0.22f);
                    if (kk <= 0f) continue;
                    float rad = Mathf.Lerp(12f, reach, Ease.Out(kk));
                    board.Lines.Ring(at, rad, EmpColor.WithA(0.85f * (1f - kk)), 12f - 8f * kk);
                    // Sparks jitter along the leading ring's edge: the static.
                    if (r == 0)
                        for (int i = 0; i < sparks; i++)
                        {
                            float th = i * Mathf.PI * 2f / sparks + Random.Range(-0.15f, 0.15f);
                            var p = at + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * (rad + Random.Range(-6f, 6f));
                            board.Lines.Dot(p, Random.Range(5f, 11f), Color.Lerp(EmpColor, Palette.White, Random.value * 0.6f).WithA(1f - kk));
                        }
                }
                board.Lines.Dot(at, 16f + 12f * Ease.Arc(k), Palette.White.WithA(0.9f * (1f - k))); // the flash at its heart
            });
            board.Lines.Rings.Clear(); board.Lines.ClearLines(); board.Lines.MarkDirtyRepaint();
            App.StartCoroutine(Tween.Shake(shake, 4f * Fx, Timing.Quick));
        }
    }
}
