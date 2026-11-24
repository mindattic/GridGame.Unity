using System;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Redirect Flow (§7, GG-T887, the Monk's skill): every debuff on the picked adjacent ally leaves it and lands, with
    /// the time it had left, on a foe adjacent to the Monk: a cleanse that also hurts. The receiver is the adjacent foe
    /// with the most HP (it lives to suffer the whole duration), ties to the lower id. A Curse Link half (Linked) stays
    /// put: it names its partner and means nothing on anyone else. Deterministic: no Rng.
    /// </summary>
    public static class RedirectFlow
    {
        /// <summary>A debuff the flow can carry off: any debuff but a Curse Link half.</summary>
        public static bool Movable(BuffInstance b) => b != null && b.Def != null && b.Def.Kind == BuffKind.Debuff && !b.Def.Links;

        /// <summary>A debuff that hurts or holds its bearer back: damage over time, a halt, a hold, a root, a silence,
        /// fear, a Slow or a blind. What the hero bot clears an ally of.</summary>
        public static bool Harmful(BuffDef d) =>
            d != null && d.Kind == BuffKind.Debuff && (d.DamagePerTick > 0 || d.HaltsTimeline || d.Immobile || d.Roots || d.BlocksSpells
                || d.Frightens || d.TimelineSpeedMultiplier < 1f || d.Tempo < 0 || d.AccuracyMultiplier < 1f);

        /// <summary>The foe a flow from <paramref name="caster"/> would pour into: the living adjacent foe with the most HP,
        /// ties to the lower id. Null when no foe stands beside the caster.</summary>
        public static Unit Receiver(BattleState s, Unit caster)
        {
            if (caster == null || !caster.Alive) return null;
            Unit best = null;
            foreach (var o in s.Units)
            {
                if (!o.Alive || o.IsNeutral || o.Team == caster.Team || o.IsCorpse || o.IsRemains || o.IsEgg) continue;
                if (!caster.IsAdjacentTo(o)) continue;
                if (best == null || o.Hp > best.Hp || (o.Hp == best.Hp && o.Id < best.Id)) best = o;
            }
            return best;
        }

        /// <summary>Can <paramref name="caster"/> redirect from <paramref name="ally"/> right now: another living teammate
        /// beside it carrying a movable debuff, with a foe beside the caster to take it?</summary>
        public static bool Can(BattleState s, Unit caster, Unit ally)
        {
            if (caster == null || ally == null || ally == caster || !ally.Alive || ally.Team != caster.Team) return false;
            if (!caster.IsAdjacentTo(ally) || !ally.Buffs.Exists(Movable)) return false;
            return Receiver(s, caster) != null;
        }

        /// <summary>What the hero bot makes of redirecting <paramref name="ally"/>'s debuffs: nothing unless one of them
        /// is <see cref="Harmful"/> and a foe is in reach; otherwise each movable debuff is worth its cleanse and its sting
        /// on the foe, more for a halt or hold and for poison or fire with time left.</summary>
        public static float Value(BattleState s, Unit caster, Unit ally)
        {
            if (!Can(s, caster, ally) || !ally.Buffs.Exists(b => Movable(b) && Harmful(b.Def))) return 0f;
            float v = 0f;
            foreach (var b in ally.Buffs)
            {
                if (!Movable(b)) continue;
                var d = b.Def;
                float w = 3f;
                if (d.HaltsTimeline || d.Immobile || d.Roots || d.Frightens) w += 6f;
                else if (d.BlocksSpells || d.TimelineSpeedMultiplier < 1f || d.Tempo < 0 || d.AccuracyMultiplier < 1f) w += 3f;
                if (d.DamagePerTick > 0) w += d.DamagePerTick * Math.Min(6, Math.Max(1, b.Remaining)) * 0.5f;
                v += 1.5f * w; // the ally is freed of it and the foe now carries it
            }
            return v;
        }
    }
}
