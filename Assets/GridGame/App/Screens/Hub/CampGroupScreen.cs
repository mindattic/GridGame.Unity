using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>The hub's Craft or Journal group (GG-T1613): that group's CampMenu destinations as tiles, named and opened as
    /// they were on the hub's flat grid.</summary>
    public sealed class CampGroupScreen : HubPage
    {
        private readonly CampGroup group;
        public CampGroupScreen(CampGroup group) { this.group = group; }

        public override string Id => "camp-group-" + group.ToString().ToLowerInvariant();
        protected override string Title => group.ToString();
        protected override string Backdrop => HubScreen.ThemeFor(C, S);

        protected override void BuildBody(VisualElement body)
        {
            var grid = Ui.El("kit-pillar", body);
            grid.name = "camp-group-tiles";
            foreach (var d in CampMenu.In(CampMenu.Build(C, S, App.Settings), group)) HubScreen.Tile(grid, d);
        }
    }
}
