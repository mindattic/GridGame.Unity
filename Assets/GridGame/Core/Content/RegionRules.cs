using System;
using System.Collections.Generic;

namespace GridGame.Core.Content
{
    // ── Region rules (GG-T359, BIBLE §14.20) ────────────────────────────────────────────────────────────────────
    // A campaign theme or a stage names the board rules it plays under (campaign.json / stages.json "Rules"); each rule
    // is a regionrules.json record: the tile states it can paint on the board, the telegraphed events it can put on the
    // timeline, and the hooks that run its effects. The battle runs them in BattleRunner.Region.cs. Monster abilities
    // (AbilityDef.RuleEffects) and deaths (ClassDef.OnDeathRule) run the same effects, and a class can shrug a rule,
    // a tile state, an event or an ability off (ClassDef.RuleImmune). A rule's name and description are the Prose
    // "rule:<id>" description text (name + short), never JSON.

    /// <summary>When a region rule's hook runs.</summary>
    public enum RuleHook
    {
        /// <summary>Once, as the battle is set up (before wave 1 spawns).</summary>
        BattleStart,
        /// <summary>Each wave's spawn (wave 1 included), after its foes are on the board.</summary>
        WaveSpawn,
        /// <summary>A unit's own timeline turn begins (an enemy or independent ally's icon arrives; a hero's at each party window).</summary>
        TurnStart,
        /// <summary>A unit's own timeline turn has ended.</summary>
        TurnEnd,
        /// <summary>A unit moved into a tile (a step, a shove, a pull, a rule's own move).</summary>
        EnterTile,
        /// <summary>A unit fell.</summary>
        UnitDeath,
        /// <summary>A unit was struck by a foe's blow or ability and lives (GG-T368: a cracked floor cracks under the blow).</summary>
        Hit,
    }

    /// <summary>What one rule effect does.</summary>
    public enum RuleEffectKind
    {
        /// <summary>Paint <see cref="RuleEffectDef.State"/> on the area (counter starts at Amount). A blocking state skips occupied tiles.</summary>
        MarkTiles,
        /// <summary>Clear the area's marks (only <see cref="RuleEffectDef.State"/> when set).</summary>
        ClearTiles,
        /// <summary>Add Amount to the counter of the area's marks (only State when set); at the state's Threshold it becomes its Becomes state.</summary>
        ModifyTiles,
        /// <summary>Reveal the area's hidden marks (only State when set) to everyone for good (GG-T843, §14.23: a hidden
        /// mine a scout's flare or a sensing ability uncovers); each one revealed is a <see cref="Battle.TileRevealedEvent"/>.</summary>
        RevealTiles,
        /// <summary>Amount damage of DamageType to the units (environmental: no stagger, no attacker).</summary>
        Damage,
        /// <summary>Heal the units by Amount.</summary>
        Heal,
        /// <summary>Apply Buff to the units.</summary>
        ApplyBuff,
        /// <summary>Move the units up to Amount tiles (Move: toward/away from the Anchor or the area's first tile, or a direction).</summary>
        MoveUnits,
        /// <summary>Put Event on the timeline with the area as its telegraphed tiles, landing in Seconds (0 = the event's own).</summary>
        Schedule,
        /// <summary>Change the timers of every queued Event (all queued hazards when Event is null): Seconds later (negative = sooner), or Cancel.</summary>
        AdjustEvents,
        /// <summary>Set the named Anchor on the area's first tile.</summary>
        PlaceAnchor,
        /// <summary>Move the named Anchor Amount tiles (Move: a direction, or toward/away from the area's first tile).</summary>
        MoveAnchor,
        /// <summary>Raise a board-wide Flag (e.g. <see cref="RuleFlags.MaskTimeline"/>).</summary>
        SetFlag,
        /// <summary>Lower a board-wide Flag.</summary>
        ClearFlag,
        /// <summary>Mint Amount mana orbs into the party's bank for each hero among the units (GG-T376).</summary>
        GrantOrbs,
        /// <summary>Put a foe of <see cref="RuleEffectDef.Class"/> (at <see cref="RuleEffectDef.Level"/>, 0 = the stage's level)
        /// on each tile of the area, or the nearest free tile when it is taken; it joins the current wave like a
        /// reinforcement (GG-T844: a slime glob nobody squished hatches into a slime).</summary>
        SpawnUnit,
        /// <summary>Add Amount to the party's mana meter (charge, <see cref="Battle.BattleConfig.ManaChargePerOrb"/> to an orb)
        /// once, when a hero is among the units (GG-T894: a hero squishing a slime glob banks a little charge).</summary>
        GrantCharge,
        /// <summary>GG-T901 (§14.23, the scorpion's burrow ambush): the subject (the caster) goes under: it leaves the board
        /// (untargetable, off every tile, still holding its wave open) under a <see cref="RuleEffectDef.State"/> mark on its
        /// tile, and <see cref="RuleEffectDef.Event"/> is queued on the tile of its nearest foe (Seconds, 0 = the event's
        /// own), where it will surface. The ability that sent it under is the blow it surfaces with.</summary>
        Burrow,
        /// <summary>GG-T901: the burrowed owner of the mark (a hero stepping on its burrow) is forced up at once beside it,
        /// its queued ambush called off. Revealing the mark (RevealTiles, an element) does the same.</summary>
        Unburrow,
        /// <summary>GG-T901: the event's burrowed source surfaces on the event's tile (the nearest free tile when someone
        /// stands there) and strikes whoever stands there with the ability that sent it under.</summary>
        Surface,
    }

