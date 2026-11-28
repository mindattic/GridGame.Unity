#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App.Dev
{
    /// <summary>
    /// GG-T2119: the emulated phone's camera cutout (Dynamic Island, notch or punch-hole) and rounded screen corners, drawn
    /// in black over every element so the window and its screenshots show what the hardware hides. Only the automation
    /// <c>deviceframe</c> endpoint turns it on (tools/scenario.py and tools/uiaudit.py send each device's geometry from
    /// tools/devices.json); a normal build never shows it. Geometry is in the device's own px, scaled to the window.
    /// </summary>
    public sealed class DeviceFrame : VisualElement
    {
        private static DeviceFrame current;

        private string type;
        private float cutW, cutH, cutTop, cutRadius, screenRadius, refW, refH;

        private DeviceFrame()
        {
            name = "device-frame";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            generateVisualContent += Draw;
        }

        /// <summary>Shows the frame of a <paramref name="refW"/>×<paramref name="refH"/> px device: a cutout of
        /// <paramref name="cutoutType"/> (island, notch, punch or none) sized <paramref name="w"/>×<paramref name="h"/>,
        /// <paramref name="top"/> below the top edge with corner radius <paramref name="radius"/>, and screen corners of
        /// <paramref name="corner"/>.</summary>
        public static void Show(string cutoutType, float w, float h, float top, float radius, float corner, float refW, float refH)
        {
            var app = GameApp.I;
            if (current == null)
            {
                current = new DeviceFrame();
                app.Ticked += KeepOnTop;
            }
            current.type = cutoutType;
            current.cutW = w; current.cutH = h; current.cutTop = top; current.cutRadius = radius;
            current.screenRadius = corner; current.refW = Mathf.Max(1f, refW); current.refH = Mathf.Max(1f, refH);
            var root = app.Document.rootVisualElement;
            if (current.parent != root) root.Add(current);
            current.BringToFront();
            current.MarkDirtyRepaint();
        }

        public static void Hide()
        {
            if (current == null) return;
            GameApp.I.Ticked -= KeepOnTop;
            current.RemoveFromHierarchy();
            current = null;
        }

        /// <summary>What the frame draws, for the endpoint's report (null when off).</summary>
        public static object Report() => current == null ? null : new
        {
            current.type, w = current.cutW, h = current.cutH, top = current.cutTop, radius = current.cutRadius,
            corner = current.screenRadius, @ref = new[] { current.refW, current.refH },
        };

        private static void KeepOnTop(float _)
        {
            var p = current?.parent;
            if (p != null && p.IndexOf(current) != p.childCount - 1) current.BringToFront();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var p = ctx.painter2D;
            p.fillColor = Color.black;
            float sx = r.width / refW, sy = r.height / refH;

            float c = screenRadius * sx;
            if (c > 0f)
            {
                Corner(p, new Vector2(0f, 0f), new Vector2(c, 0f), new Vector2(0f, c), c);
                Corner(p, new Vector2(r.width, 0f), new Vector2(r.width, c), new Vector2(r.width - c, 0f), c);
                Corner(p, new Vector2(r.width, r.height), new Vector2(r.width - c, r.height), new Vector2(r.width, r.height - c), c);
                Corner(p, new Vector2(0f, r.height), new Vector2(0f, r.height - c), new Vector2(c, r.height), c);
            }

            float w = cutW * sx, h = cutH * sx, top = cutTop * sy, rad = Mathf.Min(cutRadius * sx, w / 2f, h / 2f);
            if (w <= 0f || h <= 0f) return;
            float x0 = (r.width - w) / 2f, x1 = x0 + w, y0 = top, y1 = top + h;
            switch (type)
            {
                case "island":
                    RoundedRect(p, x0, y0, x1, y1, rad, rad);
                    break;
                case "notch":
                    RoundedRect(p, x0, 0f, x1, y1, 0f, Mathf.Min(cutRadius * sx, w / 2f, y1));
                    break;
                case "punch":
                    p.BeginPath();
                    p.Arc(new Vector2(r.width / 2f, y0 + w / 2f), w / 2f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                    break;
            }
        }

        /// <summary>Fills the square corner at <paramref name="tip"/> outside an arc of radius <paramref name="rad"/> that
        /// runs from <paramref name="a"/> to <paramref name="b"/>.</summary>
        private static void Corner(Painter2D p, Vector2 tip, Vector2 a, Vector2 b, float rad)
        {
            p.BeginPath();
            p.MoveTo(tip);
            p.LineTo(a);
            p.ArcTo(tip, b, rad);
            p.ClosePath();
            p.Fill();
        }

        private static void RoundedRect(Painter2D p, float x0, float y0, float x1, float y1, float topRad, float bottomRad)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x0 + topRad, y0));
            p.LineTo(new Vector2(x1 - topRad, y0));
            if (topRad > 0f) p.ArcTo(new Vector2(x1, y0), new Vector2(x1, y0 + topRad), topRad);
            p.LineTo(new Vector2(x1, y1 - bottomRad));
            if (bottomRad > 0f) p.ArcTo(new Vector2(x1, y1), new Vector2(x1 - bottomRad, y1), bottomRad);
            p.LineTo(new Vector2(x0 + bottomRad, y1));
            if (bottomRad > 0f) p.ArcTo(new Vector2(x0, y1), new Vector2(x0, y1 - bottomRad), bottomRad);
            p.LineTo(new Vector2(x0, y0 + topRad));
            if (topRad > 0f) p.ArcTo(new Vector2(x0, y0), new Vector2(x0 + topRad, y0), topRad);
            p.ClosePath();
            p.Fill();
        }
    }
}
#endif
