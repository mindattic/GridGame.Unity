using GridGame.Core.Content;
using System.Linq;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>
    /// Only the happy path (real content validates clean, ContentAndSoakTests) was covered before this
    /// file: a regression in ContentLoader.Validate's cross-reference checks would go unnoticed because
    /// nothing exercised the broken-reference paths. Each test here takes a small, fully valid fixture
    /// and breaks exactly one reference, asserting Validate reports exactly that problem.
    /// </summary>
    public class ContentValidationTests
    {
        private static ContentDb ValidDb()
        {
            var db = new ContentDb();

            db.Buffs["burning"] = new BuffDef { Id = "burning", Name = "Burning" };

            db.Abilities["slash"] = new AbilityDef { Id = "slash", Name = "Slash", Kind = AbilityKind.Skill };
            db.Abilities["bolt"] = new AbilityDef { Id = "bolt", Name = "Bolt", Kind = AbilityKind.Spell, ApplyBuff = "burning" };

            db.Items["potion"] = new ItemDef { Id = "potion", Name = "Potion", Type = ItemType.Consumable };
            db.Items["ore"] = new ItemDef { Id = "ore", Name = "Ore", Type = ItemType.CraftingMaterial };
            db.Items["blade"] = new ItemDef { Id = "blade", Name = "Blade", Type = ItemType.Equipment };

            db.DropTables["knightdrop"] = new DropTableDef
            {
                Id = "knightdrop",
                Entries = { new DropEntry { Item = "potion", Chance = 1f } },
            };

            db.Recipes["r1"] = new RecipeDef
            {
                Id = "r1",
                Station = CraftStation.Forge,
                BaseItem = "ore",
                Result = "blade",
                Inputs = { new ItemCount { Item = "ore", Count = 1 } },
            };

            db.Traps["spike"] = new TrapDef { Id = "spike", Name = "Spike" };

            db.Pickups["p1"] = new PickupDef { Id = "p1", Name = "Pickup", Weight = 1 };

            // Every class needs a Thumbnail and every enemy its own Support (GG-T306/GG-T307 validator rules).
            db.Classes["goblin"] = new ClassDef { Id = "goblin", Name = "Goblin", Team = Team.Enemy, Thumbnail = new Thumbnail(), Figure = new Figure(), Support = Heal() };

            db.Classes["boss"] = new ClassDef
            {
                Id = "boss",
                Name = "Boss",
                Team = Team.Enemy,
                Thumbnail = new Thumbnail(), Figure = new Figure(),
                Support = Heal(),
                ChargeAbility = "bolt",
                Trap = "spike",
                Trail = "spike",
                BossPhases = { new BossPhaseDef { HpThreshold = 0.5f, OnEnterAbility = "bolt" } },
            };

            db.SkillTrees["tanktree"] = new SkillTreeDef { Id = "tanktree", Name = "Tank Tree" };

            db.Classes["knight"] = new ClassDef
            {
                Id = "knight",
                Name = "Knight",
                Team = Team.Hero,
                Role = "Tank",
                Thumbnail = new Thumbnail(), Figure = new Figure(),
                DropTable = "knightdrop",
                Abilities = { "slash" },
                DefaultLoadout = { "slash", "item:potion" },
            };

            db.Stages["s1"] = new StageDef
            {
                Id = "s1",
                Name = "S1",
                Waves =
                {
                    new WaveDef
                    {
                        Enemies = { new SpawnDef { Class = "goblin" } },
                        Guests = { new SpawnDef { Class = "goblin" } },
                        SurpriseSpeaker = "goblin",
                    },
                },
            };
            db.StageOrder.Add(db.Stages["s1"]);

            db.Campaign.Themes.Add(new ThemeDef { Id = "t1", Name = "T1", Stages = { "s1" } });

            return db;
        }

        private static SupportDef Heal() => new SupportDef { Effect = SupportEffect.Heal, Magnitude = 0.25f, Target = SupportTarget.Attacker, Range = 3 };

        /// <summary>Validates the fixture and asserts it reports exactly one problem, returning it.</summary>
        private static string OneProblem(ContentDb db)
        {
            var problems = ContentLoader.Validate(db);
            Assert.That(problems, Has.Exactly(1).Items);
            return problems.Single();
        }

        [Test]
        public void Baseline_fixture_validates_clean()
        {
            Assert.That(ContentLoader.Validate(ValidDb()), Is.Empty);
        }

        [Test]
        public void Unknown_class_in_stage_wave_enemy_is_reported()
        {
            var db = ValidDb();
            db.Stages["s1"].Waves[0].Enemies[0].Class = "missing";
            Assert.That(OneProblem(db), Does.Contain("unknown class 'missing'"));
        }

        [Test]
        public void Unknown_class_in_stage_wave_guest_is_reported()
        {
            var db = ValidDb();
            db.Stages["s1"].Waves[0].Guests[0].Class = "missing";
            Assert.That(OneProblem(db), Does.Contain("unknown class 'missing'"));
        }

        [Test]
        public void Unknown_class_as_surprise_speaker_is_reported()
        {
            var db = ValidDb();
            db.Stages["s1"].Waves[0].SurpriseSpeaker = "missing";
            Assert.That(OneProblem(db), Does.Contain("surprise speaker"));
        }

        [Test]
        public void Unknown_ability_in_loadout_is_reported()
        {
            var db = ValidDb();
            db.Classes["knight"].DefaultLoadout[0] = "missing";
            Assert.That(OneProblem(db), Does.Contain("loadout"));
        }

        [Test]
        public void Unknown_ability_as_charge_ability_is_reported()
        {
            var db = ValidDb();
            db.Classes["boss"].ChargeAbility = "missing";
            Assert.That(OneProblem(db), Does.Contain("charge"));
        }

        [Test]
        public void Unknown_ability_in_boss_phase_is_reported()
        {
            var db = ValidDb();
            db.Classes["boss"].BossPhases[0].OnEnterAbility = "missing";
            Assert.That(OneProblem(db), Does.Contain("boss phase"));
        }

        [Test]
        public void Null_loadout_entry_is_reported()
        {
            var db = ValidDb();
            db.Classes["knight"].DefaultLoadout.Add(null);
            Assert.That(OneProblem(db), Does.Contain("null entry"));
        }

        [Test]
        public void Unknown_item_in_recipe_is_reported()
        {
            var db = ValidDb();
            db.Recipes["r1"].Result = "missing";
            Assert.That(OneProblem(db), Does.Contain("recipe r1: unknown item 'missing'"));
        }

        [Test]
        public void Unknown_item_in_droptable_is_reported()
        {
            var db = ValidDb();
            db.DropTables["knightdrop"].Entries[0].Item = "missing";
            Assert.That(OneProblem(db), Does.Contain("droptable knightdrop: unknown item 'missing'"));
        }

        [Test]
        public void Unknown_buff_is_reported()
        {
            var db = ValidDb();
            db.Abilities["bolt"].ApplyBuff = "missing";
            Assert.That(OneProblem(db), Does.Contain("unknown buff 'missing'"));
        }

        [Test]
        public void Unknown_trap_is_reported()
        {
            var db = ValidDb();
            db.Classes["boss"].Trap = "missing";
            Assert.That(OneProblem(db), Does.Contain("unknown trap 'missing'"));
        }

        [Test]
        public void Unknown_trail_is_reported()
        {
            var db = ValidDb();
            db.Classes["boss"].Trail = "missing";
            Assert.That(OneProblem(db), Does.Contain("unknown trail 'missing'"));
        }

        [Test]
        public void Theme_referencing_unknown_stage_is_reported()
        {
            var db = ValidDb();
            db.Campaign.Themes[0].Stages[0] = "missing";
            Assert.That(OneProblem(db), Does.Contain("theme t1: unknown stage 'missing'"));
        }
        [TestCase(0)]
        [TestCase(-1)]
        public void Pickup_with_nonpositive_weight_is_reported(int weight)
        {
            var db = ValidDb();
            db.Pickups["p1"].Weight = weight;
            Assert.That(OneProblem(db), Does.Contain("weight must be positive"));
        }
        [TestCase(null)]
        [TestCase("Bogus")]
        public void Invalid_role_is_reported(string role)
        {
            var db = ValidDb();
            db.Classes["knight"].Role = role;
            Assert.That(OneProblem(db), Does.Contain("needs a Role"));
        }

        [Test]
        public void Skill_tree_node_with_unknown_grant_is_reported()
        {
            var db = ValidDb();
            db.SkillTrees["tanktree"].Nodes.Add(new SkillNodeDef { Id = "n1", Grants = "missing" });
            Assert.That(OneProblem(db), Does.Contain("unknown ability 'missing'"));
        }

        [Test]
        public void Skill_tree_node_with_unknown_prerequisite_is_reported()
        {
            var db = ValidDb();
            db.SkillTrees["tanktree"].Nodes.Add(new SkillNodeDef { Id = "n1", Grants = "slash", Prerequisites = { "missing" } });
            Assert.That(OneProblem(db), Does.Contain("unknown prerequisite 'missing'"));
        }

        [Test]
        public void Skill_tree_node_prerequisite_cycle_is_reported()
        {
            var db = ValidDb();
            db.SkillTrees["tanktree"].Nodes.Add(new SkillNodeDef { Id = "n1", Grants = "slash", Prerequisites = { "n2" } });
            db.SkillTrees["tanktree"].Nodes.Add(new SkillNodeDef { Id = "n2", Grants = "slash", Prerequisites = { "n1" } });
            Assert.That(string.Join(";", ContentLoader.Validate(db)), Does.Contain("prerequisite cycle"));
        }

        /// <summary>Two branches that both trace back to the same ancestor (e.g. a finale node requiring two
        /// capstones that each descend from a shared root) is a valid diamond, not a cycle — a regression here
        /// would make every real archetype tree's finale node (GG-T74) fail validation.</summary>
        [Test]
        public void Skill_tree_diamond_shaped_prerequisites_are_not_a_cycle()
        {
            var db = ValidDb();
            var tree = db.SkillTrees["tanktree"];
            tree.Nodes.Add(new SkillNodeDef { Id = "root", Grants = "slash", Cost = 1 });
            tree.Nodes.Add(new SkillNodeDef { Id = "branch_a", Grants = "slash", Cost = 1, Prerequisites = { "root" } });
            tree.Nodes.Add(new SkillNodeDef { Id = "branch_b", Grants = "slash", Cost = 1, Prerequisites = { "root" } });
            tree.Nodes.Add(new SkillNodeDef { Id = "finale", Grants = "slash", Cost = 1, Prerequisites = { "branch_a", "branch_b" } });
            Assert.That(ContentLoader.Validate(db), Is.Empty);
        }

        /// <summary>GG-T837: a support's presentation names a vfx.json effect and a sound cue that exist.</summary>
        [Test]
        public void Support_projectile_and_sfx_must_exist()
        {
            var db = ValidDb();
            db.VfxPrefabs.Effects.Add(new VfxPrefabDef { Id = "Shuriken", Mode = VfxMode.Loop });
            db.Classes["knight"].Support = new SupportDef { Effect = SupportEffect.Heal, Magnitude = 0.2f, Projectile = "Shuriken", Motion = SupportMotion.Spin, Sfx = "battle.heal" };
            Assert.That(ContentLoader.Validate(db), Is.Empty);

            db.Classes["knight"].Support.Projectile = "NoSuchVfx";
            Assert.That(OneProblem(db), Does.Contain("Projectile 'NoSuchVfx' is not in vfx.json"));

            db.Classes["knight"].Support.Projectile = "Shuriken";
            db.Classes["knight"].Support.Sfx = "battle.kaboom"; // on the taxonomy's prefix, but no such cue
            Assert.That(OneProblem(db), Does.Contain("Sfx 'battle.kaboom'"));
        }

        /// <summary>GG-T837: every hero class's support reads as its own move — a projectile and a sound authored, and no
        /// two heroes sharing the same projectile, motion and sound.</summary>
        [Test]
        public void Every_hero_support_has_its_own_presentation()
        {
            var heroes = TestKit.Real.Classes.Values.Where(c => c.Team == Team.Hero).ToList();
            var bare = heroes.Where(c => c.Support == null || string.IsNullOrEmpty(c.Support.Projectile) || string.IsNullOrEmpty(c.Support.Sfx)).Select(c => c.Id);
            Assert.That(bare, Is.Empty);
            var twins = heroes.GroupBy(c => c.Support.Projectile + "|" + c.Support.Motion + "|" + c.Support.Sfx).Where(g => g.Count() > 1).Select(g => string.Join("+", g.Select(c => c.Id)));
            Assert.That(twins, Is.Empty);
            Assert.That(ContentLoader.Validate(TestKit.Real).Where(p => p.Contains("Projectile") || p.Contains("Sfx")), Is.Empty);
        }

        /// <summary>GG-T907: a content set with a glossary defines every battle keyword the cards print (Heavy, Floating,
        /// Rooted, Shredded, Frightened, Linked, Volley, the tactical tiles).</summary>
        [Test]
        public void A_glossary_missing_a_battle_keyword_is_reported()
        {
            ContentDb Fixture(string leaveOut)
            {
                var db = ValidDb();
                db.GlossaryOrder.Add(new GlossaryEntry { Id = "burning", Term = "Burning", Text = "On fire." });
                db.GlossaryOrder.Add(new GlossaryEntry { Id = "supporter", Term = "Supporter", Text = "Backs a pincer." });
                foreach (var k in GridGame.Core.Rules.KeywordText.Keywords)
                    if (k != leaveOut) db.GlossaryOrder.Add(new GlossaryEntry { Id = k.ToLowerInvariant().Replace(' ', '_'), Term = k, Text = "A keyword." });
                Glossary.Index(db);
                return db;
            }
            Assert.That(ContentLoader.Validate(Fixture(null)), Is.Empty);
            Assert.That(OneProblem(Fixture("Volley")), Does.Contain("no entry for the keyword 'Volley'"));
            Assert.That(ContentLoader.Validate(Fixture("Heavy")), Has.Some.Contains("no entry for the keyword 'Heavy'"));
            Assert.That(ContentLoader.Validate(Fixture("Heavy")), Has.Some.Contains("keyword line Heavy: unknown glossary term [Heavy]"));
        }

        /// <summary>GG-T838: every enemy class's support names its family's projectile, motion and sound (BIBLE 1.2.3
        /// enemy supports), all of them ids that exist.</summary>
        [Test]
        public void Every_enemy_support_has_a_presentation()
        {
            var enemies = TestKit.Real.Classes.Values.Where(c => c.Team == Team.Enemy).ToList();
            Assert.That(enemies, Is.Not.Empty);
            var bare = enemies.Where(c => c.Support == null || string.IsNullOrEmpty(c.Support.Projectile) || string.IsNullOrEmpty(c.Support.Sfx)).Select(c => c.Id);
            Assert.That(bare, Is.Empty);
        }
    }
}
