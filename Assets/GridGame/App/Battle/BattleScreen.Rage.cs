using System.Collections;
using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// Minotaur rage on the board (§14.18, GG-T898; owner: "a minotaur goes rage, steam comes out of their 'ears' from
    /// the sides of the portrait and a steam train sound plays while they charge linearly"). When a bull takes Raging
    /// its bust slides in on the red band tagged RAGE with steam jets puffing from both sides of the portrait (its ears)
    /// while the portrait flushes red; on the board its token vents steam from both sides of its head and pulses red,
    /// and a RAGE callout pops. A raging Bull Rush (RampagedEvent) blows the steam-train whistle, then every step of
    /// the run is a fast dash that chugs, kicks up a dust trail behind the bull and shakes the screen. Reduce Motion
    /// keeps the sounds, a brief red tint and the callout, and plays the run as ordinary steps (no card, steam, dust
    /// or shake).
    /// </summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color RageRed = new Color(0.88f, 0.27f, 0.17f);
        private static readonly Color RageFlush = new Color(1.6f, 0.38f, 0.3f);
        private static readonly Color SteamWhite = new Color(0.94f, 0.95f, 0.97f);
        private static readonly Color RampageDust = new Color(0.74f, 0.64f, 0.5f);

        /// <summary>Placeholder cue (docs/SFX.md): a steam-train whistle and chugging. Stand-ins play under it.</summary>
        private const string SteamTrainCue = "minotaur.steamtrain";

        // The raging run now playing: whose steps dash, how many are left, and the dust it has kicked up.
        private int rampageUnit = -1, rampageStepsLeft;
        private readonly List<(Vector2 at, float born)> rampageDust = new List<(Vector2, float)>();
        private float rampageClock;

        /// <summary>A unit takes Raging: the RAGE card with steam from its ears, the token venting and flushing red.</summary>
        private IEnumerator RageAnim(BuffAppliedEvent ba)
        {
            var u = State.UnitById(ba.UnitId);
            var def = App.Content.TryBuff(ba.BuffId);
            if (u == null || def == null || !board.Units.TryGetValue(u.Id, out var v) || v.Dead) { RefreshUnit(ba.UnitId); yield break; }
            var at = BoardView.CenterOf(v.Tile, v.W, v.H);
            App.Audio.Sfx(SteamTrainCue, 0.9f);
            App.Audio.Sfx("battle.warcry", 0.8f, 0.7f);  // stand-in: the bull's bellow...
            App.Audio.Sfx("element.steam", 1f, 0.75f);   // ...and the hiss of steam (until minotaur.steamtrain has a file)
            Text.Show(at, CombatTextKind.Callout, "RAGE!", 0.6f, RageRed);
            var priorTint = v.Art.style.unityBackgroundImageTintColor;
            if (ReduceMotion)
            {
                v.Art.style.unityBackgroundImageTintColor = Color.Lerp(Palette.White, RageRed, 0.5f);
                yield return Tween.Wait(Timing.Standard);
                v.Art.style.unityBackgroundImageTintColor = priorTint;
            }
            else
            {
                App.StartCoroutine(Tween.Shake(shake, 6f, Timing.Quick));
                var vent = TokenVent(v, at, Timing.Emphasis + Timing.Standard);
                yield return CutInsEnabled ? Tween.All(App, vent, RageSlide(u)) : vent;
                v.Art.style.unityBackgroundImageTintColor = priorTint;
            }
            RefreshUnit(u.Id);
            App.StartCoroutine(Tween.Pop(v.Buffs, 1.6f, 0.28f));
            var aura = AuraFor(u);
            if (aura.a > 0f && u.Id != SelectedHeroId && !ReduceMotion) v.SetGlow(aura, 0.45f);
            Text.Status(at, true, def.Name, Color.Lerp(RageRed, Palette.White, 0.15f));
            yield return Tween.Wait(Timing.Quick);
        }

        /// <summary>The token snorts steam from both sides of its head while its art pulses red.</summary>
        private IEnumerator TokenVent(UnitView v, Vector2 center, float seconds)
        {
            float tile = BoardView.Tile;
            var ears = new[]
            {
                (center + new Vector2(-tile * 0.36f * v.W, -tile * 0.22f * v.H), new Vector2(-1f, -0.55f)),
                (center + new Vector2(tile * 0.36f * v.W, -tile * 0.22f * v.H), new Vector2(1f, -0.55f)),
            };
            const int puffs = 5;
            yield return Tween.Run(seconds, k =>
            {
                float pulse = 0.5f - 0.5f * Mathf.Cos(k * Mathf.PI * 6f); // three red throbs
                v.Art.style.unityBackgroundImageTintColor = Color.Lerp(Palette.White, RageFlush, pulse * (1f - k * 0.3f));
                board.Lines.ClearLines();
                foreach (var (ear, dir) in ears)
                    for (int i = 0; i < puffs; i++)
                    {
                        float p = Mathf.Repeat(k * 3.2f + i / (float)puffs, 1f);
                        if (k * 3.2f < i / (float)puffs) continue; // the jets start from the ear, not mid-air
                        var pos = ear + dir.normalized * (tile * 0.55f * Ease.Out(p)) + Vector2.up * (-tile * 0.25f * p);
                        float fade = (1f - p) * (k > 0.85f ? (1f - k) / 0.15f : 1f);
                        board.Lines.Dot(pos, 6f + 14f * p, SteamWhite.WithA(0.8f * fade));
                    }
            });
            board.Lines.ClearLines();
        }

        /// <summary>The RAGE card: the bull's bust slides in on its red band, steam jets from both sides of the portrait
        /// (its ears) and the portrait flushes red in time with them, then it slides back out.</summary>
        private IEnumerator RageSlide(Unit foe)
        {
            CutInsShown++;
            const float h = 440f;
            var card = ActionCard(foe, h, Team.Enemy, "RAGE!", new Color(1f, 0.55f, 0.42f), out var portrait);
            float w = card.style.width.value.value;
            float y = BoardTop + 470f;
            float rest = GameApp.StageWidth - w * 0.8f, off = GameApp.StageWidth + w * 0.75f;
            void Put(float x) { card.style.left = x - w / 2f; card.style.top = y - h / 2f; }
            Put(off);
            // Steam puffs, two jets at the sides of the bust window a third of the way down (where the ears are).
            const int perSide = 6;
            var puffs = new List<(VisualElement e, float side, int i)>();
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < perSide; i++)
                {
                    var p = new VisualElement { name = "rage-steam", pickingMode = PickingMode.Ignore };
                    p.style.position = Position.Absolute;
                    p.style.backgroundColor = SteamWhite;
                    p.style.opacity = 0f;
                    p.style.borderTopLeftRadius = p.style.borderTopRightRadius = p.style.borderBottomLeftRadius = p.style.borderBottomRightRadius = Radius.Pill;
                    card.Add(p);
                    puffs.Add((p, s == 0 ? -1f : 1f, i));
                }
            void Steam(float t, float strength)
            {
                foreach (var (e, side, i) in puffs)
                {
                    float p = Mathf.Repeat(t * 2.4f + i / (float)perSide, 1f);
                    bool born = t * 2.4f >= i / (float)perSide;
                    float size = 18f + 70f * Ease.Out(p);
                    float ex = side < 0 ? w * 0.16f : w * 0.84f, ey = h * 0.33f;
                    float x = ex + side * (w * 0.42f * Ease.Out(p)), yy = ey - h * 0.16f * p;
                    e.style.left = x - size / 2f; e.style.top = yy - size / 2f;
                    e.style.width = size; e.style.height = size;
                    e.style.opacity = born ? 0.85f * (1f - p) * strength : 0f;
                }
            }
            var veil = CutInVeil(0.3f);
            card.BringToFront();
            App.Audio.Sfx("battle.dash", 0.7f, 0.75f);
            const float hold = 0.7f;
            App.Audio.DuckFor(Timing.Quick * 2.3f + hold);
            var baseTint = new Color(1f, 0.88f, 0.88f);
            float clock = 0f;
            yield return Tween.Run(Timing.Quick * 1.3f, k =>
            {
                Put(Mathf.LerpUnclamped(off, rest, Tween.Back(k)));
                card.style.opacity = Mathf.Min(1f, k * 3f);
                portrait.style.unityBackgroundImageTintColor = Color.Lerp(new Color(3f, 2.2f, 2.2f), baseTint, k);
            });
            App.StartCoroutine(Tween.Shake(card, 12f, Timing.Quick));
            App.Audio.Sfx("element.steam", 0.9f, 0.6f);
            yield return Tween.Run(hold, k =>
            {
                clock = k * hold;
                Steam(clock, 1f);
                float pulse = 0.5f - 0.5f * Mathf.Cos(k * Mathf.PI * 4f);
                portrait.style.unityBackgroundImageTintColor = Color.Lerp(baseTint, RageFlush, pulse);
                card.style.scale = new Scale(Vector2.one * (1f + 0.04f * k + 0.015f * pulse));
            });
            App.StartCoroutine(LiftVeil(veil));
            yield return Tween.Run(Timing.Quick, k =>
            {
                Steam(clock + k * Timing.Quick, 1f - k);
                Put(Mathf.Lerp(rest, off, k * k));
                card.style.opacity = 1f - k;
            });
            card.RemoveFromHierarchy();
        }

        /// <summary>A raging Bull Rush starts: the whistle blows and the bull's next steps along the path become the dash.</summary>
        private IEnumerator RampageAnim(RampagedEvent e)
        {
            rampageUnit = e.UnitId;
            rampageStepsLeft = e.Path.Count;
            rampageDust.Clear();
            rampageClock = 0f;
            App.Audio.Sfx(SteamTrainCue, 1f);
            App.Audio.Sfx("element.steam", 1f, 1.35f); // stand-in whistle until minotaur.steamtrain has a file
            if (!board.Units.TryGetValue(e.UnitId, out var v)) yield break;
            var at = BoardView.CenterOf(v.Tile, v.W, v.H);
            if (ReduceMotion) yield break;
            // A last snort before it goes: steam from both sides and a lurch back.
            App.StartCoroutine(Tween.Shake(shake, 5f, Timing.Quick));
            yield return TokenVent(v, at, Timing.Standard);
            v.Art.style.unityBackgroundImageTintColor = Palette.White;
        }

        /// <summary>One step of the raging run: a quick dash that chugs, trails dust and jolts the screen.</summary>
        private IEnumerator RampageStep(SteppedEvent s)
        {
            rampageStepsLeft--;
            bool last = rampageStepsLeft <= 0;
            if (last) rampageUnit = -1;
            if (!board.Units.TryGetValue(s.UnitId, out var v)) yield break;
            App.Audio.Sfx("battle.dash", 0.7f, 0.6f);     // the chug (stand-in under minotaur.steamtrain)
            App.Audio.Sfx("element.steam", 0.45f, 0.9f);  // ...and its chuff
            if (ReduceMotion) { yield return StepAnim(s.UnitId, s.To, 0.16f, "battle.slide"); yield break; }
            Vector2 from = BoardView.PosOf(v.Tile), to = BoardView.PosOf(s.To);
            var half = new Vector2(BoardView.Tile * v.W, BoardView.Tile * v.H) * 0.5f;
            var back = (from - to).normalized;
            v.Tile = s.To;
            App.StartCoroutine(Tween.Shake(shake, 7f, Timing.Quick));
            const float seconds = 0.11f;
            float t0 = rampageClock;
            yield return Tween.Run(seconds, k =>
            {
                rampageClock = t0 + k * seconds;
                var p = Vector2.Lerp(from, to, k);
                v.Root.style.left = p.x; v.Root.style.top = p.y;
                // Dust kicked out behind the hooves, a puff each frame-ish, drifting back and up.
                if (rampageDust.Count == 0 || rampageClock - rampageDust[rampageDust.Count - 1].born > 0.025f)
                {
                    var jitter = new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(0.1f, 0.4f)) * BoardView.Tile;
                    rampageDust.Add((p + half + back * (BoardView.Tile * 0.35f) + jitter, rampageClock));
                }
                DrawDust(1f);
            });
            board.Place(v, s.To);
            if (last)
            {
                App.Audio.Sfx("battle.slide", 0.8f, 0.6f);
                float t1 = rampageClock;
                yield return Tween.Run(Timing.Standard, k => { rampageClock = t1 + k * Timing.Standard; DrawDust(1f - k); });
                board.Lines.ClearLines();
                rampageDust.Clear();
            }
        }

        private void DrawDust(float alpha)
        {
            board.Lines.ClearLines();
            const float life = 0.55f;
            foreach (var (pos, born) in rampageDust)
            {
                float age = (rampageClock - born) / life;
                if (age < 0f || age >= 1f) continue;
                board.Lines.Dot(pos + Vector2.up * (-14f * age), 8f + 16f * age, RampageDust.WithA(0.65f * (1f - age) * alpha));
            }
        }

        /// <summary>Drops any raging run left half-played (playback ending or a new action).</summary>
        private void ResetRampage()
        {
            rampageUnit = -1;
            rampageStepsLeft = 0;
            rampageDust.Clear();
        }
    }
}
