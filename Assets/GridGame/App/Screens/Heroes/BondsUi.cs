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
    /// pair lines, so every bond reads without a debug tool.</summary>
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

        /// <summary>The Party tab's cohesion: one line per party pair above tier 0 ("Knight + Cleric · tier 2", gold from the
        /// Duo tier), strongest first, under a Cohesion header. Nothing while every pair is still at tier 0 (GG-T1970: the
        /// grid of zeros it replaces took a third of the page).</summary>
        public static void PairLines(ContentDb c, SaveData s, VisualElement parent)
        {
            var ids = s?.Party?.Where(id => Bonds.HasCohesion(s, id)).ToList();
            if (ids == null || ids.Count < 2) return;
            var pairs = ids.SelectMany((a, i) => ids.Skip(i + 1).Select(b => (a, b, tier: Bonds.CohesionTier(s, a, b))))
                .Where(p => p.tier > 0)
                .OrderByDescending(p => p.tier).ToList();
            if (pairs.Count == 0) return;
            Ui.Section("Cohesion", parent).name = "party-cohesion";
            foreach (var (a, b, tier) in pairs)
            {
                var line = Ui.Caption($"{c.TryClass(a)?.Name ?? a} + {c.TryClass(b)?.Name ?? b} · tier {tier} of {BondsConfig.MaxTier}",
                    parent, tier >= BondsConfig.DuoTier ? "gold" : "good");
                line.name = "party-pair-" + Bonds.PairKey(a, b);
            }
        }
    }
}
