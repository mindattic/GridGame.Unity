using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T952 (BIBLE §14.25): the Ironbark Golem, a late-game foe that is both Heavy and plated, so the answer is a
    /// Float to move it into a pincer and an armor breaker (Flay's Shredded); both must be in the player's hands first.</summary>
    public class IronbarkGolemTests
    {
        private const string Golem = "ironbarkgolem";

        [Test]
        public void The_ironbark_golem_is_heavy_plated_and_hits_hard()
        {
            var g = Real.Classes[Golem];
            Assert.That(g.Team, Is.EqualTo(Team.Enemy));
            Assert.That(g.Heavy, Is.True, "immovable without Float");
            Assert.That(g.ArmorPlate, Is.GreaterThanOrEqualTo(30), "plated like a templar");
            Assert.That(g.HasTag("Sentient"), Is.False, "mindless: it never pincers, it out-trades");
            Assert.That(g.BaseStats.Strength, Is.GreaterThan(Real.Classes["treegolem06"].BaseStats.Strength));
            Assert.That(g.Abilities, Does.Contain("counter_attack"), "trading blows with it costs");
        }

        [Test]
        public void Float_and_an_armor_breaker_join_before_the_first_ironbark_wave()
        {
            var order = Real.CampaignStages().Select(s => s.Id).ToList();
            int GuestAt(string cls) => order.FindIndex(id => Real.Stages[id].Waves.Any(w => w.Guests.Any(x => x.Class == cls)));
            var golemStages = order.Where(id => Real.Stages[id].Waves.Any(w => w.Enemies.Any(e => e.Class == Golem))).ToList();

            Assert.That(golemStages.Count, Is.GreaterThanOrEqualTo(3), "a few stages, rising in level");
            Assert.That(Real.Classes["drifter"].Abilities, Does.Contain("drifter_float"));
            Assert.That(Real.Classes["ripper"].Abilities, Does.Contain("ripper_flay"));
            int first = order.IndexOf(golemStages[0]);
            Assert.That(GuestAt("drifter"), Is.InRange(0, first - 1), "the Drifter (Float) joins earlier");
            Assert.That(GuestAt("ripper"), Is.InRange(0, first - 1), "the Ripper (Flay) joins earlier");
            foreach (var id in golemStages)
                Assert.That(Real.Stages[id].LevelOverride ?? 0, Is.GreaterThanOrEqualTo(30), id + " is late game");
        }
    }
}
