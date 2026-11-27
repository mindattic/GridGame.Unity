using System;

namespace GridGame.Core.World
{
    /// <summary>The kinds of battle the overworld starts (BIBLE 28.9); <see cref="SideStory"/> and <see cref="Story"/> keep their authored levels.</summary>
    public enum BattleKind { Ambush, Hunt, GameAnimals, WildBoss, Guardian, Defend, SideStory, Story }

    /// <summary>Everything the overworld knows when a battle starts (GG-T1700, 28.9).</summary>
    public struct DifficultyInput
    {
        /// <summary>The region's story order, 0..14.</summary>
        public int RegionIndex;
        public BattleKind Kind;

        /// <summary>Distance from the nearest road, in art pixels.</summary>
        public float DistanceFromRoad;

        /// <summary>True when the battle starts outside every camp and woken-waystone circle (the Wilds).</summary>
        public bool OutsideAllCircles;

        /// <summary>The danger gauge when the battle fired, 0..1.</summary>
        public float DangerAtTrigger;

        public bool IsNight;
        public WeatherKind Weather;
        public int PartyLevel;
        public int PartySize;

        /// <summary>How many times this area has been hunted.</summary>
        public int AreaHuntCount;

        /// <summary>Story stages cleared.</summary>
        public int StoryProgress;

        /// <summary>Woken waystones whose influence reaches this spot.</summary>
        public int NearbyWokenWaystones;
    }

    /// <summary>The library tier band to draw a battle from, the foes' level relative to the party, and the chance a foe is an elite.</summary>
    public struct DifficultyResult
    {
        public int TierMin;
        public int TierMax;
        public int LevelOffset;
        public float EliteChance;
    }

    /// <summary>Difficulty weights as data (GG-T1700). Pushes and eases are in library tiers; one tier is
    /// <see cref="LevelsPerTier"/> foe levels, so foes scale slowly as in the Crucible.</summary>
    public sealed class DifficultyConfig
    {
        /// <summary>Base tier added per region along the story (region 14 sits near the top of the library).</summary>
        public float TiersPerRegion = 0.6f;

        /// <summary>Base tier added per cleared story stage, up to <see cref="StoryProgressMaxTiers"/>.</summary>
        public float TiersPerStoryStage = 0.02f;
        public float StoryProgressMaxTiers = 0.5f;

        /// <summary>Width of the band above its lowest tier.</summary>
        public int BandWidth = 1;

        /// <summary>Push by battle kind.</summary>
        public float AmbushPush = 0.5f;
        public float HuntPush = 0f;
        public float GameAnimalsPush = 0f;
        public float WildBossPush = 2f;
        public float GuardianPush = 1.5f;
        public float DefendPush = 0.5f;

        /// <summary>Push for a battle in the Wilds, outside every circle.</summary>
        public float OutsideCirclesPush = 1f;

        /// <summary>Art pixels from the road per tier of push, capped at <see cref="DistanceMaxPush"/>.</summary>
        public float DistancePerTier = 400f;
        public float DistanceMaxPush = 1.5f;

        public float NightPush = 1f;

        /// <summary>Push at a full danger gauge; scales linearly with the gauge.</summary>
        public float FullDangerPush = 1f;

        /// <summary>Push by weather; the calm kinds push nothing.</summary>
        public float MistPush = 0.25f;
        public float RainPush = 0.5f;
        public float SnowPush = 0.5f;
        public float FogBankPush = 0.5f;
        public float StormPush = 0.75f;

        /// <summary>Push per hunt of the same area, capped at <see cref="HuntCountMaxPush"/>.</summary>
        public float PushPerAreaHunt = 0.1f;
        public float HuntCountMaxPush = 1f;

        /// <summary>Ease per nearby woken waystone, capped at <see cref="WaystoneMaxEase"/>.</summary>
        public float EasePerWaystone = 0.5f;
        public float WaystoneMaxEase = 1f;

        /// <summary>Party size below which the battle eases, and the ease per missing hero.</summary>
        public int FullPartySize = 3;
        public float EasePerMissingHero = 0.5f;

        /// <summary>Foe levels per tier of net push (the Crucible's level step).</summary>
        public float LevelsPerTier = 1f;
        public int MinLevelOffset = -2;
        public int MaxLevelOffset = 5;

        /// <summary>Elite chance at no net push, added per tier of net push, extra at night, and its cap.</summary>
        public float BaseEliteChance = 0.02f;
        public float EliteChancePerTier = 0.04f;
        public float NightEliteChance = 0.05f;
        public float MaxEliteChance = 0.4f;

