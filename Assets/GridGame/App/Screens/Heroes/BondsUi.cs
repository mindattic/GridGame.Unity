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
    /// <summary>Trust and cohesion read on the Hero screen: the Status tab's Trust meter and top bonds, and the Party tab's
    /// cohesion grid, so every bond reads without a debug tool.</summary>
    internal static class BondsUi
    {
        private const int TopBonds = 3;

        /// <summary>The Status tab's Trust block (value, tier pips, the next tier's payoff) and the hero's three strongest
        /// bonds. Nothing for a hero without Trust (a domesticated monster, a mercenary).</summary>
        public static void TrustAndBonds(ContentDb c, SaveData s, string classId, VisualElement parent)
        {
            if (!Bonds.HasTrust(s, classId)) return;
            var block = Ui.El("col", parent);
            block.name = "status-trust";
            Ui.Section("Trust", block);
            int trust = Bonds.Trust(s, classId), tier = Bonds.TrustTier(s, classId);
            var head = Ui.El("row", block);
            head.style.alignItems = Align.Center;
            Ui.Caption($"Tier {tier}  ·  {trust} / {BondsConfig.Max}", head, "gold").style.marginRight = Space.S16;
            TierPips(tier, head);
            var meter = Ui.Gauge(trust / (float)BondsConfig.Max, block).Q("fill");
            meter.RemoveFromClassList("gauge-fill--mid"); meter.RemoveFromClassList("gauge-fill--low"); // low Trust is not a warning
            Ui.Para(NextTrustPayoff(tier), block, "dim").name = "status-trust-next";

            var bondsBlock = Ui.El("col", parent);
            bondsBlock.name = "status-bonds";
            Ui.Section("Bonds", bondsBlock);
            var top = s.Roster.Select(x => x.ClassId)
                .Where(id => Bonds.Key(id) != Bonds.Key(classId) && Bonds.HasCohesion(s, id))
                .Select(id => (id, value: Bonds.Cohesion(s, classId, id)))
                .Where(p => p.value > 0)
                .OrderByDescending(p => p.value).ThenBy(p => p.id)
                .Take(TopBonds).ToList();
            if (top.Count == 0) { Ui.Para("No bonds yet. Heroes grow closer pincering together and healing, shielding or buffing each other.", bondsBlock, "dim"); return; }
            var row = Ui.El("row", bondsBlock);
            row.style.alignItems = Align.FlexStart;
            foreach (var (id, value) in top)
            {
                var col = Ui.El("col", row);
                col.name = "status-bond-" + id;
                col.style.flexGrow = 1; col.style.flexBasis = 0; col.style.minWidth = 0;
                col.style.alignItems = Align.Center;
                Ui.Thumb(id, col, Kit.Avatar);
                var name = Ui.Marquee(Ui.Caption(c.TryClass(id)?.Name ?? id, col), grow: false);
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                Ui.Caption($"Tier {Bonds.Tier(value)}", col, Bonds.Tier(value) > 0 ? "gold" : "dim");
            }
        }

        /// <summary>What the next Trust tier pays, in one line (the top tier says what it holds).</summary>
        private static string NextTrustPayoff(int tier)
        {
            string perTier = $"+{BondsConfig.CritPerTrustTier * 100f:0}% crit and +{BondsConfig.EvasionPerTrustTier * 100f:0}% evasion";
            if (tier >= BondsConfig.MaxTier)
                return $"Highest tier: {perTier} a tier, and Resolve (survive one lethal blow a battle).";
            int next = tier + 1;
            var parts = new List<string> { perTier };
            if (next == BondsConfig.ResolveTier) parts.Add("Resolve (survive one lethal blow a battle)");
            if (Array.IndexOf(BondsConfig.TrustSceneTiers, next) >= 0) parts.Add("a Trust scene");
            return $"Next: tier {next} at {Bonds.Floor(next)} Trust: {string.Join(", ", parts)}.";
        }

        /// <summary>One pip per tier, the reached ones lit gold.</summary>
        private static void TierPips(int tier, VisualElement parent)
        {
            var row = Ui.El("row", parent);
            row.pickingMode = PickingMode.Ignore;
            for (int t = 1; t <= BondsConfig.MaxTier; t++)
            {
                var p = Ui.Text("", "pip", row);
                p.style.backgroundColor = t <= tier ? Palette.Gold : Palette.Ink.WithA(0.6f);
                if (t > tier) p.style.opacity = 0.5f;
            }
        }

        /// <summary>The Party tab's cohesion grid: the party down the side and across the top, each cell the pair's cohesion
        /// tier (gold from the Duo tier). Nothing for a party of fewer than two heroes with cohesion; one line while every
        /// pair is still at tier 0 (GG-T1891: a grid of zeros took a third of the page).</summary>
        public static void CohesionGrid(ContentDb c, SaveData s, VisualElement body)
        {
            var ids = s?.Party?.Where(id => Bonds.HasCohesion(s, id)).ToList();
            if (ids == null || ids.Count < 2) return;
            if (!ids.Any(a => ids.Any(b => a != b && Bonds.CohesionTier(s, a, b) > 0)))
            {
                Ui.Caption($"Party cohesion: every pair at tier 0 of {BondsConfig.MaxTier}", Ui.Panel(body, null, false, "party-cohesion"), "dim");
                return;
            }
            var panel = Ui.Panel(body, "Party cohesion", false, "party-cohesion");
            VisualElement Cell(VisualElement row)
            {
                var cell = Ui.El("col", row);
                cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.minWidth = 0;
                cell.style.alignItems = Align.Center; cell.style.justifyContent = Justify.Center;
                cell.style.height = Size.IconM + Space.S8;
                return cell;
            }
            var header = Ui.El("row", panel);
            Cell(header);
            foreach (var id in ids) Ui.Thumb(id, Cell(header), Size.IconM);
            foreach (var a in ids)
            {
                var row = Ui.El("row", panel);
                row.name = "party-cohesion-" + a;
                Ui.Thumb(a, Cell(row), Size.IconM);
                foreach (var b in ids)
                {
                    var cell = Cell(row);
                    if (a == b) { Ui.Caption("—", cell, "dim"); continue; }
                    int t = Bonds.CohesionTier(s, a, b);
                    Ui.Caption(t.ToString(), cell, t >= BondsConfig.DuoTier ? "gold" : t > 0 ? "good" : "dim");
                }
            }
            Ui.Caption($"Cohesion tier of each pair, 0 to {BondsConfig.MaxTier}", panel, "dim");
        }
    }
}
