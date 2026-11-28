using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1914 (§9.0, owner 2026-11-28): the Abilities button and the sheet it opens. The sheet slides up over the dimmed,
    /// still-visible board (the hero's tile glows through), with the hero's header, a breadcrumb, the Abilities / Spells /
    /// Consumables tabs and the moves of <see cref="ActionTree.For"/>; a move opens its detail page, whose Perform / Cast /
    /// Use slides the sheet away and starts that move's own aiming on the board (<see cref="OnSlot"/>).
    /// </summary>
    public sealed partial class BattleScreen
    {
        private VisualElement abilitiesButton, sheetVeil, sheetGlow;
        /// <summary>The sheet's place in the tree: a tab, then folders, then (on the detail page) the move.</summary>
        private readonly List<ActionNode> sheetPath = new List<ActionNode>();
        private int sheetHeroId = -1;
        /// <summary>The last move each hero committed from the sheet (hero id → bar slot): the sheet reopens at it and a
        /// long-press of the Abilities button re-aims it.</summary>
        private readonly Dictionary<int, int> lastMove = new Dictionary<int, int>();
        /// <summary>The slots each hero had ready when its sheet last opened; one ready since then lights the button's dot.</summary>
        private readonly Dictionary<int, HashSet<int>> seenReady = new Dictionary<int, HashSet<int>>();
        /// <summary>The move row last highlighted in the sheet, or −1.</summary>
        private int highlightedMove = -1;
        private bool abilitiesHeld;

        public bool AbilitiesOpen => sheetVeil != null && sheetVeil.parent != null;

        /// <summary>The Abilities cell of the bottom row: the selected hero's thumbnail over the word, and a dot when a
        /// move became ready since the sheet last opened. A tap opens the sheet; a hold re-aims the hero's last move.</summary>
        private void BuildAbilitiesCell(VisualElement bar)
        {
            var cell = abilitiesButton = HudCell(bar);
            cell.name = "abilities";
            Ui.Pressable(cell);
            var thumb = Ui.El(null, cell);
            thumb.name = "abilities-thumb";
            thumb.pickingMode = PickingMode.Ignore;
            thumb.style.width = thumb.style.height = Size.IconL;
            thumb.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            var word = Ui.Text("Abilities", "tiny", cell);
            word.pickingMode = PickingMode.Ignore;
            Ui.Marquee(word);
            var dot = Ui.El(null, cell);
            dot.name = "abilities-dot";
            dot.pickingMode = PickingMode.Ignore;
            dot.style.position = Position.Absolute;
            dot.style.top = dot.style.right = Space.S8;
            dot.style.width = dot.style.height = Size.Pip;
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = Size.Pip / 2f;
            dot.style.backgroundColor = Palette.Gold;
            dot.style.display = DisplayStyle.None;
            IVisualElementScheduledItem hold = null;
            cell.RegisterCallback<PointerDownEvent>(_ =>
            {
                abilitiesHeld = false;
                hold?.Pause();
                hold = cell.schedule.Execute(() => abilitiesHeld = ReaimLastMove()).StartingIn((long)(InspectCard.HoldSeconds * 1000f));
            });
            cell.RegisterCallback<PointerUpEvent>(_ => hold?.Pause());
            cell.RegisterCallback<PointerLeaveEvent>(_ => hold?.Pause());
            cell.RegisterCallback<PointerCancelEvent>(_ => hold?.Pause());
            cell.RegisterCallback<ClickEvent>(_ =>
            {
                ExitExamine();
                if (abilitiesHeld) { abilitiesHeld = false; return; }
                OpenAbilities(TargetingSlot);
            });
        }

        private void SyncAbilitiesButton()
        {
            if (abilitiesButton == null) return;
            var hero = State.UnitById(SelectedHeroId);
            var thumb = abilitiesButton.Q("abilities-thumb");
            var tex = hero?.Def != null ? Art.Thumb(hero.Def.Id) : null;
            thumb.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
            thumb.style.display = tex != null ? DisplayStyle.Flex : DisplayStyle.None;
            bool fresh = false;
            if (hero != null)
            {
                seenReady.TryGetValue(hero.Id, out var seen);
                fresh = ReadySlots(hero).Any(i => seen == null || !seen.Contains(i));
            }
            abilitiesButton.Q("abilities-dot").style.display = fresh ? DisplayStyle.Flex : DisplayStyle.None;
            abilitiesButton.style.opacity = hero == null ? 0.45f : 1f;
            if (AbilitiesOpen) RenderSheet();
        }

        private IEnumerable<int> ReadySlots(Unit hero)
        {
            for (int i = 0; i < hero.Bar.Count && i < hero.UnlockedSlots; i++)
                if (!hero.Bar[i].IsEmpty && BattleEngine.GetSlotStatus(State, hero.Id, i) == SlotStatus.Ready) yield return i;
        }

        /// <summary>Re-aims the selected hero's last committed move (a long-press of the Abilities button).</summary>
        private bool ReaimLastMove()
        {
            var hero = State.UnitById(SelectedHeroId);
            if (hero == null || !lastMove.TryGetValue(hero.Id, out int slot)) return false;
            OnSlot(slot);
            return true;
        }

        /// <summary>Opens the sheet for the selected hero: at <paramref name="detail"/>'s detail page when given (Cancel while
        /// aiming, GG-T1921), else at the list holding its last move when it has one.</summary>
        private void OpenAbilities(int detail = -1)
        {
            var hero = State.UnitById(SelectedHeroId);
            if (hero == null || ended) { App.Audio.Sfx("ui.error"); return; }
            DropRefusal();
            if (TargetingSlot >= 0) CancelTargeting();
            CloseAbilities(false);
            seenReady[hero.Id] = new HashSet<int>(ReadySlots(hero));
            sheetHeroId = hero.Id;
            sheetPath.Clear();
            highlightedMove = -1;
            var tree = ActionTree.For(State, hero);
            var page = detail >= 0 ? ActionTree.PathTo(tree, detail) : null;
            var at = lastMove.TryGetValue(hero.Id, out int last) ? ActionTree.PathTo(tree, last) : null;
            if (page != null)
            {
                sheetPath.AddRange(page);
                HighlightMove(hero, detail);
            }
            else if (at != null)
            {
                sheetPath.AddRange(at.Take(at.Count - 1));
                HighlightMove(hero, last);
            }
            else sheetPath.Add(tree.FirstOrDefault(t => ActionTree.Moves(t).Any()) ?? tree[0]);

            var veil = sheetVeil = Ui.El("fill", App.Overlay);
            veil.name = "abilities-veil";
            veil.pickingMode = PickingMode.Position;
            // The board stays in view, dimmed (UI Toolkit has no backdrop blur, so the dim alone sets the sheet apart).
            veil.style.backgroundColor = Palette.Ink.WithA(0.5f);
            veil.RegisterCallback<ClickEvent>(e => { if (e.target == veil) CloseAbilities(); });
            if (board.Units.TryGetValue(hero.Id, out var hv))
            {
                var r = veil.parent.WorldToLocal(hv.Root.worldBound);
                var glow = sheetGlow = Ui.El(null, veil);
                glow.pickingMode = PickingMode.Ignore;
                glow.style.position = Position.Absolute;
                glow.style.left = r.x - Space.S16; glow.style.top = r.y - Space.S16;
                glow.style.width = r.width + Space.S32; glow.style.height = r.height + Space.S32;
                glow.style.backgroundImage = new StyleBackground(Art.Glow);
                glow.style.unityBackgroundImageTintColor = Palette.Gold;
            }
            var sheet = Ui.Window(null, veil, "window--compact");
            sheet.name = "abilities-sheet";
            sheet.style.position = Position.Absolute;
            sheet.style.left = sheet.style.right = Fit.Inset + Fit.Edge;
            sheet.style.bottom = GameApp.SafeBottom + Space.S16;
            sheet.style.height = Fit.H(62);
            RenderSheet();
            App.Audio.Sfx("ui.select");
            if (!ReduceMotion)
            {
                float h = Fit.H(62) + GameApp.SafeBottom + Space.S16;
                sheet.style.translate = new Translate(0, h);
                App.StartCoroutine(Tween.Run(Timing.Standard, k => sheet.style.translate = new Translate(0, h * (1f - k)), Ease.Out));
            }
            SyncAbilitiesButton();
        }

        /// <summary>Closes the sheet; it slides down unless Reduce Motion is on or <paramref name="animate"/> is false.</summary>
        private void CloseAbilities(bool animate = true)
        {
            if (sheetVeil == null) return;
            var veil = sheetVeil;
            sheetVeil = sheetGlow = null;
            sheetHeroId = -1;
            highlightedMove = -1;
            var sheet = veil.Q("abilities-sheet");
            if (!animate || ReduceMotion || sheet == null || veil.parent == null) { veil.RemoveFromHierarchy(); return; }
            veil.pickingMode = PickingMode.Ignore;
            float h = sheet.layout.height + GameApp.SafeBottom + Space.S16;
            App.StartCoroutine(Tween.Run(Timing.Quick, k =>
            {
                sheet.style.translate = new Translate(0, h * k);
                veil.style.backgroundColor = Palette.Ink.WithA(0.5f * (1f - k));
                if (k >= 1f) veil.RemoveFromHierarchy();
            }));
        }

        /// <summary>Back in the sheet: the detail page returns to its list, a folder to its tab, a tab closes the sheet.</summary>
        private void SheetBack()
        {
            if (sheetPath.Count > 1) { sheetPath.RemoveAt(sheetPath.Count - 1); App.Audio.Sfx("ui.back"); RenderSheet(); }
            else CloseAbilities();
        }

        /// <summary>Hook for the board preview while browsing (§9.0, later ticket): a move row was highlighted.</summary>
        private void HighlightMove(Unit hero, int slot)
        {
            highlightedMove = slot;
        }

        private void RenderSheet()
        {
            var sheet = sheetVeil?.Q("abilities-sheet");
            var hero = State.UnitById(sheetHeroId);
            if (sheet == null) return;
            if (hero == null || !hero.Alive || hero.Id != SelectedHeroId) { CloseAbilities(false); return; }
            // Rebuild the path against a fresh tree (a move may have moved tabs or gone); fall back to the tab.
            var tree = ActionTree.For(State, hero);
            var fresh = new List<ActionNode>();
            var parent = tree;
            foreach (var n in sheetPath)
            {
                var match = parent.FirstOrDefault(c => c.Name == n.Name && c.Slot == n.Slot);
                if (match == null) break;
                fresh.Add(match);
                parent = match.Children;
            }
            if (fresh.Count == 0) fresh.Add(tree[0]);
            sheetPath.Clear();
            sheetPath.AddRange(fresh);

            float scroll = sheet.Q<ScrollView>("abilities-list")?.scrollOffset.y ?? 0f;
            sheet.Clear();
            SheetHeader(sheet, hero);
            var crumbs = Ui.El("row", sheet);
            crumbs.name = "abilities-crumbs";
            crumbs.style.flexWrap = Wrap.Wrap;
            crumbs.style.marginBottom = Space.S8;
            for (int i = 0; i < sheetPath.Count; i++)
            {
                int depth = i + 1;
                bool last = depth == sheetPath.Count;
                if (i > 0) Ui.Text(" › ", "small dim", crumbs);
                var c = Ui.Text(sheetPath[i].Name, last ? "small" : "small gold", crumbs);
                c.name = "crumb" + i;
                if (!last) Ui.Tappable(c, () => { sheetPath.RemoveRange(depth, sheetPath.Count - depth); RenderSheet(); });
            }
            int tab = tree.IndexOf(sheetPath[0]);
            var tabs = Ui.Tabs(ActionTree.Tabs, tab, i => { sheetPath.Clear(); sheetPath.Add(tree[i]); RenderSheet(); }, sheet);
            for (int i = 0; i < tree.Count; i++)
                if (!ActionTree.Moves(tree[i]).Any(m => BattleEngine.GetSlotStatus(State, hero.Id, m.Slot) == SlotStatus.Ready))
                    tabs[i].style.opacity = 0.55f;
            var node = sheetPath[sheetPath.Count - 1];
            if (node.IsMove) { MoveDetail(sheet, hero, node.Slot); return; }
            var list = Ui.List(sheet);
            list.name = "abilities-list";
            list.style.flexGrow = 1;
            if (node.Children.Count == 0) Ui.Text($"{hero.Name} has no {node.Name.ToLowerInvariant()} yet.", "small dim", list);
            foreach (var child in node.Children)
            {
                if (child.IsMove) MoveRow(list, hero, child);
                else FolderRow(list, hero, child);
            }
            list.schedule.Execute(() => list.scrollOffset = new Vector2(0f, scroll));
        }

        private void SheetHeader(VisualElement sheet, Unit hero)
        {
            var head = Ui.El("row", sheet);
            head.name = "abilities-header";
            head.style.alignItems = Align.Center;
            head.style.marginBottom = Space.S8;
            var thumb = Ui.El(null, head);
            thumb.style.width = thumb.style.height = Size.Plate;
            thumb.style.marginRight = Space.S16;
            thumb.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            var tex = hero.Def != null ? Art.Thumb(hero.Def.Id) : null;
            if (tex != null) thumb.style.backgroundImage = new StyleBackground(tex);
            var col = Ui.El("col grow", head);
            col.style.flexShrink = 1;
            Ui.Marquee(Ui.Text(hero.Name, "h2", col));
            Ui.Text($"HP {hero.Hp} / {hero.MaxHp}", "small dim", col);
            var close = RowIcon.IconButton("Close", RowKind.Close, () => CloseAbilities(), "btn--small", head);
            close.name = "abilities-close";
        }

        private void FolderRow(VisualElement list, Unit hero, ActionNode folder)
        {
            int ready = ActionTree.Moves(folder).Count(m => BattleEngine.GetSlotStatus(State, hero.Id, m.Slot) == SlotStatus.Ready);
            var row = Ui.MenuRow(folder.Name, $"{ready} ready", () => { sheetPath.Add(folder); RenderSheet(); }, list);
            row.KindIcon(RowKind.Menu);
            row.name = "folder-" + folder.Name.ToLowerInvariant();
        }

        /// <summary>One move: its face (recharge sweep, readiness fill or stack), full name, readiness and a one-line summary
        /// of how and whom it targets. A tap opens its detail page.</summary>
        private void MoveRow(VisualElement list, Unit hero, ActionNode move)
        {
            int slot = move.Slot;
            var a = BattleEngine.SlotAbility(State, hero, slot);
            if (a == null) return;
            var status = BattleEngine.GetSlotStatus(State, hero.Id, slot);
            bool ready = status == SlotStatus.Ready;
            var row = Ui.El("row", list);
            row.name = "move" + slot;
            row.style.alignItems = Align.Center;
            row.style.minHeight = Size.Target;
            row.style.marginBottom = Space.S8;
            if (slot == highlightedMove) row.style.backgroundColor = Palette.Gold.WithA(0.18f);
            var face = MoveFace(row, hero, slot, status);
            face.style.width = face.style.height = Size.Target;
            face.style.marginRight = Space.S16;
            var col = Ui.El("col grow", row);
            col.style.flexShrink = 1;
            var name = Ui.Text(move.Name, "small", col);
            name.style.whiteSpace = WhiteSpace.Normal;
            var state = Ui.Text(MoveReadiness(hero, slot, a, status), ready ? "small gold" : "small dim", col);
            state.style.whiteSpace = WhiteSpace.Normal;
            var sum = Ui.Text(TargetText.How(a), "tiny dim", col);
            sum.style.whiteSpace = WhiteSpace.Normal;
            if (!ready) { face.style.opacity = 0.55f; name.style.opacity = 0.7f; }
            row.RegisterCallback<PointerEnterEvent>(_ => HighlightMove(hero, slot));
            Ui.Tappable(row, () => { HighlightMove(hero, slot); sheetPath.Add(move); RenderSheet(); });
        }

        /// <summary>A move's <see cref="Ui.AbilitySlot"/> face, filled like the bar's.</summary>
        private VisualElement MoveFace(VisualElement parent, Unit hero, int slot, SlotStatus status)
        {
            var face = Ui.El(null, parent);
            face.pickingMode = PickingMode.Ignore;
            face.style.flexShrink = 0;
            Ui.AbilitySlot(face);
            FillMoveFace(face, hero, slot, status, out _);
            return face;
        }

        /// <summary>The detail page: the move's face, full name and readiness, its whole card, and the commit button in thumb
        /// reach, labelled by kind and disabled with the reason when the move can't be used now.</summary>
        private void MoveDetail(VisualElement sheet, Unit hero, int slot)
        {
            var a = BattleEngine.SlotAbility(State, hero, slot);
            if (a == null) { SheetBack(); return; }
            var status = BattleEngine.GetSlotStatus(State, hero.Id, slot);
            bool ready = status == SlotStatus.Ready;
            var body = Ui.List(sheet);
            body.name = "abilities-detail";
            body.style.flexGrow = 1;
            var top = Ui.El("row", body);
            top.style.alignItems = Align.Center;
            top.style.marginBottom = Space.S8;
            var face = MoveFace(top, hero, slot, status);
            face.style.width = face.style.height = Size.Slot;
            face.style.marginRight = Space.S16;
            var col = Ui.El("col grow", top);
            col.style.flexShrink = 1;
            Ui.Text(ActionTree.MoveName(State, hero, slot, a), "h2", col).style.whiteSpace = WhiteSpace.Normal;
            Ui.Text(MoveReadiness(hero, slot, a, status), ready ? "small gold" : "small warn", col).style.whiteSpace = WhiteSpace.Normal;
            InspectCard.Ability(body, a, state: SlotState(hero, slot, a));

            var foot = Ui.El("row", sheet);
            foot.name = "abilities-footer";
            foot.style.justifyContent = Justify.SpaceBetween;
            foot.style.marginTop = Space.S8;
            var back = RowIcon.IconButton("Back", RowKind.Back, SheetBack, "btn--small", foot);
            back.name = "abilities-back";
            var go = RowIcon.IconButton(CommitVerb(hero, slot, a), RowKind.Confirm, () => CommitMove(hero, slot), "btn--primary", foot, confirmTap: false);
            go.name = "abilities-commit";
            go.SetEnabled(ready && CanCommand());
        }

        /// <summary>The detail page's commit button: Use for an item, Cast for a spell, else Perform.</summary>
        private static string CommitVerb(Unit hero, int slot, AbilityDef a)
        {
            var sl = hero.Bar[slot];
            return sl.IsItem ? "Use" : !sl.IsWeapon && a.Kind == AbilityKind.Spell ? "Cast" : "Perform";
        }

        /// <summary>Perform / Cast / Use: the sheet slides away and the board takes that move's own aiming.</summary>
        private void CommitMove(Unit hero, int slot)
        {
            lastMove[hero.Id] = slot;
            CloseAbilities();
            OnSlot(slot);
        }

        /// <summary>One line of a move's readiness: "Ready", "Ready in 2 turns", "0 left", the spell's line or why not.</summary>
        private string MoveReadiness(Unit hero, int slot, AbilityDef a, SlotStatus status)
        {
            var sl = hero.Bar[slot];
            switch (status)
            {
                case SlotStatus.Ready:
                    return sl.IsItem ? $"Ready · ×{sl.Charges}" : a.Kind == AbilityKind.Spell && !sl.IsWeapon ? $"Ready · {SpellReadyText(hero, slot)}" : "Ready";
                case SlotStatus.Cooldown:
                    int cd = hero.Cooldowns.TryGetValue(a.Id, out var c) ? c : 0;
                    return $"Ready in {cd} turn{(cd == 1 ? "" : "s")}";
                case SlotStatus.OutOfCharges: return "0 left";
                case SlotStatus.Unaffordable: return SpellReadyText(hero, slot);
                default: return StatusText(status);
            }
        }

        // ── Spell readiness: the one switch GG-T1915 turns from each spell's own meter to cost against the party bar ──

        /// <summary>How ready a spell is, 0..1 (1 = castable), or −1 when it has no meter.</summary>
        private float SpellFill(Unit hero, int slot)
        {
            int full = State.ManaFull(hero, slot);
            return full > 0 ? ShownMana(hero.Id, slot) / (float)full : -1f;
        }

        /// <summary>A spell's readiness in words for the sheet ("120 / 300 mana · charging 40%").</summary>
        private string SpellReadyText(Unit hero, int slot)
        {
            int full = State.ManaFull(hero, slot);
            if (full <= 0) return "No mana needed";
            int have = Mathf.Min(full, ShownMana(hero.Id, slot));
            return have >= full ? $"{full} / {full} mana" : $"{have} / {full} mana · charging {Mathf.FloorToInt(100f * have / full)}%";
        }
    }
}
