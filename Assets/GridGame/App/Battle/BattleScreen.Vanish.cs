using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Smoke Vanish on the board (§7, GG-T861, the Black Ninja): a whoosh and a grey puff of smoke billow out of the
    /// tile the ninja leaves with a "Vanish" callout, then (as its teleport fades it across) a pale flash where it reappears.
    /// The smoke cloud itself is the region tile wash its RegionTilesEvent paints. Reduce Motion keeps one fading puff.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color SmokeColor = new Color(0.62f, 0.64f, 0.7f);

        private IEnumerator VanishedAnim(VanishedEvent e)
        {
            Vector2 at = BoardView.CenterOf(e.From);
            App.Audio.Sfx("battle.whoosh", 0.9f, 0.8f);
            Text?.Show(e.From, CombatTextKind.Callout, "Vanish", 0.2f, Color.Lerp(SmokeColor, Palette.White, 0.35f));
            App.StartCoroutine(ReappearFlash(e.To));
            float reach = BoardView.Tile * 1.1f;
            if (ReduceMotion)
            {
                yield return Tween.Run(Timing.Quick, k =>
                {
                    board.Lines.ClearLines();
                    board.Lines.Dot(at, reach * 0.6f, SmokeColor.WithA(0.7f * (1f - k)));
                });
                board.Lines.ClearLines();
                yield break;
            }
            const int puffs = 9;
            var dirs = new Vector2[puffs];
            var sizes = new float[puffs];
            for (int i = 0; i < puffs; i++)
            {
                float th = i * Mathf.PI * 2f / puffs + Random.Range(-0.3f, 0.3f);
                dirs[i] = new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * Random.Range(0.6f, 1f);
                sizes[i] = Random.Range(0.28f, 0.42f) * BoardView.Tile;
            }
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                float out_ = Ease.Out(k);
                // Billowing puffs drift out, swell and thin; a dense core hides the ninja as it goes.
                for (int i = 0; i < puffs; i++)
                    board.Lines.Dot(at + dirs[i] * reach * out_, sizes[i] * (0.6f + 0.6f * out_),
                        Color.Lerp(SmokeColor, Palette.White, 0.15f * (i % 3)).WithA(0.75f * (1f - k)));
                board.Lines.Dot(at, BoardView.Tile * (0.35f + 0.25f * out_), SmokeColor.WithA(0.9f * (1f - k * k)));
            });
            board.Lines.ClearLines();
        }

        /// <summary>The flash where the ninja reappears, timed to its teleport fading it back in.</summary>
        private IEnumerator ReappearFlash(GridGame.Core.Coord to)
        {
            yield return Tween.Wait(Timing.Quick);
            Vector2 at = BoardView.CenterOf(to);
            App.Audio.Sfx("battle.whoosh", 0.6f, 1.4f);
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.Rings.Clear();
                board.Lines.Ring(at, Mathf.Lerp(8f, BoardView.Tile * 0.7f, Ease.Out(k)), Palette.White.WithA(0.85f * (1f - k)), 6f - 4f * k);
                board.Lines.Dot(at, 14f * (1f - k) + 4f, Palette.White.WithA(0.8f * (1f - k)));
            });
            board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
        }
    }
}
