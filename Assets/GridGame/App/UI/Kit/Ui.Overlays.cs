using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>The kit's overlay surfaces (docs/DESIGN_SYSTEM.md §4, GG-T2048): the modal window, the bottom and full-screen
    /// sheet, and the toast. Each sits on <see cref="GameApp.Overlay"/>; a modal or sheet stands on a <c>.veil</c> named
    /// <see cref="ModalVeil"/>, so Android Back answers the topmost one (<see cref="Confirm.CancelTop"/>) and a navigation
    /// clears it (<see cref="Confirm.DismissStale"/>).</summary>
    public static partial class Ui
    {
        /// <summary>The name every modal and sheet veil carries.</summary>
        public const string ModalVeil = "confirm-veil";

        /// <summary>A modal window's share of the stage width.</summary>
        public const float ModalWidthPct = 76f;

        /// <summary>A bottom sheet's share of the usable stage height (the Abilities sheet's, GG-T1914).</summary>
        public const float SheetHeightPct = 62f;

        /// <summary>A centred modal window over the veil: <paramref name="title"/> as a wrapping heading when given, then the
        /// caller's content; put its answers in <see cref="ModalActions"/>. <paramref name="onCancel"/> is what Android Back
        /// answers (by default it just closes). It fades in unless Reduce Motion is on or <paramref name="animate"/> is false.
        /// Returns the window; its parent is the veil, which <see cref="CloseModal"/> takes away.</summary>
        public static VisualElement Modal(string title = null, Action onCancel = null, string name = null, bool animate = true)
        {
            var app = GameApp.I;
            var veil = El("fill veil col center", app.Overlay);
            veil.name = ModalVeil;
            veil.pickingMode = PickingMode.Position;
            var w = Panel(veil, null, false, name);
            w.style.width = Length.Percent(ModalWidthPct);
            if (title != null) Heading(title, w).style.whiteSpace = WhiteSpace.Normal;
            veil.userData = onCancel ?? (Action)(() => veil.RemoveFromHierarchy());
            if (animate && !Tween.Calm) { veil.style.opacity = 0f; app.StartCoroutine(Tween.Fade(veil, 0f, 1f, Timing.Quick)); }
            return w;
        }

        /// <summary>A modal's answer row, right-aligned (Cancel, then the answer).</summary>
        public static VisualElement ModalActions(VisualElement modal)
        {
            var row = Actions(modal);
            row.style.justifyContent = Justify.FlexEnd;
            return row;
        }

        /// <summary>Takes away the modal holding <paramref name="modal"/> (its veil), or the veil itself.</summary>
        public static void CloseModal(VisualElement modal)
        {
            var veil = modal;
            while (veil != null && veil.name != ModalVeil) veil = veil.parent;
            (veil ?? modal)?.RemoveFromHierarchy();
        }

        /// <summary>The kit's sheet (§4), shaped like the Abilities sheet (GG-T1914): a compact window, full column width,
        /// rising from the thumb zone to <paramref name="heightPct"/> of the usable stage height; <paramref name="full"/>
        /// stretches it up to the safe top. A tap on the veil, Android Back or <see cref="CloseSheet"/> drops it away and
        /// runs <paramref name="onClose"/>. It rises over <see cref="Timing.Standard"/> and falls over <see cref="Timing.Quick"/>,
        /// held still under Reduce Motion. Returns the sheet window, named <paramref name="name"/>.</summary>
        public static VisualElement Sheet(string name, Action onClose = null, bool full = false, float heightPct = SheetHeightPct)
        {
            var app = GameApp.I;
            var veil = El("fill veil", app.Overlay);
            veil.name = ModalVeil;
            veil.pickingMode = PickingMode.Position;
            var sheet = Window(null, veil, "window--compact kit-sheet" + (full ? " kit-sheet--full" : ""));
            sheet.name = name;
            sheet.style.position = Position.Absolute;
            sheet.style.left = sheet.style.right = Fit.Inset + Fit.Edge;
            sheet.style.bottom = GameApp.SafeBottom + Space.S16;
            if (full) sheet.style.top = GameApp.SafeTop + Space.S16;
            else sheet.style.height = Fit.H(heightPct);
            sheet.userData = onClose;
            veil.userData = (Action)(() => CloseSheet(sheet));
            veil.RegisterCallback<ClickEvent>(e => { if (e.target == veil) CloseSheet(sheet); });
            app.Audio?.Sfx("ui.select");
            if (!Tween.Calm)
            {
                float h = (full ? Fit.Height : Fit.H(heightPct)) + GameApp.SafeBottom + Space.S16;
                sheet.style.translate = new Translate(0, h);
                veil.style.opacity = 0f;
                app.StartCoroutine(Tween.Run(Timing.Standard, k =>
                {
                    sheet.style.translate = new Translate(0, h * (1f - k));
                    veil.style.opacity = k;
                }, Ease.Out));
            }
            return sheet;
        }

        /// <summary>Drops <paramref name="sheet"/> away and runs its close action; at once when Reduce Motion is on or
        /// <paramref name="animate"/> is false. Nothing when it is already gone.</summary>
        public static void CloseSheet(VisualElement sheet, bool animate = true)
        {
            var veil = sheet?.parent;
            if (veil == null || veil.parent == null) return;
            var onClose = sheet.userData as Action;
            sheet.userData = null;
            veil.name = null; // no longer a Back target while it falls
            if (!animate || Tween.Calm) { veil.RemoveFromHierarchy(); onClose?.Invoke(); return; }
            veil.pickingMode = PickingMode.Ignore;
            float h = sheet.layout.height + GameApp.SafeBottom + Space.S16;
            GameApp.I.StartCoroutine(Tween.Run(Timing.Quick, k =>
            {
                sheet.style.translate = new Translate(0, h * k);
                veil.style.opacity = 1f - k;
                if (k >= 1f) veil.RemoveFromHierarchy();
            }));
            onClose?.Invoke();
        }

        private static int liveToasts;

        /// <summary>The kit's toast (§4): a small pane named <c>toast</c>, for <paramref name="seconds"/>, in the band just under
        /// the header (below the safe top), out of the thumb zone so it never covers a page's actions (GG-T2156); it lets
        /// taps through. A second one stacks under the first, never over it (GG-T1867).</summary>
        public static void Toast(string text, float seconds = 1.8f) => GameApp.I.StartCoroutine(ToastRoutine(text, seconds));

        private static IEnumerator ToastRoutine(string text, float seconds)
        {
            var t = Text(text, "toast shadow", GameApp.I.Overlay);
            t.name = "toast";
            t.pickingMode = PickingMode.Ignore;
            t.style.top = GameApp.SafeTop + Size.ToastTop + liveToasts * Size.ToastStack;
            liveToasts++;
            t.style.opacity = 0;
            yield return null;
            t.style.opacity = 1;
            yield return new WaitForSecondsRealtime(seconds);
            t.style.opacity = 0;
            yield return new WaitForSecondsRealtime(Timing.Standard);
            t.RemoveFromHierarchy();
            liveToasts--;
        }
    }
}
