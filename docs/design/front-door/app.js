// Front-door click-through: a fixed list of screens, hash routing, and two toggles (first launch, save in use) that change the route.
(function () {
  "use strict";
  var state = { firstRun: true, hasSave: false, name: "Commander", music: 70, sfx: 80, speed: "Normal", motion: false, crawl: 1, drill: 1, step: 1 };
  var CRAWL_PAGES = 4;
  var DRILLS = [
    "Move and the pincer",
    "Supports and chains",
    "Abilities and mana",
    "Terrain and hazards",
    "The first real fight"
  ];
  var $ = function (id) { return document.getElementById(id); };
  var esc = function (s) { return String(s).replace(/[&<>"]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]; }); };
  var valid = function (n) { return n.trim().length > 0 && n.trim().length <= 16 && !/[\\/:*?"<>|]/.test(n); };

  var S = {
    splash: {
      t: "Splash", refs: "BIBLE 11.2 SplashScreen (Not yet built). GG-T1317, GG-T1322; logo and line pending the owner pick (GG-T1333).",
      spec: "Every launch opens on the MindAttic LLC logo and the copyright line, fading in and out over about 3 s with a soft cue. A tap anywhere skips. Reduce Motion shows it without the fade. Then Create Profile on a first launch, otherwise the Main Menu.",
      go: function () { return state.firstRun ? ["profile"] : ["menu"]; },
      html: function () { return '<div class="pad center splash" data-go="' + (state.firstRun ? "profile" : "menu") + '"><div class="logo">M</div><div class="studio">MINDATTIC LLC</div><div class="copy">&copy; 2026 MindAttic LLC. All rights reserved.</div><div class="note" style="margin-top:40px">tap to skip</div></div>'; }
    },
    profile: {
      t: "Create Profile", refs: "BIBLE 15.4 first-run profile (Not yet built). GG-T1336, GG-T1339, GG-T1344, GG-T1345; name kept in saves by GG-T1342.",
      spec: "First launch only (the active profile has no game.firstRunDone and no saves). Three steps in the menu kit: 1 the Commander's name (the profile name rules: 1-16 characters, a legal folder name on every platform, not taken), 2 quick settings (music, SFX, battle speed, Reduce Motion), 3 a short confirm. The name becomes the profile and seeds SaveData.CommanderName for every new campaign; Settings can change all of it later.",
      go: function () { return ["menu"]; },
      html: function () {
        var steps = '<div class="steps">' + [1, 2, 3].map(function (i) { return '<i class="' + (i <= state.step ? "on" : "") + '"></i>'; }).join("") + "</div>";
        var body;
        if (state.step === 1) body = '<div class="pane"><h3>Who leads the crew?</h3><p>This is the name the story calls you.</p></div><label class="field">Commander name<input type="text" id="cname" maxlength="16" value="' + esc(state.name) + '"></label><div class="err" id="cerr"></div><div style="flex:1"></div><button class="btn primary" data-act="name">Next</button>';
        else if (state.step === 2) body = '<label class="field">Music <input type="range" min="0" max="100" value="' + state.music + '" data-set="music"></label><label class="field">Sound effects <input type="range" min="0" max="100" value="' + state.sfx + '" data-set="sfx"></label><div class="field">Battle speed<div class="seg">' + ["Slow", "Normal", "Fast"].map(function (s) { return '<button data-speed="' + s + '" class="' + (state.speed === s ? "on" : "") + '">' + s + "</button>"; }).join("") + '</div></div><label class="field" style="grid-auto-flow:column;justify-content:start;gap:8px;align-items:center"><input type="checkbox" data-set="motion"' + (state.motion ? " checked" : "") + '> Reduce Motion</label><div style="flex:1"></div><div class="row"><button class="btn ghost" data-act="back">Back</button><button class="btn primary" data-act="step3">Next</button></div>';
        else body = '<div class="pane"><h3>Ready, <span class="tok">' + esc(state.name) + '</span></h3><p>Music ' + state.music + " &middot; SFX " + state.sfx + " &middot; " + state.speed + " speed" + (state.motion ? " &middot; Reduce Motion" : "") + '</p></div><p class="note">You can change any of this in Settings.</p><div style="flex:1"></div><div class="row"><button class="btn ghost" data-act="back">Back</button><button class="btn primary" data-act="done">Begin</button></div>';
        return '<div class="hdr">Create Profile <small>step ' + state.step + ' of 3</small></div><div class="pad">' + steps + body + "</div>";
      }
    },
    menu: {
      t: "Main Menu", refs: "BIBLE 11.2 (TitleScreen becomes the Main Menu, Not yet built). GG-T1347, GG-T1350.",
      spec: "The old Title, reworked: a code-drawn wordmark with a mana conduit underline, a Continue card when a save exists (the Commander, the last stage, play time; one tap resumes), then Start Campaign (was New Game), Load, Profile, Settings, About and Credits. Hardware back here asks to quit.",
      go: function () { return (state.hasSave ? ["camp"] : []).concat(["start", "about", "credits"]); },
      html: function () {
        var cont = state.hasSave ? '<div class="pane" data-go="camp" style="cursor:pointer"><h3>Continue</h3><p><span class="tok">' + esc(state.name) + "</span> &middot; Tithe Moors II &middot; 3h 12m</p></div>" : "";
        return '<div class="wordmark">GRIDGAME</div><div class="conduit"></div><div class="tag">Profile: ' + esc(state.name) + '</div><div class="pad">' + cont + '<button class="btn primary" data-go="start">Start Campaign</button><button class="btn" data-go="menu" title="existing SaveSlotScreen (load)">Load</button><div class="row"><button class="btn" data-go="menu" title="existing ProfileSelectScreen">Profile</button><button class="btn" data-go="menu" title="existing SettingsScreen">Settings</button></div><div class="row"><button class="btn" data-go="about">About</button><button class="btn" data-go="credits">Credits</button></div></div>';
      }
    },
    start: {
      t: "Start Campaign: slot", refs: "BIBLE 11.2.1 (Not yet built). GG-T1351, GG-T1355, GG-T1358.",
      spec: "Start Campaign skips this picker when every slot is empty and takes slot 1. Only when a slot is in use does it show the slots; picking a used one confirms the overwrite. The turn-mode row folds under More options. The save is named after the Commander.",
      go: function () { return ["crawl"]; },
      html: function () {
        if (!state.hasSave) return '<div class="pad center"><div class="stub"><b>Picker skipped</b>Every slot is empty, so Start Campaign goes straight to slot 1.</div><button class="btn primary" data-go="crawl">Continue to the crawl</button></div>';
        return '<div class="hdr">Start Campaign <small>pick a slot</small></div><div class="pad"><div class="pane" style="opacity:.8"><h3>Slot 1</h3><p>' + esc(state.name) + ' &middot; Tithe Moors II &middot; overwrite asks first</p></div><div class="pane" data-go="crawl" style="cursor:pointer"><h3>Slot 2</h3><p>Empty</p></div><div class="pane" data-go="crawl" style="cursor:pointer"><h3>Slot 3</h3><p>Empty</p></div><button class="btn ghost">More options</button></div>';
      }
    },
    crawl: {
      t: "Opening crawl", refs: "BIBLE 27.0 Opening crawl (was Prologue; Not yet built). Keys crawl:opening/1..n; the words come from Prose under the story slice (GG-T1256, GG-T1303). Content model GG-T1363.",
      spec: "Paged, not scrolled: each page is one Prose key crawl:opening/<n> shown over the Saltmoss backdrop; tap or Next turns the page, Skip ends it. Text may use the {commander} token. It now leads into the team pick and the tutorial drills, not straight to Camp. Plays once per campaign; never for automation, Test Mode or -demo saves.",
      go: function () { return ["team"]; },
      html: function () {
        var dots = '<div class="dots">' + Array.apply(null, Array(CRAWL_PAGES)).map(function (_, i) { return '<i class="' + (i + 1 === state.crawl ? "on" : "") + '"></i>'; }).join("") + "</div>";
        return '<button class="btn small ghost skip" data-go="team">Skip &rsaquo;</button><div class="crawl"><span class="key">crawl:opening/' + state.crawl + '</span><h3>' + (state.crawl === 1 ? "Before the Road" : "&nbsp;") + '</h3><p>Words from Prose. A line that names the lead reads <span class="tok">{commander}</span>, shown as <span class="tok">' + esc(state.name) + "</span>.</p>" + dots + '<button class="btn primary" data-act="page">' + (state.crawl < CRAWL_PAGES ? "Next" : "Begin") + "</button></div>";
      }
    },
    team: {
      t: "Team pick (stub)", stub: true, refs: "Owned by the team slice (GG-T1257, GG-T1318 Founding).",
      spec: "Coordination point. The starting heroes are chosen and met here; the team slice owns the screen and the Founding conversation. Whether it sits before the drills (drills use the chosen party) or after graduation is the team slice's call; this page shows it before.",
      go: function () { return ["drill"]; },
      html: function () { return '<div class="pad center"><div class="stub"><b>Team pick</b>Owned by the team slice (GG-T1257). Starting heroes chosen; Founding talk.</div><button class="btn primary" data-act="drills">To the drills</button></div>'; }
    },
    drill: {
      t: "Tutorial drills", refs: "BIBLE 11.2.1 (Not yet built). Lessons and levels designed under GG-T1315 (the 17 GG-T539/GG-T542 coach-mark lessons over 5 drills).",
      spec: "Five short drill levels, each teaching one idea with coach marks on a small board. A drill can be replayed, and the whole set can be skipped from its pause menu (skipping still counts as graduated). Leaving mid-drill resumes at that drill next launch. Drill names here are placeholders.",
      go: function () { return ["grad"]; },
      html: function () {
        var cells = "";
        for (var i = 0; i < 36; i++) cells += '<i class="' + (i === 14 ? "h" : i === 16 ? "h" : i === 15 ? "f" : i === 20 ? "t" : "") + '"></i>';
        return '<div class="hdr">Drill ' + state.drill + ' of 5 <small>' + DRILLS[state.drill - 1] + '</small></div><div class="pad"><div class="board">' + cells + '</div><div class="coach">Drag a hero so the foe sits between two of yours: that is a pincer.</div><div style="flex:1"></div><button class="btn primary" data-act="drill">' + (state.drill < 5 ? "Clear drill" : "Finish drills") + '</button><button class="btn ghost" data-go="grad">Skip all drills</button></div>';
      }
    },
    grad: {
      t: "Graduation", refs: "BIBLE 11.2.1 (Not yet built). GG-T1315.",
      spec: "One results-style card: the drills cleared, what was learned (five lines), a small starter reward, then Continue to Camp. Sets the save's onboarding flag so the drills never auto-play again.",
      go: function () { return ["camp"]; },
      html: function () { return '<div class="hdr">Drills complete</div><div class="pad">' + DRILLS.map(function (d) { return '<div class="pane"><p>&#10003; ' + d + "</p></div>"; }).join("") + '<div class="pane"><h3>Starter supplies</h3><p><span class="tok">+200 Gold</span> &middot; 3 Potions</p></div><div style="flex:1"></div><button class="btn primary" data-go="camp">Continue to Camp</button></div>'; }
    },
    camp: {
      t: "Camp, first visit (stub)", stub: true, refs: "Hub UX slice (GG-T1325, GG-T1343, GG-T1348); GG-T174 camp unlocks.",
      spec: "Coordination point. The first Camp visit runs the hub's first-visit lesson, owned by the hub UX slice: it points at the Campaign card first. The front door ends here.",
      go: function () { return ["menu"]; },
      html: function () { return '<div class="hdr">Camp <small>' + esc(state.name) + '</small></div><div class="pad"><div class="pane glow"><h3>Campaign</h3><p>Next: Tithe Moors I</p></div><div class="coach">Your road starts here. (hub first-visit lesson, hub UX slice)</div><div class="tiles"><div class="pane"><h3>Crucible</h3><p>locked</p></div><div class="pane"><h3>Camp</h3><p>heroes, shops</p></div></div><div class="stub"><b>Hub lesson</b>Owned by the hub UX slice.</div><button class="btn ghost" data-go="menu">Save &amp; Quit</button></div>'; }
    },
    about: {
      t: "About", refs: "BIBLE 11.2 AboutScreen (Not yet built). GG-T1324, GG-T1330.",
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

  var ORDER = ["splash", "profile", "menu", "start", "crawl", "team", "drill", "grad", "camp", "about", "credits"];
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
    else if (a === "step3") state.step = 3;
    else if (a === "done") { state.firstRun = false; $("firstRun").checked = false; state.step = 1; return show("menu"); }
    else if (a === "page") { if (state.crawl < CRAWL_PAGES) state.crawl++; else { state.crawl = 1; return show("team"); } }
    else if (a === "drills") { state.drill = 1; return show("drill"); }
    else if (a === "drill") { if (state.drill < 5) state.drill++; else return show("grad"); }
    show(current);
  }

  $("flow").innerHTML = ORDER.map(function (id) { return '<button type="button" data-go="' + id + '"' + (S[id].stub ? ' class="stub"' : "") + ">" + S[id].t + "</button>"; }).join("");
  document.addEventListener("click", function (e) {
    var t = e.target.closest("[data-go],[data-act],[data-speed]");
    if (!t) return;
    if (t.dataset.speed) { state.speed = t.dataset.speed; return show(current); }
    if (t.dataset.act) return act(t.dataset.act);
    var go = t.dataset.go;
    if (go === "crawl") state.crawl = 1;
    if (go === "drill") state.drill = 1;
    show(go);
  });
  document.addEventListener("input", function (e) {
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
