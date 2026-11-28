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
    /// <summary>The Enchantress's station: the Enchant recipes that used to be the Alchemist's second tab. Every
    /// enchant is Master tier and she has no ladder of her own to climb, so her recipes still unlock with the
    /// Alchemist's station level (her reagents are his brews); her header says how far off that is.</summary>
    public sealed class EnchantressScreen : HubPage
    {
        private RecipeDef selected;
        /// <summary>0 = Armor (GG-T179: bind a passive into a worn piece), 1 = Weapons (the elemental weapon enchants).</summary>
        private int tab;
        private ArmorEnchanting.Target piece;
        private ArmorEnchantDef enchant;
        /// <summary>GG-T2022: the enchant sheet is open over the worn armor, for <see cref="piece"/>.</summary>
        private bool sheetOpen;
        /// <summary>The sheet rises on the build right after it opens, not on every rebuild under it.</summary>
        private bool sheetRises;
        /// <summary>The first-enchant plan has been applied once on this visit.</summary>
        private bool planned;
        private VisualElement sheet;
        /// <summary>The enchant sheet's height, in percent of the stage.</summary>
        private const float SheetHeightPct = 72;
        /// <summary>The most of the stage the selected enchant's Needs block takes before it scrolls, in percent.</summary>
        private const float NeedsMaxPct = 26;
        public override string Id => "enchantress";
        protected override string Title => "Enchantress";
        protected override string Backdrop => "swamp";
        protected override string Subtitle => "Unlocks with the Alchemist: " + StationProgress.HeaderText(S, StationKind.Alchemist);
        protected override bool SubtitleInBody => true;
        protected override string Npc => CampNpcs.Enchantress;

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(new[] { "Armor", "Weapons" }, tab, i => { tab = i; sheetOpen = false; Refresh(); }, body);
            if (tab == 1) { AlchemistScreen.StationBody(body, CraftStation.Enchant, true, selected, r => selected = r, this); return; }
            ArmorBody(body);
        }

        public override bool OnBack()
        {
            if (!sheetOpen) return true;
            CloseSheet();
            return false;
        }

        /// <summary>Armor enchants (GG-T179, §25.0.8), in two steps (GG-T2022): pick a worn piece (select, then confirm),
        /// then its enchants open in a sheet with the Needs list and the Enchant button. Until the first enchant is done,
        /// the sheet opens on what her camp tip walks (<see cref="ArmorEnchanting.First"/>).</summary>
        private void ArmorBody(VisualElement body)
        {
            var pieces = ArmorEnchanting.Pieces(C, S);
            var plan = ArmorEnchanting.First(C, S);
            if (plan != null && !planned) { piece = plan.Target; enchant = plan.Enchant; sheetOpen = sheetRises = true; }
            planned = true;
            piece = piece != null ? pieces.FirstOrDefault(p => p.Hero == piece.Hero && p.Slot == piece.Slot) : null;
            if (piece == null) sheetOpen = false;

            // GG-T1873: one scroll region; GG-T2022: it holds the worn armor alone, in full rows.
            var plist = Ui.ListPanel(body, null, "enchant-list");
            Ui.Section("Worn armor", plist).name = "enchant-pieces";
            if (pieces.Count == 0)
                Ui.Empty("Nobody wears armor yet. Forge a piece at the Blacksmith, then bring it here.", plist);
            foreach (var t in pieces)
            {
                var target = t;
                var bound = ArmorEnchanting.PassiveOf(C, t.Piece.Enchant);
                var row = ItemUi.Row(t.Item, bound != null ? $"{HeroName(t)} ✦ {bound.Name}" : HeroName(t), () => OpenSheet(target), plist, true);
                row.name = t.RowName;
                if (piece != null && piece.Hero == t.Hero && piece.Slot == t.Slot) Ui.Select(row);
            }
            if (sheetOpen) EnchantSheet();
        }

        private string HeroName(ArmorEnchanting.Target t) => C.TryClass(t.Hero.ClassId)?.Name ?? t.Hero.ClassId;

        private void OpenSheet(ArmorEnchanting.Target target)
        {
            if (piece == null || piece.Hero != target.Hero || piece.Slot != target.Slot) enchant = null;
            piece = target;
            sheetOpen = sheetRises = true;
            Refresh();
        }

        /// <summary>Closes the sheet back to the worn armor; it drops away unless Reduce Motion is on.</summary>
        private void CloseSheet()
        {
            if (!sheetOpen) return;
            sheetOpen = false;
            App.Audio.Sfx("ui.back");
            var s = sheet;
            sheet = null;
            if (s == null || s.panel == null || App.Settings.ReduceMotion) { Refresh(); return; }
            s.parent.pickingMode = PickingMode.Ignore;
            float h = s.layout.height + GameApp.SafeBottom + Space.S16;
            App.StartCoroutine(Tween.Run(Timing.Quick, k =>
            {
                s.style.translate = new Translate(0, h * k);
                if (k >= 1f && !sheetOpen && s.panel != null) Refresh();
            }));
        }

        /// <summary>Step two (GG-T2022): the chosen piece's enchants in a sheet rising from the thumb zone over the dimmed
        /// armor list. What can be bound now comes first with the Enchant chip, the rest under "Need materials"; the
        /// selected enchant's Needs list and the Enchant button sit at the foot, under the thumb. It lives on this page's
        /// root, not the global Overlay, so the camp tip's coach marks find its rows and button on the stage.</summary>
        private void EnchantSheet()
        {
            var veil = Ui.El("fill veil", Root);
            veil.name = "enchant-veil";
            veil.pickingMode = PickingMode.Position;
            veil.RegisterCallback<ClickEvent>(e => { if (e.target == veil) CloseSheet(); });
            var w = sheet = Ui.Window(null, veil, "window--compact");
            w.name = "enchant-sheet";
            w.style.position = Position.Absolute;
            w.style.left = w.style.right = Fit.Inset + Fit.Edge;
            w.style.bottom = GameApp.SafeBottom + Space.S16;
            w.style.height = Fit.H(SheetHeightPct);

            var head = Ui.El("row", w);
            head.name = "enchant-sheet-header";
            head.style.alignItems = Align.Center;
            head.style.marginBottom = Space.S8;
            head.KindIcon(RowKind.Item, Art.ItemIcon(piece.Item.Id));
            var title = Ui.El("col grow", head);
            title.style.flexShrink = 1;
            Ui.Name($"{HeroName(piece)}'s {piece.Item.Name}", title);
            var old = C.TryArmorEnchant(piece.Piece.Enchant);
            Ui.Caption(old != null ? "Bound: " + old.Name : "No enchant yet", title, "dim");
            RowIcon.IconButton("Close", RowKind.Close, CloseSheet, "btn--small", head).name = "enchant-close";

            var scroll = Ui.List(w);
            scroll.name = "enchant-enchants";
            var list = scroll.contentContainer;
            bool Ready(ArmorEnchantDef e) => ArmorEnchanting.IsUnlocked(S, e) && ArmorEnchanting.HasInputs(S, e)
                && ArmorEnchanting.CanAfford(C, S, e) && piece.Piece.Enchant != e.Id;
            var all = ArmorEnchanting.All(C);
            foreach (var e in all.Where(Ready)) EnchantRow(list, scroll, e, true);
            if (all.Any(e => !Ready(e))) Ui.Section("Need materials", list).name = "enchants-need";
            foreach (var e in all.Where(e => !Ready(e))) EnchantRow(list, scroll, e, false);

            var needs = new ScrollView(ScrollViewMode.Vertical);
            needs.name = "enchant-needs";
            needs.AddToClassList("list");
            needs.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            needs.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            needs.style.flexGrow = 0;
            needs.style.flexShrink = 1;
            needs.style.maxHeight = Fit.H(NeedsMaxPct);
            needs.style.marginTop = Space.S16;
            w.Add(needs);
            Needs(needs.contentContainer);
            var btn = RowIcon.IconButton("Enchant", RowKind.Confirm, () =>
            {
                var r = ArmorEnchanting.Enchant(C, S, piece?.Hero, piece?.Slot ?? EquipmentSlot.Head, enchant);
                if (r.Ok) sheetOpen = false;
                Done(r);
            }, "btn--primary", Ui.Actions(w));
            btn.name = CampTips.CraftButton;
            Ui.SetEnabled(btn, enchant != null && Ready(enchant));
            Typography.Watch(w);

            if (!sheetRises) return;
            sheetRises = false;
            App.Audio.Sfx("ui.select");
            if (App.Settings.ReduceMotion) return;
            float h = Fit.H(SheetHeightPct) + GameApp.SafeBottom + Space.S16;
            w.style.translate = new Translate(0, h);
            App.StartCoroutine(Tween.Run(Timing.Standard, k => w.style.translate = new Translate(0, h * (1f - k)), Ease.Out));
        }

        private void EnchantRow(VisualElement list, ScrollView scroll, ArmorEnchantDef e, bool can)
        {
            var ench = e;
            bool locked = !ArmorEnchanting.IsUnlocked(S, e);
            var passive = C.TryAbility(e.Passive);
            var row = Ui.MenuRow(e.Name, locked ? $"🔒 {StationProgress.TierName(e.Tier)}" : Ui.Gold(ArmorEnchanting.Gold(C, S, e)),
                () => { enchant = ench; Refresh(); }, list);
            row.KindIcon(RowKind.Ability, passive != null ? Art.AbilityIcon(passive) : null); // GG-T746 (§26.7), else the Ability emblem
            row.name = ArmorEnchanting.EnchantRowPrefix + e.Id;
            if (can) BlacksmithScreen.ReadyChip(row, "Enchant");
            else row.style.opacity = Dim.Row;
            if (enchant != e) return;
            Ui.Select(row);
            bool scrolled = false;
            scroll.RegisterCallback<GeometryChangedEvent>(_ => { if (!scrolled && row.layout.height > 0) { scrolled = true; scroll.ScrollTo(row); } });
        }

        /// <summary>The selected enchant: what it binds, what it takes against what's in the bag, and the piece it goes into.</summary>
        private void Needs(VisualElement detail)
        {
            if (enchant == null) { Ui.Note("Pick an enchant to see what it needs.", detail); return; }
            var p = C.TryAbility(enchant.Passive);
            Ui.Heading($"{enchant.Name} — {p?.Name ?? enchant.Passive}", detail);
            GlossaryText.Create(p?.Description ?? "", "kit-text dim", detail);
            Ui.Section("Needs", detail);
            foreach (var ic in enchant.Inputs)
            {
                int have = S.Count(ic.Item);
                Ui.Para($"{(have >= ic.Count ? "✔" : "✘")} {C.TryItem(ic.Item)?.Name ?? ic.Item} ×{ic.Count}   (have {have})", detail, have >= ic.Count ? "good" : "bad");
            }
            int gold = ArmorEnchanting.Gold(C, S, enchant);
            Ui.Para($"{(S.Gold >= gold ? "✔" : "✘")} {Ui.Gold(gold)}" + (gold == 0 && enchant.Gold > 0 ? "  (first rune free)" : gold > 0 && gold < enchant.Gold ? "  (Enchanter's Wagon)" : ""), detail, S.Gold >= gold ? "good" : "bad");
            if (!ArmorEnchanting.IsUnlocked(S, enchant))
                Ui.Para($"🔒 Requires {StationProgress.TierName(enchant.Tier)} (alchemist is {StationProgress.LevelName(S, StationKind.Alchemist)})", detail, "bad");
            var old = C.TryArmorEnchant(piece.Piece.Enchant);
            if (old == enchant) Ui.Para($"The {HeroName(piece)}'s {piece.Item.Name} already holds {enchant.Name}.", detail, "dim");
            else Ui.Para($"Into the {HeroName(piece)}'s {piece.Item.Name}" + (old != null ? $" (replaces {old.Name})" : ""), detail, "gold");
        }
    }
}
