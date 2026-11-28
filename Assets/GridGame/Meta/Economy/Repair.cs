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
    // Repair (§24.5, §25.2)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Repair
    {
        public const int RepairXp = 8;

        /// <summary>A first repair's price per durability point, as a share of the piece's price per point; each later
        /// repair of the same piece costs 1.6x the last. GG-T448: a camp bot that forges and keeps its gear equipped
        /// spends 97-98% of a campaign's gold on repairs regardless of this rate (measured at 0.30: 17,246 earned, 16,758
        /// repaired; at 0.15: 18,152 earned, 17,865 repaired) — cheaper repairs buy more repairs before a piece crosses
        /// <see cref="IsUneconomical"/> rather than freeing up gold, since CampRoutine repairs every worn piece back to
        /// full every visit by design (facilities and the Summon Circle are deliberately left to repair's leftovers).
        /// Halved from 0.30 regardless, since it still gives facilities/gear a little more room (150→275, 3,937→4,981
        /// gold/run) — but a player who keeps every piece of gear repaired will still see almost no gold left over for
        /// anything else. Flagged in RELEASE_SCOPE as a watch item for real play, not something this rate alone fixes.
        /// GG-T540 fixed it at the routine instead: it repairs only below <see cref="CampRoutine.RepairBelowFraction"/>,
        /// which brought repairs to 19-28% of main-campaign spend (BIBLE §25.0.11).</summary>
        public const float RepairRate = 0.15f;

        /// <summary>The piece's durability ceiling — raised by <see cref="BattleConfig.MasterworkDurabilityMultiplier"/> for a Masterwork copy (GG-T46).</summary>
        public static int EffectiveMax(ItemDef item, int repairs, float mastery = 1f) =>
            Math.Max(1, (int)Math.Round(item.Durability * (mastery > 1f ? BattleConfig.MasterworkDurabilityMultiplier : 1f)) - repairs);

        /// <summary>Repair price after the caravan's Armory Wagon discount.</summary>
        public static int Cost(ContentDb c, SaveData s, ItemDef item, int current, int repairs, float mastery = 1f)
        {
            int baseCost = Cost(item, current, repairs, mastery);
            if (baseCost <= 0) return 0;
            return Math.Max(1, (int)Math.Ceiling(baseCost * (1f - CaravanUpgrades.Value(c, s, "armory"))));
        }

        public static int Cost(ItemDef item, int current, int repairs, float mastery = 1f)
        {
            if (item == null || item.Durability <= 0) return 0;
            int missing = Math.Max(0, EffectiveMax(item, repairs, mastery) - current);
            if (missing == 0) return 0;
            float perPoint = (float)item.BaseCost / item.Durability;
            float mult = RepairRate * (float)Math.Pow(1.6f, Math.Max(0, repairs));
            return Math.Max(1, (int)Math.Ceiling(missing * perPoint * mult));
        }

        public static bool IsUneconomical(ItemDef item, int current, int repairs, float mastery = 1f) => Cost(item, current, repairs, mastery) >= item.BaseCost;

        /// <summary>GG-T544: worn below <see cref="CampRoutine.RepairBelowFraction"/> of its ceiling, the point where the camp
        /// routine repairs it. Above it a repair is "cheaper to wait": every repair, however small, raises the piece's next
        /// repair price 1.6x, so mending scratches compounds the price. The Repair tab's Repair all takes only these.</summary>
        public static bool Due(ItemDef item, int durability, int repairs, float mastery = 1f) =>
            item != null && durability < EffectiveMax(item, repairs, mastery) * CampRoutine.RepairBelowFraction;

        /// <summary>GG-T544: what <paramref name="wear"/> points of wear cost to repair at the piece's current repair count —
        /// the Repair tab's "per 10 wear" price hint (pass repairs + 1 for the price after this repair).</summary>
        public static int WearCost(ContentDb c, SaveData s, ItemDef item, int wear, int repairs, float mastery = 1f) =>
            Cost(c, s, item, Math.Max(0, EffectiveMax(item, repairs, mastery) - wear), repairs, mastery);

        /// <summary>GG-T544: every equipped piece Repair all would mend (worn and <see cref="Due"/>), most worn first.</summary>
        public static List<WornPiece> DuePieces(ContentDb c, SaveData s) =>
            Pieces(c, s).Where(p => p.Durability < p.Max && Due(p.Item, p.Durability, p.Repairs, p.Mastery)).OrderBy(p => p.Fraction).ToList();

        /// <summary>The piece's repair price now, after the Armory Wagon discount.</summary>
        public static int Cost(ContentDb c, SaveData s, WornPiece p) => Cost(c, s, p.Item, p.Durability, p.Repairs, p.Mastery);

        /// <summary>GG-T544: the Repair tab's Repair all — mends every <see cref="DuePieces"/> piece the purse covers, most
        /// worn first, and leaves pieces above the threshold for later (they can still be repaired by hand).</summary>
        public static Result RepairAll(ContentDb c, SaveData s)
        {
            var due = DuePieces(c, s);
            if (due.Count == 0) return Result.Fail("Nothing needs repair yet");
            int done = 0, spent = 0;
            foreach (var p in due)
            {
                int cost = Cost(c, s, p);
                if (cost <= 0 || cost > s.Gold) continue;
                var r = p.Slot == EquipmentSlot.Weapon ? RepairWeapon(c, s, p.Hero) : RepairArmor(c, s, p.Hero, p.Slot);
                if (r.Ok) { done++; spent += cost; }
            }
            if (done == 0) return Result.Fail("Not enough gold");
            return Result.Success(done == 1 ? $"Repaired 1 piece for {spent:N0} G" : $"Repaired {done} pieces for {spent:N0} G");
        }

        /// <summary>One equipped piece that wears (weapon or armor) and how worn it is.</summary>
        public sealed class WornPiece
        {
            public HeroSave Hero;
            public EquipmentSlot Slot;
            public ItemDef Item;
            public int Durability, Max, Repairs;
            public float Mastery = 1f;
            public float Fraction => Max <= 0 ? 1f : (float)Durability / Max;
            /// <summary>The Repair tab row's element name ("repair-knight-Weapon") — what the Blacksmith's camp tip points at.</summary>
            public string RowName => RowNameFor(Hero.ClassId, Slot);
        }

        public static string RowNameFor(string classId, EquipmentSlot slot) => $"repair-{classId}-{slot}";

        /// <summary>Every equipped weapon and armor piece across the roster that has durability.</summary>
        public static IEnumerable<WornPiece> Pieces(ContentDb c, SaveData s)
        {
            if (s == null) yield break;
            foreach (var h in s.Roster)
            {
                var w = c.TryItem(h.WeaponId);
                if (w != null && w.Durability > 0)
                    yield return new WornPiece { Hero = h, Slot = EquipmentSlot.Weapon, Item = w, Durability = h.WeaponDurability, Max = EffectiveMax(w, h.WeaponRepairs, h.WeaponMastery), Repairs = h.WeaponRepairs, Mastery = h.WeaponMastery };
                foreach (var slot in BattleEngine.ArmorSlots)
                {
                    if (!h.Armor.TryGetValue(slot, out var piece)) continue;
                    var a = c.TryItem(piece.ItemId);
                    if (a != null && a.Durability > 0)
                        yield return new WornPiece { Hero = h, Slot = slot, Item = a, Durability = piece.Durability, Max = EffectiveMax(a, piece.Repairs, piece.Mastery), Repairs = piece.Repairs, Mastery = piece.Mastery };
                }
            }
        }

        /// <summary>The most worn equipped piece (lowest durability fraction), or null when nobody wears gear that wears.</summary>
        public static WornPiece MostWorn(ContentDb c, SaveData s) => Pieces(c, s).OrderBy(p => p.Fraction).FirstOrDefault();


        public static Result RepairWeapon(ContentDb c, SaveData s, HeroSave h)
        {
            var item = c.TryItem(h.WeaponId);
            if (item == null || item.Durability <= 0) return Result.Fail("Nothing to repair");
            int cost = Cost(c, s, item, h.WeaponDurability, h.WeaponRepairs, h.WeaponMastery);
            if (cost == 0) return Result.Fail("Already in top shape");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            h.WeaponRepairs++;
            h.WeaponDurability = EffectiveMax(item, h.WeaponRepairs, h.WeaponMastery);
            StationProgress.Gain(s, StationKind.Blacksmith, RepairXp);
            return Result.Success($"Repaired {item.Name}");
        }

        /// <summary>Repairs one equipped armor piece (Head/Torso/Legs/Gloves/Boots) — each slot has its own
        /// durability/repair count, so this takes which slot instead of assuming the single old Armor slot (GG-T89).</summary>
        public static Result RepairArmor(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot)
        {
            if (!h.Armor.TryGetValue(slot, out var piece)) return Result.Fail("Nothing equipped");
            var item = c.TryItem(piece.ItemId);
            if (item == null || item.Durability <= 0) return Result.Fail("Nothing to repair");
            int cost = Cost(c, s, item, piece.Durability, piece.Repairs, piece.Mastery);
            if (cost == 0) return Result.Fail("Already in top shape");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            piece.Repairs++;
            piece.Durability = EffectiveMax(item, piece.Repairs, piece.Mastery);
            StationProgress.Gain(s, StationKind.Blacksmith, RepairXp);
            return Result.Success($"Repaired {item.Name}");
        }
    }
}
