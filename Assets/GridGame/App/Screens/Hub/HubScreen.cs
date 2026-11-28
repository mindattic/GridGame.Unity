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

            // Party window (portraits, level, HP): a 2-column grid so a full party of six fits above the menu.
            // GG-T313: built as explicit two-card rows, not a flex-wrap of six cards sized by a reactive
            // GeometryChangedEvent pixel hack. That hack (party.contentRect.width-driven, with a 1px slack
            // fudge) measured correctly but still wrapped every card into a single column at the 2:3/3:4 tablet
            // frame (confirmed live: a hand-built 6-hero save showed 1 column there and 2 everywhere else on
            // an otherwise identical 1424x2532 stage) - some layout pass during that frame transform resolved
            // the wrap decision against different available width than the one the callback later measured.
            // A row that can only ever hold two cards has no wrap arithmetic to get wrong.
            var party = Ui.Window(null, col);
            party.name = "camp-party";
            // Edge = gap = 24 (row padding 12 + card margin 12): even gutters at every width (POL-C8), and
            // 24 px between the tappable cards (POL-C3). The 12 sits on each card row, not the window, so the row's
            // own box carries it.
            party.style.paddingLeft = party.style.paddingRight = Space.S0;
            var ids = s.Party;
            // Two cards abreast on a phone; three on a 2:3 / 3:4 tablet, which has the width for them (GG-T398).
            int perRow = Fit.Roomy ? 3 : 2;
            for (int i = 0; i < ids.Count; i += perRow)
            {
                var row = Ui.El("row", party);
                row.style.paddingLeft = row.style.paddingRight = Space.TargetHalfGap;
                for (int j = i; j < Math.Min(i + perRow, ids.Count); j++)
                {
                    var id = ids[j];
                    var h = s.Hero(id);
                    if (h == null) continue;
                    var u = Progression.Preview(c, h);
                    var card = Ui.El("card", row);
                    card.name = "camp-hero-" + id;
                    // Two cards split the row exactly in half; a lone trailing card keeps its half (a hidden twin, below).
                    card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = Length.Percent(0);
                    card.style.marginTop = card.style.marginBottom = card.style.marginLeft = card.style.marginRight = Space.TargetHalfGap;
                    card.style.paddingTop = card.style.paddingBottom = card.style.paddingLeft = card.style.paddingRight = Space.S8;
                    var thumb = Ui.Thumb(id, card, Kit.Avatar);
                    thumb.style.marginRight = Space.S16;
                    var info = Ui.El("col grow", card);
                    info.style.flexShrink = 1; info.style.minWidth = 0;
                    var top = Ui.El("row", info);
                    // A compact card (kit, GG-T454): name and meta caption size, the name heavy — the Heroes roster strip's look.
                    var name = Ui.Caption(c.Class(id).Name, top, "t-heavy");
                    Ui.Marquee(name);
                    // The name has its row to itself, so a long one is never cut by the badge (GG-T395).
                    name.style.flexGrow = 1; name.style.flexShrink = 1; name.style.minWidth = 0;
                    var meta = Ui.El("row", info);
                    meta.style.alignItems = Align.Center;
                    Ui.RoleBadge(Ui.RoleOf(h, c.Class(id)), meta).style.marginLeft = Space.S0;
                    Ui.Caption($"Lv {u.Level}", meta, "gold").style.marginLeft = Space.S8;
                    int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp;
                    Ui.Gauge(hp / (float)u.MaxHp, info, 300).style.marginTop = Space.S8;
                    Ui.Caption($"{hp}/{u.MaxHp}", info, "dim");
                    MercenaryCamp.Tag(s, id, info);
                    // A short last row's cards keep their column width: invisible twins hold the empty columns, so the
                    // window stays an even grid with even gutters (POL-C8) at every party size.
                    for (int gap = j == ids.Count - 1 ? (perRow - ids.Count % perRow) % perRow : 0; gap > 0; gap--)
                    {
                        var spacer = Ui.El("card", row);
                        spacer.style.flexGrow = 1; spacer.style.flexShrink = 1; spacer.style.flexBasis = Length.Percent(0);
                        spacer.style.marginTop = spacer.style.marginBottom = spacer.style.marginLeft = spacer.style.marginRight = Space.TargetHalfGap;
                        spacer.style.visibility = Visibility.Hidden;
                        spacer.pickingMode = PickingMode.Ignore;
                    }
                    card.AddToClassList("card-tap");
                    Ui.Tappable(card, () => App.Router.Push(new StatusScreen(id)), "ui.select");
                }
            }

            var dests = CampMenu.Build(c, s, App.Settings);
            CampDestination Dest(string id) => dests.FirstOrDefault(d => d.Id == id);
            // The Inn and the Respec sit with the party they heal and refund (GG-T1612).
            var inn = Dest(CampMenu.Inn);
            if (inn != null)
            {
                var innRow = Ui.MenuRow(inn.Label, inn.Value, inn.Open ? (Action)Rest : null, party, null, inn.Open);
                innRow.name = (inn.Open ? "camp-" : "locked-") + "inn";
                innRow.KindIcon(inn.Open ? RowKind.Keeper : RowKind.Locked, inn.Open ? CampNpcs.Innkeeper + "-00" : null);
                if (!inn.Open) Subline(innRow, inn.Hint);
            }
            var respec = Dest(CampMenu.Respec);
            if (respec != null)
            {
                var respecRow = Ui.MenuRow(respec.Label, respec.Value, () => App.Router.Push(new RespecScreen()), party);
                respecRow.name = "camp-respec";
                respecRow.KindIcon(RowKind.Keeper, CampNpcs.Innkeeper + "-00");
            }

            var body = new ScrollView();
            body.style.flexGrow = 1;
            col.Add(body);
            var page = body.contentContainer;

            // Campaign card: Continue launches the next stage straight away; the Journey row opens Stage select.
            var campaign = Ui.Window(null, page);
            campaign.name = "camp-campaign";
            string next = NextStage(c, s);
            var cont = Ui.Button(next != null ? "Continue" : "Journey", () =>
            {
                if (next != null) StageLauncher.Launch(next, false);
                else App.Router.Push(new StageSelectScreen());
            }, null, campaign);
            cont.name = "camp-continue";
            if (next != null)
            {
                var st = c.TryStage(next);
                if (st != null) Ui.Caption(st.Name, campaign, "gold");
                if (!RestedPool.Exempt(s)) Ui.Caption($"Costs {RestedPool.LaunchCost} Rested", campaign, "dim");
            }
            if (TravelRules.OnTheRoad(s)) // GG-T1786: travel (§28) replaces the expedition line
                Ui.Caption(TravelLine(s), campaign, "dim").name = "camp-travel";
            var adv = Dest(CampMenu.Campaign);
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
            RestedGate.PanelIn(campaign, Refresh);

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
