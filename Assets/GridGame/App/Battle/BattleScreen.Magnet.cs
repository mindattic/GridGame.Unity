using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Magnetize on the board (§7, GG-T878): a low magnetic hum, rings pulse in on the picked tile, and a crackling
    /// blue-white arc reaches from the tile to every gripped foe before the pulls (DisplacedEvents) and plate swings
    /// (FacingChangedEvents) play. Reduce Motion: the arcs hold still for a beat and fade.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color MagnetColor = new Color(0.45f, 0.78f, 1f);

        private IEnumerator MagnetizedAnim(MagnetizedEvent e)
        {
            App.Audio.Sfx("battle.channel", 0.85f, 0.55f); // the hum
            Vector2 at = BoardView.CenterOf(e.Anchor);
            var ends = new System.Collections.Generic.List<Vector2>();
            foreach (var id in e.UnitIds) { var u = State.UnitById(id); if (u != null) ends.Add(ViewCenter(u)); }
            if (ReduceMotion)
            {
                foreach (var p in ends) board.Lines.Add(at, p, MagnetColor.WithA(0.8f), 5f);
                board.Lines.Rings.Add((at, BoardView.Tile * 0.45f, MagnetColor.WithA(0.8f), 4f));
                yield return Tween.Wait(Timing.Quick);
                board.Lines.ClearLines();
                yield break;
            }
            if (ends.Count > 0) App.Audio.Sfx("element.lightning", 0.45f, 1.3f);
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                float fade = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
                // Rings closing in on the tile: the field's pull.
                for (int r = 0; r < 3; r++)
                {
                    float t = Mathf.Repeat(k * 2f + r / 3f, 1f);
                    board.Lines.Rings.Add((at, BoardView.Tile * (1.6f - 1.3f * t), MagnetColor.WithA(0.7f * (1f - t) * fade), 3f));
                }
                // A jagged arc to each gripped foe, re-rolled every frame so it crackles.
                float reach = Mathf.Min(1f, k * 2.2f);
                foreach (var p in ends)
                {
                    var tip = Vector2.Lerp(at, p, reach);
                    var n = new Vector2(-(tip - at).y, (tip - at).x).normalized;
                    Vector2 prev = at;
                    for (int i = 1; i <= 5; i++)
                    {
                        var q = Vector2.Lerp(at, tip, i / 5f) + (i < 5 ? n * Random.Range(-9f, 9f) : Vector2.zero);
                        board.Lines.Add(prev, q, MagnetColor.WithA(0.9f * fade), 5f);
                        board.Lines.Add(prev, q, Palette.White.WithA(0.6f * fade), 1.8f);
                        prev = q;
                    }
                    if (reach >= 1f) board.Lines.Dot(p, 10f, MagnetColor.WithA(fade));
                }
                board.Lines.Dot(at, 12f + 4f * Mathf.Sin(k * Mathf.PI * 6f), MagnetColor.WithA(fade));
            });
            board.Lines.ClearLines();
            if (ends.Count > 0) Text.Show(at, CombatTextKind.Callout, "Magnetize!", 0f, MagnetColor);
        }
    }
}
