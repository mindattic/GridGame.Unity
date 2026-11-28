using System.Collections.Generic;

namespace GridGame.Core.Content
{
    public sealed class SpawnDef
    {
        public string Class;
        public int Level = 1;
        /// <summary>Optional fixed tile (0-based col); -1 = random free tile.</summary>
        public int X = -1;
        /// <summary>Optional fixed tile (0-based row, 0 = top); -1 = random free tile.</summary>
        public int Y = -1;
        /// <summary>Ticks after this wave starts before this entry spawns (§14.8). 0 = immediate, today's only behavior.</summary>
        public int DelayTicks;
        /// <summary>A finale boss's rule-break (§14.9, GG-T234); None = plays by the rules.</summary>
        public RuleBreak RuleBreak;
    }

    public sealed class WaveDef
    {
        /// <summary>An <see cref="EncounterGroupDef"/> id (encounters.json, GG-T184). When set, the loader fills
        /// <see cref="Enemies"/> from that group (its mix, formation tiles and level); leave Enemies empty then.</summary>
        public string Group;
        public List<SpawnDef> Enemies = new List<SpawnDef>();
        /// <summary>Guest allies met in this wave (§25.11): fight with you; standing at a victory (revived counts) and they join, down at the end and you'll meet them again (GG-T114).</summary>
        public List<SpawnDef> Guests = new List<SpawnDef>();
        /// <summary>Neutral recruitables met in this wave (§25.11.1, GG-T132): they stand aside until a hero talks them round.</summary>
        public List<RecruitableDef> Recruitables = new List<RecruitableDef>();
        /// <summary>Class id whose portrait/name show when this wave's first delayed reinforcement arrives (§14.8). Optional.</summary>
        public string SurpriseSpeaker;
        /// <summary>The line that speaker says. Null/empty = no dialogue beat, just the spawn (silent reinforcement).</summary>
        public string SurpriseLine;
        /// <summary>Gold banked into the battle's haul when this wave is cleared (the Crucible's per-wave pay, §22.10, GG-T430). 0 = none.</summary>
        public int ClearCoins;
    }

    /// <summary>One authored enemy group (GG-T184, encounters.json): a single-family mix of 1-5 foes, each on its
    /// own tile of the enemy half (the <see cref="Formation"/>), pitched at <see cref="Level"/>. Campaign waves
    /// reference groups by id (<see cref="WaveDef.Group"/>); the Crucible and endless encounters draw their waves
    /// from them (<see cref="EncounterCatalog"/>). No two groups share the same mix on the same tiles.</summary>
    public sealed class EncounterGroupDef
    {
        public string Id;
        public string Name;
        /// <summary>The formation's name (Pincer, Wedge, Crown ...): what the tiles in <see cref="Enemies"/> form.</summary>
        public string Formation;
        /// <summary>The campaign level the group is pitched at: a campaign wave spawns it at this level (still
        /// floored by the stage's recommended level), and the generators prefer groups at or under the wave's level.</summary>
        public int Level = 1;
        /// <summary>The foes and their tiles (Level and DelayTicks are ignored here: the group's Level applies).</summary>
        public List<SpawnDef> Enemies = new List<SpawnDef>();
    }

