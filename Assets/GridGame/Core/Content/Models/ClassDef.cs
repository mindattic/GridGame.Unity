using System.Collections.Generic;

namespace GridGame.Core.Content
{
    // Plain data definitions deserialized from Assets/GridGame/Resources/Content/*.json.
    // Property names are the JSON keys (PascalCase); enums serialize as strings.

    /// <summary>Neutral (GG-T132): a recruitable bystander on the board — neither side fights it or counts it; a hero who
    /// meets its <see cref="RecruitableDef.Conditions"/> and talks to it flips it to Hero (a guest ally).</summary>
    public enum Team { Hero, Enemy, Neutral }

    /// <summary>GG-T1416: how a hero class comes into the roster. Starter: the default party
    /// (<c>Progression.StartingRoster</c>). Story: a dialogue JoinHero/RecruitHero names it. Wanderer: met on the road
    /// (<c>Wanderers.Pool</c>, so the Diamond draw too). Guest: an authored stage guest. Mercenary: hired (mercenaries.json).
    /// Recruit: an authored stage recruitable. Summon: the Summon Circle's gold pool. CampNpc: a Camp keeper wears the
    /// class's face (the Alchemist), so no recruit pool offers it. None = unset, which a hero class may not be.</summary>
    public enum Acquire { None, Starter, Story, Wanderer, Guest, Mercenary, Recruit, Summon, CampNpc }

    /// <summary>How an enemy chooses which hero to go after (bible §14.1). Auto derives it from INT and tags.</summary>
    public enum Targeting { Auto, Closest, Weakest, Threat, Squishy, Erratic, Caster }

    /// <summary>
    /// How an enemy moves and decides (bible §14.6, owner direction 2026-10-11: "ai for every single enemy unit, they are
    /// all different in their own unique ways"). Combined with its targeting personality and kit, every family plays
    /// differently.
    /// </summary>
    public enum Brain
    {
        Hunter, Pack, Ambusher, Skirmisher, Kiter, Guardian, Berserker, Scavenger, Trapper, Sluggish,
        Hopper, Blinker, Burrower, Bomber, Medic, Commander, Duelist, Stalker, Sentinel, Relentless,
        /// <summary>GG-T1777: game that always runs (the deer) - every step breaks contact, as a wounded foe's does.</summary>
        Flee,
    }

    public sealed class Stats
    {
        public float Strength;
        public float Vitality;
        public float Agility;
        public float Speed;
        public float Intelligence;
        public float Wisdom;
        public float Luck;

        public Stats Clone() => (Stats)MemberwiseClone();

        public static Stats operator +(Stats a, Stats b) => new Stats
        {
            Strength = a.Strength + b.Strength,
            Vitality = a.Vitality + b.Vitality,
            Agility = a.Agility + b.Agility,
            Speed = a.Speed + b.Speed,
            Intelligence = a.Intelligence + b.Intelligence,
            Wisdom = a.Wisdom + b.Wisdom,
            Luck = a.Luck + b.Luck,
        };

        public static Stats operator *(Stats a, float k) => new Stats
        {
            Strength = a.Strength * k,
            Vitality = a.Vitality * k,
            Agility = a.Agility * k,
            Speed = a.Speed * k,
            Intelligence = a.Intelligence * k,
            Wisdom = a.Wisdom * k,
            Luck = a.Luck * k,
        };
    }

    public sealed class MilestoneGrowth
    {
        public int Level;
        public Stats Growth = new Stats();
    }

    public sealed class Footprint
    {
        public int W = 1;
        public int H = 1;
    }

    /// <summary>
    /// Where a cut-in's "bust" close-up (BattleScreen.CutIns.ActionCard) should anchor on this class's
    /// full-body Portrait, as a 0-100 position on each axis (CSS background-position style: X 50/Y 0 is
    /// the default — horizontally centred, anchored to the top — which is right for a front-facing
    /// humanoid but wrong for an off-centre pose, e.g. a wolf sitting in profile with its head to one
    /// side). Null (the common case) means "use the default." Owner direction 2026-10-11: "make sure
    /// their eyes are in the portrait."
    /// </summary>
    public sealed class PortraitCrop
    {
        public float X = 50f;
        public float Y;
    }

