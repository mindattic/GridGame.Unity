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
    /// <summary>Read-only routes: the state and layout dumps, logs, screenshots and recordings, element and style queries, camp and economy reads.</summary>
    public sealed partial class AutomationServer
    {
        private object StateRoutes(string path, System.Collections.Specialized.NameValueCollection q, Action<object> reply, GameApp app)
        {
            switch (path)
            {
                case "ping": return new { ok = true, screen = app.Router.Current?.Id };
                case "state": return StateDump();
                case "perf":
                {
                    // GG-T2101: the frame-time window since the last perf?reset=1 (fps, frame ms percentiles, GC alloc,
                    // texture memory, UI ms, cold start) -- tools/harness.py perf records one battle and one camp scroll.
                    if (q["reset"] == "1") { Dev.PerfStats.Reset(); return new { ok = true }; }
                    var p = Dev.PerfStats.Snapshot();
                    p["screen"] = app.Router.Current?.Id;
                    p["screen_size"] = new[] { Screen.width, Screen.height };
                    return p;
                }
                case "boardaudit":
                    // GG-T97: drawn-token audit totals (see BoardAudit); ?reset=1 zeroes them first.
                    if (q["reset"] == "1") BoardAudit.Reset();
                    if (q["now"] == "1" && app.Router.Current is BattleScreen ab && !ab.Busy) return new { audit = BoardAudit.Snapshot(), now = ab.AuditTokens(true, rendered: true) };
                    return new { audit = BoardAudit.Snapshot() };
                case "logs":
                    lock (logs) { var copy = logs.ToList(); if (q["clear"] == "1") logs.Clear(); return copy; }
                case "record":
                    StartCoroutine(Record(SafeName(q["name"], "rec"), (int)F(q["frames"], 60), Math.Max(1, (int)F(q["every"], 1)), F(q["step"], 1f / 30f), reply));
                    return Pending;
                case "screenshot":
                {
                    var shot = SafeName(q["name"], DateTime.Now.ToString("HHmmss"));
                    CaptureMarker("shot " + shot); // GG-T1005: a recording chapter per harness screenshot
                    StartCoroutine(Screenshot(shot, reply, Mathf.Max(1, (int)F(q["super"], 1))));
                    return Pending;
                }
                case "capture": return CaptureOp(q, reply); // GG-T1005
                case "element":
                {
                    var e = app.Stage.Q(q["name"]);
                    if (e == null) return new { error = "not found" };
                    var tl = DesignPoint(e.worldBound.min); var br = DesignPoint(e.worldBound.max);
                    return new { x = tl.x, y = tl.y, w = br.x - tl.x, h = br.y - tl.y, visible = e.resolvedStyle.display != DisplayStyle.None };
                }
                case "layout": return LayoutAudit();
                case "textinfo":
                {
                    // GG-T305 diagnostics: how each text element containing `text` is laid out and fitted.
                    var needle = q["text"] ?? "";
                    return app.Stage.Query<TextElement>().ToList().Where(t => (TextFit.SourceOf(t) ?? "").Contains(needle)).Take(20).Select(t => new
                    {
                        text = t.text, source = TextFit.SourceOf(t), type = t.GetType().Name, classes = string.Join(" ", t.GetClasses()),
                        parent = t.parent == null ? null : t.parent.GetType().Name + "." + string.Join(".", t.parent.GetClasses()),
                        layout = t.layout.ToString(), content = t.contentRect.ToString(), world = t.worldBound.ToString(),
                        ws = t.resolvedStyle.whiteSpace.ToString(), minW = t.resolvedStyle.minWidth.ToString(), maxW = t.resolvedStyle.maxWidth.ToString(),
                        shrink = t.resolvedStyle.flexShrink, grow = t.resolvedStyle.flexGrow, display = t.resolvedStyle.display.ToString(),
                        need = t.MeasureTextSize(TextFit.SourceOf(t) ?? "", 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).ToString(),
                        needOwn = t.MeasureTextSize(t.text ?? "", 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).ToString(),
                    }).ToList();
                }
                case "texts":
                    return app.Stage.Query<TextElement>().ToList()
                        .Where(t => !string.IsNullOrEmpty(t.text) && t.resolvedStyle.display != DisplayStyle.None && t.resolvedStyle.visibility == Visibility.Visible && t.worldBound.width > 0 && Opaque(t))
                        .Select(TextFit.SourceOf).ToList();
                case "names":
                {
                    var prefix = q["prefix"] ?? "";
                    return app.Stage.Query<VisualElement>().ToList().Where(e => e.name != null && e.name.StartsWith(prefix) && e.worldBound.width > 0).Select(e => e.name).ToList();
                }
                case "coach":
                {
                    // GG-T539: the running lesson and its current coach mark, in design pixels (box, route, destination).
                    var info = Coach.Describe(p => DesignPoint(p));
                    return info ?? (object)new { active = false };
                }
                case "music": return new { track = app.Audio.CurrentTrack };
                case "cues":
                {
                    var list = app.Audio.CueLog.ToList();
                    if (q["clear"] == "1") app.Audio.CueLog.Clear();
                    return list;
                }
                case "style":
                {
                    var e = app.Stage.Q(q["name"]);
                    if (e == null) return new { error = "not found" };
                    var rs = e.resolvedStyle;
                    string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
                    return new { border = Hex(rs.borderTopColor), borderWidth = rs.borderTopWidth, background = Hex(rs.backgroundColor), tint = Hex(rs.unityBackgroundImageTintColor), rs.opacity, rs.width, rs.height, children = e.childCount, text = (e as TextElement)?.text, key = e.viewDataKey };
                }
                case "child_style":
                {
                    // style of the Nth child of a named element (unit frames are the 2nd child of unit-<id>)
                    var e = app.Stage.Q(q["name"]);
                    int idx = (int)F(q["index"], 0);
                    if (e == null || idx >= e.childCount) return new { error = "not found" };
                    var c = e[idx];
                    if (q["child"] != null) { int ci = (int)F(q["child"], 0); if (ci >= c.childCount) return new { error = "not found" }; c = c[ci]; }
                    var rs = c.resolvedStyle;
                    string Hex(Color col) => "#" + ColorUtility.ToHtmlStringRGBA(col);
                    return new { border = Hex(rs.borderTopColor), background = Hex(rs.backgroundColor), children = c.childCount };
                }
                case "gfx":
                    return new
                    {
                        api = SystemInfo.graphicsDeviceType.ToString(), SystemInfo.graphicsDeviceName, SystemInfo.graphicsShaderLevel,
                        colorSpace = QualitySettings.activeColorSpace.ToString(),
                        clear = app.Document.panelSettings.clearColor, camera = Camera.allCamerasCount,
                        uiShader = Shader.Find("Hidden/UIElements/Runtime") != null || Shader.Find("Hidden/UIE-Runtime") != null,
                        panelTarget = app.Document.panelSettings.targetTexture != null,
                    };
                case "shaders":
                    return Resources.FindObjectsOfTypeAll<Shader>().Where(sh => sh.name.Contains("UI") || sh.name.Contains("UIE") || sh.name.Contains("Text"))
                        .Select(sh => sh.name + (sh.isSupported ? "" : " [UNSUPPORTED]")).Distinct().ToList();
                case "panelclear":
                    app.Document.panelSettings.clearColor = q["on"] == "1";
                    return new { ok = true };
                case "camera":
                    foreach (var cam in Camera.allCameras) cam.enabled = q["on"] == "1";
                    return new { ok = true };
                case "camp":
                {
                    // GG-T136 full-loop harness: what each camp system can actually do with this save right now.
                    var c = app.Content; var s = app.Save;
                    if (s == null) return new { error = "no save" };
                    // Only stations the player can use now (GG-T174/T175): a keeper who hasn't joined, or the Blacksmith's
                    // still-closed workshop, offers nothing.
                    bool StationOpen(CraftStation st) => st == CraftStation.Forge ? CampUnlocks.NpcOpen(s, CampNpcs.Blacksmith) && CampUnlocks.SmithWorkshopOpen(s)
                        : CampUnlocks.NpcOpen(s, st == CraftStation.Alchemy ? CampNpcs.Alchemist : CampNpcs.Enchantress);
                    object Craftable(CraftStation st) => Crafting.Recipes(c, st)
                        .Where(r => StationOpen(st))
                        .Where(r => RecipeDiscovery.IsDiscovered(s, r) && Forging.ArmorOpen(c, s, r) && Crafting.CanMake(c, s, r) && Crafting.HasInputs(s, r) && s.Gold >= r.Gold)
                        .Select(r => new { r.Id, r.Result, upgrade = r.BaseItem != null }).ToList();
                    return new
                    {
                        s.Gold,
                        sellable = Shop.Sellable(c, s).Select(i => new { i.Id, type = i.Type.ToString(), price = Shop.SellPrice(i), count = s.Count(i.Id) }).ToList(),
                        forge = Craftable(CraftStation.Forge), alchemy = Craftable(CraftStation.Alchemy), enchant = Craftable(CraftStation.Enchant),
                        worn = Repair.MostWorn(c, s) is var mw && mw != null ? new { row = mw.RowName, mw.Durability, mw.Max } : null,
                        // GG-T544: the rows Repair all would mend (below CampRoutine.RepairBelowFraction) and what it costs.
                        repairDue = Repair.DuePieces(c, s).Select(p => p.RowName).ToList(),
                        repairAllCost = Repair.DuePieces(c, s).Sum(p => Repair.Cost(c, s, p)),
                        smith = CampUnlocks.NpcOpen(s, CampNpcs.Blacksmith), workshop = CampUnlocks.SmithWorkshopOpen(s),
                        weapons = s.Party.Select(s.Hero).Where(h => h != null).ToDictionary(h => h.ClassId, h => h.WeaponId),
                        firstForge = Forging.First(c, s) is var ff && ff != null ? new { recipe = ff.Recipe.Id, weapon = ff.Weapon.Id, hero = ff.Hero.ClassId, ready = ff.Ready } : null,
                        firstArmor = Forging.FirstArmor(c, s) is var fa && fa != null ? new { recipe = fa.Recipe.Id, piece = fa.Weapon.Id, hero = fa.Hero.ClassId, ready = fa.Ready } : null,
                        armor = s.Party.Select(s.Hero).Where(h => h != null).ToDictionary(h => h.ClassId, h => h.Armor.Values.Select(ea => ea.ItemId).ToList()),
                        setRecipes = Crafting.Recipes(c, CraftStation.Forge).Count(r => Forging.IsSetArmor(c, r) && Forging.ArmorOpen(c, s, r)),
                        // GG-T179: the Enchantress (joins after the first armor) and the guided first armor enchant.
                        enchantress = CampUnlocks.NpcOpen(s, CampNpcs.Enchantress),
                        firstEnchant = ArmorEnchanting.First(c, s) is var fe && fe != null ? new { enchant = fe.Enchant.Id, row = fe.Target.RowName, piece = fe.Target.Item.Id, hero = fe.Target.Hero.ClassId, ready = fe.Ready } : null,
                        enchants = ArmorEnchanting.Pieces(c, s).Where(t => t.Piece.Enchant != null).ToDictionary(t => t.RowName, t => t.Piece.Enchant),
                        // GG-T180: onboarding step 7, the guided first skill, and each party hero's battle bar.
                        // GG-T1096: the guided first skill is an ability node on the skill hexagon (q/r = Training.Plan.Node).
                        firstSkill = Training.First(c, s) is var fk && fk != null ? new { skill = fk.Skill.Id, hero = fk.Hero.ClassId, slot = fk.Slot, q = fk.Node.Q, r = fk.Node.R, join = fk.Join } : null,
                        onboarded = s.Flags.Contains(Training.FirstSkillFlag),
                        bars = s.Party.Select(s.Hero).Where(h => h != null).ToDictionary(h => h.ClassId, h => h.Bar.ToList()),
                        // GG-T1096: each roster hero's unspent skill points and owned hex nodes ("q,r", SkillHexProgress.Key).
                        skillPoints = s.Roster.Where(h => h != null).ToDictionary(h => h.ClassId, h => h.SkillPoints),
                        hexNodes = s.Roster.Where(h => h != null).ToDictionary(h => h.ClassId, h => (h.HexNodes ?? new List<string>()).ToList()),
                        bounty = new { hunting = s.Bounty.Taken.ToList(), slain = s.Bounty.Slain.ToList(), claimed = s.Bounty.Claimed.Count, offers = Bounties.Offers(c, s).Select(b => b.Id).ToList() }, // GG-T431
                    };
                }
                case "economy":
                {
                    // GG-T448 playthrough: everything a player weighs at camp in one read — purse, Rested, wounds, wear and
                    // what each gold sink costs right now — so a run can log how the economy feels stage by stage.
                    var c = app.Content; var s = app.Save;
                    if (s == null) return new { error = "no save" };
                    var now = DateTime.UtcNow;
                    return new
                    {
                        s.Gold, diamonds = DiamondExchange.Have(s), bagDiamonds = DiamondExchange.Bag(s), s.HighestClearedStage, // GG-T702: wallet balance
                        rested = RestedPool.Points(s, now), restedMax = RestedPool.Max, restedExempt = RestedPool.Exempt(s),
                        refill = new { gold = Paywall.Cost(PaywallKind.RestedRefill, s), diamonds = RestedPool.DiamondRefillPrice },
                        // GG-T455: Diamonds are exchanged at goldPerDiamond (diamondGold = one Diamond's gold, as before).
                        diamondGold = DiamondExchange.GoldFor(1), goldPerDiamond = DiamondExchange.GoldPerDiamond,
                        diamondsPerDollar = DiamondExchange.DiamondsPerDollar, summonDiamonds = DiamondSummon.Price,
                        luckyDiamonds = Crafting.LuckyDiamondCost,
                        heroes = s.Roster.Select(h => new { h.ClassId, level = Progression.Level(h), hp = h.Hp, max = Progression.Preview(c, h).MaxHp, inParty = s.Party.Contains(h.ClassId), h.WeaponId }).ToList(),
                        wear = Repair.Pieces(c, s).Select(p => new { hero = p.Hero.ClassId, slot = p.Slot.ToString(), item = p.Item.Id, p.Durability, p.Max,
                            cost = Repair.Cost(c, s, p), due = Repair.Due(p.Item, p.Durability, p.Repairs, p.Mastery) }).ToList(),
                        // GG-T538: the save's own shelf (its open stock tiers plus anything bought before), and its progress tier.
                        stock = Shop.Stock(c, s).Select(i => new { i.Id, type = i.Type.ToString(), rarity = i.Rarity.ToString(), price = i.BaseCost, have = s.Count(i.Id), tier = VendorTiers.ShopTier(i) }).ToList(),
                        vendorTier = VendorTiers.ProgressTier(c, s),
                        caravan = c.Caravan.Values.Select(d => new { d.Id, d.Name, level = CaravanUpgrades.Level(s, d.Id), next = CaravanUpgrades.NextLevel(c, s, d.Id) is CaravanLevel n ? new { n.Name, n.Cost } : null }).ToList(),
                        npcs = CampNpcs.All.Where(n => CampUnlocks.NpcOpen(s, n)).ToList(),
                        inventory = s.Inventory,
                        // GG-T448: every party hero's own equipped-slot map (null where empty), so a gear pass can tell an
                        // empty slot from a worn one without guessing from the (durability-only) "wear" list above.
                        equipped = s.Party.Select(s.Hero).Where(h => h != null).ToDictionary(h => h.ClassId, h =>
                            new[] { EquipmentSlot.Weapon }.Concat(BattleEngine.ArmorSlots).Concat(Equipment.JewelrySlots)
                                .ToDictionary(sl => sl.ToString(), sl => sl == EquipmentSlot.Weapon ? h.WeaponId
                                    : Equipment.JewelrySlots.Contains(sl) ? h.Jewelry.TryGetValue(sl, out var j) ? j : null
                                    : h.Armor.TryGetValue(sl, out var a) ? a.ItemId : null)),
                    };
                }
                case "stagebounds":
                {
                    var sb = app.Stage.worldBound;
                    var root = app.Document.rootVisualElement.worldBound;
                    float k = Screen.width / Mathf.Max(1f, root.width);
                    return new { x = sb.x * k, y = sb.y * k, w = sb.width * k, h = sb.height * k, screen = new[] { Screen.width, Screen.height }, design = new[] { GameApp.StageWidth, GameApp.StageHeight }, framed = app.Document.rootVisualElement.Q("stage-frame")?.ClassListContains("hidden") == false, typeFloor = TypeFloor.Current, typeBase = GuiType.Base, dpi = Screen.dpi };
                }
            }
            return Unrouted;
        }

        /// <summary>
        /// Frame recorder for art review: animations step a fixed amount per rendered frame (Tween.FixedStep) while every
        /// Nth frame is written to artifacts/&lt;name&gt;/f###.png, so slow PNG encoding never skips a beat.
        /// </summary>
        private IEnumerator Record(string name, int frames, int every, float step, Action<object> reply)
        {
            var dir = Path.Combine(artifacts, name);
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "f*.png")) File.Delete(old);
            Tween.FixedStep = step;
            int written = 0;
            try
            {
                for (int i = 0; i < frames; i++)
                {
                    yield return new WaitForEndOfFrame();
                    if (i % every != 0) continue;
                    // An unhandled exception here (e.g. disk I/O) would otherwise kill this coroutine
                    // silently — Handle()'s try/catch can't see it, since it's on a different thread
                    // waiting on `done` — leaving the HTTP caller hanging for the full 120s timeout
                    // instead of getting an immediate error. (A yield-return can't live inside a
                    // try/catch, only try/finally, hence this inner try nested one level in.)
                    Texture2D tex = null;
                    Exception captureError = null;
                    try { tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(dir, $"f{written++:000}.png"), tex.EncodeToPNG()); }
                    catch (Exception e) { captureError = e; }
                    finally { if (tex != null) Destroy(tex); }
                    if (captureError != null) { reply(new { error = captureError.Message, dir, written }); yield break; }
                }
            }
            finally { Tween.FixedStep = 0f; }
            reply(new { dir, written });
        }

        /// <param name="superSize">GG-T487: render the shot at this multiple of the window (store screenshots taller than
        /// the desktop allows a window to be).</param>
        private IEnumerator Screenshot(string name, Action<object> reply, int superSize = 1)
        {
            yield return new WaitForEndOfFrame();
            var file = Path.Combine(artifacts, name.EndsWith(".png") ? name : name + ".png");
            Texture2D tex = null;
            // Same reasoning as Record(): without this, a disk I/O failure here would silently kill the
            // coroutine and leave the HTTP caller hanging for the full 120s timeout instead of an immediate error.
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(superSize); File.WriteAllBytes(file, tex.EncodeToPNG()); }
            catch (Exception e) { reply(new { error = e.Message }); yield break; }
            finally { if (tex != null) Destroy(tex); }
            reply(new { file });
        }

        private object StateDump()
        {
            var app = GameApp.I;
            var dump = new Dictionary<string, object>
            {
                ["screen"] = app.Router.Current?.Id,
                ["stack"] = app.Router.Stack.Select(s => s.Id).ToList(),
                ["transitioning"] = app.Router.Busy, // GG-T1857: the router fade is up, a screenshot now is black
                ["screen_size"] = new[] { Screen.width, Screen.height },
                ["fps"] = Mathf.Round(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)),
                ["board_audit"] = BoardAudit.Snapshot(),
                ["profile"] = app.Profile?.Name,
                ["slot"] = app.Save != null ? app.Slot : -1, // GG-T116: which of the profile's slots is loaded (0-based)
            };
            if (app.Save != null)
                dump["save"] = new { app.Save.Gold, app.Save.HighestClearedStage, party = app.Save.Party, inventory = app.Save.Inventory, levels = app.Save.Roster.ToDictionary(h => h.ClassId, Progression.Level), xp = app.Save.Roster.ToDictionary(h => h.ClassId, h => h.TotalXp), hp = app.Save.Roster.ToDictionary(h => h.ClassId, h => h.Hp), crucible = new { app.Save.Crucible.Best, app.Save.Crucible.Runs, app.Save.Crucible.TotalWaves }, onTheRoad = TravelRules.OnTheRoad(app.Save), carriedMana = TravelRules.StartMana(app.Save),
                    // GG-T241: story arc checks (seen beats, the ending flag, Rested, the roster, mercenary contracts).
                    seen = app.Save.SeenDialogues, flags = app.Save.Flags, rested = app.Save.Rested.Points,
                    roster = app.Save.Roster.Select(h => h.ClassId).ToList(), mercs = app.Save.Mercenaries.Contracts.Select(k => k.ClassId).ToList(),
                    // GG-T532: each hero's look ("Female"/"Male", "?" while the pick is pending) and whether the picker is up.
                    variants = app.Save.Roster.ToDictionary(h => h.ClassId, h => h.VariantPending ? "?" : h.Variant.ToString()),
                    variantPicker = VariantPicker.Open,
                    // GG-T1300: the bonds (Trust by hero, cohesion by "a|b" pair) and the heroes a Joined scene has greeted.
                    trust = app.Save.Trust, cohesion = app.Save.Cohesion, met = app.Save.MetHeroes };
            if (TestMode.Active) dump["testmode"] = new { cycles = TestMode.Completed, last = TestMode.LastLine };
            if (app.Router.Current is BattleScreen b && b.State != null)
            {
                var s = b.State;
                dump["battle"] = new
                {
                    phase = s.Phase.ToString(), turn_mode = s.TurnMode.ToString(), wave = s.Wave, clock = s.Clock, busy = b.Busy, selected = b.SelectedHeroId, targeting = b.TargetingSlot, confirming = b.Confirming, record = b.LastRecordPath,
                    mana = ManaDump(s), cutins = b.CutInsShown, coinsShown = b.CoinsShown, tallies = b.TalliesBurst, billboard = b.BillboardLines, paused = b.Paused, pauseOnTop = b.PauseOnTop, beats = b.PlaybackBeats, beatsWhilePaused = b.BeatsWhilePaused, lines = b.LineStrokes, chain = b.ChainCount, amplifier = s.Amplifier.HasValue ? new[] { s.Amplifier.Value.X, s.Amplifier.Value.Y } : null,
                    units = s.Units.Where(u => u.Alive).Select(u => new
                    {
                        u.Id, cls = u.Def.Id, team = u.Team.ToString(), x = u.Pos.X, y = u.Pos.Y, u.W, u.H, u.Hp, u.MaxHp, u.Level,
                        weapon = u.WeaponId, weaponDurability = u.WeaponDurability,
                        armor = u.Gear.ToDictionary(kv => kv.Key.ToString(), kv => new { item = kv.Value.ItemId, durability = kv.Value.Durability }),
                        buffs = u.Buffs.Select(x => x.Def.Id + ":" + x.Remaining).ToList(),
                        acts_in = u.IsHero ? -1 : BattleEngine.ActionsUntilTurn(s, u.Id),
                        acted = u.IsHero && TurnRules.HasActed(s, u), // GG-T308, §2.13: acted this party turn, as the mode says
                        may_act = u.IsHero && TurnRules.MayAct(s, u), // GG-T788: may be picked up or use its bar now, time aside
                        acted_this_round = u.IsHero && TurnRules.MovedThisRound(s, u), // GG-T801 (§2.13.2): used its place this round; false outside UnmovedThisRound
                        bar = u.IsHero ? Enumerable.Range(0, u.Bar.Count).Select(i => (u.Bar[i].AbilityId ?? u.Bar[i].ItemId ?? "-") + "=" + BattleEngine.GetSlotStatus(s, u.Id, i)).ToList() : null,
                    }).ToList(),
                    window_seconds = BattleEngine.TurnTicks(s) / (float)BattleConfig.TicksPerSecond, to_act = BattleEngine.HeroesLeftToAct(s).Select(h => h.Id).ToList(), one_hero_per_turn = TurnRules.OneHeroPerTurn(s), round = s.Round /* GG-T801: UnmovedThisRound's round, 1 in other modes */,
                    heroes_on_timeline = TurnRules.HeroesOnTimeline(s), active_hero_id = s.ActiveHeroId, // GG-T815 (§2.13.3): the hero whose own icon arrived; -1 between turns and outside HeroTimeline
                    hero_icons = s.Timeline.Where(e => e.Kind == TimelineKind.Turn && TurnRules.IsHeroIcon(s, s.UnitById(e.OwnerId))).Select(e => new { e.Id, owner = e.OwnerId, u = Math.Round(BattleEngine.BarU(s, e), 3), in_recovery = BattleEngine.InRecovery(e), active = e.OwnerId == s.ActiveHeroId }).ToList(), // GG-T815: empty outside HeroTimeline
                    too_little_time = BattleEngine.TooLittleTime(s), // GG-T308/GG-T311
                    timeline = s.Timeline.Select(e => new { e.Id, kind = e.Kind.ToString(), owner = e.OwnerId, u = Math.Round(BattleEngine.BarU(s, e), 3), e.AbilityId, in_recovery = BattleEngine.InRecovery(e), recover_seconds = Math.Round(Math.Max(0, e.RecoverTicks) / (double)BattleConfig.TicksPerSecond, 2), entry_u = Math.Round(BattleEngine.EntryBarU(s, e), 3) }).ToList(),
                    pickups = s.Pickups.Select(p => new { p.Id, kind = p.PickupId, x = p.At.X, y = p.At.Y, left = p.ActionsLeft }).ToList(), // GG-T228
                    results = new { s.Results.Coins, s.Results.Xp, s.Results.ActionsTaken },
                };
            }
            return dump;
        }

        /// <summary>Layout audit of the current screen: elements with text that overflow, zero-size visible text, out-of-stage bounds.</summary>
        private object LayoutAudit()
        {
            var stage = GameApp.I.Stage;
            var sb = stage.worldBound;
            var problems = new List<string>();
            int labels = 0;
            foreach (var l in stage.Query<Label>().ToList())
            {
                if (Hidden(l) || string.IsNullOrEmpty(l.text) || l.resolvedStyle.opacity <= 0.01f) continue;
                labels++;
                // A marquee's scrolling copy is as wide as its whole line; only its clipping box is ever drawn (GG-T398).
                bool scrolling = l.parent != null && l.parent.ClassListContains(Marquee.BoxClass);
                var wb = scrolling ? l.parent.worldBound : l.worldBound;
                if (wb.width < 1 || wb.height < 1) { problems.Add($"zero-size label '{Trim(l.text)}'"); continue; }
                if (wb.xMin < sb.xMin - 2 || wb.xMax > sb.xMax + 2) problems.Add($"label off-stage horizontally '{Trim(l.text)}' x={DesignPoint(wb.position).x:0}..{DesignPoint(wb.max).x:0}");
                var size = l.MeasureTextSize(l.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                float k = GameApp.StageWidth / sb.width;
                if (l.resolvedStyle.whiteSpace == WhiteSpace.NoWrap && size.x > l.contentRect.width + 4 && l.contentRect.width > 0 && !l.ClassListContains(Marquee.HostClass)
                    && !(l.parent != null && l.parent.ClassListContains(Marquee.BoxClass)))
                    problems.Add($"text overflows ({size.x:0}>{l.contentRect.width:0}) '{Trim(l.text)}'");
            }
            // Squashed controls: a tab or button drawn well short of its own styled height means its column ran out
            // of room and flex shrank it (the Merchant's tabs and footer, found by GG-T136's full loop).
            foreach (var el in stage.Query<VisualElement>().Where(e => e.ClassListContains("tab") || e.ClassListContains("btn")).ToList())
            {
                if (Hidden(el) || el.worldBound.height < 1) continue;
                // The battle's Party · Lore · Log strip is drawn thin by design (GG-T405, "tabs--thin": 60-px faces), each
                // face inside its own 132-px hit area (GG-T524, ".info-tab"), which POL-C2 measures.
                bool thin = el.parent != null && (el.parent.ClassListContains("tabs--thin") || el.parent.ClassListContains("info-tab"));
                float want = el.ClassListContains("tab") ? (thin ? 56f : 96f) : el.resolvedStyle.minHeight.value;
                float got = el.worldBound.height * (GameApp.StageWidth / sb.width);
                if (want > 0 && got < want * 0.8f)
                    problems.Add($"squashed {(el.ClassListContains("tab") ? "tab" : "button")} '{Trim((el as TextElement)?.text ?? el.name ?? "")}' {got:0}px of {want:0}");
            }
            return new { screen = GameApp.I.Router.Current?.Id, labels, problems, polish = PolishAudit() };
        }

        /// <summary>True when the element or any ancestor is display:none: a page of a tab panel that isn't shown
        /// (the battle's Lore and Log pages, GG-T391) keeps its labels but lays them out at zero size.</summary>
        private static bool Hidden(VisualElement e)
        {
            for (; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None) return true;
            return false;
        }

        private static string Trim(string s) => s.Length > 40 ? s.Substring(0, 40) + "…" : s;
    }
}
#endif
