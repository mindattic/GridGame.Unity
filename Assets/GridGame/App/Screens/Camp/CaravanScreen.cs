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
    public sealed class CaravanScreen : HubPage
    {
        public override string Id => "caravan";
        protected override string Title => "Caravan";
        protected override string Backdrop => "desert";

        protected override void BuildBody(VisualElement body)
        {
            var head = Ui.Panel(body);
            Ui.Para("The caravan travels with the party — its wagons are the shops between battles. Upgrade it for the long road.", head);
            BarkLine(C, S, head);
            var list = Ui.ListPanel(body);
            foreach (var def in C.Caravan.Values.Where(x => Features.OffersCaravan(x.Id)))
            {
                var d = def;
                int lv = CaravanUpgrades.Level(S, d.Id);
                var next = CaravanUpgrades.NextLevel(C, S, d.Id);
                var card = Ui.Card(list, null, null, out var col, "caravan-" + d.Id);
                // GG-T746 (§26.7): the upgrade's pixel icon, else the Station emblem, leads the card.
                var ic = card.KindIcon(RowKind.Station, Art.ItemIcon(d.Icon));
                ic.style.width = ic.style.height = Kit.Thumb;
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var top = Ui.El("row", col);
                Ui.Name(d.Name, top, "grow");
                Ui.Caption($"Lv {lv}/{d.Levels.Count}", top, "gold");
                var crewBlurb = ContentDb.Blurb(C.CrewBlurbs, d.Id); // GG-T374: Prose "character:<crew>"
                if (lv > 0) Ui.Para("Now: " + d.Levels[lv - 1].Text, col, "good");
                if (next != null) Ui.Para("Next: " + next.Text, col);
                // GG-T1868: the keeper's lore and the full description are one tap (or a hold) away, not printed on every row.
                Func<VisualElement, bool> read = sheet =>
                {
                    Ui.Name(d.Name, sheet);
                    if (!string.IsNullOrEmpty(d.Crew)) Ui.Caption("Crew: " + CaravanUpgrades.CrewName(d), sheet, "gold");
                    if (!string.IsNullOrEmpty(crewBlurb)) Ui.Para(crewBlurb, sheet, "dim");
                    Ui.Para(d.Description, sheet, "dim");
                    return true;
                };
                InspectCard.Attach(ic, read, tap: true);
                InspectCard.Attach(top, read, tap: true);
                var b = RowIcon.IconButton(next == null ? "Maxed" : Ui.Gold(next.Cost), RowKind.Confirm, () => Confirm.Show($"Buy {d.Name} ({next.Text}) for {Ui.Gold(next.Cost)}?", "Buy", () => Done(CaravanUpgrades.Buy(C, S, d.Id), cue: "camp.caravan")), "btn--small", Ui.Actions(col), confirmTap: false);
                b.name = "caravan-buy-" + d.Id;
                Ui.SetEnabled(b, next != null && S.Gold >= next.Cost);
            }
        }

        private static int barkTurn = Environment.TickCount & 0xFFFF;

        /// <summary>One rotating crew bark (GG-T75, §25.12.1): a quoted line and who said it — flavor only, nothing to
        /// tap or answer. Each call advances the rotation, so every rebuild of Camp or the Caravan shows the next line.</summary>
        public static VisualElement BarkLine(ContentDb c, SaveData s, VisualElement parent)
        {
            var (crew, line) = CaravanUpgrades.Bark(c, s, barkTurn++);
            if (line == null) return null;
            var box = Ui.El("col", parent);
            box.name = "caravan-bark";
            Ui.Para($"\"{line}\"", box, "gold");
            Ui.Caption("— " + CaravanUpgrades.CrewName(crew), box, "dim");
            return box;
        }
    }
}
