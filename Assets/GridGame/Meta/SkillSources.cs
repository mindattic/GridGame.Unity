using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Where a hero's locked skills come from (GG-T388, owner 2026-11-16: "skills start as few but they are purchased or
    /// discovered over time"). A new hero knows two moves (<see cref="Progression.StarterKit"/>); everything else it could
    /// ever use is bought (the trainer at Abilities › Learn, the skill tree at Talents) or discovered (Tomes from quests,
    /// bounties, theme clears, drops and chests). This reads all of it off the content so the Abilities screen's
    /// <b>Locked</b> tab can say, for each skill the hero lacks, exactly where to get it.
    /// </summary>
    public static class SkillSources
    {
        public sealed class Locked
        {
            public AbilityDef Ability;
            /// <summary>One line per way to get it, cheapest first ("Trainer · 100 G", "Talents › Warrior: Berserker Opener · 1 SP").</summary>
            public List<string> Sources = new List<string>();
            /// <summary>One of the hero's own default-kit moves (bought back at <see cref="Abilities.KitPrice"/>).</summary>
            public bool Kit;
        }

        /// <summary>Every skill this hero doesn't know yet that belongs to it: its kit moves first, then the rest of its class
        /// list, then its home tree's grants, then the battle passives a Tome could teach it. Each with its sources.</summary>
        public static List<Locked> For(ContentDb c, SaveData s, HeroSave h)
        {
            var result = new List<Locked>();
            var def = c?.TryClass(h?.ClassId);
            if (def == null || h.Domesticated) return result;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string id, bool kit)
            {
                var a = c.TryAbility(id);
                if (a == null || h.Known.Contains(a.Id) || PassiveLoadout.IsInnate(c, h, a.Id) || !seen.Add(a.Id)) return;
                result.Add(new Locked { Ability = a, Kit = kit, Sources = Of(c, s, h, a) });
            }
            foreach (var id in Progression.KitAbilities(def)) Add(id, true);
            foreach (var id in def.Abilities) Add(id, false);
            var home = Talents.HomeTree(c, h);
            if (home != null) foreach (var n in home.Nodes) if (!string.IsNullOrEmpty(n.Grants)) Add(n.Grants, false);
            foreach (var item in c.Items.Values.Where(i => i.Type == ItemType.Tome && i.TeachesAbility != null).OrderBy(i => i.Id, StringComparer.Ordinal))
            {
                var a = c.TryAbility(item.TeachesAbility);
                if (a != null && RewardCatalog.AllowedFor(a, def)) Add(a.Id, false);
            }
            return result;
        }

        /// <summary>Every way this hero can get <paramref name="a"/>, cheapest first; "Not taught anywhere yet" when none.</summary>
        public static List<string> Of(ContentDb c, SaveData s, HeroSave h, AbilityDef a)
        {
            var lines = new List<string>();
            if (Abilities.Learnable(c, h).Contains(a))
            {
                string needs = Abilities.NeedsText(Abilities.Unmet(c, s, h, a)); // GG-T592: locked until the stat points are in
                lines.Add($"Trainer (Abilities › Learn) · {Abilities.Price(c, s, h, a)} G" + (needs != null ? $" · {needs}" : ""));
            }
            foreach (var t in c.SkillTrees.Values)
                foreach (var n in t.Nodes)
                {
                    if (!string.Equals(n.Grants, a.Id, StringComparison.OrdinalIgnoreCase)) continue;
                    string gate = n.RequiredPointsInTree > 0 ? $", {Talents.Required(c, h, t, n)} pts in the tree first" : "";
                    lines.Add($"Talents › {t.Name}{(Talents.IsHome(c, h, t) ? "" : " (off-tree)")}: {n.Name ?? n.Id} · {Talents.Cost(c, h, t, n)} SP{gate}");
                }
            foreach (var tome in c.Items.Values.Where(i => i.Type == ItemType.Tome && string.Equals(i.TeachesAbility, a.Id, StringComparison.OrdinalIgnoreCase)))
                lines.Add($"{tome.Name}: {string.Join(", ", WhereFound(c, tome.Id).DefaultIfEmpty("not found anywhere yet"))}");
            if (lines.Count == 0) lines.Add("Not taught anywhere yet");
            return lines;
        }

        /// <summary>Where an item turns up, for the Locked tab's discovery lines: a quest or bounty reward, a theme's first
        /// clear, the Merchant, a foe's drops (up to three foes) or a chest.</summary>
        public static List<string> WhereFound(ContentDb c, string itemId)
        {
            var found = new List<string>();
            foreach (var q in c.QuestOrder) if (q.RewardItem == itemId) found.Add($"quest {q.Name}");
            foreach (var b in c.Bounties.Values.OrderBy(b => b.Id, StringComparer.Ordinal)) if (b.RewardItem == itemId) found.Add($"bounty {b.Name}");
            foreach (var t in c.Campaign.Themes) if (t.MilestoneReward == itemId) found.Add($"clearing {t.Name}");
            if (c.TryItem(itemId)?.SoldByVendor == true) found.Add("the Merchant");
            var tables = new HashSet<string>(c.DropTables.Values.Where(t => t.Entries.Any(e => e.Item == itemId)).Select(t => t.Id), StringComparer.OrdinalIgnoreCase);
            var foes = c.Classes.Values.Where(d => d.Team == Team.Enemy && d.DropTable != null && tables.Contains(d.DropTable))
                .Select(d => d.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (foes.Count > 0) found.Add("dropped by " + string.Join(", ", foes.Take(3)) + (foes.Count > 3 ? $" and {foes.Count - 3} more" : ""));
            foreach (var chest in c.Items.Values.Where(i => i.Type == ItemType.Chest && i.OpensTable != null && tables.Contains(i.OpensTable)).OrderBy(i => i.Id, StringComparer.Ordinal))
                found.Add($"a {chest.Name}");
            return found;
        }
    }
}
