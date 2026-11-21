using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core.Battle;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Unity.Shared.Core;
using Unity.Shared.Core.Json;

namespace GridGame.Meta
{
    /// <summary>
    /// Player profiles (bible §15.4, GG-T99) on top of the shared com.mindattic.unity.shared package: each profile is
    /// <c>Profiles/&lt;Name&gt;/settings.json</c> plus <c>Profiles/&lt;Name&gt;/Saves/slot1-3.json</c>. Settings belong to the
    /// profile, not to a save slot (this reverses the 2026-10-09 "settings are per save slot" amendment). The shared
    /// <c>common</c> block carries what every MindAttic game reads (volumes, mutes, reduce motion, language); everything
    /// Gaiden-only lives under <c>game</c>. <see cref="SettingsSave"/> stays the in-memory model the app reads.
    /// </summary>
    public static class GaidenSettings
    {
        /// <summary>Keys of the Gaiden-only <c>game</c> block of settings.json.</summary>
        public static class Keys
        {
            public const string BattleSpeed = "battleSpeed";
            public const string DragTimer = "dragTimer";
            public const string ColorblindMode = "colorblindMode";
            public const string PincerCutIns = "pincerCutIns";
            public const string ConfirmActions = "confirmActions";
            public const string MenuTheme = "menuTheme";
            /// <summary>The player's own window themes (GG-T407): an array of <see cref="WindowThemeDef"/> objects.</summary>
            public const string CustomThemes = "customThemes";
            /// <summary>Brightness/Contrast/Gamma per preset theme (GG-T563): {"&lt;name&gt;": {"brightness", "contrast", "gamma"}}.</summary>
            public const string ThemeTones = "themeTones";
            public const string FontName = "fontName";
            /// <summary>Which one-time look migration the file has had (GG-T385): missing/0 = from before the Chrono default.</summary>
            public const string LookVersion = "lookVersion";
            public const string Hyphenate = "hyphenate";
            public const string Difficulty = "difficulty";
            /// <summary>Settings → Text size (GG-T495): 100, 115 or 130 (percent).</summary>
            public const string TextScale = "textScale";
        }

        /// <summary>A text size the game offers (GG-T495), or 100 for anything else in the file.</summary>
        public static int ClampTextScale(int pct) => pct == 115 || pct == 130 ? pct : 100;