    /// <summary>Which tiles a rule effect works on.</summary>
    public enum RuleArea
    {
        /// <summary>The subject's tile (the unit whose turn/step/death it is, the caster), or where it fell.</summary>
        Self,
        /// <summary>An ability's anchor tile (falls back to Self).</summary>
        Target,
        /// <summary>The named <see cref="RuleEffectDef.Anchor"/>'s tile.</summary>
        Anchor,
        /// <summary>The telegraphed tiles of the event resolving (or the ability's shape).</summary>
        EventTiles,
        /// <summary>The listed <see cref="RuleEffectDef.Tiles"/>.</summary>
        Tiles,
        /// <summary>Row <see cref="RuleEffectDef.Index"/> (0 = top).</summary>
        Row,
        /// <summary>Column <see cref="RuleEffectDef.Index"/> (0 = left).</summary>
        Column,
        /// <summary><see cref="RuleEffectDef.Count"/> random tiles free for something new to appear, unmarked.</summary>
        RandomFree,
        /// <summary>The tiles of <see cref="RuleEffectDef.Count"/> random living units of <see cref="RuleEffectDef.Team"/>.</summary>
        RandomUnits,
        /// <summary>Every tile marked with <see cref="RuleEffectDef.State"/>.</summary>
        WithState,
        /// <summary>The whole board.</summary>
        All,
        /// <summary>GG-T869: <see cref="RuleEffectDef.Count"/> tiles in a straight line from the subject toward the Target,
        /// starting at the tile next to the subject (the Target's dominant axis; a diagonal pick leans to its longer leg).
        /// The Barbarian's Earthsplitter cracks the row ahead of it this way.</summary>
        Line,
    }

    /// <summary>Which units a unit effect (Damage, Heal, ApplyBuff, MoveUnits) reaches; <see cref="RuleEffectDef.Team"/> filters them.</summary>
    public enum RuleUnits { OnArea, Subject, All }

    public enum RuleTeam { Any, Hero, Enemy }

    /// <summary>Which way MoveUnits/MoveAnchor go: toward/away from a point, or a board direction.</summary>
    public enum RuleMove { Toward, Away, Up, Down, Left, Right }

    /// <summary>Board-wide flags the battle and the view read (rules may raise any name; these have a meaning).</summary>
    public static class RuleFlags
    {
        /// <summary>The view masks enemy turn icons on the timeline (GG-T365's darkness builds on it).</summary>
        public const string MaskTimeline = "mask-timeline";
        /// <summary>Foes out of every hero's sight (<see cref="Battle.RegionBoard.DarkSight"/>) and off any lit tile are hidden on
        /// the board and on the timeline (GG-T365).</summary>
        public const string Darkness = "darkness";
    }

