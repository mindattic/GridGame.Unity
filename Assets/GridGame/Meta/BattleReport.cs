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
    // Battle bridge: save → BattleSetup, BattleResults → save (§22.1)
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class BattleReport
    {
        public bool Victory;
        public bool Escaped;
        /// <summary>A voluntary pause-menu Retreat (FFIV-style partial loss, §22.2) — distinct from Defeat.</summary>
        public bool Retreated;
        public string StageId;
        /// <summary>The stage's name and theme as fought, copied at commit so the results flow never has to look the
        /// stage up again: a generated stage (a harness skirmish, a Crucible run) can be pruned from the ContentDb
        /// while the level-up and results screens are still showing it (GG-T127).</summary>
        public string StageName;
        public string StageTheme;
        public int Gold;
        public int XpEach;
        /// <summary>The difficulty dial this battle was fought at (GG-T309; 0 = unscaled), so the results can name its reward multiplier.</summary>
        public int Difficulty;
        public Dictionary<string, int> Items = new Dictionary<string, int>();
        public List<(string classId, int fromLevel, int toLevel)> LevelUps = new List<(string, int, int)>();
        public bool FirstClear;
        public string UnlockedStage;
        public int NewSlots;
        public string BountyProgress;
        /// <summary>Side quests this victory moved (§22.11, GG-T237), one line each.</summary>
        public List<string> QuestProgress = new List<string>();
        /// <summary>Loot past an item's stack limit is sold on the spot (inventory policy, US-245).</summary>
        public int AutoSold;
        /// <summary>Aetherite Dust this victory paid (GG-T623), on top of <see cref="Gold"/>; 0 on anything short of victory.</summary>
        public int DustEarned;
        /// <summary>Summon Tickets this battle earned (a first clear, a chapter's end, every fifth win; GG-T1607); 0 for none.</summary>
        public int TicketsEarned;
        /// <summary>GG-T1927 (§3.1.0): the party mana pool's points when the battle ended (GG-T676 settles them at the end of
        /// a won stage).</summary>
        public int ManaLeft;
        /// <summary>GG-T676: the Mana Dust <see cref="ManaLeft"/> settled back into at the end of a won stage
        /// (<see cref="DustEconomy.SettleMana"/>, already counted in <see cref="DustEarned"/>); 0 when the mana carried on
        /// along the road or the stage was lost.</summary>
        public int OrbDust;
        /// <summary>GG-T1811: what the former Mana Orb sources paid this victory, one line each (milestone Shards, a Halcyon
        /// find's Dust; <see cref="ManaOrbs.AfterVictory"/>).</summary>
        public List<string> OrbRewards = new List<string>();
        /// <summary>§25.20 (GG-T1573): Trust and cohesion this victory moved, before and after; empty short of victory.</summary>
        public BattleBonds BondChanges = new BattleBonds();
        public bool CampaignComplete;
        /// <summary>Campaign milestone (GG-T301): the theme whose first full clear this was, and the reward it paid (also in <see cref="Items"/>).</summary>
        public string Milestone;
        public string MilestoneItem;
        // Travel (§28, GG-T1786: replaces the expedition fields)
        /// <summary>GG-T1790, GG-T1927: mana points carried into the next battle on the road (<see cref="TravelRules.AfterBattle"/>);
        /// 0 at camp.</summary>
        public int CarriedMana;
        /// <summary>GG-T1790: HP the Medic Wagon patched up after this win on the road (<see cref="MedicWagon"/>).</summary>
        public int MedicHealed;
        // Guests (§25.11)
        public List<string> GuestsJoined = new List<string>();
        /// <summary>Tamed monsters standing at this victory with a full bond (GG-T303): each asks to join — the results
        /// screen offers <see cref="Domestication.Accept"/> or a decline.</summary>
        public List<Domestication.Offer> TameOffers = new List<Domestication.Offer>();
        /// <summary>Guests down at battle's end — not lost, they'll be met again (GG-T114).</summary>
        public List<string> GuestsFell = new List<string>();
        /// <summary>Guests lost for good — only ever filled with <see cref="Permadeath"/> on (never in the normal game).</summary>
        public List<string> GuestsLost = new List<string>();
        /// <summary>The wanderer this battle held (GG-T342), whatever became of them; null = none.</summary>
        public string Wanderer;
        /// <summary>The wanderer standing at this victory, waiting to talk (the results screen's conversation); null = none.</summary>
        public Wanderers.Offer WandererOffer;
        // Crucible (§22.10)
        public bool Crucible;
        /// <summary>Waves fully cleared before the party fell or left.</summary>
        public int CrucibleWaves;
        /// <summary>The save's best waves-survived after this run, in this run's turn mode (GG-T945).</summary>
        public int CrucibleBest;
        public bool CrucibleNewBest;
        /// <summary>The save's best haul after this run, in this run's turn mode (GG-T945).</summary>
        public int CrucibleBestGold;
        /// <summary>The turn mode this run's record is kept under (GG-T945, <see cref="TurnRules.RecordMode"/>).</summary>
        public TurnMode CrucibleMode;
        /// <summary>GG-T1626: a tutorial drill (<see cref="Drills"/>): nothing earned, spent or worn.</summary>
        public bool Drill;
        /// <summary>GG-T1647: a drill replayed from Settings for practice (<see cref="Drills.Report"/>): nothing was written.</summary>
        public bool Practice;
        /// <summary>The battle's best combo window (§1.2.8, GG-T852): its rank (None when no combo ran) and score.</summary>
        public ComboRank BestComboRank;
        public int BestComboScore;
        /// <summary>The hero class with the most combo points earned as a supporter this battle (null when none), and its points.</summary>
        public string TopSupporter;
        public int TopSupporterPoints;
        /// <summary>GG-T1828: each hero class's combo points this battle, earned attacking and supporting.</summary>
        public Dictionary<string, int> ComboAttackerPoints = new Dictionary<string, int>();
        public Dictionary<string, int> ComboSupporterPoints = new Dictionary<string, int>();
        /// <summary>GG-T944 (§2.13.1): the party turns the battle took (<see cref="BattleResults.PartyTurns"/>) and the
        /// commands it took (<see cref="BattleResults.ActionsTaken"/>, Guards included).</summary>
        public int PartyTurns;
        public int Actions;
        /// <summary>GG-T1095 (§2.13.2): rounds played (<see cref="BattleResults.Rounds"/>); 0 outside UnmovedThisRound.</summary>
        public int Rounds;
        /// <summary>GG-T1111 (§2.13.3): hero turns taken (<see cref="BattleResults.HeroTurns"/>); 0 outside HeroTimeline.</summary>
        public int HeroTurns;

        /// <summary>The results row's value: "N turns", or "N turns, M actions" when several heroes acted in a turn
        /// (the two differ), then ", R rounds" when the battle kept rounds. Under HeroTimeline (GG-T1111) it reads
        /// "N hero turns" (", M actions" only when the two differ). Empty when no turn was played.</summary>
        public string TurnsLabel()
        {
            if (HeroTurns > 0)
            {
                string heroTurns = HeroTurns == 1 ? "1 hero turn" : $"{HeroTurns} hero turns";
                if (Actions != HeroTurns) heroTurns += Actions == 1 ? ", 1 action" : $", {Actions} actions";
                return heroTurns;
            }
            if (PartyTurns <= 0 && Actions <= 0) return "";
            string turns = PartyTurns == 1 ? "1 turn" : $"{PartyTurns} turns";
            if (Actions != PartyTurns) turns += Actions == 1 ? ", 1 action" : $", {Actions} actions";
            if (Rounds > 0) turns += Rounds == 1 ? ", 1 round" : $", {Rounds} rounds";
            return turns;
        }
    }
}
