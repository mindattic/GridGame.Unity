using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Curse Link on the board (§8.1, GG-T873): a violet tether snakes out between the two Linked foes when the
    /// hex lands, and every echo runs a pulse down it from the struck foe to its partner with an "Echo" callout over the
    /// partner (its damage number follows as the echo's own DamagedEvent).</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color LinkColor = new Color(0.70f, 0.42f, 1f);

        private IEnumerator LinkedAnim(UnitsLinkedEvent e)
        {
            var a = State.UnitById(e.AId);
            var b = State.UnitById(e.BId);
            if (a == null || b == null) yield break;
            App.Audio.Sfx("element.arcane", 0.8f, 0.8f);
            Vector2 from = ViewCenter(a), to = ViewCenter(b);
            if (ReduceMotion)
            {
                board.Lines.Add(from, to, LinkColor.WithA(0.9f), 8f);
                yield return Tween.Wait(Timing.Quick);
                board.Lines.ClearLines();
            }
            else
            {
                // The tether draws out from the picked foe, then breathes once before it fades.
                yield return Tween.Run(Timing.Standard, k =>
                {
                    board.Lines.ClearLines();
                    float reach = Mathf.Min(1f, k * 1.8f);
                    var tip = Vector2.Lerp(from, to, reach);
                    float fade = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                    float w = 6f + 4f * Mathf.Sin(k * Mathf.PI * 3f);
                    board.Lines.Add(from, tip, LinkColor.WithA(0.9f * fade), w);
                    board.Lines.Add(from, tip, Palette.White.WithA(0.45f * fade), w * 0.35f);
                    board.Lines.Dot(from, 14f, LinkColor.WithA(fade));
                    if (reach >= 1f) board.Lines.Dot(to, 14f, LinkColor.WithA(fade));
                });
                board.Lines.ClearLines();
            }
            Text.Show(to, CombatTextKind.Debuff, "Linked", 0f, LinkColor);
        }

        private IEnumerator LinkEchoAnim(LinkEchoEvent e)
        {
            var a = State.UnitById(e.FromId);
            var b = State.UnitById(e.ToId);
            if (a == null || b == null) yield break;
            App.Audio.Sfx("status.debuff", 0.7f, 1.25f);
            Vector2 from = ViewCenter(a), to = ViewCenter(b);
            if (!ReduceMotion)
            {
                yield return Tween.Run(Timing.Quick, k =>
                {
                    board.Lines.ClearLines();
                    board.Lines.Add(from, to, LinkColor.WithA(0.5f * (1f - k)), 5f);
                    board.Lines.Dot(Vector2.Lerp(from, to, k), 16f, Color.Lerp(LinkColor, Palette.White, 0.4f));
                });
                board.Lines.ClearLines();
            }
            Text.Show(to, CombatTextKind.Callout, "Echo", 0f, LinkColor);
        }
    }
}
