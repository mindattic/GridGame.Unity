using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The Equip tab's "Auto-equip best" (GG-T1972, UX review E2): for each of the hero's eleven slots, the best piece in
    /// the bag the hero can wear (<see cref="Equipment.Fits(ContentDb, HeroSave, ItemDef, EquipmentSlot)"/>), when it scores
    /// above the worn one by the bots' own gear score (<see cref="MetaHeuristics.Score"/>). The plan is shown first and
    /// applied on confirm, one <see cref="Equipment.Equip"/> per slot.
    /// </summary>
    public static class AutoEquip
    {
        public readonly struct Pick
        {
            public readonly EquipmentSlot Slot;
            public readonly ItemDef Item;
            public Pick(EquipmentSlot slot, ItemDef item) { Slot = slot; Item = item; }
        }

        /// <summary>Weapon, the five armor pieces, then the five jewelry pieces: the Equip screen's order (§24.2).</summary>
        public static IEnumerable<EquipmentSlot> Slots =>
            new[] { EquipmentSlot.Weapon }.Concat(BattleEngine.ArmorSlots).Concat(Equipment.JewelrySlots);

        /// <summary>The upgrades Auto-equip would make, one per slot at most. A copy picked for one slot is not counted
        /// again for the next (two rings need two copies).</summary>
        public static List<Pick> Plan(ContentDb c, SaveData s, HeroSave h)
        {
            var picks = new List<Pick>();
            if (h == null || h.Domesticated) return picks;
            var taken = new Dictionary<string, int>();
            foreach (var slot in Slots)
            {
                float current = MetaHeuristics.Score(c.TryItem(Equipment.Equipped(h, slot)));
                var best = MetaHeuristics.BestFirst(s.Inventory.Where(kv => kv.Value > 0).Select(kv => c.TryItem(kv.Key))
                        .Where(i => Equipment.Fits(c, h, i, slot) && s.Count(i.Id) > (taken.TryGetValue(i.Id, out var n) ? n : 0)))
                    .FirstOrDefault();
                if (best == null || MetaHeuristics.Score(best) <= current) continue;
                taken[best.Id] = (taken.TryGetValue(best.Id, out var t) ? t : 0) + 1;
                picks.Add(new Pick(slot, best));
            }
            return picks;
        }

        /// <summary>New gear (GG-T2061): the first party hero with an upgrade in <see cref="Plan"/> the player hasn't been shown
        /// yet (<see cref="SaveData.SeenGear"/>), and that pick; null when there is none. It raises the Heroes tab's New badge
        /// and opens Heroes on Equip.</summary>
        public static (HeroSave Hero, Pick Pick)? FirstNew(ContentDb c, SaveData s)
        {
            foreach (var h in s.Party.Select(s.Hero).Where(h => h != null))
                foreach (var p in Plan(c, s, h))
                    if (!s.SeenGear.Contains(p.Item.Id)) return (h, p);
            return null;
        }

        /// <summary>Marks every upgrade in this hero's <see cref="Plan"/> seen, once the Equip tab has shown them.</summary>
        public static void MarkSeen(ContentDb c, SaveData s, HeroSave h)
        {
            foreach (var p in Plan(c, s, h)) s.SeenGear.Add(p.Item.Id);
        }

        /// <summary>Equips every piece in <see cref="Plan"/>; the swapped-out pieces go back to the bag.</summary>
        public static Result Apply(ContentDb c, SaveData s, HeroSave h)
        {
            var plan = Plan(c, s, h);
            if (plan.Count == 0) return Result.Fail("Already wearing the best gear in your bag");
            int done = 0;
            foreach (var p in plan)
                if (Equipment.Equip(c, s, h, p.Slot, p.Item.Id).Ok) done++;
            return done > 0 ? Result.Success(done == 1 ? "Equipped 1 upgrade" : $"Equipped {done} upgrades") : Result.Fail("Nothing could be equipped");
        }
    }
}
