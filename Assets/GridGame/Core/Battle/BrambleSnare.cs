using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Bramble Snare (§7, GG-T881, the Wild Child's spell): brambles burst up round one foe and hold it [Rooted] for 2
    /// turns. A roots debuff that lands on a foe charging a Line (a charge that carries the caster down its lane) snaps
    /// the charge off (<c>BattleRunner.ApplyBuff</c>). What the hero bot wants it for: a foe that would get away (a Kiter
    /// keeping its distance, a wounded or frightened foe fleeing, a Skirmisher about to dart off), a Line charger, or a
    /// hooker that drags heroes to it. Nothing on a foe that wouldn't move anyway (rooted already, Immobile, a Sentinel,
    /// no MoveRange) unless it is charging a Line. Deterministic: no Rng.
    /// </summary>
    public static class BrambleSnare
    {
        /// <summary>The Line charge <paramref name="u"/> is winding up, or null.</summary>
        public static TimelineEntry LineCharge(BattleState s, Unit u)
        {
            if (s == null || u == null) return null;
            foreach (var e in s.Timeline)
                if (e.Kind == TimelineKind.Cast && e.IsCharge && e.OwnerId == u.Id && e.ChargeShape == ChargeShape.Line) return e;
            return null;
        }

        /// <summary>A foe whose kit holds a pull (a hook): it drags heroes to it rather than walking.</summary>
        public static bool Hooks(BattleState s, Unit u)
        {
            if (s?.Content == null || u?.Def == null) return false;
            var charge = s.Content.TryAbility(u.Def.ChargeAbility);
            if (charge != null && charge.Pull > 0) return true;
            foreach (var id in u.Def.Abilities)
            {
                var a = s.Content.TryAbility(id);
                if (a != null && a.Pull > 0) return true;
            }
            return false;
        }

        /// <summary>A foe that would leave the fight's reach: wounded and retreating, frightened, a Skirmisher that darts
        /// off after it strikes, or a Kiter that keeps its distance.</summary>
        public static bool Flees(BattleState s, Unit u)
        {
            var brain = EnemyPlanner.EffectiveBrain(s, u);
            return EnemyPlanner.Retreats(s, u) || u.IsFrightened || brain == Brain.Skirmisher || brain == Brain.Kiter;
        }

        /// <summary>What rooting <paramref name="t"/> is worth to the party now; 0 when it buys nothing.</summary>
        public static float Value(BattleState s, Unit t)
        {
            if (s == null || t == null || !t.Alive || t.IsHero || t.IsNeutral || t.IsCorpse || t.IsRemains || t.IsEgg) return 0f;
            if (t.IsRooted || t.IsSnakeMember) return 0f;
            float v = 0f;
            var line = LineCharge(s, t);
            if (line != null)
            {
                // Snapping the charge off spares every hero on its lane.
                int caught = 0;
                if (line.LineTiles != null)
                    foreach (var tile in line.LineTiles) { var h = s.UnitAt(tile); if (h != null && h.IsHero && h.Alive) caught++; }
                v += 6f + 4f * caught;
            }
            bool walks = !t.IsImmobile && t.Def.MoveRange > 0 && EnemyPlanner.EffectiveBrain(s, t) != Brain.Sentinel;
            if (walks)
            {
                if (Flees(s, t)) v += EnemyPlanner.EffectiveBrain(s, t) == Brain.Kiter ? 5f : 6f;
                if (Hooks(s, t)) v += 4f;
            }
            // Held beside a hero, the pincer's first end is already in place.
            if (v > 0f)
                foreach (var h in s.Living(Team.Hero)) if (!h.IsRemains && h.IsAdjacentTo(t)) { v += 2f; break; }
            return v;
        }
    }
}
