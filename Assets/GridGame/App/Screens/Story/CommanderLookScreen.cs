using System;
using System.Linq;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T2127: the Founding's first question, "who are you?": the Commander's look (the two portrait variants, female
    /// commander left, male commander_male right) and pronouns (they/them, she/her, he/him; they/them by default), then
    /// Continue stores both on the save (<see cref="SaveData.CommanderLook"/>, <see cref="SaveData.CommanderPronouns"/>)
    /// and plays the Founding scene. Shown over the opening's backdrop, with no Back (the prologue goes one way).
    /// </summary>
    public sealed class CommanderLookScreen : GameScreen
    {
        public override string Id => "commander-look";

        private readonly Func<GameScreen> next;
        private readonly string backdrop;
        private HeroVariant look;
        private CommanderPronouns pronouns;
        private bool done;

        public CommanderLookScreen(Func<GameScreen> next, string backdrop)
        {
            this.next = next;
            this.backdrop = backdrop;
            look = App.Save?.CommanderLook ?? HeroVariant.Female;
            pronouns = App.Save?.CommanderPronouns ?? CommanderPronouns.TheyThem;
        }

        public override bool OnBack() => false;

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, backdrop, 0);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col center", root);
            var w = Ui.Window(null, col);
            w.name = "commander-look-window";
            w.style.width = Fit.Column - 2 * Kit.Gutter;
            var title = Ui.Display("Who are you?", w, "shadow gold", false);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            var hint = Ui.Para("Choose how you look and how the story speaks of you. Same skills, same story.", w, "dim");
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginBottom = Space.S16;

            var row = Ui.El("row", w);
            row.style.justifyContent = Justify.Center;
            foreach (var v in new[] { HeroVariant.Female, HeroVariant.Male })
            {
                bool on = v == look;
                var card = Ui.El("card", row);
                card.style.flexDirection = FlexDirection.Column;
                card.name = "commander-look-" + v.ToString().ToLowerInvariant();
                card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = Length.Percent(0); card.style.minWidth = 0;
                card.style.marginLeft = card.style.marginRight = Space.TargetHalfGap;
                card.style.alignItems = Align.Center;
                card.EnableInClassList("tap--selected", on); // R3: the chosen look is pressed in
                var art = Ui.El(null, card);
                art.pickingMode = PickingMode.Ignore;
                art.style.width = Length.Percent(100);
                art.style.height = Size.ArtH;
                art.style.backgroundImage = new StyleBackground(Art.PortraitFigure(Commander.PortraitOf(v)));
                art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var label = Ui.Name(v == HeroVariant.Female ? "Female" : "Male", card, on ? "gold" : null);
                label.pickingMode = PickingMode.Ignore;
                label.style.marginTop = Space.S8;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                var choice = v;
                Ui.Tappable(card, () => { look = choice; Refresh(); }, "ui.select");
            }

            var said = Ui.Name("Pronouns", w);
            said.style.marginTop = Space.S16;
            said.style.unityTextAlign = TextAnchor.MiddleCenter;
            var pick = Ui.El("row", w);
            pick.style.justifyContent = Justify.Center;
            var options = new[] { CommanderPronouns.TheyThem, CommanderPronouns.SheHer, CommanderPronouns.HeHim };
            var seg = Ui.Segmented(options.Select(Commander.Label).ToArray(), Array.IndexOf(options, pronouns), i => pronouns = options[i], pick, "commander-pronouns");
            seg.style.flexGrow = 1;
            for (int i = 0; i < options.Length; i++) seg[i].name = "commander-pronouns-" + options[i].ToString().ToLowerInvariant();

            var foot = Ui.El("kit-footer", w);
            foot.style.justifyContent = Justify.FlexEnd;
            foot.style.marginTop = Space.S16;
            RowIcon.IconButton("Continue", RowKind.Enter, Continue, "btn--primary", foot).name = "commander-look-continue";
        }

        private void Continue()
        {
            if (done) return;
            done = true;
            var s = App.Save;
            if (s != null)
            {
                s.CommanderLook = look;
                s.CommanderPronouns = pronouns;
                App.Persist();
            }
            App.Audio.Sfx("ui.confirm");
            App.Router.Replace(next());
        }
    }
}
