using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed class StoryCrawlScreen : GameScreen
    {
        /// <summary>GG-T1366: the text scrolls this many design px a second, times <see cref="FastForward"/> while held.</summary>
        public const float ScrollSpeed = 95f, FastForward = 4f;
        /// <summary>A page hands on to the next once its last line has risen above this share of the screen, so the
        /// next page's text enters from below while the last one leaves.</summary>
        public const float PageTurnAt = 0.3f;

        private readonly string title;
        private readonly List<CrawlPage> pages;
        private readonly Func<GameScreen> next;
        private readonly bool firstViewing;
        private VisualElement backdrops, clip, text, leaving;
        private float y, leavingY;
        private int page;
        private bool done, fast, skipArmed;
        /// <summary>A campaign crawl (GG-T386: the prologue, a chapter, the ending) over its backdrop; one with
        /// <see cref="CrawlDef.Pages"/> plays them in order, each over its own backdrop (GG-T1366).</summary>
        public StoryCrawlScreen(CrawlDef crawl, GameScreen next, bool firstViewing = true) : this(crawl, () => next, firstViewing) { }
        /// <summary>GG-T1394: a crawl whose next screen is built only once it ends (the onboarding's next step writes its
        /// checkpoint as it starts).</summary>
        public StoryCrawlScreen(CrawlDef crawl, Func<GameScreen> next, bool firstViewing = true)
            : this(crawl.Title, crawl.Pages.Count > 0 ? crawl.Pages : new List<CrawlPage> { new CrawlPage { Text = crawl.Crawl, Backdrop = crawl.Backdrop } },
                next, firstViewing) { }
        /// <summary>Any crawl (GG-T231): the prologue, a story beat's interlude, the ending.</summary>
        public StoryCrawlScreen(string title, string crawl, string backdrop, GameScreen next, bool firstViewing = true)
            : this(title, new List<CrawlPage> { new CrawlPage { Text = crawl, Backdrop = backdrop } }, next, firstViewing) { }
        /// <summary>A paged crawl (GG-T1366): <paramref name="title"/> heads the first page; each page's text scrolls
        /// over its own backdrop, cross-fading as the pages turn. A first viewing's Skip asks twice.</summary>
        public StoryCrawlScreen(string title, IEnumerable<CrawlPage> pages, GameScreen next, bool firstViewing = true)
            : this(title, pages, () => next, firstViewing) { }
        private StoryCrawlScreen(string title, IEnumerable<CrawlPage> pages, Func<GameScreen> next, bool firstViewing)
        {
            var app = GameApp.I;
            var fallback = HubScreen.ThemeFor(app.Content, app.Save);
            this.title = Commander.Fill(title, app.Save, app.Profile?.Name);
            this.pages = pages.Select(p => new CrawlPage
            {
                Name = p.Name,
                Text = Commander.Fill(p.Text, app.Save, app.Profile?.Name),
                Backdrop = p.Backdrop ?? fallback,
            }).ToList();
            this.next = next; this.firstViewing = firstViewing;
        }
        public override string Id => "crawl";
        public override string Music => music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Story) ?? "teller-of-the-tales-00";
        private string music;
        public override bool RebuildOnResize => false; // a rebuild would restart the crawl

        protected override void Build(VisualElement root)
        {
            backdrops = Ui.El("fill", root);
            backdrops.style.top = -GameApp.SafeTop;
            backdrops.style.bottom = -GameApp.SafeBottom;
            clip = Ui.El("fill", root);
            clip.style.overflow = Overflow.Hidden;
            // The scrolling text ends above the Skip button row, so it never runs under the button (GG-T397).
            clip.style.bottom = Space.S48 + Kit.Row + Space.S24;
            page = -1;
            TurnPage();
            // Hold anywhere to fast-forward (as the credits roll does); only Skip ends it.
            root.RegisterCallback<PointerDownEvent>(_ => fast = true);
            root.RegisterCallback<PointerUpEvent>(_ => fast = false);
            root.RegisterCallback<PointerLeaveEvent>(_ => fast = false);
            var skip = RowIcon.IconButton("Skip", RowKind.Enter, Skip, "btn--small", root);
            skip.name = "crawl-skip";
            skip.style.position = Position.Absolute; skip.style.right = Space.S32; skip.style.bottom = Space.S48;
            if (DemoMode.Active || TestMode.Active) root.schedule.Execute(Finish).ExecuteLater(600);
        }

        /// <summary>The next page: its backdrop cross-fades in over the last (a cut under Reduce Motion), and its text
        /// starts below the screen while the last page's text scrolls on out.</summary>
        private void TurnPage()
        {
            page++;
            var p = pages[page];
            var old = backdrops.childCount > 0 ? backdrops[backdrops.childCount - 1] : null;
            if (old == null || p.Backdrop != pages[page - 1].Backdrop)
            {
                var bg = Ui.El("fill", backdrops);
                LivingBackdrop.Show(bg, p.Backdrop, 0);
                bg.style.unityBackgroundImageTintColor = Palette.ManaIndigo;
                if (old != null)
                {
                    if (App.Settings.ReduceMotion) old.RemoveFromHierarchy();
                    else App.StartCoroutine(CrossFade(bg, old));
                }
            }
            if (text != null) { leaving?.RemoveFromHierarchy(); leaving = text; leavingY = y; }
            text = Ui.El("col", clip);
            text.style.position = Position.Absolute;
            text.style.left = text.style.right = Fit.Inset + Space.S96;
            if (page == 0 && !string.IsNullOrEmpty(title)) Ui.Display(title, text, "shadow gold crawl-title", false);
            else if (page > 0) App.Audio.Sfx("ui.select", 0.5f);
            var body = Ui.Para(p.Text ?? "", text, "shadow");
            body.style.unityTextAlign = TextAnchor.UpperCenter;
            y = GameApp.StageHeight;
            text.style.top = y;
        }

        private static IEnumerator CrossFade(VisualElement incoming, VisualElement outgoing)
        {
            incoming.style.opacity = 0;
            yield return Tween.Fade(incoming, 0, 1, Timing.Cinematic);
            outgoing.RemoveFromHierarchy();
        }

        public override void Tick(float dt)
        {
            if (done || text == null) return;
            float step = dt * ScrollSpeed * (fast ? FastForward : 1f);
            y -= step;
            text.style.top = y;
            if (leaving != null)
            {
                leavingY -= step;
                leaving.style.top = leavingY;
                if (leavingY < -leaving.layout.height - 100) { leaving.RemoveFromHierarchy(); leaving = null; }
            }
            if (text.layout.height <= 0) return;
            if (page < pages.Count - 1) { if (y + text.layout.height < GameApp.StageHeight * PageTurnAt) TurnPage(); }
            else if (y < -text.layout.height - 100) Finish();
        }

        /// <summary>Skip: a first viewing asks for a second tap, so a stray tap never throws the story away.</summary>
        private void Skip()
        {
            if (firstViewing && !skipArmed) { skipArmed = true; App.Toast("Tap again to skip"); return; }
            Finish();
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            App.Router.Replace(next());
        }

        /// <summary>Back skips the crawl like Skip ›. A crawl always hands on to what follows it (a launch's battle that
        /// has already spent Rested, the credits, Camp); popping it went back to the stage list, and relaunching charged
        /// again.</summary>
        public override bool OnBack()
        {
            Finish();
            return false;
        }
    }
}
