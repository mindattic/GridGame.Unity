using System;
using System.Linq;

namespace GridGame.App.Automation
{
    /// <summary>Screen ids → factories, for `-start <id>` and the automation `goto` endpoint (snapshot sweeps).</summary>
    public static class StartScreens
    {
        public static readonly string[] All =
        {
            "title", "hub", "stages", "items", "status", "equip", "abilities", "talents", "respec", "party", "shop", "blacksmith",
            "alchemist", "enchantress", "talk", "summon", "caravan", "bounties", "crucible", "bestiary", "settings", "windows", "credits", "newgame", "load",
            "profiles", "newprofile", "createprofile", "splash", "diamonds",
        };

        public static bool TryGet(string id, out GameScreen screen)
        {
            screen = null;
            var app = GameApp.I;
            bool needsSave = id != "title" && id != "credits" && id != "about" && id != "newgame" && id != "load" && id != "profiles" && id != "newprofile" && id != "createprofile" && id != "splash";
            if (needsSave && app.Save == null)
            {
                app.Save = Meta.Progression.NewGame(app.Content, "Preview");
                Meta.CampNpcs.JoinAll(app.Save); // the sweep shows every Camp station (GG-T119)
                app.Slot = -1;
            }
            string hero = app.Save?.Party.FirstOrDefault();
            switch (id)
            {
                case "title": screen = new TitleScreen(); break;
                case "hub": screen = new HubScreen(); break;
                case "windows": screen = new WindowEditorScreen(); break; // GG-T407
                case "stages": screen = new StageSelectScreen(); break;
                case "items": screen = new ItemsScreen(); break;
                case "status": screen = new StatusScreen(hero); break;
                case "equip": screen = new EquipScreen(hero); break;
                case "abilities": screen = new AbilitiesScreen(hero); break;
                case "talents": screen = new SkillTreeScreen(hero); break;
                case "respec": screen = new RespecScreen(); break;
                case "party": screen = new PartyScreen(); break;
                case "shop": screen = new ShopScreen(); break;
                case "blacksmith": screen = new BlacksmithScreen(); break;
                case "alchemist": screen = new AlchemistScreen(); break;
                case "enchantress": screen = new EnchantressScreen(); break;
                case "talk": screen = new TalkScreen(); break;
                case "summon": screen = new SummonScreen(); break;
                case "summon-history": screen = new SummonHistoryScreen(); break; // GG-T1430
                case "diamonds": screen = new DiamondStoreScreen(); break; // GG-T455
                case "caravan": screen = new CaravanScreen(); break; // GG-T448: missing here, so goto/_goto("caravan") always "unknown screen" -- no Caravan purchase could ever be driven through automation
                case "mercenaries": screen = new MercenaryScreen(); break;
                case "bounties": screen = new BountyScreen(); break;
                case "quests": screen = new QuestScreen(); break;
                case "crucible": screen = new CrucibleScreen(); break;
                case "bestiary": screen = new BestiaryScreen(); break;
                case "settings": screen = new SettingsScreen(); break;
                case "credits": screen = new CreditsScreen(); break;
                case "about": screen = new AboutScreen(); break; // GG-T1330
                case "newgame": screen = new SaveSlotScreen(true); break;
                case "load": screen = new SaveSlotScreen(false); break;
                case "profiles": screen = new ProfileSelectScreen(); break; // GG-T99
                case "newprofile": screen = new ProfileNameScreen(null); break;
                case "createprofile": screen = new CreateProfileScreen(); break; // GG-T1625
                // GG-T1408: `-start splash` boots the way a player's launch does (automation otherwise opens on the Title):
                // the logo, then Create Profile when the profile's first run is due (a fresh -saveRoot), else the Main Menu.
                case "splash": screen = new SplashScreen(); break;
                // GG-T513/T514: a results screen without fighting for it. "results" ends an expedition with a long
                // first-hour haul (a Tome among ~15 drops, a theme milestone, the completion bonus); "results-mid" is a
                // battle won mid-expedition (To Camp / Retreat early / Next battle).
                case "results": screen = new PostBattleScreen(PreviewReport(app, false), PreviewStage(app)); break;
                case "results-mid": screen = new PostBattleScreen(PreviewReport(app, true), PreviewStage(app)); break;
            }
            return screen != null;
        }

        private static string PreviewStage(GameApp app) => app.Content.CampaignStages()[0].Id;

        private static Meta.BattleReport PreviewReport(GameApp app, bool mid)
        {
            var c = app.Content;
            var items = c.Items.Values;
            var theme = c.ThemeOf(PreviewStage(app));
            var r = new Meta.BattleReport
            {
                Victory = true, StageId = PreviewStage(app), Gold = 214, XpEach = 38,
                ExpeditionName = theme?.Name ?? "Preview", ExpeditionLength = theme?.Stages.Count ?? 3,
            };
            if (mid) { r.ExpeditionStep = 1; r.CarriedOrbs = 4; r.NextStageId = theme?.Stages.Count > 1 ? theme.Stages[1] : null; }
            else
            {
                r.ExpeditionStep = r.ExpeditionLength; r.ExpeditionComplete = true; r.ExpeditionBonus = 342;
                r.Milestone = theme?.Name ?? "Preview";
                var milestoneItem = items.FirstOrDefault(i => i.Type == Core.Content.ItemType.Consumable);
                r.MilestoneItem = milestoneItem?.Id;
            }
            var tome = items.FirstOrDefault(i => i.Type == Core.Content.ItemType.Tome);
            foreach (var it in items.Where(i => i.Type == Core.Content.ItemType.CraftingMaterial).Take(14)) r.Items[it.Id] = 2;
            if (tome != null) { r.Items[tome.Id] = 1; app.Save.AddItem(tome.Id, 1); }
            return r;
        }
    }
}
