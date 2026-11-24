using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T836 (battle feel, GG-T823): the pincer beat sequencer, keyed on <see cref="PincerPhaseEvent"/>. A pincer plays
    /// as three readable beats instead of one blur:
    /// <list type="bullet">
    /// <item>Prep — each helping supporter pops in, a ribbon or mote flies from it to the attacker it helps (a buff aura
    /// ribbon, a heal sparkle, a crit glint) carrying its combat text, and the attacker glows with it.</item>
    /// <item>Strike — both attackers wind up (pull back, flash), then the blows land.</item>
    /// <item>Follow-up — each supporter that hits the trapped line throws its <c>SupportDef.Projectile</c> at them
    /// (a spinning shuriken, an arcing dagger, a spell echo) and lands a VfxLibrary flourish, then its status text.</item>
    /// </list>
    /// Battle speed already scales every beat (Tween.Speed). On top of it a long chain plays faster link by link and,
    /// once one action's pincers have run past <see cref="PincerBeatCapSeconds"/>, at the cap tempo; a tap during a
    /// pincer skips ahead (the rest of the action's pincers play at <see cref="SkipTempo"/>). The tempo lives only while
    /// a pincer plays: it is restored as each pincer's follow-up ends and at the end of every playback.
    /// </summary>
    public sealed partial class BattleScreen
    {
        /// <summary>Real (unpaused, pre-tempo) seconds one action's pincers may take before later beats compress.</summary>
        private const float PincerBeatCapSeconds = 7f;
        /// <summary>The fastest a long chain's links play on their own, and the tempo a tap jumps to.</summary>
        private const float MaxChainTempo = 2.5f, SkipTempo = 6f;

        /// <summary>The phase now playing, or null outside a pincer.</summary>
        private PincerPhase? pincerBeatPhase;
        /// <summary>Tween.Speed (the battle-speed setting) before the sequencer leaned on it; null while it isn't.</summary>
        private float? beatBaseSpeed;
        private bool beatSkip;
        private float actionBeatSeconds, beatStartedClock;
        private readonly List<int> beatAttackers = new List<int>();
        private readonly List<int> beatFoes = new List<int>();

        /// <summary>Automation/telemetry: a tap skipped this action's pincer beats ahead.</summary>
        public bool PincerBeatsSkipped => beatSkip;

        /// <summary>A new action or turn: its pincers start at normal tempo with a fresh budget.</summary>
        private void ResetPincerBeats()
        {
            EndPincerTempo();
            beatSkip = false;
            actionBeatSeconds = 0f;
            pincerBeatPhase = null;
        }

        /// <summary>A pincer formed (before its cut-in): remember who strikes whom and pick its tempo.</summary>
        private void BeginPincerBeats(PincerFormedEvent p)
        {
            EndPincerTempo(); // a link that never closed its phases still counts toward the budget
            beatAttackers.Clear(); beatAttackers.Add(p.Attacker1); beatAttackers.Add(p.Attacker2);
            beatFoes.Clear(); beatFoes.AddRange(p.Opponents);
            pincerBeatPhase = null;
            beatStartedClock = Tween.Clock;
            beatBaseSpeed = Tween.Speed;
            ApplyBeatTempo();
        }

        /// <summary>The tempo this pincer's beats play at, on top of battle speed: chain links speed up (the 2nd ×1.25,
        /// the 3rd ×1.5 …, at most <see cref="MaxChainTempo"/>), past the per-action cap every beat plays at the max, and
        /// a tap jumps to <see cref="SkipTempo"/>.</summary>
        private float BeatTempo =>
            beatSkip ? SkipTempo
            : actionBeatSeconds >= PincerBeatCapSeconds ? MaxChainTempo
            : Mathf.Min(MaxChainTempo, 1f + 0.25f * Mathf.Max(0, chainCount - 1));

        private void ApplyBeatTempo()
        {
            if (beatBaseSpeed == null) return;
            Tween.Speed = beatBaseSpeed.Value * BeatTempo;
        }

        /// <summary>Gives the battle-speed setting back and books the pincer's real seconds against the action's cap.</summary>
        private void EndPincerTempo()
        {
            if (beatBaseSpeed == null) return;
            actionBeatSeconds += Mathf.Max(0f, Tween.Clock - beatStartedClock);
            Tween.Speed = beatBaseSpeed.Value;
            beatBaseSpeed = null;
        }

        /// <summary>A tap while a pincer plays skips ahead: the rest of this action's pincers play at skip tempo.
        /// True when the tap was spent on it.</summary>
        private bool TapSkipsPincerBeats()
        {
            if (!playing || beatBaseSpeed == null) return false;
            if (!beatSkip) App.Audio.Sfx("ui.click", 0.5f, 1.3f);
            beatSkip = true;
            ApplyBeatTempo();
            return true;
        }

        private IEnumerator PincerPhaseAnim(PincerPhaseEvent ph)
        {
            if (ph.End)
            {
                pincerBeatPhase = null;
                if (ph.Phase == PincerPhase.FollowUp) EndPincerTempo();
                return null;
            }
            pincerBeatPhase = ph.Phase;
            ApplyBeatTempo(); // the cap may have been crossed mid-chain
            return ph.Phase == PincerPhase.Blows ? WindUpBeat() : null;
        }

        // ── Strike beat: the attackers wind up, then the blows (AttackEvent) land ──
        private IEnumerator WindUpBeat()
        {
            if (beatSkip || ReduceMotion) yield break;
            var views = new List<(UnitView v, Vector2 back)>();
            Vector2 foes = Vector2.zero; int n = 0;
            foreach (var id in beatFoes) if (State.UnitById(id) is Unit f && f.Alive) { foes += ViewCenter(f); n++; }
            foreach (var id in beatAttackers)
            {
                var u = State.UnitById(id);
                if (u == null || !u.Alive || !board.Units.TryGetValue(u.Id, out var v)) continue;
                var away = n > 0 ? (ViewCenter(u) - foes / n).normalized : Vector2.zero;
                views.Add((v, away * 22f));
                v.SetGlow(Palette.White, 0.75f);
            }
            if (views.Count == 0) yield break;
            App.Audio.Sfx("battle.whoosh", 0.55f, 0.85f);
            yield return Tween.Run(Timing.Quick, k =>
            {
                foreach (var (v, back) in views) v.Root.style.translate = new Translate(back.x * Ease.Out(k), back.y * Ease.Out(k));
            });
            yield return Tween.Wait(Timing.Instant);
            yield return Tween.Run(Timing.Instant * 0.6f, k =>
            {
                foreach (var (v, back) in views) v.Root.style.translate = new Translate(back.x * (1f - k), back.y * (1f - k));
            });
            foreach (var (v, _) in views) { v.Root.style.translate = new Translate(0, 0); App.StartCoroutine(UnglowLater(v, Timing.Quick)); }
        }

        // ── Prep beat: supporter → attacker ribbon, its text at the attacker, the attacker glows ──
        private static bool PrepFlies(SupportEffect e) => e != SupportEffect.Strike && e != SupportEffect.Volley
            && e != SupportEffect.Cast && e != SupportEffect.Status && e != SupportEffect.Pushback;

        /// <summary>The Prep beat for one helping supporter (called from <see cref="SupportAnim"/>).</summary>
        private IEnumerator PrepBeat(Unit sup, SupportActionEvent sa, string label, string what, Color c)
        {
            var targets = sa.Targets.Select(State.UnitById).Where(u => u != null && board.Units.ContainsKey(u.Id)).Take(4).ToList();
            if (targets.Count == 0 && State.UnitById(sa.AttackerId) is Unit atk && board.Units.ContainsKey(atk.Id)) targets.Add(atk);
            string text = what == null ? label + "!" : $"{label}! {what}";
            var kind = sa.Effect == SupportEffect.Heal ? CombatTextKind.Heal : CombatTextKind.Buff;
            if (targets.Count == 0 || beatSkip)
            {
                foreach (var t in targets) Text.Show(board.Units[t.Id], kind, text, 0f, c);
                if (targets.Count == 0) Text.Show(ViewCenter(sup), CombatTextKind.Support, text, 0f, c);
                yield break;
            }
            Vector2 from = ViewCenter(sup);
            var tos = targets.Select(ViewCenter).ToList();
            float travel = ReduceMotion ? 0.1f : Mathf.Clamp(tos.Max(t => Vector2.Distance(from, t)) / 1600f, 0.2f, 0.32f);
            yield return Tween.Run(travel, k =>
            {
                board.Lines.ClearLines();
                foreach (var to in tos) DrawPrepMote(sa.Effect, from, to, k, c);
            });
            board.Lines.ClearLines();
            foreach (var t in targets)
            {
                var v = board.Units[t.Id];
                v.SetGlow(c, 0.85f);
                App.StartCoroutine(UnglowLater(v, Timing.Emphasis));
                App.StartCoroutine(Tween.Pop(v.Root, 1.1f, 0.16f));
                Text.Show(v, kind, text, 0f, c);
                if (!ReduceMotion) App.StartCoroutine(ArrivalRing(ViewCenter(t), c));
            }
            yield return Tween.Wait(ReduceMotion ? 0.06f : Timing.Quick);
        }

        /// <summary>One frame of a Prep mote: a buff's aura ribbon trails behind its head, a heal sparkles as it goes, a
        /// crit glints (a four-point star) and flies flat and fast; everything else is a plain ribbon.</summary>
        private void DrawPrepMote(SupportEffect e, Vector2 from, Vector2 to, float k, Color c)
        {
            if (ReduceMotion) { board.Lines.Dot(Vector2.Lerp(from, to, k), 14f, c); return; }
            switch (e)
            {
                case SupportEffect.Crit:
                {
                    var p = Vector2.Lerp(from, to, Ease.Snap(k));
                    float r = 26f * (0.6f + 0.4f * Mathf.Sin(k * Mathf.PI * 6f));
                    board.Lines.Add(p - Vector2.right * r, p + Vector2.right * r, Palette.White, 5f);
                    board.Lines.Add(p - Vector2.up * r, p + Vector2.up * r, Palette.White, 5f);
                    board.Lines.Dot(p, 11f, c);
                    break;
                }
                case SupportEffect.Heal:
                {
                    var p = Vector2.Lerp(from, to, Ease.InOut(k)) + Vector2.up * (-60f * Ease.Arc(k));
                    board.Lines.Dot(p, 14f, c);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = k * Mathf.PI * 4f + i * Mathf.PI * 0.5f;
                        board.Lines.Dot(p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 24f, 5f, Palette.White.WithA(0.8f));
                    }
                    break;
                }
                default:
                {
                    // A ribbon: the head pulls a fading tail of segments behind it along a gentle wave.
                    var n = (to - from).normalized; n = new Vector2(-n.y, n.x);
                    Vector2 At(float u) => Vector2.Lerp(from, to, u) + n * (26f * Mathf.Sin(u * Mathf.PI * 2f) * (1f - u));
                    float tail = Mathf.Max(0f, k - 0.35f);
                    const int segs = 6;
                    for (int i = 0; i < segs; i++)
                    {
                        float u0 = Mathf.Lerp(tail, k, i / (float)segs), u1 = Mathf.Lerp(tail, k, (i + 1) / (float)segs);
                        float a = (i + 1) / (float)segs;
                        board.Lines.Add(At(u0), At(u1), c.WithA(0.25f + 0.7f * a), 4f + 8f * a);
                    }
                    board.Lines.Dot(At(k), 13f, Palette.White.WithA(0.9f));
                    break;
                }
            }
        }

        private IEnumerator ArrivalRing(Vector2 at, Color c)
        {
            yield return Tween.Run(Timing.Quick, k => board.Lines.Ring(at, 30f + 50f * k, c.WithA(0.8f * (1f - k)), 6f));
            board.Lines.Rings.Clear();
            board.Lines.MarkDirtyRepaint();
        }

        // ── Follow-up beat: the supporter's projectile flies to the trapped foes, a flourish, then its status text ──
        private static bool FollowUpFlies(SupportEffect e) => e == SupportEffect.Cast || e == SupportEffect.Status || e == SupportEffect.Pushback;

        /// <summary>
        /// The Follow-up beat for one supporter that hits the trapped line (called from <see cref="SupportAnim"/>): its
        /// class's <c>SupportDef.Projectile</c> prefab (else a code-drawn shuriken, dagger or mote by <c>Motion</c>) flies
        /// to every target at once — a Cast first echoes a ring off the supporter — the projectile's (or spell's)
        /// VfxLibrary family flourishes on impact, then the status text lands on each foe. The effect's own events
        /// (the spell's hits, the status, the push) follow.
        /// </summary>
        private IEnumerator FollowUpBeat(Unit sup, SupportActionEvent sa, string label, string what, Color c)
        {
            var targets = sa.Targets.Select(State.UnitById).Where(u => u != null && board.Units.ContainsKey(u.Id)).ToList();
            if (targets.Count == 0) targets = beatFoes.Select(State.UnitById).Where(u => u != null && board.Units.ContainsKey(u.Id)).ToList();
            string text = what ?? label + "!";
            var kind = sa.Effect == SupportEffect.Pushback ? CombatTextKind.Timer : CombatTextKind.Debuff;
            if (targets.Count == 0 || beatSkip)
            {
                foreach (var t in targets) Text.Show(board.Units[t.Id], kind, text, 0f, c);
                if (targets.Count == 0) Text.Show(ViewCenter(sup), CombatTextKind.Support, text, 0f, c);
                yield break;
            }
            var def = GridGame.Core.Rules.PincerDetector.SupportOf(sup);
            var ability = sa.Effect == SupportEffect.Cast ? App.Content.TryAbility(sa.AbilityId) : null;
            var style = ability != null ? StyleForAbility(ability)
                : sa.Effect == SupportEffect.Status && sa.BuffId != null ? StatusStyle(sa.BuffId) ?? FallbackStyle
                : FallbackStyle;
            if (ability != null || style != FallbackStyle) c = GlowOf(style);
            Vector2 from = ViewCenter(sup);
            if (ability != null && !ReduceMotion)
            {
                // The spell echo: the supporter's cast rings out before it leaves.
                App.Audio.Sfx("battle.cast", 0.55f, 1.15f);
                yield return Tween.Run(Timing.Quick, k =>
                {
                    board.Lines.ClearLines();
                    board.Lines.Ring(from, 24f + 70f * k, c.WithA(0.9f * (1f - k)), 8f);
                    board.Lines.Ring(from, 12f + 40f * k, Palette.White.WithA(0.7f * (1f - k)), 4f);
                });
            }
            App.Audio.Sfx(SupportCue(sup, sa.Effect), 0.7f, 1.15f);
            var tos = targets.Select(ViewCenter).ToList();
            var fx = prefabFx != null && !ReduceMotion && !string.IsNullOrEmpty(def.Projectile) ? App.Content.VfxPrefabs.Find(def.Projectile) : null;
            var keys = new List<string>();
            if (fx != null && fx.Mode == VfxMode.Loop)
                foreach (var t in targets)
                {
                    string key = "followup:" + sup.Id + ":" + t.Id + ":" + (++thrownSeq);
                    if (prefabFx.Play(fx, from, null, key)) { thrownLoops.Add(key); keys.Add(key); }
                    else { keys.Clear(); break; }
                }
            // A spell homes in on its targets; a thrown weapon keeps its class's motion.
            var motion = ReduceMotion ? SupportMotion.Straight : ability != null ? SupportMotion.Arc : def.Motion;
            float travel = ReduceMotion ? 0.12f : Mathf.Clamp(tos.Max(t => Vector2.Distance(from, t)) / 1700f, 0.2f, 0.34f);
            float arm = BoardView.Tile * 0.22f;
            yield return Tween.Run(travel, k =>
            {
                board.Lines.ClearLines();
                for (int i = 0; i < tos.Count; i++)
                {
                    var to = tos[i];
                    Vector2 P(float u) => motion == SupportMotion.Arc ? Vector2.Lerp(from, to, u) + Vector2.up * (-150f * Ease.Arc(u)) : Vector2.Lerp(from, to, u);
                    var p = P(k);
                    if (i < keys.Count) { prefabFx.MoveLoop(keys[i], p); continue; }
                    if (motion == SupportMotion.Spin)
                    {
                        // A spinning shuriken: two crossed blades turning as it flies.
                        float ang = k * Mathf.PI * 10f;
                        var u = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * arm;
                        var v = new Vector2(-u.y, u.x);
                        board.Lines.Add(p - u, p + u, c, 9f);
                        board.Lines.Add(p - v, p + v, c, 9f);
                        board.Lines.Dot(p, 7f, Palette.White);
                    }
                    else if (motion == SupportMotion.Arc && ability == null)
                    {
                        // An arcing dagger: a blade pointed along its flight.
                        var dir = (P(Mathf.Min(1f, k + 0.04f)) - P(Mathf.Max(0f, k - 0.04f))).normalized;
                        board.Lines.Add(p - dir * arm, p + dir * arm, c, 8f);
                        board.Lines.Dot(p + dir * arm, 6f, Palette.White);
                    }
                    else
                    {
                        board.Lines.Dot(p, 18f, c);
                        board.Lines.Dot(P(Mathf.Max(0f, k - 0.1f)), 11f, c.WithA(0.5f));
                    }
                }
            });
            board.Lines.ClearLines();
            foreach (var key in keys) thrownLoops.Remove(key); // TickPrefabFx ends the loops next frame
            string flourish = !string.IsNullOrEmpty(def.Projectile) && VfxFamilyOf.ContainsKey(def.Projectile) ? def.Projectile : ability?.ImpactVfx;
            for (int i = 0; i < targets.Count; i++)
            {
                if (fx != null && fx.Mode == VfxMode.Once) prefabFx.Play(fx, tos[i]);
                NamedImpactFlourish(flourish, tos[i], style);
                var v = board.Units[targets[i].Id];
                App.StartCoroutine(Tween.Pop(v.Root, 1.08f, 0.14f));
                Text.Show(v, kind, text, 0f, c);
            }
            yield return Tween.Wait(ReduceMotion ? 0.06f : Timing.Quick);
        }
    }
}
