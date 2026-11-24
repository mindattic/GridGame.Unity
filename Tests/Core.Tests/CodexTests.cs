using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T923: Codex unlock tracking (SaveData.Codex / CodexRead, save v16).</summary>
    public class CodexTests
    {
        [Test]
        public void Unlock_is_once_case_insensitive_and_new_until_read()
        {
            var s = new SaveData();
            Assert.That(Codex.Unlock(s, CodexKinds.Foe, "Slime01"), Is.True);
            Assert.That(Codex.Unlock(s, CodexKinds.Foe, "slime01"), Is.False);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Foe, "SLIME01"), Is.True);
            Assert.That(Codex.IsNew(s, CodexKinds.Foe, "slime01"), Is.True);
            Assert.That(Codex.NewCount(s), Is.EqualTo(1));
            Assert.That(Codex.MarkRead(s, CodexKinds.Foe, "slime01"), Is.True);
            Assert.That(Codex.IsNew(s, CodexKinds.Foe, "slime01"), Is.False);
            Assert.That(Codex.NewCount(s, CodexKinds.Foe), Is.EqualTo(0));
        }

        [Test]
        public void A_locked_entry_cannot_be_marked_read()
        {
            var s = new SaveData();
            Assert.That(Codex.MarkRead(s, CodexKinds.Item, "potion"), Is.False);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Item, "potion"), Is.False);
        }

        [Test]
        public void Gaining_an_item_unlocks_it_and_losing_it_does_not_lock_it()
        {
            var s = new SaveData();
            s.AddItem("potion", 2);
            s.AddItem("potion", -2);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Item, "potion"), Is.True);
            Assert.That(Codex.Unlocked(s, CodexKinds.Item), Is.EqualTo(new List<string> { "potion" }));
        }

        [Test]
        public void Sync_derives_entries_from_the_save()
        {
            var c = TestKit.Real;
            var first = c.CampaignStages().First(st => !st.IsTest);
            var s = new SaveData { HighestClearedStage = 0 };
            s.ClearedStages.Add(first.Id);
            s.Roster.Add(new HeroSave { ClassId = "paladin", Known = { "smite" } });
            s.Bestiary["slime01"] = new BestiaryEntry { Seen = true };
            s.Bestiary["goblin"] = new BestiaryEntry();
            s.Flags.Add("npc:merchant");
            s.Flags.Add("story:ending");
            s.SeenCrawls.Add("chapter:one");
            s.SeenDialogues.Add("intro");

            Assert.That(Codex.Sync(c, s), Is.GreaterThan(0));
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Place, first.Id), Is.True);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Place, c.ThemeOf(first.Id).Id), Is.True);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Character, "paladin"), Is.True);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Character, "merchant"), Is.True);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Ability, "smite"), Is.True);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Foe, "slime01"), Is.True);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Foe, "goblin"), Is.False, "an unseen Bestiary entry stays locked");
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Story, "ending"), Is.True);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Story, "chapter/one"), Is.True);
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Story, "dialogue/intro"), Is.True);
            var faction = c.FactionOf("paladin");
            if (faction != null) Assert.That(Codex.IsUnlocked(s, CodexKinds.Faction, faction.Id), Is.True);
            var later = c.CampaignStages().Last();
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Place, later.Id), Is.False, "an unreached stage stays locked");
            Assert.That(Codex.Sync(c, s), Is.EqualTo(0), "idempotent");
        }

        [Test]
        public void A_v15_save_back_fills_the_codex_as_read()
        {
            var s = new SaveData();
            s.Bestiary["slime01"] = new BestiaryEntry { Seen = true };
            s.Flags.Add("npc:blacksmith");
            var json = JObject.Parse(SaveFiles.Serialize(s));
            json.Remove(nameof(SaveData.Codex));
            json.Remove(nameof(SaveData.CodexRead));
            json[nameof(SaveData.Version)] = 15;
            var back = SaveMigrations.Migrate(SaveFiles.Deserialize(json.ToString()), TestKit.Real);
            Assert.That(back.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(Codex.IsUnlocked(back, CodexKinds.Foe, "slime01"), Is.True);
            Assert.That(Codex.IsUnlocked(back, CodexKinds.Character, "blacksmith"), Is.True);
            Assert.That(Codex.IsNew(back, CodexKinds.Foe, "slime01"), Is.False, "back-filled entries are taken as read");
        }

        [Test]
        public void A_current_save_round_trips_and_new_unlocks_stay_new()
        {
            var s = new SaveData();
            Codex.Unlock(s, CodexKinds.Keyword, "burn");
            var back = SaveMigrations.Migrate(SaveFiles.Deserialize(SaveFiles.Serialize(s)), TestKit.Real);
            Assert.That(Codex.IsNew(back, CodexKinds.Keyword, "burn"), Is.True);
            back.Bestiary["slime01"] = new BestiaryEntry { Seen = true };
            back = SaveMigrations.Migrate(back, TestKit.Real);
            Assert.That(Codex.IsNew(back, CodexKinds.Foe, "slime01"), Is.True, "only the v16 back-fill marks read");
        }

        [Test]
        public void Codex_content_resolves()
        {
            // GG-T936: every codex.json Key and Related key names a known kind and an existing record; each entry has a
            // title and a body (its record's words while Prose has no text) and its links resolve; no codex problems.
            var c = TestKit.Real;
            Assert.That(c.CodexOrder, Is.Not.Empty, "codex.json ships entries");
            var problems = ContentLoader.Validate(c).Where(p => p.StartsWith("codex")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems.Take(40)));
            Assert.That(c.TextProblems.Where(p => p.Contains(ProseText.CodexKey)), Is.Empty, "a Codex body is optional");
            foreach (var e in c.CodexOrder)
            {
                Assert.That(CodexKinds.TrySplit(e.Key, out var kind, out var id), Is.True, e.Key);
                Assert.That(CodexKinds.All, Does.Contain(kind), e.Key);
                var v = c.CodexEntry(kind, id);
                Assert.That(v.Exists, Is.True, e.Key);
                Assert.That(v.Key, Is.EqualTo(e.Key));
                Assert.That(v.Title, Is.Not.Null.And.Not.Empty, e.Key);
                Assert.That(v.Body, Is.Not.Null.And.Not.Empty, e.Key);
                Assert.That(v.Related.Count, Is.EqualTo(e.Related.Count), $"{e.Key}: every related key resolves");
                Assert.That(ProseText.Keys(c).ContainsKey(CodexDef.TextKey(e.Key)), Is.True, $"{e.Key}: its body key is a Prose key");
            }
        }

        [Test]
        public void Codex_entries_fall_back_to_the_records_own_words()
        {
            var c = TestKit.Real;
            var foe = c.CodexEntry(CodexKinds.Foe, "slime01");
            Assert.That(foe.Exists, Is.True);
            Assert.That(foe.Title, Is.EqualTo(c.Class("slime01").Name));
            Assert.That(foe.Body, Is.EqualTo(c.Class("slime01").Lore));
            var g = c.GlossaryOrder.First();
            var kw = c.CodexEntry(CodexKinds.Keyword, g.Id);
            Assert.That(kw.Title, Is.EqualTo(g.Term));
            Assert.That(kw.Body, Is.EqualTo(g.Text));
            var npc = c.CodexEntry(CodexKinds.Character, "merchant");
            Assert.That(npc.Title, Is.EqualTo("Lantern Merchant"), "codex.json's Title wins");
            Assert.That(npc.Body, Is.EqualTo(c.NpcBlurbs["merchant"]));
            var unknown = c.CodexEntry(CodexKinds.Foe, "no-such-foe");
            Assert.That(unknown.Exists, Is.False);
            Assert.That(unknown.Body, Does.StartWith("(missing text: codex:foe/no-such-foe"));
            Assert.That(c.CodexRecordExists("nonsense", "slime01"), Is.False);
        }
    }
}
