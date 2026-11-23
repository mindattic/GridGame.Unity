using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T706: the blue mystic fog that fills the out-of-bounds area around a letterboxed stage (bible §26.3). A screen
    /// shape outside the supported phone range (tablets, foldables opened, desktop windows) gets the nearest valid stage,
    /// centred, and this fills the rest instead of black: a deep night-blue ground under three soft fog banks tinted from
    /// the Manalith blue (#7590C8, §11.5) that drift slowly against each other, with a faint slow breathing glow. The fog
    /// is one procedural noise texture made once at start (no asset), stretched large so it reads as soft vapour.
    /// Reduce Motion: the banks hold still in their rest pose. Hidden (and not ticking) whenever the stage fills the screen.
    /// </summary>
    public sealed class MysticFog : VisualElement
    {
        private const int TexSize = 128;
        private static readonly Color Ground = new Color(0.035f, 0.05f, 0.11f);

        /// <summary>One fog bank: its tint, how far (percent of its own size) it drifts, and its drift periods in seconds.</summary>
        private readonly struct Bank
        {
            public readonly Color Tint; public readonly float Reach, PeriodX, PeriodY, Phase;
            public Bank(Color tint, float reach, float periodX, float periodY, float phase)
            { Tint = tint; Reach = reach; PeriodX = periodX; PeriodY = periodY; Phase = phase; }
        }

        private static readonly Bank[] Banks =
        {
            new Bank(new Color(0.16f, 0.22f, 0.47f, 0.85f), 3f, 47f, 61f, 0.0f),  // deep indigo body
            new Bank(new Color(0.46f, 0.56f, 0.78f, 0.42f), 4.5f, 37f, 43f, 1.7f),  // Manalith blue #7590C8
            new Bank(new Color(0.66f, 0.77f, 0.94f, 0.16f), 6f, 29f, 33f, 3.9f), // pale mana glint
        };

        private static Texture2D noise;
        private readonly VisualElement[] layers = new VisualElement[Banks.Length];
        private readonly VisualElement glow;
        private bool active;

        public MysticFog()
        {
            name = "mystic-fog";
            pickingMode = PickingMode.Ignore;
            AddToClassList("fill");
            style.overflow = Overflow.Hidden;
            style.backgroundColor = Ground;
            var tex = Noise();
            for (int i = 0; i < Banks.Length; i++)
            {
                var l = new VisualElement { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute;
                // 200% of the area, centred: drifting by up to Reach% of itself never shows an edge.
                l.style.left = l.style.top = Length.Percent(-50f);
                l.style.width = l.style.height = Length.Percent(200f);
                l.style.backgroundImage = tex;
                l.style.unityBackgroundImageTintColor = Banks[i].Tint;
                // Mirror alternate banks so the three never line up into one pattern.
                if (i == 1) l.style.scale = new Scale(new Vector3(-1f, 1f, 1f));
                if (i == 2) l.style.scale = new Scale(new Vector3(1.4f, -1.4f, 1f));
                layers[i] = l;
                Add(l);
            }
            glow = new VisualElement { pickingMode = PickingMode.Ignore };
            glow.AddToClassList("fill");
            glow.style.backgroundColor = new Color(0.46f, 0.56f, 0.78f, 0.05f);
            Add(glow);
            Pose(0f);
            style.display = DisplayStyle.None;
            schedule.Execute(Tick).Every(50);
        }

        /// <summary>Shown while the stage is letterboxed (<paramref name="on"/>), hidden while it fills the screen.</summary>
        public void SetActive(bool on)
        {
            active = on;
            style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Tick()
        {
            if (!active) return;
            var app = GameApp.I;
            bool still = app != null && app.Settings != null && app.Settings.ReduceMotion;
            Pose(still ? 0f : Time.unscaledTime);
        }

        /// <summary>Places every bank for time <paramref name="t"/> (0 = the rest pose Reduce Motion keeps).</summary>
        private void Pose(float t)
        {
            for (int i = 0; i < Banks.Length; i++)
            {
                var b = Banks[i];
                float x = b.Reach * Mathf.Sin(t * 2f * Mathf.PI / b.PeriodX + b.Phase);
                float y = b.Reach * 0.6f * Mathf.Cos(t * 2f * Mathf.PI / b.PeriodY + b.Phase);
                layers[i].style.translate = new Translate(Length.Percent(x), Length.Percent(y));
            }
            glow.style.opacity = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI / 13f);
        }

        /// <summary>A soft white fog texture: tileable fractal value noise in the alpha channel, made once.</summary>
        private static Texture2D Noise()
        {
            if (noise != null) return noise;
            noise = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                name = "MysticFogNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var rng = new System.Random(706);
            var px = new Color32[TexSize * TexSize];
            float[][] octaves = { Lattice(rng, 4), Lattice(rng, 8), Lattice(rng, 16) };
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    float u = x / (float)TexSize, v = y / (float)TexSize;
                    float n = 0.55f * Sample(octaves[0], 4, u, v) + 0.3f * Sample(octaves[1], 8, u, v) + 0.15f * Sample(octaves[2], 16, u, v);
                    // Bias toward wisps and gaps rather than an even haze.
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.78f, n));
                    px[y * TexSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            noise.SetPixels32(px);
            noise.Apply(false, true);
            return noise;
        }

        private static float[] Lattice(System.Random rng, int n)
        {
            var g = new float[n * n];
            for (int i = 0; i < g.Length; i++) g[i] = (float)rng.NextDouble();
            return g;
        }

        /// <summary>Smoothly interpolated value noise on an n×n lattice that wraps at the edges.</summary>
        private static float Sample(float[] g, int n, float u, float v)
        {
            float fx = u * n, fy = v * n;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            int x1 = (x0 + 1) % n, y1 = (y0 + 1) % n;
            x0 %= n; y0 %= n;
            float a = Mathf.Lerp(g[y0 * n + x0], g[y0 * n + x1], tx);
            float b = Mathf.Lerp(g[y1 * n + x0], g[y1 * n + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }
    }
}
