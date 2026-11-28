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
    /// <summary>The Hero screen's Party tab (§25.5, GG-T435): the active party first, then the reserve, then the party's
    /// cohesion pairs (GG-T1970). Every hero is a card; tap to select it, tap again to swap it in or out.</summary>
    internal sealed class PartyTab : HeroTab
    {
        public override string Id => "party";
        public override bool ShowsPicker => false;

        public override void BuildBody(VisualElement body)
        {
            var list = Ui.ListPanel(body, $"Active party {S.Party.Count}/{Progression.MaxPartySize}", "party-list");
            foreach (var h in S.Party.Select(S.Hero).Where(h => h != null)) HeroCard(list, h, true);
            var reserve = S.Roster.Where(h => !S.Party.Contains(h.ClassId)).OrderBy(h => h.ClassId).ToList();
            if (reserve.Count > 0)
            {
                Ui.Section("Reserve", list).name = "party-reserve";
                foreach (var h in reserve) HeroCard(list, h, false);
            }
            BondsUi.PairLines(C, S, list);
        }

        private void HeroCard(VisualElement list, HeroSave h, bool active)
        {
            var u = Progression.Preview(C, h);
            var card = Ui.Card(list, h.ClassId, null, out var col);
            var nameRow = Ui.El("row", col);
            Ui.Name(C.Class(h.ClassId).Name, nameRow);
            Ui.RoleBadge(Ui.RoleOf(h, C.Class(h.ClassId)), nameRow);
            MercenaryCamp.Tag(S, h.ClassId, nameRow);
            var facts = Ui.El("row", col);
            facts.name = "party-facts";
            var lv = RowIcon.Line("stat-level", facts);
            Ui.Caption($"Lv {u.Level}", lv);
            Ui.Margin(lv, right: Space.S24);
            Ui.Caption($"HP {u.MaxHp}", RowIcon.Line("stat-hp", facts));
            GlossaryText.Create(C.Class(h.ClassId).Card ?? "", "kit-text dim", col);
            if (active) PartyBoards.Row(C, S, h, col); // GG-T1891: the bar and a tap-through to the board, in place of the mini boards
            card.AddToClassList("card-tap");
            Ui.Tappable(card, () => Done(Party.Toggle(S, h.ClassId)), "ui.confirm", confirmTap: true);
        }
    }
}
