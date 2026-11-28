using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T1917: a hero's own arrangement of the Abilities sheet, laid over the default tree, and its save (v31).</summary>
    public class ActionLayoutTests
    {
        private static ActionMove M(string key, int tab, string category) =>
            new ActionMove { Tab = tab, Category = category, Node = new ActionNode { Name = key, Key = key, Slot = 0 } };

        // Four skills of two categories fold by default (more than FolderAbove), one spell, one item.
        private static List<ActionMove> Kit() => new List<ActionMove>
        {
            M("slash", 0, "Physical"), M("cleave", 0, "Physical"), M("ember", 0, "Fire"), M("blaze", 0, "Fire"),
            M("frost", 1, "Ice"), M("item:potion", 2, "Physical"),
        };

        private static string Shape(ActionNode n) =>
            n.IsMove ? n.Key : n.Name + "(" + string.Join(",", n.Children.Select(Shape)) + ")";

        [Test]
        public void No_layout_is_the_default_fold()
        {
            var tree = ActionTree.Arrange(Kit(), null);
            Assert.That(Shape(tree[0]), Is.EqualTo("Abilities(Physical(slash,cleave),Fire(ember,blaze))"));
            Assert.That(Shape(tree[1]), Is.EqualTo("Spells(frost)"));
            Assert.That(ActionTree.Arrange(Kit(), new ActionLayout()).Select(Shape), Is.EqualTo(tree.Select(Shape)));
        }

        [Test]
        public void Pins_sit_on_top_of_the_abilities_tab_over_the_default_fold()
        {
            var l = new ActionLayout();
            l.Pin("frost", true);
            l.Pin("ember", true);
            var tree = ActionTree.Arrange(Kit(), l);
            Assert.That(Shape(tree[0]), Is.EqualTo("Abilities(frost,ember,slash,cleave,blaze)"), "three left, so no longer folded");
            Assert.That(Shape(tree[1]), Is.EqualTo("Spells()"));
            Assert.That(ActionTree.PathTo(tree, 0), Is.Not.Null);
        }

        [Test]
        public void Renamed_reordered_and_new_folders_survive_the_merge()
        {
            var l = ActionLayout.Capture(ActionTree.Arrange(Kit(), null, keepEmpty: true));
            Assert.That(l.Rename(0, "Fire", "Ninjutsu"), Is.True);
            Assert.That(l.AddFolder(0, "Favourites"), Is.True);
            l.Place("slash", 0, "Favourites", 0);
            l.MoveRoot(0, 2, 0);
            l.Place("cleave", 0, null, 1);
            var shape = Shape(ActionTree.Arrange(Kit(), l, keepEmpty: true)[0]);
            Assert.That(shape, Is.EqualTo("Abilities(Favourites(slash),cleave,Physical(),Ninjutsu(ember,blaze))"));
            Assert.That(Shape(ActionTree.Arrange(Kit(), l)[0]), Is.EqualTo("Abilities(Favourites(slash),cleave,Ninjutsu(ember,blaze))"),
                "an empty folder is dropped from the battle sheet");
        }

        [Test]
        public void Unknown_moves_drop_and_new_moves_join_their_category_folder()
        {
            var l = ActionLayout.Capture(ActionTree.Arrange(Kit(), null, keepEmpty: true));
            l.Rename(0, "Fire", "Ninjutsu");
            l.Pin("gone", true);
            var kit = Kit().Where(m => m.Node.Key != "blaze").ToList();
            kit.Add(M("inferno", 0, "Fire"));
            kit.Add(M("smite", 0, "Holy"));
            Assert.That(Shape(ActionTree.Arrange(kit, l)[0]), Is.EqualTo("Abilities(Physical(slash,cleave),Ninjutsu(ember,inferno),smite)"));
        }

        [Test]
        public void A_move_never_leaves_its_tab_through_a_saved_folder()
        {
            var l = new ActionLayout();
            l.Folders.Add(new ActionFolder { Tab = 0, Name = "Mixed", Moves = { "frost", "slash" } });
            var tree = ActionTree.Arrange(Kit(), l);
            Assert.That(Shape(tree[0]), Does.StartWith("Abilities(Mixed(slash)"));
            Assert.That(Shape(tree[1]), Is.EqualTo("Spells(frost)"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("A name far longer than twenty")]
        [TestCase("physical")]
        public void A_bad_folder_name_is_refused(string name)
        {
            var l = ActionLayout.Capture(ActionTree.Arrange(Kit(), null, keepEmpty: true));
            Assert.That(l.Rename(0, "Fire", name), Is.False);
            Assert.That(l.AddFolder(0, name), Is.False);
            Assert.That(l.Folders.Any(f => f.Name == "Fire"), Is.True);
        }

        [Test]
        public void Removing_a_folder_leaves_its_moves_loose_in_place()
        {
            var l = ActionLayout.Capture(ActionTree.Arrange(Kit(), null, keepEmpty: true));
            l.RemoveFolder(0, "Physical");
            Assert.That(Shape(ActionTree.Arrange(Kit(), l)[0]), Is.EqualTo("Abilities(slash,cleave,Fire(ember,blaze))"));
        }

        [Test]
        public void A_v30_save_migrates_to_an_empty_layout_per_hero()
        {
            var old = new SaveData { Version = 30 };
            old.Roster.Add(new HeroSave { ClassId = "barbarian" });
            var j = JObject.Parse(SaveFiles.Serialize(old));
            ((JObject)j["Roster"][0]).Remove("Actions");
            string json = j.ToString();
            Assume.That(json, Does.Not.Contain("\"Actions\""));
            var back = SaveMigrations.Migrate(SaveFiles.Deserialize(json));
            Assert.That(back.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(back.Hero("barbarian").Actions, Is.Not.Null);
            Assert.That(back.Hero("barbarian").Actions.IsDefault(), Is.True);
        }

        [Test]
        public void A_layout_round_trips_the_save_and_a_hand_edited_one_is_tidied()
        {
            var s = new SaveData();
            var h = new HeroSave { ClassId = "barbarian" };
            h.Actions.AddFolder(0, "Ninjutsu");
            h.Actions.Place("slash", 0, "Ninjutsu", 0);
            h.Actions.Pin("ember", true);
            h.Actions.Folders.Add(new ActionFolder { Tab = 7, Name = "Nowhere" });
            h.Actions.Folders.Add(new ActionFolder { Tab = 1, Name = new string('x', 40), Moves = null });
            s.Roster.Add(h);
            var back = SaveMigrations.Migrate(SaveFiles.Deserialize(SaveFiles.Serialize(s))).Hero("barbarian").Actions;
            Assert.That(back.Pins, Is.EqualTo(new[] { "ember" }));
            Assert.That(back.Folders.Select(f => f.Name), Is.EqualTo(new[] { "Ninjutsu", new string('x', ActionLayout.MaxName) }));
            Assert.That(back.Folders[0].Moves, Is.EqualTo(new[] { "slash" }));
            Assert.That(back.Folders[1].Moves, Is.Empty);
        }

        [Test]
        public void A_saved_bar_out_of_battle_gives_the_battle_tabs_names_and_keys()
        {
            var db = TestKit.Real;
            var skill = db.Abilities.Values.First(a => a.Kind == AbilityKind.Skill);
            var spell = db.Abilities.Values.First(a => a.Kind == AbilityKind.Spell);
            var item = db.Items.Values.First(i => i.Type == ItemType.Consumable && !i.OutOfBattleOnly && i.OnUseAbility == null && i.BaseHealing > 0);
            var bar = new List<string> { spell.Id, null, "item:" + item.Id, skill.Id, spell.Id };
            var moves = ActionTree.MovesOf(db, null, bar, 4);
            Assert.That(moves.Select(m => (m.Tab, m.Node.Slot, m.Node.Key)), Is.EqualTo(new[]
            {
                (1, 0, spell.Id), (2, 2, "item:" + item.Id), (0, 3, skill.Id),
            }), "empty and locked slots are left out");
            Assert.That(moves[1].Node.Name, Is.EqualTo(item.Name));
            Assert.That(moves[2].Category, Is.EqualTo(string.IsNullOrEmpty(skill.Category) ? skill.DamageType.ToString() : skill.Category));
            Assert.That(ActionTree.Key(ActionTree.Slot("weapon:sword")), Is.EqualTo("weapon:sword"));
            Assert.That(ActionTree.Key(ActionTree.Slot("item:potion:3")), Is.EqualTo("item:potion"));
        }
    }
}
