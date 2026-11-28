using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    public sealed class HeroSave
    {
        public string ClassId;
        public int TotalXp;
        /// <summary>Current HP; 0 = full (wounds carry between battles, §15.1).</summary>
        public int Hp;
        public List<string> Known = new List<string>();
        /// <summary>Five ability-bar entries: ability id, "item:id", or null.</summary>
        public List<string> Bar = new List<string> { null, null, null, null, null };
        /// <summary>Unspent skill points (§15.1, GG-T49), earned per level plus milestone bonuses
        /// (<see cref="Formulas.SkillPointsForLevelUp"/>). Spent on skill-tree nodes (GG-T50).</summary>
        public int SkillPoints;
        /// <summary>The ability id whose "New ability ready" badge the player has seen by opening this hero's Board
        /// (GG-T1407, <see cref="NextAbility.MarkSeen"/>). An older save simply has none.</summary>
        public string AbilityReadySeen;
        /// <summary>Duplicate summons of this hero (§25.10.3, GG-T1421, <see cref="Gacha.Echo"/>), each worth
        /// <see cref="Gacha.EchoSp"/> SP, up to <see cref="Gacha.EchoCap"/>. An older save simply has none.</summary>
        public int Echoes;
        /// <summary>Skill-tree nodes bought with points, as "treeId/nodeId" (§25.6.3, GG-T53, the retired talent trees);
        /// the Inn's Respec clears it and refunds their cost. An older save simply has none.</summary>
        public List<string> TreeNodes = new List<string>();
        /// <summary>Skill points actually paid for each bought node, keyed like <see cref="TreeNodes"/> (premium included), so
        /// Respec refunds what was paid even if the off-tree premium has since changed. A node bought before this was recorded
        /// (an older save) has no entry and refunds its node's cost. Null-safe: may be missing.</summary>
        public Dictionary<string, int> TreeNodeCosts;
        /// <summary>Skill hexagon nodes this hero owns, as "q,r" (GG-T1031, <see cref="SkillHexProgress"/>), bought with
        /// <see cref="SkillPoints"/>. An older save simply has none. Null-safe: may be missing.</summary>
        public List<string> HexNodes = new List<string>();
        /// <summary>Level-up stat points put into each stat (GG-T591, <see cref="StatPoints"/>), keyed by a
        /// <see cref="StatGrantDef.Names"/> entry: how many points, not how much stat. Earned points are derived from the
        /// level, so only the picks are saved. Null-safe: an older save has none (all its points are simply unspent).</summary>
        public Dictionary<string, int> StatPicks;
        /// <summary>Learned passives the player has switched on (GG-T301, <see cref="PassiveLoadout"/>), read only once
        /// <see cref="PassivesChosen"/> is set; until then the first <see cref="PassiveLoadout.Slots"/> learned are on.</summary>
        public List<string> ActivePassives = new List<string>();
        /// <summary>The player has picked which learned passives are on (GG-T301). An older save has not.</summary>
        public bool PassivesChosen;
        /// <summary>A domesticated monster (GG-T303, <see cref="Domestication"/>): <see cref="ClassId"/> is an enemy class. It
        /// fights on its own AI, wears no gear, has no skill tree, and grows slowly up to <see cref="Domestication.LevelCap"/>.</summary>
        public bool Domesticated;
        /// <summary>GG-T532: which version of the class this hero is: the female art (the default) or the male art
        /// (the class's MalePortrait, GG-T557). Looks only: stats, kit and story are the same either way.</summary>
        public HeroVariant Variant;
        /// <summary>GG-T532: the player hasn't picked <see cref="Variant"/> yet. Every new hero joins with it set
        /// (<see cref="Progression.NewHero"/>); the game asks at its next quiet moment and clears it, or clears it silently
        /// (Female) when the class has no male art yet. A save from before v14 has it clear.</summary>
        public bool VariantPending;

        public string WeaponId;
        public int WeaponDurability;
        public int WeaponRepairs;
        /// <summary>Masterwork stat multiplier (§24.6.2, GG-T46); 1 = normal gear.</summary>
        public float WeaponMastery = 1f;
        /// <summary>Armor equipped in Head/Torso/Legs/Gloves/Boots, each with its own durability/repairs/mastery —
        /// the same per-piece tracking Weapon already had (§24.2, GG-T89).</summary>
        public Dictionary<EquipmentSlot, EquippedArmor> Armor = new Dictionary<EquipmentSlot, EquippedArmor>();
        /// <summary>Jewelry equipped in Necklace/Earring1/Earring2/Ring1/Ring2 — plain item ids, no durability
        /// (the same no-wear rule the old Relic slots had, GG-T89).</summary>
        public Dictionary<EquipmentSlot, string> Jewelry = new Dictionary<EquipmentSlot, string>();

        // ── v2-and-earlier save shape, kept only so SaveMigrations can drain them into Armor/Jewelry above.
        // Never read anywhere else — a v3+ save always has these empty/default. ──
        public string ArmorId;
        public int ArmorDurability;
        public int ArmorRepairs;
        public float ArmorMastery = 1f;
        public List<string> Relics = new List<string> { null, null, null };
    }

    /// <summary>GG-T532: a hero's version, picked when they join (owner 2026-11-21: "when they finally join your party you
    /// get to choose the gender ... no change to story or mechanics makes everything equal").</summary>
    public enum HeroVariant
    {
        Female = 0,
        Male = 1,
    }

    /// <summary>GG-T532: the rules of hero variants that need no art: the portrait id a variant reads, the heroes still
    /// waiting for the player's pick, and picking. Whether the art exists is the app's question (Art.HasMaleArt).</summary>
    public static class HeroVariants
    {
        /// <summary>The Portraits/ file <paramref name="def"/> draws as <paramref name="v"/> (GG-T557,
        /// <see cref="GridGame.Core.Content.ClassDef.PortraitFile"/>): its male art when male and it has one, else its
        /// default art; <paramref name="variant"/> picks a numbered alternate (a costume), default the class's own.</summary>
        public static string PortraitFile(GridGame.Core.Content.ClassDef def, HeroVariant v, int? variant = null) =>
            def?.PortraitFile(v == HeroVariant.Male, variant);

        /// <summary>The owned hero's variant; Female for a class the save doesn't hold.</summary>
        public static HeroVariant Of(SaveData s, string classId) =>
            s?.Roster == null || string.IsNullOrEmpty(classId) ? HeroVariant.Female : s.Hero(classId)?.Variant ?? HeroVariant.Female;

        /// <summary>Roster heroes still waiting for the player's pick, in join order.</summary>
        public static List<HeroSave> Pending(SaveData s) =>
            s?.Roster == null ? new List<HeroSave>() : s.Roster.FindAll(h => h != null && h.VariantPending && !h.Domesticated);

        /// <summary>Sets the hero's variant and ends the wait. Picking again later (the hero screen) works the same way.</summary>
        public static void Choose(HeroSave h, HeroVariant v)
        {
            if (h == null) return;
            h.Variant = v == HeroVariant.Male ? HeroVariant.Male : HeroVariant.Female;
            h.VariantPending = false;
        }

        /// <summary>A wanderer's look on the board (either, evenly), from a seed so a battle replays the same.</summary>
        public static HeroVariant Roll(int seed) => new Random(seed).Next(2) == 0 ? HeroVariant.Female : HeroVariant.Male;
    }

    /// <summary>One equipped armor piece (§24.2, GG-T89): mirrors <see cref="WornGear"/>'s shape plus the item id,
    /// since (unlike the bag's Worn list, keyed by item id already) a hero's Armor dict is keyed by slot.</summary>
    public sealed class EquippedArmor
    {
        public string ItemId;
        public int Durability;
        public int Repairs;
        public float Mastery = 1f;
        /// <summary>Armor enchant id (armorenchants.json, GG-T179) bound into this piece by the Enchantress; null = none.
        /// It grants its passive while the piece is worn and intact, and is lost if the piece breaks.</summary>
        public string Enchant;
    }

    public sealed class WornGear
    {
        public int Durability;
        public int Repairs;
        /// <summary>Masterwork stat multiplier this specific copy rolled at craft time (§24.6.2, GG-T46); 1 = normal.</summary>
        public float Mastery = 1f;
        /// <summary>Armor enchant this copy carries (GG-T179), kept while it sits in the bag; null = none.</summary>
        public string Enchant;
    }
}
