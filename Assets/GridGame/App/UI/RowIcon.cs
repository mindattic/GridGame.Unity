using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// What a tappable thing is (GG-T712/GG-T714, owner 2026-11-23: "every item or place or selectable must be prefixed with
    /// a icon so the user knows what they are pressing"). Each kind has one icon (BIBLE §26.7, the kind table): its real art
    /// where art exists, else a code-drawn Manapunk emblem (<see cref="KindEmblem"/>).
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
    }

    /// <summary>
    /// The leading kind icon of every tappable row, card, button and list entry (BIBLE §26.7, GG-T714). One call:
    /// <c>row.KindIcon(RowKind.Item, Art.ItemIcon(id))</c> (or <c>RowIcon.KindIcon(row, kind, tex)</c>, or the id form
    /// <c>row.KindIcon(RowKind.Place, themeId)</c> that finds the art itself) puts a <c>menu-icon</c> right after the row's
    /// <c>menu-cursor</c> (first child when it has none), replacing an icon already there. With no art for the kind it
    /// draws that kind's emblem, so a row is never bare text.
    /// <para>Size: the kit's <c>.menu-icon</c> (64 design px, <see cref="Size.Icon"/>, 16 px before the label), fixed:
    /// it never shrinks, and the label's shrink-to-fit (GG-T711, <see cref="TextFit"/>) fits the width left over.</para>
    /// </summary>
    public static class RowIcon
    {
        /// <summary>Class every kind icon carries besides <c>menu-icon</c> (so a screen or test can find it).</summary>
        public const string Class = "kind-icon";

        /// <summary>Lead <paramref name="row"/> with <paramref name="kind"/>'s icon: <paramref name="tex"/> when given, else
        /// the kind's fixed art (the bounty skull) or its emblem. Returns the icon.</summary>
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
        /// Anything without art falls back to the kind's emblem.</summary>
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
        /// header's back button, GG-T738): the icon centred, no label.</summary>
        public static Button IconButton(string text, RowKind kind, Action onClick, string classes = null, VisualElement parent = null)
        {
            var b = Ui.Button("", onClick, classes, parent);
            b.style.flexDirection = FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            var icon = b.KindIcon(kind);
            icon.style.width = icon.style.height = Size.IconS;
            if (string.IsNullOrEmpty(text)) { icon.style.marginRight = 0; return b; }
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
            var b = new KindEmblem(badge);
            b.AddToClassList(BadgeClass);
            b.style.position = Position.Absolute;
            b.style.width = b.style.height = Length.Percent(BadgeFrac * 100f);
            b.style.right = b.style.bottom = Length.Percent(-8f);
            icon.style.overflow = Overflow.Visible;
            icon.Add(b);
            return b;
        }

        /// <summary>Takes the corner badge off a kind icon (an entry read, GG-T925); nothing when it has none.</summary>
        public static void ClearBadge(VisualElement icon) => icon?.Q(className: BadgeClass)?.RemoveFromHierarchy();

        /// <summary>The art <paramref name="kind"/> shows for <paramref name="id"/>, or null (the emblem then).</summary>
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

        private static VisualElement Fallback(RowKind kind)
        {
            if (kind == RowKind.Bounty)
            {
                var host = Ui.El("menu-icon");
                host.style.alignItems = Align.Center;
                host.style.justifyContent = Justify.Center;
                BountySkull.Make(Size.Icon, host);
                return host;
            }
            var e = new KindEmblem(kind);
            e.AddToClassList("menu-icon");
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

    /// <summary>
    /// A kind's code-drawn Manapunk emblem (BIBLE §11.5, §26.7): a Manalith-blue (#7590C8) disc with a pewter rim and a pale
    /// mark for the kind, drawn with Painter2D like the mana orbs, so it is crisp at any size and needs no font glyph
    /// (GG-LAW-11). Stands in wherever a kind has no art.
    /// </summary>
    public sealed class KindEmblem : VisualElement
    {
        /// <summary>The disc: Manalith blue #7590C8 (§11.5, the lit mana orb's colour).</summary>
        public static readonly Color Disc = Palette.OrbLit;
        public static readonly Color Rim = Palette.PewterDark;
        public static readonly Color Mark = new Color(0.97f, 0.97f, 1f);

        public RowKind Kind { get; }

        public KindEmblem(RowKind kind)
        {
            Kind = kind;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            float r = Mathf.Min(rect.width, rect.height) / 2f;
            if (r < 3f || float.IsNaN(r)) return;
            var c = rect.center;
            var p = ctx.painter2D;
            float rim = Mathf.Max(1.5f, r * 0.08f);
            FillCircle(p, c, r, Rim);
            FillCircle(p, c, r - rim, Disc);
            // The glass highlight every Manapunk bulb carries, faint, top left.
            FillCircle(p, c + new Vector2(-0.38f, -0.40f) * r, r * 0.16f, Palette.White.WithA(0.3f));

            float u = r * 0.52f; // the mark's half-size
            Vector2 P(float x, float y) => c + new Vector2(x, y) * u;
            float w = Mathf.Max(2f, r * 0.15f); // stroke width
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.strokeColor = Mark;
            p.fillColor = Mark;
            p.lineWidth = w;

            switch (Kind)
            {
                case RowKind.Place: // map pin: a round head tapering to a point, with a hole
                    p.BeginPath();
                    p.Arc(P(0f, -0.25f), u * 0.62f, Angle.Degrees(150f), Angle.Degrees(390f));
                    p.LineTo(P(0f, 1f));
                    p.ClosePath();
                    p.Fill();
                    FillCircle(p, P(0f, -0.25f), u * 0.26f, Disc);
                    break;
                case RowKind.Stage:
                case RowKind.Enter: // the Enter arrow, pointing right
                    Shapes.Arrow(p, P(-0.8f, 0f), P(0.95f, 0f), ArrowStyle(w, u));
                    break;
                case RowKind.Back: // arrow pointing left
                    Shapes.Arrow(p, P(0.8f, 0f), P(-0.95f, 0f), ArrowStyle(w, u));
                    break;
                case RowKind.Close:
                    Line(p, P(-0.6f, -0.6f), P(0.6f, 0.6f));
                    Line(p, P(0.6f, -0.6f), P(-0.6f, 0.6f));
                    break;
                case RowKind.Confirm:
                case RowKind.Cleared: // check
                    p.lineWidth = w * 1.2f;
                    p.BeginPath();
                    p.MoveTo(P(-0.7f, 0.05f));
                    p.LineTo(P(-0.2f, 0.55f));
                    p.LineTo(P(0.75f, -0.55f));
                    p.Stroke();
                    break;
                case RowKind.Locked: // padlock: shackle over a body with a keyhole
                    p.BeginPath();
                    p.Arc(P(0f, -0.2f), u * 0.42f, Angle.Degrees(180f), Angle.Degrees(360f));
                    p.Stroke();
                    Line(p, P(-0.42f, -0.2f), P(-0.42f, 0.05f));
                    Line(p, P(0.42f, -0.2f), P(0.42f, 0.05f));
                    Rect(p, P(-0.7f, 0f), P(0.7f, 0.9f));
                    FillCircle(p, P(0f, 0.38f), u * 0.14f, Disc);
                    break;
                case RowKind.New: // four-point sparkle
                    Poly(p, P(0f, -1f), P(0.22f, -0.22f), P(1f, 0f), P(0.22f, 0.22f), P(0f, 1f), P(-0.22f, 0.22f), P(-1f, 0f), P(-0.22f, -0.22f));
                    break;
                case RowKind.Ability: // a bolt
                    Poly(p, P(0.25f, -1f), P(-0.55f, 0.15f), P(-0.05f, 0.15f), P(-0.3f, 1f), P(0.55f, -0.2f), P(0.05f, -0.2f));
                    break;
                case RowKind.Item: // a tied pouch
                    FillCircle(p, P(0f, 0.3f), u * 0.62f, Mark);
                    Poly(p, P(-0.3f, -0.35f), P(0.3f, -0.35f), P(0.12f, -0.12f), P(-0.12f, -0.12f));
                    Poly(p, P(-0.45f, -0.8f), P(0.45f, -0.8f), P(0.25f, -0.4f), P(-0.25f, -0.4f));
                    break;
                case RowKind.Status: // an aura: a ring around a core
                    p.BeginPath();
                    p.Arc(c, u * 0.85f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Stroke();
                    FillCircle(p, c, u * 0.38f, Mark);
                    break;
                case RowKind.Hero:
                case RowKind.Npc:
                case RowKind.Keeper: // a bust: head over shoulders
                    FillCircle(p, P(0f, -0.4f), u * 0.42f, Mark);
                    p.BeginPath();
                    p.Arc(P(0f, 0.95f), u * 0.85f, Angle.Degrees(180f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case RowKind.Bounty: // only drawn when asked for directly (the skull art is the bounty's icon)
                    FillCircle(p, P(0f, -0.15f), u * 0.62f, Mark);
                    Rect(p, P(-0.35f, 0.3f), P(0.35f, 0.8f));
                    FillCircle(p, P(-0.25f, -0.15f), u * 0.17f, Disc);
                    FillCircle(p, P(0.25f, -0.15f), u * 0.17f, Disc);
                    break;
                case RowKind.Quest: // exclamation mark
                    p.lineWidth = w * 1.3f;
                    Line(p, P(0f, -0.85f), P(0f, 0.3f));
                    FillCircle(p, P(0f, 0.78f), w * 0.75f, Mark);
                    break;
                case RowKind.Shop: // a coin: ring with a stamped bar
                    p.BeginPath();
                    p.Arc(c, u * 0.82f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Stroke();
                    Line(p, P(0f, -0.4f), P(0f, 0.4f));
                    break;
                case RowKind.Station: // a roof over a door: the Camp station marker
                    Poly(p, P(0f, -0.95f), P(0.95f, -0.1f), P(-0.95f, -0.1f));
                    Rect(p, P(-0.65f, -0.1f), P(0.65f, 0.85f));
                    Rect(p, P(-0.18f, 0.3f), P(0.18f, 0.85f), Disc);
                    break;
                case RowKind.Settings: // a gear: eight teeth round a ring
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(p, c + d * u * 0.55f, c + d * u * 0.95f);
                    }
                    FillCircle(p, c, u * 0.62f, Mark);
                    FillCircle(p, c, u * 0.25f, Disc);
                    break;
                case RowKind.Save: // a save disk: a square with a label and a shutter
                    Rect(p, P(-0.8f, -0.8f), P(0.8f, 0.8f));
                    Rect(p, P(-0.42f, -0.8f), P(0.42f, -0.3f), Disc);
                    Rect(p, P(-0.5f, 0.15f), P(0.5f, 0.8f), Disc);
                    break;
                case RowKind.Home: // a house in outline: a pitched roof with a chimney over walls and a door
                    p.BeginPath();
                    p.MoveTo(P(-0.95f, -0.05f));
                    p.LineTo(P(0f, -0.9f));
                    p.LineTo(P(0.95f, -0.05f));
                    p.Stroke();
                    Line(p, P(0.5f, -0.45f), P(0.5f, -0.8f));
                    p.BeginPath();
                    p.MoveTo(P(-0.65f, -0.25f));
                    p.LineTo(P(-0.65f, 0.85f));
                    p.LineTo(P(0.65f, 0.85f));
                    p.LineTo(P(0.65f, -0.25f));
                    p.Stroke();
                    Rect(p, P(-0.2f, 0.25f), P(0.2f, 0.85f));
                    break;
                case RowKind.Quit: // power: an open ring with a bar through the gap
                    p.BeginPath();
                    p.Arc(c, u * 0.78f, Angle.Degrees(-55f), Angle.Degrees(235f));
                    p.Stroke();
                    Line(p, P(0f, -1f), P(0f, -0.15f));
                    break;
                case RowKind.Talk: // a speech bubble with a tail
                    p.BeginPath();
                    p.Arc(P(0f, -0.15f), u * 0.8f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Fill();
                    Poly(p, P(-0.5f, 0.35f), P(-0.1f, 0.55f), P(-0.75f, 0.95f));
                    break;
                case RowKind.Bestiary: // an open book
                    Poly(p, P(-0.95f, -0.65f), P(-0.06f, -0.45f), P(-0.06f, 0.8f), P(-0.95f, 0.6f));
                    Poly(p, P(0.95f, -0.65f), P(0.06f, -0.45f), P(0.06f, 0.8f), P(0.95f, 0.6f));
                    break;
                case RowKind.NewGamePlus: // a loop: a ring almost closed, its arrowhead at the gap
                    {
                        // GG-T1833: the loop is one filled, outlined arrow along the arc, its head at the gap.
                        var loop = new List<Vector2>();
                        for (int k = 0; k <= 28; k++)
                        {
                            float a = Mathf.Lerp(220f, -60f, k / 28f) * Mathf.Deg2Rad;
                            loop.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * u * 0.72f);
                        }
                        Shapes.PathArrow(p, loop, ArrowStyle(w, u));
                    }
                    break;
                case RowKind.Crucible: // a chalice: a bowl on a stem and foot
                    p.BeginPath();
                    p.MoveTo(P(-0.8f, -0.8f));
                    p.LineTo(P(0.8f, -0.8f));
                    p.Arc(P(0f, -0.8f), u * 0.8f, Angle.Degrees(0f), Angle.Degrees(180f));
                    p.ClosePath();
                    p.Fill();
                    Line(p, P(0f, 0f), P(0f, 0.7f));
                    Line(p, P(-0.5f, 0.8f), P(0.5f, 0.8f));
                    break;
                case RowKind.Diamond: // a cut gem
                    Poly(p, P(-0.9f, -0.3f), P(-0.45f, -0.8f), P(0.45f, -0.8f), P(0.9f, -0.3f), P(0f, 0.95f));
                    break;
                case RowKind.Codex: // a closed tome: the cover, its spine, a bookmark ribbon hanging out the top
                    Rect(p, P(-0.65f, -0.85f), P(0.7f, 0.85f));
                    Rect(p, P(-0.45f, -0.85f), P(-0.35f, 0.85f), Disc);
                    Rect(p, P(0.15f, -1f), P(0.4f, -0.85f));
                    p.fillColor = Disc;
                    Poly(p, P(0.15f, -0.85f), P(0.4f, -0.85f), P(0.4f, -0.2f), P(0.275f, -0.35f), P(0.15f, -0.2f));
                    p.fillColor = Mark;
                    break;
                case RowKind.Lore: // a scroll: a sheet between two rolls, lines of text on it
                    Rect(p, P(-0.6f, -0.7f), P(0.6f, 0.7f));
                    p.lineWidth = w * 1.6f;
                    Line(p, P(-0.85f, -0.75f), P(0.85f, -0.75f));
                    Line(p, P(-0.85f, 0.75f), P(0.85f, 0.75f));
                    p.lineWidth = w;
                    Rect(p, P(-0.4f, -0.35f), P(0.4f, -0.25f), Disc);
                    Rect(p, P(-0.4f, -0.05f), P(0.4f, 0.05f), Disc);
                    Rect(p, P(-0.4f, 0.25f), P(0.15f, 0.35f), Disc);
                    break;
                case RowKind.Faction: // a banner on a pole, its fly cut into a swallowtail
                    Line(p, P(-0.7f, -0.95f), P(-0.7f, 0.95f));
                    Poly(p, P(-0.7f, -0.85f), P(0.85f, -0.85f), P(0.45f, -0.35f), P(0.85f, 0.15f), P(-0.7f, 0.15f));
                    break;
                case RowKind.Summon: // a summoning circle: a ring round a five-point star
                    p.BeginPath();
                    p.Arc(c, u * 0.95f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Stroke();
                    for (int k = 0; k < 5; k++)
                    {
                        float a0 = (k * 144f - 90f) * Mathf.Deg2Rad, a1 = ((k + 1) * 144f - 90f) * Mathf.Deg2Rad;
                        Line(p, P(Mathf.Cos(a0) * 0.75f, Mathf.Sin(a0) * 0.75f), P(Mathf.Cos(a1) * 0.75f, Mathf.Sin(a1) * 0.75f));
                    }
                    break;
                case RowKind.Craft: // an anvil: a flat face with a horn, a narrow waist, a wide foot
                    Poly(p, P(-0.95f, -0.6f), P(0.95f, -0.6f), P(0.95f, -0.3f), P(0.35f, -0.15f), P(0.35f, 0.3f), P(0.7f, 0.3f), P(0.7f, 0.85f), P(-0.7f, 0.85f), P(-0.7f, 0.3f), P(-0.35f, 0.3f), P(-0.35f, -0.15f), P(-0.95f, -0.3f));
                    break;
                case RowKind.Speed: // two chevrons pointing right
                    Poly(p, P(-0.9f, -0.7f), P(0f, 0f), P(-0.9f, 0.7f));
                    Poly(p, P(0f, -0.7f), P(0.9f, 0f), P(0f, 0.7f));
                    break;
                case RowKind.Timer: // a clock: a ring, two hands
                    p.BeginPath();
                    p.Arc(c, u * 0.85f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Stroke();
                    Line(p, c, P(0f, -0.5f));
                    Line(p, c, P(0.4f, 0.2f));
                    break;
                case RowKind.Focus: // a crosshair: a ring with four ticks
                    p.BeginPath();
                    p.Arc(c, u * 0.5f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.ClosePath();
                    p.Stroke();
                    Line(p, P(0f, -1f), P(0f, -0.3f));
                    Line(p, P(0f, 0.3f), P(0f, 1f));
                    Line(p, P(-1f, 0f), P(-0.3f, 0f));
                    Line(p, P(0.3f, 0f), P(1f, 0f));
                    break;
                case RowKind.Gauge: // a dial: a half ring with a needle
                    p.BeginPath();
                    p.Arc(P(0f, 0.5f), u * 0.95f, Angle.Degrees(180f), Angle.Degrees(360f));
                    p.Stroke();
                    Line(p, P(0f, 0.5f), P(0.5f, -0.1f));
                    Line(p, P(-0.95f, 0.5f), P(0.95f, 0.5f));
                    break;
                case RowKind.Shake: // a zigzag
                    p.BeginPath();
                    p.MoveTo(P(-0.95f, 0f));
                    p.LineTo(P(-0.55f, -0.6f));
                    p.LineTo(P(-0.15f, 0.6f));
                    p.LineTo(P(0.25f, -0.6f));
                    p.LineTo(P(0.6f, 0.6f));
                    p.LineTo(P(0.95f, 0f));
                    p.Stroke();
                    break;
                case RowKind.Sync: // two arrows, one each way
                    Shapes.Arrow(p, P(-0.9f, -0.45f), P(0.9f, -0.45f), ArrowStyle(w * 0.7f, u * 0.7f));
                    Shapes.Arrow(p, P(0.9f, 0.45f), P(-0.9f, 0.45f), ArrowStyle(w * 0.7f, u * 0.7f));
                    break;
                case RowKind.Window:
                case RowKind.WindowOff: // a window: a frame with a filled title bar
                    DrawWindow(p, P);
                    if (Kind == RowKind.WindowOff) Strike(p, P);
                    break;
                case RowKind.Frame: // a screen: four corner brackets
                    foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                    {
                        Line(p, P(sx * 0.9f, sy * 0.9f), P(sx * 0.9f, sy * 0.3f));
                        Line(p, P(sx * 0.9f, sy * 0.9f), P(sx * 0.3f, sy * 0.9f));
                    }
                    break;
                case RowKind.Hand: // a thumb pad inside a margin box
                    p.BeginPath();
                    p.MoveTo(P(-0.9f, -0.9f));
                    p.LineTo(P(0.9f, -0.9f));
                    p.LineTo(P(0.9f, 0.9f));
                    p.LineTo(P(-0.9f, 0.9f));
                    p.ClosePath();
                    p.Stroke();
                    FillCircle(p, P(0f, 0.3f), u * 0.4f, Mark);
                    break;
                case RowKind.Scale: // a small square growing into a large one
                    Rect(p, P(-0.9f, 0.1f), P(-0.1f, 0.9f));
                    p.BeginPath();
                    p.MoveTo(P(-0.5f, -0.1f));
                    p.LineTo(P(-0.5f, -0.9f));
                    p.LineTo(P(0.9f, -0.9f));
                    p.LineTo(P(0.9f, 0.5f));
                    p.LineTo(P(0.1f, 0.5f));
                    p.Stroke();
                    break;
                case RowKind.Text: // a capital A
                    p.BeginPath();
                    p.MoveTo(P(-0.8f, 0.9f));
                    p.LineTo(P(0f, -0.9f));
                    p.LineTo(P(0.8f, 0.9f));
                    p.Stroke();
                    Line(p, P(-0.4f, 0.25f), P(0.4f, 0.25f));
                    break;
                case RowKind.Type: // a capital T
                    Line(p, P(-0.8f, -0.8f), P(0.8f, -0.8f));
                    Line(p, P(0f, -0.8f), P(0f, 0.9f));
                    break;
                case RowKind.Eye: // an eye: a lens outline round a pupil
                    p.BeginPath();
                    p.MoveTo(P(-0.95f, 0f));
                    p.BezierCurveTo(P(-0.5f, -0.8f), P(0.5f, -0.8f), P(0.95f, 0f));
                    p.BezierCurveTo(P(0.5f, 0.8f), P(-0.5f, 0.8f), P(-0.95f, 0f));
                    p.Stroke();
                    FillCircle(p, c, u * 0.3f, Mark);
                    break;
                case RowKind.Pause: // two bars
                    Rect(p, P(-0.6f, -0.7f), P(-0.15f, 0.7f));
                    Rect(p, P(0.15f, -0.7f), P(0.6f, 0.7f));
                    break;
                case RowKind.Speaker:
                case RowKind.SpeakerOff: // a speaker cone with a sound arc
                    DrawSpeaker(p, P);
                    p.BeginPath();
                    p.Arc(P(0.1f, 0f), u * 0.75f, Angle.Degrees(-40f), Angle.Degrees(40f));
                    p.Stroke();
                    if (Kind == RowKind.SpeakerOff) Strike(p, P);
                    break;
                case RowKind.Wave: // sound waves: three arcs
                    for (int k = 1; k <= 3; k++)
                    {
                        p.BeginPath();
                        p.Arc(P(-0.8f, 0f), u * 0.55f * k, Angle.Degrees(-45f), Angle.Degrees(45f));
                        p.Stroke();
                    }
                    break;
                case RowKind.Note:
                case RowKind.NoteOff: // a music note: a head, a stem, a flag
                    FillCircle(p, P(-0.35f, 0.65f), u * 0.35f, Mark);
                    Line(p, P(-0.02f, 0.6f), P(-0.02f, -0.8f));
                    Line(p, P(-0.02f, -0.8f), P(0.7f, -0.45f));
                    if (Kind == RowKind.NoteOff) Strike(p, P);
                    break;
                default: // Menu: three bars
                    Line(p, P(-0.7f, -0.55f), P(0.7f, -0.55f));
                    Line(p, P(-0.7f, 0f), P(0.7f, 0f));
                    Line(p, P(-0.7f, 0.55f), P(0.7f, 0.55f));
                    break;
            }
        }

        private static void DrawWindow(Painter2D p, Func<float, float, Vector2> P)
        {
            Rect(p, P(-0.9f, -0.75f), P(0.9f, -0.35f));
            p.BeginPath();
            p.MoveTo(P(-0.9f, -0.35f));
            p.LineTo(P(-0.9f, 0.8f));
            p.LineTo(P(0.9f, 0.8f));
            p.LineTo(P(0.9f, -0.35f));
            p.Stroke();
        }

        private static void DrawSpeaker(Painter2D p, Func<float, float, Vector2> P)
        {
            Rect(p, P(-0.9f, -0.3f), P(-0.5f, 0.3f));
            Poly(p, P(-0.5f, -0.3f), P(0f, -0.8f), P(0f, 0.8f), P(-0.5f, 0.3f));
        }

        private static void Strike(Painter2D p, Func<float, float, Vector2> P) => Line(p, P(-0.9f, -0.9f), P(0.9f, 0.9f));

        private static void FillCircle(Painter2D p, Vector2 c, float radius, Color col)
        {
            p.fillColor = col;
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            p.Fill();
            p.fillColor = Mark;
        }

        /// <summary>GG-T1833: an emblem's arrow is a filled body in the mark colour with a dark rim outline, sized to the glyph.</summary>
        private static ShapeStyle ArrowStyle(float w, float u) => new ShapeStyle
        { Width = w, Outline = Mathf.Max(1f, w * 0.3f), HeadLength = u * 0.7f, HeadWidth = u * 1.2f, Fill = Mark, Line = Rim };

        private static void Line(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        private static void Poly(Painter2D p, params Vector2[] pts)
        {
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
            p.Fill();
        }

        private static void Rect(Painter2D p, Vector2 min, Vector2 max, Color? col = null)
        {
            p.fillColor = col ?? Mark;
            p.BeginPath();
            p.MoveTo(min);
            p.LineTo(new Vector2(max.x, min.y));
            p.LineTo(max);
            p.LineTo(new Vector2(min.x, max.y));
            p.ClosePath();
            p.Fill();
            p.fillColor = Mark;
        }
    }
}
