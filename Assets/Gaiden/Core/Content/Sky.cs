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
            foreach (var kv in s.Regions)
            {
                if (!db.Campaign.Themes.Exists(t => string.Equals(t.Id, kv.Key, StringComparison.OrdinalIgnoreCase)))
                    p.Add($"sky: region '{kv.Key}' is not a campaign theme");
                var r = kv.Value;
                if (r == null) { p.Add($"sky: region '{kv.Key}' is null"); continue; }
                if (r.Size <= 0f || r.Size > 1.5f) p.Add($"sky: region '{kv.Key}' Size {r.Size} must be in (0, 1.5]");
                if (r.X < 0f || r.X > 1f || r.Y < 0f || r.Y > 1.2f) p.Add($"sky: region '{kv.Key}' X/Y out of range (X 0-1, Y 0-1.2)");
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
