using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Fog-of-war reveal mask (GG-T1687): circle, band, clamping, round-trip, idempotence, node visibility.</summary>
    public class FogMaskTests
    {
        static WorldPoint P(float x, float y) => new WorldPoint { X = x, Y = y };

        [Test]
        public void Circle_reveals_inside_and_not_outside()
        {
            var m = new FogMask(320, 320);
            m.RevealCircle(160, 160, 48);
            Assert.That(m.IsRevealed(160, 160), Is.True);
            Assert.That(m.IsRevealed(190, 160), Is.True);
            Assert.That(m.IsRevealed(260, 160), Is.False);
            Assert.That(m.IsRevealed(10, 10), Is.False);
            Assert.That(m.RevealedFraction, Is.GreaterThan(0f));
            Assert.That(m.RevealedFraction, Is.LessThan(0.2f));
        }

        [Test]
        public void Band_follows_the_polyline()
        {
            var m = new FogMask(320, 320);
            m.RevealBand(new[] { P(20, 20), P(300, 20), P(300, 300) }, 20);
            Assert.That(m.IsRevealed(150, 20), Is.True);
            Assert.That(m.IsRevealed(300, 150), Is.True);
            Assert.That(m.IsRevealed(300, 295), Is.True);
            Assert.That(m.IsRevealed(150, 150), Is.False);
            Assert.That(m.IsRevealed(20, 200), Is.False);
        }

        [Test]
        public void Reveals_clamp_to_the_region()
        {
            var m = new FogMask(100, 100);
            m.RevealCircle(-500, 50, 20);
            Assert.That(m.RevealedCells, Is.EqualTo(0));
            m.RevealCircle(0, 0, 200);
            Assert.That(m.RevealedFraction, Is.EqualTo(1f));
            Assert.That(m.IsRevealed(-1, 5), Is.False);
            Assert.That(m.IsRevealed(5, 101), Is.False);
            Assert.That(m.IsRevealed(100, 100), Is.True);
        }

        [Test]
        public void Serialize_round_trips()
        {
            var m = new FogMask(250, 130, 16);
            m.RevealCircle(100, 60, 40);
            m.RevealBand(new[] { P(0, 0), P(240, 120) }, 10);
            var back = FogMask.FromBase64(250, 130, m.ToBase64(), 16);
            Assert.That(back.ToBase64(), Is.EqualTo(m.ToBase64()));
            Assert.That(back.RevealedCells, Is.EqualTo(m.RevealedCells));
            Assert.That(back.IsRevealed(100, 60), Is.True);
            Assert.That(back.IsRevealed(240, 10), Is.False);
        }

        [Test]
        public void Repeated_reveal_is_idempotent()
        {
            var m = new FogMask(200, 200);
            m.RevealCircle(80, 80, 30);
            var once = m.ToBase64();
            m.RevealCircle(80, 80, 30);
            Assert.That(m.ToBase64(), Is.EqualTo(once));
        }

        [Test]
        public void Hidden_node_becomes_visible()
        {
            var m = new FogMask(400, 400);
            var node = new NodeDef { X = 200, Y = 200 };
            Assert.That(m.IsVisible(node), Is.False);
            m.RevealCircle(190, 190, 30);
            Assert.That(m.IsVisible(node), Is.True);
        }
    }
}
