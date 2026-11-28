using System.Collections.Generic;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1947 (UX review §1.4 #4, §6 item 5, §9.0): enemy intent. While the party's window is open, each of the next two
    /// foes on the bar that would hurt a hero gets a thin red Bezier arrow (Shapes, filled and outlined) from its tile to
    /// the hero it would hit hardest, with a damage badge at the arrow's middle: the range (and KO odds) from
    /// <see cref="OutcomePreviewer.Intent"/>, the foes' own brains run on clones. It hides while dragging and while a
    /// timeline diamond's tap arrow shows, so the two never cross. Nothing moves, so Reduce Motion changes nothing.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private const int IntentActors = 2;
        /// <summary>How far each end stops short of its tile's centre, as a share of a tile.</summary>
        private const float IntentTrim = 0.3f;

        private VisualElement intentLayer;
        private readonly List<(int from, int to)> intentArrows = new List<(int, int)>();
        private int intentKey;

        /// <summary>30 Hz from the timeline pulse: shows or hides the arrows, re-predicting only when the board changed.</summary>
        private void TickEnemyIntent()
        {
            if (intentLayer == null || intentLayer.parent != board.CueLayer)
            {
                intentLayer = new VisualElement { name = "enemy-intent", pickingMode = PickingMode.Ignore };
                intentLayer.style.position = Position.Absolute;
                intentLayer.style.left = intentLayer.style.top = intentLayer.style.right = intentLayer.style.bottom = Space.S0;
                intentLayer.generateVisualContent += DrawEnemyIntent;
                board.CueLayer.Add(intentLayer);
                intentKey = 0;
            }
            bool show = PartyWindowOpen(ActiveHeroEntry()) && !dragging && tapArrowFrom == null;
            if (!show)
            {
                intentLayer.style.display = DisplayStyle.None;
                return;
            }
            int key = unchecked(PincerSpotsKey(-1, 0) * 31 + board.Units.Count);
            if (key != intentKey)
            {
                intentKey = key;
                PredictIntent();
            }
            intentLayer.style.display = intentArrows.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Runs the prediction and rebuilds the arrows and their badges.</summary>
        private void PredictIntent()
        {
            intentArrows.Clear();
            intentLayer.Clear();
            foreach (var intent in OutcomePreviewer.Intent(State, IntentActors))
            {
                OutcomeTarget top = null;
                foreach (var t in intent.Targets)
                    if (t.DamageMax > 0 && (top == null || t.DamageMax > top.DamageMax)) top = t;
                if (top == null || !IntentBend(intent.ActorId, top.UnitId, out var b0, out var b1, out var b2, out var b3)) continue;
                intentArrows.Add((intent.ActorId, top.UnitId));
                var mid = (b0 + 3f * b1 + 3f * b2 + b3) / 8f;
                string text = $"−{Range(top.DamageMin, top.DamageMax)}" + (top.KillChance > 0f ? (top.KillChance >= 1f ? " KO" : $" KO {Mathf.RoundToInt(top.KillChance * 100)}%") : "");
                var badge = new Label(text) { name = "intent-badge-" + intent.ActorId, pickingMode = PickingMode.Ignore };
                badge.AddToClassList("shadow");
                badge.style.position = Position.Absolute;
                badge.style.width = BoardView.Tile * 1.6f;
                badge.style.left = mid.x - BoardView.Tile * 0.8f;
                badge.style.top = mid.y - TypeScale.Base;
                badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                badge.style.fontSize = TypeScale.Base;
                badge.style.color = top.KillChance > 0f ? Palette.Gold : Palette.Text;
                Fonts.UseHeavy(badge);
                intentLayer.Add(badge);
            }
            intentLayer.MarkDirtyRepaint();
        }

        /// <summary>The arrow's control points in board space, bowed above the chord like the tap arrow; false when either
        /// end's token is gone.</summary>
        private bool IntentBend(int fromId, int toId, out Vector2 b0, out Vector2 b1, out Vector2 b2, out Vector2 b3)
        {
            b0 = b1 = b2 = b3 = default;
            if (!board.Units.TryGetValue(fromId, out var fv) || fv.Dead || !board.Units.TryGetValue(toId, out var tv) || tv.Dead) return false;
            var a = BoardView.CenterOf(fv.Tile, fv.W, fv.H);
            var b = BoardView.CenterOf(tv.Tile, tv.W, tv.H);
            var span = b - a;
            if (span.sqrMagnitude < 1f) return false;
            var trim = span.normalized * (BoardView.Tile * IntentTrim);
            b0 = a + trim; b3 = b - trim;
            span = b3 - b0;
            float bow = b3.x >= b0.x ? -TapArrowBow : TapArrowBow;
            var off = new Vector2(-span.y, span.x).normalized * (span.magnitude * bow);
            b1 = b0 + span / 3f + off; b2 = b0 + span * (2f / 3f) + off;
            return true;
        }

        private void DrawEnemyIntent(MeshGenerationContext mgc)
        {
            var style = ShapeStyle.Indicator.Tint(Palette.TeamFoe);
            foreach (var (from, to) in intentArrows)
                if (IntentBend(from, to, out var b0, out var b1, out var b2, out var b3)) Shapes.BezierArrow(mgc.painter2D, b0, b1, b2, b3, style);
        }
    }
}
