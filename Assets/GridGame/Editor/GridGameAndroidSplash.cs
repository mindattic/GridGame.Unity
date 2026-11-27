using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace GridGame.EditorTools
{
    /// <summary>
    /// GG-T1519: the Android 12+ system splash (shown on every cold start; Unity's own splash is off) gets the boot
    /// background and the adaptive icon foreground. Runs on every Android build, after Unity writes the Gradle project.
    /// </summary>
    public class GridGameAndroidSplash : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path) => GridGameBuild.ApplyAndroidSplash(path);
    }

    public static partial class GridGameBuild
    {
        public const string SplashIconSource = AppIconFolder + "app-icon-adaptive-fore-432-00.png";
        public const string SplashIconName = "gg_splash_icon";

        /// <summary>
        /// Rewrites the generated unityLibrary module (<paramref name="libraryPath"/>): copies the 432 px adaptive
        /// foreground to res/drawable-nodpi, points every windowSplashScreenAnimatedIcon in values-v31/styles.xml at it,
        /// and writes PlayerSettings.SplashScreen.backgroundColor (the in-game boot colour) into
        /// staticSplashScreenBackgroundColor. Anything missing only warns.
        /// </summary>
        public static void ApplyAndroidSplash(string libraryPath)
        {
            string res = Path.Combine(libraryPath, "src", "main", "res");
            if (!File.Exists(SplashIconSource))
            {
                Debug.LogWarning("[Build] android splash: missing " + SplashIconSource);
                return;
            }
            string drawable = Path.Combine(res, "drawable-nodpi");
            Directory.CreateDirectory(drawable);
            File.Copy(SplashIconSource, Path.Combine(drawable, SplashIconName + ".png"), true);

            string styles = Path.Combine(res, "values-v31", "styles.xml");
            if (!RewriteXml(styles, "(<item name=\"android:windowSplashScreenAnimatedIcon\">)[^<]*(</item>)",
                    "${1}@drawable/" + SplashIconName + "${2}"))
                Debug.LogWarning("[Build] android splash: no windowSplashScreenAnimatedIcon in " + styles);

            string colors = Path.Combine(res, "values", "colors.xml");
            string hex = "#FF" + ColorUtility.ToHtmlStringRGB(PlayerSettings.SplashScreen.backgroundColor);
            if (!RewriteXml(colors, "(<color name=\"staticSplashScreenBackgroundColor\">)[^<]*(</color>)", "${1}" + hex + "${2}"))
                Debug.LogWarning("[Build] android splash: no staticSplashScreenBackgroundColor in " + colors);

            Debug.Log("[Build] android splash: icon " + SplashIconName + ", background " + hex + " (GG-T1519)");
        }

        private static bool RewriteXml(string file, string pattern, string replacement)
        {
            if (!File.Exists(file)) return false;
            string xml = File.ReadAllText(file);
            if (!Regex.IsMatch(xml, pattern)) return false;
            File.WriteAllText(file, Regex.Replace(xml, pattern, replacement));
            return true;
        }
    }
}
