using System;
using GridGame.Core.Content;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// The design tokens (GG-T250, docs/POLISH.md POL-A1/B1/B4/C1/E1/E2). Every color, type size, spacing step,
    /// duration and easing in App code comes from here; Theme.uss mirrors the same values as :root custom properties.
    /// </summary>
    public static class Palette
    {
        // Surfaces and text: neutral grays (GG-T385), so they sit on the Chrono gray window (USS --color-*)
        public static readonly Color Text = new Color(0.957f, 0.957f, 0.965f);
        /// <summary>Dim text (USS <c>--color-dim</c>). GG-T495: lifted from 176 to 200 grey so it clears 4.5:1 on the lightest
        /// stop of the Classic blue window (#2C46AA): 4.9:1, was 3.8:1.</summary>
        public static readonly Color TextDim = new Color(0.784f, 0.784f, 0.816f);
        /// <summary>A window's subtitle line (USS <c>--color-sub</c>, <c>.window-sub</c>).</summary>
        public static readonly Color TextSub = new Color(0.816f, 0.816f, 0.84f);
        public static readonly Color White = Color.white;
        public static readonly Color Ink = new Color(0.055f, 0.055f, 0.07f);
        public static readonly Color Panel = new Color(0.17f, 0.17f, 0.19f, 0.94f);
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.6f);
        /// <summary>The tint that dims a full-screen backdrop behind a menu or dialogue (neutral, GG-T385).</summary>
        public static readonly Color BackdropDim = new Color(0.42f, 0.42f, 0.44f);
        public static readonly Color Frame = new Color(0.93f, 0.93f, 0.94f);
        /// <summary>The dark block outline behind text (USS <c>--color-text-edge</c>, GG-T385).</summary>
        public static readonly Color TextEdge = new Color(0.03f, 0.03f, 0.047f);

        // Semantic feedback: one meaning per hue family (POL-B4)
        public static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
        public static readonly Color GoldLight = new Color(1f, 0.93f, 0.6f);
        public static readonly Color Good = new Color(0.45f, 0.95f, 0.58f);
        /// <summary>GG-T495: a lighter red (4.5:1 on the Classic window's lightest stop; the old 255/97/87 was 2.8:1).</summary>
        public static readonly Color Bad = new Color(1f, 0.667f, 0.635f);
        public static readonly Color Warn = new Color(1f, 0.77f, 0.36f);
        public static readonly Color Info = new Color(0.86f, 0.88f, 1f);
        /// <summary>A tappable glossary [term] (GG-T300): the same light blue class cards always marked keywords with.</summary>
        public static readonly Color Term = new Color(0.61f, 0.78f, 1f);
        public static readonly Color Mana = Art.Mana;
        // GG-T673: the battle's mana orbs, their bloom and their trails, sampled from the blue Somnolith art (GG-T674,
        // owner: "it's blue like mana is expected to be"): near-black navy glass, indigo liquid, a lighter violet bloom and
        // the art's red-pink glint.
        public static readonly Color ManaDeep = new Color(0x20 / 255f, 0x19 / 255f, 0x3C / 255f);
        public static readonly Color ManaIndigo = new Color(0x4B / 255f, 0x44 / 255f, 0x7D / 255f);
        public static readonly Color ManaBloom = new Color(0x7A / 255f, 0x74 / 255f, 0xAB / 255f);
        public static readonly Color ManaGlint = new Color(0xB4 / 255f, 0x51 / 255f, 0x6C / 255f);
        // GG-T688: the orb as a Manapunk glass bulb (BIBLE §11.5): pewter collar, dark glass empty, mana blue lit.
        public static readonly Color OrbCollar = new Color(0x60 / 255f, 0x64 / 255f, 0x6F / 255f);
        public static readonly Color OrbGlass = new Color(0x22 / 255f, 0x20 / 255f, 0x3A / 255f);
        public static readonly Color OrbLit = new Color(0x75 / 255f, 0x90 / 255f, 0xC8 / 255f);
        public static readonly Color OrbLitHighlight = new Color(0xAC / 255f, 0xA5 / 255f, 0xD3 / 255f);
        // GG-T678: Manapunk lab hardware on every window (BIBLE §11.5): pewter clamp brackets with a worn dark underside, a
        // lighter bolt head, and a glass conduit carrying contained mana.
        public static readonly Color Pewter = new Color(0x60 / 255f, 0x64 / 255f, 0x6F / 255f);
        public static readonly Color PewterDark = new Color(0x34 / 255f, 0x36 / 255f, 0x3F / 255f);
        public static readonly Color PewterLight = new Color(0x8C / 255f, 0x90 / 255f, 0x9C / 255f);
        // The conduit's glass always holds a dim charge of mana (it reads as a mana line even between pulses).
        public static readonly Color ConduitGlass = new Color(0x2D / 255f, 0x34 / 255f, 0x6C / 255f, 0.9f);
        public static readonly Color ConduitMana = new Color(0x9C / 255f, 0xB2 / 255f, 0xE2 / 255f, 0.95f);

        // Teams
        public static readonly Color Hero = new Color(0.47f, 0.67f, 1f);
        public static readonly Color Enemy = new Color(0.92f, 0.27f, 0.29f);
        public static readonly Color Boss = new Color(0.86f, 0.4f, 1f);
        public static readonly Color Guest = new Color(0.43f, 0.94f, 0.63f);
        /// <summary>A tamed or domesticated monster fighting for the party on its own (GG-T303): its board keyline and turn icon.</summary>
        public static readonly Color Tamed = new Color(0.76f, 0.92f, 0.28f);
        /// <summary>A wanderer fighting the monsters on its own (GG-T342): its keyline, WANDERER tag, turn icon and trust meter.</summary>
        public static readonly Color Wanderer = new Color(1f, 0.56f, 0.8f);
        public static readonly Color Neutral = new Color(1f, 0.8f, 0.37f);
        // Casts riding the timeline (GG-T287): never the red of an enemy turn, so a spell reads apart from a foe.
        public static readonly Color CastHero = new Color(0.15f, 0.77f, 0.9f);
        public static readonly Color CastEnemy = new Color(1f, 0.55f, 0.1f);

        // GG-T525: Colorblind palette (SettingsSave.ColorblindMode, set by GameApp.ApplySettings). Red/purple/blue team
        // marks collapse for protan and deutan eyes, so with it on the board and timeline mark sides with one blue/orange
        // pair (the Okabe-Ito sky blue and a vermilion orange, apart for every common colour deficiency; Okabe-Ito's own
        // amber read as the gold selection ring): heroes and hero casts blue, foes and foe casts orange, a boss a darker
        // burnt orange (darker, so it still reads apart from a foe).
        public static bool Colorblind;
        public static readonly Color CbHero = new Color(0.34f, 0.71f, 0.91f);
        public static readonly Color CbFoe = new Color(0.96f, 0.48f, 0.04f);
        public static readonly Color CbBoss = new Color(0.75f, 0.3f, 0.02f);
        /// <summary>A foe's mark (keyline, timeline icon): enemy red, or the colourblind orange.</summary>
        public static Color TeamFoe => Colorblind ? CbFoe : Enemy;
        public static Color TeamBoss => Colorblind ? CbBoss : Boss;
        public static Color TeamCast(bool hero) => Colorblind ? (hero ? CbHero : CbFoe) : hero ? CastHero : CastEnemy;

        /// <summary>The lightest stop of the Classic blue window (#2C46AA), the background text contrast is checked against
        /// (GG-T495).</summary>
        public static readonly Color WindowLight = new Color(44f / 255f, 70f / 255f, 170f / 255f);

        /// <summary>GG-T525: <paramref name="c"/> as text on a window. Element hues and team colours are art colours, tuned to
        /// glow on the board, and several are too dark to read on the blue window (dark 1.9:1, fire 3.0:1, enemy 2.1:1, hero
        /// 3.5:1). This keeps the hue and lifts it toward white just until it clears 4.5:1 against <see cref="WindowLight"/>;
        /// a colour that already does comes back unchanged. The board keeps the art colours.</summary>
        public static Color OnWindow(Color c)
        {
            float a = c.a;
            for (int i = 0; i <= 20 && Contrast(c, WindowLight) < 4.5f; i++)
                c = Color.Lerp(c, Color.white, 0.08f);
            return new Color(c.r, c.g, c.b, a);
        }

        /// <summary>WCAG contrast ratio of two opaque sRGB colours (1..21).</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static float Luminance(Color c)
        {
            static float Lin(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        /// <summary>Text variants (GG-T525): an element's hue and the team colours, readable on a window.</summary>
        public static Color ElementText(DamageType t) => OnWindow(Element(t));
        public static Color HeroText => OnWindow(Hero);
        public static Color EnemyText => OnWindow(Enemy);

        // Statuses that aren't an element
        public static readonly Color Haste = new Color(0.35f, 1f, 0.88f);
        public static readonly Color Buff = new Color(1f, 0.72f, 0.28f);
        public static readonly Color Cleanse = new Color(0.9f, 1f, 1f);

        /// <summary>The element table: each DamageType owns one hue no status or UI message reuses (POL-B4). The hue lives
        /// in vfxstyle.json (GG-T253, POL-E4); these literals are only the fallback before content loads.</summary>
        public static Color Element(DamageType t)
        {
            var st = GameApp.I?.Content?.Vfx?.Element(t);
            if (st?.Core != null) return Hex(st.Core, Color.white);
            switch (t)
            {
                case DamageType.Fire: return new Color(1f, 0.45f, 0.15f);
                case DamageType.Ice: return new Color(0.5f, 0.85f, 1f);
                case DamageType.Lightning: return new Color(1f, 0.95f, 0.35f);
                case DamageType.Poison: return new Color(0.6f, 1f, 0.3f);
                case DamageType.Holy: return new Color(1f, 0.97f, 0.78f);
                case DamageType.Dark: return new Color(0.62f, 0.3f, 0.95f);
                case DamageType.Arcane: return new Color(1f, 0.45f, 0.85f);
                default: return new Color(0.85f, 0.88f, 0.95f);
            }
        }

        public static Color Role(string role)
        {
            switch (role)
            {
                case "Tank": return new Color(0.95f, 0.36f, 0.3f);
                case "Healer": return new Color(0.4f, 0.9f, 0.5f);
                case "Mage": return new Color(0.55f, 0.6f, 1f);
                case "Striker": return new Color(1f, 0.66f, 0.25f);
                case "Support": return new Color(0.35f, 0.85f, 0.85f);
                case "Ranged": return new Color(0.68f, 0.85f, 0.35f);
                case Ui.TamedRole: return Tamed; // a domesticated monster's badge (GG-T303)
                default: return new Color(0.8f, 0.8f, 0.85f);
            }
        }

        /// <summary>An item rarity's colour (USS <c>--color-rarity-*</c>), for a rarity named inside rich text.</summary>
        public static Color Rarity(ItemRarity r)
        {
            switch (r)
            {
                // GG-T495: junk, rare and epic lifted to 4.5:1 on the Classic window's lightest stop (were 2.4, 3.4, 3.0).
                case ItemRarity.Junk: return new Color(0.753f, 0.753f, 0.769f);
                case ItemRarity.Uncommon: return new Color(0.43f, 0.9f, 0.47f);
                case ItemRarity.Rare: return new Color(0.588f, 0.784f, 1f);
                case ItemRarity.Epic: return new Color(0.863f, 0.698f, 1f);
                case ItemRarity.Legendary: return new Color(1f, 0.78f, 0.27f);
                default: return new Color(0.925f, 0.925f, 0.94f);
            }
        }

        public static Color WithA(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>A #RRGGBB content color (vfxstyle.json), or <paramref name="fallback"/> when absent or malformed.</summary>
        public static Color Hex(string hex, Color fallback) =>
            hex != null && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }

    /// <summary>Type scale ≈1.25 (POL-A1). Body ≥ 40, captions ≥ 30 (POL-A2).</summary>
    public static class TypeScale
    {
        public const float Badge = 24, Caption = 30, Small = 36, Body = 40, H3 = 48, H2 = 60, H1 = 76, Display = 96, Title = 120;
    }

    /// <summary>Settings → Text size (GG-T495): 100, 115 or 130% of the type scale from badge to H2. The root element takes
    /// <c>text-115</c> / <c>text-130</c>, and Theme.uss redefines the <c>--type-*</c> custom properties under it, so every
    /// USS-sized label grows at once with no rebuild. The headline sizes (H1, Display, Title) and the battle's fixed-geometry
    /// labels (board floats, tile badges, set in code) stay as they are: they are already large, and grow they'd break the grid.</summary>
    public static class TextSize
    {
        public static readonly int[] Options = { 100, 115, 130 };

        /// <summary>A saved percent, or 100 when it isn't one of <see cref="Options"/>.</summary>
        public static int Clamp(int pct) => Array.IndexOf(Options, pct) >= 0 ? pct : 100;

        public static int Next(int pct) => Options[(Array.IndexOf(Options, Clamp(pct)) + 1) % Options.Length];

        /// <summary>Puts the class for <paramref name="pct"/> on the UI root and takes the others off.</summary>
        public static void Apply(UnityEngine.UIElements.VisualElement root, int pct)
        {
            if (root == null) return;
            pct = Clamp(pct);
            foreach (var o in Options) if (o != 100) root.EnableInClassList("text-" + o, o == pct);
        }
    }

    /// <summary>
    /// GG-T551: the smallest text a screen may draw. The stage scales uniformly to fit the screen, so on a small low-dpi
    /// phone (480×800 at 160 dpi) a 30-px caption lands at ~9 screen px, too few pixels to read. The floor is
    /// <see cref="MinPx"/> screen px, or <see cref="MinDp"/> dp on a dense screen (whichever is larger); when the badge to
    /// body sizes would land under it, the UI root takes a <c>type-floor-N</c> class and Theme.uss raises those
    /// <c>--type-*</c> sizes to at least N design px. It composes with Settings → Text size (Theme.uss has a rule for each
    /// pair, every size the larger of the two), so the setting keeps working. Every mainstream phone and tablet (and the
    /// harness windows) clear the floor already and see no change.
    /// </summary>
    public static class TypeFloor
    {
        /// <summary>Fewest screen pixels a line of text may be drawn at.</summary>
        public const float MinPx = 12f;
        /// <summary>Smallest physical size, in dp (1/160 inch), on a screen dense enough that it beats <see cref="MinPx"/>.</summary>
        public const float MinDp = 9f;
        /// <summary>The floors Theme.uss has classes for, in design px. The largest stops at H3 (48), which is never raised.</summary>
        public static readonly int[] Steps = { 32, 36, 40, 44, 48 };
        // A need between the badge (24) and 32 rounds up to the first step.

        /// <summary>The live floor in design px (0 = none); set by <see cref="Apply"/>.</summary>
        public static int Current { get; private set; }

        /// <summary>The floor step (design px, 0 = none) for a stage drawn at <paramref name="screenPxPerDesignPx"/> on a
        /// screen of <paramref name="dpi"/> (0 = unknown, read as 160).</summary>
        public static int StepFor(float screenPxPerDesignPx, float dpi)
        {
            if (screenPxPerDesignPx <= 0f) return 0;
            float floorPx = Mathf.Max(MinPx, MinDp * (dpi > 0f ? dpi : 160f) / 160f);
            float need = floorPx / screenPxPerDesignPx;
            if (need <= TypeScale.Badge + 0.5f) return 0;
            foreach (var s in Steps) if (s >= need - 0.5f) return s;
            return Steps[Steps.Length - 1];
        }

        /// <summary>Puts the <c>type-floor-N</c> class for this screen on the UI root and takes the others off.</summary>
        public static void Apply(UnityEngine.UIElements.VisualElement root, float screenPxPerDesignPx, float dpi)
        {
            if (root == null) return;
            Current = StepFor(screenPxPerDesignPx, dpi);
            foreach (var s in Steps) root.EnableInClassList("type-floor-" + s, s == Current);
        }
    }

    /// <summary>8-px spacing scale (POL-C1); S4 is the half step for hairline insets inside small widgets.</summary>
    public static class Space
    {
        public const float S0 = 0, S4 = 4, S8 = 8, S16 = 16, S24 = 24, S32 = 32, S48 = 48, S64 = 64, S96 = 96;
        /// <summary>Half of the 24-px gap between tap targets (POL-C3): each neighbour's margin, so two add up to S24
        /// (USS <c>--space-12</c>).</summary>
        public const float TargetHalfGap = 12;
    }

    /// <summary>Named element sizes in design px (POL-C1). Design px already scale uniformly to every resolution
    /// (bible §26.2), so fixed-size things (icons, pips, portraits) use these; layout widths use <see cref="Fit"/>.</summary>
    public static class Size
    {
        // Lines, bars and pips
        public const float Underline = 2, Hairline = 3, Track = 6, Bar = 8, Shard = 13, Gauge = 14, HpBar = 18, Crack = 24, Pip = 34;
        // Icons and pictures
        public const float IconS = 40, Chip = 50, IconM = 56, Orb = 60, Icon = 64, IconL = 76, AvatarW = 80, Glyph = 88,
            Avatar = 96, PartySlot = 180, Thumb = 120, Medallion = 140, Gem = 192, PortraitW = 360, PortraitH = 400,
            ArtW = 420, ArtH = 620;
        // GG-T673: the mana orbs row (one orb per bank slot) and the trails that fly into it. OrbBloom is how far an orb's
        // bloom spills past it; TrailSwing/TrailLift bound the bezier control point's random sideways and upward offset.
        // GG-T688: OrbRow is the row's height and the largest an orb gets (owning 1-5 they're big bulbs); OrbMin is the
        // smallest it may shrink to at 20; OrbGap the equal gap between orbs; OrbLabel the "MANA" label's width.
        public const float OrbRow = 88, OrbMin = 36, OrbGap = 12, OrbLabel = 96, OrbBloom = 26, TrailSpark = 22, TrailSwing = 120, TrailLift = 160;
        // GG-T678: Manapunk window hardware. A corner clamp's arm length and bar thickness and its bolt head; the edge
        // conduit's tube width, its flowing light's length, and the shortest window that gets a conduit at all.
        public const float ClampArm = 44, ClampBar = 6, ClampBolt = 14, Conduit = 8, ConduitGlow = 160, ConduitMinHeight = 220;
        // Controls and table columns (POL-C2: a tap target is at least 132×132)
        public const float Plate = 92, Target = 132, Cell = 170, Slot = 196, Card = 300,
            ColumnS = 70, Column = 90, ColumnL = 100, Label = 180;
        /// <summary>A multi-line paste box (a save transfer code).</summary>
        public const float CodeBox = 360;
    }

    /// <summary>
    /// The menu kit (GG-T454, owner 2026-11-20: "every single menu needs to have one standardized look and feel that can be
    /// repeated over and over and over and utilizes space as efficiently as possible"). Every menu screen is built from the
    /// same few roles (<see cref="Ui"/>'s Header, Tabs, Panel/ListPanel/DetailPanel, Section, MenuRow, Card, Note, Footer,
    /// Empty) and these are their only sizes. Theme.uss's <c>.kit-*</c> classes carry the same values.
    /// <para>Type: four sizes and one rule — page title 60, heading 48 (a window's or detail pane's title), body 40 (row
    /// labels and values, card names, buttons, and every wrapped sentence), caption 30 (one-line meta, section headers,
    /// hints). Anything that wraps is body; anything that is a single line of meta is caption.</para>
    /// <para>Space: windows sit <see cref="Gutter"/> from the stage edge and <see cref="Gap"/> apart, padded
    /// <see cref="PadY"/>/<see cref="PadX"/>; every row and card is at least one <see cref="Row"/> tall with 24 px between
    /// them (two 12-px margins, POL-C3); a row's note and a section header start at the row label's left edge
    /// (<see cref="Indent"/>). Lists and detail panes fill the column and scroll inside themselves.</para>
    /// </summary>
    public static class Kit
    {
        public const float Title = TypeScale.H2, Heading = TypeScale.H3, Body = TypeScale.Body, Caption = TypeScale.Caption;
        /// <summary>The page header bar's height (USS <c>.header</c>); under a notch it grows by the inset (GG-T483).</summary>
        public const float HeaderHeight = 150f;
        /// <summary>Stage edge to window edge, left and right (= <see cref="Fit.Edge"/>).</summary>
        public const float Gutter = Fit.Edge;
        /// <summary>Between stacked windows, and between the header and the first window (each window's S8 margin, twice).</summary>
        public const float Gap = Space.S16;
        /// <summary>A window's inner padding: <see cref="PadY"/> top and bottom, <see cref="PadX"/> left and right.</summary>
        public const float PadY = Space.S16, PadX = Space.S24;
        /// <summary>A menu row's height: one tap target (POL-C2).</summary>
        public const float Row = Size.Target;
        /// <summary>Where a row's label starts inside it (row padding 8 + cursor 56 + 8): a row's note and a section header
        /// line up with it.</summary>
        public const float Indent = Space.S8 + Size.IconM + Space.S8;
        /// <summary>A list card's portrait (quests, mercenaries, summons, bounties, caravan, party).</summary>
        public const float Thumb = Size.Thumb;
        /// <summary>A compact card's portrait (the Camp party window, the Heroes roster strip).</summary>
        public const float Avatar = Size.Avatar;
        /// <summary>The most of its column a detail pane takes before it scrolls inside itself (the list above gets the rest).</summary>
        public const float DetailMaxPct = 45f;
    }

    /// <summary>Aspect-agnostic layout units (GG-T272): fractions of the safe-area stage, which grows from 1170×2532 up to
    /// 3:4 (1899 wide) or 9:23 (2990 tall) (§26.3, GG-T398). Read them when a screen builds; the router rebuilds the
    /// current screen on <see cref="GameApp.StageResized"/> (the battle re-lays itself out).</summary>
    public static class Fit
    {
        /// <summary>The one screen-edge gutter: every full-width panel sits this far from the stage's left and right edges.</summary>
        public const float Edge = Space.S24;
        /// <summary>The widest a column of menus, rows, text or HUD controls grows: the widest stage (3:4), so the column is
        /// always the full stage. GG-T550 (owner: "every screen scales to fill all aspects 9:23-3:4 with consistent gutters,
        /// no dead margins"): it used to stop at the 9:16 width (1424) and centre, which left ~240-px empty backdrop bands
        /// either side of every page on a 3:4 tablet while the hub and battle filled the width. Paragraphs still stop at
        /// about 60 characters (<see cref="Typography"/>), so lines never stretch absurdly.</summary>
        public const float ColumnMax = GameApp.MaxStageWidth;
        /// <summary>A position far outside any stage, for an element laid out only to be measured.</summary>
        public const float Offstage = -20000f;
        public static float Width => GameApp.StageWidth;
        public static float Height => GameApp.StageHeight - GameApp.SafeTop - GameApp.SafeBottom;
        /// <summary>The content column's width: the stage width, capped at <see cref="ColumnMax"/>.</summary>
        public static float Column => Mathf.Min(Width, ColumnMax);
        /// <summary>The side inset that centres the content column on the stage (0 up to 9:16).</summary>
        public static float Inset => Mathf.Floor((Width - Column) / 2f);
        /// <summary><paramref name="pct"/> percent of the usable stage width.</summary>
        public static float W(float pct) => Width * pct / 100f;
        /// <summary><paramref name="pct"/> percent of the content column's width.</summary>
        public static float C(float pct) => Column * pct / 100f;
        /// <summary><paramref name="pct"/> percent of the usable stage height.</summary>
        public static float H(float pct) => Height * pct / 100f;
        /// <summary>Wide enough for three party cards abreast (2:3 and wider) instead of two.</summary>
        public static bool Roomy => Width >= 1600f;

        /// <summary>Pads a full-stage container so its children sit in the centred content column, plus <paramref name="pad"/>
        /// on each side (GG-T398).</summary>
        public static void Columnize(UnityEngine.UIElements.VisualElement el, float pad)
        {
            el.style.paddingLeft = Inset + pad;
            el.style.paddingRight = Inset + pad;
        }

        /// <summary>Centres an element of a full-stage column (a window under a header) in the content column, keeping
        /// <paramref name="margin"/> (the kit's <see cref="Kit.Gutter"/> by default, GG-T454) on each side (GG-T398).</summary>
        public static void Center(UnityEngine.UIElements.VisualElement el, float margin = Kit.Gutter)
        {
            el.style.marginLeft = Inset + margin;
            el.style.marginRight = Inset + margin;
        }
    }

    /// <summary>Duration tokens in seconds (POL-E1). Anything longer than Cinematic is a hold, also in the cinematic band.</summary>
    public static class Timing
    {
        public const float Instant = 0.08f, Quick = 0.15f, Standard = 0.25f, Emphasis = 0.4f, Dramatic = 0.7f, Cinematic = 1.2f;
        /// <summary>GG-T673: a lit orb's bloom breathes on this period; a mana trail drops a spark every TrailSparkEvery
        /// seconds, each living TrailSparkLife.</summary>
        public const float OrbBreathe = 2.4f, TrailSparkEvery = 0.03f, TrailSparkLife = 0.35f;
        /// <summary>GG-T678: one pass of contained mana flowing down a Manapunk window's edge conduit.</summary>
        public const float ConduitFlow = 3.6f;
    }

    /// <summary>GG-T668: a damage number strikes the target (falls onto it big and fast, squashes, settles, holds, drops
    /// away) instead of drifting up like a heal; a critical hit falls further, starts bigger and shakes its target.
    /// Distances and shakes in design px, times in seconds.</summary>
    public static class Impact
    {
        public const float StrikeDrop = 70f, StrikeDropCrit = 110f;
        public const float StrikeScale = 1.45f, StrikeScaleCrit = 1.9f, StrikeSquash = 0.88f;
        public const float StrikeIn = 0.09f, StrikeInCrit = 0.07f, StrikeSettle = Timing.Instant;
        public const float StrikeHold = 0.35f, StrikeHoldCrit = 0.5f, StrikeOut = 0.18f, StrikeExitDrop = 18f;
        /// <summary>Share of the fall kept while calm (Reduce Motion): the number still lands, with less travel.</summary>
        public const float CalmTravel = 0.3f;
        public const float HitTileShake = 14f, CritTileShake = 26f, CritIconShake = 12f;
    }

    /// <summary>Easing tokens (POL-E2).</summary>
    public static class Ease
    {
        public static readonly Func<float, float> Out = Tween.EaseOut;
        public static readonly Func<float, float> InOut = Tween.EaseInOut;
        public static readonly Func<float, float> Back = Tween.Back;
        public static readonly Func<float, float> Snap = t => 1f - Mathf.Pow(1f - t, 4f);
        /// <summary>0 → 1 → 0 over the run: a hop, a flash's rise and fall, a projectile's lift.</summary>
        public static readonly Func<float, float> Arc = t => Mathf.Sin(t * Mathf.PI);
    }
}
