using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T456: a stage draws one track from its region's shuffle bag; nothing repeats until the bag empties,
    /// a refill never opens with the track that just played, and a thin region borrows up to MinSize.</summary>
    public class MusicPlaylistTests
    {
        private static ThemeDef Theme(string id, string[] own, string[] borrow) =>
            new ThemeDef { Id = id, Music = own.ToList(), MusicBorrow = borrow.ToList() };

        [Test]
        public void NoRepeatUntilTheBagEmpties_AndNoBackToBackAcrossRefills()
        {
            var th = Theme("r", new[] { "A", "B", "C", "D" }, new string[0]);
            var camp = new CampaignDef { Themes = { th } };
            for (int seed = 0; seed < 50; seed++)
            {
                var p = new MusicPlaylist(seed);
                string prev = null;
                for (int round = 0; round < 5; round++)
                {
                    var seen = new HashSet<string>();
                    for (int i = 0; i < 4; i++)
                    {
                        string t = p.NextBattle(camp, th);
                        Assert.That(seen.Add(t), $"seed {seed}: {t} repeated inside one bag");
                        Assert.That(t, Is.Not.EqualTo(prev), $"seed {seed}: {t} played twice in a row");
                        prev = t;
                    }
                }
            }
        }

        [Test]
        public void ThinRegionBorrowsUpToMinSize_OwnTracksFirst()
        {
            var th = Theme("r", new[] { "Own" }, new[] { "B1", "B2", "B3", "B4" });
            var list = MusicPlaylist.PlaylistOf(th);
            Assert.That(list, Is.EqualTo(new[] { "Own", "B1", "B2", "B3" }));
            th.Music = new List<string> { "A", "B", "C", "D", "E" };
            Assert.That(MusicPlaylist.PlaylistOf(th), Is.EqualTo(new[] { "A", "B", "C", "D", "E" }));
        }

        [Test]
        public void HubKeepsItsTrackUntilABattle()
        {
            var camp = new CampaignDef { HubMusic = { "H1", "H2" }, Themes = { Theme("r", new[] { "A" }, new string[0]) } };
            var p = new MusicPlaylist(7);
            string h = p.Hub(camp);
            Assert.That(p.Hub(camp), Is.EqualTo(h));
            p.NextBattle(camp, camp.Themes[0]);
            Assert.That(p.Hub(camp), Is.Not.EqualTo(h), "two hub tracks: the draw after a battle is the other one");
        }

        [Test]
        public void EveryCampaignTrackShipsAsAFile()
        {
            var db = TestKit.Real;
            var names = db.Campaign.HubMusic.Concat(db.Campaign.Themes.SelectMany(t => t.Music.Concat(t.MusicBorrow))).Distinct();
            Assert.That(names.Count(), Is.GreaterThan(0), "campaign.json lists no music");
            string dir = System.IO.Path.Combine(TestKit.RepoRoot, "Assets", "Gaiden", "Resources", "Audio", "Music");
            foreach (var n in names)
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(dir, n + ".ogg")), $"{n}.ogg missing");
            foreach (var th in db.Campaign.Themes)
                Assert.That(MusicPlaylist.PlaylistOf(th).Count, Is.GreaterThanOrEqualTo(MusicPlaylist.MinSize), th.Id);
        }
    }
}
