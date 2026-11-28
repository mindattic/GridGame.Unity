using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>
    /// GG-T1945: <see cref="MoveReach"/> finds the shortest walkable route for tap-to-move and the pincer spots: the empty
    /// tiles a hero can walk to whose drop traps a live foe, with the foes each one catches.
    /// </summary>
    public class MoveReachTests
    {
        /// <summary>Knight (1,3), a wolf (2,3) east of it, the cleric (5,3) three tiles further east on the same row.</summary>
        private static BattleState Row()
        {
            var (s, _) = Battle(Mini(), Stage(("wolf", 2, 3)), ("knight", 1, 3), ("cleric", 5, 3));
            return s;
        }

        [Test]
        public void The_route_is_the_shortest_walk_within_the_budget()
        {
            var s = Row();
            var cleric = Hero(s, "cleric");
            Assert.That(MoveReach.Route(s, cleric.Id, new Coord(3, 3), 5), Is.EqualTo(P((4, 3), (3, 3))));
            Assert.That(MoveReach.Route(s, cleric.Id, new Coord(3, 3), 1), Is.Null, "two tiles away, one step allowed");
            Assert.That(MoveReach.Route(s, cleric.Id, cleric.Pos, 5), Is.Null, "already there");
        }

        [Test]
        public void The_tile_across_the_foe_is_a_pincer_spot_that_catches_it()
        {
            var s = Row();
            var cleric = Hero(s, "cleric");
            var wolf = At(s, 2, 3);
            var before = s.Units.Where(u => u.Alive).ToDictionary(u => u.Id, u => u.Pos);
            var spots = MoveReach.PincerSpots(s, cleric.Id, 5);
            var spot = spots.FirstOrDefault(p => p.Tile == new Coord(3, 3));
            Assert.That(spot, Is.Not.Null, string.Join(" ", spots.Select(p => p.Tile)));
            Assert.That(spot.FoeIds, Is.EqualTo(new[] { wolf.Id }));
            Assert.That(spot.Path, Is.EqualTo(MoveReach.Route(s, cleric.Id, spot.Tile, 5)), "the spot's route is tap-to-move's route");
            foreach (var p in spots) Assert.That(s.UnitAt(p.Tile), Is.Null, $"{p.Tile} is empty");
            Assert.That(s.Units.Where(u => u.Alive).ToDictionary(u => u.Id, u => u.Pos), Is.EqualTo(before), "the search only looks");
        }

        [Test]
        public void No_spots_out_of_reach_or_without_steps()
        {
            var s = Row();
            var cleric = Hero(s, "cleric");
            Assert.That(MoveReach.PincerSpots(s, cleric.Id, 1).Any(p => p.Tile == new Coord(3, 3)), Is.False);
            Assert.That(MoveReach.PincerSpots(s, cleric.Id, 0), Is.Empty);
        }
    }
}
