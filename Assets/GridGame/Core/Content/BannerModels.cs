using System;
using System.Collections.Generic;
using System.Linq;

namespace GridGame.Core.Content
{
    /// <summary>A summon banner's kind (BIBLE §25.10.3): Standard is always open, Featured runs for a window.</summary>
    public enum BannerKind { Standard, Featured }

    /// <summary>A summon banner (banners.json, GG-T1418, BIBLE §25.10.3). The Standard banner's pool is every summonable
    /// hero; a Featured banner brings its <see cref="Featured"/> heroes on <see cref="FeaturedShare"/> of its pulls, the rest
    /// a Standard pull. A Featured banner is either dated (<see cref="StartUtc"/> to <see cref="EndUtc"/>, once) or in the
    /// rotation (<see cref="Rotation"/> slot 1..n, each slot <see cref="Days"/> long from its StartUtc, the slots back to back
    /// and repeating).</summary>
    public sealed class BannerDef
    {
        public string Id;
        public string Name;
        public BannerKind Kind;
        /// <summary>The featured hero classes (each <see cref="Acquire.Summon"/>); empty on Standard.</summary>
        public List<string> Featured = new List<string>();
        /// <summary>The share of a Featured banner's pulls that bring a featured hero (0..1]; 0 on Standard.</summary>
        public float FeaturedShare;
        /// <summary>A dated banner's opening, or a rotation slot's first opening (UTC).</summary>
        public DateTime? StartUtc;
        /// <summary>A dated banner's close (UTC); null in the rotation and on Standard.</summary>
        public DateTime? EndUtc;
        /// <summary>The rotation slot, 1-based; 0 is a dated banner (or Standard).</summary>
        public int Rotation;
        /// <summary>How long a rotation slot runs, in days.</summary>
        public int Days;
        /// <summary>Hard pity: the Nth pull in a row without a new hero brings an unowned one (0 off).</summary>
        public int NewHeroPity;
        /// <summary>The Nth Featured pull without a featured hero brings one (0 off; Featured only).</summary>
        public int FeaturedPity;
        /// <summary>Every N pulls on this banner kind earn a Spark, a free pick of any hero on the banner (0 off).</summary>
        public int SparkPulls;
        /// <summary>Whether a pull can be paid in Summon Tickets (Tickets first).</summary>
        public bool AcceptsTickets;
        /// <summary>A x1 pull's price in Summon Tickets (0 when <see cref="AcceptsTickets"/> is off) and in Mana Shards.</summary>
        public int PullTickets;
        public int PullShards;
        /// <summary>How many pulls a multi-pull is, and its price; exactly <see cref="MultiCount"/> x1 pulls.</summary>
        public int MultiCount;
        public int MultiTickets;
        public int MultiShards;

        public bool IsRotating => Rotation > 0;
    }

    public static class BannerContent
    {
        public static void Validate(ContentDb db, List<string> p)
        {
            if (db.BannerOrder.Count == 0) return;
            if (db.BannerOrder.Count(b => b?.Kind == BannerKind.Standard) != 1) p.Add("banners: needs exactly one Standard banner");
            foreach (var b in db.BannerOrder)
            {
                if (b == null) { p.Add("banners: an empty row"); continue; }
                string at = $"banner {b.Id}";
                if (string.IsNullOrWhiteSpace(b.Name)) p.Add($"{at}: needs a Name");
                if (b.NewHeroPity < 0 || b.FeaturedPity < 0 || b.SparkPulls < 0 || b.Days < 0 || b.Rotation < 0
                    || b.PullTickets < 0 || b.PullShards < 0 || b.MultiTickets < 0 || b.MultiShards < 0) p.Add($"{at}: negative number");
                if (b.PullShards <= 0) p.Add($"{at}: needs a Shard price (PullShards)");
                if (b.AcceptsTickets != b.PullTickets > 0) p.Add($"{at}: PullTickets must be positive exactly when AcceptsTickets");
                if (b.MultiCount < 2) p.Add($"{at}: MultiCount must be at least 2");
                else if (b.MultiShards != b.PullShards * b.MultiCount || b.MultiTickets != b.PullTickets * b.MultiCount)
                    p.Add($"{at}: a x{b.MultiCount} must cost exactly {b.MultiCount} pulls (§25.10.3)");
                if (b.Kind == BannerKind.Standard)
                {
                    if (b.Featured.Count > 0 || b.FeaturedShare != 0f || b.FeaturedPity != 0) p.Add($"{at}: a Standard banner has no Featured heroes, share or pity");
                    if (b.StartUtc != null || b.EndUtc != null || b.Rotation != 0 || b.Days != 0) p.Add($"{at}: a Standard banner is always open (no dates or rotation)");
                    continue;
                }
                if (b.Featured.Count == 0) p.Add($"{at}: a Featured banner needs Featured heroes");
                if (b.Featured.Distinct(StringComparer.OrdinalIgnoreCase).Count() != b.Featured.Count) p.Add($"{at}: a Featured hero listed twice");
                foreach (var id in b.Featured)
                {
                    var c = db.TryClass(id);
                    if (c == null || c.Team != Team.Hero || c.IsCut || c.Acquire != Acquire.Summon)
                        p.Add($"{at}: Featured '{id}' must be an uncut hero class with Acquire Summon");
                }
                if (!(b.FeaturedShare > 0f && b.FeaturedShare <= 1f)) p.Add($"{at}: FeaturedShare must be above 0 and at most 1");
                if (b.StartUtc == null) p.Add($"{at}: a Featured banner needs StartUtc");
                if (b.IsRotating)
                {
                    if (b.EndUtc != null) p.Add($"{at}: a rotating banner has Days, not EndUtc");
                    if (b.Days <= 0) p.Add($"{at}: a rotating banner needs Days");
                }
                else
                {
                    if (b.Days != 0) p.Add($"{at}: a dated banner has EndUtc, not Days");
                    if (b.EndUtc == null || (b.StartUtc != null && b.EndUtc <= b.StartUtc)) p.Add($"{at}: a dated banner needs an EndUtc after its StartUtc");
                }
            }
            // The rotation: slots 1..n, back to back from slot 1's StartUtc.
            var slots = db.BannerOrder.Where(b => b != null && b.Kind == BannerKind.Featured && b.IsRotating).OrderBy(b => b.Rotation).ToList();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Rotation != i + 1) { p.Add($"banners: rotation slots must run 1..{slots.Count} once each"); break; }
                if (i > 0 && slots[i - 1].StartUtc is DateTime prev && slots[i].StartUtc is DateTime start && start != prev.AddDays(slots[i - 1].Days))
                    p.Add($"banner {slots[i].Id}: rotation slot {slots[i].Rotation} must start when slot {slots[i - 1].Rotation} ends");
            }
        }
    }
}