    /// <summary>
    /// A unit's thumbnail framing: the head window onto its real portrait. Since GG-T713 (owner 2026-11-23, superseding
    /// GG-T220's "no separate thumbnail images") tools/make_thumbs.py bakes this window into a 256 px image per portrait
    /// (Resources/Thumbs/), which every small actor draw uses; the live window is only the fallback. Rerun
    /// make_thumbs.py (then --check) after changing a framing. (<see cref="X"/>, <see cref="Y"/>) is the portrait point, as fractions of its width and height from the top-left,
    /// that sits at the thumbnail's centre; <see cref="Zoom"/> is portrait width ÷ thumbnail side (1 = the whole width
    /// fills the square). Framed in GridGame.Studio's Thumbnails tab.
    /// </summary>
    public sealed class Thumbnail
    {
        public const float MinZoom = 0.25f, MaxZoom = 20f, MinOffset = -0.5f, MaxOffset = 1.5f;
        public float Zoom = 1f;
        public float X = 0.5f;
        public float Y = 0.5f;
    }

    /// <summary>
    /// GG-T352: where the figure sits on its portrait. Owner 2026-11-07: "all characters are 1024x1024 with transparent
    /// backgrounds; that's the rule, that's what the assets are", so a portrait is the whole canvas, margins included.
    /// This is the figure's box on it (the alpha bounds, written by GridGame.Studio/tools/import_art.py when the portrait is imported), as
    /// fractions of the portrait's width and height from its top-left. Whole-figure views (dialogue, cut-ins, cards,
    /// the death dissolve) draw only this box, so a figure reads at the same size whatever its canvas margins;
    /// thumbnails frame the whole canvas through <see cref="Thumbnail"/>.
    /// </summary>
    public sealed class Figure
    {
        public float X;
        public float Y;
        public float W = 1f;
        public float H = 1f;

        /// <summary>A box with some area, inside the portrait.</summary>
        public bool IsValid => W > 0f && H > 0f && X >= 0f && Y >= 0f && X + W <= 1.0001f && Y + H <= 1.0001f;
    }

    public sealed class BossPhaseDef
    {
        /// <summary>Phase begins when HP fraction drops to or below this (0..1).</summary>
        public float HpThreshold;
        public bool PrefersCharge;
        /// <summary>Ability fired once on phase entry (e.g. Quicken on self = enrage). Optional.</summary>
        public string OnEnterAbility;
        public string Announce;
    }

    /// <summary>What a supporter does when it joins a pincer (§1.2.3, GG-T187).</summary>
    public enum SupportEffect
    {
        /// <summary>Strike every trapped opponent at <see cref="SupportDef.Magnitude"/> × damage (the classic 50% support hit).</summary>
        Strike,
        /// <summary>Heal each target <see cref="SupportDef.Magnitude"/> × its max HP.</summary>
        Heal,
        /// <summary>Apply buff <see cref="SupportDef.Buff"/> to each target (ATK up, shield, regen, taunt…).</summary>
        Buff,
        /// <summary>Each target acts <see cref="SupportDef.Magnitude"/> seconds sooner (its turn icon slides forward, or its
        /// cast is rushed; a hero's party also gains tempo). Seconds since GG-T298 (was a fraction of the bar).</summary>
        Haste,
        /// <summary>Add <see cref="SupportDef.Magnitude"/> charge to the mana meter (100 = one orb). Hero team only.</summary>
        Mana,
        /// <summary>Strip every debuff from each target.</summary>
        Cleanse,
        /// <summary>GG-T298: the attacker the supporter is chained to gains +<see cref="SupportDef.Magnitude"/> crit chance
        /// (0.2 = +20 points) on its blows in this pincer. Resolves before the attackers strike.</summary>
        Crit,
        /// <summary>GG-T298: the supporter casts a random damaging spell from its own kit (no mana, no cast time) on every
        /// trapped opponent at <see cref="SupportDef.Magnitude"/> × its power. Nothing happens if its kit has none.</summary>
        Cast,
        /// <summary>GG-T298: each trapped opponent gains status <see cref="SupportDef.Buff"/> with chance
        /// <see cref="SupportDef.Magnitude"/> (poison, blind, slow, silence, burn…).</summary>
        Status,
        /// <summary>GG-T298: <see cref="SupportDef.Magnitude"/> extra seconds of Pushback on every trapped opponent, folded
        /// into the pincer's own push (one push per foe per action, capped at PushbackMaxSeconds).</summary>
        Pushback,
        /// <summary>GG-T834: a thrown follow-up (shuriken, poison dagger): a real hit on every trapped opponent at
        /// <see cref="SupportDef.Magnitude"/> x damage that, where it lands, inflicts <see cref="SupportDef.Buff"/> with
        /// chance <see cref="SupportDef.Chance"/>. Resolves in the Follow-up phase.</summary>
        Volley,
    }

