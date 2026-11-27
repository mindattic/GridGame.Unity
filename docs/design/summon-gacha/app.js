// Renders the Summon design page from window.SUMMON: sources, the live pull model, the free-path check and the defaults.
(function () {
  const S = window.SUMMON;
  const C = Object.fromEntries(S.config.map(c => [c.name, c.value]));
  const $ = id => document.getElementById(id);
  const esc = s => String(s).replace(/[&<>"]/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[ch]));
  const pct = p => (Math.round(p * 1000) / 10) + "%";

  function rng(seed) {
    return function () {
      seed |= 0; seed = seed + 0x6D2B79F5 | 0;
      let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
      t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
  }

  $("sources").innerHTML = "<tr><th>Source</th><th>How</th><th>Owned by</th><th></th></tr>" + S.sources.map(r =>
    `<tr><td><b>${esc(r.source)}</b></td><td>${esc(r.how)}</td><td class="m">${esc(r.owner)}</td><td><span class="tag ${r.paid ? "paid" : "free"}">${r.paid ? "Tickets or Shards" : "free"}</span></td></tr>`).join("");

  $("config").innerHTML = "<tr><th>Group</th><th>Constant</th><th class=\"c\">Default</th><th>Unit</th><th>What it does</th></tr>" + S.config.map(c =>
    `<tr><td class="m">${esc(c.group)}</td><td><code>${esc(c.name)}</code></td><td class="c">${c.value}</td><td class="m">${esc(c.unit)}</td><td>${esc(c.note)}</td></tr>`).join("");

  // ---- Live model ----
  const standard = S.standardPool.concat(S.summonOnly.filter(h => h !== S.featured));
  const featuredPool = [S.featured].concat(standard);
  let st, rand, seed = 1413;

  function reset() {
    rand = rng(seed++);
    st = { banner: "std", tickets: C.StarterTickets, shards: 0, fragments: 0, owned: new Set(), echoes: {},
      newMiss: 0, featMiss: 0, sparkCount: { std: 0, feat: 0 }, sparks: { std: 0, feat: 0 }, history: [], last: [] };
  }
  const pool = () => st.banner === "std" ? standard : featuredPool;
  const unowned = () => pool().filter(h => !st.owned.has(h));

  function odds() {
    const p = pool(), un = unowned(), o = {};
    if (st.banner === "feat" && st.featMiss >= C.FeaturedPity - 1) { o[S.featured] = 1; return { o, why: "Featured pity is due." }; }
    if (st.newMiss >= C.NewHeroPity - 1 && un.length) { un.forEach(h => o[h] = 1 / un.length); return { o, why: "New-hero pity is due." }; }
    if (st.banner === "std") { p.forEach(h => o[h] = 1 / p.length); return { o, why: "Every hero equally likely." }; }
    o[S.featured] = C.FeaturedShare;
    standard.forEach(h => o[h] = (1 - C.FeaturedShare) / standard.length);
    return { o, why: "Featured share, the rest a Standard pull." };
  }

  function land(hero, paid, how) {
    let result;
    if (!st.owned.has(hero)) { st.owned.add(hero); result = "new"; }
    else if ((st.echoes[hero] || 0) < C.EchoCap) { st.echoes[hero] = (st.echoes[hero] || 0) + 1; result = "echo"; }
    else {
      st.fragments += C.FragmentsPerSpareEcho; result = "frag";
      while (st.fragments >= C.FragmentsPerTicket) { st.fragments -= C.FragmentsPerTicket; st.tickets++; }
    }
    if (how !== "spark") {
      st.newMiss = result === "new" || !unowned().length ? 0 : st.newMiss + 1;
      if (st.banner === "feat") st.featMiss = hero === S.featured ? 0 : st.featMiss + 1;
      if (++st.sparkCount[st.banner] >= C.SparkPulls) { st.sparkCount[st.banner] = 0; st.sparks[st.banner]++; }
    }
    st.last.push({ hero, result });
    st.history.unshift({ n: st.history.length + 1, banner: st.banner === "std" ? "Standard" : "Featured", hero, result, paid, newMiss: st.newMiss, featMiss: st.featMiss });
    if (st.history.length > C.HistoryKeep) st.history.pop();
  }

  function pull(count) {
    st.last = [];
    for (let i = 0; i < count; i++) {
      let paid;
      if (st.tickets >= C.PullTickets) { st.tickets -= C.PullTickets; paid = "1 Ticket"; }
      else { st.shards += C.PullShards; paid = C.PullShards + " Shards"; }
      const { o } = odds();
      let r = rand(), hero = Object.keys(o)[0];
      for (const h in o) { if ((r -= o[h]) < 0) { hero = h; break; } }
      land(hero, paid, "pull");
    }
    render();
  }

  function spark() {
    if (!st.sparks[st.banner]) return;
    st.sparks[st.banner]--; st.last = [];
    const un = unowned();
    land(st.banner === "feat" && !st.owned.has(S.featured) ? S.featured : (un[0] || pool()[0]), "Spark", "spark");
    render();
  }

  function meter(label, v, max, hot) {
    return `<div class="meter"><span>${label}</span><span class="track"><span class="fill${hot ? " hot" : ""}" style="width:${Math.min(100, v / max * 100)}%"></span></span><span>${v}/${max}</span></div>`;
  }

  function render() {
    $("bStd").classList.toggle("on", st.banner === "std");
    $("bFeat").classList.toggle("on", st.banner === "feat");
    $("spark").disabled = !st.sparks[st.banner];
    $("spark").textContent = `Use Spark (${st.sparks[st.banner]})`;
    $("wallet").innerHTML = `<span>Tickets <b>${st.tickets}</b></span><span>Fragments <b>${st.fragments}/${C.FragmentsPerTicket}</b></span><span>Shards spent <b>${st.shards}</b> <span class="m">($${(st.shards / 100).toFixed(2)})</span></span><span>Pulls <b>${st.history.length}</b></span><span>Owned <b>${st.owned.size}/${featuredPool.length}</b></span>`;
    $("meters").innerHTML = meter("New-hero pity", st.newMiss, C.NewHeroPity) +
      (st.banner === "feat" ? meter("Featured pity", st.featMiss, C.FeaturedPity, true) : "") +
      meter("Spark", st.sparkCount[st.banner], C.SparkPulls);
    $("reveal").innerHTML = st.last.map(x => `<span class="r ${x.result}${x.hero === S.featured ? " f" : ""}">${esc(x.hero)} · ${x.result === "new" ? "New!" : x.result === "echo" ? "Echo +1 SP" : "+" + C.FragmentsPerSpareEcho + " fragments"}</span>`).join("") || `<span class="m">Pull to see heroes arrive.</span>`;
    $("roster").innerHTML = pool().map(h => {
      const e = st.echoes[h] || 0;
      return `<div class="hero${st.owned.has(h) ? " own" : ""}${h === S.featured ? " feat" : ""}"><b>${esc(h)}</b><span class="m">${st.owned.has(h) ? (e ? `${e} Echo${e > 1 ? "es" : ""}` : "Owned") : "Not yet"}</span><div class="pips">${Array.from({ length: C.EchoCap }, (_, i) => `<span class="pip${i < e ? " on" : ""}"></span>`).join("")}</div></div>`;
    }).join("");
    const { o, why } = odds();
    $("odds").innerHTML = `<li class="m">${why}</li>` + Object.keys(o).filter(h => o[h] > 0).map(h => `<li>${esc(h)} <span class="pct">${pct(o[h])}</span></li>`).join("");
    $("history").innerHTML = "<tr><th>#</th><th>Banner</th><th>Hero</th><th>Result</th><th>Paid</th></tr>" + st.history.map(r =>
      `<tr><td class="c">${r.n}</td><td>${r.banner}</td><td>${esc(r.hero)}</td><td>${r.result === "new" ? "New" : r.result === "echo" ? "Echo" : "Fragments"}</td><td class="m">${r.paid}</td></tr>`).join("");
  }

  $("bStd").onclick = () => { st.banner = "std"; st.last = []; render(); };
  $("bFeat").onclick = () => { st.banner = "feat"; st.last = []; render(); };
  $("pull1").onclick = () => pull(1);
  $("pull10").onclick = () => pull(C.MultiCount);
  $("spark").onclick = spark;
  $("reset").onclick = () => { reset(); render(); };
  reset(); render();

  // ---- Free-path check ----
  function campaignTickets(wins) {
    return C.StarterTickets + S.campaign.stages * C.TicketsPerFirstClear + S.campaign.chapters * C.TicketsPerChapter + Math.floor(wins / C.WinsPerTicket);
  }
  function worstCase(n, pity, sparkN) {
    let p = 0;
    while (Math.floor(p / pity) + Math.floor(p / sparkN) < n) p++;
    return p;
  }
  function averagePulls(n, pity, sparkN) {
    const r = rng(7), runs = 2000;
    let total = 0;
    for (let k = 0; k < runs; k++) {
      let owned = 0, miss = 0, p = 0;
      const have = new Uint8Array(n);
      while (owned < n) {
        p++;
        let h;
        if (miss >= pity - 1) { h = have.indexOf(0); }
        else h = Math.floor(r() * n);
        if (!have[h]) { have[h] = 1; owned++; miss = 0; } else miss++;
        if (p % sparkN === 0 && owned < n) { have[have.indexOf(0)] = 1; owned++; }
      }
      total += p;
    }
    return total / runs;
  }
  function check() {
    const n = Math.max(1, +$("cPool").value || 1), wins = Math.max(0, +$("cWins").value || 0);
    const pity = Math.max(1, +$("cPity").value || 1), sparkN = Math.max(1, +$("cSpark").value || 1);
    const t = campaignTickets(wins), worst = worstCase(n, pity, sparkN), avg = averagePulls(n, pity, sparkN);
    $("freeTotal").textContent = campaignTickets(S.campaign.winsEstimate);
    const ok = worst <= t;
    $("verdict").className = "verdict " + (ok ? "ok" : "no");
    $("verdict").innerHTML = `<b>${ok ? "Free path holds" : "Free path breaks"}</b>: the campaign pays <span class="num">${t}</span> Tickets (${C.StarterTickets} starter + ${S.campaign.stages} first clears + ${S.campaign.chapters * C.TicketsPerChapter} chapters + ${Math.floor(wins / C.WinsPerTicket)} from wins). ` +
      `Owning all ${n} heroes takes <span class="num">${worst}</span> pulls at worst and <span class="num">${avg.toFixed(1)}</span> on average. ` +
      (ok ? "No Shards needed." : `A non-paying player is ${worst - t} Tickets short: lower the pool, the pity or the Spark, or raise the earning.`);
  }
  ["cPool", "cWins", "cPity", "cSpark"].forEach(id => $(id).addEventListener("input", check));
  check();
})();
