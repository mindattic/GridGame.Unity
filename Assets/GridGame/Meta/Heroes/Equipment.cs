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
    // Equip (§24.2, §25.4)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Equipment
    {
        /// <summary>The five jewelry slots, in the order §24.2's equip screen lists them (GG-T89).</summary>
        public static readonly EquipmentSlot[] JewelrySlots =
        {
            EquipmentSlot.Necklace, EquipmentSlot.Earring1, EquipmentSlot.Earring2, EquipmentSlot.Ring1, EquipmentSlot.Ring2,
        };

        private static bool IsArmorSlot(EquipmentSlot slot) => Array.IndexOf(BattleEngine.ArmorSlots, slot) >= 0;
        private static bool IsJewelrySlot(EquipmentSlot slot) => Array.IndexOf(JewelrySlots, slot) >= 0;

        public static string Equipped(HeroSave h, EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.Weapon) return h.WeaponId;
            if (IsArmorSlot(slot)) return h.Armor.TryGetValue(slot, out var a) ? a.ItemId : null;
            if (IsJewelrySlot(slot)) return h.Jewelry.TryGetValue(slot, out var id) ? id : null;
            return null;
        }

        public static bool Fits(ItemDef item, EquipmentSlot slot)
        {
            if (item == null || item.Type != ItemType.Equipment) return false;
            if (slot == EquipmentSlot.Earring1 || slot == EquipmentSlot.Earring2) return item.IsEarring;
            if (slot == EquipmentSlot.Ring1 || slot == EquipmentSlot.Ring2) return item.IsRing;
            return item.Slot == slot;
        }

        /// <summary>GG-T52: shape fit (above) plus proficiency — refuses gear <paramref name="h"/> isn't
        /// trained to use. The shape-only overload still drives the Equip screen's bag listing (so a locked
        /// piece the hero owns stays visible with a lock badge instead of disappearing), and bot/shop code
        /// that should never pick gear it can't use calls this one instead.</summary>
        public static bool Fits(ContentDb c, HeroSave h, ItemDef item, EquipmentSlot slot) =>
            Fits(item, slot) && ProficiencyGate(c, h, item) == null;

        /// <summary>Null if <paramref name="h"/> can equip <paramref name="item"/> today; otherwise the name
        /// of the proficiency-unlock passive that would let them (GG-T52) — e.g. "Heavy Armor Proficiency".
        /// Only the Weapon and armor slots are ever gated; jewelry and consumables never are.</summary>
        public static string ProficiencyGate(ContentDb c, HeroSave h, ItemDef item)
        {
            if (item == null || item.Type != ItemType.Equipment) return null;
            if (item.Slot == EquipmentSlot.Weapon)
            {
                if (item.WeaponClass == WeaponClass.Simple) return null;
                if (Progression.EffectiveWeaponProficiencies(c, h).Contains(item.WeaponClass)) return null;
                var passiveId = item.WeaponClass == WeaponClass.Arcane ? "arcane_focus_proficiency" : "martial_weapons_proficiency";
                return c.TryAbility(passiveId)?.Name ?? passiveId;
            }
            if (IsArmorSlot(item.Slot))
            {
                if (item.ArmorWeight <= Progression.EffectiveArmorProficiency(c, h)) return null;
                return c.TryAbility("heavy_armor_proficiency")?.Name ?? "Heavy Armor Proficiency";
            }
            return null;
        }

        /// <summary>Everything in the bag that shape-fits this slot (locked gear included, GG-T52) — the Equip
        /// screen's bag listing. Pass <paramref name="h"/> to additionally drop anything the hero isn't
        /// proficient in (bot/shop shopping lists, which should never pick gear it can't wear).</summary>
        public static List<ItemDef> Candidates(ContentDb c, SaveData s, EquipmentSlot slot, HeroSave h = null) =>
            s.Inventory.Where(kv => kv.Value > 0).Select(kv => c.TryItem(kv.Key))
                .Where(i => Fits(i, slot) && (h == null || ProficiencyGate(c, h, i) == null))
                .OrderByDescending(i => i.Rarity).ThenBy(i => i.Name).ToList();

        /// <summary>True if the one owned (unequipped) copy of this item id is a Masterwork roll (§24.6.2, GG-T46).</summary>
        public static bool IsMasterwork(SaveData s, string itemId) =>
            itemId != null && s.Worn.TryGetValue(itemId, out var list) && list.Any(w => w.Mastery > 1f);

        public static Result Equip(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot, string itemId)
        {
            var item = c.TryItem(itemId);
            if (h != null && h.Domesticated) return Result.Fail("A tamed monster wears no gear"); // GG-T303
            if (!Fits(item, slot)) return Result.Fail("That doesn't go there");
            var gate = ProficiencyGate(c, h, item);
            if (gate != null) return Result.Fail($"Not proficient — needs {gate}");
            if (s.Count(itemId) < 1) return Result.Fail("You don't own that");
            Unequip(c, s, h, slot);
            if (IsJewelrySlot(slot))
            {
                s.AddItem(itemId, -1);
                h.Jewelry[slot] = itemId;
                return Result.Success($"Equipped {item.Name}");
            }
            // Fresh copies first; otherwise the best of the worn ones (per-instance durability, US-246).
            var copy = TakeCopy(s, item);
            s.AddItem(itemId, -1);
            if (slot == EquipmentSlot.Weapon) { h.WeaponId = itemId; h.WeaponDurability = copy.Durability; h.WeaponRepairs = copy.Repairs; h.WeaponMastery = copy.Mastery; }
            else h.Armor[slot] = new EquippedArmor { ItemId = itemId, Durability = copy.Durability, Repairs = copy.Repairs, Mastery = copy.Mastery, Enchant = copy.Enchant };
            return Result.Success(copy.Mastery > 1f ? $"Equipped ★ {item.Name}" : $"Equipped {item.Name}");
        }

        private static WornGear TakeCopy(SaveData s, ItemDef item)
        {
            s.Worn.TryGetValue(item.Id, out var worn);
            int fresh = s.Count(item.Id) - (worn?.Count ?? 0);
            // An enchanted copy (GG-T179) is put on before a fresh one, so taking a piece off and back on keeps its enchant.
            bool enchanted = worn != null && worn.Any(w => w.Enchant != null);
            if (!enchanted && (fresh > 0 || worn == null || worn.Count == 0)) return new WornGear { Durability = item.Durability, Repairs = 0 };
            worn.Sort((a, b) => { int e = (b.Enchant != null).CompareTo(a.Enchant != null); return e != 0 ? e : b.Durability.CompareTo(a.Durability); });
            var best = worn[0];
            worn.RemoveAt(0);
            if (worn.Count == 0) s.Worn.Remove(item.Id);
            return best;
        }

        /// <summary>Returns the piece to the bag, remembering its wear, repairs and Masterwork roll if it isn't factory-fresh.</summary>
        public static Result Unequip(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot)
        {
            var id = Equipped(h, slot);
            if (id == null) return Result.Fail("Nothing equipped");
            var item = c.TryItem(id);
            if (IsJewelrySlot(slot))
            {
                s.AddItem(id, 1);
                h.Jewelry.Remove(slot);
                return Result.Success("Unequipped");
            }
            int dur = 0, reps = 0; float mastery = 1f; string enchant = null;
            if (slot == EquipmentSlot.Weapon) { dur = h.WeaponDurability; reps = h.WeaponRepairs; mastery = h.WeaponMastery; }
            else if (h.Armor.TryGetValue(slot, out var a)) { dur = a.Durability; reps = a.Repairs; mastery = a.Mastery; enchant = a.Enchant; }
            s.AddItem(id, 1);
            if (item != null && item.Durability > 0 && (dur < item.Durability || reps > 0 || mastery > 1f || enchant != null))
            {
                if (!s.Worn.TryGetValue(id, out var list)) s.Worn[id] = list = new List<WornGear>();
                list.Add(new WornGear { Durability = dur, Repairs = reps, Mastery = mastery, Enchant = enchant });
            }
            if (slot == EquipmentSlot.Weapon) { h.WeaponId = null; h.WeaponDurability = 0; h.WeaponRepairs = 0; h.WeaponMastery = 1f; }
            else h.Armor.Remove(slot);
            return Result.Success("Unequipped");
        }

        /// <summary>Stat preview: the hero's effective stats if <paramref name="itemId"/> replaced the slot.</summary>
        public static (Stats now, Stats then) Compare(ContentDb c, HeroSave h, EquipmentSlot slot, string itemId)
        {
            var now = Progression.Preview(c, h).Effective;
            var copy = new HeroSave
            {
                ClassId = h.ClassId, TotalXp = h.TotalXp, StatPicks = h.StatPicks, // stat picks: same base on both sides (GG-T591)
                WeaponId = h.WeaponId, WeaponMastery = h.WeaponMastery,
                Armor = h.Armor.ToDictionary(kv => kv.Key, kv => new EquippedArmor { ItemId = kv.Value.ItemId, Mastery = kv.Value.Mastery }),
                Jewelry = new Dictionary<EquipmentSlot, string>(h.Jewelry),
            };
            if (slot == EquipmentSlot.Weapon) copy.WeaponId = itemId;
            else if (IsJewelrySlot(slot)) copy.Jewelry[slot] = itemId;
            else copy.Armor[slot] = new EquippedArmor { ItemId = itemId, Mastery = 1f };
            return (now, Progression.Preview(c, copy).Effective);
        }

        /// <summary>The armor sets this hero is wearing pieces of, most pieces first (GG-T182, Status/Equip screens).</summary>
        public static List<SetProgress> WornSets(ContentDb c, HeroSave h) =>
            ArmorSets.Progress(c, h.Armor.Values.Where(a => a?.ItemId != null).Select(a => a.ItemId));

        /// <summary>Set progress if <paramref name="itemId"/> (null = empty) replaced whatever is in <paramref name="slot"/>
        /// — the Equip screen's "3/5 → 4/5" preview.</summary>
        public static List<SetProgress> WornSetsWith(ContentDb c, HeroSave h, EquipmentSlot slot, string itemId)
        {
            var ids = h.Armor.Where(kv => kv.Key != slot && kv.Value?.ItemId != null).Select(kv => kv.Value.ItemId).ToList();
            if (itemId != null && IsArmorSlot(slot)) ids.Add(itemId);
            return ArmorSets.Progress(c, ids);
        }
    }
}
