using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T2127: the Commander's look and pronouns, asked at the Founding and kept on the save (v30).</summary>
    public class CommanderLookTests
    {
        [Test]
        public void A_new_save_is_the_female_look_and_they_them()
        {
            var s = new SaveData();
            Assert.That(s.CommanderLook, Is.EqualTo(HeroVariant.Female));
            Assert.That(s.CommanderPronouns, Is.EqualTo(CommanderPronouns.TheyThem));
            Assert.That(Commander.PortraitOf(s), Is.EqualTo(Commander.Portrait));
            Assert.That(Commander.PortraitOf((SaveData)null), Is.EqualTo(Commander.Portrait));
        }

        [Test]
        public void The_male_look_draws_the_commander_male_portrait()
        {
            var s = new SaveData { CommanderLook = HeroVariant.Male };
            Assert.That(Commander.PortraitOf(s), Is.EqualTo(Commander.MalePortrait));
            Assert.That(Commander.MalePortrait, Is.EqualTo(AssetNames.Of("commander_male")));
        }

        [TestCase(CommanderPronouns.TheyThem, "they saw them; their orb was theirs, by themself. They came.")]
        [TestCase(CommanderPronouns.SheHer, "she saw her; her orb was hers, by herself. She came.")]
        [TestCase(CommanderPronouns.HeHim, "he saw him; his orb was his, by himself. He came.")]
        public void Pronoun_tokens_fill_from_the_save(CommanderPronouns p, string expected)
        {
            var s = new SaveData { CommanderPronouns = p };
            Assert.That(Commander.Fill("{they} saw {them}; {their} orb was {theirs}, by {themself}. {They} came.", s), Is.EqualTo(expected));
        }

        [Test]
        public void Name_and_pronouns_fill_together_and_no_save_is_they_them()
        {
            var s = new SaveData { CommanderName = "Ash", CommanderPronouns = CommanderPronouns.SheHer };
            Assert.That(Commander.Fill("{commander} lifts {their} lantern.", s), Is.EqualTo("Ash lifts her lantern."));
            Assert.That(Commander.Fill("{Their} lantern.", null), Is.EqualTo("Their lantern."));
            Assert.That(Commander.Fill("No tokens.", s), Is.EqualTo("No tokens."));
        }

        [Test]
        public void Only_known_pronoun_tokens_count()
        {
            Assert.That(ProseText.IsPronounToken("they"), Is.True);
            Assert.That(ProseText.IsPronounToken("Theirs"), Is.True);
            Assert.That(ProseText.IsPronounToken("she"), Is.False);
            Assert.That(ProseText.IsPronounToken(""), Is.False);
            Assert.That(Commander.Pronoun(CommanderPronouns.HeHim, "nope"), Is.Null);
        }

        [Test]
        public void A_v29_save_loads_as_the_female_look_and_they_them()
        {
            var old = new SaveData { Version = 29 };
            string json = SaveFiles.Serialize(old)
                .Replace("\"CommanderLook\": \"Female\",", "")
                .Replace("\"CommanderPronouns\": \"TheyThem\",", "");
            Assume.That(json, Does.Not.Contain("CommanderLook"));
            Assume.That(json, Does.Not.Contain("CommanderPronouns"));
            var back = SaveMigrations.Migrate(SaveFiles.Deserialize(json));
            Assert.That(back.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(back.CommanderLook, Is.EqualTo(HeroVariant.Female));
            Assert.That(back.CommanderPronouns, Is.EqualTo(CommanderPronouns.TheyThem));
        }

        [Test]
        public void The_pick_round_trips_and_unknown_values_read_as_the_defaults()
        {
            var s = new SaveData { CommanderLook = HeroVariant.Male, CommanderPronouns = CommanderPronouns.HeHim };
            var back = SaveMigrations.Migrate(SaveFiles.Deserialize(SaveFiles.Serialize(s)));
            Assert.That(back.CommanderLook, Is.EqualTo(HeroVariant.Male));
            Assert.That(back.CommanderPronouns, Is.EqualTo(CommanderPronouns.HeHim));

            string odd = SaveFiles.Serialize(s)
                .Replace("\"CommanderLook\": \"Male\"", "\"CommanderLook\": 5")
                .Replace("\"CommanderPronouns\": \"HeHim\"", "\"CommanderPronouns\": 9");
            Assume.That(odd, Does.Contain("\"CommanderPronouns\": 9"));
            var fixedUp = SaveMigrations.Migrate(SaveFiles.Deserialize(odd));
            Assert.That(fixedUp.CommanderLook, Is.EqualTo(HeroVariant.Female));
            Assert.That(fixedUp.CommanderPronouns, Is.EqualTo(CommanderPronouns.TheyThem));
        }
    }
}
