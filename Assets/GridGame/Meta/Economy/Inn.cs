using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Inn / Heal party (§25.3)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>GG-T1790: the Inn's gold heal is gone; camp rest (<see cref="CampRestService"/>) heals for free.</summary>
    public static class Inn
    {
        public static int MissingHp(ContentDb c, SaveData s, HeroSave h)
        {
            if (h.Hp <= 0) return 0;
            var max = Progression.Preview(c, h).MaxHp;
            return Math.Max(0, max - h.Hp);
        }
    }
}
