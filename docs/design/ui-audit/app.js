(function () {
  var d = window.UiAudit;
  if (!d) return;
  var SEVS = ["high", "medium", "low"];
  var SEV_MEANS = {
    high: "Blocks reading, reaching or seeing: thumb zone, clipping, text size and contrast, safe areas, outcome preview",
    medium: "Breaks a placement, efficiency or clarity rule, or an owner ask",
    low: "Capture tooling or polish"
  };
  var AREA = { hub: "Front door and hub", shops: "Shops and party", battle: "Battle" };
  var REVIEW = { reviewed: "reviewed", "not-reviewed": "not reviewed", "wrong-screen": "wrong screen", skipped: "skipped" };

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }
  function ticketStatus(id) {
    var t = d.tickets[id];
    if (!t) return "open";
    return t.status === "closed" ? "fixed" : "open";
  }

  var byTicket = {};
  d.findings.forEach(function (f) {
    f.state = ticketStatus(f.ticket);
    byTicket[f.ticket] = f;
  });

  function screenFindings(s) {
    return s.findings.map(function (id) {
      return byTicket[id] || { ticket: id, rule: "", ruleIds: [], severity: "medium", summary: (d.tickets[id] || {}).name || "", state: ticketStatus(id) };
    });
  }

  // Summary
  var tiles = document.getElementById("tiles");
  function tile(n, label, frac) {
    var t = el("div", "tile");
    t.appendChild(el("div", "n", String(n)));
    t.appendChild(el("div", "l", label));
    if (frac != null) {
      var b = el("div", "bar"), i = el("i");
      i.style.width = Math.round(frac * 100) + "%";
      b.appendChild(i);
      t.appendChild(b);
    }
    tiles.appendChild(t);
  }
  var fixed = d.findings.filter(function (f) { return f.state === "fixed"; }).length;
  var reviewed = d.screens.filter(function (s) { return s.review === "reviewed"; }).length;
  var clean = d.screens.filter(function (s) { return s.review === "reviewed" && s.findings.length === 0; }).length;
  tile(d.findings.length, "Findings", fixed / Math.max(1, d.findings.length));
  tile(fixed, "Fixed");
  tile(d.findings.length - fixed, "Open");
  tile(d.findings.filter(function (f) { return f.state === "open" && f.severity === "high"; }).length, "Open high");
  tile(d.screens.length, "Screens");
  tile(reviewed, "Reviewed");
  tile(clean, "Reviewed clean");
  tile(Object.keys(d.devices).length, "Devices");

  var sevRows = document.getElementById("sevRows");
  SEVS.forEach(function (sev) {
    var fs = d.findings.filter(function (f) { return f.severity === sev; });
    var fx = fs.filter(function (f) { return f.state === "fixed"; }).length;
    var tr = el("tr");
    var name = el("td");
    name.appendChild(el("span", "sev " + sev, sev));
    tr.appendChild(name);
    tr.appendChild(el("td", "n", String(fx)));
    tr.appendChild(el("td", "n", String(fs.length - fx)));
    tr.appendChild(el("td", "n", String(fs.length)));
    tr.appendChild(el("td", "muted", SEV_MEANS[sev]));
    sevRows.appendChild(tr);
  });

  // Screen filter options grouped by area
  var screenSel = document.getElementById("screenSel");
  screenSel.appendChild(new Option("All screens", "all"));
  Object.keys(AREA).forEach(function (a) {
    var g = el("optgroup");
    g.label = AREA[a];
    d.screens.filter(function (s) { return s.area === a; }).forEach(function (s) {
      var n = screenFindings(s).filter(function (f) { return f.state === "open"; }).length;
      g.appendChild(new Option(s.name + (n ? " (" + n + " open)" : ""), s.name));
    });
    if (g.children.length) screenSel.appendChild(g);
  });

  // Facts
  function factRow(dl, k, v, bad) {
    dl.appendChild(el("dt", "", k));
    var dd = el("dd", bad ? "bad" : "");
    if (v instanceof Node) dd.appendChild(v); else dd.textContent = v;
    dl.appendChild(dd);
  }
  function renderFacts(box, s, dev) {
    box.textContent = "";
    var f = d.devices[dev] && d.devices[dev].screens[s.name];
    if (!f) { box.appendChild(el("p", "muted", "No measured facts for this capture (harness frame only).")); return; }
    var det = el("details", "facts");
    var problems = f.taps[1] + f.taps[2] + f.contrast[1] + f.safeIntrusions + f.layoutProblems.length + Object.keys(f.polishFails).length;
    det.appendChild(el("summary", "", "Measured facts on " + dev + (problems ? " (" + problems + " flags)" : " (no flags)")));
    var dl = el("dl");
    if (f.landedOn && f.landedOn !== s.name) factRow(dl, "Landed on", f.landedOn, s.name.indexOf(f.landedOn) !== 0);
    factRow(dl, "Tap targets", f.taps[0] + " checked, " + f.taps[1] + " under 44 pt, " + f.taps[2] + " tight gaps", f.taps[1] > 0);
    factRow(dl, "Text", f.textCount + " labels, smallest " + (f.minText || "n/a"));
    factRow(dl, "Contrast", f.contrast[1] + " of " + f.contrast[0] + " samples fail" + (f.contrast[2] != null ? ", lowest " + f.contrast[2] + ":1" : ""), f.contrast[1] > 0);
    f.lowContrast.forEach(function (c) {
      var span = el("span");
      var sw = el("span", "sw"); sw.style.background = c[3];
      var sw2 = el("span", "sw"); sw2.style.background = c[4];
      span.appendChild(sw); span.appendChild(document.createTextNode(" on ")); span.appendChild(sw2);
      span.appendChild(document.createTextNode(" " + c[1] + ":1 (needs " + c[2] + ") “" + c[0] + "”"));
      factRow(dl, "", span);
    });
    factRow(dl, "Safe area", f.safeIntrusions ? f.safeIntrusions + " intrusions" : "clear", f.safeIntrusions > 0);
    var pf = Object.keys(f.polishFails);
    factRow(dl, "Polish lint", pf.length ? pf.map(function (r) { return r + " ×" + f.polishFails[r]; }).join(", ") : "no fails", pf.length > 0);
    f.layoutProblems.forEach(function (p, i) { factRow(dl, i ? "" : "Layout", p, true); });
    det.appendChild(dl);
    box.appendChild(det);
  }

  function findingItem(f) {
    var li = el("li", "finding sev-" + f.severity);
    var meta = el("div", "meta");
    meta.appendChild(el("span", "tix", f.ticket));
    meta.appendChild(el("span", "st-" + f.state, f.state));
    meta.appendChild(el("span", "sev " + f.severity, f.severity));
    if (f.rule) {
      var r = el("span", "rule", f.rule);
      var names = f.ruleIds.map(function (id) { return d.rules[id] ? id + " " + d.rules[id] : null; }).filter(Boolean);
      if (names.length) r.title = names.join("\n");
      meta.appendChild(r);
    }
    var t = d.tickets[f.ticket];
    if (t && t.name) meta.appendChild(el("span", "muted", t.name));
    li.appendChild(meta);
    if (f.summary) li.appendChild(el("p", "sum", f.summary));
    if (f.detail) {
      var det = el("details");
      det.appendChild(el("summary", "", "Detail"));
      det.appendChild(el("p", "det", f.detail));
      li.appendChild(det);
    }
    if (f.state === "fixed") {
      var ref = t && t.ref && /^[0-9a-f]{7,}$/.test(t.ref) ? "Closed at " + t.ref.slice(0, 8) : "Closed";
      li.appendChild(el("p", "ver", ref + (f.verified ? "; verified by " + f.verified : "")));
    }
    return li;
  }

  // Screens
  var host = document.getElementById("screens");
  var cards = d.screens.map(function (s) {
    var card = el("article", "screen");
    card.id = "s-" + s.name;
    var shot = el("div", "shot"), body = el("div", "body"), facts = el("div");
    var fs = screenFindings(s);

    var dev = s.devices[0];
    function showDevice(dv) {
      dev = dv;
      shot.querySelectorAll(".devs button").forEach(function (b) { b.setAttribute("aria-pressed", String(b.dataset.dev === dv)); });
      var old = shot.querySelector(".img");
      if (old) old.remove();
      var wrap;
      if (dv) {
        var src = "captures/" + dv + "/" + s.name + ".png";
        wrap = el("a", "img"); wrap.href = src; wrap.target = "_blank"; wrap.rel = "noopener";
        var img = el("img"); img.src = src; img.loading = "lazy"; img.alt = s.name + " on " + dv;
        wrap.appendChild(img);
      } else {
        wrap = el("div", "img none", "No capture");
      }
      shot.insertBefore(wrap, shot.firstChild);
      renderFacts(facts, s, dv);
    }
    if (s.devices.length > 1) {
      var devs = el("div", "devs");
      s.devices.forEach(function (dv) {
        var b = el("button", "", dv); b.type = "button"; b.dataset.dev = dv;
        b.addEventListener("click", function () { showDevice(dv); });
        devs.appendChild(b);
      });
      shot.appendChild(devs);
    } else if (s.devices.length) {
      shot.appendChild(el("div", "muted", s.devices[0]));
    }

    var head = el("div", "head");
    head.appendChild(el("h3", "", s.name));
    head.appendChild(el("span", "chip", AREA[s.area] || s.area));
    head.appendChild(el("span", "chip" + (s.review === "reviewed" ? "" : " warn"), REVIEW[s.review] || s.review));
    var open = fs.filter(function (f) { return f.state === "open"; }).length;
    if (fs.length) head.appendChild(el("span", "chip" + (open ? " warn" : " ok"), open ? open + " open of " + fs.length : "all " + fs.length + " fixed"));
    body.appendChild(head);
    s.notes.forEach(function (n) { body.appendChild(el("p", "note", n)); });
    var list = el("ul", "findings");
    var items = fs.map(function (f) { var li = findingItem(f); list.appendChild(li); return { f: f, li: li }; });
    body.appendChild(list);
    var none = el("p", "clean", s.review === "reviewed" ? "No findings: passes the checklist." : "No findings recorded.");
    body.appendChild(none);
    body.appendChild(facts);

    card.appendChild(shot);
    card.appendChild(body);
    host.appendChild(card);
    showDevice(dev);
    return { s: s, card: card, items: items, none: none };
  });

  // Notes
  var notes = document.getElementById("notes");
  if (d.falsePositives.length) {
    notes.appendChild(el("h3", "", "Not filed (false positives)"));
    var ul = el("ul");
    d.falsePositives.forEach(function (fp) { ul.appendChild(el("li", "", fp.rule + " on " + fp.screens.join(", ") + ": " + fp.note)); });
    notes.appendChild(ul);
  }
  if (d.alreadyFiled.length) {
    notes.appendChild(el("h3", "", "Filed before the battle review"));
    var ul2 = el("ul");
    d.alreadyFiled.forEach(function (id) {
      var t = d.tickets[id] || {};
      var li = el("li");
      li.appendChild(el("span", "tix", id + " "));
      li.appendChild(el("span", "st-" + ticketStatus(id), ticketStatus(id)));
      li.appendChild(document.createTextNode(" " + (t.name || "")));
      ul2.appendChild(li);
    });
    notes.appendChild(ul2);
  }
  notes.appendChild(el("h3", "", "Sources"));
  var ul3 = el("ul");
  d.reviews.forEach(function (r) {
    ul3.appendChild(el("li", "", r.file + ": review " + r.ticket + ", capture " + r.capture + (r.recheck ? "; rechecked by " + r.recheck.ticket + " on " + r.recheck.capture : "")));
  });
  Object.keys(d.devices).forEach(function (dv) { ul3.appendChild(el("li", "", "captures/" + dv + "/facts.json, captured " + d.devices[dv].captured)); });
  notes.appendChild(ul3);

  // Filters
  var state = { status: "all", screen: "all", sev: "all", hideClean: false };
  try {
    var saved = JSON.parse(localStorage.getItem("uiAuditFilters") || "null");
    if (saved) Object.keys(state).forEach(function (k) { if (k in saved) state[k] = saved[k]; });
  } catch (e) { }
  var seg = document.getElementById("statusSeg"), sevSel = document.getElementById("sevSel"), hideClean = document.getElementById("hideClean");

  function apply() {
    try { localStorage.setItem("uiAuditFilters", JSON.stringify(state)); } catch (e) { }
    seg.querySelectorAll("button").forEach(function (b) { b.setAttribute("aria-pressed", String(b.dataset.v === state.status)); });
    screenSel.value = state.screen; sevSel.value = state.sev; hideClean.checked = state.hideClean;
    var shownCards = 0, shownFindings = 0;
    cards.forEach(function (c) {
      var visible = 0;
      c.items.forEach(function (it) {
        var ok = (state.status === "all" || it.f.state === state.status) && (state.sev === "all" || it.f.severity === state.sev);
        it.li.hidden = !ok;
        if (ok) visible++;
      });
      var filtering = state.status !== "all" || state.sev !== "all";
      var show = (state.screen === "all" || c.s.name === state.screen) && !(state.hideClean && visible === 0) && !(filtering && visible === 0 && state.screen === "all");
      c.card.hidden = !show;
      c.none.hidden = c.items.length > 0 && visible > 0;
      if (c.items.length > 0) c.none.textContent = "No findings match the filters.";
      if (show) { shownCards++; shownFindings += visible; }
    });
    document.getElementById("shown").textContent = shownCards + " screens, " + shownFindings + " findings shown";
  }
  seg.addEventListener("click", function (e) { var v = e.target.dataset && e.target.dataset.v; if (v) { state.status = v; apply(); } });
  screenSel.addEventListener("change", function () { state.screen = screenSel.value; apply(); });
  sevSel.addEventListener("change", function () { state.sev = sevSel.value; apply(); });
  hideClean.addEventListener("change", function () { state.hideClean = hideClean.checked; apply(); });
  apply();
})();
