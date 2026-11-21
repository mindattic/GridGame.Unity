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

        public static void OnHub(GameScreen hub)
        {
            var s = GameApp.I.Save;
            if (s == null || Coach.Suppressed || Coach.Off(s) || hub.Root == null) return;
            // After one layout pass (rows have their size) and once any lesson that brought the player here has ended.
            int tries = 0;
            IVisualElementScheduledItem item = null;
            item = hub.Root.schedule.Execute(() =>
            {
                if (hub.Root.panel == null || ++tries > 30) { item.Pause(); return; }
                if (Coach.Active) return;
                item.Pause();
                Next(s);
            }).StartingIn(120).Every(100);
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
                    Steps = { new CoachStep { Caption = $"Tap {Label(row)}.", Element = () => CampTipOverlay.Find(row) } },
                });
                return;
            }
            if (Coach.Pending(s, "equip") && EquipPlan(GameApp.I.Content, s) is (HeroSave h, EquipmentSlot slot, ItemDef item))
                Coach.Run(EquipLesson(GameApp.I.Content, s, h, slot, item));
        }

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
