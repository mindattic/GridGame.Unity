using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The campaign checks: sky, breeds and the Opening, Ending and chapter crawls. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class CampaignValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            SkyDef.Validate(db, p); // GG-T576
            Breeds.Validate(db, p); // GG-T1774
            ValidateCrawl(db, db.Campaign.Opening, "campaign Opening", false, p);
            ValidatePages(db, db.Campaign.Opening, "campaign Opening", p);
            ValidateCrawl(db, db.Campaign.Ending, "campaign Ending", true, p);
            ValidateChapters(db, p);
        }

        /// <summary>GG-T231: a campaign crawl (Opening/Ending) is optional, but one that exists needs text, a known
        /// backdrop theme, and (the Ending) a campaign stage to follow.</summary>
        private static void ValidateCrawl(ContentDb db, CrawlDef crawl, string w, bool needsStage, List<string> p)
        {
            if (crawl == null) return;
            if (string.IsNullOrWhiteSpace(crawl.Title)) p.Add($"{w}: needs a Title");
            if (string.IsNullOrWhiteSpace(crawl.Crawl)) p.Add($"{w}: needs Crawl text");
            if (crawl.Backdrop != null && !db.Campaign.Themes.Exists(t => string.Equals(t.Id, crawl.Backdrop, StringComparison.OrdinalIgnoreCase))) p.Add($"{w}: unknown backdrop theme '{crawl.Backdrop}'");
            if (needsStage && (crawl.Stage == null || db.ThemeOf(crawl.Stage) == null)) p.Add($"{w}: needs a campaign Stage, has '{crawl.Stage}'");
            if (!needsStage && crawl.Stage != null) p.Add($"{w}: only the Ending names a Stage");
        }

        /// <summary>GG-T1363: the Opening is paged (at least one page, each with a known backdrop theme and words under
        /// its key <c>crawl:opening/&lt;n&gt;</c>); no other crawl has pages.</summary>
        private static void ValidatePages(ContentDb db, CrawlDef opening, string w, List<string> p)
        {
            if (opening != null)
            {
                if (opening.Pages.Count == 0) p.Add($"{w}: needs Pages");
                for (int i = 0; i < opening.Pages.Count; i++)
                {
                    var pg = opening.Pages[i];
                    string wp = $"{w} page {i + 1} ({CrawlDef.OpeningKey(i + 1)})";
                    if (pg == null) { p.Add($"{wp}: null entry"); continue; }
                    if (string.IsNullOrWhiteSpace(pg.Backdrop)) p.Add($"{wp}: needs a Backdrop");
                    else if (!db.Campaign.Themes.Exists(t => string.Equals(t.Id, pg.Backdrop, StringComparison.OrdinalIgnoreCase))) p.Add($"{wp}: unknown backdrop theme '{pg.Backdrop}'");
                    if (string.IsNullOrWhiteSpace(pg.Text) || pg.Text == ProseText.Placeholder(CrawlDef.OpeningKey(i + 1))) p.Add($"{wp}: no text under its key");
                }
            }
            if (db.Campaign.Ending?.Pages.Count > 0) p.Add("campaign Ending: only the Opening has Pages");
            foreach (var ch in db.Campaign.Chapters)
                if (ch?.Pages.Count > 0) p.Add($"campaign chapter '{ch.Id}': only the Opening has Pages");
        }

        /// <summary>GG-T386, GG-T1480: the chapter crawls. Each has an id, a title, text and a known backdrop, and follows a
        /// campaign stage that closes a region (its theme's last stage, STORY_OUTLINE.md §2), in campaign order, one chapter
        /// to a stage. A chapter on the Ending's stage waits for the next arrival at Camp after the credits.</summary>
        private static void ValidateChapters(ContentDb db, List<string> p)
        {
            var stages = db.CampaignStages();
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < stages.Count; i++) index[stages[i].Id] = i;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var byStage = new Dictionary<int, string>();
            int last = -1;
            foreach (var ch in db.Campaign.Chapters)
            {
                string w = $"campaign chapter '{ch?.Id}'";
                if (ch == null) { p.Add("campaign Chapters: null entry"); continue; }
                if (string.IsNullOrWhiteSpace(ch.Id) || !System.Text.RegularExpressions.Regex.IsMatch(ch.Id, "^[a-z0-9]+(-[a-z0-9]+)*$"))
                    p.Add($"{w}: needs an Id of lowercase letters, digits and dashes");
                else if (!ids.Add(ch.Id)) p.Add($"{w}: duplicate id");
                if (string.IsNullOrWhiteSpace(ch.Title)) p.Add($"{w}: needs a Title");
                if (string.IsNullOrWhiteSpace(ch.Crawl)) p.Add($"{w}: needs Crawl text");
                if (ch.Backdrop != null && !db.Campaign.Themes.Exists(t => string.Equals(t.Id, ch.Backdrop, StringComparison.OrdinalIgnoreCase)))
                    p.Add($"{w}: unknown backdrop theme '{ch.Backdrop}'");
                if (ch.Stage == null || !index.TryGetValue(ch.Stage, out int i)) { p.Add($"{w}: needs a campaign Stage, has '{ch.Stage}'"); continue; }
                var region = db.ThemeOf(ch.Stage);
                if (region == null || !string.Equals(region.Stages[region.Stages.Count - 1], ch.Stage, StringComparison.OrdinalIgnoreCase))
                    p.Add($"{w}: follows '{ch.Stage}', which is not the last stage of its region");
                if (byStage.TryGetValue(i, out var other)) p.Add($"{w}: '{ch.Stage}' already has chapter '{other}'");
                else byStage[i] = ch.Id;
                if (i <= last) p.Add($"{w}: out of campaign order");
                last = Math.Max(last, i);
            }
        }
    }
}
