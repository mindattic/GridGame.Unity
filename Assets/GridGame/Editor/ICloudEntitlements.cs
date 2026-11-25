#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace GridGame.EditorTools
{
    /// <summary>
    /// Cloud save on iOS (§15.2, GG-T609): turns on the iCloud capability with key-value storage in the exported Xcode
    /// project, so <c>ICloudCloudBackend</c> can reach NSUbiquitousKeyValueStore. Only the key-value store is used (no
    /// iCloud Documents, no CloudKit containers). Signing still needs the App ID to have iCloud enabled in the Apple
    /// developer account (Xcode's automatic signing does that when the team's account is signed into Xcode) —
    /// docs/RELEASE_BUILD.md "Cloud save: iCloud".
    /// </summary>
    public static class ICloudEntitlements
    {
        /// <summary>Relative to the Xcode project's root, next to the main target's sources.</summary>
        private const string EntitlementsFile = "Unity-iPhone/GridGame.entitlements";

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;
            var projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            var mainTarget = proj.GetUnityMainTargetGuid();
            // Reuse an entitlements file the project already names (another plugin's), rather than replacing it.
            var existing = proj.GetBuildPropertyForAnyConfig(mainTarget, "CODE_SIGN_ENTITLEMENTS");
            var file = string.IsNullOrEmpty(existing) ? EntitlementsFile : existing;
            var caps = new ProjectCapabilityManager(projPath, file, null, mainTarget);
            caps.AddiCloud(true, false, false, false, null);
            caps.WriteToFile();
            UnityEngine.Debug.Log($"[Cloud] iCloud key-value storage entitlement added ({Path.GetFileName(file)})");
        }
    }
}
#endif
