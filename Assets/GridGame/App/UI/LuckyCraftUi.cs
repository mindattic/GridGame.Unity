using System;
using System.Collections;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>The Forge's Diamond controls (§24.6.5, GG-T235): the way to the Diamond Store, and a lucky craft that spends
    /// <see cref="Crafting.LuckyDiamondCost"/> Diamonds (or their gold, GG-T455) for a far better Masterwork chance, revealed
    /// over time.</summary>
    public static class LuckyCraftUi
    {
        public static void Section(VisualElement w, ContentDb c, SaveData s, RecipeDef r, bool craftable, Action refresh)
        {
            if (r.Station != CraftStation.Forge || r.ResultCount != 1 || !(c.TryItem(r.Result) is ItemDef result)
                || result.Type != ItemType.Equipment || result.Durability <= 0) return;
            var box = Ui.El("col", w);
            box.name = "lucky-craft";
            box.style.marginTop = Space.S16;
            var head = Ui.El("row", box);
            // GG-T745 (§26.7): the Diamond's pixel icon, else the Diamond emblem.
            var img = head.KindIcon(RowKind.Diamond, Art.ItemIcon(Crafting.DiamondId));
            img.style.width = img.style.height = Size.IconS; img.style.marginRight = Space.S8;
            Ui.Text($"{ShardText.ShortPlural}: {Ui.Count(DiamondExchange.Have(s))}", "small", head);

            // GG-T455: the old 600 g one-Diamond job is retired; Diamonds come from the store's gold exchange.
            var buy = RowIcon.IconButton(ShardText.Store, RowKind.Diamond, DiamondStores.Open, "btn--small", box); // GG-T745
            buy.name = "buy-diamond";
            buy.style.marginTop = Space.S8;

            // The odds, disclosed before purchase (GG-T503): the Masterwork chance without and with the Diamond, from the
            // same Crafting.MasterworkChance the craft rolls against.
            var (plain, withDiamond) = Crafting.MasterworkOdds(c, s);
            var odds = Ui.Para(Crafting.LuckyOddsLine(c, s), box, "gold");
            odds.name = "lucky-odds";
            odds.style.marginTop = Space.S8;

            var allowed = LootBoxPolicy.AllowsLuckyCraft(); // GG-T1433
            if (!allowed.Ok)
            {
                Ui.Para(allowed.Message, box, "dim").name = "lucky-off";
                return;
            }

            bool can = Crafting.CanLucky(c, s, r);
            var lucky = RowIcon.IconButton($"Lucky craft · {DiamondExchange.Label(Crafting.LuckyDiamondCost)} · ★ {OddsText.Percent(withDiamond)}", RowKind.Confirm, () =>
            {
                if (!Crafting.CanLucky(c, s, r)) { DiamondStores.OfferStore(DiamondExchange.CanPay(s, Crafting.LuckyDiamondCost, r.Gold).Message); return; }
                var (qd, qg) = DiamondExchange.Quote(s, Crafting.LuckyDiamondCost);
                string pay = qg == 0 ? ShardText.Of(qd) : qd == 0 ? Ui.Gold(qg) : $"{ShardText.Of(qd)} and {Ui.Gold(qg)}";
                Confirm.Show($"Pay {pay} for a lucky craft? Masterwork chance {OddsText.Percent(withDiamond)} (without the {ShardText.ShortPlural} {OddsText.Percent(plain)}).", "Lucky craft", () =>
                {
                    var res = Forging.Forge(c, s, r, lucky: true);
                    if (res.Ok) GameApp.I.Persist();
                    Reveal(res, refresh);
                });
            }, can ? "btn--primary" : null, box); // GG-T745: led by the Confirm emblem
            lucky.name = "lucky-craft-btn";
            lucky.style.marginTop = Space.S8;
            // Short of Diamonds and gold it stays tappable and offers the store; it's disabled only when the craft itself isn't.
            Ui.SetEnabled(lucky, craftable);
        }

        /// <summary>The diamond spins up in a veil, then the result lands: a Masterwork with a fanfare, else a softer cue.</summary>
        private static void Reveal(Result res, Action refresh)
        {
            var app = GameApp.I;
            if (!res.Ok)
            {
                app.Toast(res.Message);
                app.Audio.Sfx("ui.error");
                refresh();
                return;
            }
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = "lucky-reveal";
            veil.pickingMode = PickingMode.Position;
            var gem = Ui.El(null, veil);
            gem.style.width = gem.style.height = Size.Gem;
            var tex = Art.ItemIcon(Crafting.DiamondId);
            if (tex != null) gem.style.backgroundImage = new StyleBackground(tex);
            var text = Ui.Text("", "h2", veil);
            text.style.marginTop = Space.S24;
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.opacity = 0f;
            app.StartCoroutine(Play(gem, text, veil, res, refresh));
        }

        private static IEnumerator Play(VisualElement gem, Label text, VisualElement veil, Result res, Action refresh)
        {
            var app = GameApp.I;
            app.Audio.Sfx("element.arcane");
            yield return Tween.Run(Timing.Cinematic, t =>
            {
                gem.style.rotate = new Rotate(new Angle(t * 720f));
                float k = 0.6f + 0.6f * t;
                gem.style.scale = new Scale(new Vector3(k, k, 1f));
            }, Tween.EaseInOut);
            bool masterwork = res.Message != null && res.Message.StartsWith("★");
            app.Audio.Sfx(masterwork ? "battle.powerup" : "battle.clang");
            text.text = res.Message;
            yield return Tween.All(app, Tween.Fade(text, 0f, 1f, 0.3f), Tween.Pop(gem, masterwork ? 1.4f : 1.1f, Timing.Emphasis));
            yield return Tween.Wait(Timing.Cinematic);
            yield return Tween.Fade(veil, 1f, 0f, 0.25f);
            veil.RemoveFromHierarchy();
            refresh();
        }
    }
}
