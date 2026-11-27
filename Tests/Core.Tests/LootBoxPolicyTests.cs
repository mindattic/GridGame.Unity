using System;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>§25.10.4 (GG-T1433): paid random items by storefront country; earned Summon Tickets stay allowed.</summary>
    public class LootBoxPolicyTests
    {
        private static BannerDef Banner => Gacha.Standard(Real);

        [TearDown]
        public void Reset()
        {
            LootBoxPolicy.Country = null;
            DiamondExchange.Wallet = null;
        }

        private static SaveData Funded(string name, int tickets, int shards)
        {
            DiamondExchange.Wallet = null;
            var s = Progression.NewGame(Real, name);
            s.Summon ??= new SummonState();
            s.Summon.Tickets = tickets;
            DiamondExchange.Add(s, -DiamondExchange.Have(s));
            DiamondExchange.Add(s, shards);
            s.Gold = 100000;
            return s;
        }

        [Test]
        public void Belgium_and_the_Netherlands_are_blocked_by_default_and_an_unknown_country_is_allowed()
        {
            Assert.That(LootBoxPolicy.DefaultBlocked, Is.EquivalentTo(new[] { "BE", "NL" }));
            LootBoxPolicy.Country = null;
            Assert.That(LootBoxPolicy.PaidAllowed, Is.True);
            LootBoxPolicy.Country = " be ";
            Assert.That(LootBoxPolicy.Country, Is.EqualTo("BE"));
            Assert.That(LootBoxPolicy.PaidAllowed, Is.False);
            LootBoxPolicy.Country = "NL";
            Assert.That(LootBoxPolicy.PaidAllowed, Is.False);
            LootBoxPolicy.Country = "US";
            Assert.That(LootBoxPolicy.PaidAllowed, Is.True);
        }

        [Test]
        public void Where_blocked_a_Shard_pull_is_refused_and_takes_nothing()
        {
            LootBoxPolicy.Country = "BE";
            var s = Funded("LB1", 0, 100000);
            var r = Gacha.Pull(Real, s, Banner, 1, DateTime.UtcNow, out var job);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Message, Is.EqualTo(LootBoxPolicy.OffLine));
            Assert.That(job, Is.Null);
            Assert.That(DiamondExchange.Have(s), Is.EqualTo(100000));
        }

        [Test]
        public void Where_blocked_a_pull_paid_wholly_in_earned_Tickets_is_allowed_and_one_short_is_not()
        {
            LootBoxPolicy.Country = "NL";
            var banner = Banner;
            var s = Funded("LB2", banner.PullTickets * banner.MultiCount, 0);
            Assert.That(LootBoxPolicy.Allows(s, banner, 1).Ok, Is.True);
            Assert.That(LootBoxPolicy.Allows(s, banner, banner.MultiCount).Ok, Is.True);
            s.Summon.Tickets--;
            Assert.That(LootBoxPolicy.Allows(s, banner, banner.MultiCount).Ok, Is.False, "the last pull would take Shards");
        }

        [Test]
        public void Where_blocked_the_lucky_craft_is_off()
        {
            LootBoxPolicy.Country = "BE";
            Assert.That(LootBoxPolicy.AllowsLuckyCraft().Ok, Is.False);
            LootBoxPolicy.Country = "DE";
            Assert.That(LootBoxPolicy.AllowsLuckyCraft().Ok, Is.True);
        }
    }
}
