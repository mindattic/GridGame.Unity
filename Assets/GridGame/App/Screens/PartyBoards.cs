using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Party boards: every party hero's skill hexagon and bar side by side (GG-T1044)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The Party tab's top strip: one column per active party hero (up to six) holding its name, a read-only miniature of
    /// its skill hexagon (<see cref="SkillHexView"/> with its <see cref="HeroBoard.Owned"/> trail, scaled to the column)
    /// and its current 5-slot battle bar (<see cref="HeroSave.Bar"/>) as icons, so a party of six builds reads at a glance.
    /// Nothing here is tappable: the board is bought on the Abilities tab's Board sub-tab and the bar is set there too.
    /// </summary>
    internal static class PartyBoards
    {
        private static readonly float BoardSide = 2f * (SkillHexView.BoardRadius + SkillHexView.Margin);

        public static void Build(ContentDb c, SaveData s, VisualElement body)
        {
            var board = HeroBoard.Board(c);
            if (board == null || s?.Party == null || s.Party.Count == 0) return;
            var panel = Ui.Panel(body, "Party builds", false, "party-boards");
            var row = Ui.El("row", panel);
            row.style.alignItems = Align.FlexStart;
            int unlocked = Progression.UnlockedSlots(s);
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                if (h == null) continue;
                Column(c, board, h, unlocked, row);
            }
        }

        private static void Column(ContentDb c, SkillHexBoard board, HeroSave h, int unlocked, VisualElement row)
        {
            var col = Ui.El("col", row);
            col.name = "party-board-" + h.ClassId;
            col.style.flexGrow = 1; col.style.flexBasis = 0; col.style.minWidth = 0;
            col.style.marginLeft = col.style.marginRight = Space.S4;
            col.style.alignItems = Align.Stretch;

            // GG-T1112: one line through the shared marquee (POL-C5), so a long class name scrolls instead of clipping.
            var name = Ui.Marquee(Ui.Caption(c.TryClass(h.ClassId)?.Name ?? h.ClassId, col), grow: false);
            name.style.unityTextAlign = TextAnchor.MiddleCenter;

            // The miniature: a square box clipping a full-size view scaled about its centre to the box's width.
            var box = Ui.El(null, col);
            box.style.overflow = Overflow.Hidden;
            box.style.position = Position.Relative;
            box.pickingMode = PickingMode.Ignore;
            var view = new SkillHexView(board) { pickingMode = PickingMode.Ignore };
            view.style.position = Position.Absolute;
            view.SetOwned(board.StartOf(h.ClassId), HeroBoard.Owned(c, h).Nodes);
            box.Add(view);

            // The bar: one square per slot, the entry's icon or an empty (dim) square; locked slots faded.
            var bar = Ui.El("row", col);
            bar.name = "party-bar-" + h.ClassId;
            bar.style.marginTop = Space.S4;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.justifyContent = Justify.Center;
            var cells = new VisualElement[Progression.MaxBarSlots];
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = Ui.El(null, bar);
                cells[i] = cell;
                cell.style.flexShrink = 0;
                cell.style.marginLeft = cell.style.marginRight = CellGap / 2f;
                cell.style.marginBottom = CellGap;
                cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 4;
                cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 1;
                var bc = Palette.Text.WithA(0.4f);
                cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = bc;
                cell.style.backgroundColor = Palette.Ink.WithA(0.9f);
                if (i >= unlocked) { cell.style.opacity = 0.3f; continue; }
                var entry = h.Bar != null && i < h.Bar.Count ? h.Bar[i] : null;
                var icon = EntryIcon(c, entry);
                if (icon != null) cell.style.backgroundImage = new StyleBackground(icon);
            }

            col.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float w = col.contentRect.width;
                if (w <= 0f) return;
                box.style.width = w; box.style.height = w;
                float off = (w - BoardSide) / 2f;
                view.style.left = off; view.style.top = off;
                view.style.scale = new Scale(Vector2.one * (w / BoardSide));
                float cw = CellSide(w, cells.Length, out int perRow);
                foreach (var cell in cells) { cell.style.width = cw; cell.style.height = cw; }
            });
        }

        /// <summary>
        /// GG-T1112: a bar cell's side for a column <paramref name="width"/> wide. All <paramref name="slots"/> share one row
        /// while each still gets at least <see cref="Size.IconS"/>; a narrower column (six heroes on a phone: about 30 px a
        /// cell) wraps the bar into balanced rows (3 + 2, then 2 + 2 + 1, ...) so every icon stays legible. The cells are
        /// read-only (nothing here is tappable), so the floor is the icon's, not a tap target's.
        /// </summary>
        internal static float CellSide(float width, int slots, out int perRow)
        {
            slots = Mathf.Max(1, slots);
            int fit = Mathf.Clamp(Mathf.FloorToInt(width / (Size.IconS + CellGap)), 1, slots);
            int rows = Mathf.CeilToInt(slots / (float)fit);
            perRow = Mathf.CeilToInt(slots / (float)rows);
            return Mathf.Max(1f, Mathf.Floor(width / perRow - CellGap)); // floored so rounding never wraps a full row
        }

        private const float CellGap = 2f;

        private static Texture2D EntryIcon(ContentDb c, string entry)
        {
            if (string.IsNullOrEmpty(entry)) return null;
            if (entry.StartsWith("item:")) return Art.ItemIcon(entry.Substring(5));
            if (entry.StartsWith("weapon:")) return Art.ItemIcon(entry.Substring(7));
            return Art.AbilityIcon(c.TryAbility(entry));
        }
    }
}
