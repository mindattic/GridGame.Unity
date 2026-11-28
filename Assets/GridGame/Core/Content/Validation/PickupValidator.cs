using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The pickups.json checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class PickupValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }
            foreach (var pk in db.Pickups.Values)
            {
                NeedBuff(pk.ApplyBuff, $"pickup {pk.Id}");
                if (pk.Weight <= 0) p.Add($"pickup {pk.Id}: weight must be positive");
            }
        }
    }
}
