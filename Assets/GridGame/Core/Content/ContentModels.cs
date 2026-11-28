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


    public enum DamageType { Physical, Fire, Ice, Lightning, Poison, Holy, Dark, Arcane }

    /// <summary>The three ability kinds (owner design, GG-T296): Skill/Spell/Item are <i>Active</i> (you take them);
    /// <c>Passive</c> just happens (an always-on modifier, or an "every time I…" effect); <c>Reactive</c> fires after
    /// something happens <i>to</i> you, usually on a chance. Passive and Reactive abilities carry a
    /// <see cref="AbilityDef.Trigger"/> that the battle's trigger engine evaluates (§4.1).</summary>
    public enum AbilityKind { Skill, Spell, Item, Passive, Reactive }

    /// <summary>When a <see cref="TriggerDef"/> is evaluated (§4.1, GG-T296), fired by the battle funnels in a fixed order.</summary>
    public enum TriggerOn
    {
        /// <summary>Not an event: an always-on modifier (DamageMultiplier, ReductionMultiplier, Immunity), checked where the number is computed.</summary>
        Always,
        /// <summary>The owner makes a primary weapon attack (as a pincer attacker, or an enemy's melee swing), hit or miss. Context: Target.</summary>
        OnAttack,
        /// <summary>A weapon blow by the owner lands (any blow: pincer, supporter, counter, melee, ranged). Context: Target, Damage.</summary>
        OnLandedHit,
        /// <summary>The owner loses HP to anything (Damage &gt; 0; Attacker is null for a poison tick or a blast).</summary>
        OnDamaged,
        /// <summary>A foe's weapon blow from an adjacent tile lands on the owner (not a miss). Context: Attacker, Damage.</summary>
        OnAttackedDirect,
        /// <summary>Any foe's blow or ability lands on the owner, from any range (not a miss). Context: Attacker, Damage.</summary>
        OnAttackedAny,
        /// <summary>A foe's weapon blow misses the owner (the dodge hook GG-T297 builds on). Context: Attacker.</summary>
        OnDodge,
        /// <summary>At the owner's turn start, at least <see cref="TriggerDef.Count"/> foes stand adjacent.</summary>
        OnSurrounded,
        /// <summary>The owner gains status <see cref="TriggerDef.Status"/> (any status when empty).</summary>
        OnStatusGained,
        /// <summary>A hit drops the owner from at/above <see cref="TriggerDef.Threshold"/> of its HP to below it, and it lives.</summary>
        OnLowHp,
        /// <summary>An ally of the owner falls. Context: Target = the fallen ally, Attacker = its killer.</summary>
        OnAllyKO,
        /// <summary>The owner fells a foe. Context: Target = the fallen foe.</summary>
        OnKill,
        /// <summary>The owner was one of a pincer's two attackers, after their blows. Context: Target = a trapped foe.</summary>
        OnPincerAsAttacker,
        /// <summary>The owner supported a pincer, after its support action (GG-T298 builds supporter effects on it). Context: Target = the attacker it backed.</summary>
        OnPincerAsSupporter,
        /// <summary>The owner's turn begins (an enemy's own turn; for heroes, each time control returns to the party).</summary>
        TurnStart,
        /// <summary>The owner's turn ends (an enemy's own turn; for heroes, each time control returns to the party).</summary>
        TurnEnd,
        /// <summary>The owner just finished an action (the hero whose command it was; an enemy at the end of its turn).</summary>
        ActionEnd,
        /// <summary>GG-T862: a foe's blow (not a miss) is about to drop the owner to 0 HP, before it lands. Context: Attacker,
        /// Damage. A <see cref="TriggerEffectKind.Substitute"/> answering it takes the blow off the owner; it also fires
        /// from inside another Reactive's effects (a counter that would KO still meets the decoy).</summary>
        OnLethalBlow,
        /// <summary>GG-T796 (§2.13.2): a new round starts under UnmovedThisRound (every able hero has acted, or a new wave
        /// began), after the party turn's TurnStart. Fires for every living commanded hero; never in the other modes.</summary>
        RoundStart,
    }

    /// <summary>What a fired trigger does (§4.1, GG-T296).</summary>
    public enum TriggerEffectKind
    {
        /// <summary>Heal Who (default Self) Amount × its MaxHP.</summary>
        Heal,
        /// <summary>Heal the owner Amount × the damage in the context.</summary>
        Lifesteal,
        /// <summary>Apply <see cref="TriggerEffect.Buff"/> to Who (default Self).</summary>
        ApplyBuff,
        /// <summary>A weapon blow at ×Amount on Who (default the attacker), only when it's a foe within the owner's reach.</summary>
        Counterstrike,
        /// <summary>Strike the context's Target again at ×Amount of the blow that fired it (Double Attack).</summary>
        FollowUp,
        /// <summary>Resolve Active ability <see cref="TriggerEffect.Ability"/> through the normal dispatcher, instantly and free,
        /// at Who (default the attacker, else the target, else Self).</summary>
        CastAbility,
        /// <summary>Deal Amount × the context's damage back to the attacker (Thorns).</summary>
        Reflect,
        /// <summary>Remove every debuff from Who (default Self).</summary>
        Cleanse,
        /// <summary>Add Amount charge to the party's mana meter (hero owners only).</summary>
        ManaCharge,
        /// <summary>The Pushback keyword on Who (default the attacker, else the target): Amount seconds, foes only.</summary>
        Pushback,
        /// <summary>Always-on: the owner's weapon blows deal ×Amount (while the condition holds).</summary>
        DamageMultiplier,
        /// <summary>Always-on: damage the owner takes is ×Amount (while the condition holds; Condition.DamageType narrows it).</summary>
        ReductionMultiplier,
        /// <summary>Always-on: the owner can't gain any of <see cref="TriggerEffect.Statuses"/>.</summary>
        Immunity,
        /// <summary>GG-T862 (OnLethalBlow only): the blow strikes a decoy instead. The owner vanishes in a puff and reappears
        /// on a random free tile beside a living ally (the battle Rng, row-major candidates), leaving tile state
        /// <see cref="TriggerEffect.State"/> (the log) where it stood. No free tile beside an ally: it doesn't fire.</summary>
        Substitute,
    }

    /// <summary>Whom an effect lands on. Default picks the effect kind's natural target (see <see cref="TriggerEffectKind"/>).</summary>
    public enum EffectTarget { Default, Self, Target, Attacker }

    /// <summary>
    /// The data half of a Passive or Reactive ability (§4.1, GG-T296): <see cref="On"/> an event, roll <see cref="Chance"/>,
    /// check <see cref="Condition"/>, then run every <see cref="Effects"/> entry. Pure data validated by
    /// <see cref="ContentLoader.Validate"/>; the battle's trigger engine is the only code that reads it, so a new passive
    /// or reactive is a new abilities.json row (GG-LAW-5).
    /// </summary>
    public sealed class TriggerDef
    {
        public TriggerOn On;
        /// <summary>0..1; 1 fires every time without touching the Rng.</summary>
        public float Chance = 1f;
        /// <summary>Added to Chance per point of the owner's Luck (optional).</summary>
        public float ChancePerLuck;
        /// <summary>OnSurrounded: how many adjacent foes.</summary>
        public int Count;
        /// <summary>OnLowHp: the HP fraction (0..1) crossed.</summary>
        public float Threshold;
        /// <summary>OnStatusGained: the buff id (empty = any).</summary>
        public string Status;
        public TriggerCondition Condition;
        public List<TriggerEffect> Effects = new List<TriggerEffect>();
        /// <summary>Once fired, the owner's turns before it can fire again (0 = none).</summary>
        public int Cooldown;
        /// <summary>Fires at most once per action (a command, or one enemy turn).</summary>
        public bool OncePerAction;
        /// <summary>GG-T862: fires at most once per battle (the White Ninja's Substitution).</summary>
        public bool OncePerBattle;
        /// <summary>The float shown when it fires; empty = the ability's name.</summary>
        public string Callout;

        /// <summary>GG-T1134: a shallow copy with its own <see cref="Effects"/> list (an element variant swaps an effect).</summary>
        public TriggerDef Copy()
        {
            var t = (TriggerDef)MemberwiseClone();
            t.Effects = new List<TriggerEffect>(Effects ?? new List<TriggerEffect>());
            return t;
        }
    }

    /// <summary>A small predicate vocabulary (§4.1): every set field must hold. "Other" is the context's attacker, else its target.</summary>
    public sealed class TriggerCondition
    {
        /// <summary>Owner HP fraction strictly below this (0 = unchecked).</summary>
        public float SelfHpBelow;
        /// <summary>Owner HP fraction strictly above this (0 = unchecked).</summary>
        public float SelfHpAbove;
        public string HasStatus;
        public string LacksStatus;
        /// <summary>At least this many foes adjacent to the owner (0 = unchecked).</summary>
        public int AdjacentFoesAtLeast;
        /// <summary>The other unit carries this class tag (Undead, Beast…).</summary>
        public string OtherTag;
        /// <summary>GG-T338: the other unit carries this status (the Ember Executioner's blows on a [Burning] hero).</summary>
        public string OtherHasStatus;
        /// <summary>GG-T340: the other unit's HP fraction is strictly below this (0 = unchecked) — the Hellspawn's
        /// Bloodscent, its blows on a hero already bleeding.</summary>
        public float OtherHpBelow;
        /// <summary>GG-T897: OnAllyKO only - the ally that fell is of this family (its class DropTable row, one per
        /// family: "herd"). The Herd rages when one of its own goes down, not for the gangers that hired it.</summary>
        public string FallenFamily;
        /// <summary>The damage in the context is of this type (null = any).</summary>
        public DamageType? DamageType;
    }

    public sealed class TriggerEffect
    {
        public TriggerEffectKind Kind;
        /// <summary>Fraction, multiplier, seconds or charge, by kind.</summary>
        public float Amount;
        public EffectTarget Who;
        /// <summary>ApplyBuff: the buff id.</summary>
        public string Buff;
        /// <summary>CastAbility: the Active ability id.</summary>
        public string Ability;
        /// <summary>Immunity: the buff ids warded off.</summary>
        public List<string> Statuses = new List<string>();
        /// <summary>Substitute: the tile state (a regionrules.json TileStates id) left on the owner's old tile.</summary>
        public string State;

        /// <summary>GG-T1134: a shallow copy (an element variant rewrites <see cref="Buff"/>).</summary>
        public TriggerEffect Copy() => (TriggerEffect)MemberwiseClone();
    }

    /// <summary>GG-T1134 (BIBLE §25.6.6.1): one element's row of an <see cref="AbilityDef.ElementFromCaster"/> ability: what
    /// changes when a unit of that element uses it. Every field is optional; unset keeps the base ability's.</summary>
    public sealed class ElementVariantDef
    {
        /// <summary>The hit's base damage in this element.</summary>
        public int? BaseDamage;
        /// <summary>The element's rider: the buff the hit applies (Active; "" = none) or its trigger's ApplyBuff effects
        /// apply (Passive/Reactive).</summary>
        public string Buff;
        /// <summary>The rider's chance: ApplyBuffChance (Active) or the trigger's Chance (Passive/Reactive), in (0, 1].</summary>
        public float? Chance;
        /// <summary>The description a unit of this element reads.</summary>
        public string Description;
    }

    /// <summary>Wall (GG-T931): a line of 2 × Radius + 1 tiles through the anchor, square across the caster's line to it
    /// (<see cref="Rules.TargetResolver.Tiles"/>): a firewall painted in front of the caster.</summary>
    public enum TargetShape { Self, SingleActor, SingleTile, Square, Diamond, Cross, Plus, Row, Column, AllEnemies, AllAllies, Wall }

    public enum TargetMode { Auto, PickActor, PickTile }

    public enum TargetFilter { Any, EnemyOnly, AllyOnly, EmptyOnly }

    public enum ProjectileMotion { None, Straight, Bezier, Homing, Spiral, Twist, Strike }

    public enum BuffKind { Buff, Debuff }

    /// <summary>What happens when a buff is applied to a unit that already has it (bible §8.6).</summary>
    public enum BuffStacking { Refresh, Stack, Ignore, Upgrade }

    public enum DurationUnit { Turns, Ticks }

    /// <summary>Tome (GG-T301): teaches <see cref="ItemDef.TeachesAbility"/>, a Passive or Reactive, to a chosen hero.
    /// Chest (GG-T301): opened from the bag, it rolls <see cref="ItemDef.OpensTable"/> (a droptables.json row).</summary>
    public enum ItemType { Equipment, Consumable, CraftingMaterial, QuestItem, RecipeScroll, Tome, Chest }

    public enum ItemRarity { Junk, Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>
    /// Every equip slot (§24.2, GG-T89). Weapon and the five armor pieces (Head/Torso/Legs/Gloves/Boots) each
    /// hold exactly one exact-matching item (<see cref="ItemDef.Slot"/> == the slot). Necklace is the same
    /// (one exact slot). Earring1/Earring2 and Ring1/Ring2 are *instances* of a slot category: any item flagged
    /// <see cref="ItemDef.IsEarring"/>/<see cref="ItemDef.IsRing"/> fits either of its pair, the same way any
    /// <c>IsRelic</c> item used to fit any of the old Relic1-3.
    /// </summary>
    public enum EquipmentSlot { None, Weapon, Head, Torso, Legs, Gloves, Boots, Necklace, Earring1, Earring2, Ring1, Ring2 }

    /// <summary>A weapon's proficiency category (GG-T52). Every hero can always wield <c>Simple</c> weapons;
    /// <c>Martial</c> and <c>Arcane</c> are gated by <see cref="ClassDef.WeaponProficiencies"/> (the class's
    /// own home-tree training) or an earned proficiency-unlock passive (Martial Weapons / Arcane Focus).</summary>
    public enum WeaponClass { Simple, Martial, Arcane }

    /// <summary>An armor piece's weight class (GG-T52), ordered light-to-heavy so a hero's allowance can be
    /// compared with <![CDATA[<=]]>. Only <c>Heavy</c> is ever actually gated today — everyone can wear Light
    /// and Medium regardless of class (see <see cref="ClassDef.ArmorProficiency"/>); Heavy needs the class's
    /// own training or the Heavy Armor Proficiency passive.</summary>
    public enum ArmorWeight { Light, Medium, Heavy }

    public enum CraftStation { Forge, Alchemy, Enchant }

    public enum ChargeShape { Single, Line }

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

    public sealed class AbilityDef
    {
        public string Id;
        /// <summary>GG-T973 (BIBLE §25.6.6.1): an alias stub for an ability merged into another. A row with MergedInto
        /// carries only Id and MergedInto; <see cref="ContentLoader.Load"/> moves it out of <see cref="ContentDb.Abilities"/>
        /// into <see cref="ContentDb.AbilityAliases"/>, and <c>SaveMigrations.Migrate</c> rewrites the old id in a save to
        /// the surviving one. The target must be a live ability (no chains, no cycles) and no content may still name the alias.</summary>
        public string MergedInto;
        public string Name;
        public string Description;
        public AbilityKind Kind;
        /// <summary>Mana cost in generic orbs (0 for skills/items).</summary>
        public int Cost;
        public float CastTime;
        public int CooldownTurns;
        public TargetShape Shape = TargetShape.SingleActor;
        public int Radius;
        public TargetMode Mode = TargetMode.PickActor;
        public TargetFilter Filter = TargetFilter.EnemyOnly;
        public ProjectileMotion Motion = ProjectileMotion.None;
        public DamageType DamageType = DamageType.Arcane;
        public int BaseDamage;
        public int BaseHeal;
        public string ApplyBuff;
        public float ApplyBuffChance = 1f;
        public bool RemovesDebuffs;
        public bool StealsMana;
        public float HastenU;
        /// <summary>Party turns a hero haste from this ability keeps the party's tempo up (GG-T268); 0 = BattleConfig.DefaultTempoTurns.</summary>
        public int TempoTurns;
        public bool IsTeleport;
        /// <summary>Pole Vault (GG-T868, the Lancer): with <see cref="IsTeleport"/>, the pick is the free tile straight beyond a
        /// unit beside the caster, and the caster leaps over that unit onto it without entering the tile between
        /// (<c>Battle.PoleVault</c>). A Skill or Spell that picks an empty tile (SingleTile, PickTile, EmptyOnly).</summary>
        public bool Vaults;
        /// <summary>Tiles the target is shoved straight away from the caster (shield bash) — §6.3.</summary>
        public int Knockback;
        /// <summary>The pick must be cardinally adjacent to the caster (melee skills).</summary>
        public bool AdjacentOnly;
        /// <summary>Redirect Flow (GG-T887, the Monk): every debuff on the picked adjacent ally moves, with its remaining
        /// duration, onto the foe beside the caster (<c>Battle.RedirectFlow</c>). A Skill or Spell that picks one ally.</summary>
        public bool RedirectsDebuffs;
        /// <summary>Afterimage (GG-T867, the Speedster): a dash of up to this many tiles in a straight cardinal line to an empty
        /// tile, past anything in between, leaving an afterimage on the tile left (<c>Battle.Afterimage</c>). For the rest of
        /// that action the afterimage is a pincer end of the dasher's own (pincers from one hero); until the dasher's next turn
        /// it draws foes' attacks (Taunting), then fades. 0 = not a dash. A PickTile / EmptyOnly Skill.</summary>
        public int Afterimage;
        /// <summary>Swipe Boon (GG-T866, the Thief): one boon (Empowered, Ward, Protection, Blurred or Regen) leaves the picked
        /// adjacent foe and the caster wears it with its remaining duration (<c>Battle.SwipeBoon</c>). A Skill or Spell that
        /// picks one foe.</summary>
        public bool StealsBoon;
        /// <summary>The Pushback keyword (§2.3, GG-T286), in seconds: each foe it reaches acts this much later, wherever its
        /// icon sits on the bar (a held cast slips instead; never cancelled). Capped per action at
        /// <see cref="Battle.BattleConfig.PushbackMaxSeconds"/>, one push per foe per action. Plain hits never push.</summary>
        public float Pushback;
        /// <summary>GG-T318 (§2.12): extra seconds the unit spends off the timeline after the turn it used this, on top of
        /// <see cref="Battle.BattleConfig.RecoverBaseSeconds"/>. Heavy blows recover longer, so a foe's rhythm can be read.
        /// 0..<see cref="Battle.BattleConfig.RecoverMaxSeconds"/>.</summary>
        public float RecoverSeconds;
        /// <summary>GG-T332: a foe's siphon draws aether out of the party — when it lands on at least one hero, up to this
        /// many orbs leave the party's bank (the Bulwark's aether pack, a portable Still). Enemy charges only (`charge_`),
        /// 0..<see cref="MaxDrainsOrbs"/>.</summary>
        public int DrainsOrbs;
        public const int MaxDrainsOrbs = 3;
        /// <summary>Movement (GG-T183): each target is dragged up to this many tiles straight toward the caster, stopping
        /// beside it or at the first thing in the way. Bosses and snakes don't budge.</summary>
        public int Pull;
        /// <summary>Magnetize (GG-T878, the Tinkerer's spell): every plated or Tech-tagged foe within <see cref="Radius"/> of
        /// the picked tile is dragged up to this many tiles toward it, nearest first, and its plate swings to face it. A Heavy
        /// foe resists (unless Floating) and a Rooted one holds, but each still turns its plate (<c>Battle.Magnetize</c>).</summary>
        public int Magnetizes;
        /// <summary>Smoke Vanish (GG-T861, the Black Ninja): the ability's <see cref="RuleEffects"/> (a smoke cloud) are laid
        /// around the caster's own tile first, then the caster vanishes and reappears on a random free, safe tile at least 2
        /// away (the battle Rng; <c>Battle.SmokeVanish</c>), auto-resolving any pincer it lands in like Teleport, and every
        /// foe's charge locked on it re-plans. A Rooted caster still drops the smoke but can't blink. A Self / Auto Skill.</summary>
        public bool Vanishes;
        /// <summary>Movement (GG-T183): the caster and its (one-tile) target trade tiles.</summary>
        public bool SwapsPlaces;
        /// <summary>GG-T865: a PickActor ability may only pick a unit within this many tiles (Manhattan, nearest footprint
        /// tile) of the caster; 0 = anywhere. The Assassin's Shadow Swap trades places with a foe within 3; a ranged swap
        /// is a blink, so like a Teleport it scans the party's pincers once it lands.</summary>
        public int MaxRange;
        /// <summary>Movement (GG-T183): once it resolves, the caster steps up to this many tiles straight away from the
        /// nearest foe (Disengage, Hit and Run).</summary>
        public int StepBack;
        /// <summary>Random WUBRG orbs added to the bank on resolve (ethers).</summary>
        public int GrantsOrbs;
        /// <summary>Removes just this buff id (Eye Drops → blinded).</summary>
        public string RemovesBuff;
        /// <summary>Brings a fallen ally back onto a free tile beside the caster with BaseHeal HP.</summary>
        public bool Revives;
        /// <summary>Ends the battle as an escape (no rewards, no defeat penalty).</summary>
        public bool Escape;
        /// <summary>Damage ×BonusMultiplier against targets with this tag (Holy Water vs Undead).</summary>
        public string BonusVsTag;
        public float BonusMultiplier = 1f;
        /// <summary>GG-T1220 (the Rootbreaker's payoff): a foe already wearing any of these buff ids when the ability lands
        /// takes damage x<see cref="BonusVsBuffsMultiplier"/> and is pushed <see cref="BonusVsBuffsPushback"/> seconds more
        /// (on top of <see cref="Pushback"/>, still one push per foe per action). Checked before this ability lays its own
        /// <see cref="ApplyBuff"/>, so it never pays off its own status.</summary>
        public List<string> BonusVsBuffs = new List<string>();
        public float BonusVsBuffsMultiplier = 1f;
        public float BonusVsBuffsPushback;
        /// <summary>GG-T879: only units whose class carries this tag are caught (the Technician's EMP Pulse: "Tech" - gremlins,
        /// cyber zombies, mecha, prototypes); everyone else in the shape is untouched. <c>TargetResolver.Collect</c> applies it,
        /// so a pick that catches no such unit (and reaches no tile its reactions work on) is no pick at all.</summary>
        public string OnlyVsTag;
        /// <summary>GG-T879: seconds each caught foe's held cast slips (a <c>CastStaggeredEvent</c>, no Wisdom shrug); the slip
        /// counts toward the interrupt like a blow's stagger, so a cast pushed past its own length is cancelled.</summary>
        public float CastStagger;
        public string Icon;
        public string CastVfx;
        public string ProjectileVfx;
        public string ImpactVfx;
        public string LingerVfx;
        /// <summary>Optional VFX tier (GG-T253): Minor, Major or Ultimate; empty derives it from cost and cooldown (VfxStyleSheet.TierOf).</summary>
        public string Intensity;
        public string Sfx;
        /// <summary>Class-agnostic Skill: learnable by any hero regardless of class, like every Spell already is
        /// (<see cref="GridGame.Meta.MetaServices.Abilities.Learnable"/>). Ignored on Spell/Passive, which already
        /// have their own universal/owning-class rules.</summary>
        public bool Universal;
        /// <summary>Passive/Reactive only (GG-T296): when and how it fires (§4.1). Null on Active abilities and on
        /// meta-only passives (the proficiency unlocks).</summary>
        public TriggerDef Trigger;
        /// <summary>Passive/Reactive only (GG-T301): who a Tome may teach it to, as hero class ids and/or Roles
        /// (Tank/Healer/Mage/Striker/Support/Ranged). Empty = any hero.</summary>
        public List<string> LearnableBy = new List<string>();
        /// <summary>The Tame effect (§25.11.3, GG-T303): base chance (0..1) to turn a tameable foe — a non-Sentient, non-boss
        /// monster — to the party's side for <see cref="TameTurns"/> of its own turns. 0 = not a tame. Any Skill or Item
        /// ability may carry it (the Monster Tamer's Tame, the Dog Whistle); <c>Taming.Chance</c> scales it by the foe's
        /// HP left, the level gap and the user's Luck/Wisdom.</summary>
        public float Tame;
        /// <summary>Own turns a successful <see cref="Tame"/> lasts before the monster breaks free (a full bond keeps it).</summary>
        public int TameTurns;
        /// <summary>Essence (GG-T333, the Herald): once the heal has landed, the caster pays this share of its own max HP
        /// (at least 1 HP, never its last one). 0 = free. A heal only (BaseHeal &gt; 0, AllyOnly), 0..<see cref="MaxLifeCost"/>.</summary>
        public float LifeCost;
        public const float MaxLifeCost = 0.5f;
        /// <summary>GG-T337 (Aether Pour, the Sister's since GG-T570): an orb grant that rides a per-target effect (a heal, a hit, a buff) instead of
        /// being the whole ability (Meditate, an ether). It resolves on its targets first, then pours
        /// <see cref="GrantsOrbs"/> into the bank.</summary>
        public bool PoursOrbs => GrantsOrbs > 0 && (BaseHeal > 0 || BaseDamage > 0 || ApplyBuff != null);
        /// <summary>Countermarch (GG-T336, the Tactician): the picked foe's turn icon trades places on the timeline with the
        /// foe whose turn is furthest off (<see cref="Battle.Countermarch"/>). A picked single foe from a Skill or Spell.</summary>
        public bool SwapsTurns;
        /// <summary>Echo (GG-T388: the Tactician's Encore, GG-T531, and the Mocking Echo): picks one unit and repeats the last Active ability that unit
        /// used this battle, cast by the echoer from where it stands (<see cref="Battle.Echo"/>): at the same target when it
        /// can reach it, else the nearest legal one. A copied spell still costs its orbs from the party's bank; a copied
        /// skill rides the Echo's own cooldown. Nothing else rides an Echo (no damage, heal or buff of its own).</summary>
        public bool Echoes;
        /// <summary>GG-T371: a tool's tile reaction set (elements.json Reactions: "disarm", "cleanse") applied to every tile it
        /// covers; a hero ability that deals damage also carries its element's own Tiles reactions.</summary>
        public string TileReaction;
        /// <summary>Region rule effects (GG-T359) run once this resolves: the caster is their subject and the anchor
        /// their Target (mark/clear/modify tiles, queue or retime events, move anchors, raise flags).</summary>
        public List<RuleEffectDef> RuleEffects = new List<RuleEffectDef>();

        /// <summary>GG-T1134 (BIBLE §25.6.6.1, the ninja family moves): the ability takes its caster class's
        /// <see cref="ClassDef.Element"/>. An Active one deals that <see cref="DamageType"/>; the element's row in
        /// <see cref="ElementVariants"/> then sets its numbers and rider (the status its hit, or its trigger's ApplyBuff,
        /// applies). A caster with no element keeps the base ability; an element with no row keeps the base numbers and
        /// rider but deals the element. Resolved by <see cref="ForCaster"/>.</summary>
        public bool ElementFromCaster;
        /// <summary>GG-T1134: per-element overrides for an <see cref="ElementFromCaster"/> ability, keyed by element.</summary>
        public Dictionary<DamageType, ElementVariantDef> ElementVariants = new Dictionary<DamageType, ElementVariantDef>();

        private Dictionary<DamageType, AbilityDef> elementCache;

        /// <summary>GG-T1134: this ability as a unit of class <paramref name="caster"/> uses it: itself, unless it is
        /// <see cref="ElementFromCaster"/> and the class has an <see cref="ClassDef.Element"/>.</summary>
        public AbilityDef ForCaster(ClassDef caster) => ForElement(caster?.Element);

        /// <summary>GG-T1134: this ability in <paramref name="element"/> (see <see cref="ElementFromCaster"/>): a cached copy
        /// with the element's damage type, numbers and rider and the same <see cref="Id"/>, so cooldowns, bars and saves are
        /// unchanged. Itself when it doesn't take an element or <paramref name="element"/> is null.</summary>
        public AbilityDef ForElement(DamageType? element)
        {
            if (!ElementFromCaster || element == null) return this;
            var e = element.Value;
            lock (this)
            {
                if (elementCache == null) elementCache = new Dictionary<DamageType, AbilityDef>();
                if (elementCache.TryGetValue(e, out var hit)) return hit;
                var copy = (AbilityDef)MemberwiseClone();
                copy.elementCache = null;
                copy.ElementFromCaster = false; // resolved: a second ForCaster leaves it as it is
                copy.DamageType = e;
                if (ElementVariants != null && ElementVariants.TryGetValue(e, out var v) && v != null)
                {
                    if (v.BaseDamage != null) copy.BaseDamage = v.BaseDamage.Value;
                    if (!string.IsNullOrEmpty(v.Description)) copy.Description = v.Description;
                    if (Trigger == null)
                    {
                        if (v.Buff != null) copy.ApplyBuff = v.Buff.Length == 0 ? null : v.Buff;
                        if (v.Chance != null) copy.ApplyBuffChance = v.Chance.Value;
                    }
                    else
                    {
                        var t = Trigger.Copy();
                        if (v.Chance != null) t.Chance = v.Chance.Value;
                        if (!string.IsNullOrEmpty(v.Buff))
                            for (int i = 0; i < t.Effects.Count; i++)
                                if (t.Effects[i] != null && t.Effects[i].Kind == TriggerEffectKind.ApplyBuff)
                                {
                                    var fx = t.Effects[i].Copy();
                                    fx.Buff = v.Buff;
                                    t.Effects[i] = fx;
                                }
                        copy.Trigger = t;
                    }
                }
                elementCache[e] = copy;
                return copy;
            }
        }

        /// <summary>GG-T590 (§25.6.5, GG-T168): the stat-threshold vector a hero must reach before the Learn screen will sell
        /// it this ability (Fireball: INT 8; a hybrid such as Paladin Bulwark: VIT 5 and WIS 4). Each non-zero stat is a
        /// minimum and every one must be met; a zero stat doesn't gate. It is measured against the stat points the hero has
        /// spent on level-ups (GG-T591's one-free-point-per-level trail), not its effective stats, so it reads on the same
        /// 0-up scale for every class. Null, or all zero, = no requirement. Authored for every Learn-screen ability
        /// (hero-usable Skills and Spells); passives, reactives, items and enemy charges leave it null.</summary>
        public Stats Requires;

        /// <summary>Does this ability carry a stat requirement at all (<see cref="Requires"/> set with a non-zero stat)?</summary>
        public bool HasRequires => Requires != null && RequiredStats().Count > 0;

        /// <summary>The non-zero stats in <see cref="Requires"/>, in the game's usual stat order, as (short label, full
        /// stat name, minimum): <c>("INT", "Intelligence", 8)</c>. Empty when there is no requirement.</summary>
        public List<(string Abbrev, string Stat, float Min)> RequiredStats()
        {
            var list = new List<(string, string, float)>();
            if (Requires == null) return list;
            foreach (var (abbrev, stat, get) in RequirementStats)
            {
                var min = get(Requires);
                if (min > 0f) list.Add((abbrev, stat, min));
            }
            return list;
        }

        /// <summary>True when <paramref name="points"/> (the hero's spent level-up stat points) meet every stat in
        /// <see cref="Requires"/>. An ability with no requirement is always met; null points count as all zero.</summary>
        public bool RequiresMet(Stats points) => UnmetRequires(points).Count == 0;

        /// <summary>The requirement stats <paramref name="points"/> falls short on, as (short label, full stat name,
        /// minimum, have): what a locked Learn entry says it needs ("needs INT 8"). Empty when <see cref="RequiresMet"/>.</summary>
        public List<(string Abbrev, string Stat, float Min, float Have)> UnmetRequires(Stats points)
        {
            var list = new List<(string, string, float, float)>();
            if (Requires == null) return list;
            foreach (var (abbrev, stat, get) in RequirementStats)
            {
                var min = get(Requires);
                var have = points == null ? 0f : get(points);
                if (min > 0f && have < min) list.Add((abbrev, stat, min, have));
            }
            return list;
        }

        private static readonly (string Abbrev, string Stat, System.Func<Stats, float> Get)[] RequirementStats =
        {
            ("STR", nameof(Stats.Strength), s => s.Strength), ("VIT", nameof(Stats.Vitality), s => s.Vitality),
            ("AGI", nameof(Stats.Agility), s => s.Agility), ("SPD", nameof(Stats.Speed), s => s.Speed),
            ("INT", nameof(Stats.Intelligence), s => s.Intelligence),
            ("WIS", nameof(Stats.Wisdom), s => s.Wisdom), ("LCK", nameof(Stats.Luck), s => s.Luck),
        };
    }

    public sealed class BuffDef
    {
        public string Id;
        public string Name;
        public BuffKind Kind = BuffKind.Debuff;
        public DurationUnit Unit = DurationUnit.Turns;
        public int Duration = 1;
        public int DamagePerTick;
        public int HealPerTick;
        public bool Immobile;
        /// <summary>Rooted (GG-T856): the bearer can't step or be displaced (drag swaps, shoves, knockback, pull, swap, rule
        /// moves all stop at it) but still acts, strikes and casts. Unlike <see cref="Immobile"/> it never skips a turn.</summary>
        public bool Roots;
        /// <summary>Heavy (GG-T882): while the bearer carries this it is Heavy (<c>Unit.IsHeavy</c>) as a Heavy class is -
        /// no shove, pull, swap or rule drag moves it, unless it floats. The buff-based path to a temporary Heavy (the
        /// Warden's Anchor Stance); the class flag (<see cref="ClassDef.Heavy"/>) stays the permanent one.</summary>
        public bool Heavy;
        /// <summary>Braces (GG-T882, the Warden's Anchor Stance): every ally cardinally beside the bearer is braced - no foe's
        /// shove, pull, swap or body-shove and no rule drag (the anchor's heave, §14.20.3) moves it (<c>Battle.AnchorStance</c>).
        /// An ally's own drag swap still does.</summary>
        public bool Braces;
        /// <summary>Lasts until the bearer's next action (GG-T882): removed ("acted") as its next command starts, and never
        /// counted down by turns (<see cref="Duration"/> is ignored for it).</summary>
        public bool EndsOnAction;
        /// <summary>Frightened (GG-T916): the bearer flees its fear source. A frightened foe spends its next turn stepping
        /// directly away (no attack) and the fear is spent; a frightened hero can't end a drag beside the source. Bosses
        /// and Heavy units shrug it off (Immune!).</summary>
        public bool Frightens;
        /// <summary>Raging (GG-T897, §14.18 the Herd): the bearer counts as Heavy (<see cref="Battle.Unit.IsHeavy"/>: no
        /// shove, pull, swap or drag moves it, and it shrugs Frightened), Pushback can't set its icon back, a fear it
        /// carries breaks when the rage lands, and a raging Gorer's Bull Rush carries it down its marked line.</summary>
        public bool Rages;
        public bool HaltsTimeline;
        public bool BreaksOnDamage;
        public bool BreaksOnMove;
        public float IncomingDamageMultiplier = 1f;
        /// <summary>Shredded (GG-T857, §8.1): each instance (so each stack) adds this to the bearer's incoming
        /// <see cref="DamageType.Physical"/> damage, additively (3 stacks of 0.10 = ×1.3). Spells of other elements ignore it.</summary>
        public float PhysicalDamageTakenBonus;
        /// <summary>Shredded (GG-T857, §13.5): each application chips this much off the bearer's armor plate, whatever side
        /// the plate faces (a <c>PlateHitEvent</c>, breaking it at 0). Nothing on an unplated unit.</summary>
        public int PlateChip;
        public float TimelineSpeedMultiplier = 1f;
        /// <summary>Signed party tempo this buff adds while it sits on a hero (GG-T268): Slow on heroes is negative, so enemy icons speed up.</summary>
        public int Tempo;
        public float AccuracyMultiplier = 1f;
        /// <summary>Evasion points (GG-T297, §13.1.0) the bearer gains while this sits on it (Blur's +15); -40..40.</summary>
        public float EvasionBonus;
        public bool BlocksSpells;
        /// <summary>Taunt (§14.1.1): while any hero carries this, enemies may only pick taunting heroes.</summary>
        public bool Taunts;
        /// <summary>Guard (GG-T533, the Warden's Stand Guard): while the bearer carries this, the next foe's blow or hostile
        /// ability that lands on it is taken by whoever applied the buff (<c>BuffInstance.SourceId</c>) instead, if that
        /// guardian is alive, free to move and within <c>BattleConfig.GuardReach</c> tiles. The buff is spent on that blow.</summary>
        public bool Intercepts;
        /// <summary>Linked (GG-T873, the Hexer's Curse Link): lands on two foes at once, each instance naming the other
        /// (<c>BuffInstance.LinkedTo</c>). While both still carry it, damage dealt to one echoes
        /// <c>BattleConfig.LinkEchoFraction</c> of itself to the other; the echo never echoes back.</summary>
        public bool Links;
        /// <summary>Damage the bearer deals is scaled by this (War Cry / Might pickup → Empowered).</summary>
        public float OutgoingDamageMultiplier = 1f;
        public string OnExpireApply;
        /// <summary>GG-T667: buffs this one removes from the bearer when it lands, so the newest wins (Burning and Frozen
        /// cancel each other). A plain removal: the cancelled buff's OnExpireApply chain doesn't run.</summary>
        public List<string> Cancels = new List<string>();
        public BuffStacking Stacking = BuffStacking.Refresh;
        /// <summary>Cap for Stack buffs (0 = unlimited). Burning stacks to 3.</summary>
        public int MaxStacks;
        public string Letter;
        public string Color;
    }

    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        public string Description;
        public ItemType Type;
        public ItemRarity Rarity = ItemRarity.Common;
        public EquipmentSlot Slot = EquipmentSlot.None;
        public string WeaponType;
        /// <summary>Weapon-slot items only (GG-T52): which proficiency category (Simple/Martial/Arcane) this
        /// weapon needs. Ignored for every other slot. Default Simple = usable by any hero.</summary>
        public WeaponClass WeaponClass = WeaponClass.Simple;
        /// <summary>Head/Torso/Legs/Gloves/Boots items only (GG-T52): how much armor training (Light/Medium/
        /// Heavy) this piece demands. Ignored for Weapon/jewelry slots. Default Light = usable by any hero.</summary>
        public ArmorWeight ArmorWeight = ArmorWeight.Light;
        public int BaseCost;
        /// <summary>-1 = BaseCost/2.</summary>
        public int SellValue = -1;
        public int MaxStack = 99;
        public int Durability;
        public int BaseHealing;
        public int BaseDamage;
        public Stats Stats = new Stats();
        public int BattleStartManaOrbs;
        /// <summary>Consumable that casts this ability on use (e.g. Sleep Dart → "sleep").</summary>
        public string OnUseAbility;
        public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>();
        public bool SoldByVendor;
        /// <summary>GG-T538: the Merchant's stock tier this item joins at (1 = on the first visit). Tier N opens on the
        /// clear of <c>Meta.VendorTiers.OpensAfter[N-1]</c>; absent or 0 reads as tier 1. Only means something on a
        /// <see cref="SoldByVendor"/> item.</summary>
        public int ShopTier;
        public string Icon;
        public DamageType DamageType = DamageType.Physical;
        public int GrantsOrbs;
        public string CuresBuff;
        public bool CuresAll;
        public bool Revives;
        public bool Escape;
        /// <summary>Usable only from the camp Items menu (Tent).</summary>
        public bool OutOfBattleOnly;
        public string BonusVsTag;
        public float BonusMultiplier = 1f;
        /// <summary>Relic passive: this hero's pincer strikes deal +X (fraction).</summary>
        public float PincerDamageBonus;
        /// <summary>Gear passive (GG-T297): Evasion points while equipped, 0..20; lost when the piece breaks.</summary>
        public float EvasionBonus;
        /// <summary>Relic passive: HP restored each time control returns to the player.</summary>
        public int RegenPerTurn;
        /// <summary>Relic passive: start every battle with Protection.</summary>
        public bool StartsProtected;
        /// <summary>RecipeScroll only (§24.6.2, GG-T36): the recipe this item teaches when read at the Items screen.</summary>
        public string TeachesRecipe;
        /// <summary>Tome only (GG-T301): the Passive or Reactive ability it teaches the hero it's read to.</summary>
        public string TeachesAbility;
        /// <summary>Chest only (GG-T301): the droptables.json row rolled when it's opened.</summary>
        public string OpensTable;
        /// <summary>Named upgrade passive (§24.6.1, GG-T47): flavor name of a weapon's fully-upgraded (Master-tier,
        /// "+3") named passive, shown on the Equip compare panel. A weapon whose base already carries an on-hit or
        /// lifesteal trait (GG-T181) names that trait here on every step, and its "+3" passive is the same mechanic,
        /// stronger. Null = no named passive.</summary>
        public string PassiveName;
        /// <summary>PassiveName's effect text for the compare panel, e.g. "Pincer strikes have a 20% chance to Burn."</summary>
        public string PassiveDescription;
        /// <summary>PassiveName mechanic: a landed pincer strike (not a supporter or counter hit) by this weapon's
        /// wielder has this chance to apply this buff to the target (e.g. Flamebrand's Burn).</summary>
        public string PincerApplyBuff;
        public float PincerApplyBuffChance;
        /// <summary>PassiveName mechanic: heals the wielder this fraction of any damage a hero lands with this weapon
        /// (e.g. Vampiric Edge).</summary>
        public float LifestealFraction;
        /// <summary>PassiveName mechanic: while this weapon's wielder stands adjacent to an ally hero, that ally takes
        /// this much less damage (the strongest adjacent guardian wins — doesn't stack).</summary>
        public float AdjacentAllyDamageReduction;

        public int EffectiveSellValue => SellValue >= 0 ? SellValue : System.Math.Max(1, BaseCost / 2);
        /// <summary>Content authors always set <c>Slot: "Earring1"</c> for an earring item; this flag (not an
        /// exact slot match) is what lets it fit either Earring1 or Earring2 (GG-T89).</summary>
        public bool IsEarring => Slot == EquipmentSlot.Earring1 || Slot == EquipmentSlot.Earring2;
        /// <summary>Content authors always set <c>Slot: "Ring1"</c> for a ring item; fits either Ring1 or Ring2.</summary>
        public bool IsRing => Slot == EquipmentSlot.Ring1 || Slot == EquipmentSlot.Ring2;
    }

    /// <summary>One tier of a named armor set's bonus (GG-T182): active while the hero wears at least
    /// <see cref="Pieces"/> of the set's five pieces. Every field stacks onto the hero exactly like the same
    /// field on an <see cref="ItemDef"/> (stats add, resistances multiply, relic passives add).</summary>
    public sealed class SetBonusDef
    {
        public int Pieces;
        public Stats Stats = new Stats();
        public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>();
        public float PincerDamageBonus;
        public int RegenPerTurn;
        public int BattleStartManaOrbs;
        public bool StartsProtected;
    }

    /// <summary>A named five-piece armor set (GG-T182, armorsets.json): one Head/Torso/Legs/Gloves/Boots piece
    /// each, all of one <see cref="ArmorWeight"/>, with 2- and 5-piece bonuses resolved by
    /// <see cref="GridGame.Core.Rules.ArmorSets"/>. <see cref="Level"/> is the campaign level it's pitched at.</summary>
    public sealed class ArmorSetDef
    {
        public string Id;
        public string Name;
        public ArmorWeight Weight;
        public int Level = 1;
        public string Description;
        public List<string> Pieces = new List<string>();
        public List<SetBonusDef> Bonuses = new List<SetBonusDef>();
    }

    public sealed class ItemCount
    {
        public string Item;
        public int Count = 1;
    }

    public sealed class RecipeDef
    {
        public string Id;
        public CraftStation Station;
        public List<ItemCount> Inputs = new List<ItemCount>();
        public int Gold;
        /// <summary>Optional base item consumed (upgrade/enchant).</summary>
        public string BaseItem;
        public string Result;
        public int ResultCount = 1;
        /// <summary>Tier ladder rung (§24.6.1): 1 Apprentice (from the start), 2 Journeyman, 3 Master —
        /// gated by the crafting station's level (GG-T35). Missing from content = 1.</summary>
        public int Tier = 1;
        /// <summary>A starter weapon recipe (GG-T177, §25.0.6): the Forge's guided first forge. An Apprentice Forge
        /// recipe for a weapon, no gold, inputs from the first theme's drops; forging one equips it on the hero it suits.</summary>
        public bool Starter;
        /// <summary>Reagent quality (GG-T45): brewing with these inputs instead of <see cref="Inputs"/> — swapping
        /// in a higher-rarity reagent for the recipe's key material — yields <see cref="PotencyResult"/> instead
        /// of <see cref="Result"/>. Same recipe, better inputs, better output. Null/empty = no potency variant.</summary>
        public List<ItemCount> PotencyInputs = new List<ItemCount>();
        public string PotencyResult;
        public int PotencyResultCount = 1;
    }

    public sealed class DropEntry
    {
        public string Item;
        public float Chance;
        public int Min = 1;
        public int Max = 1;
    }

    public sealed class DropTableDef
    {
        public string Id;
        public int CoinsMin;
        public int CoinsMax;
        public List<DropEntry> Entries = new List<DropEntry>();
    }

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

    /// <summary>A caravan upgrade (§25.12): bought with gold, one value per level.</summary>
    public sealed class CaravanUpgradeDef
    {
        public string Id;
        public string Name;
        public string Description;
        public string Icon;
        public List<CaravanLevel> Levels = new List<CaravanLevel>();
        /// <summary>The crew member who runs this wagon (GG-T75), e.g. "Brannoc".</summary>
        public string Crew;
        /// <summary>Their job on the caravan, e.g. "cask-keeper".</summary>
        public string CrewRole;
        /// <summary>One-line flavor barks shown on Camp and the Caravan screen — never dialogue: no branching, no choices (§27).</summary>
        public List<string> Barks = new List<string>();

        // ── Facility upgrades (§25.12.2, GG-T432) ──────────────────────────────────────────────
        // A crafting station's facility is a caravan upgrade whose levels gate material tiers: each level lets the
        // station make a rarer result. Only the ids below may be facilities (ContentLoader.Validate).

        /// <summary>The Blacksmith's forge (Copper → Iron → Steel → Mythril).</summary>
        public const string ForgeFacilityId = "forge";
        /// <summary>The Alchemist's still (Clay Still → Glass Alembic → Silver Alembic).</summary>
        public const string AlchemyFacilityId = "alembic";

        /// <summary>The facility id of a crafting station, or null when the station has none (the Enchant station).</summary>
        public static string FacilityId(CraftStation station) =>
            station == CraftStation.Forge ? ForgeFacilityId : station == CraftStation.Alchemy ? AlchemyFacilityId : null;

        /// <summary>A facility's name before any level is bought ("Copper Forge").</summary>
        public string BaseName;
        /// <summary>The rarest result the unbought facility can make; set only on a facility.</summary>
        public ItemRarity? BaseRarity;

        public bool IsFacility => BaseRarity.HasValue;
    }

    public sealed class CaravanLevel
    {
        public int Cost;
        public float Value;
        public string Text;
        /// <summary>A facility level's name ("Iron Forge", GG-T432); null on an ordinary upgrade.</summary>
        public string Name;
        /// <summary>The rarest result a facility at this level can make (GG-T432); null on an ordinary upgrade.</summary>
        public ItemRarity? Rarity;
    }

    public sealed class ThemeDef
    {
        public string Id;
        public string Name;
        public List<string> Stages = new List<string>();
        /// <summary>Campaign milestone (GG-T301): an item granted once, the first time the theme's last stage is won
        /// (a Tome, so a story beat can teach a passive). Null = none.</summary>
        public string MilestoneReward;
        /// <summary>GG-T677: the theme's first full clear also paid one owned Mana Orb; since GG-T1811 (orbs retired) it pays
        /// <c>ManaOrbs.MilestoneShards</c> Mana Shards instead.</summary>
        public bool MilestoneOrb;
        /// <summary>GG-T677: a Halcyon ruin. Each of its stages holds one find, made on a victory with this chance (0 = none)
        /// until found; every stage's find is collected at most once. It paid a Mana Orb; since GG-T1811 it pays
        /// <c>ManaOrbs.HalcyonFindDust</c> Mana Dust.</summary>
        public float OrbFindChance;
        /// <summary>GG-T344: the theme's background set, <c>Backgrounds/&lt;set&gt;-00..NN</c> (owner rule: named after the
        /// story region, its words hyphenated, numbered from 00, GG-T561). Null = the theme id (<see cref="Backdrops.SetOf"/>).</summary>
        public string Backgrounds;
        /// <summary>Region rule ids (regionrules.json, GG-T359) every stage of this theme plays under.</summary>
        public List<string> Rules = new List<string>();
        /// <summary>GG-T456: the region's own battle tracks (Resources/Audio/Music/&lt;name&gt;), drawn one per stage from a
        /// shuffle bag so none repeats until the whole playlist has played.</summary>
        public List<string> Music = new List<string>();
        /// <summary>GG-T456: the closest-sounding tracks from other regions, added to the bag while the region has fewer
        /// than <c>MusicPlaylist.MinSize</c> tracks of its own. Drop them as the region gets its own music.</summary>
        public List<string> MusicBorrow = new List<string>();
        /// <summary>GG-T1478: the region has an entry crawl, Prose key <see cref="CrawlKey"/>, played once on the first
        /// arrival (merged with a chapter crawl due at the same stop).</summary>
        public bool HasCrawl;
        /// <summary>The region-entry crawl's words, joined from Prose at load (never in gameplay JSON).</summary>
        public string Crawl;

        /// <summary>A region-entry crawl's Prose key (GG-T1478).</summary>
        public static string CrawlKey(string id) => "crawl:region/" + id;

        /// <summary>What <c>SaveData.SeenCrawls</c> records once the region-entry crawl has played (GG-T1478).</summary>
        public string SeenMark => "region:" + Id;
    }

    public sealed class CampaignDef
    {
        /// <summary>GG-T456: the Camp's peaceful tracks, one per return from the field.</summary>
        public List<string> HubMusic = new List<string>();
        /// <summary>GG-T458: playlists for the non-battle moments, keyed by <c>MusicPlaylist.MomentKeys</c> (title, story,
        /// victory, defeat, levelup, crucible, boss, finalboss, charselect, credits). Each draws from its own shuffle bag;
        /// an empty or missing list keeps the screen's fallback.</summary>
        public Dictionary<string, List<string>> MomentMusic = new Dictionary<string, List<string>>();
        public List<ThemeDef> Themes = new List<ThemeDef>();
        /// <summary>GG-T1374: the tutorial drill stages, in play order. Never in a theme, so campaign order,
        /// HighestClearedStage, CampUnlocks, chapter crawls and the Codex do not see them.</summary>
        public List<string> Tutorial = new List<string>();
        /// <summary>GG-T231: the crawl that starts a new campaign (GG-T1363: the Opening, paged; was the Prologue), and
        /// the one after the final boss.</summary>
        public CrawlDef Opening;
        public CrawlDef Ending;
        /// <summary>GG-T386: the chapter crawls, in campaign order, one after every fifth campaign stage (each names the
        /// stage it follows). They replace the per-theme crawls, so the player never sees two scrolls back to back.</summary>
        public List<CrawlDef> Chapters = new List<CrawlDef>();
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

    /// <summary>A bounty (§22.4, GG-T431): a marked, tougher variant of an existing enemy class, with its own title, that
    /// roams one campaign stage. Taken at the board, it joins <see cref="Wave"/> of <see cref="Stage"/>; slain, its reward
    /// is collected back at the board. Gold scales with the stage (<c>Bounties.RewardGold</c>).</summary>
    public sealed class BountyDef
    {
        public string Id;
        /// <summary>The target's title, its name in battle: "Grimjaw the Mauler".</summary>
        public string Name;
        /// <summary>The enemy class it is a variant of (no new art).</summary>
        public string TargetClass;
        /// <summary>The campaign stage it roams; the clue names it ("Seen wandering …").</summary>
        public string Stage;
        /// <summary>0-based wave it joins (the next wave with room if that one is full).</summary>
        public int Wave;
        /// <summary>An optional extra trait: a rule-breaking boss's rule (§14.9). None = only the stat buff.</summary>
        public RuleBreak RuleBreak;
        public string RewardItem;
        /// <summary>Optional skill-point payout (GG-T134): on Claim every hero in the active party gains this
        /// many skill points. 0 (the default) pays none.</summary>
        public int RewardSkillPoints;
    }

    /// <summary>An offer in the Shop's Offers tab (offers.json, GG-T1450): sold either for real money as a store product
    /// (<see cref="ProductId"/>, a <c>StoreCatalog</c> id whose contents these must match) or for Shards
    /// (<see cref="ShardPrice"/>), never both. Rules in <c>Meta/Offers</c>.</summary>
    public sealed class OfferDef
    {
        public string Id;
        public string Name;
        /// <summary>The real-money product it sells, or null for a Shard offer.</summary>
        public string ProductId;
        /// <summary>The Shards it costs; 0 for a real-money offer.</summary>
        public int ShardPrice;
        /// <summary>Shards paid into the account wallet.</summary>
        public int Shards;
        /// <summary>Summon Tickets paid into the account wallet.</summary>
        public int Tickets;
        /// <summary>Cosmetic ids the account owns once it is claimed.</summary>
        public List<string> Cosmetics = new List<string>();
        /// <summary>How many times one account may claim it; 0 is no limit.</summary>
        public int AccountLimit;
        /// <summary>Campaign stages the save must have cleared before it shows (0 shows from the start).</summary>
        public int ShowsAfter;
        /// <summary>When it leaves the Shop (UTC); null never ends.</summary>
        public System.DateTime? EndUtc;
    }

    public sealed class EnchantDef
    {
        public string Id;
        public string Name;
        public string Element;
        public string Essence;
        public int ArcaneDust = 2;
        public int Gold = 150;
        public Stats Bonus = new Stats();
    }

    /// <summary>
    /// An armor enchant (GG-T179, armorenchants.json): the Enchantress binds a <see cref="AbilityKind.Passive"/>
    /// ability into one worn armor piece (Head/Torso/Legs/Gloves/Boots). Unlike the weapon-only elemental
    /// <see cref="EnchantDef"/>s, which turn a weapon into an "(Element)" variant item, an armor enchant lives on the
    /// piece itself (its save entry's <c>Enchant</c>), so set membership, wear and Masterwork rolls are untouched; the
    /// wearer has <see cref="Passive"/> in battle while the piece is intact. One enchant per piece (a new one replaces it).
    /// </summary>
    public sealed class ArmorEnchantDef
    {
        public string Id;
        public string Name;
        /// <summary>The passive ability (abilities.json, Kind Passive) the enchanted piece grants its wearer.</summary>
        public string Passive;
        public List<ItemCount> Inputs = new List<ItemCount>();
        public int Gold;
        /// <summary>Tier ladder rung (§24.6.1): unlocks with the Alchemist's station level, like every Enchantress recipe.</summary>
        public int Tier = 1;
    }

    /// <summary>
    /// One node in an archetype skill tree (§25.6.1, GG-T50). Unlocking it costs <see cref="Cost"/> skill
    /// points and grants the ability or passive named by <see cref="Grants"/>. Gated by
    /// <see cref="RequiredPointsInTree"/> (total points already spent anywhere in the tree) and
    /// <see cref="Prerequisites"/> (sibling node ids in the same tree that must already be unlocked).
    /// Spending points against a tree (and applying a granted passive to a hero in battle) is a later
    /// ticket; this is the content shape that work will read.
    /// </summary>
    public sealed class SkillNodeDef
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>Ability or passive id granted when this node unlocks. Exactly one of this and
        /// <see cref="GrantsStat"/> is set (<see cref="ContentLoader.Validate"/>).</summary>
        public string Grants;
        /// <summary>Attribute-bonus node (GG-T77): a flat permanent bonus to one stat instead of an ability.
        /// Mutually exclusive with <see cref="Grants"/>.</summary>
        public StatGrantDef GrantsStat;
        public int Cost = 1;
        public int RequiredPointsInTree;
        public List<string> Prerequisites = new List<string>();

        public bool IsStatNode => GrantsStat != null;
    }

    /// <summary>A skill-tree attribute bonus (GG-T77): <see cref="Amount"/> flat points of the stat named by
    /// <see cref="Stat"/> — one of <see cref="Names"/>, the real <see cref="Stats"/> field names.</summary>
    public sealed class StatGrantDef
    {
        public string Stat;
        public float Amount;

        public static readonly string[] Names = { "Strength", "Vitality", "Agility", "Speed", "Intelligence", "Wisdom", "Luck" };

        public static bool IsStatName(string s) => s != null && System.Array.IndexOf(Names, s) >= 0;

        /// <summary>This bonus as a <see cref="Stats"/> block (all zero but the named stat), ready to add to a hero's stats.</summary>
        public Stats ToStats()
        {
            var s = new Stats();
            switch (Stat)
            {
                case "Strength": s.Strength = Amount; break;
                case "Vitality": s.Vitality = Amount; break;
                case "Agility": s.Agility = Amount; break;
                case "Speed": s.Speed = Amount; break;
                case "Intelligence": s.Intelligence = Amount; break;
                case "Wisdom": s.Wisdom = Amount; break;
                case "Luck": s.Luck = Amount; break;
            }
            return s;
        }
    }

    /// <summary>One retired talent tree (§25.6.1, GG-T50). GG-T1093: the talent trees gave way to the skill hexagon
    /// (skillhex.json); skilltrees.json is empty and no class names a home tree. The shape stays so an old file or a
    /// test fixture still loads and validates.</summary>
    public sealed class SkillTreeDef
    {
        public string Id;
        public string Name;
        public string Description;
        public List<SkillNodeDef> Nodes = new List<SkillNodeDef>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Dialogue (§27.1, GG-T117): dialogues.json. The engine (GridGame.Meta.DialogueRunner) is a data
    // interpreter over these shapes; conditions and effects are the save's own vocabulary, not a script.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>When a dialogue plays. Story beats (ThemeStart, StageCleared) play once, between battles,
    /// from StageLauncher; Talk dialogues are picked from Camp's Talk row and may repeat unless <see cref="DialogueDef.Once"/>.
    /// GG-T537 (owner, 2026-11-21): every Camp vendor joins on a story beat of the campaign, never on an incidental trigger,
    /// so a JoinNpc or OpenStation effect lives only in an unconditional StageCleared beat. That beat also plays on the way
    /// back to Camp after the clear (the join moment), not only at the next launch. The old GearWorn, CanBrew and
    /// FirstArmor triggers (GG-T175/T176/T179) were removed with it.
    /// Recruit (GG-T132) is never played between battles: a stage's <see cref="RecruitableDef.Dialogue"/> names it and the
    /// battle speaks it when a hero talks to the neutral (<see cref="DialogueTrigger.Id"/> = the recruitable's class).
    /// LevelReached (GG-T231) fires once any roster hero is at <see cref="DialogueTrigger.Level"/> or above — on the way back
    /// to Camp after the battle that did it, or at the next launch. StageStart (GG-T231) fires on launching the stage
    /// <see cref="DialogueTrigger.Id"/> (a boss's introduction).
    /// Wanderer (GG-T342; replaces the GG-T236 Rescue scene) is the talk with a wanderer standing at a victory, played from
    /// the results screen: "$hero" is the wanderer, "$with" the party member the player chose to talk, and
    /// <see cref="DialogueTrigger.Id"/> is the wanderer's hero class, a factions.json faction id, or null (the generic one).
    /// GG-T387 (the owner's "game of wits"): it is a contest of <see cref="DialogueDef.WitsExchanges"/> exchanges, each a
    /// question from the wanderer with three replies for the companion, one per <see cref="DialogueChoice.Wit"/> (+1 wins
    /// the point, 0 is neutral, -1 loses it); a later node goes on to <see cref="DialogueDef.OutcomeNext"/>, where the meta
    /// layer's rule (the standing) picks <see cref="DialogueDef.JoinNode"/> or <see cref="DialogueDef.PartNode"/>.
    /// Trust and Cohesion (GG-T1298, §27.1) are bond scenes in Camp's Talk row: Trust is the hero
    /// <see cref="DialogueTrigger.Id"/>'s, offered while they're in the party and open once their Trust tier reaches
    /// <see cref="DialogueTrigger.Level"/>; Cohesion is the pair Id + <see cref="DialogueTrigger.Other"/>, offered while both
    /// are in the party and open at that cohesion tier. Shown locked (with the tier they need) until then; each plays
    /// once, and "$with" is the hero Id.
    /// Founding (GG-T1318) plays once, after the opening crawl, with the starting party ("$party:N"); no Id.
    /// Joined (GG-T1318) is a hero's first talk at Camp after joining by a non-story route (a hero not in
    /// SaveData.MetHeroes): "$hero" is the joined hero, and <see cref="DialogueTrigger.Id"/> is their hero class, a
    /// factions.json faction id, or null (the generic one), picked like a Wanderer talk.</summary>
    public enum DialogueTriggerKind { ThemeStart, StageCleared, Talk, Recruit, LevelReached, StageStart, Wanderer, Trust, Cohesion, Founding, Joined }

    public sealed class DialogueTrigger
    {
        public DialogueTriggerKind Kind;
        /// <summary>ThemeStart: the theme id. StageCleared: the stage id. Talk: who it's with — a hero class id,
        /// "party" (any party member; speaker class "$with" is them) or "npc:&lt;id&gt;" (a joined Camp NPC).
        /// Trust / Cohesion: the hero class id (Cohesion: the pair's first hero). Joined: a hero class id, a faction id or null.</summary>
        public string Id;
        /// <summary>Cohesion only: the pair's second hero class id.</summary>
        public string Other;
        /// <summary>LevelReached: the hero level (2+) that fires it. Trust / Cohesion: the bond tier (1-5) that opens it.</summary>
        public int Level;
    }

    /// <summary>QuestAvailable / QuestActive / QuestDone (GG-T237, §22.11): <see cref="DialogueCondition.Id"/> is a quests.json id.
    /// Available = its unlock stage is cleared and it's neither under way nor handed in.
    /// TrustAtLeast / CohesionAtLeast (GG-T1293, §25.20): the hero <see cref="DialogueCondition.Id"/>'s Trust, or the cohesion of
    /// Id and <see cref="DialogueCondition.Other"/>, is at least <see cref="DialogueCondition.Amount"/>; each hero is a class id
    /// or a speaker token ("$with", "$recruit", "$hero", "$party:N", "$trusted"; not "$commander").</summary>
    public enum DialogueConditionType { FlagSet, FlagNotSet, HasItem, GoldAtLeast, PartyHas, RosterHas, RosterLacks, NpcJoined, QuestAvailable, QuestActive, QuestDone, TrustAtLeast, CohesionAtLeast }

    public sealed class DialogueCondition
    {
        public DialogueConditionType Type;
        /// <summary>Flag / item / class / NPC id, by type.</summary>
        public string Id;
        /// <summary>HasItem count (default 1), GoldAtLeast amount, or the Trust / cohesion to reach.</summary>
        public int Amount;
        /// <summary>CohesionAtLeast only: the second hero of the pair.</summary>
        public string Other;
    }

    /// <summary>Nothing here can remove a hero for good (owner rule: nobody dies permanently).
    /// RecruitHero hires exactly the named class (no-op if owned); JoinHero (GG-T118) is a story recruit that
    /// always brings someone new: the named class if it's free, else a hero of <see cref="DialogueEffect.Role"/>
    /// (or of a role the roster lacks) the player doesn't have yet.
    /// GrantSkillPoint (GG-T133) gives a hero — <see cref="DialogueEffect.Id"/> as a class id, or "$with"/null for the
    /// Talk partner — <see cref="DialogueEffect.Amount"/> skill points (default 1), once per dialogue per hero: the
    /// save flag <c>sp:&lt;dialogue&gt;:&lt;hero&gt;</c> guards it, so a replayed Talk can't farm it.
    /// StartQuest (GG-T237) puts the quests.json quest <see cref="DialogueEffect.Id"/> into the journal (no-op if it's under way or done).
    /// OpenStation (GG-T537) marks the story beat that opens a keeperless Camp station (<see cref="DialogueDef.Stations"/>):
    /// the station itself opens on the stage clear (Meta's CampUnlocks); the effect only announces it.
    /// AddTrust / AddCohesion (GG-T1293, §25.20) move the hero <see cref="DialogueEffect.Id"/>'s Trust, or the cohesion of Id
    /// and <see cref="DialogueEffect.Other"/>, by <see cref="DialogueEffect.Amount"/>; each hero is a class id or a speaker
    /// token ("$with", "$recruit", "$hero", "$party:N", "$trusted"; not "$commander"). Each pays once per save at its node (and choice): a replayed Talk can't farm it.</summary>
    public enum DialogueEffectType { SetFlag, ClearFlag, GrantGold, GrantItem, RecruitHero, JoinNpc, JoinHero, GrantSkillPoint, StartQuest, OpenStation, AddTrust, AddCohesion }

    public sealed class DialogueEffect
    {
        public DialogueEffectType Type;
        public string Id;
        /// <summary>GrantGold amount (negative = pay), GrantItem count (default 1), GrantSkillPoint count (default 1), or
        /// the Trust / cohesion change (either sign).</summary>
        public int Amount;
        /// <summary>JoinHero only: the role to fall back on when <see cref="Id"/> is missing or already owned.</summary>
        public string Role;
        /// <summary>AddCohesion only: the second hero of the pair.</summary>
        public string Other;
    }

    public sealed class DialogueSpeaker
    {
        public string Id;
        /// <summary>A class id (its name and portrait), or "$lead" (the first party member), "$with" (the
        /// party member a Talk dialogue is with) or "$recruit" (the hero this dialogue's first JoinHero brings, GG-T118), or
        /// "$hero" (the wanderer a Wanderer talk is with, GG-T342). GG-T1575: "$commander" (the player, named "the Commander"
        /// with a silhouette until the canon fills them), "$party:N" (the Nth party member, 1-based, up to the party cap) or
        /// "$trusted" (the party hero with the most Trust, ties to the earlier slot), bound when the dialogue opens; a
        /// dialogue whose "$party:N" or "$trusted" this save can't fill isn't offered.</summary>
        public string Class;
        /// <summary>Display name; defaults to the class's name.</summary>
        public string Name;
        /// <summary>The class whose portrait this speaker shows (an NPC borrowing a class's art, e.g. the Lantern Merchant
        /// shows the courier's); defaults to <see cref="Class"/>.</summary>
        public string Portrait;
    }

    public sealed class DialogueChoice
    {
        public string Label;
        /// <summary>Wanderer talks only (GG-T387, the game of wits): what this reply does to the standing: +1 wins the
        /// point, 0 is neutral, -1 loses it. Every exchange offers one of each. 0 everywhere else.</summary>
        public int Wit;
        /// <summary>Node id to go to; null ends the dialogue.</summary>
        public string Next;
        /// <summary>All must hold for the choice to be offered.</summary>
        public List<DialogueCondition> Conditions = new List<DialogueCondition>();
        public List<DialogueEffect> Effects = new List<DialogueEffect>();
    }

    public sealed class DialogueNode
    {
        public string Id;
        /// <summary>A <see cref="DialogueSpeaker.Id"/> of this dialogue.</summary>
        public string Speaker;
        public string Text;
        /// <summary>Auto-advance target when there are no choices; null ends the dialogue.</summary>
        public string Next;
        public List<DialogueChoice> Choices = new List<DialogueChoice>();
        /// <summary>Applied when the node is reached.</summary>
        public List<DialogueEffect> Effects = new List<DialogueEffect>();
    }

    public sealed class DialogueDef
    {
        /// <summary>Camp NPC ids a JoinNpc effect / NpcJoined condition / "npc:" Talk may name (GG-T119 gates services on them).
        /// GG-T570: the Aetherist (the retired hero class, now the keeper of the Aetherite refinery) joins at The Shardfall II.</summary>
        public static readonly string[] Npcs = { "blacksmith", "alchemist", "enchantress", "merchant", "innkeeper", "aetherist" };
        /// <summary>GG-T570: a Camp shop keeper's bark occasions, "&lt;npc&gt;/&lt;occasion&gt;". Each is the Prose bark
        /// <c>bark:shop/&lt;npc&gt;/&lt;occasion&gt;</c> (its lines, one said at a time), joined into
        /// <see cref="ContentDb.ShopBarks"/>: the Aetherist greets, refines Dust into Shards, is short of Dust, and says goodbye.</summary>
        public static readonly string[] ShopBarks = { "aetherist/greet", "aetherist/refine", "aetherist/short", "aetherist/leave" };
        /// <summary>GG-T537: the keeperless Camp stations an OpenStation effect may name (Summon Circle with the Diamond Store,
        /// Bounties with the Mercenaries, the Crucible, the Caravan).</summary>
        public static readonly string[] Stations = { "summon", "bounties", "crucible", "caravan" };

        /// <summary>Wanderer talks (GG-T342): the two endings the <see cref="OutcomeNext"/> marker resolves to.</summary>
        public const string JoinNode = "join", PartNode = "part", OutcomeNext = "$outcome";
        /// <summary>Wanderer talks (GG-T387): the exchanges in the game of wits (questions, each with a win, a neutral and
        /// a losing reply).</summary>
        public const int WitsExchanges = 3;

        public string Id;
        /// <summary>Talk row label (Talk dialogues); also a debugging name.</summary>
        public string Title;
        public DialogueTrigger Trigger = new DialogueTrigger();
        /// <summary>All must hold for the dialogue to be offered.</summary>
        public List<DialogueCondition> Conditions = new List<DialogueCondition>();
        /// <summary>Talk only: play once, then never offer again (story beats always play once).</summary>
        public bool Once;
        /// <summary>Backdrop theme id (defaults to the trigger's theme, else the camp's).</summary>
        public string Backdrop;
        /// <summary>Story beats only (GG-T231): a text crawl, titled <see cref="Title"/>, that scrolls before the conversation.
        /// Resolved from Prose ("crawl:dialogue/&lt;id&gt;", GG-T355); dialogues.json carries only <see cref="HasCrawl"/>.</summary>
        public string Crawl;
        /// <summary>GG-T355: this beat opens with a crawl, whose words live in Prose. The marker is what gives the key.</summary>
        public bool HasCrawl;
        public List<DialogueSpeaker> Speakers = new List<DialogueSpeaker>();
        /// <summary>First node id; defaults to the first node.</summary>
        public string Start;
        public List<DialogueNode> Nodes = new List<DialogueNode>();

        /// <summary>Every effect in the dialogue — nodes' then their choices', in node order (null nodes/choices skipped).</summary>
        public IEnumerable<DialogueEffect> AllEffects()
        {
            foreach (var n in Nodes)
            {
                if (n == null) continue;
                foreach (var e in n.Effects) yield return e;
                foreach (var ch in n.Choices)
                    if (ch != null) foreach (var e in ch.Effects) yield return e;
            }
        }
    }
}

