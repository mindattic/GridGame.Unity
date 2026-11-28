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
    // Armor enchants (GG-T179, §25.0.8): the Enchantress binds a passive (regeneration, poison ward, thorns…) into a
    // worn armor piece. Onboarding step 6: she joins once the first armor piece is forged, and the guided first
    // enchant (free of gold) binds one into it.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class ArmorEnchanting
    {
        /// <summary>Save flag set by the first armor enchant.</summary>
        public const string FirstEnchantFlag = "onboard:first-enchant";

        /// <summary>One worn armor piece the Enchantress can work on.</summary>
        public sealed class Target
        {
            public HeroSave Hero;
            public EquipmentSlot Slot;
            public EquippedArmor Piece;
            public ItemDef Item;
            public string RowName => PieceRowPrefix + Hero.ClassId + "-" + Slot.ToString().ToLowerInvariant();
        }

        /// <summary>The guided first enchant: which enchant, into which piece, and whether it can be bound right now.</summary>
        public sealed class Plan
        {
            public ArmorEnchantDef Enchant;
            public Target Target;
            public bool Ready;
        }

        /// <summary>Row-name prefixes on the Enchantress's Armor tab (camp-tip and automation targets).</summary>
        public const string PieceRowPrefix = "piece-", EnchantRowPrefix = "aenchant-";

        public static IReadOnlyList<ArmorEnchantDef> All(ContentDb c) => c.ArmorEnchantOrder;

        /// <summary>Like every Enchantress recipe, an armor enchant's tier unlocks with the Alchemist's station level.</summary>
        public static bool IsUnlocked(SaveData s, ArmorEnchantDef e) => e != null && StationProgress.IsUnlocked(s, StationKind.Alchemist, e.Tier);

        public static bool HasInputs(SaveData s, ArmorEnchantDef e) => s != null && e != null && e.Inputs.All(i => s.Count(i.Item) >= i.Count);

        /// <summary>The first enchant is behind this save: it has been bound, or the save is a bot's or a New Game+.</summary>
        public static bool FirstDone(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstEnchantFlag);

        /// <summary>Gold an enchant costs this save: nothing for the guided first one.</summary>
        public static int Gold(SaveData s, ArmorEnchantDef e) => e == null ? 0 : FirstDone(s) ? e.Gold : 0;

        /// <summary>Gold an enchant costs this save after the caravan's Enchanter's Wagon discount (GG-T75).</summary>
        public static int Gold(ContentDb c, SaveData s, ArmorEnchantDef e) => CaravanUpgrades.EnchantGold(c, s, Gold(s, e));

        public static bool CanAfford(SaveData s, ArmorEnchantDef e) => s != null && s.Gold >= Gold(s, e);

        public static bool CanAfford(ContentDb c, SaveData s, ArmorEnchantDef e) => s != null && s.Gold >= Gold(c, s, e);

        /// <summary>Every intact armor piece the roster wears, party heroes first.</summary>
        public static List<Target> Pieces(ContentDb c, SaveData s)
        {
            var list = new List<Target>();
            if (c == null || s == null) return list;
            var heroes = s.Party.Select(s.Hero).Where(h => h != null).Concat(s.Roster.Where(h => !s.Party.Contains(h.ClassId))).ToList();
            foreach (var h in heroes)
                foreach (var slot in BattleEngine.ArmorSlots)
                    if (h.Armor.TryGetValue(slot, out var piece) && piece?.ItemId != null && c.TryItem(piece.ItemId) is ItemDef item && item.Durability > 0)
                        list.Add(new Target { Hero = h, Slot = slot, Piece = piece, Item = item });
            return list;
        }

        public static Target Find(ContentDb c, SaveData s, string classId, EquipmentSlot slot) =>
            Pieces(c, s).FirstOrDefault(t => t.Hero.ClassId == classId && t.Slot == slot);

        /// <summary>The first enchant the Enchantress's camp tip walks and her Armor tab preselects, or null once it's done
        /// (or nobody wears armor yet): an unenchanted piece (a set piece first, as the guided first armor is), and an open
        /// enchant whose materials are in the bag first, then the fewest materials still to find, then file order.</summary>
        public static Plan First(ContentDb c, SaveData s)
        {
            if (c == null || FirstDone(s)) return null;
            var target = Pieces(c, s).Where(t => t.Piece.Enchant == null)
                .OrderByDescending(t => c.TryArmorSetOf(t.Item.Id) != null).FirstOrDefault();
            if (target == null) return null;
            var e = All(c).Select((x, i) => (x, i)).Where(p => IsUnlocked(s, p.x))
                .OrderByDescending(p => HasInputs(s, p.x))
                .ThenBy(p => p.x.Inputs.Sum(i => Math.Max(0, i.Count - s.Count(i.Item))))
                .ThenBy(p => p.i).Select(p => p.x).FirstOrDefault();
            return e == null ? null : new Plan { Enchant = e, Target = target, Ready = HasInputs(s, e) };
        }

        /// <summary>Binds <paramref name="e"/> into the armor <paramref name="h"/> wears in <paramref name="slot"/>, replacing
        /// any enchant it had. The piece keeps it through unequip and re-equip, and loses it if it breaks.</summary>
        public static Result Enchant(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot, ArmorEnchantDef e)
        {
            if (c == null || s == null || h == null || e == null) return Result.Fail("Nothing to enchant");
            if (!h.Armor.TryGetValue(slot, out var piece) || piece?.ItemId == null) return Result.Fail("No armor worn there");
            var item = c.TryItem(piece.ItemId);
            if (item == null || item.Durability <= 0) return Result.Fail("That can't hold an enchant");
            if (piece.Enchant == e.Id) return Result.Fail($"Already bound with {e.Name}");
            if (!IsUnlocked(s, e)) return Result.Fail($"Requires {StationProgress.TierName(e.Tier)}");
            if (!HasInputs(s, e)) return Result.Fail("Missing materials");
            int gold = Gold(c, s, e);
            if (s.Gold < gold) return Result.Fail("Not enough gold");
            s.Gold -= gold;
            foreach (var ic in e.Inputs) s.AddItem(ic.Item, -ic.Count);
            string replaced = c.TryArmorEnchant(piece.Enchant)?.Name;
            piece.Enchant = e.Id;
            StationProgress.Gain(s, StationKind.Alchemist, Crafting.CraftXp);
            s.Flags.Add(FirstEnchantFlag);
            string passive = c.TryAbility(e.Passive)?.Name ?? e.Name;
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            return Result.Success($"{hero}'s {item.Name} now grants {passive}" + (replaced != null ? $" (replacing {replaced})" : ""));
        }

        /// <summary>The passive ability an enchant id grants, or null.</summary>
        public static AbilityDef PassiveOf(ContentDb c, string enchantId) => c?.TryAbility(c.TryArmorEnchant(enchantId)?.Passive);
    }
}
