using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Bulwark (GG-T332, BIBLE §14.15): four Pale Concord power-armour soldiers. Their aether packs are portable
    /// Stills, so the Siphoner's charge drains orbs from the party's bank — the new <see cref="AbilityDef.DrainsOrbs"/>.</summary>
    public class BulwarkTests
    {
        private static readonly string[] Family = { "heavytrooper01", "heavytrooper02", "heavytrooper03", "heavytrooper04" };

        [Test]
        public void The_bulwark_validates_plays_four_roles_and_marches_in_halcyon()
        {
            var db = Real;
            var problems = ContentLoader.Validate(db).Where(p => p.Contains("heavytrooper") || p.Contains("bulwark") || p.Contains("aegis_field")
                                                                 || p.Contains("aether_siphon") || p.Contains("siege_shot") || p.Contains("suppressing_fire")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var halcyon = db.Campaign.Themes.Where(t => t.Id == "cityruins").SelectMany(t => t.Stages).ToList();
            foreach (var id in Family)
            {
                var c = db.Classes[id];
                Assert.That(c.HasTag("Sentient"), Is.True, $"{id}: Concord soldiers plan their pincers");
                Assert.That(c.DropTable, Is.EqualTo("bulwark"), id);
                Assert.That(c.Resistances[DamageType.Lightning], Is.GreaterThan(1f), $"{id}: power armour shorts out");
                Assert.That(new[] { 8f, 11f }, Does.Contain(c.BaseStats.Speed), $"{id}: heavy armour is slow or steady, never swift");
                Assert.That(halcyon.Any(s => db.Stages[s].Waves.Any(w => w.Enemies.Any(e => e.Class == id))), $"{id} marches in Halcyon");
            }
            var kits = Family.Select(id => db.Classes[id]).Select(c => (c.Brain, c.Targeting, c.ChargeAbility, c.AttackRange)).ToList();
            Assert.That(kits.Distinct().Count(), Is.EqualTo(4), "four roles");
            Assert.That(db.Classes["heavytrooper01"].AttackRange, Is.EqualTo(3), "the rifleman shoots");
            Assert.That(db.Classes["heavytrooper04"].ChargeShape, Is.EqualTo(ChargeShape.Line), "the siege gunner's round runs down a line");
            Assert.That(db.Abilities["charge_siege_shot"].RecoverSeconds, Is.GreaterThanOrEqualTo(2f), "a heavy round recovers long");
        }

        [Test]
        public void The_siphon_drains_an_orb_from_the_party_pool_when_it_lands()
        {
            var (s, _) = Battle(Real, Stage(("heavytrooper03", 2, 0)), ("paladin", 2, 6), ("redmage", 4, 7));
            var siphoner = s.Units.Single(u => u.Def.Id == "heavytrooper03");
            GrantMana(s, 4);
            var seen = new List<BattleEvent>();
            for (int i = 0; i < 16 && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_aether_siphon") && !s.IsOver; i++)
                seen.AddRange(Turn(s, new ShieldCommand()));
            var hit = seen.OfType<AbilityResolvedEvent>().FirstOrDefault(e => e.AbilityId == "charge_aether_siphon");
            Assert.That(hit, Is.Not.Null, "the siphoner charges and lands its siphon");
            Assert.That(Hero(s, "redmage").Id, Is.EqualTo(hit.Targets.Single()), "it hunts the caster");
            var drained = seen.OfType<ManaGainedEvent>().Where(e => e.Kind == ManaGainKind.Siphon).ToList();
            Assert.That(drained.Count, Is.EqualTo(1));
            Assert.That(drained[0].Orbs, Is.EqualTo(1));
            Assert.That(drained[0].Amount, Is.EqualTo(-BattleConfig.ManaPointsPerOrb), "an orb's worth of points leaves the pool");
            Assert.That(drained[0].After, Is.EqualTo(drained[0].Before - BattleConfig.ManaPointsPerOrb));
            Assert.That(drained[0].SourceId, Is.EqualTo(siphoner.Id));
            Assert.That(seen.OfType<AnnounceEvent>().Any(a => a.Text.StartsWith("Siphoned")), Is.True);
        }

        [Test]
        public void A_siphon_takes_no_more_than_the_pool_holds()
        {
            // §3.1.0: Guard and regen earn into the party pool whatever the bar holds, so the pool is emptied before every
            // turn and Guard is held to the end of its window (no bank). Whatever earned mana lands in the siphon's own
            // turn before it is all it can take; an empty pool has nothing to siphon.
            var (s, _) = Battle(Real, Stage(("heavytrooper03", 2, 0)), ("paladin", 2, 6), ("redmage", 4, 7));
            var siphoner = s.Units.Single(u => u.Def.Id == "heavytrooper03");
            List<BattleEvent> turn = null;
            for (int i = 0; i < 16 && !s.IsOver; i++)
            {
                s.SetPartyMana(0);
                turn = Turn(s, new ShieldCommand { HeldSeconds = 999f });
                if (turn.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_aether_siphon")) break;
                turn = null;
            }
            Assert.That(turn, Is.Not.Null, "the siphoner charges and lands its siphon");
            int landed = turn.FindIndex(e => e is AnnounceEvent a && a.UnitId == siphoner.Id && (a.Text == "No mana to take" || a.Text.StartsWith("Siphoned")));
            Assert.That(landed, Is.GreaterThanOrEqualTo(0));
            var siphon = turn.OfType<ManaGainedEvent>().Where(e => e.Kind == ManaGainKind.Siphon).ToList();
            int held = turn.Take(landed).OfType<ManaGainedEvent>().Where(e => e.Kind != ManaGainKind.Siphon).Sum(e => e.Amount);
            if (held == 0)
            {
                Assert.That(siphon, Is.Empty);
                Assert.That(((AnnounceEvent)turn[landed]).Text, Is.EqualTo("No mana to take"));
            }
            else
            {
                Assert.That(siphon.Count, Is.EqualTo(1));
                Assert.That(siphon[0].Amount, Is.EqualTo(-System.Math.Min(held, BattleConfig.ManaPointsPerOrb)));
                Assert.That(siphon[0].After, Is.EqualTo(System.Math.Max(0, held - BattleConfig.ManaPointsPerOrb)), "floored at 0");
            }
        }

        [Test]
        public void Validation_keeps_the_siphon_on_enemy_charges_and_in_range()
        {
            var siphon = Real.Abilities["charge_aether_siphon"];
            var cleave = Real.Abilities.Values.First(a => a.Kind == AbilityKind.Skill && !a.Id.StartsWith("charge_"));
            int keep = siphon.DrainsOrbs;
            try
            {
                siphon.DrainsOrbs = AbilityDef.MaxDrainsOrbs + 1;
                Assert.That(ContentLoader.Validate(Real).Any(p => p.Contains("charge_aether_siphon") && p.Contains("DrainsOrbs")), Is.True);
                siphon.DrainsOrbs = keep;
                cleave.DrainsOrbs = 1;
                Assert.That(ContentLoader.Validate(Real).Any(p => p.Contains(cleave.Id) && p.Contains("DrainsOrbs")), Is.True, "a hero skill can't siphon");
            }
            finally { siphon.DrainsOrbs = keep; cleave.DrainsOrbs = 0; }
            Assert.That(GridGame.Meta.Abilities.IsHeroUsable(siphon), Is.False, "charges are never learnable");
        }

        [Test]
        public void The_aegis_field_wards_every_soldier_near_the_shieldbearer()
        {
            var (s, _) = Battle(Real, Stage(("heavytrooper02", 2, 0), ("heavytrooper01", 4, 0)), ("paladin", 0, 7), ("cleric", 5, 7));
            var rifle = s.Units.Single(u => u.Def.Id == "heavytrooper01");
            var seen = new List<BattleEvent>();
            for (int i = 0; i < 16 && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_aegis_field") && !s.IsOver; i++)
            {
                rifle.Pos = new Coord(4, 0); // hold the rifleman inside the field's 2 tiles
                seen.AddRange(Turn(s, new ShieldCommand()));
            }
            var field = seen.OfType<AbilityResolvedEvent>().FirstOrDefault(e => e.AbilityId == "charge_aegis_field");
            Assert.That(field, Is.Not.Null, "the shieldbearer charges its field");
            Assert.That(field.Targets, Does.Contain(rifle.Id));
            Assert.That(seen.OfType<BuffAppliedEvent>().Any(b => b.UnitId == rifle.Id && b.BuffId == "ward"), Is.True);
        }

        [Test]
        public void The_siege_shot_marks_a_line_through_its_target()
        {
            var (s, _) = Battle(Real, Stage(("heavytrooper04", 2, 0)), ("paladin", 2, 6), ("cleric", 5, 7));
            var seen = new List<BattleEvent>();
            for (int i = 0; i < 16 && !seen.OfType<CastQueuedEvent>().Any(e => e.AbilityId == "charge_siege_shot") && !s.IsOver; i++)
                seen.AddRange(Turn(s, new ShieldCommand()));
            var queued = seen.OfType<CastQueuedEvent>().FirstOrDefault(e => e.AbilityId == "charge_siege_shot");
            Assert.That(queued, Is.Not.Null, "the gunner braces and charges");
            Assert.That(queued.LineTiles, Is.Not.Empty);
        }
    }
}
