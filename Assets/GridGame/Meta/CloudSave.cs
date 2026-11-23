using System;
using System.Text;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>What the cloud holds for one slot (a Play Games snapshot's metadata, GG-T608; iCloud's later, GG-T609).</summary>
    public sealed class CloudSnapshotInfo
    {
        public string Name;
        public DateTime UpdatedUtc;
        public double PlaySeconds;
        public string Description;
        /// <summary>False for a snapshot that was only just created by opening it (no save uploaded to it yet).</summary>
        public bool HasData;
    }

    /// <summary>What to do with a slot that has a cloud copy.</summary>
    public enum CloudChoice
    {
        /// <summary>Nothing (usable) in the cloud: play the local save; the next upload fills the cloud.</summary>
        NoCloud,
        /// <summary>Local and cloud are the same save (within <see cref="CloudSave.SameSaveTolerance"/>).</summary>
        InSync,
        /// <summary>The local save is newer: play it; the next upload replaces the cloud copy.</summary>
        KeepLocal,
        /// <summary>The cloud copy is newer: ask "Use the cloud save from &lt;date&gt;, &lt;play time&gt;?" (never merged).</summary>
        OfferCloud,
    }

    /// <summary>
    /// The engine-free half of cloud save (§15.2, GG-T608): one snapshot per profile slot, named
    /// <c>&lt;profile&gt;-slotN</c>, holding the slot as a <see cref="SaveTransfer"/> code (the same Pack/TryImport pair as the
    /// transfer code, so the version check and <see cref="SaveMigrations.Migrate"/> run on every download), with
    /// <c>UpdatedUtc</c> and <c>PlaySeconds</c> in the snapshot metadata. The platform plugin (Play Games Saved Games on
    /// Android) lives in the App layer and only moves bytes; every decision is made here so the tests can check it.
    /// </summary>
    public static class CloudSave
    {
        /// <summary>Play Games snapshot names: at most 100 characters of <c>[A-Za-z0-9-._~]</c>.</summary>
        public const int MaxNameLength = 100;
        /// <summary>Clock skew and the write itself: a cloud copy this close to the local save is the same save.</summary>
        public static readonly TimeSpan SameSaveTolerance = TimeSpan.FromSeconds(5);
        /// <summary>Uploads are debounced to one a minute (plus one on every pause/quit).</summary>
        public static readonly TimeSpan UploadInterval = TimeSpan.FromMinutes(1);
        /// <summary>The Play Games cover-image-free snapshot limit is 3 MB; a real slot is tens of KB packed.</summary>
        public const int MaxSnapshotBytes = 3 * 1024 * 1024;

        /// <summary>The snapshot name for <paramref name="profile"/>'s slot <paramref name="slot"/> (0-based; named 1-based).
        /// Characters Play Games refuses become '_', so two profiles differing only in those share nothing else.</summary>
        public static string SnapshotName(string profile, int slot)
        {
            var suffix = "-slot" + (slot + 1);
            var sb = new StringBuilder();
            foreach (var c in string.IsNullOrWhiteSpace(profile) ? "Default" : profile.Trim())
                sb.Append(IsNameChar(c) ? c : '_');
            int room = MaxNameLength - suffix.Length;
            if (sb.Length > room) sb.Length = room;
            return sb + suffix;
        }

        private static bool IsNameChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '.' || c == '_' || c == '~';

        /// <summary>The snapshot body: the slot's transfer code as UTF-8. Reads <paramref name="s"/> only.</summary>
        public static byte[] Encode(SaveData s) => new UTF8Encoding(false).GetBytes(SaveTransfer.Pack(s));

        /// <summary>A downloaded snapshot back into a save, checked and migrated exactly like an imported transfer code.
        /// False with the player-facing reason for an empty, damaged, foreign or newer-version snapshot. Never throws.</summary>
        public static bool TryDecode(byte[] data, ContentDb content, out SaveData save, out string error)
        {
            save = null;
            if (data == null || data.Length == 0) { error = "The cloud save is empty."; return false; }
            if (data.Length > MaxSnapshotBytes) { error = "The cloud save is too large to be a save."; return false; }
            string code;
            try { code = new UTF8Encoding(false, true).GetString(data); }
            catch (Exception) { error = "The cloud save is damaged."; return false; }
            return SaveTransfer.TryImport(code, content, out save, out error);
        }

        /// <summary>The snapshot description shown in Play Games' own saved-games list.</summary>
        public static string Description(SaveData s) =>
            s == null ? "" : $"{s.Name} · {s.Roster?.Count ?? 0} heroes · {PlayTime(s.PlaySeconds)} played";

        /// <summary>"3:07" style hours:minutes, the same as the Load Game list.</summary>
        public static string PlayTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)t.TotalHours}:{t.Minutes:00}";
        }

        /// <summary>Local save vs its cloud copy. The newer one wins by <c>UpdatedUtc</c>; a newer cloud copy is only ever
        /// offered, never applied or merged on its own. <paramref name="local"/> may be null (a slot empty on this device).</summary>
        public static CloudChoice Decide(SaveData local, CloudSnapshotInfo cloud)
        {
            if (cloud == null || !cloud.HasData) return CloudChoice.NoCloud;
            if (local == null) return CloudChoice.OfferCloud;
            var diff = cloud.UpdatedUtc.ToUniversalTime() - local.UpdatedUtc.ToUniversalTime();
            if (diff.Duration() <= SameSaveTolerance) return CloudChoice.InSync;
            return diff > TimeSpan.Zero ? CloudChoice.OfferCloud : CloudChoice.KeepLocal;
        }

        /// <summary>The question asked for <see cref="CloudChoice.OfferCloud"/>; <paramref name="cloudLocalTime"/> is the
        /// cloud copy's time already in the player's own time zone (the App converts; Meta has no time-zone policy).</summary>
        public static string OfferQuestion(CloudSnapshotInfo cloud, DateTime cloudLocalTime, SaveData local)
        {
            var q = $"Use the cloud save from {cloudLocalTime:yyyy-MM-dd HH:mm}, {PlayTime(cloud.PlaySeconds)} played?";
            if (local != null) q += $" This device's save is older ({PlayTime(local.PlaySeconds)} played) and would be replaced.";
            return q;
        }
    }

    /// <summary>The upload debounce (§15.2): every <c>Persist</c> marks the slot changed; an upload is due at most once per
    /// <see cref="CloudSave.UploadInterval"/>, and a pause or quit flushes whatever is waiting. Time is passed in, so the
    /// tests drive it without a clock.</summary>
    public sealed class CloudUploadThrottle
    {
        private readonly TimeSpan interval;
        private DateTime lastUpload = DateTime.MinValue;

        public CloudUploadThrottle() : this(CloudSave.UploadInterval) { }
        public CloudUploadThrottle(TimeSpan interval) { this.interval = interval; }

        /// <summary>A save was written that the cloud doesn't have yet.</summary>
        public bool Dirty { get; private set; }

        public void MarkDirty() => Dirty = true;

        /// <summary>Whether to upload now. <paramref name="flush"/> (pause, quit) ignores the interval.</summary>
        public bool Due(DateTime nowUtc, bool flush = false) =>
            Dirty && (flush || nowUtc - lastUpload >= interval);

        /// <summary>An upload started at <paramref name="nowUtc"/>; a save written after this marks it dirty again.</summary>
        public void Started(DateTime nowUtc) { Dirty = false; lastUpload = nowUtc; }

        /// <summary>The upload failed: try again on the next chance, without waiting out the interval.</summary>
        public void Failed() { Dirty = true; lastUpload = DateTime.MinValue; }

        /// <summary>A different slot or profile was loaded: nothing of the old one is waiting.</summary>
        public void Reset() { Dirty = false; lastUpload = DateTime.MinValue; }
    }
}
