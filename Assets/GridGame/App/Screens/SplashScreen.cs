using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1322 (bible §11.2): every launch opens on the MindAttic LLC logo and the copyright line, fading in and out
    /// over <see cref="Seconds"/> with a soft cue, then the Title, or Create Profile when the active profile's first run
    /// is due (GG-T1345, <see cref="GridGame.Meta.ProfileSession.FirstRunDue"/>). A tap anywhere (or Back) skips; Reduce
    /// Motion shows it without the fade. The logo (UI/mindattic-logo-00) is a placeholder until the owner picks one (GG-T1333).
    /// Automation, -start, -testmode, -demo and headless runs never see it (GameApp.Awake).
    /// </summary>
    public sealed class SplashScreen : GameScreen
    {
        public const float Seconds = 3f;
        private const float FadeSeconds = 0.75f;
        public const string Copyright = "© 2026 MindAttic LLC. All rights reserved.";

        public override string Id => "splash";
        private VisualElement col;
        private float elapsed;
        private bool done;

        protected override void Build(VisualElement root)
        {
            Ui.El("fill", root).style.backgroundColor = Palette.Ink;
            root.RegisterCallback<PointerDownEvent>(_ => Finish());
            col = Ui.El("fill col center", root);
            var logo = Ui.El(null, col);
            logo.name = "splash-logo";
            var tex = Resources.Load<Texture2D>(AssetNames.Path("UI", "mindattic-logo"));
            if (tex != null)
            {
                logo.style.backgroundImage = tex;
                logo.style.width = Fit.C(70f);
                logo.style.height = Fit.C(70f) * tex.height / Mathf.Max(1f, tex.width);
            }
            else Fonts.UseHeavy(Ui.Text("MINDATTIC LLC", "title", logo));
            Ui.Text(Copyright, "tiny dim splash-copy", col);
            col.style.opacity = Opacity();
        }

        public override void OnShown()
        {
            elapsed = 0f;
            App.Audio.Sfx("ui.select");
        }

        public override void Tick(float dt)
        {
            if (done) return;
            elapsed += dt;
            if (col != null) col.style.opacity = Opacity();
            if (elapsed >= Seconds) Finish();
        }

        public override bool OnBack() { Finish(); return false; }

        private float Opacity()
        {
            if (Tween.Calm) return 1f;
            return Mathf.Clamp01(Mathf.Min(elapsed, Seconds - elapsed) / FadeSeconds);
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            App.Router.ResetTo(App.Profile.FirstRunDue ? new CreateProfileScreen() : new TitleScreen());
        }
    }
}
