using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Meta;
using Unity.Shared.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Player profiles (bible §15.4, GG-T99) — GridGame2026's ProfileSelect / ProfileCreate, built in code.
    // Each profile owns its own settings.json and three save slots (Profiles/<Name>/…), via com.mindattic.unity.shared.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Pick, create, rename or delete a player profile. Reached from the Title's profile row.</summary>
    public sealed class ProfileSelectScreen : GameScreen
    {
        public override string Id => "profiles";

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, "cityruins", 0);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col", root);
            Ui.Header("Profiles", () => App.Router.Back(), col, out _);
            var list = Ui.ListPanel(Ui.PageBody(col), "Choose a profile", "profile-list");
            Ui.Para("Each profile keeps its own settings and its own three save slots.", list, "dim");
            string current = App.Profile.Name;
            foreach (var name in App.Profile.List())
            {
                string n = name;
                bool active = string.Equals(n, current, StringComparison.OrdinalIgnoreCase);
                var row = Ui.MenuRow(n, active ? "Playing" : "", () => Switch(n), list);
                row.name = "profile-" + n;
                if (active) row.Q<Label>("label").AddToClassList("gold");
                var actions = Ui.Actions(list);
                actions.AddToClassList("kit-note"); // under the row's label
                var rename = Ui.Button("Rename", () => App.Router.Push(new ProfileNameScreen(n)), "btn--small", actions);
                rename.name = "rename-" + n;
                var delete = Ui.Button("Delete", () => AskDelete(n), "btn--small", actions);
                delete.name = "delete-" + n;
            }
            var create = Ui.MenuRow("+ New profile", "", () => App.Router.Push(new ProfileNameScreen(null)), list);
            create.name = "new-profile";
            Ui.SelectFirst(list);
        }

        private void Switch(string name)
        {
            if (!string.Equals(name, App.Profile.Name, StringComparison.OrdinalIgnoreCase))
            {
                App.SwitchProfile(name);
                App.Toast($"Playing as {App.Profile.Name}");
            }
            App.Router.Back();
        }

        private void AskDelete(string name)
        {
            bool last = App.Profile.List().Count <= 1;
            string more = last ? " A fresh Default profile takes its place." : "";
            Confirm.Show($"Delete profile \"{name}\"? Its settings and save slots go with it (moved to the trash folder, not erased).{more}", "Delete", () =>
            {
                try { App.DeleteProfile(name); App.Toast($"Deleted {name}"); }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is System.IO.IOException) { App.Toast("Couldn't delete it"); Debug.LogError("[Profiles] delete: " + e.Message); }
                Refresh();
            });
        }
    }

    /// <summary>Type a profile name: a new profile (<c>renaming == null</c>) or a new name for an existing one.</summary>
    public sealed class ProfileNameScreen : GameScreen
    {
        private readonly string renaming;
        private TextField field;
        private Label problem;
        private Button ok;

        public ProfileNameScreen(string renaming) { this.renaming = renaming; }
        public override string Id => "profile-name";

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, "cityruins", 0);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col", root);
            Ui.Header(renaming == null ? "New Profile" : "Rename Profile", () => App.Router.Back(), col, out _);
            var w = Ui.Panel(Ui.PageBody(col), renaming == null ? "Name your profile" : $"New name for \"{renaming}\"");

            // A resize rebuild keeps what was typed (the old field is still referenced here until replaced).
            field = new TextField { name = "profile-name-field", maxLength = ProfileStore.MaxNameLength, value = field?.value ?? renaming ?? "" };
            field.style.fontSize = TypeScale.H3;
            field.style.marginTop = Space.S8; field.style.marginBottom = Space.S8;
            field.style.minHeight = 96;
            var input = field.Q(className: "unity-text-field__input") ?? field.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.backgroundColor = Palette.Ink.WithA(0.9f);
                input.style.color = Color.white;
                input.style.paddingLeft = input.style.paddingRight = Space.S16;
                input.style.borderTopWidth = input.style.borderBottomWidth = input.style.borderLeftWidth = input.style.borderRightWidth = 3;
                input.style.borderTopColor = input.style.borderBottomColor = input.style.borderLeftColor = input.style.borderRightColor = Palette.Neutral.WithA(0.8f);
            }
            field.RegisterValueChangedCallback(_ => Validate());
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Submit(); });
            w.Add(field);

            problem = Ui.Para("", w, "warn");
            problem.name = "profile-name-problem";

            var row = Ui.El("kit-footer", w);
            row.style.justifyContent = Justify.FlexEnd;
            Ui.Button("Cancel", () => App.Router.Back(), null, row);
            ok = Ui.Button(renaming == null ? "Create" : "Rename", Submit, "btn--primary", row);
            ok.name = "profile-name-ok";
            Validate();
            field.schedule.Execute(() => field.Focus()).ExecuteLater(200);
        }

        /// <summary>The reason <paramref name="name"/> can't be used, or null when it can.</summary>
        public static string Problem(string name, string renaming)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Type a name.";
            name = name.Trim();
            if (!ProfileStore.IsValidName(name)) return "That name can't be used (no / \\ : * ? \" < > |, no leading or trailing dot).";
            bool sameProfile = renaming != null && string.Equals(name, renaming, StringComparison.OrdinalIgnoreCase);
            if (!sameProfile && GameApp.I.Profile.Store.Exists(name)) return "There's already a profile with that name.";
            return null;
        }

        private void Validate()
        {
            var why = Problem(field.value, renaming);
            // An empty field just waits for typing; only a real problem is spelled out.
            problem.text = string.IsNullOrWhiteSpace(field.value) ? "" : why ?? "";
            Ui.SetEnabled(ok, why == null);
        }

        private void Submit()
        {
            var name = field.value?.Trim();
            var why = Problem(name, renaming);
            if (why != null) { App.Audio.Sfx("ui.error"); problem.text = why; return; }
            try
            {
                if (renaming == null)
                {
                    App.CreateProfile(name);
                    App.SwitchProfile(name); // a new profile is made to be played (GridGame2026's ProfileCreate did the same)
                    App.Toast($"Playing as {name}");
                    App.Router.ResetTo(new TitleScreen());
                    return;
                }
                if (name != renaming) App.RenameProfile(renaming, name);
                App.Router.Back();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is System.IO.IOException)
            {
                App.Audio.Sfx("ui.error");
                problem.text = "Couldn't do that: " + e.Message;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Save transfer code (bible §15.2, GG-T607): move a slot to another device as copyable text, no cloud account needed.
    // ─────────────────────────────────────────────────────────────────────────────

    internal static class SaveTransferUi
    {
        /// <summary>A tall read/write text box for a transfer code (wraps, scrolls).</summary>
        public static TextField CodeField(string name, string value, bool readOnly, VisualElement parent)
        {
            var f = new TextField { name = name, multiline = true, isReadOnly = readOnly, value = value ?? "", maxLength = SaveTransfer.MaxCodeChars };
            f.style.height = 360;
            f.style.marginTop = Space.S8; f.style.marginBottom = Space.S8;
            f.style.whiteSpace = WhiteSpace.Normal;
            var input = f.Q(className: "unity-text-field__input") ?? f.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.backgroundColor = Palette.Ink.WithA(0.9f);
                input.style.color = Color.white;
                input.style.whiteSpace = WhiteSpace.Normal;
                input.style.unityTextAlign = TextAnchor.UpperLeft;
                input.style.paddingLeft = input.style.paddingRight = Space.S16;
            }
            parent.Add(f);
            return f;
        }

        /// <summary>Every save file on this device: each profile's three slots (the IAP store scans the same set, GG-T469).</summary>
        public static List<string> AllSlotPaths(GameApp app)
        {
            var paths = new List<string>();
            try
            {
                var folders = new List<string>();
                if (app.Saves != null) folders.Add(app.Saves.Root);
                var profiles = app.Profile?.Store;
                if (profiles != null)
                    foreach (var name in profiles.List())
                        folders.Add(System.IO.Path.Combine(profiles.Root, name, "Saves"));
                foreach (var folder in folders.Select(System.IO.Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
                    for (int i = 0; i < SaveFiles.Slots; i++) paths.Add(SaveFiles.PathFor(folder, i));
            }
            catch (Exception e) { Debug.LogWarning("[Save] couldn't list the saves: " + e.Message); }
            return paths;
        }
    }

    /// <summary>Settings › Transfer save: a slot shown as a transfer code to copy onto another device (GG-T607).</summary>
    public sealed class SaveExportScreen : HubPage
    {
        public override string Id => "save-export";
        protected override string Title => "Transfer Save";
        private int slot;

        public SaveExportScreen(int slot) { this.slot = slot; }

        protected override void BuildBody(VisualElement body)
        {
            var w = Ui.Panel(body, "Copy this code to the other device", false, "save-export-panel");
            Ui.Note("On the other device: Settings › Import save, paste it, pick an empty slot. The code holds this whole slot.", w);
            var pick = Ui.Actions(w);
            for (int i = 0; i < SaveFiles.Slots; i++)
            {
                int n = i;
                var b = Ui.Button($"Slot {n + 1}", () => { slot = n; Refresh(); }, n == slot ? "btn--small btn--primary" : "btn--small", pick);
                b.name = $"save-export-slot-{n + 1}";
                Ui.SetEnabled(b, App.Saves != null && App.Saves.Exists(n));
            }

            string code = null;
            try
            {
                // The loaded game is exported as it is in memory (Persist has written the same); any other slot is read from disk.
                var save = slot == App.Slot && App.Save != null ? App.Save : App.Saves?.Exists(slot) == true ? App.Saves.Load(slot, App.Content) : null;
                if (save != null) code = SaveTransfer.Pack(save);
            }
            catch (Exception e) { Debug.LogError($"[Save] slot {slot + 1} couldn't be packed: {e.Message}"); }
            if (code == null) { Ui.Para("That slot is empty.", w, "warn").name = "save-export-empty"; return; }

            SaveTransferUi.CodeField("save-export-code", code, true, w);
            var row = Ui.El("kit-footer", w);
            row.style.justifyContent = Justify.FlexEnd;
            Ui.Button("Copy code", () => { GUIUtility.systemCopyBuffer = code; App.Audio.Sfx("ui.confirm"); App.Toast("Save code copied"); }, "btn--primary", row).name = "save-export-copy";
        }
    }

    /// <summary>Settings › Import save: paste a transfer code into an empty slot (GG-T607). Never overwrites a slot, refuses
    /// a damaged, foreign or newer-version code, and refuses a paid save already on this device (it would pay twice).</summary>
    public sealed class SaveImportScreen : HubPage
    {
        public override string Id => "save-import";
        protected override string Title => "Import Save";
        private TextField field;
        private Label problem;
        private int slot = -1;

        protected override void BuildBody(VisualElement body)
        {
            var w = Ui.Panel(body, "Paste a save code", false, "save-import-panel");
            Ui.Note("Copy the code on the other device (Settings › Transfer save), paste it here and pick an empty slot.", w);
            field = SaveTransferUi.CodeField("save-import-code", field?.value, false, w);
            var paste = Ui.Actions(w);
            Ui.Button("Paste", () => { field.value = GUIUtility.systemCopyBuffer ?? ""; problem.text = ""; }, "btn--small", paste).name = "save-import-paste";

            Ui.Section("Into slot", w);
            var pick = Ui.Actions(w);
            bool anyEmpty = false;
            for (int i = 0; i < SaveFiles.Slots; i++)
            {
                int n = i;
                bool empty = App.Saves != null && !App.Saves.Exists(n);
                anyEmpty |= empty;
                if (!empty && slot == n) slot = -1;
                var b = Ui.Button(empty ? $"Slot {n + 1} (empty)" : $"Slot {n + 1} (in use)", () => { slot = n; Refresh(); },
                    n == slot ? "btn--small btn--primary" : "btn--small", pick);
                b.name = $"save-import-slot-{n + 1}";
                Ui.SetEnabled(b, empty);
            }

            problem = Ui.Para(anyEmpty ? "" : "Every slot is in use. Delete one first; an import never overwrites a save.", w, "warn");
            problem.name = "save-import-problem";

            var row = Ui.El("kit-footer", w);
            row.style.justifyContent = Justify.FlexEnd;
            Ui.Button("Cancel", () => App.Router.Back(), null, row);
            var ok = Ui.Button("Import", Submit, "btn--primary", row);
            ok.name = "save-import-ok";
            Ui.SetEnabled(ok, anyEmpty && slot >= 0);
        }

        private void Submit()
        {
            string why = null;
            SaveData save = null;
            if (App.Saves == null) why = "No profile is open.";
            else if (slot < 0) why = "Pick an empty slot.";
            else if (App.Saves.Exists(slot)) why = "That slot is in use; an import never overwrites a save.";
            else if (!SaveTransfer.TryImport(field?.value, App.Content, out save, out why)) { }
            else if (SaveTransfer.SharesPurchasesWith(save, SaveTransferUi.AllSlotPaths(App)))
                why = "That save is already on this device. A save with purchases can be moved to another device, not copied here.";
            if (why == null && !App.Saves.Write(slot, save)) why = "Couldn't save!";
            if (why != null)
            {
                App.Audio.Sfx("ui.error");
                problem.text = why;
                return;
            }
            App.Audio.Sfx("ui.confirm");
            App.Toast($"Imported into slot {slot + 1}");
            App.Router.Back();
        }
    }
}
