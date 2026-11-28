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
    // Using items at camp (potions on a hero, Tent on everyone)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Camp
    {
        public static bool UsableAtCamp(ItemDef i) => i != null && i.Type == ItemType.Consumable && i.BaseHealing > 0 && !i.Revives;

        public static bool HealsWholeParty(ItemDef i) => i != null && i.OutOfBattleOnly;

        public static Result Use(ContentDb c, SaveData s, string itemId, string heroId)
        {
            var item = c.TryItem(itemId);
            if (!UsableAtCamp(item)) return Result.Fail("Can't use that here");
            if (s.Count(itemId) < 1) return Result.Fail("None left");
            if (HealsWholeParty(item))
            {
                if (s.Roster.All(h => Inn.MissingHp(c, s, h) == 0)) return Result.Fail("Everyone is healthy");
                foreach (var h in s.Roster) h.Hp = 0;
                s.AddItem(itemId, -1);
                return Result.Success("The party is fully rested");
            }
            var hero = s.Hero(heroId);
            if (hero == null) return Result.Fail("Pick a hero");
            string heroName = c.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
            int max = Progression.Preview(c, hero).MaxHp;
            if (hero.Hp <= 0 || hero.Hp >= max) return Result.Fail($"{heroName} is already healthy");
            int healed = Math.Min(max - hero.Hp, item.BaseHealing);
            hero.Hp += healed;
            if (hero.Hp >= max) hero.Hp = 0;
            s.AddItem(itemId, -1);
            return Result.Success($"{heroName} recovers {healed} HP");
        }
    }
}
