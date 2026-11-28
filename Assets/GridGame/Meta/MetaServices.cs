using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    public readonly struct Result
    {
        public readonly bool Ok;
        public readonly string Message;
        private Result(bool ok, string message) { Ok = ok; Message = message; }
        public static Result Success(string message = null) => new Result(true, message);
        public static Result Fail(string message) => new Result(false, message);
        public override string ToString() => (Ok ? "OK " : "FAIL ") + Message;
    }

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

    // ─────────────────────────────────────────────────────────────────────────────
    // Abilities: learn + slot (§25.6)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Abilities
    {
        /// <summary>What a hero could learn: its class list plus the general spell catalog (hero spells only). GG-T1079: no
        /// longer sold for gold (abilities come from the skill board); kept as the catalog the ability audits check.
        /// GG-T1098: the family-tree paths are retired, so every class lists its <see cref="ClassDef.Abilities"/> and the whole catalog.</summary>
        public static List<AbilityDef> Learnable(ContentDb c, HeroSave h)
        {
            var def = c.TryClass(h.ClassId);
            var set = new List<AbilityDef>();
            if (h.Domesticated) return set; // a tamed monster fights with its own instincts, never a trainer's skills (GG-T303)
            var own = def == null ? new List<string>() : def.Abilities;
            foreach (var id in own)
            {
                var a = c.TryAbility(id);
                if (a != null && IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a)) set.Add(a);
            }
            foreach (var a in c.Abilities.Values)
                if (IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a) &&
                    (a.Kind == AbilityKind.Spell || a.Universal))
                    set.Add(a);
            // GG-T388: the hero's own kit moves first (the cheapest, and what it was built to do), then by price.
            var kit = Progression.KitAbilities(def);
            return set.OrderBy(a => kit.Contains(a.Id) ? 0 : 1).ThenBy(a => Price(a)).ThenBy(a => a.Name).ToList();
        }

        /// <summary>Boss-phase abilities only an enemy fires (an <c>OnEnterAbility</c>): free, so never learnable.</summary>
        private static readonly HashSet<string> EnemyOnly = new HashSet<string> { "enrage", "chrono_rewind", "lich_soul_harvest", "stampede_rush", "hierarch_open_reservoir" };

        public static bool IsHeroUsable(AbilityDef a) => a.Kind != AbilityKind.Passive &&
            (a.Kind == AbilityKind.Spell || a.Kind == AbilityKind.Skill) && !a.Id.StartsWith("charge_") && !EnemyOnly.Contains(a.Id);

        /// <summary>GG-T215: priced off <see cref="BattleConfig"/>'s training constants — a spell by its orb cost,
        /// a skill by its CooldownTurns (skills carry no orb cost, so cooldown is the power proxy), replacing the
        /// old flat 300G-for-every-skill price the RELEASE_SCOPE watch-list flagged.</summary>
        public static int Price(AbilityDef a) => a.Kind == AbilityKind.Skill
            ? BattleConfig.SkillTrainBasePrice + BattleConfig.SkillTrainPricePerCooldown * a.CooldownTurns
            : BattleConfig.SpellTrainBasePrice + BattleConfig.SpellTrainPricePerOrb * a.Cost;

        /// <summary>Is <paramref name="a"/> one of this hero's own kit moves (its class's default loadout)? Kit moves are waived
        /// from their Requires (<see cref="RequiresWaived"/>).</summary>
        public static bool IsKitMove(ContentDb c, HeroSave h, AbilityDef a)
        {
            if (a == null || h == null) return false;
            var def = c.TryClass(h.ClassId);
            return Progression.KitAbilities(def).Contains(a.Id);
        }

        /// <summary>Every hero-usable ability some hero could learn (GG-T1079: the gold Learn screen is gone): any spell, any universal skill, and
        /// every skill on a hero class's list (the union of <see cref="Learnable"/> over the hero classes).</summary>
        public static List<AbilityDef> LearnableByAnyHero(ContentDb c)
        {
            var onHeroLists = new HashSet<string>();
            foreach (var cls in c.Classes.Values)
                if (cls.Team == Team.Hero)
                    foreach (var id in cls.Abilities) onHeroLists.Add(id);
            return c.Abilities.Values
                .Where(a => IsHeroUsable(a) && (a.Kind == AbilityKind.Spell || a.Universal || onHeroLists.Contains(a.Id)))
                .OrderBy(a => a.Id).ToList();
        }

        /// <summary>
        /// GG-T183 reskin audit: the mechanic an ability plays, with its name, numbers (damage, heal, cost, cooldown,
        /// hasten amount, chance) and art stripped away. Two abilities with the same key are the same move with a
        /// different coat of paint. The key is: how it is paid and timed (skill on a cooldown, spell cast on the
        /// timeline, instant spell), its footprint (shape, radius where it changes the footprint, auto/pick, who it
        /// filters, adjacent-only), and each effect it carries — damage (Fire and Lightning separately, since they
        /// thaw/steam and shock/blind), heal or drain, the status it applies or clears, steal, orbs, revive,
        /// bonus-vs-tag, the movement it causes (teleport, knockback, pull, swap, step back) and the timeline change
        /// (hasten, delay). Other elements are resistance-only, so they count as the same mechanic.
        /// </summary>
        public static string MechanicKey(AbilityDef a)
        {
            string pay = a.Kind == AbilityKind.Spell ? (a.CastTime > 0f ? "cast" : "instant") : "skill";
            bool area = a.Shape == TargetShape.Square || a.Shape == TargetShape.Diamond || a.Shape == TargetShape.Cross;
            bool self = a.Shape == TargetShape.Self;
            bool all = a.Shape == TargetShape.AllAllies || a.Shape == TargetShape.AllEnemies;
            var k = new List<string>
            {
                pay,
                a.Shape + (area ? a.Radius.ToString() : ""),
                self || all ? "-" : a.Mode.ToString(),
                self || all ? "-" : a.Filter.ToString(),
                a.AdjacentOnly ? "adj" : "",
            };
            if (a.BaseDamage > 0) k.Add("dmg" + (a.DamageType == DamageType.Fire || a.DamageType == DamageType.Lightning ? ":" + a.DamageType : ""));
            if (a.BaseHeal > 0) k.Add(a.Filter == TargetFilter.EnemyOnly && !self ? "drain" : "heal");
            if (a.ApplyBuff != null) k.Add("buff:" + a.ApplyBuff);
            if (a.RemovesDebuffs) k.Add("cleanse");
            if (a.RemovesBuff != null) k.Add("cure:" + a.RemovesBuff);
            if (a.StealsMana) k.Add("steal");
            if (a.DrainsOrbs > 0) k.Add("siphon"); // GG-T332
            if (a.Tame > 0f) k.Add("tame"); // GG-T303
            if (a.GrantsOrbs > 0) k.Add("orbs");
            if (a.Revives) k.Add("revive");
            if (a.Escape) k.Add("escape");
            if (a.BonusVsTag != null) k.Add("vs:" + a.BonusVsTag);
            if (a.Vaults) k.Add("vault"); // GG-T868
            else if (a.IsTeleport) k.Add("teleport");
            if (a.Knockback > 0) k.Add("knockback");
            if (a.Pull > 0) k.Add("pull");
            if (a.Magnetizes > 0) k.Add("magnetize"); // GG-T878
            if (a.Vanishes) k.Add("vanish"); // GG-T861
            if (a.SwapsPlaces) k.Add("swap");
            if (a.StepBack > 0) k.Add("stepback");
            if (a.HastenU > 0f) k.Add("hasten");
            if (a.Pushback > 0f) k.Add("pushback");
            if (a.SwapsTurns) k.Add("countermarch"); // GG-T336
            if (a.Echoes) k.Add("echo"); // GG-T388
            if (a.RedirectsDebuffs) k.Add("redirect"); // GG-T887
            if (a.Afterimage > 0) k.Add("afterimage"); // GG-T867
            if (a.StealsBoon) k.Add("swipeboon"); // GG-T866
            return string.Join("|", k);
        }

        /// <summary>Does the ability do anything at all when it resolves? (GG-T183 found four that did nothing.)</summary>
        public static bool HasEffect(AbilityDef a) =>
            a.BaseDamage > 0 || a.BaseHeal > 0 || a.ApplyBuff != null || a.RemovesDebuffs || a.RemovesBuff != null || a.StealsMana || a.DrainsOrbs > 0 ||
            a.GrantsOrbs > 0 || a.Revives || a.Escape || a.IsTeleport || a.Knockback > 0 || a.Pull > 0 || a.Magnetizes > 0 || a.Vanishes || a.SwapsPlaces ||
            a.StepBack > 0 || a.HastenU > 0f || a.Pushback > 0f || a.Tame > 0f || a.SwapsTurns || a.Echoes || a.RedirectsDebuffs || a.StealsBoon || a.Afterimage > 0; // GG-T867: an Afterimage dashes; GG-T866: a Swipe Boon steals a boon; GG-T887: a Redirect Flow moves debuffs; GG-T388: an Echo repeats a move; GG-T303: a Tame turns a monster; GG-T336: a Countermarch reorders the foes

        /// <summary>GG-T592 (§25.6.5): is <paramref name="a"/> waived from its <see cref="AbilityDef.Requires"/> for this hero?
        /// A hero's own kit moves are (what its class was built to do: a fresh Red Mage is never locked out of its own
        /// Fireball), and so is the guided first skill
        /// (GG-T180) — GG-T1086: no longer, it is taught on the skill board by <see cref="Training.Teach"/>, and dropping it
        /// spares the Learn tab a Training.First route search per row.</summary>
        public static bool RequiresWaived(ContentDb c, SaveData s, HeroSave h, AbilityDef a) => IsKitMove(c, h, a);

        /// <summary>GG-T592: the stats this hero falls short on for <paramref name="a"/> (a display hint only, GG-T1031), measured against
        /// its skill hexagon stat totals (<see cref="HeroBoard.BoardStats"/>, GG-T1058; was its spent level-up stat points).
        /// Empty when it can be bought now (met or waived).</summary>
        public static List<(string Abbrev, string Stat, float Min, float Have)> Unmet(ContentDb c, SaveData s, HeroSave h, AbilityDef a)
        {
            if (a == null || h == null || !a.HasRequires || RequiresWaived(c, s, h, a)) return new List<(string, string, float, float)>();
            return a.UnmetRequires(HeroBoard.BoardStats(c, h));
        }

        /// <summary>A locked Learn entry's reason: "needs INT 8 (have 3)", or "needs VIT 5, WIS 4 (have 2, 0)". Null when met.</summary>
        public static string NeedsText(List<(string Abbrev, string Stat, float Min, float Have)> unmet) =>
            unmet == null || unmet.Count == 0 ? null
                : $"needs {string.Join(", ", unmet.Select(u => $"{u.Abbrev} {u.Min:0}"))} (have {string.Join(", ", unmet.Select(u => $"{u.Have:0}"))})";

        /// <summary>Reorder the bar (owner direction 2026-10-11: organise your abilities): swap two unlocked slots.</summary>
        public static Result Swap(SaveData s, HeroSave h, int a, int b)
        {
            int unlocked = Progression.UnlockedSlots(s);
            if (a < 0 || b < 0 || a >= unlocked || b >= unlocked) return Result.Fail("Slot locked");
            if (a == b) return Result.Fail("Same slot");
            while (h.Bar.Count < Progression.MaxBarSlots) h.Bar.Add(null);
            var t = h.Bar[a]; h.Bar[a] = h.Bar[b]; h.Bar[b] = t;
            return Result.Success("Moved");
        }

        public static Result Assign(ContentDb c, SaveData s, HeroSave h, int slot, string entry)
        {
            if (slot < 0 || slot >= Progression.MaxBarSlots) return Result.Fail("No such slot");
            if (h != null && h.Domesticated && entry != null) return Result.Fail("A tamed monster fights on instinct"); // GG-T303: no bar
            if (slot >= Progression.UnlockedSlots(s)) return Result.Fail("Slot locked");
            if (entry != null)
            {
                if (entry.StartsWith("item:"))
                {
                    var item = c.TryItem(entry.Substring(5));
                    if (item == null || item.Type != ItemType.Consumable) return Result.Fail("Not usable in battle");
                }
                else if (entry.StartsWith("weapon:"))
                {
                    var w = c.TryItem(entry.Substring(7));
                    if (w == null || w.Slot != EquipmentSlot.Weapon) return Result.Fail("Not a weapon");
                    if (s.Count(w.Id) < 1) return Result.Fail("You need a spare one in your bag");
                }
                else if (!HeroBoard.OwnsAbility(c, h, entry)) return Result.Fail("Not learned"); // known, or on an owned board node (GG-T1043)
                int dup = h.Bar.IndexOf(entry);
                if (dup >= 0 && dup != slot) h.Bar[dup] = null;
            }
            h.Bar[slot] = entry;
            return Result.Success();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Training (GG-T180, §25.0.9): onboarding step 7, the last of the chain. Once the first armor enchant is bound, the
    // Abilities screen opens on its Board sub-tab: a guided prompt trains one new skill (free) and slots it on the bar.
    // GG-T1085: the lesson is an ability node on the skill hexagon, bought (route and all) without spending skill points.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Training
    {
        /// <summary>Save flag set by the guided first skill; the onboarding chain is complete once it is set.</summary>
        public const string FirstSkillFlag = "onboard:first-skill";
        /// <summary>The Abilities screen's camp tip while this step is ahead (its plain "abilities" tip otherwise).</summary>
        public const string TipStation = "abilities-learn";

        /// <summary>The guided first skill: which hero learns which skill, and whether the bar has room for it.</summary>
        public sealed class Plan
        {
            public HeroSave Hero;
            public AbilityDef Skill;
            public int Slot;
            /// <summary>The ability node <see cref="Teach"/> buys (GG-T1085).</summary>
            public (int Q, int R) Node;
            /// <summary>The hero isn't on the board yet: <see cref="Teach"/> joins it on its class Start path first.</summary>
            public bool Join;
        }

        /// <summary>The first skill is behind this save: trained, or a bot's or New Game+ save.</summary>
        public static bool FirstDone(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstSkillFlag);

        /// <summary>Step 7 is the current onboarding step: the first armor enchant is done (§25.0.8) and the first skill isn't.
        /// With the Enchanter hidden (<see cref="Features.Enchanter"/>, GG-T436) the enchant step is skipped and step 7
        /// follows the first armor (step 5) instead, so the chain never stalls on a station the player can't open.</summary>
        public static bool Step(SaveData s) => !FirstDone(s) &&
            (Features.Enchanter ? s.Flags.Contains(ArmorEnchanting.FirstEnchantFlag) : Forging.ArmorDone(s));

        /// <summary>Is <paramref name="a"/> right now the guided first skill (<see cref="First"/>'s pick, free, auto-slotted)?</summary>
        public static bool Guides(ContentDb c, SaveData s, AbilityDef a) => a != null && Step(s) && First(c, s)?.Skill == a;

        /// <summary>The bar slot a newly trained skill goes into: the first empty unlocked slot, else the last unlocked one.</summary>
        public static int SlotFor(SaveData s, HeroSave h)
        {
            int unlocked = Math.Min(Progression.UnlockedSlots(s), Progression.MaxBarSlots);
            if (h == null || unlocked <= 0) return -1;
            while (h.Bar.Count < Progression.MaxBarSlots) h.Bar.Add(null);
            for (int i = 0; i < unlocked; i++) if (h.Bar[i] == null) return i;
            return unlocked - 1;
        }

        /// <summary>The guided first skill (GG-T1085), or null outside step 7: for the party lead first (then the party,
        /// then the reserve), an ability node on the skill hexagon it can reach (joining its Start path first when it isn't
        /// on the board) whose ability it doesn't know: a frontier node when one exists, else the nearest by route; skills first.</summary>
        public static Plan First(ContentDb c, SaveData s)
        {
            if (c == null || !Step(s)) return null;
            var board = SkillHexProgress.Board(c);
            if (board == null) return null;
            var heroes = s.Party.Select(s.Hero).Where(h => h != null).Concat(s.Roster.Where(h => !s.Party.Contains(h.ClassId)));
            foreach (var h in heroes)
            {
                if (h.Domesticated) continue;
                bool join = (h.HexNodes?.Count ?? 0) == 0;
                IReadOnlyCollection<(int Q, int R)> owned = join ? board.StartOf(h.ClassId) : SkillHexProgress.Owned(h).Nodes;
                if (owned.Count == 0) continue;
                var pick = NodeFor(c, board, h, owned);
                if (pick == null) continue;
                return new Plan { Hero = h, Skill = c.TryAbility(board.Node(pick.Value).Ability), Node = pick.Value, Join = join, Slot = SlotFor(s, h) };
            }
            return null;
        }

        private static (int Q, int R)? NodeFor(ContentDb c, SkillHexBoard board, HeroSave h, IReadOnlyCollection<(int Q, int R)> owned)
        {
            bool Wanted((int Q, int R) pt)
            {
                var n = board.Node(pt);
                if (n == null || n.Kind != SkillHexNodeKind.Ability || string.IsNullOrEmpty(n.Ability) || owned.Contains(pt)) return false;
                var a = c.TryAbility(n.Ability);
                return a != null && !h.Known.Contains(a.Id) && !owned.Any(o => board.Node(o)?.Ability == a.Id);
            }
            int Rank((int Q, int R) pt) => c.TryAbility(board.Node(pt).Ability).Kind == AbilityKind.Skill ? 0 : 1;
            var near = board.Frontier(owned).Where(Wanted).OrderBy(Rank).ThenBy(p => p).ToList();
            if (near.Count > 0) return near[0];
            var far = board.Def.Nodes.Select(n => (n.Q, n.R)).Where(Wanted)
                .Select(pt => (pt, route: board.CheapestRoute(owned, pt))).Where(x => x.route != null)
                .OrderBy(x => x.route.Count).ThenBy(x => Rank(x.pt)).ThenBy(x => x.pt).ToList();
            return far.Count > 0 ? far[0].pt : ((int Q, int R)?)null;
        }

        /// <summary>Trains the guided first skill: Join if needed, buys the route to the node free (no SP), slots it via
        /// <see cref="SlotFor"/>, sets <see cref="FirstSkillFlag"/>.</summary>
        public static Result Teach(ContentDb c, SaveData s)
        {
            var plan = First(c, s);
            if (plan == null) return Result.Fail("Nothing to train");
            var h = plan.Hero;
            var board = SkillHexProgress.Board(c);
            if (plan.Join) { var j = SkillHexProgress.Join(c, h); if (!j.Ok) return j; }
            var route = board.CheapestRoute(SkillHexProgress.Owned(h).Nodes, plan.Node);
            if (route == null) return Result.Fail("Can't reach that");
            foreach (var pt in route)
            {
                int price = board.Price(pt);
                h.SkillPoints += price; // free: the price is granted, then spent
                var r = SkillHexProgress.Buy(c, h, pt);
                if (!r.Ok) { h.SkillPoints -= price; return r; }
            }
            Codex.Unlock(s, CodexKinds.Ability, plan.Skill.Id);
            int slot = SlotFor(s, h);
            if (slot >= 0) Abilities.Assign(c, s, h, slot, plan.Skill.Id);
            s.Flags.Add(FirstSkillFlag);
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            return Result.Success(slot >= 0 ? $"{hero} learned {plan.Skill.Name}: it's on the battle bar, slot {slot + 1}" : $"{hero} learned {plan.Skill.Name}");
        }
    }

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

    /// <summary>
    /// The one rule for whether a unit can ever be lost for good (GG-T114). Owner rule: "no one dies permanently,
    /// that's lame" — in the normal game nobody is ever lost; heroes, guests, recruits and dialogue-joined heroes fall,
    /// get revived or recover, and come back. A planned opt-in Ironman mode will switch this on; every "unit is lost"
    /// decision (the guest record in <see cref="SaveData.LostGuests"/>, the Summon Circle pool, the guest exclusion in
    /// <c>BattleFactory.Build</c>, the battle's fall announcement via <c>BattleSetup.Permadeath</c>) reads it here,
    /// so Ironman only has to change <see cref="Enabled"/>.
    /// </summary>
    public static class Permadeath
    {
        /// <summary>Always false today — no Ironman mode exists yet (BIBLE §29.1).</summary>
        public static bool Enabled(SaveData s) => false;

        /// <summary>Whether this class has been lost for good. Off → never, even for a save that recorded a loss
        /// under the old "lost forever" guest rule: those guests come back.</summary>
        public static bool IsLost(SaveData s, string classId) => Enabled(s) && s.LostGuests.Contains(classId);
    }

    public static class Summon
    {
        /// <summary>A paywall hook (§25.18, GG-T232): <see cref="Paywall.Cost"/>, no longer rising per recruit.</summary>
        public static int Price(ContentDb c, SaveData s) => Paywall.Cost(PaywallKind.Summon, s);

        /// <summary>The Summon Circle's gold pool: every hero class whose <see cref="ClassDef.Acquire"/> is Summon (GG-T1416).</summary>
        public static List<string> Ids(ContentDb c) =>
            c.Classes.Values.Where(d => d.Team == Team.Hero && d.Acquire == Acquire.Summon).Select(d => d.Id).ToList();

        public static List<ClassDef> Pool(ContentDb c, SaveData s) =>
            c.Classes.Values.Where(d => d.Team == Team.Hero && d.Acquire == Acquire.Summon && !Permadeath.IsLost(s, d.Id)).ToList();

        private static Result CanRecruit(ContentDb c, SaveData s, string classId)
        {
            var def = c.TryClass(classId);
            if (def == null || def.Team != Team.Hero || def.Acquire != Acquire.Summon) return Result.Fail("Not summonable");
            if (Permadeath.IsLost(s, classId)) return Result.Fail("Fell in battle for good");
            if (s.Hero(classId) != null) return Result.Fail("Already in your roster");
            if (Paywall.Pending(s, PaywallKind.Summon) != null) return Result.Fail("A summoning is already under way");
            if (s.Gold < Price(c, s)) return Result.Fail("Not enough gold");
            return Result.Success();
        }

        /// <summary>The instant path (bots, tests): pays and the recruit arrives at once.</summary>
        public static Result Recruit(ContentDb c, SaveData s, string classId)
        {
            var ok = CanRecruit(c, s, classId);
            if (!ok.Ok) return ok;
            s.Gold -= Price(c, s);
            return Arrive(c, s, classId);
        }

        /// <summary>The player's path: pays and starts the summoning job; <see cref="Paywall.Finish"/> brings the hero.</summary>
        public static Result BeginRecruit(ContentDb c, SaveData s, string classId, DateTime now, out PaywallJob job)
        {
            job = null;
            var ok = CanRecruit(c, s, classId);
            return ok.Ok ? Paywall.Start(s, PaywallKind.Summon, classId, now, out job) : ok;
        }

        internal static Result Arrive(ContentDb c, SaveData s, string classId)
        {
            if (c.TryClass(classId) == null || s.Hero(classId) != null) return Result.Fail("The summoning fizzles");
            s.Recruits++;
            Join(c, s, classId);
            return Result.Success($"{c.Class(classId).Name} joins you!");
        }

        /// <summary>Adds a hero to the roster (and the party, if there's room) at the roster's average experience —
        /// shared by the Summon Circle and dialogue's RecruitHero effect (GG-T117). No-op if already owned. It arrives with
        /// the Trust of <paramref name="route"/>.</summary>
        public static HeroSave Join(ContentDb c, SaveData s, string classId, JoinRoute route = JoinRoute.Hire)
        {
            var existing = s.Hero(classId);
            if (existing != null) return existing;
            var h = Progression.NewHero(c, classId);
            int avg = s.Roster.Count == 0 ? 0 : (int)s.Roster.Average(x => x.TotalXp);
            h.TotalXp = avg; // recruits arrive at the roster's average experience
            h.SkillPoints += Formulas.SkillPointsForLevelUp(1, Progression.Level(h));
            Progression.TrainTo(c, h); // trained to the level they arrive at (GG-T388)
            s.Roster.Add(h);
            Codex.UnlockHero(s, h); // GG-T929
            if (s.Party.Count < Progression.MaxPartySize) s.Party.Add(h.ClassId);
            Bonds.Joined(s, h.ClassId, route);
            return h;
        }

        /// <summary>Who a story JoinHero (GG-T118) brings into a save: <paramref name="preferred"/> if it's a hero the
        /// player doesn't have; else an unowned hero of <paramref name="role"/>; else one of a role the roster lacks
        /// (<see cref="StoryRoleOrder"/>); else anyone unowned — or null once every hero is owned. The fallbacks skip
        /// the Summon Circle's pool and the stages' guests so those stay worth having; deterministic (name order) so
        /// a beat's speaker and its joiner always agree. <paramref name="exclude"/> holds heroes already promised to
        /// earlier JoinHero effects of the same dialogue.</summary>
        public static string StoryPick(ContentDb c, SaveData s, string preferred, string role, ICollection<string> exclude = null)
        {
            bool Free(ClassDef d) => d != null && d.Team == Team.Hero && !d.IsCut && s.Hero(d.Id) == null && !Permadeath.IsLost(s, d.Id) && (exclude == null || !exclude.Contains(d.Id));
            if (preferred != null && Free(c.TryClass(preferred))) return preferred;
            var pool = c.Classes.Values.Where(d => Free(d) && d.Acquire != Acquire.Summon && d.Acquire != Acquire.Guest)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (role != null && pool.FirstOrDefault(d => d.Role == role) is ClassDef byRole) return byRole.Id;
            var have = new HashSet<string>(s.Roster.Select(h => c.TryClass(h.ClassId)?.Role).Where(r => r != null));
            foreach (var r in StoryRoleOrder)
                if (!have.Contains(r) && pool.FirstOrDefault(d => d.Role == r) is ClassDef missing) return missing.Id;
            return pool.FirstOrDefault()?.Id;
        }

        /// <summary>The order a JoinHero with no usable class or role fills gaps in the roster.</summary>
        public static readonly string[] StoryRoleOrder = { "Healer", "Tank", "Mage", "Support", "Striker", "Ranged" };
    }
}
