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
    // Party & Summon Circle (§23.4, §25.5, §25.10)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Party
    {
        public static Result Toggle(SaveData s, string classId)
        {
            if (s.Hero(classId) == null) return Result.Fail("Not in roster");
            if (s.Party.Contains(classId))
            {
                if (s.Party.Count <= 1) return Result.Fail("Someone has to go");
                s.Party.Remove(classId);
                return Result.Success("Moved to reserve");
            }
            if (s.Party.Count >= Progression.MaxPartySize) return Result.Fail($"Party is full ({Progression.MaxPartySize})");
            s.Party.Add(classId);
            return Result.Success("Joined the party");
        }
    }
}
