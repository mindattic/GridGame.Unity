namespace GridGame.Core.Battle
{
    /// <summary>
    /// Every tuning number of the battle simulation in one place (bible §2, §3, §13).
    /// Time is integer ticks (1 tick = 0.1 "game seconds"); the timeline bar is BarLength IP.
    /// The clock only advances when the player commits an action (bible §2, act-advanced amendment).
    /// </summary>
    public static class BattleConfig
    {
        public const int Cols = 6;
        public const int Rows = 8;

        /// <summary>
        /// Terra Battle-style skill tech: a fast, precise ~45° drag can slip a hero diagonally into an open
        /// tile, skipping the usual cardinal-only path instead of a softened "corner" rule. Player drags only
        /// (see <c>BattleScreen.Input.ExtendPath</c>) — enemies/bots never submit a diagonal step. Mutable, not
        /// const, so it's trivially flippable (`-noDiagonalSlip` at the command line).
        /// </summary>
        public static bool DiagonalSlipEnabled = true;

        // ── Timeline ────────────────────────────────────────────────────────────────
        public const int TicksPerSecond = 10;
        /// <summary>A turn icon fills IP up to BarLength, then acts. At <see cref="BaseIpPerTick"/> a full load is 10 s, so a
        /// HastenU of 0.15 is 1.5 s.</summary>
        public const int BarLength = 2000;
        /// <summary>GG-T318: every turn icon gains this much IP per tick, whatever its Speed. Tempo, buffs (Slowed, Frozen), Rally
        /// and the difficulty dial's EnemySpeed scale it (<see cref="BattleEngine.TimelineScale"/>). Speed sets where an icon
        /// re-enters the bar instead (<see cref="BattleEngine.EntrySeconds"/>).</summary>
        public const int BaseIpPerTick = 20;
        /// <summary>
        /// Seconds the drawn bar spans (GG-T286, Resonance-style). An icon sits at u = secondsUntilAct / BarSeconds, so u = 0
        /// is the LEFT edge (acting now). Anything further off waits at the right end.
        /// GG-T318: 10 → 6, the latest a foe re-enters.
        /// </summary>
        public static float BarSeconds = 6f;

        // ── Re-entry (GG-T318, owner: "the average time to move is 4 seconds … they start a little more to the left the
        //    faster they are"). After acting, a foe recovers off the bar, then re-enters at a place set by its Speed. ──
        /// <summary>Seconds from re-entry to acting for a foe of exactly average Speed for its level.</summary>
        public static float EntryBaseSeconds = 4f;
        /// <summary>How strongly Speed moves the entry: EntryBase × (ReferenceSpeed / Speed)^this.</summary>
        public static float EntrySpeedExponent = 0.6f;
        public static float EntryMinSeconds = 2f;
        public static float EntryMaxSeconds = 6f;
        /// <summary>Seconds a foe spends off the bar after acting, before it re-enters (plus its action's RecoverSeconds and
        /// any spacing delay from the difficulty row).</summary>
        public static float RecoverBaseSeconds = 1f;
        /// <summary>Most extra recovery one action's RecoverSeconds keyword may add (validated).</summary>
        public const float RecoverMaxSeconds = 3f;
        /// <summary>
        /// Clock a spent window takes when no enemy turn icon can arrive at all (none left, all Frozen, a wave still
        /// waiting on reinforcements). Otherwise a window always runs to the next enemy arrival (GG-T188/GG-T308, §2.0).
        /// </summary>
        public const int ActionTicks = 20;
        /// <summary>Safety cap on one run of the clock to the next enemy turn (200 game seconds): nothing real waits that long,
        /// so hitting it means no enemy icon could arrive and the window ends anyway.</summary>
        public const int MaxAdvanceTicks = 2000;
        /// <summary>
        /// GG-T308 (§2.0): game seconds an Ability, Item, Spell or Talk spends from the party's window (a drag spends the
        /// time it was held instead), so an instant action still costs budget. A spell's cast time rides the bar separately.
        /// </summary>
        public static float HeroActionSeconds = 1f;

        // ── Minimum action time (GG-T311, §2.0): a hero action can only start in a gap at least as long as it takes ──
        /// <summary>Seconds a drag costs before the hero has moved at all: picking it up (part of every drag's minimum).</summary>
        public static float DragPickupSeconds = 0.5f;
        /// <summary>Seconds a drag costs per tile walked. The shortest drag that does anything is pickup + one tile: a gap
        /// shorter than that can't start a drag (and every drag spends at least that much of the window).</summary>
        public static float DragSecondsPerTile = 0.35f;
        /// <summary>The party opens every wave: no enemy turn icon reaches the left edge sooner than this after the wave
        /// spawns (a fast foe's head start is trimmed), so the heroes always act first (GG-T311).</summary>
        public static float OpeningWindowSeconds = 3f;
        /// <summary>GG-T311: most rally stacks — forced Guards in a row (no action fitted the gap), each one tempo step slower for
        /// every enemy icon (×0.8, ×0.64, ×0.51, ×0.41, ×0.33, outside the tempo clamp) — so even foes whose turns all fall too close together leave a gap
        /// wide enough to act in after a few sacrifices. The next hero action resets it.</summary>
        public static int RallyMaxStacks = 5;

        // ── Party tempo (GG-T268, §2.11): hero haste slows enemy icons, Slow on heroes speeds them ─────────
        /// <summary>Each net tempo point scales every enemy turn icon's rate by this factor (+1 → ×0.8, −1 → ×1.25).</summary>
        public static float TempoStep = 1.25f;
        /// <summary>Clamp on the enemy icon multiplier so neither side ever freezes or runs away.</summary>
        public static float TempoMinMultiplier = 0.5f;
        public static float TempoMaxMultiplier = 2f;
        /// <summary>A hero haste ability of HastenU gives max(1, round(HastenU / this)) tempo points.</summary>
        public static float HastenUPerTempo = 0.15f;
        /// <summary>Party turns a tempo effect lasts when its source names none.</summary>
        public static int DefaultTempoTurns = 2;

        // ── Held turn (GG-T188, §2.0): picking up a hero runs the timeline in real time ──────────────────
        // GG-T311: no real-time floor any more (MinTurnSeconds 2 s let a 0.1 s gap play as a 2 s drag) — a hold runs at
        // 1 tick per 0.1 s, and a gap too short for any action is a forced Guard instead. The bot driver's drag time is
        // the human model DragPickupSeconds + DragSecondsPerTile × tiles (+ its think time, HeroBot.ThinkSeconds).
        /// <summary>Bot driver (GG-T191): a best move scoring under this is not worth a turn; the bot may Guard instead.</summary>
        public const float BotGuardBelowScore = 0.5f;
        /// <summary>Bot driver (GG-T191): chance of Guarding on such a turn (a lone hero always does).</summary>
        public const float BotGuardChance = 0.2f;

        // ── Pushback (§2.3, GG-T286): only an ability/item carrying the Pushback keyword (seconds) moves an icon, anywhere
        //    on the bar; plain hits never push. One push per foe per action, capped. ──
        /// <summary>Seconds a pincer shoves back every foe it lands a blow on (the pincer attack carries the keyword).
        /// GG-T306: 1 → 1.5 s, paying the party back for foes that now engineer their own pincers.</summary>
        public static float PincerPushbackSeconds = 1.5f;
        /// <summary>Cap on one push, whatever its source.</summary>
        public static float PushbackMaxSeconds = 2f;
        /// <summary>GG-T533: how far (tiles, Manhattan) a guardian may stand from the ally it guards and still take the blow.</summary>
        public static int GuardReach = 2;
        /// <summary>GG-T873: the share of a blow on a Linked foe (Curse Link) that echoes to its partner (min 1).</summary>
        public static float LinkEchoFraction = 0.5f;

        // ── Buffs (§8.3) ────────────────────────────────────────────────────────────
        public const int BuffTickInterval = 10;            // one tick-unit step per 1.0 s of timeline
        public const float LightningWhenWetMultiplier = 1.5f;
        public const float SleepWhenWarmMultiplier = 1.5f;
        public const float LightningBlindChance = 0.30f;
        /// <summary>Spells never miss but crit at this fraction of the caster's physical crit chance (US-115 decision).</summary>
        public const float SpellCritFraction = 0.5f;
        public const float SpellCritMultiplier = 1.6f;

        // ── Casting (§2.6 / §13.4) ─────────────────────────────────────────────────
        public const int MaxHeroCastsInFlight = 4;
        public const float DefaultChargeCastSeconds = 2.5f;
        public const float BaseInterruptDelaySeconds = 0.6f;
        public const float InterruptStrengthScale = 20f;
        public const float InterruptWisdomDelayScale = 15f;
        public const float WisdomResistPerPoint = 0.015f;
        public const float MaxResistChance = 0.60f;
        public const float ClutchChancePerLuck = 1f / 200f;
        public const float ClutchMaxChance = 0.20f;
        /// <summary>The Clutch! bonus (GG-T566): mana charge every Clutch grants, whatever earned it (a hero's cast forced
        /// through an interrupt, a crit landed while Blind; more kinds may follow, GG-T571). One shared knob.</summary>
        public const int ClutchCharge = 20;
        /// <summary>GG-T571 (§13.4.1): LastStand — the killer is at or below this share of its max HP (0 turns the kind off).</summary>
        public static float ClutchLastStandHpFraction = 0.15f;
        /// <summary>GG-T571: DoublePincer — this many pincers closed by one move (below 2 turns the kind off).</summary>
        public static int ClutchPincersInOneMove = 2;
        /// <summary>GG-T571: LastTickInterrupt — the cancelled foe charge had at most this much timeline left (0 turns it off).</summary>
        public static float ClutchLastTickSeconds = 0.5f;
        /// <summary>GG-T571: Clutch kinds switched off (no flash, no float, no bonus). Empty = every kind on. A switched-off
        /// CastThroughInterrupt still forces the cast through (that is the interrupt rule); only the Clutch bonus goes.</summary>
        public static readonly System.Collections.Generic.HashSet<ClutchKind> ClutchDisabled = new System.Collections.Generic.HashSet<ClutchKind>();
        public static bool ClutchEnabled(ClutchKind k) => !ClutchDisabled.Contains(k);

        // ── Supporters (§1.2.3) ─────────────────────────────────────────────────────
        public const float SupporterDamageFraction = 0.5f;
        /// <summary>Largest Haste one support may give, in seconds (GG-T298; validated on SupportDef.Magnitude).</summary>
        public const float SupportMaxHasteSeconds = 3f;
        /// <summary>Largest crit chance one Crit support may add (0.5 = +50 points, GG-T298)…</summary>
        public const float SupportMaxCritBonus = 0.5f;
        /// <summary>…and the cap on the attacker's total crit percent while supported.</summary>
        public const float SupportMaxSupportedCritPercent = 75f;
        /// <summary>Largest spell power fraction a Cast support may use (GG-T298).</summary>
        public const float SupportMaxCastPower = 1f;

        // ── Shield (§2.5) ───────────────────────────────────────────────────────────
        public const string ShieldBuffId = "protection";
        public const int MaxBankCapacityBonus = 4;
        /// <summary>New Game+: enemy levels rise by this per cycle; coin rewards scale by NgPlusCoinBonus per cycle.</summary>
        public const int NgPlusLevelStep = 10;
        public const float NgPlusCoinBonus = 0.5f;

        // ── Bounty targets (§22.4, GG-T431): the marked, tougher variant of a stage's foe ────
        /// <summary>A bounty target fights this many levels above the wave it joins.</summary>
        public const int BountyLevelBonus = 2;
        /// <summary>Its HP is this multiple of an ordinary foe of its class and level.</summary>
        public const float BountyHpScale = 2.5f;
        /// <summary>Every stat but Speed is scaled by this (Speed is left alone so the timeline reads the same).</summary>
        public const float BountyStatScale = 1.3f;

        // ── Time bank (§3.1.8, act-advanced amendment: drag-timer remainder) ────────
        public const float DragTimerSeconds = 4f;
        public const float SecondsPerTimeBankOrb = 1.25f;
        public const int MaxTimeBankOrbs = 3;

        // ── Guard bank (§2.5 / §3.1.9, GG-T189, owner 2026-10-21): Guard ends the turn early and the turn time it
        //    gives up fills the forming mana bubble (charge toward the next orb), proportional to the time left ──
        /// <summary>Mana charge per real second of turn left when Guard is pressed (100 charge = one orb).</summary>
        public const float GuardChargePerSecond = 10f;
        /// <summary>Most charge one Guard can bank — Guard ends the turn, so this is also the per-turn limit.</summary>
        public const int GuardMaxCharge = 40;

        // ── Steal (§6 stage 8) ──────────────────────────────────────────────────────
        public const float StealDivisor = 50f;

        // ── Weapon durability (§24.5) ───────────────────────────────────────────────
        public const float ShatterTargetMultiplier = 1.5f;
        public const float ShatterSelfDamageFraction = 0.15f;

        // ── Enemy AI (§14.1) ────────────────────────────────────────────────────────
        public const float RetreatHpThreshold = 0.30f;
        /// <summary>GG-T306 (§14.1.3) tuning, static so a soak can sweep them (GRIDGAME_TUNE): a foe's pincer planner takes Setup
        /// and Support walks (not only Complete ones), and may swap places with a hero on its walk.</summary>
        public static bool EnemyPincerSetups = true;
        public static bool EnemyPincerSwapsHeroes = true;
        /// <summary>GG-T306: a wounded, fleeing foe's step score against a corner tile (no pincer axis reaches it).</summary>
        public static float FleeCornerPenalty = 3f;

        // ── Voluntary retreat (§22.2, FFIV-style partial loss, owner direction 2026-10-11) ──
        /// <summary>A voluntary Retreat (BattlePhase.Retreated) keeps this fraction of the battle's
        /// as-yet-uncommitted Results.Coins and each item stack (floored). A true party wipe (Defeat)
        /// still keeps nothing but hard-won XP, unchanged.</summary>
        public const float RetreatKeepFraction = 0.5f;

        // ── Power Amplifier (owner request 2026-10-10, from Terra Battle) ────────────
        /// <summary>Chance per player action that an amplifier appears (when none is on the board)…</summary>
        public const float AmplifierBaseChance = 0.12f;
        /// <summary>…plus this per point of the living party's combined Luck…</summary>
        public const float AmplifierChancePerLuck = 0.003f;
        /// <summary>…capped here.</summary>
        public const float AmplifierMaxChance = 0.45f;
        /// <summary>A pincer attacker standing on the amplifier strikes this much harder (and mints a bonus orb).</summary>
        public const float AmplifierDamageMultiplier = 1.75f;

        // ── Mana charge (§3.1.9, owner direction 2026-10-11: "mana needs to be a more rare resource … the number of
        //    participants in the attack increases the multiplier needed to earn a mana bubble") ─────────────────────
        /// <summary>Charge that makes one orb ("mana bubble").</summary>
        public const int ManaChargePerOrb = 100;
        /// <summary>Orbs the party's shared bank holds before minting overflows (GG-LAW-6); also the most a spell may cost.</summary>
        public const int ManaBankCapacity = 12;
        /// <summary>Charge for a pincer that takes away a whole enemy's worth of HP (scaled by the share removed).
        /// GG-T247: 120 → 105 (the bar sat full on 17% of a skilled player's turns and ~half of all mana faded at the cap).</summary>
        public const float PincerChargePerHp = 105f;
        /// <summary>GG-T247: earned charge × (1 − this × bank fullness): an empty bank fills at full rate, a full one at 30%.</summary>
        public const float ChargeFullnessDamping = 0.7f;
        /// <summary>GG-T247: scales the Mana pincer-support action (Red Mage, Ritualist, …) — the second-biggest source.</summary>
        public const float SupportManaFactor = 0.5f;
        /// <summary>Each participant beyond the two attackers raises the divisor by this much (2 heroes ×1, 4 heroes ×2).</summary>
        public const float ChargeParticipantPenalty = 0.5f;
        /// <summary>Each link deeper in a chain earns this much more (chain ×2 → ×1.25, ×3 → ×1.5).</summary>
        public const float ChargeChainBonus = 0.25f;
        /// <summary>Any pincer that lands at least this much (chip damage on a giant still counts for something).</summary>
        public const int MinPincerCharge = 2;
        public const int CritCharge = 10;
        public const int InterruptCharge = 25;
        public const int AmplifierCharge = 30;
        // ── Mana orbs (GG-T673, owner 2026-11-22: "make spells cost mana orbs that you regenerate with time and more so with
        //    actions"; "you accumulate mana orbs by striking enemies, killing enemies, doing 'clutch' things") ─────────
        /// <summary>The most orbs a spell may cost: "just enough to show as orbs on the icon itself" (the slot's pips).</summary>
        public const int MaxSpellOrbCost = 4;
        /// <summary>The most orbs a battle's row can hold (owned Mana Orbs are a progression item, GG-T677; the bank's
        /// capacity comes from BattleSetup.OrbCapacity, defaulting to <see cref="ManaBankCapacity"/>).</summary>
        public const int MaxOrbCapacity = 20;
        // ── Owned Mana Orbs (GG-T677, owner 2026-11-22: "collecting 'Mana Orbs' becomes important because that is literally
        //    how many mana orbs you can fill in combat"; ~12-16 found in a straight playthrough, 20 possible) ─────────
        /// <summary>Mana Orbs a new player starts with: "going from one Mana Orb to two doubles your magic casting".</summary>
        public const int StartOwnedOrbs = 1;
        /// <summary>Broken Mana Orbs that can ever drop (each the Manasmith restores into an owned orb).</summary>
        public const int MaxBrokenOrbs = 3;
        /// <summary>Charge for a hero felling a real foe (not remains, eggs, corpses, snake segments or a revived foe).</summary>
        public const int KillCharge = 15;
        /// <summary>Charge per foe a hero's skill (not a spell, so mana never feeds itself) strikes for damage. Pincers keep
        /// their own HP-scaled charge.</summary>
        public const int StrikeCharge = 5;
        /// <summary>Time regen: this much charge every <see cref="RegenIntervalSeconds"/> of the battle clock, about one orb
        /// per 50 clock seconds at an empty bank (the fullness damping slows it as the row fills).</summary>
        public const int RegenCharge = 10;
        public const int RegenIntervalSeconds = 5;

        /// <summary>Global weight on enemy physical strikes (difficulty knob; tuned with BalanceReport after the brains, §14.6).</summary>
        public static float EnemyStrikeMultiplier = 1.2f;
        /// <summary>Enemy MaxHP at spawn, as a share of the stat formula (GG-T262: waves ran a median 34 player actions
        /// against POL-G6's 6–20).</summary>
        public static float EnemyHpScale = 0.45f;
        /// <summary>GG-T321: the party size enemies are designed against (the starting three)…</summary>
        public const int PartyScaleBaseHeroes = 3;
        /// <summary>…and how much stronger (strike and MaxHP) foes get for each hero fielded beyond it (<see cref="BattleEngine.PartyScale"/>).</summary>
        public static float EnemyScalePerExtraHero = 0.3f;
        /// <summary>An Ambusher lies in wait for at most this many of its own turns, then comes out (GG-T262: a lone
        /// ambushing naga held the Arkgrave's second stage's last wave to a median 116 player actions).</summary>
        public const int AmbushPatienceTurns = 3;

        // ── Masterwork crafting (§24.6.2, GG-T46) ────────────────────────────────────
        /// <summary>A Masterwork piece's durability ceiling over the content baseline.</summary>
        public const float MasterworkDurabilityMultiplier = 1.25f;

        // ── Armor plates (§13.5) and corpses (§14.7) ─────────────────────────────────
        /// <summary>Plate HP grows this much per enemy level above 1.</summary>
        public const float PlatePerLevel = 0.15f;
        /// <summary>Player actions a corpse lies on the board before it rots away.</summary>
        public const int CorpseDecayActions = 6;
        /// <summary>Gravestone/bone-pile HP as a share of the slain owner's MaxHP (§14.5, GG-T261): a marker to break,
        /// not a second body; at the owner's own level they out-lasted the party in every Halcyon wave.</summary>
        public const float RemainsHpFraction = 0.25f;
        /// <summary>Remains rise on this many of their own turns, not the first (GG-T261): every action runs the clock to
        /// the next enemy turn (§2.0), so a marker left by the last foe standing used to rise inside the killing action.</summary>
        public const int RemainsTurnsToRise = 3;

        // ── Role identity (§13.1.5) ─────────────────────────────────────────────────
        public const float CasterPowerPerPoint = 0.035f;
        public const float CasterPowerMin = 0.7f;
        public const float CasterPowerMax = 1.6f;

        // ── Pickups (§13.3.1) ──────────────────────────────────────────────────────
        /// <summary>Chance a fallen enemy leaves a pickup…</summary>
        public const float PickupDropChance = 0.2f;
        /// <summary>…plus this per point of the killer's Luck…</summary>
        public const float PickupDropChancePerLuck = 0.004f;
        /// <summary>…capped here.</summary>
        public const float PickupMaxChance = 0.4f;
        /// <summary>Player actions a pickup stays on the board before it fades.</summary>
        public const int PickupLifetimeActions = 4;
        /// <summary>Never more than this many pickups on the board at once.</summary>
        public const int MaxPickups = 3;

        // ── Wanderers (§25.11.2, GG-T342): the trust gauge, in one place ───────────
        /// <summary>A wanderer's full trust gauge.</summary>
        public const int WandererTrustNeeded = 6;
        /// <summary>Trust when a hero pincer includes the wanderer (an end or a flanking supporter): three pincers fill it.</summary>
        public const int TrustPerPincer = 2;
        /// <summary>Trust when a commanded hero restores some of the wanderer's HP.</summary>
        public const int TrustPerHeal = 1;

        /// <summary>Knockback into another unit: both take this (+ half the blow).</summary>
        public const int KnockbackCrashDamage = 8;
        /// <summary>Knockback into stone or the board edge: the target takes this (+ the full blow).</summary>
        public const int KnockbackSlamDamage = 18;

        // ── Training prices (GG-T215, §25.6, MetaServices.Abilities.Price) ──────────
        /// <summary>A spell's trainer price is this plus <see cref="SpellTrainPricePerOrb"/> × its orb cost, so a
        /// cheap spell and a bank-emptying one are priced apart (Fireball 240G, Meteor 920G at cost 10).</summary>
        public const int SpellTrainBasePrice = 120;
        public const int SpellTrainPricePerOrb = 80;
        /// <summary>GG-T215: a skill used to be a flat 300G regardless of what it did. Skills don't carry an orb
        /// cost to price by, but <c>CooldownTurns</c> is the next best power proxy (the owner's note on the
        /// RELEASE_SCOPE watch-list: "a skill is a flat 300 G whatever it does") — a 1-turn jab (CooldownTurns 1)
        /// trains for 200G, a 6-turn revive or ultimate for 450G. The base+slope is picked so the weighted average
        /// across the current 146 skills (mode CooldownTurns=3) lands within a few gold of the old flat 300.</summary>
        public const int SkillTrainBasePrice = 150;
        public const int SkillTrainPricePerCooldown = 50;

        // ── The Crucible horde ramp (§22.10, GG-T430): every wave number in one place ─────────
        /// <summary>Waves generated per run. Effectively endless: levels, counts and elites climb every wave, so a
        /// party falls long before the last one (clearing them all would be a victory, committed the same way).</summary>
        public const int CrucibleMaxWaves = 99;
        /// <summary>Enemy levels gained per wave cleared (wave 1 is at the party's own level).</summary>
        public const int CrucibleLevelStep = 1;
        /// <summary>Foes in wave 1, and the cap.</summary>
        public const int CrucibleFirstWaveEnemies = 2;
        public const int CrucibleMaxWaveEnemies = 5;
        /// <summary>Every this many waves, one more foe joins each wave (up to <see cref="CrucibleMaxWaveEnemies"/>).</summary>
        public const int CrucibleWavesPerExtraEnemy = 3;
        /// <summary>The first wave (0-based) with an elite: a foe <see cref="CrucibleEliteLevelBonus"/> levels above its wave.</summary>
        public const int CrucibleFirstEliteWave = 4;
        /// <summary>After the first elite, one more elite every this many waves (up to <see cref="CrucibleMaxElites"/>).</summary>
        public const int CrucibleWavesPerElite = 5;
        public const int CrucibleMaxElites = 3;
        public const int CrucibleEliteLevelBonus = 3;
        /// <summary>Gold banked for clearing a wave: base + per-wave × wave number (1-based) — wave 1 pays 15, wave 10 pays 60.
        /// Kept however the run ends, on top of every coin the foes drop.</summary>
        public const int CrucibleWaveGoldBase = 10;
        public const int CrucibleWaveGoldPerWave = 5;

        // ── Endless-run stall guard (GG-T61, GG-T588; App/Boot/EndlessStability) ─────────
        /// <summary>A `-demo -endless` encounter that has taken more than this many player actions ends as "stalled"
        /// (one -demo battle needed 412). `-stallTurns N` overrides it for a run.</summary>
        public const int StallActionCap = 600;

        // ── Combo (§1.2.8, GG-T849): display and stats only, first-pass numbers (balance: GG-T276) ─────────
        public static int ComboPincerPoints = 10;
        public static int ComboChainLinkPoints = 15;
        public static int ComboSupportPoints = 8;
        public static int ComboFollowUpPoints = 10;
        public static int ComboStatusPoints = 6;
        public static int ComboKillPoints = 12;
        public static int ComboTileKillPoints = 18;
        /// <summary>Variety: a contributor, a kind, or an effect new to the window adds these (not on the first credit).</summary>
        public static int ComboNewContributorBonus = 6;
        public static int ComboNewKindBonus = 4;
        public static int ComboNewEffectBonus = 3;
        /// <summary>Scores reaching D, C, B, A, S, SS, SSS (a repeat of the same unit/kind/effect halves each time).</summary>
        public static readonly int[] ComboRankThresholds = { 10, 30, 60, 100, 150, 220, 300 };
        /// <summary>…or that has run longer than this many real seconds (`-stallSeconds S` overrides it).</summary>
        public const float StallRealSecondsCap = 900f;
        /// <summary>…or that has sat this many real seconds with no action taken and nothing animating.</summary>
        public const float StallIdleSecondsCap = 75f;
    }
}
