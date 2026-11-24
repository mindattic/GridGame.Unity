using System.Collections;
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
            board.SyncRegion(State);
            if (rt.Change == "hidden") { App.Audio.Sfx("battle.slide", 0.3f, 0.8f); yield return Tween.Wait(Timing.Quick); yield break; } // GG-T843: it fades from sight
            App.Audio.Sfx(rt.Change == "cleared" ? "battle.slide" : "battle.trap", rt.Change == "changed" ? 0.4f : 0.65f);
            yield return Tween.Wait(rt.Change == "changed" ? Timing.Quick : Timing.Standard);
        }

        /// <summary>A hidden mark shows again (GG-T843, §14.23): a flash on its tile, then it is drawn for everyone.</summary>
        private IEnumerator TileRevealedAnim(TileRevealedEvent tr)
        {
            board.Mark(tr.At, Palette.Gold, 0.6f);
            App.Audio.Sfx("battle.trap", 0.8f, 1.15f);
            board.SyncRegion(State);
            yield return Tween.Wait(Timing.Standard);
            board.ClearMarks();
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
            Text.Countdown(HazardCenter(hq.Tiles), Mathf.CeilToInt(hq.Seconds), HazardTint(hq.EventId));
            yield return Tween.Wait(Timing.Emphasis);
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
            if (!hl.Rearmed) hazardTiles.Remove(hl.EntryId);
            if (hl.Tiles.Count > 0) Text.Countdown(HazardCenter(hl.Tiles), 0, zero: HazardName(hl.EventId, hl.RuleId) + "!");
            App.Audio.Sfx("battle.shatter", 0.7f);
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
    }
}
