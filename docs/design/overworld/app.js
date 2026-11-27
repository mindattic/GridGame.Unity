(function () {
  var d = window.Overworld;
  if (!d) return;

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }
  function byId(id) { return document.getElementById(id); }

  function paras(id, list) {
    var box = byId(id);
    list.forEach(function (p) { box.appendChild(el("p", null, p)); });
  }
  function rows(id, list) {
    var dl = byId(id);
    list.forEach(function (r) {
      dl.appendChild(el("dt", null, r[0]));
      dl.appendChild(el("dd", null, r[1]));
    });
  }
  function steps(id, list) {
    var ol = byId(id);
    list.forEach(function (s) {
      var li = el("li");
      li.appendChild(el("b", null, s[0]));
      li.appendChild(el("span", null, s[1]));
      ol.appendChild(li);
    });
  }
  function figs(id, list) {
    var box = byId(id);
    list.forEach(function (f) {
      var fig = el("figure", "fig");
      var a = el("a");
      a.href = f.src;
      a.target = "_blank";
      a.rel = "noopener";
      var img = el("img");
      img.src = f.src;
      img.alt = f.title;
      img.loading = "lazy";
      a.appendChild(img);
      fig.appendChild(a);
      var cap = el("figcaption");
      var t = el("b", null, f.title);
      cap.appendChild(t);
      if (f.ref) cap.appendChild(el("span", "tag", "Owner reference, not our art"));
      cap.appendChild(el("span", "fnote", f.note));
      fig.appendChild(cap);
      box.appendChild(fig);
    });
  }

  byId("quote").textContent = "“" + d.quote + "” — owner, 2026-11-27";
  paras("premise", d.premise);
  rows("maplook", d.mapLook);
  figs("refs", d.references);
  figs("starters", d.starters);
  rows("travel", d.travel);
  steps("camp", d.camp);

  var per = byId("periods");
  d.periods.forEach(function (p) {
    var s = el("div", "swatch");
    s.setAttribute("role", "listitem");
    var chip = el("span", "chip");
    chip.style.background = p.color;
    s.appendChild(chip);
    s.appendChild(el("b", null, p.name));
    s.appendChild(el("span", "fnote", p.grade));
    per.appendChild(s);
  });
  paras("daynight", d.dayNight);
  rows("weather", d.weather);
  rows("waystones", d.waystones);
  paras("library", d.library);
  rows("factors", d.factors);
  byId("diffout").textContent = d.difficultyOut;
  steps("onboarding", d.onboarding);
  byId("onbnote").textContent = d.onboardingNote;
  paras("side", d.sideStories);
  rows("objectives", d.objectives);
  rows("open", d.open);

  var tree = byId("tree");
  d.tree.forEach(function (f) {
    var det = el("details", "feature");
    det.open = true;
    var sum = el("summary");
    sum.appendChild(el("span", "tid", f[0]));
    sum.appendChild(el("span", null, f[1]));
    sum.appendChild(el("span", "lvl", "feature"));
    det.appendChild(sum);
    var stories = el("ul", "stories");
    f[2].forEach(function (s) {
      var li = el("li");
      var h = el("div", "story");
      h.appendChild(el("span", "tid", s[0]));
      h.appendChild(el("span", null, s[1]));
      h.appendChild(el("span", "lvl", "story"));
      li.appendChild(h);
      var tasks = el("ul", "tasks");
      s[2].forEach(function (t) {
        var ti = el("li", t[0] === "GG-T1659" ? "here" : null);
        ti.appendChild(el("span", "tid", t[0]));
        ti.appendChild(el("span", null, t[1]));
        tasks.appendChild(ti);
      });
      li.appendChild(tasks);
      stories.appendChild(li);
    });
    det.appendChild(stories);
    tree.appendChild(det);
  });
})();
