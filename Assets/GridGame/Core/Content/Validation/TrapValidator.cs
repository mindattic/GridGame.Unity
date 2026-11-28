using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The traps.json checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class TrapValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }
            foreach (var t in db.Traps.Values) NeedBuff(t.ApplyBuff, $"trap {t.Id}");
        }
    }
}
