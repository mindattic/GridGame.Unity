using System.Collections;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Substitution on the board (§7, GG-T862, the White Ninja): the blow that would have felled the ninja meets a
    /// log instead. A "Substitution!" callout and a puff of white smoke where the ninja stood, a log drops there with a
    /// dull thunk and a splinter splat, then the ninja reappears beside its ally in a second puff. The log itself is the
    /// substitution_log tile mark the RegionTilesEvent right after paints (a brown bar on the tile for about a turn).
    /// Reduce Motion: no fades or log drop, just the callout, the puffs and the move.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly VfxStyleDef SubstitutionSmoke = new VfxStyleDef
        {
            Id = "SubstitutionSmoke", Core = Palette.Vfx.SmokeCore, Glow = Palette.Vfx.SmokeGlow, Accent = Palette.Vfx.SmokeAccent, Shape = "smoke", Motion = "billow",
            Cue = "battle.whoosh", Count = 9, Size = 44, Reach = 70, Time = "Quick",
        };

        private static readonly Color LogColor = Palette.Vfx.Log;

        private IEnumerator SubstitutionAnim(SubstitutedEvent e)
        {
            if (!board.Units.TryGetValue(e.UnitId, out var v)) yield break;
            Vector2 from = BoardView.CenterOf(e.From), to = BoardView.CenterOf(e.To);
            App.Audio.Sfx("battle.whoosh", 0.9f, 1.3f);
            Text?.Show(from, CombatTextKind.Callout, "Substitution!", 0.3f, Palette.White);
            Emit(SubstitutionSmoke, from, 1f, quiet: true);
            if (ReduceMotion)
            {
                board.Splat(e.From, LogColor);
                board.Place(v, e.To);
                Emit(SubstitutionSmoke, to, 0.8f, quiet: true);
                yield return Tween.Wait(Timing.Quick);
                yield break;
            }
            yield return Tween.Fade(v.Root, 1, 0, Timing.Instant);
            // The log drops onto the tile the blow was aimed at: a short brown bar falling into place, then the thunk.
            float half = BoardView.Tile * 0.32f;
            yield return Tween.Run(Timing.Quick, k =>
            {
                board.Lines.ClearLines();
                float drop = (1f - k) * BoardView.Tile * 0.35f;
                var a = from + new Vector2(-half, -drop);
                var b = from + new Vector2(half, -drop);
                board.Lines.Add(a, b, LogColor, 14f);
                board.Lines.Add(a + new Vector2(0f, -3f), b + new Vector2(0f, -3f), Color.Lerp(LogColor, Palette.White, 0.35f), 3f);
            });
            board.Lines.ClearLines();
            App.Audio.Sfx("battle.hit", 0.7f, 0.55f);
            board.Splat(e.From, LogColor);
            App.StartCoroutine(Tween.Shake(shake, 3f, Timing.Instant));
            yield return Tween.Wait(Timing.Instant);
            board.Place(v, e.To);
            Emit(SubstitutionSmoke, to, 0.8f, quiet: true);
            App.Audio.Sfx("battle.whoosh", 0.7f, 1.6f);
            yield return Tween.Fade(v.Root, 0, 1, Timing.Quick);
        }
    }
}