    /// <summary>One effect a hook, an event, an ability or a death runs.</summary>
    public sealed class RuleEffectDef
    {
        public RuleEffectKind Kind;
        public RuleArea Area = RuleArea.Self;
        public List<Coord> Tiles = new List<Coord>();
        /// <summary>Row/Column index.</summary>
        public int Index;
        /// <summary>RandomFree/RandomUnits count.</summary>
        public int Count = 1;
        /// <summary>Grows the area by this Manhattan radius.</summary>
        public int Radius;
        public string Anchor;
        /// <summary>Tile state id (MarkTiles; the filter for ClearTiles, ModifyTiles and the WithState area).</summary>
        public string State;
        public RuleUnits Units = RuleUnits.OnArea;
        public RuleTeam Team = RuleTeam.Any;
        /// <summary>Damage, heal, counter delta, tiles moved; the starting counter for MarkTiles.</summary>
        public int Amount;
        public DamageType DamageType = DamageType.Physical;
        public string Buff;
        public RuleMove Move = RuleMove.Toward;
        public string Event;
        /// <summary>Schedule: seconds until it lands (0 = the event's own). AdjustEvents: seconds later (negative = sooner).</summary>
        public float Seconds;
        public bool Cancel;
        public string Flag;
        /// <summary>MarkTiles: leave a tile that already holds another state alone (after any elemental reaction), so a
        /// monster's trail never paints over a crack, a barricade or another rule's mark (GG-T370).</summary>
        public bool KeepMarks;
        /// <summary>MoveUnits: a unit stopped by another unit in its way takes this much damage, and so does that unit (GG-T378).</summary>
        public int CollideDamage;
        /// <summary>SpawnUnit: the class id of the foe it puts on the board (GG-T844).</summary>
        public string Class;
        /// <summary>SpawnUnit: its level before the wave's dial (a reinforcement's rule); 0 = the stage's recommended level.
        /// With <see cref="FromOwnerLevel"/> it is an offset instead (may be negative).</summary>
        public int Level;
        /// <summary>SpawnUnit (GG-T894): the spawn's level is its painter's level (the mark's owner, already dialled for the
        /// wave) plus <see cref="Level"/>, at least 1; no owner = the stage's level. A slime's glob hatches a slime a level below it.</summary>
        public bool FromOwnerLevel;
        /// <summary>Schedule (GG-T874): the queued event rides the unit standing on the area's tile (the ability's
        /// Target, else Self): its telegraphed tiles are that carrier's footprint grown by <see cref="Radius"/> and follow it
        /// as it moves, shoved or walking, until it lands; a carrier that falls drops it where it fell. No unit there, no event.</summary>
        public bool Follow;
    }

    /// <summary>Whom a team-owned tile state works on (GG-T842, BIBLE §14.23): a mark records the team that painted it, and
    /// its state's EnterTile/TurnEnd hooks and OnMark effects reach only these units. A mark the board itself painted (a
    /// rule's hook or event, no owner) works on everyone, whatever this says.</summary>
    public enum TileAffects
    {
        /// <summary>Everyone (today's rule, the default).</summary>
        Any,
        /// <summary>Only the painter's foes: a hero's firewall burns monsters, never the party.</summary>
        FoesOfOwner,
        /// <summary>Only the painter's own side (a ward, a healing glyph).</summary>
        AlliesOfOwner,
    }

