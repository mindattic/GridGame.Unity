using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Swipe Boon on the board (§7, GG-T866): a quick gold swipe arcs from the Thief into the foe and back, carrying
    /// a bead in the stolen boon's own colour, with a whoosh out and a pickup chime on the catch, and a "Stolen &lt;Boon&gt;!"
    /// callout over the Thief. The boon then gets its own CombatText from the events that follow: struck through as it
    /// leaves the foe (BuffRemovedEvent, Reason "stolen") and landing on the Thief (BuffAppliedEvent).</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color SwipeColor = new Color(1f, 0.82f, 0.35f);

        private IEnumerator SwipeAnim(BoonStolenEvent e)
        {
            var thief = State.UnitById(e.CasterId);
            var foe = State.UnitById(e.FromId);
            if (thief == null || foe == null) yield break;
            var def = App.Content.TryBuff(e.BuffId);
            var boon = def != null ? StatusColor(def) : SwipeColor;
            string name = def != null ? def.Name : e.BuffId;
            Vector2 from = ViewCenter(thief), to = ViewCenter(foe);
            App.Audio.Sfx("battle.whoosh", 0.8f, 1.25f);
            if (ReduceMotion)
            {
                board.Lines.Add(from, to, SwipeColor.WithA(0.85f), 5f);
                board.Lines.Dot(from, 12f, boon);
                App.Audio.Sfx("battle.pickup", 0.8f);
                Text.Show(from, CombatTextKind.Callout, $"Stolen {name}!", 0f, boon);
                yield return Tween.Wait(Timing.Quick);
                board.Lines.ClearLines();
                yield break;
            }
            var d = to - from;
            var normal = new Vector2(-d.y, d.x).normalized;
            bool chimed = false;
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                // Out to the foe along one side of the line, back along the other: a hand darting in and out.
                bool back = k >= 0.5f;
                float t = back ? 1f - (k - 0.5f) * 2f : k * 2f;
                float side = back ? -1f : 1f;
                var hand = Vector2.Lerp(from, to, t) + normal * (side * 14f * Mathf.Sin(t * Mathf.PI));
                const int segs = 6;
                var prev = from;
                for (int i = 1; i <= segs; i++)
                {
                    float u = t * i / segs;
                    var p = Vector2.Lerp(from, to, u) + normal * (side * 14f * Mathf.Sin(u * Mathf.PI));
                    board.Lines.Add(prev, p, SwipeColor.WithA(0.7f * (0.4f + 0.6f * i / segs)), 5f);
                    prev = p;
                }
                board.Lines.Dot(hand, 9f, SwipeColor);
                if (back)
                {
                    // The boon rides the hand home, a little bigger as it reaches the Thief.
                    board.Lines.Dot(hand, 10f + 4f * (1f - t), boon);
                    if (!chimed && k >= 0.92f)
                    {
                        chimed = true;
                        App.Audio.Sfx("battle.pickup", 0.8f);
                        Text.Show(from, CombatTextKind.Callout, $"Stolen {name}!", 0f, boon);
                    }
                }
                else board.Lines.Dot(to, 8f + 8f * k, boon.WithA(0.5f));
            });
            if (!chimed)
            {
                App.Audio.Sfx("battle.pickup", 0.8f);
                Text.Show(from, CombatTextKind.Callout, $"Stolen {name}!", 0f, boon);
            }
            board.Lines.ClearLines();
        }
    }
}
