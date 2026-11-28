using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Modal confirm dialog on the overlay layer.</summary>
    public static class Confirm
    {
        private const string VeilName = "confirm-veil";

        /// <summary>Returns the veil, so a caller can tell whether its question is still up.</summary>
        public static VisualElement Show(string question, string yes, Action onYes, Action onNo = null)
        {
            var overlay = GameApp.I.Overlay;
            var veil = Ui.El("fill veil col center", overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Panel(veil);
            w.style.width = Length.Percent(76);
            var q = Ui.Heading(question, w);
            q.style.whiteSpace = WhiteSpace.Normal;
            var row = Ui.Actions(w);
            row.style.justifyContent = Justify.FlexEnd;
            Action cancel = () => { veil.RemoveFromHierarchy(); onNo?.Invoke(); };
            veil.userData = cancel; // Android Back answers Cancel (CancelTop)
            // GG-T738 (§26.7): Cancel leads with the Close cross, the answer with the Confirm check.
            RowIcon.IconButton("Cancel", RowKind.Close, cancel, null, row).name = "confirm-no";
            RowIcon.IconButton(yes, RowKind.Confirm, () => { veil.RemoveFromHierarchy(); onYes?.Invoke(); }, "btn--primary", row, confirmTap: false).name = "confirm-yes";
            GameApp.I.Audio.Sfx("ui.select");
            if (!Tween.Calm) { veil.style.opacity = 0f; GameApp.I.StartCoroutine(Tween.Fade(veil, 0f, 1f, Timing.Quick)); }
            return veil;
        }

        /// <summary>GG-T1645: a question with two answers and no Cancel. <paramref name="secondIsDefault"/> picks which one is
        /// primary and which Android Back answers. It sounds as it opens and fades in, held still under Reduce Motion.</summary>
        public static VisualElement Choose(string question, string note, string first, RowKind firstKind, Action onFirst,
            string second, RowKind secondKind, Action onSecond, bool secondIsDefault, string name = null)
        {
            var app = GameApp.I;
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Panel(veil, null, false, name);
            w.style.width = Length.Percent(76);
            Ui.Heading(question, w).style.whiteSpace = WhiteSpace.Normal;
            if (!string.IsNullOrEmpty(note)) Ui.Note(note, w);
            var row = Ui.Actions(w);
            row.style.justifyContent = Justify.FlexEnd;
            Action a = () => { veil.RemoveFromHierarchy(); onFirst?.Invoke(); };
            Action b = () => { veil.RemoveFromHierarchy(); onSecond?.Invoke(); };
            veil.userData = secondIsDefault ? b : a;
            RowIcon.IconButton(first, firstKind, a, secondIsDefault ? null : "btn--primary", row).name = "choose-first";
            RowIcon.IconButton(second, secondKind, b, secondIsDefault ? "btn--primary" : null, row).name = "choose-second";
            app.Audio.Sfx("ui.select");
            if (!Tween.Calm) { veil.style.opacity = 0f; app.StartCoroutine(Tween.Fade(veil, 0f, 1f, Timing.Quick)); }
            return veil;
        }

        /// <summary>GG-T1401: a card with one answer, which Android Back also gives. It chimes as it opens, fades in and pops
        /// its title, held still under Reduce Motion.</summary>
        public static VisualElement Notice(string kicker, string title, string text, string ok, RowKind okKind, Action onOk, string name)
        {
            var app = GameApp.I;
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Panel(veil, null, false, name);
            w.style.width = Length.Percent(76);
            Ui.Caption(kicker, w, "gold");
            var head = Ui.Heading(title, w);
            head.style.whiteSpace = WhiteSpace.Normal;
            Ui.Text(text, "kit-text", w).style.whiteSpace = WhiteSpace.Normal;
            var row = Ui.Actions(w);
            row.style.justifyContent = Justify.FlexEnd;
            Action a = () => { veil.RemoveFromHierarchy(); onOk?.Invoke(); };
            veil.userData = a;
            RowIcon.IconButton(ok, okKind, a, "btn--primary", row).name = "notice-ok";
            app.Audio.Sfx("battle.powerup");
            if (!Tween.Calm)
            {
                veil.style.opacity = 0f;
                app.StartCoroutine(Tween.Fade(veil, 0f, 1f, Timing.Quick));
                app.StartCoroutine(Tween.Pop(head, 1.15f, Timing.Emphasis));
            }
            return veil;
        }

        /// <summary>Android Back with a question up: answers the topmost one Cancel. False when none is open.</summary>
        public static bool CancelTop()
        {
            var overlay = GameApp.I?.Overlay;
            if (overlay == null) return false;
            var veils = overlay.Query(VeilName).ToList();
            if (veils.Count == 0) return false;
            var top = veils[veils.Count - 1];
            if (top.userData is Action cancel) cancel(); else top.RemoveFromHierarchy();
            return true;
        }

        /// <summary>A confirm veil answers a question about the screen it was raised on — if that screen
        /// changes from under it (any navigation, or an automation call that swaps screens directly) with
        /// the veil never explicitly answered, it would otherwise stay in Overlay forever: a full-screen
        /// PickingMode.Position layer silently eating every future click with no error anywhere (the real
        /// root cause behind GG-T16 — not an Input System/UI Toolkit staleness bug as first suspected).
        /// ScreenRouter calls this on every navigation so a stale veil can never outlive its screen.</summary>
        public static void DismissStale()
        {
            var overlay = GameApp.I?.Overlay;
            if (overlay == null) return;
            foreach (var veil in overlay.Query(VeilName).ToList()) veil.RemoveFromHierarchy();
        }
    }
}
