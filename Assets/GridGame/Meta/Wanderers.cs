using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Wanderers (§25.11.2, GG-T342; amends the GG-T236 random rescues). Owner, 2026-11-07: "you will sometimes meet a hero
    /// while fighting a monster and they will fight the monsters separately but you are able to complete pincer moves with
    /// them so you fill a trust gauge by pincering, or healing them at the end of the stage and the loot you and the team
    /// member you choose has a conversation with them, if they can come to an understanding they join, if not they go back
    /// into the pool of possible recruits" — and "the point is to gain their trust, tell them what they want to hear, and
    /// then you have a follower — another party member."
    ///
    /// <para>The roll: a campaign battle the player launches may hold one wanderer — a random hero the player doesn't own —
    /// seeded from the stage, the save's creation time and the battle seed, so a battle replays the same roll. Only the
    /// player's own launches roll (BattleScreen calls <see cref="Apply"/>); the bots' balance runs, the dotnet soaks, Test
    /// Mode and the Crucible never do.</para>
    ///
    /// <para>The understanding (GG-T387, the owner's "game of wits"), deterministic, no dice: the talk is
    /// <see cref="DialogueDef.WitsExchanges"/> exchanges, each a question from the wanderer and three replies from the
    /// companion that win the point (+1), stay neutral (0) or lose it (-1). The trust earned in battle sets the starting
    /// standing (<see cref="StartStanding"/>: more trust, more slack; a companion of <b>their own faction</b> adds one), and
    /// they join when the final standing reaches <see cref="Threshold"/> (<see cref="Joins"/>).</para>
    /// </summary>
    public static class Wanderers
    {
        public const float Chance = 0.25f;

        // ── the roll ────────────────────────────────────────────────────────────────

        /// <summary>Adds the rolled wanderer (if any) to <paramref name="setup"/>.</summary>
        public static void Apply(ContentDb c, SaveData s, BattleSetup setup)
        {
            if (setup?.Stage == null) return;
            setup.Wanderer = Roll(c, s, setup.Stage, setup.Seed);
        }

        public static WandererSpec Roll(ContentDb c, SaveData s, StageDef stage, int battleSeed)
        {
            if (c == null || s == null || stage == null || s.TestMode || stage.IsTest || Crucible.IsStage(stage.Id)) return null;
            if (!c.CampaignStages().Any(x => x.Id == stage.Id) || stage.Waves.Count == 0) return null;
            // A stage still holding an authored guest or recruitable the player hasn't won keeps that meeting to itself; once
            // they've joined, a hunt there (GG-T388) can meet a wanderer like anywhere else.
            if (stage.Waves.Any(w => (w.Guests?.Any(g => s.Hero(g.Class) == null) ?? false) || (w.Recruitables?.Any(r => s.Hero(r.Class) == null) ?? false))) return null;
            var rng = new Random(SeedFor(s, stage.Id, battleSeed));
            if (rng.NextDouble() >= Chance) return null;
            var pool = Local(c, Pool(c, s), stage.Id);
            if (pool.Count == 0) return null;
            // Early in the stage, so there's a real fight to earn their trust in.
            return new WandererSpec { Class = pool[rng.Next(pool.Count)], Wave = rng.Next(Math.Min(2, stage.Waves.Count)) };
        }

        /// <summary>Every hero a wanderer may be — and the Summon Circle's Diamond draw (GG-T343) picks from: unowned, not
        /// lost, and <see cref="ClassDef.Acquire"/> Wanderer or Starter (a custom party can leave a starter unowned) — every
        /// other way in (the gold pool, guests, recruitables, story, mercenaries, the Camp NPC) keeps its own heroes (GG-T1416).
        /// Id order, so a seed always names the same hero. A wanderer who parts ways stays in it: owning the hero is the only
        /// thing that takes them out.</summary>
        public static List<string> Pool(ContentDb c, SaveData s)
        {
            return c.Classes.Values
                .Where(d => d.Team == Team.Hero && (d.Acquire == Acquire.Wanderer || d.Acquire == Acquire.Starter)
                    && !d.IsCut && s.Hero(d.Id) == null && !Permadeath.IsLost(s, d.Id))
                .Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        /// <summary>GG-T387 (owner: "random Hero NPCs from that area"): the heroes of <paramref name="pool"/> whose faction
        /// roams the stage's region (<see cref="FactionDef.Regions"/>), while any are left to meet; else the whole pool, so a
        /// region whose own people have all joined still meets strangers from the road.</summary>
        public static List<string> Local(ContentDb c, List<string> pool, string stageId)
        {
            var theme = c.ThemeOf(stageId)?.Id;
            if (theme == null) return pool;
            var local = pool.Where(id => c.FactionOf(id)?.Regions?.Exists(r => string.Equals(r, theme, StringComparison.OrdinalIgnoreCase)) ?? false).ToList();
            return local.Count > 0 ? local : pool;
        }

        public static int SeedFor(SaveData s, string stageId, int battleSeed)
        {
            unchecked
            {
                long t = s.CreatedUtc.Ticks;
                int h = (int)(t ^ (t >> 32));
                foreach (char ch in stageId) h = h * 31 + ch;
                return h * 7919 + battleSeed;
            }
        }

        // ── after the battle ────────────────────────────────────────────────────────

        /// <summary>A wanderer still standing at a victory, waiting to talk (<see cref="BattleReport.WandererOffer"/>).</summary>
        public sealed class Offer
        {
            public string ClassId;
            public int Level, Hp, MaxHp, Trust, TrustNeeded;
        }

        /// <summary>The offer a finished battle makes: the wanderer standing on the hero side at a victory, unowned.</summary>
        public static Offer OfferFrom(SaveData s, BattleState battle)
        {
            if (battle == null || battle.Phase != BattlePhase.Victory || battle.Wanderer == null || s.Hero(battle.Wanderer) != null) return null;
            var u = battle.Units.FirstOrDefault(x => x.Wanderer && x.Def.Id == battle.Wanderer);
            if (u == null || !u.Alive || !u.IsHero) return null;
            return new Offer { ClassId = u.Def.Id, Level = u.Level, Hp = u.Hp, MaxHp = u.MaxHp, Trust = u.Trust, TrustNeeded = u.TrustNeeded };
        }

        public enum TrustTier { Low, Half, Full }

        public static TrustTier Tier(int trust, int needed) =>
            needed <= 0 || trust >= needed ? TrustTier.Full : trust * 2 >= needed ? TrustTier.Half : TrustTier.Low;

        /// <summary>The companion is from the wanderer's own STORY.md faction (factions.json).</summary>
        public static bool Fits(ContentDb c, string wanderer, string companion)
        {
            var f = c.FactionOf(wanderer);
            return f != null && companion != null && c.FactionOf(companion) == f;
        }

        // ── the game of wits (GG-T387) ──────────────────────────────────────────────

        /// <summary>The standing the wanderer must be at, once the last exchange is done, to join.</summary>
        public const int Threshold = 4;
        /// <summary>A companion of their own faction starts one higher.</summary>
        public const int KinBonus = 1;
        /// <summary>The meter's top: the best start plus a point won in every exchange.</summary>
        public const int MaxStanding = 3 + KinBonus + DialogueDef.WitsExchanges;

        /// <summary>The standing trust alone gives (Low 0, Half 2, Full 3): more trust, more slack.</summary>
        public static int TrustStanding(TrustTier tier) => tier == TrustTier.Full ? 3 : tier == TrustTier.Half ? 2 : 0;

        /// <summary>Where the talk starts. Against <see cref="Threshold"/>: full trust must come out one point ahead (a kin
        /// companion: just not behind), half trust two (kin: one), too little trust can't get there (kin: every point).</summary>
        public static int StartStanding(TrustTier tier, bool fits) => TrustStanding(tier) + (fits ? KinBonus : 0);

        public static int StartStanding(ContentDb c, Offer o, string companion) =>
            o == null ? 0 : StartStanding(Tier(o.Trust, o.TrustNeeded), Fits(c, o.ClassId, companion));

        /// <summary>The whole understanding rule: the final standing reaches <see cref="Threshold"/>.</summary>
        public static bool Joins(int standing) => standing >= Threshold;

        /// <summary>Whether a talk with <paramref name="companion"/> that nets <paramref name="net"/> points over its exchanges
        /// (wins minus losses) wins them over.</summary>
        public static bool Judge(ContentDb c, Offer o, string companion, int net) => o != null && Joins(StartStanding(c, o, companion) + net);

        /// <summary>What a talk with <paramref name="companion"/> (null: anyone) needs, before anything is said (the results
        /// screen's hint).</summary>
        public static string Outlook(ContentDb c, Offer o, string companion)
        {
            int need = Threshold - StartStanding(c, o, companion);
            var f = c.FactionOf(o.ClassId);
            string kin = f == null ? "" : Fits(c, o.ClassId, companion) ? $" One of their own ({f.Name}) starts a point ahead." : $" One of {f.Name} would start a point ahead.";
            if (need > DialogueDef.WitsExchanges) return "Too little trust: no talk will win them over this time." + kin;
            if (need <= 0) return "They trust you: just don't lose more points than you win." + kin;
            if (need == DialogueDef.WitsExchanges) return "A game of wits: win every exchange to win them over." + kin;
            return $"A game of wits: come out {need} point{(need == 1 ? "" : "s")} ahead to win them over." + kin;
        }

        /// <summary>The conversation for <paramref name="classId"/>: one written for that hero, else their faction's, else the generic one.</summary>
        public static DialogueDef Scene(ContentDb c, string classId)
        {
            var all = c.DialogueOrder.Where(d => d.Trigger?.Kind == DialogueTriggerKind.Wanderer).ToList();
            var faction = c.FactionOf(classId)?.Id;
            return all.FirstOrDefault(d => string.Equals(d.Trigger.Id, classId, StringComparison.OrdinalIgnoreCase))
                ?? (faction == null ? null : all.FirstOrDefault(d => string.Equals(d.Trigger.Id, faction, StringComparison.OrdinalIgnoreCase)))
                ?? all.FirstOrDefault(d => d.Trigger.Id == null);
        }

        /// <summary>A runner for the talk: "$hero" is the wanderer, "$with" the chosen companion. Its
        /// <see cref="DialogueRunner.Wits"/> meter starts where trust and the companion put it and moves with each reply;
        /// when the talk reaches <see cref="DialogueDef.OutcomeNext"/> the standing is judged, a joiner joins, and the scene
        /// goes to its <see cref="DialogueDef.JoinNode"/> or <see cref="DialogueDef.PartNode"/>.</summary>
        public static DialogueRunner Begin(ContentDb c, SaveData s, Offer o, string companion)
        {
            var def = Scene(c, o.ClassId);
            if (def == null) return null;
            var run = new DialogueRunner(c, s, def, companion, o.ClassId)
            {
                Wits = new WitsMeter(StartStanding(c, o, companion), Threshold, MaxStanding),
            };
            run.Outcome = r =>
            {
                bool joins = Joins(r.Wits.Standing) && s.Hero(o.ClassId) == null;
                if (joins) r.Log.Add(Join(c, s, o));
                return joins ? DialogueDef.JoinNode : DialogueDef.PartNode;
            };
            return run;
        }

        /// <summary>Headless talk (tests; a bot never meets a wanderer): at every exchange picks the reply whose
        /// <see cref="DialogueChoice.Wit"/> is <paramref name="wit"/> (+1 win, 0 neutral, -1 lose). Returns whether they joined.</summary>
        public static bool Converse(ContentDb c, SaveData s, Offer o, string companion, int wit) =>
            Converse(c, s, o, companion, Enumerable.Repeat(wit, DialogueDef.WitsExchanges).ToArray());

        /// <summary>Headless talk with one <see cref="DialogueChoice.Wit"/> per exchange, in order (a missing one is neutral).</summary>
        public static bool Converse(ContentDb c, SaveData s, Offer o, string companion, IReadOnlyList<int> wits)
        {
            var run = Begin(c, s, o, companion);
            if (run == null) return false;
            int exchange = 0;
            for (int guard = 0; !run.Finished && guard < 200; guard++)
            {
                if (run.Advance()) continue;
                int want = exchange < wits.Count ? wits[exchange] : 0;
                exchange++;
                int pick = run.Choices.FindIndex(ch => ch.Wit == want);
                run.Choose(pick < 0 ? 0 : pick);
            }
            return s.Hero(o.ClassId) != null;
        }

        /// <summary>The index of the reply that wins the point in each of <paramref name="def"/>'s exchanges, in order.</summary>
        public static List<int> RightAnswers(DialogueDef def) =>
            def == null ? new List<int>() : def.Nodes.Where(n => n != null && n.Choices.Count > 0).Select(n => n.Choices.FindIndex(ch => ch != null && ch.Wit > 0)).ToList();

        /// <summary>They join exactly like a freed guest: the roster, the party if there's room, at their battle level. One won
        /// over by rapport (Trust met) arrives with the Rapport seed, else the Hire one.</summary>
        public static string Join(ContentDb c, SaveData s, Offer o)
        {
            var route = o.Trust >= o.TrustNeeded ? JoinRoute.Rapport : JoinRoute.Hire;
            bool party = BattleFactory.JoinRoster(c, s, o.ClassId, o.Level, o.Hp, o.MaxHp, route);
            var name = c.Class(o.ClassId).Name;
            return party ? $"{name} joins the caravan!" : $"{name} joins the caravan (in reserve)";
        }
    }
}