    /// <summary>GG-T834: how a support's thrown projectile travels in playback.</summary>
    public enum SupportMotion { Arc, Straight, Spin }

    /// <summary>Who a supporter's effect lands on (§1.2.3, GG-T187). Strike, Cast, Status and Pushback always hit the
    /// trapped opponents; Crit always goes to the attacker the supporter is chained to.</summary>
    public enum SupportTarget
    {
        /// <summary>The pincer attacker the supporter's chain reaches.</summary>
        Attacker,
        /// <summary>Every ally on the chain from the supporter to that attacker (both ends included).</summary>
        Chain,
        /// <summary>Every living member of the supporter's team.</summary>
        Party,
        /// <summary>The supporter itself (taunt).</summary>
        Self,
    }

    /// <summary>A class's support action (§1.2.3, GG-T187): what it does when it joins a pincer as a supporter.
    /// Classes without one (every enemy) use <see cref="Default"/>, the classic 50% strike from beside an attacker.</summary>
    public sealed class SupportDef
    {
        public SupportEffect Effect = SupportEffect.Strike;
        /// <summary>Strike: damage fraction. Heal: fraction of the target's max HP. Haste: seconds sooner. Mana: charge.
        /// Crit: crit chance added (0.2 = +20 points). Cast: spell power fraction. Status: chance 0..1. Pushback: seconds.</summary>
        public float Magnitude = 0.5f;
        public SupportTarget Target = SupportTarget.Attacker;
        /// <summary>How many links along the chain of allies the supporter may stand from the attacker (1 = adjacent).</summary>
        public int Range = 1;
        /// <summary>Buff id applied by <see cref="SupportEffect.Buff"/>, or the status inflicted by <see cref="SupportEffect.Status"/>.</summary>
        public string Buff;
        /// <summary>Short name shown on the cut-in and float text ("Mend", "Guard", "Haste"…); defaults to the effect.</summary>
        public string Label;
        /// <summary>GG-T834: Volley's chance (0..1) to inflict <see cref="Buff"/> on each foe its hit lands on.</summary>
        public float Chance;
        /// <summary>GG-T834 presentation: the vfx.json effect the thrown projectile shows (null = code-drawn), how it
        /// travels, and the sound it plays (null = the effect's default cue).</summary>
        public string Projectile;
        public SupportMotion Motion = SupportMotion.Arc;
        public string Sfx;

        public static readonly SupportDef Default = new SupportDef();

        /// <summary>Largest Haste a support may give, in seconds (GG-T298).</summary>
        public const float MaxHasteSeconds = Battle.BattleConfig.SupportMaxHasteSeconds;
        /// <summary>Largest crit chance one Crit support may add (0.5 = +50 points), and the cap on the attacker's total crit
        /// percent while supported (GG-T298).</summary>
        public const float MaxCritBonus = Battle.BattleConfig.SupportMaxCritBonus;
        public const float MaxSupportedCritPercent = Battle.BattleConfig.SupportMaxSupportedCritPercent;
        /// <summary>Largest spell power fraction a Cast support may use (GG-T298).</summary>
        public const float MaxCastPower = Battle.BattleConfig.SupportMaxCastPower;

        public string DisplayLabel => string.IsNullOrEmpty(Label) ? Effect.ToString() : Label;
    }

