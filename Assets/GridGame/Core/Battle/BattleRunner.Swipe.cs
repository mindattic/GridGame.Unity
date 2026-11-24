using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        /// <summary>Swipe Boon (§7, GG-T866): the boon <see cref="SwipeBoon.Pick"/> names leaves the picked adjacent foe
        /// (BuffRemovedEvent, Reason "stolen") and the Thief wears it with the time it had left (BuffAppliedEvent).
        /// <see cref="BoonStolenEvent"/> comes first, so the view can run the snatch before the status moves.</summary>
        private void ResolveSwipe(Unit caster, AbilityDef a, Unit foe)
        {
            if (!SwipeBoon.Can(S, caster, foe)) { Announce("Missed!", caster.Id); return; }
            var boon = SwipeBoon.Pick(foe);
            Emit(new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = foe.Pos, Targets = { foe.Id } });
            Emit(new BoonStolenEvent { CasterId = caster.Id, FromId = foe.Id, BuffId = boon.Def.Id, Remaining = boon.Remaining });
            RemoveBuffsWhere(foe, b => b == boon, "stolen");
            if (!S.IsOver && caster.Alive) ApplyBuff(caster, boon.Def, boon.Remaining, caster.Id);
        }
    }
}
