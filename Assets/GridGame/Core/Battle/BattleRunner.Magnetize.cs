using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        /// <summary>
        /// Magnetize (§7, GG-T878): the field grips every plated or Tech-tagged foe within the ability's radius of
        /// <paramref name="anchor"/> (<see cref="Magnetize.Caught"/>, nearest first) and drags each up to
        /// <see cref="AbilityDef.Magnetizes"/> tiles toward it, a free tile at a time (an open hole swallows it, a unit,
        /// movable or block stops it), then swings its plate to face the tile. A Heavy foe (unless Floating) resists with
        /// "Too heavy!", a Rooted one holds (StepBlocked "rooted"), a boss or snake won't budge: each still turns its plate.
        /// </summary>
        private void ResolveMagnetize(Unit caster, AbilityDef a, Coord anchor)
        {
            var caught = Magnetize.Caught(S, a, caster, anchor);
            var resolved = new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = anchor };
            resolved.Tiles.AddRange(TargetResolver.Tiles(anchor, a.Shape, a.Radius));
            foreach (var u in caught) resolved.Targets.Add(u.Id);
            Emit(resolved);
            var e =new MagnetizedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = anchor };
            foreach (var u in caught) e.UnitIds.Add(u.Id);
            Emit(e);
            if (caught.Count == 0) { Announce("Nothing metal", caster.Id); return; }
            foreach (var t in caught)
            {
                if (S.IsOver) return;
                if (!t.Alive || !t.OnBoard) continue;
                if (RootsHold(t, t.Pos)) { } // GG-T912: rooted, it holds (the plate still turns)
                else if (t.IsHeavy) Announce("Too heavy!", t.Id); // GG-T891: unless it floats
                else if (!t.WontBudge)
                    for (int i = 0; i < a.Magnetizes && t.Alive && !S.IsOver; i++)
                    {
                        var dir = Magnetize.StepToward(t.Pos, anchor);
                        if (dir == null) break;
                        var next = t.Pos + dir.Value;
                        if (TryShoveIntoPit(t, next, caster)) break; // GG-T368: dragged into an open hole
                        if (!S.FreeToEnter(next)) break;
                        var from = t.Pos;
                        if (!Relocate(t, next)) break;
                        Emit(new DisplacedEvent { UnitId = t.Id, From = from, To = next, ByUnitId = caster.Id });
                        NoteShove(caster, t); // GG-T571: a region danger that kills it now is a HazardKill
                        ShovePickupsUnder(t, from);
                        TriggerTrap(t);
                    }
                if (!t.Alive || !t.OnBoard || t.PlateMax <= 0) continue;
                var f = Magnetize.FaceToward(t, anchor);
                if (f == null || f.Value == t.Facing) continue;
                t.Facing = f.Value;
                Emit(new FacingChangedEvent { UnitId = t.Id, Facing = f.Value });
            }
        }
    }
}
