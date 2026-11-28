#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App.Automation
{
    /// <summary>Battle routes: start a battle, autoplay, speed, turn mode, commands, the bot's suggestion, combo and banner reads.</summary>
    public sealed partial class AutomationServer
    {
        private object BattleRoutes(string path, System.Collections.Specialized.NameValueCollection q, Action<object> reply, GameApp app)
        {
            switch (path)
            {
                case "battle":
                {
                    if (app.Save == null) { app.Save = Progression.NewGame(app.Content, "Bot"); app.Slot = 2; }
                    var stage = q["stage"] ?? app.Content.CampaignStages()[0].Id;
                    app.Router.Push(new BattleScreen(stage));
                    return new { ok = true, stage };
                }
                case "randombattle":
                {
                    // Battle-clip demo: picks a random real stage (any theme, main or postgame) and a
                    // random wave from it — one enemy family grouping, per the house "never mix families
                    // in a wave" convention — as a single-wave skirmish. Registers a throwaway synthetic
                    // StageDef in the in-memory ContentDb (never touches the JSON on disk) so the normal
                    // BattleFactory.Build path — party, LevelOverride-aware recommended level, everything —
                    // works exactly like a real stage. Launch with -capture-config pointing at
                    // capture.json (its "enabled" still true) to turn each one into its own short
                    // clip via its battle-start/battle-end rules — MindAttic.Media.Capture only reads that
                    // file when told to with -capture-config, never just because it sits in
                    // StreamingAssets (see tools/harness.py's battle_clips()).
                    // Earlier calls' throwaway stages must not re-enter the pool — a prior run of this
                    // same endpoint, left running, fed itself its own synthetic stages as "random real"
                    // sources within a few dozen calls, compounding (skirmish of a skirmish of a...) and
                    // collapsing variety toward whatever got picked early. Prune them before picking.
                    // Safe while the last skirmish's level-up/results screens are still up (GG-T127): those read the
                    // stage's name and theme from the BattleReport, and LevelUpScreen stops its fanfare when popped.
                    const string SynthPrefix = "skirmish-";
                    foreach (var oldId in app.Content.Stages.Keys.Where(k => k.StartsWith(SynthPrefix)).ToList())
                        app.Content.Stages.Remove(oldId);
                    var realStages = app.Content.Stages.Values.Where(s => !s.IsTest && s.Waves.Count > 0).ToList();
                    if (realStages.Count == 0) return new { error = "no stages available" };
                    // stage=/wave= (GG-T241) pin the pick, e.g. a biome finale's boss wave.
                    var src = q["stage"] != null ? app.Content.TryStage(q["stage"]) : realStages[UnityEngine.Random.Range(0, realStages.Count)];
                    if (src == null || src.Waves.Count == 0) return new { error = "unknown stage" };
                    var wave = q["wave"] != null ? src.Waves[Mathf.Clamp((int)F(q["wave"]), 0, src.Waves.Count - 1)] : src.Waves[UnityEngine.Random.Range(0, src.Waves.Count)];
                    int srcIndex = app.Content.CampaignStages().FindIndex(x => x.Id == src.Id);
                    int level = src.LevelOverride ?? (srcIndex >= 0 ? Progression.RecommendedLevel(srcIndex) : 5);
                    var synthId = SynthPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
                    app.Content.Stages[synthId] = new StageDef
                    {
                        // keepname=1 (GG-T487 store screenshots): the source stage's own name in the HUD, no "Skirmish".
                        Id = synthId, Name = q["keepname"] == "1" ? src.Name : $"{src.Name} Skirmish", Theme = src.Theme, Background = src.Background,
                        Music = src.Music, IsTest = false, LevelOverride = level, Waves = new List<WaveDef> { wave },
                    };
                    if (app.Save == null) { app.Save = Progression.NewGame(app.Content, "Bot"); app.Slot = 2; }
                    // ResetTo, not Push: this is meant to be called over and over for "tons of clips" —
                    // Push would grow the screen stack by two (battle, then its postbattle) every time.
                    app.Router.ResetTo(new BattleScreen(synthId));
                    return new { ok = true, stage = synthId, source = src.Id, level, enemies = wave.Enemies.Select(e => e.Class).ToList() };
                }
                case "tilt": // GG-T682: stand in for the accelerometer (gravity x,y,z in device axes); off=1 hands it back
                    StageTilt.Simulated = q["off"] == "1" ? (Vector3?)null : new Vector3(F(q["x"], 0), F(q["y"], -1), F(q["z"], 0));
                    return new { ok = true, simulated = StageTilt.Simulated.HasValue, tilt = new[] { StageTilt.Last.x, StageTilt.Last.y } };
                case "autoplay":
                    if (app.Router.Current is BattleScreen bs) { bs.AutoPlay = q["on"] != "0"; return new { ok = true }; }
                    return new { error = "not in battle" };
                case "speed":
                    Tween.Speed = F(q["x"], 1);
                    app.Settings.BattleSpeed = Tween.Speed;
                    return new { ok = true, Tween.Speed };
                case "turnmode": // GG-T775: mode=<TurnMode name> sets the profile's mode in memory (never saved); no mode reads it back
                {
                    if (!string.IsNullOrEmpty(q["mode"]))
                    {
                        if (!TurnModeSetting.TryParseBuilt(q["mode"], out var tm, out var tmError)) return new { error = tmError };
                        app.Settings.TurnMode = tm;
                    }
                    return new { ok = true, turn_mode = app.Settings.TurnMode.ToString(), built = TurnModes.Available.Select(m => m.ToString()).ToList(), save_lock = app.Save?.TurnModeLock?.ToString() };
                }
                case "cmd":
                {
                    if (!(app.Router.Current is BattleScreen b)) return new { error = "not in battle" };
                    if (!b.CanCommand()) return new { error = "busy" };
                    BattleCommand cmd = null;
                    switch (q["type"])
                    {
                        case "shield": cmd = new ShieldCommand(); break;
                        case "move": cmd = new MoveCommand { HeroId = (int)F(q["hero"]), Path = ParseTiles(q["path"]) }; break;
                        case "use": cmd = new UseSlotCommand { HeroId = (int)F(q["hero"]), Slot = (int)F(q["slot"]), Target = new Coord((int)F(q["x"]), (int)F(q["y"])) }; break;
                    }
                    if (cmd == null) return new { error = "bad type" };
                    b.Commit(cmd);
                    StartCoroutine(WaitIdle(0, reply));
                    return Pending;
                }
                case "suggest":
                {
                    if (!(app.Router.Current is BattleScreen sb2)) return new { error = "not in battle" };
                    var cmd = new GridGame.Core.Bot.HeroBot(5).BestMove(sb2.State);
                    if (cmd == null) return new { error = "no move" };
                    var pv = BattleEngine.PreviewMove(sb2.State, cmd.HeroId, cmd.Path);
                    var hero = sb2.State.UnitById(cmd.HeroId);
                    return new { hero = cmd.HeroId, from = new[] { hero.Pos.X, hero.Pos.Y }, path = cmd.Path.Select(c => new[] { c.X, c.Y }).ToList(), pincers = pv.Pincers.Count,
                                 moved = pv.Positions.Where(kv => kv.Key != cmd.HeroId && sb2.State.UnitById(kv.Key).Pos != kv.Value).Select(kv => new { id = kv.Key, x = kv.Value.X, y = kv.Value.Y }).ToList() };
                }
                case "combo": // GG-T852: the combo meter (shown and model), best combo and top supporter
                    if (app.Router.Current is BattleScreen cbs) return cbs.ComboReport();
                    return new { error = "not in battle" };
                case "banner":
                {
                    // GG-T358: "shown" is every banner/billboard line this battle has put up, so an opening announcement
                    // the billboard already dropped is still readable. "hint" is the hint or tip in the description panel (GG-T1824).
                    var bnr = app.Router.Current as BattleScreen;
                    return new { text = bnr?.BannerText, hint = bnr?.AdviceText, shown = bnr?.ShownLines?.ToList() };
                }
            }
            return Unrouted;
        }

        /// <summary>The party mana pool (§3.1.0): its points, its capacity, and the living heroes' costed spells it can pay for now.</summary>
        private static object ManaDump(BattleState s) => new
        {
            points = s.PartyMana, capacity = BattleConfig.PartyManaCapacity,
            affordable = s.Units.Where(u => u.IsHero && u.Alive).SelectMany(u => Enumerable.Range(0, u.Bar.Count).Where(i => s.SpellPrice(u, i) > 0 && s.SpellReady(u, i)).Select(i => new { hero = u.Id, slot = i, ability = u.Bar[i].AbilityId, price = s.SpellPrice(u, i) })).ToList()
        };
    }
}
#endif
