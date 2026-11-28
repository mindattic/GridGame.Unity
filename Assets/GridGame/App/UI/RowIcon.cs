using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// What a tappable thing is (GG-T712/GG-T714, owner 2026-11-23: "every item or place or selectable must be prefixed with
    /// a icon so the user knows what they are pressing"). Each kind has one icon (BIBLE §26.7, the kind table): its real art
    /// where art exists, else its icon from the menu set (<see cref="RowIcon.IconId"/>, docs/ICONS.md, GG-T2051).
    /// </summary>
    public enum RowKind
    {
        /// <summary>A region on the map: its board tile (<c>Tiles/&lt;theme&gt;-00</c>), else the place-marker emblem.</summary>
        Place,
        /// <summary>A stage to fight: the region's board tile, else the Enter-arrow emblem.</summary>
        Stage,
        /// <summary>"Enter", "Go", "Next stage": the Enter-arrow emblem.</summary>
        Enter,
        /// <summary>An item: <c>ItemIcons/&lt;id&gt;-00</c>, else the pouch emblem.</summary>
        Item,
        /// <summary>An ability: <c>AbilityIcons/&lt;id&gt;-00</c> (or its legacy icon), else the spark emblem.</summary>
        Ability,
        /// <summary>A buff, debuff or condition: <c>StatusIcons/&lt;id&gt;-00</c>, else the aura emblem.</summary>
        Status,
        /// <summary>A hero: its baked thumbnail (§12.1.1), else the bust emblem.</summary>
        Hero,
        /// <summary>A Camp NPC or other speaker: its portrait, else the bust emblem.</summary>
        Npc,
        /// <summary>A station keeper (Blacksmith, Alchemist, …): their portrait, else the bust emblem.</summary>
        Keeper,
        /// <summary>A bounty: the bounty skull (<see cref="BountySkull"/>, §22.4).</summary>
        Bounty,
        /// <summary>A menu destination with no kind of its own: the three-bar menu emblem.</summary>
        Menu,
        /// <summary>A side quest: the quest (exclamation) emblem.</summary>
        Quest,
        /// <summary>A shop (buy/sell): the coin emblem.</summary>
        Shop,
        /// <summary>A Camp station (Forge, Lab, Inn, …): the station (roof) emblem.</summary>
        Station,
        /// <summary>Back: the left-arrow emblem.</summary>
        Back,
        /// <summary>Close / cancel: the cross emblem.</summary>
        Close,
        /// <summary>Confirm / accept / buy: the check emblem.</summary>
        Confirm,
        /// <summary>Locked (not yet open): the padlock emblem.</summary>
        Locked,
        /// <summary>Cleared (a badge on a stage): the check emblem.</summary>
        Cleared,
        /// <summary>New / unseen (a badge): the sparkle emblem.</summary>
        New,
        /// <summary>Settings and options: the gear emblem.</summary>
        Settings,
        /// <summary>A document to read (a legal page, the notices): a sheet with a folded corner and lines of text.</summary>
        Document,
        /// <summary>Save (a slot, save now): the save-disk emblem.</summary>
        Save,
        /// <summary>Quit / title / leave the game: the power emblem.</summary>
        Quit,
        /// <summary>Talk (a conversation): the speech-bubble emblem.</summary>
        Talk,
        /// <summary>The Bestiary: the open-book emblem.</summary>
        Bestiary,
        /// <summary>New Game+: the loop emblem.</summary>
        NewGamePlus,
        /// <summary>The Crucible: the chalice emblem.</summary>
        Crucible,
        /// <summary>The Diamond Store / Diamonds: the diamond emblem.</summary>
        Diamond,
        /// <summary>The Codex and its Keywords tab (GG-T925): a closed tome with a bookmark.</summary>
        Codex,
        /// <summary>A Codex story entry (GG-T925): a scroll.</summary>
        Lore,
        /// <summary>A faction (GG-T925): a banner on a pole.</summary>
        Faction,
        /// <summary>Home, back to the hub root (GG-T1335): the house emblem.</summary>
        Home,
        /// <summary>The Summon Circle: a ring with a five-point star inside.</summary>
        Summon,
        /// <summary>The Craft group: an anvil.</summary>
        Craft,
        /// <summary>Settings (GG-T1886): fast-forward chevrons.</summary>
        Speed,
        /// <summary>Settings (GG-T1886): a clock.</summary>
        Timer,
        /// <summary>Settings (GG-T1886): a crosshair.</summary>
        Focus,
        /// <summary>Settings (GG-T1886): a dial.</summary>
        Gauge,
        /// <summary>Settings (GG-T1886): a zigzag.</summary>
        Shake,
        /// <summary>Settings (GG-T1886): two opposed arrows.</summary>
        Sync,
        /// <summary>Settings (GG-T1886): a window with a title bar.</summary>
        Window,
        /// <summary>Settings (GG-T1886): a screen frame with corner brackets.</summary>
        Frame,
        /// <summary>Settings (GG-T1886): a thumb pad in a margin box.</summary>
        Hand,
        /// <summary>Settings (GG-T1886): a small square growing into a large one.</summary>
        Scale,
        /// <summary>Settings (GG-T1886): a capital A.</summary>
        Text,
        /// <summary>Settings (GG-T1886): a capital T.</summary>
        Type,
        /// <summary>Settings (GG-T1886): an eye.</summary>
        Eye,
        /// <summary>Settings (GG-T1886): two bars.</summary>
        Pause,
        /// <summary>Settings (GG-T1886): a speaker cone with a sound arc.</summary>
        Speaker,
        /// <summary>Settings (GG-T1886): a music note.</summary>
        Note,
        /// <summary>Settings (GG-T1886): sound waves.</summary>
        Wave,
        /// <summary>Settings (GG-T1886): a speaker struck through.</summary>
        SpeakerOff,
        /// <summary>Settings (GG-T1886): a music note struck through.</summary>
        NoteOff,
        /// <summary>Settings (GG-T1886): a window struck through.</summary>
        WindowOff,
        /// <summary>The hamburger, the menu button top-left of every screen that has one (GG-T1910, BIBLE 9.0 row 1): three
        /// big filled, outlined bars across the whole icon, no disc. <see cref="Menu"/> stays the in-list menu-row emblem.</summary>
        Hamburger,
    }

    /// <summary>
    /// The leading kind icon of every tappable row, card, button and list entry (BIBLE §26.7, GG-T714). One call:
    /// <c>row.KindIcon(RowKind.Item, Art.ItemIcon(id))</c> (or <c>RowIcon.KindIcon(row, kind, tex)</c>, or the id form
    /// <c>row.KindIcon(RowKind.Place, themeId)</c> that finds the art itself) puts a <c>menu-icon</c> right after the row's
    /// <c>menu-cursor</c> (first child when it has none), replacing an icon already there. With no art for the kind it
    /// shows that kind's icon from the menu set (<see cref="IconId"/>), so a row is never bare text.
    /// <para>Size: the kit's <c>.menu-icon</c> (64 design px, <see cref="Size.Icon"/>, 16 px before the label), fixed:
    /// it never shrinks, and the label's shrink-to-fit (GG-T711, <see cref="TextFit"/>) fits the width left over.</para>
    /// </summary>
    public static class RowIcon
    {
        /// <summary>Class every kind icon carries besides <c>menu-icon</c> (so a screen or test can find it).</summary>
        public const string Class = "kind-icon";

        /// <summary>Lead <paramref name="row"/> with <paramref name="kind"/>'s icon: <paramref name="tex"/> when given, else
        /// the kind's icon from the menu set. Returns the icon.</summary>
        public static VisualElement KindIcon(this VisualElement row, RowKind kind, Texture2D tex = null)
        {
            VisualElement icon;
            if (tex != null && tex != Art.Placeholder)
            {
                icon = Ui.El("menu-icon");
                icon.style.backgroundImage = new StyleBackground(tex);
            }
            else icon = Fallback(kind);
            return Place(row, icon, kind);
        }

        /// <summary>Lead <paramref name="row"/> with <paramref name="kind"/>'s icon found from <paramref name="id"/>: an item
        /// or ability id, a status id, a theme id for a place or stage, a class or portrait id for a hero, NPC or keeper.
        /// Anything without art falls back to the kind's set icon.</summary>
        public static VisualElement KindIcon(this VisualElement row, RowKind kind, string id)
        {
            if (IsActor(kind) && !string.IsNullOrEmpty(id) && Art.HasPortrait(id))
            {
                var host = Ui.El("menu-icon");
                ActorThumb(host, id);
                return Place(row, host, kind);
            }
            return KindIcon(row, kind, ArtFor(kind, id));
        }

        /// <summary>A button led by its kind icon (BIBLE §26.7): empty button text, the icon, then <paramref name="text"/> as a
        /// label that takes no clicks, so the whole button stays one target. With no text it is an icon-only button (the
        /// header's back button, GG-T738): the icon centred, no label. A <see cref="RowKind.Confirm"/> button runs an action,
        /// so it is select-then-confirm (<see cref="Ui.ConfirmTap"/>, GG-T1942) unless <paramref name="confirmTap"/> says
        /// otherwise: false for one that opens its own confirm or answers a dialog.</summary>
        public static Button IconButton(string text, RowKind kind, Action onClick, string classes = null, VisualElement parent = null, bool? confirmTap = null, Texture2D tex = null)
        {
            var b = Ui.Button("", onClick, classes, parent, confirmTap ?? kind == RowKind.Confirm);
            b.style.flexDirection = FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            var icon = b.KindIcon(kind, tex);
            icon.style.width = icon.style.height = Size.IconButton;
            if (string.IsNullOrEmpty(text)) { icon.style.marginRight = Space.S0; return b; }
            icon.style.marginRight = Space.S8;
            var label = Ui.Text(text, null, b);
            label.pickingMode = PickingMode.Ignore;
            Fonts.UseHeavy(label);
            return b;
        }

        /// <summary>A small badge (<see cref="RowKind.Cleared"/>, <see cref="RowKind.New"/>, <see cref="RowKind.Locked"/>)
        /// in the bottom-right corner of a kind icon: the stage list's state marks. Returns the badge.</summary>
        public static VisualElement Badge(VisualElement icon, RowKind badge)
        {
            icon.Q(className: BadgeClass)?.RemoveFromHierarchy();
            var b = SetIcon(IconId(badge), BadgeClass);
            b.style.position = Position.Absolute;
            b.style.width = b.style.height = Length.Percent(BadgeFrac * 100f);
            b.style.right = b.style.bottom = Length.Percent(-8f);
            icon.style.overflow = Overflow.Visible;
            icon.Add(b);
            return b;
        }

        /// <summary>The hamburger's share of its button (GG-T1910): the glyph spans this much of the 132 px target.</summary>
        public const float HamburgerFrac = 0.6f;

        /// <summary>The icon-only hamburger menu button (GG-T1910): the <see cref="RowKind.Hamburger"/> glyph at
        /// <see cref="HamburgerFrac"/> of the target, named <c>menu</c>.</summary>
        public static Button HamburgerButton(Action onClick, VisualElement parent)
        {
            var b = IconButton("", RowKind.Hamburger, onClick, "btn--small btn--icon", parent);
            b.name = "menu";
            var icon = b.Q(className: Class);
            icon.style.width = icon.style.height = Size.Target * HamburgerFrac;
            return b;
        }

        /// <summary>Takes the corner badge off a kind icon (an entry read, GG-T925); nothing when it has none.</summary>
        public static void ClearBadge(VisualElement icon) => icon?.Q(className: BadgeClass)?.RemoveFromHierarchy();

        /// <summary>The art <paramref name="kind"/> shows for <paramref name="id"/>, or null (the set icon then).</summary>
        public static Texture2D ArtFor(RowKind kind, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            switch (kind)
            {
                case RowKind.Item: return Art.ItemIcon(id);
                case RowKind.Ability: return Art.AbilityIcon(GameApp.I?.Content?.TryAbility(id));
                case RowKind.Status: return Art.StatusIcon(id);
                case RowKind.Place:
                case RowKind.Stage: return Art.BoardTile(id);
                default: return null;
            }
        }

        private const string BadgeClass = "kind-icon__badge";
        private const float BadgeFrac = 0.46f;

        private static bool IsActor(RowKind k) => k == RowKind.Hero || k == RowKind.Npc || k == RowKind.Keeper;

        /// <summary>An actor's face in an icon host: its baked thumbnail through the shared helper (§12.1.1, GG-T720),
        /// falling back to the portrait through its Thumbnail framing.</summary>
        private static void ActorThumb(VisualElement host, string actorId) => Ui.PortraitThumb(host, actorId);

        /// <summary>Class a kind icon carries when it shows the kind's set icon rather than the thing's own art.</summary>
        public const string SetClass = "kind-icon--set";

        /// <summary>True when <paramref name="icon"/> is a kind's set icon, not the thing's own art.</summary>
        public static bool IsSetIcon(VisualElement icon) => icon != null && icon.ClassListContains(SetClass);

        /// <summary>The menu-set icon id (<see cref="Art.UiIcon"/>, docs/ICONS.md "KindEmblem kinds to migrate") of
        /// <paramref name="kind"/>.</summary>
        public static string IconId(RowKind kind)
        {
            switch (kind)
            {
                case RowKind.Place: return "chrome-place";
                case RowKind.Stage: return "chrome-stage";
                case RowKind.Enter: return "chrome-enter";
                case RowKind.Item: return "chrome-item";
                case RowKind.Ability: return "chrome-ability";
                case RowKind.Status: return "chrome-status";
                case RowKind.Hero: case RowKind.Npc: case RowKind.Keeper: return "chrome-hero";
                case RowKind.Bounty: return "hub-bounties";
                case RowKind.Quest: return "hub-quests";
                case RowKind.Shop: return "hub-shop";
                case RowKind.Station: return "chrome-station";
                case RowKind.Back: return "chrome-back";
                case RowKind.Close: return "chrome-close";
                case RowKind.Confirm: return "chrome-confirm";
                case RowKind.Locked: return "chrome-locked";
                case RowKind.Cleared: return "chrome-cleared";
                case RowKind.New: return "chrome-new";
                case RowKind.Settings: return "hub-settings";
                case RowKind.Document: return "chrome-document";
                case RowKind.Save: return "chrome-save";
                case RowKind.Quit: return "hub-quit-to-title";
                case RowKind.Talk: return "hub-talk";
                case RowKind.Bestiary: return "hub-bestiary";
                case RowKind.NewGamePlus: return "hub-new-game-plus";
                case RowKind.Crucible: return "hub-crucible";
                case RowKind.Diamond: return "hub-diamond-store";
                case RowKind.Codex: return "hub-codex";
                case RowKind.Lore: return "chrome-lore";
                case RowKind.Faction: return "chrome-faction";
                case RowKind.Home: return "chrome-home";
                case RowKind.Summon: return "hub-summon";
                case RowKind.Craft: return "hub-craft";
                case RowKind.Speed: return "set-battle-speed";
                case RowKind.Timer: return "set-drag-timer";
                case RowKind.Focus: return "set-pincer-close-ups";
                case RowKind.Gauge: return "set-frame-rate-cap";
                case RowKind.Shake: return "set-screen-shake";
                case RowKind.Sync: return "set-vsync";
                case RowKind.Window: return "set-window-mode";
                case RowKind.Frame: return "set-resolution";
                case RowKind.Hand: return "set-thumb-margin";
                case RowKind.Scale: return "set-ui-scale";
                case RowKind.Text: return "set-text-size";
                case RowKind.Type: return "set-font";
                case RowKind.Eye: return "set-colorblind-palette";
                case RowKind.Pause: return "set-reduce-motion";
                case RowKind.Speaker: return "set-master-volume";
                case RowKind.Note: return "set-music-volume";
                case RowKind.Wave: return "set-sound-volume";
                case RowKind.SpeakerOff: return "set-mute-sound";
                case RowKind.NoteOff: return "set-mute-music";
                case RowKind.WindowOff: return "set-mute-when-unfocused";
                case RowKind.Hamburger: return "chrome-hamburger";
                default: return "chrome-menu";
            }
        }

        /// <summary>A bare icon element showing set icon <paramref name="id"/> (<see cref="Art.UiIcon"/>), sized by its
        /// classes or the caller.</summary>
        public static VisualElement SetIcon(string id, string classes = "menu-icon")
        {
            var e = Ui.El(classes);
            e.pickingMode = PickingMode.Ignore;
            e.style.backgroundImage = new StyleBackground(Art.UiIcon(id));
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            return e;
        }

        private static VisualElement Fallback(RowKind kind)
        {
            var e = SetIcon(IconId(kind));
            e.AddToClassList(SetClass);
            return e;
        }

        private static VisualElement Place(VisualElement row, VisualElement icon, RowKind kind)
        {
            icon.AddToClassList(Class);
            icon.pickingMode = PickingMode.Ignore;
            icon.style.flexShrink = 0f; // fixed width: the label fits what's left (GG-T711)
            icon.userData = kind;
            int at = 0, i = 0;
            VisualElement old = null;
            foreach (var c in row.Children())
            {
                if (c.ClassListContains("menu-cursor")) at = i + 1;
                else if (old == null && c.ClassListContains("menu-icon")) old = c;
                i++;
            }
            if (old != null) { at = row.IndexOf(old); old.RemoveFromHierarchy(); }
            row.Insert(Mathf.Min(at, row.childCount), icon);
            return icon;
        }
    }
}
