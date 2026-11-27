using System;
using System.Collections.Generic;
using System.IO;
using GridGame.Core.Content;
using GridGame.Core.World;
using GridGame.Meta;
using Newtonsoft.Json;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1781: SaveData.World v26 (GG-T1778, BIBLE §28.13). The v25 -> v26 migration, frontier placement, the JSON
    /// round trip of every <see cref="WorldSave"/> field and the <see cref="WorldSaveAdapter"/> round trips.</summary>
    public class WorldSaveTests
    {
        /// <summary>A private copy of the real content (TestKit.Real is shared, so never mutate it).</summary>
        private static ContentDb Fresh() => ContentLoader.Load(name =>
        {
            var p = Path.Combine(ContentDir, name + ".json");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        });

        static NodeDef Node(string id, float x, float y, NodeKind kind = NodeKind.Camp, string stageId = null) =>
            new NodeDef { Id = id, Kind = kind, X = x, Y = y, StageId = stageId };

        static EdgeDef Edge(string a, string b, float length = 0) => new EdgeDef { A = a, B = b, Length = length };

        static RegionDef Region(string id, IEnumerable<NodeDef> nodes, IEnumerable<EdgeDef> edges)
        {
            var r = new RegionDef { Id = id, Width = 1000, Height = 1000, Tiles = 256 };
            r.Nodes.AddRange(nodes);
            r.Edges.AddRange(edges);
            return r;
        }

        // A(0,0) - B(10,0) - C(10,20): straight roads, legs 10 and 20 long. D hangs off A by a road the route never names.
        static RegionDef Line() => Region("test",
            new[] { Node("A", 0, 0), Node("B", 10, 0), Node("C", 10, 20), Node("D", 0, 5), Node("W", 500, 500, NodeKind.Waystone) },
            new[] { Edge("A", "B"), Edge("B", "C"), Edge("A", "D") });

        static SaveData V25(int highestCleared = -1) =>
            new SaveData { Version = 25, HighestClearedStage = highestCleared, World = null };

        /// <summary>A private content copy whose world has one synthetic region holding a Stage node for campaign stage
        /// <paramref name="stageIndex"/> (and a decoy for the stage before it).</summary>
        static ContentDb WithFrontierRegion(int stageIndex, out string stageId)
        {
            var db = Fresh();
            var stages = db.CampaignStages();
            stageId = stages[stageIndex].Id;
            var nodes = new List<NodeDef> { Node("camp", 0, 0), Node("frontier", 100, 100, NodeKind.Stage, stageId) };
            if (stageIndex > 0) nodes.Add(Node("earlier", 50, 50, NodeKind.Stage, stages[stageIndex - 1].Id));
            db.World = new WorldDef();
            db.World.Regions.Add(Region("synthetic", nodes, new[] { Edge("camp", "frontier") }));
            return db;
        }

        // ── migration ──

        [Test]
        public void A_v25_save_migrates_to_v26_with_a_filled_world()
        {
            var s = SaveMigrations.Migrate(V25());
            Assert.That(s.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(s.World, Is.Not.Null);
            Assert.That(s.World.Route, Is.Not.Null.And.Empty);
            Assert.That(s.World.Danger, Is.Not.Null);
            Assert.That(s.World.Regions, Is.Not.Null);
            Assert.That(s.World.HuntCounts, Is.Not.Null);
            Assert.That(s.World.RegionId, Is.Null);
        }

        [Test]
        public void FillNulls_fills_every_null_collection_including_a_regions_lists()
        {
            var w = new WorldSave { Route = null, Danger = null, Regions = null, HuntCounts = null };
            w.FillNulls();
            Assert.That(w.Route, Is.Not.Null);
            Assert.That(w.Danger, Is.Not.Null);
            Assert.That(w.Regions, Is.Not.Null);
            Assert.That(w.HuntCounts, Is.Not.Null);

            w.Regions["r"] = new RegionSave { Woken = null, GuardiansCleared = null, DustSince = null, RevealedEdges = null };
            w.Regions["empty"] = null;
            Assert.DoesNotThrow(() => w.FillNulls());
            Assert.That(w.Regions["r"].Woken, Is.Not.Null);
            Assert.That(w.Regions["r"].GuardiansCleared, Is.Not.Null);
            Assert.That(w.Regions["r"].DustSince, Is.Not.Null);
            Assert.That(w.Regions["r"].RevealedEdges, Is.Not.Null);
        }

        [Test]
        public void A_v25_save_with_null_region_lists_is_filled_by_migration()
        {
            var s = V25();
            s.World = new WorldSave { Route = null, Danger = null, HuntCounts = null };
            s.World.Regions["r"] = new RegionSave { Woken = null, GuardiansCleared = null, DustSince = null, RevealedEdges = null };
            SaveMigrations.Migrate(s);
            Assert.That(s.World.Route, Is.Not.Null);
            Assert.That(s.World.Danger, Is.Not.Null);
            Assert.That(s.World.HuntCounts, Is.Not.Null);
            Assert.That(s.World.Regions["r"].Woken, Is.Not.Null);
            Assert.That(s.World.Regions["r"].DustSince, Is.Not.Null);
        }

        [Test]
        public void An_old_save_is_camped_at_its_furthest_cleared_stage_node()
        {
            var db = WithFrontierRegion(2, out _);
            var s = V25(2);
            s.World = new WorldSave { Route = new List<string> { "x", "y" }, Distance = 7 };
            SaveMigrations.Migrate(s, db);
            Assert.That(s.World.RegionId, Is.EqualTo("synthetic"));
            Assert.That(s.World.NodeId, Is.EqualTo("frontier"));
            Assert.That(s.World.Camped, Is.True);
            Assert.That(s.World.Route, Is.Empty);
            Assert.That(s.World.Distance, Is.EqualTo(0));
        }

        [Test]
        public void Frontier_placement_matches_the_stage_id_without_regard_to_case()
        {
            var db = WithFrontierRegion(1, out var stageId);
            db.World.Regions[0].TryNode("frontier").StageId = stageId.ToUpperInvariant();
            var s = V25(1);
            SaveMigrations.Migrate(s, db);
            Assert.That(s.World.NodeId, Is.EqualTo("frontier"));
        }

        [Test]
        public void A_save_with_nothing_cleared_is_not_on_the_overworld()
        {
            var db = WithFrontierRegion(0, out _);
            var s = V25(-1);
            Assert.DoesNotThrow(() => SaveMigrations.Migrate(s, db));
            Assert.That(s.World.RegionId, Is.Null);
            Assert.That(s.World.NodeId, Is.Null);
        }

        [Test]
        public void With_the_real_empty_world_an_old_save_is_not_on_the_overworld()
        {
            var db = Fresh();
            Assert.That(db.CampaignStages().Count, Is.GreaterThan(0));
            var s = V25(0);
            Assert.DoesNotThrow(() => SaveMigrations.Migrate(s, db));
            Assert.That(s.World.RegionId, Is.Null);
            Assert.That(s.World.NodeId, Is.Null);
        }

        [Test]
        public void A_frontier_past_the_campaign_or_without_a_world_is_not_on_the_overworld()
        {
            var db = Fresh();
            var s = new SaveData { HighestClearedStage = db.CampaignStages().Count };
            Assert.DoesNotThrow(() => WorldSaveAdapter.PlaceAtFrontier(s, db));
            Assert.That(s.World.RegionId, Is.Null);

            db.World = null;
            s.HighestClearedStage = 0;
            Assert.DoesNotThrow(() => WorldSaveAdapter.PlaceAtFrontier(s, db));
            Assert.That(s.World.RegionId, Is.Null);
        }

        [Test]
        public void A_v26_save_is_not_placed_again()
        {
            var db = WithFrontierRegion(2, out _);
            var s = new SaveData { HighestClearedStage = 2 };
            s.World.RegionId = "elsewhere";
            s.World.NodeId = "somewhere";
            SaveMigrations.Migrate(s, db);
            Assert.That(s.World.RegionId, Is.EqualTo("elsewhere"));
            Assert.That(s.World.NodeId, Is.EqualTo("somewhere"));
        }

        // ── Travel replaces expeditions (GG-T1790) ──

        [Test]
        public void A_new_world_starts_at_camp()
        {
            Assert.That(new WorldSave().Camped, Is.True);
            Assert.That(new SaveData().World.Camped, Is.True);
            Assert.That(TravelRules.OnTheRoad(new SaveData()), Is.False);
        }

        [Test]
        public void A_save_mid_expedition_is_placed_then_drained_onto_the_road_with_its_orbs()
        {
            var db = WithFrontierRegion(2, out _);
            var s = V25(2);
            s.Expedition = new ExpeditionSave { ThemeId = "anything", Next = 1, CarriedOrbs = 3, Coins = 50 };
            SaveMigrations.Migrate(s, db);
            Assert.That(s.Expedition, Is.Null);
            Assert.That(s.World.NodeId, Is.EqualTo("frontier"), "placed at the frontier first");
            Assert.That(s.World.Camped, Is.False, "the drain runs after PlaceAtFrontier camps it");
            Assert.That(s.World.CarriedOrbs, Is.EqualTo(3));
            Assert.That(TravelRules.StartOrbs(s), Is.EqualTo(3));
        }

        [Test]
        public void A_v26_save_on_an_expedition_drains_and_one_without_loads_at_camp()
        {
            var away = new SaveData { Version = 26, Expedition = new ExpeditionSave { ThemeId = "t", CarriedOrbs = 2 } };
            away.World.Camped = true;
            SaveMigrations.Migrate(away);
            Assert.That(away.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(away.Expedition, Is.Null);
            Assert.That(away.World.Camped, Is.False);
            Assert.That(away.World.CarriedOrbs, Is.EqualTo(2));

            var home = new SaveData { Version = 26 };
            home.World.Camped = false; // the old WorldSave default
            SaveMigrations.Migrate(home);
            Assert.That(home.World.Camped, Is.True);
            Assert.That(home.World.CarriedOrbs, Is.EqualTo(0));
        }

        [Test]
        public void A_current_save_on_the_road_stays_on_the_road()
        {
            var s = new SaveData();
            TravelRules.SetOut(s);
            s.World.CarriedOrbs = 4;
            var back = SaveMigrations.Migrate(JsonConvert.DeserializeObject<SaveData>(JsonConvert.SerializeObject(s)));
            Assert.That(back.World.Camped, Is.False);
            Assert.That(back.World.CarriedOrbs, Is.EqualTo(4));
            Assert.That(back.Expedition, Is.Null);
        }

        [Test]
        public void A_negative_legacy_orb_count_drains_as_none()
        {
            var s = new SaveData { Expedition = new ExpeditionSave { CarriedOrbs = -5 } };
            SaveMigrations.DrainExpedition(s);
            Assert.That(s.World.CarriedOrbs, Is.EqualTo(0));
            Assert.That(s.World.Camped, Is.False);
        }

        // ── JSON ──

        [Test]
        public void Every_WorldSave_field_survives_a_JSON_round_trip()
        {
            var w = new WorldSave
            {
                RegionId = "greenvalley",
                NodeId = "mill",
                Route = new List<string> { "mill", "ford", "keep" },
                Distance = 12.5,
                Camped = true,
                ClockMinutes = 98765,
                Danger = new DangerSave { Level = 0.75, Seed = 42, Travelled = 330.25, Epoch = 3 },
                Food = 4,
                AteMeal = true,
                SleptFrom = 1200,
                WellRestedUntil = 2400,
                Camp = new CampSave { X = 11.5, Y = 22.25, Radius = 64, Seed = 9 },
                WildSeed = 1234,
            };
            w.Regions["greenvalley"] = new RegionSave
            {
                FogBase64 = "AQID",
                FogW = 800,
                FogH = 600,
                FogCell = 16,
                Woken = new List<string> { "stone1" },
                GuardiansCleared = new List<string> { "stone1", "stone2" },
                DustSince = new Dictionary<string, double> { ["stone1"] = 480.5 },
                RevealedEdges = new List<string> { "mill-ford" },
            };
            w.HuntCounts["greenvalley"] = 3;

            var json = JsonConvert.SerializeObject(w, ContentLoader.Settings);
            var back = JsonConvert.DeserializeObject<WorldSave>(json, ContentLoader.Settings);

            Assert.That(back.RegionId, Is.EqualTo("greenvalley"));
            Assert.That(back.NodeId, Is.EqualTo("mill"));
            Assert.That(back.Route, Is.EqualTo(new[] { "mill", "ford", "keep" }));
            Assert.That(back.Distance, Is.EqualTo(12.5));
            Assert.That(back.Camped, Is.True);
            Assert.That(back.ClockMinutes, Is.EqualTo(98765));
            Assert.That(back.Danger.Level, Is.EqualTo(0.75));
            Assert.That(back.Danger.Seed, Is.EqualTo(42));
            Assert.That(back.Danger.Travelled, Is.EqualTo(330.25));
            Assert.That(back.Danger.Epoch, Is.EqualTo(3));
            Assert.That(back.Food, Is.EqualTo(4));
            Assert.That(back.AteMeal, Is.True);
            Assert.That(back.SleptFrom, Is.EqualTo(1200));
            Assert.That(back.WellRestedUntil, Is.EqualTo(2400));
            Assert.That(back.Camp, Is.Not.Null);
            Assert.That(back.Camp.X, Is.EqualTo(11.5));
            Assert.That(back.Camp.Y, Is.EqualTo(22.25));
            Assert.That(back.Camp.Radius, Is.EqualTo(64));
            Assert.That(back.Camp.Seed, Is.EqualTo(9));
            Assert.That(back.WildSeed, Is.EqualTo(1234));
            Assert.That(back.HuntCounts["greenvalley"], Is.EqualTo(3));

            var r = back.Regions["greenvalley"];
            Assert.That(r.FogBase64, Is.EqualTo("AQID"));
            Assert.That(r.FogW, Is.EqualTo(800));
            Assert.That(r.FogH, Is.EqualTo(600));
            Assert.That(r.FogCell, Is.EqualTo(16));
            Assert.That(r.Woken, Is.EqualTo(new[] { "stone1" }));
            Assert.That(r.GuardiansCleared, Is.EqualTo(new[] { "stone1", "stone2" }));
            Assert.That(r.DustSince["stone1"], Is.EqualTo(480.5));
            Assert.That(r.RevealedEdges, Is.EqualTo(new[] { "mill-ford" }));
        }

        [Test]
        public void A_save_with_no_camp_reads_back_with_no_camp()
        {
            var json = JsonConvert.SerializeObject(new WorldSave(), ContentLoader.Settings);
            var back = JsonConvert.DeserializeObject<WorldSave>(json, ContentLoader.Settings);
            Assert.That(back.Camp, Is.Null);
            Assert.That(back.RegionId, Is.Null);
            Assert.That(back.SleptFrom, Is.EqualTo(-1));
            Assert.That(WorldSaveAdapter.Camp(back, (x, y) => true), Is.Null);
        }

        // ── adapter round trips ──

        [Test]
        public void The_clock_round_trips_its_minutes()
        {
            var clock = new WorldClock();
            clock.Advance(1234);
            long minutes = clock.Minutes;
            var w = new WorldSave();
            WorldSaveAdapter.Store(w, clock);
            Assert.That(w.ClockMinutes, Is.EqualTo(minutes));
            Assert.That(WorldSaveAdapter.Clock(w).Minutes, Is.EqualTo(minutes));

            // A second load-and-store does not drift.
            WorldSaveAdapter.Store(w, WorldSaveAdapter.Clock(w));
            Assert.That(w.ClockMinutes, Is.EqualTo(minutes));
        }

        [Test]
        public void A_save_with_no_clock_starts_at_the_start_hour()
        {
            Assert.That(WorldSaveAdapter.Clock(new WorldSave()).Minutes, Is.EqualTo(new WorldClock().Minutes));
        }

        [Test]
        public void Danger_round_trips()
        {
            var danger = new DangerState(77) { Level = 0.4, Travelled = 812.5, Epoch = 5 };
            var w = new WorldSave();
            WorldSaveAdapter.Store(w, danger);
            var back = WorldSaveAdapter.Danger(w);
            Assert.That(back.Seed, Is.EqualTo(77));
            Assert.That(back.Level, Is.EqualTo(0.4));
            Assert.That(back.Travelled, Is.EqualTo(812.5));
            Assert.That(back.Epoch, Is.EqualTo(5));
        }

        [Test]
        public void Danger_reads_from_a_null_danger_save()
        {
            var w = new WorldSave { Danger = null };
            Assert.That(WorldSaveAdapter.Danger(w).Level, Is.EqualTo(0));
        }

        [Test]
        public void The_camp_night_round_trips()
        {
            var night = new CampNight { Food = 3, AteMeal = true, SleptFrom = 1500, WellRestedUntil = 2100 };
            var w = new WorldSave();
            WorldSaveAdapter.Store(w, night);
            var back = WorldSaveAdapter.Night(w);
            Assert.That(back.Food, Is.EqualTo(3));
            Assert.That(back.AteMeal, Is.True);
            Assert.That(back.SleptFrom, Is.EqualTo(1500));
            Assert.That(back.WellRestedUntil, Is.EqualTo(2100));
        }

        [Test]
        public void The_fog_round_trips_through_base64()
        {
            var region = Line();
            var fog = new FogMask(region);
            fog.RevealCircle(500, 500, 100);
            var w = new WorldSave();
            var r = WorldSaveAdapter.Region(w, region.Id);
            WorldSaveAdapter.Store(r, fog);

            Assert.That(r.FogBase64, Is.Not.Empty);
            Assert.That(r.FogW, Is.EqualTo(1000));
            Assert.That(r.FogH, Is.EqualTo(1000));
            Assert.That(r.FogCell, Is.EqualTo(FogMask.DefaultCellSize));

            var back = WorldSaveAdapter.Fog(WorldSaveAdapter.Region(w, region.Id), region);
            Assert.That(back.IsRevealed(500, 500), Is.True);
            Assert.That(back.IsRevealed(550, 520), Is.True);
            Assert.That(back.IsRevealed(50, 50), Is.False);
            Assert.That(back.IsRevealed(900, 900), Is.False);
            Assert.That(back.RevealedCells, Is.EqualTo(fog.RevealedCells));
        }

        [Test]
        public void A_region_never_saved_starts_fully_fogged()
        {
            var region = Line();
            var fog = WorldSaveAdapter.Fog(WorldSaveAdapter.Region(new WorldSave(), region.Id), region);
            Assert.That(fog.RevealedCells, Is.EqualTo(0));
            Assert.That(fog.Width, Is.EqualTo(1000));
        }

        [Test]
        public void Waystones_round_trip_woken_and_guardian_cleared()
        {
            var region = Line();
            var state = new WaystoneState(region);
            state.ClearGuardian("W");
            Assert.That(state.Wake("W", "W"), Is.True);
            var r = new RegionSave();
            WorldSaveAdapter.Store(r, state);
            Assert.That(r.Woken, Is.EqualTo(new[] { "W" }));
            Assert.That(r.GuardiansCleared, Is.EqualTo(new[] { "W" }));

            var back = WorldSaveAdapter.Waystones(r, region);
            Assert.That(back.IsWoken("W"), Is.True);
            Assert.That(back.GuardianCleared.Contains("W"), Is.True);
            Assert.That(back.Region, Is.SameAs(region));
        }

        [Test]
        public void Waystone_dust_round_trips_its_since_minutes()
        {
            var region = Line();
            var state = new WaystoneState(region);
            state.ClearGuardian("W");
            var dust = new WaystoneDust(state);
            Assert.That(dust.Wake("W", "W", 600.5), Is.True);
            var r = new RegionSave();
            WorldSaveAdapter.Store(r, state);
            WorldSaveAdapter.Store(r, dust);
            Assert.That(r.DustSince["W"], Is.EqualTo(600.5));

            var backState = WorldSaveAdapter.Waystones(r, region);
            var back = WorldSaveAdapter.Dust(r, backState);
            Assert.That(back.SinceMinutes["W"], Is.EqualTo(600.5));
            Assert.That(back.SinceMinutes.ContainsKey("w"), Is.True, "case-insensitive like the live dictionary");
            Assert.That(back.State, Is.SameAs(backState));
        }

        [Test]
        public void Travel_is_rebuilt_mid_way_along_the_second_leg()
        {
            var region = Line();
            var w = new WorldSave { Route = new List<string> { "A", "B", "C" }, Distance = 15 };
            var travel = WorldSaveAdapter.Travel(w, region);
            Assert.That(travel, Is.Not.Null);
            Assert.That(travel.Route.Nodes, Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(travel.LegCount, Is.EqualTo(2));
            Assert.That(travel.TotalLength, Is.EqualTo(30).Within(1e-9));
            Assert.That(travel.LegIndex, Is.EqualTo(1));
            Assert.That(travel.DistanceIntoLeg, Is.EqualTo(5).Within(1e-9));
            Assert.That(travel.Arrived, Is.False);

            var again = new WorldSave();
            WorldSaveAdapter.Store(again, travel);
            Assert.That(again.Route, Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(again.Distance, Is.EqualTo(15).Within(1e-9));
        }

        [Test]
        public void Travel_at_or_past_the_whole_route_has_arrived_on_the_last_leg()
        {
            var region = Line();
            foreach (double distance in new[] { 30.0, 99.0 })
            {
                var w = new WorldSave { Route = new List<string> { "A", "B", "C" }, Distance = distance };
                var travel = WorldSaveAdapter.Travel(w, region);
                Assert.That(travel.LegIndex, Is.EqualTo(1), $"distance {distance}");
                Assert.That(travel.DistanceIntoLeg, Is.EqualTo(travel.LengthOfLeg(1)).Within(1e-9), $"distance {distance}");
                Assert.That(travel.Arrived, Is.True, $"distance {distance}");
            }
        }

        [Test]
        public void Travel_is_null_when_standing_still_or_the_route_is_gone()
        {
            var region = Line();
            Assert.That(WorldSaveAdapter.Travel(new WorldSave(), region), Is.Null);
            Assert.That(WorldSaveAdapter.Travel(new WorldSave { Route = new List<string> { "A" } }, region), Is.Null);
            Assert.That(WorldSaveAdapter.Travel(new WorldSave { Route = new List<string> { "A", "C" } }, region), Is.Null,
                "A and C share no road, and the rebuild uses only the saved route's roads");
            Assert.That(WorldSaveAdapter.Travel(new WorldSave { Route = new List<string> { "A", "B" } }, null), Is.Null);

            var w = new WorldSave { Route = new List<string> { "A", "B" }, Distance = 4 };
            WorldSaveAdapter.Store(w, (TravelState)null);
            Assert.That(w.Route, Is.Empty);
            Assert.That(w.Distance, Is.EqualTo(0));
        }
    }
}
