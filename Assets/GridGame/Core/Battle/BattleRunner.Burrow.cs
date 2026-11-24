using System;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    // ── Burrow ambush (GG-T901, BIBLE §14.23) ─────────────────────────────────────────────────────────────────────
    // A foe with a Burrow ability (the scorpion's) goes under on its turn: it leaves the board (OnBoard false, so not
    // Alive: untargetable, on no tile, its timeline icon gone) under a hidden mark on its tile, and a telegraphed ambush
    // is queued on the tile of the foe nearest it. When the ambush lands it surfaces there (beside whoever stands on it)
    // and strikes them with the ability that sent it under. A hero stepping on the burrow mark, or the mark being
    // revealed (RevealTiles, an element's hit), forces it up early on its own tile, the ambush called off. A burrowed foe
    // still holds its wave open. No Rng: replays burrow and surface on the same ticks.

    internal sealed partial class BattleRunner
    {
        /// <summary>GG-T901: on its turn a foe goes under instead of stepping and striking (<see cref="EnemyPlanner.PlanBurrow"/>):
        /// the ability's cooldown starts and its rule effects (the Burrow) run. True = the turn is spent.</summary>
        private bool TryBurrow(Unit enemy)
        {
            var a = EnemyPlanner.PlanBurrow(S, enemy);
            if (a == null) return false;
            if (a.CooldownTurns > 0) enemy.Cooldowns[a.Id] = a.CooldownTurns;
            Announce($"{a.Name}!", enemy.Id);
            RegionAbility(enemy, a, enemy.Pos);
            return true;
        }

        /// <summary>RuleEffectKind.Burrow: the subject goes under its own tile (see the file header).</summary>
        private void Burrow(RuleEffectDef e, RuleCtx ctx)
        {
            var u = ctx.Subject;
            if (!RegionActive || u == null || !u.Alive || u.Burrowed || u.IsMultiTile || u.IsSnakeMember || u.IsRemains || u.IsEgg || u.IsCorpse) return;
            var target = BurrowTarget(u);
            if (target == null) return;
            var at = u.Pos;
            if (S.Region.At(at) != null) return; // one mark to a tile: never burrow under another rule's mark
            var under = new RuleCtx { Rule = ctx.Rule, Ability = ctx.Ability, Subject = u, At = at, Owner = u.Team, OwnerId = u.Id };
            MarkTiles(new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, Tiles = { at }, State = e.State, KeepMarks = true }, under);
            var mark = S.Region.At(at);
            if (mark == null || mark.OwnerId != u.Id || S.IsOver) return;

            int entryId = S.NextEntryId;
            ScheduleRuleEvent(new RuleEffectDef { Kind = RuleEffectKind.Schedule, Area = RuleArea.Tiles, Tiles = { target.Pos }, Event = e.Event, Seconds = e.Seconds }, under);
            if (S.NextEntryId == entryId) { S.Region.Tiles.Remove(at); return; } // no such event: it stays up
            // Off the board: its turn (and any cast) leaves the bar until it surfaces.
            S.Timeline.RemoveAll(x => x.Kind != TimelineKind.Hazard && x.OwnerId == u.Id);
            u.Burrowed = true;
            u.BurrowAbility = ctx.Ability;
            u.OnBoard = false;
            Emit(new BurrowedEvent { UnitId = u.Id, At = at, Ambush = target.Pos, TargetId = target.Id, EntryId = entryId });
            Announce($"{u.Name} burrows!", u.Id);
        }

        /// <summary>The foe a burrower surfaces under: the nearest living one (footprint Manhattan), ties by id.</summary>
        private Unit BurrowTarget(Unit u)
        {
            Unit best = null; int bestD = int.MaxValue;
            foreach (var o in S.Units)
            {
                if (!o.Alive || o.Team == u.Team || o.IsNeutral || o.IsCorpse || o.IsRemains || o.IsEgg) continue;
                int d = u.DistanceTo(o);
                if (d < bestD) { best = o; bestD = d; }
            }
            return best;
        }

        /// <summary>RuleEffectKind.Surface (the ambush event): its burrowed source comes up on the event's tile and strikes
        /// whoever stands there.</summary>
        private void SurfaceAmbush(RuleCtx ctx)
        {
            var u = ctx.Subject;
            if (!RegionActive || u == null || !u.Burrowed) return;
            var at = ctx.EventTiles != null && ctx.EventTiles.Count > 0 ? ctx.EventTiles[0] : u.Pos;
            var victim = S.UnitAt(at);
            if (victim != null && (victim.Team == u.Team || victim.IsNeutral || victim.IsCorpse || victim.IsRemains || victim.IsEgg)) victim = null;
            Surface(u, at, victim, forced: false);
        }

        /// <summary>RuleEffectKind.Unburrow: the burrowed owner of the mark is forced up.</summary>
        private void Unburrow(RuleCtx ctx)
        {
            int id = ctx.Mark?.OwnerId ?? ctx.OwnerId;
            if (RegionActive && id >= 0 && S.UnitById(id) is Unit u && u.Burrowed) ForceUp(u);
        }

        /// <summary>GG-T901: mark <paramref name="m"/> on <paramref name="c"/> was revealed or triggered: a burrower under it comes up.</summary>
        private void ForceUpFrom(Coord c, TileMark m)
        {
            if (!RegionActive || m == null || m.OwnerId < 0) return;
            if (S.UnitById(m.OwnerId) is Unit u && u.Burrowed && u.Pos == c) ForceUp(u);
        }

        /// <summary>A burrower is forced up early on its burrow tile (the nearest free one), its queued ambush called off.</summary>
        private void ForceUp(Unit u)
        {
            if (!u.Burrowed) return;
            foreach (var x in S.Timeline.ToArray())
            {
                if (x.Kind != TimelineKind.Hazard || x.SourceUnitId != u.Id) continue;
                S.Timeline.Remove(x);
                Emit(new HazardRetimedEvent { EntryId = x.Id, EventId = x.RuleEvent, Cancelled = true });
            }
            Surface(u, u.Pos, null, forced: true);
        }

        /// <summary>A queued ambush was called off by something else (an AdjustEvents Cancel): its burrower comes up, never stranded.</summary>
        private void AmbushCancelled(TimelineEntry x)
        {
            if (x.SourceUnitId >= 0 && S.UnitById(x.SourceUnitId) is Unit u && u.Burrowed) Surface(u, u.Pos, null, forced: true);
        }

        /// <summary>A burrower comes back up: its mark clears, it lands on <paramref name="at"/> or the nearest free tile,
        /// rejoins the timeline at its Speed's entry, reads its landing tile, then (an ambush) strikes <paramref name="victim"/>.</summary>
        private void Surface(Unit u, Coord at, Unit victim, bool forced)
        {
            var from = u.Pos;
            var mark = S.Region.At(from);
            if (mark != null && mark.OwnerId == u.Id)
            {
                S.Region.Tiles.Remove(from);
                var cleared = new RegionTilesEvent { RuleId = mark.Rule, State = mark.State, Change = "cleared" };
                cleared.Tiles.Add(from);
                Emit(cleared);
            }
            var land = SpawnLanding(at, 1, 1) ?? SpawnLanding(from, 1, 1) ?? from;
            u.Burrowed = false;
            u.OnBoard = true;
            u.Pos = land;
            var abilityId = u.BurrowAbility;
            u.BurrowAbility = null;
            if (victim != null && victim.Alive && victim.Pos != land)
            {
                int dx = victim.Pos.X - land.X, dy = victim.Pos.Y - land.Y;
                u.Facing = Math.Abs(dx) >= Math.Abs(dy) ? new Coord(Math.Sign(dx), 0) : new Coord(0, Math.Sign(dy));
            }
            Emit(new SurfacedEvent { UnitId = u.Id, From = from, To = land, Forced = forced, VictimId = victim?.Id ?? -1 });
            Emit(new TeleportedEvent { UnitId = u.Id, From = from, To = land }); // the sprite moves even where the view knows no burrow
            var entry = new TimelineEntry { Id = S.NextEntryId++, Kind = TimelineKind.Turn, OwnerId = u.Id };
            S.Timeline.Add(entry);
            ScheduleReentry(entry, u, BattleConfig.RecoverBaseSeconds, space: true);
            Announce(forced ? $"{u.Name} is forced up!" : victim != null ? $"{u.Name} bursts up under {victim.Name}!" : $"{u.Name} bursts up!", u.Id);
            RegionEnter(u);
            if (forced || victim == null || !victim.Alive || !u.Alive || S.IsOver) return;
            var a = C.TryAbility(abilityId);
            if (a != null) ResolveAbilityEffects(u, a, victim.Pos, victim);
        }
    }
}
