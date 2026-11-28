using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    public static class BattleFactory
    {
        /// <summary>The battle spec for <paramref name="h"/>; <paramref name="s"/> (when given) supplies its Trust tier (GG-T1581).</summary>
        public static HeroSpec SpecFor(ContentDb c, HeroSave h, SaveData s = null)
        {
            var spec = new HeroSpec
            {
                ClassId = h.ClassId, Level = Progression.Level(h), Hp = h.Hp,
                WeaponId = h.WeaponId, WeaponDurability = h.WeaponDurability, WeaponMastery = h.WeaponMastery,
            };
            if (h.WeaponId != null) spec.EquippedItemIds.Add(h.WeaponId);
            foreach (var kv in h.Armor)
            {
                if (kv.Value?.ItemId == null) continue;
                spec.Armor[kv.Key] = new ArmorPieceSpec { ItemId = kv.Value.ItemId, Durability = kv.Value.Durability, Mastery = kv.Value.Mastery, Enchant = kv.Value.Enchant };
                spec.EquippedItemIds.Add(kv.Value.ItemId);
            }
            foreach (var id in h.Jewelry.Values) if (id != null) spec.EquippedItemIds.Add(id);
            spec.Bar = new List<string>(h.Bar);
            // GG-T296: learned passives/reactives (skill-tree grants like Last Bastion's Counter Attack) fight with the hero.
            // GG-T301: only the ones switched on, up to the hero's passive slots (PassiveLoadout); class-kit ones are innate.
            spec.Passives.AddRange(PassiveLoadout.Active(c, h));
            spec.Domesticated = h.Domesticated; // fights on its own AI (GG-T303)
            var boardStats = HeroBoard.BoardStats(c, h); // owned skill hexagon passives (GG-T1057; was StatPoints picks, GG-T591)
            if (!HeroBoard.IsZero(boardStats)) spec.StatBonus = boardStats;
            if (s != null) spec.TrustTier = Bonds.HasTrust(s, h.ClassId) ? Bonds.TrustTier(s, h.ClassId) : 0;
            return spec;
        }

        public static BattleSetup Build(ContentDb c, SaveData s, string stageId, int seed, int difficulty = DifficultyTable.Neutral,
            TurnMode profileTurnMode = TurnMode.PartyWindow) =>
            Build(c, s, c.Stage(stageId), seed, difficulty, profileTurnMode);

        /// <summary>A battle for this save. <paramref name="difficulty"/> is the profile's dial 1..10 (GG-T309; the app passes
        /// <c>Settings.Difficulty.Level</c>, the soaks GRIDGAME_DIFFICULTY); the default <see cref="DifficultyTable.Neutral"/> is unscaled.
        /// <paramref name="profileTurnMode"/> is the profile's turn mode (§2.13, GG-T773; the app passes <c>Settings.TurnMode</c>);
        /// the save's lock wins over it (<see cref="TurnModeSetting.Resolve"/>).</summary>
        public static BattleSetup Build(ContentDb c, SaveData s, StageDef stage, int seed, int difficulty = DifficultyTable.Neutral,
            TurnMode profileTurnMode = TurnMode.PartyWindow)
        {
            Mercenaries.Settle(c, s); // a spent contract never fights an unpaid battle (GG-T233)
            bool crucible = Crucible.IsStage(stage.Id);
            bool drill = Drills.IsDrill(stage);
            var setup = new BattleSetup
            {
                Stage = stage,
                Seed = drill && stage.Seed is int fixedSeed ? fixedSeed : seed,
                Tutorial = drill, // GG-T1626: every drill stages its lessons' situations (TutorialSceneCommand)
                UnlockedSlots = Progression.UnlockedSlots(s),
                // A Crucible run authors every wave's level from the party's own (§22.10), so neither the campaign
                // curve nor New Game+'s step may lift it further.
                RecommendedLevel = crucible ? 1 : Progression.StageRecommendedLevel(c, s, stage),
                NewGamePlus = crucible ? 0 : s.NewGamePlus,
                Difficulty = difficulty <= DifficultyTable.Neutral ? DifficultyTable.Neutral : DifficultyTable.Clamp(difficulty),
                // §3.1.0 (GG-T1927): points granted to the party mana pool before wave 1: mana carried on the road, Well
                // Rested and the Mana Casks.
                StartMana = (crucible || drill ? 0 : TravelRules.StartMana(s) + CampRestService.WellRestedMana(s)) + (int)CaravanUpgrades.Value(c, s, "mana_casks"),
                TurnMode = TurnModeSetting.Resolve(s, profileTurnMode), // §2.13 (GG-T773): the save's lock, else the profile's
            };
            foreach (var h in s.Roster) setup.ExcludedGuests.Add(h.ClassId);
            foreach (var g in s.LostGuests) if (Permadeath.IsLost(s, g)) setup.ExcludedGuests.Add(g);
            setup.Permadeath = Permadeath.Enabled(s);
            setup.Purse = s.Gold; // a recruitable's PayGold comes out of the banked coins (GG-T132)
            if (!crucible) setup.Bounties = Bounties.MarksFor(c, s, stage); // GG-T431: a hunted target roams this stage
            // One shared pool: every hero's item slots draw from the same owned counts, in party order, so five heroes
            // can't each carry a full stack of the same five potions (GG-T285: 5 owned X-Potions became 25 heals).
            var owned = new Dictionary<string, int>(s.Inventory);
            int fielded = drill && stage.PartySize is int size ? size : int.MaxValue; // GG-T1958: The Trap fields two
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                if (h == null) continue;
                if (setup.Party.Count >= fielded) break;
                var spec = SpecFor(c, h, s);
                spec.ItemCounts = owned;
                setup.Party.Add(spec);
            }
            // §25.20 (GG-T1308): each bonded pair of the party's tier, for the pincer damage bonus.
            for (int i = 0; i < s.Party.Count; i++)
                for (int j = i + 1; j < s.Party.Count; j++)
                {
                    string key = Bonds.PairKey(s.Party[i], s.Party[j]);
                    if (key == null || !Bonds.HasCohesion(s, s.Party[i]) || !Bonds.HasCohesion(s, s.Party[j])) continue;
                    int tier = Bonds.CohesionTier(s, s.Party[i], s.Party[j]);
                    if (tier > 0) setup.Cohesion[key] = tier;
                }
            return setup;
        }

        /// <summary>Commits a finished battle into the save (XP, gold, loot, wounds, durability, kills, bestiary, unlocks).</summary>
        public static BattleReport Commit(ContentDb c, SaveData s, BattleState battle)
        {
            if (Drills.IsDrill(battle.Stage)) return Drills.Commit(c, s, battle);
            // Gold paid to a recruitable mid-battle (GG-T132) left the purse at that moment, whatever the outcome.
            if (battle.Results.CoinsSpent > 0) s.Gold = Math.Max(0, s.Gold - battle.Results.CoinsSpent);
            Mercenaries.CountBattle(s); // every contract loses a battle, fought in or not (GG-T233)
            if (Crucible.IsStage(battle.Stage.Id)) return Crucible.Commit(c, s, battle);
            var r = battle.Results;
            var rep = new BattleReport
            {
                Victory = battle.Phase == BattlePhase.Victory,
                Escaped = battle.Phase == BattlePhase.Escaped,
                Retreated = battle.Phase == BattlePhase.Retreated,
                StageId = battle.Stage.Id,
                StageName = battle.Stage.Name,
                StageTheme = battle.Stage.Theme,
                Difficulty = battle.Difficulty,
                ManaLeft = battle.PartyMana,
                BestComboRank = r.BestComboRank,
                BestComboScore = r.BestComboScore,
                ComboAttackerPoints = new Dictionary<string, int>(r.ComboAttackerPoints),
                ComboSupporterPoints = new Dictionary<string, int>(r.ComboSupporterPoints),
                PartyTurns = r.PartyTurns,
                Actions = r.ActionsTaken,
                Rounds = r.Rounds,
                HeroTurns = r.HeroTurns,
            };
            // GG-T852: the top supporter, ties to the lower class id so it is deterministic.
            foreach (var kv in r.ComboSupporterPoints.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                if (kv.Value > rep.TopSupporterPoints) { rep.TopSupporter = kv.Key; rep.TopSupporterPoints = kv.Value; }
            int slotsBefore = Progression.UnlockedSlots(s);
            Bargains.Rotate(s);

            foreach (var kv in r.ItemsConsumed) s.AddItem(kv.Key, -kv.Value);
            foreach (var id in r.Seen) Entry(s, id).Seen = true;

            if (rep.Victory)
            {
                rep.Gold = r.Coins;
                s.Gold += r.Coins;
                s.LifetimeCoins += r.Coins;
                GrantItems(c, s, rep, r.Items);
                foreach (var kv in r.Kills) Entry(s, kv.Key).Defeated += kv.Value;

                var heroes = battle.Units.Where(u => u.IsHero && !u.IsVisitor).ToList(); // a wild tamed beast or a wanderer isn't yours (GG-T303, GG-T342)
                rep.XpEach = heroes.Count > 0 ? r.Xp : 0;
                int foeLevel = TopFoeLevel(battle);
                foreach (var u in heroes)
                {
                    var h = s.Hero(u.Def.Id);
                    if (h == null) continue;
                    int before = Progression.Level(h);
                    h.TotalXp += Domestication.XpFor(h, Formulas.CatchUpXp(rep.XpEach, before, foeLevel));
                    int after = Progression.Level(h);
                    if (after > before) rep.LevelUps.Add((h.ClassId, before, after));
                    h.SkillPoints += Formulas.SkillPointsForLevelUp(before, after);
                    // Wounds carry; a hero who fell revives at 1 HP; a level-up refreshes to full.
                    h.Hp = after > before ? 0 : (u.Alive ? (u.Hp >= u.MaxHp ? 0 : u.Hp) : 1);
                    SyncGear(s, h, u, r);
                }
                // Shattered weapons and broken armor become salvage-only Broken fragments instead of
                // vanishing outright (GG-T41) — can't be equipped or repaired, only scrapped for materials.
                KeepBrokenFragments(s, r);
                rep.BondChanges = Bonds.ApplyBattle(s, r.PairCredits, heroes.Where(u => !u.IsGuest).Select(u => u.Def.Id));

                int index = c.CampaignStages().FindIndex(x => x.Id == battle.Stage.Id);
                rep.FirstClear = s.ClearedStages.Add(battle.Stage.Id);
                if (rep.FirstClear && index >= 0) rep.TicketsEarned += SummonTickets.AfterFirstClear(c, s, battle.Stage.Id); // GG-T1422
                rep.TicketsEarned += SummonTickets.AfterWin(s) + SummonTickets.DailyFirstWin(s, DateTime.UtcNow);
                // GG-T623: a slow, repeatable trickle of Aetherite Dust on every victory, paid out before the Aetherist
                // joins the caravan just as gold is — it simply has nowhere to refine until then.
                rep.DustEarned = DustEconomy.DustPerVictory + (rep.FirstClear ? DustEconomy.FirstClearDustBonus : 0);
                s.AddItem(DustEconomy.DustId, rep.DustEarned);
                var all = c.CampaignStages();
                rep.CampaignComplete = all.Count > 0 && index == all.Count - 1;
                if (index > s.HighestClearedStage)
                {
                    s.HighestClearedStage = index;
                    var list = c.CampaignStages();
                    if (index + 1 < list.Count) rep.UnlockedStage = list[index + 1].Name;
                }
                rep.QuestProgress = Quests.RecordBattle(c, s, battle);
                var milestone = Milestones.Pay(c, s, rep, battle.Stage.Id); // a theme's first full clear teaches (GG-T301)
                ManaOrbs.AfterVictory(c, s, rep, battle.Stage.Id, milestone); // GG-T1811: milestone Shards, Halcyon find Dust
                rep.TameOffers.AddRange(Domestication.Offers(c, s, battle)); // a fully bonded tamed monster asks to join (GG-T303)
            }
            else if (rep.Escaped)
            {
                // Escaping keeps wounds (no defeat reset) and grants nothing.
                foreach (var u in battle.Units.Where(u => u.IsHero && !u.IsVisitor))
                {
                    var h = s.Hero(u.Def.Id);
                    if (h != null) h.Hp = u.Alive ? (u.Hp >= u.MaxHp ? 0 : u.Hp) : 1;
                }
            }
            else if (rep.Retreated)
            {
                // FFIV-style partial loss (owner direction 2026-10-11): a voluntary retreat keeps a
                // fraction of this fight's uncommitted gold/loot plus hard-won XP — better than a wipe,
                // but not free. Still resets to full HP like a Defeat (you fall back and recover).
                foreach (var id in s.Party) { var h = s.Hero(id); if (h != null) h.Hp = 0; }
                int keepCoins = (int)Math.Floor(r.Coins * BattleConfig.RetreatKeepFraction);
                rep.Gold = keepCoins;
                s.Gold += keepCoins;
                s.LifetimeCoins += keepCoins;
                var kept = new Dictionary<string, int>();
                foreach (var kv in r.Items)
                {
                    int n = (int)Math.Floor(kv.Value * BattleConfig.RetreatKeepFraction);
                    if (n > 0) kept[kv.Key] = n;
                }
                GrantItems(c, s, rep, kept);
                GrantHardWonXp(s, battle, r, rep);
            }
            else
            {
                foreach (var id in s.Party) { var h = s.Hero(id); if (h != null) h.Hp = 0; } // defeat = retry at full (§22.2)
                GrantHardWonXp(s, battle, r, rep);
            }
            // Guests: standing at a victory (a revived guest counts) → they join the caravan. Down at the end → they're
            // NOT lost (GG-T114): the offer stays open and they turn up again on that stage. Only permadeath — off in the
            // normal game, the hook for a future Ironman mode — makes a fall permanent.
            foreach (var g in battle.Units.Where(u => u.IsGuest))
            {
                if (!g.Alive)
                {
                    if (Permadeath.Enabled(s)) { s.LostGuests.Add(g.Def.Id); rep.GuestsLost.Add(g.Def.Id); }
                    else if (s.Hero(g.Def.Id) == null) rep.GuestsFell.Add(g.Def.Id);
                    continue;
                }
                if (!rep.Victory || s.Hero(g.Def.Id) != null) continue;
                JoinRoster(c, s, g.Def.Id, g.Level, g.Hp, g.MaxHp, JoinRoute.Hire);
                rep.GuestsJoined.Add(g.Def.Id);
            }
            // A wanderer (GG-T342) doesn't join on its own: standing at a victory it waits to talk on the results screen;
            // down at the end it's simply gone back to the road — nothing recorded, still in the pool.
            rep.Wanderer = battle.Wanderer;
            rep.WandererOffer = Wanderers.OfferFrom(s, battle);
            rep.BountyProgress = Bounties.RecordBattle(c, s, battle); // GG-T431: a marked target that fell is slain, whatever the ending
            // GG-T1790 (§28): on the road a win carries the party mana pool's points on and the Medic Wagon patches wounds; a
            // loss camps.
            rep.CarriedMana = TravelRules.AfterBattle(c, s, rep.Victory, rep.ManaLeft, out rep.MedicHealed);
            // GG-T676, GG-T1927 (§3.1.0): the points left at the end of a won stage settle into Mana Dust, floor(points / 100)
            // x DustPerOrbLeft. Points travel carries to its next battle settle at the battle that finally keeps them; a lost
            // stage's mana simply discharges (no Dust, like every other reward of a loss).
            if (rep.Victory && rep.CarriedMana == 0) rep.OrbDust = DustEconomy.SettleMana(rep.ManaLeft);
            if (rep.OrbDust > 0)
            {
                s.AddItem(DustEconomy.DustId, rep.OrbDust);
                rep.DustEarned += rep.OrbDust;
            }
            rep.NewSlots = Progression.UnlockedSlots(s) - slotsBefore;
            s.LastStage = battle.Stage.Id;
            // GG-T929: the place fought in, then everything else the battle showed (foes seen, loot, joiners, kits).
            Codex.UnlockStage(c, s, battle.Stage.Id);
            Codex.Sync(c, s);
            s.UpdatedUtc = DateTime.UtcNow;
            return rep;
        }

        /// <summary>A hero met in battle joins the roster at <paramref name="level"/> with its wounds (a freed guest, a
        /// wanderer won round — GG-T342), and the party when there's room, with the Trust of <paramref name="route"/>.
        /// Returns whether it took a party slot.</summary>
        public static bool JoinRoster(ContentDb c, SaveData s, string classId, int level, int hp, int maxHp, JoinRoute route)
        {
            if (s.Hero(classId) != null) return false;
            var h = Progression.NewHero(c, classId);
            h.TotalXp = Formulas.TotalXpForLevel(level);
            h.SkillPoints += Formulas.SkillPointsForLevelUp(1, level);
            Progression.TrainTo(c, h); // a hero met on the road arrives trained to their level (GG-T388)
            h.Hp = hp >= maxHp ? 0 : hp;
            s.Roster.Add(h);
            Codex.UnlockHero(s, h); // GG-T929
            bool party = s.Party.Count < Progression.MaxPartySize;
            if (party) s.Party.Add(h.ClassId);
            Bonds.Joined(s, h.ClassId, route);
            return party;
        }

        /// <summary>Writes one hero's end-of-battle gear back into the save: a bar weapon swap, current weapon/armor
        /// wear, and gear that shattered mid-battle. Shared by Victory and the Crucible (§22.10).</summary>
        internal static void SyncGear(SaveData s, HeroSave h, Unit u, BattleResults r)
        {
            r.BrokenWeapons.TryGetValue(u.Def.Id, out var shatteredWeapons);
            var finalWeapon = r.FinalWeapon.TryGetValue(u.Def.Id, out var fw) ? fw : u.WeaponId;
            // Bag bookkeeping by conservation: hand-before + bar-before = hand-after + bar-after + shattered,
            // and bar weapons live in the bag, so the bag changes by (hand-before − hand-after − shattered).
            // The old pairwise rule (old weapon back unless it shattered, new one out) missed a bar weapon
            // that was swapped in and then shattered while the original was re-equipped from the bar: that
            // copy became a Broken fragment *and* stayed whole in the bag — a duplicate (GG-T70).
            var bagDelta = new Dictionary<string, int>();
            void Delta(string id, int n) { if (id == null) return; bagDelta.TryGetValue(id, out var v); bagDelta[id] = v + n; }
            if (finalWeapon != h.WeaponId) { Delta(h.WeaponId, +1); Delta(finalWeapon, -1); }
            if (shatteredWeapons != null) foreach (var id in shatteredWeapons) Delta(id, -1);
            foreach (var kv in bagDelta) if (kv.Value != 0) s.AddItem(kv.Key, kv.Value);
            if (finalWeapon != h.WeaponId && h.WeaponId != null && bagDelta.TryGetValue(h.WeaponId, out var back) && back > 0)
            {
                // The displaced hand weapon returns to the bag with its pre-battle wear, repairs and Masterwork roll,
                // as Equipment.Unequip stows it (GG-T1247), not as a factory-fresh copy.
                if (!s.Worn.TryGetValue(h.WeaponId, out var worn)) s.Worn[h.WeaponId] = worn = new List<WornGear>();
                worn.Add(new WornGear { Durability = h.WeaponDurability, Repairs = h.WeaponRepairs, Mastery = h.WeaponMastery });
            }
            if (finalWeapon != h.WeaponId && finalWeapon != null)
            {
                // A bar swap: the hand now holds the bar's weapon (bag already settled above).
                h.WeaponId = finalWeapon;
                h.WeaponRepairs = 0;
                h.WeaponMastery = 1f; // bar backups always enter at factory spec (§24.5), never Masterwork
            }
            if (r.FinalBar.TryGetValue(u.Def.Id, out var bar))
                for (int i = 0; i < bar.Count && i < h.Bar.Count; i++)
                    if (h.Bar[i] != null && h.Bar[i].StartsWith("weapon:")) h.Bar[i] = bar[i];
            if (r.WeaponDurability.TryGetValue(u.Def.Id, out var dur))
            {
                if (dur < 0 && h.WeaponId != null && u.WeaponId == null) { h.WeaponId = null; h.WeaponDurability = 0; h.WeaponRepairs = 0; h.WeaponMastery = 1f; }
                else if (dur >= 0 && h.WeaponId != null) h.WeaponDurability = dur;
            }
            if (r.ArmorDurability.TryGetValue(u.Def.Id, out var armorDur))
                foreach (var slot in BattleEngine.ArmorSlots)
                {
                    if (!armorDur.TryGetValue(slot, out var adur) || !h.Armor.TryGetValue(slot, out var piece)) continue;
                    if (adur < 0 && !u.Gear.ContainsKey(slot)) h.Armor.Remove(slot); // broke mid-battle
                    else if (adur >= 0) piece.Durability = adur; // survived: sync current wear
                }
        }

        internal static void KeepBrokenFragments(SaveData s, BattleResults r)
        {
            foreach (var list in r.BrokenWeapons.Values) foreach (var id in list) s.AddBroken(id, 1);
            foreach (var list in r.BrokenArmors.Values) foreach (var id in list) s.AddBroken(id, 1);
        }

        /// <summary>Grants items into the save, respecting stack caps (overflow auto-sells, US-245). Shared by
        /// Victory's full haul and a voluntary Retreat's partial one.</summary>
        internal static void GrantItems(ContentDb c, SaveData s, BattleReport rep, Dictionary<string, int> items)
        {
            foreach (var kv in items)
            {
                var def = c.TryItem(kv.Key);
                bool firstEver = s.Count(kv.Key) == 0;
                // An item the Merchant won't buy (SellValue 0, e.g. Diamonds) can't be auto-sold, and dropping it would lose
                // something the player may have paid real money for: it all goes in the bag, past the stack cap if need be.
                int room = def == null || Shop.SellPrice(def) <= 0 ? kv.Value : Math.Max(0, def.MaxStack - s.Count(kv.Key));
                int keep = Math.Min(room, kv.Value), extra = kv.Value - keep;
                if (keep > 0)
                {
                    s.AddItem(kv.Key, keep);
                    rep.Items[kv.Key] = keep;
                    if (firstEver) RecipeDiscovery.OnItemAcquired(c, s, kv.Key);
                }
                if (extra > 0 && def != null) { int g = extra * Shop.SellPrice(def); s.Gold += g; rep.Gold += g; rep.AutoSold += extra; }
            }
        }

        /// <summary>Hard-won experience (amended 2026-10-11): the XP of every foe felled is kept on a non-victory
        /// end, so each retry of a long gauntlet leaves the party stronger. Shared by Defeat and Retreat.</summary>
        private static void GrantHardWonXp(SaveData s, BattleState battle, BattleResults r, BattleReport rep)
        {
            var heroes = battle.Units.Where(u => u.IsHero && !u.IsGuest && !u.IsVisitor).ToList();
            rep.XpEach = heroes.Count > 0 ? r.Xp : 0;
            int foeLevel = TopFoeLevel(battle);
            foreach (var u in heroes)
            {
                var h = s.Hero(u.Def.Id);
                if (h == null || rep.XpEach <= 0) continue;
                int before = Progression.Level(h);
                h.TotalXp += Domestication.XpFor(h, Formulas.CatchUpXp(rep.XpEach, before, foeLevel));
                int after = Progression.Level(h);
                if (after > before) rep.LevelUps.Add((h.ClassId, before, after));
                h.SkillPoints += Formulas.SkillPointsForLevelUp(before, after);
            }
            foreach (var kv in r.Kills) Entry(s, kv.Key).Defeated += kv.Value;
        }

        /// <summary>The level of the strongest foe in the battle: what catch-up XP measures a hero against (GG-T474).</summary>
        private static int TopFoeLevel(BattleState battle) =>
            battle.Units.Where(u => !u.IsHero).Select(u => u.Level).DefaultIfEmpty(0).Max();

        internal static BestiaryEntry Entry(SaveData s, string id)
        {
            if (!s.Bestiary.TryGetValue(id, out var e)) { e = new BestiaryEntry(); s.Bestiary[id] = e; }
            return e;
        }
    }
}
