using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Everything that happens in a battle is reported as an ordered stream of these. The
    /// simulation has already finished by the time the presentation sees them; the view
    /// only plays them back (one awaited animation each).
    /// </summary>
    public abstract class BattleEvent
    {
        public override string ToString() => GetType().Name;
    }

    public enum HitKind { Normal, Critical, Miss }

    public sealed class ActionBeganEvent : BattleEvent
    {
        public int ActorId; public string Label;
        public override string ToString() => $"ActionBegan {ActorId} '{Label}'";
    }

    /// <summary>GG-T188 (§2.0): the held hero's turn ran out — the next enemy icon reached ACT, so the hero was dropped
    /// where it stood and the turn ends (even with no step taken).</summary>
    public sealed class TurnTimeUpEvent : BattleEvent
    {
        public int HeroId; public float HeldSeconds;
        public override string ToString() => $"TurnTimeUp {HeroId} after {HeldSeconds:0.00}s";
    }

    public sealed class SteppedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To;
        public override string ToString() => $"Stepped {UnitId} {From}->{To}";
    }

    public sealed class DisplacedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To; public int ByUnitId;
        public override string ToString() => $"Displaced {UnitId} {From}->{To} by {ByUnitId}";
    }

    public sealed class StepBlockedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public string Reason;
        public override string ToString() => $"StepBlocked {UnitId} at {At} ({Reason})";
    }

    /// <summary>
    /// Last-resort net (two actors must never share a tile, GG-T97): the engine caught one anyway after
    /// <see cref="Command"/>, so it's a bug upstream — this is the diagnostic marker; the board
    /// self-heals in the same beat via the accompanying <see cref="DisplacedEvent"/> (or
    /// <see cref="AmplifierMovedEvent"/>). Under <see cref="BattleEngine.StrictTiles"/> it throws instead.
    /// <see cref="OtherUnitId"/> is -1 when the other occupant is a movable tile (the Power Amplifier).
    /// </summary>
    public sealed class TileConflictEvent : BattleEvent
    {
        public int UnitId; public int OtherUnitId; public Coord At; public string Command;
        public override string ToString() => $"TileConflict {UnitId} vs {(OtherUnitId < 0 ? "amplifier" : OtherUnitId.ToString())} at {At} (after {Command})";
    }

    /// <summary>Thrown instead of self-healing when <see cref="BattleEngine.StrictTiles"/> is on (tests, soak, GG-T97).</summary>
    public sealed class TileConflictException : System.Exception
    {
        public TileConflictException(string message) : base(message) { }
    }

    public sealed class TeleportedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To;
        public override string ToString() => $"Teleported {UnitId} {From}->{To}";
    }

    /// <summary>
    /// A unit's footprint (anchor tile + size) frozen at one moment of the event stream. Events that the view draws
    /// geometry from carry these because the state the view holds is already the END of the command: a unit that
    /// moved later in the same command (an enemy turn, knockback, a later chain link) is somewhere else by then (GG-T98).
    /// </summary>
    public struct UnitFootprint
    {
        public int UnitId; public Coord At; public int W; public int H;
        public static UnitFootprint Of(Unit u) => new UnitFootprint { UnitId = u.Id, At = u.Pos, W = u.W, H = u.H };
        public override string ToString() => $"{UnitId}@{At}{(W > 1 || H > 1 ? $" {W}x{H}" : "")}";
    }

    public sealed class PincerFormedEvent : BattleEvent
    {
        public Team Team; public int Attacker1; public int Attacker2;
        public List<int> Opponents = new List<int>();
        public List<int> Supporters = new List<int>();
        public int ChainIndex;
        /// <summary>Formation-time geometry (GG-T98): where each member stood the moment this pincer formed.
        /// <see cref="OpponentsAt"/>/<see cref="SupportersAt"/> run parallel to <see cref="Opponents"/>/<see cref="Supporters"/>.</summary>
        public UnitFootprint Attacker1At, Attacker2At;
        public List<UnitFootprint> OpponentsAt = new List<UnitFootprint>();
        public List<UnitFootprint> SupportersAt = new List<UnitFootprint>();
        /// <summary>Parallel to <see cref="Supporters"/> (GG-T187): the unit each supporter's chain connects through —
        /// an attacker when adjacent, else the next ally along the chain — so the view can draw the chain link by link.</summary>
        public List<int> SupporterLinks = new List<int>();
        public override string ToString() => $"Pincer {Team} {Attacker1}+{Attacker2} vs [{string.Join(",", Opponents)}] sup[{string.Join(",", Supporters)}] #{ChainIndex}";

        /// <summary>Null when this pincer's formation geometry is a legal line; otherwise what's wrong with it.</summary>
        public string GeometryError() => PincerGeometry.Error(Attacker1At, Attacker2At, OpponentsAt);
    }

    /// <summary>
    /// The shape a pincer outline may take (GG-T98 guard). A pincer is two attackers in one row or column with every
    /// tile between them covered by opponents, so the group is one tile thick across its axis — a 2×2 opponent may
    /// widen it to its own size, nothing else can. A multi-tile box is always a geometry bug upstream.
    /// </summary>
    public static class PincerGeometry
    {
        public static string Error(UnitFootprint a, UnitFootprint b, IEnumerable<UnitFootprint> opponents)
        {
            if (a.At == b.At) return $"attackers share {a.At}";
            bool row = a.At.Y == b.At.Y;
            if (!row && a.At.X != b.At.X) return $"attackers {a.At} and {b.At} are not in one row or column";
            int line = row ? a.At.Y : a.At.X;
            int lo = row ? Math.Min(a.At.X, b.At.X) : Math.Min(a.At.Y, b.At.Y);
            int hi = row ? Math.Max(a.At.X, b.At.X) : Math.Max(a.At.Y, b.At.Y);
            int perpMin = line, perpMax = line, widest = 1, multiTile = 0, count = 0;
            foreach (var o in opponents)
            {
                count++;
                int along0 = row ? o.At.X : o.At.Y, alongSize = Math.Max(1, row ? o.W : o.H);
                int perp0 = row ? o.At.Y : o.At.X, perpSize = Math.Max(1, row ? o.H : o.W);
                if (along0 <= lo || along0 + alongSize - 1 >= hi) return $"opponent {o} is not strictly between {a.At} and {b.At}";
                if (line < perp0 || line > perp0 + perpSize - 1) return $"opponent {o} does not cover the line {(row ? "row" : "column")} {line}";
                perpMin = Math.Min(perpMin, perp0); perpMax = Math.Max(perpMax, perp0 + perpSize - 1);
                widest = Math.Max(widest, perpSize);
                if (perpSize > 1) multiTile++;
            }
            if (count == 0) return "no opponents";
            int thickness = perpMax - perpMin + 1;
            // Two big opponents straddling the line in opposite directions are the one legal way past one footprint.
            int allowed = multiTile > 1 ? 2 * widest - 1 : widest;
            if (thickness > allowed) return $"outline {thickness} tiles thick across the line (allowed {allowed})";
            return null;
        }
    }

    /// <summary>The three phases one pincer resolves in (§1.2.4, GG-T833): supporters that help the attacker act, the
    /// attackers strike, then the follow-ups land.</summary>
    public enum PincerPhase
    {
        /// <summary>Supporters that help the attacker (Crit, Buff on Attacker/Chain/Party, Heal, Haste, Cleanse, Mana).</summary>
        Prep,
        /// <summary>Both attackers strike the line.</summary>
        Blows,
        /// <summary>Double Attack, OnPincerAsAttacker triggers, then the Strike/Cast/Status/Pushback (and Self Buff) supporters.</summary>
        FollowUp,
    }

    /// <summary>Brackets one phase of one pincer (GG-T833): emitted with <see cref="End"/> false as the phase opens and
    /// true as it closes, so the view can tell "buffed before the blow" from "followed up after it".
    /// <see cref="PincerIndex"/> is the pincer's <see cref="PincerFormedEvent.ChainIndex"/>.</summary>
    public sealed class PincerPhaseEvent : BattleEvent
    {
        public PincerPhase Phase; public int PincerIndex; public bool End;
        public override string ToString() => $"PincerPhase {Phase} #{PincerIndex}{(End ? " end" : "")}";
    }

    /// <summary>A chained pincer whose whole line already fell: the attackers spin in triumph (cosmetic, §13.1.1).</summary>
    public sealed class RespiteEvent : BattleEvent
    {
        public int Attacker1; public int Attacker2;
        public override string ToString() => $"Respite {Attacker1}+{Attacker2}";
    }

    public sealed class AttackEvent : BattleEvent
    {
        public int AttackerId; public int TargetId; public bool IsSupport; public bool IsMelee; public bool IsRanged;
        /// <summary>GG-T834: a Volley supporter's thrown hit (playback flies its SupportDef projectile, not a lunge).</summary>
        public bool IsThrown;
        public override string ToString() => $"Attack {AttackerId}->{TargetId}{(IsSupport ? " (support)" : "")}{(IsMelee ? " (melee)" : "")}";
    }

    /// <summary>A supporter performs its class's support action (§1.2.3, GG-T187). The effect's own events (attacks,
    /// heals, buffs, hastes, mana) follow it.</summary>
    public sealed class SupportActionEvent : BattleEvent
    {
        public int SupporterId; public int AttackerId; public SupportEffect Effect; public string Label;
        /// <summary>GG-T298: Cast — the spell from the supporter's kit it casts; Status — the buff id it tries to inflict.</summary>
        public string AbilityId; public string BuffId;
        /// <summary>GG-T298: the effect's magnitude as played (crit fraction, haste/pushback seconds, chance, power).</summary>
        public float Amount;
        public List<int> Targets = new List<int>();
        public override string ToString() => $"Support {SupporterId}->{AttackerId} {Effect} '{Label}' [{string.Join(",", Targets)}]";
    }

    public sealed class DamagedEvent : BattleEvent
    {
        public int TargetId; public int SourceId; public int Amount; public HitKind Hit; public DamageType Type;
        public int HpAfter; public string Note;
        public override string ToString() => $"Damaged {TargetId} -{Amount} {Hit} {Type} hp={HpAfter}{(Note != null ? " " + Note : "")}";
    }

    /// <summary>The defender's Evasion slipped a blow that would have landed (GG-T297, §13.1.0): no damage, a "Dodge!"
    /// float and a whoosh. Stands in for the 0-damage miss <see cref="DamagedEvent"/>; OnDodge reactives follow it.</summary>
    public sealed class DodgedEvent : BattleEvent
    {
        public int UnitId; public int AttackerId;
        public override string ToString() => $"Dodged {UnitId} <- {AttackerId}";
    }

    public sealed class HealedEvent : BattleEvent
    {
        public int TargetId; public int SourceId; public int Amount; public int HpAfter;
        public override string ToString() => $"Healed {TargetId} +{Amount} hp={HpAfter}";
    }

    public sealed class DiedEvent : BattleEvent
    {
        public int UnitId; public Team Team; public Coord At;
        public override string ToString() => $"Died {UnitId}";
    }

    public sealed class LootEvent : BattleEvent
    {
        public int FromUnitId; public int Coins; public List<ItemCount> Items = new List<ItemCount>(); public int Xp;
        public override string ToString() => $"Loot from {FromUnitId}: {Coins}c {Xp}xp items={Items.Count}";
    }

    public sealed class OrbMintedEvent : BattleEvent
    {
        public int SourceUnitId; public bool Landed; public string Reason;
        /// <summary>The bank slot this orb landed in (0-based), captured at mint time so several orbs minted in one
        /// action fan into consecutive slots instead of all flying to the final count (GG-T112). -1 when it overflowed.</summary>
        public int Slot = -1;
        public override string ToString() => $"Orb from {SourceUnitId} {(Landed ? $"landed in slot {Slot}" : "overflow")} ({Reason})";
    }

    public sealed class OrbsSpentEvent : BattleEvent
    {
        public int Count;
        /// <summary>The bank count right after this spend, captured at spend time (GG-T130) so playback redraws
        /// to this value instead of the command's end-of-action bank count, which may already include orbs this
        /// same action mints later (their fly-in would otherwise land on a bank that already looks full).</summary>
        public int CountAfter;
        /// <summary>GG-T332: a foe's siphon drained these orbs (the party didn't spend them).</summary>
        public bool Drained;
        public int DrainedById = -1;
        public override string ToString() => $"Orbs{(Drained ? "Drained" : "Spent")} {Count} -> {CountAfter}";
    }

    public sealed class TimelineAdvancedEvent : BattleEvent
    {
        public int Ticks;
        /// <summary>Snapshot of (entry id → u) after the advance segment, for smooth playback.</summary>
        public Dictionary<int, float> U = new Dictionary<int, float>();
        /// <summary>GG-T319: turn icons still off the bar after the segment: entry id → (recovery ticks left, of total).</summary>
        public Dictionary<int, (int Left, int Total)> Recover = new Dictionary<int, (int Left, int Total)>();
        public override string ToString() => $"TimelineAdvanced {Ticks}t";
    }

    public sealed class PushedBackEvent : BattleEvent
    {
        public int UnitId; public float FromU; public float ToU; public int StunTicks;
        /// <summary>GG-T319: ticks added to a recovering (off-bar) icon's recovery; 0 when it was on the bar.</summary>
        public int RecoverTicks;
        public override string ToString() => $"PushedBack {UnitId} {FromU:F2}->{ToU:F2} stun={StunTicks}";
    }

    public sealed class HastenedEvent : BattleEvent
    {
        public int EntryId; public int UnitId; public float FromU; public float ToU;
        public override string ToString() => $"Hastened {UnitId} {FromU:F2}->{ToU:F2}";
    }

    /// <summary>The party's net tempo changed (GG-T268): +N slows every enemy icon, −N speeds them.</summary>
    public sealed class TempoChangedEvent : BattleEvent
    {
        public int Net; public float Multiplier; public string Source;
        public override string ToString() => $"Tempo {Net:+0;-0;0} (enemy x{Multiplier:F2}) {Source}";
    }

    public sealed class TurnStartedEvent : BattleEvent
    {
        public int UnitId;
        public override string ToString() => $"TurnStarted {UnitId}";
    }

    public sealed class TurnSkippedEvent : BattleEvent
    {
        public int UnitId; public string Reason;
        public override string ToString() => $"TurnSkipped {UnitId} ({Reason})";
    }

    public sealed class PlayerControlEvent : BattleEvent
    {
        /// <summary>GG-T308 (§2.0): true when a new window begins (an enemy acted, a wave began, the window was spent) and
        /// every hero may act again; false when control returns mid-window to the heroes who haven't acted yet.</summary>
        public bool NewWindow = true;
        public override string ToString() => NewWindow ? "PlayerControl" : "PlayerControl (same window)";
    }

    public sealed class CastQueuedEvent : BattleEvent
    {
        public int EntryId; public int CasterId; public string AbilityId; public int Ticks; public bool IsCharge;
        public List<Coord> LineTiles;
        /// <summary>Who/where it will land (presentation: the target chip and the charging bar while it rides).</summary>
        public int TargetUnitId = -1; public Coord TargetTile;
        public override string ToString() => $"CastQueued {CasterId} {AbilityId} {Ticks}t{(IsCharge ? " charge" : "")}";
    }

    public sealed class CastStaggeredEvent : BattleEvent
    {
        public int EntryId; public int CasterId; public int DelayTicks; public bool Resisted;
        public override string ToString() => $"CastStaggered {CasterId} +{DelayTicks}t{(Resisted ? " resisted" : "")}";
    }

    public sealed class CastCancelledEvent : BattleEvent
    {
        public int EntryId; public int CasterId; public string AbilityId; public string Reason;
        public override string ToString() => $"CastCancelled {CasterId} {AbilityId} ({Reason})";
    }

    /// <summary>What earned a Clutch! (GG-T566, GG-T571; §13.4.1). Append only: replays store the name, saves never.</summary>
    public enum ClutchKind
    {
        /// <summary>A hero's cast forced through an interrupt (Luck roll, §13.4).</summary>
        CastThroughInterrupt,
        /// <summary>A critical hit landed while Blind (§13.1.1).</summary>
        BlindCrit,
        /// <summary>GG-T571: a hero at or below <see cref="BattleConfig.ClutchLastStandHpFraction"/> of max HP lands a killing blow.</summary>
        LastStand,
        /// <summary>GG-T571: a hero lands the blow that clears the wave or wins the battle (not on remains or an egg).</summary>
        Finisher,
        /// <summary>GG-T571: one move closes <see cref="BattleConfig.ClutchPincersInOneMove"/> or more pincers.</summary>
        DoublePincer,
        /// <summary>GG-T571: a hero's shove, knockback or pull carries a foe into a hole or region danger that kills it.</summary>
        HazardKill,
        /// <summary>GG-T571: a hero interrupts a foe's charge within <see cref="BattleConfig.ClutchLastTickSeconds"/> of it landing.</summary>
        LastTickInterrupt,
    }

    /// <summary>GG-T571: each <see cref="ClutchKind"/>'s stable id and its glossary entry (<c>clutch_&lt;id&gt;</c>).</summary>
    public static class ClutchKinds
    {
        public static string Id(ClutchKind k)
        {
            switch (k)
            {
                case ClutchKind.CastThroughInterrupt: return "cast_through";
                case ClutchKind.BlindCrit: return "blind_crit";
                case ClutchKind.LastStand: return "last_stand";
                case ClutchKind.Finisher: return "finisher";
                case ClutchKind.DoublePincer: return "double_pincer";
                case ClutchKind.HazardKill: return "hazard_kill";
                case ClutchKind.LastTickInterrupt: return "last_tick";
                default: return k.ToString().ToLowerInvariant();
            }
        }

        /// <summary>The glossary entry naming and defining the kind (Content/glossary.json; its words come from Prose).</summary>
        public static string GlossaryId(ClutchKind k) => "clutch_" + Id(k);
    }

    public sealed class ClutchEvent : BattleEvent
    {
        /// <summary>The cast entry a <see cref="ClutchKind.CastThroughInterrupt"/> forced through, or the foe's charge a
        /// <see cref="ClutchKind.LastTickInterrupt"/> cancelled; -1 otherwise.</summary>
        public int EntryId = -1;
        public int CasterId;
        public ClutchKind Kind;
        public override string ToString() => $"Clutch {CasterId} ({Kind})";
    }

    public sealed class AbilityResolvedEvent : BattleEvent
    {
        public int CasterId; public string AbilityId; public Coord Anchor; public List<int> Targets = new List<int>();
        public List<Coord> Tiles = new List<Coord>();
        public override string ToString() => $"AbilityResolved {CasterId} {AbilityId} @{Anchor} -> [{string.Join(",", Targets)}]";
    }

    public sealed class AbilityRefusedEvent : BattleEvent
    {
        public int CasterId; public string Reason;
        public override string ToString() => $"AbilityRefused {CasterId} ({Reason})";
    }

    public sealed class ItemUsedEvent : BattleEvent
    {
        public int UserId; public string ItemId; public int ChargesLeft;
        public override string ToString() => $"ItemUsed {UserId} {ItemId} left={ChargesLeft}";
    }

    public sealed class BuffAppliedEvent : BattleEvent
    {
        public int UnitId; public string BuffId; public int Duration;
        public override string ToString() => $"BuffApplied {UnitId} {BuffId} {Duration}";
    }

    public sealed class BuffTickedEvent : BattleEvent
    {
        public int UnitId; public string BuffId; public int Remaining;
        public override string ToString() => $"BuffTicked {UnitId} {BuffId} {Remaining}";
    }

    public sealed class BuffRemovedEvent : BattleEvent
    {
        public int UnitId; public string BuffId; public string Reason;
        public override string ToString() => $"BuffRemoved {UnitId} {BuffId} ({Reason})";
    }

    /// <summary>A Passive or Reactive ability fired (§4.1, GG-T296); its effects' own events follow. The view floats
    /// <see cref="Callout"/> over the unit with a sound (POL-D4).</summary>
    public sealed class TriggeredEvent : BattleEvent
    {
        public int UnitId; public string AbilityId; public string Callout; public AbilityKind Kind; public TriggerOn On;
        /// <summary>The first effect that did something (Counterstrike, Lifesteal…), for the cue.</summary>
        public TriggerEffectKind Effect;
        public override string ToString() => $"Triggered {UnitId} {AbilityId} ({On} → {Effect})";
    }

    /// <summary>GG-T873 (§8.1): a Curse Link bound two foes for <see cref="Turns"/> turns (each now carries Linked); the
    /// view draws a tether between them.</summary>
    public sealed class UnitsLinkedEvent : BattleEvent
    {
        public int CasterId = -1; public int AId; public int BId; public int Turns;
        public override string ToString() => $"UnitsLinked {AId} <-> {BId} for {Turns}";
    }

    /// <summary>GG-T873: a blow on a Linked unit echoes to its partner; the echo's own <see cref="DamagedEvent"/> (Note
    /// "Echo") follows. An echo never echoes back.</summary>
    public sealed class LinkEchoEvent : BattleEvent
    {
        public int FromId; public int ToId; public int Amount;
        public override string ToString() => $"LinkEcho {FromId} -> {ToId} {Amount}";
    }

    /// <summary>GG-T887 (§7): the Monk's Redirect Flow carried these debuffs (<see cref="BuffIds"/>, one per instance, in
    /// order) off ally <see cref="FromId"/> onto foe <see cref="ToId"/>; each move's BuffRemoved/BuffApplied follows. The
    /// view runs a chi stream from the ally to the foe.</summary>
    public sealed class DebuffsRedirectedEvent : BattleEvent
    {
        public int CasterId = -1; public int FromId; public int ToId; public List<string> BuffIds = new List<string>();
        public override string ToString() => $"DebuffsRedirected {FromId} -> {ToId} [{string.Join(",", BuffIds)}]";
    }

    public sealed class AnnounceEvent : BattleEvent
    {
        public string Text; public int UnitId = -1;
        public override string ToString() => $"Announce '{Text}'";
    }

    public sealed class TrapPlacedEvent : BattleEvent
    {
        public int ById; public Coord At; public string TrapId;
        public override string ToString() => $"TrapPlaced {TrapId} at {At}";
    }

    public sealed class TrapTriggeredEvent : BattleEvent
    {
        public int UnitId; public Coord At; public string TrapId;
        public override string ToString() => $"TrapTriggered {TrapId} on {UnitId} at {At}";
    }

    public sealed class WeaponWornEvent : BattleEvent
    {
        public int UnitId; public string WeaponId; public int Durability;
        public override string ToString() => $"WeaponWorn {UnitId} {WeaponId} {Durability}";
    }

    public sealed class ArmorBrokenEvent : BattleEvent
    {
        public int UnitId; public string ArmorId;
        public override string ToString() => $"ArmorBroken {UnitId} {ArmorId}";
    }

    public sealed class WeaponSwappedEvent : BattleEvent
    {
        public int UnitId; public string FromWeapon; public string ToWeapon;
        public override string ToString() => $"WeaponSwapped {UnitId} {FromWeapon}->{ToWeapon}";
    }

    public sealed class WeaponShatteredEvent : BattleEvent
    {
        public int UnitId; public string WeaponId; public int SelfDamage;
        public override string ToString() => $"WeaponShattered {UnitId} {WeaponId}";
    }

    public sealed class BossPhaseEvent : BattleEvent
    {
        public int UnitId; public int Phase; public string Text;
        public override string ToString() => $"BossPhase {UnitId} -> {Phase}";
    }

    public sealed class WaveStartedEvent : BattleEvent
    {
        public int Wave; public int TotalWaves; public List<int> Spawned = new List<int>();
        /// <summary>Where each of <see cref="Spawned"/> arrived (parallel list, GG-T98) — it may walk on in the same command.</summary>
        public List<UnitFootprint> SpawnedAt = new List<UnitFootprint>();
        public override string ToString() => $"WaveStarted {Wave + 1}/{TotalWaves} [{string.Join(",", Spawned)}]";
    }

    public sealed class WaveClearedEvent : BattleEvent
    {
        public int Wave;
        /// <summary>Gold banked for clearing it (<see cref="GridGame.Core.Content.WaveDef.ClearCoins"/>, the Crucible's per-wave pay).</summary>
        public int Coins;
        public override string ToString() => $"WaveCleared {Wave + 1}" + (Coins > 0 ? $" +{Coins}G" : "");
    }

    public sealed class BattleEndedEvent : BattleEvent
    {
        public bool Victory;
        public bool Escaped;
        public override string ToString() => Victory ? "Victory" : Escaped ? "Escaped" : "Defeat";
    }

    public sealed class RevivedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public int Hp;
        public override string ToString() => $"Revived {UnitId} at {At} hp={Hp}";
    }

    public sealed class AmplifierSpawnedEvent : BattleEvent
    {
        public Coord At;
        public override string ToString() => $"Amplifier appears at {At}";
    }

    public sealed class AmplifierMovedEvent : BattleEvent
    {
        public Coord From, To; public int ByUnitId;
        public override string ToString() => $"Amplifier shoved {From}->{To} by {ByUnitId}";
    }

    public sealed class AmplifierUsedEvent : BattleEvent
    {
        public Coord At; public int HeroId;
        public override string ToString() => $"Amplifier used by {HeroId} at {At}";
    }

    public sealed class RemainsLeftEvent : BattleEvent
    {
        public int RemainsId; public int FromUnitId; public Coord At;
        public override string ToString() => $"Remains {RemainsId} left by {FromUnitId} at {At}";
    }

    public sealed class RoseAgainEvent : BattleEvent
    {
        public int RemainsId; public int UnitId; public Coord At;
        public override string ToString() => $"Unit {UnitId} rose from remains {RemainsId} at {At}";
    }

    public sealed class GuestJoinedEvent : BattleEvent
    {
        public int UnitId; public Coord At;
        public override string ToString() => $"Guest {UnitId} joins at {At}";
    }

    /// <summary>An egg is laid mid-battle (§14.8): solid, never acts except to hatch when its turn arrives.</summary>
    public sealed class EggLaidEvent : BattleEvent
    {
        public int EggId; public int FromUnitId; public Coord At;
        public override string ToString() => $"Egg {EggId} laid by {FromUnitId} at {At}";
    }

    public sealed class EggHatchedEvent : BattleEvent
    {
        public int EggId; public int UnitId; public Coord At;
        public override string ToString() => $"Unit {UnitId} hatched from egg {EggId} at {At}";
    }

    /// <summary>A delayed mid-wave spawn arrives (§14.8), optionally with a narrative blurb on the first one per wave.</summary>
    public sealed class ReinforcementsArrivedEvent : BattleEvent
    {
        public int Wave; public List<int> Spawned = new List<int>();
        /// <summary>Where each of <see cref="Spawned"/> arrived (parallel list, GG-T98) — it may walk on in the same command.</summary>
        public List<UnitFootprint> SpawnedAt = new List<UnitFootprint>();
        public string SpeakerClassId; public string Line;
        public override string ToString() => $"Reinforcements wave {Wave + 1} [{string.Join(",", Spawned)}]";
    }

    /// <summary>Mana charge gained (§3.1.9). <see cref="Total"/> is the meter afterwards; whole orbs it made follow as OrbMinted.</summary>
    public sealed class ManaChargedEvent : BattleEvent
    {
        public int SourceId; public int Amount; public int Total; public string Reason; public int Participants; public int Chain;
        public override string ToString() => $"ManaCharged +{Amount} ({Reason}) -> {Total}";
    }

    /// <summary>A physical blow from the armored side dented the plate instead of the body (§13.5).</summary>
    public sealed class PlateHitEvent : BattleEvent
    {
        public int UnitId; public int AttackerId; public int Amount; public int Left; public int Max;
        public override string ToString() => $"Plate hit {UnitId} -{Amount} ({Left}/{Max})";
    }

    public sealed class PlateBrokenEvent : BattleEvent
    {
        public int UnitId; public int AttackerId;
        public override string ToString() => $"Plate broken on {UnitId}";
    }

    /// <summary>An armored unit turned to face its target (its plate turns with it).</summary>
    public sealed class FacingChangedEvent : BattleEvent
    {
        public int UnitId; public Coord Facing;
        public override string ToString() => $"Unit {UnitId} faces {Facing}";
    }

    /// <summary>A fallen enemy left a pickup on the board (§13.3.1).</summary>
    public sealed class PickupDroppedEvent : BattleEvent
    {
        public int PickupEntryId; public string PickupId; public Coord At; public int FromUnitId;
        public override string ToString() => $"Pickup {PickupId} dropped at {At} by {FromUnitId}";
    }

    public sealed class PickupCollectedEvent : BattleEvent
    {
        public int PickupEntryId; public string PickupId; public Coord At; public int UnitId;
        public override string ToString() => $"Pickup {PickupId} collected by {UnitId} at {At}";
    }

    /// <summary>A non-hero unit entered a pickup's tile and shoved it aside like a movable tile (§13.3.1, GG-T228).</summary>
    public sealed class PickupMovedEvent : BattleEvent
    {
        public int PickupEntryId; public string PickupId; public Coord From, To; public int ByUnitId;
        public override string ToString() => $"Pickup {PickupId} shoved {From}->{To} by {ByUnitId}";
    }

    public sealed class PickupExpiredEvent : BattleEvent
    {
        public int PickupEntryId; public string PickupId; public Coord At;
        public override string ToString() => $"Pickup {PickupId} faded at {At}";
    }

    // ── Region rules (GG-T359, §14.20) ──────────────────────────────────────────────────────────────

    /// <summary>A region rule is in play this battle (its Prose name and description, for the opening banner).</summary>
    public sealed class RegionRuleEvent : BattleEvent
    {
        public string RuleId; public string Name; public string Description;
        public override string ToString() => $"RegionRule {RuleId}";
    }

    /// <summary>Painted tiles changed: Change is "marked", "cleared" or "changed" (a counter or a Becomes), or "hidden"
    /// (GG-T843: their VisibleSeconds ran out; only the painter's side sees them now, <see cref="RegionBoard.Sees"/>).</summary>
    public sealed class RegionTilesEvent : BattleEvent
    {
        public string RuleId; public string State; public string Change; public List<Coord> Tiles = new List<Coord>();
        public override string ToString() => $"RegionTiles {Change} {State} x{Tiles.Count}";
    }

    /// <summary>A hidden mark shows again, to everyone, for good (GG-T843, §14.23). Cause is "triggered" (one of its hooks
    /// ran: a foe stepped on the party's mine), "revealed" (a RevealTiles effect) or "element" (an element hit it).</summary>
    public sealed class TileRevealedEvent : BattleEvent
    {
        public Coord At; public string State; public string RuleId; public Team OwnerTeam; public int OwnerId = -1; public string Cause;
        public override string ToString() => $"TileRevealed {State} at {At} ({Cause})";
    }

    /// <summary>A named rule anchor was placed (From null) or moved.</summary>
    public sealed class RegionAnchorEvent : BattleEvent
    {
        public string RuleId; public string Anchor; public Coord? From; public Coord To;
        public override string ToString() => $"RegionAnchor {Anchor} {From}->{To}";
    }

    /// <summary>A board-wide rule flag went up or down.</summary>
    public sealed class RegionFlagEvent : BattleEvent
    {
        public string RuleId; public string Flag; public bool On;
        public override string ToString() => $"RegionFlag {Flag} {(On ? "on" : "off")}";
    }

    /// <summary>A rule event went on the timeline as its own icon, telegraphing <see cref="Tiles"/>.</summary>
    public sealed class HazardQueuedEvent : BattleEvent
    {
        public int EntryId; public string EventId; public string RuleId; public List<Coord> Tiles = new List<Coord>(); public float Seconds;
        /// <summary>GG-T874: the unit it rides (a Follow schedule); -1 = fixed tiles.</summary>
        public int CarrierId = -1;
        public override string ToString() => $"HazardQueued {EventId} #{EntryId} in {Seconds:0.0}s on {Tiles.Count} tiles{(CarrierId >= 0 ? " riding " + CarrierId : "")}";
    }

    /// <summary>GG-T874 (§7): a carried hazard's tiles moved with its carrier (<see cref="Tiles"/> after), or its carrier fell
    /// and it stays where it fell (<see cref="Dropped"/>).</summary>
    public sealed class HazardMovedEvent : BattleEvent
    {
        public int EntryId; public string EventId; public int CarrierId; public List<Coord> Tiles = new List<Coord>(); public bool Dropped;
        public override string ToString() => $"HazardMoved {EventId} #{EntryId} with {CarrierId}{(Dropped ? " (dropped)" : "")}";
    }

    /// <summary>A queued rule event's timer changed (TicksLeft after), or it was called off.</summary>
    public sealed class HazardRetimedEvent : BattleEvent
    {
        public int EntryId; public string EventId; public int TicksLeft; public bool Cancelled;
        public override string ToString() => $"HazardRetimed {EventId} #{EntryId} {(Cancelled ? "cancelled" : TicksLeft + " ticks")}";
    }

    /// <summary>
    /// A unit fell into a hole (GG-T368, §14.21): it was on <see cref="From"/> (the hole itself when it opened underfoot,
    /// the tile it was shoved or pulled from otherwise) and climbs out on <see cref="To"/>; <see cref="Damage"/> follows as
    /// its own DamagedEvent. On a full board it traded places with <see cref="SwappedId"/>, who now stands where the hole
    /// was, and the hole filled in (<see cref="Filled"/>).
    /// </summary>
    public sealed class FellEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord Hole; public Coord To; public string RuleId; public int Damage;
        public int SwappedId = -1; public bool Filled;
        public override string ToString() => $"Fell {UnitId} into {Hole} -> {To}{(SwappedId >= 0 ? $" swapping {SwappedId}" : "")}";
    }

    /// <summary>A unit of the state's <see cref="Content.TileStateDef.SquishedBy"/> team stepped on a mark and squished it
    /// (GG-T844, §14.23): the mark is gone (a "cleared" RegionTilesEvent follows), then the state's OnSquish runs.</summary>
    public sealed class SquishedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public string State; public string RuleId; public Team OwnerTeam; public int OwnerId = -1;
        public override string ToString() => $"Squished {State} at {At} by {UnitId}";
    }

    /// <summary>A rule event reached the left edge and resolves on its tiles (its effects follow as their own events).</summary>
    public sealed class HazardLandedEvent : BattleEvent
    {
        public int EntryId; public string EventId; public string RuleId; public List<Coord> Tiles = new List<Coord>(); public bool Rearmed;
        public override string ToString() => $"HazardLanded {EventId} #{EntryId}";
    }
}
