using System.Collections;
using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>What a piece of floating battle text is saying (GG-T826, bible §9.1 "Combat text"). The kind picks the
    /// face, colour, outline, motion and size; the call only supplies the words and how big the moment is.</summary>
    public enum CombatTextKind
    {
        /// <summary>An ordinary hit's number.</summary>
        Damage,
        /// <summary>A critical hit's number: bigger, gold, slams down harder.</summary>
        Crit,
        /// <summary>The hit that took the target to 0 HP: the biggest, hottest number on the board.</summary>
        Overkill,
        /// <summary>HP restored: green, rises gently.</summary>
        Heal,
        /// <summary>A boon landed (haste, amplify, shields): warm, pops.</summary>
        Buff,
        /// <summary>A curse landed or gear broke: violet-red, shakes.</summary>
        Debuff,
        /// <summary>A damage-over-time or regen tick: small, drifts off.</summary>
        StatusTick,
        /// <summary>Timeline news (casting, charging, pushback, stagger): cool blue, drifts.</summary>
        Timer,
        /// <summary>A whiff or a dodge: pale, arcs aside.</summary>
        Miss,
        /// <summary>No effect at all (armored, warded): grey, shakes.</summary>
        Immune,
        /// <summary>An elemental weakness hit: hot orange, pops.</summary>
        Weak,
        /// <summary>A resisted effect (resisted tame, steady, clang): steel, arcs aside.</summary>
        Resist,
        /// <summary>Pincer, chain and tally totals: the combo shout, slams in.</summary>
        Combo,
        /// <summary>A big named moment (RULE BREAKER, ENRAGED, WAVE 2, BOUNTY): display face, pops and holds.</summary>
        Callout,
        /// <summary>Everything else that names an action or a pickup: plain, rises.</summary>
        Support,
        /// <summary>A status ran out or was cleansed (GG-T830): its name struck through, fading where it stood.</summary>
        Expire,
        /// <summary>A countdown reaching 0 (GG-T830): flashes and swells away.</summary>
        TimerEnd,
    }

    /// <summary>How a combat text moves on its way in and out.</summary>
    public enum CombatMotion
    {
        /// <summary>Swells in past full size, settles, holds, fades.</summary>
        Pop,
        /// <summary>Rises and fades (the old FloatAway).</summary>
        Rise,
        /// <summary>Hops sideways in a short parabola and fades.</summary>
        Arc,
        /// <summary>Falls onto the target big and fast, squashes, settles, holds, drops away (GG-T668 StrikeIn).</summary>
        Slam,
        /// <summary>Lands at once and jitters while it holds.</summary>
        Shake,
        /// <summary>Slow rise with a sideways sway.</summary>
        Drift,
        /// <summary>Jolts down onto the target, shudders, sinks away (GG-T830: debuffs drop).</summary>
        Drop,
        /// <summary>Snaps in from big like a clock hand, holds, fades (GG-T830: countdown digits).</summary>
        Tick,
        /// <summary>Pops in and flashes white, then swells out (GG-T830: a countdown hitting 0).</summary>
        Flash,
        /// <summary>Holds still, then fades and shrinks where it stood (GG-T830: expiry and cleanse).</summary>
        Fade,
    }

    /// <summary>How hard a blow read against its target (GG-T827): its share of the target's max HP, in four tiers that
    /// size the number (chip under 10%, solid under 25%, heavy under 50%, massive from half up).</summary>
    public enum DamageTier { Chip, Solid, Heavy, Massive }

    /// <summary>Which face a combat text draws in. Impact kinds use the heavy rounded cut; callouts use the Display
    /// face; chatter uses the body face.</summary>
    public enum CombatFace { Heavy, Display, Body }

    /// <summary>One row of the combat text style table. Sizes are multiples of <see cref="TypeScale.Base"/> (the battle's
    /// own exception to GG-T770's one GUI size): <see cref="Scale"/> is the resting size, <see cref="Grow"/> the extra
    /// share added at magnitude 1.</summary>
    public readonly struct CombatTextStyle
    {
        public readonly CombatFace Face;
        public readonly Color Color;
        public readonly float Scale, Grow;
        public readonly float Outline;
        public readonly Color OutlineColor;
        public readonly bool Shadow;
        public readonly CombatMotion Motion;
        /// <summary>Seconds the text is on screen in total (scaled by Battle speed through <see cref="Tween"/>).</summary>
        public readonly float Duration;
        /// <summary>Design px above the anchor the text rests at, as a share of a tile.</summary>
        public readonly float Lift;

        public CombatTextStyle(CombatFace face, Color color, float scale, float grow, float outline, Color outlineColor, bool shadow, CombatMotion motion, float duration, float lift)
        {
            Face = face; Color = color; Scale = scale; Grow = grow; Outline = outline; OutlineColor = outlineColor;
            Shadow = shadow; Motion = motion; Duration = duration; Lift = lift;
        }

        /// <summary>The font size in design px for this style at a magnitude (0..1, clamped).</summary>
        public float SizeAt(float magnitude) => TypeScale.Base * (Scale + Grow * Mathf.Clamp01(magnitude));
    }

    /// <summary>
    /// GG-T826 (epic GG-T823, owner: "action packed and bombastic like Devil May Cry"): every floating battle text goes
    /// through here, so damage, heals, buffs, debuffs, ticks, timer news and callouts share one cohesive look that still
    /// varies by what they say. <see cref="Show(UnitView, CombatTextKind, string, float, Color?)"/> is the one entry
    /// point (BattleScreen.FloatText delegates to it, BoardView.Float builds its label through <see cref="Build"/>).
    /// Texts that land on the same anchor while another is still up take the next free stacking slot, so simultaneous
    /// texts never overlap. Reduce Motion (<see cref="Tween.Calm"/>) keeps every text but trims travel, swell and jitter;
    /// Battle speed scales every duration through <see cref="Tween.Run"/>.
    /// </summary>
    public sealed class CombatText
    {
        private static readonly Color Edge = Palette.TextEdge;

        /// <summary>The style table, one row per <see cref="CombatTextKind"/>.</summary>
        public static CombatTextStyle StyleOf(CombatTextKind kind)
        {
            switch (kind)
            {
                case CombatTextKind.Damage: return new CombatTextStyle(CombatFace.Heavy, Palette.White, 1.35f, 1f, 4f, Edge, true, CombatMotion.Slam, 0.75f, 0f);
                case CombatTextKind.Crit: return new CombatTextStyle(CombatFace.Heavy, new Color(1f, 0.85f, 0.2f), 1.9f, 0.9f, 5f, new Color(0.35f, 0.1f, 0f), true, CombatMotion.Slam, 0.95f, 0f);
                case CombatTextKind.Overkill: return new CombatTextStyle(CombatFace.Heavy, new Color(1f, 0.42f, 0.18f), 2.1f, 0.8f, 6f, new Color(0.3f, 0f, 0f), true, CombatMotion.Slam, 1.05f, 0f);
                case CombatTextKind.Heal: return new CombatTextStyle(CombatFace.Heavy, Palette.Good, 1.3f, 0.4f, 3f, Edge, true, CombatMotion.Rise, 0.8f, 0.1f);
                case CombatTextKind.Buff: return new CombatTextStyle(CombatFace.Heavy, Palette.Buff, 1.05f, 0.2f, 3f, Edge, true, CombatMotion.Rise, 0.85f, 0.3f);
                case CombatTextKind.Debuff: return new CombatTextStyle(CombatFace.Heavy, new Color(0.86f, 0.45f, 1f), 1.05f, 0.2f, 3f, Edge, true, CombatMotion.Drop, 0.85f, 0.3f);
                case CombatTextKind.StatusTick: return new CombatTextStyle(CombatFace.Heavy, new Color(0.75f, 0.95f, 0.45f), 0.85f, 0.25f, 2f, Edge, true, CombatMotion.Drift, 0.7f, 0f);
                case CombatTextKind.Timer: return new CombatTextStyle(CombatFace.Heavy, new Color(0.6f, 0.85f, 1f), 1f, 0.2f, 3f, Edge, true, CombatMotion.Tick, 0.8f, 0.3f);
                case CombatTextKind.TimerEnd: return new CombatTextStyle(CombatFace.Heavy, new Color(1f, 0.9f, 0.55f), 1.4f, 0.4f, 4f, new Color(0.3f, 0.1f, 0f), true, CombatMotion.Flash, 0.9f, 0.3f);
                case CombatTextKind.Expire: return new CombatTextStyle(CombatFace.Heavy, new Color(0.72f, 0.72f, 0.78f), 0.95f, 0f, 2f, Edge, true, CombatMotion.Fade, 1f, 0.3f);
                case CombatTextKind.Miss: return new CombatTextStyle(CombatFace.Heavy, Palette.Info, 1.1f, 0f, 3f, Edge, true, CombatMotion.Arc, 0.6f, 0.1f);
                case CombatTextKind.Immune: return new CombatTextStyle(CombatFace.Heavy, new Color(0.82f, 0.84f, 0.9f), 1.3f, 0f, 4f, Edge, true, CombatMotion.Shake, 0.85f, 0.1f);
                case CombatTextKind.Weak: return new CombatTextStyle(CombatFace.Heavy, new Color(1f, 0.6f, 0.2f), 1.2f, 0.3f, 4f, new Color(0.3f, 0.05f, 0f), true, CombatMotion.Pop, 0.7f, 0.35f);
                case CombatTextKind.Resist: return new CombatTextStyle(CombatFace.Heavy, new Color(0.75f, 0.85f, 0.95f), 1f, 0f, 3f, Edge, true, CombatMotion.Arc, 0.7f, 0.3f);
                case CombatTextKind.Combo: return new CombatTextStyle(CombatFace.Heavy, Palette.GoldLight, 1.6f, 0.6f, 5f, new Color(0.3f, 0.12f, 0f), true, CombatMotion.Slam, 0.9f, 0.2f);
                case CombatTextKind.Callout: return new CombatTextStyle(CombatFace.Display, Palette.GoldLight, 1.5f, 0.5f, 3f, Edge, true, CombatMotion.Pop, 1.1f, 0.35f);
                default: return new CombatTextStyle(CombatFace.Heavy, Palette.Text, 1f, 0.2f, 2f, Edge, true, CombatMotion.Rise, 0.8f, 0.3f);
            }
        }

        private readonly BoardView board;
        private readonly MonoBehaviour host;
        /// <summary>Stacking slots in use per anchor cell (half-tile grid): a new text takes the lowest free slot.</summary>
        private readonly Dictionary<Vector2Int, HashSet<int>> slots = new Dictionary<Vector2Int, HashSet<int>>();

        public CombatText(BoardView board, MonoBehaviour host) { this.board = board; this.host = host; }

        /// <summary>The board these texts float over (a new battle builds a new board, and so a new CombatText).</summary>
        public BoardView Board => board;

        /// <summary>Shows a combat text over a unit (centred on its footprint).</summary>
        public Label Show(UnitView unit, CombatTextKind kind, string text, float magnitude = 0f, Color? tint = null) =>
            Show(BoardView.CenterOf(unit.Tile, unit.W, unit.H), kind, text, magnitude, tint);

        /// <summary>Shows a combat text over a tile.</summary>
        public Label Show(Coord tile, CombatTextKind kind, string text, float magnitude = 0f, Color? tint = null) =>
            Show(BoardView.CenterOf(tile), kind, text, magnitude, tint);

        /// <summary>Shows a combat text at a board point (design px). <paramref name="magnitude"/> 0..1 says how big the
        /// moment is (damage as a share of max HP, a chain's length) and grows the text; <paramref name="tint"/>
        /// overrides the style's colour (team colours, an element's hue).</summary>
        public Label Show(Vector2 at, CombatTextKind kind, string text, float magnitude = 0f, Color? tint = null)
        {
            var s = StyleOf(kind);
            float size = s.SizeAt(magnitude);
            var cell = new Vector2Int(Mathf.RoundToInt(at.x / (BoardView.Tile * 0.5f)), Mathf.RoundToInt(at.y / (BoardView.Tile * 0.5f)));
            if (!slots.TryGetValue(cell, out var used)) slots[cell] = used = new HashSet<int>();
            int slot = 0;
            while (used.Contains(slot)) slot++;
            used.Add(slot);
            // Each extra slot steps up a line and alternates sides, so two numbers landing together read as two.
            var offset = new Vector2(slot == 0 ? 0f : (slot % 2 == 1 ? 1f : -1f) * BoardView.Tile * 0.14f, -BoardView.Tile * s.Lift - slot * size * 0.95f);
            var l = Build(board, text, at + offset, s, tint ?? s.Color, size);
            l.AddToClassList("combat-text");
            l.AddToClassList("combat-text--" + kind.ToString().ToLowerInvariant());
            host.StartCoroutine(Animate(l, s, () => { used.Remove(slot); if (used.Count == 0) slots.Remove(cell); }));
            return l;
        }

        /// <summary>GG-T827: a crit's extra hit-stop (seconds held on the impact frame, on top of the tier's own).</summary>
        public const float CritHitStop = 0.08f;

        /// <summary>GG-T827: the tier a blow of <paramref name="amount"/> reads as against a target of <paramref name="maxHp"/>.</summary>
        public static DamageTier TierOf(int amount, int maxHp)
        {
            if (maxHp <= 0 || amount <= 0) return DamageTier.Chip;
            float share = (float)amount / maxHp;
            return share < 0.1f ? DamageTier.Chip : share < 0.25f ? DamageTier.Solid : share < 0.5f ? DamageTier.Heavy : DamageTier.Massive;
        }

        /// <summary>GG-T827: the magnitude (0..1) a tier sizes its number at: chip 0, solid 1/3, heavy 2/3, massive 1.</summary>
        public static float MagnitudeOf(DamageTier tier) => (int)tier / 3f;

        /// <summary>GG-T827: the tag an elemental read puts beside the number (null for a normal hit).</summary>
        public static string TagOf(HitEffectiveness e) =>
            e == HitEffectiveness.Weak ? "WEAK" : e == HitEffectiveness.Resist ? "RESIST" : e == HitEffectiveness.Immune ? "IMMUNE" : null;

        /// <summary>
        /// GG-T827: a damage number read by magnitude. <paramref name="kind"/> (Damage, Crit, Overkill, Combo for a pincer
        /// tally) picks the look; the blow's share of <paramref name="maxHp"/> picks one of four sizes
        /// (<see cref="TierOf"/>); a weakness, resistance or immunity tags it (WEAK / RESIST / IMMUNE in their own kinds,
        /// on the next stacking slot); a kill with damage to spare (<paramref name="overkill"/> above 0) prints OVERKILL.
        /// </summary>
        public Label Damage(Vector2 at, CombatTextKind kind, string text, int amount, int maxHp, HitEffectiveness effect = HitEffectiveness.Normal, int overkill = 0, Color? tint = null)
        {
            // GG-T2071: a blow an immunity stopped cold reads as one word, IMMUNE, not a "0" with a small tag beside it.
            if (effect == HitEffectiveness.Immune && amount <= 0) return Show(at, CombatTextKind.Immune, TagOf(effect), 0.5f);
            var tier = TierOf(amount, maxHp);
            var l = Show(at, kind, text, MagnitudeOf(tier), tint);
            l.AddToClassList("combat-text--" + tier.ToString().ToLowerInvariant());
            var tag = TagOf(effect);
            if (tag != null)
            {
                var tagKind = effect == HitEffectiveness.Weak ? CombatTextKind.Weak : effect == HitEffectiveness.Resist ? CombatTextKind.Resist : CombatTextKind.Immune;
                Show(at, tagKind, tag, effect == HitEffectiveness.Weak ? MagnitudeOf(tier) : 0f);
            }
            if (overkill > 0)
                Show(at, CombatTextKind.Callout, "OVERKILL", maxHp > 0 ? Mathf.Clamp01((float)overkill / maxHp) : 0.5f, StyleOf(CombatTextKind.Overkill).Color);
            return l;
        }

        /// <summary>The up-chevron a boon carries, the down-chevron a curse carries (GG-T830; both in the bundled fonts).</summary>
        public const string Up = "▲", Down = "▼";

        /// <summary>A status landing (GG-T830): a buff rises with an up-chevron, a debuff drops with a down-chevron, both
        /// in the status's own colour (<c>BuffDef.Color</c>).</summary>
        public Label Status(Vector2 at, bool buff, string name, Color color) =>
            Show(at, buff ? CombatTextKind.Buff : CombatTextKind.Debuff, (buff ? Up : Down) + " " + name, 0f, color);

        /// <summary>A damage-over-time tick (GG-T830): small, the status's pip glyph then the amount, in its colour.</summary>
        public Label Tick(Vector2 at, string glyph, string amount, Color color, float magnitude = 0f) =>
            Show(at, CombatTextKind.StatusTick, string.IsNullOrEmpty(glyph) ? amount : glyph + " " + amount, magnitude, color);

        /// <summary>A status running out or being cleansed (GG-T830): its name struck through, fading in a dimmed colour.</summary>
        public Label Expired(Vector2 at, string name, Color color) =>
            Show(at, CombatTextKind.Expire, "<s>" + name + "</s>", 0f, Color.Lerp(color, StyleOf(CombatTextKind.Expire).Color, 0.45f));

        /// <summary>One step of a countdown (GG-T830): 3, 2, 1 tick in as Timer digits (after an optional glyph), growing
        /// as they near 0; at 0 it flashes, showing <paramref name="zero"/> (what fires) or "0".</summary>
        public Label Countdown(Vector2 at, int left, Color? tint = null, string glyph = null, string zero = null)
        {
            if (left <= 0) return Show(at, CombatTextKind.TimerEnd, string.IsNullOrEmpty(zero) ? "0" : zero, 0.5f, null);
            string n = left.ToString();
            return Show(at, CombatTextKind.Timer, string.IsNullOrEmpty(glyph) ? n : glyph + " " + n, Mathf.Clamp01((4 - left) / 3f), tint);
        }

        /// <summary>Characters a text prints, rich-text tags (<c>&lt;s&gt;</c>) left out.</summary>
        private static int VisibleLength(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0; bool tag = false;
            foreach (char ch in text) { if (ch == '<') tag = true; else if (ch == '>' && tag) tag = false; else if (!tag) n++; }
            return n;
        }

        /// <summary>Builds (does not animate) a combat text label on the board's fx layer, its centre at
        /// <paramref name="at"/>. <see cref="BoardView.Float"/> builds through this too.</summary>
        public static Label Build(BoardView board, string text, Vector2 at, CombatTextStyle s, Color color, float size)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            if (s.Shadow) l.AddToClassList("shadow");
            l.style.position = Position.Absolute;
            // Wide enough for the words on one line (about 0.62 em a glyph), never wider than the board.
            float boardW = BoardView.Tile * BattleConfig.Cols;
            float w = Mathf.Min(boardW, Mathf.Max(Size.Card, size * (0.62f * VisibleLength(text) + 1f)));
            // GG-T317: keep the label's box on the board so a unit in the edge column can't push it off-stage.
            l.style.left = Mathf.Clamp(at.x - w / 2f, 0f, Mathf.Max(0f, boardW - w));
            l.style.top = at.y - size * 0.75f;
            l.style.width = w;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.fontSize = size;
            switch (s.Face)
            {
                case CombatFace.Display: Fonts.UseDisplay(l); break;
                case CombatFace.Body: Fonts.UseBody(l); break;
                default: Fonts.UseHeavy(l); break;
            }
            if (s.Outline > 0f) { l.style.unityTextOutlineWidth = s.Outline; l.style.unityTextOutlineColor = s.OutlineColor; }
            l.style.color = color;
            board.FxLayer.Add(l);
            return l;
        }

        private static IEnumerator Animate(Label l, CombatTextStyle s, System.Action done)
        {
            float rest = l.style.top.value.value, left = l.style.left.value.value;
            bool calm = Tween.Calm;
            float d = s.Duration;
            switch (s.Motion)
            {
                case CombatMotion.Slam:
                {
                    bool hard = s.Scale >= 1.9f;
                    float travel = (hard ? Impact.StrikeDropCrit : Impact.StrikeDrop) * (calm ? Impact.CalmTravel : 1f);
                    float big = calm ? 1.1f : hard ? Impact.StrikeScaleCrit : Impact.StrikeScale;
                    l.style.top = rest - travel;
                    l.style.scale = new Scale(Vector2.one * big);
                    yield return Tween.Run(hard ? Impact.StrikeInCrit : Impact.StrikeIn, k =>
                    {
                        float e = k * k;
                        l.style.top = rest - travel * (1f - e);
                        l.style.scale = new Scale(Vector2.one * Mathf.Lerp(big, Impact.StrikeSquash, e));
                    });
                    if (calm) l.style.scale = new Scale(Vector2.one);
                    else yield return Tween.Run(Impact.StrikeSettle, k => l.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(Impact.StrikeSquash, 1f, Ease.Back(k))));
                    yield return Tween.Wait(Mathf.Max(0f, d - Impact.StrikeIn - Impact.StrikeSettle - Impact.StrikeOut));
                    yield return Tween.Run(Impact.StrikeOut, k =>
                    {
                        l.style.top = rest + Impact.StrikeExitDrop * Ease.Out(k);
                        l.style.opacity = 1f - k;
                    });
                    break;
                }
                case CombatMotion.Pop:
                {
                    float swell = calm ? 1f : 1.35f;
                    yield return Tween.Run(d * 0.2f, k => l.style.scale = new Scale(Vector2.one * (calm ? 1f : Mathf.LerpUnclamped(0.5f, swell, Ease.Back(k)))));
                    if (!calm) yield return Tween.Run(d * 0.1f, k => l.style.scale = new Scale(Vector2.one * Mathf.Lerp(swell, 1f, k)));
                    yield return Tween.Wait(d * 0.45f);
                    yield return Tween.Run(d * 0.25f, k => { l.style.opacity = 1f - k; l.style.top = rest - 20f * k; });
                    break;
                }
                case CombatMotion.Shake:
                {
                    float amp = calm ? 0f : 8f;
                    yield return Tween.Run(d * 0.75f, k =>
                    {
                        float fall = 1f - k;
                        l.style.left = left + Random.Range(-amp, amp) * fall;
                        l.style.top = rest + Random.Range(-amp, amp) * 0.5f * fall;
                    });
                    l.style.left = left; l.style.top = rest;
                    yield return Tween.Run(d * 0.25f, k => l.style.opacity = 1f - k);
                    break;
                }
                case CombatMotion.Arc:
                {
                    float side = Random.value < 0.5f ? -1f : 1f, dx = (calm ? 20f : 60f) * side, hop = calm ? 20f : 55f;
                    yield return Tween.Run(d, k =>
                    {
                        l.style.left = left + dx * k;
                        l.style.top = rest - hop * Ease.Arc(k * 0.5f) - (calm ? 0f : 30f * k * k);
                        l.style.opacity = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                    });
                    break;
                }
                case CombatMotion.Drift:
                {
                    float rise = calm ? 25f : 60f, sway = calm ? 0f : 10f;
                    yield return Tween.Run(d, k =>
                    {
                        l.style.top = rest - rise * k;
                        l.style.left = left + sway * Mathf.Sin(k * Mathf.PI * 2f);
                        l.style.opacity = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
                    });
                    break;
                }
                case CombatMotion.Drop:
                {
                    float fall = calm ? 0f : 22f, amp = calm ? 0f : 5f, sink = calm ? 15f : 45f;
                    yield return Tween.Run(d * 0.15f, k => l.style.top = rest - fall * (1f - Ease.Out(k)));
                    yield return Tween.Run(d * 0.25f, k =>
                    {
                        l.style.left = left + Random.Range(-amp, amp) * (1f - k);
                        l.style.top = rest + Random.Range(0f, amp) * (1f - k);
                    });
                    l.style.left = left; l.style.top = rest;
                    yield return Tween.Wait(d * 0.3f);
                    yield return Tween.Run(d * 0.3f, k => { l.style.top = rest + sink * k * k; l.style.opacity = 1f - k; });
                    break;
                }
                case CombatMotion.Tick:
                {
                    float from = calm ? 1f : 1.5f;
                    yield return Tween.Run(d * 0.15f, k => l.style.scale = new Scale(Vector2.one * Mathf.Lerp(from, 1f, Ease.Out(k))));
                    yield return Tween.Wait(d * 0.55f);
                    yield return Tween.Run(d * 0.3f, k => { l.style.opacity = 1f - k; l.style.top = rest - (calm ? 5f : 14f) * k; });
                    break;
                }
                case CombatMotion.Flash:
                {
                    var baseColor = l.style.color.value;
                    float swell = calm ? 1f : 1.4f;
                    yield return Tween.Run(d * 0.15f, k => l.style.scale = new Scale(Vector2.one * (calm ? 1f : Mathf.LerpUnclamped(0.6f, swell, Ease.Back(k)))));
                    // Reduce Motion keeps one steady fade from white instead of the strobe.
                    yield return Tween.Run(d * 0.5f, k =>
                    {
                        float blink = calm ? 1f - k : Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f));
                        l.style.color = Color.Lerp(baseColor, Palette.White, blink);
                        if (!calm) l.style.scale = new Scale(Vector2.one * Mathf.Lerp(swell, 1.1f, k));
                    });
                    l.style.color = baseColor;
                    yield return Tween.Run(d * 0.35f, k =>
                    {
                        l.style.opacity = 1f - k;
                        if (!calm) l.style.scale = new Scale(Vector2.one * (1.1f + 0.4f * Ease.Out(k)));
                    });
                    break;
                }
                case CombatMotion.Fade:
                {
                    yield return Tween.Wait(d * 0.35f);
                    yield return Tween.Run(d * 0.65f, k =>
                    {
                        l.style.opacity = 1f - k;
                        l.style.scale = new Scale(Vector2.one * (calm ? 1f : 1f - 0.12f * k));
                        l.style.top = rest + (calm ? 0f : 10f * k);
                    });
                    break;
                }
                default: // Rise: the old FloatAway
                {
                    float rise = calm ? 30f : 90f;
                    yield return Tween.Run(d, k =>
                    {
                        l.style.top = rest - rise * Tween.EaseOut(k);
                        l.style.opacity = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                        float sc = calm ? 1f : k < 0.15f ? 0.6f + k / 0.15f * 0.5f : 1.1f - 0.1f * Mathf.Min(1, (k - 0.15f) * 4);
                        l.style.scale = new Scale(Vector2.one * sc);
                    });
                    break;
                }
            }
            l.RemoveFromHierarchy();
            done?.Invoke();
        }
    }
}
