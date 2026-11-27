(function () {
  "use strict";
  var D = window.MONETIZATION;

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  function table(id, head, rows) {
    var t = document.getElementById(id);
    var tr = el("tr");
    head.forEach(function (h) { tr.appendChild(el("th", null, h[0])); });
    t.appendChild(el("thead")).appendChild(tr);
    var body = t.appendChild(el("tbody"));
    rows.forEach(function (r) {
      var row = body.appendChild(el("tr"));
      head.forEach(function (h, i) {
        var v = r[i];
        var td = row.appendChild(el("td", h[1] || null));
        if (v instanceof Node) td.appendChild(v); else td.textContent = v;
      });
    });
  }

  var principles = document.getElementById("principles");
  D.principles.forEach(function (p) {
    var c = principles.appendChild(el("div", "card"));
    c.appendChild(el("h3", null, p.title));
    c.appendChild(el("p", null, p.body));
  });

  var tabs = document.getElementById("tabs"), place = document.getElementById("place");
  function show(i) {
    var p = D.placements[i];
    Array.prototype.forEach.call(tabs.children, function (b, j) { b.setAttribute("aria-selected", String(i === j)); });
    place.textContent = "";
    place.appendChild(el("p", "when m", p.when));
    var dl = place.appendChild(el("dl", "facts"));
    p.what.forEach(function (w) { dl.appendChild(el("dt", null, w[0])); dl.appendChild(el("dd", null, w[1])); });
    place.appendChild(el("p", "rule", p.rules));
    place.appendChild(el("p", "refs", p.refs));
    try { localStorage.setItem("monetization-tab", p.id); } catch (e) { }
  }
  D.placements.forEach(function (p, i) {
    var b = tabs.appendChild(el("button", null, p.name));
    b.type = "button";
    b.setAttribute("role", "tab");
    b.addEventListener("click", function () { show(i); });
  });
  var start = 0;
  try {
    var saved = localStorage.getItem("monetization-tab");
    D.placements.forEach(function (p, i) { if (p.id === saved) start = i; });
  } catch (e) { }
  show(start);

  table("products",
    [["Id", "id"], ["Name"], ["Type"], ["Limit", "n"], ["Placeholder price", "n"], ["Contents"], ["Earned path"], ["Status"]],
    D.products.map(function (p) {
      return [p.id, p.name, p.type, p.limit ? p.limit + " per account" : "none", p.price, p.contents, p.earned,
        el("span", "pill " + p.status, p.status)];
    }));

  table("shardPrices", [["What"], ["Price", "n"], ["Source", "src"]], D.shardPrices);
  table("rates", [["Rate"], ["Value"], ["Source", "src"]], D.rates.map(function (r) { return [r[0], r[1], r[2]]; }));

  var q = document.getElementById("questions");
  D.questions.forEach(function (s) { q.appendChild(el("li", null, s)); });
})();
