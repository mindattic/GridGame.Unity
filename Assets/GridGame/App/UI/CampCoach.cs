using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// Camp lessons that start on the Camp menu itself (GG-T539): a vendor who just joined the caravan (the join table in
    /// bible §25.0.1.1) gets a popup and a box on their row, so the player walks in, where the station's own lesson
    /// (<see cref="CampTipOverlay"/>) takes over; and the first piece of gear in the bag that fits an empty slot walks
    /// Heroes → Equip → the hero → the slot → the piece → equip, one boxed tap at a time.
    /// </summary>
    public static class CampCoach
    {
        private sealed class Station
        {
            public string Row, Id, Title, Text;
            public System.Func<SaveData, bool> Open;
        }

        private static readonly Station[] Stations =
        {
            new Station { Row = "camp-merchant", Id = "shop", Title = "The Merchant joined",
                Text = "A peddler now trades with the caravan: potions, gear and bottles for the gold your battles win.",
                Open = s => CampUnlocks.NpcOpen(s, CampNpcs.Merchant) },
            new Station { Row = "camp-blacksmith", Id = "blacksmith", Title = "The Blacksmith joined",
                Text = "Worn gear shatters in battle. The smith mends it, and later forges weapons and armor from what foes drop.",
                Open = s => CampUnlocks.NpcOpen(s, CampNpcs.Blacksmith) },
            new Station { Row = "camp-alchemist", Id = "alchemist", Title = "The Alchemist joined",
                Text = "Potions brewed from battle drops cost far less than the Merchant's.",
                Open = s => CampUnlocks.NpcOpen(s, CampNpcs.Alchemist) },
            new Station { Row = "camp-enchantress", Id = "enchantress", Title = "The Enchantress joined",
                Text = "Runes bound into worn armor give its wearer a lasting passive.",
                Open = s => Features.Enchanter && CampUnlocks.NpcOpen(s, CampNpcs.Enchantress) },
            new Station { Row = "camp-summon-circle", Id = "summon", Title = "The Summon Circle is open",
                Text = "Draw to call a new hero to the caravan. The odds always show before you pay.",
                Open = CampUnlocks.SummonOpen },
        };

        /// <summary>The one poller per Camp build: a Refresh replaces it rather than stacking another.</summary>
        private static IVisualElementScheduledItem poller;

        public static void OnHub(GameScreen hub)
        {
            poller?.Pause();
            poller = null;
            var s = GameApp.I.Save;
            if (s == null || hub.Root == null) return;
            if (Coach.Suppressed || Coach.Off(s)) { FinishHubIntro(s); return; }
            // After one layout pass (rows have their size) and once any lesson that brought the player here has ended.
            int tries = 0;
            IVisualElementScheduledItem item = null;
            poller = item = hub.Root.schedule.Execute(() =>
            {
                // Save & Quit nulls the save while Camp is still fading out (GG bughunt): stop, don't coach the Title.
                if (GameApp.I.Save != s || GameApp.I.Router.Current != hub) { item.Pause(); return; }
                if (hub.Root.panel != null && VariantPicker.Open) return; // GG-T532: a joining hero's look is picked first
                if (hub.Root.panel == null || ++tries > 30) { item.Pause(); return; }
                if (Coach.Active) return;
                item.Pause();
                FinishHubIntro(s);
                Next(s);
            }).StartingIn(120).Every(100);
        }

        /// <summary>GG-T1394: Camp's first visit ends the onboarding once its lesson is shown (or there is none to show).</summary>
        private static void FinishHubIntro(SaveData s)
        {
            if (s.Onboarding?.Step != OnboardingStep.HubIntro) return;
            Onboarding.Finish(s);
            GameApp.I.Persist();
        }

        private static void Next(SaveData s)
        {
            foreach (var st in Stations)
            {
                if (!st.Open(s) || CampTips.Pending(s, st.Id) == null || !Coach.Pending(s, "hub-" + st.Id)) continue;
                var row = st.Row;
                if (CampTipOverlay.Find(row) == null) continue;
                Coach.Run(new CoachLesson
                {
                    Id = "hub-" + st.Id, Kicker = "Camp", Title = st.Title, Text = st.Text, Image = "Tutorial/hub-" + st.Id,
                    Steps = TapSteps(s, row),
                });
                return;
            }
            if (Coach.Pending(s, "equip") && EquipPlan(GameApp.I.Content, s) is (HeroSave h, EquipmentSlot slot, ItemDef item))
            {
                Coach.Run(EquipLesson(GameApp.I.Content, s, h, slot, item));
                return;
            }
            // GG-T1045: the first time a party hero holds a skill point, walk Heroes > Abilities > the hero > Board.
            if (Coach.Pending(s, BoardLesson) && BoardPlan(s) is HeroSave earner && CampTipOverlay.Find("camp-heroes") != null)
            {
                Codex.Unlock(s, CodexKinds.Keyword, BoardKeyword);
                Coach.Run(BoardLessonFor(GameApp.I.Content, earner));
                return;
            }
            // GG-T926: once a stage is cleared and the Codex holds an unread entry, one lesson points at its row.
            if (Coach.Pending(s, CodexLesson) && s.ClearedStages != null && s.ClearedStages.Count > 0
                && Codex.UnreadCount(GameApp.I.Content, s) > 0 && CampTipOverlay.Find("camp-codex") != null)
                Coach.Run(new CoachLesson
                {
                    Id = CodexLesson, Kicker = "Camp", Title = "The Codex",
                    Text = "Places, people, foes and story beats you meet are written into the Codex. A New badge marks the entries you haven't read yet.",
                    Image = "Tutorial/" + CodexLesson,
                    Steps = TapSteps(s, "camp-codex"),
                });
        }

        /// <summary>GG-T1616: the taps that reach a hub destination. A Craft or Journal member (GG-T1613) sits inside its
        /// group, so the coach rings the group tile first, then the member's tile on that group's page.</summary>
        private static List<CoachStep> TapSteps(SaveData s, string row)
        {
            var group = CampMenu.Build(GameApp.I.Content, s, GameApp.I.Settings).FirstOrDefault(d => HubScreen.TileName(d) == row)?.Group;
            if (group != CampGroup.Craft && group != CampGroup.Journal)
                return new List<CoachStep> { new CoachStep { Caption = $"Tap {Label(row)}.", Element = () => CampTipOverlay.Find(row) } };
            string name = group == CampGroup.Craft ? "Craft" : "Journal", page = "camp-group-" + name.ToLowerInvariant();
            bool OnGroup() => GameApp.I.Router.Current is CampGroupScreen g && g.Id == page;
            return new List<CoachStep>
            {
                new CoachStep { Caption = $"Tap {name}.", Element = () => CampTipOverlay.Find("camp-" + name.ToLowerInvariant()), Done = OnGroup },
                new CoachStep { Caption = $"Tap {Label(row)}.", Element = () => OnGroup() ? CampTipOverlay.Find(row) : null },
            };
        }

        /// <summary>GG-T1045: the Skill Board lesson's id (its save flag is "lesson:hub-board") and its Codex keyword entry.</summary>
        public const string BoardLesson = "hub-board", BoardKeyword = "skill_board";

        /// <summary>GG-T1045: the first party hero with a skill point to spend, or null.</summary>
        public static HeroSave BoardPlan(SaveData s) =>
            s?.Party?.Select(s.Hero).FirstOrDefault(h => h != null && h.SkillPoints > 0);

        private static CoachLesson BoardLessonFor(ContentDb c, HeroSave h)
        {
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            string id = h.ClassId;
            bool OnAbilities() => GameApp.I.Router.Current is HeroScreen hs && (hs.Id == "abilities" || hs.Id == "talents");
            return new CoachLesson
            {
                Id = BoardLesson, Kicker = "Camp", Title = "Skill points",
                Text = $"The {hero} earned a skill point. Every level gives 1 SP, and every node on the Skill Board costs 1. " +
                       "Buy nodes next to the ones the hero owns: passives add stats, ability nodes teach the ability. The Codex explains the board and its six families.",
                Image = "Tutorial/" + BoardLesson,
                Steps = new List<CoachStep>
                {
                    new CoachStep { Caption = "Tap Heroes.", Element = () => CampTipOverlay.Find("camp-heroes"),
                        Done = () => GameApp.I.Router.Current is HeroScreen },
                    new CoachStep { Caption = "Tap Abilities.", Element = () => CampTipOverlay.Find("tab-abilities"), Done = OnAbilities },
                    new CoachStep { Caption = $"Tap the {hero}.", Element = () => CampTipOverlay.Find("hero-pick-" + id),
                        Done = () => OnAbilities() && GameApp.I.Router.Current is HeroScreen hs && hs.HeroId == id },
                    new CoachStep { Caption = "Tap Board.", Element = () => CampTipOverlay.Find("tab-board"),
                        Done = () => GameApp.I.Router.Current is HeroScreen hs && hs.Id == "talents" },
                    new CoachStep { Caption = "Tap a node next to your trail to see its route and cost, then Buy route.",
                        Element = () => CampTipOverlay.Find("hex-board"), Info = true },
                },
            };
        }

        /// <summary>GG-T926: the Codex lesson's id (its save flag is "lesson:hub-codex").</summary>
        public const string CodexLesson = "hub-codex";

        private static string Label(string row) =>
            row == "camp-summon-circle" ? "Summon Circle" : char.ToUpperInvariant(row[5]) + row.Substring(6);

        /// <summary>The first party hero with an empty slot that a piece in the bag fits (and that the hero may wear).</summary>
        public static (HeroSave, EquipmentSlot, ItemDef)? EquipPlan(ContentDb c, SaveData s)
        {
            var slots = new[] { EquipmentSlot.Weapon }.Concat(BattleEngine.ArmorSlots).Concat(Equipment.JewelrySlots).ToList();
            foreach (var h in s.Party.Select(s.Hero).Where(x => x != null))
                foreach (var slot in slots)
                {
                    if (Equipment.Equipped(h, slot) != null) continue;
                    var item = Equipment.Candidates(c, s, slot).FirstOrDefault(i => Equipment.ProficiencyGate(c, h, i) == null);
                    if (item != null) return (h, slot, item);
                }
            return null;
        }

        private static CoachLesson EquipLesson(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot, ItemDef item)
        {
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            string slotName = ItemUi.SlotName(slot);
            string id = h.ClassId;
            bool OnEquip() => GameApp.I.Router.Current is HeroScreen hs && hs.Id == "equip";
            return new CoachLesson
            {
                Id = "equip", Kicker = "Camp", Title = "Equip gear",
                Text = $"Your {item.Name} fits the {hero}'s empty {slotName} slot. Gear in the bag does nothing until a hero wears it.",
                Image = "Tutorial/equip",
                Steps = new List<CoachStep>
                {
                    new CoachStep { Caption = "Tap Heroes.", Element = () => CampTipOverlay.Find("camp-heroes"),
                        Done = () => GameApp.I.Router.Current is HeroScreen },
                    new CoachStep { Caption = "Tap Equip.", Element = () => CampTipOverlay.Find("tab-equip"), Done = OnEquip },
                    new CoachStep { Caption = $"Tap the {hero}.", Element = () => CampTipOverlay.Find("hero-pick-" + id),
                        Done = () => OnEquip() && GameApp.I.Router.Current is HeroScreen hs && hs.HeroId == id },
                    new CoachStep { Caption = $"Tap the {slotName} slot.", Element = () => CampTipOverlay.Find("equip-slot-" + slot) },
                    new CoachStep { Caption = $"Tap the {item.Name} to compare it.", Element = () => CampTipOverlay.Find("equip-ok-" + item.Id) },
                    new CoachStep { Caption = "Tap it again to put it on.", Element = () => CampTipOverlay.Find("equip-ok-" + item.Id),
                        Done = () => Equipment.Equipped(h, slot) == item.Id },
                },
            };
        }
    }
}
