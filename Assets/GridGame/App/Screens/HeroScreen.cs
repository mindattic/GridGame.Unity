using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Heroes: one tabbed screen for Status / Equip / Abilities (+ Talents) / Party (§25.0, GG-T435)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>One tab of the <see cref="HeroScreen"/>. It renders into the screen's body column and reads and writes the
    /// screen's selected hero, so every tab shows the same hero and a hero picked on one tab is still picked on the next.</summary>
    public abstract class HeroTab
    {
        internal HeroScreen Host;
        /// <summary>The screen id while this tab is up (the automation `goto`/`-start` names and the camp tip stations).</summary>
        public abstract string Id { get; }
        /// <summary>The camp tip this tab shows on opening (its own <see cref="Id"/> by default); null = none.</summary>
        public virtual string TipStation => Id;
        /// <summary>False for a tab that lists every hero itself (Party), so the roster strip above it is left out.</summary>
        public virtual bool ShowsPicker => true;
        /// <summary>Runs before the roster strip draws, so a tab that picks its own hero (Abilities in onboarding) is marked.</summary>
        public virtual void Prepare() { }
        /// <summary>Forgets per-hero picks (a previewed piece, a selected node) when another hero is picked.</summary>
        public virtual void OnHeroChanged() { }
        public abstract void BuildBody(VisualElement body);

        protected static GameApp App => GameApp.I;
        protected ContentDb C => App.Content;
        protected SaveData S => App.Save;
        protected string HeroId { get => Host.HeroId; set => Host.HeroId = value; }
        protected HeroSave Hero => Host.Hero;
        protected void Refresh() => Host.Refresh();
        protected void Done(Result r, bool persist = true, string cue = "ui.buy") => Host.TabDone(r, persist, cue);
    }

    /// <summary>
    /// The Hero screen (GG-T435, §25.0): Camp's single <b>Heroes</b> row in place of Status, Equip, Abilities, Talents and
    /// Party. Tabs <b>Status | Equip | Abilities | Party</b> over a roster strip (the party first and marked), then the tab's
    /// own body. Abilities keeps its sub-tabs and gains <b>Talents</b> (the skill trees with the per-hero Respec). The old
    /// screen classes are thin wrappers that open this screen on their tab, so every place that pushed one still works.
    /// </summary>
    public class HeroScreen : HubPage
    {
        public const int StatusTabIndex = 0, EquipTabIndex = 1, AbilitiesTabIndex = 2, PartyTabIndex = 3;
        private static readonly string[] TabNames = { "Status", "Equip", "Abilities", "Party" };

        internal string HeroId;
        private int tab;
        private readonly HeroTab[] tabs;

        public HeroScreen(int tab = StatusTabIndex, string heroId = null, int abilitiesSubTab = -1, (int Q, int R)? boardNode = null)
        {
            this.tab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            HeroId = heroId;
            tabs = new HeroTab[] { new StatusTab(), new EquipTab(), new AbilitiesTab(abilitiesSubTab, boardNode), new PartyTab() };
            foreach (var t in tabs) t.Host = this;
        }

        /// <summary>Heroes › Abilities › Board for this hero with the route to <paramref name="next"/> preselected (GG-T1404).</summary>
        public static HeroScreen Learn(string heroId, NextAbility.Pick next) =>
            new HeroScreen(AbilitiesTabIndex, heroId, AbilitiesTab.TalentsSub, next?.Node);

        private HeroTab Active => tabs[tab];
        internal HeroSave Hero => S.Hero(HeroId) ?? S.Roster.FirstOrDefault();

        /// <summary>The active tab's id ("status", "equip", "abilities", "talents", "party"): the automation's screen check and
        /// the camp tips key off it exactly as they did off the separate screens.</summary>
        public override string Id => Active.Id;
        /// <summary>GG-T1206: the roster and party picker's heroes (party and benched) load into its own Screen art scope, released when it closes.</summary>
        public override bool HoldsScreenArt => true;
        protected override string Title => "Heroes";
        protected override string TipStation => Active.TipStation;

        protected override void Build(VisualElement root)
        {
            // Switching tabs rebuilds in place, so the root's "screen-<id>" name follows the tab.
            root.name = "screen-" + Id;
            base.Build(root);
        }

        internal void TabDone(Result r, bool persist, string cue) => Done(r, persist, cue);

        protected override void BuildBody(VisualElement body)
        {
            Active.Prepare();
            HeroId = Hero?.ClassId;
            Ui.Tabs(TabNames, tab, i => { tab = i; Refresh(); }, body).name = "hero-tabs";
            if (Active.ShowsPicker && Hero != null) Picker(body);
            Active.BuildBody(body);
        }

        /// <summary>The roster strip: one card per hero (portrait, name, level), the party first and marked, the picked hero
        /// highlighted. It scrolls sideways when the roster is wider than the column and its cards share the row when not.</summary>
        private void Picker(VisualElement body)
        {
            var w = Ui.Window(null, body);
            w.name = "hero-picker";
            w.style.marginTop = Space.S0; // sits on the tab strip
            // Edge = gap = 24 (row padding 12 + card margin 12): the Camp party window's even gutters (POL-C8). The 12 sits
            // on the scrolled row, not the window, so the row's own box shows it and scrolled cards reach the window edge.
            w.style.paddingLeft = w.style.paddingRight = Space.S0;
            w.style.paddingTop = w.style.paddingBottom = Space.S8;
            var strip = new ScrollView(ScrollViewMode.Vertical) { name = "hero-strip" };  // GG-T760: wraps to more rows instead of scrolling sideways
            strip.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            w.Add(strip);
            var row = strip.contentContainer;
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.minWidth = Length.Percent(100);
            row.style.paddingLeft = row.style.paddingRight = Space.TargetHalfGap;

            var heroes = S.Party.Select(S.Hero).Where(h => h != null)
                .Concat(S.Roster.Where(h => !S.Party.Contains(h.ClassId))).ToList();
            VisualElement picked = null;
            foreach (var h in heroes)
            {
                string id = h.ClassId;
                bool inParty = S.Party.Contains(id);
                bool sel = id == HeroId;
                var card = Ui.El("card" + (sel ? " card--selected" : ""), row);
                card.name = "hero-pick-" + id;
                card.style.flexGrow = 1; card.style.flexShrink = 0; card.style.flexBasis = Length.Percent(26); // three to a row, more wrap below (GG-T760)
                card.style.marginTop = card.style.marginBottom = card.style.marginLeft = card.style.marginRight = Space.TargetHalfGap;
                card.style.paddingTop = card.style.paddingBottom = card.style.paddingLeft = card.style.paddingRight = Space.S8;
                var bw = sel ? 4f : 0f;
                card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = bw;
                card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = Palette.Gold;
                var thumb = Ui.Thumb(id, card, Kit.Avatar);
                thumb.style.marginRight = Space.S16;
                bool ready = NextAbility.Ready(C, S, id);
                if (ready) RowIcon.Badge(thumb, RowKind.New);
                var info = Ui.El("col grow", card);
                info.style.flexShrink = 1; info.style.minWidth = 0;
                // A compact card (kit, GG-T454): name and meta both caption size, the name heavy.
                var name = Ui.Caption(C.TryClass(id)?.Name ?? id, info, "t-heavy");
                Ui.Marquee(name);
                var meta = Ui.El("row", info);
                meta.style.alignItems = Align.Center;
                Ui.Caption($"Lv {Progression.Level(h)}", meta, "gold");
                var tag = Ui.Caption(inParty ? "Party" : "Reserve", meta, inParty ? "good" : "dim");
                tag.style.marginLeft = Space.S8;
                Ui.Marquee(tag);
                if (ready)
                {
                    var badge = Ui.Caption(NextAbility.ReadyLabel, info, "gold");
                    badge.name = "hero-ability-ready";
                    Ui.Marquee(badge);
                }
                card.AddToClassList("card-tap");
                Ui.Tappable(card, () =>
                {
                    if (HeroId == id) return;
                    HeroId = id;
                    foreach (var t in tabs) t.OnHeroChanged();
                    Refresh();
                }, "ui.select");
                if (sel) picked = card;
            }
            // Keep the picked hero in view when the roster runs taller than the strip.
            if (picked != null) strip.schedule.Execute(() => strip.ScrollTo(picked)).ExecuteLater(30);
        }
    }

    /// <summary>Opens the <see cref="HeroScreen"/> on Status (Camp's party cards, `-start status`).</summary>
    public sealed class StatusScreen : HeroScreen
    {
        public StatusScreen(string heroId) : base(StatusTabIndex, heroId) { }
    }

    /// <summary>Opens the <see cref="HeroScreen"/> on Equip (`-start equip`).</summary>
    public sealed class EquipScreen : HeroScreen
    {
        public EquipScreen(string heroId) : base(EquipTabIndex, heroId) { }
    }

    /// <summary>Opens the <see cref="HeroScreen"/> on Abilities (`-start abilities`; onboarding step 7 opens it on Learn).</summary>
    public sealed class AbilitiesScreen : HeroScreen
    {
        public AbilitiesScreen(string heroId) : base(AbilitiesTabIndex, heroId) { }
    }

    /// <summary>Opens the <see cref="HeroScreen"/> on Abilities › Talents (`-start talents`).</summary>
    public sealed class SkillTreeScreen : HeroScreen
    {
        public SkillTreeScreen(string heroId) : base(AbilitiesTabIndex, heroId, AbilitiesTab.TalentsSub) { }
    }

    /// <summary>Opens the <see cref="HeroScreen"/> on Party (the Crucible's "Change team", `-start party`).</summary>
    public sealed class PartyScreen : HeroScreen
    {
        public PartyScreen() : base(PartyTabIndex) { }
    }
}
