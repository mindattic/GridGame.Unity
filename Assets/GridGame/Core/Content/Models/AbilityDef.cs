using System.Collections.Generic;

namespace GridGame.Core.Content
{
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

    public enum ChargeShape { Single, Line }

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
        /// <summary>GG-T2069 (§13.5): hitting a foe that wears an armor plate shatters the plate outright from any side,
        /// unyielding or not (a <c>PlateShatteredEvent</c>, then its <c>PlateBrokenEvent</c>), and the hit lands on the body.</summary>
        public bool ShattersPlate;
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
}
