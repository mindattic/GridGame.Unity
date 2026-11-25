using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // The Inn's Respec (§25.6.3, GG-T53). The talent trees are retired (GG-T1081): until respec refunds skill board nodes
    // this screen is a stub.

    /// <summary>The Inn's Respec: a stub while respec moves onto the skill board.</summary>
    public sealed class RespecScreen : HubPage
    {
        public override string Id => "respec";
        protected override string Title => "Inn — Respec";
        protected override string Npc => CampNpcs.Innkeeper;

        protected override void BuildBody(VisualElement body)
        {
            var list = Ui.ListPanel(body, "Heroes", "respec-list");
            Ui.Para("Respec is moving to the skill board.", list, "dim");
        }
    }
}
