using System.Collections;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Rooted on the board (§7 Bramble Snare, §8.1, GG-T881): four bramble vines snake up from the foot of the
    /// tile and coil round the unit, thorns budding along them, with a low rustle, before the status burst and the
    /// "Rooted" callout. Plays for any roots, so every Rooted reads the same. Reduce Motion skips it (StatusBurst is not
    /// run then).</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color VineGreen = new Color(0.37f, 0.55f, 0.23f);
        private static readonly Color VineBark = new Color(0.61f, 0.48f, 0.27f);

        private IEnumerator VineCoil(Vector2 at)
        {
            App.Audio.Sfx("battle.whoosh", 0.35f, 0.55f); // the rustle's low body under the status cue
            const int vines = 4, segs = 14;
            float half = BoardView.Tile * 0.42f, rise = BoardView.Tile * 0.85f;
            var foot = at + new Vector2(0f, BoardView.Tile * 0.4f); // screen y runs down: the foot of the tile
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                for (int v = 0; v < vines; v++)
                {
                    float phase = v * Mathf.PI * 0.5f;
                    float startX = Mathf.Lerp(-half, half, v / (float)(vines - 1));
                    int shown = Mathf.CeilToInt(segs * Ease.Out(k));
                    Vector2 prev = foot + new Vector2(startX, 0f);
                    for (int i = 1; i <= shown; i++)
                    {
                        float u = i / (float)segs;
                        // Each vine swings round the unit as it climbs, narrowing toward the top.
                        float x = Mathf.Lerp(startX, 0f, u) + Mathf.Sin(u * Mathf.PI * 2.2f + phase) * half * 0.55f * (1f - 0.4f * u);
                        var p = foot + new Vector2(x, -u * rise);
                        var c = Color.Lerp(VineBark, VineGreen, u);
                        board.Lines.Add(prev, p, c.WithA(0.95f), 9f - 4f * u);
                        if (i % 4 == 2) board.Lines.Dot(p, 7f, Color.Lerp(c, Palette.White, 0.25f)); // a thorn bud
                        prev = p;
                    }
                }
            });
            board.Lines.ClearLines(); board.Lines.MarkDirtyRepaint();
        }
    }
}
