using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The buffs.json checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class BuffValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }
            foreach (var b in db.Buffs.Values)
            {
                NeedBuff(b.OnExpireApply, $"buff {b.Id}");
                foreach (var c in b.Cancels ?? new List<string>())
                {
                    NeedBuff(c, $"buff {b.Id} Cancels");
                    if (string.Equals(c, b.Id, StringComparison.OrdinalIgnoreCase)) p.Add($"buff {b.Id}: Cancels itself (GG-T667)");
                }
                if (b.Intercepts && b.Kind != BuffKind.Buff) p.Add($"buff {b.Id}: Intercepts must be a Buff (GG-T533)");
                if (b.Frightens && b.Kind != BuffKind.Debuff) p.Add($"buff {b.Id}: Frightens must be a Debuff (GG-T917)");
                if (b.Frightens && b.Immobile) p.Add($"buff {b.Id}: Frightens can't also be Immobile, a frightened unit runs (GG-T917)");
                if (!(b.EvasionBonus >= -Rules.Formulas.EvasionCap && b.EvasionBonus <= Rules.Formulas.EvasionCap))
                    p.Add($"buff {b.Id}: EvasionBonus {b.EvasionBonus} outside -{Rules.Formulas.EvasionCap}..{Rules.Formulas.EvasionCap} (GG-T297)");
            }
        }
    }
}
