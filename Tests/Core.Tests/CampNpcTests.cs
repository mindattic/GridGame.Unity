using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Camp NPCs join along the way (§25.0.1, GG-T119): each Camp service stays hidden until its keeper joins
    /// at a story beat; the camp routine only uses joined services; Test Mode and old saves have everyone already.</summary>
    public class CampNpcTests
    {
        private static ContentDb Real => TestKit.Real;

        private static readonly string[] Npcs = { CampNpcs.Merchant, CampNpcs.Blacksmith, CampNpcs.Alchemist, CampNpcs.Enchantress, CampNpcs.Innkeeper, CampNpcs.Aetherist };

        [Test]
        public void The_six_camp_npcs_are_the_dialogue_npcs()
        {
            Assert.That(CampNpcs.All, Is.EquivalentTo(Npcs));
        }

        [Test]
        public void A_new_game_has_met_nobody_yet()
        {
            var s = Progression.NewGame(Real, "T");
            Assert.That(Npcs.Where(n => CampNpcs.Joined(s, n)), Is.Empty);
        }

        /// <summary>Every path through a dialogue (each choice taken at each choice list, loops cut), as the saves
        /// those paths leave behind.</summary>
        private static List<SaveData> EveryPath(DialogueDef d, int gold = 1000)
        {
            var ends = new List<SaveData>();
            var queue = new Queue<List<int>>();
            queue.Enqueue(new List<int>());
            while (queue.Count > 0 && ends.Count < 200)
            {
                var picks = queue.Dequeue();
                var s = Progression.NewGame(Real, "T");
                s.Gold = gold;
                var r = new DialogueRunner(Real, s, d);
                int i = 0, guard = 0;
                while (!r.Finished && guard++ < 50)
                {
                    if (!r.AwaitingChoice) { r.Advance(); continue; }
                    if (i < picks.Count) { r.Choose(picks[i++]); continue; }
                    for (int k = 1; k < r.Choices.Count && picks.Count < 6; k++) queue.Enqueue(picks.Append(k).ToList());
                    picks = picks.Append(0).ToList();
                    i++;
                    r.Choose(0);
                }
                Assert.That(r.Finished, $"{d.Id}: a path never ends");
                ends.Add(s);
            }
            return ends;
        }

        [Test]
        public void Every_npc_joins_on_every_branch_of_a_story_beat()
        {
            foreach (var npc in Npcs)
            {
                var beats = Real.DialogueOrder.Where(d => d.Trigger.Kind != DialogueTriggerKind.Talk
                    && d.Nodes.Any(n => n.Effects.Concat(n.Choices.SelectMany(ch => ch.Effects)).Any(e => e.Type == DialogueEffectType.JoinNpc && e.Id == npc))).ToList();
                Assert.That(beats, Is.Not.Empty, $"no story beat joins the {npc}");
                var beat = beats[0];
                Assert.That(beat.Conditions, Is.Empty, $"{beat.Id}: a join beat must always fire, or the {npc} could be missed for good");
                foreach (var end in EveryPath(beat))
                    Assert.That(CampNpcs.Joined(end, npc), $"{beat.Id}: a branch leaves the {npc} behind");
            }
        }

        [Test]
        public void Every_npc_has_a_camp_talk()
        {
            var s = Progression.NewGame(Real, "T");
            CampNpcs.JoinAll(s);
            foreach (var npc in Npcs)
                Assert.That(DialogueRunner.TalkTopic(Real, s, ContentLoader.NpcFlagPrefix + npc), Is.Not.Null, $"nothing to say to the {npc}");
        }

        /// <summary>GG-T537 (owner): every Camp vendor joins on a story beat of the campaign, clearing a stage, one at a time:
        /// the Merchant after the tutorial (The Tithe Moors I), the Innkeeper after the second, the Blacksmith after the
        /// prologue boss (The Tithe Moors III), the Alchemist after The Lantern Road I and the Enchantress after The
        /// Arkgrave I. Played in order: each launch's pending beats, the clear, then the join beats on the way to Camp.</summary>
        [Test]
        public void Playing_the_campaign_in_order_meets_each_vendor_after_its_stage()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            var met = new Dictionary<string, string>();
            foreach (var stage in c.CampaignStages())
            {
                DialogueRunner.PlayPendingBeatsHeadless(c, s, stage.Id);
                foreach (var n in Npcs) Assert.That(CampNpcs.Joined(s, n) && !met.ContainsKey(n), Is.False, $"{n} joined before {stage.Id} was cleared, off its beat");
                s.ClearedStages.Add(stage.Id);
                for (var d = DialogueRunner.PendingVendorBeat(c, s); d != null; d = DialogueRunner.PendingVendorBeat(c, s))
                    DialogueRunner.PlayHeadless(c, s, d);
                foreach (var n in Npcs) if (CampNpcs.Joined(s, n) && !met.ContainsKey(n)) met[n] = stage.Id;
            }
            Assert.That(met.Keys, Is.EquivalentTo(Npcs), "someone is never met");
            Assert.That(met[CampNpcs.Merchant], Is.EqualTo("greenvalley-01"));
            Assert.That(met[CampNpcs.Innkeeper], Is.EqualTo("greenvalley-02"));
            Assert.That(met[CampNpcs.Blacksmith], Is.EqualTo("greenvalley-03"));
            Assert.That(met[CampNpcs.Alchemist], Is.EqualTo("lanternroad-01"));
            Assert.That(met[CampNpcs.Enchantress], Is.EqualTo("swamp-01"));
            Assert.That(met[CampNpcs.Aetherist], Is.EqualTo("desert-02")); // GG-T570: the refinery, at The Shardfall II
        }

        /// <summary>GG-T570: a save that had met every other keeper before the Aetherist existed keeps its veteran Camp.</summary>
        [Test]
        public void A_save_that_met_every_other_keeper_meets_the_aetherist_on_load()
        {
            var s = Progression.NewGame(Real, "T");
            foreach (var n in Npcs) if (n != CampNpcs.Aetherist) s.Flags.Add(ContentLoader.NpcFlagPrefix + n);
            SaveMigrations.JoinNewKeepers(s);
            Assert.That(CampNpcs.Joined(s, CampNpcs.Aetherist));
            var fresh = Progression.NewGame(Real, "T");
            SaveMigrations.JoinNewKeepers(fresh);
            Assert.That(CampNpcs.Joined(fresh, CampNpcs.Aetherist), Is.False, "a new game still meets the Aetherist on the road");
        }

        /// <summary>GG-T537: no vendor waits on an incidental trigger. Every join beat is an unconditional StageCleared beat,
        /// and no two vendors share a stage, so new systems arrive one at a time.</summary>
        [Test]
        public void Every_vendor_joins_on_its_own_stage_clear()
        {
            var stages = new HashSet<string>();
            foreach (var npc in Npcs)
            {
                var beat = CampUnlocks.JoinBeat(Real, npc);
                Assert.That(beat, Is.Not.Null, $"no stage-clear beat joins the {npc}");
                Assert.That(beat.Conditions, Is.Empty, beat.Id);
                Assert.That(stages.Add(beat.Trigger.Id), $"{npc} shares {beat.Trigger.Id} with another vendor");
            }
            foreach (var station in DialogueDef.Stations)
            {
                var beat = Real.DialogueOrder.Single(d => DialogueRunner.IsVendorBeat(d) && d.AllEffects().Any(e => e.Type == DialogueEffectType.OpenStation && e.Id == station));
                Assert.That(stages.Add(beat.Trigger.Id), $"the {station} station shares {beat.Trigger.Id} with another vendor");
            }
        }

        /// <summary>GG-T537: the Enchantress joins after The Arkgrave I, armor forged or not, and hands over an Arcane Dust
        /// toward the first enchant.</summary>
        [Test]
        public void The_enchantress_joins_after_the_arkgrave_i()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            s.ClearedStages.Add("desert-03");
            DialogueRunner.PlayPendingBeatsHeadless(c, s, "swamp-01");
            Assert.That(CampNpcs.Joined(s, CampNpcs.Enchantress), Is.False, "not before The Arkgrave I is cleared");
            int dust = s.Count("mat_arcane_dust");
            s.ClearedStages.Add("swamp-01");
            Assert.That(DialogueRunner.PendingInterlude(c, s)?.Id, Is.EqualTo("swamp_enchantress"), "her scene plays on the way to Camp");
            DialogueRunner.PlayHeadless(c, s, DialogueRunner.PendingInterlude(c, s));
            Assert.That(CampNpcs.Joined(s, CampNpcs.Enchantress), Is.True);
            Assert.That(s.Count("mat_arcane_dust"), Is.EqualTo(dust + 1), "she brings an Arcane Dust");
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Enchantress), Is.EqualTo("Joins after The Arkgrave I"));
        }

        /// <summary>Gives the lead a weapon worn to <paramref name="fraction"/> of its max, as battles would.</summary>
        internal static Repair.WornPiece WearLeadWeapon(ContentDb c, SaveData s, float fraction)
        {
            var w = c.Items.Values.First(i => i.Type == ItemType.Equipment && i.Slot == EquipmentSlot.Weapon && i.Durability >= 10);
            var h = s.Hero(s.Party[0]);
            h.WeaponId = w.Id;
            h.WeaponDurability = (int)(w.Durability * fraction);
            return Repair.MostWorn(c, s);
        }

        /// <summary>GG-T537 (supersedes GG-T521): the Blacksmith joins after the prologue boss, The Tithe Moors III, whatever
        /// the party's gear looks like. Worn gear alone no longer brings him.</summary>
        [Test]
        public void The_blacksmith_joins_after_the_prologue_boss_not_on_worn_gear()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            WearLeadWeapon(c, s, 0.1f);
            s.ClearedStages.Add("greenvalley-01"); s.ClearedStages.Add("greenvalley-02");
            DialogueRunner.PlayPendingBeatsHeadless(c, s, "greenvalley-03");
            Assert.That(CampNpcs.Joined(s, CampNpcs.Blacksmith), Is.False, "worn gear is not his beat");
            s.ClearedStages.Add("greenvalley-03");
            Assert.That(CampUnlocks.Arrivals(c, s, "greenvalley-03"), Does.Contain("Blacksmith joins the caravan"), "the results screen says so");
            Assert.That(DialogueRunner.PendingInterlude(c, s)?.Id, Is.EqualTo("greenvalley_smith"));
            var log = DialogueRunner.PlayHeadless(c, s, DialogueRunner.PendingInterlude(c, s));
            Assert.That(CampNpcs.Joined(s, CampNpcs.Blacksmith), Is.True);
            Assert.That(log, Does.Contain("Blacksmith joins the caravan!"), "named bare, like every archetype");
            Assert.That(CampUnlocks.Arrivals(c, s, "greenvalley-03"), Is.Empty, "told once");
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Blacksmith), Is.EqualTo("Joins after The Tithe Moors III"));
        }

        /// <summary>GG-T537: the Alchemist joins after The Lantern Road I with nothing looted, and hands over an Empty
        /// Bottle toward the guided first brew (section 25.0.5).</summary>
        [Test]
        public void The_alchemist_joins_after_the_lantern_road_i_with_a_bottle()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            s.ClearedStages.Add("lanternroad-01");
            for (var d = DialogueRunner.PendingVendorBeat(c, s); d != null; d = DialogueRunner.PendingVendorBeat(c, s))
                DialogueRunner.PlayHeadless(c, s, d);
            Assert.That(CampNpcs.Joined(s, CampNpcs.Alchemist), Is.True);
            Assert.That(s.Count(ContentLoader.EmptyBottle), Is.EqualTo(1), "he hands over the first bottle");
            Assert.That(CampUnlocks.SmithWorkshopOpen(s), Is.True, "and the Blacksmith's workshop opens with him");
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Alchemist), Is.EqualTo("Joins after The Lantern Road I"));
        }

        /// <summary>GG-T537: a save from before vendors joined on stage clears gets every vendor whose stage it has cleared
        /// on load, with those scenes taken as told; a current save past a beat gets the vendor too and still sees the
        /// scene; nobody met under the old triggers leaves.</summary>
        [Test]
        public void Saves_past_a_vendor_beat_get_the_vendor_on_load()
        {
            var old = Progression.NewGame(Real, "Old");
            old.Version = 11;
            old.Flags.Add("npc:merchant"); old.Flags.Add("npc:alchemist"); // met under the old ThemeStart/CanBrew triggers
            foreach (var id in new[] { "greenvalley-01", "greenvalley-02", "greenvalley-03" }) old.ClearedStages.Add(id);
            old.HighestClearedStage = 2;
            SaveMigrations.Migrate(old, Real);
            Assert.That(Npcs.Where(n => CampNpcs.Joined(old, n)), Is.EquivalentTo(new[] { CampNpcs.Merchant, CampNpcs.Innkeeper, CampNpcs.Blacksmith, CampNpcs.Alchemist }),
                "the Innkeeper and Blacksmith arrive; the early Alchemist stays");
            Assert.That(old.SeenDialogues, Does.Contain("greenvalley_smith").And.Contain("greenvalley_after_first"));
            Assert.That(DialogueRunner.PendingVendorBeat(Real, old), Is.Null, "no run of join scenes at the next Camp");

            var current = Progression.NewGame(Real, "Now");
            current.ClearedStages.Add("greenvalley-01"); current.HighestClearedStage = 0;
            SaveMigrations.Migrate(current, Real);
            Assert.That(CampNpcs.Joined(current, CampNpcs.Merchant), Is.True);
            Assert.That(DialogueRunner.PendingVendorBeat(Real, current)?.Id, Is.EqualTo("greenvalley_road_merchant"), "a current save still gets the scene");
        }

        [Test]
        public void Test_mode_saves_start_with_every_npc_joined()
        {
            var s = TestLoop.NewSave(Real);
            Assert.That(Npcs.All(n => CampNpcs.Joined(s, n)));
        }

        [Test]
        public void Old_saves_migrate_with_every_npc_joined_and_new_ones_keep_their_flags()
        {
            var old = Progression.NewGame(Real, "Old");
            old.Version = 3;
            SaveMigrations.Migrate(old);
            Assert.That(Npcs.All(n => CampNpcs.Joined(old, n)), "a v3 save was already using every service");
            Assert.That(old.Version, Is.EqualTo(SaveData.CurrentVersion));

            var fresh = Progression.NewGame(Real, "New");
            fresh.Flags.Add("npc:merchant");
            SaveMigrations.Migrate(fresh);
            Assert.That(Npcs.Where(n => CampNpcs.Joined(fresh, n)), Is.EqualTo(new[] { CampNpcs.Merchant }), "a current save keeps exactly who it has met");
        }

        [Test]
        public void Camp_routine_uses_no_service_whose_npc_has_not_joined()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            s.Gold = 5000;
            s.Hero("knight").Hp = 10;
            s.Inventory.Remove(CampRoutine.StopgapPotion);
            CampRoutine.Run(c, s);
            Assert.That(s.Hero("knight").Hp, Is.EqualTo(10), "rested without an Innkeeper");
            Assert.That(s.Count(CampRoutine.StopgapPotion), Is.EqualTo(0), "bought or brewed without a Merchant or Alchemist");
            Assert.That(s.StationXp.Values.Sum(), Is.EqualTo(0), "forged, repaired or salvaged without a Blacksmith");
        }

        [Test]
        public void Camp_routine_uses_each_service_once_its_npc_joins()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            s.Gold = 5000;
            s.Hero("knight").Hp = 10;
            s.Inventory.Remove(CampRoutine.StopgapPotion);
            s.Flags.Add("npc:" + CampNpcs.Innkeeper);
            CampRoutine.Run(c, s);
            Assert.That(s.Hero("knight").Hp, Is.EqualTo(0), "the Innkeeper lets it rest");
            Assert.That(s.Count(CampRoutine.StopgapPotion), Is.EqualTo(0), "still no Merchant");
            s.Flags.Add("npc:" + CampNpcs.Merchant);
            CampRoutine.Run(c, s);
            Assert.That(s.Count(CampRoutine.StopgapPotion), Is.GreaterThan(0), "the Merchant sells potions");
        }
    }
}
