using System;
using GridGame.Core.Content;
using GridGame.Core.World;

namespace GridGame.Meta
{
    /// <summary>GG-T1785 (BIBLE §28, GG-T1705): travel replaces expeditions. The caravan is on the road whenever it is not
    /// camped (<see cref="WorldSave.Camped"/>); launching a battle from camp sets out. A win on the road carries the bank's
    /// orbs into the next battle and wounds stay; a loss falls back to camp with no orbs. Camp rest (<see cref="CampRestService"/>)
    /// heals the whole roster for free, and the Medic Wagon (<see cref="MedicWagon"/>) patches wounds after each win on the road.
    /// Not wired into BattleFactory or the screens yet: the follow-up task swaps <see cref="Expeditions"/> for these.
    /// TODO(owner, GG-T1705): the expedition completion bonus (<see cref="Expeditions.CompletionBonus"/>, a share of the
    /// expedition's coins) has no equivalent here, since travel has no fixed end. Owner question: is there a bonus on making
    /// camp, on reaching a location, or none? And with it, what the Strongbox upgrade (caravan.json "strongbox") means, or
    /// whether it is retired. Its data stays in place, unused by travel.</summary>
    public static class TravelRules
    {
        public static bool OnTheRoad(SaveData s) => s?.World != null && !s.World.Camped;

        /// <summary>A battle launched from camp sets out: the caravan is on the road from then on.</summary>
        public static void SetOut(SaveData s)
        {
            if (s?.World == null || !s.World.Camped) return;
            s.World.Camped = false;
            s.World.CarriedOrbs = 0;
        }

        /// <summary>Makes camp: off the road, nothing carried.</summary>
        public static void MakeCamp(SaveData s)
        {
            if (s?.World == null) return;
            s.World.Camped = true;
            s.World.CarriedOrbs = 0;
        }

        /// <summary>Orbs the next battle starts with: what the last win on the road left in the bank; none at camp.</summary>
        public static int StartOrbs(SaveData s) => OnTheRoad(s) ? Math.Max(0, s.World.CarriedOrbs) : 0;

        /// <summary>After a battle fought on the road (wounds are already written by the commit): a win carries the bank's
        /// <paramref name="orbsLeft"/> on and the Medic Wagon patches the party; any other ending falls back to camp with no
        /// orbs. Returns the orbs now carried (0 at camp) and, through <paramref name="medicHealed"/>, the HP the wagon gave.
        /// Does nothing at camp.</summary>
        public static int AfterBattle(ContentDb c, SaveData s, bool victory, int orbsLeft, out int medicHealed)
        {
            medicHealed = 0;
            if (!OnTheRoad(s)) return 0;
            if (!victory)
            {
                MakeCamp(s);
                return 0;
            }
            s.World.CarriedOrbs = Math.Max(0, orbsLeft);
            medicHealed = MedicWagon.Patch(c, s);
            return s.World.CarriedOrbs;
        }
    }

    /// <summary>GG-T1785: the Medic Wagon's wagon medic patches up a share of each party hero's missing HP after every battle
    /// won on the road. The share is the upgrade level's Value, clamped to 0..1 (caravan.json "medic_wagon").</summary>
    public static class MedicWagon
    {
        public static float Share(ContentDb c, SaveData s) =>
            Math.Max(0f, Math.Min(1f, CaravanUpgrades.Value(c, s, "medic_wagon")));

        /// <summary>Heals each wounded, standing party hero by the share of their missing HP (rounded up); a hero brought to
        /// full reads Hp 0, as everywhere in the save. Returns the total HP healed.</summary>
        public static int Patch(ContentDb c, SaveData s)
        {
            float share = Share(c, s);
            if (share <= 0f) return 0;
            int healed = 0;
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                if (h == null || h.Hp <= 0) continue;
                int max = Progression.Preview(c, h).MaxHp;
                int missing = max - h.Hp;
                if (missing <= 0) { h.Hp = 0; continue; }
                int heal = Math.Min(missing, (int)Math.Ceiling(missing * share));
                healed += heal;
                h.Hp += heal;
                if (h.Hp >= max) h.Hp = 0;
            }
            return healed;
        }
    }

    /// <summary>GG-T1785: camp rest, which replaces the Inn's gold heal. Only at camp: the party eats the meal if Food allows
    /// (<see cref="CampRest.Eat"/>) and sleeps to morning (<see cref="CampRest.Sleep"/>); the whole roster wakes at full HP for
    /// free. Well Rested only comes with the meal and a full night's sleep.</summary>
    public static class CampRestService
    {
        public sealed class Outcome
        {
            public bool Ate;
            public bool WellRested;
        }

        public static Result Rest(SaveData s, out Outcome outcome, WorldTimeConfig time = null, CampRestConfig config = null)
        {
            outcome = new Outcome();
            if (s?.World == null || !s.World.Camped) return Result.Fail("On the road: set up camp to rest");
            var night = WorldSaveAdapter.Night(s.World);
            var clock = WorldSaveAdapter.Clock(s.World, time);
            outcome.Ate = night.AteMeal || CampRest.Eat(night, s.Party.Count, config);
            outcome.WellRested = CampRest.Sleep(night, clock, config);
            WorldSaveAdapter.Store(s.World, night);
            WorldSaveAdapter.Store(s.World, clock);
            foreach (var h in s.Roster) h.Hp = 0;
            return Result.Success(outcome.WellRested ? "The party wakes Well Rested" : "The party is fully rested");
        }
    }
}
