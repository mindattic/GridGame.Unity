using System;
using System.Collections.Generic;
using System.Linq;

namespace GridGame.Core.Rules
{
    /// <summary>One group of a hero's saved move tree (GG-T1917): a named folder, or (no name) a run of loose moves at the
    /// top level of its tab. <see cref="Category"/> is the default category it was made from, if any.</summary>
    public sealed class ActionFolder
    {
        public int Tab;
        public string Name;
        public string Category;
        public List<string> Moves = new List<string>();
    }

    /// <summary>
    /// A hero's own arrangement of the Abilities sheet (GG-T1917), saved on the hero and laid over the default tree by
    /// <see cref="ActionTree.Arrange"/>: folders (renamed, made, ordered), the moves in each, and pins. Moves are bar entries
    /// (<see cref="ActionTree.Key"/>), so a move the hero no longer has is simply skipped. An empty layout is the default tree.
    /// The edits work on a captured tree: the editor calls <see cref="Capture"/> on the arranged tree first.
    /// </summary>
    public sealed class ActionLayout
    {
        /// <summary>The longest folder name, so a name always fits its row and crumb without truncating.</summary>
        public const int MaxName = 20;

        public List<ActionFolder> Folders = new List<ActionFolder>();
        public List<string> Pins = new List<string>();

        public bool IsDefault() => (Folders == null || Folders.Count == 0) && (Pins == null || Pins.Count == 0);

        /// <summary>The layout that reproduces <paramref name="tabs"/> (an arranged tree, empty folders kept). Moves in
        /// <paramref name="pins"/> are kept as pins and left out of the folders.</summary>
        public static ActionLayout Capture(List<ActionNode> tabs, IEnumerable<string> pins = null)
        {
            var l = new ActionLayout();
            if (pins != null) l.Pins.AddRange(pins.Where(p => p != null).Distinct());
            for (int t = 0; t < tabs.Count; t++)
            {
                ActionFolder loose = null;
                foreach (var c in tabs[t].Children)
                {
                    if (c.IsMove)
                    {
                        if (c.Key == null || l.Pins.Contains(c.Key)) continue;
                        if (loose == null) l.Folders.Add(loose = new ActionFolder { Tab = t });
                        loose.Moves.Add(c.Key);
                        continue;
                    }
                    loose = null;
                    l.Folders.Add(new ActionFolder
                    {
                        Tab = t, Name = c.Name, Category = c.Category,
                        Moves = ActionTree.Moves(c).Select(m => m.Key).Where(k => k != null).ToList(),
                    });
                }
            }
            return l;
        }

        /// <summary>The name trimmed, or null when it is empty, longer than <see cref="MaxName"/>, or already a folder in
        /// <paramref name="tab"/> (other than <paramref name="except"/>).</summary>
        public string CheckName(int tab, string name, string except = null)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > MaxName) return null;
            bool taken = Folders.Any(f => f.Tab == tab && f.Name != null && f.Name != except
                && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            return taken ? null : name;
        }

        public bool Rename(int tab, string from, string to)
        {
            var f = Folder(tab, from);
            var name = f == null ? null : CheckName(tab, to, from);
            if (name == null) return false;
            f.Name = name;
            return true;
        }

        /// <summary>Adds an empty folder at the end of <paramref name="tab"/>.</summary>
        public bool AddFolder(int tab, string name)
        {
            name = CheckName(tab, name);
            if (name == null || tab < 0 || tab >= ActionTree.Tabs.Length) return false;
            Folders.Add(new ActionFolder { Tab = tab, Name = name });
            return true;
        }

        /// <summary>Removes a folder; its moves stay where it was, loose.</summary>
        public void RemoveFolder(int tab, string name)
        {
            var f = Folder(tab, name);
            if (f == null) return;
            f.Name = null;
            f.Category = null;
            Regroup(tab, Root(tab));
        }

        /// <summary>Pins or unpins a move: a pinned move sits on top of the Abilities tab, in pin order.</summary>
        public void Pin(string key, bool on)
        {
            if (key == null) return;
            Pins.Remove(key);
            if (on) Pins.Add(key);
        }

        /// <summary>Moves a move into <paramref name="folder"/> of <paramref name="tab"/> (null = the tab's top level) at
        /// <paramref name="index"/> (among that folder's moves, or among the top level's folders and loose moves).</summary>
        public void Place(string key, int tab, string folder, int index)
        {
            if (key == null) return;
            var into = Folder(tab, folder);
            if (folder != null && into == null) return;
            foreach (var f in Folders) f.Moves.Remove(key);
            if (into != null)
            {
                into.Moves.Insert(Clamp(index, into.Moves.Count), key);
                Regroup(tab, Root(tab));
                return;
            }
            var root = Root(tab);
            root.Insert(Clamp(index, root.Count), key);
            Regroup(tab, root);
        }

        /// <summary>Reorders the top level of <paramref name="tab"/>: the item at <paramref name="from"/> goes to <paramref name="to"/>.</summary>
        public void MoveRoot(int tab, int from, int to)
        {
            var root = Root(tab);
            if (from < 0 || from >= root.Count) return;
            var item = root[from];
            root.RemoveAt(from);
            root.Insert(Clamp(to, root.Count), item);
            Regroup(tab, root);
        }

        /// <summary>Drops null entries, groups of no tab and empty names, and cuts an over-long name (a hand-edited save).</summary>
        public void Sanitize()
        {
            Folders ??= new List<ActionFolder>();
            Pins ??= new List<string>();
            Folders.RemoveAll(f => f == null || f.Tab < 0 || f.Tab >= ActionTree.Tabs.Length);
            foreach (var f in Folders)
            {
                f.Moves ??= new List<string>();
                f.Moves.RemoveAll(k => k == null);
                f.Name = string.IsNullOrWhiteSpace(f.Name) ? null : f.Name.Trim();
                if (f.Name != null && f.Name.Length > MaxName) f.Name = f.Name.Substring(0, MaxName).TrimEnd();
            }
            Pins.RemoveAll(k => k == null);
        }

        private ActionFolder Folder(int tab, string name) =>
            name == null ? null : Folders.FirstOrDefault(f => f.Tab == tab && f.Name == name);

        private static int Clamp(int i, int count) => i < 0 ? 0 : i > count ? count : i;

        /// <summary>The tab's top level: each named folder, and each loose move key, in order.</summary>
        private List<object> Root(int tab)
        {
            var items = new List<object>();
            foreach (var f in Folders.Where(f => f.Tab == tab))
                if (f.Name != null) items.Add(f);
                else items.AddRange(f.Moves);
            return items;
        }

        /// <summary>Rewrites the tab's groups from its top level, each run of loose moves joined into one unnamed group.</summary>
        private void Regroup(int tab, List<object> root)
        {
            int at = Folders.FindIndex(f => f.Tab == tab);
            Folders.RemoveAll(f => f.Tab == tab);
            var groups = new List<ActionFolder>();
            ActionFolder loose = null;
            foreach (var item in root)
            {
                if (item is ActionFolder f) { groups.Add(f); loose = null; continue; }
                if (loose == null) groups.Add(loose = new ActionFolder { Tab = tab });
                loose.Moves.Add((string)item);
            }
            Folders.InsertRange(at < 0 ? Folders.Count : Math.Min(at, Folders.Count), groups);
        }
    }
}
