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
    public sealed class StageSelectScreen : HubPage
    {
        public override string Id => "stages";
        protected override string Title => "Adventure";
        protected override string Backdrop => HubScreen.ThemeFor(C, S);

        /// <summary>GG-T388 (owner 2026-11-16: "a simple stage select which allows you to travel to and from previous battles
        /// so you can 'hunt' existing grounds"): two steps, region then stage. Null = the region list.</summary>
        private string region;
        private bool opened;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T431: each hunted bounty's clue, so the player knows which stage to play.
            var hunting = Features.Bounties ? Bounties.Hunting(C, S) : new List<BountyDef>();
            if (hunting.Count > 0)
            {
                var b = Ui.Panel(body, null, false, "bounty-strip");
                foreach (var h in hunting)
                {
                    var line = Ui.El("row", b);
                    line.style.alignItems = Align.Center;
                    Ui.Para($"{h.Name}: {Bounties.Clue(C, h)}", line, "gold").style.flexShrink = 1;
                    line.KindIcon(RowKind.Bounty); // the bounty skull leads each hunted target's clue (GG-T715)
                }
            }
            var list = Ui.ListPanel(body, null, out var scroll, "stage-list");
            var stages = C.CampaignStages();
            // Unlocked regions, newest first; every locked one collapses into the teaser row at the bottom.
            var regions = new List<ThemeDef>();
            int locked = 0;
            for (int ti = C.Campaign.Themes.Count - 1; ti >= 0; ti--)
            {
                var theme = C.Campaign.Themes[ti];
                int first = stages.FindIndex(x => theme.Stages.Count > 0 && x.Id == theme.Stages[0]);
                if (first < 0) continue;
                if (!Progression.IsStageUnlocked(S, first)) { locked += theme.Stages.Count; continue; }
                regions.Add(theme);
            }
            // Opens on the only region there is; with several, on the list.
            if (!opened)
            {
                opened = true;
                region = regions.Count == 1 ? regions[0].Id : null;
            }
            var open = region == null ? null : regions.Find(t => t.Id == region);
            if (open == null) Regions(regions, locked, list);
            else Stages(open, stages, regions.Count > 1, list);
            Ui.SelectFirst(list);
            // The hand rests on the next stage that can be entered, never a locked one (GG-T1155).
            if (open != null)
                foreach (var row in list.Children())
                    if (row.name != null && row.name.StartsWith("stage-") && !row.ClassListContains("menu-row--disabled")) { Ui.Select(row); break; }
        }

        /// <summary>Step one: every unlocked region with its state — a stage not yet won (New), or every stage cleared
        /// (huntable). Travel (GG-T1786) shows no per-region progress: the road is one journey, not a region's run.</summary>
        private void Regions(List<ThemeDef> regions, int locked, VisualElement list)
        {
            Ui.Para("Pick a region. Cleared stages stay open: hunt them for XP, loot and wanderers.", list, "dim");
            foreach (var theme in regions)
            {
                var t = theme;
                int cleared = theme.Stages.Count(id => S.ClearedStages.Contains(id));
                string status = cleared < theme.Stages.Count ? $"New · {cleared}/{theme.Stages.Count} cleared" : $"Cleared {cleared}/{theme.Stages.Count}";
                if (Features.Bounties && theme.Stages.Any(id => Bounties.OnStage(C, S, id).Count > 0)) status = "☠ " + status; // a hunted target roams here (GG-T431)
                var row = Ui.MenuRow(theme.Name, status, () => { region = t.Id; Refresh(); }, list, null, true);
                row.name = "expedition-" + theme.Id;
                // GG-T715: the region's board tile leads the row, badged Cleared (every stage won) or New (one still to win).
                RowIcon.Badge(row.KindIcon(RowKind.Place, theme.Id), cleared == theme.Stages.Count ? RowKind.Cleared : RowKind.New);
                if (cleared < theme.Stages.Count) row.Q<Label>("value")?.AddToClassList("gold");
            }
            if (locked > 0) Ui.MenuRow($"{locked} more stage{(locked == 1 ? "" : "s")} ahead", "Locked", null, list, null, false).KindIcon(RowKind.Locked);
        }

        /// <summary>Step two: one region's stages, each Cleared or New (a cleared one can be fought again as a hunt). Any
        /// unlocked stage launches; from camp that sets out on the road (<see cref="TravelRules.SetOut"/>, GG-T1786).</summary>
        private void Stages(ThemeDef theme, List<StageDef> stages, bool canGoBack, VisualElement list)
        {
            if (canGoBack)
            {
                var back = Ui.MenuRow("All regions", "", () => { region = null; Refresh(); }, list);
                back.name = "regions-back";
                back.KindIcon(RowKind.Back);
            }
            bool done = theme.Stages.All(id => S.ClearedStages.Contains(id));
            string status = done ? "Completed" : $"{theme.Stages.Count} battles";
            var head = Ui.MenuRow(theme.Name, status, null, list, null, true);
            var headIcon = head.KindIcon(RowKind.Place, theme.Id);
            if (done) RowIcon.Badge(headIcon, RowKind.Cleared);
            head.Q<Label>("label").AddToClassList("gold");
            head.name = "region-head-" + theme.Id;
            // GG-T374: the region's description (Prose "place:<theme>"), under its header.
            var blurb = ContentDb.Blurb(C.PlaceBlurbs, theme.Id);
            if (!string.IsNullOrEmpty(blurb))
            {
                Ui.Note(blurb, list).name = "region-" + theme.Id;
            }
            for (int k = 0; k < theme.Stages.Count; k++)
            {
                var st = C.TryStage(theme.Stages[k]);
                if (st == null) continue;
                int index = stages.FindIndex(x => x.Id == st.Id);
                bool cleared = S.ClearedStages.Contains(st.Id);
                bool can = Progression.IsStageUnlocked(S, index);
                // Same level BattleFactory.Build actually fights it at: the postgame bonus stages carry a
                // LevelOverride, so the index-based curve alone advertised the wrong level for them (GG-T70).
                int recommended = st.LevelOverride ?? Progression.RecommendedLevel(index);
                string state = cleared ? (k > 0 ? "Cleared · hunt" : "Cleared") : "New";
                if (Features.Bounties && Bounties.OnStage(C, S, st.Id).Count > 0) state = "☠ " + state; // a hunted target roams here (GG-T431)
                var row = Ui.MenuRow(st.Name, can ? $"{state} · Lv {recommended} · {RestedPool.LaunchCost} Rested" : $"{state} · Locked", () => Launch(st, index), list, null, can);
                row.name = "stage-" + st.Id;
                // GG-T715: the region's tile leads every stage, badged Cleared / New / Locked (was a star, open star or
                // diamond in the text); an enterable stage ends in the Enter arrow so it reads as a way in, not a line of text.
                RowIcon.Badge(row.KindIcon(RowKind.Stage, theme.Id), cleared ? RowKind.Cleared : can ? RowKind.New : RowKind.Locked);
                if (can) EnterMark(row);
                if (!cleared && can) row.Q<Label>("value")?.AddToClassList("gold");
                if (CaravanUpgrades.Level(S, "scouts") > 0)
                {
                    var foes = st.Waves.SelectMany(wv => wv.Enemies).Select(e => C.TryClass(e.Class)?.Name).Where(n => n != null).Distinct().Take(5);
                    Ui.Note("Scouts: " + string.Join(", ", foes), list, "warn");
                }
                var drops = DropsHint(st);
                if (can && drops.Length > 0)
                {
                    Ui.Note($"Drops: {drops}", list);
                }
            }
        }

        /// <summary>The Enter arrow at the right end of an enterable stage row (GG-T715), after its value.</summary>
        private static void EnterMark(VisualElement row)
        {
            var go = RowIcon.SetIcon(RowIcon.IconId(RowKind.Enter), RowIcon.Class);
            go.name = "enter-mark";
            go.style.width = go.style.height = Size.IconS;
            go.style.flexShrink = 0f;
            go.AddToClassList("enter-mark");
            go.style.alignSelf = Align.Center;
            row.Add(go);
        }

        private string DropsHint(StageDef st)
        {
            var names = st.Waves.SelectMany(w => w.Enemies).Select(e => C.TryClass(e.Class)?.DropTable).Where(t => t != null && C.DropTables.ContainsKey(t))
                .SelectMany(t => C.DropTables[t].Entries).Select(e => C.TryItem(e.Item)?.Name).Where(n => n != null).Distinct().Take(3);
            return string.Join(", ", names);
        }

        /// <summary>GG-T723 (owner 2026-11-23: "yes confirmation step, show them whose going into battle"): a stage row opens
        /// the confirm screen, whose Enter replaces it with the battle.</summary>
        private void Launch(StageDef st, int index) => App.Router.Push(new StageConfirmScreen(st.Id));
    }
}
