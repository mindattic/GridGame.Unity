using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>The player's settings — one set per profile, stored in Profiles/&lt;Name&gt;/settings.json through
    /// <see cref="GameSettings"/> (GG-T99), not in a save slot.</summary>
    public sealed class SettingsSave
    {
        public float MusicVolume = 0.6f;
        public float SfxVolume = 0.7f;
        public bool MuteMusic;
        public bool MuteSfx;
        public bool ColorblindMode;
        public bool ReduceMotion;
        /// <summary>Terra Battle-style drag timer (§2, act-advanced amendment). Off by default.</summary>
        public bool DragTimer;
        /// <summary>Untimed turns (GG-T1978, §2.0.1): a held hero has no real-time limit and no drop is forced. Off by default.</summary>
        public bool UntimedTurns;
        /// <summary>Battle animation speed multiplier (1 = normal; 0.5 the slowest).</summary>
        public float BattleSpeed = 1f;
        /// <summary>Close-up portrait cut-in on pincers (ported from GridGame2026).</summary>
        public bool PincerCutIns = true;
        /// <summary>Pick a target, then confirm (owner direction 2026-10-11). Off = commit on the pick, for veterans.</summary>
        public bool ConfirmActions = true;
        /// <summary>Menu window theme by name: a preset from Resources/UI/themes.json or one of <see cref="CustomThemes"/>
        /// (GG-T407); unknown/missing names fall back to the default: Manapunk since 2026-11-22 (GG-T678), Dark before
        /// (see <see cref="DefaultMenuTheme"/>).</summary>
        public string MenuTheme = DefaultMenuTheme;
        /// <summary>The player's own window themes from Settings → Windows (GG-T407), at most
        /// <see cref="WindowThemeCatalog.MaxCustom"/>, each named apart from every preset.</summary>
        public List<WindowThemeDef> CustomThemes = new List<WindowThemeDef>();
        /// <summary>The player's Brightness/Contrast/Gamma for each preset theme, by theme name (GG-T563): kept here, on the
        /// device, so themes.json stays pristine and an update never wipes the tuning. A preset not listed is at 0.5/0.5/0.5;
        /// a custom theme carries its own values.</summary>
        public Dictionary<string, WindowToneDef> ThemeTones = new Dictionary<string, WindowToneDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Body font by name (App.Fonts.Presets); unknown/missing names fall back to Rounded (GG-T385).</summary>
        public string FontName = DefaultFontName;
        // Owner 2026-11-21: "we tried FF, then Chrono, then back to FF - none of these where right; but the colors
        // in all these apps is better" (GridGame.Gallery's own look, see Theme.uss's .theme-dark retint) - "Dark" is
        // the base default now, not "Classic" (the Final Fantasy blue it was reverted to under GG-T453).
        // GG-T678 (owner 2026-11-22): "Manapunk", between Dark and the FF blue, is the default; Dark stays selectable.
        public const string DefaultMenuTheme = "Manapunk", DefaultFontName = "Rounded";
        /// <summary>Settings → Text wrapping (GG-T305): false = Word wrap (default), true = Hyphenate (ragged lines and
        /// overlong words break at <see cref="Hyphenation"/> points).</summary>
        public bool Hyphenate;
        /// <summary>Settings → Text size (GG-T495): 100, 115 or 130 percent of the menu type scale; anything else reads as 100.</summary>
        public int TextScale = 100;
        /// <summary>The difficulty dial 1..10 (§22.9, GG-T309), 10 by default; applies from the next battle.</summary>
        public DifficultySettings Difficulty = new DifficultySettings();
        /// <summary>The turn mode (§2.13, GG-T773): <c>turnMode</c> in settings.json, <see cref="TurnMode.PartyWindow"/> by
        /// default. A save's <see cref="SaveData.TurnModeLock"/> overrides it (<see cref="TurnModeSetting.Resolve"/>).</summary>
        public TurnMode TurnMode = TurnMode.PartyWindow;
        /// <summary>Graphics → Quality (GG-T1221): a QualitySettings level index; -1 (default) leaves the project's own level.</summary>
        public int QualityLevel = -1;
        /// <summary>Display → Frame rate (GG-T2102): 60 (default) or 120 where the display supports it; anything else reads as 60.</summary>
        public int FrameRateCap = 60;
        /// <summary>Graphics → VSync (GG-T1221): off by default; on, it paces frames and the cap is not used.</summary>
        public bool VSync;
        /// <summary>Graphics → Screen shake (GG-T1221): 0..1 scale on every shake, 1 = full (default), 0 = off.</summary>
        public float ScreenShake = 1f;
        /// <summary>Graphics → Effects amount (GG-T1221): 0..1 scale on particle emission, 1 = full (default).</summary>
        public float VfxAmount = 1f;
        /// <summary>Sound → Master volume (GG-T1221): 0..1 over music and SFX, 1 by default.</summary>
        public float MasterVolume = 1f;
        /// <summary>Sound → Mute when unfocused (GG-T1221): silence everything while the window lacks focus. Off by default.</summary>
        public bool MuteWhenUnfocused;
        /// <summary>Display → Window mode (GG-T1222, desktop only): -1 (default) leaves the window as launched, 0 windowed,
        /// 1 fullscreen.</summary>
        public int FullScreen = -1;
        /// <summary>Display → Resolution (GG-T1222, desktop only): window size in px; 0 (default) leaves it as launched.</summary>
        public int ResolutionWidth, ResolutionHeight;
        /// <summary>Display → Thumb margin (GG-T1222, phones only): extra design px kept clear above the safe area's
        /// bottom edge, 0 (default) to <see cref="GameSettings.MaxThumbMargin"/>.</summary>
        public int ThumbMargin;
        /// <summary>Accessibility → UI scale (GG-T1222): 80, 90 or 100 (default) percent of the screen the stage fills.</summary>
        public int UiScale = 100;
        /// <summary>GG-T1618: the first-run commander-name prompt has been answered (or never applies) for this profile.</summary>
        public bool FirstRunDone;
        /// <summary>GG-T1645: the last drill's To Camp was pressed on some slot of this profile; Start Campaign's
        /// New to GridGame? question then defaults to Skip to Camp.</summary>
        public bool DrillsFinished;
        /// <summary>GG-T1456: the equipped cosmetic per <see cref="CosmeticKind"/> name (Frame, Theme, Backdrop); none = the
        /// plain look. Ownership is on the account wallet; <see cref="Cosmetics.Reconcile"/> drops what it no longer owns.</summary>
        public Dictionary<string, string> Cosmetics = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// The one place a battle's turn mode is picked (§2.13, GG-T773): the save's lock if it has one, else the profile's
    /// setting. <see cref="BattleFactory.Build"/> (campaign, stage, Crucible, bounty, wanderer, test-mode and tutorial
    /// battles), <see cref="EndlessEncounter.Build"/> and the app's battle screen all set <see cref="BattleSetup.TurnMode"/>
    /// from it; no battle picks its mode any other way.
    /// </summary>
    public static class TurnModeSetting
    {
        /// <summary>The `-turnMode` launch flag's mode (GG-T1237): wins over the profile but not a save's lock, and is never
        /// written to settings.json, so a later Settings save cannot persist it.</summary>
        public static TurnMode? LaunchOverride;

        /// <summary>The mode a battle on <paramref name="save"/> plays: its lock, else <paramref name="profile"/>.
        /// A value the enum doesn't know (a hand edit) reads as <see cref="TurnMode.PartyWindow"/>.</summary>
        public static TurnMode Resolve(SaveData save, TurnMode profile)
        {
            var mode = save?.TurnModeLock ?? LaunchOverride ?? profile;
            return Enum.IsDefined(typeof(TurnMode), mode) ? mode : TurnMode.PartyWindow;
        }

        /// <summary>The mode a battle on <paramref name="save"/> plays under <paramref name="settings"/> (null = the defaults).</summary>
        public static TurnMode Resolve(SaveData save, SettingsSave settings) =>
            Resolve(save, settings?.TurnMode ?? TurnMode.PartyWindow);

        /// <summary>New Game's choice (GG-T773): locks <paramref name="save"/> to <paramref name="mode"/>, or unlocks it
        /// (null) so it follows the profile setting.</summary>
        public static void Lock(SaveData save, TurnMode? mode)
        {
            if (save == null) return;
            save.TurnModeLock = mode is TurnMode m && Enum.IsDefined(typeof(TurnMode), m) ? m : (TurnMode?)null;
        }

        /// <summary>The mode a command-line flag or harness call names (GG-T775): any <see cref="TurnMode"/> name, any case.
        /// False with a one-line <paramref name="error"/> when the name is unknown or the mode isn't built yet
        /// (<see cref="TurnModes.IsBuilt"/>), so a typo never silently plays PartyWindow.</summary>
        public static bool TryParseBuilt(string name, out TurnMode mode, out string error)
        {
            mode = TurnMode.PartyWindow;
            error = null;
            if (string.IsNullOrWhiteSpace(name) || int.TryParse(name, out _) || !Enum.TryParse(name.Trim(), true, out TurnMode m) || !Enum.IsDefined(typeof(TurnMode), m))
            {
                error = $"unknown turn mode '{name}' (modes: {string.Join(", ", Enum.GetNames(typeof(TurnMode)))}; built: {BuiltNames()})";
                return false;
            }
            if (!TurnModes.IsBuilt(m))
            {
                error = $"turn mode {m} is not built yet (built: {BuiltNames()})";
                return false;
            }
            mode = m;
            return true;
        }

        private static string BuiltNames()
        {
            var names = new List<string>();
            foreach (var m in TurnModes.Available) names.Add(m.ToString());
            return string.Join(", ", names);
        }

        /// <summary>The mode a settings.json value names; missing or unknown reads as <see cref="TurnMode.PartyWindow"/>.</summary>
        public static TurnMode Parse(string name) =>
            !string.IsNullOrEmpty(name) && !int.TryParse(name, out _) && Enum.TryParse(name, false, out TurnMode m) && Enum.IsDefined(typeof(TurnMode), m)
                ? m : TurnMode.PartyWindow;
    }

    /// <summary>
    /// The difficulty dial (owner, GG-T309: "On a 1-10 scale you would start on 10 and let players adjust the game if
    /// they feel it is too easy or hard … the higher the difficulty the greater the rewards"). One number replaces the
    /// §22.9 scaffold's Easy/Normal/Hard/Custom preset and its six inert multipliers; what each level does is
    /// <see cref="DifficultyTable"/>. It is a profile setting (settings.json), so every save slot of the profile plays it.
    /// </summary>
    public sealed class DifficultySettings
    {
        public int Level = DifficultyTable.Default;

        /// <summary>The level an old §22.9 preset becomes. Its multipliers were pinned at 1.0 (inert), so every player
        /// actually fought the designed game — level 10 — except one who chose a lower setting on purpose: Easy → 5, and
        /// a Custom whose enemy HP/damage averaged below ×1 → the level whose enemy side is nearest that average.
        /// Normal, Hard, a Custom at ×1 or above, and anything unknown → 10.</summary>
        public static int FromLegacyPreset(string preset, float enemyHp = 1f, float enemyDamage = 1f)
        {
            switch ((preset ?? "").Trim().ToLowerInvariant())
            {
                case "easy": return 5;
                case "custom":
                    float want = (enemyHp + enemyDamage) / 2f;
                    if (!(want < 1f)) return DifficultyTable.Default;
                    int best = DifficultyTable.Min;
                    float bestGap = float.MaxValue;
                    foreach (var r in DifficultyTable.Rows)
                    {
                        float gap = Math.Abs((r.EnemyHp + r.EnemyDamage) / 2f - want);
                        if (gap < bestGap) { bestGap = gap; best = r.Level; }
                    }
                    return best;
                default: return DifficultyTable.Default;
            }
        }
    }
}
