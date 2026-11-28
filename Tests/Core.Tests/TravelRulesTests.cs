using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1785: travel rules (orbs and wounds carry on the road, a loss camps, camp rest heals free, Medic Wagon).</summary>
    public class TravelRulesTests
    {
        static SaveData Camped()
        {
            var s = Progression.NewGame(Real, "Travel");
            s.World.Camped = true;
            return s;
        }

        static HeroSave Wound(SaveData s, int hp = 1)
        {
            var h = s.Hero(s.Party[0]);
            h.Hp = hp;
            return h;
        }

        [Test]
        public void Setting_out_from_camp_puts_the_caravan_on_the_road()
        {
            var s = Camped();
            Assert.That(TravelRules.OnTheRoad(s), Is.False);
            TravelRules.SetOut(s);
            Assert.That(TravelRules.OnTheRoad(s), Is.True);
            Assert.That(TravelRules.StartMana(s), Is.EqualTo(0));
        }

        [Test]
        public void A_win_on_the_road_carries_mana_and_wounds_into_the_next_battle()
        {
            var s = Camped();
            TravelRules.SetOut(s);
            var h = Wound(s, 5);
            int carried = TravelRules.AfterBattle(Real, s, victory: true, manaLeft: 340, out _);
            Assert.That(carried, Is.EqualTo(340));
            Assert.That(s.World.CarriedMana, Is.EqualTo(340));
            Assert.That(TravelRules.StartMana(s), Is.EqualTo(340));
            Assert.That(TravelRules.OnTheRoad(s), Is.True);
            Assert.That(h.Hp, Is.EqualTo(5), "no Medic Wagon: the wound stays");
        }

        [Test]
        public void Carried_mana_arrives_as_points_in_the_party_pool()
        {
            // §3.1.0: carried mana is a whole grant of its points to the party pool, undamped.
            const int points = BattleConfig.ManaPointsPerOrb;
            var setup = new BattleSetup { Stage = Stage(("tank", 2, 2)), Seed = 7, UnlockedSlots = 5, StartMana = points };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(2, 3), Bar = { "fireball", "heal" } });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(5, 7), Bar = { "heal" } });
            var b = BattleEngine.Create(Mini(), setup, new List<BattleEvent>());
            var k = Hero(b, "knight");
            var c = Hero(b, "cleric");
            Assert.That(b.PartyMana, Is.EqualTo(points));
            Assert.That(b.ManaIn(k.Id, 0), Is.EqualTo(System.Math.Min(points, b.ManaFull(k, 0))));
            Assert.That(b.ManaIn(k.Id, 1), Is.EqualTo(System.Math.Min(points, b.ManaFull(k, 1))));
            Assert.That(b.ManaIn(c.Id, 0), Is.EqualTo(System.Math.Min(points, b.ManaFull(c, 0))));
            Assert.That(b.ManaIn(k.Id, 0), Is.LessThan(b.ManaFull(k, 0)), "a 2-cost Fireball is half full from one orb");
            Assert.That(b.SpellReady(c, 0), Is.True, "a 1-cost Heal is full from one orb");
        }

        [Test]
        public void A_defeat_on_the_road_falls_back_to_camp_with_no_mana()
        {
            var s = Camped();
            TravelRules.SetOut(s);
            TravelRules.AfterBattle(Real, s, true, 400, out _);
            int carried = TravelRules.AfterBattle(Real, s, victory: false, manaLeft: 200, out _);
            Assert.That(carried, Is.EqualTo(0));
            Assert.That(s.World.Camped, Is.True);
            Assert.That(s.World.CarriedMana, Is.EqualTo(0));
            Assert.That(TravelRules.StartMana(s), Is.EqualTo(0));
        }

        [Test]
        public void A_battle_at_camp_changes_nothing()
        {
            var s = Camped();
            Assert.That(TravelRules.AfterBattle(Real, s, true, 300, out _), Is.EqualTo(0));
            Assert.That(s.World.Camped, Is.True);
            Assert.That(s.World.CarriedMana, Is.EqualTo(0));
        }

        [Test]
        public void Camp_rest_heals_the_whole_roster_for_free()
        {
            var s = Camped();
            foreach (var h in s.Roster) h.Hp = 1;
            int gold = s.Gold;
            var r = CampRestService.Rest(s, out var outcome);
            Assert.That(r.Ok, Is.True);
            Assert.That(s.Roster.All(h => h.Hp == 0), Is.True);
            Assert.That(s.Gold, Is.EqualTo(gold));
            Assert.That(outcome.Ate, Is.True);
        }

        [Test]
        public void Camp_rest_is_refused_on_the_road()
        {
            var s = Camped();
            TravelRules.SetOut(s);
            var h = Wound(s);
            Assert.That(CampRestService.Rest(s, out _).Ok, Is.False);
            Assert.That(h.Hp, Is.EqualTo(1));
        }

        [Test]
        public void Camp_rest_without_food_heals_but_gives_no_well_rested()
        {
            var s = Camped();
            s.World.Food = 0;
            Wound(s);
            Assert.That(CampRestService.Rest(s, out var outcome).Ok, Is.True);
            Assert.That(outcome.Ate, Is.False);
            Assert.That(outcome.WellRested, Is.False);
            Assert.That(s.Roster.All(h => h.Hp == 0), Is.True);
        }

        [Test]
        public void Medic_wagon_patches_a_share_of_missing_hp_after_a_win_on_the_road()
        {
            var s = Camped();
            s.Caravan["medic_wagon"] = 1;
            float share = MedicWagon.Share(Real, s);
            Assume.That(share, Is.GreaterThan(0f).And.LessThan(1f), "caravan.json medic_wagon Value is a 0..1 share");
            TravelRules.SetOut(s);
            var h = Wound(s, 1);
            int max = Progression.Preview(Real, h).MaxHp;
            int missing = max - 1;
            TravelRules.AfterBattle(Real, s, true, 0, out int healed);
            int expected = System.Math.Min(missing, (int)System.Math.Ceiling(missing * share));
            Assert.That(healed, Is.EqualTo(expected));
            Assert.That(h.Hp, Is.EqualTo(1 + expected >= max ? 0 : 1 + expected));
        }

        [Test]
        public void Medic_wagon_never_heals_without_the_upgrade()
        {
            var s = Camped();
            Assert.That(MedicWagon.Share(Real, s), Is.EqualTo(0f));
            var h = Wound(s, 1);
            Assert.That(MedicWagon.Patch(Real, s), Is.EqualTo(0));
            Assert.That(h.Hp, Is.EqualTo(1));
        }
    }
}
