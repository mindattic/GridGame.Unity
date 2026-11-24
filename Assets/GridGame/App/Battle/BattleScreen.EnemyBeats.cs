using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// GG-T839 (battle feel, GG-T823): a foe's turn reads as beats instead of one blur.
    /// <list type="bullet">
    /// <item>Intent — as its turn starts (or as its charge fires off the bar) the board focuses on the acting foe and its
    /// portrait slides in from the right with the callout ("Gorer: Bull Rush"). A pincer's or a spell's own cut-in
    /// stands in for the card; the focus and the callout still play.</item>
    /// <item>Wind-up — just before the act, the tiles it will hit pulse in warning red.</item>
    /// <item>Act, then consequence — the act's own beats (the blow, the charge, the cast) and their CombatText follow.</item>
    /// </list>
    /// Foe pincers and foe supporters ride the pincer beat sequencer (Prep / Strike / Follow-up) like the party's.
    /// Traps and queued hazards are drawn as of the beat now playing (later beats' placings, springings, queues and
    /// landings rewound out of the end state), so trap, hazard and tile triggers show in the order they fired.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private sealed class EnemyBeat
        {
            public int ActorId;
            public string Action;
            /// <summary>A named ability (the bigger card and a longer wind-up) rather than a plain strike.</summary>
            public bool Major;
            /// <summary>The act brings its own cut-in (a pincer, a spell's caster slide): no intent card.</summary>
            public bool OwnCard;
            public int ActIndex = -1;
            public bool IntentShown;
        }

        /// <summary>The foe action now playing, or null.</summary>
        private EnemyBeat enemyBeat;
        /// <summary>The playback's events and the beat now playing (for the in-order trap and hazard views).</summary>
        private List<BattleEvent> playEvents;
        private int playIndex = -1;

        private static bool IsFoe(Unit u) => u != null && u.Team == Team.Enemy && !u.IsRemains && !u.IsEgg;

        private static bool EndsAction(BattleEvent e) =>
            e is TurnStartedEvent || e is ActionBeganEvent || e is TimelineAdvancedEvent || e is PlayerControlEvent
            || e is BattleEndedEvent || e is WaveClearedEvent;

        /// <summary>Starts a playback's in-order board views.</summary>
        private void BeginEnemyBeats(List<BattleEvent> events)
        {
            playEvents = events;
            playIndex = -1;
            enemyBeat = null;
            board.TrapsShown = TrapsAsPlayed;
            board.HazardsShown = HazardsAsPlayed;
        }

        /// <summary>The playback is over: traps and hazards are the state's own again.</summary>
        private void EndEnemyBeats()
        {
            playEvents = null;
            playIndex = -1;
            enemyBeat = null;
        }

        /// <summary>
        /// Called before beat <paramref name="i"/> plays: a new action plans its foe beats (a foe's turn), and the act
        /// itself gets its wind-up (and its intent first, for a charge firing off the bar). Null when nothing plays.
        /// </summary>
        private IEnumerator EnemyBeatBefore(List<BattleEvent> events, int i)
        {
            playIndex = i;
            var e = events[i];
            if (EndsAction(e))
            {
                enemyBeat = e is TurnStartedEvent ts ? PlanEnemyTurn(events, i, ts.UnitId) : null;
                return null;
            }
            // A foe's queued charge or spell firing off the bar (its cast ghost still rides): intent, wind-up and act all
            // at this beat. A reactive or passive answer during someone else's action is not its own action.
            if (e is AbilityResolvedEvent ar && (enemyBeat == null || enemyBeat.ActIndex < i)
                && castGhosts.Values.Any(g => g.CasterId == ar.CasterId && g.AbilityId == ar.AbilityId)
                && IsFoe(State.UnitById(ar.CasterId)))
            {
                var a = BattleEngine.ResolveAbilityId(App.Content, ar.AbilityId);
                if (a != null && a.Kind != AbilityKind.Passive && a.Kind != AbilityKind.Reactive)
                {
                    enemyBeat = new EnemyBeat { ActorId = ar.CasterId, ActIndex = i };
                    DescribeAct(enemyBeat, e);
                }
            }
            return enemyBeat != null && enemyBeat.ActIndex == i ? EnemyWindUp(enemyBeat) : null;
        }

        /// <summary>A foe's turn: its first act (a pincer, a charge, an ability, a trap, a strike) up to the end of the
        /// action. Null when it only moves or does nothing.</summary>
        private EnemyBeat PlanEnemyTurn(List<BattleEvent> events, int start, int actorId)
        {
            if (!IsFoe(State.UnitById(actorId))) return null;
            for (int j = start + 1; j < events.Count && !EndsAction(events[j]); j++)
            {
                var e = events[j];
                bool acts = e is PincerFormedEvent p ? p.Team == Team.Enemy && (p.Attacker1 == actorId || p.Attacker2 == actorId)
                    : e is CastQueuedEvent cq ? cq.CasterId == actorId
                    : e is AbilityResolvedEvent ar ? ar.CasterId == actorId
                    : e is TrapPlacedEvent tp ? tp.ById == actorId
                    : e is AttackEvent at && at.AttackerId == actorId && !at.IsSupport;
                if (!acts) continue;
                var b = new EnemyBeat { ActorId = actorId, ActIndex = j };
                DescribeAct(b, e);
                return b;
            }
            return null;
        }

        private void DescribeAct(EnemyBeat b, BattleEvent e)
        {
            switch (e)
            {
                case PincerFormedEvent _:
                    b.Action = "Pincer"; b.Major = true; b.OwnCard = true;
                    break;
                case CastQueuedEvent cq:
                    b.Action = App.Content.TryAbility(cq.AbilityId)?.Name ?? (cq.IsCharge ? "Charge" : "Cast"); b.Major = true;
                    break;
                case AbilityResolvedEvent ar:
                {
                    var a = BattleEngine.ResolveAbilityId(App.Content, ar.AbilityId);
                    b.Action = a?.Name ?? "Ability"; b.Major = true;
                    b.OwnCard = a != null && a.Kind == AbilityKind.Spell; // AbilityAnim's caster slide
                    break;
                }
                case TrapPlacedEvent _:
                    b.Action = "Lay Trap";
                    break;
                case AttackEvent at:
                    b.Action = at.IsRanged ? "Bolt" : "Strike";
                    break;
            }
        }

        /// <summary>The intent beat: the board focuses on the foe, the callout goes up and its card slides in from the right.</summary>
        private IEnumerator EnemyIntent(EnemyBeat b)
        {
            b.IntentShown = true;
            var u = State.UnitById(b.ActorId);
            if (u == null || string.IsNullOrEmpty(b.Action) || !board.Units.TryGetValue(u.Id, out var v) || v.Dead) yield break;
            string callout = $"{u.Name}: {b.Action}";
            ShowBanner(callout);
            v.SetGlow(Palette.Bad, 0.9f);
            if (!ReduceMotion)
                App.StartCoroutine(CameraPunch(new Vector2(BoardLeft, BoardTop) + ViewCenter(u), b.Major ? 1.08f : 1.05f, 1.6f, b.Major ? 0.7f : 0.5f));
            if (CutInsEnabled && !ReduceMotion && !b.OwnCard) yield return IntentSlide(u, callout, b.Major);
            else
            {
                Text.Show(ViewCenter(u), CombatTextKind.Timer, b.Action + "!", 0f, new Color(1f, 0.55f, 0.5f));
                yield return Tween.Wait(ReduceMotion ? Timing.Instant : Timing.Standard);
            }
            App.StartCoroutine(UnglowLater(v, Timing.Quick));
        }

        /// <summary>The wind-up: the tiles the act will hit pulse in warning red while the foe glows (one steady flash
        /// under Reduce Motion).</summary>
        private IEnumerator EnemyWindUp(EnemyBeat b)
        {
            if (!b.IntentShown) yield return EnemyIntent(b);
            if (playEvents == null || b.ActIndex < 0 || b.ActIndex >= playEvents.Count) yield break;
            var tiles = ActTiles(playEvents[b.ActIndex]).Where(BattleState.InBounds).Distinct().ToList();
            if (tiles.Count == 0) yield break;
            var u = State.UnitById(b.ActorId);
            UnitView v = null;
            if (u != null && board.Units.TryGetValue(u.Id, out v) && !v.Dead) v.SetGlow(Palette.Bad, 0.85f);
            App.Audio.Sfx("battle.channel", 0.45f, 1.35f);
            var c = new Color(1f, 0.22f, 0.18f);
            float inset = 6f, size = BoardView.Tile - 2f * inset;
            float secs = ReduceMotion ? Timing.Quick : b.Major ? Timing.Emphasis : Timing.Standard;
            yield return Tween.Run(secs, k =>
            {
                board.Lines.ClearLines();
                float pulse = ReduceMotion ? 1f : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f));
                foreach (var t in tiles)
                    board.Lines.Outline(new Rect(BoardView.PosOf(t) + Vector2.one * inset, Vector2.one * size), c.WithA(0.95f * pulse), 5f + 5f * pulse, 1f);
            });
            board.Lines.ClearLines();
            if (v != null) App.StartCoroutine(UnglowLater(v, Timing.Quick));
        }

        /// <summary>The tiles an act will hit, where the board shows them now.</summary>
        private IEnumerable<Coord> ActTiles(BattleEvent e)
        {
            switch (e)
            {
                case PincerFormedEvent p: return p.OpponentsAt.SelectMany(FootprintTiles).ToList();
                case CastQueuedEvent cq:
                {
                    if (cq.LineTiles != null && cq.LineTiles.Count > 0) return cq.LineTiles;
                    var tu = State.UnitById(cq.TargetUnitId);
                    return tu != null ? ViewTiles(tu).ToList() : new List<Coord> { cq.TargetTile };
                }
                case AbilityResolvedEvent ar:
                    return ar.Tiles.Concat(ar.Targets.Select(State.UnitById).Where(x => x != null).SelectMany(ViewTiles)).ToList();
                case TrapPlacedEvent tp: return new List<Coord> { tp.At };
                case AttackEvent at:
                {
                    var t = State.UnitById(at.TargetId);
                    return t != null ? ViewTiles(t).ToList() : new List<Coord>();
                }
            }
            return new List<Coord>();
        }

        // ── In-order board views: traps and queued hazards as of the beat now playing ──

        /// <summary>The traps the board shows at the beat now playing: the end state with every later beat's placing
        /// taken back and every later springing or clearing put back. Null outside a playback (the state's own).</summary>
        private IEnumerable<Coord> TrapsAsPlayed()
        {
            if (playEvents == null) return null;
            var shown = State.Traps.Select(t => t.At).ToList();
            for (int j = playEvents.Count - 1; j > playIndex; j--)
            {
                switch (playEvents[j])
                {
                    case TrapPlacedEvent tp: shown.Remove(tp.At); break;
                    case TrapTriggeredEvent tt: shown.Add(tt.At); break;
                    case TrapClearedEvent tc when tc.Into == null: shown.Add(tc.At); break;
                }
            }
            return shown;
        }

        /// <summary>The hazard telegraphs the board shows at the beat now playing: a hazard queued by a later beat is not
        /// up yet, one that lands (or is called off) in a later beat still is. Null outside a playback.</summary>
        private IEnumerable<(int Id, string RuleEvent, List<Coord> Tiles)> HazardsAsPlayed()
        {
            if (playEvents == null) return null;
            var shown = new Dictionary<int, (int, string, List<Coord>)>();
            foreach (var t in State.Timeline)
                if (t.Kind == TimelineKind.Hazard && t.HazardTiles != null) shown[t.Id] = (t.Id, t.RuleEvent, t.HazardTiles);
            for (int j = playEvents.Count - 1; j > playIndex; j--)
            {
                switch (playEvents[j])
                {
                    case HazardQueuedEvent hq: shown.Remove(hq.EntryId); break;
                    case HazardLandedEvent hl when !hl.Rearmed && hl.Tiles.Count > 0: shown[hl.EntryId] = (hl.EntryId, hl.EventId, hl.Tiles); break;
                    case HazardRetimedEvent hr when hr.Cancelled && hazardTiles.TryGetValue(hr.EntryId, out var tiles): shown[hr.EntryId] = (hr.EntryId, hr.EventId, tiles); break;
                }
            }
            return shown.Values;
        }
    }
}
