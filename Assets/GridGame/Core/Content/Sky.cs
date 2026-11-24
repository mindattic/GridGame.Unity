using System;
using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>
    /// The staged, living backdrop's sky (GG-T576, Content/sky.json): back to front a backdrop is its sky plate, the
    /// ordinary Moon, the Manalith (one canonical image in every scene, growing region by region), its drifting shards,
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
        public SkyBodyDef Manalith = new SkyBodyDef();
        public SkyBodyDef Moon = new SkyBodyDef();
        public ShardsDef Shards = new ShardsDef();
        /// <summary>GG-T670: the wake of debris the Manalith drags across the sky behind it.</summary>
        public DebrisDef Debris = new DebrisDef();
        /// <summary>GG-T670: the Aetherain, the occasional fall of aether dust from that wake.</summary>
        public AetherainDef Aetherain = new AetherainDef();
        public ParallaxDef Parallax = new ParallaxDef();
        /// <summary>The Manalith per campaign theme id (it grows as the campaign goes on; Morrow's Shadow is the
        /// largest). A theme with no entry uses its base theme's (cityruins2 → cityruins), then <see cref="Manalith"/>.</summary>
        public Dictionary<string, SkyRegionDef> Regions = new Dictionary<string, SkyRegionDef>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The Manalith's placement for <paramref name="theme"/>: its region entry, else its base theme's, else
        /// the default body's own numbers.</summary>
        public SkyRegionDef RegionFor(string theme)
        {
            if (!string.IsNullOrEmpty(theme))
            {
                if (Regions.TryGetValue(theme, out var r)) return r;
                if (Regions.TryGetValue(Backdrops.BaseTheme(theme), out r)) return r;
            }
            return new SkyRegionDef { Size = Manalith.Size, X = Manalith.X, Y = Manalith.Y };
        }

        /// <summary>The wake behind <paramref name="r"/>'s Manalith: the region's own <see cref="SkyRegionDef.Debris"/>
        /// as written, else the default <see cref="Debris"/> with its Count scaled by how large the Manalith is there
        /// (sparse while it's far off, heavy at Morrow's Shadow).</summary>
        public DebrisDef DebrisFor(SkyRegionDef r)
        {
            if (r?.Debris != null) return r.Debris;
            var d = Debris ?? new DebrisDef();
            float k = d.FullAtSize > 0f ? Math.Min(1f, Math.Max(0f, (r?.Size ?? Manalith.Size) / d.FullAtSize)) : 1f;
            var scaled = d.Clone();
            scaled.Count = Math.Max(d.MinCount, (int)Math.Round(d.Count * k));
            // GG-T734: the fine grit thickens and the wake stretches further across the sky as the Manalith closes in.
            scaled.Grit = Math.Max(d.GritMinCount, (int)Math.Round(d.Grit * k));
            scaled.Length = d.Length * (d.FarLengthShare + (1f - d.FarLengthShare) * k);
            if (r != null && r.DebrisAngle >= 0f) scaled.Angle = r.DebrisAngle;
            return scaled;
        }

        /// <summary>GG-T734: how close the Manalith has come in region <paramref name="r"/>, 0 (far off) to 1 (at
        /// <see cref="DebrisDef.FullAtSize"/> or larger): its size there, which grows with story progress. Drives how hard
        /// it is visibly disintegrating (the 3D rig's shedding, the wake's grit and trail length).</summary>
        public float ApproachFor(SkyRegionDef r)
        {
            float full = Debris?.FullAtSize > 0f ? Debris.FullAtSize : 1f;
            return Math.Min(1f, Math.Max(0f, (r?.Size ?? Manalith.Size) / full));
        }

        /// <summary>The chance (0-1) that a backdrop in region <paramref name="r"/> shows a dust fall: the region's own
        /// <see cref="SkyRegionDef.AetherainChance"/> when set (0 or more), else <see cref="AetherainDef.Chance"/> scaled by
        /// the Manalith's size there, like the wake it falls from.</summary>
        public float AetherainChanceFor(SkyRegionDef r)
        {
            if (r != null && r.AetherainChance >= 0f) return Math.Min(1f, r.AetherainChance);
            var d = Aetherain ?? new AetherainDef();
            float full = Debris?.FullAtSize > 0f ? Debris.FullAtSize : 1f;
            float k = Math.Min(1f, Math.Max(0f, (r?.Size ?? Manalith.Size) / full));
            return Math.Min(1f, d.Chance * k);
        }

        /// <summary>A palette must name at least one colour, each "#RRGGBB" (GG-T674).</summary>
        public static void ValidateColors(List<string> colors, string w, List<string> p)
        {
            if (colors == null || colors.Count == 0) { p.Add($"{w}: needs at least one colour"); return; }
            foreach (var c in colors)
            {
                bool ok = c != null && c.Length == 7 && c[0] == '#';
                for (int i = 1; ok && i < 7; i++) ok = Uri.IsHexDigit(c[i]);
                if (!ok) p.Add($"{w}: '{c}' is not #RRGGBB");
            }
        }

        public static void Validate(ContentDb db, List<string> p)
        {
            var s = db.Sky;
            if (s == null) return;
            if (string.IsNullOrEmpty(s.Manalith?.Image)) p.Add("sky: Manalith needs an Image");
            if (string.IsNullOrEmpty(s.Moon?.Image)) p.Add("sky: Moon needs an Image");
            if (s.Shards != null && s.Shards.Count > 0 && (s.Shards.Images == null || s.Shards.Images.Count == 0))
                p.Add("sky: Shards has a Count but no Images");
            if (s.Shards != null && (s.Shards.MinSize <= 0 || s.Shards.MaxSize < s.Shards.MinSize))
                p.Add("sky: Shards MinSize must be > 0 and MaxSize >= MinSize");
            if (s.Debris != null) s.Debris.Validate(s, "sky: Debris", p);
            if (!string.IsNullOrEmpty(s.Moon?.Model) && s.Moon.Model != "procedural")
                p.Add($"sky: Moon Model '{s.Moon.Model}' must be 'procedural' (or left out for the flat image)");
            if (s.Parallax != null)
            {
                var px = s.Parallax;
                if (px.Amplitude < 0f || px.TiltAmplitude < 0f || px.Amplitude + px.TiltAmplitude > 0.08f)
                    p.Add("sky: Parallax Amplitude and TiltAmplitude must be >= 0 and add up to at most 0.08");
                if (px.TiltDegrees <= 0f || px.TiltSmoothSeconds <= 0f || px.TiltRecenterSeconds <= px.TiltSmoothSeconds)
                    p.Add("sky: Parallax TiltDegrees and TiltSmoothSeconds must be > 0, and TiltRecenterSeconds longer than TiltSmoothSeconds");
                var d = px.Depths;
                if (d != null)
                    foreach (var (name, v) in new[] { ("Sky", d.Sky), ("Moon", d.Moon), ("Manalith", d.Manalith),
                                 ("ShardsFar", d.ShardsFar), ("ShardsNear", d.ShardsNear), ("Foreground", d.Foreground) })
                        if (v < 0f || v > 1f) p.Add($"sky: Parallax Depths {name} {v} must be in [0, 1]");
            }
            if (s.Aetherain != null)
            {
                var d = s.Aetherain;
                if (d.Chance < 0f || d.Chance > 1f) p.Add($"sky: Aetherain Chance {d.Chance} must be in [0, 1]");
                if (d.Motes < 0 || d.Motes > AetherainDef.MaxMotes) p.Add($"sky: Aetherain Motes {d.Motes} must be in [0, {AetherainDef.MaxMotes}]");
                if (d.FallSeconds <= 0f) p.Add("sky: Aetherain FallSeconds must be > 0");
                if (d.MinSize <= 0f || d.MaxSize < d.MinSize) p.Add("sky: Aetherain MinSize must be > 0 and MaxSize >= MinSize");
                ValidateColors(d.Colors, "sky: Aetherain Colors", p);
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
        /// <summary>GG-T679: a 3D model drawn live in place of <see cref="Image"/> (the Image stays the fallback). For the
        /// Moon (GG-T681) the only model is "procedural": a cratered sphere built in code (MoonRig).</summary>
        public string Model;
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

    /// <summary>The Manalith in one region.</summary>
    public sealed class SkyRegionDef
    {
        public float Size;
        public float X;
        public float Y;
        public float Rotation;
        /// <summary>GG-T665: opacity 0-1 of a dark backing behind the battle board in this region, so the grid and danger
        /// tiles read where a large Manalith sits behind the board. 0 (the default) keeps the board see-through
        /// (owner direction 2026-10-11: the stage background reads clearly through the board).</summary>
        public float BoardScrim;
        /// <summary>GG-T670: this region's own wake, used as written (no size scaling); null = the default scaled by Size.</summary>
        public DebrisDef Debris;
        /// <summary>GG-T670: this region's wake direction (degrees, see <see cref="DebrisDef.Angle"/>), steering the wake
        /// into visible sky where the Manalith sits near the middle of the screen; below 0 (the default) = the default Angle.</summary>
        public float DebrisAngle = -1f;
        /// <summary>GG-T670: chance 0-1 of a dust fall here; below 0 (the default) = the default Chance scaled by Size.</summary>
        public float AetherainChance = -1f;
    }

    /// <summary>GG-T670 (owner: "the Somnolith really needs to be dragging a lot of debris across the sky behind it"): a
    /// comet-like wake strung out from the Manalith along <see cref="Angle"/>, densest and largest near the body and
    /// thinning and fading with distance, flowing slowly away from it. Pieces are the shard images at small sizes plus
    /// finer specks. Cosmetic, not deterministic.</summary>
    public sealed class DebrisDef
    {
        /// <summary>Upper bound on pieces, to keep the backdrop cheap on phones.</summary>
        public const int MaxCount = 90;
        /// <summary>Pieces at <see cref="FullAtSize"/> or larger (fewer for a smaller Manalith).</summary>
        public int Count = 60;
        /// <summary>Fewest pieces even where the Manalith is far off.</summary>
        public int MinCount = 12;
        /// <summary>Manalith Size at which the wake is at its full Count.</summary>
        public float FullAtSize = 0.6f;
        /// <summary>Wake length as a share of the screen width, measured from the Manalith's edge.</summary>
        public float Length = 0.75f;
        /// <summary>Direction of the wake from the Manalith, degrees clockwise on screen from screen-right: 180 is
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
        /// <summary>GG-T674: speck colours sampled from the Manalith art; each speck picks one (repeat a colour to
        /// weight it).</summary>
        public List<string> SpeckColors = new List<string> { "#2D346C", "#46508E", "#6B3C6A", "#974B72" };
        /// <summary>Seconds a piece takes to drift from the body to the far end of the wake.</summary>
        public float DriftSeconds = 140f;
        public float Opacity = 0.85f;
        /// <summary>GG-T734: the wake's length while the Manalith is far off, as a share of <see cref="Length"/>; it reaches
        /// the full Length at <see cref="FullAtSize"/> (default wake only; a region's own wake is used as written).</summary>
        public float FarLengthShare = 0.6f;

        /// <summary>GG-T734: upper bound on grit streaks, to keep the backdrop cheap on phones.</summary>
        public const int MaxGrit = 140;
        /// <summary>GG-T734: fine grit streaming off the Manalith, each a thin streak trailing behind it so the body reads as
        /// disintegrating: this many at <see cref="FullAtSize"/> or larger (fewer while it is far off).</summary>
        public int Grit = 90;
        /// <summary>Fewest grit streaks even where the Manalith is far off.</summary>
        public int GritMinCount = 14;
        /// <summary>A grit streak's thickness, as a share of the screen width (it thins as it drifts away).</summary>
        public float GritSize = 0.0032f;
        /// <summary>A grit streak's trail length at full approach, as a share of the screen width; a far-off Manalith's
        /// streaks are a third as long.</summary>
        public float GritTrail = 0.045f;
        /// <summary>Seconds a grit streak takes to stream from the body to the far end of the wake (faster than the
        /// bigger pieces, so the fine stuff visibly pours off).</summary>
        public float GritDriftSeconds = 55f;
        /// <summary>Grit colours: the wake's own speck colours plus the Manalith's glowing blue.</summary>
        public List<string> GritColors = new List<string> { "#46508E", "#46508E", "#6D6EA9", "#7590C8", "#7590C8", "#A096C7", "#974B72" };

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
            SkyDef.ValidateColors(SpeckColors, $"{w} SpeckColors", p);
            if (FarLengthShare <= 0f || FarLengthShare > 1f) p.Add($"{w} FarLengthShare {FarLengthShare} must be in (0, 1]");
            if (Grit < 0 || Grit > MaxGrit) p.Add($"{w} Grit {Grit} must be in [0, {MaxGrit}]");
            if (GritMinCount < 0 || (Grit > 0 && GritMinCount > Grit)) p.Add($"{w} GritMinCount {GritMinCount} must be in [0, Grit]");
            if (GritSize <= 0f || GritTrail < GritSize) p.Add($"{w} GritSize must be > 0 and GritTrail >= GritSize");
            if (GritDriftSeconds <= 0f) p.Add($"{w} GritDriftSeconds must be > 0");
            if (Grit > 0) SkyDef.ValidateColors(GritColors, $"{w} GritColors", p);
            if (SpeckShare < 1f && Count > 0 && (s.Shards?.Images == null || s.Shards.Images.Count == 0))
                p.Add($"{w}: shard pieces need sky Shards Images");
        }
    }

    /// <summary>GG-T670 (owner: "that's why it sometimes rains aether dust"; "Aetherain falls"): the Aetherain. Now and
    /// then a backdrop shows sparse,
    /// slow-falling glints of aether dust shaken loose from the Manalith's wake. Rolled once per backdrop shown, with a
    /// chance that grows with the Manalith's size (<see cref="SkyDef.AetherainChanceFor"/>). Cosmetic.</summary>
    public sealed class AetherainDef
    {
        public const int MaxMotes = 60;
        /// <summary>Chance of a dust fall where the Manalith is at Debris.FullAtSize or larger.</summary>
        public float Chance = 0.3f;
        public int Motes = 28;
        /// <summary>Seconds a mote takes to fall from the wake to the bottom of the screen.</summary>
        public float FallSeconds = 16f;
        /// <summary>Mote diameter as a share of the screen width.</summary>
        public float MinSize = 0.004f;
        public float MaxSize = 0.009f;
        /// <summary>GG-T674: mote colours from the Manalith art, mostly its blue with violet and red glints; each mote
        /// picks one (repeat a colour to weight it).</summary>
        public List<string> Colors = new List<string> { "#5D64A3", "#5D64A3", "#A096C7", "#B2A4D2", "#9D6492", "#974B72" };
        public float Opacity = 0.85f;
    }

    /// <summary>Fragments of the Manalith dragged slowly across the sky (cosmetic, not deterministic).</summary>
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

        /// <summary>GG-T681: each stage layer's depth, the share of the foreground's movement it makes (0 = fixed at
        /// infinity, 1 = moves with the foreground). Shards spread between ShardsFar and ShardsNear by their size.</summary>
        public StageDepthsDef Depths = new StageDepthsDef();

        /// <summary>GG-T682: the most a depth-1 layer moves for a full device tilt, as a share of the screen's width (up
        /// and down by the same number of pixels).</summary>
        public float TiltAmplitude = 0.018f;
        /// <summary>Degrees of tilt away from the held pose that reach the full TiltAmplitude (clamped beyond).</summary>
        public float TiltDegrees = 14f;
        /// <summary>Low-pass time constant (seconds) on the accelerometer, so the stage glides instead of jittering.</summary>
        public float TiltSmoothSeconds = 0.35f;
        /// <summary>Time constant (seconds) of the slow drift that re-centres the stage on however the device is held.</summary>
        public float TiltRecenterSeconds = 6f;
    }

    /// <summary>GG-T681: the layered stage's depths (see <see cref="ParallaxDef.Depths"/>).</summary>
    public sealed class StageDepthsDef
    {
        public float Sky = 0.12f;
        public float Moon = 0.18f;
        public float Manalith = 0.3f;
        public float ShardsFar = 0.4f;
        public float ShardsNear = 0.8f;
        public float Foreground = 1f;
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
