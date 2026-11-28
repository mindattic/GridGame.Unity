using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The weapon and armor enchant checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class EnchantValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            void NeedItem(string id, string where) { if (id != null && !db.Items.ContainsKey(id)) p.Add($"{where}: unknown item '{id}'"); }
            foreach (var e in db.Enchants.Values) NeedItem(e.Essence, $"enchant {e.Id}");
            // GG-T179: an armor enchant grants a real Passive ability and costs real materials.
            foreach (var e in db.ArmorEnchantOrder)
            {
                string w = $"armor enchant {e.Id}";
                if (string.IsNullOrWhiteSpace(e.Name)) p.Add($"{w}: needs a Name");
                if (!db.Abilities.TryGetValue(e.Passive ?? "", out var passive)) p.Add($"{w}: unknown passive '{e.Passive}'");
                else if (passive.Kind != AbilityKind.Passive && passive.Kind != AbilityKind.Reactive) p.Add($"{w}: '{e.Passive}' isn't a Passive or Reactive ability");
                if (e.Inputs.Count == 0) p.Add($"{w}: needs inputs");
                foreach (var ic in e.Inputs) { NeedItem(ic.Item, w); if (ic.Count <= 0) p.Add($"{w}: input counts must be positive"); }
                if (e.Gold < 0) p.Add($"{w}: gold can't be negative");
                if (e.Tier < 1 || e.Tier > 3) p.Add($"{w}: tier must be 1-3");
            }
        }
    }
}
