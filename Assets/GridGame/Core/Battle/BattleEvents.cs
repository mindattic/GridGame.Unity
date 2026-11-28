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

    /// <summary>How the target's elemental multiplier met a blow's damage type (GG-T827, bible 9.1): above 1 is a
    /// weakness, below 1 a resistance, 0 or less no effect at all. Combat text tags the number with it.</summary>
    public enum HitEffectiveness { Normal, Weak, Resist, Immune }

    /// <summary>GG-T827: the reads a <see cref="DamagedEvent"/> carries beside its amount.</summary>
    public static class HitRead
    {
        /// <summary>The word for an elemental multiplier: above 1 Weak, 0 or less Immune, below 1 Resist, else Normal.</summary>
        public static HitEffectiveness Of(float multiplier) =>
            multiplier <= 0f ? HitEffectiveness.Immune : multiplier > 1.001f ? HitEffectiveness.Weak : multiplier < 0.999f ? HitEffectiveness.Resist : HitEffectiveness.Normal;

        /// <summary>The target's multiplier for <paramref name="type"/> (its class resistances and gear) as a word.</summary>
        public static HitEffectiveness Of(Unit target, DamageType type) => target == null ? HitEffectiveness.Normal : Of(target.ResistanceMultiplier(type));

        /// <summary>Damage past the HP left: <paramref name="amount"/> beyond <paramref name="hpBefore"/>, never below 0.</summary>
        public static int Overkill(int amount, int hpBefore) => Math.Max(0, amount - Math.Max(0, hpBefore));
    }

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

    /// <summary>GG-T862: Substitution — a blow that would have KO'd <see cref="UnitId"/> struck a log decoy at
    /// <see cref="From"/> instead (<see cref="Blocked"/> damage it never took); the unit vanished in a puff of smoke and
    /// reappeared at <see cref="To"/>, beside an ally. The log is tile state <see cref="State"/> (a RegionTilesEvent
    /// marks it next).</summary>
    public sealed class SubstitutedEvent : BattleEvent
    {
        public int UnitId; public int AttackerId = -1; public Coord From; public Coord To; public string State; public int Blocked;
        public override string ToString() => $"Substituted {UnitId} {From}->{To} ({Blocked} blocked by {State})";
    }

    /// <summary>GG-T897 (§14.18): a raging bull's Bull Rush carried <see cref="UnitId"/> from <see cref="From"/> down
    /// its marked line through <see cref="Path"/> (the tiles it will step into, in order; its SteppedEvents follow, each
    /// trampled hero's DisplacedEvent ahead of the step onto its tile). <see cref="Trampled"/> are the heroes it ran down.
    /// The view plays the steam-and-whistle run (GG-T898).</summary>
    public sealed class RampagedEvent : BattleEvent
    {
        public int UnitId; public string AbilityId; public Coord From;
        public List<Coord> Path = new List<Coord>(); public List<int> Trampled = new List<int>();
        public override string ToString() => $"Rampaged {UnitId} from {From} over {Path.Count} tile(s), trampling {Trampled.Count}";
    }

    public sealed class TeleportedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To;
        public override string ToString() => $"Teleported {UnitId} {From}->{To}";
    }

    /// <summary>GG-T868 (§7): the Lancer's Pole Vault carried <see cref="UnitId"/> from <see cref="From"/> over unit
    /// <see cref="OverId"/> (on <see cref="Over"/>, never entered) down onto <see cref="To"/>. The view arcs the jump over
    /// the planted spear and kicks up dust where it lands.</summary>
    public sealed class VaultedEvent : BattleEvent
    {
        public int UnitId; public int OverId = -1; public Coord From; public Coord Over; public Coord To;
        public override string ToString() => $"Vaulted {UnitId} {From}->{To} over {OverId}";
    }

    /// <summary>GG-T861: a Smoke Vanish: <see cref="UnitId"/> left <see cref="From"/> in its smoke and reappears on
    /// <see cref="To"/> (its <see cref="TeleportedEvent"/> follows). The view puffs smoke, calls "Vanish" and flashes the landing.</summary>
    public sealed class VanishedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To; public string AbilityId;
        public override string ToString() => $"Vanished {UnitId} {From}->{To}";
    }

    /// <summary>GG-T861: a foe's charge locked on a unit that vanished re-plans: onto <see cref="NewTargetId"/> at
    /// <see cref="TargetTile"/>, or (-1) onto the tile it was left aiming at.</summary>
    public sealed class ChargeReplannedEvent : BattleEvent
    {
        public int EntryId; public int CasterId; public int OldTargetId; public int NewTargetId = -1; public Coord TargetTile;
        public override string ToString() => $"ChargeReplanned #{EntryId} {OldTargetId}->{NewTargetId} at {TargetTile}";
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
        /// <summary>GG-T867: that end is the attacker's afterimage (at <see cref="Attacker1At"/>/<see cref="Attacker2At"/>), not the unit.</summary>
        public bool Afterimage1, Afterimage2;
        public List<int> Opponents = new List<int>();
        public List<int> Supporters = new List<int>();
        public int ChainIndex;
        /// <summary>§25.20 (GG-T1308): the two attackers' cohesion tier (<see cref="BattleSetup.Cohesion"/>); 0 = not bonded
        /// or not a hero pincer. Each tier adds <see cref="BattleConfig.PincerDamagePerCohesionTier"/> to both ends' blows.</summary>
        public int BondTier;
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
        /// <summary>§25.20 (GG-T1309): the supporter and attacker's cohesion tier; 0 = not bonded. At
        /// <see cref="BattleConfig.BondedSupportTier"/>+ <see cref="Amount"/> already carries the bonded bonus.</summary>
        public int BondTier;
        public List<int> Targets = new List<int>();
        public override string ToString() => $"Support {SupporterId}->{AttackerId} {Effect} '{Label}' [{string.Join(",", Targets)}]";
    }

    public sealed class DamagedEvent : BattleEvent
    {
        public int TargetId; public int SourceId; public int Amount; public HitKind Hit; public DamageType Type;
        public int HpAfter; public string Note;
        /// <summary>GG-T827: the damage past the HP the target had left (0 unless this blow killed it with some to spare).</summary>
        public int Overkill;
        /// <summary>GG-T827: the target's elemental multiplier for <see cref="Type"/> as a word (Normal on a miss).</summary>
        public HitEffectiveness Effectiveness;
        public override string ToString() => $"Damaged {TargetId} -{Amount} {Hit} {Type} hp={HpAfter}{(Overkill > 0 ? " overkill=" + Overkill : "")}{(Effectiveness != HitEffectiveness.Normal ? " " + Effectiveness : "")}{(Note != null ? " " + Note : "")}";
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
        /// <summary>GG-T1946: how much later the foe now acts, in ticks (the push as the rules applied it, after the cap).</summary>
        public int Ticks;
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

    /// <summary>GG-T794 (§2.13.2, UnmovedThisRound): every able hero has acted (or a new wave began), so a new round starts
    /// and each hero may act once more. Emitted at the party-turn boundary; never in other modes.</summary>
    public sealed class RoundStartedEvent : BattleEvent
    {
        public int Round;
        public override string ToString() => $"RoundStarted {Round}";
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
        /// <summary>GG-T1581: a hero at <see cref="BattleConfig.ResolveTier"/> Trust survives a lethal blow at
        /// <see cref="BattleConfig.ResolveHp"/> (once per battle).</summary>
        Resolve,
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
                case ClutchKind.Resolve: return "resolve";
                default: return k.ToString().ToLowerInvariant();
            }
        }

        /// <summary>The glossary entry naming and defining the kind (Content/glossary.json; its words come from Prose).</summary>
        public static string GlossaryId(ClutchKind k) => "clutch_" + Id(k);
    }

    /// <summary>GG-T1581: a hero's Trust Resolve left it at <see cref="HpAfter"/> instead of falling to a lethal blow.</summary>
    public sealed class ResolveEvent : BattleEvent
    {
        public int UnitId; public int SourceId = -1; public int HpAfter;
        public override string ToString() => $"Resolve {UnitId} hp={HpAfter}";
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

    /// <summary>GG-T878 (§7): a Magnetize field gripped <see cref="UnitIds"/> (nearest <see cref="Anchor"/> first); their
    /// pulls (<see cref="DisplacedEvent"/>s) and plate swings (<see cref="FacingChangedEvent"/>s) follow. The view arcs a
    /// magnetic field from the tile to each and hums.</summary>
    public sealed class MagnetizedEvent : BattleEvent
    {
        public int CasterId; public string AbilityId; public Coord Anchor;
        public List<int> UnitIds = new List<int>();
        public override string ToString() => $"Magnetized at {Anchor}: {string.Join(",", UnitIds)}";
    }

    /// <summary>GG-T887 (§7): the Monk's Redirect Flow carried these debuffs (<see cref="BuffIds"/>, one per instance, in
    /// order) off ally <see cref="FromId"/> onto foe <see cref="ToId"/>; each move's BuffRemoved/BuffApplied follows. The
    /// view runs a chi stream from the ally to the foe.</summary>
    public sealed class DebuffsRedirectedEvent : BattleEvent
    {
        public int CasterId = -1; public int FromId; public int ToId; public List<string> BuffIds = new List<string>();
        public override string ToString() => $"DebuffsRedirected {FromId} -> {ToId} [{string.Join(",", BuffIds)}]";
    }

    /// <summary>GG-T866 (§7): the Thief's Swipe Boon (<see cref="CasterId"/>) lifted boon <see cref="BuffId"/>, with
    /// <see cref="Remaining"/> left on it, off foe <see cref="FromId"/>; its BuffRemoved "stolen" and the Thief's BuffApplied
    /// follow. The view runs the snatch and a "Stolen" callout naming the boon.</summary>
    public sealed class BoonStolenEvent : BattleEvent
    {
        public int CasterId = -1; public int FromId; public string BuffId; public int Remaining;
        public override string ToString() => $"BoonStolen {FromId} -> {CasterId} {BuffId} {Remaining}";
    }

    /// <summary>GG-T867 (§7): the Speedster's Afterimage dash carried <see cref="UnitId"/> from <see cref="From"/> to
    /// <see cref="To"/> in a straight line and left an afterimage on <see cref="From"/>. The view streaks a ghost trail
    /// along the line, leaves a translucent copy on <see cref="From"/> and calls "Afterimage".</summary>
    public sealed class AfterimageLeftEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To;
        public override string ToString() => $"AfterimageLeft {UnitId} {From}->{To}";
    }

    /// <summary>GG-T867: <see cref="UnitId"/>'s afterimage on <see cref="At"/> fades: its owner's turn came round ("faded"), a
    /// foe's attack it drew struck it (<see cref="ById"/>, "struck"), its owner fell ("owner"), or a new dash replaced it.</summary>
    public sealed class AfterimageFadedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public int ById = -1; public string Reason;
        public override string ToString() => $"AfterimageFaded {UnitId}@{At} {Reason}";
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
        /// <summary>GG-T2070: a stage's scripted withdrawal ended it, with the party's <see cref="Line"/> to the Commander.
        /// Null rather than false otherwise, so the recorder leaves it out of every other battle's record.</summary>
        public bool? Withdrawn;
        public string Line;
        public override string ToString() => Victory ? "Victory" : Escaped ? "Escaped" : Withdrawn == true ? "Withdrawn" : "Defeat";
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

    /// <summary>Where a <see cref="ManaGainedEvent"/> came from (§3.1.0): an earned gain is damped, a whole grant is not, a
    /// foe's siphon takes points away.</summary>
    public enum ManaGainKind { Grant, Earned, Siphon }

    /// <summary>How one spell slot's affordability moved in a <see cref="ManaGainedEvent"/>: the pool capped at the slot's
    /// price, before and after.</summary>
    [System.Obsolete("Per-spell meters are gone (§3.1.0): read ManaGainedEvent.Before/After/Capacity.")]
    public sealed class ManaMeterDelta
    {
        public int UnitId; public int Slot; public int Delta; public int After; public int Full;
        public override string ToString() => $"{UnitId}#{Slot} {Delta:+0;-0} -> {After}/{Full}";
    }

    /// <summary>A mana gain (after damping) or siphon reached the party pool (§3.1.0). <see cref="Amount"/> is the points
    /// that landed (negative for a siphon), <see cref="Wasted"/> the points of a gain lost past capacity.</summary>
    public sealed class ManaGainedEvent : BattleEvent
    {
        public int SourceId = -1; public int Amount; public string Reason;
        public ManaGainKind Kind;
        /// <summary>The pool before and after, and its capacity.</summary>
        public int Before, After, Capacity;
        /// <summary>Points of a gain that didn't fit under <see cref="Capacity"/>.</summary>
        public int Wasted;
        /// <summary>A <see cref="ManaGainKind.Grant"/>'s whole orbs, or the orbs a <see cref="ManaGainKind.Siphon"/> took.
        /// A grant that found the pool full still reports them, with <see cref="Amount"/> 0 and all of it wasted.</summary>
        public int Orbs;
        /// <summary>An earned pincer gain's hero count and chain link (the Billboard shows the arithmetic).</summary>
        public int Participants; public int Chain;
#pragma warning disable CS0618
        [System.Obsolete("Per-spell meters are gone (§3.1.0): read Before/After/Capacity.")]
        public List<ManaMeterDelta> Meters = new List<ManaMeterDelta>();
        /// <summary>The spells this gain made affordable (each also gets a <see cref="SpellReadyEvent"/>).</summary>
        [System.Obsolete("Per-spell meters are gone (§3.1.0): use SpellReadyEvent.")]
        public List<ManaMeterDelta> Readied = new List<ManaMeterDelta>();
#pragma warning restore CS0618
        public override string ToString() => $"ManaGained {Amount:+0;-0} ({Reason}) {Before}->{After}/{Capacity}" + (Wasted > 0 ? $" wasted {Wasted}" : "");
    }

    /// <summary>A spell became affordable: the pool now holds at least its price (§3.1.0).</summary>
    public sealed class SpellReadyEvent : BattleEvent
    {
        public int UnitId; public int Slot; public string AbilityId;
        public override string ToString() => $"SpellReady {UnitId}#{Slot} {AbilityId}";
    }

    /// <summary>A cast paid its price from the party pool at cast start (§3.1.0).</summary>
    public sealed class SpellManaSpentEvent : BattleEvent
    {
        public int UnitId; public int Slot; public string AbilityId; public int Points;
        public override string ToString() => $"SpellManaSpent {UnitId}#{Slot} {AbilityId} -{Points}";
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

    /// <summary>GG-T2069 (§13.5): a <c>ShattersPlate</c> ability broke the plate outright, from any side (its
    /// <see cref="PlateBrokenEvent"/> follows). <see cref="Left"/> is what the plate had left.</summary>
    public sealed class PlateShatteredEvent : BattleEvent
    {
        public int UnitId; public int AttackerId; public string AbilityId; public int Left;
        public override string ToString() => $"Plate shattered on {UnitId} by {AbilityId} ({Left})";
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

    /// <summary>GG-T903 (§14.23): a tech gremlin's Sabotage turned another side's gadget (a buried charge, a barricade, a
    /// firewall) into its own side's: the mark at At now belongs to ToTeam and to the saboteur (UnitId).</summary>
    public sealed class TileSabotagedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public string State; public string RuleId; public Team FromTeam; public Team ToTeam;
        /// <summary>The mark was hidden from the saboteur's new foes when it turned (a buried charge: the party loses it).</summary>
        public bool Hidden;
        public override string ToString() => $"TileSabotaged {State} at {At} ({FromTeam} -> {ToTeam})";
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

    /// <summary>GG-T869 (§14.21): a Heavy unit's weight broke a cracked tile (<see cref="Content.TileStateDef.HeavyBecomes"/>)
    /// at <see cref="At"/>: <see cref="From"/> became <see cref="Into"/> (a hole) under it; its <see cref="FellEvent"/> follows.</summary>
    public sealed class BrokeThroughEvent : BattleEvent
    {
        public int UnitId; public Coord At; public string From; public string Into; public string RuleId;
        public override string ToString() => $"Broke through {From} at {At} under {UnitId}";
    }

    /// <summary>A unit of the state's <see cref="Content.TileStateDef.SquishedBy"/> team stepped on a mark and squished it
    /// (GG-T844, §14.23): the mark is gone (a "cleared" RegionTilesEvent follows), then the state's OnSquish runs.</summary>
    public sealed class SquishedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public string State; public string RuleId; public Team OwnerTeam; public int OwnerId = -1;
        public override string ToString() => $"Squished {State} at {At} by {UnitId}";
    }

    /// <summary>GG-T901 (§14.23): a unit went under the board at <see cref="At"/> (its burrow mark's tile), untargetable, and
    /// will surface on <see cref="Ambush"/> (its queued ambush, entry <see cref="EntryId"/>) beneath <see cref="TargetId"/>.</summary>
    public sealed class BurrowedEvent : BattleEvent
    {
        public int UnitId; public Coord At; public Coord Ambush; public int TargetId = -1; public int EntryId = -1;
        public override string ToString() => $"Burrowed {UnitId} at {At}, ambush on {Ambush}";
    }

    /// <summary>GG-T901: a burrowed unit came back up, from its burrow <see cref="From"/> to <see cref="To"/>: its ambush
    /// landing (<see cref="Forced"/> false; <see cref="VictimId"/> the unit it strikes, -1 none), or forced up early by a
    /// hero stepping on its burrow or the burrow being revealed (<see cref="Forced"/> true, no strike).</summary>
    public sealed class SurfacedEvent : BattleEvent
    {
        public int UnitId; public Coord From; public Coord To; public bool Forced; public int VictimId = -1;
        public override string ToString() => $"Surfaced {UnitId} {From}->{To}{(Forced ? " (forced)" : "")}";
    }

    /// <summary>A rule event reached the left edge and resolves on its tiles (its effects follow as their own events).</summary>
    public sealed class HazardLandedEvent : BattleEvent
    {
        public int EntryId; public string EventId; public string RuleId; public List<Coord> Tiles = new List<Coord>(); public bool Rearmed;
        public override string ToString() => $"HazardLanded {EventId} #{EntryId}";
    }
}
