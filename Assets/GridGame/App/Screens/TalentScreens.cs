using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // Talents: the Inn's Respec (§25.6.3, GG-T53). The node-graph tab was retired with the skill hexagon (GG-T1034).

    /// <summary>The Inn's Respec (§25.6.3, GG-T53): every hero with talent points spent, its price (scaling with level),
    /// and a confirm that refunds all of that hero's points.</summary>
    public sealed class RespecScreen : HubPage
    {
        public override string Id => "respec";
        protected override string Title => "Inn — Respec";
        protected override string Npc => CampNpcs.Innkeeper;

        protected override void BuildBody(VisualElement body)
        {
            var list = Ui.ListPanel(body, "Heroes", "respec-list");
            Ui.Para($"Rest a night and forget your training: every skill point a hero spent on talents comes back. " +
                $"Costs {Ui.Gold(Talents.RespecBaseGold)} + {Ui.Gold(Talents.RespecGoldPerLevel)} per level. " +
                "Abilities from a hero's starting kit or bought at Learn are kept.", list, "dim");
            string closed = Talents.RespecClosed(S);
            if (closed != null) Ui.Para(closed, list, "bad");
            foreach (var h in S.Roster.OrderByDescending(x => S.Party.Contains(x.ClassId)).ThenBy(x => x.ClassId))
            {
                var hero = h;
                int spent = Talents.PointsSpent(C, hero);
                int cost = Talents.RespecCost(hero);
                string name = C.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
                string value = spent == 0 ? "No talents bought" : $"{spent} pts · {Ui.Gold(cost)}";
                bool ok = closed == null && spent > 0 && S.Gold >= cost;
                // GG-T698: a card (portrait, name, level, status and the class blurb) rather than a bare menu row, so a short
                // roster's cards carry enough to fill the panel without a dead band between them (POL-C9).
                var card = Ui.Card(list, hero.ClassId, null, out var col, "respec-" + hero.ClassId);
                Ui.Name($"{name}  Lv {Progression.Level(hero)}", col);
                Ui.Caption(value, col);
                GlossaryText.Create(C.TryClass(hero.ClassId)?.Card ?? "", "kit-text dim", col);
                card.AddToClassList("card-tap");
                if (!ok) card.style.opacity = 0.55f;
                Ui.Tappable(card, () =>
                {
                    if (!ok) { GameApp.I?.Audio?.Sfx("ui.error"); return; }
                    Ask(C, hero,
                        $"Pay {Ui.Gold(cost)} to unlearn {name}'s talents and refund {spent} skill point{(spent == 1 ? "" : "s")}?",
                        r => Done(r), () => Talents.Respec(C, S, hero), () => Talents.Respec(C, S, hero, true));
                }, "ui.select");
            }
        }

        /// <summary>
        /// GG-T566 (owner: "warn and ask user"): the respec confirm. When the respec would forget a proficiency the hero's
        /// equipped weapon/armor needs, the question lists those pieces and the yes is "Unequip and respec" (they go to the
        /// bag); Cancel leaves everything as it was. Otherwise it is the plain "Pay" confirm.
        /// </summary>
        public static void Ask(ContentDb c, HeroSave hero, string question, Action<Result> done, Func<Result> respec, Func<Result> respecUnequipping)
        {
            var lost = Talents.GearLostOnRespec(c, hero);
            if (lost.Count == 0) { Confirm.Show(question, "Pay", () => done(respec())); return; }
            string items = string.Join(", ", lost.Select(g => c.TryItem(g.ItemId)?.Name ?? g.ItemId));
            Confirm.Show($"{question}\n\nAfter the respec this hero can't use: {items}. They'll be unequipped and go to the bag.",
                "Unequip and respec", () => done(respecUnequipping()));
        }
    }
}
