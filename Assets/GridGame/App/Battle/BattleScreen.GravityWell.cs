using System.Collections;
using System.Collections.Generic;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Gravity Well on the board (§7, GG-T883): a low thrum, then a violet vortex opens on the picked tile -
    /// rings collapsing inward while motes spiral down into it from the edge of its reach - before each caught foe's
    /// <see cref="DisplacedEvent"/> slides it one tile in. Reduce Motion keeps one fading ring.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color WellColor = new Color(0.55f, 0.30f, 0.95f);

        private IEnumerator GravityWellVfx(AbilityResolvedEvent ar)
        {
            Vector2 at = BoardView.CenterOf(ar.Anchor);
            float reach = BoardView.Tile * 2.5f;
            App.Audio.Sfx("element.arcane", 0.9f, 0.45f); // a low thrum
            App.Audio.Sfx("battle.whoosh", 0.5f, 0.6f);
            if (ReduceMotion)
            {
                yield return Tween.Run(Timing.Quick, k =>
                {
                    board.Lines.Rings.Clear();
                    board.Lines.Ring(at, reach * 0.6f, WellColor.WithA(0.8f * (1f - k)), 8f);
                });
                board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
                yield break;
            }
            const int motes = 14;
            var angle = new List<float>();
            for (int i = 0; i < motes; i++) angle.Add(i * Mathf.PI * 2f / motes + Random.Range(-0.2f, 0.2f));
            yield return Tween.Run(Timing.Emphasis, k =>
            {
                board.Lines.Rings.Clear();
                board.Lines.ClearLines();
                // Three rings collapse toward the centre one after another.
                for (int r = 0; r < 3; r++)
                {
                    float kk = Mathf.Repeat(k * 1.6f + r / 3f, 1f);
                    board.Lines.Ring(at, Mathf.Lerp(reach, 10f, kk), WellColor.WithA(0.75f * Ease.Arc(kk)), 6f + 6f * kk);
                }
                // Motes spiral down into the eye.
                for (int i = 0; i < motes; i++)
                {
                    float kk = Mathf.Clamp01(k * 1.25f - (i % 3) * 0.08f);
                    float rad = Mathf.Lerp(reach, 4f, Ease.Out(kk));
                    float th = angle[i] + kk * Mathf.PI * 1.5f;
                    var p = at + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * rad;
                    board.Lines.Dot(p, 9f + 7f * (1f - kk), Color.Lerp(WellColor, Palette.White, 0.35f * kk).WithA(1f - 0.6f * kk));
                }
                board.Lines.Dot(at, 18f + 10f * Ease.Arc(k), new Color(0.12f, 0.04f, 0.2f, 0.9f)); // the dark eye
            });
            board.Lines.Rings.Clear(); board.Lines.ClearLines(); board.Lines.MarkDirtyRepaint();
            App.StartCoroutine(Tween.Shake(shake, 6f * Fx, Timing.Quick));
        }
    }
}
