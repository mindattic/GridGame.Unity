using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GridGame.App.Dev
{
    /// <summary>
    /// GG-T1801: a "show me X" spec (tools/scenarios/*.json, the -scenario boot flag, the automation <c>scenario</c>
    /// endpoint). Every field is optional; <see cref="ScenarioBuilder"/> fills each gap from the target.
    /// The loose fields take "auto" (or are left out) or a value: <c>level</c> a number, <c>progress</c> a count of
    /// stages cleared, "none" or a stage id, <c>gear</c> "auto"|"none", <c>caravan</c> "auto"|"none"|"max"|a level,
    /// <c>gold</c> a number.
    /// </summary>
    public sealed class ScenarioDef
    {
        /// <summary>"first-boss", "boss:N" (1-based), "boss:&lt;classId&gt;", "final-boss", "stage:&lt;id&gt;",
        /// "region:&lt;theme&gt;", "screen:&lt;StartScreens id&gt;" or "overworld" (the default).</summary>
        public string Target;
        public string Name;
        /// <summary>Class ids for the party; the default is the starting trio plus the story recruits due by then.</summary>
        public List<string> Party;
        public JToken Level;
        public JToken Progress;
        public JToken Gear;
        public JToken Caravan;
        public JToken Gold;
        public int? Food;
        public bool? OnTheRoad;
        public int? CarriedOrbs;
        /// <summary>Every Camp NPC joined, so every station is open.</summary>
        public bool AllFeatures;
        /// <summary>Profile settings by automation <c>setting</c> key (cutins, reducemotion, colorblind, ...): 0/1 or a value.</summary>
        public Dictionary<string, JToken> Settings;
        /// <summary>Battle targets: play every wave from the target wave on, not just the target wave.</summary>
        public bool FullStage;
        /// <summary>Battle targets: launch the real stage through StageLauncher (crawl, beats, every wave).</summary>
        public bool RealLaunch;

        public static ScenarioDef Parse(string json) =>
            JsonConvert.DeserializeObject<ScenarioDef>(json) ?? new ScenarioDef();

        /// <summary>A bare target ("first-boss", "screen:caravan") as a spec.</summary>
        public static ScenarioDef ForTarget(string target) => new ScenarioDef { Target = target };

        internal static bool IsAuto(JToken t) =>
            t == null || t.Type == JTokenType.Null || t.Type == JTokenType.String && string.Equals((string)t, "auto", System.StringComparison.OrdinalIgnoreCase);

        internal static bool IsWord(JToken t, string word) =>
            t != null && t.Type == JTokenType.String && string.Equals((string)t, word, System.StringComparison.OrdinalIgnoreCase);

        internal static int? Int(JToken t) =>
            t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) ? (int?)t.Value<int>()
            : t != null && t.Type == JTokenType.String && int.TryParse((string)t, out var n) ? n : (int?)null;
    }
}
