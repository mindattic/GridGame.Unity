(function () {
  var d = window.CommanderLore;
  if (!d) return;

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  var premise = document.getElementById("premise");
  d.premise.forEach(function (p) { premise.appendChild(el("p", null, p)); });

  var dec = document.getElementById("decisions");
  d.decisions.forEach(function (x) {
    var li = el("li", "decision");
    var head = el("div", "dhead");
    head.appendChild(el("span", "did", x.id));
    head.appendChild(el("h3", null, x.title));
    head.appendChild(el("span", "pill", "Awaiting owner"));
    li.appendChild(head);
    var dl = el("dl");
    [["Default", x.def], ["Instead", x.alt], ["Source", x.src]].forEach(function (r) {
      dl.appendChild(el("dt", null, r[0]));
      dl.appendChild(el("dd", r[0] === "Source" ? "src" : null, r[1]));
    });
    li.appendChild(dl);
    dec.appendChild(li);
  });

  function sky(frac) {
    var s = el("span", "sky");
    s.setAttribute("aria-label", "Manalith " + frac + " of the screen, Moon " + d.moon);
    var m = el("span", "lith");
    var size = Math.round(frac * 60);
    m.style.width = m.style.height = size + "px";
    var moon = el("span", "moon");
    moon.style.width = moon.style.height = Math.round(d.moon * 60) + "px";
    s.appendChild(m);
    s.appendChild(moon);
    return s;
  }

  var rb = document.querySelector("#regions tbody");
  d.regions.forEach(function (r, i) {
    var tr = el("tr", i >= 8 ? "post" : null);
    tr.appendChild(el("td", "n", String(i + 1)));
    tr.appendChild(el("th", null, r[0]));
    tr.appendChild(el("td", "n", r[1]));
    var skyTd = el("td", "skycell");
    skyTd.appendChild(sky(r[2]));
    skyTd.appendChild(el("span", "n", r[2].toFixed(2)));
    tr.appendChild(skyTd);
    tr.appendChild(el("td", "mono", r[3]));
    tr.appendChild(el("td", null, r[4]));
    tr.appendChild(el("td", null, r[5]));
    rb.appendChild(tr);
  });

  var cb = document.getElementById("chapters");
  d.chapters.forEach(function (c) {
    var li = el("li", c[0] === "Ending" || c[0] === "Final" ? "chapter mark" : "chapter");
    var h = el("div", "chead");
    h.appendChild(el("span", "slot", c[0]));
    h.appendChild(el("h3", null, c[1]));
    li.appendChild(h);
    li.appendChild(el("p", "meta", "After " + c[2] + " · opens " + c[3]));
    li.appendChild(el("p", null, c[4]));
    var w = el("p", "who");
    w.appendChild(el("b", null, "Told by "));
    w.appendChild(document.createTextNode(c[5] + ". "));
    w.appendChild(el("b", null, "The Commander: "));
    w.appendChild(document.createTextNode(c[6]));
    li.appendChild(w);
    cb.appendChild(li);
  });
})();
