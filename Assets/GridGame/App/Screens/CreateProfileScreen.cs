using System;
using GridGame.Core.Battle;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// Create Profile (bible §15.4, GG-T1344): three steps in the menu kit. 1 the Commander's name (the profile name rules,
    /// <see cref="ProfileNameScreen.Problem"/>), 2 quick settings, previewed live on the active settings and restored if the
    /// wizard is left, 3 a summary and Begin, which creates the profile, switches to it, writes the picks into its
    /// settings.json and marks its first run done. Back steps back a page; on step 1 it cancels only when the active
    /// profile has finished its own first run (there is a profile to go back to). On the first launch (GG-T1345) the
    /// store's own empty Default profile is renamed to the Commander's name instead of a second profile being made.
    /// </summary>
    public sealed class CreateProfileScreen : GameScreen
    {
        public override string Id => "create-profile";

        private static readonly float[] Volumes = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };
        private static readonly float[] Speeds = { 1f, 1.5f, 2f, 3f };

        /// <summary>The six settings step 2 offers, copied to and from a <see cref="SettingsSave"/>.</summary>
        private struct Quick
        {
            public float Music, Sfx, Speed;
            public bool Motion, Colorblind;
            public int Level;

            public static Quick From(SettingsSave s) => new Quick
            {
                Music = s.MusicVolume, Sfx = s.SfxVolume, Speed = s.BattleSpeed, Motion = s.ReduceMotion,
                Colorblind = s.ColorblindMode, Level = DifficultyTable.Clamp(s.Difficulty?.Level ?? DifficultyTable.Default),
            };

            public void To(SettingsSave s)
            {
                s.MusicVolume = Music; s.SfxVolume = Sfx; s.BattleSpeed = Speed;
                s.ReduceMotion = Motion; s.ColorblindMode = Colorblind;
                s.Difficulty ??= new DifficultySettings();
                s.Difficulty.Level = Level;
            }
        }

        private int step = 1;
        private string commander = "";
        private Quick pick;
        private Quick original;
        private readonly string claim;
        private bool begun;
        private TextField field;
        private Label problem;
        private Button next;

        public CreateProfileScreen()
        {
            original = Quick.From(App.Settings);
            pick = original;
            claim = App.Profile.IsUnclaimedDefault ? App.Profile.Name : null;
        }

        private bool CanCancel => App.Settings.FirstRunDone;

        public override bool OnBack()
        {
            if (step > 1) { Go(step - 1); return false; }
            return CanCancel;
        }

        public override void OnHidden()
        {
            if (begun) return;
            original.To(App.Settings);
            App.ApplySettings();
        }

        private void Go(int to)
        {
            if (step == 1 && field != null) commander = field.value?.Trim() ?? "";
            step = to;
            Refresh();
        }

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, "cityruins", 0);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col", root);
            Action back = step > 1 || CanCancel ? () => App.Router.Back() : null;
            Ui.Header("Create Profile", back, col, out _, $"Step {step} of 3");
            var body = Ui.PageBody(col);
            if (step == 1) NameStep(body);
            else if (step == 2) SettingsStep(body);
            else SummaryStep(body);
        }

        private void NameStep(VisualElement body)
        {
            var w = Ui.Panel(body, "Who leads the crew?", false, "create-profile-name");
            Ui.Note("This is the name the story calls you. It names your profile too.", w);
            field = ProfileNameScreen.NameField("create-profile-name-field", commander, w);
            field.RegisterValueChangedCallback(_ => Validate());
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) SubmitName(); });
            problem = Ui.Para("", w, "warn");
            problem.name = "create-profile-problem";
            var row = Footer(w);
            next = RowIcon.IconButton("Next", RowKind.Confirm, SubmitName, "btn--primary", row);
            next.name = "create-profile-next";
            Validate();
            field.schedule.Execute(() => field.Focus()).ExecuteLater(200);
        }

        private void Validate()
        {
            var why = ProfileNameScreen.Problem(field.value, claim);
            problem.text = string.IsNullOrWhiteSpace(field.value) ? "" : why ?? "";
            Ui.SetEnabled(next, why == null);
        }

        private void SubmitName()
        {
            var why = ProfileNameScreen.Problem(field.value, claim);
            if (why != null) { App.Audio.Sfx("ui.error"); problem.text = why; return; }
            Go(2);
        }

        private void SettingsStep(VisualElement body)
        {
            var w = Ui.ListPanel(body, "Quick settings", "create-profile-settings");
            void Row(string label, string value, Action set, string name)
            {
                var row = Ui.MenuRow(label, value, () => { set(); Preview(); Refresh(); }, w).KindIcon(RowKind.Settings);
                row.name = name;
            }
            float Cycle(float[] opts, float value) => opts[(Array.FindIndex(opts, o => Mathf.Abs(o - value) < 0.01f) + 1) % opts.Length];
            string Pct(float v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0%}", v);

            Row("Music volume", Pct(pick.Music), () => pick.Music = Cycle(Volumes, pick.Music), "create-profile-music");
            Row("Sound volume", Pct(pick.Sfx), () => { pick.Sfx = Cycle(Volumes, pick.Sfx); }, "create-profile-sfx");
            Row("Battle speed", string.Format(System.Globalization.CultureInfo.InvariantCulture, "×{0:0.#}", pick.Speed), () => pick.Speed = Cycle(Speeds, pick.Speed), "create-profile-speed");
            Row("Reduce motion", pick.Motion ? "ON" : "OFF", () => pick.Motion = !pick.Motion, "create-profile-motion");
            Row("Colorblind palette", pick.Colorblind ? "ON" : "OFF", () => pick.Colorblind = !pick.Colorblind, "create-profile-colorblind");
            Row("Difficulty", pick.Level.ToString(), () => pick.Level = pick.Level >= DifficultyTable.Max ? DifficultyTable.Min : pick.Level + 1, "create-profile-difficulty");
            Ui.Note(DifficultyTable.Describe(pick.Level), w).name = "create-profile-difficulty-text";
            Ui.Note("You can change any of this in Settings.", w);
            var row = Footer(w);
            RowIcon.IconButton("Back", RowKind.Back, () => Go(1), null, row).name = "create-profile-back";
            RowIcon.IconButton("Next", RowKind.Confirm, () => Go(3), "btn--primary", row).name = "create-profile-next";
            Ui.SelectFirst(w);
        }

        /// <summary>The picks applied live (law 5: a sound setting is heard as it changes), not yet written to any file.</summary>
        private void Preview()
        {
            pick.To(App.Settings);
            App.ApplySettings();
            App.Audio.Sfx("ui.confirm");
        }

        private void SummaryStep(VisualElement body)
        {
            var w = Ui.Panel(body, $"Ready, {commander}", false, "create-profile-summary");
            string speed = string.Format(System.Globalization.CultureInfo.InvariantCulture, "×{0:0.#}", pick.Speed);
            string text = $"Music {pick.Music:0%} · Sound {pick.Sfx:0%} · Battle speed {speed} · Difficulty {pick.Level}";
            if (pick.Motion) text += " · Reduce motion";
            if (pick.Colorblind) text += " · Colorblind palette";
            Ui.Para(text, w).name = "create-profile-summary-text";
            Ui.Note("You can change any of this in Settings.", w);
            var row = Footer(w);
            RowIcon.IconButton("Back", RowKind.Back, () => Go(2), null, row).name = "create-profile-back";
            RowIcon.IconButton("Begin", RowKind.Confirm, Begin, "btn--primary", row).name = "create-profile-begin";
        }

        private static VisualElement Footer(VisualElement parent)
        {
            var row = Ui.El("kit-footer", parent);
            row.style.justifyContent = Justify.FlexEnd;
            return row;
        }

        private void Begin()
        {
            var why = ProfileNameScreen.Problem(commander, claim);
            if (why != null) { App.Audio.Sfx("ui.error"); App.Toast(why); Go(1); return; }
            try
            {
                if (claim != null) App.RenameProfile(claim, commander);
                else
                {
                    original.To(App.Settings); // the previews were on the old profile's settings; it keeps what it had
                    App.CreateProfile(commander);
                    App.SwitchProfile(commander);
                }
                begun = true;
                pick.To(App.Settings);
                App.Settings.FirstRunDone = true;
                App.ApplySettings();
                App.SaveSettings();
                App.Toast($"Playing as {commander}");
                App.Router.ResetTo(new TitleScreen());
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is System.IO.IOException)
            {
                App.Audio.Sfx("ui.error");
                App.Toast("Couldn't create the profile");
                Debug.LogError("[Profiles] create: " + e.Message);
            }
        }
    }
}
