using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T868: the Lancer's Pole Vault (bible §7): a leap over the unit beside it (ally or foe, Heavy included)
    /// onto the free tile beyond, never entering the tile between, with pincers checked on landing; and the HeroBot's
    /// reasons to reach for it (a way out of a box, a pincer square).</summary>
    public class PoleVaultTests
    {
        private static ContentDb WithVault(bool heavyGoblin = false)
        {
            var db = Mini();
            db.Abilities["lancer_polevault"] = Real.Ability("lancer_polevault");
            db.Buffs["rooted"] = Real.Buffs["rooted"];
            db.Classes["goblin"].DropTable = null;
            db.Classes["goblin"].Heavy = heavyGoblin;
            return db;
        }

        private static List<BattleEvent> Vault(BattleState s, Unit hero, Coord to)
        {
            hero.Bar[0] = new AbilitySlot { AbilityId = "lancer_polevault" };
            hero.ActedThisWindow = false; // one action a window (GG-T308); these tests are about the skill
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = hero.Id, Slot = 0, Target = to });
        }

        private static Unit Goblin(BattleState s) => s.Units.First(u => u.Def.Id == "goblin" && u.Alive);

        [Test]
        public void Pole_vault_is_the_lancers_blink_over_a_unit_and_its_content_is_valid()
        {
            var a = Real.Ability("lancer_polevault");
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That(a.Vaults, Is.True);
            Assert.That(a.IsTeleport, Is.True);
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EmptyOnly));
            Assert.That(Real.Classes["lancer"].Abilities, Does.Contain("lancer_polevault"));
            Assert.That(ContentLoader.Validate(Real).Where(p => p.Contains("lancer_polevault")), Is.Empty);
        }

        [Test]
        public void The_only_picks_are_free_tiles_straight_beyond_a_unit_beside_the_caster()
        {
            var db = WithVault();
            var (s, _) = Battle(db, Stage(("goblin", 2, 4), ("tank", 0, 0)), ("knight", 2, 5), ("mage", 3, 5));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "lancer_polevault" };
            var picks = BattleEngine.ValidTargets(s, k.Id, 0);
            Assert.That(picks, Is.EquivalentTo(new[] { new Coord(2, 3), new Coord(4, 5) }), "over the goblin above, over the mage beside");
        }

        [Test]
        public void A_vault_clears_a_heavy_foe_without_moving_it()
        {
            var db = WithVault(heavyGoblin: true);
            var (s, _) = Battle(db, Stage(("goblin", 2, 4), ("tank", 0, 0)), ("knight", 2, 5), ("mage", 5, 7));
            var k = Hero(s, "knight");
            var gob = Goblin(s);
            Assert.That(gob.IsHeavy, Is.True);

            var ev = Vault(s, k, new Coord(2, 3));

            Assert.That(ev.Any(e => e is AbilityRefusedEvent), Is.False, string.Join(" | ", ev));
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 3)));
            Assert.That(gob.Pos, Is.EqualTo(new Coord(2, 4)), "the vaulted unit stays where it stands");
            var v = ev.OfType<VaultedEvent>().Single();
            Assert.That(v.OverId, Is.EqualTo(gob.Id));
            Assert.That(v.Over, Is.EqualTo(new Coord(2, 4)));
            AssertInvariants(s);
        }

        [Test]
        public void A_vault_crosses_the_trap_and_hazard_under_the_unit_it_clears()
        {
            var db = WithVault();
            var (s, _) = Battle(db, Stage(("goblin", 2, 4), ("tank", 0, 0)), ("knight", 2, 5), ("mage", 5, 7));
            var k = Hero(s, "knight");
            var mid = new Coord(2, 4);
            s.Traps.Add(new Trap { At = mid, TrapId = "caltrops", OwnerId = -1 });
            s.Region.Tiles[mid] = new TileMark { State = "mine", Rule = "landmines", Hazard = true, OwnerTeam = Team.Enemy };
            int hp = k.Hp;

            var ev = Vault(s, k, new Coord(2, 3));

            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 3)));
            Assert.That(ev.OfType<TrapTriggeredEvent>().Any(), Is.False, "the tile between is never entered");
            Assert.That(s.TrapAt(mid), Is.Not.Null);
            Assert.That(s.Region.At(mid), Is.Not.Null, "the mine under the goblin is still there");
            Assert.That(k.Hp, Is.EqualTo(hp));
        }

        [Test]
        public void A_landing_that_closes_a_pincer_springs_it()
        {
            var db = WithVault();
            // knight (2,5), the mage beside it at (2,4); vault over the mage to (2,3): knight-goblin-cleric down column 2.
            var (s, _) = Battle(db, Stage(("goblin", 2, 2), ("tank", 0, 0)), ("knight", 2, 5), ("mage", 2, 4), ("cleric", 2, 1));
            var k = Hero(s, "knight");

            var ev = Vault(s, k, new Coord(2, 3));

            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 3)));
            Assert.That(ev.OfType<PincerFormedEvent>().Any(), Is.True, string.Join(" | ", ev));
        }

        [Test]
        public void A_rooted_lancer_cannot_vault()
        {
            var db = WithVault();
            var (s, _) = Battle(db, Stage(("goblin", 2, 4), ("tank", 0, 0)), ("knight", 2, 5), ("mage", 5, 7));
            var k = Hero(s, "knight");
            k.Buffs.Add(new BuffInstance { Def = db.Buffs["rooted"], Remaining = 2 });

            var ev = Vault(s, k, new Coord(2, 3));

            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 5)));
            Assert.That(ev.OfType<StepBlockedEvent>().Any(b => b.Reason == "rooted"), Is.True);
            Assert.That(ev.OfType<VaultedEvent>().Any(), Is.False);
        }

        [Test]
        public void A_vault_onto_a_tile_with_nothing_to_clear_or_an_occupied_landing_is_refused()
        {
            var db = WithVault();
            var (s, _) = Battle(db, Stage(("goblin", 2, 4), ("wolf", 2, 3), ("tank", 0, 0)), ("knight", 2, 5), ("mage", 5, 7));
            var k = Hero(s, "knight");

            Assert.That(Vault(s, k, new Coord(2, 3)).Any(e => e is AbilityRefusedEvent), Is.True, "the landing is taken");
            k.Cooldowns.Clear();
            Assert.That(Vault(s, k, new Coord(4, 5)).Any(e => e is AbilityRefusedEvent), Is.True, "nobody stands between");
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 5)));
        }

        [Test]
        public void The_bot_vaults_out_of_a_box_and_onto_a_pincer_square_but_not_for_nothing()
        {
            var db = WithVault();
            // Boxed in the corner by two goblins: vaulting over the one above is a way out.
            var (boxed, _) = Battle(db, Stage(("goblin", 0, 6), ("goblin", 1, 7), ("tank", 5, 0)), ("knight", 0, 7), ("mage", 5, 7));
            var bk = Hero(boxed, "knight");
            Assert.That(PoleVault.OpenSides(boxed, bk, bk.Pos), Is.EqualTo(0));
            Assert.That(PoleVault.Value(boxed, bk, new Coord(0, 5)), Is.GreaterThan(0f), "vault out of the box");

            // A lone foe in the open: nothing gained by hopping over it.
            var (open, _) = Battle(db, Stage(("goblin", 2, 4), ("tank", 0, 0)), ("knight", 2, 5), ("mage", 5, 7));
            var ok = Hero(open, "knight");
            Assert.That(PoleVault.Value(open, ok, new Coord(2, 3)), Is.LessThanOrEqualTo(0f));

            // Over the mage onto a pincer square.
            var (pin, _) = Battle(db, Stage(("goblin", 2, 2), ("tank", 0, 0)), ("knight", 2, 5), ("mage", 2, 4), ("cleric", 2, 1));
            var pk = Hero(pin, "knight");
            Assert.That(PoleVault.Value(pin, pk, new Coord(2, 3)), Is.GreaterThanOrEqualTo(8f));

            // Never onto a tile it can see is dangerous.
            boxed.Region.Tiles[new Coord(0, 5)] = new TileMark { State = "mine", Rule = "landmines", Hazard = true, OwnerTeam = Team.Enemy };
            Assert.That(PoleVault.Value(boxed, bk, new Coord(0, 5)), Is.EqualTo(0f));
        }
    }
}
