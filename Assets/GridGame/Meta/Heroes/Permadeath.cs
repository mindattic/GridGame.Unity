using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>
    /// The one rule for whether a unit can ever be lost for good (GG-T114). Owner rule: "no one dies permanently,
    /// that's lame" — in the normal game nobody is ever lost; heroes, guests, recruits and dialogue-joined heroes fall,
    /// get revived or recover, and come back. A planned opt-in Ironman mode will switch this on; every "unit is lost"
    /// decision (the guest record in <see cref="SaveData.LostGuests"/>, the Summon Circle pool, the guest exclusion in
    /// <c>BattleFactory.Build</c>, the battle's fall announcement via <c>BattleSetup.Permadeath</c>) reads it here,
    /// so Ironman only has to change <see cref="Enabled"/>.
    /// </summary>
    public static class Permadeath
    {
        /// <summary>Always false today — no Ironman mode exists yet (BIBLE §29.1).</summary>
        public static bool Enabled(SaveData s) => false;

        /// <summary>Whether this class has been lost for good. Off → never, even for a save that recorded a loss
        /// under the old "lost forever" guest rule: those guests come back.</summary>
        public static bool IsLost(SaveData s, string classId) => Enabled(s) && s.LostGuests.Contains(classId);
    }
}
