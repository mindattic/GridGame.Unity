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
    /// <summary>
    /// Story beats as screens (§27, GG-T231): a beat with a <see cref="DialogueDef.Crawl"/> scrolls it before its
    /// conversation; the way back to Camp after a battle plays a pending level interlude; the final boss's first
    /// defeat rolls the ending crawl, the credits, then the Title.
    /// </summary>
    public static class StoryBeats
    {
        /// <summary>The beat's crawl (if it has one), then its conversation, then <paramref name="next"/>.</summary>
        public static GameScreen Play(DialogueDef beat, Func<GameScreen> next, string backdrop)
        {
            GameScreen talk = new DialogueScreen(beat, next, backdrop);
            return string.IsNullOrEmpty(beat.Crawl) ? talk : new StoryCrawlScreen(beat.Title, beat.Crawl, beat.Backdrop ?? backdrop, talk);
        }

        /// <summary>Camp, after any level interlude that's due (one after another, each chosen when the last ends), then
        /// any Joined scene (GG-T1594).</summary>
        public static GameScreen Camp()
        {
            var app = GameApp.I;
            var beat = DialogueRunner.PendingInterlude(app.Content, app.Save);
            return beat == null ? JoinedThenCamp() : Play(beat, Camp, null);
        }

        /// <summary>GG-T386: <paramref name="next"/>, preceded by the chapter crawl that is due (if any), which is marked
        /// played (with every chapter before it) and saved as it starts.</summary>
        public static GameScreen WithChapter(GameScreen next)
        {
            var app = GameApp.I;
            var ch = Story.ChapterDue(app.Content, app.Save);
            if (ch == null) return next;
            Story.MarkChapterSeen(app.Content, app.Save, ch);
            app.Persist();
            return new StoryCrawlScreen(ch, next);
        }

        /// <summary>The results screen's Continue: the ending if this victory earned it; else the chapter crawl this
        /// victory earned (GG-T386), then any vendor join beat (GG-T537), then Camp; else Camp via any interlude. A level interlude due with a chapter waits for
        /// the next launch or results screen, so two scrolls never play back to back.</summary>
        public static void AfterResults(bool victory, string stageId)
        {
            var app = GameApp.I;
            if (Story.EndingDue(app.Content, app.Save, victory, stageId))
            {
                Story.MarkEndingSeen(app.Save);
                app.Persist();
                var e = app.Content.Campaign.Ending;
                app.Router.ResetTo(new StoryCrawlScreen(e.Title, e.Crawl, e.Backdrop ?? app.Content.ThemeOf(stageId)?.Id,
                    new CreditsScreen(() => new TitleScreen())));
                return;
            }
            // GG-T386: the chapter crawl this victory earned, then Camp. GG-T537: a vendor's join beat (no crawl of its own)
            // still plays between the scroll and Camp, so the vendor arrives with the party; a level interlude waits.
            // GG-T1478: the first victory over a region's last stage also plays the next region's entry crawl, inside the
            // chapter's scroll (same stop) or alone.
            var ch = Story.ChapterDue(app.Content, app.Save);
            var region = victory ? Story.RegionDue(app.Content, app.Save, Story.RegionOpenedBy(app.Content, stageId)?.Id) : null;
            if (ch == null && region == null) { app.Router.ResetTo(Camp()); return; }
            if (ch != null) Story.MarkChapterSeen(app.Content, app.Save, ch);
            if (region != null) Story.MarkRegionSeen(app.Save, region);
            app.Persist();
            app.Router.ResetTo(Scroll(ch, region, null, VendorsThenCamp()));
        }

        /// <summary>GG-T1478: the region-entry crawl due on a launch into <paramref name="region"/>, as one scroll with the
        /// chapter crawl due (if any) and the first beat's own crawl (if it has one), so two scrolls never play back to
        /// back; then <paramref name="beats"/> (that beat's conversation first). Marks the crawls played and saves.</summary>
        public static GameScreen WithRegion(ThemeDef region, DialogueDef beatFirst, Func<GameScreen> beats)
        {
            var app = GameApp.I;
            var ch = Story.ChapterDue(app.Content, app.Save);
            if (ch != null) Story.MarkChapterSeen(app.Content, app.Save, ch);
            Story.MarkRegionSeen(app.Save, region);
            app.Persist();
            bool beatCrawl = !string.IsNullOrEmpty(beatFirst?.Crawl);
            return Scroll(ch, region, beatCrawl ? beatFirst : null, beatCrawl ? new DialogueScreen(beatFirst, beats, region.Id) : beats());
        }

        /// <summary>One scroll of the given crawls in order (chapter, region, beat), each after the first headed by its
        /// title, over the region's backdrop when there is one.</summary>
        private static GameScreen Scroll(CrawlDef chapter, ThemeDef region, DialogueDef beat, GameScreen next)
        {
            string title = null, text = null;
            void Add(string heading, string body)
            {
                if (string.IsNullOrEmpty(body)) return;
                if (text == null) { title = heading; text = body; }
                else text += string.IsNullOrEmpty(heading) ? "\n\n" + body : "\n\n" + heading + "\n\n" + body;
            }
            Add(chapter?.Title, chapter?.Crawl);
            Add(region?.Name, region?.Crawl);
            Add(beat?.Title, beat?.Crawl);
            return text == null ? next : new StoryCrawlScreen(title, text, region?.Id ?? chapter?.Backdrop ?? beat?.Backdrop, next);
        }

        /// <summary>GG-T537: Camp, after every vendor join beat that's due (one after another), then any Joined scene (GG-T1594).</summary>
        private static GameScreen VendorsThenCamp()
        {
            var app = GameApp.I;
            var beat = DialogueRunner.PendingVendorBeat(app.Content, app.Save);
            return beat == null ? JoinedThenCamp() : Play(beat, VendorsThenCamp, null);
        }

        /// <summary>GG-T1594: Camp, after a Joined scene for each new hero who hasn't been greeted (one after another, each
        /// chosen when the last ends). Reached from the results screen's Continue, so the VariantPicker has already asked
        /// each joiner's look on the results screen.</summary>
        private static GameScreen JoinedThenCamp()
        {
            var app = GameApp.I;
            var joined = DialogueRunner.PendingJoined(app.Content, app.Save);
            return joined == null ? (GameScreen)new HubScreen() : DialogueScreen.ForRunner(joined.Begin(app.Content, app.Save), JoinedThenCamp);
        }

        /// <summary>The onboarding after the team pick (SaveSlotScreen.Start), in order: each step returns its screen,
        /// leading to the next step's, or the next step's screen when it has nothing to play. After the last, Camp.</summary>
        private static readonly Func<Func<GameScreen>, GameScreen>[] NewCampaignSteps = { OpeningCrawl, Founding, Drill };

        /// <summary>A new campaign's first screen (GG-T1358, the one onboarding sequencer): the opening crawl (if any),
        /// then the Founding scene (GG-T1594, if one is due), then the first tutorial drill when drills exist, else Camp.</summary>
        public static GameScreen NewCampaign() => NewCampaignStep(0);

        private static GameScreen NewCampaignStep(int i) =>
            i >= NewCampaignSteps.Length ? new HubScreen() : NewCampaignSteps[i](() => NewCampaignStep(i + 1));

        private static string OpeningBackdrop(CrawlDef p) =>
            p == null ? null : p.Backdrop ?? (p.Pages.Count > 0 ? p.Pages[0].Backdrop : null);

        /// <summary>GG-T1394: records the Crawl checkpoint as the crawl starts; the steps after it are built as it ends, so
        /// each writes its own.</summary>
        private static GameScreen OpeningCrawl(Func<GameScreen> next)
        {
            var app = GameApp.I;
            var p = app.Content.Campaign.Opening;
            if (p == null) return next();
            Onboarding.Begin(app.Save, OnboardingStep.Crawl);
            app.Persist();
            return new StoryCrawlScreen(p, next);
        }

        private static GameScreen Founding(Func<GameScreen> next)
        {
            var app = GameApp.I;
            var founding = DialogueRunner.PendingFounding(app.Content, app.Save);
            return founding == null ? next() : Play(founding, next, OpeningBackdrop(app.Content.Campaign.Opening));
        }

        /// <summary>The first tutorial drill (campaign.json Tutorial, GG-T1374) when its stage exists.</summary>
        private static GameScreen Drill(Func<GameScreen> next)
        {
            var app = GameApp.I;
            var c = app.Content;
            if (Drills.Skipped(app.Save))
            {
                Onboarding.Begin(app.Save, OnboardingStep.HubIntro); // GG-T1645: Start Campaign's Skip to Camp
                app.Persist();
                return next();
            }
            var first = c.Campaign.Tutorial.Count > 0 ? c.TryStage(c.Campaign.Tutorial[0]) : null;
            return first == null ? next() : DrillAt(0, first.Id);
        }

        /// <summary>Tutorial drill <paramref name="n"/>, its checkpoint written as it starts. A drill always starts from
        /// the top (a battle is never saved mid-fight, BIBLE 26.8).</summary>
        private static GameScreen DrillAt(int n, string stageId)
        {
            var app = GameApp.I;
            Onboarding.Begin(app.Save, OnboardingStep.Drill, n);
            app.Persist();
            return new BattleScreen(stageId);
        }

        /// <summary>GG-T1394: where Continue or Load resumes a campaign whose onboarding isn't done (<see cref="Onboarding.Next"/>):
        /// the opening crawl from the top (then the rest of the sequence), the drill it stopped in, or Camp's first
        /// visit, whose lesson finishes the onboarding (<see cref="CampCoach"/>). Null once onboarding is done.</summary>
        public static GameScreen Resume()
        {
            var app = GameApp.I;
            var r = Onboarding.Next(app.Save, app.Content);
            switch (r.Step)
            {
                case OnboardingStep.Crawl: return NewCampaign();
                case OnboardingStep.Drill: return DrillAt(r.Drill, r.StageId);
                case OnboardingStep.HubIntro:
                    Onboarding.Begin(app.Save, OnboardingStep.HubIntro);
                    app.Persist();
                    return new HubScreen();
                default: return null;
            }
        }
    }
}