        public float KindPush(BattleKind kind)
        {
            switch (kind)
            {
                case BattleKind.Ambush: return AmbushPush;
                case BattleKind.Hunt: return HuntPush;
                case BattleKind.GameAnimals: return GameAnimalsPush;
                case BattleKind.WildBoss: return WildBossPush;
                case BattleKind.Guardian: return GuardianPush;
                case BattleKind.Defend: return DefendPush;
                default: return 0f;
            }
        }

        public float WeatherPush(WeatherKind weather)
        {
            switch (weather)
            {
                case WeatherKind.Mist: return MistPush;
                case WeatherKind.Rain: return RainPush;
                case WeatherKind.Snow: return SnowPush;
                case WeatherKind.FogBank: return FogBankPush;
                case WeatherKind.Storm: return StormPush;
                default: return 0f;
            }
        }
    }

    /// <summary>The one Difficulty function (GG-T1700, BIBLE 28.9): the region sets the base tier, the Wilds, distance,
    /// night, danger, weather and repeat hunting push up, nearby woken waystones and a short party ease, and story kinds
    /// keep their authored level. Pure and deterministic; every output is clamped.</summary>
    public static class Difficulty
    {
        public const int RegionCount = 15;
        public const int MinTier = 1;
        public const int MaxTier = 10;

        public static bool IsStory(BattleKind kind) => kind == BattleKind.Story || kind == BattleKind.SideStory;

        public static DifficultyResult Evaluate(DifficultyInput input, DifficultyConfig config = null)
        {
            config = config ?? new DifficultyConfig();

            int region = Math.Max(0, Math.Min(RegionCount - 1, input.RegionIndex));
            float story = Math.Min(Math.Max(input.StoryProgress, 0) * config.TiersPerStoryStage, config.StoryProgressMaxTiers);
            float baseTier = MinTier + region * config.TiersPerRegion + story;

            if (IsStory(input.Kind))
            {
                var (storyMin, storyMax) = Band(baseTier, config);
                return new DifficultyResult { TierMin = storyMin, TierMax = storyMax, LevelOffset = 0, EliteChance = 0f };
            }

            float net = Push(input, config) - Ease(input, config);
            var (min, max) = Band(baseTier + net, config);

            int offset = (int)Math.Floor(net * config.LevelsPerTier + 0.5f);
            offset = Math.Max(config.MinLevelOffset, Math.Min(config.MaxLevelOffset, offset));
            if (input.PartyLevel > 0) offset = Math.Max(offset, 1 - input.PartyLevel);

            float elite = config.BaseEliteChance + net * config.EliteChancePerTier + (input.IsNight ? config.NightEliteChance : 0f);
            elite = Math.Max(0f, Math.Min(config.MaxEliteChance, elite));

            return new DifficultyResult { TierMin = min, TierMax = max, LevelOffset = offset, EliteChance = elite };
        }

        static float Push(DifficultyInput input, DifficultyConfig config)
        {
            float push = config.KindPush(input.Kind);
            if (input.OutsideAllCircles) push += config.OutsideCirclesPush;
            if (config.DistancePerTier > 0f && input.DistanceFromRoad > 0f)
                push += Math.Min(input.DistanceFromRoad / config.DistancePerTier, config.DistanceMaxPush);
            if (input.IsNight) push += config.NightPush;
            push += Clamp01(input.DangerAtTrigger) * config.FullDangerPush;
            push += config.WeatherPush(input.Weather);
            push += Math.Min(Math.Max(input.AreaHuntCount, 0) * config.PushPerAreaHunt, config.HuntCountMaxPush);
            return push;
        }

        static float Ease(DifficultyInput input, DifficultyConfig config)
        {
            float ease = Math.Min(Math.Max(input.NearbyWokenWaystones, 0) * config.EasePerWaystone, config.WaystoneMaxEase);
            int missing = config.FullPartySize - Math.Max(input.PartySize, 1);
            if (missing > 0) ease += missing * config.EasePerMissingHero;
            return ease;
        }

        static (int min, int max) Band(float tier, DifficultyConfig config)
        {
            int min = Math.Max(MinTier, Math.Min(MaxTier, (int)Math.Floor(tier)));
            int max = Math.Max(min, Math.Min(MaxTier, min + Math.Max(config.BandWidth, 0)));
            return (min, max);
        }

        static float Clamp01(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}
