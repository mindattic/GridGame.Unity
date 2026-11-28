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
        /// <summary>A wave falls. In the Crucible (§22.10, GG-T430) its pay is banked on the spot: the banner says how
        /// much, and the coins burst from the board's centre into the gold counter with the coin chime.</summary>
        private IEnumerator WaveClearedAnim(WaveClearedEvent wc)
        {
            if (wc.Coins <= 0) { ShowBanner("Wave cleared!"); yield return Tween.Wait(Timing.Emphasis); yield break; }
            ShowBanner(InCrucible ? $"Wave {wc.Wave + 1} cleared · +{wc.Coins} G banked" : $"Wave cleared! +{wc.Coins} G");
            var at = new Vector2(BoardView.Tile * 3, BoardView.Tile * 4);
            App.StartCoroutine(FloatAway(board.Float($"+{wc.Coins} G", at + new Vector2(0, -20), Palette.Gold, TypeScale.Base), 1.1f));
            App.StartCoroutine(CoinBurst(at, wc.Coins));
            App.Audio.Sfx("ui.buy", 0.8f, 1.05f);
            yield return Tween.Wait(Timing.Emphasis);
        }

        private IEnumerator LootAnim(LootEvent l)
        {
            if (l.Coins <= 0 && l.Items.Count == 0) { SyncCoins(); yield break; }
            var at = BoardView.CenterOf(board.Units.TryGetValue(l.FromUnitId, out var v) ? v.Tile : new Coord(2, 2));
            if (l.Coins > 0)
            {
                App.StartCoroutine(FloatAway(board.Float($"+{l.Coins} G", at + new Vector2(0, -20), Palette.Gold, TypeScale.Base), 0.9f));
                App.StartCoroutine(CoinBurst(at, l.Coins));
            }
            foreach (var it in l.Items)
            {
                var name = App.Content.TryItem(it.Item)?.Name ?? it.Item;
                App.StartCoroutine(FloatAway(board.Float($"{name} ×{it.Count}", at + new Vector2(0, 40), Palette.Vfx.Loot, TypeScale.Base), 1.1f));
            }
            App.Audio.Sfx("ui.buy", 0.6f);
            yield return Tween.Wait(Timing.Instant);
        }

        /// <summary>A guest ally steps onto the board (§25.11).</summary>
        private IEnumerator GuestAnim(GuestJoinedEvent e)
        {
            var u = State.UnitById(e.UnitId);
            if (u == null) yield break;
            var v = board.Units.TryGetValue(u.Id, out var ex) ? ex : board.Add(u);
            board.Place(v, e.At);
            v.SetHp(u.Hp, u.MaxHp);
            App.Audio.Sfx("battle.pickup", 0.9f);
            Billboard($"{u.Name} joins the fight! Keep them alive to recruit them.", null, Palette.Vfx.Ally);
            v.Root.style.scale = new Scale(Vector2.zero);
            yield return Tween.Run(Timing.Standard, k => v.Root.style.scale = new Scale(Vector2.one * (k < 0.7f ? k / 0.7f * 1.15f : 1.15f - 0.15f * (k - 0.7f) / 0.3f)));
            v.Root.style.scale = new Scale(Vector2.one);
        }

        /// <summary>Remains drop onto the fallen undead's tile with a thud (§14.5).</summary>
        private IEnumerator RemainsAnim(int remainsId, Coord at, bool quiet)
        {
            var r = State.UnitById(remainsId);
            if (r == null) yield break;
            if (!board.Units.TryGetValue(r.Id, out var v)) v = board.Add(r);
            board.Place(v, at);
            v.SetHp(r.Hp, r.MaxHp);
            SyncTimeline(null);
            if (!quiet) App.Audio.Sfx("battle.shatter", 0.5f, 0.6f);
            v.Root.style.translate = new Translate(0, -120);
            yield return Tween.Run(Timing.Quick, k => v.Root.style.translate = new Translate(0, -120f * (1f - k * k)));
            App.StartCoroutine(Tween.Shake(v.Root, 10f * Fx, Timing.Quick));
            Billboard($"{r.Name} — destroy it or it rises!", null, Palette.Info);
        }

        /// <summary>The remains crumble and the undead climbs back out.</summary>
        private IEnumerator RiseAnim(RoseAgainEvent e)
        {
            if (board.Units.TryGetValue(e.RemainsId, out var rv))
            {
                yield return Tween.Run(Timing.Standard, k => { rv.Root.style.opacity = 1f - k; rv.Root.style.scale = new Scale(Vector2.one * (1f - 0.3f * k)); });
                rv.Root.RemoveFromHierarchy(); rv.Dead = true;
            }
            var u = State.UnitById(e.UnitId);
            if (u == null) yield break;
            var v = board.Units.TryGetValue(u.Id, out var existing) ? existing : board.Add(u);
            board.Place(v, e.At);
            v.SetHp(u.Hp, u.MaxHp);
            SyncTimeline(null);
            App.Audio.Sfx("status.debuff", 0.9f, 0.8f);
            Billboard($"{u.Name} rises again!", null, Palette.Vfx.Revive);
            v.Root.style.translate = new Translate(0, 60);
            yield return Tween.Run(Timing.Standard, k => { v.Root.style.translate = new Translate(0, 60f * (1f - k)); v.Root.style.opacity = k; });
        }

        /// <summary>An egg drops onto a free tile beside its layer (§14.8).</summary>
        private IEnumerator EggLaidAnim(EggLaidEvent e)
        {
            var r = State.UnitById(e.EggId);
            if (r == null) yield break;
            if (!board.Units.TryGetValue(r.Id, out var v)) v = board.Add(r);
            board.Place(v, e.At);
            v.SetHp(r.Hp, r.MaxHp);
            SyncTimeline(null);
            App.Audio.Sfx("battle.pickup", 0.6f, 0.8f);
            v.Root.style.translate = new Translate(0, -60);
            yield return Tween.Run(Timing.Quick, k => v.Root.style.translate = new Translate(0, -60f * (1f - k * k)));
            Billboard($"{State.UnitById(e.FromUnitId)?.Name} lays an egg!", null, Palette.Vfx.Egg);
        }

        /// <summary>The egg splits open and the real enemy climbs out, at full strength.</summary>
        private IEnumerator HatchAnim(EggHatchedEvent e)
        {
            if (board.Units.TryGetValue(e.EggId, out var ev))
            {
                yield return Tween.Run(Timing.Standard, k => { ev.Root.style.opacity = 1f - k; ev.Root.style.scale = new Scale(Vector2.one * (1f - 0.3f * k)); });
                ev.Root.RemoveFromHierarchy(); ev.Dead = true;
            }
            var u = State.UnitById(e.UnitId);
            if (u == null) yield break;
            var v = board.Units.TryGetValue(u.Id, out var existing) ? existing : board.Add(u);
            board.Place(v, e.At);
            v.SetHp(u.Hp, u.MaxHp);
            SyncTimeline(null);
            App.Audio.Sfx("battle.crit", 0.8f, 1.1f);
            Billboard($"{u.Name} hatches!", null, Palette.GoldLight);
            v.Root.style.scale = new Scale(Vector2.zero);
            yield return Tween.Run(Timing.Standard, k => v.Root.style.scale = new Scale(Vector2.one * Mathf.Clamp01(k * 1.3f)));
            v.Root.style.scale = new Scale(Vector2.one);
        }

        /// <summary>Mid-wave reinforcements arrive (§14.8): each spawns with an entrance matched to its kind, then — on
        /// the first delayed entry a wave authored a line for — a held narrative beat announces the twist.</summary>
        private IEnumerator ReinforcementsAnim(ReinforcementsArrivedEvent ra)
        {
            for (int si = 0; si < ra.Spawned.Count; si++)
            {
                int id = ra.Spawned[si];
                var u = State.UnitById(id);
                if (u == null || board.Units.ContainsKey(id)) continue;
                var v = board.Add(u);
                if (si < ra.SpawnedAt.Count) board.Place(v, ra.SpawnedAt[si].At); // it may already have walked on this command
                v.SetHp(u.Hp, u.MaxHp);
                if (si >= ra.SpawnedAt.Count || !TryHatchPop(v, u, ra.SpawnedAt[si].At)) // GG-T895: a slime glob hatches
                    App.StartCoroutine(EntranceAnim(v, u.Def));
            }
            SyncTimeline(null);
            if (ra.Spawned.Count > 0) yield return Tween.Wait(Timing.Standard);
            if (!string.IsNullOrEmpty(ra.Line)) yield return SurpriseAnnounce(ra.SpeakerClassId, ra.Line);
        }

        /// <summary>A quick spawn-entrance styled by the class's own tags/brain (§14.8) — no new content fields needed.</summary>
        private IEnumerator EntranceAnim(UnitView v, ClassDef def)
        {
            if (def.Brain == Brain.Burrower)
            {
                App.Audio.Sfx("battle.trap", 0.8f, 0.7f);
                v.Root.style.opacity = 0;
                yield return Tween.Run(Timing.Standard, k => { v.Root.style.opacity = k; v.Root.style.scale = new Scale(Vector2.one * (0.7f + 0.3f * k)); });
                App.StartCoroutine(Tween.Shake(v.Root, 8f * Fx, Timing.Quick));
            }
            else if (def.HasTag("Flying"))
            {
                App.Audio.Sfx("battle.dash", 0.7f, 0.8f);
                v.Root.style.opacity = 0;
                yield return Tween.Run(Timing.Standard, k => { v.Root.style.opacity = k; v.Root.style.translate = new Translate(0, -160f * (1f - k)); }, Tween.EaseOut);
            }
            else if (def.HasTag("Undead"))
            {
                App.Audio.Sfx("status.debuff", 0.7f, 0.7f);
                v.Root.style.opacity = 0;
                yield return Tween.Run(Timing.Emphasis, k => v.Root.style.opacity = k);
            }
            else
            {
                App.Audio.Sfx("battle.shatter", 0.5f, 0.7f);
                v.Root.style.opacity = 0;
                yield return Tween.Run(Timing.Standard, k => { v.Root.style.opacity = k; v.Root.style.translate = new Translate(0, -80f * (1f - k)); }, Tween.EaseOut);
            }
        }

        /// <summary>A held narrative beat (§14.8): the speaker's portrait, name and a line, dismissed by a tap or a timer
        /// (StoryCrawlScreen's dual-exit idiom), reusing the 360×400 bordered-frame convention (CharacterSelectScreen,
        /// LevelUpScreen) without leaving the battle screen.</summary>
        private IEnumerator SurpriseAnnounce(string speakerClassId, string line)
        {
            var def = App.Content.TryClass(speakerClassId);
            var veil = Ui.El("fill veil col center", App.Overlay);
            veil.pickingMode = PickingMode.Position;
            veil.style.backgroundColor = Palette.Vfx.Void.WithA(0.7f);

            var row = Ui.El("row", veil);
            row.style.opacity = 0f;
            var frame = Ui.El(null, row);
            frame.style.width = Size.PortraitW; frame.style.height = Size.PortraitH;
            frame.style.borderTopWidth = frame.style.borderBottomWidth = frame.style.borderLeftWidth = frame.style.borderRightWidth = 4;
            var bandColor = BandColor(Team.Enemy);
            frame.style.borderTopColor = frame.style.borderBottomColor = frame.style.borderLeftColor = frame.style.borderRightColor = bandColor;
            frame.style.backgroundColor = Palette.Ink.WithA(0.6f);
            if (def != null)
            {
                frame.style.backgroundImage = new StyleBackground(Art.PortraitFigure(def.Id));
                frame.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var crop = def.Crop;
                frame.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, Length.Percent(crop?.X ?? 50f));
                frame.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, Length.Percent(crop?.Y ?? 0f));
            }

            var info = Ui.El("col", row);
            info.style.marginLeft = Space.S24; info.style.width = Fit.W(36f);
            var nm = Ui.Text(def?.Name ?? "???", "h1 shadow", info);
            Fonts.UseDisplay(nm);
            nm.style.color = bandColor;
            var body = Ui.Text(line, "h2 shadow", info);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginTop = Space.S16;

            bool done = false;
            void Finish() { if (done) return; done = true; }
            veil.RegisterCallback<ClickEvent>(_ => Finish());

            App.Audio.Sfx("battle.shatter", 0.5f);
            yield return Tween.Run(Timing.Standard, k =>
            {
                row.style.opacity = Mathf.Clamp01(k * 2.2f);
                row.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(1.15f, 1f, Tween.Back(k)));
            });
            row.style.opacity = 1f; row.style.scale = new Scale(Vector2.one);

            float t = 0f;
            while (!done && t < 2.6f) { t += Tween.Dt; yield return null; }

            yield return Tween.Run(Timing.Quick, k => row.style.opacity = 1f - k);
            veil.RemoveFromHierarchy();
        }

        private IEnumerator BossAnim(BossPhaseEvent bp)
        {
            App.Audio.Sfx("battle.explosion");
            App.StartCoroutine(Tween.Shake(shake, 20f * Fx, Timing.Emphasis));
            yield return FloatText(bp.UnitId, CombatTextKind.Callout, string.IsNullOrEmpty(bp.Text) ? "ENRAGED!" : bp.Text, 0.6f, 0.7f);
        }

        private IEnumerator WaveAnim(WaveStartedEvent wv)
        {
            for (int si = 0; si < wv.Spawned.Count; si++)
            {
                int id = wv.Spawned[si];
                var u = State.UnitById(id);
                if (u == null || board.Units.ContainsKey(id)) continue;
                var v = board.Add(u);
                if (si < wv.SpawnedAt.Count) board.Place(v, wv.SpawnedAt[si].At); // it may already have walked on this command
                v.SetHp(u.Hp, u.MaxHp);
                v.Root.style.opacity = 0;
                App.StartCoroutine(Tween.Run(Timing.Standard, k => { v.Root.style.opacity = k; v.Root.style.translate = new Translate(0, -80 * (1 - k)); }, Tween.EaseOut));
            }
            SyncTimeline(null);
            BossWaveMusic(wv);
            if (InCrucible)
            {
                // The Crucible's waves never run out (§22.10): no "N/total", and a rising sting marks each one.
                int elites = Crucible.WaveElites(wv.Wave);
                ShowBanner((wv.Wave == 0 ? "The Crucible: wave 1" : $"Crucible wave {wv.Wave + 1} · Lv {Crucible.WaveLevel(State.Stage.LevelOverride ?? 1, wv.Wave)}")
                           + (elites > 0 ? $" · {elites} elite{(elites == 1 ? "" : "s")}" : ""));
                App.Audio.Sfx("battle.crucible_wave", 1f, 1f + Mathf.Min(0.5f, wv.Wave * 0.04f));
                var f = board.Float($"WAVE {wv.Wave + 1}", new Vector2(BoardView.Tile * 3, BoardView.Tile * 4), Palette.Vfx.Wave, TypeScale.Base);
                App.StartCoroutine(FloatAway(f, 1.0f));
                yield return Tween.Wait(Timing.Dramatic);
            }
            else if (wv.Wave > 0 || wv.TotalWaves > 1)
            {
                ShowBanner($"Wave {wv.Wave + 1}/{wv.TotalWaves}");
                App.Audio.Sfx("battle.wave", 0.8f); // GG-T465: a campaign wave had only the banner
                var f = board.Float($"WAVE {wv.Wave + 1}", new Vector2(BoardView.Tile * 3, BoardView.Tile * 4), Palette.GoldLight, TypeScale.Base);
                App.StartCoroutine(FloatAway(f, 1.0f));
                yield return Tween.Wait(Timing.Dramatic);
            }
        }

        /// <summary>GG-T1091 (§2.13.2): a new round under Unmoved this round. Every hero is free again: the banner says so,
        /// a rising wave sting plays, every hero view drops its acted check (with a small pop unless Reduce Motion), and
        /// the timeline's round readout refreshes.</summary>
        private IEnumerator RoundAnim(RoundStartedEvent rs)
        {
            ShowBanner($"Round {rs.Round} · every hero may move again");
            Feed($"Round {rs.Round}", Palette.GoldLight);
            App.Audio.Sfx("battle.wave", 0.6f, 1.25f);
            foreach (var u in State.Units)
            {
                if (!u.IsHero || !board.Units.TryGetValue(u.Id, out var v) || v.Dead) continue;
                v.SetActed(false);
                if (!ReduceMotion) App.StartCoroutine(Tween.Pop(v.Root, 1.08f, Timing.Quick));
            }
            SyncRound();
            yield return Tween.Wait(ReduceMotion ? Timing.Quick : Timing.Standard);
        }

        private IEnumerator ReviveAnim(RevivedEvent rv)
        {
            var u = State.UnitById(rv.UnitId);
            if (u == null) yield break;
            if (!board.Units.TryGetValue(u.Id, out var v)) v = board.Add(u);
            if (v.Dead) { board.UnitLayer.Add(v.Root); v.Dead = false; }
            board.Place(v, rv.At);
            v.SetHp(rv.Hp, u.MaxHp);
            App.Audio.Sfx("battle.heal");
            yield return Tween.Run(Timing.Emphasis, k => { v.Root.style.opacity = k; v.Root.style.scale = new Scale(Vector2.one * (0.6f + 0.4f * k)); });
            yield return FloatText(u.Id, CombatTextKind.Heal, "Revived!", 0.2f, 0.6f);
        }

        private IEnumerator EndBattle()
        {
            ended = true;
            CaptureUnity.Raise("battle-end", new CaptureData
            {
                ["stage"] = State.Stage.Id,
                ["result"] = lastEnded == null ? "unknown" : lastEnded.Victory ? "victory" : lastEnded.Escaped ? "escaped" : lastEnded.Withdrawn == true ? "withdrawn" : "defeat",
            });
            SaveRecord();
            RecordEncounterTelemetry(); // GG-T59: one JSONL encounter record, flushed, before the session closes
            TelemetryUnity.Finish(new
            {
                stage = State.Stage.Id,
                result = lastEnded == null ? "unknown" : lastEnded.Victory ? "victory" : lastEnded.Escaped ? "escaped" : lastEnded.Withdrawn == true ? "withdrawn" : "defeat",
                waveReached = State.Wave + 1,
                actions = State.Results.ActionsTaken,
                ticks = State.Results.TicksElapsed,
                coins = State.Results.Coins,
                xp = State.Results.Xp,
                heroesStanding = State.Units.Count(u => u.IsHero && u.Alive),
            });
            yield return Tween.Wait(Timing.Dramatic);
            if (practice)
            {
                EndPractice();
                yield break;
            }
            if (!CommitsToSave || App.Save == null)
            {
                LeaveBattle(); // Router.Back() alone was swallowed by OnBack (it opens the pause menu)
                yield break;
            }
            var report = BattleFactory.Commit(App.Content, App.Save, State);
            App.Persist();
            PostBattleScreen.RouteFrom(report, State.Stage.Id);
        }
    }
}
