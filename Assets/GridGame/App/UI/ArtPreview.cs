using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1804: the full-art preview. Heroes are drawn as their thumbnail everywhere outside the story and reveals; tapping
    /// one calls <see cref="Open"/> to see the whole figure (the look the player picked, <see cref="Art.PortraitFigure(string)"/>)
    /// in a veil over the page: wheel or pinch zooms, a drag pans (clamped to the art), a double tap zooms in or back out,
    /// the +/- buttons do the same for anyone without a wheel or two fingers, and Close or Back dismisses it.
    /// </summary>
    public static class ArtPreview
    {
        public const string VeilName = "art-preview";
        private const float MaxZoom = 4f;
        private const float ButtonStep = 1.4f;
        private const float DoubleTapZoom = 2.5f;

        public static bool IsOpen => GameApp.I?.Overlay?.Q(VeilName) != null;

        /// <summary>Opens the preview for <paramref name="classId"/>; no art or one already open does nothing.</summary>
        public static void Open(string classId)
        {
            var app = GameApp.I;
            if (app?.Overlay == null || IsOpen) return;
            var fig = Art.PortraitFigure(classId);
            if (fig == null) return;
            app.Audio?.Sfx("ui.click");

            var veil = Ui.El("fill veil", app.Overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            veil.style.backgroundColor = Palette.Ink.WithA(0.94f);

            var view = new PanZoomView { MinScale = 1f, MaxScale = MaxZoom };
            view.style.position = Position.Absolute;
            view.style.left = view.style.top = view.style.right = view.style.bottom = Space.S0;
            veil.Add(view);

            var art = Ui.El(null, view);
            art.name = "art-preview-image";
            art.pickingMode = PickingMode.Ignore;
            art.style.backgroundImage = new StyleBackground(fig);
            art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            float aspect = fig.rect.width / Mathf.Max(1f, fig.rect.height);
            view.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var v = view.layout.size;
                if (float.IsNaN(v.x) || v.x <= 0f || v.y <= 0f) return;
                float w = Mathf.Min(v.x, v.y * aspect);
                art.style.width = w;
                art.style.height = w / aspect;
            });

            view.RegisterCallback<ClickEvent>(e =>
            {
                if (e.clickCount < 2) return;
                view.ZoomTo(view.Scale > 1.05f ? 1f : DoubleTapZoom);
            });

            var bar = Ui.El("row", veil);
            bar.style.position = Position.Absolute;
            bar.style.right = Space.S16; bar.style.top = Space.S16;
            Ui.Button("-", () => view.ZoomTo(view.Scale / ButtonStep), "btn--small", bar).name = "art-zoom-out";
            Ui.Button("+", () => view.ZoomTo(view.Scale * ButtonStep), "btn--small", bar).name = "art-zoom-in";
            RowIcon.IconButton("Close", RowKind.Close, () => Close(), "btn--small", bar).name = "art-close";
        }

        /// <summary>Closes the preview if it is up (Back and Escape); true when it was.</summary>
        public static bool Close()
        {
            var veil = GameApp.I?.Overlay?.Q(VeilName);
            if (veil == null) return false;
            GameApp.I.Audio?.Sfx("ui.cancel");
            veil.RemoveFromHierarchy();
            return true;
        }
    }
}
