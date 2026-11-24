using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Float (§7, GG-T872, the Drifter's spell): lays [Floating] (§8.1, <see cref="Unit.FloatingBuffId"/>) on the picked
    /// unit, ally or foe. On a Heavy foe it is the counter to Heavy: while it floats it can be slid, shoved, pulled and
    /// swapped (<see cref="Unit.IsHeavy"/> reads false), so a hero's Knockback, Pull, Swap or Gravity Well can set it into
    /// a pincer. On a hero it hovers over mines, slime, holes and every other ground state (a Tall firewall still burns
    /// it). Unlike other boons on a foe-targeting spell it is never redirected onto the caster (the runner's buff
    /// landing asks <see cref="Is"/>). This class says what a cast is worth to the hero bot. Pure and deterministic: it
    /// adds the buff and moves <c>Pos</c> to look, restores both, and never draws from the Rng.
    /// </summary>
    public static class FloatSpell
    {
        /// <summary>What each foe the party's next pincer would trap after the floated foe is moved one tile is worth.</summary>
        public const float TrappedValue = 10f;
        /// <summary>A ground hazard (a mine, slime, a telegraphed quake) on a hero's way in that floating lets it cross.</summary>
        public const float HazardStepValue = 4f;
        /// <summary>An open hole on a hero's way in that floating lets it cross.</summary>
        public const float HoleStepValue = 6f;
        /// <summary>The most the ally side is worth, however many hazards lie ahead.</summary>
        public const float AllyCap = 14f;
        /// <summary>How far ahead (in steps) a hero's way in is read for hazards.</summary>
        public const int Lookahead = 2;

        /// <summary>Is <paramref name="a"/> a Float: it lays Floating on its target.</summary>
        public static bool Is(AbilityDef a) => a != null && string.Equals(a.ApplyBuff, Unit.FloatingBuffId, StringComparison.OrdinalIgnoreCase);

        /// <summary>A Heavy foe that only its Heavy keeps put: floating would let it be moved (not a 2x2 boss, a snake
        /// member, remains or a corpse; not already floating).</summary>
        public static bool AnchoredOnlyByHeavy(Unit u) =>
            u != null && u.Alive && !u.IsCorpse && !u.IsFloating && u.IsHeavy && !u.IsMultiTile && !u.IsSnakeMember && !u.IsRemains;

        /// <summary>Does some living hero hold a ready ability that moves a foe (Knockback, Pull, Swap, a Gravity Well)?</summary>
        public static bool MoverReady(BattleState s)
        {
            foreach (var h in s.LivingList(Team.Hero))
                for (int slot = 0; slot < h.Bar.Count; slot++)
                {
                    if (BattleEngine.GetSlotStatus(s, h.Id, slot) != SlotStatus.Ready) continue;
                    var sa = BattleEngine.SlotAbility(s, h, slot);
                    if (sa != null && (sa.Knockback > 0 || sa.Pull > 0 || sa.SwapsPlaces || GravityWell.Is(sa))) return true;
                }
            return false;
        }

        /// <summary>
        /// What floating <paramref name="foe"/> is worth to the hero bot: nothing unless it is held only by Heavy and some
        /// hero has a mover ready; then <see cref="TrappedValue"/> for each foe the best one-tile move of the floated foe
        /// (any side, holes included; a unit, movable or block stops it) adds to the party's next pincer catch
        /// (<see cref="GravityWell.NextPincerTrapped"/>). A Heavy foe that no single move sets into a pincer is not
        /// worth the orb.
        /// </summary>
        public static float FoeValue(BattleState s, Unit foe)
        {
            if (!AnchoredOnlyByHeavy(foe) || foe.Team != Team.Enemy) return 0f;
            var def = s.Content?.TryBuff(Unit.FloatingBuffId);
            if (def == null || !MoverReady(s)) return 0f;
            int before = GravityWell.NextPincerTrapped(s);
            int best = before;
            var origin = foe.Pos;
            var lift = new BuffInstance { Def = def, Remaining = def.Duration };
            foe.Buffs.Add(lift);
            try
            {
                foreach (var dir in Dirs)
                {
                    var next = origin + dir;
                    if (!s.WalkableFor(next, foe) || s.UnitAt(next) != null || s.MovableAt(next) != null) continue;
                    foe.Pos = next;
                    int after = GravityWell.NextPincerTrapped(s);
                    foe.Pos = origin;
                    if (after > best) best = after;
                }
            }
            finally { foe.Pos = origin; foe.Buffs.Remove(lift); }
            return TrappedValue * (best - before);
        }

        /// <summary>
        /// What floating hero <paramref name="hero"/> is worth to the hero bot: for each tile within <see cref="Lookahead"/>
        /// steps on its shortest way to its nearest foe that is a ground hazard or an open hole for it now and is not
        /// once it floats, <see cref="HazardStepValue"/> (a hole <see cref="HoleStepValue"/>), capped at
        /// <see cref="AllyCap"/>. A Tall state (a firewall) still burns a floater, so it counts for nothing. Nothing for a
        /// hero already floating, a 2x2 one, one already beside a foe, or when no foe is left.
        /// </summary>
        public static float AllyValue(BattleState s, Unit hero)
        {
            if (hero == null || !hero.Alive || hero.IsCorpse || hero.IsFloating || hero.IsMultiTile || hero.Team != Team.Hero) return 0f;
            var def = s.Content?.TryBuff(Unit.FloatingBuffId);
            if (def == null) return 0f;
            Unit goal = null;
            int dist = int.MaxValue;
            foreach (var e in s.LivingList(Team.Enemy))
            {
                if (e.IsCorpse || e.IsRemains || e.IsEgg) continue;
                int d = hero.Pos.Manhattan(e.Pos);
                if (d < dist) { dist = d; goal = e; }
            }
            if (goal == null || dist <= 1) return 0f;
            var risky = new List<(Coord c, bool hole)>();
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    int step = hero.Pos.Manhattan(c);
                    if (step == 0 || step > Lookahead || goal.Occupies(c)) continue;
                    if (step + c.Manhattan(goal.Pos) != dist) continue; // not on its way in
                    bool hole = RegionBoard.IsPitFor(s, c, hero);
                    if (hole || RegionBoard.DangerAt(s, c, hero) > 0f) risky.Add((c, hole));
                }
            if (risky.Count == 0) return 0f;
            float v = 0f;
            var lift = new BuffInstance { Def = def, Remaining = def.Duration };
            hero.Buffs.Add(lift);
            try
            {
                foreach (var (c, hole) in risky)
                {
                    if (hole) { if (s.WalkableFor(c, hero)) v += HoleStepValue; }
                    else if (RegionBoard.DangerAt(s, c, hero) <= 0f) v += HazardStepValue;
                }
            }
            finally { hero.Buffs.Remove(lift); }
            return Math.Min(AllyCap, v);
        }

        /// <summary>What casting Float on <paramref name="target"/> is worth to the hero bot (before its orb cost).</summary>
        public static float Value(BattleState s, Unit caster, Unit target)
        {
            if (target == null || caster == null) return 0f;
            return target.Team == caster.Team ? AllyValue(s, target) : FoeValue(s, target);
        }

        private static readonly Coord[] Dirs = { new Coord(1, 0), new Coord(-1, 0), new Coord(0, 1), new Coord(0, -1) };
    }
}
