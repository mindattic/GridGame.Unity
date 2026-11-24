using System;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>Afterimage (§1.2, §7, GG-T867, the Speedster): the dash, the afterimage's pincer end for the rest of the
    /// action, the foes it draws until the Speedster's next turn, and its fading. See <see cref="Afterimage"/>.</summary>
    internal sealed partial class BattleRunner
    {
        private void ResolveAfterimage(Unit caster, AbilityDef a, Coord anchor)
        {
            if (RootsHold(caster, anchor)) return; // GG-T912: rooted, it can't dash
            var from = caster.Pos;
            if (!Afterimage.CanDash(S, caster, anchor, a.Afterimage) || !FootprintOpen(caster, anchor)) { Announce("Blocked", caster.Id); return; }
            FadeAfterimage(caster, "replaced");
            if (!Relocate(caster, anchor)) { Announce("Blocked", caster.Id); return; }
            caster.AfterimageAt = from;
            var resolved = new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = anchor };
            resolved.Tiles.AddRange(Afterimage.Line(from, anchor));
            Emit(resolved);
            Emit(new AfterimageLeftEvent { UnitId = caster.Id, From = from, To = anchor });
            Emit(new TeleportedEvent { UnitId = caster.Id, From = from, To = anchor });
            ShovePickupsUnder(caster, from);
            TriggerTrap(caster);
            if (a.HastenU > 0f && caster.Alive) Hasten(caster, a.HastenU, a.Id, a.TempoTurns);
            if (!caster.Alive || !caster.IsHero || S.IsOver) { FadeAfterimage(caster, "owner"); return; }
            // For the rest of this action the afterimage is a pincer end of the Speedster's own.
            caster.AfterimagePincers = true;
            try { ResolvePincers(Team.Hero, caster); }
            finally { caster.AfterimagePincers = false; }
            if (!caster.Alive) FadeAfterimage(caster, "owner");
        }

        /// <summary>Clears <paramref name="u"/>'s afterimage, if it has one, with its fade event.</summary>
        private void FadeAfterimage(Unit u, string reason, int byId = -1)
        {
            if (u == null || !(u.AfterimageAt is Coord at)) return;
            u.AfterimageAt = null;
            u.AfterimagePincers = false;
            Emit(new AfterimageFadedEvent { UnitId = u.Id, At = at, ById = byId, Reason = reason });
        }

        /// <summary>The party's new turn: every afterimage has had its round and fades (a fallen owner's too).</summary>
        private void FadeAfterimages()
        {
            foreach (var u in S.Units)
                if (u.AfterimageAt.HasValue) FadeAfterimage(u, u.Alive ? "faded" : "owner");
        }

        /// <summary>
        /// Taunting (GG-T867): a foe that can reach a standing afterimage this turn (<see cref="Afterimage.Lure"/>) goes for
        /// it instead: a ranged foe shoots it from where it stands, anyone else walks up beside it and strikes. The struck
        /// afterimage fades and the blow is spent on nothing. A foe that walks but can't get there swings at whatever hero it
        /// ended beside. True when the turn was spent on the afterimage (or walking toward it).
        /// </summary>
        private bool TryStrikeAfterimage(Unit enemy)
        {
            var owner = Afterimage.Lure(S, enemy);
            if (owner == null || !(owner.AfterimageAt is Coord at)) return false;
            bool moved = false;
            if (!enemy.IsRanged)
            {
                int steps = Math.Max(0, enemy.Def.MoveRange);
                for (int i = 0; i < steps && enemy.Alive && !S.IsOver && enemy.Pos.Manhattan(at) > 1; i++)
                {
                    Coord? best = null;
                    int bestD = enemy.Pos.Manhattan(at);
                    foreach (var d in new[] { new Coord(1, 0), new Coord(-1, 0), new Coord(0, 1), new Coord(0, -1) })
                    {
                        var c = enemy.Pos + d;
                        if (c == at || !S.FreeToEnter(c)) continue;
                        int dist = c.Manhattan(at);
                        if (dist < bestD) { bestD = dist; best = c; }
                    }
                    if (best == null || !StepEnemy(enemy, best.Value) || enemy.Pos != best.Value) break;
                    moved = true;
                }
                if (!enemy.Alive || S.IsOver) return true;
            }
            bool reaches = owner.AfterimageAt == at && (enemy.IsRanged ? enemy.Pos.Manhattan(at) <= enemy.Def.AttackRange : enemy.Pos.Manhattan(at) == 1);
            if (!reaches)
            {
                if (!moved) return false;
                EnemyAttack(enemy);
                if (CheckEnd()) return true;
                EndEnemyTurn(enemy);
                return true;
            }
            Announce($"{enemy.Name} strikes an afterimage!", enemy.Id);
            FadeAfterimage(owner, "struck", enemy.Id);
            EndEnemyTurn(enemy);
            return true;
        }
    }
}
