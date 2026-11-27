using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.World;

namespace GridGame.Core.Content
{
    /// <summary>The battle library's files (GG-T1709, BIBLE §28.9). Resources can't list a folder through the loader's
    /// text source, so battles/index.json names each battles/&lt;region&gt;/&lt;name&gt; file, and each file is a list of
    /// <see cref="LibraryBattle"/>s whose Stage is a full <see cref="StageDef"/>.</summary>
    public static class BattleLibrary
    {
        public const string Folder = "battles";
        public const string IndexFile = Folder + "/index";
        public const int WaveCount = 5;
        public const int MinTier = 1, MaxTier = 10;
        public const int MaxLevel = 99;

        public static void Load(ContentDb db, Func<string, string> readText)
        {
            foreach (var file in ContentLoader.ReadList<string>(readText, IndexFile))
            {
                if (string.IsNullOrWhiteSpace(file)) { db.LoadProblems.Add($"{IndexFile}.json: an empty entry"); continue; }
                var name = $"{Folder}/{file}";
                var text = readText(name);
                if (string.IsNullOrWhiteSpace(text)) { db.LoadProblems.Add($"{IndexFile}.json: '{file}' is missing or empty"); continue; }
                int slash = file.IndexOf('/');
                var folder = slash > 0 ? file.Substring(0, slash) : null;
                foreach (var b in ContentLoader.ReadList<LibraryBattle>(_ => text, name))
                {
                    if (b == null) { db.LoadProblems.Add($"{name}.json: a null battle"); continue; }
                    if (b.Stage != null)
                    {
                        b.Stage.Id ??= b.Id;
                        b.Stage.Theme ??= b.Region;
                        ContentLoader.ResolveGroups(db, b.Stage, $"battle {b.Id}");
                    }
                    if (!Same(folder, b.Region)) db.LoadProblems.Add($"battle {b.Id}: Region '{b.Region}' but its file is in {Folder}/{folder}/");
                    db.Battles.Add(b);
                }
            }
        }

        public static void Validate(ContentDb db, List<string> p)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in db.Battles)
            {
                string w = $"battle {b.Id}";
                if (string.IsNullOrWhiteSpace(b.Id)) p.Add("battle: needs an Id");
                else if (!ids.Add(b.Id)) p.Add($"{w}: id used twice in the library");
                if (!db.Campaign.Themes.Exists(t => Same(t?.Id, b.Region))) p.Add($"{w}: Region '{b.Region}' is not a theme");
                if (!Enum.IsDefined(typeof(BattleKind), b.Kind) || b.Kind == BattleKind.Story) p.Add($"{w}: Kind '{b.Kind}' is not a library kind (story battles stay authored stages)");
                if (b.Tier < MinTier || b.Tier > MaxTier) p.Add($"{w}: Tier {b.Tier} outside {MinTier}..{MaxTier}");
                if (b.Stage == null) { p.Add($"{w}: needs a Stage"); continue; }
                ValidateStage(db, b.Stage, w, p);
            }
        }

        static void ValidateStage(ContentDb db, StageDef s, string w, List<string> p)
        {
            if (s.Waves.Count != WaveCount) p.Add($"{w}: needs {WaveCount} waves, has {s.Waves.Count}");
            var blocks = new HashSet<(int, int)>();
            foreach (var c in s.Blocks)
            {
                if (!OnBoard(c.X, c.Y)) p.Add($"{w}: block ({c.X},{c.Y}) is off the board");
                blocks.Add((c.X, c.Y));
            }
            foreach (var id in s.Rules) if (db.TryRegionRule(id) == null) p.Add($"{w}: unknown region rule '{id}'");
            for (int i = 0; i < s.Waves.Count; i++)
            {
                var wave = s.Waves[i];
                string ww = $"{w} wave {i + 1}";
                if (wave == null) { p.Add($"{ww}: empty"); continue; }
                if (wave.Enemies.Count == 0) p.Add($"{ww}: has no foes");
                foreach (var e in wave.Enemies) Spawn(db, e, blocks, ww, p);
                foreach (var g in wave.Guests) Spawn(db, g, blocks, $"{ww} guest", p);
                foreach (var r in wave.Recruitables) RecruitableDef.Validate(db, r, $"{ww} recruitable", p);
                if (wave.SurpriseSpeaker != null && db.TryClass(wave.SurpriseSpeaker) == null) p.Add($"{ww}: unknown surprise speaker '{wave.SurpriseSpeaker}'");
                RuleBreaks.Validate(db, s, wave, p);
            }
        }

        static void Spawn(ContentDb db, SpawnDef e, HashSet<(int, int)> blocks, string w, List<string> p)
        {
            if (e == null) { p.Add($"{w}: a null spawn"); return; }
            if (db.TryClass(e.Class) == null) p.Add($"{w}: unknown class '{e.Class}'");
            if (e.Level < 1 || e.Level > MaxLevel) p.Add($"{w}: '{e.Class}' Level {e.Level} outside 1..{MaxLevel}");
            if (e.DelayTicks < 0) p.Add($"{w}: '{e.Class}' DelayTicks can't be negative");
            bool random = e.X == -1 && e.Y == -1;
            if (!random && !OnBoard(e.X, e.Y)) p.Add($"{w}: '{e.Class}' at ({e.X},{e.Y}) is off the board (0-{BattleConfig.Cols - 1}, 0-{BattleConfig.Rows - 1}; -1,-1 = random)");
            else if (!random && blocks.Contains((e.X, e.Y))) p.Add($"{w}: '{e.Class}' at ({e.X},{e.Y}) stands on a block");
        }

        static bool OnBoard(int x, int y) => x >= 0 && x < BattleConfig.Cols && y >= 0 && y < BattleConfig.Rows;

        static bool Same(string a, string b) => a != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
