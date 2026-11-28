using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The armor set checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class ArmorSetValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            foreach (var set in db.ArmorSetOrder) ValidateArmorSet(db, set, p);
        }

        /// <summary>Armor sets (GG-T182): five real armor pieces, one per armor slot, all the set's weight, no piece in
        /// two sets, and bonuses at distinct piece counts between 2 and 5.</summary>
        private static void ValidateArmorSet(ContentDb db, ArmorSetDef set, List<string> p)
        {
            string w = $"armor set {set.Id}";
            if (string.IsNullOrWhiteSpace(set.Name)) p.Add($"{w}: needs a Name");
            if (set.Pieces.Count != 5) p.Add($"{w}: needs exactly 5 pieces, has {set.Pieces.Count}");
            var slots = new HashSet<EquipmentSlot>();
            foreach (var id in set.Pieces)
            {
                var item = db.TryItem(id);
                if (item == null) { p.Add($"{w}: unknown item '{id}'"); continue; }
                if (item.Type != ItemType.Equipment || !IsArmorSlot(item.Slot)) { p.Add($"{w}: '{id}' isn't a Head/Torso/Legs/Gloves/Boots piece"); continue; }
                if (!slots.Add(item.Slot)) p.Add($"{w}: two pieces for {item.Slot}");
                if (item.ArmorWeight != set.Weight) p.Add($"{w}: '{id}' is {item.ArmorWeight}, set is {set.Weight}");
                if (db.TryArmorSetOf(id) != set) p.Add($"{w}: '{id}' already belongs to set '{db.TryArmorSetOf(id)?.Id}'");
            }
            var counts = new HashSet<int>();
            foreach (var b in set.Bonuses)
            {
                if (b == null) { p.Add($"{w}: null bonus"); continue; }
                if (b.Pieces < 2 || b.Pieces > 5) p.Add($"{w}: bonus at {b.Pieces} pieces (must be 2-5)");
                if (!counts.Add(b.Pieces)) p.Add($"{w}: two bonuses at {b.Pieces} pieces");
            }
            if (set.Bonuses.Count == 0) p.Add($"{w}: has no bonuses");
        }

        private static bool IsArmorSlot(EquipmentSlot s) =>
            s == EquipmentSlot.Head || s == EquipmentSlot.Torso || s == EquipmentSlot.Legs || s == EquipmentSlot.Gloves || s == EquipmentSlot.Boots;
    }
}
