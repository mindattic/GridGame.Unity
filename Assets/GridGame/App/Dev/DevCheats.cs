using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;

namespace GridGame.App.Dev
{
    /// <summary>
    /// GG-T1938: the dev automation's save cheats (devstage, devxp, devgive, devequipall, newgame npcs=1) as plain calls, so
    /// the automation server and the release build's Settings &gt; Developer section share one code path. Callers persist.
    /// </summary>
    public static class DevCheats
    {
        /// <summary>devstage (GG-T241): every stage before <paramref name="stageId"/> cleared, at camp (GG-T1786: travel has
        /// no per-region run to fast-forward). Null for an unknown stage, else the new highest cleared index.</summary>
        public static int? JumpToStage(ContentDb c, SaveData s, string stageId)
        {
            int idx = c.CampaignStages().FindIndex(x => x.Id == stageId);
            if (idx < 0) return null;
            s.HighestClearedStage = Math.Max(s.HighestClearedStage, idx - 1);
            TravelRules.MakeCamp(s);
            return s.HighestClearedStage;
        }

        /// <summary>Every campaign stage open: <see cref="JumpToStage"/> to the last one.</summary>
        public static void UnlockAllStages(ContentDb c, SaveData s)
        {
            var last = c.CampaignStages().LastOrDefault();
            if (last != null) JumpToStage(c, s, last.Id);
        }

        /// <summary>newgame npcs=1 (GG-T119): every Camp NPC met, so every station is open.</summary>
        public static void UnlockAllStations(SaveData s) => CampNpcs.JoinAll(s);

        /// <summary>devxp (GG-T241): each hero's experience set to the start of <paramref name="level"/> (raised or lowered).</summary>
        public static void SetLevel(IEnumerable<HeroSave> heroes, int level)
        {
            foreach (var h in heroes) { h.TotalXp = Formulas.TotalXpForLevel(level); h.Hp = 0; }
        }

        /// <summary>devgive: <paramref name="count"/> of an item into the bag; the bag's count after.</summary>
        public static int GiveItem(SaveData s, string itemId, int count)
        {
            s.AddItem(itemId, count);
            return s.Count(itemId);
        }

        /// <summary>devequipall (GG-T39): one <paramref name="itemId"/> granted and equipped in its own slot for every roster
        /// hero; <paramref name="wear"/> wears a weapon to that fraction of its ceiling (GG-T175). The number equipped.</summary>
        public static int EquipAll(ContentDb c, SaveData s, string itemId, float? wear = null)
        {
            var equipSlot = c.TryItem(itemId)?.Slot ?? EquipmentSlot.Weapon;
            int equipped = 0;
            foreach (var h in s.Roster)
            {
                s.AddItem(itemId, 1);
                if (!Equipment.Equip(c, s, h, equipSlot, itemId).Ok) continue;
                equipped++;
                if (wear != null && equipSlot == EquipmentSlot.Weapon)
                    h.WeaponDurability = Math.Max(1, (int)(Repair.EffectiveMax(c.Item(itemId), h.WeaponRepairs, h.WeaponMastery) * wear.Value));
            }
            return equipped;
        }
    }
}
