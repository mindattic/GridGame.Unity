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

        /// <summary>Compact roster cards the strip fits across before it scrolls sideways (GG-T1876).</summary>
        private const int PickerAcross = 5;

        /// <summary>The roster strip: one row of compact cards (thumbnail with the level on it, name under), the party first
        /// and reserves dimmed, the picked hero highlighted. Up to <see cref="PickerAcross"/> share the row; more scroll
        /// sideways (GG-T1876: the wrapped grid spent two rows of the tab on four heroes).</summary>
        private void Picker(VisualElement body)
        {
            var w = Ui.Panel(body, name: "hero-picker");
            Ui.Margin(w, top: Space.S0); // sits on the tab strip
            // Edge = gap = 24 (row padding 12 + card margin 12): the Camp party window's even gutters (POL-C8). The 12 sits
            // on the scrolled row, not the window, so the row's own box shows it and scrolled cards reach the window edge.
            Ui.Pad(w, Space.S0, Space.S8, Space.S0, Space.S8);
            var strip = new ScrollView(ScrollViewMode.Horizontal) { name = "hero-strip" };
            strip.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            w.Add(strip);
            var row = strip.contentContainer;
            row.style.flexDirection = FlexDirection.Row;
            row.style.minWidth = Length.Percent(100);
            Ui.Pad(row, left: Space.TargetHalfGap, right: Space.TargetHalfGap);
            float cardW = Mathf.Floor((Fit.Width - 2 * Kit.Gutter - 2 * Space.TargetHalfGap) / PickerAcross - 2 * Space.TargetHalfGap);

            var heroes = S.Party.Select(S.Hero).Where(h => h != null)
                .Concat(S.Roster.Where(h => !S.Party.Contains(h.ClassId))).ToList();
            VisualElement picked = null;
            foreach (var h in heroes)
            {
                string id = h.ClassId;
                bool inParty = S.Party.Contains(id);
                bool sel = id == HeroId;
                var card = Ui.El("card" + (sel ? " tap--selected" : ""), row); // R3: the picked hero pressed in
                card.name = "hero-pick-" + id;
                card.style.flexDirection = FlexDirection.Column;
                card.style.alignItems = Align.Center;
                card.style.flexGrow = 1; card.style.flexShrink = 0; card.style.flexBasis = cardW;
                Ui.Margin(card, Space.TargetHalfGap, Space.S16, Space.TargetHalfGap, Space.TargetHalfGap);
                Ui.Pad(card, Space.S8, Space.S8, Space.S8, Space.S8);
                var thumb = Ui.Thumb(id, card, Kit.Thumb);
                var lv = Ui.Badge($"Lv {Progression.Level(h)}", thumb, "gold");
                lv.name = "hero-level";
                lv.style.position = Position.Absolute;
                lv.style.left = lv.style.bottom = Space.S0;
                if (NextAbility.Ready(C, S, id)) RowIcon.Badge(thumb, RowKind.New);
                var name = Ui.Caption(C.TryClass(id)?.Name ?? id, card, inParty ? "t-heavy" : "t-heavy dim");
                Ui.Margin(name, top: Space.S8);
                name.style.alignSelf = Align.Stretch;
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                Ui.Marquee(name);
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

    /// <summary>The Heroes screens' set icons (docs/ICONS.md): a stat's icon beside its short name, an equipment slot's
    /// category icon beside its name.</summary>
    internal static class HeroIcons
    {
        /// <summary>The stat icon for a short stat name (STR, VIT, AGI, SPD, INT, WIS, LCK, HP); null for anything else.</summary>
        public static string Stat(string abbr)
        {
            switch (abbr)
            {
                case "STR": return "stat-strength";
                case "VIT": return "stat-vitality";
                case "AGI": return "stat-agility";
                case "SPD": return "stat-speed";
                case "INT": return "stat-intelligence";
                case "WIS": return "stat-wisdom";
                case "LCK": return "stat-luck";
                case "HP": return "stat-hp";
                default: return null;
            }
        }

        /// <summary>The item category icon of an equipment slot: weapon, armor or accessory.</summary>
        public static string Slot(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? "itemcat-weapon" : System.Array.IndexOf(GridGame.Core.Battle.BattleEngine.ArmorSlots, slot) >= 0 ? "itemcat-armor" : "itemcat-accessory";

        /// <summary>A stat's short name led by its icon, in a line <paramref name="width"/> wide (the icon's room included).</summary>
        public static Label StatLabel(string abbr, VisualElement parent, float width, string classes = "dim")
        {
            var id = Stat(abbr);
            var line = id != null ? RowIcon.Line(id, parent) : parent;
            var label = Ui.Caption(abbr, line, classes);
            (id != null ? line : label).style.width = width;
            return label;
        }

        /// <summary>The width of a stat name column that leads with its inline icon.</summary>
        public const float StatColumn = Size.Column + Size.IconInline + Space.S8;
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
