using System;
using System.Collections.Generic;
using System.Globalization;

namespace GridGame.Core.Content
{
    /// <summary>
    /// A creature breed (GG-T1774, docs/research/sprite-recolor.md): the same drawing coloured another way, a Red, a Shadow
    /// or a rarer one, rolled by weight when a foe spawns. Looks only: Core stores the breed id on the unit
    /// (<see cref="Battle.Unit.Breed"/>) and never reads a colour; the App bakes the recolour (BreedArt, the
    /// <c>GridGame/BreedRecolor</c> shader) through the art's region mask (<c>&lt;file&gt;-mask.png</c>: R body, G secondary,
    /// B keep-as-is). Content/breeds.json, in file order.
    /// </summary>
    public sealed class BreedDef
    {
        public string Id;
        /// <summary>Relative roll weight (&gt; 0); adding a breed never forces a re-balance.</summary>
        public int Weight;
        /// <summary>The body region's ramp, dark to light, 2-4 <c>#RRGGBB</c> stops. Empty on both ramps = no recolour
        /// (<see cref="Breeds.Normal"/>).</summary>
        public List<string> BodyRamp = new List<string>();
        /// <summary>The secondary region's ramp (belly, fins, frills); empty uses <see cref="BodyRamp"/>.</summary>
        public List<string> SecondaryRamp = new List<string>();
        /// <summary>-1..1: the pixel's OKLab lightness is pushed toward white (&gt; 0) or black (&lt; 0) before the ramp lookup.</summary>
        public float LightLift;
        /// <summary>0..2: scales the ramp colour's chroma (1 = as written).</summary>
        public float ChromaScale = 1f;
        /// <summary>The keep region's tint (eyes, claws, teeth), shaded by the original pixel; white leaves it as drawn.</summary>
        public string KeepTint = "#FFFFFF";
        /// <summary>0..1: how far the keep region takes <see cref="KeepTint"/> (Shadow's cyan eye glints = 1).</summary>
        public float KeepMix;
        /// <summary>0..1: the recoloured regions' opacity (Spectral's translucency); 1 leaves alpha untouched.</summary>
        public float Alpha = 1f;
        /// <summary>The rare tier's second cue (research §6): a free tag such as <c>shimmer</c> or <c>glow</c>. Nothing draws
        /// it yet.</summary>
        public string Aura;

        /// <summary>No ramp at all: the art is drawn as it is.</summary>
        public bool IsPlain => (BodyRamp == null || BodyRamp.Count == 0) && (SecondaryRamp == null || SecondaryRamp.Count == 0);
    }

    /// <summary>GG-T1774: the breed roll and breeds.json's checks.</summary>
    public static class Breeds
    {
        /// <summary>The default breed: the art as drawn.</summary>
        public const string Normal = "normal";

        /// <summary>The breed a foe of <paramref name="def"/> spawns as, or null for the art as drawn. Only a class whose
        /// art has a region mask can roll one (<see cref="ContentDb.HasBreedMask"/>; unset, as in tests and tools, means
        /// none can), and never a boss or remains. The roll hashes the battle seed, unit id and class, so it is
        /// deterministic and does not touch the battle's <see cref="Rng"/> stream (replays stay identical).</summary>
        public static string Roll(ContentDb db, ClassDef def, int seed, int unitId)
        {
            if (db == null || def == null || db.BreedOrder.Count == 0 || db.HasBreedMask == null) return null;
            if (def.HasTag("Boss") || def.HasTag("Remains") || (def.BossPhases != null && def.BossPhases.Count > 0)) return null;
            if (!db.HasBreedMask(def.Id)) return null;
            long total = 0;
            foreach (var b in db.BreedOrder) if (b != null && b.Weight > 0) total += b.Weight;
            if (total <= 0) return null;
            long pick = (long)(Hash(seed, unitId, def.Id) % (ulong)total);
            foreach (var b in db.BreedOrder)
            {
                if (b == null || b.Weight <= 0) continue;
                if (pick < b.Weight) return b.IsPlain ? null : b.Id;
                pick -= b.Weight;
            }
            return null;
        }

        private static ulong Hash(int seed, int unitId, string classId)
        {
            ulong h = 1469598103934665603UL; // FNV-1a, then a SplitMix64 finish
            void Add(ulong v) { h ^= v; h *= 1099511628211UL; }
            Add((uint)seed); Add((uint)unitId);
            foreach (char ch in classId ?? "") Add(ch);
            h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 27; h *= 0x94D049BB133111EBUL; h ^= h >> 31;
            return h;
        }

        /// <summary>Parses <c>#RRGGBB</c> or <c>#RRGGBBAA</c> to 0..1 channels; false when malformed.</summary>
        public static bool TryParseHex(string hex, out float r, out float g, out float b)
        {
            r = g = b = 0f;
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || (hex.Length != 7 && hex.Length != 9)) return false;
            if (!uint.TryParse(hex.Substring(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
            if (hex.Length == 9 && !uint.TryParse(hex.Substring(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) return false;
            r = ((v >> 16) & 0xFF) / 255f; g = ((v >> 8) & 0xFF) / 255f; b = (v & 0xFF) / 255f;
            return true;
        }

        /// <summary>breeds.json: a <c>normal</c> breed, positive weights, 2-4 valid stops per ramp, scalars in range.</summary>
        public static void Validate(ContentDb db, List<string> p)
        {
            if (db.BreedOrder.Count == 0) return;
            if (!db.Breeds.ContainsKey(Normal)) p.Add($"breeds.json: no '{Normal}' breed (the art as drawn) (GG-T1774)");
            foreach (var b in db.BreedOrder)
            {
                string w = $"breed {b.Id}";
                if (b.Weight <= 0) p.Add($"{w}: Weight {b.Weight} must be positive (GG-T1774)");
                if (b.Id == Normal && !b.IsPlain) p.Add($"{w}: the normal breed has no ramps (GG-T1774)");
                if (b.Id != Normal && (b.BodyRamp == null || b.BodyRamp.Count == 0)) p.Add($"{w}: needs a BodyRamp (GG-T1774)");
                Ramp(b.BodyRamp, "BodyRamp");
                Ramp(b.SecondaryRamp, "SecondaryRamp");
                if (!TryParseHex(b.KeepTint, out _, out _, out _)) p.Add($"{w}: KeepTint '{b.KeepTint}' is not #RRGGBB (GG-T1774)");
                if (b.LightLift < -1f || b.LightLift > 1f) p.Add($"{w}: LightLift {b.LightLift} outside -1..1 (GG-T1774)");
                if (b.ChromaScale < 0f || b.ChromaScale > 2f) p.Add($"{w}: ChromaScale {b.ChromaScale} outside 0..2 (GG-T1774)");
                if (b.KeepMix < 0f || b.KeepMix > 1f) p.Add($"{w}: KeepMix {b.KeepMix} outside 0..1 (GG-T1774)");
                if (b.Alpha < 0f || b.Alpha > 1f) p.Add($"{w}: Alpha {b.Alpha} outside 0..1 (GG-T1774)");

                void Ramp(List<string> stops, string name)
                {
                    if (stops == null || stops.Count == 0) return;
                    if (stops.Count < 2 || stops.Count > 4) p.Add($"{w}: {name} has {stops.Count} stops, wants 2-4 (GG-T1774)");
                    foreach (var s in stops)
                        if (!TryParseHex(s, out _, out _, out _)) p.Add($"{w}: {name} stop '{s}' is not #RRGGBB (GG-T1774)");
                }
            }
        }
    }
}
