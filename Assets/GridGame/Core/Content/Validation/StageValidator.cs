using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The stage checks: scenes, waves, theme membership, the tutorial drills and their lessons. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class StageValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            void NeedClass(string id, string where) { if (id != null && !db.Classes.ContainsKey(id)) p.Add($"{where}: unknown class '{id}'"); }
            foreach (var s in db.Stages.Values)
                foreach (var sc in s.Scenes ?? new List<TutorialSceneDef>())
                {
                    if (string.IsNullOrEmpty(sc.Id)) p.Add($"stage {s.Id}: a tutorial scene needs an Id");
                    NeedClass(sc.Class, $"stage {s.Id} scene {sc.Id}");
                    if (sc.Pickup != null && !db.Pickups.ContainsKey(sc.Pickup)) p.Add($"stage {s.Id} scene {sc.Id}: unknown pickup '{sc.Pickup}'");
                }
            foreach (var s in db.Stages.Values)
                if (s.Withdraw is WithdrawDef wd)
                {
                    if (wd.AfterPlateClangs < 0 || wd.AfterTurns < 0) p.Add($"stage {s.Id}: Withdraw counts can't be negative (GG-T2070)");
                    if (wd.AfterPlateClangs <= 0 && wd.AfterTurns <= 0) p.Add($"stage {s.Id}: Withdraw needs AfterPlateClangs or AfterTurns (GG-T2070)");
                    if (string.IsNullOrWhiteSpace(wd.Line)) p.Add($"stage {s.Id}: Withdraw needs a Commander Line (GG-T2070)");
                }
            foreach (var s in db.Stages.Values)
                foreach (var w in s.Waves)
                {
                    foreach (var e in w.Enemies) NeedClass(e.Class, $"stage {s.Id}");
                    foreach (var g in w.Guests) NeedClass(g.Class, $"stage {s.Id} guest");
                    foreach (var r in w.Recruitables) RecruitableDef.Validate(db, r, $"stage {s.Id} recruitable", p);
                    NeedClass(w.SurpriseSpeaker, $"stage {s.Id} surprise speaker");
                    RuleBreaks.Validate(db, s, w, p);
                }
            // ContentDb.ThemeOf resolves a stage id to the *first* theme that lists it — a stage accidentally
            // listed in two themes would silently belong to the wrong one there (expedition tracking,
            // carried orbs) with no error here to catch it, so every stage id must be theme-unique.
            var stageThemeOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in db.Campaign.Themes)
                foreach (var s in t.Stages)
                {
                    if (s == null) p.Add($"theme {t.Id}: a null entry in Stages");
                    else if (!db.Stages.ContainsKey(s)) p.Add($"theme {t.Id}: unknown stage '{s}'");
                    else if (stageThemeOwner.TryGetValue(s, out var owner)) p.Add($"theme {t.Id}: stage '{s}' already belongs to theme '{owner}'");
                    else stageThemeOwner[s] = t.Id;
                }
            // GG-T1374: the tutorial drills: real stages, never in a theme, each with its lessons; lessons only on a drill.
            var drills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in db.Campaign.Tutorial ?? new List<string>())
            {
                if (d == null || !db.Stages.ContainsKey(d)) { p.Add($"campaign Tutorial: unknown stage '{d}'"); continue; }
                if (!drills.Add(d)) p.Add($"campaign Tutorial: stage '{d}' listed twice");
                if (stageThemeOwner.TryGetValue(d, out var owner)) p.Add($"campaign Tutorial: stage '{d}' also belongs to theme '{owner}'");
                if (db.Stages[d].Lessons.Count == 0) p.Add($"campaign Tutorial: stage '{d}' has no Lessons");
            }
            foreach (var s in db.Stages.Values)
            {
                if (s.Lessons.Count > 0 && !drills.Contains(s.Id)) p.Add($"stage {s.Id}: Lessons on a stage not in campaign Tutorial");
                if (s.PartySize is int ps && (ps < 1 || !drills.Contains(s.Id))) p.Add($"stage {s.Id}: PartySize {ps} needs a drill and at least 1 (GG-T1958)");
                var seen = new HashSet<string>();
                foreach (var l in s.Lessons)
                {
                    if (Array.IndexOf(StageLessonDef.Ids, l.Id) < 0) p.Add($"stage {s.Id}: unknown lesson '{l.Id}'");
                    else if (!seen.Add(l.Id)) p.Add($"stage {s.Id}: lesson '{l.Id}' listed twice");
                    if (l.Wave < 0 || l.Wave >= s.Waves.Count) p.Add($"stage {s.Id} lesson {l.Id}: Wave {l.Wave} outside its {s.Waves.Count} waves");
                    if (l.FocusRole != null && !new List<ClassDef>(db.Classes.Values).Exists(cl => cl.Role == l.FocusRole)) p.Add($"stage {s.Id} lesson {l.Id}: no class has role '{l.FocusRole}'");
                }
            }
            foreach (var t in db.Campaign.Themes)
                if (t.OrbFindChance < 0f || t.OrbFindChance > 1f) p.Add($"theme {t.Id}: OrbFindChance {t.OrbFindChance} outside 0..1 (GG-T677)");
        }
    }
}
