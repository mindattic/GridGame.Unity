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

        public static void MarkEndingSeen(SaveData s) => s.Flags.Add(EndingFlag);

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
                if (ReferenceEquals(ch, chapter)) break;
            }
        }

        /// <summary>Marks every chapter crawl played (Test Mode saves, GG-T386).</summary>
        public static void MarkAllChaptersSeen(ContentDb c, SaveData s)
        {
            foreach (var ch in c.Campaign.Chapters) if (ch?.Id != null) s.SeenCrawls.Add(ch.SeenMark);
        }
    }
}
