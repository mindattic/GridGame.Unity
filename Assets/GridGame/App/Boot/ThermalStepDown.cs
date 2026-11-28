using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using UnityEngine.AdaptivePerformance;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace GridGame.App
{
    /// <summary>
    /// GG-T1548: while a phone reports thermal throttling, the frame cap drops to <see cref="HotFrameCap"/> and the
    /// Effects amount to <see cref="HotEffectsScale"/> of the player's own; once it has read cool for
    /// <see cref="CoolSeconds"/> in a row, the player's Settings choices come back. Never above those choices.
    ///
    /// <para><b>Signal.</b> Adaptive Performance (the built-in module) when a provider is active:
    /// <c>ThrottlingImminent</c> or <c>Throttling</c> is hot, <c>NoWarning</c> is cool. Otherwise, on iOS,
    /// <c>NSProcessInfo.thermalState</c> through <c>Plugins/iOS/GGThermal.mm</c>: serious or critical is hot, nominal or
    /// fair is cool. Desktop and WebGL players compile the watcher out and apply the settings as they are.</para>
    ///
    /// <para><b>Frame rate (GG-T2102).</b> This owns <see cref="Application.targetFrameRate"/>: the player's Settings >
    /// Display > Frame rate, 60 or <see cref="HighFrameRate"/>, the high rate only where
    /// <see cref="HighRefreshSupported"/>; the hot cap and <see cref="IdleFrameRate"/> still win over it.</para>
    /// </summary>
    public sealed class ThermalStepDown : MonoBehaviour
    {
        public static int HotFrameCap = 30;
        public static float HotEffectsScale = 0.5f;
        public static float CoolSeconds = 30f;
        public static float PollSeconds = 2f;
        public const int HighFrameRate = 120;

        private static int playerCap = 60;
        private static float playerEffects = 1f;
        private static bool hot = false;

        public static bool Hot => hot;

        /// <summary>The frame rate the player chose in Settings, after <see cref="HighRefreshSupported"/>; the hot cap may hold the game below it.</summary>
        public static int PlayerCap => playerCap;

        /// <summary>True when the display can draw <see cref="HighFrameRate"/> frames a second (ProMotion iPhones, 120 Hz Galaxies).</summary>
        public static bool HighRefreshSupported
        {
            get
            {
                if (Screen.currentResolution.refreshRateRatio.value >= HighFrameRate - 1) return true;
                foreach (var r in Screen.resolutions)
                    if (r.refreshRateRatio.value >= HighFrameRate - 1) return true;
                return false;
            }
        }

        /// <summary>Called by <see cref="GameApp.ApplySettings"/> with the player's frame rate and Effects amount.</summary>
        public static void Apply(int frameCap, float effectsAmount)
        {
            playerCap = frameCap > 60 && !HighRefreshSupported ? 60 : frameCap;
            playerEffects = Mathf.Clamp01(effectsAmount);
#if UNITY_ANDROID || UNITY_IOS
            if (watcher == null && GameApp.I != null) watcher = GameApp.I.gameObject.AddComponent<ThermalStepDown>();
#endif
            Push();
        }

        private static void Push()
        {
            int cap = playerCap;
            float fx = playerEffects;
            if (hot)
            {
                cap = cap <= 0 ? HotFrameCap : Mathf.Min(cap, HotFrameCap);
                fx *= Mathf.Clamp01(HotEffectsScale);
            }
            Application.targetFrameRate = cap;
            VfxPrefabLayer.Amount = fx;
        }

#if UNITY_ANDROID || UNITY_IOS
        private static ThermalStepDown watcher;
        private float nextPoll;
        private float coolSince = -1f;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int GGThermal_State();
#endif

        /// <summary>True hot, false cool, null when the device gives no reading.</summary>
        private static bool? ReadHot()
        {
            var ap = Holder.Instance;
            if (ap != null && ap.Active && ap.ThermalStatus != null)
                return ap.ThermalStatus.ThermalMetrics.WarningLevel != WarningLevel.NoWarning;
#if UNITY_IOS && !UNITY_EDITOR
            try { return GGThermal_State() >= 2; } // NSProcessInfoThermalState: 0 nominal, 1 fair, 2 serious, 3 critical
            catch (System.Exception) { return null; }
#else
            return null;
#endif
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < nextPoll) return;
            nextPoll = now + PollSeconds;
            bool? reading = ReadHot();
            if (reading == null) return;
            if (reading.Value)
            {
                coolSince = -1f;
                if (!hot) Set(true);
            }
            else if (hot)
            {
                if (coolSince < 0f) coolSince = now;
                else if (now - coolSince >= CoolSeconds) Set(false);
            }
        }

        private static void Set(bool value)
        {
            hot = value;
            Push();
            if (Debug.isDebugBuild)
                Debug.Log(value
                    ? $"[Thermal] throttling: frame cap {Application.targetFrameRate}, effects {VfxPrefabLayer.Amount:0.##}"
                    : $"[Thermal] cool: frame cap {playerCap}, effects {playerEffects:0.##} restored");
        }

        private void OnDestroy()
        {
            if (watcher == this) watcher = null;
        }
#endif
    }
}