    /// <summary>A persistent tile state a rule paints on the board (drawn on the tile; a mark lives until cleared or its LifeSeconds run out).</summary>
    public sealed class TileStateDef
    {
        public string Id;
        /// <summary>Nothing may enter, land on or spawn on it (a barricade, a collapsed floor). Never painted under someone.</summary>
        public bool Blocks;
        /// <summary>Harmful to stand on: the bot and the hero planner steer clear (immune units excepted).</summary>
        public bool Hazard;
        /// <summary>Counter value at which it turns into <see cref="Becomes"/> (0 = never).</summary>
        public int Threshold;
        /// <summary>The state it becomes at <see cref="Threshold"/>; null = the mark clears.</summary>
        public string Becomes;
        /// <summary>Clock seconds the mark lasts (0 = until cleared).</summary>
        public float LifeSeconds;
        /// <summary>Clock seconds a fresh mark is seen by everyone before it hides (GG-T843, §14.23; 0 = always seen): then
        /// only its painter's side still sees it (the view draws it faint for the party's own), and a board-painted mark
        /// hides from both. It shows again, for good, when it triggers (one of its hooks runs), a RevealTiles effect
        /// uncovers it, or an element hits it (painted onto it, or a hero's element or tool covering it, GG-T370). The bot
        /// and the planners of a side that can't see a mark read no danger from it (<see cref="Battle.RegionBoard.Sees"/>).</summary>
        public float VisibleSeconds;
        /// <summary>Hex colour of the tile wash.</summary>
        public string Color;
        /// <summary>A short glyph drawn on the tile.</summary>
        public string Glyph;
        /// <summary>A hole (GG-T368; needs <see cref="Blocks"/>): a unit caught on the tile as it opens, or knocked or pulled
        /// into it, falls: it loses <see cref="FallDamage"/> of its max HP and climbs out on the nearest free tile.</summary>
        public bool Pit;
        /// <summary>GG-T869 (§14.21): a Heavy unit (<see cref="ClassDef.Heavy"/>, not floating) on a mark of this state, as it
        /// is painted or as the unit enters it, breaks through at once: the tile becomes this state (a hole) and the unit
        /// falls (<see cref="Battle.BrokeThroughEvent"/>, then its fall). Null = a Heavy unit treads it like anyone.</summary>
        public string HeavyBecomes;
        /// <summary>The share of max HP a fall into this <see cref="Pit"/> costs (environmental, at least 1).</summary>
        public float FallDamage = 0.15f;
        /// <summary>Effects run on each tile as it takes this state (painted, or a counter turning into it): Self is that
        /// tile, the unit standing there (if any) the subject. A crumbling floor schedules its own cave-in this way.</summary>
        public List<RuleEffectDef> OnMark = new List<RuleEffectDef>();
        /// <summary>The element this state carries (GG-T370: fire, ice). Painted onto a tile holding a state that its
        /// elements.json row reacts to (GG-T371 <see cref="ElementDef.Tiles"/>: fire melts ice, ice douses fire, fire burns
        /// slime away, fire sets a mine off), the reaction runs on that tile and the incoming state is spent.</summary>
        public DamageType? Element;
        /// <summary>A light (GG-T365): under <see cref="RuleFlags.Darkness"/> a unit standing on it is never hidden.</summary>
        public bool Lights;
        /// <summary>Crit chance (0.15 = +15 points) a striker gains against a foe of its side standing on this state, when the
        /// mark is its own side's (a board-painted mark: any hero's), on weapon blows and spells alike (GG-T888: the
        /// Hexstalker's flare light). Several tiles under a big foe don't add up; the highest counts.</summary>
        public float CritTakenBonus;
        /// <summary>Smoke (GG-T861, the Black Ninja's Smoke Vanish): a blow or charge aimed at a unit standing on this state,
        /// by an attacker the mark works on (<see cref="Affects"/>), is rolled as if the attacker were Blinded (the
        /// <c>blinded</c> buff's accuracy; no extra Rng draw, never stacked on a real Blind).</summary>
        public bool Obscures;
        /// <summary>Whom a mark of this state works on, read against the team that painted it (GG-T842): its hooks that
        /// read the subject's tile (EnterTile, TurnEnd...) skip an unaffected subject, its OnMark and hook effects skip
        /// unaffected units, and <see cref="Battle.RegionBoard.DangerAt"/> reads no danger for them. Ignored on a mark the
        /// board painted (no owner).</summary>
        public TileAffects Affects = TileAffects.Any;
        /// <summary>Effects run on each mark whose <see cref="LifeSeconds"/> run out, before it clears (GG-T844): Self is the
        /// tile, the unit standing there (if the mark works on it) the subject. A slime glob hatches a slime this way.</summary>
        public List<RuleEffectDef> OnExpire = new List<RuleEffectDef>();
        /// <summary>A unit of this team entering the tile squishes the mark (GG-T844): it clears with a
        /// <see cref="Battle.SquishedEvent"/>, then <see cref="OnSquish"/> runs. Null = nothing squishes it.</summary>
        public RuleTeam? SquishedBy;
        /// <summary>Effects run as a unit squishes the mark (Self is the tile, the squisher the subject).</summary>
        public List<RuleEffectDef> OnSquish = new List<RuleEffectDef>();
        /// <summary>A ritual circle (GG-T902, BIBLE §14.17: the Choir's runes): when <see cref="RuneCircleDef.Size"/> units
        /// of the painters' side stand on marks of this state at once, the circle completes. Null = a plain mark.</summary>
        public RuneCircleDef Circle;
    }

    /// <summary>
    /// GG-T902 (BIBLE §14.17): what completes a circle of rune marks and what it does. A unit entering a mark of the state
    /// (its own side's, or the board's) counts the living units of its side standing on such marks; at <see cref="Size"/>
    /// the circle completes: <see cref="OnSummon"/> runs while fewer than <see cref="SummonBelow"/> of that side stand,
    /// <see cref="OnEmpower"/> otherwise (Self is the completing unit's tile, it the subject and the owner; the runes are
    /// still on the board, so a WithState area reaches the singers on them), then every rune of the state clears.
    /// </summary>
    public sealed class RuneCircleDef
    {
        /// <summary>How many units must stand on runes at once.</summary>
        public int Size = 3;
        /// <summary>The circle summons while fewer than this many of its side are alive (0 = never summons).</summary>
        public int SummonBelow;
        public List<RuleEffectDef> OnSummon = new List<RuleEffectDef>();
        public List<RuleEffectDef> OnEmpower = new List<RuleEffectDef>();
    }

