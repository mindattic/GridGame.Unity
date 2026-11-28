using UnityEngine;

namespace GridGame.App.Dev
{
    /// <summary>GG-T1938/GG-T1939: the one Developer-options gate (a PlayerPrefs flag, off by default). Owner-walkthrough
    /// tools such as the feedback Note button show only while it is on.</summary>
    public static class DevOptions
    {
        private const string Key = "dev.options";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 0) == 1;
            set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
