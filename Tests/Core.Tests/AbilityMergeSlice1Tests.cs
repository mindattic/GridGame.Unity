using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T974 (BIBLE §25.6.6.1, slice 1: the audit's Vanguard, Blade and Brawn rows). Each folded move left
    /// abilities.json as a MergedInto alias stub, every class, loadout and attack-type entry names the survivor, its board
    /// node became an Open slot, and each hero whose signature was folded has a new live signature on its Start path.
    /// Held back from this slice: the two rows that drop a side effect the owner flagged (Holy Bulwark's party reach,
    /// Contract Kill's boss bonus) and berserk into Blood Rage (a monster-wide passive), which GG-T1126 then folded for heroes only.</summary>
    public class AbilityMergeSlice1Tests
    {
        private static readonly Dictionary<string, string> Merged = new Dictionary<string, string>
        {
            ["warchief_banner"] = "warlord_rally_point",
            ["tank_juggernaut"] = "juggernaut_overrun",
            ["myrmidon_phalanx"] = "grappling_hook",
            ["swordmaster_perfectcut"] = "duelist_decisivecut",
            ["odachi_iaijutsu"] = "duelist_decisivecut",
            ["lancer_chargeline"] = "skirmisher_runningriot",
            ["ronin_masterless"] = "skirmisher_hitandrun",
            ["slasher_flurry"] = "brawler_wide_cleave",
            ["basher_skullcrack"] = "berserker_skullrattle",
            ["streetfighter_dirtytrick"] = "berserker_skullrattle",
            ["bruiser_haymaker"] = "combobrawler_knockoutcombo",
            ["prizefighter_knockout"] = "combobrawler_knockoutcombo",
            ["fighter_combo"] = "combobrawler_onetwoflurry",
            ["pugilist_jabcross"] = "combobrawler_onetwoflurry",
        };

        /// <summary>The new signature of each hero whose old one was folded.</summary>
        private static readonly Dictionary<string, string> Signatures = new Dictionary<string, string>
        {
            ["warchief"] = "warlord_rally_point", ["tank"] = "juggernaut_overrun", ["myrmidon"] = "grappling_hook",
            ["swordmaster"] = "duelist_decisivecut", ["odachi"] = "brawler_guard_break", ["lancer"] = "skirmisher_runningriot",
            ["ronin"] = "skirmisher_hitandrun", ["slasher"] = "brawler_wide_cleave", ["basher"] = "berserker_skullrattle",
            ["bruiser"] = "blood_rage", ["streetfighter"] = "dirty_fighting", ["prizefighter"] = "combobrawler_knockoutcombo",
            ["fighter"] = "combobrawler_onetwoflurry", ["pugilist"] = "counterpunch",
        };

        [Test]
        public void Merged_ids_are_aliases_not_abilities()
        {
            foreach (var kv in Merged)
            {
                Assert.That(Real.Abilities.ContainsKey(kv.Key), Is.False, kv.Key);
                Assert.That(Real.AbilityAliases.TryGetValue(kv.Key, out var to), Is.True, kv.Key);
                Assert.That(to, Is.EqualTo(kv.Value), kv.Key);
                Assert.That(Real.ResolveAbility(kv.Key), Is.EqualTo(kv.Value), kv.Key);
                Assert.That(Real.Abilities.ContainsKey(kv.Value), Is.True, kv.Value);
            }
        }

        [Test]
        public void No_class_loadout_or_attack_type_names_a_merged_id()
        {
            foreach (var c in Real.Classes.Values)
            {
                Assert.That(c.Abilities.Where(Merged.ContainsKey), Is.Empty, c.Id);
                var loadout = c.DefaultLoadout.Where(s => s != null).ToList();
                Assert.That(loadout.Where(Merged.ContainsKey), Is.Empty, c.Id);
                Assert.That(loadout.Distinct().Count(), Is.EqualTo(loadout.Count), $"{c.Id} loadout repeats a move");
            }
            Assert.That(Real.AttackTypes.Abilities.Keys.Where(Merged.ContainsKey), Is.Empty);
            Assert.That(ContentLoader.Validate(Real).Where(p => Merged.Keys.Any(p.Contains)), Is.Empty);
        }

        [Test]
        public void Each_member_class_holds_its_survivor_and_its_signature()
        {
            foreach (var kv in Signatures)
                Assert.That(Real.Classes[kv.Key].Abilities, Does.Contain(kv.Value), kv.Key);
            Assert.That(Real.Classes["odachi"].Abilities, Does.Contain("duelist_decisivecut"));
            Assert.That(Real.Classes["bruiser"].Abilities, Does.Contain("combobrawler_knockoutcombo"));
            Assert.That(Real.Classes["streetfighter"].Abilities, Does.Contain("berserker_skullrattle"));
            Assert.That(Real.Classes["pugilist"].Abilities, Does.Contain("combobrawler_onetwoflurry"));
            Assert.That(Real.Classes["myrmidon"].Abilities, Does.Contain("grappling_hook"));
        }

        [Test]
        public void Knockout_combo_carries_the_knockout_punch_sleep()
        {
            var a = Real.Ability("combobrawler_knockoutcombo");
            Assert.That(a.ApplyBuff, Is.EqualTo("sleep"));
            Assert.That(a.ApplyBuffChance, Is.EqualTo(0.5f));
            Assert.That(a.BaseDamage, Is.EqualTo(16));
        }

        [Test]
        public void The_board_frees_merged_nodes_and_keeps_every_signature_on_its_start()
        {
            var hex = Real.SkillHex;
            Assert.That(hex, Is.Not.Null);
            Assert.That(hex.Nodes.Where(n => n.Ability != null && Merged.ContainsKey(n.Ability)), Is.Empty);
            Assert.That(hex.Nodes.Count(n => n.Kind == SkillHexNodeKind.Open && (n.Note ?? "").Contains("GG-T974")), Is.EqualTo(Merged.Count));
            foreach (var h in hex.Heroes.Where(h => h.Class != null && Signatures.ContainsKey(h.Class)))
            {
                Assert.That(h.Signature, Is.EqualTo(Signatures[h.Class]), h.Class);
                var node = hex.Nodes.Single(n => n.Kind == SkillHexNodeKind.Ability && n.Ability == h.Signature);
                Assert.That(h.Start.Any(c => c[0] == node.Q && c[1] == node.R), Is.True, $"{h.Class} Start holds {h.Signature}");
            }
            Assert.That(ContentLoader.Validate(Real).Where(p => p.StartsWith("skillhex")), Is.Empty);
        }

        /// <summary>GG-T1126: berserk folds into Blood Rage for heroes only. Monsters keep berserk (+35% below half HP), so
        /// no monster plays differently; the Barbarian holds Blood Rage, the Tome teaches it, and berserk's board node is
        /// an Open slot. berserk stays a live ability (not an alias stub) for the monsters and for saves that learned it.</summary>
        [Test]
        public void Berserk_folds_into_blood_rage_for_heroes_only()
        {
            Assert.That(Real.Abilities.ContainsKey("berserk"), Is.True);
            Assert.That(Real.AbilityAliases.ContainsKey("berserk"), Is.False);
            Assert.That(Real.Classes.Values.Where(c => c.Team != Team.Enemy && c.Abilities.Contains("berserk")).Select(c => c.Id), Is.Empty);
            Assert.That(Real.Classes["barbarian"].Abilities, Does.Contain("blood_rage"));
            foreach (var id in new[] { "oni00", "purpleprototype00", "werewolf00", "sandmaw", "minotaur04", "mountaintroll" })
                Assert.That(Real.Classes[id].Abilities, Does.Contain("berserk"), id);
            Assert.That(Real.Item("tome_berserk").TeachesAbility, Is.EqualTo("blood_rage"));
            Assert.That(Real.SkillHex.Nodes.Where(n => n.Ability == "berserk"), Is.Empty);
            Assert.That(Real.SkillHex.Nodes.Count(n => n.Kind == SkillHexNodeKind.Open && (n.Note ?? "").Contains("GG-T1126")), Is.EqualTo(1));
        }
    }
}
