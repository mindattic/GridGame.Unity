using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>GG-T2023: one hero's Talk sheet, opened from their Talk row when they have bond scenes: their Talk topic,
    /// then their open scenes, then the locked ones (their Trust scenes and the pair scenes they're in) with the tier each
    /// needs.</summary>
    public sealed class TalkPartnerScreen : HubPage
    {
        private readonly string hero;

        public TalkPartnerScreen(string hero) { this.hero = hero; }

        public override string Id => "talk-partner";
        protected override string Title => C.TryClass(hero)?.Name ?? hero;
        protected override string TipStation => "talk";

        /// <summary>The bond scenes that belong in <paramref name="hero"/>'s sheet: every Trust scene of theirs, and every
        /// locked pair scene they're in (an open one plays from the Talk screen's Together section). Open ones first.</summary>
        public static List<BondScene> ScenesOf(List<BondScene> scenes, string hero) => scenes.Where(b =>
                b.Def.Trigger.Kind == DialogueTriggerKind.Trust ? string.Equals(b.Def.Trigger.Id, hero, StringComparison.OrdinalIgnoreCase)
                : b.Def.Trigger.Kind == DialogueTriggerKind.Cohesion && !b.Unlocked
                  && (string.Equals(b.With, hero, StringComparison.OrdinalIgnoreCase) || string.Equals(b.Other, hero, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(b => b.Unlocked ? 0 : 1).ThenBy(b => b.Def.Trigger.Level).ToList();

        protected override void BuildBody(VisualElement body)
        {
            var list = Ui.ListPanel(body, null, out var scroll, "talk-partner-list");
            var topic = DialogueRunner.TalkTopic(C, S, hero);
            if (topic != null)
            {
                bool fresh = !S.SeenDialogues.Contains(topic.Id);
                var row = Ui.MenuRow(topic.Title ?? "Talk", fresh ? "New!" : "", () => App.Router.Push(new DialogueScreen(topic, hero, null)), list);
                row.name = "talk-topic";
                var icon = row.KindIcon(RowKind.Talk);
                if (fresh)
                {
                    RowIcon.Badge(icon, RowKind.New);
                    row.Q<Label>("value").AddToClassList("gold");
                }
            }
            foreach (var b in ScenesOf(DialogueRunner.BondScenes(C, S), hero)) SceneRow(list, b, hero);
            if (list.childCount == 0) Ui.EmptyState(list, RowKind.Talk, "Nothing to talk about right now.", "talk-empty");
            Ui.SelectFirst(list);
        }

        /// <summary>GG-T1598 (§27.1): one bond scene's row, in a hero's sheet (<paramref name="hero"/>) or in Together: open,
        /// it reads New! in gold and plays with "$with" as the trigger hero; locked, it's disabled and says the tier it needs.</summary>
        internal static void SceneRow(VisualElement list, BondScene b, string hero)
        {
            var c = App.Content;
            var t = b.Def.Trigger;
            bool generic = ContentLoader.IsGenericCohesion(b.Def);
            bool trust = t.Kind == DialogueTriggerKind.Trust;
            string pair = $"{c.TryClass(b.With)?.Name ?? b.With} and {c.TryClass(b.Other)?.Name ?? b.Other}";
            string title = generic ? (b.Def.Title != null ? $"{pair}: {b.Def.Title}" : pair) : b.Def.Title ?? (trust ? "A word alone" : pair);
            string value = b.Unlocked ? "New!" : $"{(trust ? "Trust" : "Cohesion")} {t.Level} needed";
            var row = Ui.MenuRow(title, value, () => App.Router.Push(DialogueScreen.ForRunner(b.Begin(c, App.Save), null)), list, null, b.Unlocked);
            row.name = "talk-scene-" + b.Def.Id + (generic ? "-" + Bonds.PairKey(b.With, b.Other) : "");
            RowIcon.Badge(trust && hero != null ? row.KindIcon(RowKind.Hero, DialogueScreen.HasPortrait(hero) ? hero : null) : row.KindIcon(RowKind.Talk),
                b.Unlocked ? RowKind.New : RowKind.Locked);
            if (b.Unlocked) row.Q<Label>("value")?.AddToClassList("gold");
        }
    }
}
