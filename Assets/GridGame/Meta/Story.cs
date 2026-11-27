using System;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The campaign's bookends (§27, GG-T231): the prologue after a new campaign's party pick, and the ending —
    /// crawl, rolling credits, Title — after the first victory over <c>campaign.json</c>'s <c>Ending.Stage</c>.
    /// The postgame themes stay playable afterwards; New Game+ lets the ending play again.
    /// </summary>
    public static class Story
    {
        /// <summary>Set once the ending has played; New Game+ clears it.</summary>
        public const string EndingFlag = "story:ending";

        /// <summary>True when this result should roll the ending: a victory over the ending stage on a save that
        /// hasn't seen it. Never on a Test Mode save (bots never see story).</summary>
        public static bool EndingDue(ContentDb c, SaveData s, bool victory, string stageId) =>
            victory && s != null && !s.TestMode && c.Campaign.Ending?.Stage != null
            && string.Equals(c.Campaign.Ending.Stage, stageId, StringComparison.OrdinalIgnoreCase)
            && !s.Flags.Contains(EndingFlag);

        public static void MarkEndingSeen(SaveData s)
        {
            s.Flags.Add(EndingFlag);
            Codex.Unlock(s, CodexKinds.Story, "ending"); // GG-T929
        }

        /// <summary>GG-T386: the chapter crawl due on this save, or null: the latest chapter whose stage the save has
        /// cleared and which hasn't played. Never on a Test Mode save (bots never see story). Earlier unplayed chapters
        /// are skipped, not queued (<see cref="MarkChapterSeen"/>), so a save never sees two scrolls in a row.</summary>
        public static CrawlDef ChapterDue(ContentDb c, SaveData s)
        {
            if (c == null || s == null || s.TestMode) return null;
            CrawlDef due = null;
            foreach (var ch in c.Campaign.Chapters)
                if (ch?.Id != null && ch.Stage != null && s.ClearedStages.Contains(ch.Stage) && !s.SeenCrawls.Contains(ch.SeenMark)) due = ch;
            return due;
        }

        /// <summary>Marks <paramref name="chapter"/> and every chapter before it as played (GG-T386).</summary>
        public static void MarkChapterSeen(ContentDb c, SaveData s, CrawlDef chapter)
        {
            foreach (var ch in c.Campaign.Chapters)
            {
                if (ch?.Id == null) continue;
                s.SeenCrawls.Add(ch.SeenMark);
                Codex.Unlock(s, CodexKinds.Story, "chapter/" + ch.Id); // GG-T929
                if (ReferenceEquals(ch, chapter)) break;
            }
        }

        /// <summary>Marks every chapter crawl played (Test Mode saves, GG-T386).</summary>
        public static void MarkAllChaptersSeen(ContentDb c, SaveData s)
        {
            foreach (var ch in c.Campaign.Chapters) if (ch?.Id != null) s.SeenCrawls.Add(ch.SeenMark);
        }

        /// <summary>GG-T1478: the theme whose region-entry crawl is due on this save, or null: one with a crawl that
        /// hasn't played. Never on a Test Mode save (bots never see story).</summary>
        public static ThemeDef RegionDue(ContentDb c, SaveData s, string themeId)
        {
            if (c == null || s == null || s.TestMode || themeId == null) return null;
            foreach (var t in c.Campaign.Themes)
                if (t != null && string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase))
                    return t.HasCrawl && !string.IsNullOrEmpty(t.Crawl) && !s.SeenCrawls.Contains(t.SeenMark) ? t : null;
            return null;
        }

        /// <summary>GG-T1478: the theme that follows <paramref name="stageId"/>'s theme in campaign order when the stage is
        /// its theme's last (the stop that opens the next region), else null.</summary>
        public static ThemeDef RegionOpenedBy(ContentDb c, string stageId)
        {
            if (c == null || stageId == null) return null;
            var themes = c.Campaign.Themes;
            for (int i = 0; i < themes.Count - 1; i++)
            {
                var st = themes[i]?.Stages;
                if (st != null && st.Count > 0 && string.Equals(st[st.Count - 1], stageId, StringComparison.OrdinalIgnoreCase))
                    return themes[i + 1];
            }
            return null;
        }

        public static void MarkRegionSeen(SaveData s, ThemeDef region) => s.SeenCrawls.Add(region.SeenMark);
    }
}
