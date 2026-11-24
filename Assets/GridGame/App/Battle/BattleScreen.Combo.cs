using System.Collections;
using System.Collections.Generic;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The combo meter (GG-T851, BIBLE §1.2.8), Devil May Cry style: it plays <see cref="ComboChangedEvent"/> and
    /// <see cref="ComboEndedEvent"/> without holding up the beat. A rank letter slams in, bigger and hotter per rank
    /// (D cool grey up to SSS blazing red); a decay bar drains between credits and the meter sags when it runs dry; a
    /// rank-up flashes the frame, shakes the screen and plays a rising sting. Under the frame each credit pops a
    /// contributor chip, the hero's thumbnail (<see cref="Art.Thumb(string)"/>) with "+N" and the credit's label in the
    /// <see cref="CombatTextKind.Combo"/> style, supporters' chips in mana blue so their share is seen; the window's close
    /// stamps the final rank and lists each hero's attacker / supporter points. The frame and bar are <see cref="Shapes"/>
    /// (§11.6). It sits on the board's top-right corner in screen space (the board is the same design size at every
    /// stage aspect, so it is portrait-safe everywhere) and takes no input. Reduce Motion keeps every number and colour
    /// but drops the slam, shake, strobe and slides for plain fades.
    /// </summary>
    public sealed partial class BattleScreen
    {
        /// <summary>Seconds the decay bar takes to drain after a credit.</summary>
        private const float ComboDecaySeconds = 3f;
        private const float ComboMeterW = 380f, ComboFrameH = 176f, ComboChipH = 60f;
        private const int ComboMaxChips = 3;

        private VisualElement comboRoot, comboFrame, comboFlash, comboChips;
        private Label comboRankLabel, comboWordLabel, comboScoreLabel;
        private ComboRank comboShownRank;
        private float comboDecay;
        private int comboHits;
        private bool comboClosing;
        private Coroutine comboDecayRoutine;
        /// <summary>The two attackers of the pincer being credited, so a follow-up by anyone else reads as support.</summary>
        private readonly List<int> comboAttackers = new List<int>();

        private static Color ComboColor(ComboRank r)
        {
            switch (r)
            {
                case ComboRank.D: return new Color(0.62f, 0.7f, 0.82f);
                case ComboRank.C: return new Color(0.45f, 0.9f, 0.55f);
                case ComboRank.B: return new Color(0.35f, 0.8f, 1f);
                case ComboRank.A: return new Color(0.78f, 0.5f, 1f);
                case ComboRank.S: return new Color(1f, 0.84f, 0.25f);
                case ComboRank.SS: return new Color(1f, 0.55f, 0.15f);
                case ComboRank.SSS: return new Color(1f, 0.22f, 0.3f);
                default: return Palette.TextDim;
            }
        }

        private static string ComboWord(ComboRank r)
        {
            switch (r)
            {
                case ComboRank.D: return "Daring";
                case ComboRank.C: return "Cunning";
                case ComboRank.B: return "Brutal";
                case ComboRank.A: return "Awesome";
                case ComboRank.S: return "Savage!";
                case ComboRank.SS: return "Sensational!!";
                case ComboRank.SSS: return "SUPREME STYLE!!!";
                default: return "";
            }
        }

        /// <summary>The rank letter's size grows with the rank (D 1.0 up to SSS 1.45 of its base).</summary>
        private static float ComboLetterScale(ComboRank r) => 1f + 0.075f * Mathf.Max(0, (int)r - 1);

        private void EnsureComboMeter()
        {
            if (comboRoot != null && comboRoot.panel != null) { PlaceComboMeter(); return; }
            if (shake == null) return;
            comboRoot = new VisualElement { name = "combo-meter", pickingMode = PickingMode.Ignore };
            comboRoot.style.position = Position.Absolute;
            comboRoot.style.width = ComboMeterW;
            comboRoot.style.alignItems = Align.FlexEnd;
            comboRoot.style.opacity = 0f;
            shake.Add(comboRoot);

            comboFrame = new VisualElement { name = "combo-frame", pickingMode = PickingMode.Ignore };
            comboFrame.style.width = ComboMeterW;
            comboFrame.style.height = ComboFrameH;
            comboFrame.style.flexDirection = FlexDirection.Row;
            comboFrame.style.alignItems = Align.Center;
            comboFrame.style.paddingLeft = Space.S16; comboFrame.style.paddingRight = Space.S16;
            comboFrame.style.paddingBottom = Space.S24;
            comboFrame.generateVisualContent += DrawComboFrame;
            comboRoot.Add(comboFrame);

            comboRankLabel = new Label("") { pickingMode = PickingMode.Ignore };
            comboRankLabel.style.minWidth = 140f;
            comboRankLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            comboRankLabel.style.fontSize = TypeScale.Base * 3f;
            Fonts.UseHeavy(comboRankLabel);
            var combo = CombatText.StyleOf(CombatTextKind.Combo);
            comboRankLabel.style.unityTextOutlineWidth = combo.Outline;
            comboRankLabel.style.unityTextOutlineColor = Palette.Ink;
            comboRankLabel.AddToClassList("shadow");
            comboFrame.Add(comboRankLabel);

            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.flexGrow = 1f; col.style.flexShrink = 1f;
            col.style.alignItems = Align.FlexEnd;
            comboFrame.Add(col);
            comboWordLabel = ComboTextLabel("", TypeScale.Base * 0.9f, col);
            comboWordLabel.style.whiteSpace = WhiteSpace.Normal;
            comboWordLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            comboScoreLabel = ComboTextLabel("", TypeScale.Base * 0.75f, col);
            comboScoreLabel.style.color = Palette.Text;
            comboScoreLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            comboFlash = new VisualElement { name = "combo-flash", pickingMode = PickingMode.Ignore };
            comboFlash.style.position = Position.Absolute;
            comboFlash.style.left = comboFlash.style.top = comboFlash.style.right = comboFlash.style.bottom = Space.S8;
            comboFlash.style.backgroundColor = Palette.White;
            comboFlash.style.borderTopLeftRadius = comboFlash.style.borderTopRightRadius =
                comboFlash.style.borderBottomLeftRadius = comboFlash.style.borderBottomRightRadius = 18f;
            comboFlash.style.opacity = 0f;
            comboFrame.Add(comboFlash);

            comboChips = new VisualElement { name = "combo-chips", pickingMode = PickingMode.Ignore };
            comboChips.style.alignItems = Align.FlexEnd;
            comboChips.style.marginTop = Space.S8;
            comboRoot.Add(comboChips);

            comboShownRank = ComboRank.None;
            comboHits = 0;
            comboDecay = 0f;
            comboAttackers.Clear();
            PlaceComboMeter();
        }

        /// <summary>Pins the meter inside the board's top-right corner (the board centres on any stage width).</summary>
        private void PlaceComboMeter()
        {
            comboRoot.style.right = BoardLeft + Space.S16;
            comboRoot.style.top = BoardTop + Space.S16;
            comboRoot.BringToFront();
        }

        private static Label ComboTextLabel(string text, float size, VisualElement parent)
        {
            var s = CombatText.StyleOf(CombatTextKind.Combo);
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("shadow");
            l.style.fontSize = size;
            Fonts.UseHeavy(l);
            l.style.unityTextOutlineWidth = Mathf.Max(Size.Hairline, s.Outline * 0.6f);
            l.style.unityTextOutlineColor = s.OutlineColor;
            l.style.color = s.Color;
            parent.Add(l);
            return l;
        }

        /// <summary>The frame (a dark panel under a rank-tinted rounded border) and the decay bar along its foot, all Shapes.</summary>
        private void DrawComboFrame(MeshGenerationContext ctx)
        {
            var r = comboFrame.contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var p = ctx.painter2D;
            var rc = ComboColor(comboShownRank);
            var box = new Rect(Space.S8, Space.S8, comboFrame.layout.width - Space.S16, comboFrame.layout.height - Space.S16);
            Shapes.RoundedRect(p, box, 18f, ShapeStyle.Indicator.Tint(Palette.Ink.WithA(0.55f)), filled: true);
            Shapes.RoundedRect(p, box, 18f, ShapeStyle.Indicator.Tint(rc).Thick(6f));
            float y = box.yMax - Space.S24, x0 = box.xMin + Space.S24, x1 = box.xMax - Space.S24;
            var bar = ShapeStyle.Small.Thick(Size.Bar);
            Shapes.Bar(p, new Vector2(x0, y), new Vector2(x1, y), bar.Tint(Palette.Ink.WithA(0.6f)));
            if (comboDecay > 0.01f)
                Shapes.Bar(p, new Vector2(x0, y), new Vector2(Mathf.Lerp(x0, x1, comboDecay), y), bar.Tint(Color.Lerp(Palette.Bad, rc, Mathf.Clamp01(comboDecay * 2f))));
        }

        /// <summary>One combo credit: score, rank (slam on a rank-up), the decay bar refilled and the contributor's chip.</summary>
        private IEnumerator ComboChangedAnim(ComboChangedEvent e)
        {
            EnsureComboMeter();
            if (comboRoot == null) return null;
            if (comboClosing || comboHits == 0)
            {
                // A fresh window (or one starting over the last one's summary): clear the board and start from no rank.
                comboChips.Clear();
                comboClosing = false;
                comboShownRank = ComboRank.None;
                comboRankLabel.text = comboWordLabel.text = "";
            }
            comboRoot.style.opacity = 1f;
            comboHits++;
            comboDecay = 1f;
            comboScoreLabel.text = $"{e.Score} pts  ×{comboHits}";

            if (e.Kind == ComboKind.Pincer || e.Kind == ComboKind.ChainLink)
            {
                if (comboAttackers.Count >= 2) comboAttackers.Clear();
                comboAttackers.Add(e.ContributorId);
            }
            bool support = e.Kind == ComboKind.Support || (e.Kind == ComboKind.FollowUp && !comboAttackers.Contains(e.ContributorId));

            if (e.Rank != comboShownRank)
            {
                bool up = e.Rank > comboShownRank;
                comboShownRank = e.Rank;
                App.StartCoroutine(ComboRankSlam(e.Rank, up));
            }
            else if (!ReduceMotion) App.StartCoroutine(Tween.Pop(comboRankLabel, 1.12f, Timing.Quick));
            comboFrame.MarkDirtyRepaint();

            var who = State?.UnitById(e.ContributorId);
            App.StartCoroutine(ComboChip(who?.Def?.Id, $"+{e.Points}", e.Label, support, false));
            if (comboDecayRoutine == null) comboDecayRoutine = App.StartCoroutine(ComboDecay());
            return null;
        }

        /// <summary>The rank letter slams in (scale from big, colour from white) with a frame flash, screen shake and a sting
        /// that climbs with the rank; Reduce Motion fades the new letter in instead.</summary>
        private IEnumerator ComboRankSlam(ComboRank rank, bool up)
        {
            var color = ComboColor(rank);
            float size = ComboLetterScale(rank);
            comboRankLabel.text = rank == ComboRank.None ? "" : rank.ToString();
            // Two- and three-letter ranks narrow per letter so SSS still fits beside its word.
            comboRankLabel.style.fontSize = TypeScale.Base * 3f * size / (1f + 0.35f * Mathf.Max(0, comboRankLabel.text.Length - 1));
            comboWordLabel.text = ComboWord(rank);
            comboWordLabel.style.color = color;
            int step = Mathf.Max(0, (int)rank - 1);
            if (up) App.Audio.Sfx("battle.powerup", 0.6f + 0.05f * step, 0.9f + 0.08f * step);
            if (ReduceMotion)
            {
                comboRankLabel.style.scale = new Scale(Vector2.one);
                comboFlash.style.opacity = 0f;
                yield return Tween.Run(Timing.Standard, k =>
                {
                    comboRankLabel.style.opacity = k;
                    comboRankLabel.style.color = Color.Lerp(Palette.White, color, k);
                });
                yield break;
            }
            if (up) App.StartCoroutine(Tween.Shake(shake, 4f + 2f * step, Timing.Standard + 0.03f * step));
            App.StartCoroutine(Tween.Pop(comboWordLabel, 1.25f, Timing.Standard));
            float big = 2.6f + 0.2f * step;
            comboRankLabel.style.opacity = 1f;
            yield return Tween.Run(Impact.StrikeIn * 1.5f, k =>
            {
                float e = k * k;
                comboRankLabel.style.scale = new Scale(Vector2.one * Mathf.Lerp(big, Impact.StrikeSquash, e));
                comboRankLabel.style.rotate = new Rotate(Mathf.Lerp(-14f, 0f, e));
                comboRankLabel.style.color = Color.Lerp(Palette.White, color, e);
                comboFlash.style.opacity = up ? 0.85f * e : 0f;
            });
            comboRankLabel.style.rotate = new Rotate(0f);
            yield return Tween.Run(Timing.Emphasis, k =>
            {
                comboRankLabel.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(Impact.StrikeSquash, 1f, Tween.Back(k)));
                comboFlash.style.opacity = up ? 0.85f * (1f - k) : 0f;
                // The top ranks keep strobing between white and their colour while they settle.
                comboRankLabel.style.color = rank >= ComboRank.S ? Color.Lerp(color, Palette.White, 0.5f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k)) : color;
            });
            comboRankLabel.style.color = color;
            comboRankLabel.style.scale = new Scale(Vector2.one);
            comboFlash.style.opacity = 0f;
        }

        /// <summary>A contributor chip under the frame: thumbnail, a big "+N" and the credit's label; supporters' in mana
        /// blue. It slides in from the right with an overshoot, holds and fades (Reduce Motion: fades only). A summary
        /// chip (<paramref name="stay"/>) holds until the meter closes.</summary>
        private IEnumerator ComboChip(string classId, string points, string label, bool support, bool stay)
        {
            if (comboChips == null) yield break;
            while (comboChips.childCount >= ComboMaxChips + (stay ? 2 : 0)) comboChips.RemoveAt(comboChips.childCount - 1);
            var tint = support ? Palette.Mana : CombatText.StyleOf(CombatTextKind.Combo).Color;
            var chip = new VisualElement { name = "combo-chip", pickingMode = PickingMode.Ignore };
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.height = ComboChipH;
            chip.style.marginBottom = Space.S4;
            chip.style.paddingLeft = Space.S4; chip.style.paddingRight = Space.S16;
            chip.style.backgroundColor = Palette.Ink.WithA(0.6f);
            chip.style.borderTopLeftRadius = chip.style.borderBottomLeftRadius = ComboChipH / 2f;
            chip.style.borderTopRightRadius = chip.style.borderBottomRightRadius = Space.S8;
            chip.style.borderLeftWidth = chip.style.borderRightWidth = chip.style.borderTopWidth = chip.style.borderBottomWidth = Size.Hairline;
            chip.style.borderLeftColor = chip.style.borderRightColor = chip.style.borderTopColor = chip.style.borderBottomColor = tint;
            if (!string.IsNullOrEmpty(classId))
            {
                var thumb = Ui.Thumb(classId, chip, ComboChipH - Space.S8);
                thumb.pickingMode = PickingMode.Ignore;
                thumb.style.borderTopLeftRadius = thumb.style.borderTopRightRadius =
                    thumb.style.borderBottomLeftRadius = thumb.style.borderBottomRightRadius = (ComboChipH - Space.S8) / 2f;
                thumb.style.marginRight = Space.S8;
            }
            var pts = ComboTextLabel(points, TypeScale.Base * 0.95f, chip);
            pts.style.color = tint;
            pts.style.marginRight = Space.S8;
            if (!string.IsNullOrEmpty(label))
            {
                var caption = ComboTextLabel(support && !stay ? "Support · " + label : label, TypeScale.Base * 0.6f, chip);
                caption.style.color = Color.Lerp(tint, Palette.White, 0.35f);
            }
            comboChips.Insert(0, chip);

            bool calm = ReduceMotion;
            chip.style.opacity = 0f;
            yield return Tween.Run(calm ? Timing.Quick : Timing.Standard, k =>
            {
                chip.style.opacity = k;
                if (!calm)
                {
                    chip.style.translate = new Translate(Space.S96 * (1f - Tween.Back(k)), 0f);
                    chip.style.scale = new Scale(Vector2.one * Mathf.Lerp(1.3f, 1f, k));
                }
            });
            chip.style.translate = new Translate(0f, 0f);
            chip.style.scale = new Scale(Vector2.one);
            if (stay) yield break;
            yield return Tween.Wait(1.4f);
            if (chip.parent == null) yield break;
            yield return Tween.Run(Timing.Standard, k =>
            {
                chip.style.opacity = 1f - k;
                if (!calm) chip.style.translate = new Translate(Space.S48 * k, 0f);
            });
            chip.RemoveFromHierarchy();
        }

        /// <summary>Drains the decay bar while the meter is up; when it runs dry the meter sags until the next credit.</summary>
        private IEnumerator ComboDecay()
        {
            while (comboRoot != null && comboRoot.panel != null && comboHits > 0 && !comboClosing)
            {
                float before = comboDecay;
                comboDecay = Mathf.Max(0f, comboDecay - Tween.Dt * Tween.Speed / ComboDecaySeconds);
                if (comboDecay != before) comboFrame.MarkDirtyRepaint();
                comboRoot.style.opacity = comboDecay > 0f ? 1f : Mathf.Max(0.55f, comboRoot.style.opacity.value - Tween.Dt * 2f);
                yield return null;
            }
            comboDecayRoutine = null;
        }

        /// <summary>The window closed: the final rank and score stamp, each hero's attacker / supporter share as a chip,
        /// then the meter fades away.</summary>
        private IEnumerator ComboEndedAnim(ComboEndedEvent e)
        {
            EnsureComboMeter();
            if (comboRoot == null) return null;
            comboClosing = true;
            comboHits = 0;
            comboDecay = 0f;
            comboAttackers.Clear();
            App.StartCoroutine(ComboClose(e));
            return null;
        }

        private IEnumerator ComboClose(ComboEndedEvent e)
        {
            comboRoot.style.opacity = 1f;
            comboScoreLabel.text = $"{e.Score} pts  ×{e.Hits}";
            comboChips.Clear();
            if (e.Rank != comboShownRank) { comboShownRank = e.Rank; yield return ComboRankSlam(e.Rank, true); }
            else if (!ReduceMotion) App.StartCoroutine(Tween.Pop(comboRankLabel, 1.3f, Timing.Standard));
            comboFrame.MarkDirtyRepaint();
            if (e.Rank >= ComboRank.S) App.Audio.Sfx("ui.diamond", 0.7f);
            var shares = new List<ComboShare>(e.Breakdown);
            shares.Sort((a, b) => b.Total.CompareTo(a.Total));
            for (int i = 0; i < shares.Count && i < ComboMaxChips + 2; i++)
            {
                var s = shares[i];
                string split = s.SupporterPoints > 0 && s.AttackerPoints > 0 ? $"{s.AttackerPoints} atk · {s.SupporterPoints} sup"
                    : s.SupporterPoints > 0 ? "Support" : "Attack";
                App.StartCoroutine(ComboChip(s.ClassId, $"{s.Total}", split, s.SupporterPoints > s.AttackerPoints, true));
                yield return Tween.Wait(ReduceMotion ? Timing.Instant : Timing.Quick);
            }
            yield return Tween.Wait(1.8f);
            if (!comboClosing || comboRoot == null) yield break; // a new combo started over the summary
            float from = comboRoot.style.opacity.value;
            yield return Tween.Run(Timing.Emphasis, k => { if (comboClosing) comboRoot.style.opacity = from * (1f - k); });
            if (!comboClosing) yield break;
            comboChips.Clear();
            comboShownRank = ComboRank.None;
            comboRankLabel.text = comboWordLabel.text = comboScoreLabel.text = "";
            comboFrame.MarkDirtyRepaint();
        }
    }
}
