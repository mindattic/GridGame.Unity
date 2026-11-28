using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>The quest journal (§22.11, GG-T237): quests ready to hand in, under way, heard of, still to ask about,
    /// and done. Optional side quests — separate from the bounty board and never in the campaign's way.</summary>
    public sealed class QuestScreen : HubPage
    {
        private string handedIn;
        public override string Id => "quests";
        protected override string Title => "Quests";

        protected override void BuildBody(VisualElement body)
        {
            if (handedIn != null && C.TryQuest(handedIn) is QuestDef done) TurnInCard(done, body);
            var list = Ui.ListPanel(body, null, "quest-list");
            bool any = false;

            void Section(string title, System.Collections.Generic.IEnumerable<QuestDef> quests, System.Action<QuestDef, VisualElement> extra)
            {
                var qs = quests.ToList();
                if (qs.Count == 0) return;
                any = true;
                Ui.Section(title, list);
                foreach (var q in qs) Card(q, list, extra);
            }

            var all = C.QuestOrder;
            Section("Ready to hand in!", all.Where(q => Quests.State(C, S, q) == QuestState.Ready), (q, card) =>
            {
                var b = RowIcon.IconButton("Hand in", RowKind.Quest, () => TurnIn(q), "btn--primary btn--small", card);
                b.name = "quest-turnin-" + q.Id;
            });
            Section("Under way", all.Where(q => Quests.State(C, S, q) == QuestState.Active), (q, card) =>
                RowIcon.IconButton("Set aside", RowKind.Close, () => Confirm.Show($"Set \"{q.Name}\" aside? Progress is lost, but you can take it up again.", "Set aside", () => Done(Quests.Abandon(C, S, q.Id))), "btn--danger btn--small", card));
            var heard = Quests.Heard(C, S);
            Section("Heard of", heard, (q, card) =>
            {
                var b = RowIcon.IconButton("Accept", RowKind.Quest, () => Accept(q), "btn--small", card);
                b.name = "quest-accept-" + q.Id;
            });
            Section("Ask around", all.Where(q => Quests.State(C, S, q) == QuestState.Available && !heard.Contains(q)), null);
            Section("Done", all.Where(q => Quests.State(C, S, q) == QuestState.Done), null);
            if (!any) Ui.Empty("No quests yet. Talk to your companions and the caravan's people between battles: they'll have things they need doing.", list);
        }

        private void Card(QuestDef q, VisualElement parent, System.Action<QuestDef, VisualElement> extra)
        {
            var st = Quests.State(C, S, q);
            var thumb = GiverPortrait(q) ?? ThumbFor(q);
            var card = Ui.Card(parent, thumb, q.Name, out var col, "quest-" + q.Id);
            if (thumb == null)
            {
                var icon = card.KindIcon(RowKind.Quest);
                icon.style.width = icon.style.height = Kit.Thumb;
            }
            string theme = C.Campaign.Themes.FirstOrDefault(t => t.Id == q.Theme)?.Name;
            Ui.Caption($"{q.Giver}{(theme != null ? " · " + theme : "")}", col, "dim");
            var chain = Quests.ChainLine(C, S, q); // GG-T434: the chain's steps
            if (chain != null) Ui.Para(chain, col, "gold");
            if (st == QuestState.Available && !Quests.Heard(C, S).Contains(q))
            {
                Ui.Para(q.Giver == "Party" ? "One of your companions has something on their mind. Talk to the party." : $"The {q.Giver} has something on their mind. Talk to them.", col);
                RowIcon.IconButton("Talk", RowKind.Talk, () => Talk(q), "btn--small", Ui.Actions(col)).name = "quest-talk-" + q.Id;
                return;
            }
            Ui.Para(q.Summary, col);
            Ui.Para(st == QuestState.Done ? Quests.Describe(C, q) : $"{Quests.Describe(C, q)}  {Quests.Progress(S, q)}/{Quests.Goal(q)}", col, st == QuestState.Ready ? "good" : "gold");
            Ui.Para("Reward: " + Quests.RewardText(C, q), col, "dim");
            if (extra != null) extra(q, Ui.Actions(col));
        }

        private string GiverId(QuestDef q)
        {
            var words = (q.Giver ?? "").ToLowerInvariant().Split(' ');
            return words[words.Length - 1];
        }

        private string GiverPortrait(QuestDef q)
        {
            var id = GiverId(q);
            return DialogueScreen.HasPortrait(id) ? id : null;
        }

        /// <summary>Opens the giver's conversation: an NPC's Talk topic, else the Talk list (the party has no one voice).</summary>
        private void Talk(QuestDef q)
        {
            var id = GiverId(q);
            var topic = System.Array.IndexOf(DialogueDef.Npcs, id) >= 0 ? DialogueRunner.TalkTopic(C, S, ContentLoader.NpcFlagPrefix + id) : null;
            App.Router.Push(topic != null ? new DialogueScreen(topic, (string)null, (GameScreen)null) : (GameScreen)new TalkScreen());
        }

        /// <summary>The portrait a quest card leads with: the foe to defeat (a family's first member), or the hero to
        /// recruit, level or teach (GG-T434); null for none.</summary>
        private string ThumbFor(QuestDef q)
        {
            var o = q.Objective;
            switch (o.Kind)
            {
                case QuestObjectiveKind.Defeat:
                case QuestObjectiveKind.Recruit:
                    return C.TryClass(o.Id) != null ? o.Id : null;
                case QuestObjectiveKind.DefeatFamily:
                    return QuestCatalog.FamilyClasses(C, o.Id).FirstOrDefault()?.Id;
                case QuestObjectiveKind.ReachLevel:
                case QuestObjectiveKind.LearnSkill:
                    return o.Hero != null && C.TryClass(o.Hero) != null ? o.Hero : null;
                default: return null;
            }
        }

        private void Accept(QuestDef q)
        {
            var r = Quests.Start(C, S, q.Id);
            App.Toast(r.Message);
            App.Audio.Sfx(r.Ok ? "story.quest_accept" : "ui.error");
            if (r.Ok) App.Persist();
            Refresh();
        }

        private void TurnIn(QuestDef q)
        {
            var r = Quests.TurnIn(C, S, q.Id);
            App.Toast(r.Message, 2.6f);
            App.Audio.Sfx(r.Ok ? "story.quest_complete" : "ui.error");
            if (!r.Ok) { Refresh(); return; }
            App.Persist();
            handedIn = q.Id;
            Refresh();
        }

        /// <summary>The giver's thanks, sliding in at the top of the journal after a hand-in (no single-frame swap).</summary>
        private void TurnInCard(QuestDef q, VisualElement body)
        {
            var w = Ui.Panel(body, $"{q.Name} — complete!");
            w.AddToClassList("quest-turnin");
            w.AddToClassList("is-hidden");
            if (!string.IsNullOrEmpty(q.TurnIn)) Ui.Para($"\"{q.TurnIn}\"", w);
            Ui.Caption($"— {q.Giver}", w, "dim");
            Ui.Para("Received: " + Quests.RewardText(C, q), w, "gold");
            if (C.TryItem(q.RewardItem) is ItemDef tome && tome.Type == ItemType.Tome) TomeUi.Offer(w, tome); // GG-T301
            w.schedule.Execute(() => w.RemoveFromClassList("is-hidden")).ExecuteLater(30);
        }
    }
}
