#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace GridGame.App
{
    /// <summary>
    /// GG-T2121: the iPhone home indicator auto-hides in battle (a drag on the board's bottom row stays clear of it) and
    /// stays visible on every other screen. <c>PlayerSettings.iOS.hideHomeButton</c> is player-wide, so the build leaves it
    /// off and <c>Plugins/iOS/GGHomeIndicator.mm</c> overrides <c>prefersHomeIndicatorAutoHidden</c> on Unity's root view
    /// controller. Every other platform has no home indicator, and the calls do nothing.
    /// </summary>
    public static class HomeIndicator
    {
        public const string BattleScreenId = "battle";

        private static bool autoHidden = false;

        public static bool AutoHidden => autoHidden;

        /// <summary>The router's hook: hidden while the top screen is the battle, shown on anything else.</summary>
        public static void OnScreen(GameScreen screen) => SetAutoHidden(screen != null && screen.Id == BattleScreenId);

        public static void SetAutoHidden(bool hidden)
        {
            if (hidden == autoHidden) return;
            autoHidden = hidden;
#if UNITY_IOS && !UNITY_EDITOR
            try { GGHomeIndicator_SetAutoHidden(hidden ? 1 : 0); }
            catch (System.Exception) { }
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GGHomeIndicator_SetAutoHidden(int hidden);
#endif
    }
}
