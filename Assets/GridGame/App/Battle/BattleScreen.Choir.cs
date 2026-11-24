using System.Collections;
using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// The Choir's rune circle on the board (GG-T939, BIBLE §14.17; the rules are GG-T902's BattleRunner.Choir): a rune is
    /// painted with a soft violet puff, a hero who steps on one scuffs it out in a splat, and when the circle closes
    /// (<see cref="RuneCircleEvent"/>) violet lines run between the singers' runes, the circle flashes with a hymn sting,
    /// then the summon's rune lights or an Empowered glow wells up round the singers (the SpawnUnit and BuffApplied events
    /// that follow play their own beats). Reduce Motion: the lines and the glow only fade in and out, no drawing on,
    /// swelling ring, burst or shake.
    /// </summary>
    public sealed partial class BattleScreen
    {
        /// <summary>A rune circle's tile state (the Choir's choir_rune: it has a <see cref="TileStateDef.Circle"/>), else null.</summary>
        private TileStateDef RuneState(string state)
        {
            var def = App.Content.TryTileState(state);
            return def?.Circle != null ? def : null;
        }

        /// <summary>The rune's own colour (choir_rune's violet), else the mana colour.</summary>
        private static Color RuneColor(TileStateDef def) => Art.HexColor(def?.Color, Palette.Mana);

        /// <summary>A rune is painted (a "marked" RegionTilesEvent of a circle state): each tile flashes violet and puffs a
        /// little arcane dust with a brushy chime. Reduce Motion: the flash only.</summary>
        private IEnumerator RunePaintAnim(RegionTilesEvent rt, TileStateDef def)
        {
            var col = RuneColor(def);
            App.Audio.Sfx("element.arcane", 0.55f, 1.35f);
            App.Audio.Sfx("battle.slide", 0.35f, 1.3f); // the brush stroke
            foreach (var t in rt.Tiles)
            {
                board.Mark(t, col, 0.5f);
                if (ReduceMotion) continue;
                Emit(StyleOf(DamageType.Arcane), BoardView.CenterOf(t), 0.35f, quiet: true);
                var cell = board.RegionCellAt(t);
                if (cell != null) App.StartCoroutine(Tween.Pop(cell, 1.2f, Timing.Quick));
            }
            yield return Tween.Wait(Timing.Standard);
            board.ClearMarks();
        }

        /// <summary>A hero scuffs a rune out (its SquishedEvent; the splat itself is SquishedAnim's): a dull scrape and a
        /// "Scuffed!" callout in the rune's colour.</summary>
        private void RuneScuffed(SquishedEvent sq, TileStateDef def)
        {
            App.Audio.Sfx("battle.dash", 0.55f, 0.7f);
            Text?.Show(sq.At, CombatTextKind.Callout, "Scuffed!", 0.3f, Color.Lerp(RuneColor(def), Palette.White, 0.3f));
        }

        /// <summary>The circle closes: lines join the singers' runes in turn, the circle flashes with the hymn sting, then
        /// the summon's rune lights or the singers glow.</summary>
        private IEnumerator RuneCircleAnim(RuneCircleEvent rc)
        {
            var col = RuneColor(App.Content.TryTileState(rc.State));
            var bright = Color.Lerp(col, Palette.White, 0.35f);
            var pts = RuneCirclePoints(rc);
            if (pts.Count == 0) yield break;
            var mid = Vector2.zero;
            foreach (var p in pts) mid += p;
            mid /= pts.Count;
            float reach = 0f;
            foreach (var p in pts) reach = Mathf.Max(reach, Vector2.Distance(mid, p));
            reach += BoardView.Tile * 0.5f;

            foreach (var t in rc.Runes) board.Mark(t, col, 0.45f);
            App.Audio.Sfx("battle.channel", 0.7f, 1.35f); // the hum gathers
            bool calm = ReduceMotion;

            // The lines: drawn on one after another round the circle (Reduce Motion: they fade in together).
            int n = pts.Count;
            int segs = n == 2 ? 1 : n;
            yield return Tween.Run(calm ? Timing.Quick : Timing.Emphasis, k =>
            {
                board.Lines.ClearLines();
                float alpha = calm ? k : 1f;
                for (int i = 0; i < segs; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % n];
                    float f = calm ? 1f : Mathf.Clamp01(k * segs - i);
                    if (f <= 0f) break;
                    var end = Vector2.Lerp(a, b, f);
                    board.Lines.Add(a, end, col.WithA(0.9f * alpha), 8f);
                    board.Lines.Add(a, end, bright.WithA(0.7f * alpha), 3f);
                }
                foreach (var p in pts) board.Lines.Dot(p, 14f, bright.WithA(alpha));
            });

            // The flash and the hymn sting.
            App.Audio.Sfx("battle.powerup", 0.8f, 1.2f);
            App.Audio.Sfx("element.arcane", 0.7f, 0.8f);
            Text?.Show(mid, CombatTextKind.Callout, rc.Summoned ? "The hymn answers!" : "The Choir swells!", 0.4f, bright);
            if (!calm)
            {
                Emit(StyleOf(DamageType.Arcane), mid, 0.8f, quiet: true);
                App.StartCoroutine(Tween.Shake(shake, 5f, Timing.Quick));
            }
            yield return Tween.Run(calm ? Timing.Standard : Timing.Emphasis, k =>
            {
                board.Lines.ClearLines();
                float fade = 1f - k;
                for (int i = 0; i < segs; i++) board.Lines.Add(pts[i], pts[(i + 1) % n], col.WithA(0.9f * fade), 8f);
                if (calm) board.Lines.Ring(mid, reach, bright.WithA(0.8f * fade), 6f);
                else
                {
                    board.Lines.Ring(mid, reach * (0.6f + 0.5f * Ease.Out(k)), bright.WithA(0.9f * fade), 10f);
                    board.Lines.Ring(mid, reach * 0.4f * Ease.Out(k), col.WithA(0.6f * fade), 6f);
                }
            });
            board.Lines.ClearLines();
            board.ClearMarks();

            // Then the summon's rune lights, or the singers glow.
            if (rc.Summoned)
            {
                var caller = State.UnitById(rc.UnitId);
                if (caller != null) board.Mark(caller.Pos, bright, 0.6f);
                App.Audio.Sfx("camp.summon", 0.6f, 0.9f);
                yield return Tween.Wait(Timing.Quick);
                board.ClearMarks();
                yield break;
            }
            var buff = App.Content.TryBuff("empowered");
            var glow = buff != null ? StatusColor(buff) : bright;
            var singers = new List<Vector2>();
            foreach (var id in rc.Singers)
            {
                var s = State.UnitById(id);
                if (s != null && s.Alive) singers.Add(ViewCenter(s));
            }
            if (singers.Count == 0) yield break;
            yield return Tween.Run(Timing.Emphasis, k =>
            {
                board.Lines.ClearLines();
                float a = Ease.Out(1f - Mathf.Abs(2f * k - 1f)); // in and out
                float r = calm ? BoardView.Tile * 0.42f : BoardView.Tile * (0.3f + 0.2f * Ease.Out(k));
                foreach (var p in singers)
                {
                    board.Lines.Ring(p, r, glow.WithA(0.85f * a), 8f);
                    board.Lines.Dot(p, 10f, glow.WithA(0.4f * a));
                }
            });
            board.Lines.ClearLines();
        }

        /// <summary>Where the circle's lines run: the singers' centres (they stand on the runes), round the circle in angle
        /// order; the runes' own tiles when fewer than two singers can be found.</summary>
        private List<Vector2> RuneCirclePoints(RuneCircleEvent rc)
        {
            var pts = new List<Vector2>();
            foreach (var id in rc.Singers)
            {
                var s = State.UnitById(id);
                if (s != null && s.OnBoard) pts.Add(ViewCenter(s));
            }
            if (pts.Count < 2)
            {
                pts.Clear();
                foreach (var t in rc.Runes) pts.Add(BoardView.CenterOf(t));
            }
            if (pts.Count < 3) return pts;
            var mid = Vector2.zero;
            foreach (var p in pts) mid += p;
            mid /= pts.Count;
            pts.Sort((a, b) => Mathf.Atan2(a.y - mid.y, a.x - mid.x).CompareTo(Mathf.Atan2(b.y - mid.y, b.x - mid.x)));
            return pts;
        }
    }
}
