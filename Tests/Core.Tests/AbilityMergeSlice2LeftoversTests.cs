using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1134 (BIBLE §25.6.6.1, slice 2 leftovers). The engine seam: a class carries an Element and an ability
    /// flagged ElementFromCaster takes it, with that element's row of ElementVariants (numbers and rider). The four ninja
    /// edges fold into the new ninja_edge, and the Black Ninja's Shadow Fang into the new ninja_fang (the Black Ninja is
    /// re-signed with Smoke Vanish). Held back (GG-T1136): the other five ninja fangs and Night Stalk (their heroes have no
    /// other unique move of their own near their spot), striker_finishingblow and technician_diagnostic (the same), and
    /// chill_aura (the Ice Mauler and Yeti hold it).</summary>
    public class AbilityMergeSlice2LeftoversTests
    {
        private static readonly Dictionary<string, string> Merged = new Dictionary<string, string>
        {
            ["shadow_edge"] = "ninja_edge",
            ["rime_edge"] = "ninja_edge",
            ["ember_edge"] = "ninja_edge",
            ["static_edge"] = "ninja_edge",
            ["blackninja_shadowfang"] = "ninja_fang",
        };

        /// <summary>Ids this slice leaves live on purpose.</summary>
        private static readonly string[] HeldBack =
        {
            "blueninja_frostfang", "chromaninja_prismfang", "greenninja_venomfang", "redninja_flamefang", "yellowninja_shockfang",
            "nighthunter_nightstalk", "striker_finishingblow", "technician_diagnostic", "chill_aura", "venom_edge",
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
                Assert.That(Real.Abilities.ContainsKey(id), Is.True, id + " is held back");
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
            Assert.That(ContentLoader.Validate(Real).Where(p => Merged.Keys.Any(p.Contains) || p.Contains("GG-T1134")), Is.Empty);
        }

        [Test]
        public void The_ninjas_carry_their_element_and_the_family_moves()
        {
            var elements = new Dictionary<string, DamageType>
            {
                ["blackninja"] = DamageType.Dark, ["blueninja"] = DamageType.Ice, ["redninja"] = DamageType.Fire,
                ["yellowninja"] = DamageType.Lightning, ["greenninja"] = DamageType.Poison, ["chromaninja"] = DamageType.Physical,
                ["nighthunter"] = DamageType.Dark,
            };
            foreach (var kv in elements) Assert.That(Real.Classes[kv.Key].Element, Is.EqualTo(kv.Value), kv.Key);
            foreach (var c in new[] { "blackninja", "blueninja", "redninja", "yellowninja" })
                Assert.That(Real.Classes[c].Abilities, Does.Contain("ninja_edge"), c);
            Assert.That(Real.Classes["greenninja"].Abilities, Does.Contain("venom_edge"), "venom_edge stays the Poison edge");
            Assert.That(Real.Classes["blackninja"].Abilities, Does.Contain("ninja_fang"));
            Assert.That(Real.Classes["blackninja"].DefaultLoadout, Does.Contain("ninja_fang"));
            Assert.That(Real.Classes.Values.Where(c => c.Element != null).Select(c => c.Id), Is.EquivalentTo(elements.Keys));
        }

        [Test]
        public void Ninja_fang_hits_in_the_wielders_element_with_its_rider()
        {
            var fang = Real.Ability("ninja_fang");
            Assert.That(fang.ElementFromCaster, Is.True);
            Assert.That(fang.CooldownTurns, Is.EqualTo(3));
            Assert.That(fang.AdjacentOnly, Is.True);
            var black = fang.ForCaster(Real.Classes["blackninja"]);
            Assert.That(black.Id, Is.EqualTo("ninja_fang"), "same id: cooldowns, bars and saves are untouched");
            Assert.That(black.DamageType, Is.EqualTo(DamageType.Dark));
            Assert.That(black.BaseDamage, Is.EqualTo(14));
            Assert.That(black.ApplyBuff, Is.EqualTo("blinded"));
            Assert.That(black, Is.SameAs(fang.ForCaster(Real.Classes["blackninja"])), "one cached copy per element");
            Assert.That(black.ForCaster(Real.Classes["redninja"]), Is.SameAs(black), "a resolved copy stays as it is");
            var expect = new Dictionary<DamageType, string>
            {
                [DamageType.Ice] = "slowed", [DamageType.Poison] = "poisoned", [DamageType.Fire] = "burning", [DamageType.Lightning] = "slowed",
            };
            foreach (var kv in expect)
            {
                var v = fang.ForElement(kv.Key);
                Assert.That(v.DamageType, Is.EqualTo(kv.Key));
                Assert.That(v.BaseDamage, Is.EqualTo(14), kv.Key.ToString());
                Assert.That(v.ApplyBuff, Is.EqualTo(kv.Value), kv.Key.ToString());
            }
            // Chroma (Physical) and a hero with no element of its own: pure force, 18, no rider.
            var chroma = fang.ForCaster(Real.Classes["chromaninja"]);
            Assert.That(chroma.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(chroma.BaseDamage, Is.EqualTo(18));
            Assert.That(chroma.ApplyBuff, Is.Null);
            Assert.That(fang.ForCaster(Real.Classes["barbarian"]), Is.SameAs(fang));
            Assert.That(fang.ForCaster(null), Is.SameAs(fang));
        }

        [Test]
        public void Ninja_edge_rides_the_wearers_element_status()
        {
            var edge = Real.Ability("ninja_edge");
            Assert.That(edge.Kind, Is.EqualTo(AbilityKind.Passive));
            (string Cls, string Buff, float Chance)[] rows =
            {
                ("blackninja", "blinded", 0.15f), ("blueninja", "frozen", 0.1f), ("redninja", "burning", 0.15f), ("yellowninja", "slowed", 0.15f),
            };
            foreach (var (cls, buff, chance) in rows)
            {
                var v = edge.ForCaster(Real.Classes[cls]);
                Assert.That(v.Trigger.On, Is.EqualTo(TriggerOn.OnLandedHit), cls);
                Assert.That(v.Trigger.Chance, Is.EqualTo(chance).Within(1e-6), cls);
                Assert.That(v.Trigger.Effects.Single(e => e.Kind == TriggerEffectKind.ApplyBuff).Buff, Is.EqualTo(buff), cls);
                Assert.That(v.Description, Does.Contain("["), cls + " reads its own element's sentence");
            }
            Assert.That(edge.Trigger.Effects.Single().Buff, Is.EqualTo("blinded"), "the base row is untouched by a variant");
            Assert.That(edge.Trigger.Chance, Is.EqualTo(0.15f).Within(1e-6));
        }

        [Test]
        public void In_battle_the_wearer_fires_its_own_variant()
        {
            var (s, _) = Battle(Real, Stage(("wraithknight01", 2, 2)), ("redninja", 2, 7), ("blackninja", 4, 7));
            var red = Hero(s, "redninja");
            var edge = Triggers.Sources(Real, red).Single(a => a.Id == "ninja_edge");
            Assert.That(edge.Trigger.Effects.Single(e => e.Kind == TriggerEffectKind.ApplyBuff).Buff, Is.EqualTo("burning"));
            var black = Hero(s, "blackninja");
            for (int i = 0; i < black.Bar.Count; i++)
            {
                var a = BattleEngine.SlotAbility(s, black, i);
                if (a?.Id != "ninja_fang") continue;
                Assert.That(a.DamageType, Is.EqualTo(DamageType.Dark));
                Assert.That(a.ApplyBuff, Is.EqualTo("blinded"));
                return;
            }
            Assert.Fail("the Black Ninja's bar holds ninja_fang");
        }

        [Test]
        public void The_board_frees_the_merged_nodes_and_resigns_the_black_ninja()
        {
            var hex = Real.SkillHex;
            Assert.That(hex, Is.Not.Null);
            Assert.That(hex.Nodes.Where(n => n.Ability != null && Merged.ContainsKey(n.Ability)), Is.Empty);
            foreach (var id in new[] { "ninja_fang", "ninja_edge" })
                Assert.That(hex.Nodes.Count(n => n.Kind == SkillHexNodeKind.Ability && n.Ability == id), Is.EqualTo(1), id);
            var h = hex.Heroes.Single(x => x.Class == "blackninja");
            Assert.That(h.Signature, Is.EqualTo("blackninja_smoke_vanish"));
            var node = hex.Nodes.Single(n => n.Kind == SkillHexNodeKind.Ability && n.Ability == h.Signature);
            Assert.That(h.Start.Any(c => c[0] == node.Q && c[1] == node.R), Is.True, "Start holds the signature");
            Assert.That(Real.Classes.Values.Where(c => c.Id != "blackninja" && c.Abilities.Contains(h.Signature)), Is.Empty);
            Assert.That(ContentLoader.Validate(Real).Where(p => p.StartsWith("skillhex")), Is.Empty);
        }

        [Test]
        public void Variant_rows_are_validated()
        {
            var db = Mini();
            db.Abilities["bad_edge"] = new AbilityDef
            {
                Id = "bad_edge", Name = "Bad Edge", Kind = AbilityKind.Skill, BaseDamage = 0, ElementFromCaster = true,
                ElementVariants = { [DamageType.Fire] = new ElementVariantDef { Buff = "no_such_buff", Chance = 2f } },
            };
            db.Abilities["stray_rows"] = new AbilityDef
            {
                Id = "stray_rows", Name = "Stray", Kind = AbilityKind.Skill, BaseDamage = 5,
                ElementVariants = { [DamageType.Ice] = new ElementVariantDef { Buff = "slowed" } },
            };
            var p = ContentLoader.Validate(db);
            Assert.That(p.Any(x => x.Contains("bad_edge") && x.Contains("deals damage")), Is.True, string.Join("\n", p));
            Assert.That(p.Any(x => x.Contains("bad_edge ElementVariants Fire") && x.Contains("no_such_buff")), Is.True);
            Assert.That(p.Any(x => x.Contains("bad_edge ElementVariants Fire") && x.Contains("Chance")), Is.True);
            Assert.That(p.Any(x => x.Contains("stray_rows") && x.Contains("ElementFromCaster")), Is.True);
        }
    }
}