        /// <summary>The settings a profile's settings.json holds, with defaults for anything missing.</summary>
        public static SettingsSave Read(ProfileSettings ps)
        {
            var d = new SettingsSave();
            if (ps == null) return d;
            // Read the stored values, not CommonSettings' properties: those report 0/muted during a CLI session mute
            // (Profiles.SessionMuted), and writing that back would make a session-only mute permanent.
            var c = ps.Common.Data;
            d.MusicVolume = Clamp01(c.GetFloat(CommonSettings.Keys.MusicVolume, d.MusicVolume));
            d.SfxVolume = Clamp01(c.GetFloat(CommonSettings.Keys.SfxVolume, d.SfxVolume));
            d.MuteMusic = c.GetBool(CommonSettings.Keys.MuteMusic, d.MuteMusic);
            d.MuteSfx = c.GetBool(CommonSettings.Keys.MuteSfx, d.MuteSfx);
            d.ReduceMotion = c.GetBool(CommonSettings.Keys.ReduceMotion, d.ReduceMotion);

            var g = ps.Game;
            d.BattleSpeed = g.GetFloat(Keys.BattleSpeed, d.BattleSpeed);
            d.DragTimer = g.GetBool(Keys.DragTimer, d.DragTimer);
            d.ColorblindMode = g.GetBool(Keys.ColorblindMode, d.ColorblindMode);
            d.PincerCutIns = g.GetBool(Keys.PincerCutIns, d.PincerCutIns);
            d.ConfirmActions = g.GetBool(Keys.ConfirmActions, d.ConfirmActions);
            d.MenuTheme = g.GetString(Keys.MenuTheme, d.MenuTheme);
            d.FontName = g.GetString(Keys.FontName, d.FontName);
            // GG-T385: the default look became the Chrono gray window and the M PLUS Rounded face. A profile written before
            // that and still on the old defaults (Classic / Classic) moves to the new ones once; a later pick of Classic sticks.
            // Only a file from before GG-T385 (lookVersion missing/0): from version 1 on, Classic was a deliberate pick.
            if (g.ContainsKey(Keys.MenuTheme) && g.GetInt(Keys.LookVersion, 0) < 1)
            {
                if (d.MenuTheme == "Classic") d.MenuTheme = SettingsSave.DefaultMenuTheme;
                if (d.FontName == "Classic") d.FontName = SettingsSave.DefaultFontName;
            }
            // GG-T404: the Granite gray-blue became the default (owner 2026-11-17: the Chrono gray "looks more brown"); a
            // profile still on the Chrono default moves to it once; a later pick of Chrono sticks.
            if (g.ContainsKey(Keys.MenuTheme) && g.GetInt(Keys.LookVersion, 0) < 2 && d.MenuTheme == "Chrono")
                d.MenuTheme = SettingsSave.DefaultMenuTheme;
            // GG-T453: Classic (the Final Fantasy blue gradient) is the default again (owner 2026-11-20: "the gray backdrop is
            // bad"); a profile still on the Granite default moves to it once; a later pick of Granite sticks.
            if (g.ContainsKey(Keys.MenuTheme) && g.GetInt(Keys.LookVersion, 0) < 3 && d.MenuTheme == "Granite")
                d.MenuTheme = SettingsSave.DefaultMenuTheme;
            // GG-T566: Dark replaced Classic as the default after version 3 shipped. A version-3 file still on Classic moves
            // to Dark once — every version-3 write stamped 3 whatever the pick, so an untouched default can't be told from a
            // deliberate Classic and the owner chose to move them all. Versions 1-2 picked Classic deliberately (the default
            // was Chrono/Granite then) and keep it; from version 4 on, Classic is a deliberate pick again and sticks.
            if (g.ContainsKey(Keys.MenuTheme) && g.GetInt(Keys.LookVersion, 0) == 3 && d.MenuTheme == "Classic")
                d.MenuTheme = SettingsSave.DefaultMenuTheme;
            d.Hyphenate = g.GetBool(Keys.Hyphenate, d.Hyphenate);
            d.TextScale = ClampTextScale(g.GetInt(Keys.TextScale, d.TextScale));
            d.CustomThemes = ReadCustomThemes(g.GetArray(Keys.CustomThemes));
            d.ThemeTones = ReadThemeTones(g.GetObject(Keys.ThemeTones));
            // GG-T309: {"level": 1..10}. A file from before it still holds the §22.9 preset: map it once (it is
            // rewritten as a level on the next save); a missing block is a new profile, at the default 10.
            var diff = g.GetObject(Keys.Difficulty);
            if (diff != null)
                d.Difficulty.Level = diff.ContainsKey(DifficultyLevelKey)
                    ? DifficultyTable.Clamp(diff.GetInt(DifficultyLevelKey, DifficultyTable.Default))
                    : DifficultySettings.FromLegacyPreset(diff.GetString("preset", null), diff.GetFloat("enemyHp", 1f), diff.GetFloat("enemyDamage", 1f));
            return d;
        }

