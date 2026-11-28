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
    /// <summary>Test-only cheat routes: scenarios, gear, items, levels, stages, bonds, and in-battle dev and demo hooks.</summary>
    public sealed partial class AutomationServer
    {
        private object DevRoutes(string path, System.Collections.Specialized.NameValueCollection q, Action<object> reply, GameApp app)
        {
            switch (path)
            {
                case "scenario":
                {
                    // GG-T1801: drop straight into a "show me X" moment on a generated mid-game save (slot -1, never
                    // written). preset=first-boss (tools/scenarios/<name>.json, a path, or a bare target) or spec=<JSON>.
                    try
                    {
                        var def = q["spec"] != null ? Dev.ScenarioDef.Parse(q["spec"]) : Dev.ScenarioLander.Load(q["preset"] ?? q["target"]);
                        var r = Dev.ScenarioLander.Land(def);
                        return r.Summary(app.Content);
                    }
                    catch (Exception e) when (e is ArgumentException || e is JsonException || e is IOException)
                    {
                        return new { error = e.Message };
                    }
                }
                case "devequipall":
                {
                    // Test-only (GG-T39): a fresh save starts every hero bare-handed (weapons are bought
                    // at the Blacksmith), so harness checks of weapon-durability UI need something equipped
                    // first. Mirrors Meta.Equipment.Equip (grants + equips) across the whole roster.
                    if (app.Save == null) return new { error = "no save" };
                    // Equips into the item's own slot, so armor set pieces (GG-T182) work too; wear=0.5: battle-worn to that
                    // fraction of max (GG-T175's Blacksmith onboarding check).
                    int equipped = Dev.DevCheats.EquipAll(app.Content, app.Save, q["item"] ?? "eq_sword_iron", q["wear"] != null ? F(q["wear"]) : (float?)null);
                    return new { ok = equipped > 0, equipped };
                }
                case "devwear":
                {
                    // Test-only (GG-T544): wears one hero's equipped piece to a fraction of its ceiling (slot=Weapon by
                    // default, or an armor slot), so the Repair tab can show pieces above and below the repair threshold.
                    if (app.Save == null) return new { error = "no save" };
                    var h = app.Save.Hero(q["hero"]);
                    if (h == null) return new { error = $"no hero '{q["hero"]}'" };
                    var p = Repair.Pieces(app.Content, app.Save).FirstOrDefault(x => x.Hero == h && x.Slot.ToString() == (q["slot"] ?? "Weapon"));
                    if (p == null) return new { error = "nothing that wears in that slot" };
                    int d = Math.Max(1, (int)(p.Max * F(q["wear"], 0.5f)));
                    if (p.Slot == EquipmentSlot.Weapon) h.WeaponDurability = d; else h.Armor[p.Slot].Durability = d;
                    return new { ok = true, durability = d, p.Max };
                }
                case "devgive":
                {
                    // Test-only: grants bag items for harness checks that need a specific reagent/material
                    // without grinding a rare drop (e.g. GG-T45 potency-brew UI).
                    if (app.Save == null) return new { error = "no save" };
                    var itemId = q["item"];
                    if (itemId == null) return new { error = "missing item" };
                    return new { ok = true, count = Dev.DevCheats.GiveItem(app.Save, itemId, (int)F(q["count"], 1)) };
                }
                case "devrested":
                {
                    // Test-only (GG-T232): sets the party's Rested points; `limit=1` turns the pool on for this run.
                    if (app.Save == null) return new { error = "no save" };
                    if (q["limit"] != null) RestedPool.Unlimited = F(q["limit"]) == 0;
                    if (q["points"] != null) app.Save.Rested.Points = Math.Max(0, Math.Min(RestedPool.Max, (int)F(q["points"])));
                    app.Save.Rested.RegenUtc = DateTime.UtcNow;
                    return new { ok = true, points = app.Save.Rested.Points, unlimited = RestedPool.Unlimited };
                }
                case "devxp":
                {
                    // Test-only (GG-T241): sets a roster hero's experience to the start of `level` (LevelReached beats).
                    if (app.Save == null) return new { error = "no save" };
                    // hero=all levels the whole roster.
                    var heroes = q["hero"] == "all" ? app.Save.Roster.ToList()
                        : new[] { q["hero"] != null ? app.Save.Hero(q["hero"]) : app.Save.Roster.FirstOrDefault() }.Where(x => x != null).ToList();
                    if (heroes.Count == 0) return new { error = "no such hero" };
                    Dev.DevCheats.SetLevel(heroes, (int)F(q["level"], 10));
                    app.Persist();
                    return new { ok = true, levels = heroes.ToDictionary(h => h.ClassId, Progression.Level) };
                }
                case "devstage":
                {
                    // Test-only (GG-T241): jump the campaign to `stage` — every earlier stage cleared, at camp (GG-T1786:
                    // travel has no per-region run to fast-forward) — so a biome finale or the ending stage can be launched directly.
                    if (app.Save == null) return new { error = "no save" };
                    if (Dev.DevCheats.JumpToStage(app.Content, app.Save, q["stage"]) == null) return new { error = "unknown stage" };
                    app.Persist();
                    return new { ok = true, cleared = app.Save.HighestClearedStage };
                }
                case "devbond":
                {
                    // Test-only (GG-T1300): moves a hero's Trust (hero=cleric) or a pair's cohesion (hero=cleric&other=monk, or
                    // pair=cleric,monk) by amount=N, or to the floor of tier=N, through the same clamps play uses.
                    if (app.Save == null) return new { error = "no save" };
                    string a = q["hero"], b = q["other"];
                    if (q["pair"] != null) { var p = q["pair"].Split(','); a = p[0].Trim(); b = p.Length > 1 ? p[1].Trim() : null; }
                    bool pair = b != null;
                    if (pair ? Bonds.PairKey(a, b) == null || !Bonds.HasCohesion(app.Save, a) || !Bonds.HasCohesion(app.Save, b) : !Bonds.HasTrust(app.Save, a))
                        return new { error = "no bond for that hero or pair" };
                    int now = pair ? Bonds.Cohesion(app.Save, a, b) : Bonds.Trust(app.Save, a);
                    int delta = q["tier"] != null ? Bonds.Floor((int)F(q["tier"])) - now : (int)F(q["amount"]);
                    int value = pair ? Bonds.AddCohesion(app.Save, a, b, delta) : Bonds.AddTrust(app.Save, a, delta);
                    app.Persist();
                    return new { ok = true, value, tier = Bonds.Tier(value), talk = DialogueRunner.NewTalkCount(app.Content, app.Save) };
                }
                case "devtalk":
                {
                    // Test-only (GG-T241): a throwaway two-speaker conversation between classes a and b (portrait layout checks).
                    string a = q["a"] ?? "cleric", b = q["b"] ?? "monk";
                    if (app.Content.TryClass(a) == null || app.Content.TryClass(b) == null) return new { error = "unknown class" };
                    var def = new DialogueDef { Id = "dev_talk", Title = "Portrait check" };
                    def.Speakers.Add(new DialogueSpeaker { Id = "a", Class = a });
                    def.Speakers.Add(new DialogueSpeaker { Id = "b", Class = b });
                    def.Nodes.Add(new DialogueNode { Id = "n1", Speaker = "a", Text = "Hold still a moment. I want to see how we stand side by side.", Next = "n2" });
                    def.Nodes.Add(new DialogueNode { Id = "n2", Speaker = "b", Text = "Side by side, then. Same box, same light, whoever's speaking.", Next = "n3" });
                    def.Nodes.Add(new DialogueNode { Id = "n3", Speaker = "a", Text = "Good. Back to the road." });
                    app.Router.Push(new DialogueScreen(def, () => new HubScreen()));
                    return new { ok = true };
                }
                case "devwin":
                    if (app.Router.Current is BattleScreen dwin) return new { ok = dwin.DevWin() };
                    return new { error = "not in battle" };
                case "devwanderer":
                case "devcaptive": // the GG-T241 name, kept for old harness scripts
                    // GG-T342: the next player-launched battle holds this wanderer (skips the 25% roll).
                    BattleScreen.DevNextWanderer = q["class"] ?? Wanderers.Pool(app.Content, app.Save).FirstOrDefault();
                    return new { ok = BattleScreen.DevNextWanderer != null, wanderer = BattleScreen.DevNextWanderer };
                case "devbreeds":
                    // Test-only (GG-T1798): the breed gallery, every masked class in every breed through the real recolour bake.
                    app.Router.Push(new BreedGalleryScreen());
                    return new { ok = true };
                case "givemana":
                    if (app.Router.Current is BattleScreen gm) { gm.DevGiveMana(int.TryParse(q["n"], out var gn) ? gn : 6); return new { ok = true, mana = ManaDump(gm.State) }; }
                    return new { error = "not in battle" };
                case "devcast":
                    if (app.Router.Current is BattleScreen dcs) { int caster = dcs.DevQueueCast((int)F(q["target"]), q["ability"], (int)F(q["ticks"], 30)); return new { ok = caster >= 0, caster }; }
                    return new { error = "not in battle" };
                case "devpushback":
                    // GG-T1094: kind=push|slow|clear, shift= (bar share, default 0.35), k= freezes the slide there (omit to play it).
                    if (app.Router.Current is BattleScreen dpb) return (object)dpb.DevPushback(q["kind"] ?? "push", F(q["shift"], 0.35f), F(q["k"], -1f)) ?? new { error = "no timeline" };
                    return new { error = "not in battle" };
                case "devprefab":
                    // GG-T269: play one vfx.json effect prefab on the first foe (the prefab contact sheet).
                    if (app.Router.Current is BattleScreen dpf)
                        return new { ok = dpf.DevPrefab(q["id"], q["keep"] == "1", int.TryParse(q["nth"], out var nth) ? nth : 0) };
                    return new { error = "not in battle" };
                case "devvfx":
                    // GG-T253: play one ability's whole look (cast, flight, impact at its tier, the status) without touching state.
                    if (app.Router.Current is BattleScreen dvx) return new { ok = dvx.DevVfx(q["ability"]) };
                    return new { error = "not in battle" };
                case "devamp":
                    if (app.Router.Current is BattleScreen dab) { var ampAt = new Coord((int)F(q["x"]), (int)F(q["y"])); if (!dab.State.FreeForSpawn(ampAt) || !dab.State.ItemFree(ampAt)) return new { error = "tile not free" }; dab.State.Amplifier = ampAt; dab.SyncAll(); dab.Feed(BattleScreen.AmpSpawnedFeed, BattleScreen.AmpFeedColor); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "devpickup":
                    // GG-T229: drop a pickup (id=pk_..., default the first) on a free tile, drawn with its fade ring.
                    if (app.Router.Current is BattleScreen dpk)
                    {
                        var pkAt = new Coord((int)F(q["x"]), (int)F(q["y"]));
                        if (!dpk.State.FreeForSpawn(pkAt) || !dpk.State.ItemFree(pkAt)) return new { error = "tile not free" };
                        var pkId = q["id"] ?? dpk.State.Content.Pickups.Keys.First();
                        if (!dpk.State.Content.Pickups.ContainsKey(pkId)) return new { error = "unknown pickup " + pkId };
                        int left = Mathf.Clamp((int)F(q["left"], BattleConfig.PickupLifetimeActions), 1, BattleConfig.PickupLifetimeActions);
                        dpk.State.Pickups.Add(new Pickup { Id = dpk.State.NextPickupId++, PickupId = pkId, At = pkAt, ActionsLeft = left });
                        dpk.SyncAll();
                        return new { ok = true, id = pkId };
                    }
                    return new { error = "not in battle" };
                case "democaster":
                    if (app.Router.Current is BattleScreen democ) { democ.DemoCaster(); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "democutin":
                    if (app.Router.Current is BattleScreen dc) { dc.DemoCutIn(); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "demosurprise":
                    if (app.Router.Current is BattleScreen dsp) { dsp.DemoSurprise(q["speaker"] ?? "captain", q["line"] ?? "Reinforcements!"); return new { ok = true }; }
                    return new { error = "not in battle" };
                case "devhp":
                    if (app.Router.Current is BattleScreen dh) return new { ok = dh.DevHp((int)F(q["unit"]), (int)F(q["hp"], 1)) };
                    return new { error = "not in battle" };
                case "devweapon":
                    if (app.Router.Current is BattleScreen dw) return new { ok = dw.DevWeaponDurability((int)F(q["unit"]), (int)F(q["durability"], 1)) };
                    return new { error = "not in battle" };
                case "setbar":
                    if (app.Router.Current is BattleScreen setb) return new { ok = setb.DevSetBar((int)F(q["unit"]), (int)F(q["slot"]), q["entry"]) };
                    return new { error = "not in battle" };
                case "buff":
                    if (app.Router.Current is BattleScreen bb) return new { ok = bb.DevBuff((int)F(q["unit"]), q["id"]) };
                    return new { error = "not in battle" };
            }
            return Unrouted;
        }
    }
}
#endif
