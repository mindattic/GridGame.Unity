using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace GridGame.Meta
{
    /// <summary>
    /// One menu window theme as data (GG-T407; owner 2026-11-17: "make the gradient totally editable just like the FF and
    /// chrono trigger games ... many different themes to choose from ... textures spliced/superimposed into it"). The
    /// presets live in <c>Resources/UI/themes.json</c>; the player's own live in the profile's settings.json
    /// (<see cref="SettingsSave.CustomThemes"/>). Engine-free: the app bakes it (App/UI/WindowThemes.cs).
    /// </summary>
    public sealed class WindowThemeDef
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("gradient")] public WindowGradientDef Gradient = new WindowGradientDef();
        /// <summary>Optional texture laid over the gradient (null = none).</summary>
        [JsonProperty("texture", NullValueHandling = NullValueHandling.Ignore)] public WindowTextureDef Texture;
        [JsonProperty("frame")] public WindowFrameDef Frame = new WindowFrameDef();
        /// <summary>Optional default text colour (#RRGGBB); null keeps the stylesheet's.</summary>
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)] public string Text;
        /// <summary>Optional accent (highlights, inspect cards, the Classic frame); null derives it from the gradient.</summary>
        [JsonProperty("accent", NullValueHandling = NullValueHandling.Ignore)] public string Accent;
        /// <summary>Display adjustment (GG-T563), each 0..1 with 0.5 neutral; missing = 0.5, so themes.json and older
        /// custom themes need no migration. A preset's player tuning lives in <see cref="SettingsSave.ThemeTones"/>, not here.</summary>
        [JsonProperty("brightness", NullValueHandling = NullValueHandling.Ignore)] public float? Brightness;
        [JsonProperty("contrast", NullValueHandling = NullValueHandling.Ignore)] public float? Contrast;
        [JsonProperty("gamma", NullValueHandling = NullValueHandling.Ignore)] public float? Gamma;

        /// <summary>The theme's <see cref="WindowTone"/> (0.5 for any value not set).</summary>
        [JsonIgnore]
        public WindowTone Tone
        {
            get => new WindowTone(Brightness ?? WindowTone.Neutral, Contrast ?? WindowTone.Neutral, Gamma ?? WindowTone.Neutral);
            set
            {
                // Neutral values are left out of the JSON, so an untouched theme saves exactly as before.
                Brightness = Math.Abs(value.Brightness - WindowTone.Neutral) < 0.001f ? (float?)null : value.Brightness;
                Contrast = Math.Abs(value.Contrast - WindowTone.Neutral) < 0.001f ? (float?)null : value.Contrast;
                Gamma = Math.Abs(value.Gamma - WindowTone.Neutral) < 0.001f ? (float?)null : value.Gamma;
            }
        }

        public string ToJson() => JsonConvert.SerializeObject(this, WindowThemeCatalog.Json);
        public static WindowThemeDef FromJson(string json) => JsonConvert.DeserializeObject<WindowThemeDef>(json, WindowThemeCatalog.Json);
        public WindowThemeDef Clone() => FromJson(ToJson());
    }

    public sealed class WindowGradientDef
    {
        /// <summary><c>corners</c> (four colours, bilinear), <c>linear</c> (2-4 stops at any angle) or <c>radial</c>.</summary>
        [JsonProperty("mode")] public string Mode = WindowThemeCatalog.Linear;
        /// <summary>Corners mode: top-left, top-right, bottom-left, bottom-right.</summary>
        [JsonProperty("corners", NullValueHandling = NullValueHandling.Ignore)] public string[] Corners;
        /// <summary>Linear mode: the direction the gradient runs, CSS style (0 = upward, 90 = rightward, 180 = downward).</summary>
        [JsonProperty("angle")] public float Angle = 180f;
        /// <summary>Linear and radial: 2-4 stops, positions 0..1 in order (radial runs centre → farthest corner).</summary>
        [JsonProperty("stops", NullValueHandling = NullValueHandling.Ignore)] public List<WindowStopDef> Stops;
        /// <summary>Radial centre in window fractions (x right, y down); null = the middle.</summary>
        [JsonProperty("center", NullValueHandling = NullValueHandling.Ignore)] public float[] Center;
        /// <summary>Soft two-octave mottle baked into the gradient (0 = clean), the Granite look (GG-T404).</summary>
        [JsonProperty("grain")] public float Grain;
    }

    public sealed class WindowStopDef
    {
        [JsonProperty("at")] public float At;
        [JsonProperty("color")] public string Color;
        public WindowStopDef() { }
        public WindowStopDef(float at, string color) { At = at; Color = color; }
    }

    public sealed class WindowTextureDef
    {
        /// <summary>A procedural texture (<see cref="WindowThemeCatalog.ProceduralTextures"/>) or <c>file:&lt;name&gt;</c> for
        /// Resources/UI/WindowTextures/&lt;name&gt;.png.</summary>
        [JsonProperty("id")] public string Id;
        [JsonProperty("opacity")] public float Opacity = 0.35f;
        [JsonProperty("blend")] public string Blend = "overlay";
        /// <summary>Tile size multiplier (1 = one texel per design px).</summary>
        [JsonProperty("scale")] public float Scale = 1f;
    }

    public sealed class WindowFrameDef
    {
        /// <summary>Which Theme.uss block shapes the frame, tabs, buttons and header (<see cref="WindowThemeCatalog.FrameStyles"/>).</summary>
        [JsonProperty("style")] public string Style = "classic";
        /// <summary>Optional frame colours: 1 = solid, 2 = bevel (light top-left, dark bottom-right), 3 = light, mid
        /// (left/right), dark. None = the style's own (Classic: the derived accent).</summary>
        [JsonProperty("colors", NullValueHandling = NullValueHandling.Ignore)] public string[] Colors;
    }

    /// <summary>Parsing, validation and the vocabulary of <see cref="WindowThemeDef"/>.</summary>
    public static class WindowThemeCatalog
    {
        public const string Corners = "corners", Linear = "linear", Radial = "radial";
        public static readonly string[] Modes = { Corners, Linear, Radial };
        /// <summary>How a texture combines with the gradient. Multiply darkens, screen lightens, overlay and soft-light
        /// push both ways around mid-gray (soft-light at half strength), normal lays the texture's own colours on top.</summary>
        public static readonly string[] Blends = { "multiply", "overlay", "screen", "soft-light", "normal" };
        public static readonly string[] ProceduralTextures =
            { "granite", "marble", "parchment", "linen", "brushed", "stars", "scanlines", "checker", "noise", "carbon" };
        public const string FilePrefix = "file:";
        /// <summary>Frame styles: the Theme.uss blocks (<c>.theme-&lt;style&gt;</c>) plus <c>classic</c>, the base look.</summary>
        public static readonly string[] FrameStyles = { "classic", "granite", "chrono", "slate", "dark", "manapunk" };
        public const int MaxStops = 4, MinStops = 2, MaxNameLength = 24, MaxCustom = 8;

        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Formatting = Formatting.None,
            Culture = CultureInfo.InvariantCulture,
        };

        private sealed class ThemesFile
        {
            [JsonProperty("comment")] public string Comment { get; set; }
            [JsonProperty("themes")] public List<WindowThemeDef> Themes;
        }

        /// <summary>Reads a themes.json. Every theme that fails <see cref="Validate"/> is left out and named in
        /// <paramref name="errors"/>; a file that doesn't parse yields no themes and one error.</summary>
        public static List<WindowThemeDef> Parse(string json, out List<string> errors)
        {
            errors = new List<string>();
            var ok = new List<WindowThemeDef>();
            ThemesFile file;
            try { file = JsonConvert.DeserializeObject<ThemesFile>(json ?? "", Json); }
            catch (Exception e) { errors.Add("themes.json: " + e.Message); return ok; }
            if (file?.Themes == null || file.Themes.Count == 0) { errors.Add("themes.json: no \"themes\" array"); return ok; }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < file.Themes.Count; i++)
            {
                var t = file.Themes[i];
                var probs = Validate(t);
                if (t != null && !string.IsNullOrEmpty(t.Name) && !names.Add(t.Name)) probs.Add("duplicate name");
                if (probs.Count > 0) { errors.Add($"themes.json theme #{i + 1} '{t?.Name}': {string.Join("; ", probs)}"); continue; }
                ok.Add(t);
            }
            return ok;
        }

        /// <summary>Every problem with <paramref name="t"/> (empty = valid).</summary>
        public static List<string> Validate(WindowThemeDef t)
        {
            var p = new List<string>();
            if (t == null) { p.Add("null theme"); return p; }
            if (string.IsNullOrWhiteSpace(t.Name)) p.Add("missing name");
            else if (t.Name.Length > MaxNameLength) p.Add($"name longer than {MaxNameLength}");
            var g = t.Gradient;
            if (g == null) p.Add("missing gradient");
            else
            {
                if (!Modes.Contains(g.Mode)) p.Add($"gradient mode '{g.Mode}' is not one of {string.Join("/", Modes)}");
                if (g.Mode == Corners)
                {
                    if (g.Corners == null || g.Corners.Length != 4) p.Add("corners mode needs 4 colours (TL, TR, BL, BR)");
                    else foreach (var c in g.Corners) if (!TryHex(c, out _)) p.Add($"bad corner colour '{c}'");
                }
                if (g.Mode == Linear || g.Mode == Radial)
                {
                    if (g.Stops == null || g.Stops.Count < MinStops || g.Stops.Count > MaxStops) p.Add($"{g.Mode} needs {MinStops}-{MaxStops} stops");
                    else
                    {
                        float last = -1f;
                        foreach (var s in g.Stops)
                        {
                            if (s == null || !TryHex(s.Color, out _)) { p.Add($"bad stop colour '{s?.Color}'"); continue; }
                            if (s.At < 0f || s.At > 1f) p.Add($"stop at {s.At} outside 0..1");
                            if (s.At < last) p.Add("stops out of order");
                            last = s.At;
                        }
                    }
                }
                if (g.Mode == Linear && (g.Angle < 0f || g.Angle >= 360f || float.IsNaN(g.Angle))) p.Add($"angle {g.Angle} outside 0..359");
                if (g.Center != null && (g.Center.Length != 2 || g.Center.Any(v => v < 0f || v > 1f))) p.Add("center must be [x, y] in 0..1");
                if (g.Grain < 0f || g.Grain > 0.5f) p.Add($"grain {g.Grain} outside 0..0.5");
            }
            var x = t.Texture;
            if (x != null)
            {
                if (string.IsNullOrEmpty(x.Id) || (!ProceduralTextures.Contains(x.Id) && !(x.Id.StartsWith(FilePrefix) && x.Id.Length > FilePrefix.Length)))
                    p.Add($"texture '{x.Id}' is not one of {string.Join("/", ProceduralTextures)} or file:<name>");
                if (x.Opacity < 0f || x.Opacity > 1f) p.Add($"texture opacity {x.Opacity} outside 0..1");
                if (!Blends.Contains(x.Blend)) p.Add($"blend '{x.Blend}' is not one of {string.Join("/", Blends)}");
                if (x.Scale < 0.1f || x.Scale > 8f) p.Add($"texture scale {x.Scale} outside 0.1..8");
            }
            var f = t.Frame;
            if (f == null) p.Add("missing frame");
            else
            {
                if (!FrameStyles.Contains(f.Style)) p.Add($"frame style '{f.Style}' is not one of {string.Join("/", FrameStyles)}");
                if (f.Colors != null)
                {
                    if (f.Colors.Length > 3) p.Add("frame takes at most 3 colours");
                    foreach (var c in f.Colors) if (!TryHex(c, out _)) p.Add($"bad frame colour '{c}'");
                }
            }
            if (t.Text != null && !TryHex(t.Text, out _)) p.Add($"bad text colour '{t.Text}'");
            if (t.Accent != null && !TryHex(t.Accent, out _)) p.Add($"bad accent colour '{t.Accent}'");
            foreach (var (label, v) in new[] { ("brightness", t.Brightness), ("contrast", t.Contrast), ("gamma", t.Gamma) })
                if (v.HasValue && (float.IsNaN(v.Value) || v.Value < 0f || v.Value > 1f)) p.Add($"{label} {v} outside 0..1");
            return p;
        }

        /// <summary>Parses #RRGGBB or RRGGBB into 0xRRGGBB.</summary>
        public static bool TryHex(string s, out int rgb)
        {
            rgb = 0;
            if (string.IsNullOrEmpty(s)) return false;
            if (s[0] == '#') s = s.Substring(1);
            return s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb);
        }

        public static string Hex(int r, int g, int b) => $"#{Clamp(r):X2}{Clamp(g):X2}{Clamp(b):X2}";

        private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
    }
}
