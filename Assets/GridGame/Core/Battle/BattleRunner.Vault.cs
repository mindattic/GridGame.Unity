using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        /// <summary>Pole Vault (§7, GG-T868): the caster leaps over the unit beside it (<see cref="PoleVault.Over"/>, ally or
        /// foe, Heavy included, and left where it stands) onto the free tile beyond. The tile between is never entered, so
        /// whatever lies under the vaulted unit (a firewall, a mine, slime) is crossed; the landing tile's own traps and
        /// region hooks spring as on any arrival, and a hero who lands checks for pincers. A Rooted caster stays put.</summary>
        private void ResolveVault(Unit caster, AbilityDef a, Coord anchor)
        {
            var from = caster.Pos;
            if (RootsHold(caster, from)) return; // GG-T912: a rooted Lancer can't vault
            var over = PoleVault.Over(S, caster, anchor);
            if (over == null || !Relocate(caster, anchor)) { Announce("Blocked", caster.Id); return; }
            var mid = new Coord((from.X + anchor.X) / 2, (from.Y + anchor.Y) / 2);
            Emit(new AbilityResolvedEvent { CasterId = caster.Id, AbilityId = a.Id, Anchor = anchor, Tiles = { anchor } });
            Emit(new VaultedEvent { UnitId = caster.Id, OverId = over.Id, From = from, Over = mid, To = anchor });
            ShovePickupsUnder(caster, from);
            TriggerTrap(caster);
            if (a.HastenU > 0f && caster.Alive) Hasten(caster, a.HastenU, a.Id, a.TempoTurns);
            if (caster.Alive && caster.IsHero) ResolvePincers(Team.Hero, caster);
        }
    }
}
