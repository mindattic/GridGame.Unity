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
    // GG-T119: enchanting moved to the Enchantress's own station, and resting is the Innkeeper's (the old Heal tab
    // was the Inn under another name, which would have let the Alchemist open it before the Innkeeper joined).
    public sealed class AlchemistScreen : HubPage
    {
        private RecipeDef selected;
        public override string Id => "alchemist";
        protected override string Title => "Alchemist";
        protected override string Backdrop => "swamp";
        protected override string Subtitle => $"{Facilities.Current(C, S, CraftStation.Alchemy)} · {StationProgress.HeaderText(S, StationKind.Alchemist)}";
        protected override bool SubtitleInBody => true;
        protected override string Npc => CampNpcs.Alchemist;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T176 (§25.0.5): on the first visit his camp tip walks the first brew, so that bottle recipe opens selected.
            if (selected == null && CampTips.Pending(S, Id) != null) selected = Brewing.FirstBrew(C, S);
            StationBody(body, CraftStation.Alchemy, false, selected, r => selected = r, this);
        }

        /// <summary>A recipe list over a detail pane — the Alchemist's Brew and the Enchantress's Enchant share it.</summary>
        internal static void StationBody(VisualElement body, CraftStation station, bool upgrades, RecipeDef selected, Action<RecipeDef> onPick, HubPage page)
        {
            var list = Ui.ListPanel(body);
            var detail = Ui.DetailPanel(body);
            BlacksmithScreen.RecipeList(list, detail, GameApp.I.Content, GameApp.I.Save, station, upgrades, selected, onPick, page);
        }
    }
}
