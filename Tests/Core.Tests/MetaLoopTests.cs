using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using Newtonsoft.Json;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    public class MetaLoopTests
    {

        [Test]
        public void New_game_has_starting_trio_gold_and_loadouts()
        {
            var s = Progression.NewGame(Real, "Test");
            Assert.That(s.Party, Is.EqualTo(Progression.StartingRoster)); // a small start (GG-T1405: no fixed lead); the rest join at story beats (GG-T118)
            Assert.That(s.Party.Select(id => Real.Classes[id].Role), Is.EqualTo(new[] { "Tank", "Striker", "Healer" }));
            Assert.That(s.Gold, Is.EqualTo(Progression.StartingGold));
            foreach (var h in s.Roster) Assert.That(h.Bar.Any(e => e != null), Is.True);
            Assert.That(Progression.UnlockedSlots(s), Is.EqualTo(Progression.MaxBarSlots)); // every slot open from the start (2026-10-11)
            // One full-life stack per hero (amended 2026-10-11: the second X-Potion slot became each
            // class's new unique 5th skill).
            foreach (var h in s.Roster) Assert.That(h.Bar.Count(e => e == "item:x_potion"), Is.EqualTo(1));
            Assert.That(s.Count("x_potion"), Is.EqualTo(s.Roster.Count * Real.Items["x_potion"].MaxStack));  // one full stack of 5 each
        }

        [Test]
        public void Save_round_trips_through_json()
        {
            var s = Progression.NewGame(Real, "Round");
            s.ClearedStages.Add("greenvalley-01");
            s.Bestiary["slime00"] = new BestiaryEntry { Seen = true, Defeated = 2 };
            s.Roster[0].SkillPoints = 3;
            var json = JsonConvert.SerializeObject(s, Formatting.Indented, ContentLoader.Settings);
            var back = SaveMigrations.Migrate(JsonConvert.DeserializeObject<SaveData>(json, ContentLoader.Settings));
            Assert.That(back.Gold, Is.EqualTo(s.Gold));
            Assert.That(back.ClearedStages, Does.Contain("greenvalley-01"));
            Assert.That(back.Bestiary["slime00"].Defeated, Is.EqualTo(2));
            Assert.That(back.Roster[0].Bar, Is.EqualTo(s.Roster[0].Bar));
            Assert.That(back.Roster[0].SkillPoints, Is.EqualTo(3)); // a current-version save isn't re-backfilled
        }

        [Test] // GG-T49
        public void Migrating_a_v1_save_backfills_skill_points_once_for_heroes_already_past_level_1()
        {
            var s = Progression.NewGame(Real, "SPMigrate");
            var hero = s.Hero(s.Party[0]);
            hero.TotalXp = Formulas.TotalXpForLevel(6); // already level 6 under the pre-GG-T49 save shape
            hero.SkillPoints = 0;
            s.Version = 1; // simulate a save written before skill points existed

            var migrated = SaveMigrations.Migrate(s);
            int expected = Formulas.SkillPointsForLevelUp(1, 6);
            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(hero.SkillPoints, Is.EqualTo(expected));

            SaveMigrations.Migrate(migrated); // re-migrating a current save must not grant the points twice
            Assert.That(hero.SkillPoints, Is.EqualTo(expected));
        }

        [Test]
        public void Full_loop_battle_commit_then_every_vendor()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Loop");
            var stages = c.CampaignStages();
            int victories = 0;
            for (int i = 0; i < 6; i++)
            {
                var stage = stages[System.Math.Min(i, stages.Count - 1)];
                if (!Progression.IsStageUnlocked(s, stages.IndexOf(stage))) break;
                var setup = BattleFactory.Build(c, s, stage.Id, 100 + i, 1); // GG-T311: the meta loop, not balance — fought on the Gentle dial
                var st = BotRunner.Play(c, setup, new HeroBot(i), 3000); // the first battle is a 20-wave gauntlet; GG-T311: forced Guards count
                var rep = BattleFactory.Commit(c, s, st);
                if (rep.Victory) victories++;
                TestContext.Out.WriteLine($"{stage.Id}: {(rep.Victory ? "WIN" : "LOSS")} gold+{rep.Gold} xp+{rep.XpEach} items {string.Join(",", rep.Items.Select(kv => kv.Key + "x" + kv.Value))} lvls {string.Join(",", rep.LevelUps)}");
            }
            Assert.That(victories >= 3, Is.True, "bot should win the opening stages");
            Assert.That(s.HighestClearedStage >= 2, Is.True);
            Assert.That(Progression.UnlockedSlots(s) >= 3, Is.True);
            TestContext.Out.WriteLine($"Gold {s.Gold}; inventory {string.Join(", ", s.Inventory.Select(kv => kv.Key + "x" + kv.Value))}");

            // Merchant
            var stock = Shop.Stock(c, s).Where(i => i.MaxStack >= 2).ToList(); // a stackable consumable on this save's shelf (gear is one-per-bag)
            Assert.That(stock, Is.Not.Empty);
            s.Gold += 2000;
            var buy = new Dictionary<string, int> { [stock[0].Id] = 2 };
            int before = s.Count(stock[0].Id);
            Assert.That(Shop.Buy(c, s, buy).Ok, Is.True);
            Assert.That(s.Count(stock[0].Id), Is.EqualTo(before + 2));
            Assert.That(Shop.Sell(c, s, new Dictionary<string, int> { [stock[0].Id] = 1 }).Ok, Is.True);

            // Forge something: give ourselves the inputs of the cheapest forge recipe
            var recipe = Crafting.Recipes(c, CraftStation.Forge).First(r => r.BaseItem == null);
            foreach (var ic in recipe.Inputs) s.AddItem(ic.Item, ic.Count);
            var craft = Crafting.Craft(c, s, recipe);
            Assert.That(craft.Ok, Is.True, craft.Message);
            var forged = c.Item(recipe.Result);
            Assert.That(s.Count(forged.Id), Is.EqualTo(1));

            // Equip it on whoever it fits, then wear + repair it
            var hero = s.Hero(s.Party[0]);
            if (forged.Slot == EquipmentSlot.Weapon || System.Array.IndexOf(BattleEngine.ArmorSlots, forged.Slot) >= 0)
            {
                Assert.That(Equipment.Equip(c, s, hero, forged.Slot, forged.Id).Ok, Is.True);
                Assert.That(Equipment.Equipped(hero, forged.Slot), Is.EqualTo(forged.Id));
                if (forged.Slot == EquipmentSlot.Weapon && forged.Durability > 1)
                {
                    hero.WeaponDurability = 1;
                    Assert.That(Repair.Cost(forged, hero.WeaponDurability, hero.WeaponRepairs) > 0, Is.True);
                    Assert.That(Repair.RepairWeapon(c, s, hero).Ok, Is.True);
                    // Restored to the *new* effective max (after this repair's cap drop), not the old one (GG-T196).
                    Assert.That(hero.WeaponDurability, Is.EqualTo(Repair.EffectiveMax(forged, hero.WeaponRepairs, hero.WeaponMastery)));
                }
            }

            // Inn: camp rest, free (GG-T1790)
            hero.Hp = 5;
            int goldBeforeRest = s.Gold;
            Assert.That(CampRestService.Rest(s, out _).Ok, Is.True);
            Assert.That(hero.Hp, Is.EqualTo(0));
            Assert.That(s.Gold, Is.EqualTo(goldBeforeRest));

            // Learn + slot
            var learn = Abilities.Learnable(c, hero).First();
            hero.Known.Add(learn.Id); // GG-T1079: gold Learn is gone (the skill board teaches)
            Assert.That(Abilities.Assign(c, s, hero, 0, learn.Id).Ok, Is.True);
            Assert.That(hero.Bar[0], Is.EqualTo(learn.Id));

            // Summon
            var recruit = Summon.Pool(c, s).First(d => s.Hero(d.Id) == null);
            int price = Summon.Price(c, s);
            Assert.That(Summon.Recruit(c, s, recruit.Id).Ok, Is.True);
            Assert.That(Summon.Price(c, s), Is.EqualTo(price)); // a paywall hook now (§25.18, GG-T232): flat, not rising
            // GG-T49: a recruit arrives with the skill points its arrival level would already have earned.
            var recruited = s.Hero(recruit.Id);
            Assert.That(recruited.SkillPoints, Is.EqualTo(Formulas.SkillPointsForLevelUp(1, Progression.Level(recruited))));

            // Party cap
            while (s.Party.Count < Progression.MaxPartySize)
            {
                var extra = s.Roster.FirstOrDefault(h => !s.Party.Contains(h.ClassId));
                if (extra != null) { Assert.That(Party.Toggle(s, extra.ClassId).Ok, Is.True); continue; }
                s.Gold += Summon.Price(c, s); // recruit until the party can fill (a recruit joins while there's room)
                Assert.That(Summon.Recruit(c, s, Summon.Pool(c, s).First(d => s.Hero(d.Id) == null).Id).Ok, Is.True);
            }
            Assert.That(s.Party.Count, Is.EqualTo(Progression.MaxPartySize));
            Assert.That(Progression.MaxPartySize, Is.EqualTo(6)); // GG-T291: two attackers, two supporters each

            // Bounty
            // GG-T431: a hunt for a marked target. The kill is set on the battle's mark (BountyHuntTests covers the spawn).
            s.HighestClearedStage = System.Math.Max(s.HighestClearedStage, c.CampaignStages().Count - 1);
            var bounty = Bounties.Offers(c, s).First();
            Assert.That(Bounties.Take(c, s, bounty.Id).Ok, Is.True);
            var hunt = BattleEngine.Create(c, BattleFactory.Build(c, s, bounty.Stage, 9), new List<BattleEvent>());
            hunt.Bounties.Single().Slain = true;
            Bounties.RecordBattle(c, s, hunt);
            int g = s.Gold;
            var sp = s.Party.ToDictionary(id => id, id => s.Hero(id).SkillPoints);
            Assert.That(Bounties.Claim(c, s, bounty.Id).Ok, Is.True);
            Assert.That(s.Gold, Is.EqualTo(g + Bounties.RewardGold(c, bounty)));
            // GG-T134: an optional skill-point payout goes to every active-party hero.
            foreach (var id in s.Party) Assert.That(s.Hero(id).SkillPoints, Is.EqualTo(sp[id] + Bounties.SkillPointsPaid(bounty)));
        }

        [Test]
        public void Defeat_resets_wounds_and_grants_nothing()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Lose");
            var stage = c.CampaignStages()[0];
            var setup = BattleFactory.Build(c, s, stage.Id, 3);
            var st = BattleEngine.Create(c, setup, new List<BattleEvent>());
            foreach (var u in st.Units.Where(u => u.IsHero)) u.Hp = 1;
            // shield until the party falls
            for (int i = 0; i < 400 && !st.IsOver; i++) BattleEngine.Apply(st, new ShieldCommand());
            Assert.That(st.Phase, Is.EqualTo(BattlePhase.Defeat));
            int gold = s.Gold;
            var rep = BattleFactory.Commit(c, s, st);
            Assert.That(rep.Victory, Is.False);
            Assert.That(s.Gold, Is.EqualTo(gold));
            foreach (var h in s.Roster) Assert.That(h.Hp, Is.EqualTo(0));
            Assert.That(s.HighestClearedStage, Is.EqualTo(-1));
        }

        // ── Travel (§28, GG-T1790; replaced expeditions §22.5) ──

        private static BattleState Fight(ContentDb c, SaveData s, string stageId, bool win)
        {
            TravelRules.SetOut(s);
            var setup = BattleFactory.Build(c, s, stageId, 7);
            var st = BattleEngine.Create(c, setup, new List<BattleEvent>());
            st.Phase = win ? BattlePhase.Victory : BattlePhase.Defeat; // the meta rules don't care how it went
            return st;
        }

        [Test]
        public void A_win_on_the_road_carries_its_mana_and_wounds_into_the_next_battle_with_rest_closed()
        {
            var c = Real; var s = Progression.NewGame(c, "Ex");
            Assert.That(s.World.Camped, Is.True, "a new game starts at camp");
            var theme = c.Campaign.Themes[0];
            var b1 = Fight(c, s, theme.Stages[0], true);
            GrantMana(b1, 4);
            var rep = BattleFactory.Commit(c, s, b1);
            Assert.That(TravelRules.OnTheRoad(s), Is.True);
            Assert.That(s.World.CarriedMana, Is.EqualTo(b1.PartyMana));
            Assert.That(rep.CarriedMana, Is.EqualTo(b1.PartyMana));
            Assert.That(rep.OrbDust, Is.EqualTo(0), "carried mana settles at the battle that keeps it");
            s.Roster[0].Hp = 1;
            Assert.That(CampRestService.Rest(s, out _).Ok, Is.False, "no camp rest on the road");
            Assert.That(s.Roster[0].Hp, Is.EqualTo(1), "wounds stay on the road");
            var setup = BattleFactory.Build(c, s, theme.Stages[1], 3);
            Assert.That(setup.StartMana, Is.EqualTo(s.World.CarriedMana));
            var b2 = BattleEngine.Create(c, setup, new List<BattleEvent>());
            Assert.That(b2.PartyMana >= System.Math.Min(setup.StartMana, BattleConfig.PartyManaCapacity), Is.True, "carried mana seeds the next battle's pool");
        }

        [Test]
        public void A_defeat_on_the_road_falls_back_to_camp_with_no_mana()
        {
            var c = Real; var s = Progression.NewGame(c, "Ex3");
            var theme = c.Campaign.Themes[0];
            var b1 = Fight(c, s, theme.Stages[0], true);
            GrantMana(b1, 2);
            BattleFactory.Commit(c, s, b1);
            Assume.That(TravelRules.OnTheRoad(s), Is.True);
            var lost = BattleFactory.Commit(c, s, Fight(c, s, theme.Stages[1], false));
            Assert.That(s.World.Camped, Is.True);
            Assert.That(s.World.CarriedMana, Is.EqualTo(0));
            Assert.That(lost.CarriedMana, Is.EqualTo(0));
            Assert.That(BattleFactory.Build(c, s, theme.Stages[0], 3).StartMana, Is.EqualTo(0));
        }

        [Test]
        public void Camps_Rest_row_and_the_respec_follow_travel()
        {
            // GG-T1786: the Inn row is camp rest ("Rest", Food on hand); on the road it reads "Make camp" and the respec waits.
            var c = Real; var s = Progression.NewGame(c, "Rr");
            CampNpcs.JoinAll(s);
            var rest = CampMenu.Build(c, s, new SettingsSave()).First(d => d.Id == CampMenu.Inn);
            Assert.That(rest.Label, Is.EqualTo("Rest"));
            Assert.That(rest.Value, Is.EqualTo($"{s.World.Food} Food"));
            Assert.That(SkillHexProgress.RespecClosed(s), Is.Null);
            s.World.Food = 0;
            Assert.That(CampMenu.RestValue(s), Does.Contain("no meal"));
            TravelRules.SetOut(s);
            Assert.That(CampMenu.Build(c, s, new SettingsSave()).First(d => d.Id == CampMenu.Inn).Value, Is.EqualTo("Make camp"));
            Assert.That(CampMenu.Build(c, s, new SettingsSave()).Any(d => d.Id == CampMenu.Respec), Is.False);
            Assert.That(SkillHexProgress.RespecClosed(s), Does.Contain("On the road"));
            Assert.That(Crucible.CanLaunch(c, s, out var why), Is.False);
            Assert.That(why, Is.EqualTo("Closed on the road"));
        }

        // ── Voluntary retreat (§22.2, FFIV-style partial loss, owner direction 2026-10-11) ──

        private static BattleState DirectBattle(ContentDb c, SaveData s, string stageId, int seed = 11) =>
            BattleEngine.Create(c, BattleFactory.Build(c, s, stageId, seed), new List<BattleEvent>());

        [Test]
        public void A_voluntary_retreat_keeps_a_fraction_of_coins_and_items_but_not_the_rest()
        {
            var c = Real; var s = Progression.NewGame(c, "Retreat1");
            var st = DirectBattle(c, s, c.CampaignStages()[0].Id);
            st.Results.Coins = 100;
            st.Results.Items["ether"] = 4;
            st.Phase = BattlePhase.Retreated;
            int goldBefore = s.Gold;
            var rep = BattleFactory.Commit(c, s, st);

            Assert.That(rep.Retreated, Is.True);
            int expectedGold = (int)System.Math.Floor(100 * BattleConfig.RetreatKeepFraction);
            Assert.That(rep.Gold, Is.EqualTo(expectedGold));
            Assert.That(s.Gold, Is.EqualTo(goldBefore + expectedGold));
            Assert.That(rep.Items.GetValueOrDefault("ether"), Is.EqualTo((int)System.Math.Floor(4 * BattleConfig.RetreatKeepFraction)));
        }

        [Test]
        public void A_retreat_still_keeps_hard_won_xp_like_a_defeat()
        {
            var c = Real; var s = Progression.NewGame(c, "Retreat2");
            var st = DirectBattle(c, s, c.CampaignStages()[0].Id);
            st.Results.Xp = 40;
            st.Phase = BattlePhase.Retreated;
            var rep = BattleFactory.Commit(c, s, st);

            Assert.That(rep.XpEach, Is.EqualTo(40));
            foreach (var id in s.Party) Assert.That(s.Hero(id).TotalXp >= 40, Is.True);
            foreach (var id in s.Party) Assert.That(s.Hero(id).SkillPoints, Is.EqualTo(0)); // GG-T49: no level crossed, no points yet
        }

        [Test]
        public void Retreating_on_the_road_makes_camp_without_touching_already_banked_gold()
        {
            var c = Real; var s = Progression.NewGame(c, "Retreat3");
            var theme = c.Campaign.Themes[0];
            BattleFactory.Commit(c, s, Fight(c, s, theme.Stages[0], true));
            int goldAfterBattle1 = s.Gold;

            var b2 = DirectBattle(c, s, theme.Stages[1]);
            b2.Results.Coins = 60;
            b2.Phase = BattlePhase.Retreated;
            var rep = BattleFactory.Commit(c, s, b2);

            Assert.That(s.World.Camped, Is.True);
            int expectedKeep = (int)System.Math.Floor(60 * BattleConfig.RetreatKeepFraction);
            Assert.That(s.Gold, Is.EqualTo(goldAfterBattle1 + expectedKeep));
        }

        [Test]
        public void Defeat_still_grants_only_hard_won_xp_after_the_retreat_refactor()
        {
            var c = Real; var s = Progression.NewGame(c, "Defeat1");
            var st = DirectBattle(c, s, c.CampaignStages()[0].Id);
            st.Results.Coins = 100;
            st.Results.Items["ether"] = 4;
            st.Results.Xp = 40;
            st.Phase = BattlePhase.Defeat;
            int goldBefore = s.Gold;
            var rep = BattleFactory.Commit(c, s, st);

            Assert.That(rep.Retreated, Is.False);
            Assert.That(rep.Gold, Is.EqualTo(0));
            Assert.That(s.Gold, Is.EqualTo(goldBefore));
            Assert.That(rep.Items, Is.Empty);
            Assert.That(rep.XpEach, Is.EqualTo(40));
        }

        [Test] // GG-T49
        public void Hard_won_xp_on_defeat_still_grants_skill_points_when_it_crosses_a_level()
        {
            var c = Real; var s = Progression.NewGame(c, "Defeat2");
            var hero = s.Hero(s.Party[0]);
            hero.TotalXp = Formulas.TotalXpForLevel(2) - 5; // 5 XP short of level 2
            var st = DirectBattle(c, s, c.CampaignStages()[0].Id);
            st.Results.Xp = 10;
            st.Phase = BattlePhase.Defeat;
            BattleFactory.Commit(c, s, st);

            Assert.That(Progression.Level(hero), Is.EqualTo(2));
            Assert.That(hero.SkillPoints, Is.EqualTo(Formulas.SkillPointsForLevelUp(1, 2)));
        }

        // ── Guest allies (§25.11) ──

        private static StageDef GuestStage(string guest) => new StageDef { Id = "g", Name = "G", IsTest = true, Waves = { new WaveDef { Enemies = { new SpawnDef { Class = "slime00", Level = 1, X = 2, Y = 1 } }, Guests = { new SpawnDef { Class = guest, Level = 3, X = 0, Y = 5 } } } } };

        [Test]
        public void A_guest_who_survives_a_victory_joins_the_roster()
        {
            var c = Real; var s = Progression.NewGame(c, "G1", new[] { "paladin", "barbarian", "cleric", "redmage", "engineer" });
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1);
            setup.Stage = GuestStage("dervish");
            var ev = new List<BattleEvent>();
            var st = BattleEngine.Create(c, setup, ev);
            var guest = st.Units.Single(u => u.IsGuest);
            Assert.That(guest.Def.Id, Is.EqualTo("dervish"));
            Assert.That(ev.Any(e => e is GuestJoinedEvent), Is.True);
            st.Phase = BattlePhase.Victory;
            var rep = BattleFactory.Commit(c, s, st);
            Assert.That(rep.GuestsJoined, Does.Contain("dervish"));
            Assert.That(s.Hero("dervish"), Is.Not.Null);
            Assert.That(s.Party.Count, Is.EqualTo(Progression.MaxPartySize)); // a full party: the rescued guest waits in reserve
            // GG-T49: a rescued guest arrives with the skill points its battle level would already have earned.
            Assert.That(s.Hero("dervish").SkillPoints, Is.EqualTo(Formulas.SkillPointsForLevelUp(1, guest.Level)));
        }

        [Test] // GG-T114: a fallen guest is down, not lost — revive them and they still join
        public void A_guest_revived_mid_battle_is_recruited()
        {
            var c = Real; var s = Progression.NewGame(c, "G2");
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1);
            setup.Stage = GuestStage("drifter");
            var ev = new List<BattleEvent>();
            var st = BattleEngine.Create(c, setup, ev);
            var guest = st.Units.Single(u => u.IsGuest);
            // The guest steps onto a trap at 1 HP and falls — through the engine, so the fall is announced.
            guest.Hp = 1;
            var trapAt = new Coord(guest.Pos.X, guest.Pos.Y - 1);
            Assert.That(st.FreeForSpawn(trapAt), Is.True);
            st.Traps.Add(new Trap { At = trapAt, TrapId = c.Traps.Values.First(x => x.Damage > 0).Id });
            ev.AddRange(BattleEngine.Apply(st, new MoveCommand { HeroId = guest.Id, Path = { trapAt } }));
            Assert.That(guest.Alive, Is.False);
            var said = ev.OfType<AnnounceEvent>().Select(a => a.Text).ToList();
            Assert.That(said, Does.Contain($"{guest.Name} has fallen!"));
            Assert.That(said.Any(x => x.ToLowerInvariant().Contains("lost forever") || x.ToLowerInvariant().Contains("for good")), Is.False);
            // A Revival Charm brings the guest back like any hero.
            var hero = st.Units.First(u => u.IsHero && !u.IsGuest && u.Alive);
            hero.Bar[0] = new AbilitySlot { ItemId = "revival_charm", Charges = 1, MaxCharges = 1 };
            var revived = BattleEngine.Apply(st, new UseSlotCommand { HeroId = hero.Id, Slot = 0, Target = hero.Pos });
            Assert.That(revived.OfType<RevivedEvent>().Any(r => r.UnitId == guest.Id), Is.True);
            Assert.That(guest.Alive, Is.True);
            st.Phase = BattlePhase.Victory;
            var rep = BattleFactory.Commit(c, s, st);
            Assert.That(rep.GuestsJoined, Does.Contain("drifter"));
            Assert.That(rep.GuestsFell, Is.Empty);
            Assert.That(s.Hero("drifter"), Is.Not.Null);
        }

        [Test] // GG-T114: down at the end = not lost; the guest turns up again and can still be recruited
        public void A_guest_down_at_battle_end_can_still_be_recruited_later()
        {
            var c = Real; var s = Progression.NewGame(c, "G3");
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1);
            setup.Stage = GuestStage("drifter");
            var st = BattleEngine.Create(c, setup, new List<BattleEvent>());
            var guest = st.Units.Single(u => u.IsGuest);
            guest.Hp = 0; guest.OnBoard = false;
            st.Phase = BattlePhase.Victory;
            var rep = BattleFactory.Commit(c, s, st);
            Assert.That(rep.GuestsFell, Does.Contain("drifter"));
            Assert.That(rep.GuestsLost, Is.Empty);
            Assert.That(s.Hero("drifter"), Is.Null);
            Assert.That(s.LostGuests, Is.Empty);
            // The offer stays open: the guest reappears on a replay, and this time standing at the end recruits them.
            var again = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 2);
            again.Stage = GuestStage("drifter");
            Assert.That(again.ExcludedGuests, Does.Not.Contain("drifter"));
            var st2 = BattleEngine.Create(c, again, new List<BattleEvent>());
            Assert.That(st2.Units.Any(u => u.IsGuest && u.Def.Id == "drifter"), Is.True);
            st2.Phase = BattlePhase.Victory;
            Assert.That(BattleFactory.Commit(c, s, st2).GuestsJoined, Does.Contain("drifter"));
            Assert.That(s.Hero("drifter"), Is.Not.Null);
        }

        private static StageDef TwoWaveGuestStage(string guest) => new StageDef
        {
            Id = "g2w", Name = "G2W", IsTest = true,
            Waves =
            {
                new WaveDef { Enemies = { new SpawnDef { Class = "slime00", Level = 1, X = 2, Y = 1 } } },
                new WaveDef
                {
                    Enemies = { new SpawnDef { Class = "slime00", Level = 1, X = 2, Y = 1 } },
                    Guests = { new SpawnDef { Class = guest, Level = 3, X = 0, Y = 5 } },
                },
            },
        };

        [Test] // GG-T140: a downed hero is still on the roster (no permadeath) and must still block a same-class guest
        public void A_downed_heros_class_still_blocks_a_later_wave_guest_of_that_class()
        {
            var c = Real; var s = Progression.NewGame(c, "G5"); // default trio includes "barbarian"
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1);
            setup.Stage = TwoWaveGuestStage("barbarian");
            var ev = new List<BattleEvent>();
            var st = BattleEngine.Create(c, setup, ev);

            var barbarian = st.Units.Single(u => u.IsHero && !u.IsGuest && u.Def.Id == "barbarian");
            barbarian.Hp = 0; barbarian.OnBoard = false; // downed mid-battle, not removed: no permadeath
            var enemy = st.Units.Single(u => u.Team == Team.Enemy && u.Alive);
            enemy.Hp = 0; enemy.OnBoard = false; // clear wave 0 by hand

            ev.Clear();
            ev.AddRange(BattleEngine.Apply(st, new ShieldCommand()));

            Assert.That(st.Wave, Is.EqualTo(1), "wave 1 (the barbarian guest) should have spawned");
            Assert.That(ev.OfType<GuestJoinedEvent>(), Is.Empty, "the downed barbarian's class should still block the duplicate guest");
            Assert.That(st.Units.Count(u => u.Def.Id == "barbarian"), Is.EqualTo(1), "no duplicate barbarian on the board");
        }

        [Test] // GG-T114: "no one dies permanently" — the one permanent-loss rule is off, and Ironman doesn't exist yet
        public void Permadeath_is_off_by_default_and_old_lost_guests_come_back()
        {
            var c = Real; var s = Progression.NewGame(c, "G4");
            Assert.That(Permadeath.Enabled(s), Is.False);
            // A save from before GG-T114 that recorded a "lost forever" guest: nobody stays lost.
            var poolId = Summon.Ids(c).First(id => s.Hero(id) == null && c.TryClass(id) != null);
            s.LostGuests.Add("drifter");
            s.LostGuests.Add(poolId);
            Assert.That(Permadeath.IsLost(s, "drifter"), Is.False);
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1);
            Assert.That(setup.Permadeath, Is.False);
            Assert.That(setup.ExcludedGuests, Does.Not.Contain("drifter"));
            Assert.That(Summon.Pool(c, s).Any(d => d.Id == poolId), Is.True);
            s.Gold = Summon.Price(c, s);
            var r = Summon.Recruit(c, s, poolId);
            Assert.That(r.Ok, Is.True, r.Message);
        }

        // ── Caravan upgrades (§25.12) ──

        [Test]
        public void Caravan_upgrades_cost_gold_and_change_how_expeditions_play()
        {
            var c = Real; var s = Progression.NewGame(c, "Car");
            Assert.That(c.Caravan.Count >= 5, Is.True);
            s.Gold = 10000;
            var stage0 = c.Campaign.Themes[0].Stages[0];
            int manaBefore = BattleFactory.Build(c, s, stage0, 1).StartMana;
            Assert.That(CaravanUpgrades.Buy(c, s, "mana_casks").Ok, Is.True);
            Assert.That(BattleFactory.Build(c, s, stage0, 1).StartMana, Is.EqualTo(manaBefore + 100));
            Assert.That(10000 - c.Caravan["mana_casks"].Levels[0].Cost == s.Gold, Is.True);

            // Armory: cheaper repairs.
            var sword = c.Items.Values.First(i => i.Durability > 2 && i.Slot == EquipmentSlot.Weapon);
            int full = Repair.Cost(c, s, sword, 0, 0);
            CaravanUpgrades.Buy(c, s, "armory");
            Assert.That(Repair.Cost(c, s, sword, 0, 0) < full, Is.True);

            // Medic wagon (GG-T1790): after a win on the road the wagon medic patches up a share of missing HP.
            Assert.That(CaravanUpgrades.Buy(c, s, "medic_wagon").Ok, Is.True);
            TravelRules.SetOut(s);
            var wounded = s.Hero(s.Party[0]);
            wounded.Hp = 1;
            TravelRules.AfterBattle(c, s, true, 0, out int patched);
            Assert.That(patched, Is.GreaterThan(0));
            Assert.That(wounded.Hp == 0 || wounded.Hp > 1, Is.True);

            // Maxing out.
            while (CaravanUpgrades.NextLevel(c, s, "scouts") != null) Assert.That(CaravanUpgrades.Buy(c, s, "scouts").Ok, Is.True);
            Assert.That(CaravanUpgrades.Buy(c, s, "scouts").Ok, Is.False);
        }

        // ── Crafting station progression (§24.6.1, GG-T35) ──

        [Test]
        public void A_station_starts_at_apprentice_and_levels_up_from_real_crafting_actions()
        {
            var c = Real; var s = Progression.NewGame(c, "Station1");
            s.Flags.Add(Forging.FirstForgeFlag); // past the onboarding gate on armor-set recipes (GG-T178)
            s.Caravan[CaravanUpgradeDef.ForgeFacilityId] = 99; // GG-T432: only the skill axis here; facilities are FacilityTests
            Assert.That(StationProgress.LevelName(s, StationKind.Blacksmith), Is.EqualTo("Apprentice"));
            Assert.That(StationProgress.HeaderText(s, StationKind.Blacksmith), Is.EqualTo("Apprentice — 0/150 XP to Journeyman"));

            var t1 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 1);
            var t2 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 2);
            var t3 = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 3);
            void Stock(RecipeDef r)
            {
                s.Gold += r.Gold;
                foreach (var ic in r.Inputs) s.AddItem(ic.Item, ic.Count);
                if (r.BaseItem != null) s.AddItem(r.BaseItem, 1);
            }
            void CraftT1()
            {
                Stock(t1);
                Assert.That(Crafting.Craft(c, s, t1).Ok, Is.True);
                s.AddItem(t1.Result, -s.Count(t1.Result)); // clear the bag so MaxStack never blocks the next craft
            }

            Stock(t2);
            var blocked = Crafting.Craft(c, s, t2);
            Assert.That(blocked.Ok, Is.False);
            Assert.That(blocked.Message, Does.Contain("Journeyman"));

            for (int i = 0; i < 20 && StationProgress.Level(s, StationKind.Blacksmith) < 2; i++) CraftT1();
            Assert.That(StationProgress.LevelName(s, StationKind.Blacksmith), Is.EqualTo("Journeyman"));
            Assert.That(Crafting.Craft(c, s, t2).Ok, Is.True, "Journeyman should unlock the tier-2 recipe");

            Stock(t3);
            Assert.That(Crafting.Craft(c, s, t3).Ok, Is.False, "tier-3 still needs Master");
            for (int i = 0; i < 40 && StationProgress.Level(s, StationKind.Blacksmith) < 3; i++) CraftT1();
            Assert.That(StationProgress.LevelName(s, StationKind.Blacksmith), Is.EqualTo("Master"));
            Assert.That(StationProgress.HeaderText(s, StationKind.Blacksmith), Is.EqualTo("Master — every recipe tier unlocked"));
            Stock(t3);
            Assert.That(Crafting.Craft(c, s, t3).Ok, Is.True);

            Assert.That(StationProgress.Xp(s, StationKind.Alchemist), Is.EqualTo(0)); // Forge crafting never touches the Alchemist
        }

        [Test]
        public void Repair_and_salvage_grant_blacksmith_xp_but_never_alchemist()
        {
            var c = Real; var s = Progression.NewGame(c, "Station2");
            var recipe = Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 1
                && (c.Item(r.Result).Slot == EquipmentSlot.Weapon || System.Array.IndexOf(BattleEngine.ArmorSlots, c.Item(r.Result).Slot) >= 0)
                && c.Item(r.Result).Durability > 1);
            s.Gold += recipe.Gold;
            foreach (var ic in recipe.Inputs) s.AddItem(ic.Item, ic.Count);
            Assert.That(Crafting.Craft(c, s, recipe).Ok, Is.True);
            Assert.That(StationProgress.Xp(s, StationKind.Blacksmith), Is.EqualTo(Crafting.CraftXp));

            var item = c.Item(recipe.Result);
            var hero = s.Hero(s.Party[0]);
            Assert.That(Equipment.Equip(c, s, hero, item.Slot, item.Id).Ok, Is.True);
            s.Gold += 1000;
            Result repaired;
            if (item.Slot == EquipmentSlot.Weapon) { hero.WeaponDurability = 1; repaired = Repair.RepairWeapon(c, s, hero); }
            else { hero.Armor[item.Slot].Durability = 1; repaired = Repair.RepairArmor(c, s, hero, item.Slot); }
            Assert.That(repaired.Ok, Is.True, repaired.Message);
            Assert.That(StationProgress.Xp(s, StationKind.Blacksmith), Is.EqualTo(Crafting.CraftXp + Repair.RepairXp));

            s.AddItem(recipe.Result, 1); // a spare copy to salvage
            Assert.That(Crafting.Salvage(c, s, recipe.Result).Ok, Is.True);
            Assert.That(StationProgress.Xp(s, StationKind.Blacksmith), Is.EqualTo(Crafting.CraftXp + Repair.RepairXp + Crafting.SalvageXp));
            Assert.That(StationProgress.Xp(s, StationKind.Alchemist), Is.EqualTo(0));
        }

        [Test]
        public void Caravan_upgrades_and_station_tiers_never_double_gate_each_other()
        {
            // §25.12 caravan stat upgrades never move station XP and never open a recipe tier — GG-T35 must not make
            // either system move the other. (The facility levels of §25.12.2, GG-T432, are a separate material-tier gate.)
            var c = Real; var s = Progression.NewGame(c, "Station3");
            s.Gold = 10000;
            Assert.That(CaravanUpgrades.Buy(c, s, "armory").Ok, Is.True);
            Assert.That(CaravanUpgrades.Buy(c, s, "mana_casks").Ok, Is.True);
            Assert.That(StationProgress.Level(s, StationKind.Blacksmith), Is.EqualTo(1));
            Assert.That(StationProgress.Level(s, StationKind.Alchemist), Is.EqualTo(1));

            // Buying every caravan upgrade must not have unlocked a tier-2 recipe by itself.
            var t2 = Crafting.Recipes(c, CraftStation.Alchemy).First(r => r.Tier == 2);
            s.Gold += t2.Gold;
            foreach (var ic in t2.Inputs) s.AddItem(ic.Item, ic.Count);
            Assert.That(Crafting.Craft(c, s, t2).Ok, Is.False);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Masterwork crafting (§24.6.2, GG-T46)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Masterwork_chance_grows_with_party_luck_and_blacksmith_level_and_caps()
        {
            var c = Real; var s = Progression.NewGame(c, "MW1");
            float baseChance = Crafting.MasterworkChance(c, s);
            Assert.That(baseChance >= Crafting.MasterworkBaseChance, Is.True);

            foreach (var h in s.Roster) h.TotalXp = GridGame.Core.Rules.Formulas.TotalXpForLevel(20);
            float leveledChance = Crafting.MasterworkChance(c, s);
            Assert.That(leveledChance > baseChance, Is.True, "higher party Luck should raise the chance");

            s.StationXp["blacksmith"] = 9999;
            float masterStationChance = Crafting.MasterworkChance(c, s);
            Assert.That(masterStationChance > leveledChance, Is.True, "a higher Blacksmith level should raise the chance further");
            Assert.That(masterStationChance <= Crafting.MasterworkMaxChance, Is.True);
        }

        private static RecipeDef FindMasterworkCandidate(ContentDb c) =>
            Crafting.Recipes(c, CraftStation.Forge).First(r => r.BaseItem == null
                && (c.Item(r.Result).Slot == EquipmentSlot.Weapon || System.Array.IndexOf(BattleEngine.ArmorSlots, c.Item(r.Result).Slot) >= 0)
                && c.Item(r.Result).Durability > 1);

        /// <summary>Hunts for a seed that rolls Masterwork (probabilistic by design), same pattern as
        /// AiAndSystemsTests' clutch-chance hunt — deterministic given fixed seeds, so never flaky.</summary>
        private static Result CraftUntilMasterwork(ContentDb c, SaveData s, RecipeDef r, string resultId)
        {
            Result craft = default;
            for (int seed = 0; seed < 500; seed++)
            {
                foreach (var ic in r.Inputs) s.AddItem(ic.Item, ic.Count);
                craft = Crafting.Craft(c, s, r, rng: new Rng(seed));
                if (Equipment.IsMasterwork(s, resultId)) return craft;
                s.AddItem(resultId, -s.Count(resultId)); // free the MaxStack-1 slot for the next attempt
            }
            return craft;
        }

        private static float StatSum(GridGame.Core.Content.Stats st) =>
            st.Strength + st.Vitality + st.Agility + st.Speed + st.Intelligence + st.Wisdom + st.Luck;

        [Test]
        public void Forge_craft_can_roll_a_masterwork_variant_with_boosted_stats_and_durability()
        {
            var c = Real; var s = Progression.NewGame(c, "MW2");
            var recipe = FindMasterworkCandidate(c);
            var item = c.Item(recipe.Result);
            s.Gold += 10_000_000;

            var craft = CraftUntilMasterwork(c, s, recipe, item.Id);
            Assert.That(Equipment.IsMasterwork(s, item.Id), Is.True, "expected at least one Masterwork roll in 500 attempts");
            Assert.That(craft.Message, Does.Contain("★"));

            var worn = s.Worn[item.Id].Single();
            Assert.That(worn.Mastery, Is.InRange(1f + Crafting.MasterworkStatBonusMin, 1f + Crafting.MasterworkStatBonusMax));
            Assert.That(worn.Durability, Is.EqualTo((int)System.Math.Round(item.Durability * BattleConfig.MasterworkDurabilityMultiplier)));

            var hero = s.Hero(s.Party[0]);
            Assert.That(Equipment.Equip(c, s, hero, item.Slot, item.Id).Ok, Is.True);
            float mastery = item.Slot == EquipmentSlot.Weapon ? hero.WeaponMastery : hero.Armor[item.Slot].Mastery;
            int dur = item.Slot == EquipmentSlot.Weapon ? hero.WeaponDurability : hero.Armor[item.Slot].Durability;
            Assert.That(mastery, Is.EqualTo(worn.Mastery));
            Assert.That(dur, Is.EqualTo(worn.Durability));

            // The bonus reaches battle: GearStats and (for a weapon) WeaponPower scale by the rolled multiplier.
            var boosted = Progression.Preview(c, hero);
            Assert.That((double)StatSum(boosted.GearStats), Is.EqualTo((double)(StatSum(item.Stats) * worn.Mastery)).Within(1e-2));
            if (item.Slot == EquipmentSlot.Weapon) Assert.That((double)boosted.WeaponPower, Is.EqualTo((double)(item.BaseDamage * worn.Mastery)).Within(1e-2));
        }

        [Test]
        public void Unequipping_a_masterwork_weapon_remembers_its_roll_and_requipping_restores_it()
        {
            var c = Real; var s = Progression.NewGame(c, "MW3");
            var trained = Progression.EffectiveWeaponProficiencies(c, s.Hero(s.Party[0]));
            var recipe = Crafting.Recipes(c, CraftStation.Forge).First(r => r.BaseItem == null
                && c.Item(r.Result).Slot == EquipmentSlot.Weapon && c.Item(r.Result).Durability > 1 && trained.Contains(c.Item(r.Result).WeaponClass));
            var item = c.Item(recipe.Result);
            s.Gold += 10_000_000;

            CraftUntilMasterwork(c, s, recipe, item.Id);
            Assert.That(Equipment.IsMasterwork(s, item.Id), Is.True);
            float rolledMastery = s.Worn[item.Id].Single().Mastery;

            var hero = s.Hero(s.Party[0]);
            Assert.That(Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, item.Id).Ok, Is.True);
            Assert.That(hero.WeaponMastery, Is.EqualTo(rolledMastery));

            Assert.That(Equipment.Unequip(c, s, hero, EquipmentSlot.Weapon).Ok, Is.True);
            Assert.That(hero.WeaponMastery, Is.EqualTo(1f));
            Assert.That(Equipment.IsMasterwork(s, item.Id), Is.True); // the bag copy still remembers its roll

            Assert.That(Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, item.Id).Ok, Is.True);
            Assert.That(hero.WeaponMastery, Is.EqualTo(rolledMastery));
        }

        [Test]
        public void Enchant_crafts_never_roll_masterwork_even_though_they_also_touch_weapons()
        {
            var c = Real; var s = Progression.NewGame(c, "MW4");
            s.Gold += 1_000_000;
            s.StationXp["alchemist"] = 9999; // every Enchant recipe is Master tier (§24.6.1)
            var recipe = Crafting.Recipes(c, CraftStation.Enchant).First();
            foreach (var ic in recipe.Inputs) s.AddItem(ic.Item, ic.Count);
            if (recipe.BaseItem != null) s.AddItem(recipe.BaseItem, 1);
            var craft = Crafting.Craft(c, s, recipe);
            Assert.That(craft.Ok, Is.True, craft.Message);
            Assert.That(Equipment.IsMasterwork(s, recipe.Result), Is.False);
            Assert.That(craft.Message, Does.Not.Contain("★"));
        }
    }
}
