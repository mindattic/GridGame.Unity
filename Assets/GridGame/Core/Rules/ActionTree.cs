using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Core.Rules
{
    /// <summary>One node of a hero's move tree (§9.0, GG-T1914): a tab, a category folder, or a move on a bar slot.</summary>
    public sealed class ActionNode
    {
        public string Name;
        /// <summary>The hero's bar slot this move uses, or −1 for a tab or folder.</summary>
        public int Slot = -1;
        public readonly List<ActionNode> Children = new List<ActionNode>();
        public bool IsMove => Slot >= 0;
    }

    /// <summary>
    /// The one source of the Abilities sheet's tree (§9.0, GG-T1914): three tabs (Abilities, Spells, Consumables) of the
    /// hero's unlocked, filled bar slots. Skills and weapon attacks are Abilities, spells Spells, items Consumables. By
    /// default a tab folds its moves into category folders (the ability's damage type; content has no category field)
    /// only when it holds more than <see cref="FolderAbove"/> moves of two or more categories; otherwise it is flat.
    /// GG-T1917 swaps in the hero's saved tree here, so the sheet never builds its own.
    /// </summary>
    public static class ActionTree
    {
        public static readonly string[] Tabs = { "Abilities", "Spells", "Consumables" };
        public const int FolderAbove = 3;

        public static List<ActionNode> For(BattleState s, Unit hero)
        {
            var tabs = Tabs.Select(t => new ActionNode { Name = t }).ToList();
            if (hero == null) return tabs;
            var moves = new List<(int tab, string category, ActionNode node)>();
            for (int i = 0; i < hero.Bar.Count && i < hero.UnlockedSlots; i++)
            {
                var sl = hero.Bar[i];
                if (sl.IsEmpty) continue;
                var a = BattleEngine.SlotAbility(s, hero, i);
                if (a == null) continue;
                int tab = sl.IsItem ? 2 : sl.IsWeapon || a.Kind == AbilityKind.Skill ? 0 : 1;
                moves.Add((tab, a.DamageType.ToString(), new ActionNode { Name = MoveName(s, hero, i, a), Slot = i }));
            }
            for (int t = 0; t < tabs.Count; t++)
            {
                var mine = moves.Where(m => m.tab == t).ToList();
                bool fold = t != 2 && mine.Count > FolderAbove && mine.Select(m => m.category).Distinct().Count() > 1;
                if (!fold) { tabs[t].Children.AddRange(mine.Select(m => m.node)); continue; }
                foreach (var g in mine.GroupBy(m => m.category))
                {
                    var folder = new ActionNode { Name = g.Key };
                    folder.Children.AddRange(g.Select(m => m.node));
                    tabs[t].Children.Add(folder);
                }
            }
            return tabs;
        }

        /// <summary>The move's full name: the item's or weapon's own name, else the ability's.</summary>
        public static string MoveName(BattleState s, Unit hero, int slot, AbilityDef a)
        {
            var sl = hero.Bar[slot];
            if (sl.IsItem) return s.Content.TryItem(sl.ItemId)?.Name ?? a.Name;
            if (sl.IsWeapon) return s.Content.TryItem(sl.WeaponId)?.Name ?? a.Name;
            return a.Name;
        }

        /// <summary>Every move under a node, folders opened.</summary>
        public static IEnumerable<ActionNode> Moves(ActionNode n) =>
            n.IsMove ? new[] { n } : n.Children.SelectMany(Moves);

        /// <summary>The path from a tab down to the move on <paramref name="slot"/>, or null when the tree has no such move.</summary>
        public static List<ActionNode> PathTo(List<ActionNode> tabs, int slot)
        {
            foreach (var t in tabs)
            {
                var path = new List<ActionNode> { t };
                if (Find(t, slot, path)) return path;
            }
            return null;
        }

        private static bool Find(ActionNode n, int slot, List<ActionNode> path)
        {
            foreach (var c in n.Children)
            {
                path.Add(c);
                if (c.Slot == slot || (!c.IsMove && Find(c, slot, path))) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }
    }
}
