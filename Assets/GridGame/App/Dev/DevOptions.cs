using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App.Dev
{
    /// <summary>
    /// GG-T1938: the in-app Developer gate, separate from <see cref="CommandLine.DebugOptionsAllowed"/> (launch flags) so the
    /// owner can turn cheats on in a release build on a phone. Tapping the version line seven times (Settings or About,
    /// Android-style) unlocks Settings &gt; Developer; its Cheats switch then adds the pause menu's cheat rows and a DEV
    /// badge in the corner. Both live in this device's PlayerPrefs, never in a save, and both start off. Owner-walkthrough
    /// tools such as the feedback Note button (GG-T1939) show while <see cref="Enabled"/>.
    /// </summary>
    public static class DevOptions
    {
        private const string UnlockedKey = "dev.options";
        private const string CheatsKey = "gridgame.dev.cheats";
        public const int TapsToUnlock = 7;
        /// <summary>A pause longer than this between taps starts the count again.</summary>
        private const float TapGapSeconds = 2f;
        /// <summary>The pause menu's battle speeds with Cheats on (Settings' own dial stops at ×3).</summary>
        public static readonly float[] CheatSpeeds = { 1f, 2f, 4f, 8f };

        private static int taps;
        private static float lastTap = -10f;
        private static VisualElement stage, badge;

        public static bool Unlocked => PlayerPrefs.GetInt(UnlockedKey, 0) == 1;
        /// <summary>Developer options are on (unlocked); GG-T1939's name for <see cref="Unlocked"/>.</summary>
        public static bool Enabled => Unlocked;
        public static bool Cheats => Unlocked && PlayerPrefs.GetInt(CheatsKey, 0) == 1;
        /// <summary>The pause menu's battle speed for this run (0 = follow Settings); never saved.</summary>
        public static float CheatSpeed;

        /// <summary>The battle speed <see cref="GameApp.ApplySettings"/> uses: the cheat speed while Cheats are on.</summary>
        public static float BattleSpeed(float setting) => Cheats && CheatSpeed > 0f ? CheatSpeed : Mathf.Clamp(setting, 0.5f, 3f);

        /// <summary>One tap on a version line; the seventh in a row unlocks the Developer section.</summary>
        public static void TapVersion()
        {
            float now = Time.unscaledTime;
            if (now - lastTap > TapGapSeconds) taps = 0;
            lastTap = now;
            if (++taps < TapsToUnlock) return;
            taps = 0;
            if (Unlocked) { GameApp.I.Toast("Developer options are already on"); return; }
            PlayerPrefs.SetInt(UnlockedKey, 1);
            PlayerPrefs.Save();
            GameApp.I.Toast("Developer options on");
            if (GameApp.I.Router.Current is SettingsScreen settings) settings.Refresh();
        }

        public static void SetCheats(bool on)
        {
            PlayerPrefs.SetInt(CheatsKey, on ? 1 : 0);
            PlayerPrefs.Save();
            if (!on) CheatSpeed = 0f;
            SyncBadge();
        }

        /// <summary>Makes the version line <paramref name="label"/> count taps toward the unlock.</summary>
        public static void CountTaps(VisualElement label)
        {
            label.pickingMode = PickingMode.Position;
            label.RegisterCallback<ClickEvent>(_ => TapVersion());
        }

        /// <summary>Shows the DEV badge on <paramref name="root"/> (the stage, above every screen and overlay) while Cheats
        /// are on, so screenshots show it; hides it when they go off.</summary>
        public static void SyncBadge(VisualElement root = null)
        {
            if (root != null) stage = root;
            if (stage == null) return;
            if (Cheats && badge == null)
            {
                badge = Ui.Text("DEV", "tiny gold", stage);
                badge.name = "dev-badge";
                badge.pickingMode = PickingMode.Ignore;
                badge.style.position = Position.Absolute;
                badge.style.right = 24; badge.style.top = 24;
                badge.style.paddingLeft = 12; badge.style.paddingRight = 12;
                badge.style.backgroundColor = new Color(0.55f, 0.08f, 0.08f, 0.85f);
                badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = 8;
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 8;
            }
            else if (!Cheats && badge != null) { badge.RemoveFromHierarchy(); badge = null; }
            badge?.BringToFront();
        }
    }
}
