using System;
using System.Globalization;
using System.Threading;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.Scripting;
#if GG_IAP
using UnityEngine.Purchasing;
#endif

namespace GridGame.App
{
    /// <summary>
    /// The storefront country for <see cref="LootBoxPolicy.Country"/> (BIBLE §25.10.4, GG-T1635). A <c>-storefront XX</c>
    /// launch argument sets it in every build and nothing replaces it. Otherwise only release builds on Android and iOS
    /// (where the real store sells) resolve one; editor, development, desktop and WebGL builds leave it unknown.
    /// Android asks Google Play Billing itself (its own BillingClient, so it works with the Diamond store off) and falls
    /// back to the device region; iOS starts from the device region and takes the App Store storefront once
    /// <see cref="UnityIapDiamondStore"/> connects.
    /// </summary>
    public static class StorefrontCountry
    {
        /// <summary>Where the current country came from ("override", "play", "appstore", "device"), null while unknown.</summary>
        public static string Source { get; private set; }

        private static SynchronizationContext main;

        /// <summary>The <c>-storefront XX</c> launch argument: the country for this run, whatever the store says.</summary>
        public static void Override(string code)
        {
            var alpha2 = Alpha2(code);
            if (alpha2 == null) { Debug.LogError($"[CommandLine] -storefront ignored: '{code}' is not an ISO 3166-1 country code"); return; }
            Source = "override";
            LootBoxPolicy.Country = alpha2;
            Debug.Log("[Storefront] country " + alpha2 + " (override)");
        }

        private static bool Resolves => Source != "override" && !Application.isEditor && !Debug.isDebugBuild
            && (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer);

        /// <summary>At boot, independent of <see cref="Features.DiamondStore"/>.</summary>
        public static void Resolve()
        {
            if (!Resolves) return;
            main = SynchronizationContext.Current;
#if GG_IAP
            if (Application.platform == RuntimePlatform.Android) { QueryPlay(); return; }
#endif
            Set(DeviceRegion(), "device");
        }

        private static void Set(string code, string source)
        {
            var alpha2 = Alpha2(code);
            if (alpha2 == null || Source == "override") return;
            // The device region never replaces a store's answer.
            if (source == "device" && Source != null && Source != "device") return;
            Source = source;
            LootBoxPolicy.Country = alpha2;
            Debug.Log($"[Storefront] country {alpha2} ({source})");
        }

        /// <summary>Called from a store or JNI callback thread: lands on the main thread.</summary>
        private static void Post(string code, string source)
        {
            if (main != null) main.Post(_ => Set(code, source), null);
            else Set(code, source);
        }

        /// <summary>An ISO 3166-1 alpha-2 code from an alpha-2 or alpha-3 one (the App Store's), upper-cased; null if neither.</summary>
        public static string Alpha2(string code)
        {
            code = code?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return null;
            if (code.Length == 2) return code == "IV" || code == "ZZ" ? null : code; // invariant / unknown region
            if (code.Length != 3) return null;
            try
            {
                foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                {
                    RegionInfo region;
                    try { region = new RegionInfo(culture.Name); }
                    catch (ArgumentException) { continue; }
                    if (string.Equals(region.ThreeLetterISORegionName, code, StringComparison.OrdinalIgnoreCase)) return region.TwoLetterISORegionName.ToUpperInvariant();
                }
            }
            catch (Exception e) { Debug.LogWarning("[Storefront] region table unavailable: " + e.Message); }
            return null;
        }

        /// <summary>The device's region: the system locale's country on Android, else <see cref="RegionInfo.CurrentRegion"/>.</summary>
        private static string DeviceRegion()
        {
            try
            {
                if (Application.platform == RuntimePlatform.Android)
                {
                    using var resources = new AndroidJavaClass("android.content.res.Resources");
                    using var system = resources.CallStatic<AndroidJavaObject>("getSystem");
                    using var config = system.Call<AndroidJavaObject>("getConfiguration");
                    using var locales = config.Call<AndroidJavaObject>("getLocales");
                    using var locale = locales.Call<AndroidJavaObject>("get", 0);
                    return locale.Call<string>("getCountry");
                }
                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Storefront] device region unavailable: " + e.Message);
                return null;
            }
        }

#if GG_IAP
        /// <summary>The store connected (<see cref="UnityIapDiamondStore"/>): on iOS, take the App Store storefront.</summary>
        public static void OnStoreConnected(StoreController store)
        {
            if (!Resolves || Application.platform != RuntimePlatform.IPhonePlayer) return;
            main ??= SynchronizationContext.Current;
            var apple = store?.AppleStoreExtendedService;
            if (apple == null) return;
            apple.FetchStorefront(s => Post(s?.CountryCode, "appstore"),
                error => Debug.LogWarning("[Storefront] App Store storefront unavailable: " + error));
        }

