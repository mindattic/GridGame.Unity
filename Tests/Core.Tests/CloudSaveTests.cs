using System;
using System.Text;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Cloud save's engine-free rules (§15.2, GG-T608): snapshot names, the snapshot body, which copy wins, and the
    /// one-upload-a-minute debounce. The Play Games plugin itself only moves the bytes these produce.</summary>
    public class CloudSaveTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        private static SaveData Game(DateTime updated)
        {
            var s = Progression.NewGame(Real, "Cloud");
            s.Gold = 777;
            s.UpdatedUtc = updated;
            s.PlaySeconds = 3 * 3600 + 7 * 60;
            return s;
        }

        private static CloudSnapshotInfo Cloud(DateTime updated) =>
            new CloudSnapshotInfo { Name = "Default-slot1", UpdatedUtc = updated, PlaySeconds = 100, HasData = true };

        [Test]
        public void Snapshot_names_are_one_per_profile_slot_and_only_use_allowed_characters()
        {
            Assert.That(CloudSave.SnapshotName("Default", 0), Is.EqualTo("Default-slot1"));
            Assert.That(CloudSave.SnapshotName("Default", 2), Is.EqualTo("Default-slot3"));
            Assert.That(CloudSave.SnapshotName("Ann Marie!", 1), Is.EqualTo("Ann_Marie_-slot2"));
            Assert.That(CloudSave.SnapshotName(null, 0), Is.EqualTo("Default-slot1"));
            var longName = CloudSave.SnapshotName(new string('x', 300), 0);
            Assert.That(longName.Length, Is.LessThanOrEqualTo(CloudSave.MaxNameLength));
            Assert.That(longName, Does.EndWith("-slot1"));
        }

        [Test]
        public void A_snapshot_reads_back_into_the_same_save()
        {
            var s = Game(T0);
            var bytes = CloudSave.Encode(s);
            Assert.That(bytes.Length, Is.LessThan(CloudSave.MaxSnapshotBytes));
            Assert.That(CloudSave.TryDecode(bytes, Real, out var back, out var why), Is.True, why);
            Assert.That(back.Gold, Is.EqualTo(777));
            Assert.That(back.Roster.Count, Is.EqualTo(s.Roster.Count));
        }

        [Test]
        public void An_empty_or_damaged_snapshot_is_refused_without_throwing()
        {
            Assert.That(CloudSave.TryDecode(null, Real, out _, out var why), Is.False);
            Assert.That(why, Is.Not.Empty);
            Assert.That(CloudSave.TryDecode(new byte[0], Real, out _, out _), Is.False);
            Assert.That(CloudSave.TryDecode(Encoding.UTF8.GetBytes("not a save"), Real, out _, out _), Is.False);
            Assert.That(CloudSave.TryDecode(new byte[] { 0xFF, 0xFE, 0x00, 0xC3 }, Real, out _, out _), Is.False);
        }

        [Test]
        public void The_newer_copy_wins_and_a_newer_cloud_copy_is_only_offered()
        {
            var local = Game(T0);
            Assert.That(CloudSave.Decide(local, null), Is.EqualTo(CloudChoice.NoCloud));
            Assert.That(CloudSave.Decide(local, new CloudSnapshotInfo { UpdatedUtc = T0.AddDays(1), HasData = false }), Is.EqualTo(CloudChoice.NoCloud));
            Assert.That(CloudSave.Decide(local, Cloud(T0.AddSeconds(2))), Is.EqualTo(CloudChoice.InSync));
            Assert.That(CloudSave.Decide(local, Cloud(T0.AddMinutes(-10))), Is.EqualTo(CloudChoice.KeepLocal));
            Assert.That(CloudSave.Decide(local, Cloud(T0.AddMinutes(10))), Is.EqualTo(CloudChoice.OfferCloud));
            Assert.That(CloudSave.Decide(null, Cloud(T0)), Is.EqualTo(CloudChoice.OfferCloud));
        }

        [Test]
        public void The_offer_names_the_cloud_date_and_play_time()
        {
            var q = CloudSave.OfferQuestion(new CloudSnapshotInfo { PlaySeconds = 2 * 3600 + 5 * 60, HasData = true },
                new DateTime(2026, 11, 22, 18, 30, 0), Game(T0));
            Assert.That(q, Does.Contain("2026-11-22 18:30"));
            Assert.That(q, Does.Contain("2:05 played"));
            Assert.That(q, Does.Contain("3:07 played"));
        }

        [Test]
        public void Uploads_wait_out_the_minute_unless_flushed()
        {
            var t = new CloudUploadThrottle();
            Assert.That(t.Due(T0), Is.False, "nothing saved yet");
            t.MarkDirty();
            Assert.That(t.Due(T0), Is.True, "the first save goes up at once");
            t.Started(T0);
            t.MarkDirty();
            Assert.That(t.Due(T0.AddSeconds(30)), Is.False);
            Assert.That(t.Due(T0.AddSeconds(30), flush: true), Is.True, "pause/quit flushes");
            Assert.That(t.Due(T0.AddSeconds(61)), Is.True);
            t.Started(T0.AddSeconds(61));
            Assert.That(t.Due(T0.AddMinutes(5)), Is.False, "nothing new since the upload");
            t.MarkDirty();
            t.Failed();
            Assert.That(t.Due(T0.AddSeconds(62)), Is.True, "a failed upload retries at the next chance");
        }

        [Test]
        public void Encoding_never_changes_the_save()
        {
            var s = Game(T0);
            var before = Newtonsoft.Json.JsonConvert.SerializeObject(s, SaveFiles.Settings);
            CloudSave.Encode(s);
            CloudSave.Description(s);
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(s, SaveFiles.Settings), Is.EqualTo(before));
        }

        // ---- iCloud key-value store (GG-T609) ----

        [Test]
        public void An_iCloud_value_reads_back_into_the_same_save_and_metadata()
        {
            var s = Game(T0);
            var value = CloudSave.ICloudPack(CloudSave.Encode(s), s.UpdatedUtc, s.PlaySeconds, "Cloud · 1 heroes\n· 3:07 played");
            Assert.That(CloudSave.ICloudTryUnpack("Default-slot1", value, out var info, out var body), Is.True);
            Assert.That(info.Name, Is.EqualTo("Default-slot1"));
            Assert.That(info.UpdatedUtc, Is.EqualTo(T0), "the save's own UpdatedUtc, so the same save reads back in sync");
            Assert.That(info.PlaySeconds, Is.EqualTo(s.PlaySeconds));
            Assert.That(info.Description, Does.Not.Contain("\n"));
            Assert.That(info.HasData, Is.True);
            Assert.That(CloudSave.Decide(s, info), Is.EqualTo(CloudChoice.InSync));
            Assert.That(CloudSave.TryDecode(body, Real, out var back, out var error), Is.True, error);
            Assert.That(back.Gold, Is.EqualTo(777));
        }

        [Test]
        public void A_missing_foreign_or_damaged_iCloud_value_is_nothing()
        {
            Assert.That(CloudSave.ICloudTryUnpack("n", null, out _, out _), Is.False);
            Assert.That(CloudSave.ICloudTryUnpack("n", "", out _, out _), Is.False);
            Assert.That(CloudSave.ICloudTryUnpack("n", "hello", out _, out _), Is.False);
            Assert.That(CloudSave.ICloudTryUnpack("n", "GGC1\nnot-ticks\n1\nd\ncode", out _, out _), Is.False);
            Assert.That(CloudSave.ICloudTryUnpack("n", "GGC1\n1\nNaN\nd\ncode", out _, out _), Is.False);
            Assert.That(CloudSave.ICloudTryUnpack("n", "GGC2\n1\n1\nd\ncode", out _, out _), Is.False);
        }

        [Test]
        public void iCloud_keys_fit_in_64_bytes_and_stay_distinct()
        {
            Assert.That(CloudSave.ICloudKey("Default-slot1"), Is.EqualTo("Default-slot1"));
            var a = CloudSave.ICloudKey(CloudSave.SnapshotName(new string('x', 90) + "a", 0));
            var b = CloudSave.ICloudKey(CloudSave.SnapshotName(new string('x', 90) + "b", 0));
            Assert.That(a.Length, Is.LessThanOrEqualTo(CloudSave.MaxICloudKeyLength));
            Assert.That(a, Does.EndWith("-slot1"));
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(CloudSave.ICloudKey(CloudSave.SnapshotName(new string('x', 90) + "a", 0)), Is.EqualTo(a), "stable");
        }

        [Test]
        public void A_conflict_keeps_the_longer_play_time_like_Play_Games()
        {
            CloudSnapshotInfo Info(double play) => new CloudSnapshotInfo { PlaySeconds = play, HasData = true };
            Assert.That(CloudSave.KeepMineOnConflict(Info(200), Info(100)), Is.True);
            Assert.That(CloudSave.KeepMineOnConflict(Info(100), Info(200)), Is.False);
            Assert.That(CloudSave.KeepMineOnConflict(Info(100), Info(100)), Is.False, "a tie keeps the cloud's copy");
            Assert.That(CloudSave.KeepMineOnConflict(Info(1), null), Is.True);
            Assert.That(CloudSave.KeepMineOnConflict(null, Info(1)), Is.False);
        }
    }
}
