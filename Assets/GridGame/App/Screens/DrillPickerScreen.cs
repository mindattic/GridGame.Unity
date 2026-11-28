using System.Collections.Generic;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1648: Settings > How to play > Play a drill. One row per campaign.json Tutorial drill; a row replaces this
    /// screen with <see cref="BattleScreen.Practice"/>, so the drill card's Back to Settings lands on Settings.
    /// </summary>
    public sealed class DrillPickerScreen : GameScreen
    {
        public override string Id => "drill-picker";

        protected override void Build(VisualElement root)
        {
            var col = Ui.El("fill col", root);
            Ui.Header("Play a Drill", () => App.Router.Back(), col, out _);
            var list = Ui.ListPanel(Ui.PageBody(col), null, "drill-list");
            var c = App.Content;
            var drills = c.Campaign?.Tutorial ?? new List<string>();
            for (int i = 0; i < drills.Count; i++)
            {
                var stage = c.TryStage(drills[i]);
                if (stage == null) continue;
                string id = stage.Id;
                var row = Ui.MenuRow($"Drill {i + 1} of {drills.Count}", stage.Name, () => App.Router.Replace(BattleScreen.Practice(id)), list);
                row.name = "drill-" + id;
                row.KindIcon(RowKind.Stage);
            }
            if (list.childCount == 0) Ui.EmptyState(list, RowKind.Stage, "No drills in this build yet. Back returns to Settings.", "drill-empty");
        }
    }
}
