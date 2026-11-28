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
    public sealed class BestiaryScreen : HubPage
    {
        /// <summary>The tile behind an unseen creature's black silhouette: light enough that the shape reads against it.</summary>
        internal static readonly Color UnseenTile = new Color(0.45f, 0.50f, 0.60f, 1f);
        private string selected;
        public override string Id => "bestiary";
        protected override string Title => "Bestiary";

        /// <summary>The creature the detail shows (GG-T925: the Codex opens a linked foe here).</summary>
        internal string Selected { get => selected; set => selected = value; }

        protected override void BuildBody(VisualElement body) => BuildGrid(body);

        /// <summary>The grid and detail into <paramref name="body"/>: this screen's own body, and the Codex's Foes tab
        /// (GG-T925), which embeds it rather than copying it. <paramref name="onPick"/> hears each creature opened;
        /// <paramref name="isNew"/> puts the New badge on a seen creature's card until it is opened.</summary>
        internal void BuildGrid(VisualElement body, Action<string> onPick = null, Func<string, bool> isNew = null)
        {
            var enemies = C.Classes.Values.Where(c => c.Team == Team.Enemy).OrderBy(c => c.Name).ToList();
            int seen = enemies.Count(e => S.Bestiary.TryGetValue(e.Id, out var b) && b.Seen);
            var grid = Ui.ListPanel(body, $"Seen {seen}/{enemies.Count}", out var scroll, "bestiary-grid");
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            var detail = Ui.DetailPanel(body);
            foreach (var e in enemies)
            {
                var def = e;
                S.Bestiary.TryGetValue(e.Id, out var entry);
                var t = Ui.Thumb(e.Id, grid, 160, thumbOnly: true); // GG-T1210: 100+ cards, baked thumbs only
                t.name = "beast-" + e.Id;
                if (entry == null || !entry.Seen) { t.style.backgroundColor = UnseenTile; t.Q(Ui.ThumbArt).style.unityBackgroundImageTintColor = new Color(0, 0, 0, 0.92f); } // GG-T1889: a black silhouette on a light slate tile (over 4:1), not on the dark card
                else if (isNew != null && isNew(e.Id)) RowIcon.Badge(t, RowKind.New);
                t.AddToClassList("card-tap");
                Ui.Tappable(t, () => { selected = def.Id; Show(detail, def, entry); RowIcon.ClearBadge(t); onPick?.Invoke(def.Id); });
            }
            grid.style.flexDirection = FlexDirection.Column;
            grid.style.flexWrap = Wrap.NoWrap;
            var cells = grid.Children().ToList();
            int laidCols = -1;
            // Listen on the viewport, not the ScrollView: the vertical scroller appears only after the cells fill it,
            // shrinking the viewport without resizing the ScrollView, so a ScrollView listener never re-lays the grid.
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                // The content viewport (inside the ScrollView's border, padding and scroller) is what the rows really get.
                laidCols = EvenGrid(grid, cells, scroll.contentViewport.layout.width, 160f, Space.S24, laidCols);
            });
            if (selected != null && C.TryClass(selected) is ClassDef sel) { Show(detail, sel, S.Bestiary.TryGetValue(selected, out var se) ? se : null); onPick?.Invoke(selected); }
            else Ui.Empty("Tap a creature.", detail);
        }

        /// <summary>Fits as many cells of about <paramref name="target"/> px as the width allows, then stretches them so the
        /// left, right and between-cell gutters are all exactly <paramref name="gap"/> (POL-C1/C8): no ragged right edge.
        /// Cells go into explicit row containers (never wrap), so every row has exactly the same count and edges.
        /// Returns the column count laid out; a call with the same count only resizes.</summary>
        private static int EvenGrid(VisualElement grid, List<VisualElement> cells, float width, float target, float gap, int laidCols)
        {
            float w = width - grid.resolvedStyle.paddingLeft - grid.resolvedStyle.paddingRight;
            if (float.IsNaN(w) || w <= target) return laidCols;
            int cols = Mathf.Max(1, Mathf.FloorToInt((w - gap) / (target + gap)));
            // Exact (unfloored) size: flooring dumped the remainder on the right edge, a ragged gutter (POL-C8).
            float size = (w - gap * (cols + 1)) / cols - 0.01f;
            if (cols != laidCols)
            {
                grid.Clear();
                VisualElement row = null;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (i % cols == 0)
                    {
                        row = new VisualElement { name = "grid-row" };
                        row.style.flexDirection = FlexDirection.Row;
                        Ui.Pad(row, left: gap / 2f, right: gap / 2f);
                        Ui.Margin(row, top: i == 0 ? gap / 2f : 0);
                        grid.Add(row);
                    }
                    row.Add(cells[i]);
                }
                // Pad a short last row with invisible fillers so its cells stay the same size as every other row's.
                for (int i = cells.Count % cols; i > 0 && i < cols; i++)
                {
                    var filler = new VisualElement { name = "grid-filler", pickingMode = PickingMode.Ignore };
                    filler.style.visibility = Visibility.Hidden;
                    filler.style.flexBasis = 0; filler.style.flexGrow = 1; filler.style.flexShrink = 1;
                    Ui.Margin(filler, left: gap / 2f, right: gap / 2f);
                    row.Add(filler);
                }
            }
            foreach (var t in cells)
            {
                // Flex shares the row exactly (equal grow, zero basis): a fixed width computed here drifted wider than the row
                // by the cells' own borders and min sizes, leaving a ragged right edge. Height follows the computed size.
                t.style.width = StyleKeyword.Auto; t.style.minWidth = 0; t.style.maxWidth = StyleKeyword.None;
                t.style.flexBasis = 0; t.style.flexGrow = 1; t.style.flexShrink = 1;
                t.style.height = size;
                Ui.Margin(t, gap / 2f, gap / 2f, gap / 2f, gap / 2f);
            }
            return cols;
        }

        /// <summary>The detail panel (§25.11, GG-T290): name and description once Seen, then each knowledge row in the order
        /// defeats unlock it — Speed, HP and stats, elements, abilities — with a locked row reading "???" and a hint
        /// naming how many more defeats the next one takes.</summary>
        private void Show(VisualElement w, ClassDef d, BestiaryEntry e)
        {
            w.Clear();
            if (e == null || !e.Seen) { Ui.Heading("??? — not yet encountered", w, "dim"); return; }
            int tier = BestiaryKnowledge.Tier(e);
            var top = Ui.El("row", w);
            Ui.Heading(d.Name, top, "grow");
            Ui.Caption($"Defeated {e.Defeated}", top, "gold");
            if (!string.IsNullOrEmpty(d.Description)) GlossaryText.Create(d.Description, "kit-text", w);
            if (!string.IsNullOrEmpty(d.Lore) && d.Lore != d.Description) GlossaryText.Create(d.Lore, "kit-text dim", w);
            Fact(w, "beast-known", "Known", Known(tier));
            if (d.Heavy) GlossaryText.Create(InspectCard.HeavyLine, "kit-text gold", w).name = "beast-heavy"; // GG-T892: its keyword and counters, once seen
            if (d.DropTable != null && C.DropTables.TryGetValue(d.DropTable, out var drops) && drops.Entries.Count > 0)
            {
                var names = drops.Entries.OrderByDescending(x => x.Chance).Select(de => C.TryItem(de.Item)).Where(it => it != null)
                    .Select(it => $"<color=#{ColorUtility.ToHtmlStringRGB(Palette.Rarity(it.Rarity))}>{it.Name}</color>");
                Ui.Field("Drops", string.Join(",  ", names), w, null, "beast-drops").Q<Label>("value").enableRichText = true;
            }
            Fact(w, "beast-speed", "Speed", tier >= BestiaryKnowledge.Speed ? $"SPD {d.BaseStats.Speed:0}" : null);
            Fact(w, "beast-stats", "Stats", tier >= BestiaryKnowledge.Stats
                ? $"HP {Formulas.MaxHp(d.BaseStats, 1)} at Lv 1  ·  " + Ui.StatLine(Formulas.StatsAtLevel(d, 1)).Replace("\n", "  ") + $"  ·  EVA {Formulas.Evasion(Formulas.StatsAtLevel(d, 1)):0}%" : null);
            Fact(w, "beast-elements", "Elements", tier >= BestiaryKnowledge.Elements || d.RevealResistances ? BestiaryKnowledge.ElementLine(d) : null);
            Fact(w, "beast-abilities", "Abilities", tier >= BestiaryKnowledge.Abilities ? "" : null);
            if (tier >= BestiaryKnowledge.Abilities)
            {
                var kit = Ui.KitTraits(C, d);
                if (kit.Length > 0) GlossaryText.Create(kit, "kit-text gold", w);
                // What it does standing in a pincer chain (GG-T307, §1.2.3): every foe has its own support, read at this tier.
                GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, d.Support, named: true), "kit-text gold", w).name = "beast-support";
                // Its abilities as chips: hold one to read it whole (GG-T252).
                var ids = d.Abilities.Concat(string.IsNullOrEmpty(d.ChargeAbility) ? Enumerable.Empty<string>() : new[] { d.ChargeAbility }).Distinct();
                var chips = Ui.El("row", w);
                chips.style.flexWrap = Wrap.Wrap;
                foreach (var id in ids)
                {
                    if (!(C.TryAbility(id) is AbilityDef ab) || string.IsNullOrEmpty(ab.Name)) continue;
                    var chip = Ui.El("row", chips);
                    chip.name = "beast-ability-" + ab.Id;
                    chip.style.alignItems = Align.Center;
                    chip.style.minHeight = Kit.Row;
                    Ui.Margin(chip, right: Space.S24);
                    var icon = Art.AbilityIcon(ab);
                    if (icon != null) { var ic = Ui.El("menu-icon", chip); ic.style.backgroundImage = new StyleBackground(icon); }
                    Ui.Name(ab.Name, chip);
                    InspectCard.Attach(chip, b => InspectCard.Ability(b, ab), tap: true); // GG-T450: chips have no other tap
                }
                Ui.Para(GridGame.Core.Rules.EnemyPlanner.Describe(d.Targeting != Targeting.Auto ? d.Targeting : d.BaseStats.Intelligence >= 14 ? Targeting.Weakest : d.Tags.Contains("Sentient") ? Targeting.Threat : Targeting.Closest), w, "warn");
                Ui.Para(GridGame.Core.Rules.EnemyPlanner.DescribeBrain(d.Brain), w);
            }
            var hint = BestiaryKnowledge.Hint(e);
            if (hint != null) Ui.Para(hint, w, "dim").name = "beast-hint";
        }

        /// <summary>How much of it the Bestiary knows, by the highest tier reached.</summary>
        private static string Known(int tier) =>
            tier >= BestiaryKnowledge.Abilities ? "Everything" : tier >= BestiaryKnowledge.Elements ? "Its weaknesses"
            : tier >= BestiaryKnowledge.Stats ? "Its HP and stats" : tier >= BestiaryKnowledge.Speed ? "Its speed" : "Its name and story";

        /// <summary>One knowledge row: its label, then the value, or "???" while <paramref name="value"/> is still locked (null).</summary>
        private static void Fact(VisualElement w, string name, string label, string value)
        {
            Ui.Field(label, value ?? "???", w, value == null ? "dim" : null, name);
        }
    }
}
