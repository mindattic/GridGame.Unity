using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed class CrucibleScreen : HubPage
    {
        public override string Id => "crucible";
        protected override string Title => "Crucible";
        protected override string Backdrop => HubScreen.ThemeFor(C, S);

        /// <summary>The Camp row's value: the save's best run in <paramref name="mode"/> (the mode the next run plays,
        /// GG-T945: records are kept per turn mode), or an invitation before the first one.</summary>
        public static string RecordText(SaveData s, TurnMode mode) => CampMenu.CrucibleRecord(s, mode);

        protected override void BuildBody(VisualElement body)
        {
            // GG-T454 (the menu kit): the rules and the record in one scrolling list, the team under it, and the primary action
            // in the footer at the bottom of the screen, where the thumb is (POL-C4); nothing can push Enter off a phone (GG-T439).
            var list = Ui.ListPanel(body, "The Crucible", "crucible-intro");
            Ui.Para("A horde that never ends: wave after wave, each one harder, until your team falls or you leave. Wounds, mana and items carry from wave to wave, with no camp between. " +
                    "Every wave you clear banks gold, and every coin, drop and point of experience earned is <b>kept</b>, even in defeat. The campaign doesn't move.", list);
            int start = Crucible.PartyLevel(S);
            Ui.Field("First wave", $"Lv {start}, +{Crucible.LevelStep} each wave", list);
            Ui.Field("Horde", $"{Crucible.FirstWaveEnemies}→{Crucible.MaxWaveEnemies} foes, elites from wave {BattleConfig.CrucibleFirstEliteWave + 1}", list);
            Ui.Field("Pay", $"{Ui.Gold(Crucible.WaveGold(0))}, +{BattleConfig.CrucibleWaveGoldPerWave} G a wave", list, "gold");
            var fams = Crucible.Families(C, S);
            Ui.Field("Foes", $"{fams.Count} famil{(fams.Count == 1 ? "y" : "ies")} met", list);
            if (fams.Count > 0)
            {
                var names = Ui.Para(string.Join(", ", fams.Select(f => f[0].Name)), list, "dim");
                names.style.display = DisplayStyle.None;
                Button toggle = null;
                toggle = Ui.Button("Show names", () =>
                {
                    bool show = names.style.display == DisplayStyle.None;
                    names.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    toggle.text = show ? "Hide names" : "Show names";
                }, "btn--small", list);
                toggle.name = "crucible-foes-toggle";
            }
            var rec = Ui.El("col", list);
            rec.name = "crucible-record";
            Ui.Section("Record", rec);
            bool ran = S.Crucible.Runs > 0;
            var mode = TurnModeSetting.Resolve(S, App.Settings); // GG-T945: the record of the mode the next run plays
            Ui.Field("Best", ran ? $"{S.Crucible.BestFor(mode)} waves · {Ui.Gold(S.Crucible.BestGoldFor(mode))}" : "None yet", rec, "gold");
            Ui.Field("Runs", ran ? $"{S.Crucible.Runs} · {S.Crucible.TotalWaves} waves" : "0", rec);

            // GG-T430: you pick one team before the run (the active party, up to the party-size cap) and it fights
            // every wave; "Change team" swaps it on the Party page and comes straight back here.
            var team = Ui.Panel(body, $"Your team {S.Party.Count}/{Progression.MaxPartySize}", false, "crucible-team");
            // GG-T658: the faces share the row evenly (each cell grows from the avatar's width), with the edge equal to
            // the gap (row padding + cell margins = 24 each), instead of piling up at the left edge (POL-C8).
            var faces = Ui.El("row", team);
            faces.style.flexWrap = Wrap.Wrap;
            faces.style.paddingLeft = faces.style.paddingRight = Space.TargetHalfGap;
            foreach (var id in S.Party)
            {
                var h = S.Hero(id);
                if (h == null) continue;
                var cell = Ui.El("col", faces);
                cell.style.alignItems = Align.Center;
                cell.style.flexGrow = 1; cell.style.flexShrink = 0; cell.style.flexBasis = Kit.Avatar;
                Ui.Margin(cell, left: Space.TargetHalfGap, right: Space.TargetHalfGap);
                Ui.Thumb(id, cell, Kit.Avatar).style.marginRight = Space.S0;
                Ui.Caption($"Lv {Progression.Level(h)}", cell);
            }
            RowIcon.IconButton("Change team", RowKind.Hero, () => App.Router.Push(new PartyScreen()), "btn--small", Ui.Actions(team)).name = "crucible-team-change"; // GG-T750

            bool can = Crucible.CanLaunch(C, S, out var why);
            var foot = Ui.Footer(body, "crucible-footer");
            var costCol = Ui.El("col grow", foot);
            if (!can) Ui.Para(why, costCol, "bad");
            Ui.Caption($"Costs {RestedPool.LaunchCost} Rested: the energy for a run, up to {RestedPool.Max}, +1 every {(int)RestedPool.RegenEvery.TotalMinutes} min", costCol, "dim");
            var enter = RowIcon.IconButton("Enter the Crucible", RowKind.Enter, () => CrucibleLauncher.Launch(false), "btn--primary", foot);
            enter.name = "crucible-enter";
            Ui.SetEnabled(enter, can);
        }
    }
}
