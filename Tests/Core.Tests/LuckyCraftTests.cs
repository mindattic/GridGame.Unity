using System;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>§24.6.5 (GG-T235): the Diamond lucky craft, priced in Diamonds and payable with gold through the exchange (GG-T455).</summary>
    public class LuckyCraftTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

        private static RecipeDef Candidate(ContentDb c) =>
            Crafting.Recipes(c, CraftStation.Forge).First(r => r.BaseItem == null && r.ResultCount == 1
                && (c.Item(r.Result).Slot == EquipmentSlot.Weapon || Array.IndexOf(BattleEngine.ArmorSlots, c.Item(r.Result).Slot) >= 0)
                && c.Item(r.Result).Durability > 1 && !Forging.IsSetArmor(c, r));

        private static SaveData Rich(ContentDb c)
        {
            var s = Progression.NewGame(c, "Lucky");
            s.Gold = 100000;
            s.StationXp["blacksmith"] = 9999;
            return s;
        }

        [Test]
        public void The_diamond_is_a_material_no_vendor_sells()
        {
            var d = Real.Item(Crafting.DiamondId);
            Assert.That(d.Type, Is.EqualTo(ItemType.CraftingMaterial));
            Assert.That(d.SoldByVendor, Is.False);
        }

        [Test]
        public void The_old_one_diamond_paywall_job_is_retired_and_an_old_saves_pending_one_is_refunded()
        {
            var c = Real; var s = Rich(c);
            int gold = s.Gold;
            Assert.That(Paywall.Start(s, PaywallKind.LuckyDiamond, Crafting.DiamondId, T0, out _).Ok, Is.False, "the exchange sells Diamonds now");
            Assert.That(s.Gold, Is.EqualTo(gold));
            // A save from before GG-T455 with the 600 g purchase still pending.
            var old = new PaywallJob { Kind = PaywallKind.LuckyDiamond, Arg = Crafting.DiamondId, StartedUtc = T0, Seconds = 1, Paid = 600 };
            s.Jobs.Add(old);
            var r = Paywall.FinishReady(c, s, T0.AddSeconds(5)).Single();
            Assert.That(r.Ok, Is.True);
            Assert.That(s.Gold, Is.EqualTo(gold + 600), "refunded in full");
            Assert.That(s.Jobs, Is.Empty);
        }

        [Test]
        public void A_lucky_craft_raises_the_masterwork_chance_up_to_its_own_cap()
        {
            var c = Real; var s = Progression.NewGame(c, "Lucky2");
            float plain = Crafting.MasterworkChance(c, s);
            float lucky = Crafting.MasterworkChance(c, s, lucky: true);
            Assert.That(lucky, Is.GreaterThan(plain));
            Assert.That(lucky, Is.LessThanOrEqualTo(Crafting.LuckyMaxChance));
            s.StationXp["blacksmith"] = 9999;
            foreach (var h in s.Roster) h.TotalXp = GridGame.Core.Rules.Formulas.TotalXpForLevel(60);
            Assert.That(Crafting.MasterworkChance(c, s, lucky: true), Is.LessThanOrEqualTo(Crafting.LuckyMaxChance));
        }

        [Test]
        public void The_disclosed_lucky_odds_are_the_chances_the_craft_rolls()
        {
            // GG-T503: the Blacksmith shows the Masterwork chance with and without the Diamond before purchase, from the
            // same function the craft rolls against.
            var c = Real; var s = Progression.NewGame(c, "LuckyOdds");
            var (plain, lucky) = Crafting.MasterworkOdds(c, s);
            Assert.That(plain, Is.EqualTo(Crafting.MasterworkChance(c, s)));
            Assert.That(lucky, Is.EqualTo(Crafting.MasterworkChance(c, s, lucky: true)));
            Assert.That(lucky, Is.GreaterThan(plain));
            Assert.That(Crafting.LuckyOddsLine(c, s), Is.EqualTo(
                $"Masterwork chance: {OddsText.Percent(plain)} without a Diamond, {OddsText.Percent(lucky)} with one"));
        }

        [Test]
        public void A_craft_rolls_at_exactly_the_shown_chance_even_when_it_levels_the_forge()
        {
            // The craft's first roll is the Masterwork roll (Rng.Chance: NextFloat() < p), so for every seed the outcome is
            // exactly "first float under the shown chance": the odds shown are the odds rolled, fixed before the craft's own
            // station XP can move them. Station XP just under a level-up makes this craft's XP cross it.
            var c = Real; var r = Candidate(c);
            var probe = new SaveData();
            int threshold = 0;
            while (threshold < 100000 && StationProgress.Level(probe, StationKind.Blacksmith) == 1) probe.StationXp["blacksmith"] = ++threshold;
            foreach (bool lucky in new[] { false, true })
            foreach (int startXp in new[] { 0, Math.Max(0, threshold - 1) })
            for (int seed = 0; seed < 150; seed++)
            {
                var s = Progression.NewGame(c, "LuckyRoll");
                s.Gold = 100000;
                s.StationXp["blacksmith"] = startXp;
                foreach (var ic in r.Inputs) s.AddItem(ic.Item, ic.Count);
                if (lucky) s.AddItem(Crafting.DiamondId, Crafting.LuckyDiamondCost);
                var (plain, luckyChance) = Crafting.MasterworkOdds(c, s);
                float shown = lucky ? luckyChance : plain;
                bool expected = new Rng(seed).Chance(shown);
                var res = Crafting.Craft(c, s, r, rng: new Rng(seed), lucky: lucky);
                Assert.That(res.Ok, Is.True, res.Message);
                Assert.That(Equipment.IsMasterwork(s, r.Result), Is.EqualTo(expected), $"lucky={lucky} xp={startXp} seed={seed} shown={shown}");
            }
        }

        [Test]
        public void A_lucky_craft_needs_its_diamond_price_and_spends_exactly_that()
        {
            var c = Real; var s = Rich(c);
            var r = Candidate(c);
            foreach (var ic in r.Inputs) s.AddItem(ic.Item, ic.Count);
            s.Gold = r.Gold + DiamondExchange.GoldFor(Crafting.LuckyDiamondCost) - 1; // one gold short of exchanging the Diamonds
            Assert.That(Crafting.CanLucky(c, s, r), Is.False);
            Assert.That(Crafting.Craft(c, s, r, rng: new Rng(1), lucky: true).Ok, Is.False, "no Diamonds and too little gold");
            Assert.That(r.Inputs.All(ic => s.Count(ic.Item) >= ic.Count), Is.True, "a refused lucky craft consumes nothing");
            Assert.That(s.Gold, Is.EqualTo(r.Gold + DiamondExchange.GoldFor(Crafting.LuckyDiamondCost) - 1));

            s.AddItem(Crafting.DiamondId, Crafting.LuckyDiamondCost + 1);
            int gold = s.Gold;
            Assert.That(Crafting.Craft(c, s, r, rng: new Rng(1), lucky: true).Ok, Is.True);
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(1));
            Assert.That(s.Gold, Is.EqualTo(gold - r.Gold), "Diamonds, not gold, for the luck");
        }

        [Test]
        public void A_lucky_craft_with_no_diamonds_exchanges_gold_on_top_of_the_recipe()
        {
            var c = Real; var s = Rich(c);
            var r = Candidate(c);
            foreach (var ic in r.Inputs) s.AddItem(ic.Item, ic.Count);
            s.Gold = r.Gold + DiamondExchange.GoldFor(Crafting.LuckyDiamondCost);
            Assert.That(Crafting.CanLucky(c, s, r), Is.True);
            Assert.That(Crafting.Craft(c, s, r, rng: new Rng(1), lucky: true).Ok, Is.True);
            Assert.That(s.Gold, Is.EqualTo(0));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(0));
        }

        [Test]
        public void Over_the_same_seeds_lucky_crafts_roll_more_masterworks()
        {
            var c = Real; var r = Candidate(c);
            int Count(bool lucky)
            {
                int n = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    var s = Progression.NewGame(c, "Seeds");
                    s.Gold = 100000;
                    foreach (var ic in r.Inputs) s.AddItem(ic.Item, ic.Count);
                    if (lucky) s.AddItem(Crafting.DiamondId, Crafting.LuckyDiamondCost);
                    var res = Crafting.Craft(c, s, r, rng: new Rng(seed), lucky: lucky);
                    Assert.That(res.Ok, Is.True, res.Message);
                    if (Equipment.IsMasterwork(s, r.Result)) n++;
                }
                return n;
            }
            int plain = Count(false), lucky = Count(true);
            Assert.That(lucky, Is.GreaterThan(plain * 2), $"lucky {lucky} vs plain {plain}");
        }

        [Test]
        public void The_camp_routine_never_buys_diamonds()
        {
            var c = Real; var s = Rich(c);
            CampRoutine.Run(c, s);
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(0));
            Assert.That(s.Jobs.Any(j => j.Kind == PaywallKind.LuckyDiamond), Is.False);
        }
    }
}