        private const string Billing = "com.android.billingclient.api.";
        private static AndroidJavaObject client;

        /// <summary>Google Play's billing country (<c>BillingConfig.getCountryCode</c>), through a short-lived BillingClient of
        /// our own: Unity IAP keeps its own country internal. The device region stands in when Play can't answer.</summary>
        private static void QueryPlay()
        {
            if (client != null) return;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var builder = new AndroidJavaClass(Billing + "BillingClient").CallStatic<AndroidJavaObject>("newBuilder", activity);
                builder.Call<AndroidJavaObject>("setListener", new PurchasesListener()).Dispose();
                using var pendingBuilder = new AndroidJavaClass(Billing + "PendingPurchasesParams").CallStatic<AndroidJavaObject>("newBuilder");
                pendingBuilder.Call<AndroidJavaObject>("enableOneTimeProducts").Dispose();
                using var pending = pendingBuilder.Call<AndroidJavaObject>("build");
                builder.Call<AndroidJavaObject>("enablePendingPurchases", pending).Dispose();
                client = builder.Call<AndroidJavaObject>("build");
                client.Call("startConnection", new StateListener());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Storefront] Play billing unavailable: " + e.Message);
                EndPlay();
                Set(DeviceRegion(), "device");
            }
        }

        private static void OnPlayConnected()
        {
            try
            {
                using var paramsBuilder = new AndroidJavaClass(Billing + "GetBillingConfigParams").CallStatic<AndroidJavaObject>("newBuilder");
                using var configParams = paramsBuilder.Call<AndroidJavaObject>("build");
                client.Call("getBillingConfigAsync", configParams, new ConfigListener());
            }
            catch (Exception e) { OnPlayAnswer(null, "billing config failed: " + e.Message); }
        }

        /// <summary>Main thread: Play's country, or the device region when it had none. Only the first answer counts.</summary>
        private static void OnPlayAnswer(string country, string problem)
        {
            if (client == null) return;
            EndPlay();
            if (problem != null) Debug.LogWarning("[Storefront] Play billing country unavailable: " + problem);
            if (Alpha2(country) != null) Set(country, "play");
            else Set(DeviceRegion(), "device");
        }

        private static void EndPlay()
        {
            if (client == null) return;
            try { client.Call("endConnection"); }
            catch (Exception e) { Debug.LogWarning("[Storefront] endConnection: " + e.Message); }
            client.Dispose();
            client = null;
        }

        private static int Code(AndroidJavaObject billingResult) => billingResult?.Call<int>("getResponseCode") ?? -1;

        private static void PostPlay(string country, string problem)
        {
            if (main != null) main.Post(_ => OnPlayAnswer(country, problem), null);
            else OnPlayAnswer(country, problem);
        }

        private sealed class PurchasesListener : AndroidJavaProxy
        {
            public PurchasesListener() : base(Billing + "PurchasesUpdatedListener") { }

            [Preserve]
            public void onPurchasesUpdated(AndroidJavaObject billingResult, AndroidJavaObject purchases) { }
        }

        private sealed class StateListener : AndroidJavaProxy
        {
            public StateListener() : base(Billing + "BillingClientStateListener") { }

            [Preserve]
            public void onBillingSetupFinished(AndroidJavaObject billingResult)
            {
                int code = Code(billingResult);
                billingResult?.Dispose();
                if (code != 0) { PostPlay(null, "setup response " + code); return; }
                if (main != null) main.Post(_ => { if (client != null) OnPlayConnected(); }, null);
                else OnPlayConnected();
            }

            [Preserve]
            public void onBillingServiceDisconnected() => PostPlay(null, "service disconnected");
        }

        private sealed class ConfigListener : AndroidJavaProxy
        {
            public ConfigListener() : base(Billing + "BillingConfigResponseListener") { }

            [Preserve]
            public void onBillingConfigResponse(AndroidJavaObject billingResult, AndroidJavaObject billingConfig)
            {
                int code = Code(billingResult);
                string country = null;
                try { if (code == 0 && billingConfig != null) country = billingConfig.Call<string>("getCountryCode"); }
                catch (Exception e) { Debug.LogWarning("[Storefront] getCountryCode: " + e.Message); }
                billingResult?.Dispose();
                billingConfig?.Dispose();
                PostPlay(country, country == null ? "config response " + code : null);
            }
        }
#endif
    }
}
