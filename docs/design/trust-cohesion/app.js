(function () {
  var d = window.TrustCohesion;
  if (!d) return;

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  function fill(id, rows, cols, rowClass) {
    var body = document.querySelector("#" + id + " tbody");
    rows.forEach(function (r) {
      var tr = el("tr", rowClass ? rowClass(r) : "");
      cols.forEach(function (c) {
        tr.appendChild(el("td", c.n ? "n" : "", String(r[c.k])));
      });
      body.appendChild(tr);
    });
  }

  function bondClass(r) { return "bond-" + r.bond.toLowerCase(); }

  var ladder = document.getElementById("ladder");
  var span = d.scale.max - d.scale.min;
  d.tiers.forEach(function (t, i) {
    var next = i + 1 < d.tiers.length ? d.tiers[i + 1].floor : d.scale.max;
    var seg = el("div", "rung t" + t.tier);
    seg.style.flexGrow = Math.max(next - t.floor, 0) || 0.0001;
    seg.style.flexBasis = "0";
    seg.appendChild(el("span", "rung-tier", "T" + t.tier));
    seg.appendChild(el("span", "rung-floor", String(t.floor)));
    ladder.appendChild(seg);
  });
  ladder.title = d.scale.min + " to " + d.scale.max + " (" + span + ")";

  document.getElementById("floorRule").textContent = d.floorRule;

  fill("tiers", d.tiers, [{ k: "tier", n: true }, { k: "floor", n: true }, { k: "trust" }, { k: "cohesion" }]);
  fill("sources", d.sources, [{ k: "bond" }, { k: "source" }, { k: "amount", n: true }, { k: "note" }], bondClass);
  fill("payoffs", d.payoffs, [{ k: "bond" }, { k: "what" }, { k: "value" }], bondClass);

  var who = document.getElementById("who");
  d.whoHasTrust.forEach(function (w) {
    var li = el("li", w.trust ? "yes" : "no");
    li.appendChild(el("span", "mark", w.trust ? "Yes" : "No"));
    li.appendChild(el("span", "", w.who));
    who.appendChild(li);
  });

  var cast = document.getElementById("cast");
  d.coreCast.forEach(function (c) { cast.appendChild(el("span", "chip", c)); });
  var n = d.coreCast.length;
  document.getElementById("pairs").textContent = n + " heroes, " + (n * (n - 1) / 2) + " pairs.";

  var naming = document.getElementById("naming");
  [["Trust", d.naming.trust], ["Rapport", d.naming.rapport]].forEach(function (p) {
    naming.appendChild(el("dt", "", p[0]));
    naming.appendChild(el("dd", "", p[1]));
  });
})();
