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
    public sealed class BlacksmithScreen : HubPage
    {
        private const int RepairTab = 3;
        /// <summary>-1 until first built: then Repair if the workshop is still closed or his camp tip is due (GG-T175), else Forge.</summary>
        private int tab = -1;
        private RecipeDef selected;
        public override string Id => "blacksmith";
        protected override string Title => "Blacksmith";
        protected override string Backdrop => "cave";
        protected override string Subtitle => $"{Facilities.Current(C, S, CraftStation.Forge)} · {StationProgress.ShortText(S, StationKind.Blacksmith)}";
        protected override bool SubtitleInBody => true;
        protected override string Npc => CampNpcs.Blacksmith;
        /// <summary>The Forge tab has its own tip, the first forge (GG-T177), then the first armor once the weapon is made
        /// (GG-T178); Repair keeps the Blacksmith's.</summary>
        protected override string TipStation => tab != 0 ? Id : Forging.ArmorStep(S) ? Forging.ArmorTipStation : Forging.TipStation;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T175 (§25.0.4): he joins to mend worn gear, so Repair is his only open tab at first; Forge, Upgrade and
            // Salvage are dimmed and say when they arrive.
            bool workshop = CampUnlocks.SmithWorkshopOpen(S);
            if (tab < 0) tab = workshop && (CampTips.Pending(S, Id) == null || !Repair.Pieces(C, S).Any()) ? 0 : RepairTab;
            if (!workshop) tab = RepairTab;
            var bar = Ui.Tabs(new[] { "Forge", "Upgrade", "Salvage", "Repair" }, tab, i =>
            {
                if (!workshop && i != RepairTab) { App.Toast(CampUnlocks.SmithWorkshopHint(App.Content)); App.Audio.Sfx("ui.error"); return; }
                tab = i; selected = null; Refresh();
            }, body);
            if (!workshop)
                for (int i = 0; i < bar.childCount; i++)
                    if (i != RepairTab) { bar[i].style.opacity = 0.4f; bar[i].AddToClassList("tab--locked"); }
            var list = Ui.ListPanel(body);
            var detail = Ui.DetailPanel(body);
            // GG-T177 (§25.0.6): until the first forge is made, the Forge opens on the starter weapon its tip walks.
            // GG-T178 (§25.0.7): once it's made (the starter still selected), it moves on to the first armor piece.
            if (tab == 0 && (selected == null || (Forging.IsStarter(selected) && Forging.Done(S))))
                selected = Forging.First(C, S)?.Recipe ?? Forging.FirstArmor(C, S)?.Recipe ?? selected;
            if (tab <= 1) RecipeList(list, detail, C, S, CraftStation.Forge, tab == 1, selected, r => selected = r, this);
            else if (tab == 2) SalvageList(list, detail);
            else RepairList(list, detail);
        }

        /// <summary>The station's recipe rows over its detail pane. <paramref name="onPick"/> keeps the page's own selection in
        /// step with the row the player taps, so a Craft/Brew (which rebuilds the page) stays on that recipe — and the list
        /// scrolls back to it — instead of jumping to the first or guided one.</summary>
        internal static void RecipeList(VisualElement list, VisualElement detail, ContentDb c, SaveData s, CraftStation station, bool upgrades, RecipeDef selected, Action<RecipeDef> onPick, HubPage page)
        {
            var recipes = Crafting.Recipes(c, station).Where(r => (r.BaseItem != null) == upgrades).ToList();
            if (station == CraftStation.Enchant) recipes = Crafting.Recipes(c, station).ToList();
            // Recipe discovery (§24.6.2, GG-T36): undiscovered recipes are hidden, except a "???" tease
            // for ones the player is exactly one input away from.
            // Armor-set recipes open after the first forge (GG-T178, §25.0.7).
            // GG-T538 (§25.0.10): the Forge and Alchemy lists also grow with progress (VendorTiers.RecipeOpen).
            var visible = recipes.Where(r => Forging.ArmorOpen(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && (RecipeDiscovery.IsDiscovered(s, r) || RecipeDiscovery.OneAwayMissing(s, r) != null)).ToList();
            // GG-T432 (§25.12.2): the station's next facility level, bought right here (also a card on the Caravan screen).
            if (Facilities.Next(c, s, station) is CaravanLevel nextFacility)
            {
                var build = Ui.MenuRow($"Build {nextFacility.Name}", Ui.Gold(nextFacility.Cost), () => ConfirmFacility(c, s, station, page), list);
                build.name = "facility-build";
                build.KindIcon(RowKind.Station); // GG-T746 (§26.7)
            }
            // GG-T538: the station's next rank, in forges or brews ("Journeyman at 150 XP: 40/150, about 8 more brews").
            if (VendorTiers.Gated(recipes.FirstOrDefault()) && VendorTiers.NextRankLine(s, Crafting.StationFor(station)) is string rankLine)
                Ui.Note(rankLine, list).name = "rank-next";
            if (visible.Count == 0) Ui.Empty("No recipes.", list);
            RecipeDef sel = selected;
            // GG-T198: two discovered recipes can share a Result (the old Slime Gel brew and the new Empty Bottle
            // brew, GG-T176) and otherwise look identical in the list apart from price. When that happens, name
            // each row's key inputs so a player can tell them apart without opening both.
            var resultCounts = visible.Where(x => RecipeDiscovery.IsDiscovered(s, x)).GroupBy(x => x.Result)
                .ToDictionary(g => g.Key, g => g.Count());
            // GG-T2021: what can be made right now comes first, each with the station's verb as a chip; everything else
            // (short of inputs or gold, rank- or facility-locked, or a "???" tease) follows under one section.
            string verb = station == CraftStation.Alchemy ? "Brew" : station == CraftStation.Enchant ? "Enchant" : "Craft";
            bool Ready(RecipeDef r) => RecipeDiscovery.IsDiscovered(s, r) && Crafting.IsUnlocked(s, r) && Facilities.Allows(c, s, r)
                && Crafting.HasInputs(s, r) && s.Gold >= r.Gold;
            var ready = visible.Where(Ready).ToList();
            var rest = visible.Where(r => !Ready(r)).ToList();
            foreach (var r in ready) AddRow(r, true);
            if (rest.Count > 0)
                Ui.Section(station == CraftStation.Alchemy ? "Need ingredients" : "Need materials", list).name = "recipes-need";
            foreach (var r in rest) AddRow(r, false);
            void AddRow(RecipeDef r, bool can)
            {
                var rec = r;
                VisualElement row;
                if (RecipeDiscovery.IsDiscovered(s, r))
                {
                    var result = c.Item(r.Result);
                    bool tierLocked = !Crafting.IsUnlocked(s, r), facilityLocked = !Facilities.Allows(c, s, r);
                    bool locked = tierLocked || facilityLocked;
                    string nameOverride = resultCounts[r.Result] > 1 ? $"{result.Name} — {InputSummary(c, r)}" : null;
                    string lockLabel = tierLocked ? $"🔒 {StationProgress.TierName(r.Tier)}" : $"🔒 {Facilities.NeededName(c, r)}";
                    row = ItemUi.Row(result, locked ? lockLabel : $"{r.Gold} G", () => { onPick?.Invoke(rec); ShowRecipe(detail, c, s, rec, page); }, list, true, false, nameOverride);
                    row.name = "recipe-" + r.Id;
                    if (can) ReadyChip(row, verb);
                    else row.style.opacity = Dim.Row;
                }
                else
                {
                    var missing = RecipeDiscovery.OneAwayMissing(s, r);
                    var missingItem = c.TryItem(missing.Item);
                    row = Ui.MenuRow("???", $"Missing {missingItem?.Name ?? missing.Item}", () => { onPick?.Invoke(rec); ShowRecipe(detail, c, s, rec, page); }, list);
                    row.KindIcon(RowKind.Item); // GG-T717: an undiscovered result shows the Item emblem
                    row.style.opacity = 0.75f;
                }
                row.AddToClassList("menu-row--compact");
                if (sel == r)
                {
                    Ui.Select(row);
                    var picked = row;
                    var scroll = list.GetFirstAncestorOfType<ScrollView>();
                    bool scrolled = false;
                    if (scroll != null)
                        scroll.RegisterCallback<GeometryChangedEvent>(_ => { if (!scrolled && picked.layout.height > 0) { scrolled = true; scroll.ScrollTo(picked); } });
                }
            }
            if (VendorTiers.Gated(recipes.FirstOrDefault())) ComingLater(list, detail, c, s, station, recipes, visible);
            if (sel != null && visible.Contains(sel)) ShowRecipe(detail, c, s, sel, page);
            else if (sel != null && !VendorTiers.RecipeOpen(c, s, sel)) ShowLocked(detail, c, s, sel, LockReason(c, s, sel));
            else if (sel != null) ShowRecipe(detail, c, s, sel, page);
            else Ui.Empty("Pick a recipe to see what it needs.", detail);
        }

        /// <summary>The "Brew" / "Craft" / "Enchant" pill at the end of a recipe row the player can make right now (GG-T2021):
        /// the bright Good green on the badge's near-black fill, well over 4.5:1 (GG-T1863).</summary>
        internal static void ReadyChip(VisualElement row, string verb)
        {
            var chip = Ui.Text(verb, "role-badge", row);
            chip.name = "recipe-chip";
            chip.style.fontSize = TypeScale.Base;
            chip.style.color = Palette.Good;
            chip.style.borderTopColor = chip.style.borderBottomColor = chip.style.borderLeftColor = chip.style.borderRightColor = Palette.Good;
        }

        /// <summary>GG-T538 (§25.0.10): the next tier, shown locked at the foot of the list. Up to
        /// <see cref="VendorTiers.PreviewRows"/> recipes from the next rank ("🔒 Journeyman") and from the next progress tier
        /// ("🔒 The Lantern Road III"), with a count of the rest, so the player sees what is coming and what opens it.</summary>
        private static void ComingLater(VisualElement list, VisualElement detail, ContentDb c, SaveData s, CraftStation station, List<RecipeDef> recipes, List<RecipeDef> visible)
        {
            var kind = Crafting.StationFor(station);
            int rank = StationProgress.Level(s, kind), tier = VendorTiers.ProgressTier(c, s);
            var shown = new HashSet<RecipeDef>(visible);
            // Next rank: open on the road, hidden today only because the station's rank is short.
            var byRank = recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && r.Tier == rank + 1).ToList();
            // Next progress tier: the first tier above this save's with anything for this list.
            int next = recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && !VendorTiers.RecipeOpen(c, s, r))
                .Select(r => VendorTiers.RecipeTier(c, r)).Where(t => t > tier).DefaultIfEmpty(0).Min();
            var byRoad = next == 0 ? new List<RecipeDef>() : recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && !VendorTiers.RecipeOpen(c, s, r) && VendorTiers.RecipeTier(c, r) == next)
                .OrderBy(r => r.Tier).ThenBy(r => r.Gold).ToList();
            if (byRank.Count == 0 && byRoad.Count == 0) return;
            Ui.Section("Coming later", list).name = "recipes-later";
            void Rows(List<RecipeDef> set, string value, string more)
            {
                foreach (var r in set.Take(VendorTiers.PreviewRows))
                {
                    var rec = r;
                    var row = ItemUi.Row(c.Item(r.Result), value, () => ShowLocked(detail, c, s, rec, LockReason(c, s, rec)), list, true, false);
                    row.name = "recipe-locked-" + r.Id;
                    row.AddToClassList("menu-row--compact");
                    row.style.opacity = Dim.Row;
                }
                if (set.Count > VendorTiers.PreviewRows) Ui.Note($"+{set.Count - VendorTiers.PreviewRows} more {more}", list);
            }
            if (byRank.Count > 0) Rows(byRank, $"🔒 {StationProgress.TierName(rank + 1)}", $"at {StationProgress.TierName(rank + 1)}");
            if (byRoad.Count > 0)
            {
                string after = VendorTiers.OpensHint(c, next);
                Rows(byRoad, "🔒 " + after.Substring("after ".Length), after);
            }
        }

        /// <summary>Why a previewed recipe is locked: "Opens after The Lantern Road III", "Needs a Journeyman blacksmith (40/150 XP)", or both.</summary>
        private static string LockReason(ContentDb c, SaveData s, RecipeDef r)
        {
            var kind = Crafting.StationFor(r.Station);
            var parts = new List<string>();
            if (!VendorTiers.RecipeOpen(c, s, r)) parts.Add("Opens " + VendorTiers.OpensHint(c, VendorTiers.RecipeTier(c, r)));
            if (!Crafting.IsUnlocked(s, r))
                parts.Add($"Needs a {StationProgress.TierName(r.Tier)} {StationProgress.Id(kind)} (you are {StationProgress.LevelName(s, kind)}, {StationProgress.Xp(s, kind)} XP)");
            return string.Join(". ", parts);
        }

        /// <summary>A previewed recipe's detail: what it makes, why it is locked, and what goes in (GG-T538).</summary>
        private static void ShowLocked(VisualElement w, ContentDb c, SaveData s, RecipeDef r, string reason)
        {
            w.Clear();
            ItemUi.Detail(w, c.Item(r.Result), c);
            Ui.Para("🔒 " + reason, w, "bad");
            Ui.Section("Needs", w);
            if (r.BaseItem != null) Ui.Text($"{c.TryItem(r.BaseItem)?.Name ?? r.BaseItem} ×1", "kit-text dim", w);
            foreach (var ic in r.Inputs) Ui.Text($"{c.TryItem(ic.Item)?.Name ?? ic.Item} ×{ic.Count}", "kit-text dim", w);
            Ui.Para(Ui.Gold(r.Gold), w, "dim");
        }

        /// <summary>"Slime Gel" or "Empty Bottle + Wolf Pelt + Tattered Cloth" — the recipe's own inputs, joined,
        /// used to tell apart two recipes that share a Result (GG-T198).</summary>
        private static string InputSummary(ContentDb c, RecipeDef r) =>
            string.Join(" + ", r.Inputs.Select(ic => c.TryItem(ic.Item)?.Name ?? ic.Item));

        /// <summary>Asks, then builds the station's next facility level (GG-T432): a coin cue, then the forge's clang or
        /// the still's glug as the new equipment goes in.</summary>
        private static void ConfirmFacility(ContentDb c, SaveData s, CraftStation station, HubPage page)
        {
            var next = Facilities.Next(c, s, station);
            if (next == null) return;
            if (s.Gold < next.Cost) { GameApp.I.Toast($"{next.Name} costs {Ui.Gold(next.Cost)}"); GameApp.I.Audio.Sfx("ui.error"); return; }
            Confirm.Show($"Build the {next.Name} for {Ui.Gold(next.Cost)}? {next.Text}.", "Build", () =>
            {
                var res = Facilities.Build(c, s, station);
                GameApp.I.Toast(res.Message);
                GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                if (res.Ok)
                {
                    GameApp.I.Audio.Sfx(station == CraftStation.Forge ? "battle.clang" : "battle.glug");
                    GameApp.I.Persist();
                }
                page.Refresh();
            });
        }

        internal static void ShowRecipe(VisualElement w, ContentDb c, SaveData s, RecipeDef r, HubPage page)
        {
            w.Clear();
            if (!RecipeDiscovery.IsDiscovered(s, r))
            {
                Ui.Heading("??? — Undiscovered Recipe", w, "dim");
                Ui.Para("You sense you're close. Find the missing material to learn how this is made.", w, "dim");
                var undiscoveredNeed = w;
                if (r.BaseItem != null)
                {
                    var b = c.TryItem(r.BaseItem);
                    int have = s.Count(r.BaseItem);
                    Ui.Text($"{(have >= 1 ? "✔" : "✘")} {b?.Name ?? r.BaseItem} ×1   (have {have})", "kit-text " + (have >= 1 ? "good" : "bad"), undiscoveredNeed);
                }
                foreach (var ic in r.Inputs)
                {
                    var it = c.TryItem(ic.Item);
                    int have = s.Count(ic.Item);
                    Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), undiscoveredNeed);
                }
                return;
            }
            var result = c.Item(r.Result);
            ItemUi.Detail(w, result, c);
            bool locked = !Crafting.CanMake(c, s, r);
            if (!Crafting.IsUnlocked(s, r))
            {
                var kind = Crafting.StationFor(r.Station);
                Ui.Para($"🔒 Requires {StationProgress.TierName(r.Tier)} ({StationProgress.Id(kind)} is {StationProgress.LevelName(s, kind)})", w, "bad");
            }
            if (!Facilities.Allows(c, s, r))
            {
                // GG-T432: shown locked, never hidden — the facility that works this material is named, with its price.
                Ui.Para($"🔒 Needs {Facilities.NeededName(c, r)} (you have the {Facilities.Current(c, s, r.Station)})", w, "bad");
                if (Facilities.Next(c, s, r.Station) is CaravanLevel next)
                {
                    var b = RowIcon.IconButton($"Build {next.Name} · {Ui.Gold(next.Cost)}", RowKind.Confirm, () => ConfirmFacility(c, s, r.Station, page), "btn--small", Ui.Actions(w), confirmTap: false);
                    b.name = "facility-build-detail";
                    Ui.SetEnabled(b, s.Gold >= next.Cost);
                }
            }
            Ui.Section("Needs", w);
            var need = w;
            if (r.BaseItem != null)
            {
                var b = c.TryItem(r.BaseItem);
                int have = s.Count(r.BaseItem);
                Ui.Text($"{(have >= 1 ? "✔" : "✘")} {b?.Name ?? r.BaseItem} ×1   (have {have})", "kit-text " + (have >= 1 ? "good" : "bad"), need);
            }
            foreach (var ic in r.Inputs)
            {
                var it = c.TryItem(ic.Item);
                int have = s.Count(ic.Item);
                Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), need);
            }
            Ui.Para($"{(s.Gold >= r.Gold ? "✔" : "✘")} {Ui.Gold(r.Gold)}", need, s.Gold >= r.Gold ? "good" : "bad");
            if (r.Station == CraftStation.Forge)
                Ui.Para($"★ {OddsText.Percent(Crafting.MasterworkChance(c, s))} chance of a Masterwork result", need, "gold");
            if (Forging.IsGuided(s, r) && Forging.SuitedHero(c, s, result) is HeroSave suited)
                Ui.Para($"First forge: free, and the {c.TryClass(suited.ClassId)?.Name ?? suited.ClassId} equips it", need, "gold");
            if (Forging.IsGuidedArmor(c, s, r) && Forging.SuitedWearer(c, s, result) is HeroSave wearer)
                Ui.Para($"First armor: the {c.TryClass(wearer.ClassId)?.Name ?? wearer.ClassId} wears it", need, "gold");
            string verb = r.Station == CraftStation.Alchemy ? "Brew" : r.Station == CraftStation.Enchant ? "Enchant" : "Craft";
            var btn = RowIcon.IconButton(verb, RowKind.Confirm, () =>
            {
                // The guided first forge (GG-T177) and first armor (GG-T178) also equip what they make on the hero it suits.
                var res = Forging.Forge(c, s, r);
                GameApp.I.Toast(res.Message);
                GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                if (res.Ok) GameApp.I.Persist();
                page.Refresh();
            }, "btn--primary", Ui.Actions(w));
            btn.name = "craft";
            Ui.SetEnabled(btn, !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold);
            LuckyCraftUi.Section(w, c, s, r, !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold, page.Refresh);

            if (r.PotencyResult != null)
            {
                var greater = c.Item(r.PotencyResult);
                Ui.Section($"With a rarer reagent: {greater.Name}", w);
                var pneed = w;
                foreach (var ic in r.PotencyInputs)
                {
                    var it = c.TryItem(ic.Item);
                    int have = s.Count(ic.Item);
                    Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), pneed);
                }
                var pbtn = RowIcon.IconButton($"{verb} {greater.Name}", RowKind.Confirm, () =>
                {
                    var res = Crafting.Craft(c, s, r, potency: true);
                    GameApp.I.Toast(res.Message);
                    GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                    if (res.Ok) GameApp.I.Persist();
                    page.Refresh();
                }, null, Ui.Actions(w));
                Ui.SetEnabled(pbtn, !locked && Crafting.HasPotencyInputs(s, r) && s.Gold >= r.Gold);
            }
        }

        private void SalvageList(VisualElement list, VisualElement detail)
        {
            var gear = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null && i.Type == ItemType.Equipment).ToList();
            var broken = S.Broken.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null).ToList();
            if (gear.Count == 0 && broken.Count == 0) Ui.Empty("No spare gear in your bag.", list);
            Ui.Para("Break spare gear into materials — worn gear yields less; the worst copy goes first. Broken gear can only be salvaged.", detail, "dim");
            foreach (var it in gear)
            {
                var item = it;
                var yield = Crafting.SalvagePreview(C, S, item.Id);
                string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
                ItemUi.Row(item, $"×{S.Count(item.Id)}", () => Confirm.Show($"Salvage {item.Name} into {y}?", "Salvage", () => Done(Crafting.Salvage(C, S, item.Id))), list, yield.Count > 0, Equipment.IsMasterwork(S, item.Id));
            }
            foreach (var it in broken)
            {
                var item = it;
                var yield = Crafting.SalvageBrokenPreview(C, S, item.Id);
                string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
                var row = Ui.MenuRow($"Broken {item.Name}", $"×{S.BrokenCount(item.Id)}",
                    () => Confirm.Show($"Salvage Broken {item.Name} into {y}?", "Salvage", () => Done(Crafting.SalvageBroken(C, S, item.Id))), list, null, yield.Count > 0);
                row.KindIcon(RowKind.Item, Art.ItemIcon(item.Id)); // GG-T717
                row.Q<Label>("label").AddToClassList("dim");
            }
        }

        /// <summary>The Repair tab's Repair all button (GG-T544). Not "repair-…": the Blacksmith's tip rings the first
        /// control whose name starts with that.</summary>
        public const string RepairAllButton = "smith-repair-all";

        /// <summary>How much wear the Repair tab's price hint quotes (GG-T544): "3 G per 10 wear, 5 G after this repair".</summary>
        private const int HintWear = 10;

        private void RepairList(VisualElement list, VisualElement detail)
        {
            bool salvage = CampUnlocks.SmithWorkshopOpen(S); // "Salvage instead" waits for the Salvage tab (GG-T175)
            int threshold = (int)Math.Round(CampRoutine.RepairBelowFraction * 100);
            // GG-T544: one threshold for the camp routine and the player. Each repair compounds the piece's next price, so
            // the tab says so, Repair all mends only pieces worn below the threshold, and the rest say "Cheaper to wait".
            Ui.Para($"Each repair lowers the piece's max durability by 1 and makes its next repair cost 1.6× as much, so mend gear once it's below {threshold}%, not after every scratch."
                    + (salvage ? " An uneconomical repair offers salvage instead." : ""), detail, "dim");
            var due = Repair.DuePieces(C, S);
            int dueCost = due.Sum(p => Repair.Cost(C, S, p));
            var all = RowIcon.IconButton(due.Count == 0 ? "Nothing needs repair yet" : $"Repair all ({due.Count}) · {Ui.Gold(dueCost)}", RowKind.Confirm,
                () => Done(Repair.RepairAll(C, S), cue: "camp.repair"), "btn--primary", Ui.Actions(detail));
            all.name = RepairAllButton;
            Ui.SetEnabled(all, due.Any(p => Repair.Cost(C, S, p) <= S.Gold));
            bool any = false;
            int sound = 0, worn = 0;
            foreach (var p in Repair.Pieces(C, S))
            {
                any = true;
                var piece = p;
                int cost = Repair.Cost(C, S, p);
                if (cost <= 0) { sound++; continue; } // GG-T1865: full-durability pieces collapse into one row below
                worn++;
                bool bad = cost > 0 && Repair.IsUneconomical(p.Item, p.Durability, p.Repairs, p.Mastery);
                string name = (p.Mastery > 1f ? "★ " : "") + p.Item.Name;
                var row = Ui.MenuRow($"{C.Class(p.Hero.ClassId).Name} — {name}  {p.Durability}/{p.Max}", cost > 0 ? Ui.Gold(cost) : "OK",
                    () => Done(piece.Slot == EquipmentSlot.Weapon ? Repair.RepairWeapon(C, S, piece.Hero) : Repair.RepairArmor(C, S, piece.Hero, piece.Slot), cue: "camp.repair"),
                    list, null, cost > 0);
                row.name = p.RowName;
                row.KindIcon(RowKind.Item, Art.ItemIcon(p.Item.Id)); // GG-T746 (§26.7): the worn piece's icon, else the Item emblem
                if (bad) row.Q<Label>("value").AddToClassList("warn");
                if (cost > 0) RepairPriceNote(list, p);
                if (bad && salvage) SalvageInsteadRow(list, p.Hero, p.Slot, p.Item, p.Durability, cost);
            }
            string soundText = $"All gear sound ({sound} piece{(sound == 1 ? "" : "s")} at full durability)";
            if (worn > 0 && sound > 0) Ui.Note(soundText, list);
            if (worn > 0) return;
            // GG-T1883: nothing to mend, so the tab is a compact note that points at the work he can do instead.
            var state = Ui.EmptyState(list, any ? RowKind.Cleared : RowKind.Craft, any ? soundText : "Nobody has gear that wears out. Equip forged weapons first.", "smith-empty");
            if (!salvage) return;
            var next = Ui.Actions(state);
            if (any)
            {
                RowIcon.IconButton("Upgrade", RowKind.Craft, () => { tab = 1; selected = null; Refresh(); }, "btn--small", next);
                RowIcon.IconButton("Salvage", RowKind.Item, () => { tab = 2; selected = null; Refresh(); }, "btn--small", next);
            }
            else RowIcon.IconButton("Forge", RowKind.Craft, () => { tab = 0; selected = null; Refresh(); }, "btn--small", next);
        }

        /// <summary>GG-T544: the line under a worn piece's repair row — "Cheaper to wait" while it's above the threshold, then
        /// how the price grows: what 10 points of wear cost now, and after this repair (1.6x per repair, BIBLE §24.5).</summary>
        private void RepairPriceNote(VisualElement list, Repair.WornPiece p)
        {
            int now = Repair.WearCost(C, S, p.Item, HintWear, p.Repairs, p.Mastery);
            int next = Repair.WearCost(C, S, p.Item, HintWear, p.Repairs + 1, p.Mastery);
            bool wait = !Repair.Due(p.Item, p.Durability, p.Repairs, p.Mastery);
            var note = Ui.Note((wait ? $"Cheaper to wait ({Mathf.RoundToInt(p.Fraction * 100)}%) · " : "")
                               + $"{Ui.Gold(now)} per {HintWear} wear, {Ui.Gold(next)} after this repair", list);
            note.name = $"wear-note-{p.Hero.ClassId}-{p.Slot}"; // not "repair-…" (the tip rings that prefix)
        }

        /// <summary>The "repair is a ripoff — salvage it instead" offer, shown right under an uneconomical
        /// repair row so both outcomes (repair cost vs. salvage yield) sit side by side (GG-T42).</summary>
        private void SalvageInsteadRow(VisualElement list, HeroSave hero, EquipmentSlot slot, ItemDef item, int durability, int repairCost)
        {
            var yield = Crafting.SalvageYield(C, item, durability);
            string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
            var row = Ui.MenuRow("Salvage instead", y, () =>
                Confirm.Show($"Repair costs {Ui.Gold(repairCost)}. Scrap the {item.Name} instead for {y}? This destroys it.", "Salvage",
                    () => Done(Crafting.SalvageEquipped(C, S, hero, slot))), list);
            row.KindIcon(RowKind.Item, Art.ItemIcon(item.Id)); // GG-T746 (§26.7): the piece it scraps
            row.AddToClassList("kit-note"); // a sub-row of the repair above, indented under its label
            row.Q<Label>("value").AddToClassList("dim");
        }
    }
}
