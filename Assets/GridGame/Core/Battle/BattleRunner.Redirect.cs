using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        /// <summary>Redirect Flow (§7, GG-T887): every movable debuff (<see cref="RedirectFlow.Movable"/>) leaves the picked
        /// ally (BuffRemovedEvent, Reason "redirected") and lands with the time it had left on the foe beside the caster
        /// (<see cref="RedirectFlow.Receiver"/>), applied by the caster, so the foe's own immunities and wards still hold.
        /// <see cref="DebuffsRedirectedEvent"/> comes first, so the view can run the chi stream before the statuses move.</summary>
        private void ResolveRedirect(Unit caster, AbilityDef a, Unit ally)
        {
            if (!RedirectFlow.Can(S, caster, ally)) { Announce("Missed!", caster.Id); return; }
            var foe = RedirectFlow.Receiver(S, caster);
            var moved = ally.Buffs.FindAll(RedirectFlow.Movable);
            Emit(new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = ally.Pos, Targets = { ally.Id, foe.Id } });
            var ev = new DebuffsRedirectedEvent { CasterId = caster.Id, FromId = ally.Id, ToId = foe.Id };
            foreach (var b in moved) ev.BuffIds.Add(b.Def.Id);
            Emit(ev);
            RemoveBuffsWhere(ally, b => moved.Contains(b), "redirected");
            foreach (var b in moved)
            {
                if (S.IsOver || !foe.Alive) break;
                ApplyBuff(foe, b.Def, b.Remaining, caster.Id);
            }
        }
    }
}
