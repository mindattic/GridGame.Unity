using System;

namespace GridGame.Core.Content
{
    /// <summary>One theme's background set as found on disk: its stem and how many images it has.</summary>
    public readonly struct BackdropSet
    {
        public readonly string Name;
        public readonly int Count;

        public BackdropSet(string name, int count) { Name = name; Count = count; }
    }

    /// <summary>
    /// Which backdrop a theme shows (GG-T344). Background files are named after their story region and follow the asset
    /// naming rule (<see cref="AssetNames"/>, GG-T561): the region's words joined by hyphens, numbered from 00
    /// (<c>Backgrounds/tithe-moors-00.jpg</c> … <c>tithe-moors-04.jpg</c>), at the art's original resolution. A campaign
    /// theme names its set (<c>ThemeDef.Backgrounds</c>, e.g. <c>"tithe-moors"</c>); otherwise the set is the theme id. A
    /// set has as many images as the art gives it, and an index wraps over that count. A theme with no set borrows its
    /// base theme's (a postgame theme like <c>cave2</c> shows <c>cave</c>'s), then the default theme's, then
    /// <see cref="Fallback"/>. Pure: the App passes a Resources probe, the tests a file probe.
    /// </summary>
    public static class Backdrops
    {
        public const string Folder = "Backgrounds/";
        public const string DefaultTheme = "greenvalley";
        /// <summary>The default theme's (the Tithe Moors') set.</summary>
        public const string DefaultSet = "tithe-moors";
        /// <summary>The last resort: the default theme's first image.</summary>
        public static readonly string Fallback = Key(DefaultSet, 0);
        /// <summary>The most backgrounds counted per set (a guard on the probe, not a design limit).</summary>
        public const int MaxPerTheme = 32;

        /// <summary>The resource key of image <paramref name="n"/> (0-based, the file's variant number) of a set.</summary>
        public static string Key(string set, int n) => Folder + AssetNames.File(set, n);

        public static string Key(BackdropSet s, int n) => Key(s.Name, n);

        /// <summary>Counts <paramref name="set"/>'s images, from <c>-00</c> up while they exist.</summary>
        public static BackdropSet Probe(string set, Func<string, bool> exists)
        {
            if (string.IsNullOrEmpty(set) || exists == null) return new BackdropSet(set, 0);
            int n = 0;
            while (n < MaxPerTheme && exists(Key(set, n))) n++;
            return new BackdropSet(set, n);
        }

        /// <summary>A postgame theme's art theme: trailing digits dropped ("greenvalley2" → "greenvalley").</summary>
        public static string BaseTheme(string theme)
        {
            if (string.IsNullOrEmpty(theme)) return theme;
            int end = theme.Length;
            while (end > 0 && char.IsDigit(theme[end - 1])) end--;
            return end == 0 ? theme : theme.Substring(0, end);
        }

        /// <summary>The set a campaign theme (or a stage's art theme) uses: its <c>Backgrounds</c> name, else its id.</summary>
        public static string SetOf(ContentDb db, string theme)
        {
            if (string.IsNullOrEmpty(theme)) return theme;
            var t = db?.Campaign?.Themes?.Find(x => string.Equals(x.Id, theme, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrEmpty(t?.Backgrounds) ? theme : t.Backgrounds;
        }

        /// <summary>The resource key for <paramref name="theme"/>'s <paramref name="index"/>th backdrop, wrapped over its
        /// set's own count (negative indices wrap too). <paramref name="probe"/> answers a set's images (cache it —
        /// <see cref="Probe"/> probes); <paramref name="setOf"/> maps a theme to its set name (null = the theme id).</summary>
        public static string Resolve(string theme, int index, Func<string, BackdropSet> probe, Func<string, string> setOf = null)
        {
            setOf ??= x => x;
            theme = string.IsNullOrEmpty(theme) ? DefaultTheme : theme;
            foreach (var candidate in new[] { theme, BaseTheme(theme), DefaultTheme })
            {
                var s = probe(setOf(candidate) ?? candidate);
                if (s.Count > 0) return Key(s, ((index % s.Count) + s.Count) % s.Count);
            }
            return Fallback;
        }
    }
}
