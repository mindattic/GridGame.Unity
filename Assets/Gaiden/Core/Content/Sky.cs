using System;
using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>
    /// The staged, living backdrop's sky (GG-T576, Content/sky.json): back to front a backdrop is its sky plate, the
    /// ordinary Moon, the Somnolith (one canonical image in every scene, growing region by region), its drifting shards,
    /// then the foreground plate (the landscape with the sky cut out). Which backdrops have a sky to split is
    /// <see cref="BackdropLayerDef"/> (Content/backdroplayers.json, written by tools/split_backdrops.py).
    ///
    /// Placement is in screen terms so it holds at every aspect (9:23 to 3:4): X is across the screen (0 left, 1 right);
    /// Y is down the VISIBLE sky, 0 at the top of the screen and 1 where this backdrop's sky ends on screen
    /// (<see cref="BackdropLayerDef.SkyBottom"/>), so a body never lands in the ground; Size is the body's width as a
    /// share of the screen's width, capped at 0.6 of its height on squat screens.
    /// </summary>
    public sealed class SkyDef
    {
        public SkyBodyDef Somnolith = new SkyBodyDef();
        public SkyBodyDef Moon = new SkyBodyDef();
        public ShardsDef Shards = new ShardsDef();
        /// <summary>GG-T670: the wake of debris the Somnolith drags across the sky behind it.</summary>
        public DebrisDef Debris = new DebrisDef();
        /// <summary>GG-T670: the Aetherain, the occasional fall of aether dust from that wake.</summary>
        public AetherainDef Aetherain = new AetherainDef();
        public ParallaxDef Parallax = new ParallaxDef();
        /// <summary>The Somnolith per campaign theme id (it grows as the campaign goes on; Morrow's Shadow is the
        /// largest). A theme with no entry uses its base theme's (cityruins2 → cityruins), then <see cref="Somnolith"/>.</summary>
        public Dictionary<string, SkyRegionDef> Regions = new Dictionary<string, SkyRegionDef>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The Somnolith's placement for <paramref name="theme"/>: its region entry, else its base theme's, else
        /// the default body's own numbers.</summary>
        public SkyRegionDef RegionFor(string theme)
        {
            if (!string.IsNullOrEmpty(theme))
            {
                if (Regions.TryGetValue(theme, out var r)) return r;
                if (Regions.TryGetValue(Backdrops.BaseTheme(theme), out r)) return r;
            }
            return new SkyRegionDef { Size = Somnolith.Size, X = Somnolith.X, Y = Somnolith.Y };
        }

        /// <summary>The wake behind <paramref name="r"/>'s Somnolith: the region's own <see cref="SkyRegionDef.Debris"/>
        /// as written, else the default <see cref="Debris"/> with its Count scaled by how large the Somnolith is there
        /// (sparse while it's far off, heavy at Morrow's Shadow).</summary>
        public DebrisDef DebrisFor(SkyRegionDef r)
        {
            if (r?.Debris != null) return r.Debris;
            var d = Debris ?? new DebrisDef();
            float k = d.FullAtSize > 0f ? Math.Min(1f, Math.Max(0f, (r?.Size ?? Somnolith.Size) / d.FullAtSize)) : 1f;
            var scaled = d.Clone();
            scaled.Count = Math.Max(d.MinCount, (int)Math.Round(d.Count * k));
            if (r != null && r.DebrisAngle >= 0f) scaled.Angle = r.DebrisAngle;
            return scaled;
        }

        /// <summary>The chance (0-1) that a backdrop in region <paramref name="r"/> shows a dust fall: the region's own
        /// <see cref="SkyRegionDef.AetherainChance"/> when set (0 or more), else <see cref="AetherainDef.Chance"/> scaled by
        /// the Somnolith's size there, like the wake it falls from.</summary>
        public float AetherainChanceFor(SkyRegionDef r)
        {
            if (r != null && r.AetherainChance >= 0f) return Math.Min(1f, r.AetherainChance);
            var d = Aetherain ?? new AetherainDef();
            float full = Debris?.FullAtSize > 0f ? Debris.FullAtSize : 1f;
            float k = Math.Min(1f, Math.Max(0f, (r?.Size ?? Somnolith.Size) / full));
            return Math.Min(1f, d.Chance * k);
        }

        public static void Validate(ContentDb db, List<string> p)
        {
            var s = db.Sky;
            if (s == null) return;
            if (string.IsNullOrEmpty(s.Somnolith?.Image)) p.Add("sky: Somnolith needs an Image");
            if (string.IsNullOrEmpty(s.Moon?.Image)) p.Add("sky: Moon needs an Image");
            if (s.Shards != null && s.Shards.Count > 0 && (s.Shards.Images == null || s.Shards.Images.Count == 0))
                p.Add("sky: Shards has a Count but no Images");
            if (s.Shards != null && (s.Shards.MinSize <= 0 || s.Shards.MaxSize < s.Shards.MinSize))
                p.Add("sky: Shards MinSize must be > 0 and MaxSize >= MinSize");
            if (s.Debris != null) s.Debris.Validate(s, "sky: Debris", p);
            if (s.Aetherain != null)
            {
                var d = s.Aetherain;
                if (d.Chance < 0f || d.Chance > 1f) p.Add($"sky: Aetherain Chance {d.Chance} must be in [0, 1]");
                if (d.Motes < 0 || d.Motes > AetherainDef.MaxMotes) p.Add($"sky: Aetherain Motes {d.Motes} must be in [0, {AetherainDef.MaxMotes}]");
                if (d.FallSeconds <= 0f) p.Add("sky: Aetherain FallSeconds must be > 0");
                if (d.MinSize <= 0f || d.MaxSize < d.MinSize) p.Add("sky: Aetherain MinSize must be > 0 and MaxSize >= MinSize");
            }
            foreach (var kv in s.Regions)
            {
                if (!db.Campaign.Themes.Exists(t => string.Equals(t.Id, kv.Key, StringComparison.OrdinalIgnoreCase)))
                    p.Add($"sky: region '{kv.Key}' is not a campaign theme");
                var r = kv.Value;
                if (r == null) { p.Add($"sky: region '{kv.Key}' is null"); continue; }
                if (r.Size <= 0f || r.Size > 1.5f) p.Add($"sky: region '{kv.Key}' Size {r.Size} must be in (0, 1.5]");
                if (r.X < 0f || r.X > 1f || r.Y < 0f || r.Y > 1.2f) p.Add($"sky: region '{kv.Key}' X/Y out of range (X 0-1, Y 0-1.2)");
                if (r.BoardScrim < 0f || r.BoardScrim > 0.8f) p.Add($"sky: region '{kv.Key}' BoardScrim {r.BoardScrim} must be in [0, 0.8]");
                if (r.AetherainChance > 1f) p.Add($"sky: region '{kv.Key}' AetherainChance {r.AetherainChance} must be at most 1 (below 0 means 'derive from Size')");
                r.Debris?.Validate(s, $"sky: region '{kv.Key}' Debris", p);
                if (r.DebrisAngle >= 360f) p.Add($"sky: region '{kv.Key}' DebrisAngle {r.DebrisAngle} must be below 360 (below 0 means 'the default')");
            }
            foreach (var kv in db.BackdropLayers)
                if (kv.Value != null && kv.Value.Sky && (kv.Value.SkyBottom <= 0f || kv.Value.SkyBottom > 1f))
                    p.Add($"backdroplayers: '{kv.Key}' SkyBottom {kv.Value.SkyBottom} must be in (0, 1]");
        }
    }

    /// <summary>A sky body: its image (a Resources key) and default placement (see <see cref="SkyDef"/>).</summary>
    public sealed class SkyBodyDef
    {
        public string Image;
        /// <summary>True while the image is stand-in art awaiting the owner's.</summary>
        public bool Placeholder;
        public float Size = 0.1f;
        public float X = 0.5f;
        public float Y = 0.3f;
        /// <summary>Degrees, clockwise.</summary>
        public float Rotation;
        /// <summary>Opacity 0-1 (atmosphere).</summary>
        public float Opacity = 1f;
    }

    /// <summary>The Somnolith in one region.</summary>
    public sealed class SkyRegionDef
    {
        public float Size;
        public float X;
        public float Y;
        public float Rotation;
        /// <summary>GG-T665: opacity 0-1 of a dark backing behind the battle board in this region, so the grid and danger
        /// tiles read where a large Somnolith sits behind the board. 0 (the default) keeps the board see-through
        /// (owner direction 2026-10-11: the stage background reads clearly through the board).</summary>
        public float BoardScrim;
        /// <summary>GG-T670: this region's own wake, used as written (no size scaling); null = the default scaled by Size.</summary>
        public DebrisDef Debris;
        /// <summary>GG-T670: this region's wake direction (degrees, see <see cref="DebrisDef.Angle"/>), steering the wake
        /// into visible sky where the Somnolith sits near the middle of the screen; below 0 (the default) = the default Angle.</summary>
        public float DebrisAngle = -1f;
        /// <summary>GG-T670: chance 0-1 of a dust fall here; below 0 (the default) = the default Chance scaled by Size.</summary>
        public float AetherainChance = -1f;
    }

    /// <summary>GG-T670 (owner: "the Somnolith really needs to be dragging a lot of debris across the sky behind it"): a
    /// comet-like wake strung out from the Somnolith along <see cref="Angle"/>, densest and largest near the body and
    /// thinning and fading with distance, flowing slowly away from it. Pieces are the shard images at small sizes plus
    /// finer specks. Cosmetic, not deterministic.</summary>
    public sealed class DebrisDef
    {
        /// <summary>Upper bound on pieces, to keep the backdrop cheap on phones.</summary>
        public const int MaxCount = 90;
        /// <summary>Pieces at <see cref="FullAtSize"/> or larger (fewer for a smaller Somnolith).</summary>
        public int Count = 60;
        /// <summary>Fewest pieces even where the Somnolith is far off.</summary>
        public int MinCount = 12;
        /// <summary>Somnolith Size at which the wake is at its full Count.</summary>
        public float FullAtSize = 0.6f;
        /// <summary>Wake length as a share of the screen width, measured from the Somnolith's edge.</summary>
        public float Length = 0.75f;
        /// <summary>Direction of the wake from the Somnolith, degrees clockwise on screen from screen-right: 180 is
        /// straight left, a little over 180 tilts it up-left (the body coming down and in, trailing its wake behind).</summary>
        public float Angle = 192f;
        /// <summary>Half-width of the wake across its direction at its far end, as a share of the screen width; it is
        /// narrower near the body.</summary>
        public float Spread = 0.09f;
        /// <summary>Largest piece (near the body) and smallest (at the far end), widths as shares of the screen width.</summary>
        public float MaxSize = 0.05f;
        public float MinSize = 0.008f;
        /// <summary>Share of pieces drawn as fine specks (small dots) instead of shard images.</summary>
        public float SpeckShare = 0.45f;
        public string SpeckColor = "#6B4E33";
        /// <summary>Seconds a piece takes to drift from the body to the far end of the wake.</summary>
        public float DriftSeconds = 140f;
        public float Opacity = 0.85f;

        public DebrisDef Clone() => (DebrisDef)MemberwiseClone();

        public void Validate(SkyDef s, string w, List<string> p)
        {
            if (Count < 0 || Count > MaxCount) p.Add($"{w} Count {Count} must be in [0, {MaxCount}]");
            if (MinCount < 0 || MinCount > Count) p.Add($"{w} MinCount {MinCount} must be in [0, Count]");
            if (Length <= 0f || Length > 2f) p.Add($"{w} Length {Length} must be in (0, 2]");
            if (Spread < 0f || Spread > 0.5f) p.Add($"{w} Spread {Spread} must be in [0, 0.5]");
            if (MinSize <= 0f || MaxSize < MinSize) p.Add($"{w} MinSize must be > 0 and MaxSize >= MinSize");
            if (SpeckShare < 0f || SpeckShare > 1f) p.Add($"{w} SpeckShare {SpeckShare} must be in [0, 1]");
            if (DriftSeconds <= 0f) p.Add($"{w} DriftSeconds must be > 0");
            if (Opacity < 0f || Opacity > 1f) p.Add($"{w} Opacity {Opacity} must be in [0, 1]");
            if (SpeckShare < 1f && Count > 0 && (s.Shards?.Images == null || s.Shards.Images.Count == 0))
                p.Add($"{w}: shard pieces need sky Shards Images");
        }
    }

    /// <summary>GG-T670 (owner: "that's why it sometimes rains aether dust"; "Aetherain falls"): the Aetherain. Now and
    /// then a backdrop shows sparse,
    /// slow-falling glints of aether dust shaken loose from the Somnolith's wake. Rolled once per backdrop shown, with a
    /// chance that grows with the Somnolith's size (<see cref="SkyDef.AetherainChanceFor"/>). Cosmetic.</summary>
    public sealed class AetherainDef
    {
        public const int MaxMotes = 60;
        /// <summary>Chance of a dust fall where the Somnolith is at Debris.FullAtSize or larger.</summary>
        public float Chance = 0.3f;
        public int Motes = 28;
        /// <summary>Seconds a mote takes to fall from the wake to the bottom of the screen.</summary>
        public float FallSeconds = 16f;
        /// <summary>Mote diameter as a share of the screen width.</summary>
        public float MinSize = 0.004f;
        public float MaxSize = 0.009f;
        public string Color = "#F2D27A";
        public float Opacity = 0.85f;
    }

    /// <summary>Fragments of the Somnolith dragged slowly across the sky (cosmetic, not deterministic).</summary>
    public sealed class ShardsDef
    {
        public List<string> Images = new List<string>();
        public int Count = 7;
        /// <summary>Shard width as a share of the screen width.</summary>
        public float MinSize = 0.025f;
        public float MaxSize = 0.07f;
        /// <summary>Seconds the nearest (largest) shard takes to cross the screen; smaller ones are slower.</summary>
        public float CrossSeconds = 90f;
        /// <summary>Degrees per second a shard turns at most.</summary>
        public float Spin = 4f;
        public float Opacity = 0.9f;
    }

    /// <summary>The gentle idle drift that gives the layers depth: the foreground sways by Amplitude (a share of the
    /// screen width) over Seconds; farther layers move less.</summary>
    public sealed class ParallaxDef
    {
        public float Amplitude = 0.012f;
        public float Seconds = 36f;
    }

    /// <summary>One backdrop's split (tools/split_backdrops.py): whether it has a sky layer and how far down it reaches.</summary>
    public sealed class BackdropLayerDef
    {
        public bool Sky;
        /// <summary>How far down the image (0-1) the sky reaches.</summary>
        public float SkyBottom;
        public float SkyShare;
        /// <summary>Share of the frame the split dropped as painted moons/planets/floating rocks.</summary>
        public float Removed;
        /// <summary>Why a backdrop stays flat ("interior", a reject reason).</summary>
        public string Why;
    }
}
