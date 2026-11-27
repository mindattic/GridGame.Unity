// Renders the hub navigation model from window.HUB_NAV and runs a small stack simulator of its rules.
(function () {
  "use strict";
  var D = window.HUB_NAV;
  var byId = {};
  D.screens.forEach(function (s) { byId[s.id] = s; });

  function el(tag, cls, text, parent) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    if (parent) parent.appendChild(e);
    return e;
  }

  // Graphs
  document.getElementById("graph-old").textContent = D.currentGraph.join("\n");
  document.getElementById("graph-new").textContent = D.newGraph.join("\n");

  // Pillars
  var pillars = document.getElementById("pillars");
  D.pillars.forEach(function (p) {
    var card = el("article", "pillar pillar-" + p.id, null, pillars);
    el("h3", null, p.name, card);
    el("p", null, p.body, card);
    var ul = el("ul", "chips", null, card);
    p.items.forEach(function (i) { el("li", null, i, ul); });
  });

  // Rules
  var rules = document.getElementById("rules");
  D.rules.forEach(function (r) {
    el("dt", null, r[0], rules);
    el("dd", null, r[1], rules);
  });

  // Screen table, with a group filter
  var groups = [];
  D.screens.forEach(function (s) { if (groups.indexOf(s.group) < 0) groups.push(s.group); });
  var filter = document.getElementById("group-filter");
  el("option", null, "All groups (" + D.screens.length + ")", filter).value = "";
  groups.forEach(function (g) { el("option", null, g, filter).value = g; });
  var tbody = document.getElementById("screens");
  function renderTable() {
    tbody.textContent = "";
    D.screens.forEach(function (s) {
      if (filter.value && s.group !== filter.value) return;
      var tr = el("tr", null, null, tbody);
      var name = el("td", null, null, tr);
      el("b", null, s.name, name);
      el("div", "cls", s.cls, name);
      el("td", null, s.group, tr);
      el("td", "mono", s.path, tr);
      var nav = el("td", s.nav ? "yes" : "no", s.nav ? "Yes" : "No", tr);
      nav.style.textAlign = "center";
      el("td", null, s.back, tr);
    });
  }
  filter.addEventListener("change", renderTable);
  renderTable();

  // BIBLE drafts and stories
  var bible = document.getElementById("bible");
  D.bible.forEach(function (b) {
    var d = el("div", "draft", null, bible);
    el("h3", null, b.sec, d);
    el("p", null, b.text, d);
  });
  var stories = document.getElementById("stories");
  D.stories.forEach(function (s) {
    var tr = el("tr", null, null, stories);
    el("td", "mono", "US-new", tr);
    el("td", null, s[0], tr);
    el("td", "mono", "§" + s[1], tr);
    el("td", null, "planned", tr);
  });

  // Simulator
  var RUN = { storycrawl: 1, battle: 1 };
  var stack = ["hub"];
  var log = [];
  var pending = null;
  var phone = document.getElementById("phone");

  function top() { return stack[stack.length - 1]; }
  function hubRooted() { return stack[0] === "hub"; }
  function pages() { return stack.filter(function (id) { return id !== "dialogue"; }).length; }
  function say(msg) { log.unshift(msg); if (log.length > 6) log.pop(); }

  function open(id) {
    var s = byId[id];
    if (id === "continue") { stack = ["hub", "storycrawl"]; say("Continue: Replace above Hub with the stage's story crawl"); return; }
    if (id === "hub") { stack = ["hub"]; say("ResetTo(Hub)"); return; }
    if (RUN[id] && hubRooted()) {
      if (top() === "postbattle" && id === "battle") say("Retry / Next stage: Replace the run with Battle");
      else say("Launch run: Replace everything above Hub with " + s.name);
      stack = ["hub", id];
      return;
    }
    if (id === "prologue") { stack = ["prologue"]; say("New game: the Prologue replaces the stack"); return; }
    if (id === "levelup" || id === "postbattle") { stack = ["hub", "battle", id]; say("Results over Battle"); return; }
    if (hubRooted() && id !== "dialogue" && pages() >= 3) {
      stack[stack.length - 1] = id;
      say("Push would make a 4th entry: Replace top with " + s.name);
      return;
    }
    stack.push(id);
    say("Push " + s.name);
  }

  function back() {
    var t = top();
    if (t === "hub") { pending = "title"; say("Back at the root: \"Return to the title?\""); return; }
    if (t === "title") { say("Back at Title exits the app"); return; }
    if (t === "battle") { say("Battle swallows Back: opens Pause"); return; }
    if (t === "storycrawl" || t === "prologue") { say("Story crawl swallows Back"); return; }
    if (t === "postbattle" || t === "levelup") { stack = ["hub"]; say("Back = Continue: ResetTo(Hub)"); return; }
    stack.pop();
    say("Back: pop to " + byId[top()].name);
  }

  function tab(t) {
    if (t.target == null) { stack = ["hub"]; say("Home: ResetTo(Hub)"); return; }
    stack = ["hub", t.target];
    say(t.label + " tab: ResetTo(Hub) + Push(" + byId[t.target].name + ")");
  }

  function litTab(s) {
    if (s.id === "hub") return "home";
    return D.tabFor[s.group] || "home";
  }

  function render() {
    phone.textContent = "";
    var s = byId[top()];
    var crumbs = el("div", "stack", null, phone);
    stack.forEach(function (id, i) {
      if (i) el("span", "sep", "›", crumbs);
      el("span", "crumb", byId[id].name, crumbs);
    });
    var screen = el("div", "screen", null, phone);
    var head = el("div", "head", null, screen);
    var b = el("button", "hbtn", "‹ Back", head);
    b.addEventListener("click", function () { back(); render(); });
    el("h3", null, s.name, head);
    if (s.nav) {
      var h = el("button", "hbtn", "Home", head);
      h.addEventListener("click", function () { stack = ["hub"]; say("Header Home: ResetTo(Hub)"); render(); });
    }
    el("div", "cls", s.cls, screen);
    if (pending === "title") {
      var c = el("div", "confirm", null, screen);
      el("p", null, "Return to the title?", c);
      var yes = el("button", "btn", "Yes", c);
      var no = el("button", "btn ghost", "No", c);
      yes.addEventListener("click", function () { pending = null; stack = ["title"]; say("Persist, ResetTo(Title)"); render(); });
      no.addEventListener("click", function () { pending = null; say("Stay on the hub"); render(); });
    }
    var links = el("div", "links", null, screen);
    if (s.id === "hub") {
      D.pillars.forEach(function (p) {
        el("div", "plabel", p.name, links);
        var row = el("div", "lrow", null, links);
        var ids = p.id === "campaign" ? ["continue", "stageselect"] : p.id === "crucible" ? ["crucible"] : ["hero", "summon", "shop", "craft", "journal", "settings"];
        ids.forEach(function (id) { link(id, row); });
      });
    } else {
      var row = el("div", "lrow", null, links);
      s.links.forEach(function (id) { link(id, row); });
      if (!s.links.length) el("span", "m", "No onward links.", row);
    }
    if (s.nav) {
      var nav = el("nav", "bottom", null, screen);
      var lit = litTab(s);
      D.tabs.forEach(function (t) {
        var tb = el("button", t.id === lit ? "tab on" : "tab", t.label, nav);
        tb.addEventListener("click", function () { tab(t); render(); });
      });
    } else el("div", "nonav", "No bottom nav on this screen", screen);
    var lg = el("ol", "log", null, phone);
    log.forEach(function (m) { el("li", null, m, lg); });
    document.getElementById("depth").textContent = "Stack depth " + stack.length + (hubRooted() ? " (Hub + " + (stack.length - 1) + ")" : "");
  }

  function link(id, parent) {
    var s = byId[id];
    var a = el("button", "btn small", s.name, parent);
    a.addEventListener("click", function () { open(id); render(); });
  }

  document.getElementById("reset").addEventListener("click", function () { stack = ["hub"]; log = []; pending = null; render(); });
  document.getElementById("fromtitle").addEventListener("click", function () { stack = ["title"]; log = []; pending = null; render(); });
  render();
})();
