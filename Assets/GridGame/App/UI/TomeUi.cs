using System;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// Learnable-passive rewards on screen (§25.6.4, GG-T301): a Tome's taught passive as its generated sentence (terms
    /// tappable through GlossaryText) with a "Teach to…" row of hero buttons, and a Chest's "Open" button. Used by the
    /// results screen, the Items bag, the quest hand-in card and the bounty board, so every reward source reads the same.
    /// </summary>
    internal static class TomeUi
    {
        private static GameApp App => GameApp.I;

        /// <summary>"[Reactive] Counter Attack: When [attacked directly]: …" — the kind, the name and the trigger sentence.</summary>
        public static string Sentence(ContentDb c, AbilityDef a) =>
            a == null ? "" : $"[{TriggerText.KindLabel(a)}] {a.Name}: {TriggerText.Describe(c, a)}";

        /// <summary>The taught passive's sentence alone (no buttons), for a reward that isn't in the bag yet.</summary>
        public static void Preview(VisualElement parent, ItemDef tome)
        {
            var a = RewardCatalog.TaughtBy(App.Content, tome);
            if (a == null) return;
            var l = GlossaryText.Create("Teaches " + Sentence(App.Content, a), "kit-text dim", parent);
            l.style.whiteSpace = WhiteSpace.Normal;
        }

        /// <summary>The sentence plus "Teach to…": one button per roster hero (party first), disabled for a hero who already
        /// knows it or whose class can't learn it. Teaching uses up one Tome from the bag; the block redraws itself.</summary>
        /// <param name="sentence">False when the caller already shows the sentence (the Items detail panel).</param>
        public static VisualElement Offer(VisualElement parent, ItemDef tome, Action<Result> after = null, bool sentence = true)
        {
            var box = Ui.El("col", parent);
            box.name = "tome-" + tome.Id;
            box.style.marginLeft = Space.S16; box.style.marginBottom = Space.S8;
            Build(box, tome, after, null, sentence);
            return box;
        }

        private static void Build(VisualElement box, ItemDef tome, Action<Result> after, string taughtTo, bool sentence)
        {
            box.Clear();
            var c = App.Content; var s = App.Save;
            var a = RewardCatalog.TaughtBy(c, tome);
            if (a == null) return;
            if (sentence) GlossaryText.Create(Sentence(c, a), "small", box).style.whiteSpace = WhiteSpace.Normal;
            if (taughtTo != null)
            {
                var done = Ui.Text($"{taughtTo} learned {a.Name}.", "small good", box);
                App.StartCoroutine(Tween.Pop(done, 1.12f, 0.18f));
            }
            if (s.Count(tome.Id) <= 0) return;
            Ui.Text(s.Count(tome.Id) > 1 ? $"Teach to ({s.Count(tome.Id)} tomes)" : "Teach to", "small dim", box);
            var row = Ui.El("row", box);
            row.style.flexWrap = Wrap.Wrap;
            foreach (var h in s.Roster.OrderByDescending(x => s.Party.Contains(x.ClassId)).ToList())
            {
                string id = h.ClassId, name = c.TryClass(id)?.Name ?? id;
                string why = Tomes.WhyNot(c, s, tome.Id, id);
                var b = RowIcon.IconButton(name, RowKind.Hero, () => // GG-T754 (§26.7)
                {
                    var r = Tomes.Teach(c, s, tome.Id, id);
                    App.Toast(r.Message, 2.4f);
                    App.Audio.Sfx(r.Ok ? "battle.powerup" : "ui.error");
                    if (r.Ok) App.Persist();
                    Build(box, tome, after, r.Ok ? name : null, sentence);
                    after?.Invoke(r);
                }, "btn--small", row);
                b.name = "teach-" + id;
                b.style.flexGrow = 1; b.style.flexShrink = 1; b.style.flexBasis = Length.Percent(30); b.style.minWidth = 0;
                Ui.SetEnabled(b, why == null);
                if (why != null) b.tooltip = why;
            }
        }

        /// <summary>GG-T1969: a looted Tome on the results as one card: the tome's icon and "Teach Counter Attack to", the
        /// taught passive's sentence, then a thumbnail button per roster hero (party first), dimmed for a hero who can't
        /// learn it. A tap selects a hero and a second tap teaches (it uses up one Tome); the card redraws itself.</summary>
        public static VisualElement TeachCard(VisualElement parent, ItemDef tome)
        {
            var card = Ui.Panel(parent, null, false, "tome-" + tome.Id);
            DrawTeachCard(Ui.El("col", card), tome, null);
            return card;
        }

        private static void DrawTeachCard(VisualElement box, ItemDef tome, string taughtTo)
        {
            box.Clear();
            var c = App.Content; var s = App.Save;
            var a = RewardCatalog.TaughtBy(c, tome);
            if (a == null) return;
            int left = s.Count(tome.Id);
            var head = Ui.El("row", box);
            head.style.alignItems = Align.Center;
            var title = Ui.Heading(left > 1 ? $"Teach {a.Name} to ({left} tomes)" : left > 0 ? $"Teach {a.Name} to" : a.Name, head, "gold");
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.flexShrink = 1;
            head.KindIcon(RowKind.Item, Art.ItemIcon(tome.Id));
            GlossaryText.Create(Sentence(c, a), "kit-text dim", box).style.whiteSpace = WhiteSpace.Normal;
            if (taughtTo != null)
            {
                var done = Ui.Para($"{taughtTo} learned {a.Name}.", box, "good");
                App.StartCoroutine(Tween.Pop(done, 1.12f, Timing.Quick));
            }
            if (left <= 0) return;
            var row = Ui.El("row", box);
            row.style.flexWrap = Wrap.Wrap;
            foreach (var h in s.Roster.OrderByDescending(x => s.Party.Contains(x.ClassId)).ToList())
            {
                string id = h.ClassId, name = c.TryClass(id)?.Name ?? id;
                string why = Tomes.WhyNot(c, s, tome.Id, id);
                var b = Ui.Button("", () =>
                {
                    var r = Tomes.Teach(c, s, tome.Id, id);
                    App.Toast(r.Message, 2.4f);
                    App.Audio.Sfx(r.Ok ? "battle.powerup" : "ui.error");
                    if (r.Ok) App.Persist();
                    DrawTeachCard(box, tome, r.Ok ? name : null);
                }, "btn--small", row, confirmTap: true);
                b.name = "teach-" + id;
                b.style.flexDirection = FlexDirection.Column;
                b.style.alignItems = Align.Center;
                b.style.flexGrow = 1; b.style.flexShrink = 1; b.style.flexBasis = Length.Percent(22); b.style.minWidth = Size.Target;
                Ui.Pad(b, Space.S8, Space.S8, Space.S8, Space.S8);
                Ui.Thumb(id, b, Size.Avatar).pickingMode = PickingMode.Ignore;
                Ui.Caption(name, b).pickingMode = PickingMode.Ignore;
                Ui.SetEnabled(b, why == null);
                if (why != null) b.tooltip = why;
            }
        }

        /// <summary>A Chest's "Open" button: rolls it, toasts the haul, and calls <paramref name="opened"/> with what came out.</summary>
        public static Button OpenButton(VisualElement parent, ItemDef chest, Action<Result, Chests.Haul> opened)
        {
            var b = RowIcon.IconButton(App.Save.Count(chest.Id) > 1 ? $"Open ({App.Save.Count(chest.Id)})" : "Open", RowKind.Item, () =>
            {
                var r = Chests.Open(App.Content, App.Save, chest.Id, out var haul);
                App.Toast(r.Message, 2.8f);
                App.Audio.Sfx(r.Ok ? "battle.pickup" : "ui.error");
                if (r.Ok) App.Persist();
                opened?.Invoke(r, haul);
            }, "btn--small btn--primary", parent);
            b.name = "open-" + chest.Id;
            Ui.SetEnabled(b, App.Save.Count(chest.Id) > 0);
            return b;
        }
    }
}
