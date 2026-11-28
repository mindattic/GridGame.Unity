using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Training (GG-T180, §25.0.9): onboarding step 7, the last of the chain. Once the first armor enchant is bound, the
    // Abilities screen opens on its Board sub-tab: a guided prompt trains one new skill (free) and slots it on the bar.
    // GG-T1085: the lesson is an ability node on the skill hexagon, bought (route and all) without spending skill points.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Training
    {
        /// <summary>Save flag set by the guided first skill; the onboarding chain is complete once it is set.</summary>
        public const string FirstSkillFlag = "onboard:first-skill";
        /// <summary>The Abilities screen's camp tip while this step is ahead (its plain "abilities" tip otherwise).</summary>
        public const string TipStation = "abilities-learn";

        /// <summary>The guided first skill: which hero learns which skill, and whether the bar has room for it.</summary>
        public sealed class Plan
        {
            public HeroSave Hero;
            public AbilityDef Skill;
            public int Slot;
            /// <summary>The ability node <see cref="Teach"/> buys (GG-T1085).</summary>
            public (int Q, int R) Node;
            /// <summary>The hero isn't on the board yet: <see cref="Teach"/> joins it on its class Start path first.</summary>
            public bool Join;
        }

        /// <summary>The first skill is behind this save: trained, or a bot's or New Game+ save.</summary>
        public static bool FirstDone(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstSkillFlag);

        /// <summary>Step 7 is the current onboarding step: the first armor enchant is done (§25.0.8) and the first skill isn't.
        /// With the Enchanter hidden (<see cref="Features.Enchanter"/>, GG-T436) the enchant step is skipped and step 7
        /// follows the first armor (step 5) instead, so the chain never stalls on a station the player can't open.</summary>
        public static bool Step(SaveData s) => !FirstDone(s) &&
            (Features.Enchanter ? s.Flags.Contains(ArmorEnchanting.FirstEnchantFlag) : Forging.ArmorDone(s));

        /// <summary>Is <paramref name="a"/> right now the guided first skill (<see cref="First"/>'s pick, free, auto-slotted)?</summary>
        public static bool Guides(ContentDb c, SaveData s, AbilityDef a) => a != null && Step(s) && First(c, s)?.Skill == a;

        /// <summary>The bar slot a newly trained skill goes into: the first empty unlocked slot, else the last unlocked one.</summary>
        public static int SlotFor(SaveData s, HeroSave h)
        {
            int unlocked = Math.Min(Progression.UnlockedSlots(s), Progression.MaxBarSlots);
            if (h == null || unlocked <= 0) return -1;
            while (h.Bar.Count < Progression.MaxBarSlots) h.Bar.Add(null);
            for (int i = 0; i < unlocked; i++) if (h.Bar[i] == null) return i;
            return unlocked - 1;
        }

        /// <summary>The guided first skill (GG-T1085), or null outside step 7: for the party lead first (then the party,
        /// then the reserve), an ability node on the skill hexagon it can reach (joining its Start path first when it isn't
        /// on the board) whose ability it doesn't know: a frontier node when one exists, else the nearest by route; skills first.</summary>
        public static Plan First(ContentDb c, SaveData s)
        {
            if (c == null || !Step(s)) return null;
            var board = SkillHexProgress.Board(c);
            if (board == null) return null;
            var heroes = s.Party.Select(s.Hero).Where(h => h != null).Concat(s.Roster.Where(h => !s.Party.Contains(h.ClassId)));
            foreach (var h in heroes)
            {
                if (h.Domesticated) continue;
                bool join = (h.HexNodes?.Count ?? 0) == 0;
                IReadOnlyCollection<(int Q, int R)> owned = join ? board.StartOf(h.ClassId) : SkillHexProgress.Owned(h).Nodes;
                if (owned.Count == 0) continue;
                var pick = NodeFor(c, board, h, owned);
                if (pick == null) continue;
                return new Plan { Hero = h, Skill = c.TryAbility(board.Node(pick.Value).Ability), Node = pick.Value, Join = join, Slot = SlotFor(s, h) };
            }
            return null;
        }

        private static (int Q, int R)? NodeFor(ContentDb c, SkillHexBoard board, HeroSave h, IReadOnlyCollection<(int Q, int R)> owned)
        {
            bool Wanted((int Q, int R) pt)
            {
                var n = board.Node(pt);
                if (n == null || n.Kind != SkillHexNodeKind.Ability || string.IsNullOrEmpty(n.Ability) || owned.Contains(pt)) return false;
                var a = c.TryAbility(n.Ability);
                return a != null && !h.Known.Contains(a.Id) && !owned.Any(o => board.Node(o)?.Ability == a.Id);
            }
            int Rank((int Q, int R) pt) => c.TryAbility(board.Node(pt).Ability).Kind == AbilityKind.Skill ? 0 : 1;
            var near = board.Frontier(owned).Where(Wanted).OrderBy(Rank).ThenBy(p => p).ToList();
            if (near.Count > 0) return near[0];
            var far = board.Def.Nodes.Select(n => (n.Q, n.R)).Where(Wanted)
                .Select(pt => (pt, route: board.CheapestRoute(owned, pt))).Where(x => x.route != null)
                .OrderBy(x => x.route.Count).ThenBy(x => Rank(x.pt)).ThenBy(x => x.pt).ToList();
            return far.Count > 0 ? far[0].pt : ((int Q, int R)?)null;
        }

        /// <summary>Trains the guided first skill: Join if needed, buys the route to the node free (no SP), slots it via
        /// <see cref="SlotFor"/>, sets <see cref="FirstSkillFlag"/>.</summary>
        public static Result Teach(ContentDb c, SaveData s)
        {
            var plan = First(c, s);
            if (plan == null) return Result.Fail("Nothing to train");
            var h = plan.Hero;
            var board = SkillHexProgress.Board(c);
            if (plan.Join) { var j = SkillHexProgress.Join(c, h); if (!j.Ok) return j; }
            var route = board.CheapestRoute(SkillHexProgress.Owned(h).Nodes, plan.Node);
            if (route == null) return Result.Fail("Can't reach that");
            foreach (var pt in route)
            {
                int price = board.Price(pt);
                h.SkillPoints += price; // free: the price is granted, then spent
                var r = SkillHexProgress.Buy(c, h, pt);
                if (!r.Ok) { h.SkillPoints -= price; return r; }
            }
            Codex.Unlock(s, CodexKinds.Ability, plan.Skill.Id);
            int slot = SlotFor(s, h);
            if (slot >= 0) Abilities.Assign(c, s, h, slot, plan.Skill.Id);
            s.Flags.Add(FirstSkillFlag);
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            return Result.Success(slot >= 0 ? $"{hero} learned {plan.Skill.Name}: it's on the battle bar, slot {slot + 1}" : $"{hero} learned {plan.Skill.Name}");
        }
    }
}
