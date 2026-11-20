using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>§25.18.1 (GG-T455): the Diamond exchange — every Diamond price payable with gold, and the real-money store.</summary>
    public class DiamondExchangeTests
    {
        private static SaveData Save(int diamonds, int gold)
        {
            var s = Progression.NewGame(Real, "Exchange");
            s.Gold = gold;
            if (diamonds > 0) s.AddItem(Crafting.DiamondId, diamonds);
            return s;
        }

        [Test]
        public void The_rate_keeps_the_old_one_diamond_gold_price_for_a_hundred()
        {
            Assert.That(DiamondExchange.GoldFor(100), Is.EqualTo(600));
            Assert.That(DiamondExchange.Label(RestedPool.DiamondRefillPrice), Is.EqualTo($"{RestedPool.DiamondRefillPrice} Diamonds (or {RestedPool.DiamondRefillPrice * DiamondExchange.GoldPerDiamond} G)"));
            Assert.That(Real.Item(Crafting.DiamondId).MaxStack, Is.GreaterThanOrEqualTo(10000), "the bag holds thousands");
        }

        [Test]
        public void Paying_with_enough_diamonds_takes_only_diamonds()
        {
            var s = Save(40, 500);
            Assert.That(DiamondExchange.Quote(s, 25), Is.EqualTo((25, 0)));
            Assert.That(DiamondExchange.Pay(s, 25, out var paid).Ok, Is.True);
            Assert.That(paid, Is.EqualTo((25, 0)));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(15));
            Assert.That(s.Gold, Is.EqualTo(500));
        }

        [Test]
        public void A_diamond_shortfall_is_exchanged_from_gold()
        {
            var s = Save(10, 500);
            Assert.That(DiamondExchange.Quote(s, 25), Is.EqualTo((10, 15 * DiamondExchange.GoldPerDiamond)));
            Assert.That(DiamondExchange.Pay(s, 25, out var paid).Ok, Is.True);
            Assert.That(paid, Is.EqualTo((10, 15 * DiamondExchange.GoldPerDiamond)));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(0));
            Assert.That(s.Gold, Is.EqualTo(500 - 15 * DiamondExchange.GoldPerDiamond));
        }

        [Test]
        public void When_gold_cannot_cover_the_shortfall_nothing_is_taken()
        {
            var s = Save(10, 15 * DiamondExchange.GoldPerDiamond - 1);
            var r = DiamondExchange.Pay(s, 25);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Message, Does.Contain("Diamonds").And.Contain("15 more"));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(10));
            Assert.That(s.Gold, Is.EqualTo(15 * DiamondExchange.GoldPerDiamond - 1));
            Assert.That(DiamondExchange.CanPay(Save(0, 1000), 100, keepGold: 500).Ok, Is.False, "gold kept back for the rest of the action");
        }

        [Test]
        public void Gold_buys_diamonds_at_the_rate_instantly()
        {
            var s = Save(0, 1000);
            Assert.That(DiamondExchange.MaxBuy(Real, s), Is.EqualTo(1000 / DiamondExchange.GoldPerDiamond));
            Assert.That(DiamondExchange.Buy(Real, s, 100).Ok, Is.True);
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(100));
            Assert.That(s.Gold, Is.EqualTo(1000 - 100 * DiamondExchange.GoldPerDiamond));
            Assert.That(DiamondExchange.Buy(Real, s, 1000).Ok, Is.False, "not enough gold");
            Assert.That(DiamondExchange.Buy(Real, s, 0).Ok, Is.False);
            Assert.That(s.Jobs, Is.Empty, "no timed job");
        }

        [Test]
        public void The_store_sells_flat_rate_packs_and_only_the_dev_stub_grants()
        {
            Assert.That(DiamondStore.Packs.Select(p => (p.ProductId, p.Diamonds, p.Price)), Is.EqualTo(new[]
            {
                ("diamonds_100", 100, "$1.00"), ("diamonds_500", 500, "$5.00"), ("diamonds_1000", 1000, "$10.00"), ("diamonds_2500", 2500, "$25.00"),
            }));
            Assert.That(DiamondStore.Packs.All(p => p.Diamonds == p.Cents * DiamondExchange.DiamondsPerDollar / 100), Is.True, "no bonus tiers");

            var s = Save(0, 0);
            Result got = default;
            new UnconnectedDiamondStore().Purchase(Real, s, DiamondStore.Packs[0], r => got = r);
            Assert.That(got.Ok, Is.False);
            Assert.That(got.Message, Does.Contain(DiamondStore.NotConnected));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(0));

            new DevDiamondStore().Purchase(Real, s, DiamondStore.Packs[1], r => got = r);
            Assert.That(got.Ok, Is.True);
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(500));
        }
    }
}
