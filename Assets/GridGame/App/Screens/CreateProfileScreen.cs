using System;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// Create Profile (bible §15.4, GG-T1344, GG-T1957): one step in the menu kit. The Commander's name, prefilled with
    /// a suggestion (<see cref="Suggestion"/>) and checked by the profile name rules (<see cref="ProfileNameScreen.Problem"/>);
    /// Create (one tap) creates the profile, switches to it, marks its first run done and goes to the Main Menu. Settings
    /// keep their defaults and live in Settings. Back cancels only when the active profile has finished its own first run
    /// (there is a profile to go back to). On the first launch (GG-T1345) the store's own empty Default profile is renamed
    /// to the Commander's name instead of a second profile being made.
    /// </summary>
    public sealed class CreateProfileScreen : GameScreen
    {
        public override string Id => "create-profile";

        private readonly string claim;
        private bool begun;
        private TextField field;
        private Label problem;
        private Button create;

        public CreateProfileScreen()
        {
            claim = App.Profile.IsUnclaimedDefault ? App.Profile.Name : null;
        }

        private bool CanCancel => App.Settings.FirstRunDone;

        public override bool OnBack() => CanCancel;

        /// <summary>The name the field starts with: <see cref="Commander.Fallback"/>, numbered past any name <paramref name="taken"/> refuses.</summary>
        internal static string Suggestion(Func<string, bool> taken)
        {
            string name = Commander.Fallback;
            for (int n = 2; taken(name); n++) name = $"{Commander.Fallback} {n}";
            return name;
        }

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, "cityruins", 0);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col", root);
            Action back = CanCancel ? () => App.Router.Back() : null;
            Ui.Header("Create Profile", back, col, out _);
            var body = Ui.PageBody(col);

            var w = Ui.Panel(body, "Who leads the crew?", false, "create-profile-name");
            Ui.Note("This is the name the story calls you. It names your profile too.", w);
            // A resize rebuild keeps what was typed (the old field is still referenced here until replaced).
            string value = field?.value ?? Suggestion(n => ProfileNameScreen.Problem(n, claim) != null);
            field = ProfileNameScreen.NameField("create-profile-name-field", value, w);
            field.RegisterValueChangedCallback(_ => Validate());
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Create(); });
            problem = Ui.Para("", w, "warn");
            problem.name = "create-profile-problem";
            Ui.Note("Music, sound, difficulty and the rest are in Settings.", w);

            var row = Ui.El("kit-footer", w);
            row.style.justifyContent = Justify.FlexEnd;
            create = RowIcon.IconButton("Create", RowKind.Confirm, Create, "btn--primary", row, confirmTap: false);
            create.name = "create-profile-create";
            Validate();
            field.schedule.Execute(() => { field.Focus(); field.SelectAll(); }).ExecuteLater(200);
        }

        private void Validate()
        {
            var why = ProfileNameScreen.Problem(field.value, claim);
            problem.text = string.IsNullOrWhiteSpace(field.value) ? "" : why ?? "";
            Ui.SetEnabled(create, why == null);
        }

        private void Create()
        {
            if (begun) return;
            var commander = field.value?.Trim();
            var why = ProfileNameScreen.Problem(commander, claim);
            if (why != null) { App.Audio.Sfx("ui.error"); problem.text = why; return; }
            try
            {
                if (claim != null) App.RenameProfile(claim, commander);
                else
                {
                    App.CreateProfile(commander);
                    App.SwitchProfile(commander);
                }
                begun = true;
                App.Settings.FirstRunDone = true;
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
