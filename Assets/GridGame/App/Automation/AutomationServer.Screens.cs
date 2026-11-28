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
    /// <summary>Navigation and app routes: back, home, new game, launch, goto, settings, profiles, safe area, device frame, window size, quit.</summary>
    public sealed partial class AutomationServer
    {
        private object ScreensRoutes(string path, System.Collections.Specialized.NameValueCollection q, Action<object> reply, GameApp app)
        {
            switch (path)
            {
                case "back": app.Router.Back(); return new { ok = true };
                case "home": return app.Router.Home() ? (object)new { ok = true } : new { error = "a timed job is playing out" }; // GG-T1335
                case "newgame":
                {
                    int slot = (int)F(q["slot"], 0);
                    app.Save = Progression.NewGame(app.Content, q["name"] ?? "Bot");
                    app.Slot = slot;
                    if (q["gold"] != null) app.Save.Gold = (int)F(q["gold"]);
                    // npcs=1: every Camp NPC already met (GG-T119); npcs=merchant,blacksmith: just those (GG-T175).
                    if (q["npcs"] == "1") Dev.DevCheats.UnlockAllStations(app.Save);
                    else if (q["npcs"] != null)
                        foreach (var n in q["npcs"].Split(',')) app.Save.Flags.Add(ContentLoader.NpcFlagPrefix + n.Trim());
                    // seen=blacksmith,shop: those camp tips already dismissed, to reach a later onboarding step (GG-T177).
                    if (q["seen"] != null)
                        foreach (var t in q["seen"].Split(',')) CampTips.MarkSeen(app.Save, t.Trim());
                    // lessons=hub-shop,equip: those coach lessons already seen (GG-T542), so a camp-tip harness can open a
                    // station directly instead of being walked in by the Camp-menu lesson of every vendor it seeds.
                    if (q["lessons"] != null)
                        foreach (var l in q["lessons"].Split(',')) Coach.MarkSeen(app.Save, l.Trim());
                    app.ApplySettings();
                    app.Persist();
                    app.Router.ResetTo(new HubScreen());
                    return new { ok = true };
                }
                case "launch":
                    // GG-T241: the real launch path (crawl, story beats, Rested, expedition gate) for one stage.
                    StageLauncher.Launch(q["stage"], false);
                    return new { ok = true, screen = app.Router.Current?.Id };
                case "goto":
                    if (!StartScreens.TryGet(q["screen"], out var screen)) return new { error = "unknown screen" };
                    app.Router.Push(screen);
                    return new { ok = true };
                case "setting":
                {
                    var st = app.Settings; // the active profile's (GG-T99); in memory, so a harness run never writes it
                    bool on = q["value"] == "1" || q["value"] == "true";
                    switch (q["key"])
                    {
                        case "colorblind": st.ColorblindMode = on; break;
                        case "reducemotion": st.ReduceMotion = on; break;
                        case "dragtimer": st.DragTimer = on; break;
                        case "mutemusic": st.MuteMusic = on; break;
                        case "mutesfx": st.MuteSfx = on; break;
                        case "cutins": st.PincerCutIns = on; break;
                        case "confirm": st.ConfirmActions = on; break;
                        case "menutheme": st.MenuTheme = q["value"]; break; // GG-T185: value=Slate|Classic|...
                        case "hyphenate": st.Hyphenate = on; break; // GG-T305: Text wrapping → Hyphenate
                        case "textsize": st.TextScale = TextSize.Clamp((int)F(q["value"], 100)); break; // GG-T495: value=100|115|130
                        default: return new { error = "unknown key" };
                    }
                    app.ApplySettings();
                    return new { ok = true };
                }
                case "profile":
                {
                    // GG-T99: profiles under the run's own root (-saveRoot), never the real ones.
                    // op=list | switch&name= | create&name= | rename&name=&to= | delete&name=
                    try
                    {
                        switch (q["op"] ?? "list")
                        {
                            case "list": break;
                            case "switch": app.SwitchProfile(q["name"]); break;
                            case "create": app.CreateProfile(q["name"]); break;
                            case "rename": app.RenameProfile(q["name"], q["to"]); break;
                            case "delete": app.DeleteProfile(q["name"]); break;
                            default: return new { error = "unknown op" };
                        }
                    }
                    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { return new { error = e.Message }; }
                    return new { ok = true, current = app.Profile.Name, profiles = app.Profile.List(), root = app.Profile.Store.Root };
                }
                case "safearea":
                {
                    // GG-T483: fake a notch / gesture bar. top=&bottom= in window px, or quoted at a phone's height with
                    // ref= (top=90&bottom=48&ref=2400 is a 1080x2400 phone's, scaled to this window); clear=1 goes back to
                    // the device's own. Applies on the next frame; with no arguments, reports the insets in force.
                    if (q["clear"] == "1") app.FakeSafeArea(-1f, -1f);
                    else if (q["top"] != null || q["bottom"] != null)
                    {
                        float scale = q["ref"] != null ? Screen.height / Mathf.Max(1f, F(q["ref"], Screen.height)) : 1f;
                        app.FakeSafeArea(F(q["top"]) * scale, F(q["bottom"]) * scale);
                        return new { ok = true, applies = "next frame", topPx = F(q["top"]) * scale, bottomPx = F(q["bottom"]) * scale };
                    }
                    var screensBox = app.Stage.Q("screens")?.layout ?? default;
                    return new
                    {
                        safeTop = GameApp.SafeTop, safeBottom = GameApp.SafeBottom, design = new[] { GameApp.StageWidth, GameApp.StageHeight },
                        screens = new[] { screensBox.y, screensBox.height }, device = Screen.safeArea.ToString(), screen = new[] { Screen.width, Screen.height },
                    };
                }
                case "deviceframe":
                {
                    // GG-T2119: draw an emulated phone's camera cutout and rounded screen corners over everything.
                    // type=island|notch|punch|none&w=&h=&top=&r=&corner= in the device's px, refw=&refh= its panel; clear=1
                    // removes it; no arguments reports what is drawn (null when off).
                    if (q["clear"] == "1") { Dev.DeviceFrame.Hide(); return new { ok = true, frame = (object)null }; }
                    if (q["refh"] != null)
                    {
                        Dev.DeviceFrame.Show(q["type"] ?? "none", F(q["w"]), F(q["h"]), F(q["top"]), F(q["r"]), F(q["corner"]),
                            F(q["refw"], Screen.width), F(q["refh"], Screen.height));
                        return new { ok = true, frame = Dev.DeviceFrame.Report() };
                    }
                    return new { frame = Dev.DeviceFrame.Report() };
                }
                case "resize":
                    Screen.SetResolution((int)F(q["w"], 585), (int)F(q["h"], 1266), FullScreenMode.Windowed);
                    return new { ok = true };
                case "quit": Application.Quit(); return new { ok = true };
            }
            return Unrouted;
        }
    }
}
#endif
