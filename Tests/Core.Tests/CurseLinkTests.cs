using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Curse Link (§7, §8.1, GG-T873): the Hexer's spell binds the picked foe to its farthest teammate with Linked;
    /// half of a blow on one echoes to the other, never back, and the link dies with either foe.</summary>
    public class CurseLinkTests
    {
        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        /// <summary>Mini plus the real Linked, an instant link spell and a plain 10-damage dart.</summary>
        private static ContentDb WithLink()
        {
            var db = Mini();
            db.Buffs["linked"] = Real.Buffs["linked"];
            db.Abilities["link_hex"] = new AbilityDef { Id = "link_hex", Name = "Link Hex", Kind = AbilityKind.Spell, Cost = 1, DamageType = DamageType.Dark, ApplyBuff = "linked", Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            db.Abilities["dart"] = new AbilityDef { Id = "dart", Name = "Dart", Kind = AbilityKind.Spell, Cost = 1, BaseDamage = 10, DamageType = DamageType.Arcane, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            db.Classes["witch"].ChargeAbility = null; // keep the board quiet: these tests are about the link
            return db;
        }

        private static List<BattleEvent> Cast(BattleState s, Unit caster, int slot, Unit target)
        {
            GrantMana(s, 1);
            caster.ActedThisWindow = false; // one cast a window (GG-T308)
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = caster.Id, Slot = slot, Target = target.Pos });
        }

        private static (BattleState s, Unit mage, Unit tank, Unit witch) Board(ContentDb db)
        {
            var (s, _) = Battle(db, Stage(("tank", 2, 3), ("witch", 5, 0)), ("knight", 2, 4), ("mage", 0, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "link_hex" };
            m.Bar[1] = new AbilitySlot { AbilityId = "dart" };
            return (s, m, Enemy(s, "tank"), Enemy(s, "witch"));
        }

        [Test]
        public void Curse_link_ships_as_the_hexers_two_turn_linked_spell_with_a_glossary_entry()
        {
            var d = Real.Buffs["linked"];
            Assert.That(d.Links, Is.True);
            Assert.That(d.Kind, Is.EqualTo(BuffKind.Debuff));
            Assert.That(d.Unit, Is.EqualTo(DurationUnit.Turns));
            Assert.That(d.Duration, Is.EqualTo(2));
            var a = Real.Abilities["blackwitch_curselink"];
            Assert.That(a.ApplyBuff, Is.EqualTo("linked"));
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Spell));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickActor));
            Assert.That(Real.Classes["blackwitch"].Abilities, Does.Contain("blackwitch_curselink"));
            Assert.That(Real.TryTerm("Linked"), Is.Not.Null);
            Assert.That(BattleConfig.LinkEchoFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void Linking_binds_the_picked_foe_to_its_farthest_teammate()
        {
            var db = WithLink();
            var (s, _) = Battle(db, Stage(("tank", 2, 3), ("goblin", 3, 3), ("witch", 5, 0)), ("knight", 2, 4), ("mage", 0, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "link_hex" };
            var tank = Enemy(s, "tank");
            var witch = Enemy(s, "witch");
            Assert.That(CurseLink.Partner(s, tank), Is.SameAs(witch), "the witch stands farther from the tank than the goblin");

            var ev = Cast(s, m, 0, tank);

            var linked = ev.OfType<UnitsLinkedEvent>().Single();
            Assert.That((linked.AId, linked.BId), Is.EqualTo((tank.Id, witch.Id)));
            Assert.That(CurseLink.PartnerOf(s, tank), Is.SameAs(witch));
            Assert.That(CurseLink.PartnerOf(s, witch), Is.SameAs(tank));
            Assert.That(Enemy(s, "goblin").HasBuff("linked"), Is.False);
        }

        [Test]
        public void A_blow_on_one_linked_foe_echoes_half_to_the_other_and_never_back()
        {
            var (s, m, tank, witch) = Board(WithLink());
            Cast(s, m, 0, tank);
            int tankBefore = tank.Hp, witchBefore = witch.Hp;

            var ev = Cast(s, m, 1, tank);

            int dealt = tankBefore - tank.Hp;
            Assert.That(dealt, Is.GreaterThan(0));
            Assert.That(witchBefore - witch.Hp, Is.EqualTo(CurseLink.Echo(dealt)), "half the blow, rounded, reaches the partner");
            var echo = ev.OfType<LinkEchoEvent>().Single();
            Assert.That((echo.FromId, echo.ToId, echo.Amount), Is.EqualTo((tank.Id, witch.Id, CurseLink.Echo(dealt))));
            Assert.That(ev.OfType<DamagedEvent>().Count(d => d.TargetId == tank.Id), Is.EqualTo(1), "the echo never echoes back");
            Assert.That(ev.OfType<DamagedEvent>().Single(d => d.TargetId == witch.Id).Note, Is.EqualTo("Echo"));
        }

        [Test]
        public void An_unlinked_foe_takes_no_echo()
        {
            var (s, m, tank, witch) = Board(WithLink());
            int witchBefore = witch.Hp;
            var ev = Cast(s, m, 1, tank);
            Assert.That(ev.OfType<LinkEchoEvent>(), Is.Empty);
            Assert.That(witch.Hp, Is.EqualTo(witchBefore));
        }

        [Test]
        public void The_link_breaks_when_either_foe_falls()
        {
            var (s, m, tank, witch) = Board(WithLink());
            Cast(s, m, 0, tank);
            tank.Hp = 1;
            Cast(s, m, 1, tank);
            Assert.That(tank.Alive, Is.False);
            Assert.That(witch.HasBuff("linked"), Is.False, "the survivor's half of the link goes with its partner");
        }

        [Test]
        public void A_lone_foe_cannot_be_linked()
        {
            var db = WithLink();
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 2, 4), ("mage", 0, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "link_hex" };
            var tank = Enemy(s, "tank");
            Cast(s, m, 0, tank);
            Assert.That(tank.HasBuff("linked"), Is.False);
        }

        [Test]
        public void The_bot_values_linking_a_pincer_target_to_a_foe_out_of_reach()
        {
            var (s, _, tank, witch) = Board(WithLink());
            // The knight stands beside the tank (one more hero pincers it); no hero is near the witch.
            Assert.That(CurseLink.Flanked(s, tank), Is.True);
            Assert.That(CurseLink.InReach(s, witch), Is.False);
            Assert.That(CurseLink.Value(s, tank), Is.GreaterThanOrEqualTo(10f));
            Assert.That(CurseLink.Value(s, witch), Is.GreaterThanOrEqualTo(10f), "either end of the same pair");

            // Both foes within the party's reach: nothing a pincer can't already do.
            var (s2, _) = Battle(WithLink(), Stage(("tank", 2, 3), ("witch", 3, 4)), ("knight", 2, 4), ("mage", 0, 7));
            Assert.That(CurseLink.Value(s2, Enemy(s2, "tank")), Is.LessThan(1f));
        }
    }
}
