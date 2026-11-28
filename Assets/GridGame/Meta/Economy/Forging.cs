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
    // The first forge (GG-T177, §25.0.6): once the Blacksmith's workshop opens, the Forge offers starter weapon
    // recipes that cost no gold and take only early drops; the guided forge equips the result on the hero it suits.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Forging
    {
        /// <summary>Save flag set by the guided first forge.</summary>
        public const string FirstForgeFlag = "onboard:first-forge";
        /// <summary>The camp-tip key of the Blacksmith's Forge tab (his Repair tab's tip is "blacksmith").</summary>
        public const string TipStation = "blacksmith-forge";

        public sealed class Plan
        {
            public RecipeDef Recipe;
            /// <summary>What it makes: the starter weapon (<see cref="First"/>) or the set piece (<see cref="FirstArmor"/>).</summary>
            public ItemDef Weapon;
            /// <summary>The party hero it suits: the one who would equip it on forging.</summary>
            public HeroSave Hero;
            /// <summary>Every input is in the bag (it costs no gold), so Craft works now.</summary>
            public bool Ready;
        }

        public static bool IsStarter(RecipeDef r) => r != null && r.Starter && r.Station == CraftStation.Forge && r.BaseItem == null;

        public static List<RecipeDef> StarterRecipes(ContentDb c) => Crafting.Recipes(c, CraftStation.Forge).Where(IsStarter).ToList();

        /// <summary>The first forge is behind this save: it has been made, or the save is a bot's or a New Game+.</summary>
        public static bool Done(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstForgeFlag);

        /// <summary>How well a weapon's stats match a hero's class: its stat bonuses weighted by the class's growth,
        /// per point — so a Wisdom lantern suits the Cleric and a Strength/Vitality club the Paladin.</summary>
        public static float Affinity(ContentDb c, HeroSave h, ItemDef item)
        {
            var g = c.TryClass(h.ClassId)?.Growth;
            var st = item?.Stats;
            if (g == null || st == null) return 0f;
            float total = st.Strength + st.Vitality + st.Agility + st.Speed + st.Intelligence + st.Wisdom + st.Luck;
            if (total <= 0f) return 0f;
            return (st.Strength * g.Strength + st.Vitality * g.Vitality + st.Agility * g.Agility + st.Speed * g.Speed
                    + st.Intelligence * g.Intelligence + st.Wisdom * g.Wisdom + st.Luck * g.Luck) / total;
        }

        /// <summary>The party hero a weapon suits: one who can wield it and would be better off with it than with what
        /// they hold now, the best stat match first (then the bigger gain, then party order); null when nobody gains.</summary>
        public static HeroSave SuitedHero(ContentDb c, SaveData s, ItemDef item)
        {
            if (c == null || s == null || item == null || item.Slot != EquipmentSlot.Weapon) return null;
            float score = GridGame.Core.Bot.MetaHeuristics.Score(item);
            return s.Party.Select(s.Hero)
                .Where(h => h != null && Equipment.Fits(c, h, item, EquipmentSlot.Weapon))
                .Select(h => (h, gain: score - GridGame.Core.Bot.MetaHeuristics.Score(c.TryItem(h.WeaponId))))
                .Where(x => x.gain > 0f)
                .OrderByDescending(x => Affinity(c, x.h, item)).ThenByDescending(x => x.gain)
                .Select(x => x.h).FirstOrDefault();
        }

        /// <summary>The starter recipe the Forge's camp tip walks and the Forge preselects, or null once the first forge
        /// is done: one that suits a party hero and can be carried, ready-to-forge ones first, then the best stat match,
        /// then the fewest materials still to find.</summary>
        public static Plan First(ContentDb c, SaveData s)
        {
            if (c == null || Done(s)) return null;
            return StarterRecipes(c)
                .Where(r => Crafting.CanMake(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && c.TryItem(r.Result) is ItemDef w && s.Count(w.Id) + r.ResultCount <= w.MaxStack)
                .Select(r => { var w = c.Item(r.Result); return new Plan { Recipe = r, Weapon = w, Hero = SuitedHero(c, s, w), Ready = Crafting.HasInputs(s, r) && s.Gold >= r.Gold }; })
                .Where(p => p.Hero != null)
                .OrderByDescending(p => p.Ready).ThenByDescending(p => Affinity(c, p.Hero, p.Weapon))
                .ThenBy(p => p.Recipe.Inputs.Sum(i => Math.Max(0, i.Count - s.Count(i.Item))))
                .FirstOrDefault();
        }

        /// <summary>Forging <paramref name="r"/> is the guided first forge: it is a starter recipe and the first forge is still ahead.</summary>
        public static bool IsGuided(SaveData s, RecipeDef r) => IsStarter(r) && !Done(s);

        /// <summary>The guided first forge: makes the starter weapon and equips it on the hero it suits (when one gains
        /// from it), then marks the first forge done. Anything else is an ordinary <see cref="Crafting.Craft"/>.</summary>
        public static Result Forge(ContentDb c, SaveData s, RecipeDef r, bool lucky = false)
        {
            if (IsGuidedArmor(c, s, r)) return ForgeFirstArmor(c, s, r, lucky);
            if (!IsGuided(s, r)) return Crafting.Craft(c, s, r, lucky: lucky);
            var weapon = c.TryItem(r.Result);
            var hero = SuitedHero(c, s, weapon);
            var made = Crafting.Craft(c, s, r, lucky: lucky);
            if (!made.Ok) return made;
            s.Flags.Add(FirstForgeFlag);
            if (hero == null) return made;
            var equipped = Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, weapon.Id);
            string name = c.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
            return equipped.Ok ? Result.Success($"{made.Message}: the {name} equips it") : made;
        }

        // ── Onboarding step 5: forge armor (GG-T178, §25.0.7) ─────────────────────────────────────────────
        // The armor-set recipes (GG-T182) open at the Forge once the first weapon is made; the guided first armor
        // forges one set piece and equips it on the hero it suits.

        /// <summary>Save flag set by the guided first armor forge.</summary>
        public const string FirstArmorFlag = "onboard:first-armor";
        /// <summary>The camp-tip key of the Forge tab once the first weapon is made and the first armor is still ahead.</summary>
        public const string ArmorTipStation = "blacksmith-armor";
        /// <summary>Why a set piece can't be forged yet.</summary>
        public const string ArmorLockedHint = "Armor recipes open after your first forge";

        /// <summary>A Forge recipe for a piece of a named armor set (GG-T182).</summary>
        public static bool IsSetArmor(ContentDb c, RecipeDef r) =>
            c != null && r != null && r.Station == CraftStation.Forge && r.BaseItem == null && c.TryArmorSetOf(r.Result) != null;

        /// <summary>The armor-set recipes are open at the Forge: always, except for a set piece before the first forge.</summary>
        public static bool ArmorOpen(ContentDb c, SaveData s, RecipeDef r) => !IsSetArmor(c, r) || Done(s);

        public static List<RecipeDef> SetArmorRecipes(ContentDb c) => Crafting.Recipes(c, CraftStation.Forge).Where(r => IsSetArmor(c, r)).ToList();

        /// <summary>The first armor is behind this save: it has been forged, or the save is a bot's or a New Game+.</summary>
        public static bool ArmorDone(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstArmorFlag);

        /// <summary>The first armor step is on: the first weapon is made and the first armor isn't.</summary>
        public static bool ArmorStep(SaveData s) => Done(s) && !ArmorDone(s);

        /// <summary>The party hero an armor piece suits: one who is proficient in its weight and better off with it than
        /// with what they wear in that slot, the best stat match first (then the bigger gain, then party order).</summary>
        public static HeroSave SuitedWearer(ContentDb c, SaveData s, ItemDef item)
        {
            if (c == null || s == null || item == null || Array.IndexOf(BattleEngine.ArmorSlots, item.Slot) < 0) return null;
            float score = GridGame.Core.Bot.MetaHeuristics.Score(item);
            return s.Party.Select(s.Hero)
                .Where(h => h != null && Equipment.Fits(c, h, item, item.Slot))
                .Select(h => (h, gain: score - GridGame.Core.Bot.MetaHeuristics.Score(c.TryItem(Equipment.Equipped(h, item.Slot)))))
                .Where(x => x.gain > 0f)
                .OrderByDescending(x => Affinity(c, x.h, item)).ThenByDescending(x => x.gain)
                .Select(x => x.h).FirstOrDefault();
        }

        /// <summary>The set piece the Forge's armor tip walks and the Forge preselects, or null outside the armor step:
        /// an open Apprentice set recipe that suits a party hero and can be carried, ready-to-forge ones first, then the
        /// best stat match, then the fewest materials still to find, then the cheapest.</summary>
        public static Plan FirstArmor(ContentDb c, SaveData s)
        {
            if (c == null || !ArmorStep(s)) return null;
            return SetArmorRecipes(c)
                .Where(r => r.Tier <= 1 && Crafting.CanMake(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && c.TryItem(r.Result) is ItemDef a && s.Count(a.Id) + r.ResultCount <= a.MaxStack)
                .Select(r => { var a = c.Item(r.Result); return new Plan { Recipe = r, Weapon = a, Hero = SuitedWearer(c, s, a), Ready = Crafting.HasInputs(s, r) && s.Gold >= r.Gold }; })
                .Where(p => p.Hero != null)
                .OrderByDescending(p => p.Ready).ThenByDescending(p => Affinity(c, p.Hero, p.Weapon))
                .ThenBy(p => p.Recipe.Inputs.Sum(i => Math.Max(0, i.Count - s.Count(i.Item)))).ThenBy(p => p.Recipe.Gold)
                .FirstOrDefault();
        }

        /// <summary>Forging <paramref name="r"/> is the guided first armor: a set piece, during the armor step.</summary>
        public static bool IsGuidedArmor(ContentDb c, SaveData s, RecipeDef r) => IsSetArmor(c, r) && ArmorStep(s);

        /// <summary>The guided first armor: makes the set piece and equips it on the hero it suits (when one gains from
        /// it), then marks the first armor done.</summary>
        private static Result ForgeFirstArmor(ContentDb c, SaveData s, RecipeDef r, bool lucky)
        {
            var piece = c.TryItem(r.Result);
            var hero = SuitedWearer(c, s, piece);
            var made = Crafting.Craft(c, s, r, lucky: lucky);
            if (!made.Ok) return made;
            s.Flags.Add(FirstArmorFlag);
            if (hero == null) return made;
            var equipped = Equipment.Equip(c, s, hero, piece.Slot, piece.Id);
            string name = c.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
            return equipped.Ok ? Result.Success($"{made.Message}: the {name} wears it") : made;
        }
    }
}