    public sealed class ClassDef
    {
        public string Id;
        public string Name;
        public Team Team;
        /// <summary>GG-T1416: how a hero class is acquired; every hero class sets it (enemies leave it None).</summary>
        public Acquire Acquire;
        public List<string> Tags = new List<string>();
        public string Description;
        public string Lore;
        public string Card;
        /// <summary>GG-T557/GG-T561: the stem of this class's art under Resources/Portraits (<see cref="AssetNames"/>): a hero's
        /// female art (<c>acolyte-female</c>), an enemy's own name (<c>hag</c>, <c>harpy-raider</c>), or another class's
        /// stem when it draws that art (Monster Tamer → <c>wild-child-female</c>, placeholders → <c>mannequin</c>). The
        /// file is <see cref="PortraitFile"/>. Empty = the stem of the <see cref="Id"/>.</summary>
        public string Portrait;
        /// <summary>The stem of the male art (<c>acolyte-male</c>, GG-T532); null = the class has none yet.</summary>
        public string MalePortrait;
        /// <summary>Which numbered file of the stem(s) this class draws (<c>hag-02</c> = 2): one name's looks are numbered
        /// from 00, so a costume is another number here, not a code change. Default 0.</summary>
        public int PortraitVariant;
        /// <summary>Tools only: the GridGame2026 art key GridGame.Studio/tools/import_art.py imported this class's portrait from.</summary>
        public string ArtSource;
        public PortraitCrop Crop;
        public Thumbnail Thumbnail;
        /// <summary>The figure's box on the portrait (GG-T352).</summary>
        public Figure Figure;
        /// <summary>GG-T532: the thumbnail framing of the male art (<see cref="MalePortrait"/>); null = <see cref="Thumbnail"/>.</summary>
        public Thumbnail MaleThumbnail;
        /// <summary>GG-T532: the figure's box on the male art (GridGame.Studio/tools/import_art.py writes it); null = <see cref="Figure"/>.</summary>
        public Figure MaleFigure;
        public Footprint Footprint = new Footprint();
        public Stats BaseStats = new Stats();
        public Stats Growth = new Stats();
        public List<MilestoneGrowth> Milestones = new List<MilestoneGrowth>();
        public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>();
        /// <summary>GG-T2071 (BIBLE §3.3.1): a story foe whose resistances are the lesson (the prologue's Ward-Engine, "Immune
        /// to all but Lightning"): its inspect card, description panel and Bestiary page show them whatever the Bestiary tier.</summary>
        public bool RevealResistances;
        /// <summary>GG-T1134 (BIBLE §25.6.6.1): the class's own element (the six ninjas, the Night Hunter). An ability flagged
        /// <see cref="AbilityDef.ElementFromCaster"/> deals it and carries its rider (<see cref="AbilityDef.ForCaster"/>).
        /// Null = no element of its own: such an ability keeps its base numbers.</summary>
        public DamageType? Element;
        /// <summary>Abilities this class can learn/use (hero) or uses (enemy charge / trap).</summary>
        public List<string> Abilities = new List<string>();
        /// <summary>Default ability bar preset (ability ids; items as "item:id").</summary>
        public List<string> DefaultLoadout = new List<string>();
        public string DropTable;
        public int MoveRange = 1;
        /// <summary>GG-T891 (§1.1.3, §14.2): too heavy to be moved by anyone else - drag steps into it are refused, shoves,
        /// pulls, swaps and rule drags fizzle - unless it is Floating (Unit.IsHeavy).</summary>
        public bool Heavy;
        /// <summary>Trap id left on every tile this enemy steps off (slime trail, mire, quicksand — §14.4).</summary>
        public string Trail;
        /// <summary>Buff a landed enemy attack may inflict (venom, frost, blinding dirt — §14.4)…</summary>
        public string OnHitBuff;
        /// <summary>…with this chance per landed hit.</summary>
        public float OnHitChance;
        /// <summary>GG-T904: turns the on-hit buff lasts; 0 = the buff's own Duration (a tree golem's Root Grasp roots for 1).</summary>
        public int OnHitTurns;
        /// <summary>Class id of the remains this enemy leaves (gravestone, bone pile — §14.5). Destroy them or it rises again.</summary>
        public string Remains;
        /// <summary>Class id of the egg this enemy lays periodically (§14.8). Optional.</summary>
        public string LaysEgg;
        /// <summary>Lay an egg every Nth own turn (only used when LaysEgg is set).</summary>
        public int EggInterval = 4;
        /// <summary>Damage dealt to every adjacent hero when this enemy dies (cyber-zombie self-destruct). 0 = none.</summary>
        public int DeathBurst;
        /// <summary>Enemy targeting personality (§14.1).</summary>
        public Targeting Targeting = Targeting.Auto;
        /// <summary>Armor plate HP on the side it faces (§13.5); 0 = none. Physical blows from that side dent the plate, not the body.</summary>
        public int ArmorPlate;
        /// <summary>GG-T2069 (§13.5): the plate is unyielding: an ordinary blow from the plated side clangs for 0 and nothing
        /// dents it (Shredded's chip included); only an ability with <see cref="AbilityDef.ShattersPlate"/> takes it off.
        /// Needs an ArmorPlate.</summary>
        public bool PlateUnyielding;
        /// <summary>Movement / decision style (§14.6).</summary>
        public Brain Brain = Brain.Hunter;
        /// <summary>Straight-line attack reach for the Ranged archetype (1 = melee only).</summary>
        public int AttackRange = 1;
        /// <summary>Buff ids this class shrugs off ("Immune!") — the Mechanical archetype.</summary>
        public List<string> ImmuneBuffs = new List<string>();
        public int XpReward = 10;
        public List<BossPhaseDef> BossPhases = new List<BossPhaseDef>();
        /// <summary>Enemy charge spell id (Caster archetype). Optional.</summary>
        public string ChargeAbility;
        public ChargeShape ChargeShape = ChargeShape.Single;
        /// <summary>Trap id laid by trap-layers. Optional.</summary>
        public string Trap;
        /// <summary>Snake body segments grown behind this head at spawn (0 = not a snake).</summary>
        public int SnakeSegments;
        /// <summary>Hero role (§25.13): Tank, Healer, Mage, Striker or Support — what the class is for in a party.</summary>
        public string Role;
        /// <summary>Support action (§1.2.3, GG-T187) this class performs when chained to a pincer attacker; null = the
        /// default 50% strike from an adjacent tile (<see cref="SupportDef.Default"/>).</summary>
        public SupportDef Support;
        /// <summary>Hero family id (families.json, GG-T971, BIBLE §25.6.6): the loose collection of classes this one
        /// shares a fantasy and moves with. Hero-only; required for Team.Hero, left null for Team.Enemy.</summary>
        public string Family;
        /// <summary>Weapon classes (GG-T52) this class is trained in from level 1 — no proficiency passive
        /// needed. Every hero can always use <see cref="WeaponClass.Simple"/> regardless of this list; leave
        /// it empty for a class that starts Simple-only (e.g. a healer/support archetype).</summary>
        public List<WeaponClass> WeaponProficiencies = new List<WeaponClass>();
        /// <summary>Heaviest armor (GG-T52) this class can wear from level 1 without a proficiency passive.
        /// Defaults to Medium — the only weight actually gated is Heavy, reserved for the Tank archetype
        /// (Guardian) by default; every other class earns it through Heavy Armor Proficiency.</summary>
        public ArmorWeight ArmorProficiency = ArmorWeight.Medium;
        /// <summary>Bond a tamed monster of this class needs before it asks to join (§25.11.3, GG-T303); 0 = derived from its
        /// level (<c>Taming.BondFor</c>). Rarer, stronger monsters author more.</summary>
        public int TameBond;
        /// <summary>Region rules (GG-T359): rule, tile state, rule event or ability ids whose rule effects this class
        /// shrugs off (a flier over a crumbled floor, a water-born foe in the flood). Hooks don't fire for it either.</summary>
        public List<string> RuleImmune = new List<string>();
        /// <summary>Region rule effects (GG-T359) run where a unit of this class falls (it is their subject).</summary>
        public List<RuleEffectDef> OnDeathRule = new List<RuleEffectDef>();
        /// <summary>Hazard trails (GG-T370): region rule effects run on the tile this unit steps off (Self; it is their
        /// subject), every <see cref="TrailEvery"/>th step: a slime, fire or ice trail, a mine. A state it paints brings its
        /// rule into play on any stage.</summary>
        public List<RuleEffectDef> TrailRule = new List<RuleEffectDef>();
        /// <summary>Lays <see cref="TrailRule"/> every Nth step (1 = every step; a mine layer spaces its mines out).</summary>
        public int TrailEvery = 1;

