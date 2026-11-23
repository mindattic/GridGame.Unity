using System;
using System.Text;

namespace GridGame.Core.Content
{
    /// <summary>
    /// The one asset file-naming rule (GG-T561, owner 2026-11-21: "all assets (png, ogg, w/e) should follow this naming
    /// standard 'word-word-00.{ext}' so lowercase, hyphen delimited, and numbered even if there is only 1 they should all
    /// start with a -00; you never know if you want to start selling Christmas costume variants").
    /// <para>A file name is a <see cref="Stem"/> (lowercase words joined by <c>-</c>) plus a two-digit variant from
    /// <c>-00</c>: <c>acolyte-female-00.png</c>, <c>hag-02.png</c>, <c>tithe-moors-00.jpg</c>, <c>battle-hit-slash-00.ogg</c>.
    /// Variant 00 is the default art; 01, 02, ... are alternates of the same thing (a costume, another backdrop of the same
    /// region). Every place that builds an asset path goes through here; <c>tools/asset_names.py</c> is the same rule for
    /// the tools. Ids never change: only the files are named this way.</para>
    /// </summary>
    public static class AssetNames
    {
        /// <summary>The default variant: the art an asset shows unless data picks an alternate.</summary>
        public const int DefaultVariant = 0;

        /// <summary>Lowercase words joined by <c>-</c>. Spaces, underscores and hyphens separate words, as does a
        /// lower-to-upper case change (<c>PandaKeeper</c> → <c>panda-keeper</c>); other punctuation is dropped
        /// (<c>Morrow's Shadow</c> → <c>morrows-shadow</c>). Digits stay with their word (<c>plus3</c>).</summary>
        public static string Stem(params string[] words)
        {
            var sb = new StringBuilder();
            bool pendingBreak = false;
            if (words == null) return "";
            foreach (var w in words)
            {
                if (string.IsNullOrEmpty(w)) continue;
                pendingBreak = true;
                for (int i = 0; i < w.Length; i++)
                {
                    char c = w[i];
                    if (c == ' ' || c == '_' || c == '-' || c == '\t') { pendingBreak = true; continue; }
                    if (!char.IsLetterOrDigit(c)) continue; // punctuation is dropped, not a break
                    bool camel = char.IsUpper(c) && i > 0 && (char.IsLower(w[i - 1]) || char.IsDigit(w[i - 1])
                                 || (char.IsUpper(w[i - 1]) && i + 1 < w.Length && char.IsLower(w[i + 1])));
                    if ((pendingBreak || camel) && sb.Length > 0) sb.Append('-');
                    pendingBreak = false;
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }

        /// <summary>The file name (no extension) of <paramref name="stem"/>'s <paramref name="variant"/>: <c>stem-00</c>.</summary>
        public static string File(string stem, int variant = DefaultVariant)
        {
            if (variant < 0 || variant > 99) throw new ArgumentOutOfRangeException(nameof(variant), variant, "asset variants run 00..99");
            return string.IsNullOrEmpty(stem) ? null : stem + "-" + variant.ToString("00");
        }

        /// <summary><see cref="File"/> of the <see cref="Stem"/> of <paramref name="id"/>: an asset keyed by an id
        /// (<c>aegisguard_hands_of_mercy</c> → <c>aegisguard-hands-of-mercy-00</c>).</summary>
        public static string Of(string id, int variant = DefaultVariant) => string.IsNullOrEmpty(id) ? null : File(Stem(id), variant);

        /// <summary>A Resources key: <paramref name="folder"/> (no trailing slash) + "/" + <see cref="Of"/>.</summary>
        public static string Path(string folder, string id, int variant = DefaultVariant)
        {
            var f = Of(id, variant);
            return f == null ? null : folder + "/" + f;
        }

        /// <summary>Splits a file name (no extension) into its stem and variant; false when it doesn't follow the rule.</summary>
        public static bool TryParse(string file, out string stem, out int variant)
        {
            stem = null; variant = -1;
            if (string.IsNullOrEmpty(file) || file.Length < 4) return false;
            int n = file.Length;
            if (file[n - 3] != '-' || !char.IsDigit(file[n - 2]) || !char.IsDigit(file[n - 1])) return false;
            var s = file.Substring(0, n - 3);
            if (Stem(s) != s) return false;
            stem = s; variant = (file[n - 2] - '0') * 10 + (file[n - 1] - '0');
            return true;
        }

        /// <summary>True for a name that follows the rule (<c>word-word-00</c>).</summary>
        public static bool IsValid(string file) => TryParse(file, out _, out _);
    }
}
