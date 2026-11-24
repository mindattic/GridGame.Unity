using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>One painted tile (GG-T359): which state, which rule painted it, its counter and how long it has left.</summary>
    public sealed class TileMark
    {
        public string State;
        public string Rule;
        public int Counter;
        /// <summary>Clock ticks left (0 = until cleared).</summary>
        public int LifeTicks;
        /// <summary>Copied from the state at paint time, so <see cref="BattleState.Walkable"/> needs no content lookup.</summary>
        public bool Blocks;
        public bool Hazard;
        /// <summary>The team that painted it (GG-T842): a hero's ability paints as the party, a monster's ability, trail or
        /// death as its side; <see cref="Team.Neutral"/> = the board itself (a rule's hook or event).</summary>
        public Team OwnerTeam = Team.Neutral;
        /// <summary>The unit that painted it, or -1.</summary>
        public int OwnerId = -1;
        /// <summary>Clock ticks it is still seen by everyone (GG-T843): its state's VisibleSeconds at paint time, counted
        /// down by the clock; at 0 it hides (<see cref="Hidden"/>). 0 from the start = never hides.</summary>
        public int SightTicks;
        /// <summary>Hidden (GG-T843, §14.23): only its painter's side sees it now (nobody, for a board-painted mark); see
        /// <see cref="RegionBoard.Sees"/>. A trigger, a RevealTiles effect or an element's hit shows it again for good.</summary>
        public bool Hidden;
        public TileMark Clone() => (TileMark)MemberwiseClone();
    }

    /// <summary>
    /// The board's region-rule state (GG-T359, BIBLE §14.20): the rules in play, painted tiles, named anchors, board-wide
    /// flags and each hook's match count. Queued rule events live on the timeline (<see cref="TimelineKind.Hazard"/>).
    /// </summary>
    public sealed class RegionState
    {
        public List<string> Rules = new List<string>();
        public Dictionary<Coord, TileMark> Tiles = new Dictionary<Coord, TileMark>();
        public Dictionary<string, Coord> Anchors = new Dictionary<string, Coord>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> HookCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        public TileMark At(Coord c) => Tiles.Count > 0 && Tiles.TryGetValue(c, out var m) ? m : null;
        public bool BlocksAt(Coord c) => Tiles.Count > 0 && Tiles.TryGetValue(c, out var m) && m.Blocks;
        public bool HasFlag(string flag) => flag != null && Flags.Contains(flag);
        public Coord? Anchor(string name) => name != null && Anchors.TryGetValue(name, out var c) ? c : (Coord?)null;

        public RegionState Clone()
        {
            var r = new RegionState
            {
                Rules = new List<string>(Rules),
                Anchors = new Dictionary<string, Coord>(Anchors, StringComparer.OrdinalIgnoreCase),
                Flags = new HashSet<string>(Flags, StringComparer.OrdinalIgnoreCase),
                HookCounts = new Dictionary<string, int>(HookCounts, StringComparer.Ordinal),
            };
            foreach (var kv in Tiles) r.Tiles[kv.Key] = kv.Value.Clone();
            return r;
        }
    }

    /// <summary>Pure queries on the region state for the rules, the planners, the bot and the view (GG-T359).</summary>
    public static class RegionBoard
    {
        /// <summary>Does <paramref name="u"/> shrug off effects from any of these sources (rule, tile state, event, ability ids)?</summary>
        public static bool Immune(Unit u, string a, string b = null, string c = null)
        {
            var list = u?.Def?.RuleImmune;
            if (list == null || list.Count == 0) return false;
            foreach (var id in list)
                if (id != null && (Same(id, a) || Same(id, b) || Same(id, c))) return true;
            return false;
        }

        private static bool Same(string x, string y) => y != null && string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Does mark <paramref name="m"/> work on <paramref name="u"/> (GG-T842, BIBLE §14.23)? A board-painted mark (no
        /// owner) or a state that affects <see cref="TileAffects.Any"/> works on everyone; otherwise the state's
        /// <see cref="TileStateDef.Affects"/> is read against the team that painted it.
        /// </summary>
        public static bool Affects(BattleState s, TileMark m, Unit u)
        {
            if (m == null || u == null || m.OwnerTeam == Team.Neutral) return true;
            switch (s.Content?.TryTileState(m.State)?.Affects ?? TileAffects.Any)
            {
                case TileAffects.FoesOfOwner: return u.Team != m.OwnerTeam;
                case TileAffects.AlliesOfOwner: return u.Team == m.OwnerTeam;
                default: return true;
            }
        }

        /// <summary>
        /// Does <paramref name="viewer"/>'s side know mark <paramref name="m"/> is there (GG-T843, BIBLE §14.23)? Every side
        /// sees a mark that isn't <see cref="TileMark.Hidden"/>; a hidden one only its painter's side does (a board-painted
        /// hidden mark: nobody). The bot and the planners read danger only from marks their side sees, so neither side
        /// cheats; the view draws only what the party sees (the party's own hidden marks faint).
        /// </summary>
        public static bool Sees(TileMark m, Team viewer) =>
            m != null && (!m.Hidden || (m.OwnerTeam != Team.Neutral && m.OwnerTeam == viewer));

        /// <summary>Is <paramref name="e"/> a queued rule event whose tiles are dangerous?</summary>
        public static bool IsHazardEntry(BattleState s, TimelineEntry e) =>
            e.Kind == TimelineKind.Hazard && (s.Content?.TryRuleEvent(e.RuleEvent)?.Hazard ?? true);

        /// <summary>
        /// How dangerous standing on <paramref name="c"/> is for <paramref name="u"/> (0 = safe): a hazardous painted state
        /// counts 1, a telegraphed hazard landing there counts 2 (more the sooner it lands). Immune units read 0, and so
        /// does a unit a team-owned mark doesn't work on (GG-T842: a hero's firewall is danger to foes only, so the enemy
        /// planner steers round it and the hero bot values shoving a foe into it). The bot and the planners steer by it.
        /// A hidden mark counts only when <paramref name="viewer"/>'s side (default: <paramref name="u"/>'s own) sees it
        /// (GG-T843: a foe walks onto the party's hidden mine none the wiser; the hero bot still knows it is there).
        /// </summary>
        public static float DangerAt(BattleState s, Coord c, Unit u, Team? viewer = null)
        {
            float d = 0f;
            var m = s.Region.At(c);
            if (m != null && !Affects(s, m, u)) m = null;
            if (m != null && m.Hidden && !Sees(m, viewer ?? u?.Team ?? Team.Neutral)) m = null; // GG-T843: no cheating
            if (m != null && m.Hazard && !Immune(u, m.Rule, m.State)) d += 1f;
            else if (m != null && AboutToGive(s, m) && !Immune(u, m.Rule, m.State)) d += 0.5f; // GG-T368: one more crack and it gives
            foreach (var e in s.Timeline)
            {
                if (e.Kind != TimelineKind.Hazard || e.HazardTiles == null || !e.HazardTiles.Contains(c)) continue;
                if (!IsHazardEntry(s, e) || Immune(u, e.RuleId, e.RuleEvent)) continue;
                float soon = 1f - Math.Min(1f, Math.Max(0, e.RemainingTicks) / (float)(BattleConfig.TicksPerSecond * BattleConfig.BarSeconds));
                d += 2f + 2f * soon;
            }
            return d;
        }

        /// <summary>Is <paramref name="m"/> one count from turning into a hazard or a hole (a crack about to give, GG-T368)?</summary>
        public static bool AboutToGive(BattleState s, TileMark m)
        {
            var def = s.Content?.TryTileState(m?.State);
            if (def == null || def.Threshold <= 0 || def.Becomes == null || m.Counter + 1 < def.Threshold) return false;
            var next = s.Content.TryTileState(def.Becomes);
            // "Crumbling" is neither a hazard nor a pit: it schedules the cave-in that opens the hole (GG-T443), so a
            // state that queues an event when marked is danger too; without it no real crack ever read as about to give.
            return next != null && (next.Hazard || next.Pit || next.OnMark.Any(e => e.Kind == RuleEffectKind.Schedule));
        }

        /// <summary>
        /// GG-T934 (BIBLE §14.23): is <paramref name="c"/> a standing wall a line charge runs into (a blocking painted
        /// mark that isn't a hole: the Machinist's barricade, a hold's barricade)? A charge stops there and spares whoever
        /// is beyond it; a hole it runs over.
        /// </summary>
        public static bool StopsCharge(BattleState s, Coord c)
        {
            var m = s.Region.At(c);
            return m != null && m.Blocks && !(s.Content?.TryTileState(m.State)?.Pit ?? false);
        }

        /// <summary>Is <paramref name="c"/> an open hole <paramref name="u"/> would fall into (GG-T368)?</summary>
        public static bool IsPitFor(BattleState s, Coord c, Unit u)
        {
            var m = s.Region.At(c);
            if (m == null || !m.Blocks || u == null || u.IsMultiTile || u.IsSnakeMember || u.IsRemains || u.IsCorpse || u.IsEgg) return false;
            return (s.Content?.TryTileState(m.State)?.Pit ?? false) && !Immune(u, m.Rule, m.State);
        }

        /// <summary>
        /// What shoving <paramref name="u"/> up to <paramref name="tiles"/> tiles along <paramref name="dir"/> does with the
        /// floor (GG-T368, for the bot): 1 when it would go into an open hole, 0.5 when it would end on a dangerous tile (a
        /// crumbling floor, a crack about to give, a telegraphed strike), else 0. A <paramref name="stopBeside"/> unit ends
        /// the shove beside it (a pull stops next to the puller). It reads the floor as <paramref name="viewer"/>'s side
        /// knows it (GG-T843: the hero bot, the default, counts the party's hidden mine; a foe's hidden one it can't see).
        /// </summary>
        public static float ShoveFloorValue(BattleState s, Unit u, Coord dir, int tiles, Unit stopBeside = null, Team viewer = Team.Hero)
        {
            if (s.Region.Tiles.Count == 0 && !s.Timeline.Exists(x => x.Kind == TimelineKind.Hazard)) return 0f;
            if (u == null || u.WontBudge || (dir.X == 0 && dir.Y == 0)) return 0f;
            var c = u.Pos;
            for (int i = 0; i < tiles; i++)
            {
                if (stopBeside != null && c.Manhattan(stopBeside.Pos) <= 1) break;
                var next = c + dir;
                if (IsPitFor(s, next, u)) return 1f;
                if (!s.FreeToEnter(next)) break;
                c = next;
            }
            return c != u.Pos && DangerAt(s, c, u, viewer) > 0f ? 0.5f : 0f;
        }

        /// <summary>True when the timeline should mask enemy turn icons (a rule raised <see cref="RuleFlags.MaskTimeline"/>).</summary>
        public static bool MasksTimeline(BattleState s) => s.Region.HasFlag(RuleFlags.MaskTimeline);

        /// <summary>A class tag: while hidden, the foe's timeline icon wears a hero's face (GG-T379).</summary>
        public const string MimicTag = "Mimic";

        /// <summary>The hero whose face a hidden mimic's timeline icon shows (one per mimic, by id), else null (GG-T379).</summary>
        public static Unit MimicFace(BattleState s, Unit u)
        {
            if (u?.Def == null || !u.Def.HasTag(MimicTag) || !Hidden(s, u)) return null;
            var heroes = s.Units.FindAll(h => h.Alive && h.IsHero);
            return heroes.Count == 0 ? null : heroes[u.Id % heroes.Count];
        }

        /// <summary>How far (Manhattan) a hero sees under <see cref="RuleFlags.Darkness"/> (GG-T365).</summary>
        public const int DarkSight = 2;

        /// <summary>Under <see cref="RuleFlags.Darkness"/>, is foe <paramref name="u"/> out of sight: more than
        /// <see cref="DarkSight"/> tiles from every living hero and on no lit tile (GG-T365)? The view hides it on the board
        /// and masks its timeline icon; the rules of play don't change.</summary>
        public static bool Hidden(BattleState s, Unit u)
        {
            if (u == null || !u.Alive || u.Team != Team.Enemy || !s.Region.HasFlag(RuleFlags.Darkness)) return false;
            foreach (var t in u.Tiles())
                if (s.Content?.TryTileState(s.Region.At(t)?.State)?.Lights == true) return false;
            foreach (var h in s.Units)
            {
                if (!h.Alive || !h.IsHero) continue;
                foreach (var t in u.Tiles())
                    if (t.Manhattan(h.Pos) <= DarkSight) return false;
            }
            return true;
        }
    }
}
