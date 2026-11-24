using System.Collections;
using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Afterimage on the board (§7, GG-T867): the Speedster's dash streaks a pale ghost trail down its line (a few
    /// fading copies of its art), leaves a translucent, shimmering copy on the tile it left with an "Afterimage" callout,
    /// and that copy stays until its AfterimageFadedEvent: struck (it shatters with a flash) or faded (it thins away).</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color AfterimageColor = new Color(0.62f, 0.88f, 1f);
        private readonly Dictionary<int, VisualElement> afterimageGhosts = new Dictionary<int, VisualElement>();

        private VisualElement GhostOf(int unitId, Coord at, float opacity)
        {
            var g = new VisualElement { pickingMode = PickingMode.Ignore };
            g.style.position = Position.Absolute;
            g.style.left = at.X * BoardView.Tile; g.style.top = at.Y * BoardView.Tile;
            g.style.width = BoardView.Tile; g.style.height = BoardView.Tile;
            if (board.Units.TryGetValue(unitId, out var v)) g.style.backgroundImage = v.Art.style.backgroundImage;
            g.style.unityBackgroundImageTintColor = AfterimageColor;
            g.style.opacity = opacity;
            board.MarkLayer.Add(g);
            return g;
        }

        private IEnumerator AfterimageLeftAnim(AfterimageLeftEvent e)
        {
            App.Audio.Sfx("battle.slide", 0.8f, 1.35f);
            Vector2 from = BoardView.CenterOf(e.From);
            Text.Show(from, CombatTextKind.Callout, "Afterimage", 0f, AfterimageColor);
            if (afterimageGhosts.TryGetValue(e.UnitId, out var old)) { old.RemoveFromHierarchy(); afterimageGhosts.Remove(e.UnitId); }
            var ghost = GhostOf(e.UnitId, e.From, 0.45f);
            afterimageGhosts[e.UnitId] = ghost;
            if (ReduceMotion) { yield return Tween.Wait(Timing.Quick); yield break; }
            // The trail: a copy on every tile the dash crossed, thinning out toward where it ends.
            var trail = new List<VisualElement>();
            var line = Afterimage.Line(e.From, e.To);
            for (int i = 1; i < line.Count - 1; i++) trail.Add(GhostOf(e.UnitId, line[i], 0f));
            yield return Tween.Run(Timing.Standard, k =>
            {
                for (int i = 0; i < trail.Count; i++)
                {
                    float start = (i + 1f) / (trail.Count + 1f) * 0.4f;
                    float tk = Mathf.Clamp01((k - start) / 0.6f);
                    trail[i].style.opacity = 0.5f * Mathf.Sin(tk * Mathf.PI);
                }
                ghost.style.opacity = 0.45f + 0.2f * Mathf.Sin(k * Mathf.PI * 4f);
                board.Lines.ClearLines();
                board.Lines.Add(from, Vector2.Lerp(from, BoardView.CenterOf(e.To), Mathf.Min(1f, k * 2f)), AfterimageColor.WithA(0.6f * (1f - k)), 10f);
            });
            board.Lines.ClearLines();
            foreach (var t in trail) t.RemoveFromHierarchy();
            ghost.style.opacity = 0.45f;
        }

        private IEnumerator AfterimageFadedAnim(AfterimageFadedEvent e)
        {
            if (!afterimageGhosts.TryGetValue(e.UnitId, out var ghost)) yield break;
            afterimageGhosts.Remove(e.UnitId);
            bool struck = e.Reason == "struck";
            App.Audio.Sfx(struck ? "battle.shatter" : "battle.slide", struck ? 0.8f : 0.4f, struck ? 1.4f : 0.8f);
            if (struck) Text.Show(BoardView.CenterOf(e.At), CombatTextKind.Callout, "Afterimage", 0f, AfterimageColor);
            if (ReduceMotion) { ghost.RemoveFromHierarchy(); yield return Tween.Wait(Timing.Instant); yield break; }
            yield return Tween.Run(struck ? Timing.Quick : Timing.Standard, k =>
            {
                ghost.style.opacity = 0.45f * (1f - k);
                if (struck) ghost.style.scale = new Scale(Vector3.one * (1f + 0.3f * k));
            });
            ghost.RemoveFromHierarchy();
        }
    }
}
