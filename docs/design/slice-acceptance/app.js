/* Renders window.SLICE (data.js): the ask, the status tiles and filters, one card per ask with a row per part (beat, status, proof, tickets), and the next steps. */
(function () {
  const S = window.SLICE;
  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;" }[c]));
  const order = ["done", "built", "missing"];
  const parts = S.asks.flatMap((a) => a.parts);
  const count = (st) => parts.filter((p) => p.status === st).length;

  $("stamp").textContent = "Checked " + S.checked + " against main at " + S.main + ". Reference device: " + S.deviceName + " (" + S.device + ").";
  $("ask").textContent = "“" + S.ask + "”";

  $("tiles").innerHTML = order.map((st) => "<div class=\"tile " + st + "\"><b>" + count(st) + "</b><span>" + esc(S.statuses[st]) + "</span></div>").join("") +
    "<div class=\"tile\"><b>" + S.asks.length + "</b><span>Asks, " + parts.length + " rows</span></div>";
  $("meter").innerHTML = order.map((st) => "<i class=\"" + st + "\" style=\"flex:" + count(st) + "\"></i>").join("");

  const proof = (p) => {
    const i = p.indexOf(":"), kind = p.slice(0, i), v = p.slice(i + 1);
    if (kind === "cap") {
      const src = S.captureDir + S.device + "/" + v + ".png";
      return "<a class=\"cap\" href=\"" + esc(src) + "\" target=\"_blank\" rel=\"noopener\"><img src=\"" + esc(src) + "\" alt=\"\" loading=\"lazy\"><span>" + esc(v) + ".png</span></a>";
    }
    if (kind === "pre") return "<div class=\"cmd\"><span class=\"tag\">Preset</span><code>python tools/scenario.py " + esc(v) + " --device " + S.device + "</code></div>";
    if (kind === "run") return "<div class=\"cmd\"><span class=\"tag\">Target</span><code>python tools/scenario.py " + esc(v) + " --device " + S.device + "</code></div>";
    if (kind === "play") return "<div class=\"cmd\"><span class=\"tag\">New game</span><span>Prologue battle " + (S.prologue.indexOf(v) + 1) + ", <code>" + esc(v) + "</code></span></div>";
    return "<div class=\"cmd\"><span class=\"tag\">File</span><code>" + esc(v) + "</code></div>";
  };
  const tickets = (xs, cls) => xs.map((t) => "<span class=\"tk " + cls + "\">" + esc(t) + "</span>").join(" ");

  const worst = (a) => order.slice().reverse().find((st) => a.parts.some((p) => p.status === st));
  $("asks").innerHTML = S.asks.map((a) => {
    const w = worst(a);
    const rows = a.parts.map((p) =>
      "<tr data-status=\"" + p.status + "\">" +
      "<td><b>" + esc(p.what) + "</b>" + (p.note ? "<p class=\"note\">" + esc(p.note) + "</p>" : "") + "</td>" +
      "<td data-l=\"Beat\"><code class=\"beat\">" + esc(p.beat) + "</code></td>" +
      "<td data-l=\"Status\"><span class=\"pill " + p.status + "\">" + esc(S.statuses[p.status]) + "</span></td>" +
      "<td data-l=\"Proof\"><div class=\"proof\">" + (p.proof.length ? p.proof.map(proof).join("") : "<span class=\"none\">None yet</span>") + "</div></td>" +
      "<td data-l=\"Tickets\">" + (p.landed.length ? "<div class=\"tl\">Landed " + tickets(p.landed, "landed") + "</div>" : "") +
      (p.open.length ? "<div class=\"tl\">Open " + tickets(p.open, "open") + "</div>" : "") + "</td></tr>").join("");
    return "<article class=\"ask-card\" id=\"" + a.id + "\">" +
      "<header><h3>" + esc(a.title) + "</h3><span class=\"pill " + w + "\">" + a.parts.filter((p) => p.status === "done").length + " of " + a.parts.length + " done</span></header>" +
      "<p class=\"quote\">“" + esc(a.quote) + "”</p>" +
      "<div class=\"scroll\"><table><thead><tr><th>What</th><th>Beat</th><th>Status</th><th>Proof</th><th>Tickets</th></tr></thead><tbody>" + rows + "</tbody></table></div></article>";
  }).join("");

  const key = "slice-acceptance-filter";
  let filter = "all";
  try { filter = localStorage.getItem(key) || "all"; } catch (e) { /* storage off: show all */ }
  const opts = [["all", "All"]].concat(order.map((st) => [st, S.statuses[st]]));
  const apply = () => {
    document.querySelectorAll("#filters button").forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.f === filter)));
    document.querySelectorAll("#asks tr[data-status]").forEach((tr) => { tr.hidden = filter !== "all" && tr.dataset.status !== filter; });
    document.querySelectorAll(".ask-card").forEach((c) => { c.hidden = !c.querySelector("tr[data-status]:not([hidden])"); });
  };
  $("filters").innerHTML = opts.map(([f, l]) => "<button type=\"button\" data-f=\"" + f + "\">" + esc(l) + "</button>").join("");
  $("filters").addEventListener("click", (e) => {
    const b = e.target.closest("button[data-f]"); if (!b) return;
    filter = b.dataset.f;
    try { localStorage.setItem(key, filter); } catch (err) { /* ignore */ }
    apply();
  });
  apply();

  $("how").innerHTML = "Captures are the uiaudit set on the " + esc(S.deviceName) + " at " + esc(S.captureCommit) + ", under <code>docs/design/ui-audit/captures/" + S.device + "/</code>. " +
    "A preset is <code>tools/scenarios/&lt;name&gt;.json</code>; a target (<code>stage:&lt;id&gt;</code>, <code>screen:&lt;id&gt;</code>, <code>overworld</code>) builds a coherent save on the fly. Both run with <code>python tools/scenario.py &lt;preset|target&gt; --device " + S.device + "</code> and land live at that point. Run one player at a time. The in-target matrix is <code>tools/devices.json</code>. " +
    "The six prologue battles (" + S.prologue.map((id) => "<code>" + id + "</code>").join(", ") + ") are not stages of a campaign region, so <code>scenario.py stage:prologue-*</code> refuses them. Reach them by playing a new game until the harness prologue flow (GG-T2083) captures each beat.";
  $("next").innerHTML = S.next.map(([t, d]) => "<li><span class=\"tk open\">" + esc(t) + "</span> " + esc(d) + "</li>").join("");
})();
