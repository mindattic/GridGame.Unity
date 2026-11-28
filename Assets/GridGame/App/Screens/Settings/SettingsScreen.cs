using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed class SettingsScreen : HubPage
    {
        public override string Id => "settings";
        protected override string Title => "Settings";
        /// <summary>The active profile (GG-T99), in the header (GG-T454).</summary>
        protected override string Subtitle => $"Profile: {App.Profile?.Name}";
        protected override string SubtitleName => "settings-profile";

        /// <summary>The difficulty dial (§22.9, GG-T309): − / level / + (each a 132-px target), then one line naming what
        /// the level does and its reward multiplier. Saved at once; the next battle is built at the new level.</summary>
        private void DifficultyControl(SettingsSave st, VisualElement parent)
        {
            st.Difficulty ??= new DifficultySettings();
            int level = DifficultyTable.Clamp(st.Difficulty.Level);
            void Set(int to)
            {
                st.Difficulty.Level = Math.Max(DifficultyTable.Min, Math.Min(DifficultyTable.Max, to));
                App.SaveSettings(); Refresh();
            }
            var row = Ui.El("row", parent);
            row.name = "settings-difficulty";
            row.style.alignItems = Align.Center;
            row.style.minHeight = Kit.Row;
            Ui.Pad(row, left: Kit.Indent); // the label lines up with the menu rows' labels
            row.KindIcon(RowKind.Bounty); // GG-T750 (§26.7): lines up with the setting rows' icons
            Ui.Text("Difficulty", "menu-label grow", row);
            var n = Ui.Stepper(row, level.ToString(), () => Set(level - 1), () => Set(level + 1), out var minus, out var plus, "gold");
            minus.name = "difficulty-down";
            n.name = "difficulty-level";
            plus.name = "difficulty-up";
            Ui.SetEnabled(minus, level > DifficultyTable.Min);
            Ui.SetEnabled(plus, level < DifficultyTable.Max);
            Ui.Note(DifficultyTable.Describe(level) + " Applies from the next battle.", parent).name = "difficulty-text";
        }

        /// <summary>The turn mode (§2.13, GG-T774): the profile's <see cref="SettingsSave.TurnMode"/>, cycled like Battle
        /// speed through <see cref="TurnModes.Available"/> (only modes that are built), then one line explaining it. A save
        /// New Game locked to a mode keeps playing that one, and the line says so; changing the mode here releases that lock
        /// so the save follows Settings again (GG-T1216: switching modes to test them is never blocked).</summary>
        private void TurnModeControl(SettingsSave st, VisualElement parent)
        {
            var c = App.Content;
            var mode = TurnModes.IsBuilt(st.TurnMode) ? st.TurnMode : TurnModes.Available[0];
            var row = Ui.MenuRow("Turn mode", TurnModes.Name(c, mode), () =>
            {
                st.TurnMode = TurnModes.Next(mode);
                App.SaveSettings();
                if (App.Save?.TurnModeLock != null) { TurnModeSetting.Lock(App.Save, null); App.Persist(); }
                Refresh();
            }, parent);
            row.name = "settings-turn-mode";
            row.KindIcon(RowKind.NewGamePlus);
            string text = TurnModes.Describe(c, mode);
            if (App.Save?.TurnModeLock is TurnMode locked)
                text += $" This save is locked to {TurnModes.Name(c, locked)} until you change it here.";
            Ui.Note(text, parent).name = "settings-turn-mode-text";
        }

        /// <summary>A parent passed the gate this visit, so the limit cycles freely until Settings closes.</summary>
        private bool limitUnlocked;

        /// <summary>The monthly spending limit (§25.18.1, GG-T1559): the wallet's limit and this month's spend, cycled through
        /// <see cref="SpendLimits.Choices"/> behind the parental gate unless the wallet answered adult.</summary>
        private void SpendingLimitRow(AccountWallet wallet, VisualElement parent)
        {
            void Cycle()
            {
                wallet.MonthlyLimitCents = SpendLimits.Next(wallet.MonthlyLimitCents);
                DiamondExchange.WalletDirty = true;
                App.PersistWallet();
                Refresh();
            }
            string spent = SpendLimits.Money(SpendLimits.SpentCents(wallet, DateTime.UtcNow));
            var row = Ui.MenuRow("Spending limit", $"{SpendLimits.Describe(wallet)} · {spent} this month", () =>
            {
                if (limitUnlocked || !SpendLimits.LimitGated(wallet)) Cycle();
                else SpendGate.Parental(() => { limitUnlocked = true; Cycle(); });
            }, parent);
            row.name = "settings-spending-limit";
            row.KindIcon(RowKind.Shop);
        }

        /// <summary>Delete all game data (§15, GG-T1580): two questions, then <see cref="GameApp.DeleteAllData"/> erases this
        /// device's profiles, slots, settings and wallet, and the cloud copies, and the app starts over at the title.</summary>
        private void DeleteAllDataRow(VisualElement parent)
        {
            var row = Ui.MenuRow("Delete all game data", "Erase", () =>
                Confirm.Show("Delete all game data? Every profile, save slot and setting on this device is erased, and so are their cloud copies. This can't be undone.", "Continue", () =>
                    Confirm.Show($"Purchased {ShardText.Plural}, owned products and cosmetics are lost too, and are not refunded. Delete everything?", "Delete all", () =>
                        App.DeleteAllData(ok => App.Toast(ok ? "All game data deleted" : "Some game data couldn't be deleted")))), parent);
            row.name = "settings-delete-all-data";
            row.KindIcon(RowKind.Close);
        }

        /// <summary>Party levels the Developer section cycles through.</summary>
        private static readonly int[] DevLevels = { 1, 5, 10, 20, 30, 40, 50 };

        /// <summary>Settings &gt; Developer (GG-T1938): shown only once the version line was tapped seven times on this device.
        /// The Cheats switch adds the pause menu's cheat rows and the DEV badge; with it on, the save cheats below run the
        /// dev automation's own code paths (<see cref="Dev.DevCheats"/>) on the loaded save.</summary>
        private void DeveloperSection(VisualElement w, Action<string> section)
        {
            section("Developer");
            bool cheats = Dev.DevOptions.Cheats;
            var toggle = Ui.MenuRow("Cheats", cheats ? "ON" : "OFF", () => { Dev.DevOptions.SetCheats(!cheats); Refresh(); }, w);
            toggle.name = "settings-dev-cheats";
            toggle.KindIcon(RowKind.Settings);
            if (!cheats) { Ui.Note("Turn Cheats on for the pause menu's Kill all enemies, Win battle, Full heal, Fill mana and speeds up to ×8.", w); return; }
            var s = App.Save;
            if (s == null) { Ui.Note("Load a save to give gold, items, stages and levels.", w); return; }
            var c = App.Content;
            void Cheat(string label, string value, string name, Action act, string done)
            {
                var row = Ui.MenuRow(label, value, () => { act(); App.Persist(); App.Toast(done); Refresh(); }, w);
                row.name = name;
                row.KindIcon(RowKind.Settings);
            }
            Cheat("Give gold", "+10,000", "settings-dev-gold", () => s.Gold += 10000, "+10,000 gold");
            Cheat("Give items", "10 of each consumable", "settings-dev-items", () =>
            {
                foreach (var item in c.Items.Values.Where(i => i.Type == ItemType.Consumable)) Dev.DevCheats.GiveItem(s, item.Id, 10);
            }, "Consumables added");
            Cheat("Unlock all stages", $"{s.HighestClearedStage + 1}/{c.CampaignStages().Count} cleared", "settings-dev-stages",
                () => Dev.DevCheats.UnlockAllStages(c, s), "Every stage unlocked");
            Cheat("Unlock all stations", "", "settings-dev-stations", () => Dev.DevCheats.UnlockAllStations(s), "Every station unlocked");
            int level = s.Roster.Count > 0 ? s.Roster.Max(Progression.Level) : 1;
            int next = DevLevels.FirstOrDefault(l => l > level);
            if (next == 0) next = DevLevels[0];
            Cheat("Party level", $"{level} → {next}", "settings-dev-level", () => Dev.DevCheats.SetLevel(s.Roster, next), $"Party level {next}");
        }

        protected override void BuildBody(VisualElement body)
        {
            // The active profile's settings (GG-T99, §15.4): one set for every save slot, kept in its settings.json.
            var st = App.Settings;
            // GG-T454: one scrolling list (the rows, then How to play), so the last rows are never cut off a short phone.
            // GG-T1223: grouped into sections; GG-T1968 orders them Accessibility, Gameplay, Sound, Graphics, Display, Experimental, Data.
            var w = Ui.ListPanel(body, null, "settings-list");
            // GG-T750 (§26.7): every setting row leads with its own kind icon (GG-T1886), the save rows with the Save disk.
            VisualElement Row(RowKind kind, string label, string value, Action set, string name = null)
            {
                Action apply = () => { set(); App.ApplySettings(); App.SaveSettings(); Refresh(); };
                var row = Ui.MenuRow(label, value, apply, w).KindIcon(kind);
                if (name != null) row.name = name;
                return row;
            }
            void Toggle(RowKind kind, string label, bool value, Action<bool> set, string name = null) => Row(kind, label, value ? "ON" : "OFF", () => set(!value), name);
            void Cycle(RowKind kind, string label, float value, float[] opts, string fmt, Action<float> set, string name = null)
            {
                int i = Array.FindIndex(opts, o => Mathf.Abs(o - value) < 0.01f);
                Row(kind, label, string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, value), () => set(opts[(i + 1) % opts.Length]), name);
            }
            void CycleInt(RowKind kind, string label, int value, int[] opts, Func<int, string> text, Action<int> set, string name)
            {
                int i = Array.IndexOf(opts, value);
                Row(kind, label, text(value), () => set(opts[(i + 1) % opts.Length]), name);
            }
            void Section(string title) => Ui.Section(title, w).name = "settings-section-" + title.ToLowerInvariant();
            void CycleFont()
            {
                var names = Fonts.Presets.Select(p => p.Name).ToArray();
                int i = Array.IndexOf(names, st.FontName);
                st.FontName = names[(i + 1) % names.Length];
                App.ApplySettings(); App.SaveSettings(); Refresh();
            }
            // GG-T1222: Window mode and Resolution only apply to a desktop player, Thumb margin only to a phone.
            bool mobile = Application.isMobilePlatform;
            bool desktop = !mobile && Application.platform != RuntimePlatform.WebGLPlayer;

            Section("Accessibility");
            // GG-T495: text size, applied live to every screen (TextSize classes on the UI root).
            Row(RowKind.Text, "Text size", $"{TextSize.Clamp(st.TextScale)}%", () => st.TextScale = TextSize.Next(st.TextScale), "settings-text-size");
            Toggle(RowKind.Eye, "Colorblind palette", st.ColorblindMode, v => st.ColorblindMode = v);
            Toggle(RowKind.Pause, "Reduce motion", st.ReduceMotion, v => st.ReduceMotion = v);
            Toggle(RowKind.Timer, "Untimed turns", st.UntimedTurns, v => st.UntimedTurns = v, "settings-untimed-turns"); // GG-T1978 (§2.0.1)
            // GG-T392: the look presets are developer dials, hidden while Features.LookPresets is off (GameApp.ApplySettings
            // then draws with the default theme, font and word wrap).
            if (Features.LookPresets)
            {
                // GG-T407: every window theme (presets, the player's own, and the editor for gradient, texture and frame).
                // GG-T1968: the theme editor is a developer tool, so only dev builds and the editor list it.
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    var windows = Ui.MenuRow("Windows", Art.ThemeName, () => App.Router.Push(new WindowEditorScreen()), w);
                    windows.name = "settings-windows";
                    windows.KindIcon(RowKind.Window);
                }
                Ui.MenuRow("Font", st.FontName, CycleFont, w).KindIcon(RowKind.Type);
                // GG-T305: how wrapped text breaks, applied live to every screen (TextFit).
                Row(RowKind.TextWrap, "Text wrapping", st.Hyphenate ? "Hyphenate" : "Word wrap", () => st.Hyphenate = !st.Hyphenate, "settings-text-wrap");
            }

            Section("Gameplay");
            DifficultyControl(st, w); // GG-T309: the 1..10 dial
            Cycle(RowKind.Speed, "Battle speed", st.BattleSpeed, new[] { 0.5f, 1f, 1.5f, 2f, 3f }, "×{0:0.#}", v => st.BattleSpeed = v);
            Toggle(RowKind.Timer, "Drag timer (4 s)", st.DragTimer, v => st.DragTimer = v);
            Toggle(RowKind.Confirm, "Confirm before acting", st.ConfirmActions, v => st.ConfirmActions = v);
            Toggle(RowKind.Focus, "Pincer close-ups", st.PincerCutIns, v => st.PincerCutIns = v);

            Section("Sound");
            Cycle(RowKind.Speaker, "Master volume", st.MasterVolume, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, "{0:0%}", v => st.MasterVolume = v, "settings-master-volume");
            Cycle(RowKind.Note, "Music volume", st.MusicVolume, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, "{0:0%}", v => st.MusicVolume = v);
            Cycle(RowKind.Wave, "Sound volume", st.SfxVolume, new[] { 0f, 0.25f, 0.5f, 0.7f, 0.85f, 1f }, "{0:0%}", v => st.SfxVolume = v);
            Toggle(RowKind.NoteOff, "Mute music", st.MuteMusic, v => st.MuteMusic = v);
            Toggle(RowKind.SpeakerOff, "Mute sound", st.MuteSfx, v => st.MuteSfx = v);
            Toggle(RowKind.WindowOff, "Mute when unfocused", st.MuteWhenUnfocused, v => st.MuteWhenUnfocused = v, "settings-mute-unfocused");

            // GG-T1221: quality (-1 = the project's own), frame-rate cap, vsync, shake and effects amount.
            Section("Graphics");
            var qualities = QualitySettings.names;
            CycleInt(RowKind.Quality, "Quality", st.QualityLevel >= 0 && st.QualityLevel < qualities.Length ? st.QualityLevel : -1,
                Enumerable.Range(-1, qualities.Length + 1).ToArray(), q => q < 0 ? "Default" : qualities[q], v => st.QualityLevel = v, "settings-quality");
            CycleInt(RowKind.Gauge, "Frame rate cap", GameSettings.ClampFrameRateCap(st.FrameRateCap), new[] { 30, 60, -1 },
                f => f < 0 ? "Uncapped" : $"{f} fps", v => st.FrameRateCap = v, "settings-frame-cap");
            if (!mobile) Toggle(RowKind.Sync, "VSync", st.VSync, v => st.VSync = v, "settings-vsync"); // GG-T1240: mobile ignores vsync
            Cycle(RowKind.Shake, "Screen shake", st.ScreenShake, new[] { 0f, 0.5f, 1f }, "{0:0%}", v => st.ScreenShake = v, "settings-screen-shake");
            Cycle(RowKind.Effects, "Effects", st.VfxAmount, new[] { 0f, 0.5f, 1f }, "{0:0%}", v => st.VfxAmount = v, "settings-effects");

            Section("Display");
            if (desktop)
            {
                CycleInt(RowKind.Window, "Window mode", GameSettings.ClampFullScreen(st.FullScreen), new[] { -1, 0, 1 },
                    m => m < 0 ? "As launched" : m == 1 ? "Fullscreen" : "Windowed", v => st.FullScreen = v, "settings-window-mode");
                var sizes = new List<(int w, int h)> { (0, 0) };
                foreach (var r in UnityEngine.Screen.resolutions)
                    if (!sizes.Contains((r.width, r.height))) sizes.Add((r.width, r.height));
                // GG-T1238: a saved size missing from this monitor's list (other monitor, hand edit) is still applied by
                // ApplyDisplay, so list it rather than show "As launched" for it.
                var saved = (st.ResolutionWidth, st.ResolutionHeight);
                if (saved.Item1 > 0 && saved.Item2 > 0 && !sizes.Contains(saved)) sizes.Add(saved);
                int at = Math.Max(0, sizes.IndexOf(saved));
                Row(RowKind.Frame, "Resolution", at == 0 ? "As launched" : $"{sizes[at].w}×{sizes[at].h}", () =>
                {
                    var next = sizes[(at + 1) % sizes.Count];
                    st.ResolutionWidth = next.w; st.ResolutionHeight = next.h;
                }, "settings-resolution");
            }
            if (mobile)
                CycleInt(RowKind.Hand, "Thumb margin", GameSettings.ClampThumbMargin(st.ThumbMargin), new[] { 0, 24, 48, 72, GameSettings.MaxThumbMargin },
                    m => m == 0 ? "OFF" : $"{m} px", v => st.ThumbMargin = v, "settings-thumb-margin");
            CycleInt(RowKind.Scale, "UI scale", GameSettings.ClampUiScale(st.UiScale), new[] { 80, 90, 100 }, s => $"{s}%", v => st.UiScale = v, "settings-ui-scale");

            // GG-T1968 (UX review): Turn mode stays an experiment until GG-T821 picks a mode; still reachable from the title (GG-T1216).
            Section("Experimental");
            TurnModeControl(st, w); // GG-T774 (§2.13)

            Section("Data");
            // GG-T607 (§15.2): move a slot to another device as a copyable code, no cloud account needed.
            var transfer = Ui.MenuRow("Transfer save", "Copy code", () => App.Router.Push(new SaveExportScreen(App.Slot >= 0 ? App.Slot : Math.Max(0, App.Saves?.Root == null ? 0 : SaveFiles.MostRecent(App.Saves.Root)))), w);
            transfer.name = "settings-transfer-save";
            transfer.KindIcon(RowKind.Save);
            var importRow = Ui.MenuRow("Import save", "Paste code", () => App.Router.Push(new SaveImportScreen()), w);
            importRow.name = "settings-import-save";
            importRow.KindIcon(RowKind.Save);
            if (Features.DiamondStore && DiamondExchange.Wallet != null) SpendingLimitRow(DiamondExchange.Wallet, w);
            DeleteAllDataRow(w);
            if (Dev.DevOptions.Unlocked) DeveloperSection(w, Section);
            // GG-T1330: version, legal links and third-party notices.
            var about = Ui.MenuRow("About", "", () => App.Router.Push(new AboutScreen()), w);
            about.name = "settings-about";
            about.KindIcon(RowKind.Codex);
            var version = Ui.Note("Version " + Legal.VersionLine(), w);
            version.name = "settings-version";
            Dev.DevOptions.CountTaps(version); // GG-T1938: seven taps unlock the Developer section
            var help = Ui.El("col", w);
            help.name = "settings-help";
            Ui.Section("How to play", help);
            Ui.Para(Coach.LoopLine, help).name = "settings-loop-line";
            var t = Ui.Para(
                "• Drag a hero tile by tile. Anyone in the way swaps into the tile you left.\n" +
                "• Trap enemies in a straight line between two heroes to PINCER them. Allies next to the attackers join in.\n" +
                "• Moving never hurts. Only pincers and spells deal damage.\n" +
                "• Time moves only when you act. Icons flow left; whoever reaches the left edge acts, and speed decides who gets there first.\n" +
                "• Pincer an enemy to push its icon back on the timeline.\n" +
                "• Every spell has its own mana bar. Pincers and every other mana gain fill every spell's bar, cheap spells faster. Casting empties only that spell's bar. Skills and items cost a turn.\n" +
                "• Tap a hero, a slot, a target, then Cast. Spells charge on the timeline before they land.\n" +
                "• Roles need each other: Tanks Taunt so enemies leave your casters alone, Healers mend (WIS), Mages hit hardest with spells (INT), Strikers swing Cleave and War Cry (STR).\n" +
                "• Fallen enemies sometimes drop a pickup. Walk a hero over it before it fades.\n" +
                "• GUARD skips to the next enemy turn, shields everyone and banks the unused time as mana.", help);
            if (S != null)
            {
                // GG-T539: one button brings back every lesson: the first battle's (on The Tithe Moors I), each battle system's
                // the next time it comes up, and every Camp and vendor lesson. It also turns lessons back on after Skip tutorial.
                var replay = RowIcon.IconButton("Replay tutorials", RowKind.NewGamePlus, () => { Coach.ResetAll(S); Done(Result.Success("Every lesson will show again")); }, "btn--small", Ui.Actions(help));
                replay.name = "replay-camp-tips";
                // GG-T1648: practice a drill from Settings; not from the pause menu, which opens Settings mid-battle.
                if (!App.Router.Stack.Any(x => x is BattleScreen))
                    RowIcon.IconButton("Play a drill", RowKind.Stage, () => App.Router.Push(new DrillPickerScreen()), "btn--small", replay.parent).name = "settings-play-drill";
            }
        }
    }
}
