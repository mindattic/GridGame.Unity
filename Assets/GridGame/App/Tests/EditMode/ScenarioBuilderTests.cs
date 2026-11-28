using System.Linq;
using GridGame.App.Dev;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using UnityEngine;

namespace GridGame.App.Tests
{
    /// <summary>GG-T1801: a scenario's generated save stands coherently at its target.</summary>
    public class ScenarioBuilderTests
    {
        private static ContentDb content;
        private static ContentDb C => content ??= ContentLoader.Load(name => Resources.Load<TextAsset>("Content/" + name)?.text);

        [Test]
        public void First_boss_resolves_to_the_greenvalley_captain()
        {
            var t = ScenarioBuilder.Resolve(C, "first-boss");
            Assert.That(t.Kind, Is.EqualTo(ScenarioKind.Battle));
            Assert.That(t.StageId, Is.EqualTo("greenvalley-03"));
            Assert.That(t.Wave, Is.EqualTo(4));
            Assert.That(t.IsBoss, Is.True);
        }

        [Test]
        public void Every_stage_before_the_target_is_cleared()
        {
            var r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget("first-boss"));
            var stages = C.CampaignStages();
            int index = stages.FindIndex(s => s.Id == "greenvalley-03");
            Assert.That(r.Save.ClearedStages.Count, Is.EqualTo(index));
            Assert.That(r.Save.HighestClearedStage, Is.EqualTo(index - 1));
            Assert.That(stages.Take(index).All(s => r.Save.ClearedStages.Contains(s.Id)), Is.True);
        }

        [Test]
        public void The_party_stands_at_the_recommended_level_plus_one_for_a_boss()
        {
            var r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget("first-boss"));
            int rec = Progression.StageRecommendedLevel(C, r.Save, C.Stage("greenvalley-03"));
            Assert.That(r.Level, Is.EqualTo(rec + 1));
            Assert.That(r.Save.Party.Select(r.Save.Hero).All(h => Progression.Level(h) >= rec + 1), Is.True);
        }

        [Test]
        public void First_boss_heroes_wield_a_weapon_of_their_own_family()
        {
            var r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget("first-boss"));
            var heroes = r.Save.Party.Select(r.Save.Hero).ToList();
            Assert.That(heroes.All(h => h.WeaponId != null), Is.True);
            Assert.That(heroes.Select(h => h.WeaponId).Distinct().Count(), Is.GreaterThan(1));
            foreach (var h in heroes)
                Assert.That(C.TryItem(h.WeaponId).WeaponClass, Is.EqualTo(ScenarioBuilder.WeaponFamily(C, h)), h.ClassId);
        }

        [Test]
        public void A_bag_upgrade_raises_the_heroes_new_badge_until_it_is_seen()
        {
            var def = ScenarioDef.Parse("{\"target\":\"camp\",\"gear\":\"none\",\"bag\":{\"eq_armor_chain\":1}}");
            var s = ScenarioBuilder.Build(C, def).Save;
            Assert.That(s.Count("eq_armor_chain"), Is.EqualTo(1));
            var heroes = NavBadges.Tabs(C, s, new SettingsSave()).First(t => t.Id == CampMenu.Heroes);
            Assert.That(heroes.Badge, Is.EqualTo(NavBadges.NewGear));
            var fresh = AutoEquip.FirstNew(C, s);
            Assert.That(fresh?.Pick.Item.Id, Is.EqualTo("eq_armor_chain"));
            AutoEquip.MarkSeen(C, s, fresh.Value.Hero);
            Assert.That(AutoEquip.FirstNew(C, s)?.Pick.Item.Id, Is.Not.EqualTo("eq_armor_chain"));
        }

        [Test]
        public void An_equipped_potion_sits_on_the_bar_and_in_the_bag()
        {
            var def = ScenarioDef.Parse("{\"target\":\"stage:greenvalley-01\",\"consumables\":{\"knight\":{\"healing_potion_basic\":2}}}");
            var r = ScenarioBuilder.Build(C, def);
            var knight = r.Save.Hero("knight");
            Assert.That(knight.Bar.Take(Progression.UnlockedSlots(r.Save)), Does.Contain("item:healing_potion_basic"));
            Assert.That(r.Save.Count("healing_potion_basic"), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void No_story_beat_is_left_pending_before_the_target()
        {
            foreach (var target in new[] { "first-boss", "overworld", "final-boss" })
            {
                var r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget(target));
                Assert.That(r.Save.TestMode, Is.False, target);
                Assert.That(DialogueRunner.PendingBeat(C, r.Save, r.Target.StageId)?.Id, Is.Null, target);
            }
        }

        [Test]
        public void A_prologue_stage_resolves_as_a_drill_target()
        {
            var t = ScenarioBuilder.Resolve(C, "stage:prologue-bridge-a");
            Assert.That(t.Kind, Is.EqualTo(ScenarioKind.Battle));
            Assert.That(t.StageId, Is.EqualTo("prologue-bridge-a"));
            Assert.That(t.Drill, Is.True);
            Assert.That(t.StageIndex, Is.EqualTo(0));
        }

        [Test]
        public void A_prologue_target_clears_the_earlier_prologue_battles_and_fields_the_stage_party()
        {
            var r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget("stage:prologue-vault"));
            Assert.That(r.Save.Party.Count, Is.EqualTo(C.Stage("prologue-vault").PartySize));
            r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget("stage:prologue-bridge-a"));
            var order = C.Campaign.Prologue;
            int at = order.IndexOf("prologue-bridge-a");
            Assert.That(order.Take(at).All(id => r.Save.ClearedDrills.Contains(id)), Is.True);
            Assert.That(r.Save.ClearedDrills.Contains("prologue-bridge-a"), Is.False);
            Assert.That(r.Save.ClearedStages.Count, Is.EqualTo(0));
            Assert.That(r.Save.Roster.Any(h => h.ClassId == "redmage"), Is.False);
        }

        [Test]
        public void A_name_that_is_no_stage_is_still_refused()
        {
            Assert.Throws<System.ArgumentException>(() => ScenarioBuilder.Resolve(C, "stage:prologue-nope"));
        }

        [Test]
        public void Open_parses_into_an_opener_and_a_path()
        {
            var def = ScenarioDef.Parse("{\"target\": \"stage:greenvalley-01\", \"open\": \"Abilities:Spells/Fire\"}");
            var open = ScenarioOpen.Parse(def.Open);
            Assert.That(open.What, Is.EqualTo("abilities"));
            Assert.That(open.Path, Is.EqualTo(new[] { "Spells", "Fire" }));
            Assert.That(ScenarioLander.Openers.ContainsKey(open.What), Is.True);

            var info = ScenarioOpen.Parse(" info ");
            Assert.That(info.What, Is.EqualTo("info"));
            Assert.That(info.Path, Is.Empty);
            Assert.That(ScenarioLander.Openers.ContainsKey(info.What), Is.True);

            Assert.That(ScenarioOpen.Parse(ScenarioDef.Parse("{}").Open), Is.Null);
        }
    }
}
