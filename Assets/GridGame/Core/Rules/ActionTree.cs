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
        /// <summary>A move's identity across battles, the hero's bar entry (<see cref="ActionTree.Key"/>); null for a tab or folder.</summary>
        public string Key;
        /// <summary>A folder's default category (the one a newly learned move of that category joins), or null.</summary>
        public string Category;
        public readonly List<ActionNode> Children = new List<ActionNode>();
        public bool IsMove => Slot >= 0;
    }

    /// <summary>One move the tree places: its default tab and category folder, and its node.</summary>
    public sealed class ActionMove
    {
        public int Tab;
        public string Category;
        public ActionNode Node;
    }

    /// <summary>
    /// The one source of the Abilities sheet's tree (§9.0, GG-T1914): three tabs (Abilities, Spells, Consumables) of the
    /// hero's unlocked, filled bar slots. Skills and weapon attacks are Abilities, spells Spells, items Consumables. By
    /// default a tab folds its moves into category folders (the ability's damage type; content has no category field)
    /// only when it holds more than <see cref="FolderAbove"/> moves of two or more categories; otherwise it is flat.
    /// GG-T1917: the hero's saved <see cref="ActionLayout"/> (<see cref="Unit.Layout"/>) is laid over that default by
    /// <see cref="Arrange"/>, so the sheet never builds its own.
    /// </summary>
    public static class ActionTree
    {
        public static readonly string[] Tabs = { "Abilities", "Spells", "Consumables" };
        public const int FolderAbove = 3;

        public static List<ActionNode> For(BattleState s, Unit hero)
        {
            var moves = new List<ActionMove>();
            if (hero == null) return Arrange(moves, null);
            for (int i = 0; i < hero.Bar.Count && i < hero.UnlockedSlots; i++)
            {
                var sl = hero.Bar[i];
                if (sl.IsEmpty) continue;
                var a = BattleEngine.SlotAbility(s, hero, i);
                if (a != null) moves.Add(Move(s.Content, sl, i, a));
            }
            return Arrange(moves, hero.Layout);
        }

        /// <summary>The moves of a saved bar out of battle (GG-T2159, the Heroes > Abilities tree editor): the same tabs,
        /// categories, names and keys <see cref="For"/> gives in battle, from the bar entries and the content alone.</summary>
        public static List<ActionMove> MovesOf(ContentDb c, ClassDef caster, IReadOnlyList<string> bar, int unlocked)
        {
            var moves = new List<ActionMove>();
            for (int i = 0; bar != null && i < bar.Count && i < unlocked; i++)
            {
                var sl = Slot(bar[i]);
                if (sl.IsEmpty) continue;
                var a = sl.IsItem || sl.IsWeapon ? BattleRunner.AbilityForSlot(c, sl) : c.TryAbility(sl.AbilityId)?.ForCaster(caster);
                if (a != null) moves.Add(Move(c, sl, i, a));
            }
            return moves;
        }

        /// <summary>A saved bar entry as a slot (no charges): an ability id, "item:id[:n]" or "weapon:id".</summary>
        public static AbilitySlot Slot(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return new AbilitySlot();
            if (entry.StartsWith("item:", System.StringComparison.Ordinal)) return new AbilitySlot { ItemId = entry.Split(':')[1] };
            if (entry.StartsWith("weapon:", System.StringComparison.Ordinal)) return new AbilitySlot { WeaponId = entry.Substring(7) };
            return new AbilitySlot { AbilityId = entry };
        }

        /// <summary>One filled slot's move: items are Consumables, skills and weapon swaps Abilities, the rest Spells; its
        /// default category is the ability's damage type.</summary>
        private static ActionMove Move(ContentDb c, AbilitySlot sl, int slot, AbilityDef a)
        {
            int tab = sl.IsItem ? 2 : sl.IsWeapon || a.Kind == AbilityKind.Skill ? 0 : 1;
            return new ActionMove { Tab = tab, Category = a.DamageType.ToString(), Node = new ActionNode { Name = MoveName(c, sl, a), Slot = slot, Key = Key(sl) } };
        }

        /// <summary>The slot's bar entry, the same string a hero's saved bar holds: an ability id, "item:id" or "weapon:id".</summary>
        public static string Key(AbilitySlot sl) =>
            sl == null || sl.IsEmpty ? null : sl.IsItem ? "item:" + sl.ItemId : sl.IsWeapon ? "weapon:" + sl.WeaponId : sl.AbilityId;

        /// <summary>
        /// Lays <paramref name="moves"/> into the three tabs. Pinned moves come first, on top of the Abilities tab; then each
        /// of the layout's folders, in order, takes its saved moves (a key the hero no longer has is skipped); a move the layout
        /// never placed (newly learned) goes into the folder made from its category, else loose at the end of its tab. With no
        /// folders saved, the tabs keep the default fold. Empty folders are dropped unless <paramref name="keepEmpty"/> (the
        /// editor shows a folder the player just made).
        /// </summary>
        public static List<ActionNode> Arrange(List<ActionMove> moves, ActionLayout layout, bool keepEmpty = false)
        {
            var tabs = Tabs.Select(t => new ActionNode { Name = t }).ToList();
            var left = new List<ActionMove>(moves);
            ActionNode Take(string key, int tab)
            {
                var m = left.FirstOrDefault(x => x.Node.Key == key && (tab < 0 || x.Tab == tab));
                if (m == null) return null;
                left.Remove(m);
                return m.Node;
            }
            if (layout?.Pins != null)
                foreach (var key in layout.Pins)
                    if (Take(key, -1) is ActionNode pinned) tabs[0].Children.Add(pinned);
            if (layout?.Folders == null || layout.Folders.Count == 0)
            {
                Fold(tabs, left);
                return tabs;
            }
            var byCategory = new Dictionary<(int, string), ActionNode>();
            foreach (var f in layout.Folders)
            {
                if (f == null || f.Tab < 0 || f.Tab >= tabs.Count) continue;
                var into = tabs[f.Tab];
                if (!string.IsNullOrEmpty(f.Name))
                {
                    var folder = new ActionNode { Name = f.Name, Category = f.Category };
                    into.Children.Add(folder);
                    if (f.Category != null) byCategory.TryAdd((f.Tab, f.Category), folder);
                    into = folder;
                }
                if (f.Moves == null) continue;
                foreach (var key in f.Moves)
                    if (Take(key, f.Tab) is ActionNode n) into.Children.Add(n);
            }
            foreach (var m in left)
                (byCategory.TryGetValue((m.Tab, m.Category), out var folder) ? folder : tabs[m.Tab]).Children.Add(m.Node);
            if (!keepEmpty)
                foreach (var t in tabs) t.Children.RemoveAll(c => !c.IsMove && c.Children.Count == 0);
            return tabs;
        }

        private static void Fold(List<ActionNode> tabs, List<ActionMove> moves)
        {
            for (int t = 0; t < tabs.Count; t++)
            {
                var mine = moves.Where(m => m.Tab == t).ToList();
                bool fold = t != 2 && mine.Count > FolderAbove && mine.Select(m => m.Category).Distinct().Count() > 1;
                if (!fold) { tabs[t].Children.AddRange(mine.Select(m => m.Node)); continue; }
                foreach (var g in mine.GroupBy(m => m.Category))
                {
                    var folder = new ActionNode { Name = g.Key, Category = g.Key };
                    folder.Children.AddRange(g.Select(m => m.Node));
                    tabs[t].Children.Add(folder);
                }
            }
        }

        /// <summary>The move's full name: the item's or weapon's own name, else the ability's.</summary>
        public static string MoveName(BattleState s, Unit hero, int slot, AbilityDef a) => MoveName(s.Content, hero.Bar[slot], a);

        public static string MoveName(ContentDb c, AbilitySlot sl, AbilityDef a)
        {
            if (sl.IsItem) return c.TryItem(sl.ItemId)?.Name ?? a.Name;
            if (sl.IsWeapon) return c.TryItem(sl.WeaponId)?.Name ?? a.Name;
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
