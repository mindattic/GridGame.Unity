using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>One tab of the hub's bottom nav (GG-T1338): whether it opens, why not, and its badge text.</summary>
    public sealed class NavTab
    {
        public string Id;
        public string Label;
        /// <summary>False: dimmed with a padlock; a tap shows <see cref="Hint"/> and goes nowhere.</summary>
        public bool Open = true;
        public string Hint = "";
        /// <summary>The badge's text ("3", "!"), empty for no badge.</summary>
        public string Badge = "";
    }

    /// <summary>
    /// The bottom nav's five tabs (GG-T1338, docs/design/hub-navigation): Home, Campaign, Heroes, Summon, Shop, read off
    /// <see cref="CampMenu"/> so a tab locks, hints and badges exactly as its Camp destination does. Engine-free: the App
    /// layer maps <see cref="NavTab.Id"/> to a screen and an icon. Summon follows <see cref="CampUnlocks.SummonOpen"/>;
    /// Shop opens when the Merchant joins or the Shard Store opens, whichever comes first.
    /// </summary>
    public static class NavBadges
    {
        public const string Home = "home";

        public static List<NavTab> Tabs(ContentDb c, SaveData s, SettingsSave settings)
        {
            var menu = CampMenu.Build(c, s, settings);
            return new List<NavTab>
            {
                new NavTab { Id = Home, Label = "Home", Badge = HomeBadge(menu) },
                new NavTab { Id = CampMenu.Campaign, Label = "Campaign", Badge = CampaignBadge(c, s) },
                new NavTab { Id = CampMenu.Heroes, Label = "Heroes", Badge = HeroesBadge(c, s) },
                Group(menu, CampMenu.Summon, "Summon", CampGroup.Summon, SummonBadge(c, s)),
                Group(menu, CampMenu.Shop, "Shop", CampGroup.Shop, ShopBadge(c, s)),
            };
        }

        private static NavTab Group(List<CampDestination> menu, string id, string label, CampGroup group, string badge)
        {
            bool open = CampMenu.GroupOpen(menu, group);
            return new NavTab { Id = id, Label = label, Open = open, Hint = CampMenu.GroupHint(menu, group), Badge = open ? badge : "" };
        }

        /// <summary>Something waits in a Camp group the bar has no tab for (Craft, Journal, System): a quest to hand in, a
        /// bounty to collect, a new talk, unread Codex pages.</summary>
        private static string HomeBadge(List<CampDestination> menu) =>
            menu.Any(d => d.Badge && d.Group != CampGroup.Heroes && d.Group != CampGroup.Summon && d.Group != CampGroup.Shop && d.Group != CampGroup.Campaign) ? "!" : "";

        /// <summary>"!" while onboarding's train-a-skill step is ahead or a party hero has a new ability ready
        /// (<see cref="NextAbility.ReadyInParty"/>, GG-T1407), else the party's unspent skill points.</summary>
        private static string HeroesBadge(ContentDb c, SaveData s)
        {
            if (Training.Step(s) || NextAbility.ReadyInParty(c, s)) return "!";
            int points = s.Party.Select(s.Hero).Where(h => h != null).Sum(h => h.SkillPoints);
            return points > 0 ? points.ToString() : "";
        }

        // Hooks for later features (a free daily summon, new stock, a new stage); nothing to say yet.
        private static string CampaignBadge(ContentDb c, SaveData s) => "";
        private static string SummonBadge(ContentDb c, SaveData s) => "";
        private static string ShopBadge(ContentDb c, SaveData s) => "";
    }
}
