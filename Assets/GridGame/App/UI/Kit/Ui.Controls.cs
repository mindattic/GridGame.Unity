using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>The kit's small controls (docs/DESIGN_SYSTEM.md §3, GG-T2048): icon button, chip, badge, segmented control and
    /// progress bar. Each takes its sizes from <see cref="Size"/> and its look from Theme.uss's <c>.kit-*</c> rules; a
    /// tappable one sinks on press (<see cref="Pressable{T}"/>) and shows its selection pressed in (R3).</summary>
    public static partial class Ui
    {
        /// <summary>An icon-only button (§3): a full 132 target holding <paramref name="kind"/>'s icon at
        /// <paramref name="icon"/> (an ICONS.md role: <see cref="Size.IconInline"/>, <see cref="Size.IconButton"/>,
        /// <see cref="Size.Icon"/>, <see cref="Size.IconNav"/>). A <see cref="RowKind.Confirm"/> one is select-then-confirm
        /// unless <paramref name="confirmTap"/> says otherwise.</summary>
        public static Button IconButton(RowKind kind, Action onClick, VisualElement parent, float icon = Size.IconButton,
            string classes = null, bool? confirmTap = null, Texture2D tex = null)
        {
            var b = RowIcon.IconButton("", kind, onClick, "btn--icon " + (classes ?? ""), parent, confirmTap, tex);
            var i = b.Q(className: RowIcon.Class);
            i.style.width = i.style.height = icon;
            return b;
        }

        /// <summary>A chip (§3): a pill holding an optional inline icon (<see cref="Size.IconInline"/>) and a short label. With
        /// <paramref name="onClick"/> it is a full tap target that sinks on press; <paramref name="selected"/> shows it pressed
        /// in (<see cref="SetSelected"/>). <paramref name="tone"/> colours the label (gold, dim, good, bad).</summary>
        public static VisualElement Chip(string text, VisualElement parent, RowKind? icon = null, Action onClick = null,
            bool selected = false, string tone = null, string name = null)
        {
            var chip = El("kit-chip", parent);
            if (name != null) chip.name = name;
            if (icon.HasValue)
            {
                var i = chip.KindIcon(icon.Value);
                i.style.width = i.style.height = Size.IconInline;
                i.style.marginRight = Space.S8;
            }
            Text(text, "kit-chip__label " + (tone ?? ""), chip).pickingMode = PickingMode.Ignore;
            if (onClick != null)
            {
                chip.AddToClassList("kit-chip--tap");
                Tappable(chip, onClick);
            }
            SetSelected(chip, selected);
            return chip;
        }

        /// <summary>A badge (§3): a small count or word on a pill that takes no taps (a nav tab's count, "New").
        /// <paramref name="classes"/> adds placement (<c>kit-navtab-badge</c>) or a tone.</summary>
        public static Label Badge(string text, VisualElement parent, string classes = null)
        {
            var b = Text(text, "kit-badge " + (classes ?? ""), parent);
            b.pickingMode = PickingMode.Ignore;
            return b;
        }

        /// <summary>Shows a chip or segment selected (pressed in, R3) or not.</summary>
        public static void SetSelected(VisualElement e, bool selected)
        {
            e.EnableInClassList("kit-chip--selected", selected && e.ClassListContains("kit-chip"));
            e.EnableInClassList("kit-segment--selected", selected && e.ClassListContains("kit-segment"));
        }

        /// <summary>A segmented control (§3): one row of equal buttons sharing the width, exactly one selected and shown
        /// pressed in. A tap on another moves the selection and runs <paramref name="onPick"/>; a tap on the selected one
        /// does nothing. Each segment is named <c>seg-</c> plus its label (lower case, dashes). Returns the row.</summary>
        public static VisualElement Segmented(string[] options, int selected, Action<int> onPick, VisualElement parent, string name = null)
        {
            var row = El("kit-segmented", parent);
            if (name != null) row.name = name;
            var segs = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var b = segs[i] = Button(options[i], null, "btn--small kit-segment", row);
                b.name = "seg-" + options[i].ToLowerInvariant().Replace(' ', '-');
                b.clicked += () =>
                {
                    if (b.ClassListContains("kit-segment--selected")) return;
                    foreach (var s in segs) SetSelected(s, s == b);
                    onPick?.Invoke(idx);
                };
                SetSelected(b, i == selected);
            }
            return row;
        }

        /// <summary>A progress bar (§3): the kit's gauge (<see cref="Gauge"/>) across the width, with an optional
        /// <paramref name="label"/> ("3 / 10") at its right. <see cref="SetProgress"/> moves it. Returns the bar.</summary>
        public static VisualElement ProgressBar(float frac, VisualElement parent, string label = null, string name = null)
        {
            var bar = El("kit-progress", parent);
            if (name != null) bar.name = name;
            Gauge(frac, bar).AddToClassList("kit-progress__gauge");
            var l = Caption(label ?? "", bar);
            l.AddToClassList("kit-progress__label");
            l.name = "progress-label";
            l.style.display = string.IsNullOrEmpty(label) ? DisplayStyle.None : DisplayStyle.Flex;
            return bar;
        }

        /// <summary>Moves a <see cref="ProgressBar"/> to <paramref name="frac"/>, and its label when one is given.</summary>
        public static void SetProgress(VisualElement bar, float frac, string label = null)
        {
            SetGauge(bar.Q(className: "gauge"), frac);
            if (label == null) return;
            var l = bar.Q<Label>("progress-label");
            l.text = label;
            l.style.display = label.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
