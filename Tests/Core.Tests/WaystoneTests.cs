using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Mana Waystone wake rule (GG-T1692): glint under fog, guardian gate, idempotent wake, circles and their union.</summary>
    public class WaystoneTests
    {
        static WaystoneState Make()
        {
            var r = new RegionDef
            {
                Id = "moors", Width = 1000, Height = 1000,
                Nodes = new List<NodeDef>
                {
                    new NodeDef { Id = "a", Kind = NodeKind.Waystone, X = 100, Y = 100, Radius = 80 },
                    new NodeDef { Id = "b", Kind = NodeKind.Waystone, X = 220, Y = 100, Radius = 80 },
                    new NodeDef { Id = "c", Kind = NodeKind.Waystone, X = 800, Y = 800, Radius = 50 },
                    new NodeDef { Id = "town", Kind = NodeKind.Settlement, X = 500, Y = 500 },
                },
            };
            return new WaystoneState(r);
        }

        [Test]
        public void Dormant_stone_is_hidden_under_fog_and_glints_after_reveal()
        {
            var s = Make();
            var a = s.Region.TryNode("a");
            Assert.That(s.IsDormant("a"), Is.True);
            Assert.That(s.IsGlintVisible(a, (x, y) => false), Is.False);
            Assert.That(s.IsGlintVisible(a, (x, y) => x == 100 && y == 100), Is.True);
        }

        [Test]
        public void Cannot_wake_before_guardian_is_cleared_or_away_from_the_node()
        {
            var s = Make();
            Assert.That(s.CanWake("a", "a"), Is.False);
            Assert.That(s.Wake("a", "a"), Is.False);
            Assert.That(s.IsWoken("a"), Is.False);
            s.ClearGuardian("a");
            Assert.That(s.CanWake("a", "b"), Is.False);
            Assert.That(s.CanWake("a", null), Is.False);
            Assert.That(s.CanWake("a", "a"), Is.True);
        }

        [Test]
        public void Wake_is_idempotent_and_ends_the_glint()
        {
            var s = Make();
            s.ClearGuardian("a");
            Assert.That(s.Wake("a", "a"), Is.True);
            Assert.That(s.Wake("a", "a"), Is.True);
            Assert.That(s.Woken.Count, Is.EqualTo(1));
            Assert.That(s.IsWoken("a"), Is.True);
            Assert.That(s.IsDormant("a"), Is.False);
            Assert.That(s.IsGlintVisible(s.Region.TryNode("a"), (x, y) => true), Is.False);
        }

        [Test]
        public void Only_waystones_can_wake()
        {
            var s = Make();
            s.ClearGuardian("town");
            Assert.That(s.CanWake("town", "town"), Is.False);
            Assert.That(s.Wake("town", "town"), Is.False);
            Assert.That(s.IsDormant("town"), Is.False);
        }

        [Test]
        public void Circles_exist_only_for_woken_stones()
        {
            var s = Make();
            Assert.That(s.Circles(), Is.Empty);
            s.ClearGuardian("c");
            s.Wake("c", "c");
            var cs = s.Circles();
            Assert.That(cs.Count, Is.EqualTo(1));
            Assert.That(cs[0].NodeId, Is.EqualTo("c"));
            Assert.That(cs[0].X, Is.EqualTo(800f));
            Assert.That(cs[0].Radius, Is.EqualTo(50f));
        }

        [Test]
        public void InAnyCircle_is_the_union_of_overlapping_circles()
        {
            var s = Make();
            foreach (var id in new[] { "a", "b" }) { s.ClearGuardian(id); s.Wake(id, id); }
            Assert.That(s.InAnyCircle(160, 100), Is.True, "in the overlap");
            Assert.That(s.InAnyCircle(30, 100), Is.True, "only circle a");
            Assert.That(s.InAnyCircle(290, 100), Is.True, "only circle b");
            Assert.That(s.InAnyCircle(160, 190), Is.False, "in the gap above both rims");
            Assert.That(s.InAnyCircle(500, 500), Is.False);
            Assert.That(s.InAnyCircle(800, 800), Is.False, "dormant stone casts nothing");
        }
    }
}
