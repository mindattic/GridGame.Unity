using System.Collections;
using System.Collections.Generic;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Anchor Stance on the board (§7, GG-T882): the Warden's shield drops and plants in front of it with a clang
    /// and a dust ring, then a steel brace line runs out to each ally beside it (the allies it now holds in place), with an
    /// "Anchored!" callout over the Warden and "Braced" over each held ally. The buff itself gets its own CombatText from
    /// the BuffAppliedEvent that follows; a foe's shove that then fails calls "Braced!" (AnnounceEvent). Reduce Motion
    /// shows the planted shield and the lines without the drop.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color AnchorSteel = new Color(0.62f, 0.72f, 0.84f);
        private static readonly Color AnchorRim = new Color(1f, 0.84f, 0.45f);

        private IEnumerator AnchorStanceVfx(Unit warden)
        {
            Vector2 at = ViewCenter(warden);
            var braced = new List<Unit>();
            foreach (var u in State.Units)
                if (u != warden && u.Alive && u.OnBoard && u.Team == warden.Team && u.IsAdjacentTo(warden)) braced.Add(u);
            float size = BoardView.Tile * 0.32f;
            if (ReduceMotion)
            {
                Shield(at, size, 1f);
                foreach (var b in braced) board.Lines.Add(at, ViewCenter(b), AnchorSteel.WithA(0.8f), 5f);
                App.Audio.Sfx("battle.clang", 0.8f, 0.8f);
                Text.Show(at, CombatTextKind.Callout, "Anchored!", 0f, AnchorSteel);
                yield return Tween.Wait(Timing.Quick);
                board.Lines.ClearLines();
                yield break;
            }
            // The shield drops from above and plants.
            App.Audio.Sfx("battle.whoosh", 0.5f, 0.7f);
            yield return Tween.Run(Timing.Quick, k =>
            {
                board.Lines.ClearLines();
                Shield(at + new Vector2(0f, -BoardView.Tile * 0.6f * (1f - k * k)), size, k);
            });
            App.Audio.Sfx("battle.clang", 0.9f, 0.75f); // the plant: low and heavy
            App.Audio.Sfx("battle.clank", 0.6f, 0.6f);
            App.StartCoroutine(Tween.Shake(shake, 5f * Fx, Timing.Quick));
            Text.Show(at, CombatTextKind.Callout, "Anchored!", 0f, AnchorSteel);
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                board.Lines.Rings.Clear();
                Shield(at, size, 1f);
                board.Lines.Ring(at, Mathf.Lerp(size, BoardView.Tile * 0.9f, Ease.Out(k)), AnchorSteel.WithA(0.7f * (1f - k)), 6f);
                // Brace lines reach out to each ally beside the Warden.
                foreach (var b in braced)
                {
                    var to = ViewCenter(b);
                    var tip = Vector2.Lerp(at, to, Mathf.Clamp01(k * 1.6f));
                    board.Lines.Add(at, tip, AnchorSteel.WithA(0.85f), 6f);
                    if (k * 1.6f >= 1f) board.Lines.Dot(to, 8f, AnchorRim);
                }
            });
            foreach (var b in braced) Text.Show(ViewCenter(b), CombatTextKind.Support, "Braced", 0f, AnchorSteel);
            board.Lines.ClearLines(); board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
        }

        /// <summary>A kite shield outline centred at <paramref name="c"/>: flat top, sides, a point at the bottom, a boss.</summary>
        private void Shield(Vector2 c, float r, float alpha)
        {
            var tl = c + new Vector2(-r, -r);
            var tr = c + new Vector2(r, -r);
            var ml = c + new Vector2(-r, r * 0.25f);
            var mr = c + new Vector2(r, r * 0.25f);
            var tip = c + new Vector2(0f, r * 1.25f);
            var rim = AnchorRim.WithA(alpha);
            board.Lines.Add(tl, tr, rim, 6f);
            board.Lines.Add(tl, ml, rim, 6f);
            board.Lines.Add(tr, mr, rim, 6f);
            board.Lines.Add(ml, tip, rim, 6f);
            board.Lines.Add(mr, tip, rim, 6f);
            board.Lines.Add(c + new Vector2(0f, -r), tip, AnchorSteel.WithA(alpha), 4f);
            board.Lines.Dot(c + new Vector2(0f, -r * 0.2f), r * 0.28f, AnchorSteel.WithA(alpha));
        }
    }
}
