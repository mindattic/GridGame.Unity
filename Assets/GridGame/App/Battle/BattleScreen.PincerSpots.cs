using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1945 (UX review §6.3, §9.0): pincer spots. While a hero is selected and the clock is frozen, every empty tile it
    /// can walk to this window whose drop traps a foe (<see cref="MoveReach.PincerSpots"/>, the same route search tap-to-move
    /// uses) carries a small gold dot, and each foe one of them would catch wears a thin gold outline. Light on purpose: no
    /// rings round the hero, nothing while dragging, aiming, picking a tap-move or watching playback. It breathes gently, or
    /// holds still under Reduce Motion. Tapping a spot is an ordinary tap-move pick.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private const float PincerSpotDot = BoardView.Tile * 0.1f, PincerSpotFoeInset = 10f;

        private VisualElement pincerSpotLayer;
        private List<PincerSpot> pincerSpots;
        private int pincerSpotsKey;

        /// <summary>30 Hz from the timeline pulse: shows, hides and breathes the spots, re-searching only when the board,
        /// the selection or the step budget changed.</summary>
        private void TickPincerSpots(float t)
        {
            if (pincerSpotLayer == null || pincerSpotLayer.parent != board.CueLayer)
            {
                pincerSpotLayer = new VisualElement { name = "pincer-spots", pickingMode = PickingMode.Ignore };
                pincerSpotLayer.style.position = Position.Absolute;
                pincerSpotLayer.style.left = pincerSpotLayer.style.top = pincerSpotLayer.style.right = pincerSpotLayer.style.bottom = Space.S0;
                pincerSpotLayer.generateVisualContent += DrawPincerSpots;
                board.CueLayer.Add(pincerSpotLayer);
                pincerSpots = null;
            }
            var hero = State.UnitById(SelectedHeroId);
            bool show = hero != null && hero.Alive && hero.IsHero && hero.IsCommandable && CanCommand() && !TapMoving
                && TargetingSlot < 0 && (Untimed || heroTurnHero < 0) && BattleEngine.CanMove(State, hero.Id);
            if (!show)
            {
                pincerSpotLayer.style.display = DisplayStyle.None;
                return;
            }
            int steps = BattleEngine.MaxDragSteps(State);
            int key = PincerSpotsKey(hero.Id, steps);
            if (pincerSpots == null || key != pincerSpotsKey)
            {
                pincerSpotsKey = key;
                pincerSpots = MoveReach.PincerSpots(State, hero.Id, steps);
                pincerSpotLayer.MarkDirtyRepaint();
            }
            pincerSpotLayer.style.display = pincerSpots.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            pincerSpotLayer.style.opacity = ReduceMotion ? 1f : 0.75f + 0.25f * Mathf.Sin(t * 3f);
        }

        /// <summary>What the search depends on: the state, the hero, the step budget, the clock and where every unit stands.</summary>
        private int PincerSpotsKey(int heroId, int steps)
        {
            unchecked
            {
                int k = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(State);
                k = k * 31 + heroId;
                k = k * 31 + steps;
                k = k * 31 + State.Results.ActionsTaken;
                k = k * 31 + State.Clock.GetHashCode();
                foreach (var u in State.Units)
                    if (u.Alive) k = (k * 31 + u.Id) * 31 + u.Pos.GetHashCode();
                return k;
            }
        }

        /// <summary>A filled, outlined gold dot on each spot and a thin gold frame round each foe caught (once each).</summary>
        private void DrawPincerSpots(MeshGenerationContext mgc)
        {
            if (pincerSpots == null || pincerSpots.Count == 0) return;
            var p = mgc.painter2D;
            var style = ShapeStyle.Small.Tint(Palette.Gold);
            var framed = new HashSet<int>();
            foreach (var spot in pincerSpots)
            {
                foreach (var id in spot.FoeIds)
                {
                    if (!framed.Add(id) || !board.Units.TryGetValue(id, out var v) || v.Dead) continue;
                    var r = new Rect(BoardView.PosOf(v.Tile), new Vector2(v.W * BoardView.Tile, v.H * BoardView.Tile));
                    float i = PincerSpotFoeInset;
                    Shapes.Line(p, new[] { new Vector2(r.xMin + i, r.yMin + i), new Vector2(r.xMax - i, r.yMin + i),
                        new Vector2(r.xMax - i, r.yMax - i), new Vector2(r.xMin + i, r.yMax - i) }, style, closed: true);
                }
            }
            foreach (var spot in pincerSpots) Shapes.Dot(p, BoardView.CenterOf(spot.Tile), PincerSpotDot, style);
        }
    }
}
