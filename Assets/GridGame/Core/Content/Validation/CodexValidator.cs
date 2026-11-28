using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The codex.json checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class CodexValidator
    {
        /// <summary>GG-T936: every codex.json Key and Related key names a known <see cref="CodexKinds"/> kind and a record
        /// that exists (<see cref="ContentDb.CodexRecordExists"/>), in its lower-case form, once.</summary>
        internal static void Validate(ContentDb db, List<string> p)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string Check(string key, string where)
            {
                if (!CodexKinds.TrySplit(key?.Trim(), out var kind, out var id)) return $"{where}: '{key}' is not '<kind>:<id>'";
                if (Array.IndexOf(CodexKinds.All, kind) < 0) return $"{where}: unknown codex kind '{kind}' in '{key}'";
                if (!db.CodexRecordExists(kind, id)) return $"{where}: '{key}' names no {kind} record";
                if (key.Trim() != CodexKinds.Key(kind, id)) return $"{where}: '{key}' should be '{CodexKinds.Key(kind, id)}' (lower case)";
                return null;
            }
            foreach (var e in db.CodexOrder)
            {
                string where = $"codex {e.Key}";
                var bad = Check(e.Key, "codex entry");
                if (bad != null) { p.Add(bad); continue; }
                if (!seen.Add(e.Key.Trim())) p.Add($"{where}: listed twice");
                foreach (var r in e.Related)
                {
                    var rb = Check(r, where + " Related");
                    if (rb != null) p.Add(rb);
                    else if (string.Equals(r.Trim(), e.Key.Trim(), StringComparison.OrdinalIgnoreCase)) p.Add($"{where}: relates to itself");
                }
            }
        }
    }
}