    /// <summary>A telegraphed event a rule puts on the timeline: its own icon rides the bar like a cast and resolves its Effects on arrival.</summary>
    public sealed class RuleEventDef
    {
        public string Id;
        /// <summary>Seconds from queued to landing (a Schedule may override it).</summary>
        public float Seconds = 4f;
        /// <summary>Re-arms for the same Seconds and tiles after it lands (a rule's heartbeat).</summary>
        public bool Repeat;
        /// <summary>Its telegraphed tiles are dangerous (drawn as a warning; the bot steps off them).</summary>
        public bool Hazard = true;
        /// <summary>Glyph on its timeline icon and its warning tiles.</summary>
        public string Glyph;
        public string Color;
        /// <summary>Optional UI icon name (Resources/Icons) for the timeline diamond.</summary>
        public string Icon;
        public List<RuleEffectDef> Effects = new List<RuleEffectDef>();
    }

    /// <summary>A hook: when <see cref="On"/> happens (and the filters hold), run the effects.</summary>
    public sealed class RuleHookDef
    {
        public RuleHook On;
        /// <summary>Whose turn/step/death it must be.</summary>
        public RuleTeam Team = RuleTeam.Any;
        /// <summary>The subject must stand on this tile state (TurnStart/TurnEnd/EnterTile/UnitDeath).</summary>
        public string State;
        /// <summary>Only in this wave (0-based); -1 = every wave.</summary>
        public int Wave = -1;
        /// <summary>Runs every Nth time it matches (1 = every time).</summary>
        public int Every = 1;
        public float Chance = 1f;
        /// <summary>GG-T869: runs only when the rule is the stage's own (its theme's or stage's list), not one an ability or a
        /// trail brought into play by painting its state, so a hero's cracks never turn every wave into a cracking floor.</summary>
        public bool StageOnly;
        public List<RuleEffectDef> Effects = new List<RuleEffectDef>();
    }

    /// <summary>One region rule (regionrules.json).</summary>
    public sealed class RegionRuleDef
    {
        public string Id;
        /// <summary>From Prose ("rule:&lt;id&gt;" payload "name"); never authored in JSON.</summary>
        public string Name;
        /// <summary>From Prose ("rule:&lt;id&gt;" payload "short"); never authored in JSON.</summary>
        public string Description;
        public List<TileStateDef> TileStates = new List<TileStateDef>();
        public List<RuleEventDef> Events = new List<RuleEventDef>();
        public List<RuleHookDef> Hooks = new List<RuleHookDef>();
    }

    public static class RegionRuleCatalog
    {
        /// <summary>GG-T869 (<see cref="RuleArea.Line"/>): the one-tile step from <paramref name="from"/> toward
        /// <paramref name="to"/> along the longer axis (a tie goes across); (0, 0) when they are the same tile.</summary>
        public static (int dx, int dy) LineStep(Coord from, Coord to)
        {
            int dx = to.X - from.X, dy = to.Y - from.Y;
            if (dx == 0 && dy == 0) return (0, 0);
            return Math.Abs(dx) >= Math.Abs(dy) ? (Math.Sign(dx), 0) : (0, Math.Sign(dy));
        }

        /// <summary>GG-T869: the <paramref name="count"/> tiles straight ahead of <paramref name="from"/> toward
        /// <paramref name="to"/>, nearest first, the first next to <paramref name="from"/>; off-board ones dropped.</summary>
        public static List<Coord> LineTiles(Coord from, Coord to, int count)
        {
            var list = new List<Coord>();
            var (dx, dy) = LineStep(from, to);
            if (dx == 0 && dy == 0) return list;
            for (int i = 1; i <= Math.Max(1, count); i++)
            {
                var c = new Coord(from.X + dx * i, from.Y + dy * i);
                if (Battle.BattleState.InBounds(c)) list.Add(c);
            }
            return list;
        }

