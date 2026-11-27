using System;
using System.Collections.Generic;

namespace GridGame.Core.World
{
    /// <summary>Camp circle numbers as data (GG-T1695, BIBLE 28.5); lengths are art pixels.</summary>
    public sealed class CampConfig
    {
        public double CircleRadius = 120;
        public int HuntSpotCount = 3;
        public int ForageSpotCount = 4;

        /// <summary>Least distance between any two spots, hunt or forage.</summary>
        public double MinSpotSpacing = 24;

        /// <summary>Random tries per spot before it is given up.</summary>
        public int MaxTriesPerSpot = 30;

        public string DefaultHuntKind = HuntSpot.GameAnimals;
    }

    public sealed class HuntSpot
    {
        public const string GameAnimals = "GameAnimals";

        public string Id;
        public string Kind = GameAnimals;
        public double X, Y;
    }

    public sealed class ForageSpot
    {
        public string Id;
        public double X, Y;
    }

    /// <summary>A pitched camp circle with its hunt and forage spots (GG-T1695, 28.5). Pure and seeded.</summary>
    public sealed class CampSite
    {
        public double X, Y, Radius;
        public HuntSpot[] HuntSpots = new HuntSpot[0];
        public ForageSpot[] ForageSpots = new ForageSpot[0];

        public bool Contains(double x, double y)
        {
            double dx = x - X, dy = y - Y;
            return dx * dx + dy * dy <= Radius * Radius;
        }

        /// <summary>Pitches a camp at (x, y). Spots are placed by rejection sampling inside the circle on ground
        /// <paramref name="isWalkable"/> accepts, at least MinSpotSpacing apart; a spot with no room after the bounded
        /// tries is dropped, so poor ground yields fewer spots. The same seed gives the same camp.</summary>
        public static CampSite Pitch(double x, double y, int seed, Func<double, double, bool> isWalkable, CampConfig config = null)
        {
            config = config ?? new CampConfig();
            var rng = new Rng(seed);
            var placed = new List<double[]>();
            var site = new CampSite { X = x, Y = y, Radius = config.CircleRadius };

            var hunts = new List<HuntSpot>();
            for (int i = 0; i < config.HuntSpotCount; i++)
                if (TryPlace(site, rng, isWalkable, config, placed, out var p))
                    hunts.Add(new HuntSpot { Id = "hunt-" + i, Kind = config.DefaultHuntKind, X = p[0], Y = p[1] });

            var forage = new List<ForageSpot>();
            for (int i = 0; i < config.ForageSpotCount; i++)
                if (TryPlace(site, rng, isWalkable, config, placed, out var p))
                    forage.Add(new ForageSpot { Id = "forage-" + i, X = p[0], Y = p[1] });

            site.HuntSpots = hunts.ToArray();
            site.ForageSpots = forage.ToArray();
            return site;
        }

        static bool TryPlace(CampSite site, Rng rng, Func<double, double, bool> isWalkable, CampConfig config, List<double[]> placed, out double[] spot)
        {
            double minSq = config.MinSpotSpacing * config.MinSpotSpacing;
            for (int t = 0; t < config.MaxTriesPerSpot; t++)
            {
                double r = site.Radius * Math.Sqrt(rng.NextFloat());
                double a = rng.NextFloat() * 2 * Math.PI;
                double px = site.X + r * Math.Cos(a), py = site.Y + r * Math.Sin(a);
                if (!site.Contains(px, py) || !isWalkable(px, py)) continue;
                bool clear = true;
                foreach (var q in placed)
                {
                    double dx = px - q[0], dy = py - q[1];
                    if (dx * dx + dy * dy < minSq) { clear = false; break; }
                }
                if (!clear) continue;
                spot = new[] { px, py };
                placed.Add(spot);
                return true;
            }
            spot = null;
            return false;
        }
    }
}
