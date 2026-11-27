using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1667: world.json loads through the content pipeline and WorldContent.Validate's rules each fail with a clear message.</summary>
    public class WorldContentTests
    {
        private static ContentDb Db()
        {
            var db = new ContentDb();
            db.Stages["s1"] = new StageDef { Id = "s1", Name = "S1" };
            db.Stages["s2"] = new StageDef { Id = "s2", Name = "S2" };
            db.Campaign.Themes.Add(new ThemeDef { Id = "t1", Name = "T1", Stages = { "s1" } });
            db.Campaign.Themes.Add(new ThemeDef { Id = "t2", Name = "T2", Stages = { "s2" } });
            db.Classes["wolf"] = new ClassDef { Id = "wolf", Name = "Wolf", DropTable = "wolves" };
            db.EncounterOrder.Add(new EncounterGroupDef { Id = "g1", Name = "G1", Enemies = { new SpawnDef { Class = "wolf" } } });
            return db;
        }

        private static NodeDef N(string id, float x, float y, NodeKind kind = NodeKind.Settlement) => new NodeDef { Id = id, Kind = kind, X = x, Y = y };

        private static RegionDef Region(string id = "t1") => new RegionDef
        {
            Id = id, Art = "Maps/" + id, Width = 1000, Height = 800, Tiles = 256,
            Nodes = { N("a", 100, 100), N("b", 300, 100, NodeKind.Stage), N("w", 200, 300, NodeKind.Waystone) },
        };

        private static RegionDef Valid()
        {
            var r = Region();
            r.Nodes[1].StageId = "s1";
            r.Nodes[2].Radius = 50;
            r.Edges.Add(new EdgeDef
            {
                A = "a", B = "b", Encounters = { "wolves" }, RevealedBy = "w",
                Points = { new WorldPoint { X = 100, Y = 100 }, new WorldPoint { X = 200, Y = 150 }, new WorldPoint { X = 300, Y = 100 } },
            });
            return r;
        }

        private static List<string> Check(RegionDef r, params RegionDef[] more)
        {
            var w = new WorldDef();
            w.Regions.Add(r);
            w.Regions.AddRange(more);
            var p = new List<string>();
            WorldContent.Validate(Db(), w, p);
            return p;
        }

        private static void Fails(List<string> p, string part)
        {
            Assert.That(p.Any(m => m.Contains(part)), Is.True, $"expected a problem containing '{part}', got: {string.Join(" | ", p)}");
        }

        [Test]
        public void The_shipped_world_file_loads_empty_and_valid()
        {
            var db = ContentLoader.Load(name =>
            {
                var path = Path.Combine(ContentDir, name + ".json");
                return File.Exists(path) ? File.ReadAllText(path) : null;
            });
            Assert.That(db.World, Is.Not.Null);
            Assert.That(db.World.Regions, Is.Empty);
        }

        [Test]
        public void A_missing_world_file_leaves_World_null()
        {
            Assert.That(ContentLoader.Load(_ => null).World, Is.Null);
        }

        [Test]
        public void Lookups_find_regions_nodes_signs_and_edges()
        {
            var db = Db();
            var r = Valid();
            r.Roadsigns.Add(new RoadsignDef { Id = "rs", X = 10, Y = 10, Node = "a", Leads = "t2", Arrives = "back" });
            db.World = new WorldDef { Regions = { r } };
            Assert.That(db.Region("T1"), Is.SameAs(r));
            Assert.That(db.Region("nope"), Is.Null);
            Assert.That(db.Node("t1", "b").StageId, Is.EqualTo("s1"));
            Assert.That(db.Node("t1", "zz"), Is.Null);
            Assert.That(db.Roadsign("t1", "rs"), Is.SameAs(r.Roadsigns[0]));
            Assert.That(db.EdgesOf("t1", "a"), Has.Count.EqualTo(1));
            Assert.That(db.EdgesOf("t1", "b"), Has.Count.EqualTo(1));
            Assert.That(db.EdgesOf("t1", "w"), Is.Empty);
            Assert.That(db.EdgesOf("nope", "a"), Is.Empty);
            Assert.That(new ContentDb().Region("t1"), Is.Null);
        }

        [Test]
        public void A_small_valid_region_passes()
        {
            Assert.That(Check(Valid()), Is.Empty);
        }

        [Test]
        public void A_null_world_is_skipped()
        {
            var p = new List<string>();
            WorldContent.Validate(Db(), null, p);
            Assert.That(p, Is.Empty);
        }

        [Test]
        public void An_unknown_theme_fails()
        {
            var r = Valid();
            r.Id = "nowhere";
            Fails(Check(r), "world region nowhere: not a campaign theme");
        }

        [Test]
        public void Duplicate_ids_fail()
        {
            Fails(Check(Valid(), Valid()), "world region t1: duplicate id");
            var r = Valid();
            r.Nodes.Add(N("a", 10, 10));
            Fails(Check(r), "node a: duplicate id");
            r = Valid();
            r.Roadsigns.Add(new RoadsignDef { Id = "rs", Node = "a", Leads = "t1" });
            r.Roadsigns.Add(new RoadsignDef { Id = "rs", Node = "a", Leads = "t1" });
            Fails(Check(r), "roadsign rs: duplicate id");
        }

        [Test]
        public void Coordinates_outside_the_art_fail()
        {
            var r = Valid();
            r.Nodes.Add(N("far", 1001, 10));
            Fails(Check(r), "node far: (1001, 10) is outside the 1000x800 art");
            r = Valid();
            r.Roadsigns.Add(new RoadsignDef { Id = "rs", X = -1, Y = 5, Node = "a", Leads = "t1" });
            Fails(Check(r), "roadsign rs: (-1, 5) is outside");
        }

        [Test]
        public void An_unknown_stage_id_fails()
        {
            var r = Valid();
            r.Nodes[1].StageId = "ghost";
            Fails(Check(r), "unknown stage 'ghost'");
            r = Valid();
            r.Nodes[1].StageId = null;
            Fails(Check(r), "a Stage node needs a StageId");
        }

        [Test]
        public void A_waystone_needs_a_radius()
        {
            var r = Valid();
            r.Nodes[2].Radius = 0;
            Fails(Check(r), "a Waystone needs a Radius above 0");
            r = Valid();
            r.Nodes[0].Radius = 10;
            Fails(Check(r), "only a Waystone has a Radius");
        }

        [Test]
        public void Bad_edge_endpoints_fail()
        {
            var r = Valid();
            r.Edges[0].B = "ghost";
            Fails(Check(r), "unknown node 'ghost'");
            r = Valid();
            r.Edges[0].B = "a";
            Fails(Check(r), "an edge joins two different nodes");
        }

        [Test]
        public void Polyline_ends_beyond_tolerance_fail()
        {
            var r = Valid();
            r.Edges[0].Points[0].X = 100 + WorldContent.EndpointTolerance + 1;
            Fails(Check(r), "the first point must be at node a");
            r = Valid();
            r.Edges[0].Points[2].Y = 100 + WorldContent.EndpointTolerance + 1;
            Fails(Check(r), "the last point must be at node b");
            r = Valid();
            r.Edges[0].Points[0].X = 100 + WorldContent.EndpointTolerance;
            Assert.That(Check(r), Is.Empty, "exactly the tolerance is allowed");
        }

        [Test]
        public void An_unknown_encounter_family_fails()
        {
            var r = Valid();
            r.Edges[0].Encounters.Add("dragons");
            Fails(Check(r), "no encounter group of family 'dragons'");
        }

        [Test]
        public void A_bad_RevealedBy_fails()
        {
            var r = Valid();
            r.Edges[0].RevealedBy = "a";
            Fails(Check(r), "RevealedBy node 'a' is not a Waystone");
            r = Valid();
            r.Edges[0].RevealedBy = "ghost";
            Fails(Check(r), "RevealedBy 'ghost' is neither a waystone here nor a stage");
            r = Valid();
            r.Edges[0].RevealedBy = "s2";
            Assert.That(Check(r), Is.Empty, "a stage id reveals too");
        }

        private static RegionDef WithSign(string region, string id, string leads, string arrives)
        {
            var r = Region(region);
            r.Roadsigns.Add(new RoadsignDef { Id = id, X = 10, Y = 10, Node = "a", Leads = leads, Arrives = arrives });
            return r;
        }

        [Test]
        public void A_matched_roadsign_pair_passes()
        {
            Assert.That(Check(WithSign("t1", "east", "t2", "west"), WithSign("t2", "west", "t1", "east")), Is.Empty);
        }

        [Test]
        public void A_roadsign_pair_that_does_not_point_back_fails()
        {
            Fails(Check(WithSign("t1", "east", "t2", "west"), WithSign("t2", "west", "t1", "other")), "must lead back here (Leads t1, Arrives east)");
            Fails(Check(WithSign("t1", "east", "t2", "missing"), Region("t2")), "has no roadsign 'missing'");
            Fails(Check(WithSign("t1", "east", "t3", "west")), "Leads 't3' is not a world region");
            Fails(Check(WithSign("t1", "east", "t1", "east")), "must lead to another region");
        }

        [Test]
        public void LengthOf_computes_from_points_nodes_or_the_authored_length()
        {
            var r = Region();
            var e = new EdgeDef { A = "a", B = "b", Points = { new WorldPoint { X = 0, Y = 0 }, new WorldPoint { X = 3, Y = 4 }, new WorldPoint { X = 3, Y = 10 } } };
            Assert.That(WorldContent.LengthOf(r, e), Is.EqualTo(11f).Within(0.001f));
            e.Length = 42;
            Assert.That(WorldContent.LengthOf(r, e), Is.EqualTo(42f));
            Assert.That(WorldContent.LengthOf(r, new EdgeDef { A = "a", B = "b" }), Is.EqualTo(200f).Within(0.001f));
            Assert.That(WorldContent.LengthOf(r, new EdgeDef { A = "a", B = "ghost" }), Is.EqualTo(0f));
        }
    }
}
