using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1330: the studio's legal links, in one place for the About screen (and the store listing to match). The URLs
    /// are placeholders until the owner names the real pages and contact (GG-T1558).
    /// </summary>
    public static class Legal
    {
        public const string Studio = "MindAttic LLC";
        public const string PrivacyPolicyUrl = "https://mindattic.com/privacy";
        public const string TermsUrl = "https://mindattic.com/terms";

        /// <summary>Resources path of the bundled third-party notices.</summary>
        public const string ThirdPartyNoticesResource = "Legal/ThirdPartyNotices";

        public static string ThirdPartyNotices() =>
            Resources.Load<TextAsset>(ThirdPartyNoticesResource)?.text ?? "Built with Unity.";

        /// <summary>The version the player reports (version.txt via PlayerSettings) and this build's id: the first 8
        /// characters of <see cref="Application.buildGUID"/>, "editor" when run in the editor.</summary>
        public static string VersionLine()
        {
            var guid = Application.buildGUID;
            string build = string.IsNullOrEmpty(guid) ? "editor" : guid.Substring(0, Mathf.Min(8, guid.Length));
            return $"{Application.version} (build {build}{(Debug.isDebugBuild ? ", development" : "")})";
        }

        /// <summary>Opens <paramref name="url"/> in the browser; a test or demo run toasts it instead of leaving the game.</summary>
        public static void Open(string url)
        {
            if (TestMode.Active || DemoMode.Active) { GameApp.I.Toast(url); return; }
            Application.OpenURL(url);
        }
    }
}
