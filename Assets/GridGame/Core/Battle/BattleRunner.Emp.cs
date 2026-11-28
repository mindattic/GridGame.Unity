using System;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// The Technician's EMP Pulse (GG-T879, §7): a Square(1) spell that catches only <see cref="Tag"/>-tagged foes
    /// (<see cref="AbilityDef.OnlyVsTag"/>: gremlins, cyber zombies, mecha, prototypes). Each one caught is [Silenced] and
    /// its held cast slips <see cref="AbilityDef.CastStagger"/> seconds (no Wisdom shrug; past its own length it is
    /// cancelled). Its "emp" tile reactions (elements.json) set off the mines in the area (a mine or a buried mine turns
    /// armed and blows), fry a gremlin's shock mine, and the pulse shows every hidden mark it covers. Pure queries here
    /// for the bot and the view; the rules live in <see cref="BattleRunner"/>.
    /// </summary>
    public static class EmpPulse
    {
        /// <summary>The class tag the pulse reaches.</summary>
        public const string Tag = "Tech";

        /// <summary>Is <paramref name="a"/> an EMP-style pulse (a tag-only cast staggerer)? The view plays its ring for it.</summary>
        public static bool Is(AbilityDef a) => a != null && a.OnlyVsTag != null && a.CastStagger > 0f;

        /// <summary>
        /// What the hero bot thinks pulsing <paramref name="anchor"/> is worth: a tech foe holding a cast is the prize (more
        /// the closer it is to landing, more again when the slip cancels it), an unsilenced one that charges spells is worth
        /// silencing; each seen mine it sets off with no hero beside it, or trap it fries, counts; and a tile near a living
        /// sapper (a foe that lays traps) is a suspected mine, worth a little. Nothing at all reads 0, so it is never superfluous.
        /// </summary>
        public static float Value(BattleState s, Unit caster, AbilityDef a, Coord anchor)
        {
            if (s?.Content == null || caster == null || a == null) return 0f;
            float v = 0f;
            int stagger = a.CastStagger > 0f ? Math.Max(1, Formulas.SecondsToTicks(a.CastStagger)) : 0;
            foreach (var t in TargetResolver.Collect(s, a, caster, anchor))
            {
                if (t.Team == caster.Team || t.IsNeutral || !t.Alive) continue;
                var cast = s.CastEntryOf(t.Id);
                if (cast != null && stagger > 0)
                {
                    float near = cast.TotalCastTicks > 0 ? 1f - Math.Min(1f, (float)cast.RemainingTicks / cast.TotalCastTicks) : 1f;
                    v += 8f + 6f * near;
                    if (cast.AccumulatedDelayTicks + stagger >= cast.TotalCastTicks) v += 4f; // the slip cancels it outright
                }
                else if (!t.IsSilenced) v += t.Def?.ChargeAbility != null ? 3f : 0.5f;
            }
            var reactions = ElementCounters.ReactionsOf(s.Content, a);
            if (reactions.Count == 0) return v;
            foreach (var c in TileReactions.Area(a, anchor))
            {
                if (s.TrapAt(c) is Trap trap && TileReactions.Usable(s.Content, ElementCounters.Match(reactions, trap.TrapId))) { v += 3f; continue; }
                var m = s.Region.At(c);
                if (m != null && RegionBoard.Sees(m, caster.Team))
                {
                    var r = ElementCounters.Match(reactions, m.State);
                    if (!TileReactions.UsableOnMark(s.Content, r)) continue;
                    bool blows = r.Kind == TileReactionKind.Becomes && s.Content.TryTileState(r.Into) is TileStateDef next && next.OnMark.Count > 0;
                    v += blows ? 4f - 6f * HeroesNear(s, c) : 3f;
                    continue;
                }
                if (NearSapper(s, c)) v += 0.4f; // GG-T879: a suspected mine (the party can't see a hidden one)
            }
            return v;
        }

        /// <summary>Living heroes within one tile of <paramref name="c"/> (a set-off mine's blast reach).</summary>
        private static int HeroesNear(BattleState s, Coord c)
        {
            int n = 0;
            foreach (var h in s.LivingList(Team.Hero))
                if (Math.Abs(h.Pos.X - c.X) <= 1 && Math.Abs(h.Pos.Y - c.Y) <= 1) n++;
            return n;
        }

        /// <summary>Is a living foe that lays traps (a sapper) within two tiles of <paramref name="c"/>?</summary>
        private static bool NearSapper(BattleState s, Coord c)
        {
            foreach (var e in s.LivingList(Team.Enemy))
                if (e.Def?.Trap != null && Math.Abs(e.Pos.X - c.X) <= 2 && Math.Abs(e.Pos.Y - c.Y) <= 2) return true;
            return false;
        }
    }

    internal sealed partial class BattleRunner
    {
        /// <summary>
        /// GG-T879: the EMP Pulse jams <paramref name="t"/>'s held cast: it slips <paramref name="seconds"/> (no Wisdom shrug,
        /// no Clutch for a hero), and the slip counts toward the interrupt as a blow's stagger does, so a cast pushed past
        /// its own length is cancelled ("Interrupted!", the interrupt charge, a LastTickInterrupt Clutch when it was about to land).
        /// </summary>
        private void JamCast(Unit caster, Unit t, float seconds)
        {
            var cast = S.CastEntryOf(t.Id);
            if (cast == null || !(seconds > 0f)) return;
            int delay = Math.Max(1, Formulas.SecondsToTicks(seconds));
            int leftBefore = cast.RemainingTicks;
            cast.AccumulatedDelayTicks += delay;
            cast.RemainingTicks += delay;
            Emit(new CastStaggeredEvent { EntryId = cast.Id, CasterId = t.Id, DelayTicks = delay });
            if (cast.AccumulatedDelayTicks < cast.TotalCastTicks) { Announce("Jammed!", t.Id); return; }
            S.Timeline.Remove(cast);
            Emit(new CastCancelledEvent { EntryId = cast.Id, CasterId = t.Id, AbilityId = cast.AbilityId, Reason = "interrupted" });
            Announce("Interrupted!", t.Id);
            if (cast.CasterTeam != Team.Enemy) return;
            EarnMana(BattleConfig.InterruptCharge, t.Id, "interrupt");
            float window = BattleConfig.ClutchLastTickSeconds;
            if (window > 0f && caster.IsHero && leftBefore <= Formulas.SecondsToTicks(window))
                Clutch(caster, ClutchKind.LastTickInterrupt, cast.Id);
        }
    }
}
