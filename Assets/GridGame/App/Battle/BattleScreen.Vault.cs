using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Pole Vault on the board (§7, GG-T868): the Lancer crouches, plants the spear on the tile it clears and
    /// arcs high over the unit there (a whoosh on take-off, a "Vault" callout, the token swelling at the top of the jump
    /// while the spear swings over), then lands with a thud and a ring of dust kicked out from the landing tile. Reduce
    /// Motion slides the token straight across instead, with the thud and no dust.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color VaultDust = new Color(0.78f, 0.70f, 0.56f);
        private static readonly Color VaultPole = new Color(0.92f, 0.86f, 0.70f);

        private IEnumerator VaultAnim(VaultedEvent e)
        {
            if (!board.Units.TryGetValue(e.UnitId, out var v)) yield break;
            Vector2 from = BoardView.PosOf(v.Tile), to = BoardView.PosOf(e.To);
            Vector2 plant = BoardView.CenterOf(e.Over), land = BoardView.CenterOf(e.To);
            var half = new Vector2(BoardView.Tile, BoardView.Tile) * 0.5f;
            v.Tile = e.To;
            App.Audio.Sfx("battle.whoosh", 0.8f, 1.1f);
            Text.Show(BoardView.CenterOf(e.From), CombatTextKind.Callout, "Vault", 0f, VaultPole);
            if (ReduceMotion)
            {
                yield return Tween.Move(v.Root, from, to, Timing.Quick);
                App.Audio.Sfx("battle.dash", 0.6f, 0.8f);
                yield break;
            }
            float lift = BoardView.Tile * 0.9f;
            yield return Tween.Run(Timing.Standard, k =>
            {
                var p = Vector2.Lerp(from, to, Ease.InOut(k)) + Vector2.up * (-lift * Ease.Arc(k));
                v.Root.style.left = p.x;
                v.Root.style.top = p.y;
                v.Root.style.scale = new Scale(Vector2.one * (1f + 0.15f * Ease.Arc(k)));
                // The spear: planted on the cleared tile, its top in the Lancer's hands until it lets go past the peak.
                board.Lines.ClearLines();
                if (k < 0.65f)
                {
                    float fade = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.15f;
                    var grip = p + half;
                    board.Lines.Add(plant, grip, VaultPole.WithA(0.9f * fade), 7f);
                    board.Lines.Dot(plant, 6f, VaultPole.WithA(fade));
                }
                // The shadow stays on the ground, shrinking as the Lancer rises.
                board.Lines.Dot(Vector2.Lerp(from, to, Ease.InOut(k)) + half, BoardView.Tile * (0.3f - 0.12f * Ease.Arc(k)), new Color(0f, 0f, 0f, 0.25f));
            });
            v.Root.style.scale = new Scale(Vector2.one);
            board.Place(v, e.To);
            App.Audio.Sfx("battle.dash", 0.7f, 0.8f);
            // Landing dust: puffs kicked out in a ring that spread, rise a little and fade.
            const int puffs = 8;
            yield return Tween.Run(Timing.Quick * 1.5f, k =>
            {
                board.Lines.ClearLines();
                for (int i = 0; i < puffs; i++)
                {
                    float ang = (i + 0.5f) / puffs * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.45f);
                    var at = land + Vector2.up * (BoardView.Tile * 0.3f) + dir * (BoardView.Tile * (0.25f + 0.45f * Ease.Out(k))) + Vector2.up * (-12f * k);
                    board.Lines.Dot(at, 10f + 8f * k, VaultDust.WithA(0.7f * (1f - k)));
                }
            });
            board.Lines.ClearLines();
        }
    }
}
