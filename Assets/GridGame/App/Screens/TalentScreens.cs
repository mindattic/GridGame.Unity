using System;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // The Inn's Respec (§25.6.3, GG-T53), on the skill board since the talent trees retired (GG-T1081, GG-T1088).

    /// <summary>The Inn's Respec: every hero with skill board nodes bought past its Start path, its price (scaling with
    /// level), and a confirm that refunds those nodes as skill points (<see cref="SkillHexProgress.Respec"/>).</summary>
    public sealed class RespecScreen : HubPage
    {
        public override string Id => "respec";
        protected override string Title => "Inn — Respec";
        protected override string Npc => CampNpcs.Innkeeper;

        protected override void BuildBody(VisualElement body)
        {
            var list = Ui.ListPanel(body, "Heroes", "respec-list");
            Ui.Para("Rest a night and forget your training: every skill point a hero spent on Talents comes back. " +
                $"Costs {Ui.Gold(SkillHexProgress.RespecBaseGold)} + {Ui.Gold(SkillHexProgress.RespecGoldPerLevel)} per level. " +
                "The Start path, the starting kit and abilities bought at Learn are kept.", list, "dim");
            string closed = SkillHexProgress.RespecClosed(S);
            if (closed != null) Ui.Para(closed, list, "bad");
            foreach (var h in S.Roster.OrderByDescending(x => S.Party.Contains(x.ClassId)).ThenBy(x => x.ClassId))
            {
                var hero = h;
                int nodes = SkillHexProgress.Refundable(C, hero).Count;
                int owned = HeroBoard.Owned(C, hero).Count;
                int refund = SkillHexProgress.RefundPoints(C, hero);
                int cost = SkillHexProgress.RespecCost(hero);
                string name = C.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
                string value = nodes == 0 ? $"Owns {owned} node{(owned == 1 ? "" : "s")}, none bought" : $"{refund} SP · {Ui.Gold(cost)}";
                bool ok = closed == null && nodes > 0 && S.Gold >= cost;
                // GG-T698: a card (portrait, name, level, status and the class blurb) rather than a bare menu row.
                var card = Ui.Card(list, hero.ClassId, null, out var col, "respec-" + hero.ClassId);
                var title = Ui.Name($"{name}  Lv {Progression.Level(hero)}", col);
                var status = Ui.Caption(value, col);
                GlossaryText.Create(C.TryClass(hero.ClassId)?.Card ?? "", "kit-text dim", col);
                card.AddToClassList("card-tap");
                card.AddToClassList("card--fill");
                if (!ok) title.style.opacity = status.style.opacity = Dim.Row; // GG-T1862: the blurb keeps full colour (4.5:1)
                Ui.Tappable(card, () =>
                {
                    if (!ok) { GameApp.I?.Audio?.Sfx("ui.error"); return; }
                    Ask(C, hero,
                        $"Pay {Ui.Gold(cost)} to give back {name}'s {nodes} Talents node{(nodes == 1 ? "" : "s")} and refund {refund} skill point{(refund == 1 ? "" : "s")}?",
                        r => Done(r), () => SkillHexProgress.Respec(C, S, hero), () => SkillHexProgress.Respec(C, S, hero, true));
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
            var lost = SkillHexProgress.GearLostOnRespec(c, hero);
            if (lost.Count == 0) { Confirm.Show(question, "Pay", () => done(respec())); return; }
            string items = string.Join(", ", lost.Select(g => c.TryItem(g.ItemId)?.Name ?? g.ItemId));
            Confirm.Show($"{question}\n\nAfter the respec this hero can't use: {items}. They'll be unequipped and go to the bag.",
                "Unequip and respec", () => done(respecUnequipping()));
        }
    }
}
