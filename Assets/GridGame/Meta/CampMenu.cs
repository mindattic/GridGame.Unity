using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>Where a Camp destination sits on the hub (GG-T1346, docs/design/hub-navigation): the two pillars, the Camp
    /// tiles that open a screen directly, the Craft and Journal group menus, and the System rows.</summary>
    public enum CampGroup { Campaign, Crucible, Heroes, Summon, Shop, Craft, Journal, System }

    /// <summary>One Camp destination: what the row or tile reads, whether it opens, why not, and what waits there.</summary>
    public sealed class CampDestination
    {
        /// <summary>Stable id, the hub-navigation design's screen id ("blacksmith", "codex", "quit").</summary>
        public string Id;
        public string Label;
        public CampGroup Group;
        /// <summary>False: shown dimmed with <see cref="Hint"/> and not tappable.</summary>
        public bool Open;
        /// <summary>Why a closed destination is closed ("Joins after The Tithe Moors III", "Closed on an expedition").</summary>
        public string Hint = "";
        /// <summary>The row's value text ("3 new", "2 pts", "Stage 4"), empty when there's nothing to say.</summary>
        public string Value = "";
        /// <summary>Something is waiting there (skill points, a bounty to collect, a quest to hand in, unread Codex pages):
        /// the tile or group carries a badge.</summary>
        public bool Badge;
        /// <summary>The keeper whose face leads the row (a <see cref="CampNpcs"/> id), or null.</summary>
        public string Npc;
    }

    /// <summary>
    /// The Camp hub's destinations as one list (GG-T1346), lifted out of HubScreen.Build's inline rows so the hub root,
    /// its group menus and the tests read the same open/locked state, unlock hints and badges. Engine-free: the App
    /// layer maps <see cref="CampDestination.Id"/> to a screen and an icon. Release-scope switches that are off
    /// (<see cref="Features"/>) leave their destination out; everything else is listed, open or locked, in hub order.
    /// </summary>
    public static class CampMenu
    {
        public const string Campaign = "campaign", Crucible = "crucible", Heroes = "heroes", Items = "items", Inn = "inn",
            Respec = "respec", Summon = "summon", Mercenaries = "mercenaries", Shop = "shop", Diamonds = "diamonds",
            Blacksmith = "blacksmith", Alchemist = "alchemist", Enchantress = "enchantress", Aetherist = "aetherist",
            Caravan = "caravan", Talk = "talk", Quests = "quests", Bounties = "bounties", Bestiary = "bestiary",
            Codex = "codex", Settings = "settings", NewGamePlus = "newgameplus", Quit = "quit";

        public static List<CampDestination> Build(ContentDb c, SaveData s, SettingsSave settings)
        {
            var list = new List<CampDestination>();
            CampDestination Add(string id, string label, CampGroup group, bool open = true, string hint = "", string value = "", bool badge = false, string npc = null)
            {
                var d = new CampDestination { Id = id, Label = label, Group = group, Open = open, Hint = open ? "" : hint ?? "", Value = value ?? "", Badge = open && badge, Npc = npc };
                list.Add(d);
                return d;
            }
            void Keeper(string id, string label, CampGroup group, string npc) =>
                Add(id, label, group, CampUnlocks.NpcOpen(s, npc), CampUnlocks.JoinHint(c, npc), npc: npc);

            Add(Campaign, "Adventure", CampGroup.Campaign, value: $"Stage {Math.Min(s.HighestClearedStage + 2, c.CampaignStages().Count)}");
            if (Features.Crucible)
            {
                if (CampUnlocks.CrucibleOpen(s))
                {
                    bool canLaunch = GridGame.Meta.Crucible.CanLaunch(c, s, out var why);
                    Add(Crucible, "Crucible", CampGroup.Crucible, canLaunch, why, canLaunch ? CrucibleRecord(s, TurnModeSetting.Resolve(s, settings)) : "");
                }
                else Add(Crucible, "Crucible", CampGroup.Crucible, false, CampUnlocks.MilestoneHint(c, CampUnlocks.CrucibleAfter));
            }

            int partyPoints = s.Party.Select(s.Hero).Where(h => h != null).Sum(h => h.SkillPoints);
            bool training = Training.Step(s);
            Add(Heroes, "Heroes", CampGroup.Heroes, value: training ? "Train a skill!" : partyPoints > 0 ? $"{partyPoints} pts" : "", badge: training || partyPoints > 0);
            if (CampUnlocks.NpcOpen(s, CampNpcs.Innkeeper))
            {
                bool rest = Expeditions.InnOpen(s);
                int cost = GridGame.Meta.Inn.Cost(c, s);
                Add(Inn, s.Expedition != null && rest ? "Inn (Medic Wagon)" : "Inn", CampGroup.Heroes, rest, "On the road", rest ? (cost > 0 ? Gold(cost) : "Rested") : "", npc: CampNpcs.Innkeeper);
                if (Features.InnRespecRow && rest) Add(Respec, "Inn: Respec", CampGroup.Heroes, npc: CampNpcs.Innkeeper);
            }
            else Keeper(Inn, "Inn", CampGroup.Heroes, CampNpcs.Innkeeper);

            Add(Summon, "Summon Circle", CampGroup.Summon, CampUnlocks.SummonOpen(s), CampUnlocks.MilestoneHint(c, CampUnlocks.SummonAfter));

            Keeper(Shop, "Merchant", CampGroup.Shop, CampNpcs.Merchant);
            if (Features.DiamondStore)
                Add(Diamonds, ShardText.Store, CampGroup.Shop, CampUnlocks.DiamondStoreOpen(s), CampUnlocks.MilestoneHint(c, CampUnlocks.SummonAfter),
                    $"{Count(DiamondExchange.Have(s))} {ShardText.ShortPlural}");

            Add(Items, "Items", CampGroup.Craft);
            Keeper(Blacksmith, "Blacksmith", CampGroup.Craft, CampNpcs.Blacksmith);
            Keeper(Alchemist, "Alchemist", CampGroup.Craft, CampNpcs.Alchemist);
            Keeper(Aetherist, "Manasmith", CampGroup.Craft, CampNpcs.Aetherist);
            if (Features.Enchanter) Keeper(Enchantress, "Enchantress", CampGroup.Craft, CampNpcs.Enchantress);
            if (Features.Caravan)
            {
                int levels = c.Caravan.Keys.Sum(k => CaravanUpgrades.Level(s, k));
                Add(Caravan, "Caravan", CampGroup.Craft, CampUnlocks.CaravanOpen(s), CampUnlocks.MilestoneHint(c, CampUnlocks.CaravanAfter), levels > 0 ? $"{levels} upgrades" : "");
            }

            if (c.QuestOrder.Any(q => GridGame.Meta.Quests.State(c, s, q) != QuestState.Locked))
            {
                int ready = GridGame.Meta.Quests.ReadyCount(c, s);
                Add(Quests, "Quests", CampGroup.Journal, value: ready > 0 ? $"! {ready} to hand in" : $"{s.Quests.Active.Count} under way", badge: ready > 0);
            }
            if (Features.Bounties)
                Add(Bounties, "Bounties", CampGroup.Journal, CampUnlocks.BountiesOpen(s), CampUnlocks.MilestoneHint(c, CampUnlocks.BountiesAfter),
                    s.Bounty.Slain.Count > 0 ? $"☠ {s.Bounty.Slain.Count} to collect" : s.Bounty.Taken.Count > 0 ? $"Hunting {s.Bounty.Taken.Count}" : "", s.Bounty.Slain.Count > 0);
            Add(Mercenaries, "Mercenaries", CampGroup.Journal, GridGame.Meta.Mercenaries.Open(s), CampUnlocks.MilestoneHint(c, CampUnlocks.BountiesAfter),
                s.Mercenaries.Contracts.Count > 0 ? $"{s.Mercenaries.Contracts.Count} hired" : "");
            int newTalks = DialogueRunner.TalkPartners(s).Count(p => { var t = DialogueRunner.TalkTopic(c, s, p); return t != null && !s.SeenDialogues.Contains(t.Id); });
            Add(Talk, "Talk", CampGroup.Journal, value: newTalks > 0 ? $"{newTalks} new" : "", badge: newTalks > 0);
            int codexNew = GridGame.Meta.Codex.UnreadCount(c, s);
            Add(Codex, "Codex", CampGroup.Journal, value: codexNew > 0 ? $"{codexNew} new" : "", badge: codexNew > 0);
            Add(Bestiary, "Bestiary", CampGroup.Journal);

            Add(Settings, "Settings", CampGroup.System);
            if (Progression.CanStartNewGamePlus(c, s)) Add(NewGamePlus, $"New Game+ {s.NewGamePlus + 1}", CampGroup.System);
            Add(Quit, "Save & Quit", CampGroup.System);
            return list;
        }

        /// <summary>The destinations in <paramref name="group"/>, in hub order.</summary>
        public static List<CampDestination> In(IEnumerable<CampDestination> menu, CampGroup group) => menu.Where(d => d.Group == group).ToList();

        /// <summary>A group's tile: open if any destination in it is open, else dimmed with its first hint; badged if any
        /// open destination is.</summary>
        public static bool GroupOpen(IEnumerable<CampDestination> menu, CampGroup group) => menu.Any(d => d.Group == group && d.Open);
        public static string GroupHint(IEnumerable<CampDestination> menu, CampGroup group) =>
            GroupOpen(menu, group) ? "" : menu.FirstOrDefault(d => d.Group == group && !string.IsNullOrEmpty(d.Hint))?.Hint ?? "";
        public static bool GroupBadge(IEnumerable<CampDestination> menu, CampGroup group) => menu.Any(d => d.Group == group && d.Badge);

        /// <summary>The Crucible row's record: "Endless waves" before the first run, then "Best: 12 waves" for this turn mode.</summary>
        public static string CrucibleRecord(SaveData s, Core.Battle.TurnMode mode)
        {
            if (s.Crucible == null || s.Crucible.Runs == 0) return "Endless waves";
            int best = s.Crucible.BestFor(mode);
            return $"Best: {best} wave{(best == 1 ? "" : "s")}";
        }

        private static string Count(int n) => n.ToString("N0", CultureInfo.InvariantCulture);
        private static string Gold(int g) => Count(g) + " G";
    }
}
