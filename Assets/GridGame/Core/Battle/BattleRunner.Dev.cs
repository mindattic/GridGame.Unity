using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// GG-T1938: the pause menu's Developer cheats (<see cref="DevCheatCommand"/>), resolved through the same choke-points
    /// play uses: foes fall through <see cref="Kill"/> and <see cref="CheckEnd"/> (waves advance, reinforcements land, the
    /// last wave ends in a real Victory), heroes mend through <see cref="Heal"/>.
    /// </summary>
    internal sealed partial class BattleRunner
    {
        /// <summary>More passes than any stage has waves; an Undying foe that rises takes a second.</summary>
        private const int DevKillPasses = 64;

        private void DoDevCheat(DevCheatCommand cmd)
        {
            switch (cmd.Cheat)
            {
                case DevCheat.KillAllEnemies:
                    for (int pass = 0; pass < DevKillPasses && !S.IsOver; pass++)
                    {
                        var foes = new List<Unit>();
                        foreach (var u in S.Units)
                            if (u.Team == Team.Enemy && u.Hp > 0 && (u.OnBoard || u.Burrowed)) foes.Add(u);
                        foreach (var u in foes)
                        {
                            if (u.Hp <= 0) continue;
                            Emit(new DamagedEvent { TargetId = u.Id, SourceId = -1, Amount = u.Hp, Hit = HitKind.Normal, HpAfter = 0, Note = "Dev" });
                            Kill(u, null);
                        }
                        DeathBurstPending.Clear(); // a cheat's kills don't blast the party
                        CheckEnd();
                    }
                    break;
                case DevCheat.HealParty:
                    foreach (var h in S.Living(Team.Hero)) Heal(h, h.MaxHp - h.Hp, null);
                    break;
            }
        }
    }
}
