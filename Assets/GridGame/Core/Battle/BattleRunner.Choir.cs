using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// The Choir's rune circle (GG-T902, BIBLE §14.17): cultists paint rune marks (a tile state with a
    /// <see cref="TileStateDef.Circle"/>), walk onto them (<see cref="EnemyPlanner.PlanRune"/>) and hold them; when the
    /// circle's Size of their side stand on runes at once it completes: it summons a reinforcement while their side is
    /// thin, or empowers the singers, and every rune clears. A hero stepping on a rune scuffs it (SquishedBy Hero), so
    /// where the party stands breaks the ritual.
    /// </summary>
    internal sealed partial class BattleRunner
    {
        /// <summary>GG-T902: the foe's ritual turn (<see cref="EnemyPlanner.PlanRune"/>): paint a rune near itself, or walk
        /// toward its rune (re-planning each step, up to MoveRange), or hold its rune; a walker or holder still strikes an
        /// adjacent hero. True = the turn is done.</summary>
        private bool TryRune(Unit enemy)
        {
            var plan = EnemyPlanner.PlanRune(S, enemy);
            if (plan == null) return false;
            if (plan.Kind == RunePlanKind.Paint)
            {
                var a = plan.Ability;
                if (a.CooldownTurns > 0) enemy.Cooldowns[a.Id] = a.CooldownTurns;
                Announce($"{enemy.Name} paints a rune!", enemy.Id);
                ResolveAbility(enemy, a, plan.At, null);
                if (CheckEnd()) return true;
                EndEnemyTurn(enemy);
                return true;
            }
            // A holder checks the circle too: a singer set down on a rune some way that never entered it still closes it.
            if (plan.Kind == RunePlanKind.Hold) RuneCircle(enemy);
            if (!enemy.Alive || S.IsOver) return true;
            if (plan.Kind == RunePlanKind.Walk)
            {
                int steps = Math.Max(1, enemy.Def.MoveRange);
                for (int i = 0; i < steps && plan != null && plan.Kind == RunePlanKind.Walk && enemy.Alive && !S.IsOver; i++)
                {
                    if (!StepEnemy(enemy, plan.At)) break;
                    plan = EnemyPlanner.PlanRune(S, enemy);
                }
                if (!enemy.Alive || S.IsOver) return true;
            }
            EnemyAttack(enemy);
            if (CheckEnd()) return true;
            EndEnemyTurn(enemy);
            return true;
        }

        /// <summary>
        /// GG-T902: <paramref name="u"/> entered a tile. On a rune of a circle state (its own side's, or the board's), with
        /// the circle's Size of its side now standing on such runes, the circle completes: a <see cref="RuneCircleEvent"/>,
        /// then OnSummon while fewer than SummonBelow of its side live (else OnEmpower) with <paramref name="u"/> as subject
        /// and owner, then every rune of the state its side may use clears.
        /// </summary>
        private void RuneCircle(Unit u)
        {
            if (!RegionActive || u.IsCorpse || u.IsRemains || u.IsEgg || !u.OnBoard) return;
            var m = S.Region.At(u.Pos);
            if (m == null) return;
            var def = C.TryTileState(m.State);
            var circle = def?.Circle;
            if (circle == null || !EnemyPlanner.IsRuneOf(m, def.Id, u.Team)) return;
            var singers = EnemyPlanner.RuneSingers(S, u.Team, def.Id);
            if (singers.Count < Math.Max(2, circle.Size)) return;
            int side = 0;
            foreach (var a in S.Units) if (a.Alive && a.OnBoard && a.Team == u.Team && !a.IsCorpse && !a.IsRemains && !a.IsEgg) side++;
            bool summon = circle.SummonBelow > 0 && side < circle.SummonBelow;
            var runes = new List<Coord>();
            foreach (var kv in S.Region.Tiles) if (EnemyPlanner.IsRuneOf(kv.Value, def.Id, u.Team)) runes.Add(kv.Key);
            runes.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            var ev = new RuneCircleEvent { UnitId = u.Id, State = def.Id, RuleId = m.Rule, Summoned = summon, Runes = new List<Coord>(runes) };
            foreach (var sgr in singers) ev.Singers.Add(sgr.Id);
            Emit(ev);
            Announce(summon ? "The circle closes: something answers the hymn!" : "The circle closes: the Choir swells!", u.Id);
            RunRuleEffects(summon ? circle.OnSummon : circle.OnEmpower, new RuleCtx
            {
                Rule = m.Rule, State = def.Id, Subject = u, At = u.Pos, Owner = u.Team, OwnerId = u.Id,
            });
            var cleared = new RegionTilesEvent { RuleId = m.Rule, State = def.Id, Change = "cleared" };
            foreach (var c in runes)
                if (EnemyPlanner.IsRuneOf(S.Region.At(c), def.Id, u.Team)) { S.Region.Tiles.Remove(c); cleared.Tiles.Add(c); }
            if (cleared.Tiles.Count > 0) Emit(cleared);
            CheckEnd();
        }
    }

    /// <summary>GG-T902 (BIBLE §14.17): a rune circle completed: <see cref="Singers"/> stood on its <see cref="Runes"/> at
    /// once (<see cref="UnitId"/> stepped in last). It summoned (<see cref="Summoned"/>) or empowered; its effects follow
    /// as their own events, then the runes clear (a "cleared" RegionTilesEvent).</summary>
    public sealed class RuneCircleEvent : BattleEvent
    {
        public int UnitId; public string State; public string RuleId; public bool Summoned;
        public List<int> Singers = new List<int>();
        public List<Coord> Runes = new List<Coord>();
        public override string ToString() => $"RuneCircle {State} by {UnitId} ({(Summoned ? "summon" : "empower")})";
    }
}