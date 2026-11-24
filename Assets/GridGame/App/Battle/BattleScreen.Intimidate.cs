using System.Collections;
using System.Linq;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Intimidate on the board (§7, GG-T884): the Blue Lion's roar - a low war cry under a growl, a camera
    /// shake, and pale-blue shockwave rings rolling off the Lion while each adjacent foe it reaches flinches violet - before
    /// each foe's <see cref="BuffAppliedEvent"/> lays [Frightened] on it with its "Frightened" callout (or "Immune!" on a
    /// boss or a Heavy foe). Reduce Motion keeps one fading ring.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color RoarColor = new Color(0.55f, 0.80f, 1f);
        private static readonly Color FearColor = new Color(0.69f, 0.49f, 0.91f);

        private IEnumerator IntimidateVfx(Unit caster, AbilityResolvedEvent ar)
        {
            Vector2 at = ViewCenter(caster);
            float reach = BoardView.Tile * 1.9f;
            App.Audio.Sfx("battle.warcry", 1f, 0.7f); // the roar, pitched down
            App.Audio.Sfx("battle.taunt", 0.6f, 0.55f); // a growl under it
            var foes = ar.Targets.Select(State.UnitById).Where(u => u != null && board.Units.ContainsKey(u.Id)).ToList();
            if (ReduceMotion)
            {
                yield return Tween.Run(Timing.Quick, k =>
                {
                    board.Lines.Rings.Clear();
                    board.Lines.Ring(at, reach * 0.7f, RoarColor.WithA(0.8f * (1f - k)), 8f);
                });
                board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
                yield break;
            }
            App.StartCoroutine(Tween.Shake(shake, 10f * Fx, Timing.Standard));
            if (board.Units.TryGetValue(caster.Id, out var lv))
                App.StartCoroutine(Tween.Run(Timing.Quick, k => lv.Root.style.scale = new Scale(Vector2.one * (1f + 0.15f * Ease.Arc(k)))));
            bool flinched = false;
            yield return Tween.Run(Timing.Emphasis, k =>
            {
                board.Lines.Rings.Clear();
                // Three shockwaves roll out one after another, thick and bright near the Lion, thin as they spend.
                for (int r = 0; r < 3; r++)
                {
                    float kk = Mathf.Clamp01(k * 1.5f - r * 0.22f);
                    if (kk <= 0f || kk >= 1f) continue;
                    board.Lines.Ring(at, Mathf.Lerp(30f, reach, Ease.Out(kk)), Color.Lerp(RoarColor, Palette.White, 0.3f * (1f - kk)).WithA(0.9f * (1f - kk)), 18f * (1f - kk) + 4f);
                }
                if (!flinched && k > 0.3f)
                {
                    flinched = true;
                    foreach (var f in foes)
                        if (board.Units.TryGetValue(f.Id, out var fv)) { fv.SetGlow(FearColor, 0.8f); App.StartCoroutine(UnglowLater(fv, 0.4f)); }
                }
            });
            board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
        }
    }
}