        /// <summary>The rules a stage plays under: its theme's (campaign.json), then its own (stages.json), each once.</summary>
        public static List<RegionRuleDef> ActiveFor(ContentDb db, StageDef stage)
        {
            var list = new List<RegionRuleDef>();
            if (db == null || stage == null || db.RegionRules.Count == 0) return list;
            var theme = db.ThemeOf(stage.Id) ?? db.Campaign.Themes.Find(t => string.Equals(t.Id, stage.Theme, StringComparison.OrdinalIgnoreCase));
            void Add(List<string> ids)
            {
                if (ids == null) return;
                foreach (var id in ids)
                    if (db.TryRegionRule(id) is RegionRuleDef r && !list.Contains(r)) list.Add(r);
            }
            Add(theme?.Rules);
            Add(stage.Rules);
            return list;
        }

        /// <summary>Indexes every rule's tile states and events by id (ids are unique across all rules; Validate reports a clash).</summary>
        public static void Index(ContentDb db, List<RegionRuleDef> rules)
        {
            foreach (var r in rules)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) throw new FormatException("RegionRuleDef with empty Id");
                if (db.RegionRules.ContainsKey(r.Id)) throw new FormatException($"Duplicate RegionRuleDef id '{r.Id}'");
                db.RegionRules[r.Id] = r;
                db.RegionRuleOrder.Add(r);
                foreach (var t in r.TileStates) if (t?.Id != null && !db.TileStates.ContainsKey(t.Id)) { db.TileStates[t.Id] = t; db.TileStateRule[t.Id] = r.Id; }
                foreach (var e in r.Events) if (e?.Id != null && !db.RuleEvents.ContainsKey(e.Id)) { db.RuleEvents[e.Id] = e; db.RuleEventRule[e.Id] = r.Id; }
            }
        }

        /// <summary>Cross-reference check of regionrules.json and every reference to it (themes, stages, classes, abilities).</summary>
        public static void Validate(ContentDb db, List<string> p)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in db.RegionRuleOrder)
            {
                string w = $"region rule {r.Id}";
                foreach (var t in r.TileStates)
                {
                    if (t == null || string.IsNullOrEmpty(t.Id)) { p.Add($"{w}: a tile state needs an Id"); continue; }
                    if (!seen.Add(t.Id)) p.Add($"{w}: tile state id '{t.Id}' is used twice (ids are shared by every rule, event and state)");
                    if (t.Threshold < 0) p.Add($"{w} state {t.Id}: Threshold can't be negative");
                    if (t.Becomes != null && db.TryTileState(t.Becomes) == null) p.Add($"{w} state {t.Id}: unknown Becomes state '{t.Becomes}'");
                    if (t.Becomes != null && t.Threshold <= 0) p.Add($"{w} state {t.Id}: Becomes needs a Threshold");
                    if (t.HeavyBecomes != null && !(db.TryTileState(t.HeavyBecomes) is TileStateDef hb && hb.Pit)) p.Add($"{w} state {t.Id}: HeavyBecomes must name a Pit state");
                    if (!(t.LifeSeconds >= 0f)) p.Add($"{w} state {t.Id}: LifeSeconds can't be negative");
                    if (!(t.VisibleSeconds >= 0f)) p.Add($"{w} state {t.Id}: VisibleSeconds can't be negative"); // GG-T843
                    if (!(t.CritTakenBonus >= 0f && t.CritTakenBonus <= 1f)) p.Add($"{w} state {t.Id}: CritTakenBonus outside 0..1"); // GG-T888
                    if (t.Pit && !t.Blocks) p.Add($"{w} state {t.Id}: a Pit must also Block (nothing stands in a hole)");
                    if (t.Pit && !(t.FallDamage >= 0f && t.FallDamage <= 1f)) p.Add($"{w} state {t.Id}: FallDamage outside 0..1");
                    ValidateEffects(db, t.OnMark, $"{w} state {t.Id} OnMark", p);
                    ValidateEffects(db, t.OnExpire, $"{w} state {t.Id} OnExpire", p);
                    ValidateEffects(db, t.OnSquish, $"{w} state {t.Id} OnSquish", p);
                    if (t.OnExpire.Count > 0 && !(t.LifeSeconds > 0f)) p.Add($"{w} state {t.Id}: OnExpire needs LifeSeconds (it never runs out)");
                    if (t.OnSquish.Count > 0 && t.SquishedBy == null) p.Add($"{w} state {t.Id}: OnSquish needs SquishedBy");
                    if (t.SquishedBy != null && t.Blocks) p.Add($"{w} state {t.Id}: a blocking state can't be squished (nobody enters it)");
                    if (t.Circle != null) // GG-T902
                    {
                        if (t.Circle.Size < 2) p.Add($"{w} state {t.Id}: a Circle needs a Size of at least 2");
                        if (t.Blocks) p.Add($"{w} state {t.Id}: a Circle's runes are stood on, so they can't Block");
                        if (t.Circle.OnSummon.Count == 0 && t.Circle.OnEmpower.Count == 0) p.Add($"{w} state {t.Id}: a Circle does nothing (no OnSummon or OnEmpower)");
                        if (t.Circle.SummonBelow > 0 && t.Circle.OnSummon.Count == 0) p.Add($"{w} state {t.Id}: Circle SummonBelow needs OnSummon");
                        ValidateEffects(db, t.Circle.OnSummon, $"{w} state {t.Id} Circle OnSummon", p);
                        ValidateEffects(db, t.Circle.OnEmpower, $"{w} state {t.Id} Circle OnEmpower", p);
                    }
                }
                foreach (var e in r.Events)
                {
                    if (e == null || string.IsNullOrEmpty(e.Id)) { p.Add($"{w}: an event needs an Id"); continue; }
                    if (!seen.Add(e.Id)) p.Add($"{w}: event id '{e.Id}' is used twice (ids are shared by every rule, event and state)");
                    if (!(e.Seconds > 0f)) p.Add($"{w} event {e.Id}: Seconds must be positive (every hazard shows on the timeline ahead of time)");
                    if (e.Effects.Count == 0) p.Add($"{w} event {e.Id}: no Effects");
                    ValidateEffects(db, e.Effects, $"{w} event {e.Id}", p);
                }
                if (r.Hooks.Count == 0 && r.Events.Count == 0) p.Add($"{w}: no Hooks (nothing would ever run it)");
                for (int i = 0; i < r.Hooks.Count; i++)
                {
                    var h = r.Hooks[i];
                    string hw = $"{w} hook {i + 1} ({h?.On})";
                    if (h == null) { p.Add($"{w}: null hook"); continue; }
                    if (h.Every < 1) p.Add($"{hw}: Every must be at least 1");
                    if (!(h.Chance > 0f && h.Chance <= 1f)) p.Add($"{hw}: Chance outside (0, 1]");
                    if (h.State != null && db.TryTileState(h.State) == null) p.Add($"{hw}: unknown tile state '{h.State}'");
                    if (h.State != null && (h.On == RuleHook.BattleStart || h.On == RuleHook.WaveSpawn)) p.Add($"{hw}: a State filter needs a subject (TurnStart, TurnEnd, EnterTile, UnitDeath)");
                    if (h.Effects.Count == 0) p.Add($"{hw}: no Effects");
                    ValidateEffects(db, h.Effects, hw, p);
                }
            }
            foreach (var t in db.Campaign.Themes) NeedRules(db, t.Rules, $"theme {t.Id}", p);
            foreach (var s in db.StageOrder) NeedRules(db, s.Rules, $"stage {s.Id}", p);
            foreach (var c in db.Classes.Values)
            {
                foreach (var id in c.RuleImmune)
                    if (!IsRuleSource(db, id)) p.Add($"class {c.Id}: RuleImmune '{id}' is no region rule, tile state, rule event or ability");
                ValidateEffects(db, c.OnDeathRule, $"class {c.Id} OnDeathRule", p);
                ValidateEffects(db, c.TrailRule, $"class {c.Id} TrailRule", p);
                if (c.TrailEvery < 1) p.Add($"class {c.Id}: TrailEvery must be at least 1");
            }
            foreach (var a in db.Abilities.Values) ValidateEffects(db, a.RuleEffects, $"ability {a.Id} RuleEffects", p);
        }

        private static void NeedRules(ContentDb db, List<string> ids, string where, List<string> p)
        {
            if (ids == null) return;
            var once = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                if (db.TryRegionRule(id) == null) p.Add($"{where}: unknown region rule '{id}'");
                else if (!once.Add(id)) p.Add($"{where}: region rule '{id}' listed twice");
            }
        }

        /// <summary>An id a class may be immune to: a rule, a tile state, a rule event, or an ability (its RuleEffects).</summary>
        public static bool IsRuleSource(ContentDb db, string id) =>
            id != null && (db.TryRegionRule(id) != null || db.TryTileState(id) != null || db.TryRuleEvent(id) != null || db.TryAbility(id) != null);

        public static void ValidateEffects(ContentDb db, List<RuleEffectDef> effects, string where, List<string> p)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                string w = $"{where} effect {i + 1}";
                if (e == null) { p.Add($"{w}: null"); continue; }
                w += $" ({e.Kind})";
                if (e.State != null && db.TryTileState(e.State) == null) p.Add($"{w}: unknown tile state '{e.State}'");
                if (e.Event != null && db.TryRuleEvent(e.Event) == null) p.Add($"{w}: unknown rule event '{e.Event}'");
                if (e.Buff != null && db.TryBuff(e.Buff) == null) p.Add($"{w}: unknown buff '{e.Buff}'");
                if (e.Radius < 0 || e.Radius > 8) p.Add($"{w}: Radius outside 0..8");
                if (e.CollideDamage < 0) p.Add($"{w}: CollideDamage can't be negative");
                switch (e.Area)
                {
                    case RuleArea.Tiles: if (e.Tiles.Count == 0) p.Add($"{w}: area Tiles lists none"); break;
                    case RuleArea.Anchor: if (string.IsNullOrEmpty(e.Anchor)) p.Add($"{w}: area Anchor needs an Anchor name"); break;
                    case RuleArea.WithState: if (e.State == null) p.Add($"{w}: area WithState needs a State"); break;
                    case RuleArea.RandomFree:
                    case RuleArea.RandomUnits: if (e.Count < 1) p.Add($"{w}: Count must be at least 1"); break;
                    case RuleArea.Row: if (e.Index < 0 || e.Index >= Battle.BattleConfig.Rows) p.Add($"{w}: Row {e.Index} is off the board"); break;
                    case RuleArea.Column: if (e.Index < 0 || e.Index >= Battle.BattleConfig.Cols) p.Add($"{w}: Column {e.Index} is off the board"); break;
                }
                switch (e.Kind)
                {
                    case RuleEffectKind.MarkTiles: if (e.State == null) p.Add($"{w}: needs a State"); break;
                    case RuleEffectKind.Burrow: // GG-T901
                        if (e.State == null) p.Add($"{w}: needs a State (the burrow mark)");
                        if (e.Event == null) p.Add($"{w}: needs an Event (the ambush)");
                        break;
                    case RuleEffectKind.Damage:
                    case RuleEffectKind.Heal:
                    case RuleEffectKind.GrantOrbs:
                    case RuleEffectKind.GrantCharge: if (e.Amount <= 0) p.Add($"{w}: Amount must be positive"); break;
                    case RuleEffectKind.ApplyBuff: if (e.Buff == null) p.Add($"{w}: needs a Buff"); break;
                    case RuleEffectKind.MoveUnits:
                    case RuleEffectKind.MoveAnchor:
                        if (e.Amount <= 0) p.Add($"{w}: Amount (tiles) must be positive");
                        if (e.Kind == RuleEffectKind.MoveAnchor && string.IsNullOrEmpty(e.Anchor)) p.Add($"{w}: needs an Anchor");
                        break;
                    case RuleEffectKind.Schedule:
                        if (e.Event == null) p.Add($"{w}: needs an Event");
                        if (e.Seconds < 0f) p.Add($"{w}: Seconds can't be negative");
                        if (e.Follow && e.Area != RuleArea.Target && e.Area != RuleArea.Self) p.Add($"{w}: Follow needs a Target or Self area (its carrier's tile)");
                        break;
                    case RuleEffectKind.AdjustEvents: if (!e.Cancel && e.Seconds == 0f) p.Add($"{w}: needs Seconds or Cancel"); break;
                    case RuleEffectKind.PlaceAnchor: if (string.IsNullOrEmpty(e.Anchor)) p.Add($"{w}: needs an Anchor"); break;
                    case RuleEffectKind.SetFlag:
                    case RuleEffectKind.ClearFlag: if (string.IsNullOrEmpty(e.Flag)) p.Add($"{w}: needs a Flag"); break;
                    case RuleEffectKind.SpawnUnit:
                        if (string.IsNullOrEmpty(e.Class)) p.Add($"{w}: needs a Class");
                        else if (db.TryClass(e.Class) == null) p.Add($"{w}: unknown class '{e.Class}'");
                        if (e.Level < 0 && !e.FromOwnerLevel) p.Add($"{w}: Level can't be negative");
                        break;
                }
            }
        }
    }
}
