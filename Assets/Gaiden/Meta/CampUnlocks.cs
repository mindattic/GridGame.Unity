using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Which Camp stations are open, and if not, when they will be (GG-T174, bible §25.0.3). GG-T537 (owner, 2026-11-21):
    /// every station arrives on a story beat of the campaign, clearing a stage, one new system at a time, never on an
    /// incidental trigger (worn gear, money, loot). Keeper stations open when their NPC joins at that beat (a StageCleared
    /// dialogue with a JoinNpc effect, §25.0.1); the keeperless ones open on the clear of their stage, and their beat (an
    /// OpenStation effect) is the story moment. A station the save has already used never locks again, and bot-played saves
    /// see everything.
    /// </summary>
    public static class CampUnlocks
    {
        /// <summary>Campaign stages that must be cleared before each keeperless station opens (GG-T537): the Summon Circle
        /// (and the Diamond Store) after The Lantern Road II, Bounties (and the Mercenaries) after The Lantern Road III, the
        /// Crucible after The Shardfall I, the Caravan after The Arkgrave II. Each has an OpenStation beat on that stage
        /// (dialogues.json), which <c>CampUnlocksTests</c> holds to these numbers.</summary>
        public const int SummonAfter = 5, BountiesAfter = 6, CrucibleAfter = 7, CaravanAfter = 11;

        /// <summary>The OpenStation ids (<see cref="DialogueDef.Stations"/>).</summary>
        public const string Summon = "summon", Bounties = "bounties", Crucible = "crucible", Caravan = "caravan";

        private static int Cleared(SaveData s) => s.HighestClearedStage + 1;
        /// <summary>A save that has already graduated the whole onboarding chain: Test Mode, New Game+, or every Camp
        /// NPC already joined (the v4 migration's "they'd been using every service already" — <see
        /// cref="SaveMigrations.Migrate"/> — and the balance sim's no-story yardstick, GG-T199). Such a save never saw the
        /// milestone locks go up in the first place, so it doesn't see them now either.</summary>
        private static bool Veteran(SaveData s) => s.TestMode || s.NewGamePlus > 0 || CampNpcs.All.All(n => CampNpcs.Joined(s, n));

        public static bool NpcOpen(SaveData s, string npc) => s != null && (CampNpcs.Joined(s, npc) || s.TestMode);

        public static bool BountiesOpen(SaveData s) => s != null && (Veteran(s) || Cleared(s) >= BountiesAfter || (s.Bounty != null && (s.Bounty.Used || s.Bounty.Taken.Count > 0 || s.Bounty.Claimed.Count > 0)));
        public static bool SummonOpen(SaveData s) => s != null && (Veteran(s) || Cleared(s) >= SummonAfter || s.Recruits > 0);
        public static bool CrucibleOpen(SaveData s) => s != null && (Veteran(s) || Cleared(s) >= CrucibleAfter || (s.Crucible?.Runs ?? 0) > 0);
        // GG-T432: a facility level is bought at its own station, so building one doesn't open the Caravan row early.
        public static bool CaravanOpen(SaveData s) => s != null && (Veteran(s) || Cleared(s) >= CaravanAfter || (s.Caravan?.Where(kv => !Facilities.IsFacilityId(kv.Key)).Sum(kv => kv.Value) ?? 0) > 0);
        /// <summary>GG-T537: the Diamond Store's Camp row opens with the Summon Circle (a Diamond's first use is a summon), or
        /// once the save has bought anything with real money. A paywall prompt short of Diamonds still offers the store.</summary>
        public static bool DiamondStoreOpen(SaveData s) => s != null && (SummonOpen(s) || (s.Purchases?.Granted?.Count ?? 0) > 0);

        /// <summary>The Blacksmith's Forge, Upgrade and Salvage tabs (GG-T175, §25.0.4). He joins to mend worn gear, so
        /// Repair is his only tab at first; the workshop opens a step later in the onboarding chain — once the Alchemist
        /// has joined (GG-T177 hands it the first forge). Old saves joined everyone (v4 migration), so they keep it.</summary>
        public static bool SmithWorkshopOpen(SaveData s) => s != null && (Veteran(s) || CampNpcs.Joined(s, CampNpcs.Alchemist));

        /// <summary>The locked workshop tab's hint: "Opens when the Alchemist joins, after The Lantern Road I".</summary>
        public static string SmithWorkshopHint(ContentDb c)
        {
            var stage = c?.TryStage(JoinStage(c, CampNpcs.Alchemist));
            return "Opens when the Alchemist joins" + (stage != null ? ", after " + stage.Name : "");
        }

        /// <summary>"Opens after &lt;stage&gt;": the stage whose clear opens a keeperless station.</summary>
        public static string MilestoneHint(ContentDb c, int clears)
        {
            var stages = c.CampaignStages();
            if (clears <= 0 || stages.Count == 0) return "";
            return "Opens after " + stages[System.Math.Min(clears, stages.Count) - 1].Name;
        }

        /// <summary>A keeperless station's Camp row name ("summon" → "Summon Circle").</summary>
        public static string StationName(string station)
        {
            switch (station)
            {
                case Summon: return "Summon Circle";
                case Bounties: return "Bounties";
                case Crucible: return "Crucible";
                case Caravan: return "Caravan";
                default: return DialogueRunner.Npc(station);
            }
        }

        /// <summary>GG-T537: the story beat that recruits <paramref name="npc"/> (a StageCleared dialogue with its JoinNpc), or null.</summary>
        public static DialogueDef JoinBeat(ContentDb c, string npc) =>
            c?.DialogueOrder.FirstOrDefault(d => DialogueRunner.IsVendorBeat(d) && d.AllEffects().Any(e => e?.Type == DialogueEffectType.JoinNpc && e.Id == npc));

        /// <summary>The stage whose clear brings <paramref name="npc"/> (its join beat's stage), or null.</summary>
        public static string JoinStage(ContentDb c, string npc) => JoinBeat(c, npc)?.Trigger.Id;

        /// <summary>"Joins after The Tithe Moors III": read from the story beat whose JoinNpc recruits this keeper, so the
        /// hint follows the content.</summary>
        public static string JoinHint(ContentDb c, string npc)
        {
            var stage = c.TryStage(JoinStage(c, npc));
            return stage == null ? "Joins later on the road" : "Joins after " + stage.Name;
        }

        /// <summary>GG-T537, the results screen's unlock lines: who joins and what opens at Camp because of this clear of
        /// <paramref name="stageId"/> ("Blacksmith joins the caravan", "Summon Circle opens at Camp"), from the vendor beats
        /// on that stage the save hasn't played yet. None for a bot-played save, which never sees the locks.</summary>
        public static List<string> Arrivals(ContentDb c, SaveData s, string stageId)
        {
            var list = new List<string>();
            if (c == null || s == null || stageId == null || Veteran(s)) return list;
            foreach (var d in c.DialogueOrder)
            {
                if (!DialogueRunner.IsVendorBeat(d) || s.SeenDialogues.Contains(d.Id)
                    || !string.Equals(d.Trigger.Id, stageId, System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var e in d.AllEffects().Where(e => e != null).GroupBy(e => (e.Type, e.Id)).Select(g => g.First()))
                {
                    if (e.Type == DialogueEffectType.JoinNpc && !CampNpcs.Joined(s, e.Id)) list.Add($"{DialogueRunner.Npc(e.Id)} joins the caravan");
                    else if (e.Type == DialogueEffectType.OpenStation) list.Add($"{StationName(e.Id)} opens at Camp");
                }
            }
            return list;
        }
    }
}
