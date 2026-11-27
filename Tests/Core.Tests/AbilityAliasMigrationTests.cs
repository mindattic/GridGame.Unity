using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using GridGame.Meta;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T973 (BIBLE §25.6.6.1): merged abilities leave an alias stub in abilities.json (Id + MergedInto). The loader
    /// moves stubs into <see cref="ContentDb.AbilityAliases"/>, Validate keeps them pointing at live abilities (no chains, no
    /// cycles) and keeps every class, tree and item off them, and SaveMigrations rewrites an old save's ids to the survivor.</summary>
    public class AbilityAliasMigrationTests
    {
        /// <summary>The real content with extra rows appended to abilities.json (a private copy: TestKit.Real is shared).</summary>
        private static ContentDb WithRows(params JObject[] rows)
        {
            return ContentLoader.Load(name =>
            {
                var p = Path.Combine(ContentDir, name + ".json");
                var text = File.Exists(p) ? File.ReadAllText(p) : null;
                if (name != "abilities" || text == null || rows.Length == 0) return text;
                var arr = JArray.Parse(text);
                foreach (var r in rows) arr.Add(r);
                return arr.ToString();
            });
        }

        private static JObject Alias(string id, string into) => new JObject { ["Id"] = id, ["MergedInto"] = into };

        private static List<string> AliasProblems(ContentDb db) =>
            ContentLoader.Validate(db).Where(x => x.Contains("GG-T973") || x.StartsWith("ability alias")).ToList();

        /// <summary>A Passive with a Trigger in the real content (what ActivePassives holds).</summary>
        private static string APassive(ContentDb db) =>
            db.Abilities.Values.Where(a => a.Kind == AbilityKind.Passive && a.Trigger != null).OrderBy(a => a.Id).First().Id;

        // ── content ──

        [Test]
        public void The_real_content_has_no_alias_problems()
        {
            Assert.That(AliasProblems(Real), Is.Empty);
            foreach (var kv in Real.AbilityAliases)
                Assert.That(Real.Abilities.ContainsKey(kv.Value), Is.True, $"{kv.Key} -> {kv.Value}");
        }

        [Test]
        public void An_alias_row_is_indexed_as_an_alias_not_an_ability()
        {
            var db = WithRows(Alias("old_fireball", "fireball"));
            Assert.That(db.Abilities.ContainsKey("old_fireball"), Is.False);
            Assert.That(db.AbilityAliases["old_fireball"], Is.EqualTo("fireball"));
            Assert.That(db.ResolveAbility("OLD_FIREBALL"), Is.EqualTo("fireball"));
            Assert.That(db.ResolveAbility("heal"), Is.EqualTo("heal"));
            Assert.That(db.ResolveAbility(null), Is.Null);
            Assert.That(AliasProblems(db), Is.Empty);
        }

        [Test]
        public void An_alias_must_name_a_live_ability()
        {
            var db = WithRows(Alias("old_thing", "no_such_ability"));
            Assert.That(AliasProblems(db), Has.Some.Contains("unknown ability 'no_such_ability'"));
        }

        [Test]
        public void An_alias_may_not_chain_or_cycle()
        {
            var chain = WithRows(Alias("a_old", "b_old"), Alias("b_old", "fireball"));
            Assert.That(AliasProblems(chain), Has.Some.Contains("itself an alias"));
            var cycle = WithRows(Alias("a_old", "b_old"), Alias("b_old", "a_old"));
            Assert.That(AliasProblems(cycle).Count(x => x.Contains("itself an alias")), Is.EqualTo(2));
            var self = WithRows(Alias("a_old", "a_old"));
            Assert.That(AliasProblems(self), Has.Some.Contains("merged into itself"));
        }

        [Test]
        public void An_alias_row_carries_only_Id_and_MergedInto()
        {
            var row = Alias("old_fireball", "fireball");
            row["Name"] = "Old Fireball";
            row["Cost"] = 2;
            var db = WithRows(row);
            Assert.That(AliasProblems(db), Has.Some.Contains("carries only Id and MergedInto").And.Contains("Name").And.Contains("Cost"));
        }

        [Test]
        public void An_alias_id_cannot_also_be_a_live_ability()
        {
            var db = WithRows(Alias("heal", "fireball"));
            Assert.That(db.Abilities.ContainsKey("heal"), Is.True);
            Assert.That(AliasProblems(db), Has.Some.Contains("is also a live ability id"));
        }

        [Test]
        public void No_class_tree_or_item_may_still_name_an_alias()
        {
            var db = new ContentDb();
            db.Abilities["fireball"] = new AbilityDef { Id = "fireball", Name = "Fireball" };
            db.AbilityAliases["old_fireball"] = "fireball";
            db.Classes["mage"] = new ClassDef { Id = "mage", Abilities = { "old_fireball" }, DefaultLoadout = { "old_fireball", "item:potion" }, ChargeAbility = "old_fireball" };
            db.SkillTrees["arcana"] = new SkillTreeDef { Id = "arcana", Nodes = { new SkillNodeDef { Id = "n1", Grants = "old_fireball" } } };
            db.Items["tome_old"] = new ItemDef { Id = "tome_old", Type = ItemType.Tome, TeachesAbility = "old_fireball" };
            db.Items["dart_old"] = new ItemDef { Id = "dart_old", Type = ItemType.Consumable, OnUseAbility = "old_fireball" };
            var p = new List<string>();
            ContentLoader.ValidateAbilityAliases(db, p);
            Assert.That(p, Has.Some.StartsWith("class mage abilities"));
            Assert.That(p, Has.Some.StartsWith("class mage loadout"));
            Assert.That(p, Has.Some.StartsWith("class mage charge"));
            Assert.That(p, Has.Some.StartsWith("tree arcana node n1"));
            Assert.That(p, Has.Some.StartsWith("item tome_old tome"));
            Assert.That(p, Has.Some.StartsWith("item dart_old"));
            Assert.That(p, Has.All.Contains("name 'fireball'"));
            Assert.That(p.Count, Is.EqualTo(6), string.Join("\n", p));
        }

        // ── saves ──

        /// <summary>A v16 save (written before aliases existed) whose Warden knows abilities that were later merged.</summary>
        private static SaveData OldSave() => SaveFiles.Deserialize(@"{
  ""Version"": 16,
  ""Roster"": [ {
    ""ClassId"": ""warden"",
    ""Known"": [ ""old_fireball"", ""heal"", ""fireball"", ""old_passive"" ],
    ""Bar"": [ ""old_fireball"", null, ""old_heal"", ""heal"", ""item:potion"" ],
    ""ActivePassives"": [ ""old_passive"" ],
    ""PassivesChosen"": true
  } ],
  ""Party"": [ ""warden"" ],
  ""Codex"": [ ""ability:old_fireball"" ],
  ""CodexRead"": [ ""ability:old_fireball"" ]
}");

        private static ContentDb MergedDb(out string passive)
        {
            passive = APassive(Real);
            return WithRows(Alias("old_fireball", "fireball"), Alias("old_heal", "heal"), Alias("old_passive", passive));
        }

        [Test]
        public void An_old_save_resolves_merged_ids_to_their_survivors()
        {
            var db = MergedDb(out var passive);
            var s = SaveMigrations.Migrate(OldSave(), db);
            var h = s.Hero("warden");
            Assert.That(s.Version, Is.EqualTo(SaveData.CurrentVersion));
            // Known: rewritten in place and deduped (old_fireball became fireball, so the later fireball goes).
            Assert.That(h.Known.Take(3), Is.EqualTo(new[] { "fireball", "heal", passive }));
            Assert.That(h.Known.Count(id => id == "fireball"), Is.EqualTo(1));
            Assert.That(h.Known, Has.None.StartsWith("old_"));
            // Bar: the alias's slot keeps the survivor; one already on the bar empties the alias's slot; items untouched.
            Assert.That(h.Bar[0], Is.EqualTo("fireball"));
            Assert.That(h.Bar[2], Is.Null);
            Assert.That(h.Bar[3], Is.EqualTo("heal"));
            Assert.That(h.Bar[4], Is.EqualTo("item:potion"));
            Assert.That(h.Bar.Count, Is.EqualTo(5));
            Assert.That(h.ActivePassives, Is.EqualTo(new[] { passive }));
            Assert.That(h.PassivesChosen, Is.True);
            // Codex: the entry moves to the survivor, still read.
            Assert.That(s.Codex, Does.Contain(CodexKinds.Key(CodexKinds.Ability, "fireball")));
            Assert.That(s.Codex, Does.Not.Contain("ability:old_fireball"));
            Assert.That(s.CodexRead, Does.Contain(CodexKinds.Key(CodexKinds.Ability, "fireball")));
            Assert.That(SaveMigrations.Notes, Has.Some.Contains("GG-T973"));
        }

        [Test]
        public void Resolving_is_idempotent()
        {
            var db = MergedDb(out _);
            var s = SaveMigrations.Migrate(OldSave(), db);
            var known = s.Hero("warden").Known.ToList();
            var bar = s.Hero("warden").Bar.ToList();
            Assert.That(SaveMigrations.ResolveAbilityAliases(s, db.AbilityAliases), Is.EqualTo(0));
            SaveMigrations.Migrate(s, db);
            Assert.That(s.Hero("warden").Known, Is.EqualTo(known));
            Assert.That(s.Hero("warden").Bar, Is.EqualTo(bar));
        }

        [Test]
        public void A_current_save_still_resolves_a_later_merge()
        {
            // A merge after v17 adds an alias without a version bump: the rewrite runs on every load.
            var s = new SaveData { Roster = { new HeroSave { ClassId = "warden", Known = { "old_heal" }, Bar = { [1] = "old_heal" } } } };
            Assert.That(s.Version, Is.EqualTo(SaveData.CurrentVersion));
            SaveMigrations.Migrate(s, WithRows(Alias("old_heal", "heal")));
            var h = s.Hero("warden");
            Assert.That(h.Known, Does.Contain("heal").And.Not.Contain("old_heal"));
            Assert.That(h.Bar[1], Is.EqualTo("heal"));
        }

        [Test]
        public void Without_content_ids_are_left_alone()
        {
            var s = SaveMigrations.Migrate(OldSave());
            Assert.That(s.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(s.Roster[0].Known, Does.Contain("old_fireball"));
            Assert.That(s.Roster[0].Bar[0], Is.EqualTo("old_fireball"));
        }

        [Test]
        public void Retired_abilities_still_drop()
        {
            var s = OldSave();
            s.Roster[0].Known.Add("scan");
            s.Roster[0].Bar[1] = "scan";
            SaveMigrations.Migrate(s, MergedDb(out _));
            Assert.That(s.Hero("warden").Known, Does.Not.Contain("scan"));
            Assert.That(s.Hero("warden").Bar[1], Is.Null);
        }
    }
}
