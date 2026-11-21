using System.Collections.Generic;
using System.Linq;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The first-use lesson for a Camp station (GG-T173; coach marks since GG-T539): a blocking popup naming the station's
    /// one important action, then the stage dims except the exact control to use, an arrow points at it, and only it takes
    /// taps until the player has used it (the Merchant's + and then Buy, a recipe and then Craft, the worn piece to mend).
    /// Shown once per station per save (<see cref="SaveData.SeenCampTips"/>); Settings › How to play › Replay tutorials
    /// brings them back. The words live in <see cref="CampTips"/>.
    /// </summary>
    public static class CampTipOverlay
    {
        /// <summary>The one poller waiting to show a tip: every page Build (each Refresh, a resize) replaces it, so
        /// rebuilds never stack pollers that each open the lesson.</summary>
        private static IVisualElementScheduledItem poller;

        public static void ShowIfPending(GameScreen page, string station)
        {
            poller?.Pause();
            poller = null;
            var save = GameApp.I.Save;
            var tip = CampTips.Resolve(GameApp.I.Content, save, CampTips.Pending(save, station));
            if (tip == null || Coach.Suppressed || Coach.Off(save) || page.Root == null) return;
            // Wait for a layout pass so the target has a size, and for a Camp-row lesson that led here to finish.
            int tries = 0;
            IVisualElementScheduledItem item = null;
            poller = item = page.Root.schedule.Execute(() =>
            {
                // Save & Quit drops the save while this page is still fading out: never start a lesson then.
                if (page.Root.panel == null || ++tries > 40 || GameApp.I.Save != save) { item.Pause(); return; }
                if (Coach.Active) return;
                item.Pause();
                Show(page, tip);
            }).StartingIn(60).Every(100);
        }

        private static void Show(GameScreen page, CampTip tip)
        {
            if (CampTips.Pending(GameApp.I.Save, tip.Station) == null) return;
            Coach.Run(new CoachLesson
            {
                Id = "camp-" + tip.Station,
                Kicker = "Camp",
                Title = tip.Title,
                Text = tip.Text,
                Image = "Tutorial/camp-" + tip.Station,
                Steps = Steps(tip),
                // The page stays the lesson's home: a station left mid-lesson ends its marks (they'd point at nothing).
                Hold = () => GameApp.I.Overlay.Q(TimedJobView.VeilName) != null,
                Finished = how =>
                {
                    if (how == CoachEnd.Completed || how == CoachEnd.Aborted) { CampTips.MarkSeen(GameApp.I.Save, tip.Station); GameApp.I.Persist(); }
                },
                CardName = "camp-tip-card", NextName = "camp-tip-ok", RingName = "camp-tip-ring",
            });
        }

        /// <summary>The control a camp tip names (a name, or a name prefix ending in '-'), visible on the stage.</summary>
        public static VisualElement Find(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            bool prefix = target.EndsWith("-");
            var stage = GameApp.I.Stage.Q("screens") ?? GameApp.I.Stage;
            return stage.Query<VisualElement>().Where(e => e.name != null && (prefix ? e.name.StartsWith(target) : e.name == target)
                                                         && e.resolvedStyle.display != DisplayStyle.None && e.worldBound.height > 0).First();
        }

        private static string Verb(string station) =>
            station == "alchemist" ? "Brew" : station == "enchantress" ? "Enchant" : station == Training.TipStation ? "Learn" : "Craft";

        /// <summary>What a station's action changes (gold, the bag, gear wear and mastery, station XP, crafted recipes, flags).</summary>
        private static long Fingerprint(SaveData s)
        {
            if (s == null) return 0;
            long f = s.Gold * 7919L + s.CraftedRecipes.Count * 104729L + s.Flags.Count * 1299709L + s.Roster.Count * 15485863L;
            foreach (var kv in s.Inventory) f += kv.Value * 31L + kv.Key.GetHashCode();
            foreach (var kv in s.StationXp) f += kv.Value * 131L;
            foreach (var kv in s.Worn) foreach (var w in kv.Value) f += w.Durability * 7L + w.Repairs * 13L + (w.Enchant?.Length ?? 0) * 17L;
            foreach (var h in s.Roster)
            {
                if (h == null) continue;
                f += h.Known.Count * 101L + h.WeaponDurability * 3L + h.WeaponRepairs * 5L + (h.WeaponId?.Length ?? 0) * 11L + h.Armor.Count * 19L + h.Jewelry.Count * 23L;
                foreach (var b in h.Bar) f += (b?.Length ?? 0) * 29L;
                foreach (var a in h.Armor.Values) if (a != null) f += (a.ItemId?.Length ?? 0) * 37L + a.Durability * 41L + (a.Enchant?.Length ?? 0) * 43L;
            }
            return f;
        }

        /// <summary>The marks a tip walks: one or two taps on the exact controls.</summary>
        private static List<CoachStep> Steps(CampTip tip)
        {
            var steps = new List<CoachStep>();
            if (string.IsNullOrEmpty(tip.Target)) return steps;
            CoachStep Tap(string target, string caption)
            {
                // GG-T542: also done once the tap's effect lands in the save (bought, crafted, repaired, learned): a Craft
                // rebuilds its page under the finger and the release could miss the box, leaving the mark up after the work.
                long before = 0;
                return new CoachStep { Caption = caption, Element = () => Find(target),
                    Enter = () => before = Fingerprint(GameApp.I.Save), Also = () => Fingerprint(GameApp.I.Save) != before };
            }
            string verb = Verb(tip.Station);
            if (tip.Station == "shop")
            {
                steps.Add(Tap(tip.Target, "Tap + to put a Healing Potion in your cart."));
                steps.Add(Tap("shop-commit", "Tap Buy."));
            }
            else if (tip.Target.StartsWith(CampTips.RecipeRowPrefix) || tip.Target.StartsWith(ArmorEnchanting.EnchantRowPrefix))
            {
                steps.Add(Tap(tip.Target, "Tap the recipe to see what goes in."));
                steps.Add(Tap(CampTips.CraftButton, $"Tap {verb}."));
            }
            else if (tip.Target == CampTips.CraftButton) steps.Add(Tap(CampTips.CraftButton, $"Tap {verb}."));
            else if (tip.Station == "blacksmith") steps.Add(Tap(tip.Target, "Tap the worn piece to mend it."));
            else if (tip.Station == "summon") steps.Add(Tap(tip.Target, "Tap Draw."));
            else if (tip.Station == "enchantress") steps.Add(Tap(tip.Target, "Tap a piece of worn armor."));
            else steps.Add(Tap(tip.Target, "Tap here."));
            return steps;
        }
    }
}
