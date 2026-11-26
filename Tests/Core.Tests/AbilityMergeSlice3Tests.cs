using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T976 (BIBLE §25.6.6.1, slice 3: the audit's Arcane, Occult and Grace rows). Each folded move left
    /// abilities.json as a MergedInto alias stub, every class and loadout names the survivor, and each hero whose
    /// signature was folded, or became a survivor other classes now hold, takes a new signature that is its own (no
    /// other class holds it) on or beside its spot. Held back (GG-T1135): courier_relay, jadeknight_resolve,
    /// cleric_grace, whitewitch_ward and sage_insight (no unique move of their hero's own near its spot), and the
    /// owner-flagged hinder and alchemist_transmute.</summary>
    public class AbilityMergeSlice3Tests
    {
        private static readonly Dictionary<string, string> Merged = new Dictionary<string, string>
        {
            ["redmage_fury"] = "meteor",
            ["ritualist_bloodrite"] = "hexweaver_festeringmark",
            ["vampire_enervate"] = "attrition_bloodletting",
            ["deathguard_venomous_grip"] = "attrition_bloodletting",
            ["darktemplar_rite"] = "blackwitch_shadowbolt",
            ["monk_peace"] = "whiteninja_moonlit",
            ["herald_rebloom"] = "inner_calm",
        };

        /// <summary>The new signature of each re-signed hero.</summary>
        private static readonly Dictionary<string, string> Signatures = new Dictionary<string, string>
        {
            ["redmage"] = "firewall", ["ritualist"] = "blood_price", ["darktemplar"] = "dark_retribution",
            ["monk"] = "monk_redirectflow", ["vampire"] = "attrition_soulfeast", ["blackwitch"] = "blackwitch_curselink",
            ["whiteninja"] = "whiteninja_substitution",
        };

        /// <summary>Ids this slice leaves live on purpose.</summary>
        private static readonly string[] HeldBack =
        {
            "courier_relay", "jadeknight_resolve", "cleric_grace", "whitewitch_ward", "sage_insight", "hinder", "alchemist_transmute",
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
                Assert.That(Real.Abilities.ContainsKey(id), Is.True, id + " is held back from slice 3");
        }

        [Test]
        public void No_class_or_loadout_names_a_merged_id()
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
                var others = Real.Classes.Values.Where(c => c.Id != kv.Key && (c.Abilities.Contains(kv.Value) || c.DefaultLoadout.Contains(kv.Value))).Select(c => c.Id);
                Assert.That(others, Is.Empty, $"{kv.Key}'s signature {kv.Value} is its own");
            }
            Assert.That(Real.Classes["redmage"].Abilities, Does.Contain("meteor"));
            Assert.That(Real.Classes["ritualist"].Abilities, Does.Contain("hexweaver_festeringmark"));
            foreach (var c in new[] { "vampire", "darktemplar", "jadeknight" })
                Assert.That(Real.Classes[c].Abilities, Does.Contain("attrition_bloodletting"), c);
            Assert.That(Real.Classes["darktemplar"].Abilities, Does.Contain("blackwitch_shadowbolt"));
            Assert.That(Real.Classes["monk"].Abilities, Does.Contain("whiteninja_moonlit"));
            Assert.That(Real.Classes["herald"].Abilities, Does.Contain("inner_calm"));
        }

        [Test]
        public void Inner_calm_takes_rebloom_numbers()
        {
            var calm = Real.Ability("inner_calm");
            Assert.That(calm.Trigger, Is.Not.Null);
            Assert.That(calm.Trigger.On, Is.EqualTo(TriggerOn.TurnStart));
            Assert.That(calm.Trigger.Condition, Is.Not.Null);
            Assert.That(calm.Trigger.Condition.SelfHpBelow, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(calm.Trigger.Effects.Single().Amount, Is.EqualTo(0.05f).Within(1e-4f));
        }

        [Test]
        public void The_board_frees_merged_nodes_and_keeps_every_signature_on_its_start()
        {
            var hex = Real.SkillHex;
            Assert.That(hex, Is.Not.Null);
            Assert.That(hex.Nodes.Where(n => n.Ability != null && Merged.ContainsKey(n.Ability)), Is.Empty);
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
