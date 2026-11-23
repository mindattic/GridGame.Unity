using System;
using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>A STORY.md faction (factions.json, GG-T342) and the hero classes who belong to it. A wanderer talked to by a
    /// companion of their own faction is easier to win over (Meta <c>Wanderers.Judge</c>), and a faction's Wanderer talk
    /// (<see cref="DialogueTriggerKind.Wanderer"/> with this <see cref="Id"/>) speaks for every hero in it without a scene
    /// of their own.</summary>
    public sealed class FactionDef
    {
        public string Id;
        /// <summary>As the game says it: "the Lantern Road".</summary>
        public string Name;
        public List<string> Heroes = new List<string>();
        /// <summary>GG-T387: the campaign themes (regions) this faction's people roam. A wanderer met in one of them is one of
        /// its own heroes while any are left to meet (Meta <c>Wanderers.Roll</c>): "random Hero NPCs from that area".</summary>
        public List<string> Regions = new List<string>();
    }

    public static class WandererContent
    {
        /// <summary>factions.json: known hero classes, each in at most one faction, no duplicate ids; each region a campaign
        /// theme claimed by at most one faction (GG-T387).</summary>
        public static void ValidateFactions(ContentDb db, List<string> p)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var member = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var roams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in db.Factions)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.Id)) { p.Add("faction with empty Id"); continue; }
                string w = $"faction {f.Id}";
                if (!ids.Add(f.Id)) p.Add($"{w}: duplicate id");
                if (string.IsNullOrWhiteSpace(f.Name)) p.Add($"{w}: needs a Name");
                if (db.TryClass(f.Id) != null) p.Add($"{w}: id clashes with a class id");
                foreach (var h in f.Heroes)
                {
                    if (db.TryClass(h)?.Team != Team.Hero) { p.Add($"{w}: '{h}' is not a hero class"); continue; }
                    if (member.TryGetValue(h, out var other)) p.Add($"{w}: '{h}' is already in faction {other}");
                    else member[h] = f.Id;
                }
                foreach (var r in f.Regions ?? new List<string>())
                {
                    if (!db.Campaign.Themes.Exists(t => string.Equals(t.Id, r, StringComparison.OrdinalIgnoreCase))) { p.Add($"{w}: region '{r}' is not a campaign theme"); continue; }
                    if (roams.TryGetValue(r, out var other)) p.Add($"{w}: region '{r}' already belongs to faction {other}");
                    else roams[r] = f.Id;
                }
            }
        }

        /// <summary>A Wanderer talk's shape (GG-T342; the game of wits since GG-T387): Id is a hero class, a faction or none;
        /// no effects (the rule, not the script, decides); exactly <see cref="DialogueDef.WitsExchanges"/> questions, each
        /// with three unconditional replies that all go on to the same node, one per <see cref="DialogueChoice.Wit"/> (+1, 0,
        /// -1); a node that goes on to <see cref="DialogueDef.OutcomeNext"/>; and a <see cref="DialogueDef.JoinNode"/> and a
        /// <see cref="DialogueDef.PartNode"/> that end it.</summary>
        public static void ValidateDialogue(ContentDb db, DialogueDef d, List<string> p)
        {
            string w = $"dialogue {d.Id}";
            var id = d.Trigger?.Id;
            if (id != null && db.TryClass(id)?.Team != Team.Hero && !db.Factions.Exists(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase)))
                p.Add($"{w}: Wanderer needs a hero class, a faction or no Id, has '{id}'");
            if (d.AllEffects().GetEnumerator().MoveNext()) p.Add($"{w}: a Wanderer talk can't have effects (the understanding rule decides)");
            var asks = d.Nodes.FindAll(n => n != null && n.Choices.Count > 0);
            if (asks.Count != DialogueDef.WitsExchanges) p.Add($"{w}: a Wanderer talk needs {DialogueDef.WitsExchanges} exchanges (questions), has {asks.Count}");
            foreach (var ask in asks)
            {
                string aw = $"{w} node {ask.Id}";
                if (ask.Choices.Count != 3) { p.Add($"{aw}: an exchange needs three replies, has {ask.Choices.Count}"); continue; }
                foreach (int wit in new[] { 1, 0, -1 })
                {
                    int n = ask.Choices.FindAll(ch => ch != null && ch.Wit == wit).Count;
                    if (n != 1) p.Add($"{aw}: an exchange needs exactly one reply with Wit {wit:+0;-0;0}, has {n}");
                }
                if (ask.Choices.Exists(ch => ch != null && (ch.Wit < -1 || ch.Wit > 1))) p.Add($"{aw}: a reply's Wit is +1, 0 or -1");
                if (ask.Choices.Exists(ch => ch != null && ch.Conditions.Count > 0)) p.Add($"{aw}: the replies can't be conditional");
                var next = ask.Choices[0]?.Next;
                if (next == null || ask.Choices.Exists(ch => ch == null || !Is(ch.Next, next))) p.Add($"{aw}: every reply must go on to the same node (the standing, not the script, keeps score)");
            }
            if (!d.Nodes.Exists(n => n != null && Is(n.Next, DialogueDef.OutcomeNext))) p.Add($"{w}: a node must go on to \"{DialogueDef.OutcomeNext}\"");
            foreach (var end in new[] { DialogueDef.JoinNode, DialogueDef.PartNode })
            {
                var n = d.Nodes.Find(x => Is(x?.Id, end));
                if (n == null) p.Add($"{w}: needs a '{end}' node");
                else if (n.Next != null || n.Choices.Count > 0) p.Add($"{w}: '{end}' must end the talk");
            }
        }

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
