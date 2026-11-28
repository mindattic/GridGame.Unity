using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Smokebomb on the board (§7, GG-T861, GG-T1916, the Black Ninja): a whoosh and a grey burst of smoke billow out
    /// of the ninja's tile with a "Vanish" callout and its token goes with it (off the board until its next turn; the turn's
    /// timeline entry carries the marker). When that turn comes a smaller burst and a pale flash mark where it reappears,
    /// timed to its teleport fading it back in. The smoke cloud itself is the region tile wash its RegionTilesEvent paints.
    /// Reduce Motion keeps one fading puff.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color SmokeColor = Palette.Vfx.Smoke;

        private IEnumerator VanishedAnim(VanishedEvent e)
        {
            App.Audio.Sfx("battle.whoosh", 0.9f, 0.8f);
            Text?.Show(e.From, CombatTextKind.Callout, "Vanish", 0.2f, Color.Lerp(SmokeColor, Palette.White, 0.35f));
            if (board.Units.TryGetValue(e.UnitId, out var v) && !v.Dead) App.StartCoroutine(FadeOffBoard(v));
            yield return SmokeBurst(e.From, 1.1f);
        }

        /// <summary>GG-T1916: the vanished unit's token leaves the board inside the burst (the TeleportedEvent of its
        /// reappearance brings it back, as for a burrower).</summary>
        private IEnumerator FadeOffBoard(UnitView v)
        {
            yield return Tween.Fade(v.Root, 1, 0, Timing.Quick);
            v.Root.RemoveFromHierarchy();
            v.Dead = true;
        }

        private IEnumerator ReappearedAnim(ReappearedEvent e)
        {
            App.StartCoroutine(ReappearFlash(e.To));
            yield return SmokeBurst(e.To, 0.7f);
        }

        /// <summary>Billowing puffs of <see cref="SmokeColor"/> drift out of <paramref name="tile"/>, swell and thin, round a
        /// dense core; <paramref name="scale"/> of a tile and a bit of reach.</summary>
        private IEnumerator SmokeBurst(GridGame.Core.Coord tile, float scale)
        {
            Vector2 at = BoardView.CenterOf(tile);
            float reach = BoardView.Tile * scale;
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
                sizes[i] = Random.Range(0.25f, 0.38f) * reach;
            }
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                float out_ = Ease.Out(k);
                // Billowing puffs drift out, swell and thin; a dense core hides the ninja as it goes.
                for (int i = 0; i < puffs; i++)
                    board.Lines.Dot(at + dirs[i] * reach * out_, sizes[i] * (0.6f + 0.6f * out_),
                        Color.Lerp(SmokeColor, Palette.White, 0.15f * (i % 3)).WithA(0.75f * (1f - k)));
                board.Lines.Dot(at, reach * (0.32f + 0.23f * out_), SmokeColor.WithA(0.9f * (1f - k * k)));
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
