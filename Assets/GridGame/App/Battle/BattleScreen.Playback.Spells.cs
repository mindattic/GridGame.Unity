using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using MindAttic.Media.Capture;
using MindAttic.Media.Capture.Unity;
using MindAttic.Telemetry.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed partial class BattleScreen
    {
        /// <summary>The label of the action now playing (its ActionBeganEvent), naming what granted an orb.</summary>
        private string lastActionLabel;

        /// <summary>Where a mana mote lands when the party bar has no layout yet: the middle of the action row.</summary>
        private Vector2 BarCenter() => new Vector2(GameApp.StageWidth / 2f, BarTop + BarHeight / 2f);

        private VisualElement FlyingOrb()
        {
            var orb = new VisualElement { pickingMode = PickingMode.Ignore };
            orb.style.position = Position.Absolute;
            orb.style.width = orb.style.height = Size.Orb;
            // A glowing blue mote: a soft halo behind a bright orb.
            var halo = new VisualElement { pickingMode = PickingMode.Ignore };
            halo.style.position = Position.Absolute;
            halo.style.left = halo.style.top = halo.style.right = halo.style.bottom = -Space.S24;
            halo.style.backgroundImage = new StyleBackground(Art.Glow);
            halo.style.unityBackgroundImageTintColor = Art.Mana.WithA(0.7f);
            orb.Add(halo);
            var core = new VisualElement { pickingMode = PickingMode.Ignore };
            core.style.position = Position.Absolute;
            core.style.left = core.style.top = core.style.right = core.style.bottom = Space.S8;
            core.style.backgroundImage = new StyleBackground(Art.Orb);
            core.style.unityBackgroundImageTintColor = Art.Mana;
            orb.Add(core);
            App.Overlay.Add(orb);
            return orb;
        }

        private static void PlaceOrb(VisualElement orb, Vector2 p)
        {
            orb.style.left = p.x - Size.Orb / 2f; orb.style.top = p.y - Size.Orb / 2f;
        }

        /// <summary>Where a mint or a charge came from, in Overlay (stage) coordinates, from the real event (GG-T405): Guard
        /// from the GUARD cell (the bottom row's right end), a time-banked orb from the drag timer, battle-start gear and
        /// carried orbs from the top bar, and everything else from its source unit's tile (the pincer's attacker, the robbed foe, the hero on the
        /// pickup, the rule's unit, the caster).</summary>
        private Vector2 MintFrom(string reason, int sourceId)
        {
            VisualElement anchor = reason == "guard" ? guardButton : reason == "time bank" ? timerLabel
                : reason == "battle start" || reason == "carried" ? waveLabel : null;
            if (anchor != null && !float.IsNaN(anchor.worldBound.x) && anchor.worldBound.width > 0f)
                return anchor.worldBound.center - App.Stage.worldBound.position;
            if (sourceId >= 0 && board.Units.TryGetValue(sourceId, out var v))
                return new Vector2(BoardLeft, BoardTop) + BoardView.CenterOf(v.Tile, v.W, v.H);
            return new Vector2(GameApp.StageWidth / 2f, BoardTop + BoardView.Tile * BattleConfig.Rows / 2f);
        }

        /// <summary>The player-facing name of a mana gain's source (ManaGainedEvent.Reason).</summary>
        private static string MintLabel(string reason)
        {
            switch (reason)
            {
                case "pincer": return "Pincer";
                case "critical": return "Critical";
                case "interrupt": return "Interrupt";
                case "clutch": return "Clutch!";
                case "amplifier": return "Amplifier";
                case "guard": return "Guard";
                case "support": return "Support";
                case "passive": return "Passive";
                case "armor": return "Armor";
                case "steal": return "Steal";
                case "item": return "Item";
                case "aether": return "Mana Pour";
                case "battle start": return "Gear";
                case "carried": return "Carried";
                case "time bank": return "Quick move";
                case "pickup": return "Pickup";
            }
            if (reason != null && reason.StartsWith("rule:"))
            {
                string id = reason.Substring(5).Replace('_', ' ').Replace('-', ' ');
                return id.Length > 0 ? char.ToUpperInvariant(id[0]) + id.Substring(1) : "Rule";
            }
            return string.IsNullOrEmpty(reason) ? "Mana" : char.ToUpperInvariant(reason[0]) + reason.Substring(1);
        }

        /// <summary>A mana mote flies from its source along a quadratic bezier (GG-T673, owner: "particle trails spawn and
        /// bezier curve their way"): the control point sits off the midpoint by a randomised
        /// sideways and upward offset, so several motes launched together fan out instead of stacking, and the mote sheds a
        /// fading trail of sparks as it goes. A time slice (Guard) starts gold and turns mana-blue on the way.
        /// <paramref name="to"/> defaults to the ability bar.</summary>
        private IEnumerator MoteFlight(Vector2 from, float seconds, bool fades, Color? startTint = null, float scale = 1f, Vector2? to = null)
        {
            var orb = FlyingOrb();
            orb.style.scale = new Scale(Vector2.one * scale);
            Vector2 end = to ?? BarCenter();
            PlaceOrb(orb, from);
            var core = orb[1];
            Vector2 mid = (from + end) / 2f, dir = end - from;
            Vector2 normal = dir.sqrMagnitude > 1f ? new Vector2(-dir.y, dir.x).normalized : Vector2.right;
            Vector2 control = mid + normal * Random.Range(-Size.TrailSwing, Size.TrailSwing) + Vector2.down * Random.Range(Size.TrailLift * 0.5f, Size.TrailLift);
            float sparkClock = 0f;
            yield return Tween.Run(seconds, k =>
            {
                float t = Tween.EaseInOut(k), u = 1f - t;
                Vector2 at = u * u * from + 2f * u * t * control + t * t * end;
                PlaceOrb(orb, at);
                if (startTint.HasValue) core.style.unityBackgroundImageTintColor = Color.Lerp(startTint.Value, Palette.ManaBloom, k);
                if (fades) orb.style.opacity = 1f - k;
                sparkClock += Tween.Dt;
                if (sparkClock >= Timing.TrailSparkEvery) { sparkClock = 0f; App.StartCoroutine(TrailSpark(at, scale * (1f - 0.5f * k))); }
            });
            orb.RemoveFromHierarchy();
        }

        /// <summary>One spark of a mana trail: a small violet-to-glint dot left behind the mote that shrinks and fades.</summary>
        private IEnumerator TrailSpark(Vector2 at, float scale)
        {
            if (App.Overlay == null) yield break;
            var s = new VisualElement { pickingMode = PickingMode.Ignore };
            s.style.position = Position.Absolute;
            s.style.width = s.style.height = Size.TrailSpark;
            s.style.left = at.x - Size.TrailSpark / 2f; s.style.top = at.y - Size.TrailSpark / 2f;
            s.style.backgroundImage = new StyleBackground(Art.Glow);
            s.style.unityBackgroundImageTintColor = Random.value < 0.25f ? Palette.ManaGlint : Palette.ManaBloom;
            App.Overlay.Add(s);
            yield return Tween.Run(Timing.TrailSparkLife, k =>
            {
                s.style.opacity = 1f - k;
                s.style.scale = new Scale(Vector2.one * scale * (1f - 0.6f * k));
            });
            s.RemoveFromHierarchy();
        }

        /// <summary>A whole grant (§3.1.0: points into the party bar) goes in the Log under the action that gave it (a Mana
        /// Potion, Meditate, Mana Pour) or its source; the party bar fills from the same ManaGainedEvent.</summary>
        private void GrantLine(ManaGainedEvent g)
        {
            string label = (g.Reason == "item" || g.Reason == "aether") && !string.IsNullOrEmpty(lastActionLabel) ? lastActionLabel : MintLabel(g.Reason);
            if (g.Amount > 0) Feed($"{label}: +{g.Amount + g.Wasted} mana", Palette.Mana);
            else Feed($"{label}: the party mana was already full", Palette.Gold);
        }

        private IEnumerator TimelineAnim(TimelineAdvancedEvent ta)
        {
            // The clock moves on: whoever just acted gives up the prominence slot. Every icon glides from where it is
            // drawn (lastAt) to the snapshot; one that only now appears starts at its own snapshot u, so nothing is
            // created and then torn down mid-glide (GG-T287).
            actingEntry = -1;
            var starts = lastAt != null ? new Dictionary<int, float>(lastAt) : new Dictionary<int, float>();
            float seconds = Mathf.Clamp(ta.Ticks / 40f, 0.18f, 0.45f);
            // GG-T319: the tray follows the snapshot: rings wind down over the glide, and an icon whose recovery ran out
            // in this stretch slides onto the bar as it goes.
            var recStart = new Dictionary<int, (int left, int total)>(recoverView);
            var recEnd = ta.Recover;
            void Recover(float k)
            {
                if (recEnd == null) return;
                recoverView.Clear();
                foreach (var kv in recEnd)
                {
                    int from = recStart.TryGetValue(kv.Key, out var r0) ? r0.left : kv.Value.Left;
                    recoverView[kv.Key] = (Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(from, kv.Value.Left, k))), kv.Value.Total);
                }
            }
            yield return Tween.Run(seconds, k =>
            {
                Recover(k);
                var at = new Dictionary<int, float>();
                foreach (var kv in ta.U) at[kv.Key] = Mathf.Lerp(starts.TryGetValue(kv.Key, out var v) ? v : kv.Value, kv.Value, k);
                SyncTimeline(at); // lanes, overtakes, badges and cast rings follow the glide
            }, Tween.EaseInOut);
            Recover(1f);
            SyncTimeline(ta.U);
        }

        /// <summary>Glide one icon between two u positions while everything else holds still.</summary>
        private IEnumerator GlideOne(int entryId, float fromU, float toU, float seconds)
        {
            var hold = new Dictionary<int, float>();
            foreach (var e in State.Timeline) hold[e.Id] = lastAt != null && lastAt.TryGetValue(e.Id, out var u) ? u : BattleEngine.BarU(State, e);
            yield return Tween.Run(seconds, k => { hold[entryId] = Mathf.Lerp(fromU, toU, Tween.EaseOut(k)); SyncTimeline(hold); });
        }

        private IEnumerator PushAnim(PushedBackEvent pb)
        {
            var entry = State.TurnEntryOf(pb.UnitId);
            App.Audio.Sfx("battle.slide", 0.8f, 0.8f);
            if (entry != null && inTray.Contains(entry.Id))
                // GG-T319: off the bar, so it recovers longer: its tray ring winds back out instead of a bar icon moving.
                App.StartCoroutine(ExtendRecovery(entry.Id, pb.RecoverTicks > 0 ? pb.RecoverTicks
                    : Mathf.Max(1, Mathf.RoundToInt((pb.ToU - pb.FromU) * BattleConfig.BarSeconds * BattleConfig.TicksPerSecond))));
            else if (entry != null && timelineIconViews.ContainsKey(entry.Id))
                App.StartCoroutine(TossOne(entry.Id, pb.FromU, pb.ToU, 0.38f));
            yield return FloatText(pb.UnitId, CombatTextKind.Timer, "Pushback!", 0.12f);
        }

        /// <summary>GG-T367 / GG-T1071: Pushback tosses the icon back down the bar: it moves to its new u at once in the
        /// view's model, and PushSlide draws it sliding back along its line, spinning about its Y axis and easing in.</summary>
        private IEnumerator TossOne(int entryId, float fromU, float toU, float seconds)
        {
            var hold = new Dictionary<int, float>();
            foreach (var e in State.Timeline) hold[e.Id] = lastAt != null && lastAt.TryGetValue(e.Id, out var u) ? u : BattleEngine.BarU(State, e);
            hold[entryId] = toU;
            SyncTimeline(hold);
            yield return PushSlide(entryId, fromU, toU, seconds);
        }

        /// <summary>A staggered cast slips right on the bar by its delay (GG-T287), with the knock's slide.</summary>
        private IEnumerator StaggerAnim(CastStaggeredEvent cs)
        {
            if (!cs.Resisted && cs.DelayTicks > 0 && lastAt != null && lastAt.TryGetValue(cs.EntryId, out var from))
            {
                App.Audio.Sfx("battle.slide", 0.7f, 1.15f);
                App.StartCoroutine(TossOne(cs.EntryId, from, Mathf.Clamp01(from + CastSpanU(cs.DelayTicks)), 0.38f)); // GG-T1071: a delay is a pushback
            }
            yield return FloatText(cs.CasterId, cs.Resisted ? CombatTextKind.Resist : CombatTextKind.Timer, cs.Resisted ? "Steady!" : "Stagger!", 0.25f, 0.4f);
        }

        private IEnumerator HastenAnim(HastenedEvent h)
        {
            if (timelineIconViews.ContainsKey(h.EntryId))
                App.StartCoroutine(GlideOne(h.EntryId, h.FromU, h.ToU, 0.3f));
            yield return FloatText(h.UnitId, CombatTextKind.Buff, "Haste!", 0.2f);
        }

        private IEnumerator TurnAnim(TurnStartedEvent ts)
        {
            var u = State.UnitById(ts.UnitId);
            if (u != null) Feed($"▶ {u.Name}'s turn", new Color(1f, 0.6f, 0.5f));
            App.Audio.Sfx("battle.turn", 0.7f);
            var entry = State.TurnEntryOf(ts.UnitId);
            if (entry != null && timelineIconViews.ContainsKey(entry.Id))
            {
                // It takes the prominence slot for its turn (GG-T287) while its bar icon flies into the recovery tray
                // (GG-T319) with a full ring; the next TimelineAdvanced snapshot carries its real recovery from there.
                actingEntry = entry.Id;
                var reset = new Dictionary<int, float>();
                foreach (var e in State.Timeline) reset[e.Id] = lastAt != null && lastAt.TryGetValue(e.Id, out var lu) ? lu : BattleEngine.BarU(State, e);
                reset[entry.Id] = BattleEngine.BarU(State, entry);
                int total = Mathf.Max(BattleConfig.TicksPerSecond, entry.RecoverTotalTicks);
                recoverView[entry.Id] = (total, total);
                SyncTimeline(reset);
                if (!ReduceMotion) App.StartCoroutine(Tween.Pop(timelineSlot, 1.3f, Timing.Standard));
            }
            if (u != null && board.Units.TryGetValue(u.Id, out var v))
            {
                v.SetGlow(Palette.Bad, 0.85f);
                ShowBanner($"{u.Name}'s turn");
                yield return Tween.Pop(v.Root, 1.1f, 0.2f);
                v.SetGlow(Palette.White, 0f);
            }
            // GG-T839: a foe's intent: focus, callout and its card from the right (its wind-up waits for the act).
            if (enemyBeat != null && enemyBeat.ActorId == ts.UnitId && !enemyBeat.IntentShown) yield return EnemyIntent(enemyBeat);
        }

        private IEnumerator CastQueuedAnim(CastQueuedEvent cq)
        {
            unplayedCasts.Remove(cq.EntryId); // from this beat on the cast rides the bar, born at its cast time
            SyncTimeline(null);
            App.Audio.Sfx("battle.cast");
            var caster = State.UnitById(cq.CasterId);
            if (cq.LineTiles != null) foreach (var t in cq.LineTiles) board.Mark(t, new Color(1f, 0.2f, 0.15f), 0.4f);
            string what = App.Content.TryAbility(cq.AbilityId)?.Name ?? "spell";
            var entry = State.Timeline.FirstOrDefault(x => x.Id == cq.EntryId);
            var target = entry == null ? null : State.UnitById(entry.TargetUnitId) ?? State.UnitAt(entry.TargetTile);
            string on = target != null ? $" on {target.Name}" : cq.LineTiles != null ? " down a line" : "";
            float secs = cq.Ticks / (float)BattleConfig.TicksPerSecond;
            ShowBanner($"{caster?.Name} {(cq.IsCharge ? "charges" : "casts")} {what}{on} — lands in {secs:0.#}s");
            Billboard($"{caster?.Name}: {what}{on}", Art.AbilityIcon(App.Content.TryAbility(cq.AbilityId)), caster != null && caster.IsHero ? Palette.White : new Color(1f, 0.7f, 0.65f));
            AddGhost(cq);
            var tu = State.UnitById(cq.TargetUnitId);
            DodgeBillboard(new[] { cq.TargetTile }.Concat(tu != null ? ViewTiles(tu) : Enumerable.Empty<Coord>()).Concat(cq.LineTiles ?? new List<Coord>()));
            var castColor = CastColor(App.Content.TryAbility(cq.AbilityId), cq.AbilityId);
            if (caster != null) yield return Gather(caster, castColor);
            if (caster != null) { StartChannel(caster.Id, castColor); App.Audio.Sfx("battle.channel", 0.7f); }
            SyncTimeline(null);
            yield return FloatText(cq.CasterId, CombatTextKind.Timer, cq.IsCharge ? "Charging" : "Casting", 0.3f);
        }

        private IEnumerator ClutchAnim(ClutchEvent c)
        {
            var flash = Ui.El("fill", App.Overlay);
            flash.style.backgroundColor = Palette.White;
            flash.pickingMode = PickingMode.Ignore;
            App.Audio.Sfx("battle.heal");
            yield return Tween.Fade(flash, 0.9f * Mathf.Max(0.3f, Fx), 0f, 0.35f);
            flash.RemoveFromHierarchy();
            yield return FloatText(c.CasterId, CombatTextKind.Callout, "CLUTCH!", 0.3f, 1f);
            // GG-T571: what earned it, small under the float: the kind's glossary headword, "Clutch: " dropped.
            if (App.Content.GlossaryIndex.TryGetValue(ClutchKinds.GlossaryId(c.Kind), out var g) && !string.IsNullOrEmpty(g.Term))
            {
                string name = g.Term.StartsWith("Clutch: ", System.StringComparison.Ordinal) ? g.Term.Substring(8) : g.Term;
                yield return FloatText(c.CasterId, CombatTextKind.Support, name, 0.2f);
            }
        }

        private IEnumerator AbilityAnim(AbilityResolvedEvent ar)
        {
            var a = BattleEngine.ResolveAbilityId(App.Content, ar.AbilityId);
            hitTier = VfxStyleSheet.TierOf(a);
            var abilityStyle = StyleForAbility(a);
            DodgeBillboard(ar.Tiles.Concat(ar.Targets.Select(State.UnitById).Where(u => u != null).SelectMany(ViewTiles)));
            var caster = State.UnitById(ar.CasterId);
            var hit = ar.Targets.Select(State.UnitById).Where(u => u != null).Select(u => u.Name).Distinct().ToList();
            string who = hit.Count == 0 ? "" : hit.Count <= 2 ? " → " + string.Join(" & ", hit) : $" → {hit.Count} targets";
            ShowBanner(a == null ? "" : caster != null ? $"{caster.Name}: {a.Name}{who}" : a.Name + who);
            if (a != null && a.Kind != AbilityKind.Passive && a.Kind != AbilityKind.Reactive) Billboard(a.Name + who, Art.AbilityIcon(a), Palette.White);
            // The caster's portrait is the cast-splash beat: it slides in and out BEFORE the projectile flies, so it never covers the flight.
            if (a != null && caster != null && a.Kind == AbilityKind.Spell && CutInsEnabled && !ReduceMotion) yield return CasterSlide(caster, a.Name);
            board.ClearMarks();
            Color c = a == null ? Palette.White : a.BaseHeal > 0 ? Palette.Good : ColorFor(a.DamageType);
            foreach (var t in ar.Tiles) board.Mark(t, c, 0.22f);
            var targets = ar.Targets.Select(State.UnitById).Where(u => u != null).ToList();
            EndGhost(ar.CasterId, ar.AbilityId);
            SyncCastGhosts(lastAt);
            bool itemUse = pendingItemUser >= 0 && pendingItemUser == ar.CasterId;
            string itemId = pendingItemId;
            pendingItemUser = -1; pendingItemId = null;
            if (itemUse && caster != null && a != null)
                yield return ItemVfx(caster, targets, a, itemId);
            else if (caster != null && FloatSpell.Is(a)) yield return FloatVfx(ar); // GG-T872: the updraft and the lift, then Floating
            else if (caster != null && GravityWell.Is(a)) yield return GravityWellVfx(ar); // GG-T883: the vortex, then the pull's slides
            else if (caster != null && AnchorStance.Is(State, a)) yield return AnchorStanceVfx(caster); // GG-T882: the planted shield and its brace lines
            else if (EmpPulse.Is(a)) yield return EmpPulseVfx(ar); // GG-T879: the static ring, before the jams and the mines
            else if (caster != null && Intimidate.Is(App.Content, a)) yield return IntimidateVfx(caster, ar); // GG-T884: the roar's shockwaves, then each Frightened
            else if (caster != null && a != null && a.Kind == AbilityKind.Spell && targets.Count > 0)
            {
                yield return SpellVfx(caster, targets, a, c);
                if (ar.Tiles.Count > 1) yield return AreaSplash(ar.Anchor, ar.Tiles, c);
            }
            else if (caster != null && a != null && a.Kind == AbilityKind.Skill && a.AdjacentOnly && targets.Count > 0)
                yield return MeleeSkillVfx(caster, targets[0]);
            else if (caster != null && a != null && a.Kind == AbilityKind.Skill && (a.Shape == TargetShape.Self || a.Shape == TargetShape.AllAllies) && a.ApplyBuff != null)
                yield return RallyVfx(caster, a);
            else if (caster != null && a != null && a.Motion != ProjectileMotion.None && targets.Count > 0)
            {
                Vector2 from = ViewCenter(caster);
                App.Audio.Sfx("battle.cast", 0.7f);
                yield return Tween.Run(a.Motion == ProjectileMotion.Strike ? 0.18f : 0.26f, k =>
                {
                    board.Lines.ClearLines();
                    foreach (var t in targets)
                    {
                        Vector2 to = ViewCenter(t);
                        var p = ProjectilePoint(a.Motion, from, to, k);
                        board.Lines.Dot(p, 26f, c);
                        board.Lines.Dot(ProjectilePoint(a.Motion, from, to, Mathf.Max(0f, k - 0.08f)), 16f, new Color(c.r, c.g, c.b, 0.5f));
                    }
                });
                board.Lines.ClearLines();
                foreach (var t in targets) NamedImpactFlourish(a.ImpactVfx, ViewCenter(t), abilityStyle);
            }
            else if (caster != null && WallRaisedBy(a) != null) yield return BarricadeDropAnim(ar.Anchor); // GG-T935
            else if (caster != null && a != null && OilFlask.Kindling(App.Content, a) is var oil && oil != null) yield return OilFlaskAnim(caster, oil.Value.State, ar); // GG-T886
            else if (caster != null && GlobPainted(a) is TileStateDef glob) yield return GlobLobAnim(caster, glob, ar.Anchor); // GG-T895
            else if (caster != null && FissurePainted(a) is TileStateDef crack) yield return FissureAnim(caster, a, crack, ar.Anchor); // GG-T869
            else if (caster != null && a != null && targets.Count == 0 && FlareRadius(a) >= 0)
                yield return FlareShotVfx(caster, a, ar.Anchor, FlareRadius(a), abilityStyle); // GG-T888
            else yield return Tween.Wait(Timing.Quick);
            App.StartCoroutine(ClearMarksLater(0.35f));
        }

        /// <summary>Where the last spell mote stopped above each target: the element strike (lightning…) starts there.</summary>
        private readonly Dictionary<int, Vector2> strikeFrom = new Dictionary<int, Vector2>();

        /// <summary>
        /// Every spell plays the same readable arc (owner direction 2026-10-10): the caster draws energy (motes spiral
        /// in), a cast burst, then one mote per target curves to the space above it; the impact (lightning strike,
        /// flame burst, frost ring, heal sparkles …) follows from that point when the damage/heal lands.
        /// </summary>
        private IEnumerator SpellVfx(Unit caster, List<Unit> targets, AbilityDef a, Color c)
        {
            Vector2 from = ViewCenter(caster);
            bool calm = ReduceMotion;
            var st = StyleForAbility(a);
            float sag = st.Shape == "bubble" || st.Shape == "droplet" ? 1f : 0.2f; // heavy trails drip, light ones hang
            board.Units.TryGetValue(caster.Id, out var cv);
            // 1. Drawing energy (a timed cast already drew it when it was queued).
            if (a.CastTime <= 0f) yield return Gather(caster, c);
            // 2. Cast burst.
            App.Audio.Sfx("battle.cast", 0.9f, 1.2f);
            yield return Tween.Run(Timing.Quick, k => { board.Lines.Rings.Clear(); board.Lines.Ring(from, 40f + 90f * k, new Color(c.r, c.g, c.b, 1f - k), 10f); });
            board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
            if (cv != null) cv.SetGlow(Palette.White, 0f);
            // 3. One mote per target curves up and over to the space above it.
            var ends = targets.ToDictionary(t => t.Id, t => ViewCenter(t) + new Vector2(0f, -BoardView.Tile * 0.85f));
            var bends = targets.ToDictionary(t => t.Id, t => (Random.value < 0.5f ? -1f : 1f) * Random.Range(90f, 160f));
            float travel = calm ? 0.2f : Mathf.Clamp(Vector2.Distance(from, ends.Values.First()) / 1600f, 0.26f, 0.5f);
            // Each projectile is a glowing orb (bloom + white-hot core) leaving a comet tail of fading embers.
            var heads = targets.ToDictionary(t => t.Id, t => (glow: Particle(110f, new Color(c.r, c.g, c.b, 0.95f)), core: Particle(34f, Palette.White)));
            Vector2 Mid(int id, float kk)
            {
                Vector2 to = ends[id];
                var d = to - from; var n = new Vector2(-d.y, d.x).normalized;
                Vector2 ctrl = (from + to) / 2f + n * bends[id] + Vector2.up * -120f;
                float e = Tween.EaseInOut(kk);
                return (1 - e) * (1 - e) * from + 2 * (1 - e) * e * ctrl + e * e * to;
            }
            yield return Tween.Run(travel, k =>
            {
                foreach (var t in targets)
                {
                    var p = Mid(t.Id, k);
                    var (glow, core) = heads[t.Id];
                    glow.style.left = p.x - 55; glow.style.top = p.y - 55;
                    core.style.left = p.x - 17; core.style.top = p.y - 17;
                    if (!calm)
                    {
                        // The comet tail is the element's own particle (embers for fire, shards for ice, bubbles for poison).
                        var ember = Shaped(st, Random.Range(18f, 36f) * 0.7f, Color.Lerp(c, Palette.White, Random.value * 0.35f), Random.Range(0f, 6.28f));
                        var ep = p + new Vector2(Random.Range(-8f, 8f), Random.Range(-8f, 8f));
                        Fly(ember, Timing.Standard, 0f, kk => (ep + Vector2.up * 30f * kk * sag, 1f - kk, 1f - 0.6f * kk));
                    }
                }
            });
            foreach (var (glow, core) in heads.Values) { glow.RemoveFromHierarchy(); core.RemoveFromHierarchy(); }
            App.Audio.Sfx("battle.whoosh", 0.5f, 1.3f);
            // Pure cleanse/hasten utility spells read as their own bespoke burst rather than the
            // generic ring (bible §6.2 future-pass VFX wiring) — plain damage/heal/debuff spells keep it.
            bool isCleanse = a.BaseDamage <= 0 && a.BaseHeal <= 0 && (a.RemovesDebuffs || a.RemovesBuff != null);
            bool isHastenOnly = a.BaseDamage <= 0 && a.BaseHeal <= 0 && a.HastenU > 0f && a.ApplyBuff == null;
            foreach (var t in targets)
            {
                var at = ViewCenter(t);
                if (isCleanse) App.StartCoroutine(CleanseBurst(at));
                else if (isHastenOnly) App.StartCoroutine(HastenBurst(at));
                else
                {
                    ImpactSplash(at, c, a.BaseDamage > 0 ? 0.8f : 1f);
                    NamedImpactFlourish(a.ImpactVfx, at, st);
                }
            }
            if (a.BaseDamage <= 0) App.Audio.Sfx("element.water", 0.6f, 1.2f);
            foreach (var kv in ends) strikeFrom[kv.Key] = kv.Value;
            if (a.BaseHeal > 0 || (a.Filter == TargetFilter.AllyOnly && !isCleanse && !isHastenOnly))
                foreach (var t in targets) App.StartCoroutine(HealBurst(ViewCenter(t)));
        }

        /// <summary>Projectile curves (bible §6.1). Reduce Motion flattens everything to a straight line.</summary>
        private Vector2 ProjectilePoint(ProjectileMotion m, Vector2 from, Vector2 to, float k)
        {
            if (ReduceMotion) m = ProjectileMotion.Straight;
            var d = to - from;
            var n = new Vector2(-d.y, d.x).normalized;
            switch (m)
            {
                case ProjectileMotion.Bezier: return Vector2.Lerp(from, to, k) + Vector2.up * (-160f * Ease.Arc(k));
                case ProjectileMotion.Homing: return Vector2.Lerp(from, to, Ease.InOut(k)) + n * (90f * Ease.Arc(k) * (1f - k));
                case ProjectileMotion.Spiral: { float r = 70f * (1f - k); float ang = k * Mathf.PI * 6f; return Vector2.Lerp(from, to, k) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r; }
                case ProjectileMotion.Twist: return Vector2.Lerp(from, to, k) + n * (40f * Mathf.Sin(k * Mathf.PI * 3f));
                case ProjectileMotion.Strike: { var top = new Vector2(to.x, to.y - 520f); return Vector2.Lerp(top, to, k * k); }
                default: return Vector2.Lerp(from, to, k);
            }
        }

        private IEnumerator ClearMarksLater(float s)
        {
            yield return Tween.Wait(s);
            if (TargetingSlot < 0) board.ClearMarks();
        }

        /// <summary>The colour a spell's energy reads as: its element (fire orange, poison green…), heals green, status spells their status colour.</summary>
        private static Color CastColor(AbilityDef a, string id)
        {
            if (a == null) return Art.SpellColor(id);
            if (a.BaseHeal > 0) return Palette.Good;
            if (a.DamageType != DamageType.Physical && a.DamageType != DamageType.Arcane) return ColorFor(a.DamageType);
            var b = App.Content.TryBuff(a.ApplyBuff);
            if (b != null) return Art.HexColor(b.Color, Art.SpellColor(id));
            // Non-damage utility spells read gold (cleanse's breaking chains, hasten's sparkle)
            // instead of falling through to a generic arcane purple (bible §6.2 future-pass VFX wiring).
            if (a.RemovesDebuffs || a.RemovesBuff != null || a.HastenU > 0f) return Palette.Gold;
            return Art.SpellColor(id);
        }

        /// <summary>Cleanse / dispel: every debuff burns off as breaking gold motes (bible §6.2 AntidoteSparkle).</summary>
        private IEnumerator CleanseBurst(Vector2 at)
        {
            App.Audio.Sfx("battle.heal", 0.7f, 1.3f);
            var gold = new Color(1f, 0.88f, 0.5f);
            for (int i = 0; i < 10; i++)
            {
                var p = Particle(Random.Range(12f, 20f), gold);
                float ang = Random.Range(0f, 6.28f), dist = Random.Range(50f, 110f);
                App.StartCoroutine(Tween.Run(Timing.Emphasis, k =>
                {
                    var pos = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist * Tween.EaseOut(k) + Vector2.up * (-40f * k);
                    p.style.left = pos.x - 9; p.style.top = pos.y - 9; p.style.opacity = 1f - k; if (k >= 1f) p.RemoveFromHierarchy();
                }));
            }
            yield return Tween.Wait(Timing.Standard);
        }

        /// <summary>Hasten-only spells (Quicken): a gold updraft nudges the target's timeline icon forward.</summary>
        private IEnumerator HastenBurst(Vector2 at)
        {
            App.Audio.Sfx("battle.cast", 0.6f, 1.6f);
            var gold = Palette.Gold;
            for (int i = 0; i < 8; i++)
            {
                var p = Particle(Random.Range(10f, 16f), gold);
                float x0 = Random.Range(-40f, 40f);
                App.StartCoroutine(Tween.Run(Timing.Emphasis, k =>
                {
                    p.style.left = at.x + x0 - 7; p.style.top = at.y - 120f * Tween.EaseOut(k) - 7;
                    p.style.opacity = 1f - k; if (k >= 1f) p.RemoveFromHierarchy();
                }));
            }
            yield return Tween.Wait(Timing.Standard);
        }

        private static Color ColorFor(DamageType t) => Palette.Element(t);

        private VisualElement Particle(float size, Color c)
        {
            var p = new VisualElement { pickingMode = PickingMode.Ignore };
            p.style.position = Position.Absolute;
            p.style.width = p.style.height = size;
            p.style.backgroundImage = new StyleBackground(Art.Glow);
            p.style.unityBackgroundImageTintColor = c;
            board.FxLayer.Add(p);
            return p;
        }

        /// <summary>An element's impact (bible §6.2, GG-T253): its vfxstyle.json look at the hit's tier.</summary>
        private IEnumerator ElementBurst(Vector2 at, VfxStyleDef style, VfxTierDef tier)
        {
            if (TryPrefab(style, at, tier.Tier, out _)) { yield return Tween.Wait(Timing.Instant); yield break; }
            Emit(style, at, tier.Count, sizeMul: tier.Size);
            yield return Tween.Wait(style.Motion == "bolt" ? Timing.Quick : Timing.Instant);
        }

        /// <summary>"Drawing energy": motes spiral into the caster while its frame glows in the spell colour.</summary>
        private IEnumerator Gather(Unit caster, Color c)
        {
            Vector2 from = ViewCenter(caster);
            App.Audio.Sfx("battle.cast", 0.5f);
            board.Units.TryGetValue(caster.Id, out var cv);
            cv?.SetGlow(c, 0.8f);
            int motes = ReduceMotion ? 4 : 12;
            for (int i = 0; i < motes; i++)
            {
                var p = Particle(Random.Range(14f, 24f), c);
                float ang = i * Mathf.PI * 2f / motes + Random.Range(-0.2f, 0.2f), dist = Random.Range(120f, 170f), spin = Random.Range(1.5f, 2.5f);
                App.StartCoroutine(Tween.Run(Timing.Emphasis, k =>
                {
                    float r = dist * (1f - k * k), aa = ang + spin * k;
                    var pos = from + new Vector2(Mathf.Cos(aa), Mathf.Sin(aa)) * r;
                    p.style.left = pos.x - 10; p.style.top = pos.y - 10;
                    p.style.opacity = 0.4f + 0.6f * k; if (k >= 1f) p.RemoveFromHierarchy();
                }));
            }
            yield return Tween.Wait(Timing.Emphasis);
            cv?.SetGlow(Palette.White, 0f);
        }

        /// <summary>The mote end above the unit at <paramref name="at"/>, consumed once.</summary>
        private Vector2? TakeStrikeOrigin(Vector2 at)
        {
            foreach (var kv in strikeFrom)
                if (Mathf.Abs(kv.Value.x - at.x) < BoardView.Tile * 0.6f && kv.Value.y < at.y && at.y - kv.Value.y < BoardView.Tile * 2.2f)
                { strikeFrom.Remove(kv.Key); return kv.Value; }
            return null;
        }

        private IEnumerator HealBurst(Vector2 at)
        {
            Emit(HealStyle, at);
            yield break;
        }

        /// <summary>Little particles radiating from a hit (UI Toolkit, no particle system needed).</summary>
        private void Sparks(Vector2 at, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                var p = new VisualElement { pickingMode = PickingMode.Ignore };
                p.style.position = Position.Absolute;
                float size = Random.Range(10f, 22f);
                p.style.width = p.style.height = size;
                p.style.backgroundImage = new StyleBackground(Art.Glow);
                p.style.unityBackgroundImageTintColor = color;
                board.FxLayer.Add(p);
                float ang = Random.Range(0f, Mathf.PI * 2f), dist = Random.Range(60f, 150f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                App.StartCoroutine(Tween.Run(Timing.Standard, k =>
                {
                    var pos = at + dir * dist * Tween.EaseOut(k) + Vector2.up * (60f * k * k);
                    p.style.left = pos.x - size / 2; p.style.top = pos.y - size / 2;
                    p.style.opacity = 1f - k;
                    if (k >= 1f) p.RemoveFromHierarchy();
                }));
            }
        }
    }
}
