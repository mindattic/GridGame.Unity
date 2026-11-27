using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Generic Cohesion scenes (GG-T1623 content, GG-T1627 runtime): selector validation, DialogueRunner.GenericMatch
    /// scoring, BondScenes offering one generic per pair per Level, the pair flag, and PlayBondScenesHeadless.</summary>
    public class GenericCohesionTests
    {
        private static ContentDb Db(params DialogueDef[] dialogues)
        {
            var db = new ContentDb();
            db.Items["potion"] = new ItemDef { Id = "potion", Name = "Potion", Type = ItemType.Consumable };
            foreach (var (id, role) in new[] { ("knight", "Tank"), ("mage", "Mage"), ("cleric", "Healer"), ("archer", "Ranged") })
                db.Classes[id] = new ClassDef { Id = id, Name = char.ToUpper(id[0]) + id.Substring(1), Team = Team.Hero, Role = role, Thumbnail = new Thumbnail(), Figure = new Figure() };
            db.Factions.Add(new FactionDef { Id = "north", Name = "the North", Heroes = { "knight", "mage" } });
            db.Factions.Add(new FactionDef { Id = "south", Name = "the South", Heroes = { "cleric", "archer" } });
            db.Campaign.Themes.Add(new ThemeDef { Id = "vale", Name = "Vale" });
            foreach (var d in dialogues) { db.Dialogues[d.Id] = d; db.DialogueOrder.Add(d); }
            return db;
        }

        private static DialogueDef Scene(string id, string a, string b, int level = 1, params DialogueEffect[] effects)
        {
            var d = new DialogueDef
            {
                Id = id,
                Trigger = new DialogueTrigger { Kind = DialogueTriggerKind.Cohesion, Id = a, Other = b, Level = level },
                Speakers = { new DialogueSpeaker { Id = "w", Class = "$with" }, new DialogueSpeaker { Id = "h", Class = "$hero" } },
                Nodes = { new DialogueNode { Id = "a", Speaker = "w", Text = "Hi", Next = "b" }, new DialogueNode { Id = "b", Speaker = "h", Text = "Hello" } },
            };
            d.Nodes[1].Effects.AddRange(effects);
            return d;
        }

        private static List<string> Problems(DialogueDef d) => ContentLoader.Validate(Db(d), _ => true).Where(p => p.Contains(d.Id)).ToList();

        private static SaveData Save(params string[] party)
        {
            var s = new SaveData();
            foreach (var h in party) { s.Roster.Add(new HeroSave { ClassId = h }); s.Party.Add(h); }
            return s;
        }

        private static void Bond(SaveData s, string a, string b) => Bonds.AddCohesion(s, a, b, 1000);

        // ── validation ─────────────────────────────────────────────────────────

        [Test]
        public void Selectors_validate()
        {
            foreach (var (a, b) in new[] { ("role:Tank", "role:Mage"), ("faction:north", "faction:south"), ("faction:same", "any"), ("any", "faction:other"), ("any", "any"), ("knight", "any"), ("any", "mage"), ("role:Tank", "role:Tank"), ("faction:north", "faction:north") })
                Assert.That(Problems(Scene("g", a, b)), Is.Empty, $"{a} + {b}");
        }

        [Test]
        public void Unknown_role_or_faction_is_rejected()
        {
            Assert.That(Problems(Scene("g", "role:Bard", "any")), Has.Some.Contains("unknown Role 'role:Bard'"));
            Assert.That(Problems(Scene("g", "any", "role:Bard")), Has.Some.Contains("Other names unknown Role"));
            Assert.That(Problems(Scene("g", "faction:zz", "any")), Has.Some.Contains("unknown faction 'faction:zz'"));
            Assert.That(Problems(Scene("g", "any", "faction:zz")), Has.Some.Contains("Other names unknown faction"));
            Assert.That(Problems(Scene("g", "wizard", "any")), Has.Some.Contains("needs a hero class or a selector"));
        }

        [Test]
        public void Two_different_heroes_is_only_for_class_ids()
        {
            Assert.That(Problems(Scene("g", "knight", "knight")), Has.Some.Contains("two different heroes"));
            Assert.That(Problems(Scene("g", "knight", "mage")), Is.Empty);
        }

        [Test]
        public void Pair_flag_prefix_is_reserved_from_SetFlag_and_ClearFlag()
        {
            foreach (var type in new[] { DialogueEffectType.SetFlag, DialogueEffectType.ClearFlag })
                Assert.That(Problems(Scene("g", "any", "any", 1, new DialogueEffect { Type = type, Id = "pair:g:a|b" })), Has.Some.Contains("reserved"), type.ToString());
            Assert.That(Problems(Scene("g", "any", "any", 1, new DialogueEffect { Type = DialogueEffectType.SetFlag, Id = "paired" })), Is.Empty);
        }

        // ── GenericMatch ───────────────────────────────────────────────────────

        private static (int score, string first, string second) Match(string idSel, string otherSel, string a, string b) =>
            DialogueRunner.GenericMatch(Db(), new DialogueTrigger { Kind = DialogueTriggerKind.Cohesion, Id = idSel, Other = otherSel, Level = 1 }, a, b);

        [Test]
        public void GenericMatch_scores_each_side_by_specificity()
        {
            Assert.That(Match("knight", "any", "knight", "mage").score, Is.EqualTo(4), "class 4 + any 0");
            Assert.That(Match("knight", "mage", "knight", "mage").score, Is.EqualTo(8), "two classes");
            Assert.That(Match("role:Tank", "role:Mage", "knight", "mage").score, Is.EqualTo(4), "role 2 + role 2");
            Assert.That(Match("faction:north", "any", "knight", "mage").score, Is.EqualTo(2), "faction id 2");
            Assert.That(Match("faction:same", "any", "knight", "mage").score, Is.EqualTo(1));
            Assert.That(Match("faction:other", "any", "knight", "cleric").score, Is.EqualTo(1));
            Assert.That(Match("any", "any", "knight", "mage").score, Is.EqualTo(0));
        }

        [Test]
        public void GenericMatch_rejects_pairs_that_do_not_fit()
        {
            Assert.That(Match("role:Healer", "any", "knight", "mage").score, Is.EqualTo(-1));
            Assert.That(Match("faction:south", "any", "knight", "mage").score, Is.EqualTo(-1));
            Assert.That(Match("faction:same", "any", "knight", "cleric").score, Is.EqualTo(-1), "different factions");
            Assert.That(Match("faction:other", "any", "knight", "mage").score, Is.EqualTo(-1), "same faction");
            Assert.That(Match("archer", "any", "knight", "mage").score, Is.EqualTo(-1), "class not in the pair");
        }

        [Test]
        public void GenericMatch_takes_either_orientation_and_orders_the_pair_by_it()
        {
            Assert.That(Match("role:Tank", "any", "mage", "knight"), Is.EqualTo((2, "knight", "mage")), "Id side is the Tank, given the pair reversed");
            Assert.That(Match("any", "knight", "knight", "mage"), Is.EqualTo((4, "mage", "knight")), "Other side is the named class");
            Assert.That(Match("any", "any", "knight", "mage"), Is.EqualTo((0, "knight", "mage")), "a tie keeps the given order");
        }

        // ── BondScenes ─────────────────────────────────────────────────────────

        [Test]
        public void BondScenes_offers_the_most_specific_generic_and_ties_go_to_file_order()
        {
            var db = Db(Scene("any1", "any", "any"), Scene("role1", "role:Tank", "any"), Scene("role2", "role:Tank", "any"), Scene("fac", "faction:same", "any"));
            var s = Save("knight", "mage"); Bond(s, "knight", "mage");
            var offered = DialogueRunner.BondScenes(db, s);
            Assert.That(offered.Select(b => b.Def.Id), Is.EqualTo(new[] { "role1" }), "one scene per pair per Level, first of the best");
            Assert.That(offered[0].Unlocked);

            db = Db(Scene("role1", "role:Tank", "any"), Scene("cls", "any", "knight"));
            Assert.That(DialogueRunner.BondScenes(db, s).Select(b => b.Def.Id), Is.EqualTo(new[] { "cls" }), "a class outranks a role");
        }

        [Test]
        public void BondScenes_binds_with_to_the_first_hero_and_hero_to_the_second()
        {
            var db = Db(Scene("g", "role:Mage", "role:Tank"));
            var s = Save("knight", "mage"); Bond(s, "knight", "mage");
            var b = DialogueRunner.BondScenes(db, s).Single();
            Assert.That((b.With, b.Other), Is.EqualTo(("mage", "knight")));
            var r = b.Begin(db, s);
            Assert.That(r.Resolve(new DialogueSpeaker { Class = "$with" }).name, Is.EqualTo("Mage"));
            Assert.That(r.Resolve(new DialogueSpeaker { Class = "$hero" }).name, Is.EqualTo("Knight"));
        }

        [Test]
        public void BondScenes_is_locked_until_the_pair_reaches_the_Level()
        {
            var db = Db(Scene("g", "any", "any", 2));
            var s = Save("knight", "mage");
            Assert.That(DialogueRunner.BondScenes(db, s).Single().Unlocked, Is.False);
            Bond(s, "knight", "mage");
            Assert.That(DialogueRunner.BondScenes(db, s).Single().Unlocked, Is.True);
        }

        [Test]
        public void BondScenes_offers_a_generic_per_Level_and_per_pair()
        {
            var db = Db(Scene("one", "any", "any", 1), Scene("two", "any", "any", 2));
            var s = Save("knight", "mage", "cleric");
            var offered = DialogueRunner.BondScenes(db, s);
            Assert.That(offered.Count, Is.EqualTo(6), "three pairs x two Levels");
            Assert.That(offered.Select(b => b.Def.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(DialogueRunner.BondScenes(db, Save("knight")), Is.Empty, "no pair");
        }

        [Test]
        public void An_authored_scene_at_a_Level_replaces_the_generic_for_its_pair_only_there()
        {
            var db = Db(Scene("gen1", "any", "any", 1), Scene("gen2", "any", "any", 2), Scene("auth1", "mage", "knight", 1));
            var s = Save("knight", "mage");
            Assert.That(DialogueRunner.BondScenes(db, s).Select(b => b.Def.Id), Is.EquivalentTo(new[] { "auth1", "gen2" }), "either orientation of the authored pair counts");

            s = Save("knight", "cleric");
            Assert.That(DialogueRunner.BondScenes(db, s).Select(b => b.Def.Id), Is.EquivalentTo(new[] { "gen1", "gen2" }), "another pair keeps its generics");
        }

        [Test]
        public void A_told_generic_sets_the_pair_flag_and_is_not_offered_again()
        {
            var db = Db(Scene("g1", "any", "any", 1), Scene("g2", "role:Tank", "any", 1));
            var s = Save("knight", "mage"); Bond(s, "knight", "mage");
            var b = DialogueRunner.BondScenes(db, s).Single();
            Assert.That(b.Def.Id, Is.EqualTo("g2"));
            var r = b.Begin(db, s);
            for (int guard = 0; !r.Finished && guard < 20; guard++) if (!r.Advance()) r.Choose(0);
            Assert.That(r.Finished);

            string flag = DialogueRunner.PairSceneFlag("g2", "knight", "mage");
            Assert.That(flag, Does.StartWith("pair:g2:"));
            Assert.That(DialogueRunner.PairSceneFlag("g2", "mage", "knight"), Is.EqualTo(flag), "either order names the same pair");
            Assert.That(DialogueRunner.PairSceneFlag("g2", "knight", "knight"), Is.Null);
            Assert.That(s.Flags, Does.Contain(flag));
            Assert.That(DialogueRunner.BondScenes(db, s), Is.Empty, "the pair's Level is done; the other generic does not replace it");

            DialogueRunner.ApplyEffect(db, s, new DialogueEffect { Type = DialogueEffectType.ClearFlag, Id = flag });
            Assert.That(s.Flags, Does.Contain(flag), "ClearFlag cannot reset it");
        }

        [Test]
        public void Another_pair_is_still_offered_the_same_generic()
        {
            var db = Db(Scene("g", "any", "any", 1));
            var s = Save("knight", "mage", "cleric"); Bond(s, "knight", "mage");
            s.Flags.Add(DialogueRunner.PairSceneFlag("g", "knight", "mage"));
            var offered = DialogueRunner.BondScenes(db, s);
            Assert.That(offered.Count, Is.EqualTo(2));
            Assert.That(offered.All(o => new[] { o.With, o.Other }.Contains("cleric")));
        }

        // ── PlayBondScenesHeadless ─────────────────────────────────────────────

        [Test]
        public void PlayBondScenesHeadless_plays_the_unlocked_generics_with_their_pair()
        {
            var db = Db(Scene("one", "role:Tank", "any", 1), Scene("two", "any", "any", 2), Scene("three", "any", "any", 3));
            var s = Save("knight", "mage", "cleric"); Bond(s, "knight", "mage");
            DialogueRunner.PlayBondScenesHeadless(db, s);
            Assert.That(s.Flags, Does.Contain(DialogueRunner.PairSceneFlag("one", "knight", "mage")));
            Assert.That(s.Flags, Does.Contain(DialogueRunner.PairSceneFlag("two", "knight", "mage")));
            Assert.That(s.Flags.Any(f => f.StartsWith("pair:") && f.Contains("cleric")), Is.False, "pairs without cohesion stay locked");
            Assert.That(DialogueRunner.BondScenes(db, s).Where(b => b.Unlocked), Is.Empty);
            Assert.That(DialogueRunner.PlayBondScenesHeadless(db, s), Is.Empty, "nothing left to play");
        }
    }
}
