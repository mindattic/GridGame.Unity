using System.Collections;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// Region rules (GG-T359, BIBLE §14.20), the view half: the opening banner per rule, painted tiles and telegraphed
    /// hazards redrawn on the board (BoardView.SyncRegion), rule events riding the timeline as their own icons, each beat
    /// with its own sound and time.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private IEnumerator RegionRuleAnim(RegionRuleEvent rr)
        {
            board.SyncRegion(State);
            App.Audio.Sfx("battle.whoosh", 0.7f);
            ShowBanner(string.IsNullOrEmpty(rr.Description) ? rr.Name : $"{rr.Name}: {rr.Description}");
            yield return Tween.Wait(Timing.Cinematic);
        }

        private IEnumerator RegionTilesAnim(RegionTilesEvent rt)
        {
            TrackGlobs(rt); // GG-T895
            board.SyncRegion(State);
            if (rt.Change == "hidden") { App.Audio.Sfx("battle.slide", 0.3f, 0.8f); yield return Tween.Wait(Timing.Quick); yield break; } // GG-T843: it fades from sight
            if (rt.Change == "marked" && RuneState(rt.State) is TileStateDef rune) { yield return RunePaintAnim(rt, rune); yield break; } // GG-T939: a Choir rune's paint puff
            App.Audio.Sfx(rt.Change == "cleared" ? "battle.slide" : "battle.trap", rt.Change == "changed" ? 0.4f : 0.65f);
            yield return Tween.Wait(rt.Change == "changed" ? Timing.Quick : Timing.Standard);
        }

        /// <summary>A hidden mark shows again (GG-T843, §14.23): a flash on its tile, then it is drawn for everyone.</summary>
        private IEnumerator TileRevealedAnim(TileRevealedEvent tr)
        {
            if (tr.State == "buriedmine") { yield return BuriedChargePop(tr); yield break; }
            board.Mark(tr.At, Palette.Gold, 0.6f);
            App.Audio.Sfx("battle.trap", 0.8f, 1.15f);
            board.SyncRegion(State);
            RevealPop(tr.At); // GG-T846
            yield return Tween.Wait(Timing.Standard);
            board.ClearMarks();
        }

        /// <summary>GG-T876 (§14.23): a Buried Charge pops out of the ground with a click, its fuse about to run (the
        /// mineblast's own Timer countdown follows from its HazardQueuedEvent, and its boom from HazardLandedAnim).</summary>
        private IEnumerator BuriedChargePop(TileRevealedEvent tr)
        {
            var at = BoardView.CenterOf(tr.At);
            board.Mark(tr.At, new Color(1f, 0.3f, 0.2f), 0.75f);
            Emit(StyleOf(DamageType.Physical), at, 0.6f, quiet: true);
            App.Audio.Sfx("battle.trap", 0.9f, 1.7f); // the click
            Text.Countdown(at, 0, new Color(1f, 0.45f, 0.3f), zero: "Click!");
            board.SyncRegion(State);
            yield return Tween.Wait(Timing.Standard);
            board.ClearMarks();
        }

        /// <summary>GG-T888: how far a board ability's light reaches (its MarkTiles of a Lights state, the Hexstalker's
        /// Flare Shot), or -1 when it lights nothing.</summary>
        private int FlareRadius(AbilityDef a)
        {
            int r = -1;
            foreach (var fx in a.RuleEffects)
                if (fx.Kind == RuleEffectKind.MarkTiles && App.Content.TryTileState(fx.State)?.Lights == true) r = Mathf.Max(r, fx.Radius);
            return r;
        }

        /// <summary>
        /// GG-T888 (§14.23): a flare arcs from the caster to its tile with a sputtering spark trail, then fizzes alight: a
        /// warm glow swells out to its radius, a soft gold burst, and its lit tiles wash in (the RegionTilesEvent that
        /// follows draws them). Reduce Motion flies it straight and skips the swell.
        /// </summary>
        private IEnumerator FlareShotVfx(Unit caster, AbilityDef a, Coord anchor, int radius, VfxStyleDef style)
        {
            var glow = new Color(1f, 0.78f, 0.38f);
            Vector2 from = ViewCenter(caster), to = BoardView.CenterOf(anchor);
            App.Audio.Sfx("battle.cast", 0.6f, 1.3f);
            yield return Tween.Run(0.42f, k =>
            {
                board.Lines.ClearLines();
                board.Lines.Dot(ProjectilePoint(ProjectileMotion.Bezier, from, to, k), 20f, glow);
                for (int i = 1; i <= 3; i++)
                {
                    float back = Mathf.Max(0f, k - 0.06f * i);
                    board.Lines.Dot(ProjectilePoint(ProjectileMotion.Bezier, from, to, back), 14f - 3f * i, new Color(1f, 0.9f, 0.6f, 0.55f - 0.15f * i));
                }
            });
            board.Lines.ClearLines();
            App.Audio.Sfx(a.Sfx ?? "battle.fizzle", 0.85f, 1.25f); // the fizz as it catches
            foreach (var t in FlareTiles(anchor, radius)) board.Mark(t, glow, 0.35f);
            NamedImpactFlourish(a.ImpactVfx, to, style);
            Emit(StyleOf(DamageType.Holy), to, 0.5f, quiet: true);
            if (!ReduceMotion)
            {
                float reach = (radius + 0.5f) * BoardView.Tile;
                yield return Tween.Run(Timing.Emphasis, k =>
                {
                    board.Lines.Rings.Clear();
                    board.Lines.Ring(to, 30f + reach * Ease.Out(k), new Color(glow.r, glow.g, glow.b, 0.9f * (1f - k)), 10f);
                    board.Lines.Ring(to, 18f + 0.5f * reach * k, new Color(1f, 0.95f, 0.75f, 0.6f * (1f - k)), 6f);
                });
                board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
            }
            else yield return Tween.Wait(Timing.Quick);
        }

        private static System.Collections.Generic.IEnumerable<Coord> FlareTiles(Coord at, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var c = new Coord(at.X + dx, at.Y + dy);
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) <= radius && BattleState.InBounds(c)) yield return c;
                }
        }

        private IEnumerator RegionAnchorAnim(RegionAnchorEvent ra)
        {
            board.SyncRegion(State);
            App.Audio.Sfx("battle.dash", 0.6f);
            yield return Tween.Wait(Timing.Standard);
        }

        private IEnumerator RegionFlagAnim(RegionFlagEvent rf)
        {
            SyncTimeline(null);
            if (rf.Flag == RuleFlags.MaskTimeline) RedrawTimelineArt();
            App.Audio.Sfx("battle.channel", rf.On ? 0.8f : 0.5f, rf.On ? 0.8f : 1.2f);
            yield return Tween.Wait(Timing.Emphasis);
        }

        private IEnumerator HazardQueuedAnim(HazardQueuedEvent hq)
        {
            board.SyncRegion(State);
            SyncTimeline(null);
            App.Audio.Sfx("battle.channel", 0.7f);
            ShowBanner($"{HazardName(hq.EventId, hq.RuleId)} lands in {hq.Seconds:0.#}s");
            // GG-T830: the hazard's countdown ticks over its area (seconds of timeline time), flashing when it lands.
            hazardTiles[hq.EntryId] = hq.Tiles;
            if (hq.CarrierId >= 0 && board.Units.TryGetValue(hq.CarrierId, out var cv) && !cv.Dead)
            {
                // GG-T874: a Time Bomb ticks over its carrier, not the middle of its tiles, and a fuse hisses as it sticks.
                App.Audio.Sfx("battle.trap", 0.8f, 1.4f);
                board.Mark(cv.Tile, HazardTint(hq.EventId), 0.6f);
                BombTick(hq.EntryId, hq.EventId, cv, Mathf.CeilToInt(hq.Seconds));
            }
            else Text.Countdown(HazardCenter(hq.Tiles), Mathf.CeilToInt(hq.Seconds), HazardTint(hq.EventId));
            yield return Tween.Wait(Timing.Emphasis);
            if (hq.CarrierId >= 0) board.ClearMarks();
        }

        /// <summary>GG-T874: a carried hazard's tiles moved with its carrier (or it fell and the bomb stays): redraw them.</summary>
        private void HazardMoved(HazardMovedEvent hm)
        {
            hazardTiles[hm.EntryId] = hm.Tiles;
            board.SyncRegion(State);
            if (hm.Dropped) bombShown.Remove(hm.EntryId);
        }

        /// <summary>GG-T874: the seconds last shown over each carried bomb, so each second ticks once.</summary>
        private readonly System.Collections.Generic.Dictionary<int, int> bombShown = new System.Collections.Generic.Dictionary<int, int>();

        /// <summary>GG-T874: the ticking glyph over a Time Bomb's carrier: a Timer countdown with the fuse glyph and a tick.</summary>
        private void BombTick(int entryId, string eventId, UnitView v, int secs)
        {
            if (bombShown.TryGetValue(entryId, out int was) && was == secs) return;
            bombShown[entryId] = secs;
            if (secs <= 0) return; // the landing flashes its own name
            Text.Countdown(BoardView.CenterOf(v.Tile, v.W, v.H), secs, HazardTint(eventId), "⏲");
            App.Audio.Sfx("battle.trap", 0.45f, secs <= 1 ? 2f : 1.6f);
        }

        /// <summary>GG-T874: each carried bomb on the bar ticks its seconds left over its carrier (once per second shown).</summary>
        private void BombCountdownTexts()
        {
            foreach (var e in State.Timeline)
            {
                if (e.Kind != TimelineKind.Hazard || e.CarrierId < 0) continue;
                if (!board.Units.TryGetValue(e.CarrierId, out var v) || v.Dead) continue;
                BombTick(e.Id, e.RuleEvent, v, Mathf.CeilToInt(Mathf.Max(0, e.RemainingTicks) / (float)BattleConfig.TicksPerSecond));
            }
        }

        private IEnumerator HazardRetimedAnim(HazardRetimedEvent hr)
        {
            board.SyncRegion(State);
            SyncTimeline(null);
            App.Audio.Sfx("battle.slide", 0.5f, hr.Cancelled ? 0.8f : 1.2f);
            var entry = State.Timeline.Find(x => x.Id == hr.EntryId);
            // A called-off hazard has left the timeline: its tiles are the ones it was queued with.
            var tiles = entry?.HazardTiles ?? (hazardTiles.TryGetValue(hr.EntryId, out var q) ? q : null);
            if (hr.Cancelled) hazardTiles.Remove(hr.EntryId);
            if (tiles != null && tiles.Count > 0)
            {
                if (hr.Cancelled) Text.Expired(HazardCenter(tiles), HazardName(hr.EventId, entry?.RuleId), HazardTint(hr.EventId));
                else Text.Countdown(HazardCenter(tiles), Mathf.CeilToInt(hr.TicksLeft / (float)BattleConfig.TicksPerSecond), HazardTint(hr.EventId));
            }
            yield return Tween.Wait(Timing.Quick);
        }

        private IEnumerator HazardLandedAnim(HazardLandedEvent hl)
        {
            foreach (var t in hl.Tiles) board.Mark(t, new Color(1f, 0.45f, 0.15f), 0.6f);
            if (!hl.Rearmed) { hazardTiles.Remove(hl.EntryId); bombShown.Remove(hl.EntryId); }
            if (hl.Tiles.Count > 0) Text.Countdown(HazardCenter(hl.Tiles), 0, zero: HazardName(hl.EventId, hl.RuleId) + "!");
            if (hl.EventId == "mineblast" && hl.Tiles.Count > 0) // GG-T876: the boom after the click
            {
                App.Audio.Sfx("battle.explosion", 0.9f);
                Emit(StyleOf(DamageType.Fire), HazardCenter(hl.Tiles));
            }
            else if (hl.EventId == "timebomb" && hl.Tiles.Count > 0) // GG-T874: the Time Bomb goes off round its carrier
            {
                App.Audio.Sfx("battle.explosion", 1f, 0.85f);
                Emit(StyleOf(DamageType.Fire), HazardCenter(hl.Tiles), 1.3f, sizeMul: 1.4f);
                foreach (var t in hl.Tiles) Emit(StyleOf(DamageType.Fire), BoardView.CenterOf(t), 0.5f, quiet: true);
                App.StartCoroutine(Tween.Shake(shake, 10f * Fx, Timing.Standard));
            }
            else App.Audio.Sfx("battle.shatter", 0.7f);
            yield return Tween.Wait(Timing.Standard);
            board.ClearMarks();
            board.SyncRegion(State);
            SyncTimeline(null);
        }

        /// <summary>
        /// A unit falls into a hole (GG-T368, §14.21): carried to the hole if it was shoved or pulled in, it shrinks and fades
        /// into the dark, a full board's swap partner steps onto the filled hole, then it climbs out on its landing tile
        /// (the fall's damage is its own beat after this one).
        /// </summary>
        private IEnumerator FellAnim(FellEvent f)
        {
            if (!board.Units.TryGetValue(f.UnitId, out var v)) { board.SyncRegion(State); yield break; }
            if (v.Tile != f.Hole)
            {
                App.Audio.Sfx("battle.slide", 0.6f);
                var from = BoardView.PosOf(v.Tile);
                v.Tile = f.Hole;
                yield return Tween.Move(v.Root, from, BoardView.PosOf(f.Hole), 0.14f);
            }
            App.Audio.Sfx("battle.shatter", 0.8f, 0.7f);
            yield return Tween.Run(Timing.Standard, t =>
            {
                v.Root.style.scale = new Scale(Vector3.one * Mathf.Lerp(1f, 0.25f, t));
                v.Root.style.opacity = 1f - t;
            });
            if (f.SwappedId >= 0) { board.SyncRegion(State); yield return StepAnim(f.SwappedId, f.Hole, 0.14f, "battle.slide"); }
            board.Place(v, f.To);
            board.SyncRegion(State);
            App.Audio.Sfx("battle.dash", 0.6f);
            yield return Tween.Run(Timing.Quick, t =>
            {
                v.Root.style.scale = new Scale(Vector3.one * Mathf.Lerp(0.6f, 1f, t));
                v.Root.style.opacity = t;
            });
            v.Root.style.scale = new Scale(Vector3.one);
            v.Root.style.opacity = 1f;
        }

        private readonly System.Collections.Generic.HashSet<int> darkHidden = new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// Darkness (GG-T365, §14.20.6): a foe out of every hero's sight and off any lit tile vanishes from the board and
        /// its timeline icon turns to a "?" (RegionBoard.Hidden); it reappears the beat it comes into sight.
        /// </summary>
        private void SyncDarkness()
        {
            if (State == null || (darkHidden.Count == 0 && !State.Region.HasFlag(RuleFlags.Darkness))) return;
            bool changed = false;
            foreach (var kv in board.Units)
            {
                bool hide = RegionBoard.Hidden(State, State.UnitById(kv.Key));
                if (hide == darkHidden.Contains(kv.Key)) continue;
                changed = true;
                if (hide) darkHidden.Add(kv.Key); else darkHidden.Remove(kv.Key);
                kv.Value.Root.style.visibility = hide ? Visibility.Hidden : Visibility.Visible;
            }
            if (changed) RedrawTimelineArt();
        }

        /// <summary>What a rule event is called on screen: its rule's Prose name, else the event id.</summary>
        private string HazardName(string eventId, string ruleId) =>
            App.Content.TryRegionRule(ruleId)?.Name ?? App.Content.TryRegionRule(App.Content.RuleEventRule.TryGetValue(eventId ?? "", out var r) ? r : null)?.Name ?? "Hazard";

        /// <summary>GG-T830: each queued hazard's tiles by timeline entry, for its countdown once it leaves the bar.</summary>
        private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<GridGame.Core.Coord>> hazardTiles = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<GridGame.Core.Coord>>();

        /// <summary>GG-T830: where a hazard's countdown floats: the middle of its tiles.</summary>
        private static Vector2 HazardCenter(System.Collections.Generic.List<GridGame.Core.Coord> tiles)
        {
            var sum = Vector2.zero;
            foreach (var t in tiles) sum += BoardView.CenterOf(t);
            return sum / tiles.Count;
        }

        /// <summary>A rule event's colour for its countdown text.</summary>
        private static Color HazardTint(string eventId) =>
            Color.Lerp(Art.HexColor(GameApp.I?.Content?.TryRuleEvent(eventId)?.Color, Palette.CastEnemy), Palette.White, 0.2f);

        /// <summary>A rule event's colour on the timeline and the board.</summary>
        private static Color HazardColor(TimelineEntry e) =>
            Art.HexColor(GameApp.I?.Content?.TryRuleEvent(e.RuleEvent)?.Color, Palette.CastEnemy);

        /// <summary>The timeline diamond's art for a rule event: its UI icon when it names one, else its glyph.</summary>
        private void HazardArt(VisualElement art, TimelineEntry e)
        {
            var def = App.Content.TryRuleEvent(e.RuleEvent);
            art.style.backgroundColor = HazardColor(e).WithA(0.3f);
            var tex = string.IsNullOrEmpty(def?.Icon) ? null : Art.UiIcon(def.Icon);
            if (tex != null)
            {
                var ic = new VisualElement { name = "tl-ability", pickingMode = PickingMode.Ignore };
                ic.style.position = Position.Absolute;
                ic.style.left = ic.style.top = ic.style.right = ic.style.bottom = Length.Percent(24);
                ic.style.backgroundImage = new StyleBackground(tex);
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                art.Add(ic);
                return;
            }
            var l = new Label(string.IsNullOrEmpty(def?.Glyph) ? "⚠" : def.Glyph) { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.left = l.style.top = l.style.right = l.style.bottom = Space.S0;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.fontSize = TypeScale.Base;
            l.style.color = Palette.Text;
            art.Add(l);
        }

        /// <summary>The hold card for a rule event icon: its rule's name and description and when it lands.</summary>
        private bool BuildHazardCard(VisualElement body, TimelineEntry e)
        {
            var rule = App.Content.TryRegionRule(e.RuleId)
                       ?? App.Content.TryRegionRule(App.Content.RuleEventRule.TryGetValue(e.RuleEvent ?? "", out var r) ? r : null);
            float secs = BattleEngine.SecondsUntilAct(State, e);
            int tiles = e.HazardTiles?.Count ?? 0;
            return InspectCard.Hazard(body, rule?.Name ?? "Hazard", rule?.Description,
                $"Lands in {secs:0.#}s{(tiles > 0 ? $" on {tiles} marked tile{(tiles == 1 ? "" : "s")}" : "")} — step off before it reaches the left edge.",
                HazardColor(e));
        }

        /// <summary>GG-T846: a revealed mark pops on its tile with a short board shake (Reduce Motion: neither; the gold
        /// flash still shows it).</summary>
        private void RevealPop(GridGame.Core.Coord at)
        {
            if (ReduceMotion) return;
            var cell = board.RegionCellAt(at);
            if (cell != null) App.StartCoroutine(Tween.Pop(cell, 1.25f, Timing.Quick));
            App.StartCoroutine(Tween.Shake(shake, 6f, Timing.Quick));
        }

        /// <summary>GG-T846 (§14.23): a unit squishes a mark (a slime glob): a splat of the mark's colour bursts from the
        /// tile with a wet slap, then the "cleared" RegionTilesEvent that follows redraws the board without it.</summary>
        private IEnumerator SquishedAnim(SquishedEvent sq)
        {
            var def = App.Content.TryTileState(sq.State);
            var col = Art.HexColor(def?.Color, Palette.Info);
            board.Splat(sq.At, col);
            App.Audio.Sfx("battle.slide", 0.8f, 0.6f);
            if (BoardView.Hatches(def)) // GG-T895: a slime glob squelches with its own cue and a callout
            {
                globTiles.Remove(sq.At);
                App.Audio.Sfx("slime.squish", 0.9f);
                Text?.Show(sq.At, CombatTextKind.Callout, "Squish!", 0.3f, Color.Lerp(col, Palette.White, 0.25f));
            }
            else if (def?.Circle != null) RuneScuffed(sq, def); // GG-T939: a hero scuffs a Choir rune out
            if (!ReduceMotion) App.StartCoroutine(Tween.Shake(shake, 4f, Timing.Quick));
            yield return Tween.Wait(Timing.Quick);
        }

        // ── GG-T869 (§7, §14.21): the Barbarian's Earthsplitter ─────────────────────────────────

        /// <summary>The cracking state an ability paints along a line ahead of its caster (Earthsplitter's cracked), or null.</summary>
        private TileStateDef FissurePainted(AbilityDef a)
        {
            if (a?.RuleEffects == null) return null;
            foreach (var e in a.RuleEffects)
                if (e != null && e.Kind == RuleEffectKind.MarkTiles && e.Area == RuleArea.Line && App.Content.TryTileState(e.State) is TileStateDef def)
                    return def;
            return null;
        }

        /// <summary>The slam: a low rumble under a board shake, then the fissure runs out from the caster tile by tile, each
        /// one flashing the crack's colour with a burst of grit and a stone crack (the "marked" region event that follows
        /// draws the cracks). Reduce Motion: no shake, one quick pass.</summary>
        private IEnumerator FissureAnim(Unit caster, AbilityDef a, TileStateDef crack, GridGame.Core.Coord anchor)
        {
            int count = 3;
            foreach (var e in a.RuleEffects) if (e != null && e.Area == RuleArea.Line) { count = e.Count; break; }
            var line = RegionRuleCatalog.LineTiles(caster.Pos, anchor, count);
            var col = Art.HexColor(crack.Color, Palette.Gold);
            App.Audio.Sfx("battle.explosion", 0.55f, 0.45f); // the rumble
            if (!ReduceMotion) App.StartCoroutine(Tween.Shake(shake, 7f, Timing.Standard));
            float beat = ReduceMotion ? Timing.Quick * 0.3f : Timing.Quick * 0.6f;
            foreach (var t in line)
            {
                board.Mark(t, col, 0.7f);
                Emit(StyleOf(DamageType.Physical), BoardView.CenterOf(t), 0.5f, quiet: true);
                App.Audio.Sfx("battle.shatter", 0.45f, 0.55f);
                yield return Tween.Wait(beat);
            }
            board.SyncRegion(State);
        }

        /// <summary>A Heavy unit breaks through a crack (GG-T869): the tile flashes dark under a deep rumble and a hard shake,
        /// "Broke through!" over it, then its <see cref="FellEvent"/> plays the fall.</summary>
        private IEnumerator BrokeThroughAnim(BrokeThroughEvent bt)
        {
            board.Mark(bt.At, new Color(0.25f, 0.18f, 0.12f), 0.85f);
            Emit(StyleOf(DamageType.Physical), BoardView.CenterOf(bt.At), 0.8f, quiet: true);
            App.Audio.Sfx("battle.explosion", 0.8f, 0.35f);
            App.Audio.Sfx("battle.shatter", 0.7f, 0.5f);
            Text?.Show(bt.At, CombatTextKind.Callout, "Broke through!", 0.3f, new Color(0.85f, 0.65f, 0.4f));
            if (!ReduceMotion) App.StartCoroutine(Tween.Shake(shake, 10f, Timing.Standard));
            board.SyncRegion(State);
            yield return Tween.Wait(Timing.Quick);
            board.ClearMarks();
        }

        // ── GG-T895 (§14.23): slime glob visuals ─────────────────────────────────────────────────

        /// <summary>Hatching marks (slime globs) on the board, by tile: their state id, kept from the region events so a
        /// rule spawn landing on one plays the hatch pop instead of the generic entrance.</summary>
        private readonly System.Collections.Generic.Dictionary<GridGame.Core.Coord, string> globTiles = new System.Collections.Generic.Dictionary<GridGame.Core.Coord, string>();

        private void TrackGlobs(RegionTilesEvent rt)
        {
            if (rt.Change == "cleared" || rt.Change == "changed") foreach (var t in rt.Tiles) globTiles.Remove(t);
            if (rt.Change == "marked" && BoardView.Hatches(App.Content.TryTileState(rt.State))) foreach (var t in rt.Tiles) globTiles[t] = rt.State;
        }

        /// <summary>The hatching state an ability paints (Vomit Glob's slime_glob), or null.</summary>
        private TileStateDef GlobPainted(AbilityDef a)
        {
            if (a?.RuleEffects == null) return null;
            foreach (var e in a.RuleEffects)
            {
                if (e == null || e.Kind != RuleEffectKind.MarkTiles) continue;
                var def = App.Content.TryTileState(e.State);
                if (BoardView.Hatches(def)) return def;
            }
            return null;
        }

        /// <summary>A slime vomits its glob: it squashes down and heaves, a gooey blob arcs high onto the tile trailing
        /// drips, and lands with a wet splat (the "marked" region event that follows draws the wobbling glob there).
        /// Reduce Motion: a short straight toss, no squash or drips.</summary>
        private IEnumerator GlobLobAnim(Unit caster, TileStateDef glob, GridGame.Core.Coord at)
        {
            var col = Art.HexColor(glob.Color, Palette.Good);
            bool calm = ReduceMotion;
            Vector2 from = ViewCenter(caster), to = BoardView.CenterOf(at);
            board.Units.TryGetValue(caster.Id, out var cv);
            // The heave: squash wide and low, then spring back up as the glob leaves.
            if (cv != null && !calm)
            {
                yield return Tween.Run(Timing.Quick * 0.8f, k => cv.Root.style.scale = new Scale(new Vector3(1f + 0.18f * k, 1f - 0.22f * k, 1f)));
                App.StartCoroutine(Tween.Run(Timing.Quick, k => cv.Root.style.scale = new Scale(new Vector3(1.18f - 0.18f * k, 0.78f + 0.22f * k, 1f))));
            }
            App.Audio.Sfx("slime.vomit", 0.9f);
            App.Audio.Sfx("element.water", 0.7f, 0.6f); // stand-in until slime.vomit has its own sound
            float dist = Vector2.Distance(from, to);
            float travel = calm ? 0.2f : Mathf.Clamp(dist / 1400f, 0.32f, 0.55f);
            float height = Mathf.Max(220f, dist * 0.55f);
            var drip = col.WithA(0.55f);
            Vector2 P(float kk) => Vector2.Lerp(from, to, kk) + (calm ? Vector2.zero : Vector2.up * (-height * 4f * kk * (1f - kk)));
            yield return Tween.Run(travel, k =>
            {
                board.Lines.ClearLines();
                float wob = calm ? 0f : Mathf.Sin(k * Mathf.PI * 5f) * 4f; // the goo jiggles in flight
                board.Lines.Dot(P(k), 30f + wob, col);
                board.Lines.Dot(P(k) + new Vector2(-8f, -9f), 8f, Palette.White.WithA(0.6f));
                if (!calm)
                    for (int i = 1; i <= 3; i++) board.Lines.Dot(P(Mathf.Max(0f, k - 0.06f * i)) + Vector2.up * (10f * i), 14f - 3f * i, drip);
            });
            board.Lines.ClearLines();
            if (cv != null) cv.Root.style.scale = new Scale(Vector3.one);
            board.Splat(at, col);
            if (!calm) App.StartCoroutine(Tween.Shake(shake, 3f, Timing.Quick));
            App.Audio.Sfx("battle.slide", 0.7f, 0.5f);
        }

        /// <summary>A glob hatches (its OnExpire SpawnUnit): the blob bursts in a splat and the slime pops out of it with an
        /// overshoot and a "Hatched!" callout. False when the spawn didn't land on a tracked glob of a state that spawns its
        /// class (the caller then plays the usual entrance).</summary>
        private bool TryHatchPop(UnitView v, Unit u, GridGame.Core.Coord at)
        {
            if (!globTiles.TryGetValue(at, out var state)) return false;
            var def = App.Content.TryTileState(state);
            bool spawnsIt = false;
            if (def?.OnExpire != null)
                foreach (var e in def.OnExpire) if (e != null && e.Kind == RuleEffectKind.SpawnUnit && e.Class == u.Def?.Id) spawnsIt = true;
            if (!spawnsIt) return false;
            globTiles.Remove(at);
            App.StartCoroutine(HatchPop(v, at, Art.HexColor(def.Color, Palette.Good)));
            return true;
        }

        /// <summary>The hatch pop. Reduce Motion: the slime fades in, no shake or overshoot.</summary>
        private IEnumerator HatchPop(UnitView v, GridGame.Core.Coord at, Color col)
        {
            board.Splat(at, col);
            App.Audio.Sfx("battle.crit", 0.7f, 1.4f);
            App.Audio.Sfx("element.water", 0.6f, 0.8f);
            Text?.Show(at, CombatTextKind.Callout, "Hatched!", 0.4f, Color.Lerp(col, Palette.White, 0.25f));
            if (ReduceMotion)
            {
                v.Root.style.opacity = 0f;
                yield return Tween.Run(Timing.Quick, k => v.Root.style.opacity = k);
                v.Root.style.opacity = 1f;
                yield break;
            }
            App.StartCoroutine(Tween.Shake(shake, 5f, Timing.Quick));
            v.Root.style.scale = new Scale(Vector3.zero);
            // Out of the goo with a springy overshoot: 0, up to 1.25, back to 0.92, settling at 1.
            yield return Tween.Run(Timing.Standard, k =>
            {
                float s = k < 0.5f ? Mathf.Lerp(0f, 1.25f, k / 0.5f) : k < 0.8f ? Mathf.Lerp(1.25f, 0.92f, (k - 0.5f) / 0.3f) : Mathf.Lerp(0.92f, 1f, (k - 0.8f) / 0.2f);
                v.Root.style.scale = new Scale(new Vector3(s, s * (1f + 0.1f * Mathf.Sin(k * Mathf.PI * 3f)), 1f));
            });
            v.Root.style.scale = new Scale(Vector3.one);
        }
    }
}
