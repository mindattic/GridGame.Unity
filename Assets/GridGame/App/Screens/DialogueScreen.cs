using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// A conversation (§27.1, GG-T117, GG-T225): the speakers stand on the left and right as their whole, uncropped
    /// portraits, each with a name plate on the one shared text box below. The lines trade back and forth in that
    /// box — each replaces the last and types out — and whoever is talking has their name plate lit (theme accent,
    /// glow) and their portrait full-bright and nudged forward, while the listener's plate and portrait dim. The
    /// player's choices appear in the same box. It only draws what <see cref="DialogueRunner"/> says; every save
    /// change happens there.
    /// </summary>
    public sealed class DialogueScreen : GameScreen
    {
        private const float CharsPerSecond = 70f;
        /// <summary>How long a talking/listening swap (and a speaker stepping on or off) takes.</summary>
        private const float SwapSeconds = 0.2f;
        private readonly DialogueDef def;
        private readonly string with;
        private DialogueRunner run;
        private readonly GameScreen next;
        private readonly Func<GameScreen> nextFactory;
        private readonly string backdrop;
        private readonly Side[] sides = { new Side(0), new Side(1) };
        private int talking = -1;
        private VisualElement controls;
        private Label typing, typingLabel;
        private string typingText;
        private float typed;
        private int shownLines, shownLog;
        private bool done;
        /// <summary>A wanderer talk's game-of-wits meter (GG-T387): one pip per point of standing, the joining mark ringed.</summary>
        private VisualElement[] witsPips;
        private Label witsCaption;
        /// <summary>Where bond chips (GG-T1297) stack while they show.</summary>
        private VisualElement bondChips;

        /// <summary>One side of the conversation: who stands there, their whole portrait and their name plate.
        /// <see cref="Lit"/> (talking 1 / listening 0) and <see cref="Present"/> (on stage 1 / off 0) ease toward
        /// their targets every frame, so no change is ever a single-frame swap.</summary>
        private sealed class Side
        {
            public readonly int Index;
            public string Speaker, Portrait, Name;
            /// <summary>A new occupant waiting for the old one to step off (Present reaching 0) before stepping on.</summary>
            public bool Pending;
            public string PendingSpeaker, PendingPortrait, PendingName;
            public float Lit, LitTarget, Present, PresentTarget;
            public VisualElement Art, Plate, GlowNear, GlowFar;
            public Label NameLabel;
            public Side(int index) { Index = index; }
            public float Toward => Index == 0 ? 1f : -1f; // +x points toward the middle of the screen
        }

        /// <param name="next">Screen to replace this one with when it ends (a story beat's battle); null = go back (Camp Talk).</param>
        public DialogueScreen(DialogueDef def, string with, GameScreen next, string backdrop = null)
        {
            this.def = def; this.with = with;
            this.next = next;
            this.backdrop = def.Backdrop ?? backdrop ?? HubScreen.ThemeFor(GameApp.I.Content, GameApp.I.Save);
        }

        /// <param name="next">Builds the screen to replace this one with once it ends — decided then, not now, so a
        /// chain of story beats sees what the earlier ones did (GG-T119).</param>
        public DialogueScreen(DialogueDef def, Func<GameScreen> next, string backdrop = null) : this(def, null, (GameScreen)null, backdrop)
        {
            nextFactory = next;
        }

        /// <summary>A conversation with <paramref name="with"/> as "$with" that then goes to whatever <paramref name="next"/> builds.</summary>
        public static DialogueScreen WithThen(DialogueDef def, string with, Func<GameScreen> next) => new DialogueScreen(next, def, with);

        private DialogueScreen(Func<GameScreen> next, DialogueDef def, string with) : this(def, with, (GameScreen)null)
        {
            nextFactory = next;
        }

        /// <summary>Plays a runner already set up by the meta layer (GG-T342: a wanderer talk, whose
        /// <see cref="DialogueRunner.Outcome"/> decides how it ends), then goes to whatever <paramref name="next"/> builds.</summary>
        public static DialogueScreen ForRunner(DialogueRunner runner, Func<GameScreen> next) =>
            new DialogueScreen(next, runner.Def, runner.With) { run = runner };

        public override string Id => "dialogue";
        /// <summary>Keeps whatever was playing (the crawl's theme before a beat, camp music for a chat).</summary>
        public override string Music => null;
        public override bool RebuildOnResize => false; // a rebuild would replay the scene from its first line

        /// <summary>Without this, a screen change away from a still-playing dialogue (demo-mode AutoPlay is the
        /// only thing that keeps running on its own) left AutoPlay ticking on a popped screen: it kept tapping
        /// through lines, applying effects and eventually called Finish(), which Replace()/Back()'d whatever
        /// screen had since been mounted — the same failure class as LevelUpScreen's fanfare coroutine (GG-T127).</summary>
        public override void OnHidden() => done = true;

        /// <summary>A scene that hands on to a next screen (a story beat before a battle that has already spent Rested, a
        /// beat on the way to Camp) treats Back as Skip ›: popping it went back to the stage list, and relaunching
        /// charged again. Choices are never skipped, so at one Back does nothing. A plain Camp chat still pops.</summary>
        public override bool OnBack()
        {
            if (next == null && nextFactory == null) return true;
            if (!done && run != null) SkipAhead();
            return false;
        }

        protected override void Build(VisualElement root)
        {
            // Started on first show, not at construction: a beat queued behind a story crawl mustn't apply its
            // first line's effects before the player sees it.
            run ??= new DialogueRunner(App.Content, App.Save, def, with);
            shownLines = shownLog = 0;
            talking = -1;
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, backdrop, 0);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;

            // The stage: a whole portrait standing on each side, feet behind the text box.
            var stage = Ui.El(null, root);
            stage.name = "dlg-stage";
            stage.style.position = Position.Absolute;
            // The figures stand at the content column's edges, over the text box's name plates (GG-T398).
            stage.style.left = stage.style.right = Fit.Inset; stage.style.top = Fit.H(5.9f); stage.style.bottom = Fit.H(25.3f);
            stage.RegisterCallback<ClickEvent>(_ => TapLine());

            var title = Ui.Text(def.Title ?? "", "small dim shadow", root);
            title.style.position = Position.Absolute; title.style.left = Fit.Inset + Space.S32; title.style.top = Space.S48;

            var skip = RowIcon.IconButton("Skip", RowKind.Enter, SkipAhead, "btn--small", root);
            skip.name = "dlg-skip";
            skip.style.position = Position.Absolute; skip.style.right = Space.S24; skip.style.top = Space.S24;

            BuildWits(root);

            bondChips = Ui.El(null, root);
            bondChips.name = "dlg-bonds";
            bondChips.pickingMode = PickingMode.Ignore;
            bondChips.style.position = Position.Absolute;
            bondChips.style.left = bondChips.style.right = Fit.Inset + Fit.Edge; bondChips.style.top = Fit.H(12f);
            bondChips.style.alignItems = Align.Center;

            // The one shared text box: each line replaces the last in it; the choices sit in it too.
            var box = Ui.Panel(root, null, false, "dlg-box");
            box.AddToClassList("window--plain");
            box.AddToClassList("dlg-box");
            box.style.position = Position.Absolute;
            box.style.left = box.style.right = Fit.Inset + Fit.Edge; box.style.bottom = Space.S48;
            box.style.overflow = Overflow.Visible;
            box.RegisterCallback<ClickEvent>(e =>
            {
                // A choice/Next button's click bubbles up here too; only a tap on the box itself is "next line".
                if (e.target is VisualElement t && controls != null && (t == controls || controls.Contains(t))) return;
                TapLine();
            });
            var text = Ui.Text("", "h2", box);
            text.name = "dlg-text";
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.unityFontStyleAndWeight = FontStyle.Normal;
            text.style.fontSize = TypeScale.Base;
            text.style.flexGrow = 1;
            text.style.minHeight = 230;
            typing = null;

            controls = Ui.El("col", box);
            controls.name = "dlg-controls";
            controls.style.justifyContent = Justify.FlexEnd;

            foreach (var s in sides)
            {
                s.Speaker = s.Portrait = s.Name = null;
                s.Pending = false;
                s.Lit = s.LitTarget = s.Present = s.PresentTarget = 0f;
                BuildSide(s, stage, box);
            }

            typingLabel = text;
            ShowNew();
            if (DemoMode.Active) App.StartCoroutine(AutoPlay());
        }

        /// <summary>On-screen height of a full 640-px-tall portrait canvas (POL-H1: 640 px = one person's height).</summary>
        private const float FigureHeight = 1200f;
        private const float PersonCanvas = 640f;

        /// <summary>Builds a side's whole-portrait frame (contain-fit, bottom-aligned, never a thumbnail framing) and
        /// its name plate, which sits on the text box's top edge with two soft glow layers behind it.</summary>
        private static void BuildSide(Side s, VisualElement stage, VisualElement box)
        {
            string tag = s.Index == 0 ? "left" : "right";
            var p = Ui.El(null, stage);
            p.name = "dlg-portrait-" + tag;
            p.pickingMode = PickingMode.Ignore;
            p.style.position = Position.Absolute;
            p.style.bottom = Space.S0; p.style.height = FigureHeight; p.style.width = FigureHeight;
            // GG-T387: flush with the screen edge, not 64 px past it: the frame is the figure's own box (Art.PortraitFigure),
            // so any overhang cut the figure's outer edge off.
            if (s.Index == 0) p.style.left = Space.S0; else p.style.right = Space.S0;
            p.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            p.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
            p.style.opacity = 0f;
            s.Art = p;

            var plate = Ui.El(null, box);
            plate.name = "dlg-plate-" + tag;
            plate.pickingMode = PickingMode.Ignore;
            plate.style.position = Position.Absolute;
            plate.style.top = -Space.S64;
            if (s.Index == 0) plate.style.left = Space.S32; else plate.style.right = Space.S32;
            plate.style.height = Size.Plate;
            plate.AddToClassList("dlg-plate");
            plate.style.justifyContent = Justify.Center;
            plate.style.alignItems = Align.Center;
            SetRadius(plate, Radius.R22);
            SetBorder(plate, 4);
            plate.style.opacity = 0f;
            s.GlowFar = Glow(plate, 22, 0.18f);
            s.GlowNear = Glow(plate, 10, 0.4f);
            s.NameLabel = Ui.Text("", null, plate);
            s.NameLabel.name = "dlg-name-" + tag;
            s.NameLabel.style.fontSize = TypeScale.Base;
            Fonts.UseHeavy(s.NameLabel);
            s.NameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            s.Plate = plate;
        }

        private static VisualElement Glow(VisualElement plate, float spread, float alpha)
        {
            var g = new VisualElement { pickingMode = PickingMode.Ignore };
            g.style.position = Position.Absolute;
            g.style.left = g.style.right = g.style.top = g.style.bottom = -spread;
            SetRadius(g, Radius.R22 + spread);
            g.style.opacity = 0f;
            g.userData = alpha;
            plate.Add(g);
            return g;
        }

        private static void SetRadius(VisualElement e, float r) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        private static void SetBorder(VisualElement e, float w) =>
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;

        public override void Tick(float dt)
        {
            foreach (var s in sides) Animate(s, dt);
            if (typing == null || typed >= typingText.Length) return;
            App.Audio.DuckFor(Timing.Dramatic); // POL-F3: music sits under the line while it types
            int before = (int)typed;
            typed = Mathf.Min(typingText.Length, typed + dt * CharsPerSecond);
            if ((int)typed != before) typing.text = typingText.Substring(0, (int)typed);
            if (typed >= typingText.Length) LineDone();
        }

        /// <summary>Eases a side toward its targets and draws it: the talker's portrait full-bright, full-size and
        /// nudged toward the middle, its plate lit in the theme accent with a glow; the listener's dimmed, shrunk
        /// back, plate greyed. A side being handed to a new speaker steps the old one off first.</summary>
        private void Animate(Side s, float dt)
        {
            float step = dt / SwapSeconds;
            s.Lit = Mathf.MoveTowards(s.Lit, s.LitTarget, step);
            float presentTarget = s.Pending ? 0f : s.PresentTarget;
            s.Present = Mathf.MoveTowards(s.Present, presentTarget, step);
            if (s.Pending && s.Present <= 0f)
            {
                s.Pending = false;
                Occupy(s, s.PendingSpeaker, s.PendingPortrait, s.PendingName);
            }
            if (s.Art == null) return;

            float lit = Tween.EaseOut(s.Lit), present = Tween.EaseOut(s.Present);
            var accent = Art.ThemeAccent;

            s.Art.style.opacity = s.Portrait != null ? present : 0f;
            float tint = Mathf.Lerp(0.42f, 1f, lit);
            s.Art.style.unityBackgroundImageTintColor = Palette.Grey(tint);
            s.Art.style.scale = new Scale(Vector2.one * Mathf.Lerp(0.93f, 1f, lit));
            s.Art.style.translate = new Translate(s.Toward * (Mathf.Lerp(0f, 40f, lit) - (1f - present) * 140f), 0);

            s.Plate.style.opacity = present * Mathf.Lerp(0.6f, 1f, lit);
            s.Plate.style.scale = new Scale(Vector2.one * Mathf.Lerp(0.9f, 1.06f, lit));
            s.Plate.style.translate = new Translate(0, Mathf.Lerp(8f, 0f, lit));
            var dimBg = Palette.Ink.WithA(0.92f);
            var litBg = Color.Lerp(accent, Palette.Black, 0.3f); litBg.a = 0.98f;
            s.Plate.style.backgroundColor = Color.Lerp(dimBg, litBg, lit);
            var border = Color.Lerp(Palette.PlateRim, Color.Lerp(accent, Palette.White, 0.55f), lit);
            s.Plate.style.borderTopColor = s.Plate.style.borderBottomColor = s.Plate.style.borderLeftColor = s.Plate.style.borderRightColor = border;
            s.NameLabel.style.color = Color.Lerp(Palette.TextDim, Palette.White, lit);
            foreach (var g in new[] { s.GlowNear, s.GlowFar })
            {
                var c = accent; c.a = (float)g.userData;
                g.style.backgroundColor = c;
                g.style.opacity = lit;
            }
        }

        private static void Occupy(Side s, string speaker, string portrait, string name)
        {
            s.Speaker = speaker;
            s.Portrait = HasPortrait(portrait) ? portrait : null;
            s.Name = name;
            if (s.Art == null) return;
            var fig = s.Portrait != null ? Art.PortraitFigure(s.Portrait) : null;
            s.Art.style.backgroundImage = new StyleBackground(fig);
            // One scale for every speaker: a 640-unit figure = FigureHeight. The figure is its box on the 1024 portrait
            // fitted inside 640 (Art.FigureSize, GG-T352), so a creature smaller than that is drawn that much smaller
            // (POL-H1) and a 234-px slime isn't stretched to a person's height.
            if (fig != null)
            {
                float k = FigureHeight / PersonCanvas;
                var size = Art.FigureSize(s.Portrait);
                float h = Mathf.Min(size.y, PersonCanvas) * k;
                // Never drawn above its own pixels (POL-H4, GG-T437): a ~1000-px figure at 1200 blurred on the largest
                // stage, so a figure stops at its native height and keeps its aspect.
                float cap = Art.FigureNativeHeight(s.Portrait);
                if (h > cap) k *= cap / h;
                s.Art.style.height = Mathf.Min(size.y, PersonCanvas) * k;
                s.Art.style.width = size.x * k;
            }
            s.NameLabel.text = name ?? "";
            s.Plate.style.display = string.IsNullOrEmpty(name) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private bool Typing => typing != null && typed < typingText.Length;

        /// <summary>Puts the newest transcript line in the box (one per Advance/Choose; lines rushed past by Skip
        /// still claim their sides) and toasts what effects did; a bond change shows as a bond chip instead.</summary>
        private void ShowNew()
        {
            int chips = 0;
            while (shownLog < run.Log.Count)
            {
                int at = shownLog++;
                if (run.BondChanges.TryGetValue(at, out var bond) && bondChips != null) ShowBond(bond, chips++);
                else
                {
                    App.Toast(run.Log[at]);
                    App.Audio.Sfx("ui.buy");
                }
            }
            if (run.Finished) { Finish(); return; }
            if (shownLines >= run.Transcript.Count) return;
            int before = talking;
            DialogueLine line = null;
            while (shownLines < run.Transcript.Count)
            {
                line = run.Transcript[shownLines++];
                Speak(line);
            }
            if (talking != before && before >= 0) App.Audio.Sfx("battle.slide", 0.45f);
            ShowLine(line);
        }

        /// <summary>Hands the line's speaker a side and lights it: the talker's plate and portrait come up, the
        /// other side's go down.</summary>
        private void Speak(DialogueLine line)
        {
            int side = SideFor(line);
            var s = sides[side];
            string speaker = line.SpeakerId ?? "";
            string portrait = HasPortrait(line.Portrait) ? line.Portrait : null;
            bool same = s.Pending ? s.PendingSpeaker == speaker : s.Speaker == speaker;
            if (!same || (s.Pending ? s.PendingPortrait : s.Portrait) != portrait)
            {
                if (s.Speaker == null || s.Present <= 0f) { s.Pending = false; Occupy(s, speaker, portrait, line.Name); }
                else { s.Pending = true; s.PendingSpeaker = speaker; s.PendingPortrait = portrait; s.PendingName = line.Name; }
            }
            s.PresentTarget = 1f;
            s.LitTarget = 1f;
            sides[1 - side].LitTarget = 0f;
            if (talking != side) s.Art?.BringToFront();
            talking = side;
        }

        private void ShowLine(DialogueLine line)
        {
            typing = typingLabel;
            typingText = line.Text ?? "";
            typed = 0;
            typing.text = "";
            controls.Clear();
            App.Audio.Sfx("ui.select");
        }

        /// <summary>The side a speaker stands on: theirs if they already have one, else a free one, else (a third
        /// speaker) the side of whoever is not talking right now, so the one who just spoke stays put. GG-T1576: in a
        /// dialogue with a Commander speaker, the Commander stands left and everyone else right.</summary>
        private int SideFor(DialogueLine line)
        {
            if (CommanderSpeaker != null) return line.Commander ? 0 : 1;
            string speakerId = line.SpeakerId ?? "";
            for (int i = 0; i < 2; i++)
                if ((sides[i].Pending ? sides[i].PendingSpeaker : sides[i].Speaker) == speakerId) return i;
            for (int i = 0; i < 2; i++)
                if (sides[i].Speaker == null && !sides[i].Pending) return i;
            return talking < 0 ? 0 : 1 - talking;
        }

        /// <summary>True when there is portrait art for class <paramref name="id"/> (a speaker's class, or the class an NPC
        /// speaker's <c>Portrait</c> borrows, GG-T557). Dialogue always shows it whole (GG-T225).</summary>
        internal static bool HasPortrait(string id) => !string.IsNullOrEmpty(id) && !id.StartsWith("$") && Art.HasPortrait(id);

        /// <summary>The dialogue's Commander speaker (GG-T1576), or null when the player doesn't speak in it.</summary>
        private DialogueSpeaker CommanderSpeaker => run.Def.Speakers.FirstOrDefault(x => x?.Class == Commander.Token);

        /// <summary>The line has fully typed out: offer its choices, or a Next button. GG-T1576: choices are the
        /// Commander's lines, so in a dialogue with a Commander speaker their side lights (stepping on if not there yet).</summary>
        private void LineDone()
        {
            if (typing != null) typing.text = typingText;
            controls.Clear();
            if (run.AwaitingChoice)
            {
                var commander = CommanderSpeaker;
                if (commander != null)
                {
                    var (name, portrait) = run.Resolve(commander);
                    Speak(new DialogueLine { SpeakerId = commander.Id, Name = name, Portrait = portrait, Commander = true });
                }
                var choices = run.Choices;
                for (int i = 0; i < choices.Count; i++)
                {
                    int idx = i;
                    var b = RowIcon.IconButton(Commander.Fill(choices[i].Label, App.Save, App.Profile?.Name), RowKind.Talk, () => Choose(idx), i == 0 ? "btn--primary" : null, controls);
                    b.name = "choice-" + i;
                    b.AddToClassList("dlg-choice");
                    b.style.opacity = 0f;
                    App.StartCoroutine(FadeUp(b, i * 0.06f));
                }
            }
            else
            {
                var b = RowIcon.IconButton(run.Node.Next == null ? "Done" : "Next", RowKind.Enter, Advance, "btn--primary btn--small", controls);
                b.name = "dlg-next";
                b.style.alignSelf = Align.FlexEnd; b.style.width = Size.Card;
                b.style.opacity = 0f;
                App.StartCoroutine(FadeUp(b, 0f));
            }
        }

        private IEnumerator FadeUp(VisualElement e, float delay)
        {
            if (delay > 0) yield return Tween.Wait(delay);
            yield return Tween.Run(Timing.Quick, k => { e.style.opacity = k; e.style.translate = new Translate(0, Mathf.Lerp(30f, 0f, k)); }, Tween.EaseOut);
        }

        private void TapLine()
        {
            if (done) return;
            if (Typing) { FinishLine(); return; }
            if (!run.AwaitingChoice) Advance();
        }

        /// <summary>The line being typed shows in full at once (GG-T2150: the audit's settled dialogue box).</summary>
        internal void FinishLine()
        {
            if (done || !Typing) return;
            typed = typingText.Length;
            typing.text = typingText;
            LineDone();
        }

        private void Advance()
        {
            if (done || Typing || !run.Advance()) return;
            ShowNew();
        }

        private void Choose(int index)
        {
            if (done) return;
            int before = run.Wits?.Standing ?? 0;
            var r = run.Choose(index);
            if (!r.Ok) { App.Audio.Sfx("ui.error"); return; }
            if (run.Wits != null) MoveWits(before);
            else App.Audio.Sfx("ui.confirm");
            App.Persist();
            ShowNew();
        }

        // ── the game of wits (GG-T387) ──────────────────────────────────────────────

        /// <summary>The standing meter over a wanderer talk: a pip per point up to <see cref="WitsMeter.Max"/>, filled up to
        /// the standing, the joining mark (<see cref="WitsMeter.Threshold"/>) ringed in gold. Nothing for other dialogues.</summary>
        private void BuildWits(VisualElement root)
        {
            witsPips = null; witsCaption = null;
            var w = run.Wits;
            if (w == null) return;
            var panel = Ui.El(null, root);
            panel.name = "dlg-wits";
            panel.pickingMode = PickingMode.Ignore;
            panel.style.position = Position.Absolute;
            panel.style.left = panel.style.right = Fit.Inset + Fit.Edge; panel.style.top = Fit.H(5.9f);
            panel.style.alignItems = Align.Center;
            var row = Ui.El("dlg-wits-row", panel);
            row.pickingMode = PickingMode.Ignore;
            row.style.backgroundColor = Palette.Ink.WithA(0.72f);
            SetRadius(row, Radius.R28);
            Ui.Text("Standing", "small dlg-wits-name", row);
            witsPips = new VisualElement[w.Max];
            for (int i = 0; i < w.Max; i++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore, name = "dlg-wits-pip-" + (i + 1) };
                pip.AddToClassList("dlg-wits-pip");
                pip.style.width = pip.style.height = Size.IconS;
                SetRadius(pip, Radius.R20);
                SetBorder(pip, i + 1 == w.Threshold ? 5 : 3);
                var edge = i + 1 == w.Threshold ? Palette.Gold : Palette.TextDim;
                pip.style.borderTopColor = pip.style.borderBottomColor = pip.style.borderLeftColor = pip.style.borderRightColor = edge;
                pip.style.backgroundColor = PipColor(w, i, w.Standing);
                row.Add(pip);
                witsPips[i] = pip;
            }
            witsCaption = Ui.Text(WitsCaption(w, 0, false), "small dlg-wits-caption", panel);
            witsCaption.name = "dlg-wits-caption";
        }

        /// <summary>Pip <paramref name="i"/> (0-based) at <paramref name="standing"/>: lit in the wanderer's rose below the
        /// joining mark, in gold from it on; dark when not reached.</summary>
        private static Color PipColor(WitsMeter w, int i, int standing) =>
            i + 1 > standing ? Palette.Ink.WithA(0.85f) : i + 1 >= w.Threshold ? Palette.Gold : Palette.Wanderer;

        private static string WitsCaption(WitsMeter w, int move, bool after)
        {
            string head = !after ? "" : move > 0 ? "Point won! " : move < 0 ? "Point lost. " : "Even. ";
            int need = w.Threshold - w.Standing;
            return head + (need <= 0 ? "They'd join you now." : $"{need} more to win them over.");
        }

        /// <summary>A reply has landed: the meter steps to the new standing over a beat (the pip that changes swells and
        /// recolours), with a cue for a point won, an even reply or a point lost (Law 5).</summary>
        private void MoveWits(int before)
        {
            var w = run.Wits;
            int after = w.Standing, move = after - before;
            App.Audio.Sfx(move > 0 ? "battle.powerup" : move < 0 ? "status.debuff" : "ui.click");
            if (witsPips == null) return;
            if (witsCaption != null) witsCaption.text = WitsCaption(w, move, true);
            int at = Mathf.Clamp((move >= 0 ? after : before) - 1, 0, witsPips.Length - 1);
            var pip = witsPips[at];
            var from = PipColor(w, at, before);
            var to = PipColor(w, at, after);
            App.StartCoroutine(Tween.Run(Timing.Quick * 2f, k =>
            {
                pip.style.scale = new Scale(Vector2.one * (1f + 0.4f * Ease.Arc(k)));
                pip.style.backgroundColor = Color.Lerp(from, to, k);
            }, Tween.EaseOut));
        }

        // ── bond chips (GG-T1297) ───────────────────────────────────────────────────

        /// <summary>A Trust or cohesion change that landed: the hero's (or the pair's) thumbnails and the amount slide in,
        /// hold and fade; a tier-up adds a gold "Tier N!" line, a pop and the power-up cue (Law 5). Reduce Motion keeps
        /// the text and sound and drops the slide and pop. <paramref name="order"/> staggers chips from one choice.</summary>
        private void ShowBond(DialogueRunner.BondChange b, int order)
        {
            string Name(string id) => App.Content.TryClass(id)?.Name ?? id;
            var chip = Ui.El("kit-chip dlg-bond" + (b.TierUp ? " dlg-bond--tier" : ""), bondChips);
            chip.name = "dlg-bond-" + (b.B == null ? b.A : b.A + "-" + b.B);
            chip.pickingMode = PickingMode.Ignore;
            chip.style.opacity = 0f;
            foreach (var id in b.B == null ? new[] { b.A } : new[] { b.A, b.B })
                Ui.Thumb(id, chip, Size.Icon).AddToClassList("dlg-bond__thumb");
            var col = Ui.El("col dlg-bond__text", chip);
            string what = b.B == null ? $"{Name(b.A)}'s Trust" : $"{Name(b.A)} & {Name(b.B)}";
            var amount = Ui.Text($"{what} {b.Moved:+0;-0}", b.Moved > 0 ? null : "dim", col);
            Fonts.UseHeavy(amount);
            if (b.TierUp) Ui.Text($"Tier {b.TierAfter}!", "small gold", col);
            App.StartCoroutine(PlayBond(chip, b, order * Timing.Emphasis));
        }

        private IEnumerator PlayBond(VisualElement chip, DialogueRunner.BondChange b, float delay)
        {
            if (delay > 0) yield return Tween.Wait(delay);
            bool calm = App.Settings.ReduceMotion;
            App.Audio.Sfx(b.TierUp ? "battle.powerup" : b.Moved > 0 ? "ui.buy" : "status.debuff");
            yield return Tween.Run(Timing.Standard, k =>
            {
                chip.style.opacity = k;
                if (!calm) chip.style.translate = new Translate(0, Mathf.Lerp(-40f, 0f, k));
            }, Tween.EaseOut);
            if (b.TierUp) yield return Tween.Pop(chip, 1.15f, Timing.Emphasis);
            yield return Tween.Wait(b.TierUp ? Timing.Cinematic * 2f : Timing.Cinematic * 1.5f);
            yield return Tween.Run(Timing.Standard, k =>
            {
                chip.style.opacity = 1f - k;
                if (!calm) chip.style.translate = new Translate(0, Mathf.Lerp(0f, -20f, k));
            });
            chip.RemoveFromHierarchy();
        }

        /// <summary>Skip ›: rushes through lines up to the next choice (choices are never skipped — they can change the
        /// save), or to the end.</summary>
        private void SkipAhead()
        {
            if (done) return;
            if (Typing) { typed = typingText.Length; typing.text = typingText; }
            int guard = 0;
            while (!run.Finished && !run.AwaitingChoice && guard++ < 200) run.Advance();
            ShowNew();
            if (!done && typing != null) { typed = typingText.Length; LineDone(); }
        }

        private IEnumerator AutoPlay()
        {
            while (!done)
            {
                yield return Tween.Wait(Timing.Cinematic + Timing.Quick);
                if (done) yield break;
                if (Typing) TapLine();
                else if (run.AwaitingChoice) Choose(run.Choices.Count - 1); // the last choice is the way out by convention
                else Advance();
            }
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            controls?.Clear();
            App.Persist();
            var then = next ?? nextFactory?.Invoke();
            if (then != null) App.Router.Replace(then);
            else if (!App.Router.Back()) App.Router.ResetTo(new HubScreen()); // nothing under it: never strand on a finished scene
        }
    }

    /// <summary>Camp's Talk row (§27.1, GG-T117): the party and every joined Camp NPC, one row each with what they'd talk
    /// about. GG-T2023: a hero's bond scenes, open or locked, sit in that hero's own sheet (<see cref="TalkPartnerScreen"/>),
    /// not as rows under them; the row carries the New badge and a count when anything there is new.</summary>
    public sealed class TalkScreen : HubPage
    {
        public override string Id => "talk";
        protected override string Title => "Talk";

        protected override void BuildBody(VisualElement body)
        {
            var list = Ui.ListPanel(body, null, out var scroll, "talk-list");
            var scenes = DialogueRunner.BondScenes(C, S);
            var partners = DialogueRunner.TalkPartners(S);
            foreach (var who in partners)
            {
                bool npc = who.StartsWith(ContentLoader.NpcFlagPrefix);
                string npcId = npc ? who.Substring(ContentLoader.NpcFlagPrefix.Length) : null;
                var topic = DialogueRunner.TalkTopic(C, S, who);
                string name = npc ? DialogueRunner.Npc(npcId) : C.TryClass(who)?.Name ?? who;
                var own = npc ? new List<BondScene>() : TalkPartnerScreen.ScenesOf(scenes, who);
                int fresh = (topic != null && !S.SeenDialogues.Contains(topic.Id) ? 1 : 0) + own.Count(b => b.Unlocked);
                string value = fresh > 1 ? $"{fresh} new" : fresh == 1 ? "New!"
                    : topic == null ? (own.Count > 0 ? "Nothing new" : "Nothing to say") : topic.Title ?? "";
                Action open = own.Count > 0 ? () => App.Router.Push(new TalkPartnerScreen(who))
                    : (Action)(() => App.Router.Push(new DialogueScreen(topic, npc ? null : who, null)));
                var row = Ui.MenuRow(name, value, open, list, null, topic != null || own.Count > 0);
                // GG-T748: every partner leads with its kind icon (BIBLE §26.7). An NPC: the thumbnail of the topic's first
                // speaker with art (GG-T225), else the bust emblem; a hero: its baked thumbnail (§12.1.1, GG-T720). The whole
                // figure stands only in the conversation itself.
                var icon = npc ? row.KindIcon(RowKind.Npc, topic?.Speakers.Select(sp => sp.Portrait ?? sp.Class).FirstOrDefault(DialogueScreen.HasPortrait))
                    : row.KindIcon(RowKind.Hero, DialogueScreen.HasPortrait(who) ? who : null);
                row.name = "talk-" + (npc ? npcId : who);
                if (fresh > 0)
                {
                    RowIcon.Badge(icon, RowKind.New);
                    row.Q<Label>("value").AddToClassList("gold");
                }
            }
            var together = scenes.Where(b => b.Unlocked && b.Def.Trigger.Kind == DialogueTriggerKind.Cohesion).ToList();
            if (together.Count > 0)
            {
                Ui.Section("Together", list).name = "talk-together";
                foreach (var b in together) TalkPartnerScreen.SceneRow(list, b, null);
            }
            if (partners.Count == 0 && together.Count == 0) Ui.EmptyState(list, RowKind.Talk, "No one to talk to yet.", "talk-empty");
            Ui.SelectFirst(list);
        }
    }
}
