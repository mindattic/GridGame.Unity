using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T252 (POL-D1/D2): hold-to-inspect on the battle's units, slots, statuses and timeline icons, and the outcome
    /// preview on the confirm card and the drag preview. Presentation only — every number comes from
    /// <see cref="OutcomePreviewer"/>, which runs on clones and never touches the battle.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private int inspectUnitId = -1;
        private float inspectSince;
        private bool inspectOpen;
        private readonly List<VisualElement> outcomeTags = new List<VisualElement>();

        /// <summary>How much the party knows about <paramref name="u"/> (§25.11, GG-T290): a hero is fully known; any other
        /// unit shows what the save's Bestiary has learned from defeating its class (<see cref="BestiaryKnowledge"/>).</summary>
        private int Knowledge(Unit u) =>
            u.IsHero ? BestiaryKnowledge.Abilities : BestiaryKnowledge.Tier(App.Save, u.Def.Id);

        /// <summary>GG-T1895: a tapped slot that can't be aimed (recharging, no mana, no target ...) still reads, in the
        /// description panel (GG-T1823); no card pops over the board (§9.0).</summary>
        private void TapSlotCard(int idx)
        {
            var hero = State.UnitById(SelectedHeroId);
            if (hero != null && BattleEngine.SlotAbility(State, hero, idx) != null) ReadSlot(idx);
        }

        /// <summary>The slot <see cref="TapSlotCard"/> last read without aiming, shown in the description panel (GG-T1823)
        /// until another hero is selected, a unit is read or an aim takes the panel.</summary>
        private int readSlot = -1, readSlotHero = -1;

        private void ReadSlot(int idx)
        {
            if (readSlot == idx && readSlotHero == SelectedHeroId && panelUnitId < 0) return;
            readSlot = idx;
            readSlotHero = SelectedHeroId;
            panelUnitId = -1;
            if (TargetingSlot < 0) SyncPanel();
        }

        /// <summary>A slot's live state (GG-T1823): items left, recharge turns, how full its mana meter is, and why it can't be
        /// used right now, one line for its card and the description panel.</summary>
        private string SlotState(Unit hero, int slot, AbilityDef a)
        {
            var parts = new List<string>();
            var sl = slot >= 0 && slot < hero.Bar.Count ? hero.Bar[slot] : null;
            if (sl != null && sl.IsItem) parts.Add($"{sl.Charges} / {sl.MaxCharges} left");
            if (hero.Cooldowns.TryGetValue(a.Id, out var cd) && cd > 0) parts.Add($"recharging: ready in {cd} turn{(cd == 1 ? "" : "s")}");
            int price = State.SpellPrice(hero, slot);
            if (price > 0)
            {
                int pool = ShownPool;
                parts.Add(pool >= price ? $"costs {price} mana · bar {pool}" : $"costs {price} mana · bar {pool} · {price - pool} short");
            }
            var status = BattleEngine.GetSlotStatus(State, hero.Id, slot);
            if (status == SlotStatus.Ready) { if (TargetingSlot != slot) parts.Add($"tap to aim with {hero.Name}"); }
            else if (status != SlotStatus.Cooldown && status != SlotStatus.Unaffordable && status != SlotStatus.OutOfCharges) parts.Add(StatusText(status));
            if (parts.Count == 0) return null;
            string line = string.Join(" · ", parts);
            return char.ToUpperInvariant(line[0]) + line.Substring(1) + (line.EndsWith(".") || line.EndsWith("!") ? "" : ".");
        }

        private void AttachTimelineInspect(VisualElement icon, TimelineEntry e)
        {
            icon.pickingMode = PickingMode.Position;
            InspectCard.Attach(icon, body => !examining && BuildTimelineCard(body, e), tap: true); // GG-T450: a tap selects and reads
        }

        /// <summary>The hold card for a timeline entry (an icon or the prominence slot): a cast's ability, else its owner.</summary>
        private bool BuildTimelineCard(VisualElement body, TimelineEntry e)
        {
            if (e.Kind == TimelineKind.Hazard) return BuildHazardCard(body, e); // GG-T359
            var owner = State.UnitById(e.OwnerId);
            if (e.Kind == TimelineKind.Cast)
                return InspectCard.Ability(body, App.Content.TryAbility(e.AbilityId)?.ForCaster(owner?.Def), // GG-T1134
                    owner == null ? "Lands when it reaches the left edge." : $"{owner.Name}'s spell — lands when it reaches the left edge. Hit {owner.Name} to stagger it back.");
            return owner != null && InspectCard.Unit(body, State, owner, Knowledge(owner));
        }

        private void AttachStatusInspect(VisualElement pip, BuffDef d, int remaining)
        {
            pip.pickingMode = PickingMode.Position;
            InspectCard.Attach(pip, body => InspectCard.Buff(body, d, remaining), tap: true); // GG-T450
        }

        // ── Holding a unit on the board ─────────────────────────────────────────

        private void BeginBoardInspect(Unit u)
        {
            inspectUnitId = u.Id;
            inspectSince = Time.unscaledTime;
            inspectOpen = false;
        }

        private void TickBoardInspect()
        {
            if (inspectUnitId < 0 || inspectOpen || dragging) return;
            if (Time.unscaledTime - inspectSince < InspectCard.HoldSeconds) return;
            var u = State.UnitById(inspectUnitId);
            if (u == null || !u.Alive || !board.Units.TryGetValue(u.Id, out var v)) { inspectUnitId = -1; return; }
            inspectOpen = InspectCard.Show(v.Root, body => UnitSheet(body, u));
        }

        /// <summary>A unit's sheet (GG-T1825, the old Party page's readout): its stats and statuses with the turns they have
        /// left (<see cref="InspectCard.Unit"/>), and for a hero its skills, each with its kind and when it is ready.</summary>
        private bool UnitSheet(VisualElement body, Unit u)
        {
            if (!InspectCard.Unit(body, State, u, Knowledge(u))) return false;
            if (!u.IsHero) return true;
            var head = Ui.Text("Skills", "t-caption gold", body);
            head.style.marginTop = Space.S16;
            for (int i = 0; i < u.Bar.Count && i < u.UnlockedSlots; i++)
            {
                var sl = u.Bar[i];
                var a = sl.IsEmpty ? null : BattleEngine.SlotAbility(State, u, i);
                if (a == null) continue;
                var row = Ui.El("row", body);
                row.name = "sheet-skill" + i;
                row.style.alignItems = Align.Center;
                row.style.marginTop = Space.S8;
                var icon = Ui.El("menu-icon", row);
                icon.style.width = icon.style.height = Size.Pip;
                icon.style.marginRight = Space.S8;
                var tex = sl.IsItem ? Art.ItemIcon(sl.ItemId) : sl.IsWeapon ? Art.ItemIcon(sl.WeaponId) : null;
                icon.style.backgroundImage = new StyleBackground(tex ?? Art.AbilityIcon(a) ?? Art.Orb);
                string name = sl.IsWeapon ? App.Content.TryItem(sl.WeaponId)?.Name ?? a.Name : sl.IsItem ? App.Content.TryItem(sl.ItemId)?.Name ?? a.Name : a.Name;
                int cd = u.Cooldowns.TryGetValue(a.Id, out var c) ? c : 0;
                int price = a.Kind == AbilityKind.Spell ? State.SpellPrice(u, i) : 0;
                string when = sl.IsItem ? $"item · {sl.Charges}/{sl.MaxCharges} left"
                    : a.Kind == AbilityKind.Skill ? (cd > 0 ? $"skill · ready in {cd} turns" : "skill · ready")
                    : price > 0 ? $"spell · costs {price} mana · bar {ShownPool}" : "spell";
                var t = Ui.Text($"{name} — {when}", "t-caption", row);
                t.style.flexShrink = 1;
                t.style.whiteSpace = WhiteSpace.Normal;
            }
            return true;
        }

        /// <summary>
        /// GG-T450: a quick tap on a foe (no drag, no hold card, released on its own tile) opens its card after whatever
        /// the tap already did. A hero's tap only selects it into the description panel (GG-T1905, BIBLE 9.0: no card over
        /// the board); its sheet is the long-press. The card stays until the next press
        /// anywhere, and that press still acts — picking the hero up to drag it closes the card and drags as before.
        /// A recruitable is left out: its tap talks to it.
        /// </summary>
        private void TapUnitCard(int unitId, Vector2 local)
        {
            var u = State.UnitById(unitId);
            if (u == null || !u.Alive || u.IsNeutral || u.IsHero || !CanCommand() || TargetingSlot >= 0) return;
            if (board.TileAt(local) != u.Pos || !board.Units.TryGetValue(u.Id, out var v) || v.Dead) return;
            InspectCard.Tap(v.Root, body => UnitSheet(body, u));
        }

        /// <summary>Ends a board hold; true when it had opened a card (the release then does nothing else).</summary>
        private bool EndBoardInspect()
        {
            bool was = inspectOpen;
            inspectUnitId = -1;
            inspectOpen = false;
            if (was) InspectCard.Hide();
            return was;
        }

        // ── Examine mode (GG-T1918, §9.0): Info, then tap anything ─────────────

        /// <summary>Examine mode is on: board and timeline taps read what they land on into the description panel and
        /// never act, and <see cref="CanCommand"/> is false, so no battle time passes until it closes.</summary>
        private bool examining;
        /// <summary>The card the last examine tap picked (null = the prompt alone); rebuilt on every panel sync so it stays live.</summary>
        private Func<VisualElement, bool> examineCard;
        private string examineNote;
        private VisualElement infoButton, examineTint;

        /// <summary>The Info button: opens Examine mode, or closes it when it is already on.</summary>
        private void ToggleExamine()
        {
            if (examining) { ExitExamine(); App.Audio.Sfx("ui.cancel"); return; }
            if (ended || State.IsOver || dragging || Paused) { App.Audio.Sfx("ui.error"); return; }
            CancelTargeting();
            ClearTapMove();
            ClearHint();
            EndBoardInspect();
            InspectCard.Hide();
            examining = true;
            examineCard = null;
            examineNote = null;
            if (examineTint == null || examineTint.parent != board.Root)
            {
                examineTint = new VisualElement { name = "examine-tint", pickingMode = PickingMode.Ignore };
                examineTint.style.position = Position.Absolute;
                examineTint.style.left = examineTint.style.top = examineTint.style.right = examineTint.style.bottom = Space.S0;
                examineTint.style.backgroundColor = Palette.Info.WithA(0.14f);
                board.Root.Add(examineTint);
            }
            examineTint.BringToFront();
            examineTint.style.display = DisplayStyle.Flex;
            SyncInfoButton();
            App.Audio.Sfx("ui.click");
            SyncPanel();
        }

        /// <summary>Closes Examine mode (Info again, Abilities or GUARD); a no-op when it is off.</summary>
        private void ExitExamine()
        {
            if (!examining) return;
            examining = false;
            examineCard = null;
            examineNote = null;
            if (examineTint != null) examineTint.style.display = DisplayStyle.None;
            ClearTapArrow();
            SyncInfoButton();
            SyncPanel();
        }

        private void SyncInfoButton()
        {
            if (infoButton == null) return;
            var edge = examining ? new StyleColor(Palette.Gold) : new StyleColor(StyleKeyword.Null);
            infoButton.style.borderTopColor = infoButton.style.borderBottomColor = infoButton.style.borderLeftColor = infoButton.style.borderRightColor = edge;
        }

        /// <summary>An examine tap on the board: the unit there (its sheet, gated by the Bestiary), else a pickup, else a
        /// queued hazard aimed at the tile, else the region mark painted on it.</summary>
        private void ExamineTile(Coord tile)
        {
            examineNote = null;
            examineCard = null;
            var u = State.UnitAt(tile);
            var mark = State.Region?.At(tile);
            if (u != null)
            {
                int id = u.Id;
                examineCard = b => State.UnitById(id) is Unit x && x.Alive && UnitSheet(b, x);
                if (!ReduceMotion && board.Units.TryGetValue(id, out var v) && !v.Dead) App.StartCoroutine(Tween.Pop(v.Root, 1.08f, Timing.Quick));
            }
            else if (State.PickupAt(tile) is Pickup p)
            {
                int pid = p.Id;
                examineCard = b => State.Pickups.FirstOrDefault(x => x.Id == pid) is Pickup q && PickupCard(b, q);
            }
            else if (State.Timeline.FirstOrDefault(e => e.Kind == TimelineKind.Hazard && e.HazardTiles != null && e.HazardTiles.Contains(tile)) is TimelineEntry he)
                examineCard = b => BuildHazardCard(b, he);
            else if (RegionBoard.Sees(mark, Team.Hero))
                examineCard = b => MarkCard(b, mark);
            else examineNote = "Nothing here: an empty tile.";
            App.Audio.Sfx("ui.click");
            SyncPanel();
        }

        /// <summary>An examine tap on a timeline entry: the tap arrow to its owner (GG-T2118) and that turn, cast or hazard
        /// explained in the panel.</summary>
        private void ExamineTimeline(VisualElement from, TimelineEntry e)
        {
            ClearTapArrow();
            var owner = State.UnitById(e.OwnerId);
            if (owner != null && owner.Alive && board.Units.TryGetValue(owner.Id, out var v) && !v.Dead) ShowTapArrow(from, owner.Id);
            examineNote = null;
            examineCard = b => BuildTimelineCard(b, e);
            App.Audio.Sfx("ui.click");
            SyncPanel();
        }

        /// <summary>The panel in Examine mode: the prompt, then the examined card, else the region rules in play, each a tap
        /// to read it.</summary>
        private void ExaminePanel()
        {
            var head = Ui.Text(examineCard == null ? "Examine: tap anything" : "Examine: tap anything else · tap Info to close", "small gold", panel);
            head.name = "examine-prompt";
            head.style.whiteSpace = WhiteSpace.Normal;
            head.style.marginBottom = Space.S8;
            if (examineCard != null)
            {
                var card = Ui.El("col", panel);
                card.name = "examine-card";
                if (examineCard(card)) return;
                card.RemoveFromHierarchy();
                examineCard = null;
            }
            if (examineNote != null) PanelLine(panel, examineNote, "tiny dim", "examine-note");
            var rules = State.Region?.Rules;
            if (rules == null) return;
            foreach (var id in rules)
            {
                var rule = App.Content.TryRegionRule(id);
                if (rule == null || string.IsNullOrEmpty(rule.Name)) continue;
                var row = Ui.Text($"Rule: {rule.Name}", "small", panel);
                row.name = "examine-rule";
                row.style.marginTop = Space.S8;
                Ui.Pressable(row);
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    examineNote = null;
                    examineCard = b => InspectCard.Explain(b, null, rule.Name, "Region rule", Palette.Info, rule.Description, Enumerable.Empty<string>());
                    App.Audio.Sfx("ui.click");
                    SyncPanel();
                });
            }
        }

        private bool PickupCard(VisualElement body, Pickup p)
        {
            App.Content.Pickups.TryGetValue(p.PickupId ?? "", out var def);
            return InspectCard.Explain(body, Art.UiIcon(def?.Icon), def?.Name ?? "Pickup", "Pickup", Art.HexColor(def?.Color, Palette.Gold), def?.Description,
                new[] { "Walk a hero over it to grab it.", $"Fades in {p.ActionsLeft} action{(p.ActionsLeft == 1 ? "" : "s")}." });
        }

        /// <summary>A painted region mark: the rule that painted it and what the mark does to whoever stands on it.</summary>
        private bool MarkCard(VisualElement body, TileMark m)
        {
            var def = App.Content.TryTileState(m.State);
            var rule = App.Content.TryRegionRule(m.Rule);
            var bullets = new List<string>();
            if (m.Blocks) bullets.Add("Blocks the tile: nothing can enter it.");
            if (m.Hazard) bullets.Add("Harmful to stand on.");
            if (m.Counter > 0 && def != null && def.Threshold > 0) bullets.Add($"Counter {m.Counter} / {def.Threshold}.");
            return InspectCard.Explain(body, null, rule?.Name ?? m.State, m.Hazard ? "Hazard" : "Marked tile", Art.HexColor(def?.Color, Palette.Info), rule?.Description, bullets);
        }

        // ── Outcome preview ─────────────────────────────────────────────────────

        private static string Range(int a, int b) => a == b ? a.ToString() : $"{a}–{b}";

        /// <summary>One line per unit the pending pick would touch: damage or heal range, KO chance, statuses, orbs.</summary>
        private void OutcomeLines(VisualElement parent, AbilityDef a)
        {
            var hero = State.UnitById(SelectedHeroId);
            if (hero == null || !PendingTarget.HasValue) return;
            var p = OutcomePreviewer.Preview(State, new UseSlotCommand { HeroId = hero.Id, Slot = TargetingSlot, Target = PendingTarget.Value }, 6);
            if (p.Refused) return;
            int shown = 0;
            foreach (var t in p.Targets.OrderByDescending(t => t.DamageMax + t.HealMax))
            {
                var u = State.UnitById(t.UnitId);
                if (u == null || (t.DamageMax == 0 && t.HealMax == 0 && t.StatusChance.Count == 0)) continue;
                if (++shown > 3) break;
                var parts = new List<string>();
                if (t.DamageMax > 0) parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Palette.Bad)}>−{Range(t.DamageMin, t.DamageMax)}</color>");
                if (t.HealMax > 0) parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Palette.Good)}>+{Range(t.HealMin, t.HealMax)}</color>");
                if (t.KillChance > 0f) parts.Add(t.KillChance >= 1f ? "KO" : $"KO {Mathf.RoundToInt(t.KillChance * 100)}%");
                foreach (var kv in t.StatusChance)
                    parts.Add($"{App.Content.TryBuff(kv.Key)?.Name ?? kv.Key}{(kv.Value < 1f ? $" {Mathf.RoundToInt(kv.Value * 100)}%" : "")}");
                // Each value stays whole on its line: a damage range or a status name never breaks (GG-T1903).
                var l = Ui.Text($"{u.Name}: {string.Join(" · ", parts.Select(x => $"<nobr>{x}</nobr>"))}", "t-caption", parent);
                l.name = "confirm-outcome";
                l.style.whiteSpace = WhiteSpace.Normal;
            }
            if (p.ManaMax > 0)
            {
                var m = Ui.Text($"Mana <nobr>+{Range(p.ManaMin, p.ManaMax)}</nobr>{(p.FillsMax > 0 ? $" · <nobr>{Range(p.FillsMin, p.FillsMax)} spell{(p.FillsMax == 1 ? "" : "s")} affordable</nobr>" : "")}", "t-caption", parent);
                m.style.color = Palette.Mana;
                m.style.whiteSpace = WhiteSpace.Normal;
            }
        }

        /// <summary>Small damage tags over the foes a drop would pincer (and its KO odds), refreshed per drag step or tap-move pick.</summary>
        private void ShowDragOutcome(MovePreview pv, int heroId, List<Coord> path)
        {
            ClearOutcomeTags();
            if (pv.Pincers.Count == 0 || path.Count == 0) return;
            var p = OutcomePreviewer.Preview(State, new MoveCommand { HeroId = heroId, Path = new List<Coord>(path) }, 3);
            if (p.Refused) return;
            foreach (var t in p.Targets)
            {
                var u = State.UnitById(t.UnitId);
                if (u == null || u.IsHero || t.DamageMax <= 0) continue;
                var at = pv.Positions.TryGetValue(u.Id, out var pos) ? pos : u.Pos;
                var c = BoardView.CenterOf(at);
                string text = $"−{Range(t.DamageMin, t.DamageMax)}" + (t.KillChance > 0f ? (t.KillChance >= 1f ? " KO" : $" KO {Mathf.RoundToInt(t.KillChance * 100)}%") : "");
                var tag = new Label(text) { name = "outcome-tag", pickingMode = PickingMode.Ignore };
                tag.AddToClassList("shadow");
                tag.style.position = Position.Absolute;
                tag.style.width = BoardView.Tile * 1.6f;
                tag.style.left = c.x - BoardView.Tile * 0.8f;
                tag.style.top = c.y - BoardView.Tile * 0.46f; // inside the foe's own tile, along its top edge
                tag.style.unityTextAlign = TextAnchor.MiddleCenter;
                tag.style.fontSize = TypeScale.Base;
                tag.style.color = t.KillChance > 0f ? Palette.Gold : Palette.Text;
                Fonts.UseHeavy(tag);
                board.FxLayer.Add(tag);
                outcomeTags.Add(tag);
            }
        }

        private void ClearOutcomeTags()
        {
            foreach (var t in outcomeTags) t.RemoveFromHierarchy();
            outcomeTags.Clear();
        }

        // ── Cancelling a drag ───────────────────────────────────────────────────

        /// <summary>A drop off the board, or back on the start tile with nothing shoved, is a cancel: no command, no clock.</summary>
        private bool DropCancels(Vector2 local)
        {
            bool offBoard = local.x < 0 || local.y < 0 || local.x > BoardView.Tile * BattleConfig.Cols || local.y > BoardView.Tile * BattleConfig.Rows;
            var hero = State.UnitById(dragHeroId);
            if (hero == null) return false;
            bool home = dragTile == hero.Pos && (preview == null || ShovePreview.Of(State, dragHeroId, preview).Displaced.Count == 0);
            return offBoard || home;
        }

        /// <summary>The seconds held that travel with a drop. A coached drag (GG-T539) never counts as running the window out:
        /// the player may take their time reading the marks. Untimed (GG-T1978) it is the human drag model's seconds for the
        /// path, kept inside the window so it is never a forced drop.</summary>
        private float HeldSeconds()
        {
            if (Untimed && !simulatingDrag)
            {
                float model = holdElapsed + BattleEngine.DragSeconds(dragPath.Count);
                return holdWindow > 0f ? Mathf.Min(model, holdWindow * 0.99f) : model;
            }
            float held = holdWindow > 0f ? Mathf.Min(holdElapsed, holdWindow) : holdElapsed;
            if ((CoachRail || coachCancelStep) && holdWindow > 0f) held = Mathf.Min(held, holdWindow * 0.9f);
            return held;
        }

        private void CancelDrag()
        {
            if (!dragging) return;
            float held = HeldSeconds();
            // A forced drop still ends the turn (§2.0), except under a coach mark (GG-T539), which never forces one.
            if (BattleEngine.IsTimeUp(State, held) && !CoachRail && !coachCancelStep) { CommitDrag(); return; }
            dragging = false;
            pressing = false;
            board.Lines.ClearLines();
            board.ClearMarks();
            ClearOutcomeTags();
            var v = board.Units[dragHeroId];
            board.Trail.Release(v.Root);
            v.Root.style.scale = new Scale(Vector2.one);
            EndHold();
            board.Place(v, State.UnitById(dragHeroId).Pos);
            dragPath.Clear();
            preview = null;
            App.Audio.Sfx("ui.cancel");
            ShowBanner("Move cancelled");
            SyncAll();
        }
    }
}
