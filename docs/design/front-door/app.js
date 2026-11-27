// Front-door click-through: a fixed list of screens, hash routing, and two toggles (first launch, save in use) that change the route.
(function () {
  "use strict";
  var state = { firstRun: true, hasSave: false, skipped: false, name: "Commander", veteran: false, music: 60, sfx: 80, speed: 1, motion: false, cblind: false, level: 10, crawl: 1, drill: 1, step: 1 };
  var CRAWL_PAGES = 4;
  var VOLUMES = [0, 20, 40, 60, 80, 100], SPEEDS = [1, 1.5, 2, 3];
  var NOT_PLAYTESTED = "Built, not yet playtested.";
  var cycle = function (opts, v) { return opts[(opts.indexOf(v) + 1) % opts.length]; };
  var fmtSpeed = function (v) { return "\u00d7" + v; };
  // The drill stages as built (stages.json tutorial-01..05, campaign.json Tutorial).
  var DRILLS = ["First Steps", "The Line", "Mana", "The Clock", "Roles"];
  var DRILL_LESSONS = [
    "Moving, Tap to read, Pincers",
    "Shoving, Chains, Supporters",
    "Mana orbs, Mana, Spells, Buffs & debuffs",
    "The hold timer, The timeline, Guard, Interrupting",
    "Roles & Taunt, Pickups, Armor plates, Ready"
  ];
  var $ = function (id) { return document.getElementById(id); };
  var esc = function (s) { return String(s).replace(/[&<>"]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]; }); };
  var valid = function (n) { return n.trim().length > 0 && n.trim().length <= 16 && !/[\\/:*?"<>|]/.test(n); };

  var S = {
    splash: {
      t: "Splash", refs: "BIBLE 11.2 SplashScreen (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). GG-T1317, GG-T1322, GG-T1345, GG-T1406, GG-T1769; logo and line pending the owner pick (GG-T1333).",
      spec: "Every launch opens on the MindAttic LLC logo and the copyright line over a full-bleed Palette.Ink backdrop that bleeds under the safe-area insets, fading in and out over about 3 s with a soft cue. A tap anywhere skips. Reduce Motion shows it without the fade. The logo is a placeholder image; with none, the fallback is the studio name as a Cinzel wordmark (Ui.Display). The copyright line sits 32 px below, as the splash-copy class. Then Create Profile when the active profile has no game.firstRunDone and no saves, otherwise the Main Menu. Automation, -start, -testmode, -demo and headless runs skip the splash. " + NOT_PLAYTESTED,
      look: [["Backdrop", "Palette.Ink (#0E0E12), full bleed", "none"], ["Studio wordmark", "Ui.Display, Cinzel (--display)", "soft cue as it fades in"], ["Copyright", "tiny dim splash-copy, margin --space-32", "none"]],
      go: function () { return state.firstRun ? ["profile"] : ["menu"]; },
      html: function () { return '<div class="pad center splash" data-go="' + (state.firstRun ? "profile" : "menu") + '"><div class="logo">M</div><div class="studio">MINDATTIC LLC</div><div class="copy">&copy; 2026 MindAttic LLC. All rights reserved.</div><div class="note" style="margin-top:40px">tap to skip</div></div>'; }
    },
    profile: {
      t: "Create Profile", refs: "BIBLE 15.4 first-run profile (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). GG-T1336, GG-T1339, GG-T1344, GG-T1345, GG-T1406, GG-T1769; name kept in saves by GG-T1342.",
      spec: "First launch only (the active profile has no game.firstRunDone and no saves). A three-step wizard in the menu kit: 1 the Commander's name (the profile name rules: 1-16 characters, a legal folder name on every platform, not taken; a refused name plays ui.error), 2 quick settings as icon rows that cycle on tap (music and sound volume, battle speed, Reduce motion, Colorblind palette, Difficulty with its description), each previewed live with ui.confirm and restored if you leave, 3 a summary and Begin. Back on a step plays ui.cancel. Begin renames the store's empty Default profile to the Commander's name, writes the picks and game.firstRunDone, and seeds SaveData.CommanderName for every new campaign. Settings can change all of it later. " + NOT_PLAYTESTED,
      look: [["Settings rows", "Ui.MenuRow + KindIcon(RowKind.Settings), Ui.ListPanel", "ui.confirm on each change"], ["Step Back", "RowKind.Back icon button", "ui.cancel"], ["Begin", "btn--primary, RowKind.Confirm", "none"], ["Name error", "problem line under the field", "ui.error"]],
      go: function () { return ["menu"]; },
      html: function () {
        var steps = '<div class="steps">' + [1, 2, 3].map(function (i) { return '<i class="' + (i <= state.step ? "on" : "") + '"></i>'; }).join("") + "</div>";
        var body;
        if (state.step === 1) body = '<div class="pane"><h3>Who leads the crew?</h3><p>This is the name the story calls you.</p></div><label class="field">Commander name<input type="text" id="cname" maxlength="16" value="' + esc(state.name) + '"></label><div class="err" id="cerr"></div><div style="flex:1"></div><button class="btn primary" data-act="name">Next</button>';
        else if (state.step === 2) body = '<div class="pane rows"><h3>Quick settings</h3>' + [["music", "Music volume", state.music + "%"], ["sfx", "Sound volume", state.sfx + "%"], ["speed", "Battle speed", fmtSpeed(state.speed)], ["motion", "Reduce motion", state.motion ? "ON" : "OFF"], ["cblind", "Colorblind palette", state.cblind ? "ON" : "OFF"], ["level", "Difficulty", String(state.level)]].map(function (r) { return '<button class="mrow" data-cyc="' + r[0] + '"><span class="ico">&#9881;</span><span>' + r[1] + "</span><b>" + r[2] + "</b></button>"; }).join("") + '<p class="note">Level ' + state.level + ' of 10. You can change any of this in Settings.</p></div><div style=\"flex:1\"></div><div class=\"row\"><button class=\"btn ghost\" data-act=\"back\">Back</button><button class=\"btn primary\" data-act=\"step3\">Next</button></div>';
        else body = '<div class="pane"><h3>Ready, <span class="tok">' + esc(state.name) + '</span></h3><p>Music ' + state.music + "% &middot; Sound " + state.sfx + "% &middot; Battle speed " + fmtSpeed(state.speed) + " &middot; Difficulty " + state.level + (state.motion ? " &middot; Reduce motion" : "") + (state.cblind ? " &middot; Colorblind palette" : "") + '</p></div><p class="note">You can change any of this in Settings.</p><div style="flex:1"></div><div class="row"><button class="btn ghost" data-act="back">Back</button><button class="btn primary" data-act="done">Begin</button></div>';
        return '<div class="hdr">Create Profile <small>step ' + state.step + ' of 3</small></div><div class="pad">' + steps + body + "</div>";
      }
    },
    menu: {
      t: "Main Menu", refs: "BIBLE 11.2 (TitleScreen becomes the Main Menu; built, " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). GG-T1347, GG-T1350, GG-T1406, GG-T1769.",
      spec: "The old Title, reworked: a Cinzel wordmark (Ui.Display, shadowed) over a mana conduit underline, the profile tag, a Continue card when a save exists (the Commander in gold, the stage Continue leads to, play time and when it was last played, the party's faces; one tap resumes), then one Ui.Panel of icon rows: Start Campaign, Load, Profile, Settings, About, Credits. A Palette.Ink shade sits behind the panel. The wordmark rises, the conduit charges outward with the orb cue, then the tagline, Continue card and rows follow each with a soft tick; Reduce Motion shows everything at once. Hardware back here asks to quit. " + NOT_PLAYTESTED,
      look: [["Wordmark", "Ui.Display, Cinzel, shadow", "none"], ["Conduit", "mana blue bar, 56 wide, scaled in", "battle.orb"], ["Entrance steps", "rise 30 px and fade, Timing.Quick", "ui.click each"], ["Continue card", "card-tap, title-continue, gold Commander caption", "ui.select on tap"], ["Rows", "Ui.Panel + Ui.MenuRow + KindIcon", "ui.select"], ["Shade", "Palette.Ink at 45%", "none"]],
      go: function () { return (state.hasSave ? ["camp"] : []).concat(["start", "about", "credits"]); },
      html: function () {
        var cont = state.hasSave ? '<div class="pane" data-go="camp" style="cursor:pointer"><h3>Continue</h3><p class="tok">' + esc(state.name) + "</p><p>Tithe Moors II</p><p>3:12 played &middot; 2026-11-27 18:40</p><p>&#9679; &#9679; &#9679;</p></div>" : "";
        var rows = [["start", "Start Campaign"], ["menu", "Load"], ["menu", "Profile"], ["menu", "Settings"], ["about", "About"], ["credits", "Credits"]].map(function (r) { return '<button class="mrow" data-go="' + r[0] + '"><span class="ico">&#9670;</span><span>' + r[1] + "</span></button>"; }).join("");
        return '<div class="wordmark">GRIDGAME</div><div class="conduit"></div><div class="tag">Profile: ' + esc(state.name) + '</div><div class="pad">' + cont + '<div class="pane rows">' + rows + "</div></div>";
      }
    },
    start: {
      t: "Start Campaign: slot", refs: "BIBLE 11.2.1 (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). GG-T1351, GG-T1355, GG-T1358, GG-T1769 (reviewed clean).",
      spec: "Start Campaign skips this picker when every slot is empty and takes slot 1. Only when a slot is in use does it show the slots; picking a used one confirms the overwrite (a kit dialog: Ui.Panel, sound on show, fade in). The turn-mode row folds under More options. The save is named after the Commander. Then the team pick, and once per new campaign the New to GridGame? question. " + NOT_PLAYTESTED,
      look: [["Slot list", "Ui.ListPanel, MenuRow", "ui.select"], ["Overwrite dialog", "Confirm.Show on Ui.Panel + Ui.Actions", "ui.select as it opens, fade in"]],
      go: function () { return ["team"]; },
      html: function () {
        if (!state.hasSave) return '<div class="pad center"><div class="stub"><b>Picker skipped</b>Every slot is empty, so Start Campaign goes straight to slot 1.</div><button class="btn primary" data-go="team">Continue</button></div>';
        return '<div class="hdr">Start Campaign <small>pick a slot</small></div><div class="pad"><div class="pane" style="opacity:.8"><h3>Slot 1</h3><p>' + esc(state.name) + ' &middot; Tithe Moors II &middot; overwrite asks first</p></div><div class="pane" data-go="team" style="cursor:pointer"><h3>Slot 2</h3><p>Empty</p></div><div class="pane" data-go="team" style="cursor:pointer"><h3>Slot 3</h3><p>Empty</p></div><button class="btn ghost">More options</button></div>';
      }
    },
    ask: {
      t: "New to GridGame?", refs: "Confirm.Choose (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). GG-T1645, GG-T1406.",
      spec: "Asked once per new campaign, only when drill stages exist (never in demo). A two-answer kit dialog with no Cancel: Play the drills, or Skip to Camp. Skip turns every lesson off, marks the drills skipped and carries the onboarding to Camp. Once this profile has finished the drills on any slot, the copy becomes 'You've run the drills before, Commander' and Skip to Camp is the primary button and what Android Back answers; otherwise Play the drills is. The dialog sounds as it opens and fades in, held still under Reduce Motion. " + NOT_PLAYTESTED,
      look: [["Dialog", "Ui.Panel, 76% wide, Ui.Heading + Ui.Note, Ui.Actions", "ui.select as it opens"], ["Fade", "Timing.Quick, none under Reduce Motion", "none"], ["Buttons", "icon buttons, RowKind.Enter / Station; default is btn--primary", "none"]],
      go: function () { return ["crawl"]; },
      html: function () {
        var vet = state.veteran;
        return '<div class="pad center"><div class="pane dlg"><h3>New to GridGame?</h3><p>' + (vet ? "You've run the drills before, Commander. Skip them and go straight to Camp, or run them again." : "A few short drills teach the board before the campaign begins. Skip them if you know the game.") + '</p><div class="row end"><button class="btn small' + (vet ? "" : " primary") + '" data-act="play">Play the drills</button><button class="btn small' + (vet ? " primary" : "") + '" data-act="skip">Skip to Camp</button></div></div><label class="note"><input type="checkbox" id="vet"' + (vet ? " checked" : "") + '> Drills finished on another slot</label></div>';
      }
    },
    crawl: {
      t: "Opening crawl", refs: "BIBLE 27.0 Opening crawl (was Prologue; built, " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). Keys crawl:opening/1..n; the words come from Prose under the story slice (GG-T1256, GG-T1303). Content model GG-T1363; paged crawl GG-T1366; look GG-T1406, GG-T1769.",
      spec: "A paged crawl: each page's text scrolls up at 95 px/s over its own backdrop, tinted Palette.ManaIndigo and bleeding under the safe-area insets; backdrops cross-fade as pages turn (a cut under Reduce Motion) and each turn plays ui.select at half volume. The first page carries the story's title, centred in Cinzel gold with a 64 px gap (Ui.Display, crawl-title). Hold anywhere to fast-forward x4; a tap does not skip. Skip ends it, and on a first viewing asks 'Tap again to skip'. Text may use the {commander} token, filled per page. It follows the team pick and New to GridGame?, and leads into the Founding scene when due, then the tutorial drills (or Camp's first visit when the drills were skipped). Plays once per campaign; never for Test Mode or -demo saves, nor the automation newgame shortcut (tools/harness.py frontdoor taps Start Campaign, so it sees the crawl and taps Skip). " + NOT_PLAYTESTED,
      look: [["Backdrop tint", "Palette.ManaIndigo (#4B447D)", "none"], ["Story title", "Ui.Display (Cinzel) shadow gold crawl-title, 64 px below", "none"], ["Page turn", "Timing.Cinematic cross-fade", "ui.select at 0.5"], ["Skip", "RowKind.Enter btn--small", "none (toast: Tap again to skip)"]],
      go: function () { return [state.skipped ? "grad" : "drill"]; },
      html: function () {
        var dots = '<div class="dots">' + Array.apply(null, Array(CRAWL_PAGES)).map(function (_, i) { return '<i class="' + (i + 1 === state.crawl ? "on" : "") + '"></i>'; }).join("") + "</div>";
        return '<button class="btn small ghost skip" data-act="crawlend">Skip &rsaquo;</button><div class="crawl indigo"><span class="key">crawl:opening/' + state.crawl + '</span>' + (state.crawl === 1 ? '<h3 class="crawl-title">BEFORE THE ROAD</h3>' : "") + '<p>Words from Prose, scrolling up. A line that names the lead reads <span class="tok">{commander}</span>, shown as <span class="tok">' + esc(state.name) + "</span>.</p>" + dots + '<button class="btn primary" data-act="page">' + (state.crawl < CRAWL_PAGES ? "Next page" : "Begin") + "</button></div>";
      }
    },
    team: {
      t: "Team pick (stub)", stub: true, refs: "Owned by the team slice (GG-T1257, GG-T1318 Founding). CharacterSelectScreen and the VariantPicker, then SaveSlotScreen.Start asks New to GridGame?.",
      spec: "Coordination point. The starting heroes are chosen here, straight after Start Campaign (as built: before the opening crawl, so the crawl, the Founding conversation and the drills play with the chosen party). The team slice owns the screen and the Founding conversation, which plays after the crawl when due.",
      go: function () { return ["ask"]; },
      html: function () { return '<div class="pad center"><div class="stub"><b>Team pick</b>Owned by the team slice (GG-T1257). Starting heroes chosen; the Founding talk follows the crawl.</div><button class="btn primary" data-go="ask">Lock in the party</button></div>'; }
    },
    drill: {
      t: "Tutorial drills", refs: "BIBLE 11.2.1 (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). Lessons and levels GG-T1315, GG-T1374; intro card GG-T1641; skip GG-T1398; practice GG-T1647; coach look GG-T1769; harness flow GG-T1408.",
      spec: "Five short drill levels, each teaching one idea with coach marks on a small board. Each opens on an intro card in the Commander's voice (the stage's Intro line, a markless coach popup with its chime; Next opens lesson 1). A lesson card's example line is Palette.Term blue. A lesson card's kicker reads 'Drill n of 5 · Lesson k of m'. Skip tutorial (in the drill's Tutorial tab, with a confirm) skips the whole set and goes to Camp; Settings > How to play lists the drills to practise without touching the save. Leaving mid-drill resumes at that drill next launch. Drills cost no Rested and pay no XP, gold or loot. The names and lessons are the built stages (tutorial-01..05); python tools/harness.py frontdoor plays all five, mark by mark. " + NOT_PLAYTESTED,
      look: [["Intro card", "coach popup, no marks", "chime, fade in"], ["Example line", "Palette.Term (#9BC7FF)", "none"], ["Button row", "Ui.Pad top --space-16", "none"]],
      go: function () { return ["drillres"]; },
      html: function () {
        var cells = "";
        for (var i = 0; i < 36; i++) cells += '<i class="' + (i === 14 ? "h" : i === 16 ? "h" : i === 15 ? "f" : i === 20 ? "t" : "") + '"></i>';
        return '<div class="hdr">Drill ' + state.drill + ' of 5 <small>' + DRILLS[state.drill - 1] + '</small></div><div class="pad"><p class="note">' + DRILL_LESSONS[state.drill - 1] + '</p><div class="board">' + cells + '</div><div class="coach">Drag a hero so the foe sits between two of yours: that is a pincer.<br><span class="term">Example: a hero on each side of the foe.</span></div><div style="flex:1"></div><button class="btn primary" data-act="drill">Win the drill</button><button class="btn ghost" data-act="skip">Skip tutorial</button></div>';
      }
    },
    drillres: {
      t: "Drill results", refs: "PostBattleScreen drill card (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). GG-T1379, GG-T1641, GG-T1406.",
      spec: "A short card instead of the loot screen, since a drill earns nothing. A win shows DRILL CLEARED in gold with 'Drill n of 5' and the outro line (the Commander voice); Next drill launches the next, and after the last, To Camp records the onboarding and moves on. A loss, retreat or escape shows TRY AGAIN in Palette.Bad with 'Drills are practice: nothing is lost'; Retry replays the drill from its start (Back does the same). The title pops after a short wait with its chime; Reduce Motion keeps the sound and drops the pop. " + NOT_PLAYTESTED,
      look: [["DRILL CLEARED", "t-display, Palette.Gold", "battle.powerup + title pop"], ["TRY AGAIN", "t-display, Palette.Bad (#FFAAA2)", "ui.cancel + title pop"], ["Card", "Ui.Panel, kit-text, margin --space-48", "none"], ["Buttons", "Next drill / To Camp / Retry, btn--primary", "none"]],
      go: function () { return ["drill", "grad"]; },
      html: function () {
        var last = state.drill >= 5;
        return '<div class="pad center"><div class="drill-title">DRILL CLEARED</div><p class="note">Drill ' + state.drill + ' of 5 &middot; ' + DRILLS[state.drill - 1] + '</p><div class="pane" style="width:100%"><p>' + (last ? "Every drill cleared. The caravan is ready for the road." : "Lesson learned. On to the next drill.") + '</p></div><button class="btn primary" data-act="next">' + (last ? "To Camp" : "Next drill") + '</button><div class="drill-title bad">TRY AGAIN</div><p class="note">the loss variant: Drills are practice, nothing is lost. <a class="lnk" data-act="drill">Retry</a></p></div>';
      }
    },
    camp: {
      t: "Camp, first visit", refs: "CampCoach.AdventureLesson hub-adventure (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). Hub UX slice (GG-T1325, GG-T1343, GG-T1348), GG-T1401; GG-T174 camp unlocks.",
      spec: "After the graduation card's Lead on, the hub-adventure lesson boxes the Adventure row (camp-adventure) and names the first stage; the onboarding is Done when it ends. Later Camp lessons wait for the next visit. The front door ends here. " + NOT_PLAYTESTED,
      go: function () { return ["menu"]; },
      html: function () { return '<div class="hdr">Camp <small>' + esc(state.name) + '</small></div><div class="pad"><div class="pane glow"><h3>Campaign</h3><p>Next: Tithe Moors I</p></div><div class="coach">Adventure opens the map of stages; Continue above it marches straight to the next one.</div><div class="tiles"><div class="pane"><h3>Crucible</h3><p>locked</p></div><div class="pane"><h3>Camp</h3><p>heroes, shops</p></div></div><button class="btn ghost" data-go="menu">Save &amp; Quit</button></div>'; }
    },
    grad: {
      t: "Graduation card", refs: "Confirm.Notice on Camp's first visit (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). GG-T1401, GG-T1394.",
      spec: "Camp's first visit, after drill 5 or a skip, opens a 'Your heroes are ready' card in the heroes' voice (a skipper hears 'We know our work, Commander'): a gold Camp kicker, the title, a line, and Lead on. It chimes, fades in and pops its title, held still under Reduce Motion; Android Back answers Lead on. Then the hub-adventure lesson boxes the Adventure row, naming the first stage. Onboarding is done when that lesson ends, or as the card shows with lessons off. Bots, test saves and demos skip it. " + NOT_PLAYTESTED,
      look: [["Card", "Ui.Panel, 76% wide, gold kicker Caption, Ui.Heading", "battle.powerup + title pop"], ["Lead on", "RowKind.Enter, btn--primary", "none"]],
      go: function () { return ["camp"]; },
      html: function () { return '<div class="hdr">Camp <small>' + esc(state.name) + '</small></div><div class="pad dimmed"><div class="pane"><h3>Adventure</h3><p>Next: Tithe Moors I</p></div></div><div class="veil"><div class="pane dlg"><span class="tok">Camp</span><h3>Your heroes are ready</h3><p>Drills done, Commander. The front line holds, the healer mends, and the rest of us hit hard. Say the word and we march.</p><div class="row end"><button class="btn small primary" data-go="camp">Lead on</button></div></div></div>'; }
    },
    about: {
      t: "About", refs: "BIBLE 11.2 AboutScreen (built; " + NOT_PLAYTESTED.toLowerCase().replace(".", "") + "). GG-T1324, GG-T1330.",
      spec: "From the Main Menu and Settings: studio, version and build, the copyright line, Privacy Policy and Terms links, and the third-party notices (fonts, the Unity.Shared package). Back returns where it came from.",
      go: function () { return ["menu"]; },
      html: function () { return '<div class="hdr">About</div><div class="pad"><div class="pane"><h3>GridGame</h3><p>MindAttic LLC &middot; version 0.0.0 (build)</p></div><div class="pane"><p>&copy; 2026 MindAttic LLC. All rights reserved.</p></div><button class="btn">Privacy Policy</button><button class="btn">Terms</button><button class="btn">Third-party notices</button><div style="flex:1"></div><button class="btn ghost" data-go="menu">Back</button></div>'; }
    },
    credits: {
      t: "Credits", refs: "BIBLE 27.0 CreditsScreen (built; static mode from the menu).",
      spec: "The existing CreditsScreen, unchanged, reached from the Main Menu.",
      go: function () { return ["menu"]; },
      html: function () { return '<div class="hdr">Credits</div><div class="credits">Resources/Credits.txt</div><div class="pad" style="flex:0"><button class="btn ghost" data-go="menu">Back</button></div>'; }
    }
  };

  var ORDER = ["splash", "profile", "menu", "start", "team", "ask", "crawl", "drill", "drillres", "grad", "camp", "about", "credits"];
  var current = "splash", timer = null;

  function show(id) {
    if (!S[id]) id = "splash";
    current = id;
    if (location.hash !== "#" + id) history.replaceState(null, "", "#" + id);
    var s = S[id];
    $("screen").innerHTML = s.html();
    $("ntitle").textContent = s.t;
    $("nspec").textContent = s.spec;
    $("nrefs").textContent = s.refs;
    $("nlook").innerHTML = (s.look || []).map(function (r) { return "<tr><td>" + esc(r[0]) + "</td><td>" + esc(r[1]) + "</td><td>" + esc(r[2]) + "</td></tr>"; }).join("");
    $("looksec").hidden = !(s.look && s.look.length);
    $("nlinks").innerHTML = s.go().map(function (g) { return '<li><a data-go="' + g + '">' + S[g].t + "</a></li>"; }).join("");
    $("flow").querySelectorAll("button").forEach(function (b) { b.classList.toggle("on", b.dataset.go === id); });
    var i = ORDER.indexOf(id);
    $("prev").disabled = i <= 0;
    $("next").disabled = i >= ORDER.length - 1;
    clearTimeout(timer);
    if (id === "splash") timer = setTimeout(function () { if (current === "splash") show(state.firstRun ? "profile" : "menu"); }, 3000);
  }

  function act(a) {
    if (a === "name") {
      var v = $("cname").value;
      if (!valid(v)) { $("cerr").textContent = "1-16 characters, no \\ / : * ? \" < > |"; return; }
      state.name = v.trim(); state.step = 2;
    } else if (a === "back") state.step = Math.max(1, state.step - 1);
    else if (a === "next") { if (state.drill < 5) { state.drill++; return show("drill"); } return show("grad"); }
    else if (a === "step3") state.step = 3;
    else if (a === "done") { state.firstRun = false; $("firstRun").checked = false; state.step = 1; return show("menu"); }
    else if (a === "page") { if (state.crawl < CRAWL_PAGES) state.crawl++; else return act("crawlend"); }
    else if (a === "crawlend") { state.crawl = 1; state.drill = 1; return show(state.skipped ? "grad" : "drill"); }
    else if (a === "play") { state.skipped = false; state.crawl = 1; return show("crawl"); }
    else if (a === "skip") { state.skipped = true; if (current === "ask") { state.crawl = 1; return show("crawl"); } return show("grad"); }
    else if (a === "drill") return show("drillres");
    show(current);
  }

  $("flow").innerHTML = ORDER.map(function (id) { return '<button type="button" data-go="' + id + '"' + (S[id].stub ? ' class="stub"' : "") + ">" + S[id].t + "</button>"; }).join("");
  document.addEventListener("click", function (e) {
    var t = e.target.closest("[data-go],[data-act],[data-cyc]");
    if (!t) return;
    if (t.dataset.cyc) {
      var k = t.dataset.cyc;
      if (k === "music" || k === "sfx") state[k] = cycle(VOLUMES, state[k]);
      else if (k === "speed") state.speed = cycle(SPEEDS, state.speed);
      else if (k === "level") state.level = state.level >= 10 ? 1 : state.level + 1;
      else state[k] = !state[k];
      return show(current);
    }
    if (t.dataset.act) return act(t.dataset.act);
    var go = t.dataset.go;
    if (go === "crawl") state.crawl = 1;
    if (go === "drill") state.drill = 1;
    show(go);
  });
  document.addEventListener("input", function (e) {
    if (e.target.id === "vet") { state.veteran = e.target.checked; return show(current); }
    var k = e.target.dataset && e.target.dataset.set;
    if (!k) return;
    state[k] = k === "motion" ? e.target.checked : +e.target.value;
  });
  $("prev").onclick = function () { show(ORDER[Math.max(0, ORDER.indexOf(current) - 1)]); };
  $("next").onclick = function () { show(ORDER[Math.min(ORDER.length - 1, ORDER.indexOf(current) + 1)]); };
  $("restart").onclick = function () { state.step = 1; show("splash"); };
  $("firstRun").onchange = function (e) { state.firstRun = e.target.checked; show(current); };
  $("hasSave").onchange = function (e) { state.hasSave = e.target.checked; show(current); };
  window.addEventListener("hashchange", function () { show(location.hash.slice(1)); });
  show(location.hash.slice(1) || "splash");
})();
