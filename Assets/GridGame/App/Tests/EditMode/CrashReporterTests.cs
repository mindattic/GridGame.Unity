using NUnit.Framework;

namespace GridGame.App.Tests
{
    /// <summary>GG-T484: the error log never carries the player's folders or profile name.</summary>
    public class CrashReporterTests
    {
        [Test]
        public void Scrub_RemovesTheDataFolder()
        {
            var s = CrashReporter.Scrub("IOException: C:/Users/Ann/AppData/LocalLow/MindAttic/GridGame/x.json locked",
                "C:/Users/Ann/AppData/LocalLow/MindAttic/GridGame");
            Assert.AreEqual("IOException: <data>/x.json locked", s);
        }

        [Test]
        public void Scrub_RemovesUserHomeFolders()
        {
            Assert.AreEqual(@"at D:\x C:\Users\<user>\file.cs", CrashReporter.Scrub(@"at D:\x C:\Users\Ann\file.cs", null));
            Assert.AreEqual("/home/<user>/a and /Users/<user>/b", CrashReporter.Scrub("/home/bob/a and /Users/carol/b", null));
        }

        [Test]
        public void Scrub_RemovesTheProfileName()
        {
            Assert.AreEqual("<data>/Profiles/<profile>/Saves/slot1.json",
                CrashReporter.Scrub("/data/user/0/pkg/files/Profiles/Ann Smith/Saves/slot1.json", "/data/user/0/pkg/files"));
        }

        [Test]
        public void Scrub_LeavesOrdinaryTextAlone()
        {
            Assert.AreEqual("NullReferenceException at BattleScreen.Tick", CrashReporter.Scrub("NullReferenceException at BattleScreen.Tick", "/x"));
            Assert.AreEqual("", CrashReporter.Scrub(null, "/x"));
        }
    }
}
