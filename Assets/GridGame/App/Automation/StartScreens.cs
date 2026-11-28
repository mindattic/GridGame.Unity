using System;
using System.Collections;
using System.Linq;
using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App.Automation
{
    /// <summary>Screen ids → factories, for `-start <id>` and the automation `goto` endpoint (snapshot sweeps).</summary>
    public static class StartScreens
    {
        public static readonly string[] All =
        {
            "title", "hub", "stages", "items", "status", "equip", "abilities", "talents", "respec", "party", "shop", "blacksmith",
            "alchemist", "enchantress", "talk", "summon", "caravan", "bounties", "crucible", "bestiary", "settings", "windows", "credits", "newgame", "load",
            "profiles", "newprofile", "createprofile", "splash", "diamonds", "mercenaries", "summon-history", "quests", "about", "components", "drill-picker",
            "codex",
        };

        /// <summary>Dev-only targets: a debug build, or a release player who unlocked Settings > Developer.</summary>
        private static bool DevGoto => CommandLine.DebugOptionsAllowed || Dev.DevOptions.Unlocked;

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
                case "codex": screen = new CodexScreen(); break; // GG-T2147: Camp > Codex, a tall scrolling page for uiaudit
                case "settings": screen = new SettingsScreen(); break;
                case "credits": screen = new CreditsScreen(); break;
                case "about": screen = new AboutScreen(); break; // GG-T1330
                // GG-T2049: the dev-only kit gallery; a release player reaches it only after unlocking Settings > Developer.
                case "components": if (DevGoto) screen = new ComponentGalleryScreen(); break;
                case "drill-picker": screen = new DrillPickerScreen(); break; // GG-T2152: Settings > Play a drill
                case "newgame": screen = new SaveSlotScreen(true); break;
                case "load": screen = new SaveSlotScreen(false); break;
                case "profiles": screen = new ProfileSelectScreen(); break; // GG-T99
                case "newprofile": screen = new ProfileNameScreen(null); break;
                case "createprofile": screen = new CreateProfileScreen(); break; // GG-T1625
                // GG-T1408: `-start splash` boots the way a player's launch does (automation otherwise opens on the Title):
                // the logo, then Create Profile when the profile's first run is due (a fresh -saveRoot), else the Main Menu.
                case "splash": screen = new SplashScreen(); break;
                // GG-T513/T514: a results screen without fighting for it. "results" is a win at camp with a long
                // first-hour haul (a Tome among ~15 drops, a theme milestone); "results-mid" is a battle won on the road
                // (GG-T1786: orbs carried, the Medic Wagon, To Camp / Next battle).
                case "results": screen = new PostBattleScreen(PreviewReport(app, false), PreviewStage(app)); break;
                case "results-mid": screen = new PostBattleScreen(PreviewReport(app, true), PreviewStage(app)); break;
                // GG-T2091: the other end-of-battle cards for the audit: a defeat with its last hits, a prologue battle's
                // drill card, and the level-up fanfare (three heroes, so it is still playing when the capture lands).
                case "results-defeat": screen = new PostBattleScreen(PreviewDefeat(app), PreviewStage(app)); break;
                case "results-drill": screen = PreviewDrill(app); break;
                case "levelup": screen = new LevelUpScreen(PreviewLevelUps(app), PreviewStage(app)); break;
                // GG-T2150: the kit's overlays for the audit, each opened over its host once the router's fade is done and
                // taken away when the host leaves: the dialogue box with its first line typed out, the coach card, the
                // Inspect card, a confirm and a toast.
                case "overlay-dialogue": if (DevGoto) screen = PreviewDialogue(app); break;
                case "overlay-coach": if (DevGoto) screen = OverOn(new BlacksmithScreen(), PreviewCoach); break;
                case "overlay-inspect": if (DevGoto) screen = OverOn(new StatusScreen(hero), s => PreviewInspect(app, s, hero)); break;
                case "overlay-confirm": if (DevGoto) screen = OverOn(new HubScreen(), _ => Confirm.Show("Return to the title? Your progress is saved.", "Title", null)); break;
                case "overlay-toast": if (DevGoto) screen = OverOn(new HubScreen(), _ => Ui.Toast("The Merchant restocks tomorrow", ToastSeconds)); break;
            }
            return screen != null;
        }

        /// <summary>Long enough for the audit's settle and shot; the host leaving takes it away sooner.</summary>
        private const float ToastSeconds = 20f;

        private static GameScreen OverOn(GameScreen host, Action<GameScreen> open)
        {
            GameApp.I.StartCoroutine(OverlayOn(host, open));
            return host;
        }

        private static IEnumerator OverlayOn(GameScreen host, Action<GameScreen> open)
        {
            var app = GameApp.I;
            float end = Time.realtimeSinceStartup + 10f;
            while (app.Router.Current != host || app.Router.Busy || host.Root?.panel == null)
            {
                if (Time.realtimeSinceStartup > end) yield break;
                yield return null;
            }
            yield return null;
            Coach.Cancel();
            InspectCard.Hide();
            open(host);
            while (app.Router.Current == host) yield return null;
            Coach.Cancel();
            InspectCard.Hide();
            foreach (var t in app.Overlay.Query("toast").ToList()) t.RemoveFromHierarchy();
        }

        private static GameScreen PreviewDialogue(GameApp app)
        {
            var party = app.Save.Party;
            string a = party.FirstOrDefault(), b = party.Skip(1).FirstOrDefault() ?? a;
            if (a == null) return null;
            var def = new DialogueDef { Id = "preview_talk", Title = "On the road" };
            def.Speakers.Add(new DialogueSpeaker { Id = "a", Class = a });
            def.Speakers.Add(new DialogueSpeaker { Id = "b", Class = b });
            def.Nodes.Add(new DialogueNode { Id = "n1", Speaker = "a", Text = "The road bends west past the mill. If the bridge still stands, we reach the next camp before dark.", Next = "n2" });
            def.Nodes.Add(new DialogueNode { Id = "n2", Speaker = "b", Text = "Then we keep moving." });
            var screen = new DialogueScreen(def, () => new HubScreen());
            return OverOn(screen, s => ((DialogueScreen)s).FinishLine());
        }

        private static void PreviewCoach(GameScreen _)
        {
            var tip = Meta.CampTips.For("blacksmith") ?? Meta.CampTips.All.FirstOrDefault();
            if (tip == null) return;
            Coach.Run(new CoachLesson
            {
                Id = "camp-" + tip.Station, Kicker = "Camp", Title = tip.Title, Text = tip.Text,
                Image = "Tutorial/camp-" + tip.Station, Practice = true,
            });
        }

        private static void PreviewInspect(GameApp app, GameScreen host, string hero)
        {
            var h = app.Save.Hero(hero);
            var ability = h == null ? null : Meta.HeroBoard.OwnedAbilities(app.Content, h).Select(app.Content.TryAbility).FirstOrDefault(x => x != null)
                ?? app.Content.Abilities.Values.FirstOrDefault();
            if (ability == null) return;
            var block = host.Root.Q("status-known-abilities");
            var anchor = block?.Children().Skip(1).FirstOrDefault() ?? block ?? host.Root;
            InspectCard.Show(anchor, c => InspectCard.Ability(c, ability, wearer: h == null ? null : app.Content.TryClass(h.ClassId)));
        }

        private static string PreviewStage(GameApp app) => app.Content.CampaignStages()[0].Id;

        private static Meta.BattleReport PreviewDefeat(GameApp app)
        {
            var c = app.Content;
            var party = app.Save.Party;
            string foe = c.Classes.Keys.FirstOrDefault(k => !party.Contains(k));
            string target = party.FirstOrDefault();
            var r = new Meta.BattleReport { StageId = PreviewStage(app), XpEach = 12 };
            r.LastHits.Add(new Core.Battle.RecordedHit { Trap = true, TargetClass = party.LastOrDefault(), Amount = 40 });
            r.LastHits.Add(new Core.Battle.RecordedHit { AttackerClass = foe, TargetClass = target, AbilityId = c.Abilities.Keys.FirstOrDefault(), Amount = 1250 });
            r.LastHits.Add(new Core.Battle.RecordedHit { AttackerClass = foe, TargetClass = target, Amount = 96, Knockout = true });
            return r;
        }

        private static GameScreen PreviewDrill(GameApp app)
        {
            var order = Meta.Drills.NewGameOrder(app.Content);
            string stage = order.Skip(1).FirstOrDefault(id => app.Content.TryStage(id) != null) ?? order.FirstOrDefault();
            if (stage == null) return null;
            var r = new Meta.BattleReport { Victory = true, StageId = stage, Gold = 60, XpEach = 14 };
            foreach (var it in app.Content.Items.Values.Where(i => i.Type == Core.Content.ItemType.CraftingMaterial).Take(3)) r.Items[it.Id] = 1;
            return new PostBattleScreen(r, stage);
        }

        private static Meta.BattleReport PreviewLevelUps(GameApp app)
        {
            var r = new Meta.BattleReport { Victory = true, StageId = PreviewStage(app), Gold = 214, XpEach = 380 };
            foreach (var id in app.Save.Party.Take(3))
            {
                var h = app.Save.Hero(id);
                int lv = h != null ? Meta.Progression.Level(h) : 1;
                r.LevelUps.Add((id, lv, lv + 2));
            }
            return r;
        }

        private static Meta.BattleReport PreviewReport(GameApp app, bool mid)
        {
            var c = app.Content;
            var items = c.Items.Values;
            var theme = c.ThemeOf(PreviewStage(app));
            var r = new Meta.BattleReport
            {
                Victory = true, StageId = PreviewStage(app), Gold = 214, XpEach = 38,
            };
            // GG-T1786: "results-mid" is a win on the road (orbs carried, the Medic Wagon's patch); the other a win back at camp.
            if (mid) { Meta.TravelRules.SetOut(app.Save); app.Save.World.CarriedMana = 400; r.CarriedMana = 400; r.MedicHealed = 18; }
            else
            {
                Meta.TravelRules.MakeCamp(app.Save);
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
