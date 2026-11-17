namespace GridGame.Core.Content
{
    /// <summary>
    /// A story crawl (§27): campaign.json's <c>Prologue</c> (played once after a new campaign's party is locked in,
    /// before the first Camp), its <c>Ending</c> (played after the first victory over <see cref="Stage"/>, then the
    /// rolling credits, then the Title) and its <c>Chapters</c> (GG-T386: one after every fifth campaign stage, played
    /// once after the first victory over <see cref="Stage"/>). The words are Prose's (<see cref="Key"/>).
    /// </summary>
    public sealed class CrawlDef
    {
        /// <summary>Chapters only: the chapter's id (its Prose key is <c>crawl:chapter/&lt;Id&gt;</c>, its seen-mark in
        /// <c>SaveData.SeenCrawls</c> is <see cref="SeenMark"/>).</summary>
        public string Id;
        public string Title;
        public string Crawl;
        /// <summary>Theme id whose art sits behind the text.</summary>
        public string Backdrop;
        /// <summary>The Ending and every chapter: the campaign stage whose first victory plays it.</summary>
        public string Stage;

        /// <summary>A chapter's Prose key (GG-T386).</summary>
        public static string ChapterKey(string id) => "crawl:chapter/" + id;

        /// <summary>What <c>SaveData.SeenCrawls</c> records once a chapter has played (theme ids lived there before
        /// GG-T386, so chapters carry a prefix of their own).</summary>
        public string SeenMark => "chapter:" + Id;
    }
}
