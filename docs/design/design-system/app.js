// Renders the design system page from window.DS (GG-T2044). No fetch, so it opens from file://.
(function () {
  "use strict";
  var D = window.DS, G = D.generated, L = D.links;

  function el(tag, cls, text, parent) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    if (parent) parent.appendChild(e);
    return e;
  }
  function $(id) { return document.getElementById(id); }
  var SVGNS = "http://www.w3.org/2000/svg";
  function svg(tag, attrs, parent) {
    var e = document.createElementNS(SVGNS, tag);
    for (var k in attrs) e.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(e);
    return e;
  }
  function table(t, head, rows) {
    t.textContent = "";
    var tr = el("tr", null, null, el("thead", null, null, t));
    head.forEach(function (h) { el("th", null, h, tr); });
    var tb = el("tbody", null, null, t);
    rows.forEach(function (r) {
      var row = el("tr", null, null, tb);
      r.forEach(function (c) {
        var td = el("td", null, null, row);
        if (c && c.code) el("code", null, c.code, td); else td.textContent = c;
      });
    });
    return tb;
  }

  // Links
  $("l-spec").href = L.spec;
  [["DESIGN_SYSTEM.md", L.spec], ["BIBLE", L.bible], ["UX review (GG-T1940)", L.review],
   [L.copy.label, L.copy.md], ["Copy guide page", L.copy.page], [L.icons.label, L.icons.md], ["Icon set page", L.icons.page]]
    .forEach(function (p) { var a = el("a", null, p[0], $("links")); a.href = p[1]; });

  // ── Mock builders (1 design px = 1/3 CSS px) ──
  function icon(kind, size) {
    var s = svg("svg", { viewBox: "0 0 24 24", width: size || 22, height: size || 22, "aria-hidden": "true" });
    var paths = {
      menu: "M3 5.5h18v3H3zM3 10.5h18v3H3zM3 15.5h18v3H3z",
      back: "M10.5 4.5 3 12l7.5 7.5 2.1-2.1L8.7 13.5H21v-3H8.7l3.9-3.9z",
      home: "M12 3 2 11.5h3V21h5.5v-6h3v6H19v-9.5h3z",
      hand: "M8 21v-6.5L5 11.6l1.6-1.4L9 12V3.5a1.5 1.5 0 0 1 3 0V10l6 1.2c1 .2 1.7 1.1 1.6 2.1L19 21z"
    };
    svg("path", { d: paths[kind], fill: "currentColor", stroke: "#0E0E18", "stroke-width": "1", "paint-order": "stroke" }, s);
    return s;
  }
  function btn(text, cls, parent) { var b = el("button", "mp-btn " + (cls || ""), text, parent); b.type = "button"; return b; }
  function iconBtn(kind, parent) { var b = btn("", "icon", parent); b.appendChild(icon(kind)); b.setAttribute("aria-label", kind); return b; }
  function row(label, value, cls, parent) {
    var r = el("div", "mp-row " + (cls || ""), null, parent);
    el("i", "ic", null, r); el("span", "lb", label, r);
    if (value) el("span", "val", value, r);
    return r;
  }
  function bar(left, title, parent, home) {
    var b = el("div", "mp-bar mp", null, parent);
    iconBtn(left, b);
    if (home) iconBtn("home", b);
    el("span", "ttl", title, b);
    el("span", "gold", "1,250 G", b);
    return b;
  }
  function thumbCard(parent, name, sub, cls) {
    var c = el("div", "mp-card mp " + (cls || ""), null, parent);
    var th = el("i", "th", null, c);
    th.style.backgroundImage = "url('" + L.thumb + "')";
    var col = el("div", null, null, c);
    el("div", null, name, col);
    el("div", null, sub, col).style.color = "var(--mp-dim)";
    return c;
  }

  // Arrows: a ribbon polygon around a centreline, head part of the same outline (as Shapes.cs builds them).
  function ribbon(pts, w, head) {
    var n = pts.length, L1 = [], R1 = [];
    var end = pts[n - 1], prev = pts[n - 2];
    var dx = end[0] - prev[0], dy = end[1] - prev[1], len = Math.hypot(dx, dy) || 1;
    var ux = dx / len, uy = dy / len;
    var base = [end[0] - ux * head, end[1] - uy * head];
    var line = pts.slice(0, n - 1).concat([base]);
    for (var i = 0; i < line.length; i++) {
      var a = line[Math.max(0, i - 1)], b = line[Math.min(line.length - 1, i + 1)];
      var tx = b[0] - a[0], ty = b[1] - a[1], tl = Math.hypot(tx, ty) || 1;
      var nx = -ty / tl * w / 2, ny = tx / tl * w / 2;
      L1.push([line[i][0] + nx, line[i][1] + ny]); R1.push([line[i][0] - nx, line[i][1] - ny]);
    }
    var hw = w * 1.4, nx2 = -uy * hw, ny2 = ux * hw;
    var poly = L1.concat([[base[0] + nx2, base[1] + ny2], end, [base[0] - nx2, base[1] - ny2]]).concat(R1.reverse());
    return "M" + poly.map(function (p) { return p[0].toFixed(1) + " " + p[1].toFixed(1); }).join("L") + "Z";
  }
  function cubic(p0, p1, p2, p3, steps) {
    var out = [];
    for (var i = 0; i <= steps; i++) {
      var t = i / steps, m = 1 - t;
      out.push([m * m * m * p0[0] + 3 * m * m * t * p1[0] + 3 * m * t * t * p2[0] + t * t * t * p3[0],
                m * m * m * p0[1] + 3 * m * m * t * p1[1] + 3 * m * t * t * p2[1] + t * t * t * p3[1]]);
    }
    return out;
  }
  function ortho(a, corner, b, r) {
    var out = [a], dx1 = Math.sign(corner[0] - a[0]), dy1 = Math.sign(corner[1] - a[1]);
    var dx2 = Math.sign(b[0] - corner[0]), dy2 = Math.sign(b[1] - corner[1]);
    var s = [corner[0] - dx1 * r, corner[1] - dy1 * r], e = [corner[0] + dx2 * r, corner[1] + dy2 * r];
    return out.concat(cubic(s, corner, corner, e, 8)).concat([b]);
  }
  function arrowPath(parent, d, fill) {
    svg("path", { d: d, fill: fill, stroke: "#0E0E18", "stroke-width": 3, "stroke-linejoin": "round", "paint-order": "stroke" }, parent);
  }

  // ── Rules ──
  var demos = {
    corners: function (d) { bar("menu", "Camp", d); el("div", "m", "Hub and battle: hamburger top-left, gold top-right.", d); },
    back: function (d) { bar("back", "Equip", d); bar("back", "Blacksmith", d, true); el("div", "m", "Back top-left; Home after it only above the hub + 1.", d); },
    confirm: function (d) { d.classList.add("mp"); row("Elixir", "120 G", "selected", d); row("Ether", "60 G", "", d); },
    cursor: function (d) {
      d.classList.add("mp");
      el("div", "tag ok", "phone", d); row("Stage select", null, "", d);
      el("div", "tag", "PC build only", d);
      var r = row("Stage select", null, "", d); var h = el("span", "hand", null); h.appendChild(icon("hand", 19)); h.style.color = "#fff"; r.insertBefore(h, r.firstChild);
    },
    trunc: function (d) {
      d.classList.add("mp");
      row("Judgment of Warding", "1,200 G", "", d);
      var bad = el("div", "tag no", "do not", d);
      var r = row("Judgment of Wa…", "1,2", "", d); r.style.opacity = ".5"; r.style.textDecoration = "line-through";
      bad.title = "ellipsis and a clipped value";
    },
    arrows: function (d) {
      var s = svg("svg", { viewBox: "0 0 300 120", width: "100%" }, d);
      arrowPath(s, ribbon(cubic([20, 100], [60, 10], [140, 10], [170, 70], 28), 9, 18), "#7590C8");
      arrowPath(s, ribbon(ortho([200, 20], [200, 90], [285, 90], 14), 9, 18), "#FFD959");
      var t = svg("text", { x: 20, y: 117, fill: "#9097a8", "font-size": 11, "font-family": "IBM Plex Mono, monospace" }, s);
      t.textContent = "BezierArrow · OrthoArrow: filled, outlined";
      var bad = el("div", null, null, d); el("span", "tag no", "do not", bad);
      var s2 = svg("svg", { viewBox: "0 0 300 30", width: "100%" }, bad);
      svg("line", { x1: 10, y1: 15, x2: 260, y2: 15, stroke: "#7590C8", "stroke-width": 3 }, s2);
      var tt = svg("text", { x: 266, y: 21, fill: "#7590C8", "font-size": 18 }, s2); tt.textContent = "→";
    },
    target: function (d) {
      var r = el("div", null, null, d); r.style.cssText = "display:flex;gap:16px;align-items:center";
      var t = el("div", null, null, r); t.style.cssText = "width:44px;height:44px;border:2px dashed #7590C8;display:grid;place-items:center;font:11px var(--mono);color:#9097a8"; t.textContent = "132";
      var g = el("div", null, null, r); g.style.cssText = "width:8px;height:44px;background:repeating-linear-gradient(45deg,#2a2f3d 0 3px,transparent 3px 6px)"; g.title = "24 px gap";
      var t2 = el("div", null, null, r); t2.style.cssText = "width:44px;height:44px;border:2px dashed #7590C8;";
      var no = el("div", null, null, r); el("span", "tag no", "do not", no); var dot = el("i", null, null, no); dot.style.cssText = "display:inline-block;width:7px;height:7px;border-radius:50%;background:#7590C8;margin-left:8px";
      el("div", "m", "132 × 132 design px with 24 px between. A 20-px node is not a target.", d);
    },
    type: function (d) {
      d.classList.add("mp");
      el("div", null, "Base 40 px: every label", d);
      var f = el("div", null, "Floor 30 px: the smallest anything gets", d); f.style.fontSize = "calc(30 * var(--u))"; f.style.color = "var(--mp-dim)";
    },
    thumb: function (d) {
      var c = thumbCard(d, "Knight", "Tap for full art");
      c.style.cursor = "pointer";
      c.addEventListener("click", function () { openArt(); });
    }
  };
  D.rules.forEach(function (r) {
    var c = el("div", "card rule", null, $("rule-grid"));
    var h = el("div", null, null, c); el("span", "rid", r.id + " ", h); el("h3", null, r.title, h).style.display = "inline";
    el("p", null, r.body, c);
    var demo = el("div", "demo", null, c);
    demos[r.demo](demo);
    var ref = el("div", "ref", null, c); ref.textContent = r.ref + " · "; el("code", null, r.code, ref);
  });

  function openArt() {
    var v = el("div", null, null, document.body);
    v.style.cssText = "position:fixed;inset:0;background:rgba(5,5,12,.72);display:grid;place-items:center;z-index:10;cursor:pointer";
    var img = el("img", null, null, v);
    img.src = L.thumb.replace("/Thumbs/", "/Portraits/");
    img.alt = "Full art";
    img.style.cssText = "max-width:min(90vw,520px);max-height:90vh";
    img.onerror = function () { img.replaceWith(el("div", "mp", "Full art (Portraits/ not found from this path)")); };
    v.addEventListener("click", function () { v.remove(); });
  }

  // ── Colour ──
  function swatch(parent, hex, alpha, name, sub) {
    var s = el("div", "sw", null, parent);
    var i = el("i", null, null, s);
    i.style.background = hex; if (alpha != null && alpha < 1) i.style.opacity = alpha;
    var t = el("div", null, null, s);
    el("b", null, name, t);
    el("span", null, hex + (alpha != null && alpha < 1 ? " / " + alpha : "") + " · " + sub, t);
  }
  D.roles.forEach(function (r) { swatch($("roles"), r.hex, r.alpha, r.role, r.token + " · " + r.use); });
  G.colors.forEach(function (c) { swatch($("uss"), c.hex, c.alpha, c.var, "Theme.uss :root"); });

  // ── Space, size ──
  G.space.forEach(function (s) {
    var b = el("div", "bar", null, $("space-bars"));
    el("span", null, s + " px", b);
    var i = el("i", null, null, b); i.style.width = (s * 2) + "px";
  });
  var st = $("size-table"); st.parentNode.style.maxHeight = "320px";
  table(st, ["Size", "design px"], G.size.map(function (x) { return [{ code: "Size." + x[0] }, x[1]]; }));

  // ── Radii, elevation ──
  D.radii.forEach(function (r) {
    var x = el("div", "radius", null, $("radii-list"));
    el("i", null, null, x).style.borderRadius = (r.px / 3 * 2) + "px";
    el("span", null, r.px + " · " + r.what, x);
  });
  D.elevation.forEach(function (e) {
    var x = el("div", null, null, $("elev")); x.style.setProperty("--l", e.level);
    el("b", null, String(e.level), x); el("span", null, e.layer, x); el("span", "m", e.holds + " · " + e.edge, x);
  });

  // ── Motion ──
  var mo = $("motion-rows");
  function mrow(label, dur, ease) {
    var r = el("div", "mrow", null, mo);
    el("span", null, label, r);
    var t = el("div", "track", null, r);
    var d = el("i", "dot", null, t);
    d.style.transitionDuration = dur + "s"; d.style.transitionTimingFunction = ease;
  }
  G.timing.filter(function (t) { return ["Instant", "Quick", "Standard", "Emphasis", "Dramatic", "Cinematic"].indexOf(t[0]) >= 0; })
    .forEach(function (t) { mrow("Timing." + t[0] + " " + t[1], t[1], D.eases[0].css); });
  D.eases.forEach(function (e) { mrow("Ease." + e.name, 0.7, e.css); });
  mo.addEventListener("click", function () { mo.classList.toggle("go"); });

  // ── Components ──
  var states = [["default", ""], ["pressed", "is-pressed"], ["selected-pressed", "selected"], ["disabled", "disabled"]];
  function mock(kind, cls, parent) {
    if (kind === "btn") return btn(cls.indexOf("danger") >= 0 ? "Delete" : cls.indexOf("primary") >= 0 ? "Buy" : "Sell", cls, parent);
    if (kind === "row") return row("Iron Sword", "300 G", cls, parent);
    if (kind === "card") { var c = thumbCard(parent, "Cleric", "Lv 4 · Healer", cls); c.style.width = "100%"; return c; }
    if (kind === "tab") { var t = el("div", "mp-tabs mp", null, parent); el("div", "mp-tab " + cls, "Gear", t); return t; }
    if (kind === "nav") { var n = el("div", "mp-nav mp", null, parent); n.style.width = "100%"; el("div", "mp-navtab " + cls, "Heroes", n); return n; }
  }
  D.components.forEach(function (c) {
    var card = el("div", "card comp", null, $("comp-grid"));
    el("h3", null, c.name, card);
    var s = el("div", "states", null, card);
    var parts = c.demo.split(" "), kind = parts[0], extra = parts[1] || "";
    states.forEach(function (st) {
      var cell = el("div", null, null, s);
      var nav = (kind === "tab" || kind === "nav");
      var label = st[0], cls = st[1];
      if (nav && st[0] === "selected-pressed") { label = "active (no confirm)"; cls = "active"; }
      el("span", "lab", label, cell);
      var host = el("div", "demo", null, cell); host.style.minHeight = "70px"; host.style.padding = "8px"; host.style.width = "100%";
      mock(kind, (extra + " " + cls).trim(), host);
    });
    var m = el("div", "meta", null, card);
    el("code", null, c.builder, m); m.appendChild(document.createTextNode(" · " + c.classes));
    el("div", "meta", "Select-then-confirm: " + c.confirm, card);
  });

  // Select-then-confirm demo (touch rules: nothing preselected; second tap on the selected item runs it)
  var cd = $("confirm-demo"), log = $("confirm-log");
  var pane = el("div", "mp-pane", null, cd);
  [["Elixir", "120 G", true], ["Phoenix Down", "300 G", true], ["Ether", "Sold out", false]].forEach(function (it) {
    var r = row(it[0], it[1], it[2] ? "" : "disabled", pane);
    r.addEventListener("click", function () {
      if (!it[2]) { log.textContent = "Disabled: no sink, no select. Toasts \"Sold out until tomorrow.\""; return; }
      if (r.classList.contains("selected")) {
        log.textContent = "ui.confirm: bought " + it[0] + ". Second tap ran it.";
        r.classList.remove("selected");
        var t = el("div", "mp-toast", "Bought " + it[0], cd); setTimeout(function () { t.remove(); }, 1800);
      } else {
        pane.querySelectorAll(".selected").forEach(function (x) { x.classList.remove("selected"); });
        r.classList.add("selected");
        log.textContent = "ui.select: " + it[0] + " is pressed in. Tap it again to buy, or tap another row.";
      }
    });
  });

  table($("kit-table"), ["Component", "Builder", "Look"], D.kit.map(function (k) { return [k[0], { code: k[1] }, k[2]]; }));
  table($("ov-table"), ["Surface", "Builder", "Shape", "Dismiss", "Confirm rule"],
    D.overlays.map(function (o) { return [o.name, { code: o.builder }, o.shape, o.dismiss, o.rule]; }));

  // Pressed state while held, on every mock
  document.addEventListener("pointerdown", function (e) {
    var t = e.target.closest(".mp-btn, .mp-row, .mp-card, .mp-tab, .mp-navtab");
    if (!t || t.classList.contains("disabled")) return;
    t.classList.add("is-pressed");
    function up() { t.classList.remove("is-pressed"); document.removeEventListener("pointerup", up); document.removeEventListener("pointercancel", up); }
    document.addEventListener("pointerup", up); document.addEventListener("pointercancel", up);
  });

  // ── Layout and thumb zone ──
  var ph = $("phone");
  function draw(s) {
    ph.textContent = "";
    var W = s.stage[0], H = s.stage[1];
    ph.setAttribute("viewBox", "-20 -20 " + (W + 40) + " " + (H + 40));
    svg("rect", { x: 0, y: 0, width: W, height: H, rx: 60, fill: "#0C0C16", stroke: "#60646F", "stroke-width": 6 }, ph);
    var safeB = H - s.safeBottom;
    svg("rect", { x: 0, y: 0, width: W, height: s.safeTop, fill: "#2a2f3d", opacity: .7 }, ph);
    svg("rect", { x: 0, y: safeB, width: W, height: s.safeBottom, fill: "#2a2f3d", opacity: .7 }, ph);
    svg("rect", { x: 0, y: s.thumbTop, width: W, height: safeB - s.thumbTop, fill: "#6fd6c4", opacity: .13 }, ph);
    svg("line", { x1: 0, y1: s.thumbTop, x2: W, y2: s.thumbTop, stroke: "#6fd6c4", "stroke-width": 5, "stroke-dasharray": "24 16" }, ph);
    svg("rect", { x: 0, y: s.safeTop, width: W, height: 150, fill: "#100F1C", stroke: "#7590C8", "stroke-width": 3 }, ph);
    svg("rect", { x: 0, y: safeB - 132, width: W, height: 132, fill: "#100F1C", stroke: "#60646F", "stroke-width": 3 }, ph);
    var colW = (W - 48 - 3 * 24) / 4;
    for (var i = 0; i < 4; i++) svg("rect", { x: 24 + i * (colW + 24), y: s.safeTop + 166, width: colW, height: safeB - 132 - 16 - s.safeTop - 166, fill: "#7590C8", opacity: .07 }, ph);
    svg("rect", { x: 24, y: s.safeTop + 24, width: 132, height: 102, rx: 8, fill: "#2A2840", stroke: "#ffcf6b", "stroke-width": 4 }, ph);
    svg("rect", { x: W - 24 - 260, y: s.safeTop + 45, width: 260, height: 60, rx: 8, fill: "none", stroke: "#FFD959", "stroke-width": 4 }, ph);
    svg("rect", { x: W - 24 - 420, y: safeB - 132 - 16 - 132, width: 420, height: 132, rx: 8, fill: "#7590C8", stroke: "#D6D2F0", "stroke-width": 4 }, ph);
    function label(x, y, t, c) { var e = svg("text", { x: x, y: y, fill: c || "#9097a8", "font-size": 56, "font-family": "IBM Plex Mono, monospace" }, ph); e.textContent = t; }
    label(40, s.safeTop - 20 > 40 ? s.safeTop - 20 : 40, "safe top " + s.safeTop);
    label(176, s.safeTop + 90, "Back / ≡", "#ffcf6b");
    label(W - 290, s.safeTop + 150 + 50, "gold", "#FFD959");
    label(40, s.thumbTop - 20, "thumb zone (bottom 45 %) y=" + s.thumbTop, "#6fd6c4");
    label(W - 400, safeB - 132 - 16 - 50, "primary action", "#0C0C16");
    label(40, safeB - 50, "nav 132");
    label(40, H - 20, "safe bottom " + s.safeBottom);
    label(W / 2 - 200, H / 2 - 200, s.stage[0] + " × " + s.stage[1], "#e6e8ee");
  }
  var tb = table($("dev-table"), ["Shape", "Target", "Stage", "Safe top / bottom", "Thumb zone y", "132 px is", "30 px is"], []);
  var rowsBy = [];
  G.shapes.slice().sort(function (a, b) { return (b.inTarget - a.inTarget); }).forEach(function (s) {
    var unit = s.rep.indexOf("iphone") === 0 || s.rep === "ipad" ? " pt" : " dp";
    var tr = el("tr", null, null, tb);
    el("td", null, s.id + (s.reference ? " (reference)" : ""), tr);
    el("td", null, s.inTarget ? "yes" : "no", tr);
    el("td", null, s.stage[0] + " × " + s.stage[1] + (s.framed ? " framed" : ""), tr);
    el("td", null, s.safeTop + " / " + s.safeBottom, tr);
    el("td", null, String(s.thumbTop), tr);
    el("td", s.targetPt < 44 ? "warn" : null, s.targetPt + unit, tr);
    el("td", null, s.floorPt + unit, tr);
    tr.title = s.label + " · " + s.rep + " " + s.px.join("×");
    tr.addEventListener("click", function () { pick(s, tr); });
    rowsBy.push([s, tr]);
  });
  function pick(s, tr) {
    rowsBy.forEach(function (p) { p[1].classList.toggle("on", p[1] === tr); });
    draw(s);
    $("dev-note").textContent = s.label + ". Representative " + s.rep + ", " + s.px.join(" × ") + " px, " + s.pt.join(" × ") + " pt/dp. Values in design px.";
  }
  var ref = rowsBy.filter(function (p) { return p[0].reference; })[0] || rowsBy[0];
  pick(ref[0], ref[1]);
  el("p", "m", "Red: 132 design px falls under 44 pt/dp on that shape (the narrow Galaxy screens). An open question for the owner, in DESIGN_SYSTEM.md §5. Devices file checked " + G.devicesChecked + ".", $("dev-table").parentNode);

  // ── Do and do not ──
  D.dos.forEach(function (d) {
    var r = el("div", "do-row", null, $("dos-list"));
    el("div", "yes", "Do: " + d[0], r);
    el("div", "no", "Do not: " + d[1], r);
    el("div", "r", d[2], r);
  });
})();
