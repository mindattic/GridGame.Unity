using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Wanderers (§25.11.2, GG-T342; was the GG-T236 random rescues): the seeded roll now brings a wandering hero
    /// who fights the monsters on their own, earns trust from hero pincers and heals, and at a victory may be talked into
    /// joining — full trust, or half with a companion of their faction, and the answer they want to hear.</summary>
    public class WanderTests
    {
        private static StageDef PlainStage(ContentDb c) =>
            c.CampaignStages().First(st => st.Waves.All(w => w.Guests.Count == 0 && w.Recruitables.Count == 0));

        // ── the roll ────────────────────────────────────────────────────────────────

        [Test]
        public void The_roll_is_deterministic_and_lands_about_a_quarter_of_the_time()
        {
            var c = Real; var s = Progression.NewGame(c, "W1");
            var stage = PlainStage(c);
            int hits = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                var a = Wanderers.Roll(c, s, stage, seed);
                var b = Wanderers.Roll(c, s, stage, seed);
                Assert.That(b?.Class, Is.EqualTo(a?.Class));
                Assert.That(b?.Wave, Is.EqualTo(a?.Wave));
                if (a == null) continue;
                hits++;
                Assert.That(a.Wave, Is.InRange(0, stage.Waves.Count - 1));
                Assert.That(Wanderers.Pool(c, s), Does.Contain(a.Class));
            }
            Assert.That(hits, Is.InRange(400 * Wanderers.Chance * 0.5f, 400 * Wanderers.Chance * 1.5f));
        }

        [Test]
        public void A_wanderer_is_never_a_hero_promised_another_way_in()
        {
            var c = Real; var s = Progression.NewGame(c, "W2");
            var pool = Wanderers.Pool(c, s);
            Assert.That(pool, Is.Not.Empty);
            var guests = c.Stages.Values.SelectMany(st => st.Waves).SelectMany(w => w.Guests.Select(g => g.Class).Concat(w.Recruitables.Select(r => r.Class)));
            var named = c.Dialogues.Values.SelectMany(d => d.AllEffects()).Where(e => e != null && (e.Type == DialogueEffectType.JoinHero || e.Type == DialogueEffectType.RecruitHero) && e.Id != null).Select(e => e.Id);
            foreach (var id in pool)
            {
                Assert.That(c.Class(id).Team, Is.EqualTo(Team.Hero), id);
                Assert.That(s.Hero(id), Is.Null, id);
                Assert.That(Summon.Ids(c), Does.Not.Contain(id));
                Assert.That(guests, Does.Not.Contain(id));
                Assert.That(named, Does.Not.Contain(id));
                Assert.That(c.Mercenaries.Select(m => m.Class), Does.Not.Contain(id));
            }
        }

        [Test]
        public void No_roll_in_test_mode_the_crucible_or_a_stage_with_its_own_guest()
        {
            var c = Real;
            var s = Progression.NewGame(c, "W3");
            var withGuest = c.CampaignStages().FirstOrDefault(st => st.Waves.Any(w => w.Guests.Count > 0 || w.Recruitables.Count > 0));
            var test = Progression.NewGame(c, "W3t"); test.TestMode = true;
            for (int seed = 1; seed <= 200; seed++)
            {
                Assert.That(Wanderers.Roll(c, test, PlainStage(c), seed), Is.Null);
                if (withGuest != null) Assert.That(Wanderers.Roll(c, s, withGuest, seed), Is.Null);
                Assert.That(Wanderers.Roll(c, s, new StageDef { Id = Crucible.IdPrefix + seed, Name = "C", Waves = { new WaveDef() } }, seed), Is.Null);
            }
        }

        // ── on the board ────────────────────────────────────────────────────────────

        /// <summary>Mini content: knight at (0,4) and a healing cleric at (5,7); goblins at (1,3) and far off at (5,0); the
        /// wanderer (a mage) moved to (2,3) — so the knight stepping up to (0,3) pincers the goblin with it.</summary>
        private static (BattleState s, Unit w) MiniWanderer(int seed = 7)
        {
            var db = Mini();
            var setup = new BattleSetup { Stage = Stage(("goblin", 1, 3), ("goblin", 5, 0)), Seed = seed, UnlockedSlots = 5 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(0, 4) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(5, 7), Bar = new List<string> { "heal" } });
            setup.Wanderer = new WandererSpec { Class = "mage", Wave = 0 };
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var w = s.Units.Single(u => u.Wanderer);
            w.Pos = new Coord(2, 3);
            return (s, w);
        }

        private static void ActsNext(BattleState s, Unit u)
        {
            foreach (var e in s.Timeline.Where(e => e.Kind == TimelineKind.Turn)) e.Ip = 0;
            s.TurnEntryOf(u.Id).Ip = BattleConfig.BarLength - 1;
        }

        [Test]
        public void The_roll_brings_a_wanderer_who_is_an_independent_ally()
        {
            var c = Real; var s = Progression.NewGame(c, "W4");
            string cls = Wanderers.Pool(c, s)[0];
            var ev = new List<BattleEvent>();
            var setup = BattleFactory.Build(c, s, PlainStage(c).Id, 1);
            setup.Wanderer = new WandererSpec { Class = cls, Wave = 0 };
            var st = BattleEngine.Create(c, setup, ev);
            var w = st.Units.Single(u => u.Wanderer);
            Assert.That(w.Def.Id, Is.EqualTo(cls));
            Assert.That(w.IsHero && w.IsIndependent && w.CanPincer && w.IsVisitor, Is.True);
            Assert.That(w.IsGuest || w.IsCommandable, Is.False, "never commanded, never an auto-joining guest");
            Assert.That(w.Trust, Is.EqualTo(0));
            Assert.That(w.TrustNeeded, Is.EqualTo(BattleConfig.WandererTrustNeeded));
            Assert.That(st.TurnEntryOf(w.Id), Is.Not.Null, "its own timeline icon");
            Assert.That(BattleEngine.CanMove(st, w.Id), Is.False);
            Assert.That(BattleEngine.HeroesLeftToAct(st), Does.Not.Contain(w));
            Assert.That(ev.OfType<WandererJoinedEvent>().Single().UnitId, Is.EqualTo(w.Id));
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text.StartsWith("A wandering")), Is.True);
        }

        [Test]
        public void It_fights_the_monsters_on_its_own_turn()
        {
            var (s, w) = MiniWanderer();
            ActsNext(s, w);
            var ev = Turn(s, new ShieldCommand());
            int turn = ev.FindIndex(e => e is TurnStartedEvent t && t.UnitId == w.Id);
            Assert.That(turn, Is.GreaterThanOrEqualTo(0), "the wanderer took its own turn");
            var mine = ev.Skip(turn).OfType<AttackEvent>().Where(a => a.AttackerId == w.Id).ToList();
            Assert.That(mine, Is.Not.Empty, "it attacked");
            foreach (var a in mine) Assert.That(s.UnitById(a.TargetId).Team, Is.EqualTo(Team.Enemy));
        }

        [Test]
        public void A_hero_pincer_with_it_earns_trust()
        {
            var (s, w) = MiniWanderer();
            var knight = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((0, 3)) });
            Assert.That(ev.OfType<PincerFormedEvent>().Any(p => p.Team == Team.Hero && (p.Attacker1 == w.Id || p.Attacker2 == w.Id)), Is.True, "the knight closed a pincer with the wanderer");
            var grew = ev.OfType<TrustGrewEvent>().Where(t => t.UnitId == w.Id).ToList();
            Assert.That(grew.Select(t => t.Reason), Has.Member("pincer"));
            Assert.That(w.Trust, Is.EqualTo(BattleConfig.TrustPerPincer));
        }

        [Test]
        public void A_heal_from_a_hero_earns_trust_and_trust_stops_at_full()
        {
            var (s, w) = MiniWanderer();
            GrantMana(s, 6);
            w.Hp = w.MaxHp / 3;
            var cleric = Hero(s, "cleric");
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = cleric.Id, Slot = 0, Target = w.Pos });
            Assert.That(ev.OfType<TrustGrewEvent>().Any(t => t.UnitId == w.Id && t.Reason == "heal" && t.Gain == BattleConfig.TrustPerHeal), Is.True);
            Assert.That(w.Trust, Is.EqualTo(BattleConfig.TrustPerHeal));
            // Full is the cap.
            w.Trust = w.TrustNeeded;
            Assert.That(w.TrustFull, Is.True);
            w.Hp = 1;
            foreach (var u in s.Units) if (u.IsCommandable) u.ActedThisWindow = false;
            cleric.Cooldowns.Clear();
            ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = cleric.Id, Slot = 0, Target = w.Pos });
            Assert.That(ev.OfType<TrustGrewEvent>(), Is.Empty);
            Assert.That(w.Trust, Is.EqualTo(w.TrustNeeded));
        }

        [Test]
        public void A_wanderer_alone_cannot_hold_the_field()
        {
            var (s, w) = MiniWanderer();
            foreach (var h in s.Units.Where(u => u.IsCommandable)) { h.Hp = 0; h.OnBoard = false; }
            Assert.That(w.Alive, Is.True);
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(s.Phase == BattlePhase.Defeat || ev.Any(e => e is BattleEndedEvent b && !b.Victory), Is.True);
        }

        // ── after the battle ────────────────────────────────────────────────────────

        private static (BattleState st, BattleReport rep) Won(ContentDb c, SaveData s, string cls, bool standing, int trust)
        {
            var setup = BattleFactory.Build(c, s, PlainStage(c).Id, 1);
            setup.Wanderer = new WandererSpec { Class = cls, Wave = 0 };
            var st = BattleEngine.Create(c, setup, new List<BattleEvent>());
            var w = st.Units.Single(u => u.Wanderer);
            w.Trust = trust;
            if (!standing) { w.Hp = 0; w.OnBoard = false; }
            st.Phase = BattlePhase.Victory;
            return (st, BattleFactory.Commit(c, s, st));
        }

        [Test]
        public void A_standing_wanderer_waits_to_talk_and_a_fallen_one_goes_back_to_the_pool()
        {
            var c = Real; var s = Progression.NewGame(c, "W5");
            string cls = Wanderers.Pool(c, s)[0];
            var (_, rep) = Won(c, s, cls, true, 4);
            Assert.That(rep.Wanderer, Is.EqualTo(cls));
            Assert.That(rep.WandererOffer, Is.Not.Null);
            Assert.That(rep.WandererOffer.Trust, Is.EqualTo(4));
            Assert.That(rep.GuestsJoined, Does.Not.Contain(cls), "a wanderer never joins without the talk");
            Assert.That(s.Hero(cls), Is.Null);

            var s2 = Progression.NewGame(c, "W5b");
            var (_, down) = Won(c, s2, cls, false, 6);
            Assert.That(down.WandererOffer, Is.Null);
            Assert.That(s2.Hero(cls), Is.Null);
            Assert.That(s2.LostGuests, Is.Empty);
            Assert.That(Wanderers.Pool(c, s2), Does.Contain(cls));
        }

        [Test]
        public void The_game_of_wits_starts_from_trust_and_a_faction_mate_starts_a_point_ahead()
        {
            var T = Wanderers.TrustTier.Full; var H = Wanderers.TrustTier.Half; var L = Wanderers.TrustTier.Low;
            Assert.That(Wanderers.StartStanding(T, false), Is.EqualTo(3));
            Assert.That(Wanderers.StartStanding(T, true), Is.EqualTo(4));
            Assert.That(Wanderers.StartStanding(H, false), Is.EqualTo(2));
            Assert.That(Wanderers.StartStanding(H, true), Is.EqualTo(3));
            Assert.That(Wanderers.StartStanding(L, false), Is.EqualTo(0));
            Assert.That(Wanderers.StartStanding(L, true), Is.EqualTo(1));
            Assert.That(Wanderers.Joins(Wanderers.Threshold), Is.True);
            Assert.That(Wanderers.Joins(Wanderers.Threshold - 1), Is.False);
            Assert.That(Wanderers.Joins(Wanderers.StartStanding(T, false) + 1), Is.True, "full trust: one point ahead");
            Assert.That(Wanderers.Joins(Wanderers.StartStanding(T, false)), Is.False, "full trust: even isn't enough");
            Assert.That(Wanderers.Joins(Wanderers.StartStanding(T, true)), Is.True, "full trust with one of their own: just don't fall behind");
            Assert.That(Wanderers.Joins(Wanderers.StartStanding(H, false) + 2), Is.True);
            Assert.That(Wanderers.Joins(Wanderers.StartStanding(H, false) + 1), Is.False, "half trust: two points ahead");
            Assert.That(Wanderers.Joins(Wanderers.StartStanding(H, true) + 1), Is.True);
            Assert.That(Wanderers.Joins(Wanderers.StartStanding(L, false) + DialogueDef.WitsExchanges), Is.False, "too little trust: no talk will do");
            Assert.That(Wanderers.Joins(Wanderers.StartStanding(L, true) + DialogueDef.WitsExchanges), Is.True, "unless one of their own wins every point");
            Assert.That(Wanderers.MaxStanding, Is.EqualTo(Wanderers.StartStanding(T, true) + DialogueDef.WitsExchanges));
            Assert.That(Wanderers.Tier(6, 6), Is.EqualTo(T));
            Assert.That(Wanderers.Tier(3, 6), Is.EqualTo(H));
            Assert.That(Wanderers.Tier(2, 6), Is.EqualTo(L));
        }

        [Test]
        public void Each_reply_moves_the_standing_by_its_wit()
        {
            var c = Real; var s = Progression.NewGame(c, "W5w");
            var (cls, _, other) = Cast(c, s);
            var offer = new Wanderers.Offer { ClassId = cls, Level = 5, Hp = 30, MaxHp = 30, Trust = 3, TrustNeeded = 6 };
            var run = Wanderers.Begin(c, s, offer, other);
            Assert.That(run.Wits.Standing, Is.EqualTo(Wanderers.StartStanding(Wanderers.TrustTier.Half, false)));
            var moves = new[] { 1, -1, 0 };
            int exchange = 0;
            for (int guard = 0; !run.Finished && guard < 50; guard++)
            {
                if (run.Advance()) continue;
                int before = run.Wits.Standing, want = moves[exchange++];
                Assert.That(run.Choose(run.Choices.FindIndex(ch => ch.Wit == want)).Ok, Is.True);
                Assert.That(run.Wits.Standing - before, Is.EqualTo(want));
            }
            Assert.That(exchange, Is.EqualTo(DialogueDef.WitsExchanges));
            Assert.That(run.Wits.Moves, Is.EqualTo(moves));
            Assert.That(run.Transcript.Last().NodeId, Is.EqualTo(DialogueDef.PartNode), "2 + 0 net is short of the mark: they part");
            Assert.That(s.Hero(cls), Is.Null);
        }

        [Test]
        public void A_wanderer_met_in_a_region_is_one_of_its_own_people_while_any_are_left()
        {
            var c = Real; var s = Progression.NewGame(c, "W5r");
            foreach (var f in c.Factions)
                foreach (var region in f.Regions)
                {
                    var stage = c.Campaign.Themes.First(t => t.Id == region).Stages.FirstOrDefault();
                    if (stage == null) continue;
                    var pool = Wanderers.Pool(c, s);
                    var local = Wanderers.Local(c, pool, stage);
                    if (pool.Any(id => c.FactionOf(id) == f))
                        Assert.That(local, Is.Not.Empty.And.All.Matches<string>(id => c.FactionOf(id) == f), $"{region}: {f.Id}'s people");
                    else Assert.That(local, Is.EqualTo(pool), $"{region}: none left, so anyone from the road");
                }
            Assert.That(c.Factions.SelectMany(f => f.Regions), Is.Not.Empty, "factions roam regions");
        }

        /// <summary>A pool hero with a faction, a companion from that faction, and one from another.</summary>
        private static (string w, string kin, string other) Cast(ContentDb c, SaveData s)
        {
            foreach (var id in Wanderers.Pool(c, s))
            {
                var f = c.FactionOf(id);
                if (f == null) continue;
                var kin = f.Heroes.FirstOrDefault(h => h != id);
                var other = c.Factions.First(x => x != f).Heroes[0];
                if (kin != null) return (id, kin, other);
            }
            Assert.Fail("no pool hero with a faction-mate");
            return default;
        }

        [Test]
        public void Saying_what_they_want_to_hear_with_full_trust_makes_them_a_full_party_member()
        {
            var c = Real; var s = Progression.NewGame(c, "W6");
            var (cls, _, other) = Cast(c, s);
            var offer = new Wanderers.Offer { ClassId = cls, Level = 7, Hp = 10, MaxHp = 40, Trust = 6, TrustNeeded = 6 };
            Assert.That(Wanderers.RightAnswers(Wanderers.Scene(c, cls)), Has.Count.EqualTo(DialogueDef.WitsExchanges).And.All.InRange(0, 2));
            int partyBefore = s.Party.Count;
            Assert.That(Wanderers.Converse(c, s, offer, other, new[] { 1, 0, 0 }), Is.True, "full trust, one point ahead");
            var h = s.Hero(cls);
            Assert.That(h, Is.Not.Null);
            Assert.That(h.Domesticated, Is.False, "a normal hero, not a limited follower");
            Assert.That(Progression.Level(h), Is.EqualTo(7));
            Assert.That(h.Hp, Is.EqualTo(10));
            if (partyBefore < Progression.MaxPartySize) Assert.That(s.Party, Does.Contain(cls));
            Assert.That(Wanderers.Pool(c, s), Does.Not.Contain(cls));
        }

        [Test]
        public void Losing_the_game_of_wits_or_too_little_trust_sends_them_back_to_the_pool()
        {
            var c = Real;
            var s = Progression.NewGame(c, "W7");
            var (cls, kin, other) = Cast(c, s);
            var full = new Wanderers.Offer { ClassId = cls, Level = 5, Hp = 30, MaxHp = 30, Trust = 6, TrustNeeded = 6 };
            Assert.That(Wanderers.Converse(c, s, full, kin, -1), Is.False, "losing every point loses even full trust");
            Assert.That(s.Hero(cls), Is.Null);
            Assert.That(Wanderers.Pool(c, s), Does.Contain(cls), "parting ways records nothing");
            Assert.That(Wanderers.Converse(c, s, full, other, new[] { 1, 0, -1 }), Is.False, "full trust: even isn't enough");

            var half = new Wanderers.Offer { ClassId = cls, Level = 5, Hp = 30, MaxHp = 30, Trust = 3, TrustNeeded = 6 };
            Assert.That(Wanderers.Converse(c, s, half, other, new[] { 1, 0, 0 }), Is.False, "half trust must come out two ahead");
            var low = new Wanderers.Offer { ClassId = cls, Level = 5, Hp = 30, MaxHp = 30, Trust = 2, TrustNeeded = 6 };
            Assert.That(Wanderers.Converse(c, s, low, other, 1), Is.False, "too little trust: no talk will do");
            Assert.That(Wanderers.Converse(c, s, low, kin, new[] { 1, 1, 0 }), Is.False);
            Assert.That(s.Hero(cls), Is.Null);
            Assert.That(Wanderers.Converse(c, s, half, kin, new[] { 1, 0, 0 }), Is.True, "half trust and one of their own: one point ahead");
        }

        [Test]
        public void The_talk_is_deterministic()
        {
            var c = Real;
            var a = Progression.NewGame(c, "W8"); var b = Progression.NewGame(c, "W8");
            var (cls, kin, _) = Cast(c, a);
            var offer = new Wanderers.Offer { ClassId = cls, Level = 5, Hp = 30, MaxHp = 30, Trust = 4, TrustNeeded = 6 };
            for (int wit = -1; wit <= 1; wit++)
            {
                var sa = Progression.NewGame(c, "W8" + wit); var sb = Progression.NewGame(c, "W8" + wit);
                Assert.That(Wanderers.Converse(c, sa, offer, kin, wit), Is.EqualTo(Wanderers.Converse(c, sb, offer, kin, wit)));
            }
        }

        // ── content ─────────────────────────────────────────────────────────────────

        [Test]
        public void Every_pool_hero_has_a_faction_and_every_faction_a_talk()
        {
            var c = Real; var s = Progression.NewGame(c, "W9");
            foreach (var id in Wanderers.Pool(c, s))
            {
                Assert.That(c.FactionOf(id), Is.Not.Null, id);
                var scene = Wanderers.Scene(c, id);
                Assert.That(scene, Is.Not.Null, id);
                Assert.That(Wanderers.RightAnswers(scene), Has.Count.EqualTo(DialogueDef.WitsExchanges).And.All.InRange(0, 2), scene.Id);
            }
            foreach (var f in c.Factions)
                Assert.That(c.DialogueOrder.Any(d => d.Trigger.Kind == DialogueTriggerKind.Wanderer && d.Trigger.Id == f.Id), Is.True, f.Id);
            Assert.That(c.DialogueOrder.Any(d => d.Trigger.Kind == DialogueTriggerKind.Wanderer && d.Trigger.Id == null), Is.True, "a generic talk");
            Assert.That(ContentLoader.Validate(c, null).Where(p => p.Contains("wanderer_") || p.Contains("faction")), Is.Empty);
        }

        private static ContentDb TalkDb(DialogueDef d)
        {
            var db = new ContentDb();
            db.Classes["knight"] = new ClassDef { Id = "knight", Name = "Knight", Team = Team.Hero, Role = "Tank" };
            db.Classes["goblin"] = new ClassDef { Id = "goblin", Name = "Goblin", Team = Team.Enemy };
            db.Factions.Add(new FactionDef { Id = "holds", Name = "the Free Holds", Heroes = { "knight" } });
            db.Dialogues[d.Id] = d;
            db.DialogueOrder.Add(d);
            return db;
        }

        private static DialogueNode Exchange(string id, string next, params int[] wits)
        {
            var n = new DialogueNode { Id = id, Speaker = "h", Text = "Why?" };
            foreach (var w in wits) n.Choices.Add(new DialogueChoice { Label = "r" + w, Next = next, Wit = w });
            return n;
        }

        private static DialogueDef GoodTalk(string id = "holds") => new DialogueDef
        {
            Id = "wanderer_x", Trigger = new DialogueTrigger { Kind = DialogueTriggerKind.Wanderer, Id = id },
            Speakers = { new DialogueSpeaker { Id = "h", Class = "$hero" }, new DialogueSpeaker { Id = "w", Class = "$with" } },
            Nodes =
            {
                Exchange("ask1", "ask2", 1, 0, -1),
                Exchange("ask2", "ask3", 0, -1, 1),
                Exchange("ask3", "weigh", -1, 1, 0),
                new DialogueNode { Id = "weigh", Speaker = "h", Text = "Well.", Next = "$outcome" },
                new DialogueNode { Id = "join", Speaker = "h", Text = "I'm in." },
                new DialogueNode { Id = "part", Speaker = "w", Text = "Farewell." },
            },
        };

        [Test]
        public void Validation_holds_a_wanderer_talk_to_its_shape()
        {
            Assert.That(ContentLoader.Validate(TalkDb(GoodTalk()), null).Where(p => p.Contains("wanderer_x")), Is.Empty);
            Assert.That(ContentLoader.Validate(TalkDb(GoodTalk("knight")), null).Where(p => p.Contains("wanderer_x")), Is.Empty);
            Assert.That(ContentLoader.Validate(TalkDb(GoodTalk(null)), null).Where(p => p.Contains("wanderer_x")), Is.Empty);
            Assert.That(ContentLoader.Validate(TalkDb(GoodTalk("goblin")), null), Has.Some.Contains("Wanderer needs a hero class, a faction or no Id"));

            var two = GoodTalk(); two.Nodes[0].Choices[1].Wit = 1;
            Assert.That(ContentLoader.Validate(TalkDb(two), null), Has.Some.Contains("exactly one reply with Wit +1"));
            var short2 = GoodTalk(); short2.Nodes.RemoveAt(2); short2.Nodes[1].Choices.ForEach(ch => ch.Next = "weigh");
            Assert.That(ContentLoader.Validate(TalkDb(short2), null), Has.Some.Contains("needs 3 exchanges"));
            var split = GoodTalk(); split.Nodes[0].Choices[2].Next = "ask3";
            Assert.That(ContentLoader.Validate(TalkDb(split), null), Has.Some.Contains("every reply must go on to the same node"));
            var noJoin = GoodTalk(); noJoin.Nodes.RemoveAt(4);
            Assert.That(ContentLoader.Validate(TalkDb(noJoin), null), Has.Some.Contains("needs a 'join' node"));
            var effect = GoodTalk(); effect.Nodes[0].Choices[0].Effects.Add(new DialogueEffect { Type = DialogueEffectType.SetFlag, Id = "x" });
            Assert.That(ContentLoader.Validate(TalkDb(effect), null), Has.Some.Contains("can't have effects"));
            var four = GoodTalk(); four.Nodes[0].Choices.Add(new DialogueChoice { Label = "d", Next = "ask2" });
            Assert.That(ContentLoader.Validate(TalkDb(four), null), Has.Some.Contains("three replies"));

            // "$hero" belongs to a wanderer talk only; "$outcome" too.
            var talk = GoodTalk(); talk.Trigger = new DialogueTrigger { Kind = DialogueTriggerKind.Talk, Id = "knight" };
            var problems = ContentLoader.Validate(TalkDb(talk), null);
            Assert.That(problems, Has.Some.Contains("\"$hero\" only fits a Wanderer talk"));
            Assert.That(problems, Has.Some.Contains("unknown next node '$outcome'"));

            var db = TalkDb(GoodTalk());
            db.Factions.Add(new FactionDef { Id = "again", Name = "Again", Heroes = { "knight", "goblin" } });
            db.Factions[0].Regions.Add("nowhere");
            var fp = ContentLoader.Validate(db, null);
            Assert.That(fp, Has.Some.Contains("region 'nowhere' is not a campaign theme"));
            Assert.That(fp, Has.Some.Contains("'knight' is already in faction holds"));
            Assert.That(fp, Has.Some.Contains("'goblin' is not a hero class"));
        }
    }
}
