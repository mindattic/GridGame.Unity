using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Results: victory/defeat/retreat, gold, loot, unlocks (bible §22). Level-ups are shown
    /// first, by LevelUpScreen — route through <see cref="RouteFrom"/> rather than constructing this directly.</summary>
    public sealed class PostBattleScreen : GameScreen
    {
        private readonly BattleReport report;
        private readonly string stageId;
        private readonly List<Coroutine> reveals = new List<Coroutine>();
        public PostBattleScreen(BattleReport report, string stageId) { this.report = report; this.stageId = stageId; }
        public override string Id => "postbattle";
        /// <summary>GG-T458: campaign.json MomentMusic.victory (a Crucible run's results too) or .defeat.</summary>
        public override string Music => music ??= report.Victory || report.Crucible
            ? App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Victory) ?? "heroic-age-00"
            : App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Defeat) ?? "heartbreaking-00";
        private string music;

        public override void OnHidden()
        {
            foreach (var co in reveals) if (co != null) App.StopCoroutine(co);
            reveals.Clear();
        }

        /// <summary>What the Continue button does (set by Build): Android Back does the same, so it never pops back past the
        /// results to the stage list and skips StoryBeats.AfterResults (the ending crawl, the credits, chapter crawls).</summary>
        private System.Action onContinue;
        private bool continued;

        public override bool OnBack()
        {
            if (!continued && onContinue != null) { continued = true; onContinue(); }
            return false;
        }

        /// <summary>Tame offers already answered on this results screen (class id → the settled line), so a re-mount (a
        /// resize, back from a wanderer's talk) shows the answer, not Accept/Decline again.</summary>
        private readonly Dictionary<string, (string msg, bool good)> tameSettled = new Dictionary<string, (string, bool)>();

        /// <summary>The fought stage's theme and name, read from the report (GG-T127): a generated stage may already be
        /// pruned from the ContentDb while its results still show; fall back to a live lookup for older reports.</summary>
        public static string ThemeOf(ContentDb c, BattleReport report, string stageId) => report.StageTheme ?? c.TryStage(stageId)?.Theme;
        public static string NameOf(ContentDb c, BattleReport report, string stageId) => report.StageName ?? c.TryStage(stageId)?.Name ?? "";

        /// <summary>Battle-end routing: a level-up fanfare first if anyone leveled, else straight to loot.</summary>
        public static void RouteFrom(BattleReport report, string stageId) =>
            App.Router.Replace(report.LevelUps.Count > 0 ? (GameScreen)new LevelUpScreen(report, stageId) : new PostBattleScreen(report, stageId));

        protected override void Build(VisualElement root)
        {
            var c = App.Content;
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, ThemeOf(c, report, stageId), 3);
            bg.style.unityBackgroundImageTintColor = new Color(0.45f, 0.45f, 0.55f);
            var col = Ui.El("fill col", root);
            Fit.Columnize(col, Space.S32); // the results sit in the centred content column (GG-T398)
            Ui.El("", col).style.height = Fit.H(5.5f);

            if (report.Crucible) { BuildCrucible(root, col, c); return; }
            if (Drills.IsDrill(c.TryStage(stageId))) { BuildDrill(col, c); return; }
            var title = Ui.Text(report.Victory ? "VICTORY" : report.Retreated ? "RETREATED" : report.Escaped ? "ESCAPED" : "DEFEAT", "t-display shadow", col);
            Fonts.UseDisplay(title);
            title.style.color = report.Victory ? Palette.Gold : new Color(1f, 0.45f, 0.45f);
            Ui.Text(NameOf(c, report, stageId), "t-h2 shadow dim", col).style.unityTextAlign = TextAnchor.MiddleCenter;

            var w = Ui.Panel(col, null, true);
            Ui.Margin(w, top: Space.S48);
            var list = Ui.List(w).contentContainer;
            if (report.Victory)
            {
                Ui.MenuRow("Gold", "+" + Ui.Gold(report.Gold), null, list).WithKindIcon(RowKind.Shop).Q<Label>("value").AddToClassList("gold"); // GG-T749: the coin emblem
                // GG-T623: a slow, repeatable trickle, same row style as Gold — the Aetherist refines it once joined.
                // GG-T676: orbs left in the row settle back into Dust; the row says how much of it came from them.
                if (report.DustEarned > 0)
                    Ui.MenuRow(DustEconomy.DustName, report.OrbDust > 0
                        ? $"+{report.DustEarned:N0} ({report.OrbDust:N0} from {report.OrbsLeft} unspent {(report.OrbsLeft == 1 ? "orb" : "orbs")})"
                        : $"+{report.DustEarned:N0}", null, list).WithKindIcon(RowKind.Item, Art.ItemIcon(DustEconomy.DustId)).name = "results-dust";
                TicketsRow(list);
                // GG-T677: an owned Mana Orb gained here is one more orb to fill in every battle from now on.
                if (report.OrbsGained > 0)
                {
                    var orbRow = Ui.MenuRow(report.OrbsGained == 1 ? "Mana Orb found" : $"{report.OrbsGained} Mana Orbs found",
                        $"you now hold {report.OrbsOwned}", null, list);
                    orbRow.name = "results-orbs";
                    orbRow.KindIcon(RowKind.Item);
                    orbRow.Q<Label>("label")?.AddToClassList("gold");
                    Ui.Text(string.Join(" · ", report.OrbsGainedFrom), "small dim", list).style.whiteSpace = WhiteSpace.Normal;
                }
                if (report.BrokenOrbFound)
                    Ui.MenuRow("Broken Mana Orb", "the Manasmith can restore it", null, list).WithKindIcon(RowKind.Item, Art.ItemIcon(ManaOrbs.BrokenOrbId)).name = "results-broken-orb";
                Ui.MenuRow("Experience", $"+{report.XpEach} each", null, list).KindIcon(RowKind.Hero);
                NextAbilityRows(c, list);
                TurnsRow(list);
                ComboRows(c, list);
                BondRows(c, list);
                // Higher difficulty, greater rewards (GG-T309): name the dial the gold, XP and drops above were scaled by.
                if (report.Difficulty > GridGame.Core.Battle.DifficultyTable.Neutral)
                    Ui.MenuRow($"Difficulty {report.Difficulty}", "rewards " + GridGame.Core.Battle.DifficultyTable.RewardLabel(report.Difficulty), null, list).WithKindIcon(RowKind.Settings).name = "results-difficulty";
                // GG-T514: milestones, unlocks and decisions (a joining hero, a tame or wanderer offer, a tome's
                // "Teach to…") come before the loot list. A first-hour stage drops ~15 rows, which pushed all of
                // these off the bottom, so a player tapping on past the results never saw them.
                if (report.CampaignComplete) Ui.MenuRow("Campaign complete!", "New Game+ unlocked at camp", null, list).WithKindIcon(RowKind.NewGamePlus).Q<Label>("label").AddToClassList("gold");
                if (report.ExpeditionComplete)
                {
                    Ui.MenuRow($"{report.ExpeditionName} expedition complete!", "+" + Ui.Gold(report.ExpeditionBonus), null, list).WithKindIcon(RowKind.Place).Q<Label>("label").AddToClassList("gold");
                    Ui.Text("The caravan makes camp — everyone rests.", "small good", list);
                }
                else if (report.ExpeditionName != null)
                {
                    var er = Ui.MenuRow($"Expedition: battle {report.ExpeditionStep} of {report.ExpeditionLength} won", report.CarriedOrbs == 0 ? "no orbs carry over" : report.CarriedOrbs == 1 ? "1 orb carries over" : $"{report.CarriedOrbs} orbs carry over", null, list);
                    er.name = "expedition-progress";
                    er.KindIcon(RowKind.Stage);
                    Ui.Text("Wounds carry to the next battle; the inn stays closed until the expedition ends.", "small dim", list).style.whiteSpace = WhiteSpace.Normal;
                }
                if (report.Milestone != null) // GG-T301: a theme's first full clear
                    Ui.MenuRow($"{report.Milestone} cleared!", "Milestone: " + (c.TryItem(report.MilestoneItem)?.Name ?? report.MilestoneItem), null, list).WithKindIcon(RowKind.Item, Art.ItemIcon(report.MilestoneItem)).name = "results-milestone";
                if (report.UnlockedStage != null) Ui.MenuRow("New stage unlocked", report.UnlockedStage, null, list).WithKindIcon(RowKind.Stage).Q<Label>("value").AddToClassList("gold");
                if (report.NewSlots > 0) Ui.MenuRow("Ability slot unlocked!", $"+{report.NewSlots}", null, list).WithKindIcon(RowKind.Ability).Q<Label>("label").AddToClassList("good");
                foreach (var g in report.GuestsJoined)
                    Ui.MenuRow($"{c.Class(g).Name} joins the caravan!", "New hero", null, list).WithKindIcon(RowKind.Hero, g).Q<Label>("label").AddToClassList("good");
                // GG-T537: a vendor whose story beat is this clear arrives at Camp (its join scene plays on the way there).
                foreach (var arrival in CampUnlocks.Arrivals(c, App.Save, stageId))
                {
                    var ar = Ui.MenuRow(arrival + "!", "At Camp", null, list);
                    ar.name = "results-arrival";
                    ar.KindIcon(RowKind.Npc);
                    ar.Q<Label>("label").AddToClassList("good");
                }
                foreach (var offer in report.TameOffers) TameOfferBlock(offer, c, list); // GG-T303
                if (report.WandererOffer != null) WandererBlock(report.WandererOffer, c, list); // GG-T342
                if (Features.Bounties && report.BountyProgress != null) Ui.MenuRow("Bounty", report.BountyProgress, null, list).KindIcon(RowKind.Bounty);
                foreach (var note in report.QuestProgress) Ui.MenuRow("Quest", note, null, list).KindIcon(RowKind.Quest);
                if (report.Items.Count == 0) Ui.Text("No items dropped.", "small dim", list);
                else Ui.Text("Loot", "small dim", list).name = "results-loot-head";
                RevealItems(report.Items, c, list);
                if (report.AutoSold > 0) Ui.MenuRow("Stack full — sold extras", SoldCount(report.AutoSold), null, list).KindIcon(RowKind.Shop);
            }
            else
            {
                var t = Ui.Text(report.Retreated ? "You fall back with part of your spoils — safer than risking the whole party. The stage is yours to retry." :
                    report.Escaped ? "The party slips away in the smoke. Wounds remain; no rewards." :
                    report.ExpeditionFailed ? $"{WithThe(report.ExpeditionName)} expedition has failed. The party falls back to the caravan and recovers — the expedition restarts from battle 1." :
                    "The party retreats to camp and recovers. The gold and loot are lost — but every hero keeps the experience they earned. The stage is yours to retry.", "kit-text", list);
                t.style.whiteSpace = WhiteSpace.Normal;
                if (report.Retreated)
                {
                    Ui.MenuRow("Gold", "+" + Ui.Gold(report.Gold), null, list).WithKindIcon(RowKind.Shop).Q<Label>("value").AddToClassList("gold");
                    RevealItems(report.Items, c, list);
                }
                if (!report.Escaped && report.XpEach > 0)
                    Ui.MenuRow("Hard-won experience", $"+{report.XpEach} each", null, list).WithKindIcon(RowKind.Hero).name = "defeat-xp";
                NextAbilityRows(c, list);
                TurnsRow(list);
                ComboRows(c, list);
            }

            // A guest down at the end isn't lost (GG-T114) — they'll be met on this stage again.
            foreach (var g in report.GuestsFell)
                Ui.MenuRow($"{c.Class(g).Name} fell in battle", "Will return", null, list).WithKindIcon(RowKind.Hero, g).Q<Label>("label").AddToClassList("dim");
            // A wanderer who fell (or saw a defeat) goes back to the road: still out there to be met again (GG-T342).
            if (report.Wanderer != null && report.WandererOffer == null && App.Save.Hero(report.Wanderer) == null && c.TryClass(report.Wanderer) != null)
                Ui.MenuRow($"The wandering {c.Class(report.Wanderer).Name} goes on their way", "May be met again", null, list).WithKindIcon(RowKind.Hero, report.Wanderer).Q<Label>("label").AddToClassList("dim");
            foreach (var g in report.GuestsLost) // permadeath only (a future Ironman mode) — never in the normal game
                Ui.MenuRow($"{c.Class(g).Name} fell in battle", "Gone for good", null, list).WithKindIcon(RowKind.Hero, g).Q<Label>("label").AddToClassList("bad");
            var row = Ui.El("row", col);
            row.style.justifyContent = Justify.Center;
            if (!report.Victory)
            {
                // A failed expedition restarts from its first battle.
                var theme = c.ThemeOf(stageId);
                string retryId = report.ExpeditionFailed && theme != null ? theme.Stages[0] : stageId;
                // A pruned generated stage can't be retried. GG-T749: named "retry" (tools/playthrough_sim.py looks for that name).
                if (c.TryStage(retryId) != null) RowIcon.IconButton("Retry", RowKind.NewGamePlus, () => StageLauncher.Launch(retryId, true), null, row).name = "retry";
            }
            bool ending = Story.EndingDue(c, App.Save, report.Victory, stageId);
            // GG-T513: mid-expedition, "Continue" beside "Next battle ›" read as "carry on" though it goes back to Camp
            // (the expedition is kept), and it wore the same orange. There it says where it goes and steps back to a
            // plain button, leaving Next battle the one primary. Its automation name stays "continue" either way.
            bool midExpedition = !ending && report.Victory && report.NextStageId != null;
            onContinue = () => StoryBeats.AfterResults(report.Victory, stageId);
            var cont = RowIcon.IconButton(midExpedition ? "To Camp" : "Continue", midExpedition ? RowKind.Station : RowKind.Enter, () => { if (continued) return; continued = true; onContinue(); }, midExpedition ? null : "btn--primary", row);
            cont.name = "continue";
            if (ending) Ui.Text("The road ahead is quiet, for now. Continue to see how the story closes.", "small gold", list).style.whiteSpace = WhiteSpace.Normal;
            if (midExpedition)
            {
                RowIcon.IconButton("Retreat early", RowKind.Back, () => Confirm.Show(
                    "Stop the expedition here? You keep everything you've earned so far — you just give up the completion bonus for pushing on.",
                    "Retreat early", () => { Expeditions.Abandon(App.Save); App.Persist(); App.Router.ResetTo(new HubScreen()); }), null, row);
                RowIcon.IconButton("Next battle", RowKind.Enter, () => StageLauncher.Launch(report.NextStageId, true), "btn--primary", row).name = "next";
            }
            else if (!ending && report.Victory)
            {
                var stages = c.CampaignStages();
                int idx = stages.FindIndex(s => s.Id == stageId);
                if (idx >= 0 && idx + 1 < stages.Count && Progression.IsStageUnlocked(App.Save, idx + 1) && Expeditions.CanLaunch(c, App.Save, stages[idx + 1].Id, out _))
                    RowIcon.IconButton("Next expedition", RowKind.Stage, () => StageLauncher.Launch(stages[idx + 1].Id, true), null, row).name = "next";
            }

            Ui.El("", col).style.height = Space.S48;
            if (TestMode.Active) root.schedule.Execute(() => TestMode.AfterBattle(report, stageId)).ExecuteLater(1500);
            else if (DemoMode.Active) root.schedule.Execute(() => DemoMode.AfterBattle(report, stageId)).ExecuteLater(1500);
        }

        /// <summary>Crucible results (§22.10): waves survived, the record, and the whole haul, which a Crucible run
        /// keeps however it ended. Again › starts the save's next gauntlet; Continue goes back to camp.</summary>
        private void BuildCrucible(VisualElement root, VisualElement col, ContentDb c)
        {
            var title = Ui.Text("CRUCIBLE", "t-display shadow", col);
            Fonts.UseDisplay(title);
            title.style.color = new Color(1f, 0.6f, 0.3f);
            var waves = Ui.Text($"Survived {report.CrucibleWaves} wave{(report.CrucibleWaves == 1 ? "" : "s")}", "t-h1 shadow gold", col);
            waves.name = "crucible-waves";
            waves.style.unityTextAlign = TextAnchor.MiddleCenter;
            var best = Ui.Text(report.CrucibleNewBest ? "New best!" : $"Best: {report.CrucibleBest} waves", report.CrucibleNewBest ? "t-h2 shadow good" : "t-h2 shadow dim", col);
            best.name = "crucible-best";
            best.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (report.CrucibleNewBest)
            {
                // The record is the run's headline: a fanfare and a pop, after the results have settled in.
                best.style.opacity = 0f;
                reveals.Add(App.StartCoroutine(NewBest(best)));
            }

            var w = Ui.Panel(col, null, true);
            Ui.Margin(w, top: Space.S32);
            var list = Ui.List(w).contentContainer;
            var t = Ui.Text(report.Victory ? "Every wave falls. The Crucible is silent, and the whole haul is yours." :
                report.Retreated || report.Escaped ? "The party steps out of the Crucible with everything it earned, and recovers." :
                "The party falls in the Crucible, but every coin, drop and point of experience earned is kept. Everyone recovers.", "small", list);
            t.style.whiteSpace = WhiteSpace.Normal;
            Ui.MenuRow("Gold banked", "+" + Ui.Gold(report.Gold), null, list).WithKindIcon(RowKind.Shop).Q<Label>("value").AddToClassList("gold");
            // GG-T945: the run's own mode's record (Crucible.Commit filled it), not another mode's.
            Ui.MenuRow("Best haul", Ui.Gold(report.CrucibleBestGold), null, list).WithKindIcon(RowKind.Crucible).name = "crucible-best-gold";
            TicketsRow(list);
            Ui.MenuRow("Experience", $"+{report.XpEach} each", null, list).WithKindIcon(RowKind.Hero).name = "crucible-xp";
            NextAbilityRows(c, list);
            TurnsRow(list);
            if (report.Items.Count == 0) Ui.Text("No items dropped.", "small dim", list);
            RevealItems(report.Items, c, list);
            if (Features.Bounties && report.BountyProgress != null) Ui.MenuRow("Bounty", report.BountyProgress, null, list).KindIcon(RowKind.Bounty);
            if (report.AutoSold > 0) Ui.MenuRow("Stack full — sold extras", SoldCount(report.AutoSold), null, list).KindIcon(RowKind.Shop);

            var row = Ui.El("row", col);
            row.style.justifyContent = Justify.Center;
            onContinue = () => App.Router.ResetTo(new HubScreen());
            var cont = RowIcon.IconButton("Continue", RowKind.Enter, () => { if (continued) return; continued = true; onContinue(); }, "btn--primary", row);
            cont.name = "continue";
            var again = RowIcon.IconButton("Again", RowKind.Crucible, () => CrucibleLauncher.Launch(true), null, row);
            again.name = "crucible-again";
            Ui.SetEnabled(again, Crucible.CanLaunch(c, App.Save, out _));
            Ui.El("", col).style.height = Space.S48;
            if (DemoMode.Active) root.schedule.Execute(() => App.Router.ResetTo(new HubScreen())).ExecuteLater(1500);
        }

        /// <summary>GG-T1379: a drill's results are one short card, since a drill earns nothing. A win goes on to the next
        /// drill (Next drill ›), or after the last one to Camp, recording the onboarding step either way. A loss, retreat or
        /// escape offers Retry, which replays the drill from its start at no cost; Back does the same.</summary>
        private void BuildDrill(VisualElement col, ContentDb c)
        {
            var drills = c.Campaign?.Tutorial ?? new List<string>();
            int idx = drills.IndexOf(stageId);
            string nextId = idx >= 0 && idx + 1 < drills.Count && c.TryStage(drills[idx + 1]) != null ? drills[idx + 1] : null;

            var title = Ui.Text(report.Victory ? "DRILL CLEARED" : "TRY AGAIN", "t-display shadow", col);
            title.name = "drill-title";
            Fonts.UseDisplay(title);
            title.style.color = report.Victory ? Palette.Gold : new Color(1f, 0.45f, 0.45f);
            string where = idx >= 0 ? $"Drill {idx + 1} of {drills.Count} · {NameOf(c, report, stageId)}" : NameOf(c, report, stageId);
            Ui.Text(where, "t-h2 shadow dim", col).style.unityTextAlign = TextAnchor.MiddleCenter;

            var w = Ui.Panel(col, null, true);
            Ui.Margin(w, top: Space.S48);
            var list = Ui.List(w).contentContainer;
            var t = Ui.Text(!report.Victory ? "Nothing is lost in a drill, Commander. Run us through it again from the start."
                : nextId != null ? "Drill cleared. We're ready for the next, Commander."
                : "That's all we know, Commander. Lead us out.", "kit-text", list);
            t.name = "drill-line";
            t.style.whiteSpace = WhiteSpace.Normal;
            TurnsRow(list);
            ComboRows(c, list);

            var row = Ui.El("row", col);
            row.style.justifyContent = Justify.Center;
            if (!report.Victory)
            {
                onContinue = () => StageLauncher.Launch(stageId, true);
                RowIcon.IconButton("Retry", RowKind.NewGamePlus, Go, "btn--primary", row).name = "retry";
            }
            else if (nextId != null)
            {
                onContinue = () =>
                {
                    Onboarding.Begin(App.Save, OnboardingStep.Drill, idx + 1);
                    App.Persist();
                    StageLauncher.Launch(nextId, true);
                };
                RowIcon.IconButton("Next drill", RowKind.Enter, Go, "btn--primary", row).name = "next";
            }
            else
            {
                onContinue = () =>
                {
                    Onboarding.Begin(App.Save, OnboardingStep.HubIntro);
                    App.Persist();
                    if (App.Settings != null && !App.Settings.DrillsFinished)
                    {
                        App.Settings.DrillsFinished = true; // GG-T1645: Start Campaign's New to GridGame? now defaults to Skip
                        App.SaveSettings();
                    }
                    StoryBeats.AfterResults(true, stageId);
                };
                RowIcon.IconButton("To Camp", RowKind.Station, Go, "btn--primary", row).name = "continue";
            }
            Ui.El("", col).style.height = Space.S48;
            reveals.Add(App.StartCoroutine(DrillCue(title, report.Victory)));

            void Go()
            {
                if (continued) return;
                continued = true;
                onContinue();
            }
        }

        /// <summary>The drill card's cue (Law 5): a chime and a pop on the title once the card is up; Reduce Motion keeps
        /// the sound and drops the pop.</summary>
        private IEnumerator DrillCue(VisualElement title, bool cleared)
        {
            yield return Tween.Wait(Timing.Quick);
            App.Audio.Sfx(cleared ? "battle.powerup" : "ui.cancel");
            if (!App.Settings.ReduceMotion) yield return Tween.Pop(title, 1.15f, Timing.Emphasis);
        }

        private IEnumerator NewBest(VisualElement label)
        {
            yield return Tween.Wait(Timing.Emphasis);
            App.Audio.Sfx("battle.crucible_best");
            label.style.opacity = 1f;
            yield return Tween.Pop(label, 1.25f, 0.3f);
        }

        /// <summary>Items pop in one at a time instead of dumping the whole list at once (owner direction
        /// 2026-10-11: the same tempo and pause that makes a results screen enjoyable to read).</summary>
        /// <summary>Overflow past an item's stack cap auto-sells (US-245); "1 item", not "1 items".</summary>
        private static string WithThe(string name) => name.StartsWith("The ", System.StringComparison.OrdinalIgnoreCase) ? name : "The " + name;

        private static string SoldCount(int n) => n == 1 ? "1 item" : $"{n} items";

        /// <summary>GG-T944 (§2.13.1): how long the battle ran, "N turns" or "N turns, M actions" when several heroes acted
        /// in a turn (<see cref="BattleReport.TurnsLabel"/>), "N hero turns" under HeroTimeline (GG-T1111); the turn mode
        /// itself is never read here.</summary>
        private void TurnsRow(VisualElement list)
        {
            string value = report.TurnsLabel();
            if (value.Length == 0) return;
            Ui.MenuRow("Battle length", value, null, list).WithKindIcon(RowKind.Hero).name = "results-turns";
        }

        /// <summary>GG-T852 (§1.2.8): the battle's best combo (rank and score) and its top supporter (the hero with the most
        /// combo points earned supporting), from <see cref="BattleReport"/>; nothing when no combo ran.</summary>
        private void ComboRows(ContentDb c, VisualElement list)
        {
            if (report.BestComboRank == GridGame.Core.Battle.ComboRank.None) return;
            var best = Ui.MenuRow("Best combo", $"{report.BestComboRank} · {report.BestComboScore} pts", null, list);
            best.name = "results-best-combo";
            best.KindIcon(RowKind.Hero);
            best.Q<Label>("value")?.AddToClassList(report.BestComboRank >= GridGame.Core.Battle.ComboRank.S ? "gold" : "good");
            if (report.TopSupporter == null || report.TopSupporterPoints <= 0) return;
            string who = c.TryClass(report.TopSupporter)?.Name ?? report.TopSupporter;
            Ui.MenuRow("Top supporter", $"{who} · {report.TopSupporterPoints} pts", null, list).WithKindIcon(RowKind.Hero, report.TopSupporter).name = "results-top-supporter";
        }

        /// <summary>GG-T1556 (§22.8): each hero who leveled up this battle, with <see cref="NextAbility.Line"/> and a Learn button
        /// that opens their Board with the route preselected on top of the results, so Back comes back here.</summary>
        private void NextAbilityRows(ContentDb c, VisualElement list)
        {
            var seen = new HashSet<string>();
            foreach (var (classId, _, _) in report.LevelUps)
            {
                if (!seen.Add(classId)) continue;
                var h = App.Save.Hero(classId);
                var next = NextAbility.For(c, h);
                if (next == null) continue;
                var row = Ui.El("row", list);
                row.name = "results-next-ability-" + classId;
                row.style.alignItems = Align.Center;
                var info = Ui.MenuRow(c.TryClass(classId)?.Name ?? classId, NextAbility.Line(c, h), null, row).WithKindIcon(RowKind.Hero, classId);
                info.style.flexGrow = 1; info.style.flexShrink = 1;
                RowIcon.IconButton("Learn", RowKind.Ability, () => App.Router.Push(HeroScreen.Learn(classId, next)), "btn--small", row).name = "results-learn-" + classId;
            }
        }

        /// <summary>At most this many bond rows; the rest fold into one "and N more" line.</summary>
        private const int BondRowsShown = 4;

        /// <summary>GG-T1302 (§25.20): the Bonds block, from <see cref="BattleReport.BondChanges"/>: each pair whose cohesion
        /// grew (both thumbnails, +N) and each hero whose Trust reached a new tier, tier-ups first. The rows come in one at a
        /// time, each with a cue; a tier-up gets a gold "Tier N!", a longer beat and the power-up cue (Law 5). Reduce Motion
        /// keeps the text and sound and drops the slide and pop.</summary>
        private void BondRows(ContentDb c, VisualElement list)
        {
            var bonds = report.BondChanges;
            if (bonds == null) return;
            var rows = new List<(string[] ids, BondChange change)>();
            foreach (var b in bonds.Cohesion)
            {
                var ids = b.Key?.Split('|');
                if (b.After > b.Before && ids != null && ids.Length == 2) rows.Add((ids, b));
            }
            foreach (var b in bonds.Trust)
                if (b.Key != null && Bonds.Tier(b.After) > Bonds.Tier(b.Before)) rows.Add((new[] { b.Key }, b));
            if (rows.Count == 0) return;
            bool TierUp(BondChange b) => Bonds.Tier(b.After) > Bonds.Tier(b.Before);
            rows = rows.OrderBy(r => TierUp(r.change) ? 0 : 1).ThenByDescending(r => r.change.After - r.change.Before).ToList();

            string Name(string id) => c.TryClass(id)?.Name ?? id;
            Ui.Text("Bonds", "small dim", list).name = "results-bonds-head";
            var shown = new List<(VisualElement row, bool tierUp)>();
            foreach (var (ids, b) in rows.Take(BondRowsShown))
            {
                bool up = TierUp(b);
                var row = Ui.El("row", list);
                row.name = "results-bond-" + b.Key.Replace('|', '-');
                row.style.alignItems = Align.Center;
                Ui.Margin(row, top: Space.S8, bottom: Space.S8);
                foreach (var id in ids) Ui.Margin(Ui.Thumb(id, row, Size.Icon), right: Space.S8);
                var text = Ui.El("col", row);
                text.style.flexGrow = 1; text.style.flexShrink = 1;
                Ui.Margin(text, left: Space.S8);
                string what = ids.Length == 2 ? $"{Name(ids[0])} & {Name(ids[1])}" : $"{Name(ids[0])}'s Trust";
                var head = Ui.Text($"{what} +{b.After - b.Before}", up ? "gold" : null, text);
                head.style.whiteSpace = WhiteSpace.Normal;
                Fonts.UseHeavy(head);
                if (up) Ui.Text($"Tier {Bonds.Tier(b.After)}!", "small gold", text);
                row.style.opacity = 0f;
                shown.Add((row, up));
            }
            if (rows.Count > BondRowsShown)
            {
                var more = Ui.Text($"and {rows.Count - BondRowsShown} more", "small dim", list);
                more.name = "results-bonds-more";
                more.style.opacity = 0f;
                shown.Add((more, false));
            }
            reveals.Add(App.StartCoroutine(RevealBonds(shown)));
        }

        private IEnumerator RevealBonds(List<(VisualElement row, bool tierUp)> rows)
        {
            yield return Tween.Wait(Timing.Emphasis);
            bool calm = App.Settings.ReduceMotion;
            foreach (var (row, up) in rows)
            {
                App.Audio.Sfx(up ? "battle.powerup" : "ui.buy", up ? 1f : 0.6f);
                yield return Tween.Run(Timing.Standard, k =>
                {
                    row.style.opacity = k;
                    if (!calm) row.style.translate = new Translate(Mathf.Lerp(-40f, 0f, k), 0);
                }, Tween.EaseOut);
                if (up && !calm) yield return Tween.Pop(row, 1.12f, Timing.Emphasis);
                yield return Tween.Wait(up ? Timing.Emphasis : Timing.Quick);
            }
        }

        /// <summary>GG-T1607: the Summon Tickets this battle earned, when it earned any.</summary>
        private void TicketsRow(VisualElement list)
        {
            if (report.TicketsEarned <= 0) return;
            Ui.MenuRow(TicketText.Plural, "+" + report.TicketsEarned.ToString("N0"), null, list).WithKindIcon(RowKind.Item, Art.ItemIcon("mat_summon_ticket")).name = "results-tickets";
        }

        private void RevealItems(Dictionary<string, int> items, ContentDb c, VisualElement list)
        {
            int i = 0;
            // GG-T514: a Tome (with its "Teach to…" choice) or a Chest (with Open) heads the loot, above the plain drops.
            bool Choice(string id) { var t = c.TryItem(id)?.Type; return t == ItemType.Tome || t == ItemType.Chest; }
            foreach (var kv in items.OrderBy(kv => Choice(kv.Key) ? 0 : 1))
            {
                var it = c.TryItem(kv.Key);
                if (it == null) continue;
                var row = ItemUi.Row(it, $"×{kv.Value}", null, list);
                row.style.opacity = 0f;
                reveals.Add(App.StartCoroutine(RevealRow(row, i * 0.08f)));
                // GG-T301: a looted Tome offers "Teach to…" right here; a Chest can be opened on the spot.
                VisualElement extra = null;
                if (it.Type == ItemType.Tome) extra = TomeUi.Offer(list, it);
                else if (it.Type == ItemType.Chest) extra = ChestBlock(it, c, list);
                if (extra != null)
                {
                    extra.style.opacity = 0f;
                    reveals.Add(App.StartCoroutine(RevealRow(extra, i * 0.08f + 0.04f)));
                }
                i++;
            }
        }

        /// <summary>A looted chest's Open button; what comes out is listed beneath it (Tomes with their own Teach row).</summary>
        private VisualElement ChestBlock(ItemDef chest, ContentDb c, VisualElement list)
        {
            var box = Ui.El("col", list);
            Ui.Margin(box, left: Space.S16, bottom: Space.S8);
            var row = Ui.El("row", box);
            TomeUi.OpenButton(row, chest, (r, haul) =>
            {
                if (!r.Ok || haul == null) return;
                box.Clear();
                var inner = Ui.El("col", box);
                if (haul.Gold > 0) Ui.MenuRow("Gold", "+" + Ui.Gold(haul.Gold), null, inner).WithKindIcon(RowKind.Shop).Q<Label>("value").AddToClassList("gold");
                RevealItems(haul.Items, c, inner);
                if (App.Save.Count(chest.Id) > 0) ChestBlock(chest, c, box);
            });
            return box;
        }

        /// <summary>A fully bonded tamed monster asks to join (GG-T303): Accept takes it into the party (or the reserve when
        /// the party is full) as a domesticated monster; Decline lets it go back to the wild.</summary>
        private void TameOfferBlock(Domestication.Offer offer, ContentDb c, VisualElement list)
        {
            var def = c.TryClass(offer.ClassId);
            if (def == null) return;
            var box = Ui.El("row", list);
            box.name = "tame-offer-" + offer.ClassId;
            box.style.alignItems = Align.Center;
            Ui.Margin(box, top: Space.S8, bottom: Space.S8);
            Ui.Margin(Ui.Thumb(def.Id, box, 96), right: Space.S16);
            var text = Ui.El("col", box);
            text.style.flexGrow = 1; text.style.flexShrink = 1;
            var head = Ui.Text($"{def.Name} wants to join you!", "t-h2 good", text);
            head.style.whiteSpace = WhiteSpace.Normal;
            var sub = Ui.Text($"Lv {offer.Level} - fights on its own with the Pincer keyword. No gear or skills; grows slowly (max Lv {Domestication.LevelCap}).", "small dim", text);
            sub.style.whiteSpace = WhiteSpace.Normal;
            var buttons = Ui.El("row", text);
            Ui.Margin(buttons, top: Space.S8);
            void Show(string msg, bool good)
            {
                buttons.Clear();
                Ui.Text(msg, good ? "small good" : "small dim", buttons).style.whiteSpace = WhiteSpace.Normal;
            }
            void Settle(string msg, bool good)
            {
                tameSettled[offer.ClassId] = (msg, good); // the answer stands across a re-mount
                Show(msg, good);
            }
            if (tameSettled.TryGetValue(offer.ClassId, out var settled)) Show(settled.msg, settled.good);
            else
            {
                RowIcon.IconButton("Accept", RowKind.Confirm, () =>
                {
                    if (tameSettled.ContainsKey(offer.ClassId)) return;
                    var r = Domestication.Accept(c, App.Save, offer);
                    App.Audio.Sfx(r.Ok ? "battle.victory" : "ui.error", r.Ok ? 0.6f : 1f);
                    if (r.Ok) { App.Persist(); Settle(r.Message, true); }
                    else Show(r.Message, false); // a refusal (no room, say) isn't an answer: a re-mount offers it again
                }, "btn--primary", buttons).name = "tame-accept";
                RowIcon.IconButton("Decline", RowKind.Close, () =>
                {
                    if (tameSettled.ContainsKey(offer.ClassId)) return;
                    App.Audio.Sfx("ui.cancel");
                    Settle($"{def.Name} slips back into the wild.", false);
                }, null, buttons).name = "tame-decline";
            }
            box.style.opacity = 0f;
            reveals.Add(App.StartCoroutine(RevealRow(box, 0.2f)));
        }

        /// <summary>The wanderer's talk (GG-T342), once per results screen: true once it has been played.</summary>
        private bool wandererTalked;

        /// <summary>A wanderer standing at the victory (§25.11.2, GG-T342): their trust, the rule in one line, and a button per
        /// party member — the one the player picks talks with them (Wanderers.Begin). The talk plays as a dialogue on top
        /// and comes back here, which then says whether they joined or went their way. Leaving without talking lets them go.</summary>
        private void WandererBlock(Wanderers.Offer offer, ContentDb c, VisualElement list)
        {
            var def = c.TryClass(offer.ClassId);
            if (def == null) return;
            var box = Ui.El("row", list);
            box.name = "wanderer-offer";
            box.style.alignItems = Align.FlexStart;
            Ui.Margin(box, top: Space.S8, bottom: Space.S8);
            Ui.Margin(Ui.Thumb(def.Id, box, 96), right: Space.S16);
            var text = Ui.El("col", box);
            text.style.flexGrow = 1; text.style.flexShrink = 1;
            if (wandererTalked)
            {
                bool joined = App.Save.Hero(def.Id) != null;
                Ui.Text(joined ? $"{def.Name} joins the caravan!" : $"{def.Name} goes their own way.", joined ? "h2 good" : "h2 dim", text).style.whiteSpace = WhiteSpace.Normal;
                if (!joined) Ui.Text("Back to the road. You may meet them again.", "small dim", text);
                return;
            }
            var head = Ui.Text($"The wandering {def.Name} lingers to talk.", "h2", text);
            head.style.whiteSpace = WhiteSpace.Normal;
            head.style.color = Palette.Wanderer;
            var trust = Ui.El("row", text);
            trust.style.alignItems = Align.Center;
            Ui.Margin(Ui.Text($"Trust {offer.Trust}/{offer.TrustNeeded}", "small", trust), right: Space.S16);
            Ui.Gauge(offer.TrustNeeded <= 0 ? 1f : Mathf.Clamp01(offer.Trust / (float)offer.TrustNeeded), trust, 220).name = "wanderer-trust";
            var faction = c.FactionOf(def.Id);
            var factionBlurb = ContentDb.Blurb(c.FactionBlurbs, faction?.Id); // GG-T374: Prose "faction:<id>"
            if (!string.IsNullOrEmpty(factionBlurb))
            {
                var fb = Ui.Text(factionBlurb, "small dim", text);
                fb.name = "wanderer-faction";
                fb.style.whiteSpace = WhiteSpace.Normal;
            }
            var rule = Ui.Text(Wanderers.Outlook(c, offer, null), "small dim", text);
            rule.name = "wanderer-outlook";
            rule.style.whiteSpace = WhiteSpace.Normal;
            Ui.Margin(Ui.Text("Who talks with them?", "small", text), top: Space.S8);
            var buttons = Ui.El("row", text);
            buttons.style.flexWrap = Wrap.Wrap;
            foreach (var id in App.Save.Party)
            {
                var h = App.Save.Hero(id);
                var hd = c.TryClass(id);
                if (h == null || hd == null || h.Domesticated || id == def.Id) continue; // a tamed beast doesn't talk
                string who = id;
                bool kin = Wanderers.Fits(c, def.Id, who);
                var b = RowIcon.IconButton(kin && faction != null ? $"{hd.Name} ({faction.Name})" : hd.Name, RowKind.Talk, () => Talk(offer, who), kin ? "btn--primary btn--small" : "btn--small", buttons);
                b.name = "wanderer-talk-" + who;
                Ui.Margin(b, top: Space.S8, right: Space.S8);
            }
            box.style.opacity = 0f;
            reveals.Add(App.StartCoroutine(RevealRow(box, 0.25f)));
        }

        /// <summary><paramref name="companion"/> talks with the wanderer: the dialogue plays on top; its ending is the rule's.</summary>
        private void Talk(Wanderers.Offer offer, string companion)
        {
            if (wandererTalked) return;
            var run = Wanderers.Begin(App.Content, App.Save, offer, companion);
            if (run == null) { App.Audio.Sfx("ui.error"); return; }
            wandererTalked = true;
            App.Audio.Sfx("ui.confirm");
            App.Router.Push(DialogueScreen.ForRunner(run, null));
        }

        private IEnumerator RevealRow(VisualElement row, float delay)
        {
            yield return Tween.Wait(delay);
            row.style.opacity = 1f;
            yield return Tween.Pop(row, 1.12f, 0.18f);
        }
    }

    internal static class ResultRows
    {
        /// <summary>GG-T749: lead a results row with its kind icon (BIBLE §26.7) and hand the row back, so a chained
        /// <c>.Q&lt;Label&gt;</c> or <c>.name</c> still lands on the row (KindIcon itself returns the icon).</summary>
        public static VisualElement WithKindIcon(this VisualElement row, RowKind kind, Texture2D tex = null) { row.KindIcon(kind, tex); return row; }

        public static VisualElement WithKindIcon(this VisualElement row, RowKind kind, string id) { row.KindIcon(kind, id); return row; }
    }
}
