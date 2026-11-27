using System;
using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>What a place on a region map is (BIBLE §28.2). A <see cref="Stage"/> node points at a stages.json stage; a
    /// <see cref="Waystone"/> has a protective circle (<see cref="NodeDef.Radius"/>, §28.8).</summary>
    public enum NodeKind { Stage, Settlement, Waystone, Camp, Bridge, Pass, Ruin }

    /// <summary>world.json (GG-T1666, BIBLE §28): the overworld, one region map per campaign theme. Regions connect only by
    /// <see cref="RoadsignDef"/>s.</summary>
    public sealed class WorldDef
    {
        public List<RegionDef> Regions = new List<RegionDef>();

        public RegionDef TryRegion(string id) => id == null ? null : Regions.Find(r => r != null && string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>One region's painted relief map and the hand-authored graph over it (§28.1, §28.2). Coordinates are art
    /// pixels, origin top-left.</summary>
    public sealed class RegionDef
    {
        /// <summary>A <see cref="ThemeDef.Id"/>.</summary>
        public string Id;
        /// <summary>Resources path of the map art.</summary>
        public string Art;
        /// <summary>The art's size in pixels.</summary>
        public int Width, Height;
        /// <summary>Scroll tile size in pixels (the art is cut into Tiles x Tiles pieces).</summary>
        public int Tiles;
        public List<NodeDef> Nodes = new List<NodeDef>();
        public List<EdgeDef> Edges = new List<EdgeDef>();
        public List<RoadsignDef> Roadsigns = new List<RoadsignDef>();

        public NodeDef TryNode(string id) => id == null ? null : Nodes.Find(n => n != null && string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase));
        public RoadsignDef TryRoadsign(string id) => id == null ? null : Roadsigns.Find(r => r != null && string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public sealed class NodeDef
    {
        public string Id;
        public NodeKind Kind;
        public float X, Y;
        /// <summary>A <see cref="NodeKind.Stage"/> node's stages.json stage.</summary>
        public string StageId;
        /// <summary>As the location card says it; a stage node falls back to its stage's name.</summary>
        public string Name;
        /// <summary>Two or three lines for the location card (§28.3).</summary>
        public string Lore;
        /// <summary>A <see cref="NodeKind.Waystone"/>'s protective circle, in art pixels (§28.8).</summary>
        public float Radius;
    }

    public sealed class WorldPoint
    {
        public float X, Y;
    }

    /// <summary>A road between two nodes, drawn as a polyline along valleys, passes and bridges (§28.2).</summary>
    public sealed class EdgeDef
    {
        public string A, B;
        /// <summary>The road from A to B, first point at A and last at B; empty is a straight line.</summary>
        public List<WorldPoint> Points = new List<WorldPoint>();
        /// <summary>Travel length in art pixels; 0 means the polyline's own length (<see cref="WorldContent.LengthOf"/>).</summary>
        public float Length;
        /// <summary>How fast the danger gauge rises along this road (§28.4); 0 is safe.</summary>
        public float Danger;
        public string Biome;
        /// <summary>EncounterCatalog family ids an ambush on this road draws from.</summary>
        public List<string> Encounters = new List<string>();
        /// <summary>A waystone node id in this region or a stage id that opens this road; null is open from the start.</summary>
        public string RevealedBy;
    }

    /// <summary>The only way between regions (§28.1): taking it moves the caravan to <see cref="Arrives"/> on the map of
    /// <see cref="Leads"/>, whose sign leads back here.</summary>
    public sealed class RoadsignDef
    {
        public string Id;
        public float X, Y;
        /// <summary>The node the sign stands at.</summary>
        public string Node;
        /// <summary>The region the road leads to.</summary>
        public string Leads;
        /// <summary>That region's roadsign the caravan arrives at.</summary>
        public string Arrives;
        /// <summary>A stage that must be cleared before the sign can be read; null is readable from the start.</summary>
        public string RequiresStage;
    }

    public static class WorldContent
    {
        /// <summary>How far, in art pixels, an edge's first and last points may sit from its end nodes.</summary>
        public const float EndpointTolerance = 4f;

        /// <summary>The edge's travel length: <see cref="EdgeDef.Length"/>, or when that is 0 the polyline's length
        /// (the straight line between its nodes when it has no points).</summary>
        public static float LengthOf(RegionDef region, EdgeDef e)
        {
            if (e.Length > 0) return e.Length;
            var pts = new List<WorldPoint>();
            if (e.Points != null && e.Points.Count > 0) pts.AddRange(e.Points);
            else
            {
                var a = region.TryNode(e.A);
                var b = region.TryNode(e.B);
                if (a == null || b == null) return 0;
                pts.Add(new WorldPoint { X = a.X, Y = a.Y });
                pts.Add(new WorldPoint { X = b.X, Y = b.Y });
            }
            float len = 0;
            for (int i = 1; i < pts.Count; i++) len += Distance(pts[i - 1].X, pts[i - 1].Y, pts[i].X, pts[i].Y);
            return len;
        }

        /// <summary>world.json: each region a campaign theme, once, with its art and size; unique node, roadsign ids per
        /// region; nodes and signs inside the art; stage nodes on known stages; waystones with a radius; edges between
        /// known nodes whose polylines start and end at them, known encounter families and a waystone or stage that
        /// reveals them; roadsign pairs that point back at each other. A missing world.json (null) is skipped.</summary>
        public static void Validate(ContentDb db, WorldDef world, List<string> p)
        {
            if (world == null) return;
            var regionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in world.Regions)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.Id)) { p.Add("world region with empty Id"); continue; }
                string w = $"world region {r.Id}";
                if (!regionIds.Add(r.Id)) p.Add($"{w}: duplicate id");
                if (!db.Campaign.Themes.Exists(t => Is(t?.Id, r.Id))) p.Add($"{w}: not a campaign theme");
                if (string.IsNullOrWhiteSpace(r.Art)) p.Add($"{w}: needs an Art path");
                if (r.Width <= 0 || r.Height <= 0) p.Add($"{w}: Width and Height must be positive, has {r.Width}x{r.Height}");
                if (r.Tiles <= 0) p.Add($"{w}: Tiles must be positive, has {r.Tiles}");
                ValidateNodes(db, r, w, p);
                ValidateEdges(db, r, w, p);
                ValidateRoadsigns(db, world, r, w, p);
            }
        }

        private static void ValidateNodes(ContentDb db, RegionDef r, string w, List<string> p)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in r.Nodes)
            {
                if (n == null || string.IsNullOrWhiteSpace(n.Id)) { p.Add($"{w}: node with empty Id"); continue; }
                string nw = $"{w} node {n.Id}";
                if (!ids.Add(n.Id)) p.Add($"{nw}: duplicate id");
                if (!Inside(r, n.X, n.Y)) p.Add($"{nw}: ({n.X}, {n.Y}) is outside the {r.Width}x{r.Height} art");
                if (n.Kind == NodeKind.Stage)
                {
                    if (string.IsNullOrWhiteSpace(n.StageId)) p.Add($"{nw}: a Stage node needs a StageId");
                    else if (db.TryStage(n.StageId) == null) p.Add($"{nw}: unknown stage '{n.StageId}'");
                }
                else if (n.StageId != null) p.Add($"{nw}: only a Stage node has a StageId");
                if (n.Kind == NodeKind.Waystone && n.Radius <= 0) p.Add($"{nw}: a Waystone needs a Radius above 0");
                else if (n.Kind != NodeKind.Waystone && n.Radius != 0) p.Add($"{nw}: only a Waystone has a Radius");
            }
        }

        private static void ValidateEdges(ContentDb db, RegionDef r, string w, List<string> p)
        {
            foreach (var e in r.Edges)
            {
                if (e == null) { p.Add($"{w}: null edge"); continue; }
                string ew = $"{w} edge {e.A}-{e.B}";
                var a = r.TryNode(e.A);
                var b = r.TryNode(e.B);
                if (a == null) p.Add($"{ew}: unknown node '{e.A}'");
                if (b == null) p.Add($"{ew}: unknown node '{e.B}'");
                if (Is(e.A, e.B)) p.Add($"{ew}: an edge joins two different nodes");
                if (e.Length < 0) p.Add($"{ew}: Length can't be negative");
                if (e.Danger < 0) p.Add($"{ew}: Danger can't be negative");
                var pts = e.Points ?? new List<WorldPoint>();
                if (pts.Exists(pt => pt == null)) p.Add($"{ew}: null point");
                else if (pts.Count > 0)
                {
                    if (pts.Count < 2) p.Add($"{ew}: a polyline needs at least two points");
                    foreach (var pt in pts)
                        if (!Inside(r, pt.X, pt.Y)) { p.Add($"{ew}: point ({pt.X}, {pt.Y}) is outside the art"); break; }
                    if (a != null && Distance(pts[0].X, pts[0].Y, a.X, a.Y) > EndpointTolerance) p.Add($"{ew}: the first point must be at node {a.Id}");
                    var last = pts[pts.Count - 1];
                    if (b != null && Distance(last.X, last.Y, b.X, b.Y) > EndpointTolerance) p.Add($"{ew}: the last point must be at node {b.Id}");
                }
                foreach (var fam in e.Encounters ?? new List<string>())
                    if (EncounterCatalog.ForFamily(db, fam).Count == 0) p.Add($"{ew}: no encounter group of family '{fam}'");
                if (e.RevealedBy != null)
                {
                    var by = r.TryNode(e.RevealedBy);
                    if (by != null) { if (by.Kind != NodeKind.Waystone) p.Add($"{ew}: RevealedBy node '{by.Id}' is not a Waystone"); }
                    else if (db.TryStage(e.RevealedBy) == null) p.Add($"{ew}: RevealedBy '{e.RevealedBy}' is neither a waystone here nor a stage");
                }
            }
        }

        private static void ValidateRoadsigns(ContentDb db, WorldDef world, RegionDef r, string w, List<string> p)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in r.Roadsigns)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.Id)) { p.Add($"{w}: roadsign with empty Id"); continue; }
                string sw = $"{w} roadsign {s.Id}";
                if (!ids.Add(s.Id)) p.Add($"{sw}: duplicate id");
                if (!Inside(r, s.X, s.Y)) p.Add($"{sw}: ({s.X}, {s.Y}) is outside the {r.Width}x{r.Height} art");
                if (r.TryNode(s.Node) == null) p.Add($"{sw}: unknown node '{s.Node}'");
                if (s.RequiresStage != null && db.TryStage(s.RequiresStage) == null) p.Add($"{sw}: unknown RequiresStage '{s.RequiresStage}'");
                if (Is(s.Leads, r.Id)) { p.Add($"{sw}: a roadsign must lead to another region"); continue; }
                var to = world.TryRegion(s.Leads);
                if (to == null) { p.Add($"{sw}: Leads '{s.Leads}' is not a world region"); continue; }
                var back = to.TryRoadsign(s.Arrives);
                if (back == null) p.Add($"{sw}: region {to.Id} has no roadsign '{s.Arrives}'");
                else if (!Is(back.Leads, r.Id) || !Is(back.Arrives, s.Id)) p.Add($"{sw}: region {to.Id} roadsign {back.Id} must lead back here (Leads {r.Id}, Arrives {s.Id})");
            }
        }

        private static bool Inside(RegionDef r, float x, float y) => x >= 0 && y >= 0 && x <= r.Width && y <= r.Height;

        private static float Distance(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
