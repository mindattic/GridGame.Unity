using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1977 (accessibility I1, WCAG 2.5.7): tap-to-move, the alternative to dragging. With a hero selected, a tap on an
    /// empty tile picks it: the shortest route the hero can walk there this window is drawn as a path arrow, the pincers and
    /// shoves the walk would make show as the drag preview does, and the description panel turns into the move's confirm
    /// card (Cancel and Go). A second tap on the same tile, or Go, commits it; any other tap re-picks or drops it. The clock
    /// stays frozen while picking. The command carries the drag model's seconds for the route
    /// (<see cref="BattleEngine.DragSeconds"/>), so a recorded tap-move replays exactly. The drag stays as the fast path.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private int tapMoveHeroId = -1;
        private Coord? tapMoveTile;
        private List<Coord> tapMovePath;
        private MovePreview tapMovePreview;
        /// <summary>The empty tile a press began on, picked on its release over the same tile.</summary>
        private Coord? tapMovePress;

        /// <summary>A picked tap-move tile awaiting Go.</summary>
        public bool TapMoving => tapMoveTile.HasValue;

        /// <summary>A press on <paramref name="tile"/> may pick a tap-move: no aim, a selected hero who may move now, and no
        /// coached lesson step owning the board.</summary>
        private bool TapMoveAllowed(Coord tile)
        {
            if (TargetingSlot >= 0 || Coach.InSteps || State.UnitAt(tile) != null) return false;
            var hero = State.UnitById(SelectedHeroId);
            return hero != null && hero.Alive && hero.IsHero && hero.IsCommandable && BattleEngine.CanMove(State, hero.Id);
        }

        /// <summary>
        /// Picks <paramref name="tile"/> as the selected hero's tap-move; the same tile again confirms. The hook for pincer
        /// spots (GG-T1945): a spot's tap calls this with its tile, so it previews the same route and outcome.
        /// </summary>
        public bool TapMoveTo(Coord tile)
        {
            if (!CanCommand()) return false;
            if (TapMoving && tapMoveTile == tile && tapMoveHeroId == SelectedHeroId) { ConfirmTapMove(); return true; }
            ClearTapMove();
            if (!TapMoveAllowed(tile)) return false;
            var hero = State.UnitById(SelectedHeroId);
            var path = TapMoveRoute(State, hero.Id, tile, BattleEngine.MaxDragSteps(State));
            if (path == null)
            {
                App.Audio.Sfx("ui.error", 0.5f);
                ShowBanner($"{hero.Name} can't reach that tile this turn");
                return false;
            }
            tapMoveHeroId = hero.Id;
            tapMoveTile = tile;
            tapMovePath = path;
            tapMovePreview = BattleEngine.PreviewMove(State, hero.Id, path);
            if (!ConfirmActions || AutoPlay) { ConfirmTapMove(); return true; }
            DrawTapMove();
            App.Audio.Sfx("ui.select", 0.8f);
            SyncPanel();
            var btn = panel.Q<Button>("move-confirm-action");
            if (btn != null) App.StartCoroutine(Tween.Pop(btn, 1.08f, 0.25f));
            return true;
        }

        /// <summary>
        /// The shortest cardinal route (one tile at a time, §1.1) from the hero to <paramref name="dest"/> that the move
        /// rules let it walk in full, within <paramref name="maxSteps"/>; null when there is none. Neighbours are tried in a
        /// fixed order, so the same board always gives the same route.
        /// </summary>
        private static List<Coord> TapMoveRoute(BattleState s, int heroId, Coord dest, int maxSteps)
        {
            var hero = s.UnitById(heroId);
            if (hero == null || hero.Pos == dest || maxSteps <= 0) return null;
            var seen = new HashSet<Coord> { hero.Pos };
            var frontier = new Queue<List<Coord>>();
            frontier.Enqueue(new List<Coord>());
            while (frontier.Count > 0)
            {
                var path = frontier.Dequeue();
                if (path.Count >= maxSteps) continue;
                var at = path.Count > 0 ? path[path.Count - 1] : hero.Pos;
                foreach (var step in new[] { new Coord(at.X, at.Y - 1), new Coord(at.X + 1, at.Y), new Coord(at.X, at.Y + 1), new Coord(at.X - 1, at.Y) })
                {
                    if (step.X < 0 || step.Y < 0 || step.X >= BattleConfig.Cols || step.Y >= BattleConfig.Rows || !seen.Add(step)) continue;
                    var next = new List<Coord>(path) { step };
                    if (BattleEngine.PreviewMove(s, heroId, next).StepsTaken.Count < next.Count) continue;
                    if (step == dest) return next;
                    frontier.Enqueue(next);
                }
            }
            return null;
        }

        /// <summary>The picked route as a path arrow from the hero to the tile, the tile itself in gold, and the drop's
        /// pincers, shoves and damage tags as the drag preview draws them.</summary>
        private void DrawTapMove()
        {
            var hero = State.UnitById(tapMoveHeroId);
            if (hero == null || tapMovePreview == null) return;
            ApplyPreview(tapMovePreview, hero.Id);
            ShowDragOutcome(tapMovePreview, hero.Id, tapMovePath);
            var pts = new List<Vector2> { BoardView.CenterOf(hero.Pos) };
            foreach (var c in tapMovePath) pts.Add(BoardView.CenterOf(c));
            board.Lines.PathArrow(pts, ShapeStyle.Indicator.Tint(HeroTrail.WithA(0.95f)));
            board.Mark(tapMoveTile.Value, Palette.Gold, 0.4f);
        }

        /// <summary>Commits the picked tap-move (Go, or a second tap on the same tile).</summary>
        public void ConfirmTapMove()
        {
            if (!TapMoving || !CanCommand()) return;
            var hero = State.UnitById(tapMoveHeroId);
            if (hero == null || !BattleEngine.CanMove(State, hero.Id)) { ClearTapMove(); SyncPanel(); return; }
            float held = BattleEngine.DragSeconds(tapMovePath.Count);
            float window = BattleEngine.TurnWindowSeconds(State);
            if (window > 0f) held = Mathf.Min(held, window);
            var cmd = new MoveCommand { HeroId = hero.Id, Path = new List<Coord>(tapMovePath), HeldSeconds = held };
            App.Audio.Sfx("ui.confirm");
            ClearTapMove();
            Commit(cmd);
        }

        /// <summary>Player-facing cancel of a picked tap-move (the card's Cancel, Back).</summary>
        public void CancelTapMove()
        {
            if (!TapMoving) return;
            App.Audio.Sfx("ui.cancel", 0.8f);
            ClearTapMove();
            SyncPanel();
        }

        /// <summary>Drops a picked tap-move: the preview's slid tokens go back, its lines, marks, tags and glows clear.</summary>
        private void ClearTapMove()
        {
            tapMovePress = null;
            if (!TapMoving) return;
            tapMoveTile = null;
            tapMovePath = null;
            tapMovePreview = null;
            tapMoveHeroId = -1;
            board.Lines.ClearLines();
            board.ClearMarks();
            ClearOutcomeTags();
            ShowBanner("");
            SyncAll();
            foreach (var u in State.Units)
                if (board.Units.TryGetValue(u.Id, out var v) && u.Alive)
                    v.SetGlow(new Color(1f, 0.9f, 0.5f), u.Id == SelectedHeroId ? 0.65f : 0f);
        }

        /// <summary>The move's confirm card in the description panel: where to, the pincers it makes and their damage, then
        /// Cancel and Go.</summary>
        private void TapMoveCard(Unit hero)
        {
            var col = Ui.El("col", panel);
            col.name = "move-card";
            col.style.marginBottom = Space.S16;
            int tiles = tapMovePath.Count;
            int pincers = tapMovePreview?.Pincers.Count ?? 0;
            string what = pincers > 1 ? $" · PINCER ×{pincers}" : pincers == 1 ? " · PINCER" : "";
            var summary = Ui.Text($"Move {hero.Name} {tiles} tile{(tiles == 1 ? "" : "s")}{what}", "small gold", col);
            summary.name = "move-confirm-summary";
            summary.style.whiteSpace = WhiteSpace.Normal;
            var p = OutcomePreviewer.Preview(State, new MoveCommand { HeroId = hero.Id, Path = new List<Coord>(tapMovePath) }, 3);
            if (!p.Refused)
                foreach (var t in p.Targets)
                {
                    var u = State.UnitById(t.UnitId);
                    if (u == null || u.IsHero || t.DamageMax <= 0) continue;
                    string ko = t.KillChance > 0f ? (t.KillChance >= 1f ? " · KO" : $" · KO {Mathf.RoundToInt(t.KillChance * 100)}%") : "";
                    var l = Ui.Text($"{u.Name}: <nobr><color=#{ColorUtility.ToHtmlStringRGB(Palette.Bad)}>−{Range(t.DamageMin, t.DamageMax)}</color>{ko}</nobr>", "t-caption", col);
                    l.name = "move-confirm-outcome";
                    l.style.whiteSpace = WhiteSpace.Normal;
                }
            // Cancel left, Go right (L7), as the ability confirm card.
            var btns = Ui.El("row", col);
            btns.name = "move-confirm-buttons";
            btns.style.justifyContent = Justify.SpaceBetween;
            btns.style.marginTop = Space.S8;
            var cancel = RowIcon.IconButton("Cancel", RowKind.Close, CancelTapMove, "btn--small", btns);
            cancel.name = "move-confirm-cancel";
            var ok = RowIcon.IconButton("Go", RowKind.Confirm, ConfirmTapMove, "btn--primary", btns, confirmTap: false);
            ok.name = "move-confirm-action";
        }
    }
}
