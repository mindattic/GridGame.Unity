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
    /// <summary>The Aetherist's counter (GG-T624): buy Aetherite Dust with gold, refine it into Aetherite Shards. The
    /// only station whose two actions are both currency exchanges — no recipes, no gear.</summary>
    public sealed class AetheristScreen : HubPage
    {
        public override string Id => "aetherist";
        protected override string Title => "Manasmith";
        protected override string Backdrop => "desert";
        protected override string Npc => CampNpcs.Aetherist;

        private int buyAmount = 10;
        private int refineAmount = 1;

        protected override void BuildBody(VisualElement body)
        {
            int dust = DustEconomy.Have(S);
            int shards = DiamondExchange.Have(S);
            int maxBuy = DustEconomy.MaxBuy(C, S);
            int maxRefine = DustEconomy.MaxRefine(C, S);
            buyAmount = Math.Max(1, buyAmount);
            refineAmount = Math.Max(1, refineAmount);

            var bal = Ui.Panel(body, null, false, "aetherist-balance");
            Ui.Field(DustEconomy.DustName, dust.ToString("N0"), bal, null, "aetherist-dust-count");
            Ui.Field(ShardText.Plural, ShardText.ShortOf(shards), bal, null, "aetherist-shard-count");
            var greet = ShopBark(C, "aetherist/greet");
            if (greet != null) Ui.Para(greet, bal, "dim");

            var buy = Ui.Panel(body, "Buy Dust with gold", false, "aetherist-buy-panel");
            var pick = Ui.Actions(buy);
            pick.style.alignItems = Align.Center;
            var buyCount = Ui.Stepper(pick, buyAmount.ToString("N0"), () => { buyAmount = Math.Max(1, buyAmount - 1); Refresh(); }, () => { buyAmount += 1; Refresh(); }, out var buyMinus, out var buyPlus, "gold");
            buyMinus.name = "aetherist-buy-minus";
            buyCount.name = "aetherist-buy-amount";
            buyPlus.name = "aetherist-buy-plus";
            Ui.SetEnabled(buyMinus, buyAmount > 1);
            var maxBuyBtn = RowIcon.IconButton("Max", RowKind.Station, () => { buyAmount = Math.Max(1, maxBuy); Refresh(); }, "btn--small", pick);
            maxBuyBtn.name = "aetherist-buy-max";
            Ui.SetEnabled(maxBuyBtn, maxBuy > 0);
            Ui.Field("Costs", Ui.Gold(DustEconomy.GoldFor(buyAmount)), buy, S.Gold >= DustEconomy.GoldFor(buyAmount) ? null : "bad", "aetherist-buy-quote");
            var buyBtn = RowIcon.IconButton($"Buy {buyAmount:N0} {DustEconomy.DustName}", RowKind.Confirm, () => Done(DustEconomy.Buy(C, S, buyAmount), true, "ui.buy"), "btn--primary", buy);
            buyBtn.name = "aetherist-buy";
            Ui.SetEnabled(buyBtn, buyAmount <= maxBuy);

            var refine = Ui.Panel(body, "Refine into " + ShardText.Plural, false, "aetherist-refine-panel");
            var rpick = Ui.Actions(refine);
            rpick.style.alignItems = Align.Center;
            var refineCount = Ui.Stepper(rpick, refineAmount.ToString("N0"), () => { refineAmount = Math.Max(1, refineAmount - 1); Refresh(); }, () => { refineAmount += 1; Refresh(); }, out var refineMinus, out var refinePlus, "gold");
            refineMinus.name = "aetherist-refine-minus";
            refineCount.name = "aetherist-refine-amount";
            refinePlus.name = "aetherist-refine-plus";
            Ui.SetEnabled(refineMinus, refineAmount > 1);
            var maxRefineBtn = RowIcon.IconButton("Max", RowKind.Station, () => { refineAmount = Math.Max(1, maxRefine); Refresh(); }, "btn--small", rpick);
            maxRefineBtn.name = "aetherist-refine-max";
            Ui.SetEnabled(maxRefineBtn, maxRefine > 0);
            Ui.Field("Needs", $"{refineAmount * DustEconomy.DustPerShard:N0} {DustEconomy.DustName}", refine,
                dust >= refineAmount * DustEconomy.DustPerShard ? null : "bad", "aetherist-refine-quote");
            var refineBtn = RowIcon.IconButton($"Refine {ShardText.ShortOf(refineAmount)}", RowKind.Confirm, () =>
            {
                string occasion = dust < maxRefine * DustEconomy.DustPerShard ? "aetherist/short" : "aetherist/refine";
                var r = DustEconomy.Refine(C, S, refineAmount);
                var bark = r.Ok ? ShopBark(C, occasion) : null;
                Done(r, true, "ui.diamond");
                if (bark != null) App.Toast(bark, 2.6f);
            }, "btn--primary", refine);
            refineBtn.name = "aetherist-refine";
            Ui.SetEnabled(refineBtn, refineAmount <= maxRefine);


            Ui.Note($"{DustEconomy.DustPerShard} {DustEconomy.DustName} refines into 1 {ShardText.Short}. {DustEconomy.DustName} costs {DustEconomy.GoldPerDust} G a piece here — " +
                $"buying {ShardText.Plural} outright at the {ShardText.Store} is the better rate; Dust is the free-play path, earned in battle.", body);
        }

        /// <summary>A random line for this shop occasion, or null with nothing authored for it yet.</summary>
        private static string ShopBark(ContentDb c, string key) =>
            c != null && c.ShopBarks.TryGetValue(key, out var lines) && lines.Count > 0 ? lines[UnityEngine.Random.Range(0, lines.Count)] : null;
    }
}
