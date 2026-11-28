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
    /// <summary>The one way into a battle: plays a chapter crawl that is still due (§27, GG-T386: one after every fifth
    /// stage, normally on the way back from its results), then any pending story-beat dialogue (§27.1), then the stage.</summary>
    public static class StageLauncher
    {
        public static void Launch(string stageId, bool replaceCurrent)
        {
            var app = GameApp.I;
            if (app.Save != null)
            {
                // GG-T1379: a drill is practice, so launching or retrying one costs no Rested.
                if (!Drills.IsDrill(app.Content.TryStage(stageId)))
                {
                    if (!RestedGate.Check(() => Launch(stageId, replaceCurrent))) return;
                    RestedGate.Spend();
                }
                if (app.Content.ThemeOf(stageId) != null) TravelRules.SetOut(app.Save); // GG-T1790: a campaign battle sets out from camp
                app.Persist();
            }
            var theme = app.Content.ThemeOf(stageId);
            // Story beats between battles (§27.1, GG-T117): play after the crawl, before the battle, once per save —
            // every one pending, one after another (GG-T119: a recruit and a Camp NPC can share a stage), each
            // picked when the last ends so later beats see what earlier ones did; the same set the bots play headless.
            GameScreen BeatsThenBattle()
            {
                var beat = DialogueRunner.PendingBeat(app.Content, app.Save, stageId);
                return beat == null ? new BattleScreen(stageId) : StoryBeats.Play(beat, BeatsThenBattle, theme?.Id);
            }
            var beatFirst = app.Save == null ? null : DialogueRunner.PendingBeat(app.Content, app.Save, stageId);
            // GG-T1478: the region-entry crawl the results screen didn't get to play plays before the theme's beats, in
            // one scroll with any due chapter crawl and the first beat's own crawl.
            var region = Story.RegionDue(app.Content, app.Save, theme?.Id);
            GameScreen next;
            if (region != null) next = StoryBeats.WithRegion(region, beatFirst, BeatsThenBattle);
            else
            {
                next = BeatsThenBattle();
                // GG-T386: a chapter crawl the results screen didn't get to play ("Next stage ›", or a save from before
                // chapters) plays now, unless a beat opening with its own crawl is up next: it waits for the next stop, so
                // two scrolls never play back to back.
                if (string.IsNullOrEmpty(beatFirst?.Crawl)) next = StoryBeats.WithChapter(next);
            }
            if (replaceCurrent) app.Router.Replace(next); else app.Router.Push(next);
        }
    }
}
