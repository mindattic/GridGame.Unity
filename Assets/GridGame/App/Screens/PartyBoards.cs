using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Party builds: each hero's battle bar and a tap-through to its board (GG-T1044, GG-T1891)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A Party tab card's build row: the hero's 5-slot battle bar (<see cref="HeroSave.Bar"/>) as icons at a legible size,
    /// then a <b>Board</b> button that opens that hero's skill hexagon (Heroes › Abilities › Board), where every skill is
    /// explained in full. GG-T1891: this replaced a strip of miniature boards whose labels and icons were far under the
    /// type and icon floors. The bar cells are read-only; the card around the row stays tap-to-swap.
    /// </summary>
    internal static class PartyBoards
    {
        public static void Row(ContentDb c, SaveData s, HeroSave h, VisualElement parent)
        {
            var row = Ui.El("row", parent);
            row.name = "party-bar-" + h.ClassId;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;
            Ui.Margin(row, top: Space.S8);
            int unlocked = Progression.UnlockedSlots(s);
            for (int i = 0; i < Progression.MaxBarSlots; i++)
            {
                var cell = Ui.El(null, row);
                cell.pickingMode = PickingMode.Ignore;
                cell.style.flexShrink = 0;
                cell.style.width = cell.style.height = Size.IconM;
                Ui.Margin(cell, right: Space.S8, bottom: Space.S8);
                cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 4;
                cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 1;
                var bc = Palette.Text.WithA(0.4f);
                cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = bc;
                cell.style.backgroundColor = Palette.Ink.WithA(0.9f);
                if (i >= unlocked) { cell.style.opacity = 0.3f; continue; }
                var icon = EntryIcon(c, h.Bar != null && i < h.Bar.Count ? h.Bar[i] : null);
                if (icon != null) cell.style.backgroundImage = new StyleBackground(icon);
            }
            string id = h.ClassId;
            var board = RowIcon.IconButton("Talents", RowKind.Ability,
                () => GameApp.I.Router.Push(new HeroScreen(HeroScreen.AbilitiesTabIndex, id, AbilitiesTab.TalentsSub)), "btn--small", row);
            board.name = "party-board-" + id;
            Ui.Margin(board, bottom: Space.S8);
            board.RegisterCallback<ClickEvent>(e => e.StopPropagation()); // the card's own tap swaps the hero
        }

        private static Texture2D EntryIcon(ContentDb c, string entry)
        {
            if (string.IsNullOrEmpty(entry)) return null;
            if (entry.StartsWith("item:")) return Art.ItemIcon(entry.Substring(5));
            if (entry.StartsWith("weapon:")) return Art.ItemIcon(entry.Substring(7));
            return Art.AbilityIcon(c.TryAbility(entry));
        }
    }
}
