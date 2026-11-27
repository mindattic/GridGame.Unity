using System;
using System.IO;
using System.Runtime.InteropServices;
using GridGame.Meta;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Three save slots as JSON files (atomic write via temp + replace, with the previous write kept as a
    /// rolling backup that loads automatically when the slot file is bad, GG-T482) in one folder: the active player
    /// profile's <c>Profiles/&lt;Name&gt;/Saves</c> (GG-T99), or a test folder.</summary>
    public sealed class SaveStore
    {
        public const int Slots = SaveFiles.Slots;
        public readonly string Root;

        /// <summary>
        /// Save files get their own settings rather than reusing <see cref="ContentLoader.Settings"/>: that one's
        /// MissingMemberHandling.Error is right for strict content validation, but applied to a save it would throw
        /// on deserialization the moment any field is renamed/removed — before SaveMigrations.Migrate ever runs,
        /// defeating the migration path SaveData documents. Saves tolerate unknown/stale properties instead
        /// (<see cref="SaveFiles.Settings"/>, shared with the engine-free tests).
        /// </summary>
        private static JsonSerializerSettings SaveSettings => SaveFiles.Settings;

        /// <summary><paramref name="root"/> is required: the old default (persistentDataPath/Saves) is the pre-profile
        /// folder LegacySaveMigration moves away, and recreating it would migrate it again on the next launch.</summary>
        public SaveStore(string root)
        {
            if (string.IsNullOrEmpty(root)) throw new ArgumentException("A save folder is needed.", nameof(root));
            Root = root;
            Directory.CreateDirectory(Root);
        }

        private string PathFor(int slot) => SaveFiles.PathFor(Root, slot);
        /// <summary>GG-T482: the slot as it was before its latest write (slotN.json.bak).</summary>
        public string BackupPathFor(int slot) => PathFor(slot) + ".bak";

        public bool Exists(int slot) => File.Exists(PathFor(slot)) || File.Exists(BackupPathFor(slot));

        /// <summary><paramref name="content"/> drives GG-T52's proficiency grandfather clause during
        /// migration (a save equipped before that gate existed keeps its gear instead of losing it);
        /// omit it only where content isn't loaded yet (e.g. save-shape-only tests).
        /// GG-T482: when the slot file is missing or unreadable (a write cut off by a dead battery, a corrupted flash
        /// block) the rolling backup — the save one write earlier — is loaded instead and put back as the slot file, so
        /// the next write can't rotate the bad file into the backup. The bad file is kept as slotN.json.corrupt.</summary>
        public SaveData Load(int slot, GridGame.Core.Content.ContentDb content = null)
        {
            var p = PathFor(slot);
            var bak = BackupPathFor(slot);
            if (File.Exists(p))
            {
                var s = TryRead(p, slot, content, out var error, out var keep);
                if (s != null) return s;
                Debug.LogError($"[Save] slot {slot + 1} unreadable: {error}");
                // GG-T1245: a newer-build save or a transient IO failure isn't corruption; leave every file as it is.
                if (keep) return null;
            }
            if (!File.Exists(bak)) return null;
            var restored = TryRead(bak, slot, content, out var bakError, out _);
            if (restored == null)
            {
                Debug.LogError($"[Save] slot {slot + 1} backup unreadable too: {bakError}");
                return null;
            }
            try
            {
                if (File.Exists(p)) File.Copy(p, p + ".corrupt", true);
                File.Copy(bak, p, true);
                Flush();
            }
            catch (Exception e) { Debug.LogError($"[Save] slot {slot + 1} couldn't put the backup back: {e.Message}"); }
            Debug.LogWarning($"[Save] slot {slot + 1} restored from its backup (saved {restored.UpdatedUtc:u})");
            return restored;
        }

        private static SaveData TryRead(string path, int slot, GridGame.Core.Content.ContentDb content, out string error, out bool keepFiles)
        {
            error = null;
            keepFiles = false;
            try
            {
                var s = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(path), SaveSettings);
                if (s == null) { error = "empty file"; return null; }
                var migrated = SaveMigrations.Migrate(s, content);
                foreach (var note in SaveMigrations.Notes) Debug.LogWarning($"[Save] slot {slot + 1}: {note}"); // GG-T531
                return migrated;
            }
            catch (Exception e)
            {
                error = e.Message;
                keepFiles = e is IOException || e is UnauthorizedAccessException || e is SaveMigrations.SaveTooNewException;
                return null;
            }
        }

        /// <summary>
        /// Writes the slot atomically (temp file flushed to disk, then replace). Never throws: a disk-full/locked-file/
        /// permissions failure at any step is caught, logged, and reported back as <c>false</c> instead of silently losing
        /// the player's progress (GG-T146) — mirroring how <see cref="Load"/> reports a bad read with a log and
        /// a sentinel return rather than an exception. Best-effort-deletes the temp file on failure so a stray
        /// `.tmp` doesn't linger; a save attempted again later overwrites it either way.
        /// GG-T482: the replace keeps the previous slot file as the rolling backup (<see cref="BackupPathFor"/>).
        /// </summary>
        public bool Write(int slot, SaveData s) => Write(slot, s, true);

        /// <summary><see cref="Write(int, SaveData)"/>; <paramref name="stamp"/> false keeps the save's UpdatedUtc, for a
        /// housekeeping write the player didn't make (GG-T702's boot move into the wallet), so it can't change which slot
        /// is the most recent.</summary>
        public bool Write(int slot, SaveData s, bool stamp)
        {
            if (stamp) s.UpdatedUtc = DateTime.UtcNow;
            var p = PathFor(slot);
            var tmp = p + ".tmp";
            var bak = BackupPathFor(slot);
            try
            {
                var bytes = new System.Text.UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(s, Formatting.Indented, SaveSettings));
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true); // on the disk before the rename, so a power cut can't leave a renamed empty file
                }
                try
                {
                    if (File.Exists(p)) File.Replace(tmp, p, bak, true);
                    else File.Move(tmp, p);
                }
                catch (PlatformNotSupportedException)
                {
                    // WebGL's file system has no atomic replace.
                    if (File.Exists(p)) { File.Copy(p, bak, true); File.Delete(p); }
                    File.Move(tmp, p);
                }
                Flush();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] slot {slot + 1} write failed: {e.Message}");
                try { if (File.Exists(tmp)) File.Delete(tmp); }
                catch (Exception) { /* best effort; a later write overwrites it anyway */ }
                return false;
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GridGameSyncFiles();
        private static void Flush() => GridGameSyncFiles();
#else
        private static void Flush() { }
#endif

        /// <summary>Pushes file writes made outside this store (profile settings.json, profile create/rename/delete) to
        /// durable storage — a no-op everywhere but WebGL.</summary>
        public static void SyncFiles() => Flush();

        public void Delete(int slot)
        {
            var p = PathFor(slot);
            foreach (var f in new[] { p, BackupPathFor(slot), p + ".corrupt" })
                if (File.Exists(f)) File.Delete(f);
            Flush();
        }

        public int MostRecent() => SaveFiles.MostRecent(Root);
    }
}
