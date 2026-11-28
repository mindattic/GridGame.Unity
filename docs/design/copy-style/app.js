(function () {
  var d = window.CopyStyle;
  if (!d) return;
  var m = d.measure, perChar = m.base * m.glyphEm, perCharFloor = m.floor * m.glyphEm, SCALE = 0.25;

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }
  function byId(id) { return document.getElementById(id); }
  // Renders "[term]" as an underlined glossary word, the way the game shows it (brackets hidden).
  function rich(text) {
    var f = document.createDocumentFragment();
    String(text).split(/(\[[^\]]+\])/).forEach(function (part) {
      if (/^\[[^\]]+\]$/.test(part)) f.appendChild(el("span", "term", part.slice(1, -1)));
      else if (part) f.appendChild(document.createTextNode(part));
    });
    return f;
  }
  function list(id, items) { var ul = byId(id); items.forEach(function (t) { var li = el("li"); li.appendChild(rich(t)); ul.appendChild(li); }); }
  function rows(id, data, cls) {
    var tb = byId(id);
    data.forEach(function (r) {
      var tr = el("tr");
      r.forEach(function (c, i) { var td = el("td", cls && cls[i] || ""); td.appendChild(rich(c)); tr.appendChild(td); });
      tb.appendChild(tr);
    });
  }
  function budget(r) { return Math.floor(r.width / perChar); }
  function cap(r) { return Math.floor(r.width / perCharFloor); }

  d.links.forEach(function (l) {
    var a = el("a", "", l.ticket + " · " + l.name);
    a.href = l.href;
    byId("links").appendChild(a);
  });

  d.ownerRules.forEach(function (r) {
    var c = el("div", "card");
    c.appendChild(el("b", "", r[0]));
    c.appendChild(el("span", "", r[1]));
    byId("ownerRules").appendChild(c);
  });

  list("voiceRules", d.voiceRules);
  rows("voice", d.voice.map(function (v) { return [v.do, v.dont]; }), ["do", "dont"]);
  rows("casing", d.casing);
  rows("verbs", d.verbs, ["tix"]);
  list("verbNotes", d.verbNotes);

  var names = byId("names");
  d.names.forEach(function (n) {
    var tr = el("tr", n[3] === "pending" ? "pending" : "");
    tr.appendChild(el("td", "", n[0]));
    tr.appendChild(el("td", "", n[1]));
    tr.appendChild(el("td", "dont", n[2]));
    var st = el("td");
    st.appendChild(el("span", "pill " + n[3], n[3]));
    if (n[4]) st.appendChild(document.createTextNode(" " + n[4]));
    tr.appendChild(st);
    names.appendChild(tr);
  });
  byId("namesNote").textContent = d.namesNote;

  rows("numbers", d.numbers);

  byId("measureAbout").textContent = m.about;
  var roleRows = byId("roles");
  d.roles.forEach(function (r) {
    var tr = el("tr");
    tr.appendChild(el("td", "", r.name));
    tr.appendChild(el("td", "n", String(r.width)));
    tr.appendChild(el("td", "", r.how));
    var lines = r.lines > 1 ? " a line, " + r.lines + " lines" : "";
    tr.appendChild(el("td", "n", budget(r) + lines));
    tr.appendChild(el("td", "n", String(cap(r))));
    tr.appendChild(el("td", "", r.example + " (" + r.example.length + ")"));
    roleRows.appendChild(tr);
  });
  list("fitRules", d.fitRules);

  rows("states", d.states, ["", "", "do", "dont"]);
  list("glossary", d.glossary);

  var open = byId("open");
  d.open.forEach(function (q) {
    var tr = el("tr", q[3] ? "pending" : "");
    tr.appendChild(el("td", "", q[0]));
    var td = el("td");
    td.appendChild(el("b", "", q[1]));
    if (q[3]) { td.appendChild(document.createTextNode(" ")); td.appendChild(el("span", "pill pending", "pending owner")); }
    tr.appendChild(td);
    tr.appendChild(el("td", "", q[2]));
    open.appendChild(tr);
  });

  rows("drift", d.drift, ["", "dont", "do"]);
  list("checklist", d.checklist);

  // Label fit checker.
  var sel = byId("fitRole"), input = byId("fitText"), box = byId("fitBox"), label = byId("fitLabel"), verdict = byId("fitVerdict");
  d.roles.forEach(function (r) {
    var o = el("option", "", r.name + " (" + budget(r) + (r.lines > 1 ? " x " + r.lines : "") + ")");
    o.value = r.id;
    sel.appendChild(o);
  });
  sel.value = "foot2";
  function check() {
    var r = d.roles.filter(function (x) { return x.id === sel.value; })[0];
    var text = input.value, n = text.length, b = budget(r), total = b * r.lines;
    box.style.setProperty("--w", Math.round(r.width * SCALE) + "px");
    box.classList.toggle("single", r.lines === 1);
    label.textContent = text;
    var over = n > total, longWord = text.split(/\s+/).some(function (w) { return w.length > b; });
    var ellipsis = /\.\.\.|…/.test(text);
    box.classList.toggle("over", over || longWord || ellipsis);
    var msg, cls;
    if (ellipsis) { msg = "Contains an ellipsis. Owner rule: no ... anywhere."; cls = "bad"; }
    else if (n === 0) { msg = "Type a label."; cls = "warn"; }
    else if (longWord) { msg = "A word is longer than one line holds (" + b + "). Pick a shorter word."; cls = "bad"; }
    else if (!over) { msg = n + " of " + total + " characters. Fits at its own size."; cls = "ok"; }
    else if (n <= cap(r) * r.lines) { msg = n + " of " + total + ". Over budget: it would only fit by shrinking. Shorten it."; cls = "warn"; }
    else { msg = n + " of " + total + ". Past the hard cap (" + cap(r) * r.lines + "): it would marquee. Shorten it."; cls = "bad"; }
    verdict.textContent = msg;
    verdict.className = "verdict " + cls;
  }
  sel.addEventListener("change", check);
  input.addEventListener("input", check);
  check();
})();
