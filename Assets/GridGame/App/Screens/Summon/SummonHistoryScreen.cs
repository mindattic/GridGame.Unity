using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>The Summon history (§25.10.3, GG-T1430): the save's last <see cref="Gacha.HistoryKeep"/> banner pulls,
    /// newest first, each a card (<c>summon-history-{i}</c>) with the hero, what the pull gave, when, on which banner and
    /// what paid for it. Reached from a banner tab's <b>History</b> button.</summary>
    public sealed class SummonHistoryScreen : HubPage
    {
        public override string Id => "summon-history";
        protected override string Title => "Summon history";
        protected override string Backdrop => "cityruins";

        protected override void BuildBody(VisualElement body)
        {
            var pulls = S.Summon?.History ?? new List<PullRecord>();
            var list = Ui.ListPanel(body, pulls.Count == 0 ? "Pulls" : $"Last {pulls.Count} pulls", "summon-history");
            if (pulls.Count == 0)
            {
                var state = Ui.EmptyState(list, RowKind.Summon, "No pulls yet. Each banner pull is kept here, newest first.");
                RowIcon.IconButton("Summon", RowKind.Summon, () => App.Router.Pop(), "btn--small", Ui.Actions(state)).name = "summon-history-summon";
                return;
            }
            for (int i = 0; i < pulls.Count; i++)
            {
                var p = pulls[i];
                var def = C.TryClass(p.ClassId);
                var banner = C.TryBanner(p.BannerId);
                bool featured = SummonReveal.IsFeatured(banner, p);
                Ui.Card(list, def?.Id, def?.Name ?? p.ClassId, out var col, "summon-history-" + i);
                Ui.Caption(SummonReveal.Badge(p, featured), col, p.Result == PullResult.New ? "gold" : "good");
                string when = p.Utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                string paid = string.IsNullOrEmpty(p.Paid) ? "" : " · " + p.Paid;
                Ui.Caption($"{when} · {banner?.Name ?? p.BannerId ?? "Standard"}{paid}", col, "dim");
            }
        }
    }
}
