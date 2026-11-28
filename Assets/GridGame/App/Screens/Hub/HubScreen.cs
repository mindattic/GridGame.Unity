using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed class HubScreen : GameScreen
    {
        public override string Id => "hub";
        /// <summary>GG-T456: the Camp's peaceful track, redrawn after each trip to the field.</summary>
        public override string Music => App.Playlist.Hub(App.Content.Campaign) ?? "minstrel-guild-00";
        /// <summary>The bottom nav shows on the hub root too, Home lit (BIBLE §11.3, GG-T1963).</summary>
        public override bool ShowsNav => true;

        protected override void Build(VisualElement root)
        {
            var c = App.Content; var s = App.Save;
            // Paywall jobs left running when the app closed land now (§25.18), unless one is playing out on screen.
            if (App.Overlay.Q(TimedJobView.VeilName) == null)
                foreach (var done in Paywall.FinishReady(c, s, DateTime.UtcNow)) { App.Toast(done.Message ?? "Done"); App.Persist(); }
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, ThemeFor(c, s), 2);
            bg.style.unityBackgroundImageTintColor = Palette.TextDim;

            var frame = Ui.El("fill col", root);
            var col = Ui.El("col grow", frame);
            // Camp's header is a corner bar, not a title bar: the hamburger top-left, gold (and shards) top-right (GG-T1845);
            // its windows keep the kit's gutters (Kit.Gutter at the sides, Kit.Gap at the bottom).
            col.style.paddingTop = Space.S8; col.style.paddingLeft = col.style.paddingRight = Kit.Gutter - Space.S8; col.style.paddingBottom = Kit.Gap - Space.S8;

            var corners = Ui.El("row", col);
            corners.name = "camp-corners";
            corners.style.alignItems = Align.Center;
            corners.style.flexShrink = 0;
            corners.style.justifyContent = Justify.SpaceBetween;
            RowIcon.HamburgerButton(() => OpenMenu(), corners);
            var currencies = Ui.El("header-currencies", corners);
            currencies.name = "header-currencies";
            Ui.Currencies(currencies, s, () => App.Router.PushOrReturn(ShopHubScreen.OnShards()));

            // Party strip (UX review §2.2, GG-T1962): one row of hero thumbnails with an HP ring each; a tap opens that hero's
            // Status. Full art waits for a tap there (R9), so the strip stays a single short row at any party size.
            var party = Ui.El("row", col);
            party.name = "camp-party";
            party.style.flexShrink = 0;
            party.style.justifyContent = Justify.SpaceEvenly;
            party.style.marginTop = party.style.marginBottom = Space.S8;
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                if (h == null) continue;
                var u = Progression.Preview(c, h);
                int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp;
                var slot = Ui.El("col", party);
                slot.name = "camp-hero-" + id;
                slot.style.alignItems = Align.Center;
                var ring = Ui.El(null, slot);
                ring.style.width = ring.style.height = Size.Thumb + 2 * Space.S16;
                var thumb = Ui.Thumb(id, ring, Size.Thumb);
                thumb.style.position = Position.Absolute;
                thumb.style.left = thumb.style.top = Space.S16;
                ring.Add(new HpRing(hp / (float)u.MaxHp));
                Ui.Caption($"Lv {u.Level}", slot, "gold");
                MercenaryCamp.Tag(s, id, slot);
                Ui.Tappable(slot, () => App.Router.Push(new StatusScreen(id)), "ui.select");
            }

            var dests = CampMenu.Build(c, s, App.Settings);
            CampDestination Dest(string id) => dests.FirstOrDefault(d => d.Id == id);
            // Mode banner (GG-T1962): names the road-or-camp mode (TravelRules) in one line and carries its one action, the
            // Inn's rest: "Set up camp" on the road, "Rest" once camped (GG-T1612, GG-T1786).
            bool road = TravelRules.OnTheRoad(s);
            int wounded = s.Party.Select(s.Hero).Count(h => h != null && GridGame.Meta.Inn.MissingHp(c, s, h) > 0);
            var inn = Dest(CampMenu.Inn);
            var respec = Dest(CampMenu.Respec);
            var banner = Ui.Window(null, col);
            banner.name = "camp-mode";
            banner.style.flexShrink = 0;
            var modeRow = Ui.El("row", banner);
            modeRow.style.alignItems = Align.Center;
            var mode = Ui.Para(ModeLine(s, wounded, inn != null && inn.Open, respec != null), modeRow);
            mode.name = "camp-travel";
            mode.style.flexGrow = 1; mode.style.flexShrink = 1; mode.style.minWidth = 0;
            if (inn != null && inn.Open)
            {
                var rest = Ui.Button(road ? "Set up camp" : "Rest", Rest, "btn--small", modeRow, true);
                rest.name = "camp-inn";
                rest.style.marginLeft = Space.S16;
            }
            if (respec != null)
            {
                var respecBtn = Ui.Button("Respec", () => App.Router.Push(new RespecScreen()), "btn--small", modeRow);
                respecBtn.name = "camp-respec";
                respecBtn.style.marginLeft = Space.S24;
            }
            if (inn != null && !inn.Open && !string.IsNullOrEmpty(inn.Hint)) Ui.Note(inn.Hint, banner).name = "locked-inn";

            // The NEXT card anchors to the bottom of the body, so its one filled Fight button sits in the thumb zone above
            // the tiles and nav (DESIGN_SYSTEM §5); a short screen scrolls instead of clipping.
            var body = new ScrollView();
            body.style.flexGrow = 1;
            col.Add(body);
            var page = body.contentContainer;
            page.style.justifyContent = Justify.FlexEnd;
            body.contentViewport.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float vh = body.contentViewport.layout.height;
                if (!float.IsNaN(vh) && vh > 0f) page.style.minHeight = Mathf.Floor(vh);
            });

            var campaign = Ui.Window(null, page);
            campaign.name = "camp-campaign";
            string next = NextStage(c, s);
            var adv = Dest(CampMenu.Campaign);
            var st = next != null ? c.TryStage(next) : null;
            Ui.Section("Next", campaign);
            if (st != null)
            {
                var title = Ui.Heading(st.Name, campaign);
                title.name = "camp-next";
                Ui.Caption($"Recommended Lv {Progression.StageRecommendedLevel(c, s, st)}", campaign, "dim");
            }
            var cont = Ui.Button(next != null ? "Fight" : adv?.Label ?? "Journey", () =>
            {
                if (next != null) StageLauncher.Launch(next, false);
                else App.Router.Push(new StageSelectScreen());
            }, "btn--primary", campaign);
            cont.name = "camp-continue";
            cont.style.marginTop = Space.S16;
            if (next != null && !RestedPool.Exempt(s))
                Ui.Caption($"Costs {RestedPool.LaunchCost} Rested", campaign, "dim").name = "camp-fight-cost";
            // Rested shows here only while it gates: below full, its meter, timer and refills (§25.18).
            if (!RestedPool.Exempt(s) && RestedPool.Points(s, DateTime.UtcNow) < RestedPool.Max) RestedGate.PanelIn(campaign, Refresh);
            var advRow = Ui.MenuRow(adv?.Label ?? "Journey", adv?.Value ?? "", () => App.Router.Push(new StageSelectScreen()), campaign);
            advRow.name = "camp-adventure";
            advRow.KindIcon(RowKind.Place, ThemeFor(c, s));
            Ui.Caption($"{TimeSpan.FromSeconds(s.PlaySeconds):h\\:mm\\:ss} · Cleared {s.ClearedStages.Count(id => c.Stages.ContainsKey(id) && !c.Stage(id).IsTest)}/{c.CampaignStages().Count}", campaign, "dim");
            if (s.NewGamePlus > 0) Ui.Caption($"New Game+ {s.NewGamePlus}", campaign, "gold");
            var ngp = Dest(CampMenu.NewGamePlus);
            if (ngp != null)
            {
                var ngpRow = Ui.MenuRow(ngp.Label, "", () => Confirm.Show("Start New Game+? Keep your heroes, gear and gold; the campaign restarts with enemies 10 levels stronger.", "Begin", () =>
                {
                    var r = Progression.StartNewGamePlus(App.Content, App.Save);
                    App.Toast(r.Message);
                    if (r.Ok) { App.Persist(); Refresh(); }
                }), campaign);
                ngpRow.name = "camp-newgameplus";
                ngpRow.KindIcon(RowKind.NewGamePlus).parent.Q<Label>("label").AddToClassList("gold");
            }

            // Crucible card (§22.10): endless waves; closed mid-expedition or before its milestone, saying why.
            var crucible = Dest(CampMenu.Crucible);
            if (crucible != null)
            {
                var card = Ui.Window(null, page);
                var row = Ui.MenuRow(crucible.Label, crucible.Value, crucible.Open ? (Action)(() => App.Router.Push(new CrucibleScreen())) : null, card, null, crucible.Open);
                row.name = crucible.Open ? "camp-crucible" : "locked-crucible";
                row.KindIcon(crucible.Open ? RowKind.Crucible : RowKind.Locked);
                if (!crucible.Open) Subline(row, crucible.Hint);
            }

            // The tiles are the two groups the nav has no tab for (GG-T1963): Craft and Journal, each opening a CampGroupScreen of
            // its members (GG-T1613). Every destination keeps its old Camp row name as a hidden full-size marker, so camp tips,
            // the coach and the harness still find it on the hub: inside its group tile, or over the nav tab that now opens it.
            //   camp-craft:   camp-items, camp-blacksmith, camp-alchemist, camp-manasmith, camp-enchantress, camp-caravan
            //   camp-journal: camp-quests, camp-bounties, camp-talk, camp-codex, camp-bestiary
            //   nav-heroes:   camp-heroes;  nav-summon: camp-summon-circle, camp-mercenaries;  nav-shop: camp-merchant, camp-diamonds
            // (each locked-<name> while that member is closed).
            var grid = Ui.El("kit-pillar", page);
            grid.name = "camp-tiles";
            var nav = Ui.NavBar(frame, this);
            var groups = new Dictionary<CampGroup, VisualElement>();
            foreach (var d in dests)
            {
                VisualElement host;
                if (d.Group == CampGroup.Craft || d.Group == CampGroup.Journal)
                {
                    if (!groups.TryGetValue(d.Group, out host)) groups[d.Group] = host = GroupTile(grid, dests, d.Group);
                }
                else
                {
                    string tab = d.Id == CampMenu.Heroes ? CampMenu.Heroes : d.Group == CampGroup.Summon ? CampMenu.Summon : d.Group == CampGroup.Shop ? CampMenu.Shop : null;
                    host = tab != null ? nav.Q("nav-" + tab) : null;
                    if (host == null) continue;
                }
                var marker = Ui.El(null, host);
                marker.name = TileName(d);
                marker.style.position = Position.Absolute;
                marker.style.left = marker.style.top = marker.style.right = marker.style.bottom = Space.S0;
                marker.style.visibility = Visibility.Hidden;
                marker.pickingMode = PickingMode.Ignore;
            }
            Ui.Select(cont);

            // GG-T75 (§25.12.1): once the Caravan station is open, a rotating one-line crew bark under the tiles.
            if (Features.Caravan && CampUnlocks.CaravanOpen(s))
            {
                var gossip = Ui.Panel(page, null, false, "camp-gossip");
                Ui.Caption("Around the wagons", gossip, "dim");
                CaravanScreen.BarkLine(c, s, gossip);
            }

            // GG-T518: a closed destination's hint sits whole, wrapping if it must, in small type under the row's name.
            void Subline(VisualElement row, string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                var name = row.Q<Label>("label");
                var stack = Ui.El("col", row);
                stack.style.flexGrow = 1f; stack.style.flexShrink = 1f;
                row.Insert(row.IndexOf(name), stack);
                stack.Add(name);
                var sub = Ui.Text(text, "small dim", stack);
                sub.name = "hint";
                sub.style.whiteSpace = WhiteSpace.Normal;
            }
            MercenaryCamp.OnCamp(this);
            CampCoach.OnHub(this); // GG-T539: a vendor who just joined, or a first piece of gear to put on, gets its lesson
        }

        /// <summary>The hamburger's menu (GG-T1845): Settings and Save &amp; Quit, kept off the tile grid so the hub fits one screen
        /// and Quit is out of the thumb sweep. A veil on the global Overlay, closed by Resume.</summary>
        private void OpenMenu()
        {
            if (App.Overlay.Q("camp-menu-veil") != null) return;
            var veil = Ui.El("fill veil col center", App.Overlay);
            veil.name = "camp-menu-veil";
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Window("Menu", veil);
            w.style.width = Length.Percent(72);
            void Close() => veil.RemoveFromHierarchy();
            var settings = Ui.MenuRow("Settings", "", () => { Close(); App.Router.Push(new SettingsScreen()); }, w);
            settings.name = "camp-settings";
            settings.KindIcon(RowKind.Settings);
            var quit = Ui.MenuRow("Save & Quit", "", () => { Close(); Confirm.Show("Save and return to the title?", "Quit", ToTitle); }, w);
            quit.name = "camp-save-&-quit";
            quit.KindIcon(RowKind.Quit);
            Ui.MenuRow("Resume", "", Close, w).KindIcon(RowKind.Enter);
            Ui.Select(settings);
        }

        /// <summary>The stage Continue launches: the first uncleared campaign stage. Travel (§28, GG-T1786) has no fixed
        /// battle order inside a region, so a stage mid-region is fought directly once it is unlocked.</summary>
        internal static string NextStage(ContentDb c, SaveData s) =>
            c.CampaignStages().FirstOrDefault(st => !s.ClearedStages.Contains(st.Id))?.Id;

        /// <summary>GG-T1786: "On the road · N mana carried": the caravan has set out and has not made camp.</summary>
        internal static string TravelLine(SaveData s)
        {
            int n = TravelRules.StartMana(s);
            return "On the road · " + (n == 0 ? "no mana carried" : $"{n} mana carried");
        }

        /// <summary>GG-T1962: the mode banner's line. On the road, wounds carry; once camped, the Inn (and its Respec) is open.</summary>
        internal static string ModeLine(SaveData s, int wounded, bool innOpen, bool respecOpen)
        {
            if (!TravelRules.OnTheRoad(s)) return respecOpen ? "Camped · Inn and Respec open" : "Camped";
            if (wounded > 0) return $"On the road · {wounded} {(wounded == 1 ? "hero" : "heroes")} wounded" + (innOpen ? " · Set up camp to heal" : "");
            int n = TravelRules.StartMana(s);
            return "On the road · " + (n == 0 ? "wounds carry" : $"{n} mana carried · wounds carry");
        }

        /// <summary>GG-T1962: the party strip's HP ring around a thumbnail: a pewter track and an arc for the HP left,
        /// good, warn or bad by how much is left (the arc's length carries it too, never colour alone).</summary>
        private sealed class HpRing : VisualElement
        {
            public HpRing(float frac)
            {
                frac = Mathf.Clamp01(frac);
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = style.top = style.right = style.bottom = Space.S0;
                generateVisualContent += ctx =>
                {
                    var r = contentRect;
                    var st = ShapeStyle.Small.Thick(Size.Track);
                    float radius = Mathf.Min(r.width, r.height) / 2f - st.Width / 2f - st.Outline;
                    if (radius <= 0f) return;
                    Shapes.Ring(ctx.painter2D, r.center, radius, st.Tint(Palette.PewterDark));
                    if (frac <= 0f) return;
                    var tone = frac > 0.5f ? Palette.Good : frac > 0.25f ? Palette.Warn : Palette.Bad;
                    Shapes.Ring(ctx.painter2D, r.center, radius, st.Tint(tone), -90f, 360f * frac);
                };
            }
        }

        /// <summary>A destination's tile name: camp-&lt;old row&gt; when open, locked-&lt;old row&gt; when closed.</summary>
        internal static string TileName(CampDestination d) =>
            (d.Open ? "camp-" : "locked-") + (d.Id == CampMenu.Diamonds ? "diamonds" : d.Label.ToLowerInvariant().Replace(' ', '-'));

        /// <summary>One destination's tile in <paramref name="grid"/>: opens its screen, or toasts why it's closed.</summary>
        internal static VisualElement Tile(VisualElement grid, CampDestination d)
        {
            var tile = Ui.El("kit-tile", grid);
            tile.name = TileName(d);
            tile.EnableInClassList("kit-tile--locked", !d.Open);
            var icon = d.Id != CampMenu.Shop && d.Npc != null ? tile.KindIcon(RowKind.Keeper, d.Npc + "-00") : tile.KindIcon(TileKind(d.Id), Art.UiIcon(TileIcon(d.Id)));
            if (!d.Open) RowIcon.Badge(icon, RowKind.Locked);
            else if (d.Badge) RowIcon.Badge(icon, RowKind.New);
            Ui.Marquee(Ui.Text(d.Label, "kit-tile-label", tile));
            if (!string.IsNullOrEmpty(d.Value)) Ui.Text(d.Value, "kit-tile-value", tile).name = "value";
            else if (!d.Open && !string.IsNullOrEmpty(d.Hint)) Ui.Text(d.Hint, "kit-tile-value", tile).name = "hint";
            string id = d.Id, hint = d.Hint;
            if (d.Open) Ui.Tappable(tile, () => { var dest = Ui.NavDestination(App.Save, id); if (dest != null) App.Router.PushOrReturn(dest); }, "ui.select");
            else Ui.Tappable(tile, () => App.Toast(hint), "ui.error");
            return tile;
        }

        /// <summary>The Craft or Journal tile: open if any member is, badged if any member waits, else dimmed with the
        /// group's first hint; it opens the group's <see cref="CampGroupScreen"/>.</summary>
        private static VisualElement GroupTile(VisualElement grid, List<CampDestination> dests, CampGroup group)
        {
            bool open = CampMenu.GroupOpen(dests, group);
            string label = group == CampGroup.Craft ? "Craft" : "Journal", hint = CampMenu.GroupHint(dests, group);
            var tile = Ui.El("kit-tile", grid);
            tile.name = (open ? "camp-" : "locked-") + label.ToLowerInvariant();
            tile.EnableInClassList("kit-tile--locked", !open);
            var icon = tile.KindIcon(group == CampGroup.Craft ? RowKind.Craft : RowKind.Codex, Art.UiIcon(group == CampGroup.Craft ? "hub-craft" : "hub-journal"));
            if (!open) RowIcon.Badge(icon, RowKind.Locked);
            else if (CampMenu.GroupBadge(dests, group)) RowIcon.Badge(icon, RowKind.New);
            Ui.Marquee(Ui.Text(label, "kit-tile-label", tile));
            if (!open && !string.IsNullOrEmpty(hint)) Ui.Text(hint, "kit-tile-value", tile).name = "hint";
            if (open) Ui.Tappable(tile, () => App.Router.PushOrReturn(new CampGroupScreen(group)), "ui.select");
            else Ui.Tappable(tile, () => App.Toast(hint), "ui.error");
            return tile;
        }

        /// <summary>A destination's icon in the menu set (docs/ICONS.md, hub tiles and stations).</summary>
        internal static string TileIcon(string id)
        {
            switch (id)
            {
                case CampMenu.Campaign: return "hub-stage-select";
                case CampMenu.Diamonds: return "hub-diamond-store";
                case CampMenu.NewGamePlus: return "hub-new-game-plus";
                case CampMenu.Quit: return "hub-quit-to-title";
                case CampMenu.Blacksmith: case CampMenu.Alchemist: case CampMenu.Enchantress: case CampMenu.Aetherist: case CampMenu.Caravan:
                    return "station-" + id;
                default: return "hub-" + id;
            }
        }

        private static RowKind TileKind(string id)
        {
            switch (id)
            {
                case CampMenu.Shop: return RowKind.Shop;
                case CampMenu.Heroes: case CampMenu.Mercenaries: return RowKind.Hero;
                case CampMenu.Items: return RowKind.Item;
                case CampMenu.Talk: return RowKind.Talk;
                case CampMenu.Bounties: return RowKind.Bounty;
                case CampMenu.Quests: return RowKind.Quest;
                case CampMenu.Diamonds: return RowKind.Diamond;
                case CampMenu.Bestiary: return RowKind.Bestiary;
                case CampMenu.Codex: return RowKind.Codex;
                case CampMenu.Settings: return RowKind.Settings;
                case CampMenu.Summon: return RowKind.Summon;
                default: return RowKind.Station;
            }
        }

        private void Rest()
        {
            TravelRules.MakeCamp(App.Save); // GG-T1790: camp rest, free; on the road the caravan makes camp first
            var r = CampRestService.Rest(App.Save, out _);
            App.Toast(r.Message);
            App.Audio.Sfx(r.Ok ? "battle.heal" : "ui.error");
            if (r.Ok) { App.Persist(); Refresh(); }
        }

        /// <summary>GG-T1335: Back at the hub root asks before it saves and goes to the Title; a second Back answers Cancel.</summary>
        public override bool OnBack()
        {
            Confirm.Show("Return to the title? Your progress is saved.", "Title", ToTitle);
            return false;
        }

        internal static void ToTitle()
        {
            App.Persist(); App.Save = null; App.Slot = -1; App.Router.ResetTo(new TitleScreen());
        }

        public static string ThemeFor(ContentDb c, SaveData s)
        {
            var list = c.CampaignStages();
            if (list.Count == 0) return "greenvalley";
            var st = list[Mathf.Clamp(s.HighestClearedStage + 1, 0, list.Count - 1)];
            return st.Theme ?? "greenvalley";
        }
    }
}
