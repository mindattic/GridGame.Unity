using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The per-hero Abilities tree editor (GG-T1917, GG-T2159): this hero's battle Abilities sheet, out of battle, in a sheet.
    /// Pick a folder or a move (pressed in), then move it up or down, file the move in a folder, pin it, or rename, make and
    /// remove folders. Every change starts from a capture of the arranged tree (<see cref="ActionLayout.Capture"/>) and is
    /// saved on the hero (<see cref="HeroSave.Actions"/>) at once.
    /// </summary>
    internal sealed partial class AbilitiesTab
    {
        private VisualElement treeSheet;
        private int treeTab;
        /// <summary>The picked move's key, or the picked folder's name when <see cref="pickedFolder"/>; null when nothing is.</summary>
        private string picked;
        private bool pickedFolder;
        /// <summary>Where the picked move goes on Move: a folder of its tab, or "" for the tab's top level; null until picked.</summary>
        private string target;

        private List<ActionNode> Tree(HeroSave h) =>
            ActionTree.Arrange(ActionTree.MovesOf(C, C.TryClass(h.ClassId), h.Bar, Progression.UnlockedSlots(S)), h.Actions, keepEmpty: true);

        private void OpenTree()
        {
            CloseTree(false);
            treeTab = 0;
            picked = target = null;
            VisualElement s = null;
            s = treeSheet = Ui.Sheet("abtree-sheet", () => { if (treeSheet == s) treeSheet = null; }, full: true);
            RenderTree();
        }

        private void CloseTree(bool animate)
        {
            var s = treeSheet;
            treeSheet = null;
            if (s != null) Ui.CloseSheet(s, animate);
        }

        private void EditTree(Action<ActionLayout> edit)
        {
            var h = Hero;
            var l = ActionLayout.Capture(Tree(h), h.Actions?.Pins);
            edit(l);
            h.Actions = l;
            App.Persist();
            App.Audio.Sfx("ui.select");
            RenderTree();
        }

        private void RenderTree()
        {
            var sh = treeSheet;
            var h = Hero;
            if (sh == null || sh.parent?.parent == null || h == null) { CloseTree(false); return; }
            float scroll = sh.Q<ScrollView>("abtree-list")?.scrollOffset.y ?? 0f;
            sh.Clear();
            var tree = Tree(h);
            var pins = h.Actions?.Pins ?? new List<string>();

            var head = Ui.El("row", sh);
            head.name = "abtree-header";
            head.style.alignItems = Align.Center;
            head.style.marginBottom = Space.S8;
            Ui.Margin(Ui.Thumb(h.ClassId, head, Size.Avatar), right: Space.S16);
            var who = Ui.El("col grow", head);
            who.style.flexShrink = 1;
            who.style.minWidth = 0;
            Ui.Name(C.TryClass(h.ClassId)?.Name ?? h.ClassId, who);
            Ui.Caption("Their Abilities sheet in battle", who, "dim").style.whiteSpace = WhiteSpace.Normal;
            Ui.IconButton(RowKind.Close, () => CloseTree(true), head).name = "abtree-close";

            Ui.Tabs(ActionTree.Tabs, treeTab, i => { treeTab = i; picked = target = null; RenderTree(); }, sh).name = "abtree-tabs";
            var tab = tree[treeTab];
            TreeTools(sh, tab, pins);

            var list = Ui.List(sh);
            list.name = "abtree-list";
            list.style.flexGrow = 1;
            if (tab.Children.Count == 0) Ui.Empty($"No {tab.Name.ToLowerInvariant()} on the bar yet.", list);
            foreach (var c in tab.Children)
            {
                if (c.IsMove) { TreeMove(list, h, c, pins.Contains(c.Key), false); continue; }
                TreeFolder(list, c);
                foreach (var m in c.Children) TreeMove(list, h, m, false, true);
            }
            list.schedule.Execute(() => list.scrollOffset = new Vector2(0f, scroll));
        }

        /// <summary>The actions for what is picked: New folder always; a folder's Move up / down, Rename and Remove; a move's
        /// Move up / down, Pin, and where to file it (a folder chip or Top level, then Move).</summary>
        private void TreeTools(VisualElement sh, ActionNode tab, List<string> pins)
        {
            var tools = Ui.Actions(sh);
            tools.name = "abtree-tools";
            tools.style.flexWrap = Wrap.Wrap;
            RowIcon.IconButton("New folder", RowKind.New, () => NameFolder(null), "btn--small", tools).name = "abtree-new-folder";

            // The tab's top level, as ActionLayout counts it: every folder and loose move, pins left out.
            var root = tab.Children.Where(c => !(c.IsMove && pins.Contains(c.Key))).ToList();
            var folder = pickedFolder ? root.FirstOrDefault(c => !c.IsMove && c.Name == picked) : null;
            var move = pickedFolder ? null : ActionTree.Moves(tab).FirstOrDefault(m => m.Key == picked);
            if (folder == null && move == null)
            {
                picked = target = null;
                Ui.Note("Pick a folder or a move to arrange it.", sh).name = "abtree-hint";
                return;
            }
            int t = treeTab;
            if (folder != null)
            {
                int r = root.IndexOf(folder);
                Shift(tools, r > 0, r + 1 < root.Count, d => EditTree(l => l.MoveRoot(t, r, r + d)));
                RowIcon.IconButton("Rename", RowKind.Text, () => NameFolder(folder.Name), "btn--small", tools).name = "abtree-rename";
                string name = folder.Name;
                RowIcon.IconButton("Remove folder", RowKind.Close, () => { picked = null; EditTree(l => l.RemoveFolder(t, name)); }, "btn--small", tools, confirmTap: true).name = "abtree-remove";
                Ui.Note(folder.Children.Count == 0 ? "Removing it leaves nothing behind." : "Removing it keeps its moves here, loose.", sh).name = "abtree-hint";
                return;
            }

            string key = move.Key;
            bool pinned = pins.Contains(key);
            var home = root.FirstOrDefault(c => !c.IsMove && c.Children.Contains(move));
            if (pinned)
            {
                int p = pins.IndexOf(key);
                Shift(tools, p > 0, p + 1 < pins.Count, d => EditTree(l =>
                {
                    int i = l.Pins.IndexOf(key);
                    l.Pins.RemoveAt(i);
                    l.Pins.Insert(Math.Max(0, Math.Min(l.Pins.Count, i + d)), key);
                }));
            }
            else if (home != null)
            {
                int i = home.Children.IndexOf(move);
                string into = home.Name;
                Shift(tools, i > 0, i + 1 < home.Children.Count, d => EditTree(l => l.Place(key, t, into, i + d)));
            }
            else
            {
                int r = root.IndexOf(move);
                Shift(tools, r > 0, r + 1 < root.Count, d => EditTree(l => l.MoveRoot(t, r, r + d)));
            }
            Ui.Chip(pinned ? "Pinned" : "Pin", tools, onClick: () => EditTree(l => l.Pin(key, !pinned)), selected: pinned, name: "abtree-pin");
            if (pinned) { Ui.Note("Pinned moves sit at the top of Abilities. Unpin it to file it in a folder.", sh).name = "abtree-hint"; return; }

            // Select then confirm: a chip picks where it goes (pressed in), Move files it there.
            string at = home?.Name ?? "";
            var dest = Ui.El("row", sh);
            dest.name = "abtree-dest";
            dest.style.flexWrap = Wrap.Wrap;
            dest.style.alignItems = Align.Center;
            Ui.Caption("File in", dest, "dim").style.marginRight = Space.S16;
            void Dest(string label, string where, string id)
            {
                var chip = Ui.Chip(label, dest, onClick: () => { target = where == at ? null : where; RenderTree(); },
                    selected: target == where, tone: where == at ? "dim" : null, name: id);
                Ui.Margin(chip, right: Space.S8, bottom: Space.S8);
            }
            Dest("Top level", "", "abtree-to-top");
            foreach (var f in root.Where(c => !c.IsMove)) Dest(f.Name, f.Name, "abtree-to-" + Slug(f.Name));
            var go = RowIcon.IconButton("Move", RowKind.Confirm, () =>
            {
                string where = target;
                target = null;
                EditTree(l => l.Place(key, t, where.Length == 0 ? null : where, int.MaxValue));
            }, "btn--small btn--primary", dest, confirmTap: false);
            go.name = "abtree-move";
            Ui.SetEnabled(go, target != null && target != at);
        }

        /// <summary>Move up and Move down, the bar's arrows turned upright; <paramref name="by"/> gets −1 or +1.</summary>
        private static void Shift(VisualElement tools, bool up, bool down, Action<int> by)
        {
            var u = RowIcon.IconButton("Move up", RowKind.Back, () => by(-1), "btn--small", tools);
            u.name = "abtree-up";
            u.Q(className: RowIcon.Class).style.rotate = new Rotate(90);
            Ui.SetEnabled(u, up);
            var d = RowIcon.IconButton("Move down", RowKind.Enter, () => by(1), "btn--small", tools);
            d.name = "abtree-down";
            d.Q(className: RowIcon.Class).style.rotate = new Rotate(90);
            Ui.SetEnabled(d, down);
        }

        private VisualElement TreeRow(VisualElement list, bool selected, Action onTap)
        {
            var row = Ui.El("row card card-tap" + (selected ? " tap--selected" : ""), list);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = Kit.Row;
            row.style.marginBottom = Space.S8;
            Ui.Tappable(row, onTap);
            return row;
        }

        /// <summary>A folder: its full name (wrapping, never cut), and how many moves it holds.</summary>
        private void TreeFolder(VisualElement list, ActionNode f)
        {
            var row = TreeRow(list, pickedFolder && picked == f.Name, () => Pick(f.Name, true));
            row.name = "abtree-folder-" + Slug(f.Name);
            Ui.Margin(row.KindIcon(RowKind.Menu), right: Space.S16);
            var name = Ui.Text(f.Name, "kit-text grow", row);
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.flexShrink = 1;
            Ui.Caption(f.Children.Count == 1 ? "1 move" : $"{f.Children.Count} moves", row, "dim");
        }

        /// <summary>A move, under its folder when it has one.</summary>
        private void TreeMove(VisualElement list, HeroSave h, ActionNode m, bool pinned, bool inFolder)
        {
            var row = TreeRow(list, !pickedFolder && picked == m.Key, () => Pick(m.Key, false));
            row.name = "abtree-move-" + m.Slot;
            if (inFolder) Ui.Margin(row, left: Space.S48);
            Ui.Margin(row.KindIcon(RowKind.Ability, EntryIcon(h.Bar[m.Slot])), right: Space.S16);
            var name = Ui.Text(m.Name, "kit-text grow", row);
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.flexShrink = 1;
            if (pinned) Ui.Caption("Pinned", row, "gold");
        }

        /// <summary>A tap picks a row (pressed in); a tap on the picked row lets it go.</summary>
        private void Pick(string id, bool folder)
        {
            bool same = picked == id && pickedFolder == folder;
            picked = same ? null : id;
            pickedFolder = folder;
            target = null;
            RenderTree();
        }

        /// <summary>The name window: a new folder (<paramref name="from"/> null) or a rename. The answer stays off until the
        /// name passes <see cref="ActionLayout.CheckName"/> (1 to <see cref="ActionLayout.MaxName"/> letters, not already a
        /// folder of this tab).</summary>
        private void NameFolder(string from)
        {
            int t = treeTab;
            var layout = ActionLayout.Capture(Tree(Hero), Hero.Actions?.Pins);
            VisualElement veil = null;
            Action cancel = () => veil.RemoveFromHierarchy();
            var w = Ui.Modal(from == null ? "New folder" : "Rename folder", cancel, "abtree-name-window");
            veil = w.parent;
            var field = Ui.Input("abtree-name", from, w, ActionLayout.MaxName);
            var note = Ui.Note("", w);
            note.name = "abtree-name-note";
            note.style.whiteSpace = WhiteSpace.Normal;
            var row = Ui.ModalActions(w);
            RowIcon.IconButton("Cancel", RowKind.Close, cancel, null, row).name = "abtree-name-cancel";
            var ok = RowIcon.IconButton(from == null ? "Make" : "Rename", RowKind.Confirm, () =>
            {
                var name = layout.CheckName(t, field.value, from);
                if (name == null) return;
                veil.RemoveFromHierarchy();
                if (from == null) { pickedFolder = true; picked = name; }
                else if (pickedFolder && picked == from) picked = name;
                EditTree(l => { if (from == null) l.AddFolder(t, name); else l.Rename(t, from, name); });
            }, "btn--primary", row, confirmTap: false);
            ok.name = "abtree-name-ok";
            void Check()
            {
                var name = layout.CheckName(t, field.value, from);
                Ui.SetEnabled(ok, name != null);
                note.text = name != null ? $"{name.Length} of {ActionLayout.MaxName} letters"
                    : string.IsNullOrWhiteSpace(field.value) ? $"Give it a name, up to {ActionLayout.MaxName} letters."
                    : $"{ActionTree.Tabs[t]} already has a folder called {field.value.Trim()}.";
            }
            field.RegisterValueChangedCallback(_ => Check());
            Check();
            field.schedule.Execute(() => field.Focus());
        }

        private static string Slug(string name) => name.Trim().ToLowerInvariant().Replace(' ', '-');
    }
}
