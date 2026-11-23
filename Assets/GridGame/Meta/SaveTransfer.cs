using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GridGame.Core.Content;
using Newtonsoft.Json;

namespace GridGame.Meta
{
    /// <summary>
    /// The platform-free save transfer code (§15.2, GG-T607): one save slot as copyable text, for moving a game to another
    /// device with no cloud account. <c>GGS1.&lt;checksum&gt;.&lt;payload&gt;</c>, where the payload is the slot's compact
    /// JSON, gzipped and base64'd, and the checksum is the first 8 hex digits of the JSON's SHA-256. The checksum catches a
    /// code cut short or mangled by a paste; it is not a seal (anyone can rebuild it). <see cref="Pack"/> and
    /// <see cref="TryUnpack"/> are the one serialize/deserialize pair, for the cloud saves (GG-T608/T609) too.
    /// Nothing here throws: every failure is a reason string for the player.
    /// </summary>
    public static class SaveTransfer
    {
        public const string Magic = "GGS";
        public const int FormatVersion = 1;
        /// <summary>A guard on what a pasted code may expand to (a real slot is tens of KB).</summary>
        public const int MaxJsonBytes = 8 * 1024 * 1024;
        public const int MaxCodeChars = 4 * 1024 * 1024;

        private static string Header => Magic + FormatVersion;

        /// <summary>The slot as a transfer code. Reads <paramref name="s"/> only; never changes it.</summary>
        public static string Pack(SaveData s)
        {
            var json = JsonConvert.SerializeObject(s, Formatting.None, SaveFiles.Settings);
            var raw = new UTF8Encoding(false).GetBytes(json);
            using var outStream = new MemoryStream();
            using (var gz = new GZipStream(outStream, CompressionLevel.Optimal, true)) gz.Write(raw, 0, raw.Length);
            return $"{Header}.{Checksum(raw)}.{Convert.ToBase64String(outStream.ToArray())}";
        }

        /// <summary>The slot JSON inside a transfer code, or false with the reason. Spaces and line breaks a paste adds are
        /// ignored.</summary>
        public static bool TryUnpack(string code, out string json, out string error)
        {
            json = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(code)) { error = "Paste a save code first."; return false; }
                if (code.Length > MaxCodeChars) { error = "That code is far too long to be a save."; return false; }
                var clean = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
                var parts = clean.Split('.');
                if (parts.Length != 3 || !parts[0].StartsWith(Magic, StringComparison.Ordinal))
                { error = "That isn't a save code."; return false; }
                if (!int.TryParse(parts[0].Substring(Magic.Length), out int format)) { error = "That isn't a save code."; return false; }
                if (format > FormatVersion) { error = "That code was made by a newer version of the game. Update the game, then import it."; return false; }
                if (format < 1) { error = "That isn't a save code."; return false; }

                byte[] packed;
                try { packed = Convert.FromBase64String(parts[2]); }
                catch (FormatException) { error = "That code is damaged (it may have been cut short when copied)."; return false; }

                byte[] raw;
                using (var input = new MemoryStream(packed))
                using (var gz = new GZipStream(input, CompressionMode.Decompress))
                using (var outStream = new MemoryStream())
                {
                    var buffer = new byte[81920];
                    int n;
                    while ((n = gz.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        outStream.Write(buffer, 0, n);
                        if (outStream.Length > MaxJsonBytes) { error = "That code is far too large to be a save."; return false; }
                    }
                    raw = outStream.ToArray();
                }
                if (!string.Equals(Checksum(raw), parts[1], StringComparison.OrdinalIgnoreCase))
                { error = "That code is damaged (it may have been cut short when copied)."; return false; }
                json = new UTF8Encoding(false).GetString(raw);
                return true;
            }
            catch (Exception)
            {
                json = null;
                error = "That code is damaged (it may have been cut short when copied).";
                return false;
            }
        }

        /// <summary>A transfer code read back into a save, version-checked and run through <see cref="SaveMigrations.Migrate"/>
        /// like any slot loaded from disk. False with the reason for anything unreadable, foreign or from a newer build.</summary>
        public static bool TryImport(string code, ContentDb content, out SaveData save, out string error)
        {
            save = null;
            if (!TryUnpack(code, out var json, out error)) return false;
            SaveData s;
            try { s = SaveFiles.Deserialize(json); }
            catch (Exception) { error = "That code doesn't hold a save."; return false; }
            if (s == null || s.Roster == null) { error = "That code doesn't hold a save."; return false; }
            if (s.Version > SaveData.CurrentVersion)
            { error = "That save was made by a newer version of the game. Update the game, then import it."; return false; }
            try { save = SaveMigrations.Migrate(s, content); }
            catch (Exception e) { error = "That save couldn't be read: " + e.Message; save = null; return false; }
            if (save == null) { error = "That code doesn't hold a save."; return false; }
            return true;
        }

        /// <summary>The store transaction ids <paramref name="s"/>'s purchase ledger has granted (GG-T469).</summary>
        public static IEnumerable<string> PurchaseIds(SaveData s) =>
            s?.Purchases?.Granted?.Keys ?? Enumerable.Empty<string>();

        /// <summary>Whether any real-money purchase in <paramref name="imported"/> is already granted in one of the save files
        /// at <paramref name="slotPaths"/> (each file and its .bak). A paid save copied into a second slot on the same device
        /// would pay its Shards twice, so the import screen refuses it; moving to another device is unaffected. Read-only.</summary>
        public static bool SharesPurchasesWith(SaveData imported, IEnumerable<string> slotPaths)
        {
            var ids = PurchaseIds(imported).ToList();
            if (ids.Count == 0 || slotPaths == null) return false;
            foreach (var p in slotPaths)
                foreach (var tx in ids)
                    if (PurchaseLedger.FileHas(p, tx) || PurchaseLedger.FileHas(p + ".bak", tx)) return true;
            return false;
        }

        private static string Checksum(byte[] raw)
        {
            using var sha = SHA256.Create();
            var h = sha.ComputeHash(raw);
            var sb = new StringBuilder(8);
            for (int i = 0; i < 4; i++) sb.Append(h[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
