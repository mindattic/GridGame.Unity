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
            var path = MoveReach.Route(State, hero.Id, tile, BattleEngine.MaxDragSteps(State));
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
                    v.SetGlow(Palette.Vfx.SelectGlow, u.Id == SelectedHeroId ? 0.65f : 0f);
        }

        /// <summary>The move's confirm card in the description panel: where to, the pincers it makes and their outcome, then
        /// Cancel and Go.</summary>
        private void TapMoveCard(Unit hero)
        {
            var col = Ui.El("col", panel);
            col.name = "move-card";
            col.style.marginBottom = Space.S16;
            int tiles = tapMovePath.Count;
            int pincers = tapMovePreview?.Pincers.Count ?? 0;
            string what = pincers > 1 ? $" · PINCER ×{pincers}" : pincers == 1 ? " · PINCER" : "";
            var summary = Ui.Text(PanelCopy(PanelState.TapMove, hero.Name, n: tiles, extra: what), "small gold", col);
            summary.name = "move-confirm-summary";
            summary.style.whiteSpace = WhiteSpace.Normal;
            PincerOutcome(col, hero);
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

        /// <summary>
        /// GG-T1946 (UX review §6.4): the drop's pincer outcome, as the turn strip shows units (thumbnails, not names). One
        /// row of the heroes who strike (both ends and their supporters), then a row per foe caught: its damage range and
        /// KO odds from <see cref="OutcomePreviewer"/>, and how many seconds its turn is pushed back, as the rules applied
        /// it in the same samples. Each value stays whole and the line wraps beside the thumb, so nothing is cut off.
        /// </summary>
        private void PincerOutcome(VisualElement parent, Unit hero)
        {
            var p = OutcomePreviewer.Preview(State, new MoveCommand { HeroId = hero.Id, Path = new List<Coord>(tapMovePath) }, 3);
            if (p.Refused) return;
            var strikers = new List<int>();
            foreach (var pc in tapMovePreview?.Pincers ?? new List<Pincer>())
                foreach (var list in new[] { new List<Unit> { pc.A, pc.B }, pc.SupportersA, pc.SupportersB })
                    foreach (var u in list)
                        if (u != null && u.IsHero && !strikers.Contains(u.Id)) strikers.Add(u.Id);
            if (strikers.Count > 0)
            {
                var who = Ui.El("row", parent);
                who.name = "move-confirm-strikers";
                who.style.alignItems = Align.Center;
                who.style.flexWrap = Wrap.Wrap;
                who.style.marginTop = Space.S8;
                Ui.Text("Strike", "t-caption", who).style.marginRight = Space.S8;
                foreach (var id in strikers)
                    if (State.UnitById(id) is Unit u) OutcomeThumb(u, who);
            }
            foreach (var t in p.Targets)
            {
                var u = State.UnitById(t.UnitId);
                if (u == null || u.IsHero || (t.DamageMax <= 0 && t.PushMax <= 0f)) continue;
                var parts = new List<string>();
                if (t.DamageMax > 0) parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Palette.Bad)}>−{Range(t.DamageMin, t.DamageMax)}</color>");
                if (t.KillChance > 0f) parts.Add(t.KillChance >= 1f ? "KO" : $"KO {Mathf.RoundToInt(t.KillChance * 100)}%");
                if (t.PushMax > 0f) parts.Add($"pushed back {Secs(t.PushMin, t.PushMax)}");
                var row = Ui.El("row", parent);
                row.name = "move-confirm-outcome";
                row.style.alignItems = Align.Center;
                row.style.marginTop = Space.S8;
                OutcomeThumb(u, row);
                var l = Ui.Text(string.Join(" · ", parts.ConvertAll(x => $"<nobr>{x}</nobr>")), "t-caption", row);
                l.style.flexShrink = 1;
                l.style.whiteSpace = WhiteSpace.Normal;
            }
        }

        private static void OutcomeThumb(Unit u, VisualElement parent)
        {
            var t = Ui.Thumb(u.Def.Id, parent, Size.IconS);
            t.name = "outcome-unit-" + u.Id;
            t.style.flexShrink = 0;
            t.pickingMode = PickingMode.Ignore;
            t.style.marginRight = Space.S8;
            t.style.borderTopLeftRadius = t.style.borderTopRightRadius = t.style.borderBottomLeftRadius = t.style.borderBottomRightRadius = Space.S8;
        }

        /// <summary>Seconds as the HUD prints them: "1.5 s", or "1–1.5 s" when the samples differ.</summary>
        private static string Secs(float a, float b)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string x = a.ToString("0.#", inv), y = b.ToString("0.#", inv);
            return (x == y ? x : $"{x}–{y}") + " s";
        }
    }
}