namespace GridGame.Core.Content
{
    /// <summary>GG-T923: the kinds of Codex entry (the in-game Lore / Encyclopedia, GG-T922). An entry's key is
    /// "&lt;kind&gt;:&lt;id&gt;" (<see cref="Key"/>, Meta <c>Codex.Key</c>), ids lower-cased. Lives in Core so the content
    /// loader can check codex.json (GG-T936).</summary>
    public static class CodexKinds
    {
        /// <summary>A campaign region (theme id) or stage id.</summary>
        public const string Place = "place";
        /// <summary>A factions.json faction id.</summary>
        public const string Faction = "faction";
        /// <summary>A hero class id, a Camp NPC id (merchant, blacksmith, …) or a caravan crew member's Prose slug.</summary>
        public const string Character = "character";
        /// <summary>An enemy class id (its Bestiary entry is Seen).</summary>
        public const string Foe = "foe";
        public const string Item = "item";
        public const string Ability = "ability";
        /// <summary>A glossary entry id.</summary>
        public const string Keyword = "keyword";
        /// <summary>"chapter/&lt;id&gt;" (a chapter crawl played), "dialogue/&lt;id&gt;" (a dialogue played to the end), or the
        /// rest of a "story:" flag ("ending").</summary>
        public const string Story = "story";

