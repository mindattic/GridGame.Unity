using System.Linq;
using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1798, test-only (automation <c>devbreeds</c>): every enemy class whose art has a breed mask, one row each, one
    /// portrait per breed in breeds.json, all drawn through the live path (<see cref="Ui.PortraitThumb"/> with a breed, so
    /// <see cref="BreedArt"/> bakes them with the real BreedRecolor shader). A visual check of the masks and ramps.
    /// </summary>
    public sealed class BreedGalleryScreen : GameScreen
    {
        public override string Id => "breedgallery";

        protected override void Build(VisualElement root)
        {
            root.style.backgroundColor = new Color(0.07f, 0.07f, 0.10f);
            root.style.paddingTop = root.style.paddingLeft = root.style.paddingRight = Space.TargetHalfGap;
            var db = GameApp.I.Content;
            var breeds = db.BreedOrder.Where(b => b != null).ToList();
            var classes = db.Classes.Values
                .Where(c => !c.HasTag("Boss") && !c.HasTag("Remains") && db.HasBreedMask != null && db.HasBreedMask(c.Id))
                .OrderBy(c => c.Id).ToList();

            var title = new Label($"Breeds: {classes.Count} masked classes x {breeds.Count} breeds") { name = "breed-title" };
            title.style.color = Color.white;
            title.style.fontSize = 28;
            title.style.marginBottom = Space.S8;
            root.Add(title);

            var head = Row();
            head.Add(Cell(""));
            foreach (var b in breeds) head.Add(Cell(b.Id));
            root.Add(head);
            foreach (var c in classes)
            {
                var row = Row();
                row.Add(Cell(c.Id));
                foreach (var b in breeds)
                {
                    var host = new VisualElement { name = $"breed-{c.Id}-{b.Id}" };
                    host.style.flexGrow = 1;
                    host.style.flexBasis = 0;
                    host.style.height = Size.Medallion;
                    host.style.marginLeft = host.style.marginRight = Space.S2;
                    host.style.backgroundColor = new Color(0.13f, 0.13f, 0.17f);
                    Ui.PortraitThumb(host, c.Id, useThumb: false, breed: b.IsPlain ? null : b.Id);
                    row.Add(host);
                }
                root.Add(row);
            }
        }

        private static VisualElement Row()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            r.style.marginBottom = Space.S4;
            return r;
        }

        private static Label Cell(string text)
        {
            var l = new Label(text);
            l.style.color = new Color(0.9f, 0.88f, 0.84f);
            l.style.fontSize = 18;
            l.style.flexGrow = 1;
            l.style.flexBasis = 0;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }
    }
}