    public sealed class StageDef
    {
        public string Id;
        public string Name;
        public string Theme;
        public string Background;
        public string Music;
        public bool IsTest;
        /// <summary>Overrides the flattened-campaign-index-derived RecommendedLevel (Progression.RecommendedLevel)
        /// when set — for postgame/bonus stages appended after the main campaign, whose difficulty shouldn't keep
        /// climbing just because of where they sit in CampaignStages()'s flattened order (§7 frontier note).</summary>
        public int? LevelOverride;
        /// <summary>Physical blocks (rocks, pillars, wreckage): tiles no unit may enter, land on, be pushed into or spawn on (§1.1.1).</summary>
        public List<Coord> Blocks = new List<Coord>();
        public List<WaveDef> Waves = new List<WaveDef>();
        /// <summary>Region rule ids (regionrules.json, GG-T359) this stage plays under, on top of its theme's.</summary>
        public List<string> Rules = new List<string>();
        /// <summary>GG-T542: the tutorial battle's lesson set-ups: which foe a lesson brings on when its situation isn't on
        /// the board (a <see cref="TutorialSceneDef"/> per lesson id). Read only in a tutorial battle.</summary>
        public List<TutorialSceneDef> Scenes = new List<TutorialSceneDef>();
        /// <summary>GG-T1374: a fixed battle seed (the tutorial drills); null plays on the caller's seed.</summary>
        public int? Seed;
        /// <summary>GG-T1374: a drill's coach-mark lessons in order, each with the wave it waits for. Non-empty only on a
        /// drill (campaign.json <see cref="CampaignDef.Tutorial"/>).</summary>
        public List<StageLessonDef> Lessons = new List<StageLessonDef>();
        /// <summary>GG-T1641: a drill's intro card line, the heroes speaking to the Commander, shown before its first lesson.</summary>
        public string Intro;
        /// <summary>GG-T1958: a drill fields only the party's first N heroes (The Trap: two); null fields the whole party.</summary>
        public int? PartySize;
        /// <summary>GG-T2070: a scripted withdrawal (the prologue's first Tithe Collector fight): once due, the battle ends as
        /// <c>BattlePhase.Withdrawn</c>, neither won nor lost. Null = an ordinary battle.</summary>
        public WithdrawDef Withdraw;
    }

    /// <summary>GG-T2070: when a stage's scripted withdrawal comes, and the line the party says to the Commander as it falls
    /// back. Either trigger ends the battle; at least one is set (0 = unused).</summary>
    public sealed class WithdrawDef
    {
        /// <summary>Front blows clanged off an unyielding plate (<see cref="ClassDef.PlateUnyielding"/>) before the party falls back.</summary>
        public int AfterPlateClangs;
        /// <summary>Party turns ended before the party falls back.</summary>
        public int AfterTurns;
        /// <summary>The party to the Commander, shown on the battle's end and the Fall back card.</summary>
        public string Line;
    }

    /// <summary>GG-T1374: one lesson of a tutorial drill: its id (<see cref="Ids"/>), the wave (0-based) it waits for, and
    /// the role its marks focus on (Tank, Healer), when it points at one hero.</summary>
    public sealed class StageLessonDef
    {
        public string Id;
        public int Wave;
        public string FocusRole;

        /// <summary>The battle lessons (BattleScreen.Tutorial.cs), in their first-battle order.</summary>
        public static readonly string[] Ids =
        {
            "move", "pincer", "read", "shove", "hold", "orbs", "mana", "chain", "support", "spell", "buffs", "timeline",
            "taunt", "guard", "pickup", "interrupt", "plate", "ready",
        };
    }

    /// <summary>GG-T542: one lesson's set-up in the tutorial battle: the lesson id (chain, interrupt, plate ...) and the
    /// foe it brings on (spawned at the tile that makes the lesson's move possible).</summary>
    public sealed class TutorialSceneDef
    {
        public string Id;
        public string Class;
        public int Level = 1;
        /// <summary>The Pickups lesson's pickup id (pickups.json), dropped where a hero can walk onto it.</summary>
        public string Pickup;
    }

    /// <summary>
    /// A battle pickup (§13.3.1): a fallen enemy sometimes leaves one on the board; the first hero to step on it
    /// collects it. It fades after <see cref="BattleConfig.PickupLifetimeActions"/> player actions.
    /// </summary>
    public sealed class PickupDef
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>Relative chance among pickups when one drops.</summary>
        public int Weight = 1;
        /// <summary>Heals the collector by this fraction of its max HP.</summary>
        public float HealFraction;
        /// <summary>Orbs minted into the bank.</summary>
        public int Orbs;
        /// <summary>Timeline hasten for the collector (u).</summary>
        public float HastenU;
        /// <summary>Buff applied to the collector.</summary>
        public string ApplyBuff;
        public string Color;
        public string Icon;
    }

    public sealed class TrapDef
    {
        public string Id;
        public string Name;
        public int Damage;
        public string ApplyBuff;
        public float LayChance = 0.45f;
        /// <summary>Element of the trap's damage (default Poison).</summary>
        public DamageType DamageType = DamageType.Poison;
    }
}
