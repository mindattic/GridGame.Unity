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
        private IEnumerator RespiteAnim(RespiteEvent rs)
        {
            foreach (var id in new[] { rs.Attacker1, rs.Attacker2 })
                if (board.Units.TryGetValue(id, out var v))
                    App.StartCoroutine(Tween.Run(Timing.Emphasis, k => v.Root.style.rotate = new Rotate(k >= 1f ? 0f : 360f * Tween.EaseInOut(k))));
            App.Audio.Sfx("battle.whoosh", 0.7f, 1.2f); // GG-T465: the spin was silent
            yield return FloatText(rs.Attacker1, CombatTextKind.Buff, "Respite", 0.45f);
        }

        /// <summary>GG-T187 (§1.2.3): a supporter performs its class's support action — its cut-in card carries the action's
        /// name and the action floats over it; the effect's own beats (heal, buff, haste, mana, strikes) follow.</summary>
        private IEnumerator SupportAnim(SupportActionEvent sa)
        {
            var sup = State.UnitById(sa.SupporterId);
            if (sup == null || !board.Units.TryGetValue(sup.Id, out var sv)) yield break;
            string label = string.IsNullOrEmpty(sa.Label) ? sa.Effect.ToString() : sa.Label;
            // §25.20 (GG-T1309): a bonded supporter's card names its bond.
            SupporterPop(sup, sup.Team, sa.BondTier > 0 ? $"{label.ToUpperInvariant()} · BOND {sa.BondTier}" : label.ToUpperInvariant());
            // The support strikes, and Volley's thrown hits (GG-T915), animate themselves.
            if (sa.Effect == SupportEffect.Strike || sa.Effect == SupportEffect.Volley) yield break;
            var color = SupportColor(sa.Effect);
            if (sa.Effect == SupportEffect.Cast && !sup.IsHero) color = Palette.CastEnemy; // GG-T839: a foe supporter's spell echo reads as theirs
            // GG-T298: the float names the edge it brings ("+20% Crit", "Fireball", "Poisoned", "Pushback +0.5 s").
            string what = GridGame.Core.Rules.SupportText.Short(App.Content, sa.Effect, sa.Amount, sa.AbilityId, sa.BuffId);
            Feed(what == null ? $"✦ {sup.Name}: {label}" : $"✦ {sup.Name}: {label} — {what}", color);
            App.Audio.Sfx(SupportCue(sup, sa.Effect), 0.7f, 1.2f);
            App.StartCoroutine(Tween.Pop(sv.Root, 1.12f, 0.18f));
            // GG-T836: inside a pincer the effect flies — Prep to the attacker it helps, Follow-up to the trapped foes.
            if (pincerBeatPhase == PincerPhase.Prep && PrepFlies(sa.Effect)) { yield return PrepBeat(sup, sa, label, what, color); yield break; }
            if (pincerBeatPhase == PincerPhase.FollowUp && FollowUpFlies(sa.Effect)) { yield return FollowUpBeat(sup, sa, label, what, color); yield break; }
            var at = BoardView.CenterOf(sv.Tile, sv.W, sv.H);
            Text.Show(at, CombatTextKind.Support, what == null ? label + "!" : $"{label}! {what}", 0f, color);
            yield return Tween.Wait(ReduceMotion ? 0.08f : 0.2f);
        }

        /// <summary>One sound per support effect (POL-D4), reusing the battle ids.</summary>
        private static string SupportSfx(SupportEffect e)
        {
            switch (e)
            {
                case SupportEffect.Heal: return "battle.heal";
                case SupportEffect.Mana: return "battle.orb";
                case SupportEffect.Crit: return "battle.crit";
                case SupportEffect.Status: return "status.debuff";
                case SupportEffect.Pushback: return "battle.whoosh";
                case SupportEffect.Volley: return "battle.whoosh";
                case SupportEffect.Buff: return "battle.powerup";
                default: return "battle.cast";
            }
        }

        private static Color SupportColor(SupportEffect e)
        {
            switch (e)
            {
                case SupportEffect.Heal: return Palette.Good;
                case SupportEffect.Buff: return Palette.Buff;
                case SupportEffect.Haste: return Palette.Haste;
                case SupportEffect.Mana: return Palette.Mana;
                case SupportEffect.Cleanse: return Palette.Cleanse;
                case SupportEffect.Crit: return Palette.Gold;
                case SupportEffect.Cast: return Palette.CastHero;
                case SupportEffect.Status: return Palette.Bad;
                case SupportEffect.Pushback: return Palette.Warn;
                case SupportEffect.Volley: return Palette.PewterLight;
                default: return Palette.Info;
            }
        }

        /// <summary>GG-T915: the supporter's own <c>SupportDef.Sfx</c> when its class sets one, else the effect's cue.</summary>
        private static string SupportCue(Unit sup, SupportEffect e)
        {
            string own = GridGame.Core.Rules.PincerDetector.SupportOf(sup).Sfx;
            return string.IsNullOrEmpty(own) ? SupportSfx(e) : own;
        }

        private IEnumerator AttackAnim(AttackEvent at)
        {
            var a = State.UnitById(at.AttackerId); var t = State.UnitById(at.TargetId);
            if (a == null || t == null || !board.Units.TryGetValue(a.Id, out var av)) yield break;
            if (at.IsThrown) { yield return ThrownAnim(a, t); yield break; }
            Vector2 dir = (ViewCenter(t) - ViewCenter(a)).normalized;
            if (at.IsRanged)
            {
                // A hexed bolt flies down the lane.
                var from = ViewCenter(a); var to = ViewCenter(t);
                App.Audio.Sfx("element.arcane", 0.7f);
                yield return Tween.Run(Timing.Standard, k => { board.Lines.ClearLines(); board.Lines.Dot(ProjectilePoint(ProjectileMotion.Twist, from, to, k), 22f, new Color(0.7f, 0.35f, 1f)); });
                board.Lines.ClearLines();
                yield break;
            }
            if (at.IsSupport) SupporterPop(a, a.Team);
            float lunge = (at.IsSupport ? 18f : 34f) * Fx + 6f;
            yield return Tween.Run(Timing.Instant, k => av.Root.style.translate = new Translate(dir.x * lunge * k, dir.y * lunge * k));
            App.StartCoroutine(Tween.Run(Timing.Quick, k => av.Root.style.translate = new Translate(dir.x * lunge * (1 - k), dir.y * lunge * (1 - k))));
        }

        /// <summary>Volley projectiles in flight as looping prefabs (GG-T915); TickPrefabFx keeps their loops alive.</summary>
        private readonly HashSet<string> thrownLoops = new HashSet<string>();

        private int thrownSeq;

        /// <summary>
        /// GG-T915 (§1.2.3): a Volley hit (<c>AttackEvent.IsThrown</c>) is thrown from the supporter to the target, as its
        /// class's <c>SupportDef</c> says: a Loop <c>Projectile</c> prefab flies the path (a Once one bursts at impact, and
        /// none draws a dot), <c>Motion</c> Arc curves it, Straight and Spin fly flat, Spin with a spinning cross, and
        /// <c>Sfx</c> (else battle.whoosh) sounds the throw. Reduce Motion flattens every throw and keeps it code-drawn.
        /// </summary>
        private IEnumerator ThrownAnim(Unit a, Unit t)
        {
            var def = GridGame.Core.Rules.PincerDetector.SupportOf(a);
            var c = SupportColor(SupportEffect.Volley);
            Vector2 from = ViewCenter(a), to = ViewCenter(t);
            App.Audio.Sfx(SupportCue(a, SupportEffect.Volley), 0.7f, 1.2f);
            var fx = prefabFx != null && !ReduceMotion && !string.IsNullOrEmpty(def.Projectile) ? App.Content.VfxPrefabs.Find(def.Projectile) : null;
            string key = null;
            if (fx != null && fx.Mode == VfxMode.Loop)
            {
                key = "throw:" + a.Id + ":" + t.Id + ":" + (++thrownSeq);
                if (prefabFx.Play(fx, from, null, key)) thrownLoops.Add(key);
                else key = null;
            }
            var motion = ReduceMotion ? SupportMotion.Straight : def.Motion;
            float travel = ReduceMotion ? 0.14f : Mathf.Clamp(Vector2.Distance(from, to) / 1800f, 0.18f, 0.32f);
            float arm = BoardView.Tile * 0.22f;
            yield return Tween.Run(travel, k =>
            {
                Vector2 p = motion == SupportMotion.Arc
                    ? Vector2.Lerp(from, to, k) + Vector2.up * (-140f * Ease.Arc(k))
                    : Vector2.Lerp(from, to, k);
                board.Lines.ClearLines();
                if (key != null) { prefabFx.MoveLoop(key, p); return; }
                if (motion == SupportMotion.Spin)
                {
                    float ang = k * Mathf.PI * 8f;
                    var u = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * arm;
                    var v = new Vector2(-u.y, u.x);
                    board.Lines.Add(p - u, p + u, c, 9f);
                    board.Lines.Add(p - v, p + v, c, 9f);
                    board.Lines.Dot(p, 7f, Palette.White);
                }
                else
                {
                    board.Lines.Dot(p, 18f, c);
                    board.Lines.Dot(Vector2.Lerp(from, to, Mathf.Max(0f, k - 0.1f)), 11f, new Color(c.r, c.g, c.b, 0.5f));
                }
            });
            board.Lines.ClearLines();
            if (key != null) thrownLoops.Remove(key); // TickPrefabFx ends the loop next frame
            if (fx != null && fx.Mode == VfxMode.Once) prefabFx.Play(fx, to);
        }

        /// <summary>The ability whose hits are playing (GG-T556), or null for weapon strikes.</summary>
        private string hitAbility;

        /// <summary>A physical hit's sound by attack type (GG-T556, Content/attacktypes.json): the resolving ability's,
        /// else the attacker's weapon's, else its natural attack; Audio falls back to the generic hits while a type
        /// has no files.</summary>
        private string AttackHitCue(Unit src, string note)
        {
            var sheet = App.Content.AttackTypes;
            if (sheet == null || sheet.Types.Count == 0) return "battle.hit";
            string weaponType = src?.WeaponId != null ? App.Content.TryItem(src.WeaponId)?.WeaponType : null;
            string type = sheet.ForHit(src?.Def?.Id, weaponType, hitAbility, note);
            return Audio.HitPrefix + type;
        }

        private IEnumerator DamageAnim(DamagedEvent d)
        {
            var t = State.UnitById(d.TargetId);
            if (t == null || !board.Units.TryGetValue(t.Id, out var v)) yield break;
            var at = BoardView.CenterOf(v.Tile, v.W, v.H);
            if (d.Hit == HitKind.Miss)
            {
                App.Audio.Sfx("battle.miss");
                Text.Show(at, CombatTextKind.Miss, "MISS");
                yield return Tween.Wait(Timing.Quick);
                yield break;
            }
            bool crit = d.Hit == HitKind.Critical;
            var style = StyleOf(d.Type);
            // A crit or a quarter of the target's HP hits one tier harder than the action's own (POL-E5).
            var tier = Vfx.TierDef(crit || d.Amount >= t.MaxHp * 0.25f ? Bump(hitTier) : hitTier);
            var src = State.UnitById(d.SourceId);
            string cue = d.SourceId < 0 ? "status.debuff" : crit ? "battle.crit" : style.Cue;
            if (cue == "battle.hit") cue = AttackHitCue(src, d.Note);
            var burn = d.Amount > 0 ? FireTileBurn(d, v.Tile) : null; // GG-T933: scorched by a firewall
            if (burn != null) FireCrackle(0.9f); else App.Audio.Sfx(cue);
            Feed($"{(d.SourceId < 0 ? "☠" : "†")} {(src != null ? src.Name + " → " : "")}{t.Name}: {d.Amount}{(crit ? " CRIT" : "")}{(d.Note != null ? " (" + d.Note + ")" : "")}", t.IsHero ? new Color(1f, 0.65f, 0.6f) : Palette.White.WithA(0.9f));
            // Anticipation (POL-E6): the target braces for a beat before the hit lands.
            if (!ReduceMotion) yield return Tween.Run(Timing.Instant, k => v.Root.style.scale = new Scale(Vector2.one * (1f - 0.06f * Ease.Out(k))));
            if (d.Amount > 0 && !ReduceMotion)
            {
                if (d.SourceId < 0) Emit(style, at, 0.5f, quiet: true); // a damage-over-time tick: a small puff of its element
                else App.StartCoroutine(ElementBurst(at, style, tier));
            }
            string text = d.Note == "Armored!" ? "ARMORED" : (crit ? $"{d.Amount}!" : d.Amount.ToString());
            // GG-T495: an elemental hit names its element under Colorblind palette, since its burst says it only in colour.
            if (Colorblind && d.Note != "Armored!" && d.Type != DamageType.Physical) text += " " + d.Type;
            var color = d.Note == "Armored!" ? new Color(0.7f, 0.7f, 0.75f) : crit ? new Color(1f, 0.85f, 0.2f) : t.IsHero ? new Color(1f, 0.5f, 0.45f) : Palette.White;
            if (tallying && d.SourceId >= 0 && d.Note != "Armored!") AddToTally(t, at, d.Amount, crit, d.Note, d.Effectiveness, d.Overkill);
            else
            {
                // GG-T826: the hit's kind (armored, damage-over-time tick, killing blow, crit, plain hit) picks its
                // style; its share of the target's max HP grows it. A plain hit on a hero keeps its red tint.
                var kind = d.Note == "Armored!" ? CombatTextKind.Immune : d.SourceId < 0 ? CombatTextKind.StatusTick
                    : d.HpAfter <= 0 ? CombatTextKind.Overkill : crit ? CombatTextKind.Crit : CombatTextKind.Damage;
                // GG-T830: a damage-over-time tick names its status: small, its pip glyph and its colour.
                var dot = kind == CombatTextKind.StatusTick && d.Note != null ? t.Buffs.Find(b => b.Def.Name == d.Note)?.Def : null;
                // GG-T933: a firewall's burn ticks with the mark's own glyph and colour.
                if (burn != null && kind == CombatTextKind.StatusTick)
                    Text.Tick(at, string.IsNullOrEmpty(burn.Glyph) ? "♨" : burn.Glyph, text, Art.HexColor(burn.Color, Palette.Bad), t.MaxHp > 0 ? d.Amount * 2f / t.MaxHp : 0f);
                else if (dot != null) Text.Tick(at, StatusGlyph(dot), text, StatusColor(dot), t.MaxHp > 0 ? d.Amount * 2f / t.MaxHp : 0f);
                else if (kind == CombatTextKind.Immune || kind == CombatTextKind.StatusTick)
                    Text.Show(at, kind, text, t.MaxHp > 0 ? d.Amount * 2f / t.MaxHp : 0f, kind == CombatTextKind.Immune ? color : (Color?)null);
                // GG-T827: a hit's number reads by its share of max HP in four sizes (chip, solid, heavy, massive), tagged
                // WEAK / RESIST / IMMUNE by the target's elemental multiplier, with OVERKILL over a kill that overshot.
                else Text.Damage(at, kind, text, d.Amount, t.MaxHp, d.Effectiveness, d.Overkill, kind == CombatTextKind.Damage ? color : (Color?)null);
            }
            v.SetHp(d.HpAfter, t.MaxHp);
            v.Frame.style.unityBackgroundImageTintColor = Palette.Bad;
            v.Art.style.unityBackgroundImageTintColor = new Color(1f, 0.55f, 0.55f);
            App.StartCoroutine(Tween.Shake(v.Root, (crit ? Impact.CritTileShake : Impact.HitTileShake) * Fx, crit ? Timing.Standard : Timing.Quick));
            if (crit) App.StartCoroutine(Tween.Shake(shake, Impact.CritIconShake * Fx, Timing.Quick)); // GG-T827: a crit jolts the screen
            if (crit) ShakeTurnIcon(t.Id); // GG-T668: a crit jolts the target's timeline icon too
            if (tier.Shake > 0f) App.StartCoroutine(Tween.Shake(shake, tier.Shake * Fx, Timing.Standard));
            // Impact: the hit-stop holds the frame, longer for heavier tiers (POL-E5/E6); Reduce Motion skips it.
            // GG-T827: a crit slams: it holds the frame a beat longer than its tier alone would.
            yield return Tween.Wait(Timing.Instant + (ReduceMotion ? 0f : tier.HitStop + (crit ? CombatText.CritHitStop : 0f)));
            // Follow-through: the target recoils back past rest and settles.
            if (!ReduceMotion) App.StartCoroutine(Tween.Run(Timing.Quick, k => v.Root.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(0.94f, 1f, Ease.Back(k)))));
            v.Art.style.unityBackgroundImageTintColor = Palette.White;
            v.Frame.style.unityBackgroundImageTintColor = Palette.White;
        }

        private IEnumerator HealAnim(HealedEvent h)
        {
            var t = State.UnitById(h.TargetId);
            if (t == null || !board.Units.TryGetValue(t.Id, out var v)) yield break;
            Feed($"✚ {t.Name} +{h.Amount} HP", Palette.Good);
            App.Audio.Sfx("battle.heal");
            if (!ReduceMotion) App.StartCoroutine(HealBurst(BoardView.CenterOf(v.Tile, v.W, v.H)));
            v.SetHp(h.HpAfter, t.MaxHp);
            v.SetGlow(new Color(0.4f, 1f, 0.5f), 0.8f);
            Text.Show(v, CombatTextKind.Heal, "+" + h.Amount, t.MaxHp > 0 ? h.Amount * 2f / t.MaxHp : 0f);
            yield return Tween.Wait(Timing.Standard);
            v.SetGlow(Palette.White, 0f);
        }

        private IEnumerator DeathAnim(DiedEvent d)
        {
            if (!board.Units.TryGetValue(d.UnitId, out var v)) yield break;
            var du = State.UnitById(d.UnitId);
            if (du != null) Feed($"★ {du.Name} is defeated", du.IsHero ? new Color(1f, 0.45f, 0.4f) : Palette.Gold);
            App.Audio.Sfx("battle.death");
            // GridGame2026's death-portrait dissolve (GG-T96): the token goes at once and the unit's whole,
            // uncropped artwork takes its place over the board, shuddering, shrinking and fading on its own —
            // playback carries straight on (loot burst, the next kill in the chain) while it plays out.
            if (du != null) App.StartCoroutine(DeathDissolve(du, v));
            v.Root.RemoveFromHierarchy();
            v.Dead = true;
            yield return Tween.Wait(Timing.Quick);
        }

        // Tuned from GridGame2026's per-frame Portrait3D dissolve at 60 fps: alpha 196/255 minus 1% a frame
        // (about 1.3 s), scale ×0.99 a frame, jittering up to ±1/12 tile around its start point.
        private const float DissolveStartAlpha = 196f / 255f;

        private const float DissolveSeconds = DissolveStartAlpha / 0.6f;

        private const float DissolveShrinkPerSecond = 0.6f; // -ln(0.99) × 60 = 0.603

        /// <summary>The fallen unit's full portrait, centred on its footprint and well over a tile so the whole art reads.</summary>
        private IEnumerator DeathDissolve(Unit u, UnitView v)
        {
            string classId = u.IsCorpse && u.CorpseOf != null ? u.CorpseOf : u.Def?.Id;
            if (string.IsNullOrEmpty(classId) || !Art.HasPortrait(classId)) yield break;
            var fig = Art.PortraitFigure(classId);
            var size = Art.FigureSize(classId);
            float aspect = size.y > 0 ? size.x / size.y : 1f;
            float box = BoardView.Tile * (2f + Mathf.Max(v.W, v.H));
            float w = aspect >= 1f ? box : box * aspect, h = aspect >= 1f ? box / aspect : box;
            var centre = BoardView.CenterOf(v.Tile, v.W, v.H);
            var el = new VisualElement { name = "dissolve-" + u.Id, pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            el.style.width = w; el.style.height = h;
            // Centred on the footprint, but nudged back inside the board at its edges so the whole art
            // still reads instead of sliding under the HUD and banners (a top-row kill otherwise lost half).
            float boardW = BoardView.Tile * BattleConfig.Cols, boardH = BoardView.Tile * BattleConfig.Rows;
            el.style.left = w < boardW ? Mathf.Clamp(centre.x - w / 2f, 0f, boardW - w) : centre.x - w / 2f;
            el.style.top = h < boardH ? Mathf.Clamp(centre.y - h / 2f, 0f, boardH - h) : centre.y - h / 2f;
            el.style.backgroundImage = new StyleBackground(fig);
            el.style.opacity = DissolveStartAlpha;
            // Beneath the floating numbers and coin burst on the fx layer, above every token, mark and line.
            board.FxLayer.Insert(0, el);
            float jitter = BoardView.Tile / 12f;
            yield return Tween.Run(DissolveSeconds, k =>
            {
                float t = k * DissolveSeconds;
                el.style.opacity = Mathf.Max(0f, DissolveStartAlpha - 0.6f * t);
                el.style.scale = new Scale(Vector2.one * Mathf.Exp(-DissolveShrinkPerSecond * t));
                el.style.translate = ReduceMotion || k >= 1f
                    ? new Translate(0, 0)
                    : new Translate(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter));
            });
            el.RemoveFromHierarchy();
        }

        // ── Damage tally (owner direction 2026-10-10): hits within a pincer stack up, then explode into the total ──

        private sealed class Tally
        {
            public Label El; public List<string> Terms = new List<string>(); public int Total; public bool Crit; public Vector2 At; public int UnitId;
            public HitEffectiveness Effect; public int Overkill; // GG-T827: the burst carries the hits' elemental read and overshoot
        }

        private readonly Dictionary<int, Tally> tallies = new Dictionary<int, Tally>();

        private bool tallying, ampTally;

        public int TalliesBurst { get; private set; }

        private void AddToTally(Unit t, Vector2 at, int amount, bool crit, string note, HitEffectiveness effect = HitEffectiveness.Normal, int overkill = 0)
        {
            if (!tallies.TryGetValue(t.Id, out var tl))
            {
                tl = new Tally { At = at, UnitId = t.Id };
                tl.El = board.Float("", at + new Vector2(0, -BoardView.Tile * 0.62f), Palette.White, TypeScale.Base);
                tl.El.name = "tally-" + t.Id;
                tallies[t.Id] = tl;
            }
            tl.Terms.Add(crit ? amount + "!" : amount.ToString());
            if (ampTally && tl.Terms.Count == 1) tl.Terms.Add("AMP");
            tl.Total += amount; tl.Crit |= crit; tl.Overkill += overkill;
            if (tl.Effect == HitEffectiveness.Normal) tl.Effect = effect;
            // Each hit lands on its own line below the last, stacking downward like a fighting-game combo list.
            tl.El.text = string.Join("\n+ ", tl.Terms).Replace("+ AMP", "×AMP");
            tl.El.style.color = tl.Crit ? Palette.Gold : Palette.White;
            App.StartCoroutine(Tween.Pop(tl.El, 1.25f, 0.12f));
        }

        /// <summary>Each target's stacked terms collapse into one big number that bursts and floats away.</summary>
        private void FlushTallies()
        {
            tallying = false; ampTally = false;
            if (tallies.Count == 0) return;
            foreach (var tl in tallies.Values.ToList()) App.StartCoroutine(BurstTally(tl));
            tallies.Clear();
        }

        private IEnumerator BurstTally(Tally tl)
        {
            int realTerms = tl.Terms.Count(x => x != "AMP");
            if (realTerms <= 1)
            {
                tl.El.text = tl.Terms.Count > 0 ? tl.Terms[0] : "";
                yield return StrikeIn(tl.El, tl.Crit);
                yield break;
            }
            TalliesBurst++;
            yield return Tween.Run(Timing.Quick, k => { tl.El.style.scale = new Scale(Vector2.one * (1f - 0.5f * k)); tl.El.style.opacity = 1f - 0.6f * k; });
            tl.El.RemoveFromHierarchy();
            // GG-T827: the total bursts in the damage style: sized by its share of the target's max HP, tagged, OVERKILL on a spill.
            int maxHp = State.UnitById(tl.UnitId)?.MaxHp ?? 0;
            var total = Text.Damage(tl.At, tl.Crit ? CombatTextKind.Crit : CombatTextKind.Combo, tl.Crit ? tl.Total + "!" : tl.Total.ToString(), tl.Total, maxHp, tl.Effect, tl.Overkill);
            total.name = "tally-total";
            App.Audio.Sfx("battle.crit", 0.7f, 0.9f);
            App.StartCoroutine(Tween.Shake(shake, 10f * Fx, Timing.Quick));
            if (tl.Crit)
            {
                if (board.Units.TryGetValue(tl.UnitId, out var tv)) App.StartCoroutine(Tween.Shake(tv.Root, Impact.CritTileShake * Fx, Timing.Standard));
                ShakeTurnIcon(tl.UnitId);
            }
        }

        /// <summary>GG-T668: a damage number strikes. It starts big above the target, falls onto it accelerating like a
        /// blow, squashes, settles with a small overshoot, holds, then drops and fades fast: the opposite of a heal's
        /// gentle <see cref="FloatAway"/> rise. A crit falls further, starts bigger and lands faster. Calm (Reduce Motion)
        /// keeps the landing with less travel and no overshoot.</summary>
        private IEnumerator StrikeIn(Label l, bool crit)
        {
            float rest = l.style.top.value.value;
            float travel = (crit ? Impact.StrikeDropCrit : Impact.StrikeDrop) * (Tween.Calm ? Impact.CalmTravel : 1f);
            float big = crit ? Impact.StrikeScaleCrit : Impact.StrikeScale;
            l.style.opacity = 1f;
            l.style.top = rest - travel;
            l.style.scale = new Scale(Vector2.one * big);
            yield return Tween.Run(crit ? Impact.StrikeInCrit : Impact.StrikeIn, k =>
            {
                float e = k * k;
                l.style.top = rest - travel * (1f - e);
                l.style.scale = new Scale(Vector2.one * Mathf.Lerp(big, Impact.StrikeSquash, e));
            });
            if (Tween.Calm) l.style.scale = new Scale(Vector2.one);
            else yield return Tween.Run(Impact.StrikeSettle, k => l.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(Impact.StrikeSquash, 1f, Ease.Back(k))));
            yield return Tween.Wait(crit ? Impact.StrikeHoldCrit : Impact.StrikeHold);
            yield return Tween.Run(Impact.StrikeOut, k =>
            {
                l.style.top = rest + Impact.StrikeExitDrop * Ease.Out(k);
                l.style.opacity = 1f - k;
            });
            l.RemoveFromHierarchy();
        }

        /// <summary>Shakes the unit's turn icon on the timeline, if it has one drawn (GG-T668). View only.</summary>
        private void ShakeTurnIcon(int unitId)
        {
            foreach (var e in drawn.Values)
                if (e.OwnerId == unitId && e.Kind == TimelineKind.Turn && timelineIconViews.TryGetValue(e.Id, out var icon))
                {
                    App.StartCoroutine(Tween.Shake(icon, Impact.CritIconShake * Fx, Timing.Standard));
                    return;
                }
        }

        /// <summary>A Passive or Reactive fired (GG-T296, POL-D4): its callout floats over the unit with a cue — a clang for a
        /// counterstrike or reflect, a war-cry for a free cast, a soft power-up for the rest; its effects' own beats follow.</summary>
        private IEnumerator TriggeredAnim(TriggeredEvent tr)
        {
            RefreshUnit(tr.UnitId);
            if (tr.Effect == TriggerEffectKind.Substitute) return null; // GG-T862: SubstitutionAnim calls it out over the log
            string sfx = tr.Effect == TriggerEffectKind.Counterstrike || tr.Effect == TriggerEffectKind.Reflect ? "battle.clang"
                : tr.Effect == TriggerEffectKind.CastAbility ? "battle.warcry" : "battle.powerup";
            App.Audio.Sfx(sfx, tr.Kind == AbilityKind.Reactive ? 0.8f : 0.5f);
            string text = string.IsNullOrEmpty(tr.Callout) ? "!" : tr.Callout.EndsWith("!") ? tr.Callout : tr.Callout + "!";
            return FloatText(tr.UnitId, tr.Kind == AbilityKind.Reactive ? CombatTextKind.Buff : CombatTextKind.Support, text, tr.Kind == AbilityKind.Reactive ? 0.2f : 0.1f);
        }

        /// <summary>Evasion slipped a blow (GG-T297, POL-D4): the defender sidesteps away from the attacker and back with
        /// a whoosh while "Dodge!" floats up.</summary>
        private IEnumerator DodgeAnim(DodgedEvent dg)
        {
            var t = State.UnitById(dg.UnitId);
            var a = State.UnitById(dg.AttackerId);
            App.Audio.Sfx("battle.whoosh", 0.8f, 1.2f);
            if (t != null && board.Units.TryGetValue(t.Id, out var v) && !ReduceMotion)
            {
                Vector2 away = a != null ? (ViewCenter(t) - ViewCenter(a)).normalized : Vector2.right;
                Vector2 side = new Vector2(-away.y, away.x); // step out of the line of the blow
                float d = 26f * Fx + 6f;
                App.StartCoroutine(Tween.Run(Timing.Quick, k =>
                {
                    float s = Ease.Arc(k);
                    v.Root.style.translate = new Translate(side.x * d * s, side.y * d * s);
                }));
            }
            return FloatText(dg.UnitId, CombatTextKind.Miss, "Dodge!", 0.3f);
        }

        /// <summary>GG-T1582: a tier-5 Trust hero's Resolve held it at 1 HP against a lethal blow: a clang, the hero
        /// braces and "Resolve!" floats up, ahead of the Clutch! that pays for it.</summary>
        private IEnumerator ResolveAnim(ResolveEvent rv)
        {
            App.Audio.Sfx("battle.clang", 0.9f, 0.9f);
            RefreshUnit(rv.UnitId);
            if (board.Units.TryGetValue(rv.UnitId, out var v) && !ReduceMotion) App.StartCoroutine(Tween.Pop(v.Root, 1.15f, 0.22f));
            return FloatText(rv.UnitId, CombatTextKind.Callout, "Resolve!", 0.3f);
        }

        /// <summary>GG-T826: the battle's floating texts, one per board (bible §9.1).</summary>
        private CombatText combatText;

        private CombatText Text => combatText != null && combatText.Board == board ? combatText : combatText = new CombatText(board, App);

        /// <summary>GG-T828: floats a combat text over a unit (the board centre when it has no view) and waits
        /// <paramref name="hold"/>. The kind's style token picks the face, colour, motion and size; <paramref name="magnitude"/>
        /// (0..1) only says how big the moment is; <paramref name="tint"/> recolours it for a mechanic with its own colour
        /// (tame, wanderer, rule breaker, bounty). Every BattleScreen unit float comes through here: no call passes a size
        /// (GG-T829, POL-A1 in tools/polish_lint.py).</summary>
        /// <summary>GG-T892: the callout Core announces when a Heavy unit won't be shoved, pulled or swapped (GG-T891).</summary>
        private const string TooHeavyText = "Too heavy!";

        /// <summary>GG-T892: the "Too heavy!" callout's iron grey.</summary>
        private static readonly Color HeavyTint = new Color(0.72f, 0.76f, 0.84f);

        /// <summary>
        /// GG-T892 (§1.1.3, §14.2): a Heavy unit refuses to move - a fizzled knockback, pull or swap (its unit id) or a drag
        /// step into it (its tile): a low thud, a short shake of its token and a pop of its weight badge, and "Too heavy!"
        /// over it as a CombatText callout (not a banner). Reduce Motion keeps the sound and the callout.
        /// </summary>
        private IEnumerator TooHeavyAnim(int unitId, Coord? at)
        {
            UnitView v = null;
            if (unitId >= 0) board.Units.TryGetValue(unitId, out v);
            else if (at.HasValue && State.UnitAt(at.Value) is Unit occ) board.Units.TryGetValue(occ.Id, out v);
            HeavyThud(v, at ?? v?.Tile);
            yield return Tween.Wait(Timing.Standard);
        }

        /// <summary>GG-T892: the thud, shake, badge pop and "Too heavy!" callout of a Heavy unit that wouldn't budge (the drag
        /// plays it live, playback for a shove, pull, swap or a bot's step).</summary>
        private void HeavyThud(UnitView v, Coord? tile)
        {
            App.Audio.Sfx("battle.clang", 0.55f, 0.45f);
            App.Audio.Sfx("battle.hit", 0.7f, 0.5f);
            var at = v != null && !v.Dead ? BoardView.CenterOf(v.Tile, v.W, v.H) : tile.HasValue ? BoardView.CenterOf(tile.Value) : (Vector2?)null;
            if (at.HasValue) Text?.Show(at.Value, CombatTextKind.Callout, TooHeavyText, 0.3f, HeavyTint);
            if (v == null || v.Dead || ReduceMotion) return;
            App.StartCoroutine(Tween.Shake(v.Root, 5f * Fx, Timing.Quick));
            if (v.HeavyBadge != null) App.StartCoroutine(Tween.Pop(v.HeavyBadge, 1.35f, Timing.Standard));
        }

        private IEnumerator FloatText(int unitId, CombatTextKind kind, string text, float hold, float magnitude = 0f, Color? tint = null)
        {
            Text.Show(FloatAt(unitId), kind, text, magnitude, tint);
            yield return Tween.Wait(hold);
        }

        /// <summary>Where a unit's float sits: its view's centre, else the board centre.</summary>
        private Vector2 FloatAt(int unitId) =>
            State.UnitById(unitId) != null && board.Units.TryGetValue(unitId, out var v) ? BoardView.CenterOf(v.Tile, v.W, v.H) : new Vector2(BoardView.Tile * 3, BoardView.Tile * 4);
    }
}
