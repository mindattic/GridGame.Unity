using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T925: the Codex, the in-game Lore / Encyclopedia (GG-T922) a player reads to catch up. One tab per
    /// <see cref="CodexKinds"/> kind, each led by its kind icon and carrying its entry count; an entry is listed once it is
    /// unlocked (<see cref="Codex"/>), and one codex.json names but the save hasn't reached shows as a "???" silhouette.
    /// An unlocked, unread entry wears the New badge until it is opened. Opening one shows the reading view: its title,
    /// its body (glossary terms tappable, <see cref="GlossaryText"/>) and its related entries as links. The Foes tab is
    /// the Bestiary's own grid and detail (<see cref="BestiaryScreen.BuildGrid"/>), not a copy of it.
    /// </summary>
    public sealed class CodexScreen : HubPage
    {
        private readonly struct Tab
        {
            public readonly string Kind, Label;
            public readonly RowKind Icon;
            /// <summary>Open-ended kinds (items, abilities, keywords) list only what has been found: codex.json doesn't
            /// name every one, so they have no total and no silhouettes.</summary>
            public readonly bool OpenEnded;
            public Tab(string kind, string label, RowKind icon, bool openEnded = false) { Kind = kind; Label = label; Icon = icon; OpenEnded = openEnded; }
        }

        private static readonly Tab[] Tabs =
        {
            new Tab(CodexKinds.Place, "Places", RowKind.Place),
            new Tab(CodexKinds.Faction, "Factions", RowKind.Faction),
            new Tab(CodexKinds.Character, "Characters", RowKind.Hero),
            new Tab(CodexKinds.Foe, "Foes", RowKind.Bestiary),
            new Tab(CodexKinds.Item, "Items", RowKind.Item, true),
            new Tab(CodexKinds.Ability, "Abilities", RowKind.Ability, true),
            new Tab(CodexKinds.Keyword, "Keywords", RowKind.Codex, true),
            new Tab(CodexKinds.Story, "Story", RowKind.Lore),
        };

        /// <summary>The silhouette tint on a locked entry's art (the Bestiary's unseen tint).</summary>
        private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.92f);

        private int tab;
        private string selected;
        private bool synced;
        private readonly BestiaryScreen bestiary = new BestiaryScreen();
        private VisualElement[] tabIcons;
        private readonly Dictionary<string, VisualElement> rowIcons = new Dictionary<string, VisualElement>();

        public override string Id => "codex";
        protected override string Title => "Codex";

        protected override void BuildBody(VisualElement body)
        {
            // Catch up on anything the save already shows before the first look (Codex.Sync is idempotent).
            if (!synced)
            {
                synced = true;
                if (Codex.Sync(C, S) > 0) App.Persist();
            }
            TabBar(body);
            var t = Tabs[tab];
            if (t.Kind == CodexKinds.Foe)
            {
                bestiary.BuildGrid(body, id => { if (Codex.MarkRead(S, CodexKinds.Foe, id)) { App.Persist(); UpdateTabBadge(tab); } },
                    id => Codex.IsNew(S, CodexKinds.Foe, id));
                return;
            }

            var entries = Entries(t);
            int open = entries.Count(e => e.Unlocked);
            var list = Ui.ListPanel(body, CountLabel(t, open, entries.Count), "codex-list");
            var detail = Ui.DetailPanel(body, "codex-detail");
            rowIcons.Clear();
            foreach (var e in entries)
            {
                string id = e.Id;
                if (!e.Unlocked)
                {
                    var locked = Ui.MenuRow("???", "", null, list, null, false);
                    locked.name = "codex-locked-" + id;
                    Silhouette(locked, t.Kind, id);
                    continue;
                }
                var row = Ui.MenuRow(e.Title, "", () => Read(id, detail), list);
                row.name = "codex-" + id;
                var icon = EntryIcon(row, t.Kind, id);
                if (Codex.IsNew(S, t.Kind, id)) RowIcon.Badge(icon, RowKind.New);
                rowIcons[id] = icon;
            }
            if (open == 0) Ui.Empty(t.OpenEnded ? "Nothing found yet." : "Nothing here yet. Keep exploring.", list);
            if (selected != null && Codex.IsUnlocked(S, t.Kind, selected)) Read(selected, detail);
            else Ui.Empty("Pick an entry to read it.", detail);
        }

        // ── Tabs ────────────────────────────────────────────────────────────────────

        /// <summary>The kit's tab bar (<see cref="Ui.Tabs"/>'s classes), each tab led by its kind icon and showing its
        /// count; a tab with unread entries wears the New badge on its icon.</summary>
        private void TabBar(VisualElement body)
        {
            var bar = Ui.El("tabs", body);
            bar.name = "codex-tabs";
            bar.style.flexWrap = Wrap.Wrap;
            tabIcons = new VisualElement[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                int idx = i;
                var t = Tabs[i];
                var el = Ui.El("tab" + (i == tab ? " tab--active" : ""), bar);
                el.name = "tab-" + t.Kind;
                el.style.flexDirection = FlexDirection.Row;
                el.style.alignItems = Align.Center;
                el.pickingMode = PickingMode.Position;
                var icon = el.KindIcon(t.Icon);
                icon.style.width = icon.style.height = Size.IconS;
                icon.style.marginRight = Space.S8;
                tabIcons[i] = icon;
                var label = Ui.Text($"{t.Label} {TabCount(t)}", null, el);
                label.pickingMode = PickingMode.Ignore;
                UpdateTabBadge(i);
                Ui.Tappable(el, () =>
                {
                    if (tab == idx) return;
                    tab = idx;
                    selected = null;
                    Refresh();
                });
            }
        }

        private void UpdateTabBadge(int i)
        {
            if (tabIcons == null || i < 0 || i >= tabIcons.Length || tabIcons[i] == null) return;
            if (Codex.NewCount(S, Tabs[i].Kind) > 0) RowIcon.Badge(tabIcons[i], RowKind.New);
            else RowIcon.ClearBadge(tabIcons[i]);
        }

        /// <summary>"3/15" for a listed kind (unlocked of every entry), "12" for an open-ended one; the Foes tab counts
        /// what the Bestiary has seen of every foe, as its own grid does.</summary>
        private string TabCount(Tab t)
        {
            if (t.Kind == CodexKinds.Foe)
            {
                var foes = C.Classes.Values.Where(c => c.Team == Team.Enemy).ToList();
                return $"{foes.Count(f => S.Bestiary.TryGetValue(f.Id, out var b) && b.Seen)}/{foes.Count}";
            }
            var entries = Entries(t);
            int open = entries.Count(e => e.Unlocked);
            return t.OpenEnded ? Ui.Count(open) : $"{open}/{entries.Count}";
        }

        private static string CountLabel(Tab t, int open, int total) =>
            t.OpenEnded ? $"{t.Label}: {Ui.Count(open)} found" : $"{t.Label}: {open}/{total}";

        // ── Entries ─────────────────────────────────────────────────────────────────

        private readonly struct Entry
        {
            public readonly string Id, Title;
            public readonly bool Unlocked;
            public Entry(string id, string title, bool unlocked) { Id = id; Title = title; Unlocked = unlocked; }
        }

        /// <summary>One tab's entries: codex.json's, in its order, then any other unlocked one by title. Only entries whose
        /// record exists; a locked one has no title (it is never shown).</summary>
        private List<Entry> Entries(Tab t)
        {
            var list = new List<Entry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in C.Codex.Keys)
            {
                if (!CodexKinds.TrySplit(key.Trim().ToLowerInvariant(), out var kind, out var id) || kind != t.Kind) continue;
                if (!seen.Add(id) || !C.CodexRecordExists(kind, id)) continue;
                bool open = Codex.IsUnlocked(S, kind, id);
                list.Add(new Entry(id, open ? C.CodexEntry(kind, id).Title : null, open));
            }
            var extra = new List<Entry>();
            foreach (var id in Codex.Unlocked(S, t.Kind))
                if (seen.Add(id) && C.CodexRecordExists(t.Kind, id)) extra.Add(new Entry(id, C.CodexEntry(t.Kind, id).Title, true));
            list.AddRange(extra.OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>The kind icon an entry's row leads with: its own art (board tile, portrait, item or ability icon) where
        /// there is one, else its kind's emblem.</summary>
        private VisualElement EntryIcon(VisualElement row, string kind, string id)
        {
            switch (kind)
            {
                case CodexKinds.Place: return row.KindIcon(RowKind.Place, id);
                case CodexKinds.Faction: return row.KindIcon(RowKind.Faction);
                case CodexKinds.Character: return row.KindIcon(C.TryClass(id)?.Team == Team.Hero ? RowKind.Hero : RowKind.Npc, id);
                case CodexKinds.Foe: return row.KindIcon(RowKind.Hero, id);
                case CodexKinds.Item: return row.KindIcon(RowKind.Item, id);
                case CodexKinds.Ability: return row.KindIcon(RowKind.Ability, id);
                case CodexKinds.Keyword: return row.KindIcon(RowKind.Codex);
                default: return row.KindIcon(RowKind.Lore);
            }
        }

        /// <summary>A locked entry's icon: its art blacked out (the Bestiary's unseen silhouette), or the padlock where it
        /// has only an emblem.</summary>
        private void Silhouette(VisualElement row, string kind, string id)
        {
            var icon = EntryIcon(row, kind, id);
            if (icon is KindEmblem) { row.KindIcon(RowKind.Locked); return; }
            icon.style.unityBackgroundImageTintColor = Shadow;
            icon.Query<VisualElement>().ForEach(e => e.style.unityBackgroundImageTintColor = Shadow);
        }

        // ── Reading view ────────────────────────────────────────────────────────────

        /// <summary>Shows one entry in the reading view and marks it read (its New badges go).</summary>
        private void Read(string id, VisualElement w)
        {
            var t = Tabs[tab];
            selected = id;
            if (rowIcons.TryGetValue(id, out var entryIcon) && entryIcon.parent != null) Ui.Select(entryIcon.parent);
            if (Codex.MarkRead(S, t.Kind, id))
            {
                App.Persist();
                RowIcon.ClearBadge(entryIcon);
                UpdateTabBadge(tab);
            }
            var e = C.CodexEntry(t.Kind, id);
            w.Clear();
            var top = Ui.El("row", w);
            Ui.Heading(e.Title, top, "grow").name = "codex-title";
            Ui.Caption(KindName(t.Kind), top, "dim");
            GlossaryText.Create(e.Body, "kit-text", w).name = "codex-body";
            if (e.Related.Count == 0) return;
            Ui.Section("See also", w);
            foreach (var key in e.Related)
            {
                if (!CodexKinds.TrySplit(key, out var rk, out var rid)) continue;
                if (!Codex.IsUnlocked(S, rk, rid))
                {
                    var locked = Ui.MenuRow("???", KindName(rk), null, w, null, false);
                    locked.name = "codex-link-" + rk + "-" + rid;
                    Silhouette(locked, rk, rid);
                    continue;
                }
                string kind = rk, target = rid;
                var link = Ui.MenuRow(C.CodexEntry(rk, rid).Title, KindName(rk), () => Jump(kind, target), w);
                link.name = "codex-link-" + rk + "-" + rid;
                var icon = EntryIcon(link, rk, rid);
                if (Codex.IsNew(S, rk, rid)) RowIcon.Badge(icon, RowKind.New);
            }
        }

        /// <summary>Follows a link: opens that entry's tab with it selected (a foe opens in the Bestiary's detail).</summary>
        private void Jump(string kind, string id)
        {
            int i = Array.FindIndex(Tabs, x => x.Kind == kind);
            if (i < 0) return;
            tab = i;
            if (kind == CodexKinds.Foe)
            {
                bestiary.Selected = id;
                selected = null;
                Codex.MarkRead(S, kind, id);
                App.Persist();
            }
            else selected = id;
            Refresh();
        }

        private static string KindName(string kind)
        {
            switch (kind)
            {
                case CodexKinds.Place: return "Place";
                case CodexKinds.Faction: return "Faction";
                case CodexKinds.Character: return "Character";
                case CodexKinds.Foe: return "Foe";
                case CodexKinds.Item: return "Item";
                case CodexKinds.Ability: return "Ability";
                case CodexKinds.Keyword: return "Keyword";
                default: return "Story";
            }
        }
    }
}
