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
    /// The step between the stage list and the battle (GG-T723, owner 2026-11-23: "yes confirmation step, show them whose
    /// going into battle"): the stage (its region's tile, name, region, battle k/n, waves, enemy level, Cleared/New, the
    /// Rested it costs, a hunted bounty) and the party that will fight it (face, name, role, level, HP), with Change party,
    /// Back and Enter. Enter replaces this screen with the battle (<see cref="StageLauncher"/>), so the battle's results
    /// never come back here; the Rested refill retry still lands, since this screen stays current while the refill runs.
    /// Only the stage list comes through here: Next/Retry after a battle, -demo, Test Mode and automation launch directly.
    /// </summary>
    public sealed class StageConfirmScreen : HubPage
    {
        private readonly string stageId;
        public StageConfirmScreen(string stageId) { this.stageId = stageId; }

        public override string Id => "stage-confirm";
        protected override string Title => "Battle";
        protected override string Backdrop => C.ThemeOf(stageId)?.Id ?? HubScreen.ThemeFor(C, S);

        protected override void BuildBody(VisualElement body)
        {
            var st = C.TryStage(stageId);
            if (st == null) { Ui.Empty("That stage is gone.", Ui.Panel(body)); Foot(body, false, "That stage is gone."); return; }
            var theme = C.ThemeOf(stageId);
            var stages = C.CampaignStages();
            int index = stages.FindIndex(x => x.Id == stageId);
            int k = theme == null ? -1 : theme.Stages.IndexOf(stageId);
            bool cleared = S.ClearedStages.Contains(stageId);

            // The stage: the region's tile (badged Cleared / New, as on the list), its name and region, then the facts.
            var info = Ui.Panel(body, null, false, "stage-confirm-stage");
            var top = Ui.El("row", info);
            top.style.alignItems = Align.Center;
            var titles = Ui.El("col grow", top);
            titles.style.flexShrink = 1; titles.style.minWidth = 0;
            Ui.Heading(st.Name, titles, "gold").name = "stage-confirm-name";
            if (theme != null) Ui.Caption(theme.Name, titles, "dim");
            RowIcon.Badge(top.KindIcon(RowKind.Stage, theme?.Id), cleared ? RowKind.Cleared : RowKind.New);
            if (theme != null && k >= 0) Ui.Field("Battle", $"{k + 1} of {theme.Stages.Count}", info);
            Ui.Field("Waves", st.Waves.Count.ToString(), info);
            // The level BattleFactory.Build fights it at (a postgame stage's LevelOverride, else the curve; GG-T70).
            Ui.Field("Enemy level", $"Lv {st.LevelOverride ?? Progression.RecommendedLevel(Math.Max(0, index))}", info);
            Ui.Field("State", cleared ? (k > 0 ? "Cleared · hunt" : "Cleared") : "New", info, cleared ? null : "gold", "stage-confirm-state");
            if (theme != null) Ui.Field("Travel", TravelRules.OnTheRoad(S) ? HubScreen.TravelLine(S) : "Sets out from camp", info, null, "stage-confirm-travel"); // GG-T1786
            if (!RestedPool.Exempt(S)) Ui.Field("Rested", $"Costs {RestedPool.LaunchCost} (have {S.Rested.Points}/{RestedPool.Max})", info, null, "stage-confirm-rested");
            if (Features.Bounties)
            {
                var bounties = Bounties.OnStage(C, S, stageId);
                if (bounties.Count > 0) Ui.Field("Bounty", string.Join(", ", bounties.Select(b => b.Name)), info, "gold", "stage-confirm-bounty");
            }

            // Who is going into battle: the active party, each with face, name, role, level and HP.
            var party = Ui.ListPanel(body, $"Into battle {S.Party.Count}/{Progression.MaxPartySize}", "stage-confirm-party");
            foreach (var id in S.Party)
            {
                var h = S.Hero(id);
                var def = h == null ? null : C.TryClass(id);
                if (def == null) continue;
                var u = Progression.Preview(C, h);
                int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp; // 0 = full (§15.1)
                Ui.Card(party, id, def.Name, out var col, "stage-hero-" + id);
                var meta = Ui.El("row", col);
                meta.style.alignItems = Align.Center;
                Ui.Margin(Ui.RoleBadge(Ui.RoleOf(h, def), meta), left: Space.S0);
                Ui.Margin(Ui.Caption($"Lv {u.Level}  HP {hp}/{u.MaxHp}", meta, hp < u.MaxHp ? "warn" : "dim"), left: Space.S8);
            }
            if (S.Party.Count == 0) Ui.Empty("No one is in the party.", party);
            RowIcon.IconButton("Change party", RowKind.Hero, () => App.Router.Push(new PartyScreen()), "btn--small", Ui.Actions(party)).name = "stage-party-change";

            bool can = index < 0 || Progression.IsStageUnlocked(S, index);
            string why = can ? null : "Locked";
            if (can && S.Party.Count == 0) { can = false; why = "Put someone in the party first."; }
            Foot(body, can, why);
        }

        /// <summary>The footer: Back on the left, the primary Enter on the right, the reason above them when Enter is off.</summary>
        private void Foot(VisualElement body, bool can, string why)
        {
            var foot = Ui.Footer(body, "stage-confirm-footer");
            RowIcon.IconButton("Back", RowKind.Back, () => App.Router.Back(), "btn--small", foot).name = "stage-back";
            var mid = Ui.El("col grow", foot);
            mid.style.flexShrink = 1; mid.style.minWidth = 0;
            if (!can && !string.IsNullOrEmpty(why)) Ui.Para(why, mid, "bad").name = "stage-enter-why";
            var enter = RowIcon.IconButton("Enter", RowKind.Enter, () => StageLauncher.Launch(stageId, true), "btn--primary", foot);
            enter.name = "stage-enter";
            Ui.SetEnabled(enter, can);
        }
    }
}
