using System;
using System.Collections.Generic;

namespace GridGame.Core.World
{
    public enum WildEventKind { RecruitHero, WildBoss, Treasure, Bounty }

    /// <summary>An icon in the Wilds (GG-T1696, BIBLE 28.5). Payload is opaque: hero class id, boss family, treasure id or bounty id by Kind.</summary>
    public sealed class WildEvent
    {
        public string Id;
        public WildEventKind Kind;
        public double X, Y;
        public string Payload;
    }

    /// <summary>Wild event numbers as data; lengths are art pixels.</summary>
    public sealed class WildConfig
    {
        public int MinCount = 2;
        public int MaxCount = 5;

        /// <summary>Distance beyond the camp circle edge an event may sit.</summary>
        public double MinDistance = 40;
        public double MaxDistance = 400;

        public double RecruitHeroWeight = 1;
        public double WildBossWeight = 1;
        public double TreasureWeight = 2;
        public double BountyWeight = 1;

        /// <summary>Least distance between any two events.</summary>
        public double MinSpacing = 48;

        /// <summary>Random tries per event before it is given up.</summary>
        public int MaxTriesPerEvent = 40;

        public double WeightOf(WildEventKind kind)
        {
            switch (kind)
            {
                case WildEventKind.RecruitHero: return RecruitHeroWeight;
                case WildEventKind.WildBoss: return WildBossWeight;
                case WildEventKind.Treasure: return TreasureWeight;
                default: return BountyWeight;
            }
        }
    }

    /// <summary>Axis-aligned region rectangle events must stay inside.</summary>
    public readonly struct WildBounds
    {
        public readonly double MinX, MinY, MaxX, MaxY;
        public WildBounds(double minX, double minY, double maxX, double maxY) { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
        public bool Contains(double x, double y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    /// <summary>Seeded Wild event spawner (GG-T1696, 28.5). Pure.
    /// Despawn is simply dropping the returned list when camp breaks; nothing here holds state.</summary>
    public static class WildEvents
    {
        /// <summary>Spawns events in a ring beyond the camp circle, only where <paramref name="isWalkable"/> and
        /// <paramref name="outsideEveryCircle"/> (no camp or woken waystone circle) accept, inside <paramref name="bounds"/>,
        /// at least MinSpacing apart. A kind with no candidates (or zero weight) is never chosen; an event with no room after
        /// the bounded tries is dropped. The same seed gives the same events.</summary>
        public static List<WildEvent> Spawn(int seed, CampSite camp, Func<double, double, bool> isWalkable,
            Func<double, double, bool> outsideEveryCircle, IDictionary<WildEventKind, IList<string>> candidates,
            WildBounds bounds, WildConfig config = null)
        {
            config = config ?? new WildConfig();
            var events = new List<WildEvent>();
            if (camp == null || candidates == null) return events;
            var rng = new Rng(seed);

            var kinds = new List<WildEventKind>();
            foreach (WildEventKind k in Enum.GetValues(typeof(WildEventKind)))
                if (config.WeightOf(k) > 0 && candidates.TryGetValue(k, out var list) && list != null && list.Count > 0)
                    kinds.Add(k);
            if (kinds.Count == 0) return events;

            int count = config.MaxCount <= config.MinCount ? config.MinCount : rng.Range(config.MinCount, config.MaxCount + 1);
            double minSq = config.MinSpacing * config.MinSpacing;

            for (int i = 0; i < count; i++)
            {
                var kind = PickKind(rng, kinds, config);
                var pool = candidates[kind];
                string payload = pool[rng.Range(0, pool.Count)];
                for (int t = 0; t < config.MaxTriesPerEvent; t++)
                {
                    double r = camp.Radius + config.MinDistance + (config.MaxDistance - config.MinDistance) * rng.NextFloat();
                    double a = rng.NextFloat() * 2 * Math.PI;
                    double px = camp.X + r * Math.Cos(a), py = camp.Y + r * Math.Sin(a);
                    if (!bounds.Contains(px, py) || camp.Contains(px, py)) continue;
                    if (!outsideEveryCircle(px, py) || !isWalkable(px, py)) continue;
                    bool clear = true;
                    foreach (var e in events)
                    {
                        double dx = px - e.X, dy = py - e.Y;
                        if (dx * dx + dy * dy < minSq) { clear = false; break; }
                    }
                    if (!clear) continue;
                    events.Add(new WildEvent { Id = "wild-" + i, Kind = kind, X = px, Y = py, Payload = payload });
                    break;
                }
            }
            return events;
        }

        static WildEventKind PickKind(Rng rng, List<WildEventKind> kinds, WildConfig config)
        {
            double total = 0;
            foreach (var k in kinds) total += config.WeightOf(k);
            double roll = rng.NextFloat() * total;
            foreach (var k in kinds)
            {
                roll -= config.WeightOf(k);
                if (roll < 0) return k;
            }
            return kinds[kinds.Count - 1];
        }

        /// <summary>Events whose spot is revealed; those under fog stay hidden (28.5).</summary>
        public static List<WildEvent> Visible(IEnumerable<WildEvent> events, Func<double, double, bool> isRevealed)
        {
            var list = new List<WildEvent>();
            if (events == null) return list;
            foreach (var e in events)
                if (e != null && isRevealed(e.X, e.Y)) list.Add(e);
            return list;
        }
    }
}
