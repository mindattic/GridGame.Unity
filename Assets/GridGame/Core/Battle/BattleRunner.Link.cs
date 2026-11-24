using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Curse Link (§7, §8.1, GG-T873): a Links buff (Linked) lands on the picked foe and on its <see cref="CurseLink.Partner"/>,
    /// each instance naming the other. <see cref="Damage"/> echoes a share of every blow on one to the other
    /// (<see cref="EchoLink"/>); the echo never echoes back. Linking either foe again first breaks its old link, and a
    /// fallen foe's partner loses Linked at once.
    /// </summary>
    internal sealed partial class BattleRunner
    {
        /// <summary>Set while an echo is being dealt, so the echo itself never echoes back.</summary>
        private bool linkEchoing;

        /// <summary>Binds <paramref name="picked"/> to its partner with <paramref name="def"/>; fizzles with nobody to bind
        /// it to, or when either side shrugs the curse off (immune, warded).</summary>
        private void LinkFoes(Unit caster, Unit picked, BuffDef def)
        {
            var partner = CurseLink.Partner(S, picked);
            if (partner == null) { Announce("No link!", picked.Id); return; }
            BreakLinks(picked, "relinked");
            BreakLinks(partner, "relinked");
            ApplyBuff(picked, def, def.Duration, caster.Id);
            ApplyBuff(partner, def, def.Duration, caster.Id);
            var a = picked.Buffs.FindLast(b => b.Def == def);
            var z = partner.Buffs.FindLast(b => b.Def == def);
            if (a == null || z == null || !picked.Alive || !partner.Alive)
            {
                // Half a link is no link: the side it did reach drops it again.
                if (a != null) RemoveBuffsWhere(picked, b => b == a, "unlinked");
                if (z != null) RemoveBuffsWhere(partner, b => b == z, "unlinked");
                return;
            }
            a.LinkedTo = partner.Id;
            z.LinkedTo = picked.Id;
            Emit(new UnitsLinkedEvent { CasterId = caster.Id, AId = picked.Id, BId = partner.Id, Turns = def.Duration });
        }

        /// <summary>Removes <paramref name="u"/>'s link and its partner's matching half.</summary>
        private void BreakLinks(Unit u, string reason)
        {
            foreach (var b in u.Buffs.ToArray())
            {
                if (!b.Def.Links) continue;
                var p = b.LinkedTo >= 0 ? S.UnitById(b.LinkedTo) : null;
                if (p != null && p.Alive) RemoveBuffsWhere(p, x => x.Def.Links && x.LinkedTo == u.Id, reason);
            }
            if (u.Alive) RemoveBuffsWhere(u, b => b.Def.Links, reason);
        }

        /// <summary>A fallen unit's partners lose their half of the link (the fallen one's own buffs are already gone).</summary>
        private void BreakLinksTo(Unit fallen)
        {
            foreach (var o in S.Units)
                if (o != fallen && o.Alive && o.Buffs.Exists(b => b.Def.Links && b.LinkedTo == fallen.Id))
                    RemoveBuffsWhere(o, b => b.Def.Links && b.LinkedTo == fallen.Id, "link broken");
        }

        /// <summary>The partner a blow of <paramref name="amount"/> on <paramref name="target"/> echoes to, or null.</summary>
        private Unit LinkEchoTarget(Unit target, int amount, HitKind hit) =>
            linkEchoing || amount <= 0 || hit == HitKind.Miss ? null : CurseLink.PartnerOf(S, target);

        /// <summary>Deals the echo: <see cref="CurseLink.Echo"/> of the blow to the partner, from the same source and element.</summary>
        private void EchoLink(Unit from, Unit to, int amount, Unit source, DamageType type)
        {
            if (to == null || !to.Alive || S.IsOver) return;
            int echo = CurseLink.Echo(amount);
            if (echo <= 0) return;
            Emit(new LinkEchoEvent { FromId = from.Id, ToId = to.Id, Amount = echo });
            linkEchoing = true;
            try { Damage(to, echo, source, HitKind.Normal, type, "Echo"); }
            finally { linkEchoing = false; }
        }
    }
}
