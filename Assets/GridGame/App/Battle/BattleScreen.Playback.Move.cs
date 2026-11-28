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
        // ── Individual beats ──────────────────────────────────────────────────────

        private IEnumerator StepAnim(int unitId, Coord to, float seconds, string sfx)
        {
            if (!board.Units.TryGetValue(unitId, out var v)) yield break;
            var from = BoardView.PosOf(v.Tile);
            // GG-T219: a walking unit streaks; a displaced one is shown by its shove instead.
            bool trail = sfx == "battle.move" && v.Tile != to;
            if (trail) board.Trail.Follow(v.Root, State.UnitById(unitId)?.IsHero == true ? HeroTrail : EnemyTrail);
            v.Tile = to;
            App.Audio.Sfx(sfx, 0.6f);
            try { yield return Tween.Move(v.Root, from, BoardView.PosOf(to), seconds); }
            finally { if (trail) board.Trail.Release(v.Root); }
        }

        /// <summary>GG-T1159: the unit is off the board (not Alive) until it surfaces, so its token leaves the board now —
        /// another token may legitimately step onto the burrow tile meanwhile.</summary>
        private IEnumerator BurrowAnim(BurrowedEvent b)
        {
            if (!board.Units.TryGetValue(b.UnitId, out var v) || v.Dead) yield break;
            yield return Tween.Fade(v.Root, 1, 0, Timing.Quick);
            v.Root.RemoveFromHierarchy();
            v.Dead = true;
        }

        private IEnumerator TeleportAnim(TeleportedEvent t)
        {
            if (!board.Units.TryGetValue(t.UnitId, out var v)) yield break;
            App.Audio.Sfx("element.arcane");
            if (v.Dead) { board.UnitLayer.Add(v.Root); v.Dead = false; v.Root.style.opacity = 0; } // GG-T1159: a burrower coming back up
            else yield return Tween.Fade(v.Root, 1, 0, Timing.Quick);
            board.Place(v, t.To);
            yield return Tween.Fade(v.Root, 0, 1, Timing.Quick);
        }

        private IEnumerator PincerAnim(PincerFormedEvent p)
        {
            chainCount++;
            supportersShown.Clear();
            var a = State.UnitById(p.Attacker1); var b = State.UnitById(p.Attacker2);
            if (a == null || b == null) yield break;
            board.Lines.ClearLines();
            var color = p.Team == Team.Hero ? Palette.Gold.WithA(0.95f) : Palette.Vfx.FoePincer.WithA(0.95f);
            // Every coordinate below is the event's formation-time snapshot. State is already the END of the command,
            // so reading positions from it stretched the outline across the board whenever a member moved later in the
            // same command (GG-T98: "a gold box ~3×3 tiles"). Views that drifted from the snapshot are put back on it.
            var aAt = p.Attacker1At; var bAt = p.Attacker2At;
            var oppAt = p.OpponentsAt;
            var geometryError = p.GeometryError();
            if (geometryError != null && Debug.isDebugBuild) Debug.LogError($"[GG-T98] malformed pincer outline: {geometryError} — {p}");
            foreach (var fp in new[] { aAt, bAt }.Concat(oppAt))
                if (board.Units.TryGetValue(fp.UnitId, out var fv) && !fv.Dead && fv.Tile != fp.At)
                {
                    if (Debug.isDebugBuild) Debug.LogWarning($"[GG-T98] view of unit {fp.UnitId} was on {fv.Tile}, pincer formed with it on {fp.At}");
                    board.Place(fv, fp.At);
                }
            DodgeBillboard(new[] { aAt, bAt }.Concat(oppAt).SelectMany(FootprintTiles));
            // The group (Hero, foe…, Hero) gets an outline that draws itself around it; supporters link edge to edge.
            var group = new List<(Coord, int, int)> { (aAt.At, aAt.W, aAt.H), (bAt.At, bAt.W, bAt.H) };
            foreach (var o in oppAt) group.Add((o.At, o.W, o.H));
            var rect = BoardView.GroupRect(group);
            var links = new List<(Vector2, Vector2)>();
            // GG-T187: a supporter links to the unit its chain runs through (an attacker or the next ally on the chain).
            var members = new List<UnitFootprint> { aAt, bAt };
            members.AddRange(p.SupportersAt);
            for (int si = 0; si < p.SupportersAt.Count; si++)
            {
                var s = p.SupportersAt[si];
                int linkId = si < p.SupporterLinks.Count ? p.SupporterLinks[si] : -1;
                var hit = members.Where(m => m.UnitId == linkId).ToList();
                var partner = hit.Count > 0 ? hit[0] : FootprintTiles(s).Any(t => t.IsCardinalNeighbor(aAt.At)) ? aAt : bAt;
                links.Add(BoardView.EdgeLink(s.At, partner.At));
            }
            App.Audio.Sfx("battle.pincer", 1f, 1f + 0.08f * (chainCount - 1));
            // The outline draws itself fully around the trapped line before anything else happens — a beat of
            // realization — so the pincer reads as "caught" before the cut-in slams in (owner request).
            yield return Tween.Run(ReduceMotion ? 0.05f : 0.24f, k =>
            {
                board.Lines.ClearLines();
                board.Lines.Outline(rect, color, 9f, k);
                if (k >= 0.6f) foreach (var (la, lb) in links) board.Lines.Bar(la, Vector2.Lerp(la, lb, (k - 0.6f) / 0.4f), ShapeStyle.Indicator.Tint(Palette.Mana.WithA(0.95f)).Thick(12f));
            });
            if (!ReduceMotion) yield return Tween.Wait(Timing.Quick);
            tallying = true;
            string label = p.Team == Team.Hero ? (chainCount > 1 ? $"CHAIN ×{chainCount}!" : "PINCER!") : "Enemy pincer!";
            // The first link's cut-in is the announcement (§9.0, GG-T1913): the overlay line and board callout say it again, so they show only when no cut-in plays.
            bool cutInPlays = CutInsEnabled && !ReduceMotion && chainCount == 1;
            Feed($"{(p.Team == Team.Hero ? "" : "Enemy ")}{(chainCount > 1 ? $"Chain ×{chainCount}" : "Pincer")}: {a.Name} & {b.Name}", p.Team == Team.Hero ? Palette.Gold : Palette.Vfx.FoeText);
            var supporters = p.Supporters.Select(State.UnitById).Where(x => x != null).ToList();
            if (supporters.Count > 0) Billboard("Support: " + string.Join(", ", supporters.Select(x => x.Name)), null, Palette.Vfx.Sky);
            var mid = (BoardView.CenterOf(aAt.At) + BoardView.CenterOf(bAt.At)) / 2f;
            if (!cutInPlays) ShowBanner(label);
            if (p.BondTier > 0) // §25.20 (GG-T1308): bonded attackers hit harder
            {
                Text.Show(mid + new Vector2(0f, 44f), CombatTextKind.Callout, $"Bond {p.BondTier}", 0f, Palette.Vfx.Bond);
                Billboard($"Bond {p.BondTier}: +{Mathf.RoundToInt(p.BondTier * BattleConfig.PincerDamagePerCohesionTier * 100f)}% pincer damage", null, Palette.Vfx.Bond);
            }
            if (board.Units.TryGetValue(a.Id, out var av)) App.StartCoroutine(Tween.Pop(av.Root, 1.15f, 0.2f));
            if (board.Units.TryGetValue(b.Id, out var bv)) App.StartCoroutine(Tween.Pop(bv.Root, 1.15f, 0.2f));
            // Cut-in modes keep a multi-attacker frenzy readable: the full sweep on the first link (with the supporters
            // sliding up from the bottom), quick pop-up cards beside the attackers on every chained link.
            if (!CutInsEnabled || ReduceMotion) yield return Tween.Wait(Timing.Standard);
            else if (chainCount == 1)
            {
                yield return PincerCutIn(a, b, p.Team, aAt.At, bAt.At);
            }
            else yield return PopUpCards(new[] { a, b }, p.Team); // their views sit on the snapshot tiles (snapped above)
        }

        // ── Pincer cut-in (ported from GridGame2026 PortraitManager / Portrait2DInstance, US-145) ──
        // Both attackers' portraits sweep past in close-up lanes flanking the attacked line on the old
        // slide curve (fast in, slight overshoot, drift back, fast out) with hand-held sway, while the
        // board "camera" pushes in on the pincer and settles back afterwards.

        public int CutInsShown { get; private set; }

        private bool CutInsEnabled => App.Settings.PincerCutIns;

        private static readonly AnimationCurve CutInCurve = new AnimationCurve(
            new Keyframe(0.0f, -1.0f, 0f, 0f, 0f, 0f),
            new Keyframe(0.8f, 0.05202637f, 0f, 0f, 0.33333334f, 0.70263505f),
            new Keyframe(1.2f, -0.05f, 0f, 0f, 0.33333334f, 0.33322528f),
            new Keyframe(1.993103f, 1.0f, 0f, 0f, 0f, 0f));

        private const float CutInSpeed = 1.75f;

        /// <summary>How far a rotated shot band bleeds past the stage so its tilted ends never show.</summary>
        private const float ShotOverscan = 200f;

        private const float CutInStagger = 0.14f;

        private static float CutInSeconds => 1.993103f / CutInSpeed;

        /// <summary>Debug/automation: play the cut-in on the first two living heroes.</summary>
        public void DemoCutIn()
        {
            var heroes = State.Units.Where(u => u.IsHero && u.Alive).Take(2).ToList();
            if (heroes.Count == 2) App.StartCoroutine(PincerCutIn(heroes[0], heroes[1], Team.Hero, ViewTile(heroes[0]), ViewTile(heroes[1])));
        }

        /// <summary>Debug/automation: play the mid-wave surprise narrative beat (§14.8) on demand.</summary>
        public void DemoSurprise(string speakerClassId, string line)
        {
            App.StartCoroutine(SurpriseAnnounce(speakerClassId, line));
        }

        private sealed class CutInPortrait
        {
            public VisualElement El; public bool Horizontal; public float Lane; public float Dir; public float Delay;
            public float Distance; public float W, H; public float Seed;
        }

        /// <param name="aAt">Where <paramref name="a"/> stood when the pincer formed (not its end-of-command State tile).</param>
        private IEnumerator PincerCutIn(Unit a, Unit b, Team team, Coord aAt, Coord bAt)
        {
            CutInsShown++;
            var layer = new VisualElement { name = "cutin", pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = Space.S0; layer.style.top = Space.S0; layer.style.right = Space.S0; layer.style.bottom = Space.S0;
            layer.style.overflow = Overflow.Hidden;
            var dim = new VisualElement { pickingMode = PickingMode.Ignore };
            dim.style.position = Position.Absolute;
            dim.style.left = Space.S0; dim.style.top = Space.S0; dim.style.right = Space.S0; dim.style.bottom = Space.S0;
            dim.style.backgroundColor = team == Team.Hero ? Palette.Vfx.DimHero : Palette.Vfx.DimFoe;
            dim.style.opacity = 0f;
            layer.Add(dim);
            App.Overlay.Add(layer);

            var origin = new Vector2(BoardLeft, BoardTop);
            var mid = origin + (BoardView.CenterOf(aAt, a.W, a.H) + BoardView.CenterOf(bAt, b.W, b.H)) / 2f;
            bool rowLine = aAt.Y == bAt.Y;
            float StageW = GameApp.StageWidth, StageH = GameApp.StageHeight;

            // Entry pattern (old US-145): counter sweep, mirrored, same-side chase, staggered one-two.
            float d1 = Random.value < 0.5f ? 1f : -1f, d2 = -d1, delay2 = 0f;
            switch (Random.Range(0, 4))
            {
                case 2: d2 = d1; delay2 = CutInStagger; break;
                case 3: delay2 = CutInStagger; break;
            }

            var shots = new List<CutInPortrait>();
            Unit[] who = { a, b };
            for (int i = 0; i < 2; i++)
            {
                var fig = Art.PortraitFigure(who[i].Def.Id);
                var size = Art.FigureSize(who[i].Def.Id);
                float aspect = fig != null && size.y > 0 ? size.x / size.y : 0.7f;
                float h = rowLine ? 760f : 1040f, w = h * aspect;
                if (!rowLine && w > 540f) { w = 540f; h = w / aspect; }
                var el = new VisualElement { name = $"cutin-{who[i].Id}", pickingMode = PickingMode.Ignore };
                el.style.position = Position.Absolute;
                el.style.width = w; el.style.height = h;
                el.style.backgroundImage = new StyleBackground(fig);
                el.style.left = -w * 2; el.style.top = -h * 2;
                layer.Add(el);
                shots.Add(new CutInPortrait
                {
                    El = el, Horizontal = rowLine, Dir = i == 0 ? d1 : d2, Delay = i == 0 ? 0f : delay2, W = w, H = h,
                    Distance = rowLine ? StageW / 2f + w / 2f + 2f : StageH / 2f + h / 2f + 2f, Seed = Random.Range(0f, 100f),
                });
            }
            // Lanes flank the attacked line without covering it; at the board edge both shift into the open space.
            if (rowLine)
            {
                float gap = BoardView.Tile / 2f + 20f;
                float la = mid.y - gap - shots[0].H / 2f, lb = mid.y + gap + shots[1].H / 2f;
                float shift = Mathf.Max(0f, shots[0].H / 2f - la) - Mathf.Max(0f, lb + shots[1].H / 2f - StageH);
                shots[0].Lane = la + shift; shots[1].Lane = lb + shift;
            }
            else
            {
                float gap = BoardView.Tile / 2f + 10f;
                float la = mid.x - gap - shots[0].W / 2f, lb = mid.x + gap + shots[1].W / 2f;
                float shift = Mathf.Max(0f, shots[0].W / 2f - la) - Mathf.Max(0f, lb + shots[1].W / 2f - StageW);
                shots[0].Lane = la + shift; shots[1].Lane = lb + shift;
            }

            // The standard cut-in look (feathered team band with a bright core) runs along each portrait's lane.
            var bands = new List<VisualElement>();
            foreach (var shot in shots)
            {
                var band = new VisualElement { pickingMode = PickingMode.Ignore };
                band.style.position = Position.Absolute;
                band.style.backgroundImage = new StyleBackground(BandTex);
                var bc = BandColor(team);
                band.style.unityBackgroundImageTintColor = bc.WithA(0.9f);
                float thick = (rowLine ? shot.H : shot.W) * 0.28f;
                var core = new VisualElement { pickingMode = PickingMode.Ignore };
                core.style.position = Position.Absolute;
                core.style.backgroundImage = new StyleBackground(BandTex);
                core.style.unityBackgroundImageTintColor = Palette.White.WithA(0.95f);
                if (rowLine) { core.style.left = Space.S0; core.style.right = Space.S0; core.style.top = Length.Percent(46); core.style.height = Size.Track; }
                else { core.style.top = Space.S0; core.style.bottom = Space.S0; core.style.left = Length.Percent(46); core.style.width = Size.Track; }
                band.Add(core);
                if (rowLine) { band.style.left = -ShotOverscan; band.style.width = StageW + ShotOverscan * 2f; band.style.top = shot.Lane - thick / 2f; band.style.height = thick; band.style.rotate = new Rotate(shot.Dir * -3f); }
                else { band.style.top = -ShotOverscan; band.style.height = StageH + ShotOverscan * 2f; band.style.left = shot.Lane - thick / 2f; band.style.width = thick; band.style.rotate = new Rotate(shot.Dir * 3f); }
                band.style.opacity = 0f;
                layer.Insert(1, band); // above the dim, below the portraits
                bands.Add(band);
            }
            App.Audio.Sfx("battle.dash", 0.7f, 0.9f);
            if (delay2 > 0f) App.StartCoroutine(DelayedSfx("battle.dash", delay2, 1.05f));
            var cam = boardHost;
            cam.style.transformOrigin = new TransformOrigin(mid.x - origin.x, mid.y - origin.y);
            float total = CutInSeconds + delay2;
            float camSeed = Random.Range(0f, 100f);
            float camTwist = (Random.value < 0.5f ? -1f : 1f) * 2.6f;
            var flash = new VisualElement { pickingMode = PickingMode.Ignore };
            flash.style.position = Position.Absolute;
            flash.style.left = flash.style.top = flash.style.right = flash.style.bottom = Space.S0;
            flash.style.backgroundColor = Palette.White;
            layer.Add(flash);
            App.StartCoroutine(Tween.Shake(shake, 14f * Fx, Timing.Standard));
            yield return Tween.Run(total, k =>
            {
                float elapsed = k * total;
                float fade = Mathf.Clamp01(Mathf.Min(elapsed / 0.15f, (total - elapsed) / 0.2f));
                dim.style.opacity = 0.5f * fade;
                foreach (var band in bands) band.style.opacity = fade;
                // Board camera: slow push-in on the pincer with a hand-held drift.
                float zoom = 1f + 0.14f * Tween.Back(Mathf.Clamp01(elapsed / 0.35f));
                float sx = (Mathf.PerlinNoise(camSeed, elapsed * 1.6f) - 0.5f) * 18f;
                float sy = (Mathf.PerlinNoise(camSeed + 7f, elapsed * 1.6f) - 0.5f) * 18f;
                cam.style.scale = new Scale(new Vector2(zoom, zoom));
                cam.style.translate = new Translate(sx, sy);
                cam.style.rotate = new Rotate(camTwist * Tween.EaseOut(Mathf.Clamp01(elapsed / 0.35f)) + (Mathf.PerlinNoise(camSeed + 13f, elapsed * 1.1f) - 0.5f) * 1.2f);
                flash.style.opacity = Mathf.Clamp01(1f - elapsed / 0.12f) * 0.7f;
                foreach (var s in shots)
                {
                    float t = Mathf.Max(0f, elapsed - s.Delay) * CutInSpeed;
                    float v = t <= 0f ? -1f : CutInCurve.Evaluate(Mathf.Min(t, 1.993103f));
                    float along = s.Dir * s.Distance * v;
                    float hx = (Mathf.PerlinNoise(s.Seed, elapsed * 2.4f) - 0.5f) * 30f;
                    float hy = (Mathf.PerlinNoise(s.Seed + 3f, elapsed * 2.4f) - 0.5f) * 30f;
                    float cx = s.Horizontal ? StageW / 2f + along : s.Lane;
                    float cy = s.Horizontal ? s.Lane : StageH / 2f + along;
                    float push = 1f + 0.07f * Mathf.Clamp01(t / 1.993103f);
                    s.El.style.left = cx - s.W / 2f + hx;
                    s.El.style.top = cy - s.H / 2f + hy;
                    s.El.style.scale = new Scale(new Vector2(push, push));
                    s.El.style.rotate = new Rotate((Mathf.PerlinNoise(s.Seed + 9f, elapsed * 1.7f) - 0.5f) * 4f);
                }
            });
            layer.RemoveFromHierarchy();
            App.StartCoroutine(SettleCamera(cam));
        }

        private IEnumerator DelayedSfx(string cue, float delay, float pitch)
        {
            yield return Tween.Wait(delay);
            App.Audio.Sfx(cue, 0.7f, pitch);
        }

        private static IEnumerator SettleCamera(VisualElement cam)
        {
            float s0 = cam.resolvedStyle.scale.value.x;
            var t0 = cam.resolvedStyle.translate;
            float r0 = cam.resolvedStyle.rotate.angle.value;
            yield return Tween.Run(Timing.Standard, k =>
            {
                float e = Tween.EaseInOut(k);
                float s = Mathf.Lerp(s0, 1f, e);
                cam.style.scale = new Scale(new Vector2(s, s));
                cam.style.translate = new Translate(t0.x * (1f - e), t0.y * (1f - e));
                cam.style.rotate = new Rotate(r0 * (1f - e));
            });
            cam.style.scale = new Scale(Vector2.one);
            cam.style.translate = new Translate(0, 0);
            cam.style.rotate = new Rotate(0);
        }

        /// <summary>The feed line for a spawned amplifier; the dev <c>devamp</c> endpoint posts it too, so
        /// uicheck's layout audit covers the real text (GG-T110).</summary>
        public const string AmpSpawnedFeed = "Power Amplifier appeared — pincer on its tile";

        public static readonly Color AmpFeedColor = Palette.Gold;

        private IEnumerator AmpSpawnAnim(AmplifierSpawnedEvent e)
        {
            board.SyncAmplifier(e.At);
            App.Audio.Sfx("battle.orb", 0.8f, 0.7f);
            Feed(AmpSpawnedFeed, AmpFeedColor);
            yield return Tween.Pop(board.Amp, 1.6f, 0.3f);
        }

        private IEnumerator AmpMoveAnim(AmplifierMovedEvent e)
        {
            var from = BoardView.CenterOf(e.From); var to = BoardView.CenterOf(e.To);
            App.Audio.Sfx("battle.slide", 0.6f, 1.3f);
            yield return Tween.Run(Timing.Standard, k => board.PlaceAmp(Vector2.Lerp(from, to, Tween.EaseOut(k))));
            board.SyncAmplifier(e.To);
            var by = State.UnitById(e.ByUnitId);
            if (by != null) Feed($"{by.Name} shoved the amplifier", Palette.Vfx.Shove);
        }

        /// <summary>A non-hero entered a pickup's tile and shoved it aside (§13.3.1, GG-T228): it slides like the amplifier.</summary>
        private IEnumerator PickupMoveAnim(PickupMovedEvent e)
        {
            if (!board.PickupViews.TryGetValue(e.PickupEntryId, out var v)) yield break;
            var from = BoardView.PickupPos(e.From); var to = BoardView.PickupPos(e.To);
            v.userData = e.To;
            App.Audio.Sfx("battle.slide", 0.6f, 1.5f);
            yield return Tween.Run(Timing.Standard, k => { var p = Vector2.Lerp(from, to, Tween.EaseOut(k)); v.style.left = p.x; v.style.top = p.y; });
            BoardView.PlacePickup(v, e.To);
        }

        private IEnumerator AmpUsedAnim(AmplifierUsedEvent e)
        {
            var at = BoardView.CenterOf(e.At);
            App.Audio.Sfx("battle.crit", 0.9f);
            Billboard("Power Amplifier ×1.75", null, Palette.Gold);
            ampTally = true;
            yield return Tween.Run(Timing.Standard, k => { board.Lines.Rings.Clear(); board.Lines.Ring(at, 40f + 160f * k, Palette.Vfx.AmpRing.WithA(1f - k), 12f); });
            board.Lines.Rings.Clear(); board.Lines.MarkDirtyRepaint();
            board.SyncAmplifier(null);
            yield return FloatText(e.HeroId, CombatTextKind.Buff, "AMPLIFIED!", 0.2f, 0.6f);
        }
    }
}
