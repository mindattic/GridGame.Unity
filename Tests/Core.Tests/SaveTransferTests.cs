using System;
using System.IO;
using System.Linq;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The platform-free save transfer code (§15.2, GG-T607): a slot as copyable text that reads back into the same
    /// save, refuses anything damaged, foreign or newer, and spots a paid save already on the device.</summary>
    public class SaveTransferTests
    {
        private static SaveData Game()
        {
            var s = Progression.NewGame(Real, "Transfer");
            s.Gold = 1234;
            return s;
        }

        [Test]
        public void A_code_reads_back_into_the_same_save()
        {
            var s = Game();
            var code = SaveTransfer.Pack(s);
            Assert.That(code, Does.StartWith("GGS1."));
            Assert.That(SaveTransfer.TryImport(code, Real, out var back, out var why), Is.True, why);
            Assert.That(back.Gold, Is.EqualTo(1234));
            Assert.That(back.Roster.Select(h => h.ClassId), Is.EqualTo(s.Roster.Select(h => h.ClassId)));
        }

        [Test]
        public void Spaces_and_line_breaks_from_a_paste_are_ignored()
        {
            var code = SaveTransfer.Pack(Game());
            var wrapped = string.Join("\n  ", Enumerable.Range(0, (code.Length + 59) / 60).Select(i => code.Substring(i * 60, Math.Min(60, code.Length - i * 60))));
            Assert.That(SaveTransfer.TryImport(wrapped, Real, out _, out var why), Is.True, why);
        }

        [Test]
        public void A_damaged_cut_short_or_foreign_code_is_refused_without_throwing()
        {
            var code = SaveTransfer.Pack(Game());
            foreach (var bad in new[] { "", "   ", "hello", "GGS1.deadbeef.!!!", code.Substring(0, code.Length / 2),
                         code.Substring(0, 5) + "00000000" + code.Substring(13), // a wrong checksum
                         code.Substring(0, 14) + (code[14] == 'A' ? 'B' : 'A') + code.Substring(15) }) // a mangled payload
            {
                Assert.That(SaveTransfer.TryImport(bad, Real, out var s, out var why), Is.False, bad);
                Assert.That(s, Is.Null);
                Assert.That(why, Is.Not.Empty);
            }
        }

        [Test]
        public void A_code_from_a_newer_format_or_save_version_is_refused()
        {
            var code = SaveTransfer.Pack(Game());
            Assert.That(SaveTransfer.TryUnpack("GGS9" + code.Substring(4), out _, out var why), Is.False);
            Assert.That(why, Does.Contain("newer version"));

            var future = Game();
            future.Version = SaveData.CurrentVersion + 1;
            Assert.That(SaveTransfer.TryImport(SaveTransfer.Pack(future), Real, out _, out why), Is.False);
            Assert.That(why, Does.Contain("newer version"));
        }

        [Test]
        public void Packing_never_changes_the_save()
        {
            var s = Game();
            var before = SaveFiles.Serialize(s);
            SaveTransfer.Pack(s);
            Assert.That(SaveFiles.Serialize(s), Is.EqualTo(before));
        }

        [Test]
        public void A_paid_save_already_in_a_slot_on_this_device_is_spotted()
        {
            var paid = Game();
            PurchaseLedger.Grant(paid, "tx-1", DiamondStore.Packs[0].ProductId, DateTime.UtcNow, "Test", out _);
            var dir = Path.Combine(Path.GetTempPath(), "gg-transfer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var slot = SaveFiles.PathFor(dir, 0);
                Assert.That(SaveTransfer.SharesPurchasesWith(paid, new[] { slot }), Is.False, "no file yet");
                File.WriteAllText(slot, SaveFiles.Serialize(paid));
                Assert.That(SaveTransfer.SharesPurchasesWith(paid, new[] { slot }), Is.True);
                Assert.That(SaveTransfer.SharesPurchasesWith(Game(), new[] { slot }), Is.False, "a save with no purchases is never refused");
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
