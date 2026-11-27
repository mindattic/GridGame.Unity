using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>
    /// A story crawl (§27): campaign.json's <c>Opening</c> (GG-T1363: played once at the start of a new campaign, in
    /// <see cref="Pages"/>), its <c>Ending</c> (played after the first victory over <see cref="Stage"/>, then the
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

        /// <summary>The Opening only (GG-T1363): its pages in order, each with its own backdrop; page n's words are the Prose
        /// key <c>crawl:opening/&lt;n&gt;</c> (<see cref="OpeningKey"/>, 1-based). The Opening's <see cref="Crawl"/> is the
        /// pages' text joined, for a screen that scrolls rather than pages.</summary>
        public List<CrawlPage> Pages = new List<CrawlPage>();

        /// <summary>Page n's Prose key (GG-T1363), n from 1.</summary>
        public static string OpeningKey(int n) => "crawl:opening/" + n;

        /// <summary>A chapter's Prose key (GG-T386).</summary>
        public static string ChapterKey(string id) => "crawl:chapter/" + id;

        /// <summary>What <c>SaveData.SeenCrawls</c> records once a chapter has played (theme ids lived there before
        /// GG-T386, so chapters carry a prefix of their own).</summary>
        public string SeenMark => "chapter:" + Id;
    }

    /// <summary>One page of a paged crawl (GG-T1363).</summary>
    public sealed class CrawlPage
    {
        /// <summary>What the page is about, for authors (not shown).</summary>
        public string Name;
        /// <summary>Theme id whose art sits behind the page.</summary>
        public string Backdrop;
        /// <summary>The page's words, joined from Prose at load (never in gameplay JSON).</summary>
        public string Text;
    }
}
