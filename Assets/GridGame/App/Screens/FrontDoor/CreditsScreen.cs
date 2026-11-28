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
    public sealed class CreditsScreen : GameScreen
    {
        private readonly Func<GameScreen> rollThen;
        private VisualElement roll;
        private float rollY;
        private bool fast, rolled;
        public CreditsScreen() { }
        /// <summary>GG-T231: the end-of-story roll — the credits scroll up over the last battlefield, hold-to-speed, then
        /// <paramref name="then"/> (the Title).</summary>
        public CreditsScreen(Func<GameScreen> then) { rollThen = then; }
        public override string Id => rollThen != null ? "credits-roll" : "credits";
        public override string Music => rollThen == null ? null
            : music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Credits) ?? "heroic-age-00";
        private string music;
        public override bool RebuildOnResize => rollThen == null; // a rebuild would restart the roll

        protected override void Build(VisualElement root)
        {
            if (rollThen != null) { BuildRoll(root); return; }
            var col = Ui.El("fill col", root);
            Ui.Header("Credits", () => App.Router.Back(), col, out _);
            var list = Ui.ListPanel(Ui.PageBody(col), null, "credits-list");
            Ui.Para(Resources.Load<TextAsset>("Credits")?.text ?? $"{GameApp.Title} by MindAttic", list);
        }

        private void BuildRoll(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, App.Content.Campaign.Ending?.Backdrop ?? "cityruins", 3);
            bg.style.unityBackgroundImageTintColor = Palette.Backdrop.Credits;
            var clip = Ui.El("fill", root);
            clip.style.overflow = Overflow.Hidden;
            // The scrolling text ends above the Skip button row, so it never runs under the button (GG-T397).
            clip.style.bottom = Space.S48 + Kit.Row + Space.S24;
            roll = Ui.El("col", clip);
            roll.name = "credits-roll";
            roll.style.position = Position.Absolute;
            roll.style.left = roll.style.right = Fit.Inset + Space.S96;
            var title = Ui.Display(GameApp.Title, roll, "shadow gold", false);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = Space.S96;
            var text = Resources.Load<TextAsset>("Credits")?.text ?? $"{GameApp.Title} by MindAttic";
            // Credits.txt opens with the game's name; the display title above already says it.
            var lines = text.Replace("\r", "").Split('\n');
            if (lines.Length > 1 && lines[0].Trim().Equals(GameApp.Title, StringComparison.OrdinalIgnoreCase))
                text = string.Join("\n", lines.Skip(1)).TrimStart('\n');
            var body = Ui.Para(text, roll, "shadow");
            body.style.unityTextAlign = TextAnchor.UpperCenter;
            var coda = Ui.Para("Thank you for playing.", roll, "shadow dim");
            coda.style.unityTextAlign = TextAnchor.MiddleCenter;
            coda.style.marginTop = Fit.H(8.7f);
            rollY = GameApp.StageHeight;
            roll.style.top = rollY;
            // Hold anywhere to fast-forward; Skip ends it.
            root.RegisterCallback<PointerDownEvent>(_ => fast = true);
            root.RegisterCallback<PointerUpEvent>(_ => fast = false);
            root.RegisterCallback<PointerLeaveEvent>(_ => fast = false);
            var skip = RowIcon.IconButton("Skip", RowKind.Enter, EndRoll, "btn--small", root);
            skip.name = "credits-skip";
            skip.style.position = Position.Absolute; skip.style.right = Space.S32; skip.style.bottom = Space.S48;
            App.Audio.Sfx("ui.confirm");
            if (DemoMode.Active || TestMode.Active) root.schedule.Execute(EndRoll).ExecuteLater(600);
        }

        public override void Tick(float dt)
        {
            if (rollThen == null || rolled || roll == null) return;
            rollY -= dt * (fast ? 480f : 110f);
            roll.style.top = rollY;
            // Ends once the coda has scrolled up to the middle of the screen and held there a moment.
            if (roll.layout.height > 0 && rollY < -roll.layout.height + GameApp.StageHeight * 0.5f - 300f) EndRoll();
        }

        private void EndRoll()
        {
            if (rolled) return;
            rolled = true;
            App.Audio.Sfx("ui.select");
            App.Router.ResetTo(rollThen());
        }
    }
}
