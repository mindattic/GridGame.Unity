using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T2049, dev-only (Settings &gt; Developer, <c>-start components</c>): every kit component (docs/DESIGN_SYSTEM.md §3,
    /// §4) in each of its states on one scrolling page, so <c>tools/uiaudit.py</c> captures the kit on every device shape.
    /// States are set by class (<c>is-pressed</c>, <c>tap--selected</c>, <c>menu-row--selected</c>, disabled), and the
    /// overlays are drawn in place, held still, so two captures of the page compare.
    /// </summary>
    public sealed class ComponentGalleryScreen : GameScreen
    {
        public override string Id => "components";

        private static readonly string[] States = { "Default", "Pressed", "Selected", "Disabled" };

        protected override void Build(VisualElement root)
        {
            var col = Ui.El("fill col", root);
            Ui.Header("Components", () => App.Router.Back(), col, out _);
            var page = Ui.ListPanel(Ui.PageBody(col), null, "components-list");
            TypeRoles(page);
            Buttons(page);
            IconButtons(page);
            Icons(page);
            Rows(page);
            Pickers(page);
            Counts(page);
            Cards(page);
            Overlays(page);
            ShapeKinds(page);
        }

        private static void TypeRoles(VisualElement page)
        {
            Ui.Section("Type", page);
            Ui.Display("Display", page);
            Ui.Heading("Heading", page);
            Ui.Name("Name", page);
            Ui.Para("Para: body text that wraps onto a second line when it runs longer than the column is wide.", page);
            Ui.Caption("Caption", page);
            Ui.Note("Note: a dim sentence under a row.", page);
            var tones = Ui.El("row", page);
            foreach (var tone in new[] { "gold", "good", "bad", "warn", "dim" }) Ui.Margin(Ui.Caption(tone, tones, tone), right: Space.S24);
            Ui.Field("Field", "A read-only value", page);
            var scrim = Ui.Para("Scrim", page);
            Ui.Scrim(scrim);
            scrim.style.alignSelf = Align.FlexStart;
        }

        private static void Buttons(VisualElement page)
        {
            Ui.Section("Buttons", page);
            foreach (var classes in new[] { null, "btn--primary", "btn--danger", "btn--small" })
            {
                Ui.Caption(classes ?? "btn", page, "dim");
                var row = Ui.Actions(page);
                for (int i = 0; i < States.Length; i++) SetState(Ui.Button(States[i], null, classes, row, confirmTap: true), i);
            }
        }

        private static void IconButtons(VisualElement page)
        {
            Ui.Section("Icon buttons", page);
            var row = Ui.Actions(page);
            for (int i = 0; i < States.Length; i++) SetState(Ui.IconButton(RowKind.Confirm, null, row), i);
            Ui.Caption("Icon sizes in a button: inline, button, row, nav", page, "dim");
            var sizes = Ui.Actions(page);
            foreach (var size in new[] { Size.IconInline, Size.IconButton, Size.Icon, Size.IconNav }) Ui.IconButton(RowKind.Settings, null, sizes, size);
        }

        private static void Icons(VisualElement page)
        {
            Ui.Section("Icons", page);
            var sizes = Ui.El("row", page);
            foreach (var (label, size) in new[] { ("Inline", Size.IconInline), ("Button", Size.IconButton), ("Row", Size.Icon), ("Nav", Size.IconNav) })
            {
                var cell = Ui.El("col center", sizes);
                Ui.Margin(cell, right: Space.S24);
                var icon = RowIcon.SetIcon(RowIcon.IconId(RowKind.Summon), null);
                icon.style.width = icon.style.height = size;
                cell.Add(icon);
                Ui.Caption($"{label} {size:0}", cell, "dim");
            }
            Ui.Caption("Set icons (Art.UiIcon), one per row kind", page, "dim");
            var grid = Ui.El("row", page);
            grid.style.flexWrap = Wrap.Wrap;
            foreach (var id in Enum.GetValues(typeof(RowKind)).Cast<RowKind>().Select(RowIcon.IconId).Distinct())
            {
                var icon = RowIcon.SetIcon(id);
                icon.name = "icon-" + id;
                Ui.Margin(icon, right: Space.S16, bottom: Space.S16);
                grid.Add(icon);
            }
            Ui.Para("RowIcon.Line: an inline icon before wrapping text.", RowIcon.Line(RowIcon.IconId(RowKind.Quest), page));
            RowIcon.HaveLine(true, "Have: 3 / 3", page);
            RowIcon.HaveLine(false, "Need: 1 / 4", page);
        }

        private static void Rows(VisualElement page)
        {
            Ui.Section("Menu rows", page);
            var plain = Ui.MenuRow("Default", "Value", null, page);
            plain.KindIcon(RowKind.Menu);
            RowIcon.Prefix(plain.Q<Label>("value"), RowIcon.IconId(RowKind.Shop));
            var pressed = Ui.MenuRow("Pressed", "Value", null, page);
            pressed.KindIcon(RowKind.Menu);
            pressed.AddToClassList("is-pressed");
            var selected = Ui.MenuRow("Selected", "Value", null, page);
            selected.KindIcon(RowKind.Menu);
            Ui.Select(selected);
            Ui.MenuRow("Disabled", "Value", null, page, enabled: false).KindIcon(RowKind.Locked);
            var compact = Ui.MenuRow("Compact", "Value", null, page);
            compact.KindIcon(RowKind.Settings);
            compact.AddToClassList("menu-row--compact");
            RowIcon.Prefix(Ui.MenuRow("Prefixed label", "", null, page).Q<Label>("label"), RowIcon.IconId(RowKind.New));
        }

        private static void Pickers(VisualElement page)
        {
            Ui.Section("Tabs", page);
            Ui.Tabs(new[] { "Active", "Tab", "Locked" }, 0, _ => { }, page, i => i == 2);
            Ui.Section("Segmented", page);
            Ui.Segmented(new[] { "One", "Two", "Three" }, 1, null, page);
            Ui.Section("Chips", page);
            var chips = Ui.El("row", page);
            chips.style.flexWrap = Wrap.Wrap;
            Ui.Chip("Plain", chips);
            Ui.Chip("Icon", chips, RowKind.Item);
            Ui.Chip("Tap", chips, RowKind.Item, () => { });
            Ui.Chip("Selected", chips, RowKind.Item, () => { }, true);
            Ui.Chip("Gold", chips, tone: "gold");
            Ui.Section("Stepper", page);
            Ui.Stepper(Ui.El("row", page), "5", null, null, out _, out _);
            Ui.Stepper(Ui.El("row", page), "1", null, null, out var minus, out _, "gold");
            Ui.SetEnabled(minus, false);
        }

        private static void Counts(VisualElement page)
        {
            Ui.Section("Badges", page);
            var badges = Ui.El("row", page);
            Ui.Margin(Ui.Badge("3", badges), right: Space.S24);
            Ui.Margin(Ui.Badge("New", badges), right: Space.S24);
            Ui.Badge("99+", badges, "gold");
            Ui.Section("Progress bars", page);
            Ui.ProgressBar(0f, page);
            Ui.ProgressBar(0.35f, page, "7 / 20");
            Ui.ProgressBar(1f, page, "20 / 20");
            Ui.Section("Gauges", page);
            foreach (var frac in new[] { 0.15f, 0.5f, 0.9f }) Ui.Margin(Ui.Gauge(frac, page), bottom: Space.S16);
            Ui.Section("Party mana bar: empty, part, priced, short", page);
            foreach (var (points, price) in new[] { (0, 0), (60, 0), (60, 40), (30, 50) })
            {
                var bar = new PartyManaBar { Capacity = 100, Points = points, Price = price };
                bar.style.height = Size.Gauge;
                Ui.Margin(bar, bottom: Space.S24);
                page.Add(bar);
            }
        }

        private void Cards(VisualElement page)
        {
            Ui.Section("Cards", page);
            var hero = App.Content.Classes.Values.Where(c => !string.IsNullOrEmpty(c.Role) && Art.HasPortrait(c.Id)).OrderBy(c => c.Id).FirstOrDefault();
            for (int i = 0; i < States.Length - 1; i++)
            {
                var card = Ui.Card(page, hero?.Id, States[i], out var info);
                if (hero != null) Ui.RoleBadge(hero.Role, info);
                Ui.Caption(hero?.Name ?? "No hero art", info, "dim");
                Ui.Tappable(card, null, confirmTap: true);
                if (i == 1) card.AddToClassList("is-pressed");
                if (i == 2) card.AddToClassList("tap--selected");
            }
            Ui.Section("Empty state", page);
            var empty = Ui.Panel(null);
            Ui.Actions(Ui.EmptyState(empty, RowKind.Item, "Nothing here yet.")).Add(Ui.Button("Next step", null, "btn--small"));
            page.Add(empty);
        }

        private static void Overlays(VisualElement page)
        {
            Ui.Section("Modal", page);
            var modal = Ui.Panel(Ui.El("fill veil col center", Frame(page, Size.PortraitH)));
            modal.style.width = Length.Percent(Ui.ModalWidthPct);
            Ui.Heading("A question for the player?", modal).style.whiteSpace = WhiteSpace.Normal;
            var answers = Ui.ModalActions(modal);
            Ui.Button("Cancel", null, null, answers);
            Ui.Button("Yes", null, "btn--primary", answers);

            Ui.Section("Sheet", page);
            var veil = Ui.El("fill veil", Frame(page, Size.ArtH));
            var sheet = Ui.Window(null, veil, "window--compact kit-sheet");
            sheet.style.position = Position.Absolute;
            sheet.style.left = sheet.style.right = sheet.style.bottom = Space.S16;
            sheet.style.height = Length.Percent(Ui.SheetHeightPct);
            Ui.Heading("Sheet", sheet);
            Ui.MenuRow("Row", "Value", null, sheet).KindIcon(RowKind.Ability);

            Ui.Section("Toast", page);
            var toast = Ui.Text("Saved", "toast shadow", Frame(page, Size.PortraitH / 2f));
            toast.style.bottom = Space.S24;
        }

        /// <summary>A stand-in for the stage, <paramref name="height"/> tall, that an overlay sample sits in.</summary>
        private static VisualElement Frame(VisualElement page, float height)
        {
            var f = Ui.El(null, page);
            f.style.height = height;
            f.style.flexShrink = 0f;
            f.style.overflow = Overflow.Hidden;
            Ui.Margin(f, bottom: Space.S24);
            return f;
        }

        private static readonly (string name, Action<Painter2D, Rect> draw)[] ShapeSamples =
        {
            ("Arrow", (p, r) => Shapes.Arrow(p, At(r, 0.15f, 0.8f), At(r, 0.85f, 0.2f), ShapeStyle.Indicator)),
            ("BezierArrow", (p, r) => Shapes.BezierArrow(p, At(r, 0.15f, 0.8f), At(r, 0.85f, 0.2f), ShapeStyle.Tutorial)),
            ("OrthoArrow", (p, r) => Shapes.OrthoArrow(p, At(r, 0.15f, 0.8f), At(r, 0.85f, 0.2f), ShapeStyle.Indicator.Tint(Palette.Gold))),
            ("OrthoPathArrow", (p, r) => Shapes.OrthoPathArrow(p, new List<Vector2> { At(r, 0.15f, 0.8f), At(r, 0.5f, 0.8f), At(r, 0.5f, 0.2f), At(r, 0.85f, 0.2f) }, ShapeStyle.Indicator.Tint(Palette.Hero))),
            ("DashedArrow", (p, r) => Shapes.DashedArrow(p, At(r, 0.15f, 0.5f), At(r, 0.85f, 0.5f), ShapeStyle.Tutorial)),
            ("Chevron", (p, r) => Shapes.Chevron(p, At(r, 0.5f, 0.5f), Vector2.right, r.height * 0.5f, ShapeStyle.Small.Thick(Size.Bar))),
            ("Bar", (p, r) => Shapes.Bar(p, At(r, 0.15f, 0.5f), At(r, 0.85f, 0.5f), ShapeStyle.Indicator.Tint(Palette.Pewter))),
            ("Dot", (p, r) => Shapes.Dot(p, At(r, 0.5f, 0.5f), r.height * 0.25f, ShapeStyle.Indicator.Tint(Palette.Mana))),
            ("Ring", (p, r) => Shapes.Ring(p, At(r, 0.5f, 0.5f), r.height * 0.3f, ShapeStyle.Indicator.Tint(Palette.Enemy))),
            ("Pie", (p, r) => Shapes.Pie(p, At(r, 0.5f, 0.5f), r.height * 0.3f, ShapeStyle.Indicator.Tint(Palette.OrbLit), -90f, 250f)),
            ("RoundedRect", (p, r) => Shapes.RoundedRect(p, Inset(r), Radius.R12, ShapeStyle.Indicator)),
            ("Filled rect", (p, r) => Shapes.RoundedRect(p, Inset(r), Radius.R12, ShapeStyle.Indicator.Tint(Palette.Good), filled: true)),
            ("Dashed rect", (p, r) => Shapes.RoundedRect(p, Inset(r), Radius.R12, ShapeStyle.Tutorial, dashed: true)),
            ("Progress frame", (p, r) => Shapes.RoundedRect(p, Inset(r), Radius.R12, ShapeStyle.Indicator.Tint(Palette.Gold), portion: 0.6f)),
        };

        private static void ShapeKinds(VisualElement page)
        {
            Ui.Section("Shapes", page);
            var grid = Ui.El("row", page);
            grid.style.flexWrap = Wrap.Wrap;
            foreach (var (name, draw) in ShapeSamples)
            {
                var cell = Ui.El("col", grid);
                cell.style.width = Length.Percent(50f);
                var canvas = Ui.El(null, cell);
                canvas.name = "shape-" + name.ToLowerInvariant().Replace(' ', '-');
                canvas.style.height = Size.Thumb;
                canvas.generateVisualContent += ctx => draw(ctx.painter2D, canvas.contentRect);
                Ui.Caption(name, cell, "dim");
                Ui.Margin(cell, bottom: Space.S16);
            }
        }

        private static Vector2 At(Rect r, float x, float y) => new Vector2(r.xMin + r.width * x, r.yMin + r.height * y);

        private static Rect Inset(Rect r) => new Rect(r.xMin + Space.S24, r.yMin + Space.S16, r.width - Space.S24 * 2f, r.height - Space.S16 * 2f);

        /// <summary>Shows a button in state <paramref name="i"/> of <see cref="States"/>.</summary>
        private static void SetState(Button b, int i)
        {
            if (i == 1) b.AddToClassList("is-pressed");
            if (i == 2) b.AddToClassList("tap--selected");
            if (i == 3) Ui.SetEnabled(b, false);
        }
    }
}
