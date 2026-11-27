using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The widget kit. Every screen composes these; nothing positions itself with magic pixel offsets
    /// except the battle board. All styling is in Theme.uss classes.
    /// </summary>
    public static class Ui
    {
        public static VisualElement El(string classes = null, VisualElement parent = null)
        {
            var e = new VisualElement();
            AddClasses(e, classes);
            parent?.Add(e);
            return e;
        }

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            bool heavy = false;
            foreach (var c in classes.Split(' '))
            {
                if (c.Length == 0) continue;
                e.AddToClassList(c);
                heavy |= HeavyClasses.Contains(c);
            }
            if (heavy) Fonts.UseHeavy(e);
        }

        /// <summary>Classes that used to be faux-bold in Theme.uss; they take the real Heavy cut instead (POL-A3).
        /// Fonts are runtime SDF assets, so USS can't reference them; the weight is applied here.</summary>
        private static readonly HashSet<string> HeavyClasses = new HashSet<string>
            { "t-heavy", "title", "h1", "h2", "window-title", "header-title", "btn", "tab--active", "role-badge", "pip", "kit-heading", "kit-name", "kit-section" };

        /// <summary>Heavy for elements a screen tagged with a heavy class outside these helpers, leaving any element
        /// whose font is already set inline (a Display headline) alone.</summary>
        public static void ApplyHeavy(VisualElement root)
        {
            root.Query<VisualElement>().Where(x => x.style.unityFontDefinition.keyword == StyleKeyword.Null && x.GetClasses().Any(HeavyClasses.Contains))
                .ForEach(Fonts.UseHeavy);
        }

        public static Label Text(string text, string classes = null, VisualElement parent = null)
        {
            // Content text that reaches a plain label still shows its [terms] as terms, never raw brackets (GG-T300).
            var l = new Label(GlossaryText.Highlight(text)) { enableRichText = true };
            l.pickingMode = PickingMode.Ignore;
            AddClasses(l, classes);
            parent?.Add(l);
            return l;
        }

        /// <summary>GG-T271/T305: makes a label one line that may shrink below its text; when it overflows it becomes the
        /// shared marquee (see <see cref="GridGame.App.Marquee"/>; every overflowing one-liner gets it, this only opts a
        /// label into shrinking).</summary>
        public static Label Marquee(Label label, bool grow = true) => GridGame.App.Marquee.Apply(label, grow);

        public static VisualElement Window(string title = null, VisualElement parent = null, string classes = null)
        {
            // GG-T454: a window right under a tab strip sits flush on it, so no screen has to zero its top margin.
            bool tabbed = parent != null && parent.childCount > 0 && parent[parent.childCount - 1].ClassListContains("tabs");
            var w = El("window " + (classes ?? "") + (tabbed ? " window--tabbed" : ""), parent);
            w.style.backgroundImage = new StyleBackground(Art.Window);
            TintWindow(w);
            // GG-T407: the theme's texture layer (absolute, picking-ignored, first child). A screen that clears and refills
            // a window drops it; the next geometry change (or theme apply) puts it back.
            WindowThemes.Dress(w);
            w.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                bool want = WindowThemes.Overlay(WindowThemes.Current).tex != null;
                bool has = w.hierarchy.childCount > 0 && w.hierarchy[0].ClassListContains(WindowThemes.OverlayClass);
                if (want != has) WindowThemes.Dress(w);
            });
            if (title != null) Text(title, "window-title", w);
            return w;
        }

        /// <summary>The window frame's inline colours (GG-T407, <see cref="WindowThemes.FrameSides"/>): a theme's own 1-3
        /// frame colours, or Classic's derived accent; cleared when the theme's Theme.uss block owns the frame.</summary>
        private static void TintWindow(VisualElement w)
        {
            var sides = WindowThemes.FrameSides(WindowThemes.Current, Art.ThemeAccent);
            StyleColor C(int i) => sides == null ? new StyleColor(StyleKeyword.Null) : new StyleColor(sides[i]);
            w.style.borderTopColor = C(0);
            w.style.borderLeftColor = C(1);
            w.style.borderRightColor = C(2);
            w.style.borderBottomColor = C(3);
        }

        /// <summary>
        /// Applies the active menu theme (Art.ApplyTheme) live to everything already on screen (GG-T185, GG-T407): puts the
        /// frame style's root class on <paramref name="root"/> (removing every other one) so Theme.uss's scoped
        /// <c>.theme-* .window/.tab/.btn</c> rules take over, re-tints every open window's frame, lays the theme's texture
        /// layer into every window and sets the default text colour. The gradient is repainted in place by Art.ApplyTheme.
        /// </summary>
        public static void ApplyMenuTheme(VisualElement root)
        {
            if (root == null) return;
            string mine = Art.ThemeUssClass;
            foreach (var c in WindowThemes.AllUssClasses) root.EnableInClassList(c, c == mine);
            var text = WindowThemes.TextColor;
            var stage = root.Q(className: "stage");
            if (stage != null) stage.style.color = text.HasValue ? new StyleColor(text.Value) : new StyleColor(StyleKeyword.Null);
            root.Query(className: "window").ForEach(w => { TintWindow(w); WindowThemes.Dress(w); });
        }

        public static Button Button(string text, Action onClick, string classes = null, VisualElement parent = null)
        {
            var b = new Button(() =>
            {
                GameApp.I?.Audio?.Sfx("ui.click");
                onClick?.Invoke();
            }) { text = text };
            b.AddToClassList("btn");
            Fonts.UseHeavy(b);
            AddClasses(b, classes);
            Pressable(b);
            parent?.Add(b);
            return b;
        }

        /// <summary>
        /// The one press feel every tappable thing shares (owner direction 2026-10-11): on touch it sinks — drops a few
        /// pixels onto its lip, shrinks, darkens — with a soft press tick; on release it springs back. Disabled things
        /// don't sink. Styling lives in Theme.uss (.pressable / .is-pressed); this only toggles the state.
        /// </summary>
        public static T Pressable<T>(T e, bool sound = true) where T : VisualElement
        {
            if (e.ClassListContains("pressable")) return e;
            e.AddToClassList("pressable");
            void Release()
            {
                if (!e.ClassListContains("is-pressed")) return;
                e.RemoveFromClassList("is-pressed");
                e.AddToClassList("is-released");
                e.schedule.Execute(() => e.RemoveFromClassList("is-released")).StartingIn(140);
            }
            e.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (!e.enabledInHierarchy || e.ClassListContains("btn--disabled") || e.ClassListContains("menu-row--disabled")) return;
                e.RemoveFromClassList("is-released");
                e.AddToClassList("is-pressed");
                if (sound) GameApp.I?.Audio?.Sfx("ui.press", 0.55f);
            }, TrickleDown.TrickleDown);
            e.RegisterCallback<PointerUpEvent>(_ => Release(), TrickleDown.TrickleDown);
            e.RegisterCallback<PointerLeaveEvent>(_ => Release());
            e.RegisterCallback<PointerCancelEvent>(_ => Release());
            e.RegisterCallback<PointerCaptureOutEvent>(_ => Release());
            return e;
        }

        /// <summary>A tappable card/cell (party card, bestiary thumb, bar cell): the shared press feel plus a click handler.</summary>
        public static T Tappable<T>(T e, Action onClick, string sfx = "ui.click") where T : VisualElement
        {
            Pressable(e);
            e.RegisterCallback<ClickEvent>(_ =>
            {
                try
                {
                    if (sfx != null) GameApp.I?.Audio?.Sfx(sfx);
                    onClick?.Invoke();
                }
                catch (Exception ex) { Debug.LogException(ex); }
            });
            return e;
        }

        public static void SetEnabled(Button b, bool enabled)
        {
            b.SetEnabled(enabled);
            b.EnableInClassList("btn--disabled", !enabled);
        }

        /// <summary>The page title (kit role, GG-T454): back button, title (with an optional one-line subtitle under it, e.g. a
        /// station's level), the currency strip (<see cref="Currencies"/> fills it). The bar spans the stage; its contents
        /// line up with the windows below (Kit.Gutter). <paramref name="onHome"/> adds the Home emblem button after Back (GG-T1335).</summary>
        public static VisualElement Header(string title, Action onBack, VisualElement parent, out VisualElement currencies, string subtitle = null, Action onHome = null)
        {
            var h = El("header", parent);
            // The bar spans the stage; its back button, title and gold line up with the content column (GG-T398).
            h.style.paddingLeft = h.style.paddingRight = Fit.Inset + Kit.Gutter;
            // GG-T483: under a notch the bar's band reaches up to the screen's top edge, its controls staying below the inset.
            if (GameApp.SafeTop > 0f)
            {
                h.style.marginTop = -GameApp.SafeTop;
                h.style.paddingTop = GameApp.SafeTop;
                h.style.minHeight = Kit.HeaderHeight + GameApp.SafeTop;
            }
            // GG-T738 (§26.7): the back button is the Back kind emblem, not a "‹" glyph.
            if (onBack != null) RowIcon.IconButton(null, RowKind.Back, onBack, "btn--small btn--icon", h).name = "back";
            if (onHome != null) RowIcon.IconButton(null, RowKind.Home, onHome, "btn--small btn--icon", h).name = "home";
            if (subtitle == null) Text(title, "header-title shadow", h);
            else
            {
                var titles = El("header-titles", h);
                Text(title, "header-title shadow", titles);
                var sub = Text(subtitle, "header-sub", titles);
                sub.name = "header-sub";
                Marquee(sub);
            }
            currencies = El("header-currencies", h);
            currencies.name = "header-currencies";
            return h;
        }

        /// <summary>GG-T1361: fills a header's currency strip with what <paramref name="s"/> holds: Gold, Shards (tapping them
        /// runs <paramref name="onShards"/>, the Shard Store) and, while an account wallet is bound, its Summon Tickets.
        /// Rebuilds the strip each call, so a refresh shows the new balances.</summary>
        public static void Currencies(VisualElement bar, SaveData s, Action onShards = null)
        {
            if (bar == null) return;
            bar.Clear();
            if (s == null) return;
            Text(Gold(s.Gold), "header-gold shadow", bar).name = "header-gold";
            var shards = RowIcon.IconButton(Count(DiamondExchange.Have(s)), RowKind.Diamond, onShards, "btn--small", bar);
            shards.name = "header-shards";
            shards.tooltip = ShardText.Store;
            if (onShards == null) shards.pickingMode = PickingMode.Ignore;
            if (DiamondExchange.Wallet is AccountWallet w)
            {
                int t = Math.Max(0, w.Tickets);
                Text($"{Count(t)} Ticket{(t == 1 ? "" : "s")}", "header-gold shadow", bar).name = "header-tickets";
            }
        }

        /// <summary>The hub's bottom nav (kit role, GG-T1590, BIBLE §11.3): Home, Campaign, Heroes, Summon and Shop off
        /// <see cref="NavBadges.Tabs"/>, sharing the stage's width, each <see cref="Kit.NavHeight"/> tall. Under a gesture
        /// bar its band reaches the screen's bottom edge, its tabs staying above the inset (like <see cref="Header"/>). The
        /// lit tab is the page directly above the hub on the stack (<paramref name="current"/> with no hub on it); a
        /// locked tab is dimmed with a padlock and its tap only toasts why.</summary>
        public static VisualElement NavBar(VisualElement parent, GameScreen current)
        {
            var app = GameApp.I;
            var bar = El("kit-navbar", parent);
            bar.name = "navbar";
            if (GameApp.SafeBottom > 0f)
            {
                bar.style.marginBottom = -GameApp.SafeBottom;
                bar.style.paddingBottom = GameApp.SafeBottom;
                bar.style.minHeight = Kit.NavHeight + GameApp.SafeBottom;
            }
            string lit = NavLit(app.Router, current);
            foreach (var tab in NavBadges.Tabs(app.Content, app.Save, app.Settings))
            {
                var t = El("kit-navtab", bar);
                t.name = "nav-" + tab.Id;
                t.EnableInClassList("kit-navtab--active", tab.Id == lit);
                t.EnableInClassList("kit-navtab--locked", !tab.Open);
                var icon = t.KindIcon(NavKind(tab.Id));
                if (!tab.Open) RowIcon.Badge(icon, RowKind.Locked);
                Marquee(Text(tab.Label, "kit-navtab-label", t));
                if (!string.IsNullOrEmpty(tab.Badge)) Text(tab.Badge, "kit-navtab-badge", t).name = "nav-badge";
                string id = tab.Id, hint = tab.Hint;
                if (tab.Open) Tappable(t, () => NavGo(app, id));
                else Tappable(t, () => app.Toast(hint), "ui.error");
            }
            return bar;
        }

        private static RowKind NavKind(string id)
        {
            switch (id)
            {
                case CampMenu.Campaign: return RowKind.Place;
                case CampMenu.Heroes: return RowKind.Hero;
                case CampMenu.Summon: return RowKind.New;
                case CampMenu.Shop: return RowKind.Shop;
                default: return RowKind.Home;
            }
        }

        /// <summary>The tab the page directly above the hub belongs to; Home on the hub itself or a page no tab owns.</summary>
        private static string NavLit(ScreenRouter router, GameScreen current)
        {
            GameScreen above = current, prev = null;
            foreach (var s in router.Stack)
            {
                if (s is HubScreen) { above = prev; break; }
                prev = s;
            }
            switch (above)
            {
                case StageSelectScreen _: case StageConfirmScreen _: return CampMenu.Campaign;
                case HeroScreen _: return CampMenu.Heroes; // Status, Abilities, Party
                case SummonScreen _: case MercenaryScreen _: return CampMenu.Summon;
                case ShopScreen _: case DiamondStoreScreen _: return CampMenu.Shop;
                default: return NavBadges.Home;
            }
        }

        /// <summary>Home goes back to the hub; every other tab opens its page one step above a fresh hub, so Back from it
        /// lands on the hub. Nothing happens while a timed paywall job plays out (<see cref="ScreenRouter.Home"/>).</summary>
        private static void NavGo(GameApp app, string id)
        {
            var s = app.Save;
            GameScreen dest;
            switch (id)
            {
                case CampMenu.Campaign: dest = new StageSelectScreen(); break;
                case CampMenu.Heroes:
                    string lead = s.Party.FirstOrDefault();
                    dest = Training.Step(s) ? new AbilitiesScreen(lead) : (GameScreen)new StatusScreen(lead);
                    break;
                case CampMenu.Summon: dest = new SummonScreen(); break;
                case CampMenu.Shop: dest = CampUnlocks.NpcOpen(s, CampNpcs.Merchant) ? new ShopScreen() : (GameScreen)new DiamondStoreScreen(); break;
                default: dest = null; break;
            }
            if (app.Router.Home() && dest != null) app.Router.Push(dest);
        }

        // ── The menu kit (GG-T454, Tokens.cs Kit): the builders every menu screen lays itself out with ──────────────

        /// <summary>A page's body under its <see cref="Header"/>: the centred content column, padded so each window (with its
        /// own S8 margin) sits <see cref="Kit.Gutter"/> from the stage edge and <see cref="Kit.Gap"/> from the header, the
        /// other windows and the bottom. Every menu page lays its windows into this.</summary>
        public static VisualElement PageBody(VisualElement col)
        {
            var body = El("col grow", col);
            body.name = "page-body";
            // The column is the whole stage on every aspect (GG-T550), so a tablet's windows fill its width like the hub's.
            Fit.Columnize(body, Kit.Gutter - Space.S8);
            body.style.paddingTop = body.style.paddingBottom = Kit.Gap - Space.S8;
            return body;
        }

        /// <summary>A window holding a block of content (an intro, a form, a summary). <paramref name="fill"/> = it takes the
        /// column's leftover height (and shares it evenly with any other filling window).</summary>
        public static VisualElement Panel(VisualElement parent, string title = null, bool fill = false, string name = null)
        {
            var w = Window(title, parent, fill ? "window--fill" : null);
            if (name != null) w.name = name;
            return w;
        }

        /// <summary>A window holding a scrolling list that fills the column's leftover height (two of them split it evenly).
        /// Returns the list's content container, where rows and cards go.</summary>
        public static VisualElement ListPanel(VisualElement parent, string title = null, string name = null) =>
            ListPanel(parent, title, out _, name);

        public static VisualElement ListPanel(VisualElement parent, string title, out ScrollView scroll, string name = null)
        {
            var w = Panel(parent, title, true, name);
            scroll = List(w);
            // A short list's rows grow into the panel's spare height, capped (Theme.uss .list--fill), so the screen has no
            // dead band under them (GG-T654, POL-C9); a long list scrolls as before.
            scroll.AddToClassList("list--fill");
            var sv = scroll;
            sv.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => FillViewport(sv));
            // Rows added after the viewport settles (a refresh, a late-built list) still pick their spacing.
            sv.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => FillViewport(sv));
            return scroll.contentContainer;
        }

        /// <summary>GG-T654: a column list's content is at least its viewport's height, so its rows have spare height to
        /// grow into (USS can't size it: a percent of the viewport didn't resolve through the ScrollView's own wrapper). A
        /// grid (row-wrap content, e.g. the Bestiary) opts out and drops the fill class, so its cells never stretch.</summary>
        private static void FillViewport(ScrollView s)
        {
            float h = s.contentViewport.layout.height;
            if (float.IsNaN(h) || h <= 0f) return;
            bool column = s.contentContainer.resolvedStyle.flexDirection == FlexDirection.Column;
            s.EnableInClassList("list--fill", column);
            s.contentContainer.style.minHeight = column ? new StyleLength(Mathf.Floor(h)) : new StyleLength(StyleKeyword.Null);
            // Rows stop growing at two targets (--row-fill-max); a list of rows too short to fill even then spreads the rest
            // evenly between them instead of leaving one dead band under the last (no effect once the list scrolls). A
            // list mixing rows with an intro, heading or note spreads with SpaceBetween instead, so its first line stays
            // flush at the top (never floating mid-panel) and the band is shared out between its parts. A list with an
            // empty-state message and no rows stays top-aligned: that message grows into the spare height itself.
            var kids = s.contentContainer.Children().ToList();
            bool anyRow = column && kids.Any(IsFillRow);
            bool rowsOnly = anyRow && kids.All(IsFillRow);
            bool empty = kids.Any(k => k.ClassListContains("kit-empty"));
            s.contentContainer.style.justifyContent =
                rowsOnly ? new StyleEnum<Justify>(Justify.SpaceEvenly)
                : anyRow && !empty ? new StyleEnum<Justify>(Justify.SpaceBetween)
                : new StyleEnum<Justify>(StyleKeyword.Null);
        }

        private static bool IsFillRow(VisualElement e) =>
            e is Button || e.ClassListContains("menu-row") || e.ClassListContains("card") || e.ClassListContains("pressable");

        /// <summary>The detail pane under a list: as tall as what it shows, at most <see cref="Kit.DetailMaxPct"/>% of the
        /// column, then it scrolls inside itself. Returns its content container (clear and refill it on selection).</summary>
        public static VisualElement DetailPanel(VisualElement parent, string name = null)
        {
            var w = Window(null, parent, "window--detail");
            if (name != null) w.name = name;
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.AddToClassList("list");
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            w.Add(sv);
            return sv.contentContainer;
        }

        /// <summary>The action bar at the foot of a screen (a total, then the commit button). Returns its row.</summary>
        public static VisualElement Footer(VisualElement parent, string name = null)
        {
            var w = Window(null, parent);
            if (name != null) w.name = name;
            return El("kit-footer", w);
        }

        /// <summary>A row of buttons inside a window or card; they wrap, 24 px apart and 24 px in from the text column.</summary>
        public static VisualElement Actions(VisualElement parent) => El("kit-actions", parent);

        /// <summary>A group header ("Weapon", "Under way"): caption size, gold, flush with the window's left edge.</summary>
        public static Label Section(string text, VisualElement parent)
        {
            var l = Text(text, "kit-section", parent);
            if (parent != null && parent.childCount == 1) l.AddToClassList("kit-section--first");
            return l;
        }

        /// <summary>What an empty list or pane says ("Nothing to sell."): body size, dim, centred.</summary>
        public static Label Empty(string text, VisualElement parent) => Text(text, "kit-empty", parent);

        /// <summary>A heading (48): a window-level or detail pane's title. <paramref name="tone"/>: gold, dim, good, bad, a rarity class.</summary>
        public static Label Heading(string text, VisualElement parent, string tone = null) => Text(text, "kit-heading " + (tone ?? ""), parent);

        /// <summary>A card's name (body size, heavy).</summary>
        public static Label Name(string text, VisualElement parent, string tone = null) => Text(text, "kit-name " + (tone ?? ""), parent);

        /// <summary>A sentence or paragraph (body size, wraps). Anything that wraps is body text (POL-A2).</summary>
        public static Label Para(string text, VisualElement parent, string tone = null) => Text(text, "kit-text " + (tone ?? ""), parent);

        /// <summary>One line of meta ("Lv 3 · HP 120", "Spent 2"): caption size; a line too long for its space scrolls (marquee).</summary>
        public static Label Caption(string text, VisualElement parent, string tone = null) => Text(text, "kit-caption " + (tone ?? ""), parent);

        /// <summary>A row's note: a sentence under a menu row (an ability's description, a stage's drops), indented to the
        /// row's label. Body size, dim unless <paramref name="tone"/> says otherwise.</summary>
        public static Label Note(string text, VisualElement parent, string tone = "dim")
        {
            var l = Para(text, parent, tone);
            l.AddToClassList("kit-note");
            return l;
        }

        /// <summary>A read-only fact: a caption label in a fixed column, then its value in body text (wraps). For facts a
        /// player reads, not picks (Status gear, bestiary knowledge, a Crucible rule); a pickable line is a <see cref="MenuRow"/>.</summary>
        public static VisualElement Field(string label, string value, VisualElement parent, string tone = null, string name = null)
        {
            var row = El("kit-field", parent);
            if (name != null) row.name = name;
            Text(label, "kit-field__label", row);
            var v = Text(value ?? "", "kit-field__value " + (tone ?? ""), row);
            v.name = "value";
            return row;
        }

        /// <summary>A list card (kit role): portrait (<see cref="Kit.Thumb"/>, or none for a null <paramref name="classId"/>),
        /// then a column with the card's name. Returns the card; <paramref name="col"/> takes its other lines.</summary>
        public static VisualElement Card(VisualElement parent, string classId, string name, out VisualElement col, string cardName = null)
        {
            var card = El("card kit-card", parent);
            if (cardName != null) card.name = cardName;
            if (classId != null) Thumb(classId, card, Kit.Thumb);
            col = El("col grow", card);
            col.style.flexShrink = 1; col.style.minWidth = 0;
            if (name != null) Name(name, col);
            return card;
        }

        public static string Gold(int g) => Count(g) + " G";
        /// <summary>A whole count with thousands commas ("1,250"), the same on every device: the phone's own culture
        /// would print "1.250" or a narrow no-break space the game font may not have.</summary>
        public static string Count(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>FF-style menu row: cursor hand, optional icon, label, right-aligned value.</summary>
        public static VisualElement MenuRow(string label, string value, Action onClick, VisualElement parent, Texture2D icon = null, bool enabled = true)
        {
            var row = El("menu-row", parent);
            var cur = El("menu-cursor", row);
            cur.style.backgroundImage = new StyleBackground(Art.Cursor);
            if (icon != null)
            {
                var ic = El("menu-icon", row);
                ic.style.backgroundImage = new StyleBackground(icon);
            }
            var l = Text(label, "menu-label", row);
            l.name = "label";
            // A one-word label ("Blacksmith", "Enchantress") never breaks mid-word: it keeps its width and the value
            // beside it gives way and scrolls instead (GG-T395).
            bool oneWord = !string.IsNullOrEmpty(label) && label.IndexOf(' ') < 0;
            if (oneWord) { l.style.whiteSpace = WhiteSpace.NoWrap; l.style.flexShrink = 0f; }
            var v = Text(value ?? "", "menu-value", row);
            v.name = "value";
            // A value (price, count, "Lv 1 · 5 waves") stays on one line: never squeezed below its text, never wider
            // than 55% of the row; longer ones scroll (GG-T271). The label wraps instead.
            Marquee(v, false);
            v.style.flexShrink = oneWord ? 1f : 0f;
            // In px from the row's width: a percent max-width on a text node resolves against the wrong base (GG-T305).
            row.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float cap = Mathf.Floor(row.contentRect.width * 0.55f);
                if (cap > 0 && Mathf.Abs(v.resolvedStyle.maxWidth.value - cap) > 0.5f) v.style.maxWidth = cap;
            });
            row.EnableInClassList("menu-row--disabled", !enabled);
            Pressable(row);
            row.RegisterCallback<PointerEnterEvent>(_ => { if (enabled) Select(row); });
            row.RegisterCallback<ClickEvent>(_ =>
            {
                try
                {
                    Select(row);
                    if (!enabled) { GameApp.I?.Audio?.Sfx("ui.error"); return; }
                    GameApp.I?.Audio?.Sfx("ui.select");
                    onClick?.Invoke();
                }
                catch (Exception e) { Debug.LogException(e); }
            });
            return row;
        }

        public static void Select(VisualElement row)
        {
            var p = row.parent;
            if (p != null) foreach (var sib in p.Children()) sib.RemoveFromClassList("menu-row--selected");
            row.AddToClassList("menu-row--selected");
        }

        /// <summary>Put the cursor on the first enabled row of a menu container.</summary>
        public static void SelectFirst(VisualElement menu)
        {
            foreach (var c in menu.Children())
                if (c.ClassListContains("menu-row") && !c.ClassListContains("menu-row--disabled")) { Select(c); return; }
        }

        public static ScrollView List(VisualElement parent)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.AddToClassList("list");
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            parent?.Add(sv);
            // The window holding a list takes only the space left over and scrolls; it is the one that gives way
            // when a screen runs long, never the tabs or footer around it.
            if (parent != null && parent.ClassListContains("window")) parent.AddToClassList("window--scroller");
            return sv;
        }

        public static VisualElement Tabs(string[] names, int active, Action<int> onPick, VisualElement parent)
        {
            var bar = El("tabs", parent);
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var t = Text(names[i], "tab" + (i == active ? " tab--active" : ""), bar);
                t.name = "tab-" + names[i].ToLowerInvariant().Replace(' ', '-');
                t.pickingMode = PickingMode.Position;
                Tappable(t, () => onPick(idx));
            }
            return bar;
        }

        public static VisualElement Gauge(float frac, VisualElement parent, float width = -1)
        {
            var g = El("gauge", parent);
            if (width > 0) g.style.width = width;
            var f = El("gauge-fill", g);
            f.name = "fill";
            SetGauge(g, frac);
            return g;
        }

        public static void SetGauge(VisualElement gauge, float frac)
        {
            var f = gauge.Q("fill");
            frac = Mathf.Clamp01(frac);
            f.style.width = Length.Percent(frac * 100f);
            f.EnableInClassList("gauge-fill--mid", frac <= 0.5f && frac > 0.25f);
            f.EnableInClassList("gauge-fill--low", frac <= 0.25f);
        }

        /// <summary>A framed card thumbnail: the class's baked thumbnail (<see cref="PortraitThumb"/>). Above
        /// <see cref="ThumbMaxSize"/> it draws the full portrait through its Thumbnail window instead (the summon reveal).
        /// Tint the art (e.g. the bestiary's unseen silhouette) on <c>t.Q(ThumbArt)</c>, not on the frame.
        /// <paramref name="thumbOnly"/> (GG-T1210): a grid of 100+ cards (hub, bestiary) draws baked thumbs only, never a
        /// full portrait, even for a class with no baked thumb (that card then shows empty).</summary>
        public static VisualElement Thumb(string classId, VisualElement parent, float size = Kit.Thumb, bool thumbOnly = false)
        {
            var t = El("thumb", parent);
            t.style.width = size;
            t.style.height = size;
            PortraitThumb(t, classId, thumbOnly || size <= ThumbMaxSize, thumbOnly);
            return t;
        }

        /// <summary>GG-T720: the largest draw (design px) that uses the baked thumbnail; bigger ones use the portrait.
        /// GG-T1210: a thumb bakes at 320 px with <see cref="ThumbMargin"/> on every side, so its visible window is
        /// ~267 px, still above 256.</summary>
        public const float ThumbMaxSize = 200f;

        /// <summary>GG-T1210: the baked thumb's extra margin on every side, as a fraction of the visible (owner-framed)
        /// side. tools/make_thumbs.py (MARGIN) bakes a square of side ×(1 + 2·ThumbMargin) around the framing's centre,
        /// and <see cref="PortraitThumb"/> draws it at that size offset by −ThumbMargin, so at rest exactly the framing
        /// shows. <see cref="ThumbWobble"/> drifts the art inside this margin.</summary>
        public const float ThumbMargin = 0.1f;

        /// <summary>Name of the portrait child <see cref="PortraitThumb"/> puts in a host (tint/opacity it, not the host).</summary>
        public const string ThumbArt = "thumb-art";

        /// <summary>
        /// Fills a square host with the actor's thumbnail (BIBLE §12.1.1, GG-T713/GG-T720): its baked 256 px
        /// thumbnail (<see cref="Art.Thumb(string)"/>, the class's Thumbnail crop already in it) drawn whole. With no
        /// baked thumb, or <paramref name="useThumb"/> false (a draw too large for 256 px), it falls back to the full
        /// portrait zoomed and offset by its <see cref="Thumbnail"/> (GG-T221). The host clips (overflow hidden);
        /// inside it an inset-0 clip box holds the art child, sized and placed in percent of that box (width =
        /// Zoom×100%, height = Zoom×aspect×100%, left = 50% − X·width, top = 50% − Y·height), so the framing is
        /// right at any size with no layout pass. A portrait with no Thumbnail (or an id that isn't a class) shows
        /// whole, contained. Returns the art child (named <see cref="ThumbArt"/>), which callers tint.
        /// GG-T1210: the baked thumb carries <see cref="ThumbMargin"/> on every side and is drawn at 120%, offset −10%,
        /// so at rest it shows exactly the owner's framing, as the live fallback does (the portrait itself is the margin
        /// there); both framed paths get the shared handheld drift (<see cref="ThumbWobble"/>).
        /// <paramref name="thumbOnly"/>: never load the full portrait (grids of 100+); no baked thumb draws nothing.
        /// </summary>
        public static VisualElement PortraitThumb(VisualElement host, string classId, bool useThumb = true, bool thumbOnly = false)
        {
            host.style.overflow = Overflow.Hidden;
            var clip = host.Q("thumb-clip");
            if (clip == null)
            {
                clip = new VisualElement { name = "thumb-clip", pickingMode = PickingMode.Ignore };
                clip.style.position = Position.Absolute;
                clip.style.left = clip.style.top = clip.style.right = clip.style.bottom = Space.S0;
                clip.style.overflow = Overflow.Hidden;
                host.Insert(0, clip);
            }
            var art = clip.Q(ThumbArt);
            if (art == null)
            {
                art = new VisualElement { name = ThumbArt, pickingMode = PickingMode.Ignore };
                art.style.position = Position.Absolute;
                clip.Add(art);
            }
            var thumb = useThumb || thumbOnly ? Art.Thumb(classId) : null;
            if (thumb != null)
            {
                // The crop is baked in (tools/make_thumbs.py) with ThumbMargin spare on every side: drawn at 120%,
                // offset −10%, the visible box is exactly the owner's framing. Hosts are reused, so every placement is reset.
                art.style.backgroundImage = new StyleBackground(thumb);
                art.style.left = art.style.top = Length.Percent(-ThumbMargin * 100f);
                art.style.width = art.style.height = Length.Percent((1f + 2f * ThumbMargin) * 100f);
                art.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                ThumbWobble.Track(art);
                return art;
            }
            if (thumbOnly)
            {
                art.style.backgroundImage = StyleKeyword.Null;
                ThumbWobble.Untrack(art);
                return art;
            }
            var tex = string.IsNullOrEmpty(classId) ? null : Art.Portrait(classId);
            art.style.backgroundImage = new StyleBackground(tex);
            var th = string.IsNullOrEmpty(classId) ? null : Art.ThumbnailFor(classId, tex); // the male art's own framing (GG-T532)
            if (th == null || tex == null || tex.width <= 0)
            {
                art.style.left = art.style.top = Space.S0;
                art.style.width = art.style.height = Length.Percent(100);
                art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                ThumbWobble.Untrack(art);
                return art;
            }
            // The live framing: the portrait around the window is the margin the drift moves into.
            ThumbWobble.Track(art);
            float w = th.Zoom * 100f, h = w * tex.height / tex.width;
            art.style.width = Length.Percent(w);
            art.style.height = Length.Percent(h);
            art.style.left = Length.Percent(50f - th.X * w);
            art.style.top = Length.Percent(50f - th.Y * h);
            art.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            return art;
        }

        /// <summary>Most orb pips a cost draws before it switches to one orb and a count (GG-T372).</summary>
        /// <summary>GG-T673: spells cost at most BattleConfig.MaxSpellOrbCost orbs, so every cost shows as pips.</summary>
        public const int MaxCostPips = GridGame.Core.Battle.BattleConfig.MaxSpellOrbCost;

        /// <summary>Mana cost as orb pips: 3 → three orbs; above <see cref="MaxCostPips"/> one orb and "×N" (out of space).
        /// <paramref name="have"/> (GG-T673): orbs the party holds; pips past it dim, so an unaffordable spell reads at a glance.</summary>
        public static VisualElement CostPips(int cost, VisualElement parent, float size = 34, int have = int.MaxValue)
        {
            var row = El("row", parent);
            row.pickingMode = PickingMode.Ignore;
            row.style.alignItems = Align.Center;
            int pips = cost > MaxCostPips ? 1 : cost;
            for (int i = 0; i < pips; i++)
            {
                var p = Text("", "pip pip--mana", row);
                p.style.width = size; p.style.height = size; p.style.borderTopLeftRadius = p.style.borderTopRightRadius = p.style.borderBottomLeftRadius = p.style.borderBottomRightRadius = size / 2;
                p.style.fontSize = size * 0.6f;
                if (i >= have) p.style.opacity = 0.3f;
            }
            if (cost > MaxCostPips)
            {
                var n = Text("×" + cost, "", row);
                n.pickingMode = PickingMode.Ignore;
                n.style.fontSize = size * 0.8f;
                n.style.marginLeft = size * 0.15f;
                n.style.color = Palette.Text;
                Fonts.UseHeavy(n);
            }
            return row;
        }

        public static string RarityClass(ItemRarity r) => "rarity-" + r;

        /// <summary>Class blurbs mark their keywords as [word]; show them as highlighted words instead of raw brackets
        /// (inert; <see cref="GlossaryText.Create"/> makes them tappable).</summary>
        public static string Keywords(string text) => string.IsNullOrEmpty(text) ? "" : GlossaryText.Highlight(text);

        public static Color RoleColor(string role) => Palette.Role(role);

        /// <summary>The badge a domesticated monster wears in place of a hero role (GG-T303).</summary>
        public const string TamedRole = "Tamed";

        /// <summary>The badge for a roster member: its class's Role, or <see cref="TamedRole"/> for a domesticated monster.</summary>
        public static string RoleOf(GridGame.Meta.HeroSave h, ClassDef def) => h != null && h.Domesticated ? TamedRole : def?.Role;

        /// <summary>What each role brings to the party (§25.13), one line.</summary>
        public static string RoleBlurb(string role)
        {
            switch (role)
            {
                case "Tank": return "Taunts enemies onto itself and takes the hits, so casters can finish their spells.";
                case "Healer": return "Mends and cleanses. Heals grow with WIS.";
                case "Mage": return "Ranged elemental spells. Damage grows with INT.";
                case "Striker": return "Pincer damage, Cleave and War Cry. Grows with STR.";
                case "Support": return "Bends the timeline: Quicken allies, Slow foes, rally the party.";
                case "Ranged": return "Precision shots from distance — never needs to be adjacent to strike.";
                case TamedRole: return "A tamed monster: fights on its own with the Pincer keyword. No gear or skills; grows slowly.";
                default: return "";
            }
        }

        /// <summary>A coloured pill naming a hero's role, at the one GUI size (GG-T770).</summary>
        public static Label RoleBadge(string role, VisualElement parent)
        {
            var c = RoleColor(role);
            var l = Text((role ?? "").ToUpperInvariant(), "role-badge", parent);
            l.style.fontSize = TypeScale.Base;
            l.style.color = c;
            l.style.borderTopColor = l.style.borderBottomColor = l.style.borderLeftColor = l.style.borderRightColor = c;
            return l;
        }

        public static string StatLine(Stats s) =>
            $"STR {s.Strength:0}  VIT {s.Vitality:0}  AGI {s.Agility:0}  SPD {s.Speed:0}\nINT {s.Intelligence:0}  WIS {s.Wisdom:0}  LCK {s.Luck:0}";

        public static string Delta(float now, float then)
        {
            float d = then - now;
            if (Mathf.Abs(d) < 0.5f) return $"{then:0}";
            return d > 0 ? $"{then:0} <color=#78EC96>▲{d:0}</color>" : $"{then:0} <color=#FF6E6E>▼{-d:0}</color>";
        }

        /// <summary>What a buff does, in words, built from its mechanics so it never drifts from the numbers (GG-T907: the
        /// one definition lives in Core, <see cref="GridGame.Core.Rules.KeywordText.Buff"/>, where the tests read it).</summary>
        public static string BuffEffect(BuffDef d) => GridGame.Core.Rules.KeywordText.Buff(d);

        /// <summary>An enemy's kit in a few words (§14.4) — shown on its card and in the bestiary.</summary>
        public static string KitTraits(ContentDb c, ClassDef d)
        {
            var t = new List<string>();
            if (d.Heavy) t.Add("[Heavy] (no shove, pull or swap moves it)"); // GG-T907
            if (d.MoveRange > 1) t.Add($"moves {d.MoveRange} tiles");
            if (d.AttackRange > 1) t.Add($"shoots {d.AttackRange} tiles");
            if (!string.IsNullOrEmpty(d.ChargeAbility)) t.Add("casts " + (c.TryAbility(d.ChargeAbility)?.Name ?? d.ChargeAbility));
            if (c.TryBuff(c.TryAbility(d.ChargeAbility)?.ApplyBuff) is BuffDef drain && drain.Tempo < 0)
                t.Add("drains party tempo (interrupt it, cleanse " + drain.Name + " or out-haste it)");
            if (!string.IsNullOrEmpty(d.Trap) && c.Traps.TryGetValue(d.Trap, out var tr)) t.Add("lays " + tr.Name);
            if (!string.IsNullOrEmpty(d.Trail) && c.Traps.TryGetValue(d.Trail, out var tl)) t.Add("leaves a " + tl.Name);
            if (!string.IsNullOrEmpty(d.OnHitBuff)) t.Add($"hits may inflict {c.TryBuff(d.OnHitBuff)?.Name ?? d.OnHitBuff} ({Mathf.RoundToInt(d.OnHitChance * 100)}%)");
            if (d.DeathBurst > 0) t.Add($"explodes when killed ({d.DeathBurst})");
            foreach (var id in d.Abilities)
                if (c.TryAbility(id) is AbilityDef a && (a.Kind == AbilityKind.Passive || a.Kind == AbilityKind.Reactive) && !string.IsNullOrEmpty(a.Name))
                    t.Add(a.Trigger == null ? a.Name.ToLowerInvariant()
                        : $"{a.Name.ToLowerInvariant()} ({GridGame.Core.Rules.TriggerText.KindLabel(a).ToLowerInvariant()}: {GridGame.Core.Rules.TriggerText.Describe(c, a).TrimEnd('.')})"); // GG-T296
            if (d.ImmuneBuffs != null && d.ImmuneBuffs.Count > 0) t.Add("immune to " + string.Join("/", d.ImmuneBuffs));
            return t.Count == 0 ? "" : char.ToUpper(t[0][0]) + string.Join(" · ", t).Substring(1);
        }

        /// <summary>
        /// A status pip: the pixel-art icon (flame, snowflake, poison bottle …) on a dark disc ringed in the status colour.
        /// Falls back to the letter only when an effect has no icon yet.
        /// </summary>
        public static VisualElement StatusPip(string iconId, string letter, Color color, float size, VisualElement parent = null)
        {
            var pip = new VisualElement { pickingMode = PickingMode.Ignore, viewDataKey = iconId };
            pip.style.width = pip.style.height = size;
            pip.style.flexShrink = 0;
            pip.style.borderTopLeftRadius = pip.style.borderTopRightRadius = pip.style.borderBottomLeftRadius = pip.style.borderBottomRightRadius = size / 2f;
            pip.style.borderTopWidth = pip.style.borderBottomWidth = pip.style.borderLeftWidth = pip.style.borderRightWidth = 3;
            pip.style.borderTopColor = pip.style.borderBottomColor = pip.style.borderLeftColor = pip.style.borderRightColor = color;
            pip.style.backgroundColor = Palette.Ink.WithA(0.95f);
            var tex = Art.StatusIcon(iconId);
            if (tex != null)
            {
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.style.position = Position.Absolute;
                img.style.left = img.style.top = img.style.right = img.style.bottom = Space.S0;
                img.style.backgroundImage = new StyleBackground(tex);
                img.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                pip.Add(img);
            }
            else
            {
                var l = new Label(letter) { pickingMode = PickingMode.Ignore };
                l.style.flexGrow = 1; l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.fontSize = size * 0.55f; l.style.color = color; Fonts.UseHeavy(l);
                pip.Add(l);
            }
            parent?.Add(pip);
            return pip;
        }
    }
}
