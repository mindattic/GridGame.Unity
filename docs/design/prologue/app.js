/* Renders window.PROLOGUE (data.js): the timeline with its card ticks and the 60 s check, the selected beat, the beat table, the slice arc and the canon table. */
(function () {
  const P = window.PROLOGUE;
  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;" }[c]));
  const clock = (min) => { const s = Math.round(min * 60); return Math.floor(s / 60) + ":" + String(s % 60).padStart(2, "0"); };
  const list = (xs) => "<ul>" + xs.map((x) => "<li>" + esc(x) + "</li>").join("") + "</ul>";

  const total = P.beats.reduce((m, b) => Math.max(m, b.start + b.minutes), 0);
  const cards = [];
  P.beats.forEach((b) => b.cards.forEach((c) => cards.push({ beat: b.id, at: b.start + c.at, text: c.text })));
  cards.sort((a, b) => a.at - b.at);
  cards.forEach((c, i) => { c.gap = i ? (c.at - cards[i - 1].at) * 60 : Infinity; c.bad = c.gap < P.budget.cardGapSeconds - 0.5; });
  const bad = cards.filter((c) => c.bad);
  const inRange = total >= P.budget.minMinutes && total <= P.budget.maxMinutes;

  $("premise").innerHTML = P.premise.map((p) => "<p>" + esc(p) + "</p>").join("");
  $("bar").innerHTML = P.bar.map(([k, v]) => "<li><b>" + esc(k) + "</b>" + esc(v) + "</li>").join("");
  $("budget").innerHTML = "Played straight through: <b class=\"" + (inRange ? "ok" : "badtxt") + "\">" + clock(total) + "</b> (target " + P.budget.minMinutes + " to " + P.budget.maxMinutes + " min). " +
    cards.length + " blocking cards, " + (bad.length ? "<b class=\"badtxt\">" + bad.length + " closer than 60 s</b>" : "<b class=\"ok\">none closer than 60 s</b>") +
    " (REVIEW.md 4.2: one concept per minute). Each beat's Skip collapses it to a few seconds.";

  const tl = $("timeline");
  P.beats.forEach((b) => {
    const btn = document.createElement("button");
    btn.type = "button"; btn.role = "tab"; btn.dataset.id = b.id;
    btn.style.setProperty("--w", b.minutes);
    if (b.battle) btn.classList.add("has-battle");
    btn.title = clock(b.start) + " " + b.title;
    btn.innerHTML = "<span class=\"lbl\">" + esc(b.title) + "</span>" +
      cards.filter((c) => c.beat === b.id).map((c) => "<i class=\"tick" + (c.bad ? " bad" : "") + "\" style=\"left:" + ((c.at - b.start) / b.minutes * 100).toFixed(1) + "%\" title=\"" + esc(clock(c.at) + " " + c.text) + "\"></i>").join("");
    btn.addEventListener("click", () => select(b.id));
    tl.appendChild(btn);
  });
  const ticks = $("ticks");
  for (let m = 0; m <= Math.ceil(total); m += 5) ticks.insertAdjacentHTML("beforeend", "<span style=\"left:" + (m / total * 100).toFixed(1) + "%\">" + m + "m</span>");

  const head = "<thead><tr><th>Time</th><th>Beat</th><th>Map node</th><th>Teaches</th><th>Battle</th><th>Reward</th><th>Skip</th></tr></thead>";
  $("beats").innerHTML = head + "<tbody>" + P.beats.map((b) => "<tr data-id=\"" + b.id + "\"><td><code>" + clock(b.start) + "</code></td><td><b>" + esc(b.title) + "</b></td><td><code>" + esc(b.node.id) + "</code><br>" + esc(b.node.kind + ": " + b.node.name) + "</td><td>" + esc(b.teaches.join("; ")) + "</td><td>" + (b.battle ? "<code>" + esc(b.battle.stage) + "</code><br>" + esc(b.battle.foes) : "None") + "</td><td>" + esc(b.reward) + "</td><td>" + esc(b.skip) + "</td></tr>").join("") + "</tbody>";
  $("beats").addEventListener("click", (e) => { const tr = e.target.closest("tr[data-id]"); if (tr) { select(tr.dataset.id); $("detail").scrollIntoView({ behavior: "smooth", block: "nearest" }); } });
  $("skipall").innerHTML = "<b>Skip all.</b> " + esc(P.skipAll);
  $("steps").innerHTML = P.onboardingSteps.map((s) => "<code>" + esc(s) + "</code>").join(" &rarr; ");

  $("slice").innerHTML = P.slice.map((s) => "<li><b>" + esc(s.title) + "</b><div class=\"meta\">" + esc(s.node) + " &middot; " + esc(s.minutes) + " min</div><p>" + esc(s.what) + "</p></li>").join("");
  $("canon").innerHTML = "<thead><tr><th>Topic</th><th>Canon says</th><th>The slice does</th><th></th></tr></thead><tbody>" +
    P.canon.map((c) => "<tr><td><b>" + esc(c.topic) + "</b></td><td>" + esc(c.canon) + "</td><td>" + esc(c.slice) + "</td><td>" + (c.owner ? "<b class=\"flag\">Owner</b>" : "") + "</td></tr>").join("") + "</tbody>";

  function select(id) {
    const b = P.beats.find((x) => x.id === id) || P.beats[0];
    tl.querySelectorAll("button").forEach((x) => x.setAttribute("aria-selected", String(x.dataset.id === b.id)));
    document.querySelectorAll("#beats tr[data-id]").forEach((r) => r.classList.toggle("sel", r.dataset.id === b.id));
    const mine = cards.filter((c) => c.beat === b.id);
    $("detail").innerHTML =
      "<div><div class=\"kicker\">" + clock(b.start) + " to " + clock(b.start + b.minutes) + " &middot; " + esc(b.node.kind) + " <code>" + esc(b.node.id) + "</code></div>" +
      "<h2>" + esc(b.title) + "</h2><p>" + esc(b.what) + "</p>" +
      "<dl><dt>Node</dt><dd>" + esc(b.node.name) + "</dd><dt>Teaches</dt><dd>" + list(b.teaches) + "</dd>" +
      "<dt>Battle</dt><dd>" + (b.battle ? "<code>" + esc(b.battle.stage) + "</code>: " + esc(b.battle.foes) + ". " + esc(b.battle.note) : "None") + "</dd>" +
      "<dt>Reward</dt><dd>" + esc(b.reward) + "</dd><dt>Skip</dt><dd>" + esc(b.skip) + "</dd></dl></div>" +
      "<div><h3>Blocking cards</h3><ol class=\"cards\">" + (mine.length ? mine.map((c) => "<li><code>" + clock(c.at) + "</code> " + esc(c.text) + (c.gap === Infinity ? "" : " <span class=\"" + (c.bad ? "badtxt" : "ok") + "\">(+" + Math.round(c.gap) + " s)</span>") + "</li>").join("") : "<li>None</li>") + "</ol></div>";
  }
  let start = P.beats[0].id;
  try { start = localStorage.getItem("prologue.beat") || start; } catch (e) { /* storage blocked */ }
  select(start);
  tl.addEventListener("click", (e) => { const b = e.target.closest("button"); if (b) try { localStorage.setItem("prologue.beat", b.dataset.id); } catch (err) { /* storage blocked */ } });
})();
