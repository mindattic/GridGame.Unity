using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1547: a menu or hub screen where nothing has moved for <see cref="IdleSeconds"/> draws only every Nth frame
    /// (<see cref="OnDemandRendering.renderFrameInterval"/>), about <see cref="IdleFps"/> a second; the next input or
    /// animation draws every frame again. The game still updates every frame, so input is read at the full rate and
    /// the first frame after a touch is drawn. The battle never idles: every effect plays out over time (Law 5).
    ///
    /// <para><b>Movement.</b> Any pointer, wheel, key or navigation event on the panel, a slider or scroller value
    /// changing (a ScrollView coasting), a USS transition, a screen change, a held touch, and every animator that calls
    /// <see cref="Stir"/> while it moves: <see cref="Tween.Run"/> and a living backdrop. A player run under
    /// <c>-automation</c> never idles.</para>
    ///
    /// <para><b>Frame cap.</b> <see cref="ThermalStepDown"/> owns <see cref="Application.targetFrameRate"/>; this only
    /// picks the interval from whatever cap is in force, so a throttled 30 cap idles at 15 and an idle frame is never
    /// above the player's Settings.</para>
    /// </summary>
    public sealed class IdleFrameRate : MonoBehaviour
    {
        public static int IdleFps = 15;
        public static float IdleSeconds = 2f;
        /// <summary>Off while automation drives the player: its taps and screenshots expect every frame drawn.</summary>
        public static bool Enabled = string.IsNullOrEmpty(CommandLine.Value("-automation"));

        private static float lastStir;
        private static IdleFrameRate instance;

        public static bool Idle => OnDemandRendering.renderFrameInterval > 1;

        /// <summary>Something moved this frame: draw every frame for at least <see cref="IdleSeconds"/> more.</summary>
        public static void Stir() => lastStir = Time.unscaledTime;

        public static void Install(GameApp app)
        {
            if (instance != null || app == null) return;
            instance = app.gameObject.AddComponent<IdleFrameRate>();
            if (app.Router != null) app.Router.Changed += _ => Stir();
            var root = app.Document != null ? app.Document.rootVisualElement : null;
            if (root == null) return;
            root.RegisterCallback<PointerDownEvent>(_ => Stir(), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(_ => Stir(), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(_ => Stir(), TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(_ => Stir(), TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(_ => Stir(), TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(_ => Stir(), TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<float>>(_ => Stir(), TrickleDown.TrickleDown);
            root.RegisterCallback<TransitionRunEvent>(_ => Stir(), TrickleDown.TrickleDown);
            root.RegisterCallback<TransitionEndEvent>(_ => Stir(), TrickleDown.TrickleDown);
        }

        private void Update()
        {
            var app = GameApp.I;
            if (!Enabled || app == null || app.Router == null || app.Router.Preparing || app.Router.Current is BattleScreen || InputHeld())
                Stir();
            float fps = QualitySettings.vSyncCount > 0 || Application.targetFrameRate <= 0
                ? (float)Screen.currentResolution.refreshRateRatio.value / Mathf.Max(1, QualitySettings.vSyncCount)
                : Application.targetFrameRate;
            int interval = Time.unscaledTime - lastStir < IdleSeconds ? 1 : Mathf.Max(1, Mathf.RoundToInt(fps / Mathf.Max(1, IdleFps)));
            if (OnDemandRendering.renderFrameInterval != interval) OnDemandRendering.renderFrameInterval = interval;
        }

        private static bool InputHeld()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var kb = Keyboard.current;
            return kb != null && kb.anyKey.wasPressedThisFrame;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            OnDemandRendering.renderFrameInterval = 1;
        }
    }
}