        /// <summary>GG-T1037 (skill-hex, owner 2026-11-25): a hero class cut from the roster names the placed hero it becomes
        /// (Basher → Bruiser). A cut class leaves recruit pools, stage guests and drops; its Codex lore stays. Null = in play.</summary>
        public string CutInto;

        /// <summary>GG-T1037: true for a hero class cut from the roster (<see cref="CutInto"/> set).</summary>
        public bool IsCut => !string.IsNullOrEmpty(CutInto);

        public bool HasTag(string tag) => Tags != null && Tags.Contains(tag);

        /// <summary>True when the class has male art (<see cref="MalePortrait"/>).</summary>
        public bool HasMalePortrait => !string.IsNullOrEmpty(MalePortrait);

        /// <summary>The one portrait-file rule (GG-T557): the file under Resources/Portraits (no extension) this class
        /// draws, its male art when <paramref name="male"/> and it has one, at <paramref name="variant"/> (default its
        /// <see cref="PortraitVariant"/>): <c>acolyte-female-00</c>, <c>acolyte-male-00</c>, <c>hag-02</c>.</summary>
        public string PortraitFile(bool male = false, int? variant = null) =>
            AssetNames.File(male && HasMalePortrait ? MalePortrait : string.IsNullOrEmpty(Portrait) ? AssetNames.Stem(Id) : Portrait,
                variant ?? PortraitVariant);
    }
}
