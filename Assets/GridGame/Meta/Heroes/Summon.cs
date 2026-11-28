using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    public static class Summon
    {
        /// <summary>A paywall hook (§25.18, GG-T232): <see cref="Paywall.Cost"/>, no longer rising per recruit.</summary>
        public static int Price(ContentDb c, SaveData s) => Paywall.Cost(PaywallKind.Summon, s);

        /// <summary>The Summon Circle's gold pool: every hero class whose <see cref="ClassDef.Acquire"/> is Summon (GG-T1416).</summary>
        public static List<string> Ids(ContentDb c) =>
            c.Classes.Values.Where(d => d.Team == Team.Hero && d.Acquire == Acquire.Summon).Select(d => d.Id).ToList();

        public static List<ClassDef> Pool(ContentDb c, SaveData s) =>
            c.Classes.Values.Where(d => d.Team == Team.Hero && d.Acquire == Acquire.Summon && !Permadeath.IsLost(s, d.Id)).ToList();

        private static Result CanRecruit(ContentDb c, SaveData s, string classId)
        {
            var def = c.TryClass(classId);
            if (def == null || def.Team != Team.Hero || def.Acquire != Acquire.Summon) return Result.Fail("Not summonable");
            if (Permadeath.IsLost(s, classId)) return Result.Fail("Fell in battle for good");
            if (s.Hero(classId) != null) return Result.Fail("Already in your roster");
            if (Paywall.Pending(s, PaywallKind.Summon) != null) return Result.Fail("A summoning is already under way");
            if (s.Gold < Price(c, s)) return Result.Fail("Not enough gold");
            return Result.Success();
        }

        /// <summary>The instant path (bots, tests): pays and the recruit arrives at once.</summary>
        public static Result Recruit(ContentDb c, SaveData s, string classId)
        {
            var ok = CanRecruit(c, s, classId);
            if (!ok.Ok) return ok;
            s.Gold -= Price(c, s);
            return Arrive(c, s, classId);
        }

        /// <summary>The player's path: pays and starts the summoning job; <see cref="Paywall.Finish"/> brings the hero.</summary>
        public static Result BeginRecruit(ContentDb c, SaveData s, string classId, DateTime now, out PaywallJob job)
        {
            job = null;
            var ok = CanRecruit(c, s, classId);
            return ok.Ok ? Paywall.Start(s, PaywallKind.Summon, classId, now, out job) : ok;
        }

        internal static Result Arrive(ContentDb c, SaveData s, string classId)
        {
            if (c.TryClass(classId) == null || s.Hero(classId) != null) return Result.Fail("The summoning fizzles");
            s.Recruits++;
            Join(c, s, classId);
            return Result.Success($"{c.Class(classId).Name} joins you");
        }

        /// <summary>Adds a hero to the roster (and the party, if there's room) at the roster's average experience —
        /// shared by the Summon Circle and dialogue's RecruitHero effect (GG-T117). No-op if already owned. It arrives with
        /// the Trust of <paramref name="route"/>.</summary>
        public static HeroSave Join(ContentDb c, SaveData s, string classId, JoinRoute route = JoinRoute.Hire)
        {
            var existing = s.Hero(classId);
            if (existing != null) return existing;
            var h = Progression.NewHero(c, classId);
            int avg = s.Roster.Count == 0 ? 0 : (int)s.Roster.Average(x => x.TotalXp);
            h.TotalXp = avg; // recruits arrive at the roster's average experience
            h.SkillPoints += Formulas.SkillPointsForLevelUp(1, Progression.Level(h));
            Progression.TrainTo(c, h); // trained to the level they arrive at (GG-T388)
            s.Roster.Add(h);
            Codex.UnlockHero(s, h); // GG-T929
            if (s.Party.Count < Progression.MaxPartySize) s.Party.Add(h.ClassId);
            Bonds.Joined(s, h.ClassId, route);
            return h;
        }

        /// <summary>Who a story JoinHero (GG-T118) brings into a save: <paramref name="preferred"/> if it's a hero the
        /// player doesn't have; else an unowned hero of <paramref name="role"/>; else one of a role the roster lacks
        /// (<see cref="StoryRoleOrder"/>); else anyone unowned — or null once every hero is owned. The fallbacks skip
        /// the Summon Circle's pool and the stages' guests so those stay worth having; deterministic (name order) so
        /// a beat's speaker and its joiner always agree. <paramref name="exclude"/> holds heroes already promised to
        /// earlier JoinHero effects of the same dialogue.</summary>
        public static string StoryPick(ContentDb c, SaveData s, string preferred, string role, ICollection<string> exclude = null)
        {
            bool Free(ClassDef d) => d != null && d.Team == Team.Hero && !d.IsCut && s.Hero(d.Id) == null && !Permadeath.IsLost(s, d.Id) && (exclude == null || !exclude.Contains(d.Id));
            if (preferred != null && Free(c.TryClass(preferred))) return preferred;
            var pool = c.Classes.Values.Where(d => Free(d) && d.Acquire != Acquire.Summon && d.Acquire != Acquire.Guest)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (role != null && pool.FirstOrDefault(d => d.Role == role) is ClassDef byRole) return byRole.Id;
            var have = new HashSet<string>(s.Roster.Select(h => c.TryClass(h.ClassId)?.Role).Where(r => r != null));
            foreach (var r in StoryRoleOrder)
                if (!have.Contains(r) && pool.FirstOrDefault(d => d.Role == r) is ClassDef missing) return missing.Id;
            return pool.FirstOrDefault()?.Id;
        }

        /// <summary>The order a JoinHero with no usable class or role fills gaps in the roster.</summary>
        public static readonly string[] StoryRoleOrder = { "Healer", "Tank", "Mage", "Support", "Striker", "Ranged" };
    }
}
