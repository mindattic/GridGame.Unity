using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T975 (BIBLE §25.6.6.1, slice 2: the audit's Shadow, Wild and Machina rows). Each folded move left
    /// abilities.json as a MergedInto alias stub, every class, loadout and attack-type entry names the survivor, and each
    /// hero whose signature was folded holds a new signature that is its own (no other class holds it) on or beside its
    /// spot. Hex Shot is the one new family id. Held back: the two ninja element-variant rows (ninja_fang, ninja_edge),
    /// striker_finishingblow and technician_diagnostic (no unique move of their own near their spot), and chill_aura
    /// (monsters hold it).</summary>
    public class AbilityMergeSlice2Tests
    {
        private static readonly Dictionary<string, string> Merged = new Dictionary<string, string>
        {
            ["assassain_killingblow"] = "duelist_exploit_opening",
            ["ripper_rend"] = "opportunist_exploit",
            ["bluelion_roar"] = "primalpack_rally",
            ["hexsniper_piercingshot"] = "hex_shot",
            ["hexwarden_snareshot"] = "hex_shot",
            ["hexstalker_fadingshot"] = "hex_shot",
            ["trapper_binding_shot"] = "hex_shot",
            ["marksman_deadeye_shot"] = "ranged_quickshot",
            ["skirmisher_rapid_barrage"] = "ranged_calledshot",
            ["marksman_piercing_volley"] = "ranged_suppressingfire",
            ["skirmisher_covering_volley"] = "ranged_suppressingfire",
            ["fieldcaster_wardgrid"] = "fieldeng_mine",
            ["operative_shock"] = "fieldeng_overdrive",
            ["tinkerer_juryrig"] = "fieldeng_patch",
        };

        /// <summary>The new signature of each hero whose old one was folded.</summary>
        private static readonly Dictionary<string, string> Signatures = new Dictionary<string, string>
        {
            ["assassain"] = "assassain_shadow_swap", ["ripper"] = "ripper_flay", ["bluelion"] = "bluelion_intimidate",
            ["hexsniper"] = "marksman_execution_shot", ["hexwarden"] = "trapper_snare_net", ["hexstalker"] = "hexstalker_flare_shot",
            ["operative"] = "operative_timebomb", ["tinkerer"] = "tinkerer_magnetize",
        };

        /// <summary>Ids this slice leaves live on purpose.</summary>
        private static readonly string[] HeldBack =
        {
            "blackninja_shadowfang", "blueninja_frostfang", "chromaninja_prismfang", "greenninja_venomfang", "redninja_flamefang",
            "yellowninja_shockfang", "nighthunter_nightstalk", "shadow_edge", "rime_edge", "ember_edge", "static_edge",
            "striker_finishingblow", "technician_diagnostic", "chill_aura",
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
            foreach (var id in HeldBack)
                Assert.That(Real.Abilities.ContainsKey(id), Is.True, id + " is held back from slice 2");
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
        public void Each_member_class_holds_its_survivor_and_a_signature_of_its_own()
        {
            foreach (var kv in Signatures)
            {
                Assert.That(Real.Classes[kv.Key].Abilities, Does.Contain(kv.Value), kv.Key);
                var others = Real.Classes.Values.Where(c => c.Id != kv.Key && c.Abilities.Contains(kv.Value)).Select(c => c.Id);
                Assert.That(others, Is.Empty, $"{kv.Key}'s signature {kv.Value} is its own");
            }
            foreach (var h in new[] { "hexsniper", "hexwarden", "hexstalker" })
                Assert.That(Real.Classes[h].Abilities, Does.Contain("hex_shot"), h);
            Assert.That(Real.Classes["assassain"].Abilities, Does.Contain("duelist_exploit_opening"));
            Assert.That(Real.Classes["ripper"].Abilities, Does.Contain("opportunist_exploit"));
            Assert.That(Real.Classes["bluelion"].Abilities, Does.Contain("primalpack_rally"));
            Assert.That(Real.Classes["operative"].Abilities, Does.Contain("fieldeng_overdrive"));
            Assert.That(Real.Classes["operative"].Abilities, Does.Contain("fieldeng_mine"));
            Assert.That(Real.Classes["drifter"].Abilities, Does.Contain("fieldeng_mine"));
            Assert.That(Real.Classes["tinkerer"].Abilities, Does.Contain("fieldeng_patch"));
        }

        [Test]
        public void Survivors_carry_the_audit_numbers()
        {
            var hex = Real.Ability("hex_shot");
            Assert.That(hex.DamageType, Is.EqualTo(DamageType.Dark));
            Assert.That(hex.BaseDamage, Is.EqualTo(14));
            Assert.That(hex.ApplyBuff, Is.EqualTo("slowed"));
            Assert.That(hex.CooldownTurns, Is.EqualTo(3));
            var patch = Real.Ability("fieldeng_patch");
            Assert.That(patch.BaseHeal, Is.EqualTo(10));
            Assert.That(patch.RemovesDebuffs, Is.True, "Field Patch gains Jury-Rig's Cleanse");
            Assert.That(patch.CooldownTurns, Is.EqualTo(3));
        }

        [Test]
        public void The_board_frees_merged_nodes_and_keeps_every_signature_on_its_start()
        {
            var hex = Real.SkillHex;
            Assert.That(hex, Is.Not.Null);
            Assert.That(hex.Nodes.Where(n => n.Ability != null && Merged.ContainsKey(n.Ability)), Is.Empty);
            Assert.That(hex.Nodes.Count(n => n.Kind == SkillHexNodeKind.Ability && n.Ability == "hex_shot"), Is.EqualTo(1));
            foreach (var h in hex.Heroes.Where(h => h.Class != null && Signatures.ContainsKey(h.Class)))
            {
                Assert.That(h.Signature, Is.EqualTo(Signatures[h.Class]), h.Class);
                var node = hex.Nodes.Single(n => n.Kind == SkillHexNodeKind.Ability && n.Ability == h.Signature);
                Assert.That(h.Start.Any(c => c[0] == node.Q && c[1] == node.R), Is.True, $"{h.Class} Start holds {h.Signature}");
            }
            Assert.That(ContentLoader.Validate(Real).Where(p => p.StartsWith("skillhex")), Is.Empty);
        }
    }
}
