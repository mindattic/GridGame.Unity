(function () {
  var d = window.StoreReadiness;
  if (!d) return;

  var AREAS = ["Android", "iOS", "Shared"];
  var STATUSES = ["done", "partial", "missing", "owner"];
  var state = { area: "all", status: "all" };

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  // Markdown inline code (`x`) becomes <code>; everything else stays text.
  function rich(cls, text) {
    var td = el("td", cls);
    text.split("`").forEach(function (part, i) {
      if (!part) return;
      td.appendChild(i % 2 ? el("code", "", part) : document.createTextNode(part));
    });
    return td;
  }

  function idList(ids, isOn) {
    var td = el("td", "ids");
    if (!ids.length) td.textContent = "-";
    ids.forEach(function (id) { td.appendChild(el("span", isOn && !isOn(id) ? "off" : "", id)); });
    return td;
  }

  function matches(r) {
    return (state.area === "all" || r.area === state.area) && (state.status === "all" || r.status === state.status);
  }

  function buttons(id, values, key, label) {
    var group = document.getElementById(id);
    ["all"].concat(values).forEach(function (v) {
      var b = el("button", key === "status" && v !== "all" ? "s-" + v : "");
      b.type = "button";
      b.dataset.value = v;
      b.appendChild(document.createTextNode(v === "all" ? "All " + label : v));
      b.appendChild(el("span", "count"));
      b.addEventListener("click", function () { state[key] = v; render(); });
      group.appendChild(b);
    });
  }

  buttons("areaFilter", AREAS, "area", "areas");
  buttons("statusFilter", STATUSES, "status", "statuses");

  var rowsBody = document.querySelector("#rows tbody");
  var rowEls = d.rows.map(function (r) {
    var tr = el("tr");
    tr.appendChild(el("td", "", r.id));
    tr.appendChild(rich("", r.item));
    var st = el("td");
    st.appendChild(el("span", "badge s-" + r.status, r.status));
    tr.appendChild(st);
    tr.appendChild(rich("", r.evidence));
    tr.appendChild(idList(r.gaps));
    tr.appendChild(idList(r.recheck));
    rowsBody.appendChild(tr);
    return tr;
  });

  var byId = {};
  d.rows.forEach(function (r) { byId[r.id] = r; });
  function visible(id) { return byId[id] ? matches(byId[id]) : state.area === "all" && state.status === "all"; }

  // Owner and sibling rows stay listed; one is dimmed when none of the item rows it names is in the filter.
  function linked(id, items, key) {
    var body = document.querySelector("#" + id + " tbody");
    return items.map(function (it) {
      var tr = el("tr");
      body.appendChild(tr);
      return { it: it, tr: tr, key: key };
    });
  }

  var ownerRows = linked("owner", d.owner, "unblocks");
  var siblingRows = linked("siblings", d.siblings, "rows");

  function fillLinked(list, cells) {
    list.forEach(function (x) {
      x.tr.textContent = "";
      cells(x.it).forEach(function (c) { x.tr.appendChild(c); });
      x.tr.classList.toggle("dim", !x.it[x.key].some(visible));
    });
  }

  function render() {
    var shown = 0;
    d.rows.forEach(function (r, i) {
      var on = matches(r);
      rowEls[i].hidden = !on;
      if (on) shown++;
    });

    [["areaFilter", "area"], ["statusFilter", "status"]].forEach(function (g) {
      Array.prototype.forEach.call(document.querySelectorAll("#" + g[0] + " button"), function (b) {
        var v = b.dataset.value;
        b.setAttribute("aria-pressed", String(state[g[1]] === v));
        var probe = { area: state.area, status: state.status };
        probe[g[1]] = v;
        b.querySelector(".count").textContent = d.rows.filter(function (r) {
          return (probe.area === "all" || r.area === probe.area) && (probe.status === "all" || r.status === probe.status);
        }).length;
      });
    });

    var tally = STATUSES.map(function (s) {
      return d.rows.filter(function (r) { return matches(r) && r.status === s; }).length + " " + s;
    }).join(", ");
    document.getElementById("summary").textContent = shown + " of " + d.rows.length + " rows: " + tally + ".";

    fillLinked(ownerRows, function (o) {
      return [el("td", "ids", o.ticket), rich("", o.what), idList(o.unblocks, visible)];
    });
    fillLinked(siblingRows, function (s) {
      var slice = el("td");
      slice.appendChild(el("span", "ids", s.ticket + " "));
      slice.appendChild(document.createTextNode(s.name));
      return [slice, idList(s.rows, visible), rich("", s.why)];
    });
  }

  render();
})();
