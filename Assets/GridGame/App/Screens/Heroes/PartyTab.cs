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
    /// <summary>The Hero screen's Party tab (§25.5, GG-T435): every roster hero as a card; tap to swap in or out.</summary>
    internal sealed class PartyTab : HeroTab
    {
        public override string Id => "party";
        public override bool ShowsPicker => false;

        public override void BuildBody(VisualElement body)
        {
            BondsUi.CohesionGrid(C, S, body); // GG-T1299
            var list = Ui.ListPanel(body, $"Active party {S.Party.Count}/{Progression.MaxPartySize} — tap to swap", "party-list");
            foreach (var h in S.Roster.OrderByDescending(x => S.Party.Contains(x.ClassId)).ThenBy(x => x.ClassId))
            {
                var hero = h;
                var u = Progression.Preview(C, h);
                bool active = S.Party.Contains(h.ClassId);
                var card = Ui.Card(list, h.ClassId, null, out var col);
                card.EnableInClassList("card--selected", active);
                var nameRow = Ui.El("row", col);
                Ui.Name(C.Class(h.ClassId).Name, nameRow);
                Ui.RoleBadge(Ui.RoleOf(h, C.Class(h.ClassId)), nameRow);
                MercenaryCamp.Tag(S, h.ClassId, nameRow);
                Ui.Caption($"Lv {u.Level}   HP {u.MaxHp}   {(active ? "<color=#78EC96>Active</color>" : "Reserve")}", col);
                GlossaryText.Create(C.Class(h.ClassId).Card ?? "", "kit-text dim", col);
                PartyBoards.Row(C, S, h, col); // GG-T1891: the bar and a tap-through to the board, in place of the mini boards
                card.AddToClassList("card-tap");
                Ui.Tappable(card, () => Done(Party.Toggle(S, hero.ClassId)), "ui.confirm", confirmTap: true);
            }
        }
    }
}
