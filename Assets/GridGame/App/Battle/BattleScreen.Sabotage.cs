using System.Collections;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// The tech gremlin's Sabotage on the board (GG-T948, the view half of GG-T903, BIBLE §14.23): a crackling spark of
    /// tampering jumps from the gremlin to the gadget's tile with a zap and a clank, the mark's team edge turns from its old
    /// side's colour to the saboteur's, a sabotaged hidden charge (a buried mine the party knew of) fades from the party's
    /// view instead of re-colouring, and a "Sabotaged!" callout rises over the tile. A mark the party never saw stays
    /// unseen: no spark gives its place away. Reduce Motion: a straight spark line, the edge and fade snap.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color SabotageSpark = Palette.Vfx.Sabotage;

        private static Color TeamEdge(Team t) =>
            t == Team.Hero ? (Palette.Colorblind ? Palette.CbHero : Palette.Hero) : Palette.TeamFoe;

        private IEnumerator SabotageAnim(TileSabotagedEvent e)
        {
            var cell = board.RegionCellAt(e.At); // the mark as the party saw it before it turned
            var mark = State.Region?.At(e.At);
            bool seesNow = mark != null && RegionBoard.Sees(mark, Team.Hero);
            if (cell == null && !seesNow) { board.SyncRegion(State); yield break; }
            var gremlin = State.UnitById(e.UnitId);
            Vector2 to = BoardView.CenterOf(e.At);
            Vector2 from = gremlin != null ? ViewCenter(gremlin) : to;
            Color fromEdge = TeamEdge(e.FromTeam), toEdge = TeamEdge(e.ToTeam);
            bool fades = cell != null && !seesNow; // the party's hidden charge, now the foes' secret
            App.Audio.Sfx("element.lightning", 0.55f, 1.6f);

            void Edge(float k)
            {
                if (cell == null) return;
                var c = Color.Lerp(fromEdge, toEdge, k);
                cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = c;
                if (fades) cell.style.opacity = 1f - k;
            }

            if (ReduceMotion)
            {
                board.Lines.Add(from, to, SabotageSpark.WithA(0.85f), 3f);
                board.Lines.Dot(to, 10f, SabotageSpark);
                Edge(1f);
                App.Audio.Sfx("battle.clank", 0.5f, 1.4f);
                Text.Show(to, CombatTextKind.Callout, "Sabotaged!", 0f, toEdge);
                yield return Tween.Wait(Timing.Quick);
                board.Lines.ClearLines();
                board.SyncRegion(State);
                yield break;
            }

            // The spark: a jagged bolt that crawls from the gremlin to the gadget, re-jittered every frame so it crackles.
            var d = to - from;
            var normal = d.sqrMagnitude > 0.01f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
            yield return Tween.Run(Timing.Quick, k =>
            {
                board.Lines.ClearLines();
                const int segs = 7;
                var prev = from;
                for (int i = 1; i <= segs; i++)
                {
                    float u = k * i / segs;
                    var p = Vector2.Lerp(from, to, u);
                    if (i < segs) p += normal * UnityEngine.Random.Range(-7f, 7f);
                    board.Lines.Add(prev, p, SabotageSpark.WithA(0.5f + 0.5f * i / segs), 3f);
                    prev = p;
                }
                board.Lines.Dot(prev, 7f + 3f * UnityEngine.Random.value, Palette.White);
            });

            // It bites: a clank, a burst of sparks on the tile, and the edge turns (or the hidden charge fades away).
            App.Audio.Sfx("battle.clank", 0.55f, 1.4f);
            Text.Show(to, CombatTextKind.Callout, "Sabotaged!", 0f, toEdge);
            yield return Tween.Run(Timing.Standard, k =>
            {
                board.Lines.ClearLines();
                float fade = 1f - k;
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * Mathf.PI / 3f + k * 2f;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    board.Lines.Add(to + dir * (6f + 10f * k), to + dir * (12f + 20f * k), SabotageSpark.WithA(0.9f * fade), 2f);
                }
                board.Lines.Dot(to, 6f + 10f * k, toEdge.WithA(0.6f * fade));
                Edge(Mathf.Clamp01(k * 1.6f));
            });
            board.Lines.ClearLines();
            board.SyncRegion(State);
        }
    }
}
