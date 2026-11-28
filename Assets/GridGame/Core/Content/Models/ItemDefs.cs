using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>Tome (GG-T301): teaches <see cref="ItemDef.TeachesAbility"/>, a Passive or Reactive, to a chosen hero.
    /// Chest (GG-T301): opened from the bag, it rolls <see cref="ItemDef.OpensTable"/> (a droptables.json row).</summary>
    public enum ItemType { Equipment, Consumable, CraftingMaterial, QuestItem, RecipeScroll, Tome, Chest }

    public enum ItemRarity { Junk, Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>
    /// Every equip slot (§24.2, GG-T89). Weapon and the five armor pieces (Head/Torso/Legs/Gloves/Boots) each
    /// hold exactly one exact-matching item (<see cref="ItemDef.Slot"/> == the slot). Necklace is the same
    /// (one exact slot). Earring1/Earring2 and Ring1/Ring2 are *instances* of a slot category: any item flagged
    /// <see cref="ItemDef.IsEarring"/>/<see cref="ItemDef.IsRing"/> fits either of its pair, the same way any
    /// <c>IsRelic</c> item used to fit any of the old Relic1-3.
    /// </summary>
    public enum EquipmentSlot { None, Weapon, Head, Torso, Legs, Gloves, Boots, Necklace, Earring1, Earring2, Ring1, Ring2 }

    /// <summary>A weapon's proficiency category (GG-T52). Every hero can always wield <c>Simple</c> weapons;
    /// <c>Martial</c> and <c>Arcane</c> are gated by <see cref="ClassDef.WeaponProficiencies"/> (the class's
    /// own home-tree training) or an earned proficiency-unlock passive (Martial Weapons / Arcane Focus).</summary>
    public enum WeaponClass { Simple, Martial, Arcane }

    /// <summary>An armor piece's weight class (GG-T52), ordered light-to-heavy so a hero's allowance can be
    /// compared with <![CDATA[<=]]>. Only <c>Heavy</c> is ever actually gated today — everyone can wear Light
    /// and Medium regardless of class (see <see cref="ClassDef.ArmorProficiency"/>); Heavy needs the class's
    /// own training or the Heavy Armor Proficiency passive.</summary>
    public enum ArmorWeight { Light, Medium, Heavy }

    public enum CraftStation { Forge, Alchemy, Enchant }

    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        public string Description;
        public ItemType Type;
        public ItemRarity Rarity = ItemRarity.Common;
        public EquipmentSlot Slot = EquipmentSlot.None;
        public string WeaponType;
        /// <summary>Weapon-slot items only (GG-T52): which proficiency category (Simple/Martial/Arcane) this
        /// weapon needs. Ignored for every other slot. Default Simple = usable by any hero.</summary>
        public WeaponClass WeaponClass = WeaponClass.Simple;
        /// <summary>Head/Torso/Legs/Gloves/Boots items only (GG-T52): how much armor training (Light/Medium/
        /// Heavy) this piece demands. Ignored for Weapon/jewelry slots. Default Light = usable by any hero.</summary>
        public ArmorWeight ArmorWeight = ArmorWeight.Light;
        public int BaseCost;
        /// <summary>-1 = BaseCost/2.</summary>
        public int SellValue = -1;
        public int MaxStack = 99;
        public int Durability;
        public int BaseHealing;
        public int BaseDamage;
        public Stats Stats = new Stats();
        public int BattleStartManaOrbs;
        /// <summary>Consumable that casts this ability on use (e.g. Sleep Dart → "sleep").</summary>
        public string OnUseAbility;
        public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>();
        public bool SoldByVendor;
        /// <summary>GG-T538: the Merchant's stock tier this item joins at (1 = on the first visit). Tier N opens on the
        /// clear of <c>Meta.VendorTiers.OpensAfter[N-1]</c>; absent or 0 reads as tier 1. Only means something on a
        /// <see cref="SoldByVendor"/> item.</summary>
        public int ShopTier;
        public string Icon;
        public DamageType DamageType = DamageType.Physical;
        public int GrantsOrbs;
        public string CuresBuff;
        public bool CuresAll;
        public bool Revives;
        public bool Escape;
        /// <summary>Usable only from the camp Items menu (Tent).</summary>
        public bool OutOfBattleOnly;
        public string BonusVsTag;
        public float BonusMultiplier = 1f;
        /// <summary>Relic passive: this hero's pincer strikes deal +X (fraction).</summary>
        public float PincerDamageBonus;
        /// <summary>Gear passive (GG-T297): Evasion points while equipped, 0..20; lost when the piece breaks.</summary>
        public float EvasionBonus;
        /// <summary>Relic passive: HP restored each time control returns to the player.</summary>
        public int RegenPerTurn;
        /// <summary>Relic passive: start every battle with Protection.</summary>
        public bool StartsProtected;
        /// <summary>RecipeScroll only (§24.6.2, GG-T36): the recipe this item teaches when read at the Items screen.</summary>
        public string TeachesRecipe;
        /// <summary>Tome only (GG-T301): the Passive or Reactive ability it teaches the hero it's read to.</summary>
        public string TeachesAbility;
        /// <summary>Chest only (GG-T301): the droptables.json row rolled when it's opened.</summary>
        public string OpensTable;
        /// <summary>Named upgrade passive (§24.6.1, GG-T47): flavor name of a weapon's fully-upgraded (Master-tier,
        /// "+3") named passive, shown on the Equip compare panel. A weapon whose base already carries an on-hit or
        /// lifesteal trait (GG-T181) names that trait here on every step, and its "+3" passive is the same mechanic,
        /// stronger. Null = no named passive.</summary>
        public string PassiveName;
        /// <summary>PassiveName's effect text for the compare panel, e.g. "Pincer strikes have a 20% chance to Burn."</summary>
        public string PassiveDescription;
        /// <summary>PassiveName mechanic: a landed pincer strike (not a supporter or counter hit) by this weapon's
        /// wielder has this chance to apply this buff to the target (e.g. Flamebrand's Burn).</summary>
        public string PincerApplyBuff;
        public float PincerApplyBuffChance;
        /// <summary>PassiveName mechanic: heals the wielder this fraction of any damage a hero lands with this weapon
        /// (e.g. Vampiric Edge).</summary>
        public float LifestealFraction;
        /// <summary>PassiveName mechanic: while this weapon's wielder stands adjacent to an ally hero, that ally takes
        /// this much less damage (the strongest adjacent guardian wins — doesn't stack).</summary>
        public float AdjacentAllyDamageReduction;

        public int EffectiveSellValue => SellValue >= 0 ? SellValue : System.Math.Max(1, BaseCost / 2);
        /// <summary>Content authors always set <c>Slot: "Earring1"</c> for an earring item; this flag (not an
        /// exact slot match) is what lets it fit either Earring1 or Earring2 (GG-T89).</summary>
        public bool IsEarring => Slot == EquipmentSlot.Earring1 || Slot == EquipmentSlot.Earring2;
        /// <summary>Content authors always set <c>Slot: "Ring1"</c> for a ring item; fits either Ring1 or Ring2.</summary>
        public bool IsRing => Slot == EquipmentSlot.Ring1 || Slot == EquipmentSlot.Ring2;
    }

    /// <summary>One tier of a named armor set's bonus (GG-T182): active while the hero wears at least
    /// <see cref="Pieces"/> of the set's five pieces. Every field stacks onto the hero exactly like the same
    /// field on an <see cref="ItemDef"/> (stats add, resistances multiply, relic passives add).</summary>
    public sealed class SetBonusDef
    {
        public int Pieces;
        public Stats Stats = new Stats();
        public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>();
        public float PincerDamageBonus;
        public int RegenPerTurn;
        public int BattleStartManaOrbs;
        public bool StartsProtected;
    }

    /// <summary>A named five-piece armor set (GG-T182, armorsets.json): one Head/Torso/Legs/Gloves/Boots piece
    /// each, all of one <see cref="ArmorWeight"/>, with 2- and 5-piece bonuses resolved by
    /// <see cref="GridGame.Core.Rules.ArmorSets"/>. <see cref="Level"/> is the campaign level it's pitched at.</summary>
    public sealed class ArmorSetDef
    {
        public string Id;
        public string Name;
        public ArmorWeight Weight;
        public int Level = 1;
        public string Description;
        public List<string> Pieces = new List<string>();
        public List<SetBonusDef> Bonuses = new List<SetBonusDef>();
    }

    public sealed class ItemCount
    {
        public string Item;
        public int Count = 1;
    }

    public sealed class RecipeDef
    {
        public string Id;
        public CraftStation Station;
        public List<ItemCount> Inputs = new List<ItemCount>();
        public int Gold;
        /// <summary>Optional base item consumed (upgrade/enchant).</summary>
        public string BaseItem;
        public string Result;
        public int ResultCount = 1;
        /// <summary>Tier ladder rung (§24.6.1): 1 Apprentice (from the start), 2 Journeyman, 3 Master —
        /// gated by the crafting station's level (GG-T35). Missing from content = 1.</summary>
        public int Tier = 1;
        /// <summary>A starter weapon recipe (GG-T177, §25.0.6): the Forge's guided first forge. An Apprentice Forge
        /// recipe for a weapon, no gold, inputs from the first theme's drops; forging one equips it on the hero it suits.</summary>
        public bool Starter;
        /// <summary>Reagent quality (GG-T45): brewing with these inputs instead of <see cref="Inputs"/> — swapping
        /// in a higher-rarity reagent for the recipe's key material — yields <see cref="PotencyResult"/> instead
        /// of <see cref="Result"/>. Same recipe, better inputs, better output. Null/empty = no potency variant.</summary>
        public List<ItemCount> PotencyInputs = new List<ItemCount>();
        public string PotencyResult;
        public int PotencyResultCount = 1;
    }

    public sealed class DropEntry
    {
        public string Item;
        public float Chance;
        public int Min = 1;
        public int Max = 1;
    }

    public sealed class DropTableDef
    {
        public string Id;
        public int CoinsMin;
        public int CoinsMax;
        public List<DropEntry> Entries = new List<DropEntry>();
    }

    public sealed class EnchantDef
    {
        public string Id;
        public string Name;
        public string Element;
        public string Essence;
        public int ArcaneDust = 2;
        public int Gold = 150;
        public Stats Bonus = new Stats();
    }

    /// <summary>
    /// An armor enchant (GG-T179, armorenchants.json): the Enchantress binds a <see cref="AbilityKind.Passive"/>
    /// ability into one worn armor piece (Head/Torso/Legs/Gloves/Boots). Unlike the weapon-only elemental
    /// <see cref="EnchantDef"/>s, which turn a weapon into an "(Element)" variant item, an armor enchant lives on the
    /// piece itself (its save entry's <c>Enchant</c>), so set membership, wear and Masterwork rolls are untouched; the
    /// wearer has <see cref="Passive"/> in battle while the piece is intact. One enchant per piece (a new one replaces it).
    /// </summary>
    public sealed class ArmorEnchantDef
    {
        public string Id;
        public string Name;
        /// <summary>The passive ability (abilities.json, Kind Passive) the enchanted piece grants its wearer.</summary>
        public string Passive;
        public List<ItemCount> Inputs = new List<ItemCount>();
        public int Gold;
        /// <summary>Tier ladder rung (§24.6.1): unlocks with the Alchemist's station level, like every Enchantress recipe.</summary>
        public int Tier = 1;
    }
}
