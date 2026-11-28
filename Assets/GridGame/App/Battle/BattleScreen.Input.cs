using System;
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
    public sealed partial class BattleScreen
    {
        private const float DragThreshold = 18f;
        // Diagonal slip (BattleConfig.DiagonalSlipEnabled): how fast and how close to 45° a drag must be,
        // frame to frame, to offer a diagonal step instead of the safe two-cardinal-step route.
        private const float DiagonalSlipMinSpeed = BoardView.Tile * 6f; // px/s
        private const float DiagonalSlipAngleTolerance = 12f; // degrees off a true 45°

        /// <summary>The finger (or mouse) that owns the current press; a second finger's move/up is ignored, so it can't
        /// hijack a drag or commit an aim at its own tile (-1 = none).</summary>
        private int activePointerId = -1;

        /// <summary>Outside the board's tiles (TileAt clamps, so a release past the edge would read as an edge tile).</summary>
        private static bool OffBoard(Vector2 local) =>
            local.x < 0 || local.y < 0 || local.x > BoardView.Tile * BattleConfig.Cols || local.y > BoardView.Tile * BattleConfig.Rows;

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (TapSkipsPincerBeats()) return; // GG-T836: a tap during a pincer skips its beats ahead
            ClearHint();
            DropAdvice();
            DropRefusal();
            if (!CanCommand() || pressing) return;
            activePointerId = evt.pointerId;
            var tile = board.TileAt(evt.localPosition);
            var unit = State.UnitAt(tile);

            if (TargetingSlot >= 0)
            {
                // Show the area this pick would hit; commit on release over the same tile.
                pressing = true;
                pressPos = evt.localPosition;
                dragTile = tile;
                PreviewTarget(tile);
                board.Root.CapturePointer(evt.pointerId);
                return;
            }

            if (unit != null)
            {
                BeginBoardInspect(unit);
                // The release must come back here even off the board, or the hold timer opens a card after the lift.
                board.Root.CapturePointer(evt.pointerId);
            }
            if (unit != null && unit.IsIndependent) { panelUnitId = unit.Id; SyncPanel(); ShowBanner(unit.Wanderer ? $"{unit.Name} is a wanderer: they fight on their own. Pincer beside them or heal them to earn their trust" : $"{unit.Name} is tamed: it fights on its own"); return; } // GG-T303, GG-T342
            if (unit != null && unit.IsHero)
            {
                SelectHero(unit.Id);
                // GG-T539: a tap mark on a hero only selects it; a move mark lifts only the hero it is for.
                if (CoachBlocksDrag || (coachHeroId >= 0 && unit.Id != coachHeroId)) return;
                if (!BattleEngine.CanMove(State, unit.Id))
                {
                    bool acted = TurnRules.HasActed(State, unit); // §2.13: the turn mode says who has acted
                    if (TurnRules.NotThisHerosTurn(State, unit)) // GG-T814 (§2.13.3): only the hero whose own icon arrived may be picked up
                    {
                        var active = State.UnitById(State.ActiveHeroId);
                        ShowBanner(active != null && active.Alive ? $"It's {active.Name}'s turn · {unit.Name} waits for its own icon" : $"{unit.Name} waits for its own icon");
                        App.Audio.Sfx("ui.error", 0.5f);
                        return;
                    }
                    if (!acted && !unit.IsImmobile && !BattleEngine.ActionFits(State, BattleEngine.MinDragSeconds)) { TooLittleTimeCue(); return; } // GG-T311
                    ShowBanner(!acted ? $"{unit.Name} can't move" : TurnRules.MovedThisRound(State, unit) ? $"{unit.Name} has moved this round · free again in round {State.Round + 1}" // GG-T800 (§2.13.2)
                        : TurnRules.OneHeroPerTurn(State) ? "The party has acted this turn" : $"{unit.Name} has acted this turn"); // GG-T787
                    return;
                }
                pressing = true;
                dragHeroId = unit.Id;
                pressPos = evt.localPosition;
                dragTile = unit.Pos;
                dragPath.Clear();
                preview = null;
                lastPointerPos = evt.localPosition;
                lastPointerTime = Time.unscaledTime;
                board.Root.CapturePointer(evt.pointerId);
            }
            else if (unit != null && unit.IsNeutral)
            {
                // Talk goes on the tap's release (OnPointerUp), never on the press or a hold.
                panelUnitId = unit.Id;
                SyncPanel();
            }
            else if (unit != null)
            {
                panelUnitId = unit.Id;
                SyncPanel();
                App.Audio.Sfx("ui.click");
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!pressing || Paused) return; // a drag held through Escape-to-pause stays frozen (GG-T126)
            if (activePointerId >= 0 && evt.pointerId != activePointerId) return; // a second finger never steers the press
            Vector2 local = evt.localPosition;
            if (TargetingSlot >= 0)
            {
                var t = board.TileAt(local);
                if (t != dragTile) { dragTile = t; PreviewTarget(t); }
                return;
            }
            HandlePointerPosition(local);
        }

        /// <summary>
        /// The real drag-following/diagonal-slip-gating logic, factored out of <see cref="OnPointerMove"/> so a
        /// simulated human cursor (<see cref="SimulatedHumanDrag"/>) can drive the exact same code a real
        /// pointer does — a diagonal slip succeeds or fails there for the same kinematic reasons either way,
        /// never by being told the outcome.
        /// </summary>
        private void HandlePointerPosition(Vector2 local)
        {
            if (!pressing) return; // the press was reset (a rebuild, a drop): a late simulated step must not pick the hero up again
            if (!dragging)
            {
                if ((local - pressPos).magnitude < DragThreshold) return;
                EndBoardInspect();
                BeginDrag();
            }
            // Diagonal slip tech: a fast, near-45° flick between polled frames offers a diagonal step;
            // anything slower/straighter falls back to the safe two-cardinal-step route, unchanged.
            var rawDelta = local - lastPointerPos;
            float dt = Mathf.Max(0.0001f, Time.unscaledTime - lastPointerTime);
            float speed = rawDelta.magnitude / dt;
            float angleFromDiagonal = Mathf.Abs(Mathf.Atan2(Mathf.Abs(rawDelta.y), Mathf.Max(0.0001f, Mathf.Abs(rawDelta.x))) * Mathf.Rad2Deg - 45f);
            bool fastDiagonal = BattleConfig.DiagonalSlipEnabled && speed > DiagonalSlipMinSpeed && angleFromDiagonal < DiagonalSlipAngleTolerance;
            lastPointerPos = local;
            lastPointerTime = Time.unscaledTime;
            var v = board.Units[dragHeroId];
            if (CoachRail)
            {
                // GG-T539: a coached move rides its dotted route: the token follows the finger's nearest point on it, and
                // the path grows only along the route.
                var rail = OnRail(local);
                v.Root.style.left = rail.x - BoardView.Tile / 2f;
                v.Root.style.top = rail.y - BoardView.Tile / 2f;
                ExtendOnRail(board.TileAt(rail));
                return;
            }
            // The hero follows the finger, lifted slightly so it isn't hidden under it.
            v.Root.style.left = Mathf.Clamp(local.x - BoardView.Tile / 2f, -BoardView.Tile * 0.3f, BoardView.Tile * (BattleConfig.Cols - 0.7f));
            v.Root.style.top = Mathf.Clamp(local.y - BoardView.Tile * 0.62f, -BoardView.Tile * 0.3f, BoardView.Tile * (BattleConfig.Rows - 0.7f));
            var over = board.TileAt(new Vector2(local.x, local.y - BoardView.Tile * 0.12f));
            ExtendPath(over, fastDiagonal);
        }

        /// <summary>Starts a drag on <paramref name="heroId"/> with no real pointer to capture — used by the
        /// simulated human cursor (<see cref="SimulatedHumanDrag"/>) instead of <see cref="OnPointerDown"/>.</summary>
        private bool BeginSimulatedDrag(int heroId)
        {
            var hero = State.UnitById(heroId);
            if (hero == null || !hero.IsHero || !BattleEngine.CanMove(State, heroId)) return false;
            ClearHint();
            SelectHero(heroId);
            pressing = true;
            dragHeroId = heroId;
            var startPos = BoardView.CenterOf(hero.Pos);
            pressPos = startPos;
            dragTile = hero.Pos;
            dragPath.Clear();
            preview = null;
            lastPointerPos = startPos;
            lastPointerTime = Time.unscaledTime;
            return true;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            // Release first: when the drag timer auto-commits mid-drag (CommitDrag clears `pressing`), the
            // finger/mouse is still captured — returning before this left board.Root holding the capture, so
            // the next click anywhere (a slot, GUARD, the menu) was routed to the board instead (GG-T70).
            // A second finger lifting never ends (or commits) the first finger's press.
            if (activePointerId >= 0 && evt.pointerId != activePointerId) return;
            activePointerId = -1;
            bool wasPressing = pressing;
            if (board.Root.HasPointerCapture(evt.pointerId)) board.Root.ReleasePointer(evt.pointerId);
            // A release while paused (hold, Escape, let go) can't commit: the drag goes back where it started.
            if (Paused)
            {
                EndBoardInspect();
                AbortDrag();
                pressing = false;
                return;
            }
            // A press still waiting to become a hold, never dragged, is a tap: it reads the unit too (GG-T450).
            int tapUnit = inspectUnitId >= 0 && !inspectOpen && !dragging ? inspectUnitId : -1;
            // A hold that opened an inspect card only read it: the release does nothing else (GG-T252).
            if (EndBoardInspect() && !dragging) { pressing = false; return; }
            if (tapUnit >= 0)
            {
                // A recruitable's tap talks to it — on the release over it, not on the press or a hold.
                var tu = State.UnitById(tapUnit);
                if (tu != null && tu.IsNeutral)
                {
                    if (tu.Alive && CanCommand() && TargetingSlot < 0 && !OffBoard(evt.localPosition) && State.UnitAt(board.TileAt(evt.localPosition)) == tu)
                        TapRecruitable(tu);
                }
                else TapUnitCard(tapUnit, evt.localPosition);
            }
            if (!wasPressing) return;
            if (TargetingSlot >= 0)
            {
                pressing = false;
                // A release off the board is a cancel of this pick, not a pick of the clamped edge tile.
                if (OffBoard(evt.localPosition)) { RedrawTargeting(); return; }
                var t = board.TileAt(evt.localPosition);
                var hero = State.UnitById(SelectedHeroId);
                var a = hero == null ? null : BattleEngine.SlotAbility(State, hero, TargetingSlot);
                bool legal = hero != null && a != null && a.Mode != TargetMode.Auto && TargetResolver.IsValidPick(State, a, hero, t);
                if (legal && (!ConfirmActions || AutoPlay)) Commit(new UseSlotCommand { HeroId = hero.Id, Slot = TargetingSlot, Target = t });
                else if (legal && PendingTarget == t) ConfirmPending(); // a second tap on the same pick confirms
                else if (legal) SetPending(t);
                else
                {
                    if (a == null || a.Mode != TargetMode.Auto) App.Audio.Sfx("ui.error");
                    RedrawTargeting();
                }
                return;
            }
            if (dragging)
            {
                // GG-T539: during the hold-timer mark every drop is the cancel it teaches; a coached move drops only on its
                // marked tile (anywhere else puts the hero back, no time spent).
                if (coachCancelStep) { CancelDrag(); coachCancelDone = true; }
                else if (CoachRail && dragTile != CoachDest) { CancelDrag(); ShowBanner("Drop it on the marked tile"); }
                else if (DropCancels(evt.localPosition) && !CoachRail) CancelDrag();
                else CommitDrag();
            }
            pressing = false;
        }

        private void BeginDrag()
        {
            dragging = true;
            dragTimeLeft = BattleConfig.DragTimerSeconds;
            var v = board.Units[dragHeroId];
            v.Root.BringToFront();
            v.Root.style.scale = new Scale(new Vector2(1.12f, 1.12f));
            v.SetGlow(Palette.GoldLight, 0.9f);
            board.Trail.Follow(v.Root, HeroTrail);
            App.Audio.Sfx("battle.pickup");
            ShowBanner("");
            BeginHold();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // The held turn (GG-T188, §2.0): picking a hero up runs the timeline in real time. The party's window lasts until
        // the next enemy icon reaches the left edge; a hold that runs it out is dropped where it is (forced drop) and that
        // enemy acts. Core never ticks during the hold — the view plays the icons forward (BattleEngine.TimelineUAfter)
        // and the drop's MoveCommand carries the seconds held; the engine spends that share of the window (GG-T308), and
        // the heroes who haven't acted yet go on with whatever is left.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// GG-T308: control came back. Mid-window the selection moves on to a hero who can still act, and a banner says
        /// who is left and how long the window still runs; a new window just lets everyone act again.
        /// </summary>
        private void WindowContinues(bool newWindow)
        {
            SyncGuardCall();
            if (State.IsOver) return;
            if (BattleEngine.TooLittleTime(State)) { TooLittleTimeCue(); return; } // GG-T311: the gap fits no action
            if (TurnRules.OneHeroPerTurn(State)) { AnyHeroTurn(); return; } // GG-T787 (§2.13.1): every control return is a new one-action turn
            if (newWindow) return;
            var left = BattleEngine.HeroesLeftToAct(State);
            if (left.Count == 0) return;
            var sel = State.UnitById(SelectedHeroId);
            if (sel == null || !sel.Alive || TurnRules.HasActed(State, sel)) SelectHero(left[0].Id);
            float w = BattleEngine.TurnTicks(State) / (float)BattleConfig.TicksPerSecond;
            string who = left.Count == 1 ? left[0].Name : $"{left.Count} heroes";
            ShowBanner(w > 0f ? $"{who} can still act · {w:0.0}s left" : $"{who} can still act");
            App.Audio.Sfx("ui.select", 0.5f);
        }

        /// <summary>
        /// GG-T787 (§2.13.1): a party turn of one action by any hero. The selection stays on the hero who acted last (the
        /// same hero may go again), unless it fell or can't act; the banner says so with the seconds the gap runs.
        /// </summary>
        private void AnyHeroTurn()
        {
            var left = BattleEngine.HeroesLeftToAct(State);
            if (left.Count == 0) return;
            if (!left.Exists(h => h.Id == SelectedHeroId)) SelectHero(left[0].Id);
            float w = BattleEngine.TurnTicks(State) / (float)BattleConfig.TicksPerSecond;
            ShowBanner(w > 0f ? $"Move any hero · {w:0.0}s left" : "Move any hero");
        }

        /// <summary>
        /// GG-T311 (§2.0): the gap until the next enemy reaches the left is shorter than any hero's action (a minimum drag,
        /// a skill's use time) — say so plainly with the gap's seconds; GUARD is the lit choice (<see cref="SyncGuardCall"/>).
        /// </summary>
        private void TooLittleTimeCue()
        {
            float gap = BattleEngine.TurnTicks(State) / (float)BattleConfig.TicksPerSecond;
            string rally = State.Rally > 0 ? $" · Rally {State.Rally}: foes slowed" : "";
            string anyone = TurnRules.OneHeroPerTurn(State) ? " for any hero" : ""; // GG-T787: the one action fits nobody
            ShowBanner($"Too little time{anyone} — Guard ({gap:0.0}s gap, a move needs {BattleEngine.MinDragSeconds:0.0#}s){rally}");
            App.Audio.Sfx("ui.error", 0.5f);
            SyncGuardCall();
        }

        /// <summary>GG-T311: GUARD glows gold while it is the party's only choice (a gap too short for any action).</summary>
        private void SyncGuardCall()
        {
            var guard = guardButton;
            if (guard == null || State == null) return;
            bool forced = BattleEngine.TooLittleTime(State);
            guard.EnableInClassList("guard--forced", forced);
            guard.style.borderTopColor = guard.style.borderBottomColor = guard.style.borderLeftColor = guard.style.borderRightColor =
                forced ? Palette.Gold : GuardEdge;
            guard.style.backgroundColor = forced ? new StyleColor(Palette.Gold.WithA(0.35f)) : new StyleColor(StyleKeyword.Null);
        }

        /// <summary>GG-T1978 (§2.0.1): the player's turns have no real-time limit — the hold and Mode C turn clocks stand
        /// still, no drop or Guard is forced, and a drop reports the human drag model's seconds (<see cref="HeldSeconds"/>).
        /// The bot's own simulated drag keeps its timed hold.</summary>
        private bool Untimed => App.Settings.UntimedTurns;

        private float holdElapsed, holdWindow = -1f, holdLastTime;
        private int holdTurnTicks = -1;
        private VisualElement holdDrain, holdDrainFill, holdLane;
        private Label holdLabel;
        private IVisualElementScheduledItem holdTicker;
        private static readonly Color HoldFull = Palette.Good, HoldEmpty = new Color(1f, 0.3f, 0.25f);

        private void BeginHold()
        {
            EndHold();
            holdElapsed = TurnRules.HeroesOnTimeline(State) ? heroTurnElapsed : 0f; // GG-T1183: the 4 s began with the turn, so a pickup carries on from where it is
            holdLastTime = Time.unscaledTime;
            holdTurnTicks = BattleEngine.TurnTicks(State);
            holdWindow = BattleEngine.TurnWindowSeconds(State);
            if (!board.Units.TryGetValue(dragHeroId, out var v)) return;

            // A drain bar under the held hero: the share of the turn left.
            holdDrain = new VisualElement { name = "hold-drain", pickingMode = PickingMode.Ignore };
            holdDrain.style.position = Position.Absolute;
            holdDrain.style.left = Length.Percent(8); holdDrain.style.right = Length.Percent(8);
            holdDrain.style.bottom = -Space.S16; holdDrain.style.height = Size.Gauge;
            holdDrain.style.backgroundColor = Palette.Ink.WithA(0.85f);
            Round(holdDrain, 7);
            holdDrainFill = new VisualElement { name = "hold-drain-fill", pickingMode = PickingMode.Ignore };
            holdDrainFill.style.position = Position.Absolute;
            holdDrainFill.style.left = Space.S4; holdDrainFill.style.top = Space.S4; holdDrainFill.style.bottom = Space.S4;
            holdDrainFill.style.width = Length.Percent(100);
            Round(holdDrainFill, 5);
            holdDrain.Add(holdDrainFill);
            if (Untimed) holdDrain.style.display = DisplayStyle.None; // nothing drains
            v.Root.Add(holdDrain);

            // On the timeline: the stretch between the left edge (acting) and the next enemy icon, and the seconds it stands for.
            holdLane = new VisualElement { name = "hold-lane", pickingMode = PickingMode.Ignore };
            holdLane.style.position = Position.Absolute;
            holdLane.style.left = XOfU(0f); holdLane.style.top = TrackY - Size.Gauge / 2f; holdLane.style.height = Size.Gauge;
            holdLane.style.backgroundColor = Palette.Gold.WithA(0.55f);
            Round(holdLane, 7);
            timelineFrame.Insert(1, holdLane); // above the track, under the icons
            holdLabel = Ui.Text("", "tiny warn shadow", timelineFrame);
            holdLabel.name = "hold-time";
            holdLabel.pickingMode = PickingMode.Ignore;
            holdLabel.style.position = Position.Absolute;
            holdLabel.style.top = TrackY + IconSpan / 2f - Space.S8;
            UpdateHold();
            holdTicker = timelineFrame.schedule.Execute(TickHold).Every(16);
        }

        private static void Round(VisualElement e, float r) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        private float heroTurnElapsed;
        private int heroTurnHero = -1;

        /// <summary>GG-T1183 (§2.13.3): a Mode C hero's 4 s starts when its icon arrives, held or not. Untouched at 0 s the turn
        /// resolves itself as a Guard; a pickup carries the elapsed time into the hold (BeginHold), which forces its own drop.</summary>
        private void TickHeroTurn(float dt)
        {
            if (State == null || !TurnRules.HeroesOnTimeline(State) || State.IsOver || State.Phase != BattlePhase.PlayerInput || State.ActiveHeroId < 0 || playing || ended)
            {
                heroTurnHero = -1;
                return;
            }
            if (heroTurnHero != State.ActiveHeroId) { heroTurnHero = State.ActiveHeroId; heroTurnElapsed = 0f; }
            if (dragging) { heroTurnElapsed = holdElapsed; return; }
            if (!Untimed) heroTurnElapsed += dt;
            float window = BattleEngine.TurnWindowSeconds(State);
            if (heroTurnElapsed >= window && window > 0f && !pressing && TargetingSlot < 0 && !AutoPlay && !CoachRail && !coachCancelStep && CanCommand())
            {
                heroTurnHero = -1;
                App.Audio.Sfx("ui.error");
                ShowBanner("Time's up!");
                Commit(new ShieldCommand());
                return;
            }
            SyncGap(null, false);
        }

        private void TickHold()
        {
            float now = Time.unscaledTime, dt = now - holdLastTime;
            holdLastTime = now;
            if (!dragging) { EndHold(); return; }
            if (Paused) return; // a paused battle holds the clock too (GG-T126)
            if (Untimed && !simulatingDrag) { UpdateHold(); return; }
            holdElapsed += dt;
            UpdateHold();
            // The bot's simulated drag already fits its path inside the window (HeroBot), and its coroutine owns the drop.
            // A coached drag (GG-T539) is never forced: the lesson waits for the player's own drop.
            if (holdWindow > 0f && holdElapsed >= holdWindow && !simulatingDrag && !CoachRail && !coachCancelStep)
            {
                App.Audio.Sfx("ui.error");
                ShowBanner("Time's up!");
                CommitDrag();
            }
        }

        private void UpdateHold()
        {
            if (holdDrainFill == null) return;
            float k = holdWindow > 0f ? Mathf.Clamp01(holdElapsed / holdWindow) : 0f;
            bool frozen = TurnRules.HeroesOnTimeline(State); // GG-T1172: a Mode C hero's turn never runs the clock
            int ticks = !frozen && holdTurnTicks > 0 ? Mathf.FloorToInt(k * holdTurnTicks) : 0;
            var at = BattleEngine.TimelineUAfter(State, ticks);
            RefreshRecoverView(ticks); // GG-T319: tray rings count down with the hold; one that runs out slides onto the bar
            SyncTimeline(at);
            holdDrainFill.style.width = Length.Percent((1f - k) * 100f);
            holdDrainFill.style.backgroundColor = Color.Lerp(HoldEmpty, HoldFull, 1f - k);
            float nextU = frozen ? 1f - k : 2f; // GG-T286: u is seconds-to-act over BarSeconds, so the next enemy is the lowest u (left = acts)
            if (!frozen)
                foreach (var e in State.Timeline)
                    if (e.Kind == TimelineKind.Turn && State.UnitById(e.OwnerId) is Unit o && !o.IsHero && !o.IsTimelineHalted && at.TryGetValue(e.Id, out var u))
                        nextU = Mathf.Min(nextU, u);
            bool limited = holdWindow > 0f && nextU <= 1f;
            holdLane.style.display = limited ? DisplayStyle.Flex : DisplayStyle.None;
            holdLabel.style.display = limited ? DisplayStyle.Flex : DisplayStyle.None;
            if (!limited) return;
            float laneW = XOfU(nextU) - XOfU(0f);
            holdLane.style.width = laneW; // the stretch between the left edge (acts) and the next enemy
            holdLane.style.backgroundColor = Color.Lerp(new Color(1f, 0.3f, 0.25f, 0.7f), Palette.Gold.WithA(0.55f), 1f - k);
            holdLabel.text = $"{Mathf.Max(0f, holdWindow - holdElapsed):0.0}s";
            holdLabel.style.left = XOfU(0f) + Mathf.Max(0f, laneW * 0.5f - 30f);
        }

        private void EndHold()
        {
            holdTicker?.Pause();
            holdTicker = null;
            holdDrain?.RemoveFromHierarchy(); holdDrain = holdDrainFill = null;
            holdLane?.RemoveFromHierarchy(); holdLane = null;
            holdLabel?.RemoveFromHierarchy(); holdLabel = null;
        }

        /// <summary>Turns the tile under the finger into cardinal steps from the last logical tile (one tile at a
        /// time, §1.1) — or, on a fast near-45° flick straight onto a diagonal tile, a single diagonal slip.</summary>
        private void ExtendPath(Coord target, bool fastDiagonal = false)
        {
            int guard = 0;
            while (dragTile != target && guard++ < 16)
            {
                int dx = target.X - dragTile.X, dy = target.Y - dragTile.Y;
                if (fastDiagonal && Math.Abs(dx) == 1 && Math.Abs(dy) == 1)
                {
                    var diag = new Coord(dragTile.X + Math.Sign(dx), dragTile.Y + Math.Sign(dy));
                    var diagCandidate = new List<Coord>(dragPath) { diag };
                    var diagPv = BattleEngine.PreviewMove(State, dragHeroId, diagCandidate);
                    if (diagPv.StepsTaken.Count == diagCandidate.Count)
                    {
                        dragPath.Add(diag);
                        dragTile = diag;
                        preview = diagPv;
                        App.Audio.Sfx("battle.dash", 0.7f);
                        ApplyPreview(diagPv, dragHeroId);
                        continue;
                    }
                }
                Coord step = Math.Abs(dx) >= Math.Abs(dy)
                    ? new Coord(dragTile.X + Math.Sign(dx), dragTile.Y)
                    : new Coord(dragTile.X, dragTile.Y + Math.Sign(dy));
                var candidate = new List<Coord>(dragPath) { step };
                var pv = BattleEngine.PreviewMove(State, dragHeroId, candidate);
                if (pv.StepsTaken.Count < candidate.Count)
                {
                    // Blocked (edge / boss / snake / Heavy): try the other axis once, else hold position.
                    var blocked = step;
                    if (dx != 0 && dy != 0)
                    {
                        Coord alt = Math.Abs(dx) >= Math.Abs(dy)
                            ? new Coord(dragTile.X, dragTile.Y + Math.Sign(dy))
                            : new Coord(dragTile.X + Math.Sign(dx), dragTile.Y);
                        candidate[candidate.Count - 1] = alt;
                        pv = BattleEngine.PreviewMove(State, dragHeroId, candidate);
                        if (pv.StepsTaken.Count < candidate.Count) { BumpHeavy(blocked, dragPath); return; }
                        step = alt;
                    }
                    else { BumpHeavy(blocked, dragPath); return; }
                }
                dragPath.Add(step);
                dragTile = step;
                preview = pv;
                App.Audio.Sfx("battle.move", 0.5f);
                ApplyPreview(pv, dragHeroId);
            }
        }

        private Coord? heavyBumpAt;
        private float heavyBumpTime = -10f;

        /// <summary>
        /// GG-T892 (§1.1.3): the drag ran into a Heavy unit (it stands on <paramref name="blocked"/> once the walk so far,
        /// <paramref name="path"/>, has moved everyone it moves): the step is refused with a thud and "Too heavy!" over it.
        /// Played once per push against that tile, not on every pointer move while the finger keeps pressing.
        /// </summary>
        private void BumpHeavy(Coord blocked, List<Coord> path)
        {
            var at = path.Count > 0 ? BattleEngine.PreviewMove(State, dragHeroId, path).Positions : null;
            Unit occ = null;
            foreach (var u in State.Units)
            {
                if (!u.Alive || u.Id == dragHeroId) continue;
                var pos = at != null && at.TryGetValue(u.Id, out var p) ? p : u.Pos;
                if (pos == blocked) { occ = u; break; }
            }
            if (occ == null || !occ.IsHeavy) return;
            if (heavyBumpAt == blocked && Time.unscaledTime - heavyBumpTime < 0.8f) { heavyBumpTime = Time.unscaledTime; return; }
            heavyBumpAt = blocked; heavyBumpTime = Time.unscaledTime;
            board.Units.TryGetValue(occ.Id, out var v);
            HeavyThud(v, blocked);
        }

        private static readonly Color HeroTrail = new Color(1f, 0.9f, 0.45f), EnemyTrail = Palette.Bad;

        /// <summary>Slide displaced actors to their preview tiles and draw the pincers this drop would make.</summary>
        private void ApplyPreview(MovePreview pv, int heroId)
        {
            foreach (var kv in pv.Positions)
            {
                if (kv.Key == heroId) continue;
                if (!board.Units.TryGetValue(kv.Key, out var v)) continue;
                if (v.Tile != kv.Value)
                {
                    App.Audio.Sfx("battle.slide", 0.6f);
                    var from = BoardView.PosOf(v.Tile);
                    v.Tile = kv.Value;
                    App.StartCoroutine(Tween.Move(v.Root, from, BoardView.PosOf(kv.Value), Timing.Instant));
                }
            }
            board.Lines.ClearLines();
            board.ClearMarks();
            if (heroId == dragHeroId && dragging && !simulatingDrag) ShowDragOutcome(pv);
            foreach (var u in State.Units)
                if (board.Units.TryGetValue(u.Id, out var uv) && u.Alive && u.Id != heroId) uv.SetGlow(Color.white, 0f);
            // GG-T190: shoving is a tactic, so show the whole migration — a ghost on every displaced hero's and
            // enemy's current tile and an arrow to where the drop leaves it (the tokens themselves already slid there).
            var shove = ShovePreview.Of(State, heroId, pv);
            foreach (var d in shove.Displaced) DrawShoveArrow(d);
            foreach (var p in pv.Pincers)
            {
                pv.Positions.TryGetValue(p.A.Id, out var a);
                pv.Positions.TryGetValue(p.B.Id, out var b);
                // Preview: the group outline and edge links the drop would make (no line across the art).
                // The pincer's units belong to the preview's cloned state, so their positions are the drop's (GG-T98).
                var group = new List<(Coord, int, int)> { (a, p.A.W, p.A.H), (b, p.B.W, p.B.H) };
                var oppAt = p.Opponents.Select(o => new UnitFootprint { UnitId = o.Id, At = pv.Positions.TryGetValue(o.Id, out var op) ? op : o.Pos, W = o.W, H = o.H }).ToList();
                foreach (var o in oppAt) group.Add((o.At, o.W, o.H));
                var geometryError = PincerGeometry.Error(new UnitFootprint { UnitId = p.A.Id, At = a, W = p.A.W, H = p.A.H }, new UnitFootprint { UnitId = p.B.Id, At = b, W = p.B.W, H = p.B.H }, oppAt);
                if (geometryError != null && Debug.isDebugBuild) Debug.LogError($"[GG-T98] malformed preview pincer outline: {geometryError}");
                board.Lines.Outline(BoardView.GroupRect(group), Palette.Gold.WithA(0.9f), 8f, 1f);
                foreach (var sup in p.SupportersA.Concat(p.SupportersB).Distinct())
                {
                    // GG-T187: link each supporter to the unit its chain runs through (an attacker or the next ally).
                    var via = p.LinkOf(sup) ?? p.AttackerOf(sup);
                    var sp = pv.Positions.TryGetValue(sup.Id, out var spp) ? spp : sup.Pos;
                    var partner = pv.Positions.TryGetValue(via.Id, out var vp) ? vp : via.Pos;
                    var (la, lb) = BoardView.EdgeLink(sp, partner);
                    board.Lines.Bar(la, lb, ShapeStyle.Indicator.Tint(Palette.Mana.WithA(0.9f)).Thick(10f));
                }
                foreach (var o in p.Opponents)
                {
                    foreach (var t in o.Tiles()) board.Mark(t, new Color(1f, 0.25f, 0.2f), 0.32f);
                    if (board.Units.TryGetValue(o.Id, out var ov)) ov.SetGlow(new Color(1f, 0.3f, 0.2f), 0.8f);
                }
                foreach (var s in p.SupportersA) if (board.Units.TryGetValue(s.Id, out var sv)) sv.SetGlow(Palette.Mana, 0.7f);
                foreach (var s in p.SupportersB) if (board.Units.TryGetValue(s.Id, out var sv)) sv.SetGlow(Palette.Mana, 0.7f);
            }
            // A pincer a shove completes: the shoved foe (or the shoved partner) pulses so the cause reads at a glance.
            foreach (var p in pv.Pincers)
            {
                if (!shove.CompletedByShove(p)) continue;
                foreach (var u in p.Opponents.Concat(new[] { p.A, p.B }))
                    if (u.Id != heroId && shove.IsShoved(u.Id) && board.Units.TryGetValue(u.Id, out var sv))
                        App.StartCoroutine(Tween.Pop(sv.Root, 1.1f, 0.2f));
            }
            if (pv.Pincers.Count == 0)
            {
                int foes = shove.Displaced.Count(d => d.IsEnemy);
                ShowBanner(foes > 0 ? (foes > 1 ? $"Shoving {foes} foes" : "Shoving a foe") : "");
                return;
            }
            string banner = pv.Pincers.Count > 1 ? $"PINCER ×{pv.Pincers.Count}!" : "PINCER!";
            int shoved = shove.ShovePincerCount;
            if (shoved > 0) banner = (shoved == pv.Pincers.Count ? "SHOVE → " : $"SHOVE ×{shoved} → ") + banner;
            // GG-T39: the attacker (either endpoint, never a supporter) whose weapon is on its last point
            // would wear it to 0 and shatter in this very drop (§24.5) — call that out before it's a surprise.
            Unit shatterer = null;
            foreach (var p in pv.Pincers)
            {
                if (p.A.IsHero && p.A.WeaponId != null && p.A.WeaponDurability == 1) { shatterer = p.A; break; }
                if (p.B.IsHero && p.B.WeaponId != null && p.B.WeaponDurability == 1) { shatterer = p.B; break; }
            }
            if (shatterer != null)
                banner += $"  This pincer will shatter your {App.Content.TryItem(shatterer.WeaponId)?.Name ?? "weapon"}!";
            ShowBanner(banner);
        }

        /// <summary>GG-T190: a displaced actor's ghost tile plus an arrow to where the drop leaves it (amber for a foe, blue for a hero).</summary>
        private void DrawShoveArrow(ShovedActor d)
        {
            var col = d.IsEnemy ? new Color(1f, 0.55f, 0.2f, 0.9f) : Palette.Mana.WithA(0.9f);
            board.Mark(d.From, col, 0.16f);
            var a = BoardView.CenterOf(d.From);
            var b = BoardView.CenterOf(d.To);
            if ((b - a).sqrMagnitude < 1f) return;
            // Stop short of both token centres so the arrow reads between them, not over the art.
            board.Lines.Arrow(a, b, ShapeStyle.Indicator.Tint(col), BoardView.Tile * 0.22f);
        }

        /// <summary>Puts a drag back where it started with no command and no clock spent — for a drop that can't be
        /// committed (paused, the battle ended or busy), so the view never shows a move the state refused.</summary>
        private void AbortDrag()
        {
            if (!dragging) return;
            dragging = false;
            pressing = false;
            board.Lines.ClearLines();
            board.ClearMarks();
            ClearOutcomeTags();
            EndHold();
            if (board.Units.TryGetValue(dragHeroId, out var v))
            {
                board.Trail.Release(v.Root);
                v.Root.style.scale = new Scale(Vector2.one);
                var hero = State.UnitById(dragHeroId);
                if (hero != null) board.Place(v, hero.Pos);
            }
            dragPath.Clear();
            preview = null;
            SyncAll();
        }

        private void CommitDrag(BattleCommand perfectCmd = null)
        {
            if (!dragging) return;
            // Commit() refuses while paused / ended / mid-playback: snap back instead of tweening to a tile it won't take.
            if (playing || ended || State.IsOver || Paused || awayOnSettings) { AbortDrag(); return; }
            // GG-T539: a coached move lands only on its marked tile (a lost pointer capture puts the hero back instead).
            if (CoachRail && dragTile != CoachDest) { CancelDrag(); return; }
            if (CoachRail) coachMoveDone = true;
            dragging = false;
            pressing = false;
            ClearOutcomeTags();
            board.Lines.ClearLines();
            board.ClearMarks();
            var v = board.Units[dragHeroId];
            board.Trail.Release(v.Root);
            v.Root.style.scale = new Scale(Vector2.one);
            // GG-T188: the seconds held travel with the drop; a hold that ran the window out is a forced drop that
            // ends the turn even where the hero never left its tile.
            float held = HeldSeconds();
            EndHold();
            if (dragPath.Count == 0 && !BattleEngine.IsTimeUp(State, held))
            {
                board.Place(v, State.UnitById(dragHeroId).Pos);
                SyncAll();
                return;
            }
            float timeLeft = !App.Settings.DragTimer ? 0f : Untimed ? Mathf.Max(0f, BattleConfig.DragTimerSeconds - held) : Mathf.Max(0f, dragTimeLeft);
            var cmd = new MoveCommand { HeroId = dragHeroId, Path = new List<Coord>(dragPath), TimeLeftSeconds = timeLeft, HeldSeconds = held };
            // Snap the dragged hero onto its final tile before playback.
            App.StartCoroutine(Tween.Move(v.Root, new Vector2(v.Root.resolvedStyle.left, v.Root.resolvedStyle.top), BoardView.PosOf(dragTile), Timing.Instant));
            Commit(cmd, true, perfectCmd);
        }

        private void PreviewTarget(Coord tile)
        {
            var hero = State.UnitById(SelectedHeroId);
            var a = hero == null ? null : BattleEngine.SlotAbility(State, hero, TargetingSlot);
            if (a == null || a.Mode == TargetMode.Auto) return;
            board.ClearMarks();
            foreach (var vt in BattleEngine.ValidTargets(State, hero.Id, TargetingSlot)) board.Mark(vt, Palette.Gold, 0.25f);
            if (!TargetResolver.IsValidPick(State, a, hero, tile)) return;
            bool harmful = a.BaseDamage > 0 || a.Filter == TargetFilter.EnemyOnly;
            var color = harmful ? new Color(1f, 0.3f, 0.25f) : new Color(0.4f, 0.9f, 0.5f);
            if (a.IsTeleport) { board.Mark(tile, Palette.Mana, 0.6f); return; }
            foreach (var t in TargetResolver.Tiles(tile, a.Shape, a.Radius, hero.Pos)) board.Mark(t, color, 0.38f); // GG-T933: a Wall lies across the caster's line
        }
    }
}
