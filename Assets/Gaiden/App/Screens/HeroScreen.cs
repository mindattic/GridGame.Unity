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
        protected void Done(Result r, bool persist = true) => Host.TabDone(r, persist);
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

        public HeroScreen(int tab = StatusTabIndex, string heroId = null, int abilitiesSubTab = -1)
        {
            this.tab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            HeroId = heroId;
            tabs = new HeroTab[] { new StatusTab(), new EquipTab(), new AbilitiesTab(abilitiesSubTab), new PartyTab() };
            foreach (var t in tabs) t.Host = this;
        }

        private HeroTab Active => tabs[tab];
        internal HeroSave Hero => S.Hero(HeroId) ?? S.Roster.FirstOrDefault();

        /// <summary>The active tab's id ("status", "equip", "abilities", "talents", "party"): the automation's screen check and
        /// the camp tips key off it exactly as they did off the separate screens.</summary>
        public override string Id => Active.Id;
        protected override string Title => "Heroes";
        protected override string TipStation => Active.TipStation;

        protected override void Build(VisualElement root)
        {
            // Switching tabs rebuilds in place, so the root's "screen-<id>" name follows the tab.
            root.name = "screen-" + Id;
            base.Build(root);
        }

        internal void TabDone(Result r, bool persist) => Done(r, persist);

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
            // Edge = gap = 24 (window padding 12 + card margin 12): the Camp party window's even gutters (POL-C8).
            w.style.paddingLeft = w.style.paddingRight = Space.TargetHalfGap;
            w.style.paddingTop = w.style.paddingBottom = Space.S8;
            var strip = new ScrollView(ScrollViewMode.Horizontal) { name = "hero-strip" };
            strip.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            w.Add(strip);
            var row = strip.contentContainer;
            row.style.flexDirection = FlexDirection.Row;
            row.style.minWidth = Length.Percent(100);

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
                card.style.flexGrow = 1; card.style.flexShrink = 0; card.style.flexBasis = Size.Card;
                card.style.marginTop = card.style.marginBottom = card.style.marginLeft = card.style.marginRight = Space.TargetHalfGap;
                card.style.paddingTop = card.style.paddingBottom = card.style.paddingLeft = card.style.paddingRight = Space.S8;
                var bw = sel ? 4f : 0f;
                card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = bw;
                card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = Palette.Gold;
                Ui.Thumb(id, card, Size.Avatar).style.marginRight = Space.S16;
                var info = Ui.El("col grow", card);
                info.style.flexShrink = 1; info.style.minWidth = 0;
                var name = Ui.Text(C.TryClass(id)?.Name ?? id, "small", info);
                Fonts.UseHeavy(name);
                Ui.Marquee(name);
                var meta = Ui.El("row", info);
                meta.style.alignItems = Align.Center;
                Ui.Text($"Lv {Progression.Level(h)}", "tiny gold", meta);
                var tag = Ui.Text(inParty ? "Party" : "Reserve", inParty ? "tiny good" : "tiny dim", meta);
                tag.style.marginLeft = Space.S8;
                Ui.Marquee(tag);
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
            // Keep the picked hero in view when the roster runs wider than the strip.
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