        public static readonly string[] All = { Place, Faction, Character, Foe, Item, Ability, Keyword, Story };

        public static string Key(string kind, string id) => kind + ":" + (id ?? "").Trim().ToLowerInvariant();

        /// <summary>Splits "&lt;kind&gt;:&lt;id&gt;"; false when there is no colon or either side is empty.</summary>
        public static bool TrySplit(string key, out string kind, out string id)
        {
            kind = id = null;
            int colon = key?.IndexOf(':') ?? -1;
            if (colon <= 0 || colon == key.Length - 1) return false;
            kind = key.Substring(0, colon);
            id = key.Substring(colon + 1);
            return true;
        }
    }

    /// <summary>GG-T936: one codex.json entry (GG-T924): which Codex entry exists, its title and what it links to. The
    /// body is Prose (description key "codex:&lt;kind&gt;/&lt;id&gt;", payload "long"), joined into <see cref="Body"/>; with
    /// no text yet the entry falls back to the record's own words (<see cref="ContentDb.CodexEntry"/>).</summary>
    public sealed class CodexDef
    {
        /// <summary>"&lt;kind&gt;:&lt;id&gt;" (<see cref="CodexKinds.Key"/>).</summary>
        public string Key;
        /// <summary>Null = the record's own name.</summary>
        public string Title;
        /// <summary>Other entries' keys this one links to.</summary>
        public List<string> Related = new List<string>();
        /// <summary>Filled by <see cref="ProseText.Join"/> from the Prose text; null when there is none yet.</summary>
        public string Body;

        /// <summary>The Prose key of this entry's body: "codex:&lt;kind&gt;/&lt;id&gt;".</summary>
        public static string TextKey(string key)
        {
            if (!CodexKinds.TrySplit(key, out var kind, out var id)) return ProseText.CodexKey + key;
            return ProseText.CodexKey + kind + "/" + id;
        }
    }

    /// <summary>GG-T936: one Codex entry as the screen shows it, resolved by <see cref="ContentDb.CodexEntry"/>.</summary>
    public sealed class CodexEntryView
    {
        public string Kind;
        public string Id;
        public string Key;
        public string Title;
        public string Body;
        /// <summary>Related keys that resolve to an entry.</summary>
        public List<string> Related = new List<string>();
        /// <summary>True when the kind is known and the record exists.</summary>
        public bool Exists;
    }
}
