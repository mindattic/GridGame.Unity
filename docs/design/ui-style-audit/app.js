(function () {
  var d = window.UiStyleAudit;
  if (!d) return;
  var RULES = Object.keys(d.rules);
  var LABEL = { "POL-K1": "Header", "POL-K2": "Body / footer", "POL-K3": "Icons", "POL-K4": "Type / spacing" };

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }
  function count(hits, rule) { return hits.filter(function (h) { return h.rule === rule; }).length; }

  var head = el("tr");
  ["Fix", "Screen"].concat(RULES.map(function (r) { return LABEL[r] + " (" + r + ")"; }), ["Total"]).forEach(function (t) { head.appendChild(el("th", "", t)); });
  document.getElementById("sumHead").appendChild(head);

  var rows = document.getElementById("sumRows"), groups = document.getElementById("groups");
  d.groups.forEach(function (g) {
    var sec = el("section");
    var h = el("h2", "", g.name + " ");
    h.appendChild(el("span", "tix", g.ticket));
    sec.appendChild(h);
    g.screens.forEach(function (s) {
      var tr = el("tr");
      tr.appendChild(el("td", "tix", g.ticket));
      tr.appendChild(el("td", "", s.name));
      RULES.forEach(function (r) {
        var n = count(s.hits, r);
        tr.appendChild(el("td", "n " + (n ? "some" : "zero"), String(n)));
      });
      tr.appendChild(el("td", "n " + (s.hits.length ? "some" : "zero"), String(s.hits.length)));
      rows.appendChild(tr);

      var det = el("details");
      var sum = el("summary", "", s.name);
      sum.appendChild(el("span", "c", s.hits.length ? s.hits.length + " hits" : "no hits"));
      det.appendChild(sum);
      det.appendChild(el("div", "note", "Classes: " + s.classes.join(", ")));
      s.hits.forEach(function (x) {
        var p = el("div", "hit");
        p.appendChild(el("span", "rule", x.rule));
        p.appendChild(document.createTextNode(x.file + ":" + x.line + "  " + x.what));
        det.appendChild(p);
      });
      sec.appendChild(det);
    });
    groups.appendChild(sec);
  });

  document.getElementById("scope").textContent =
    "Not linted per screen: " + d.notLinted.join("; ") + ". Out of scope: " + d.outOfScope.join(", ") + ".";
})();