        /// <summary>Writes <paramref name="s"/> into a profile's settings (in memory; the caller saves the file).
        /// Unknown fields already in the file are kept.</summary>
        public static void Write(ProfileSettings ps, SettingsSave s)
        {
            if (ps == null || s == null) return;
            var c = ps.Common;
            c.MusicVolume = s.MusicVolume;
            c.SfxVolume = s.SfxVolume;
            c.MuteMusic = s.MuteMusic;
            c.MuteSfx = s.MuteSfx;
            c.ReduceMotion = s.ReduceMotion;

            var g = ps.Game;
            g.Set(Keys.BattleSpeed, s.BattleSpeed);
            g.Set(Keys.DragTimer, s.DragTimer);
            g.Set(Keys.ColorblindMode, s.ColorblindMode);
            g.Set(Keys.PincerCutIns, s.PincerCutIns);
            g.Set(Keys.ConfirmActions, s.ConfirmActions);
            g.Set(Keys.MenuTheme, s.MenuTheme ?? SettingsSave.DefaultMenuTheme);
            g.Set(Keys.FontName, s.FontName ?? SettingsSave.DefaultFontName);
            g.Set(Keys.LookVersion, CurrentLookVersion);
            g.Set(Keys.Hyphenate, s.Hyphenate);
            g.Set(Keys.TextScale, ClampTextScale(s.TextScale));
            var themes = new JsonArray();
            foreach (var t in s.CustomThemes ?? new List<WindowThemeDef>())
                if (t != null) themes.Add(JsonObject.Parse(t.ToJson()));
            g.Set(Keys.CustomThemes, themes);
            var tones = new JsonObject();
            foreach (var kv in s.ThemeTones ?? new Dictionary<string, WindowToneDef>())
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == null || kv.Value.Tone.IsNeutral) continue;
                var t = kv.Value.Tone;
                tones.Set(kv.Key, new JsonObject().Set(ToneBrightness, t.Brightness).Set(ToneContrast, t.Contrast).Set(ToneGamma, t.Gamma));
            }
            g.Set(Keys.ThemeTones, tones);
            var x = s.Difficulty ?? new DifficultySettings();
            var diff = g.GetOrAddObject(Keys.Difficulty);
            diff.Set(DifficultyLevelKey, DifficultyTable.Clamp(x.Level));
            foreach (var legacy in new[] { "preset", "enemyHp", "enemyDamage", "enemySpeed", "rewardGold", "rewardXp", "itemDropRate" })
                diff.Remove(legacy); // the §22.9 scaffold's keys, retired by the dial (GG-T309)
        }

        /// <summary>The custom window themes in a settings.json (GG-T407). One that no longer validates (a hand edit, a
        /// renamed field) is dropped rather than failing the whole file, and so is a second one with the same name.</summary>
        private static List<WindowThemeDef> ReadCustomThemes(JsonArray arr)
        {
            var list = new List<WindowThemeDef>();
            if (arr == null) return list;
            foreach (var o in arr)
            {
                if (!(o is JsonObject jo)) continue;
                WindowThemeDef t;
                try { t = WindowThemeDef.FromJson(jo.ToJson()); }
                catch (Exception) { continue; }
                if (WindowThemeCatalog.Validate(t).Count > 0 || list.Any(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(t);
                if (list.Count >= WindowThemeCatalog.MaxCustom) break;
            }
            return list;
        }

        private const string ToneBrightness = "brightness", ToneContrast = "contrast", ToneGamma = "gamma";

        /// <summary>The preset tone map in a settings.json (GG-T563); a missing value reads 0.5, a missing map is empty.</summary>
        private static Dictionary<string, WindowToneDef> ReadThemeTones(JsonObject o)
        {
            var map = new Dictionary<string, WindowToneDef>(StringComparer.OrdinalIgnoreCase);
            if (o == null) return map;
            foreach (var kv in o)
            {
                if (string.IsNullOrEmpty(kv.Key) || !(kv.Value is JsonObject t)) continue;
                var tone = new WindowTone(t.GetFloat(ToneBrightness, WindowTone.Neutral), t.GetFloat(ToneContrast, WindowTone.Neutral), t.GetFloat(ToneGamma, WindowTone.Neutral));
                if (!tone.IsNeutral) map[kv.Key] = WindowToneDef.From(tone);
            }
            return map;
        }

        /// <summary>The look migration this build writes (<see cref="Keys.LookVersion"/>, GG-T385).</summary>
        public const int CurrentLookVersion = 4; // GG-T566: 4 = Classic-at-3 moved to Dark

        /// <summary>The dial's key inside the <c>difficulty</c> object (GG-T309).</summary>
        public const string DifficultyLevelKey = "level";

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }

    /// <summary>The three save-slot files in one folder (a profile's <c>Saves/</c>): names and the JSON shape. Engine-free,
    /// so the dotnet tests and the app's <c>SaveStore</c> read and write the same files the same way.</summary>
    public static class SaveFiles
    {
        public const int Slots = 3;

        /// <summary>Saves tolerate unknown/stale properties (unlike strict content loading) so a renamed or removed field
        /// never stops <see cref="SaveMigrations.Migrate"/> from running — including the old per-slot "Settings" block.</summary>
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static string FileName(int slot) => $"slot{slot + 1}.json";

        public static string PathFor(string folder, int slot) => Path.Combine(folder, FileName(slot));

        public static string Serialize(SaveData s) => JsonConvert.SerializeObject(s, Formatting.Indented, Settings);

        public static SaveData Deserialize(string json) => JsonConvert.DeserializeObject<SaveData>(json, Settings);

        /// <summary>The slot most recently written in <paramref name="folder"/>, or -1 when it has none.</summary>
        public static int MostRecent(string folder)
        {
            int best = -1;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < Slots; i++)
            {
                var p = PathFor(folder, i);
                if (!File.Exists(p)) continue;
                var t = File.GetLastWriteTimeUtc(p);
                if (t > bestTime) { bestTime = t; best = i; }
            }
            return best;
        }
    }

    /// <summary>What <see cref="LegacySaveMigration.Run"/> did.</summary>
    public sealed class LegacyMigrationResult
    {
        /// <summary>False when there was nothing to migrate (no legacy folder, or it was already done).</summary>
        public bool Ran;
        /// <summary>Slot files copied into the Default profile.</summary>
        public readonly List<string> Copied = new List<string>();
        /// <summary>Slot files left where they were because the profile already had one by that name (still in Saves.migrated).</summary>
        public readonly List<string> Skipped = new List<string>();
        /// <summary>The slot (0-based) whose settings seeded the profile's settings.json, or -1.</summary>
        public int SeededFromSlot = -1;
        /// <summary>Where the legacy folder now is (persistentDataPath/Saves.migrated), or null.</summary>
        public string MovedTo;
    }

    /// <summary>
    /// The one-time move from the pre-profile layout (<c>&lt;data&gt;/Saves/slot1-3.json</c>, each slot carrying its own
    /// settings) to profiles: the slots are copied into the <see cref="ProfileStore.DefaultName"/> profile's <c>Saves/</c>,
    /// the most recently played slot's settings seed that profile's settings.json, and the old folder is kept, renamed
    /// <c>Saves.migrated</c>. Nothing is deleted. Runs only while <c>Saves</c> exists, so it never runs twice.
    /// </summary>
    public static class LegacySaveMigration
    {
        public const string LegacyFolderName = "Saves";
        public const string MigratedFolderName = "Saves.migrated";

        public static LegacyMigrationResult Run(string dataPath, ProfileStore store)
        {
            var result = new LegacyMigrationResult();
            if (string.IsNullOrEmpty(dataPath) || store == null) return result;
            string legacy = Path.Combine(dataPath, LegacyFolderName);
            if (!Directory.Exists(legacy)) return result;
            var slots = Enumerable.Range(0, SaveFiles.Slots).Where(i => File.Exists(SaveFiles.PathFor(legacy, i))).ToList();
            result.Ran = true;

            var profile = store.Get(ProfileStore.DefaultName);
            bool seedSettings = profile == null || profile.Settings.Status != SettingsLoadStatus.Loaded;
            profile ??= store.Create(ProfileStore.DefaultName);
            string target = profile.EnsureSavesFolder();
            foreach (int i in slots)
            {
                var from = SaveFiles.PathFor(legacy, i);
                var to = SaveFiles.PathFor(target, i);
                // A target left truncated by a crash mid-copy (an earlier run that never reached the folder move below) is
                // not a real save: replace it. Anything the same size as the source, or that parses, is kept.
                if (File.Exists(to) && !TruncatedCopy(from, to)) { result.Skipped.Add(SaveFiles.FileName(i)); continue; }
                // Copy to a temp file, then move it into place, so a crash never leaves a half-written slot under the real name.
                string tmp = to + ".migrating";
                File.Copy(from, tmp, true);
                if (File.Exists(to)) File.Delete(to);
                File.Move(tmp, to);
                File.SetLastWriteTimeUtc(to, File.GetLastWriteTimeUtc(from)); // keeps "Continue" pointing at the same slot
                result.Copied.Add(SaveFiles.FileName(i));
            }

            if (seedSettings)
            {
                int recent = MostRecentlyPlayed(legacy, slots);
                var settings = recent >= 0 ? ReadLegacySettings(SaveFiles.PathFor(legacy, recent)) : null;
                if (settings != null)
                {
                    GaidenSettings.Write(profile.Settings, settings);
                    result.SeededFromSlot = recent;
                }
                profile.SaveSettings();
            }

            string moved = Path.Combine(dataPath, MigratedFolderName);
            for (int n = 2; Directory.Exists(moved) || File.Exists(moved); n++) moved = Path.Combine(dataPath, MigratedFolderName + "-" + n);
            Directory.Move(legacy, moved);
            result.MovedTo = moved;
            if (store.List().Count == 1) store.Select(profile.Name);
            return result;
        }

        /// <summary>True when <paramref name="to"/> looks like an interrupted copy of <paramref name="from"/>: shorter than the
        /// source and not a JSON object that parses. Never throws (an unreadable target counts as truncated only if shorter).</summary>
        private static bool TruncatedCopy(string from, string to)
        {
            try
            {
                if (new FileInfo(to).Length >= new FileInfo(from).Length) return false;
                try { JObject.Parse(File.ReadAllText(to)); return false; }
                catch (Exception) { return true; }
            }
            catch (Exception) { return false; }
        }

        /// <summary>The slot whose save says it was played last (UpdatedUtc), falling back to the file's write time.</summary>
        private static int MostRecentlyPlayed(string folder, List<int> slots)
        {
            int best = -1;
            DateTime bestTime = DateTime.MinValue;
            foreach (int i in slots)
            {
                var p = SaveFiles.PathFor(folder, i);
                DateTime t = File.GetLastWriteTimeUtc(p);
                try
                {
                    var updated = JObject.Parse(File.ReadAllText(p))["UpdatedUtc"];
                    if (updated != null && updated.Type == JTokenType.Date) t = updated.Value<DateTime>().ToUniversalTime();
                }
                catch (Exception) { /* a damaged slot still counts by its write time; it is copied untouched either way */ }
                if (best < 0 || t > bestTime) { bestTime = t; best = i; }
            }
            return best;
        }

        /// <summary>The "Settings" block an old save slot carried, or null if it has none or can't be read.</summary>
        public static SettingsSave ReadLegacySettings(string slotPath)
        {
            try
            {
                var token = JObject.Parse(File.ReadAllText(slotPath))["Settings"];
                if (token == null || token.Type != JTokenType.Object) return null;
                var s = token.ToObject<SettingsSave>(JsonSerializer.Create(SaveFiles.Settings));
                if (s != null)
                {
                    // A slot's Settings block predates the dial (GG-T309): it can only hold the §22.9 preset.
                    s.Difficulty = new DifficultySettings();
                    if (token["Difficulty"] is JObject d && d["Level"] == null)
                        s.Difficulty.Level = DifficultySettings.FromLegacyPreset((string)d["Preset"],
                            d["EnemyHp"]?.Value<float>() ?? 1f, d["EnemyDamage"]?.Value<float>() ?? 1f);
                    else if (token["Difficulty"] is JObject d2) s.Difficulty.Level = DifficultyTable.Clamp(d2["Level"].Value<int>());
                }
                return s;
            }
            catch (Exception) { return null; }
        }
    }

    /// <summary>
    /// The profile being played and its settings — what the app's Title picker and Settings screen work through.
    /// Switching profiles reloads settings from the new profile's settings.json; create / rename / delete go through
    /// the shared <see cref="ProfileStore"/> (deleting moves the folder to <c>.trash</c>, never erases it).
    /// </summary>
    public sealed class ProfileSession
    {
        public ProfileStore Store { get; }
        public Profile Profile { get; private set; }
        /// <summary>The active profile's settings (in memory; <see cref="SaveSettings"/> writes them).</summary>
        public SettingsSave Settings { get; private set; }

        /// <summary>Opens <paramref name="requested"/> (made if it doesn't exist yet), or the store's current profile.</summary>
        public ProfileSession(ProfileStore store, string requested = null)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            if (!string.IsNullOrWhiteSpace(requested))
            {
                requested = requested.Trim();
                if (!ProfileStore.IsValidName(requested)) throw new ArgumentException("Not a valid profile name: \"" + requested + "\".", nameof(requested));
                if (!store.Exists(requested)) store.Create(requested);
                Use(store.Select(requested));
            }
            else Use(store.Current);
        }

        public string Name => Profile.Name;

        /// <summary>This profile's save-slot folder (made if missing).</summary>
        public string SavesFolder => Profile.EnsureSavesFolder();

        public IReadOnlyList<string> List() => Store.List();

        /// <summary>Makes <paramref name="name"/> the active profile and loads its settings.</summary>
        public void Switch(string name) => Use(Store.Select(name));

        /// <summary>Makes a new profile (it doesn't become active).</summary>
        public Profile Create(string name) => Store.Create(name?.Trim());

        public void Rename(string name, string newName) => Store.Rename(name, newName?.Trim());

        /// <summary>Moves a profile to the trash. Deleting the active one switches to whichever the store picks next
        /// (another profile, or a fresh Default when it was the last).</summary>
        public void Delete(string name)
        {
            bool active = string.Equals(name, Profile.Name, StringComparison.OrdinalIgnoreCase);
            Store.Delete(name);
            if (active) Use(Store.Current);
        }

        /// <summary>Writes <see cref="Settings"/> to the active profile's settings.json. False when the disk refuses.</summary>
        public bool SaveSettings()
        {
            GaidenSettings.Write(Profile.Settings, Settings);
            return Profile.TrySaveSettings();
        }

        private void Use(Profile p)
        {
            Profile = p;
            Settings = GaidenSettings.Read(p.ReloadSettings());
        }
    }
}
