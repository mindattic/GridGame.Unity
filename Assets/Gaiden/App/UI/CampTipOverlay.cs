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
        public static void ShowIfPending(GameScreen page, string station)
        {
            var save = GameApp.I.Save;
            var tip = CampTips.Resolve(GameApp.I.Content, save, CampTips.Pending(save, station));
            if (tip == null || Coach.Suppressed || Coach.Off(save) || page.Root == null) return;
            // Wait for a layout pass so the target has a size, and for a Camp-row lesson that led here to finish.
            int tries = 0;
            IVisualElementScheduledItem item = null;
            item = page.Root.schedule.Execute(() =>
            {
                if (page.Root.panel == null || ++tries > 40) { item.Pause(); return; }
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

        /// <summary>The marks a tip walks: one or two taps on the exact controls.</summary>
        private static List<CoachStep> Steps(CampTip tip)
        {
            var steps = new List<CoachStep>();
            if (string.IsNullOrEmpty(tip.Target)) return steps;
            CoachStep Tap(string target, string caption) => new CoachStep { Caption = caption, Element = () => Find(target) };
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
