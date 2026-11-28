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

        private void AttachSlotInspect(VisualElement slot, int idx) => InspectCard.Attach(slot, body => SlotCard(body, idx));

        private bool SlotCard(VisualElement body, int idx)
        {
            var hero = State.UnitById(SelectedHeroId);
            var ab = hero == null ? null : BattleEngine.SlotAbility(State, hero, idx);
            if (ab != null) ReadSlot(idx);
            return InspectCard.Ability(body, ab, ab == null ? null : SlotState(hero, idx, ab));
        }

        /// <summary>GG-T450: a tapped slot that can't be aimed (recharging, no mana, no target …) still reads — its card
        /// opens on the tap. A slot that aims needs no card: the description panel shows the ability while it's aimed.</summary>
        private void TapSlotCard(int idx)
        {
            if (idx >= 0 && idx < slotViews.Count) InspectCard.Tap(slotViews[idx], body => SlotCard(body, idx));
        }

        /// <summary>The slot <see cref="SlotCard"/> last read without aiming, shown in the description panel (GG-T1823)
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
            int full = State.ManaFull(hero, slot);
            if (full > 0)
            {
                int have = ShownMana(hero.Id, slot);
                parts.Add(have >= full ? $"charged: {full} / {full} mana" : $"charging: {have} / {full} mana, fills with every gain");
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
            InspectCard.Attach(icon, body => BuildTimelineCard(body, e), tap: true); // GG-T450: a tap selects and reads
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
                int full = a.Kind == AbilityKind.Spell ? State.ManaFull(u, i) : 0;
                string when = sl.IsItem ? $"item · {sl.Charges}/{sl.MaxCharges} left"
                    : a.Kind == AbilityKind.Skill ? (cd > 0 ? $"skill · ready in {cd} turns" : "skill · ready")
                    : full > 0 ? $"spell · {ShownMana(u.Id, i)}/{full} mana" : "spell";
                var t = Ui.Text($"{name} — {when}", "t-caption", row);
                t.style.flexShrink = 1;
                t.style.whiteSpace = WhiteSpace.Normal;
            }
            return true;
        }

        /// <summary>
        /// GG-T450: a quick tap on a unit (no drag, no hold card, released on its own tile) opens its card after whatever
        /// the tap already did (a hero is selected, a foe fills the actor panel). The card stays until the next press
        /// anywhere, and that press still acts — picking the hero up to drag it closes the card and drags as before.
        /// A recruitable is left out: its tap talks to it.
        /// </summary>
        private void TapUnitCard(int unitId, Vector2 local)
        {
            var u = State.UnitById(unitId);
            if (u == null || !u.Alive || u.IsNeutral || !CanCommand() || TargetingSlot >= 0) return;
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
                var l = Ui.Text($"{u.Name}: {string.Join(" · ", parts)}", "t-caption", parent);
                l.name = "confirm-outcome";
                l.style.whiteSpace = WhiteSpace.Normal;
            }
            if (p.ManaMax > 0)
                Ui.Text($"Mana +{Range(p.ManaMin, p.ManaMax)} to every spell{(p.FillsMax > 0 ? $" · fills {Range(p.FillsMin, p.FillsMax)}" : "")}", "t-caption", parent).style.color = Palette.Mana;
        }

        /// <summary>Small damage tags over the foes a drop would pincer (and its KO odds), refreshed per drag step.</summary>
        private void ShowDragOutcome(MovePreview pv)
        {
            ClearOutcomeTags();
            if (pv.Pincers.Count == 0 || dragPath.Count == 0) return;
            var p = OutcomePreviewer.Preview(State, new MoveCommand { HeroId = dragHeroId, Path = new List<Coord>(dragPath) }, 3);
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
        /// the player may take their time reading the marks.</summary>
        private float HeldSeconds()
        {
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
