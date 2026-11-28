using System.Collections;
using System.Linq;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Float on the board (§7, GG-T872, the Drifter's spell): an airy whoosh over a soft chime, then pale sky-blue
    /// updraft rings widen under the picked unit while motes rise past it, and the unit lifts off its tile and bobs on the
    /// air before settling, with a "Float" callout; its <see cref="BuffAppliedEvent"/> then lays [Floating] on it. Reduce
    /// Motion keeps one fading ring and the callout.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color FloatColor = Palette.Vfx.Float; // buffs.json floating #9FD3F0

        private IEnumerator FloatVfx(AbilityResolvedEvent ar)
        {
            var target = ar.Targets.Select(State.UnitById).FirstOrDefault(u => u != null);
            Vector2 at = target != null ? ViewCenter(target) : BoardView.CenterOf(ar.Anchor);
            float reach = BoardView.Tile * 0.7f;
            App.Audio.Sfx("battle.whoosh", 0.8f, 1.35f); // an airy rush, pitched up
            App.Audio.Sfx("battle.powerup", 0.35f, 1.4f); // a light lift under it
            if (ReduceMotion)
            {
                yield return Tween.Run(Timing.Quick, k =>
                {
                    board.Lines.Rings.Clear();
                    board.Lines.Ring(at, reach, FloatColor.WithA(0.8f * (1f - k)), 6f);
                });
                board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
                if (target != null) Text.Show(at, CombatTextKind.Callout, "Float", 0f, FloatColor);
                yield break;
            }
            UnitView v = null;
            if (target != null) board.Units.TryGetValue(target.Id, out v);
            if (v != null) v.SetGlow(FloatColor, 0.7f);
            const int motes = 8;
            yield return Tween.Run(Timing.Emphasis, k =>
            {
                board.Lines.Rings.Clear();
                board.Lines.ClearLines();
                // Two updraft rings widen and fade beneath the unit, one after the other.
                for (int r = 0; r < 2; r++)
                {
                    float kk = Mathf.Clamp01(k * 1.4f - r * 0.3f);
                    if (kk <= 0f || kk >= 1f) continue;
                    board.Lines.Ring(at + new Vector2(0f, BoardView.Tile * 0.3f), Mathf.Lerp(reach * 0.3f, reach, Ease.Out(kk)), FloatColor.WithA(0.85f * (1f - kk)), 5f);
                }
                // Motes ride the air up past the unit.
                for (int i = 0; i < motes; i++)
                {
                    float kk = Mathf.Repeat(k * 1.3f + i / (float)motes, 1f);
                    float x = (i - motes / 2f + 0.5f) * BoardView.Tile * 0.11f;
                    var p = at + new Vector2(x, BoardView.Tile * (0.45f - 0.9f * kk));
                    board.Lines.Dot(p, 6f + 4f * (1f - kk), Color.Lerp(FloatColor, Palette.White, 0.4f).WithA(Ease.Arc(kk)));
                }
                // The unit lifts, then bobs on the air.
                if (v != null)
                {
                    float lift = BoardView.Tile * 0.14f * Ease.Out(Mathf.Clamp01(k * 2.5f));
                    float bob = BoardView.Tile * 0.05f * Mathf.Sin(k * Mathf.PI * 4f);
                    v.Root.style.translate = new Translate(0f, -(lift + bob));
                }
            });
            board.Lines.Rings.Clear(); board.Lines.ClearLines(); board.Lines.MarkDirtyRepaint();
            if (target != null) Text.Show(at, CombatTextKind.Callout, "Float", 0f, FloatColor);
            if (v != null)
            {
                App.StartCoroutine(UnglowLater(v, 0.3f));
                var settle = v;
                yield return Tween.Run(Timing.Quick, k => settle.Root.style.translate = new Translate(0f, -BoardView.Tile * 0.14f * (1f - Ease.Out(k))));
                settle.Root.style.translate = new Translate(0f, 0f);
            }
        }
    }
}
