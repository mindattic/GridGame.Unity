using System.Collections.Generic;

namespace GridGame.Core.Content
{
    public enum BuffKind { Buff, Debuff }

    /// <summary>What happens when a buff is applied to a unit that already has it (bible §8.6).</summary>
    public enum BuffStacking { Refresh, Stack, Ignore, Upgrade }

    public enum DurationUnit { Turns, Ticks }

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
}
