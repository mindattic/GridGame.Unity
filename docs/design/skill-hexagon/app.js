(() => {
// Draws and edits the board from skillhex.json (window.SKILLHEX, written by tools/skillhex/sync.py): node kinds and
// positions, passive stats, ability ids, Open slots, hero start paths and signatures. data.js (window.WHEEL_DATA)
// only supplies ability and class details for tooltips and the ability list. Nothing here places anything on its
// own; generate.js does that, and only when tools/skillhex/regenerate.py rebuilds skillhex.json from content.
// Edits happen in memory and are saved as a new skillhex.json in the same one-node-or-hero-per-line layout.
const D = window.WHEEL_DATA;
let B = window.SKILLHEX;
const STAT_IDS = ['VIT', 'STR', 'AGI', 'SPD', 'WIS', 'INT'];
const RECIPE_POINTS = 4;
const CONTENT_PATH = 'Assets/GridGame/Resources/Content/skillhex.json';
const FROM = { Vitality: 'VIT', Strength: 'STR', Agility: 'AGI', Speed: 'SPD', Wisdom: 'WIS', Intelligence: 'INT', Stamina: 'VIT', Luck: 'AGI' };
const R = 520;
const NS = 'http://www.w3.org/2000/svg';
const svg = document.getElementById('hex'), tip = document.getElementById('tip'), $ = id => document.getElementById(id);
const el = (t, a, p) => { const e = document.createElementNS(NS, t); for (const k in a) e.setAttribute(k, a[k]); if (p) p.appendChild(e); return e; };
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[c]));
const css = v => getComputedStyle(document.documentElement).getPropertyValue(v).trim();
const sc = s => css('--s-' + s);
const CLS = Object.fromEntries(D.classes.map(c => [c.Id, c]));
const DIRS = [[1, 0], [0, 1], [-1, 1], [-1, 0], [0, -1], [1, -1]];
const rot = (x, y) => { const a = -Math.PI / 2; return [x * Math.cos(a) - y * Math.sin(a), x * Math.sin(a) + y * Math.cos(a)]; };

// --- Board: every node of the file at its axial (Q, R), rotated so a corner points up. Rebuilt after every edit.
// Corners clockwise from the top. Families are the wedges between them.
let STATS, FAMS, N, d, PASS, CV, pts, idx, ABI, HEROES, sigAt;
const at = ([q, r]) => idx.get(q + ',' + r);
function build() {
  STATS = B.Stats.map(s => s.Id);
  FAMS = B.Families.map(f => f.Name);
  B.Stats.forEach(s => document.documentElement.style.setProperty('--s-' + s.Id, s.Color));
  N = B.Radius; d = R / N;
  PASS = (B.Costs && B.Costs.PlannerPassThroughAbility) || 4;
  CV = STATS.map((_, i) => { const a = (-90 + 60 * i) * Math.PI / 180; return [Math.cos(a), Math.sin(a)]; });
  idx = new Map(); ABI = {};
  pts = B.Nodes.map((n, i) => {
    const [x, y] = rot((n.Q + n.R / 2) * d, (n.R * Math.sqrt(3) / 2) * d);
    const p = { i, q: n.Q, r: n.R, x, y, kind: n.Kind, ab: n.Kind !== 'Passive', note: n.Note || '' };
    if (n.Kind === 'Ability' && n.Ability) { p.a = ABI[n.Ability] = { Name: n.Ability, Classes: [], ...D.abilities[n.Ability], id: n.Ability, node: i }; }
    if (n.Kind === 'Passive') p.pas = n.Stats || {};
    idx.set(n.Q + ',' + n.R, i);
    return p;
  });
  pts.forEach(p => { p.nb = DIRS.map(([a, b]) => idx.get((p.q + a) + ',' + (p.r + b))).filter(v => v !== undefined); });
  // Heroes: signature and start path straight from the file; the kit count comes from the class loadout in data.js.
  HEROES = B.Heroes.map((h, k) => {
    const c = h.Class && CLS[h.Class], sig = h.Signature && ABI[h.Signature] ? h.Signature : null;
    const kit = c ? c.Loadout.filter(id => ABI[id]) : [];
    return { k, h, id: String(k), cls: h.Class, name: h.Name, status: h.Status, sig, kit: [...new Set([sig, ...kit].filter(Boolean))],
      start: (h.Start || []).map(at).filter(v => v !== undefined) };
  }).sort((a, b) => a.name.localeCompare(b.name));
  sigAt = {};
  HEROES.filter(h => h.sig && h.start.length).forEach(h => { (sigAt[ABI[h.sig].node] = sigAt[ABI[h.sig].node] || []).push(h); });
}

// Stat weights at a point: barycentric in its wedge (corner A, corner B, centre). Only colours and family labels use it.
function weights(x, y) {
  const ang = (Math.atan2(y, x) * 180 / Math.PI + 90 + 360) % 360, k = Math.floor(ang / 60) % 6;
  const A = CV[k], B = CV[(k + 1) % 6], det = A[0] * B[1] - A[1] * B[0];
  const a = (x / R * B[1] - y / R * B[0]) / det, b = (A[0] * y / R - A[1] * x / R) / det;
  return { k, a: Math.max(0, a), b: Math.max(0, b), o: Math.max(0, 1 - a - b) };
}
const pasLabel = m => Object.entries(m).map(([s, v]) => `+${v} ${s}`).join(' ') || 'no stats';
const recipeLabel = m => Object.entries(m || {}).map(([s, v]) => `${s} ${v}`).join(' · ');

// --- Planner: cheapest walk. Passives cost 1, passing through another ability costs PASS, so paths snake through passives.
function bfs(from, to) {
  const n = pts.length, dist = new Float64Array(n).fill(Infinity), prev = new Int32Array(n).fill(-1), done = new Uint8Array(n);
  const src = new Set(from); from.forEach(f => dist[f] = 0);
  for (;;) {
    let u = -1, best = Infinity; for (let i = 0; i < n; i++) if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
    if (u < 0 || u === to) break; done[u] = 1;
    for (const v of pts[u].nb) { const w = v === to ? 1 : pts[v].a ? PASS : 1; if (dist[u] + w < dist[v]) { dist[v] = dist[u] + w; prev[v] = u; } }
  }
  const path = []; let u = to; while (u !== -1 && !src.has(u)) { path.push(u); u = prev[u]; } return path.reverse();
}

// --- Validation: the client-side half of tools/skillhex/validate.py. Abilities and classes come from data.js, not
// abilities.json / classes.json, and the signature-distance warning and the schema check stay in validate.py.
function validate(doc) {
  const E = [], W = [], err = m => E.push(m), warn = m => W.push(m), P = (q, r) => `(${q}, ${r})`;
  const stats = (doc.Stats || []).map(s => s && s.Id);
  if (stats.length !== 6) err(`Stats: expected 6, found ${stats.length}`);
  if (new Set(stats).size !== stats.length) err('Stats: duplicate ids');
  stats.forEach(s => { if (!STAT_IDS.includes(s)) err(`Stats: unknown stat ${s}`); });
  const fams = doc.Families || [];
  if (fams.length !== 6) err(`Families: expected 6, found ${fams.length}`);
  if (new Set(fams.map(f => f.Id)).size !== fams.length) err('Families: duplicate ids');
  if (stats.length === 6) fams.slice(0, 6).forEach((f, k) => { const a = stats[k], b = stats[(k + 1) % 6];
    if (f.From !== a || f.To !== b) err(`Families[${k}] ${f.Id}: runs ${f.From}->${f.To}, corner order needs ${a}->${b}`); });
  const n = doc.Radius, inLat = (q, r) => Math.max(Math.abs(q), Math.abs(r), Math.abs(q + r)) <= n;
  const slot = (q, r) => ((q - r) % 3 + 3) % 3 === 0;
  const seen = new Map(), placed = new Map();
  if (!Number.isInteger(n) || n < 2) err(`Radius: expected an integer >= 2, found ${n}`);
  else {
    (doc.Nodes || []).forEach((node, i) => {
      if (!node || !Number.isInteger(node.Q) || !Number.isInteger(node.R)) { err(`Nodes[${i}]: needs integer Q and R`); return; }
      const q = node.Q, r = node.R, p = P(q, r), kind = node.Kind, s = slot(q, r);
      seen.set(q + ',' + r, (seen.get(q + ',' + r) || 0) + 1);
      if (!inLat(q, r)) { err(`node ${p}: outside the radius-${n} lattice`); return; }
      if ((kind === 'Ability' || kind === 'Open') && !s) err(`node ${p}: ${kind} on a passive point ((Q-R) mod 3 != 0)`);
      else if (kind === 'Passive' && s) err(`node ${p}: Passive on an ability slot ((Q-R) mod 3 == 0)`);
      else if (!['Ability', 'Open', 'Passive'].includes(kind)) err(`node ${p}: unknown Kind ${kind}`);
      if (kind === 'Ability') {
        const aid = node.Ability;
        if (!aid) err(`node ${p}: Ability node without an Ability id`);
        else {
          if (!placed.has(aid)) placed.set(aid, []); placed.get(aid).push(P(q, r));
          const a = D.abilities[aid];
          if (!a) err(`node ${p}: ability ${aid} is not in data.js`);
          else if (a.MergedInto) err(`node ${p}: ability ${aid} is an alias stub (MergedInto ${a.MergedInto}); place ${a.MergedInto} instead`);
        }
      } else if ('Ability' in node) err(`node ${p}: Ability id on a ${kind} node`);
      if (kind === 'Passive') {
        const st = node.Stats;
        if (!st || typeof st !== 'object' || !Object.keys(st).length) err(`node ${p}: Passive without Stats`);
        else Object.entries(st).forEach(([k, v]) => {
          if (!stats.includes(k)) err(`node ${p}: passive stat ${k} is not one of the six Stats`);
          if (!Number.isInteger(v) || v < 1) err(`node ${p}: passive ${k} value ${v} must be a positive integer`);
        });
      } else if ('Stats' in node) err(`node ${p}: Stats on a ${kind} node`);
    });
    seen.forEach((c, k) => { if (c > 1) err(`node (${k.replace(',', ', ')}): listed ${c} times`); });
    const missing = [];
    for (let q = -n; q <= n; q++) for (let r = -n; r <= n; r++) if (Math.abs(q + r) <= n && !seen.has(q + ',' + r)) missing.push(P(q, r));
    if (missing.length) err(`${missing.length} lattice point(s) missing: ${missing.slice(0, 20).join(' ')}${missing.length > 20 ? ` (+${missing.length - 20} more)` : ''}`);
    placed.forEach((ps, aid) => { if (ps.length > 1) err(`ability ${aid} placed ${ps.length} times: ${ps.join(' ')}`); });
  }
  const heroes = doc.Heroes || [], byClass = {}, sigs = {}, users = {};
  heroes.forEach(h => { if (h.Class) byClass[h.Class] = (byClass[h.Class] || 0) + 1; });
  Object.entries(byClass).forEach(([c, k]) => { if (k > 1) err(`hero class ${c} listed ${k} times`); });
  D.classes.forEach(c => [...(c.Abilities || []), ...(c.Loadout || [])].forEach(a => (users[a] = users[a] || new Set()).add(c.Id)));
  heroes.forEach(h => {
    const cls = h.Class, status = h.Status, tag = `hero ${cls || h.Name || '?'}`;
    if (cls == null) { if (status !== 'New') err(`${tag}: Class is null but Status is ${status} (only New heroes may lack a class)`); }
    else if (!CLS[cls]) err(`${tag}: class ${cls} is not in data.js`);
    const rec = h.Recipe;
    if (rec == null) { if (status !== 'Cut') err(`${tag}: ${status} hero without a Recipe`); }
    else if (typeof rec === 'object') {
      const total = Object.values(rec).filter(Number.isInteger).reduce((s, v) => s + v, 0);
      if (total !== RECIPE_POINTS) err(`${tag}: Recipe ${recipeLabel(rec)} sums to ${total}, not ${RECIPE_POINTS}`);
      Object.keys(rec).forEach(s => { if (!STAT_IDS.includes(s)) err(`${tag}: Recipe stat ${s} is not one of the six Stats`); });
    }
    const cells = [];
    (h.Start || []).forEach(c => { if (Array.isArray(c) && c.length === 2 && c.every(Number.isInteger)) cells.push(c[0] + ',' + c[1]); else err(`${tag}: bad Start entry ${JSON.stringify(c)}`); });
    const set = new Set(cells);
    if (set.size !== cells.length) warn(`${tag}: Start lists a point more than once`);
    const off = [...set].filter(k => { const [q, r] = k.split(',').map(Number); return Number.isInteger(n) && !inLat(q, r); });
    if (off.length) err(`${tag}: Start points outside the lattice: ${off.join(' ')}`);
    if (!cells.length && status !== 'New') err(`${tag}: ${status} hero with an empty Start`);
    off.forEach(k => set.delete(k));
    if (set.size) {
      const first = set.values().next().value, reach = new Set([first]), stack = [first];
      while (stack.length) { const [q, r] = stack.pop().split(',').map(Number);
        DIRS.forEach(([a, b]) => { const k = (q + a) + ',' + (r + b); if (set.has(k) && !reach.has(k)) { reach.add(k); stack.push(k); } }); }
      if (reach.size !== set.size) err(`${tag}: Start is not connected; cut off from the rest: ${[...set].filter(k => !reach.has(k)).map(k => `(${k.replace(',', ', ')})`).join(' ')}`);
    }
    const sig = h.Signature;
    if (sig == null) { if (status !== 'New') err(`${tag}: ${status} hero without a Signature`); return; }
    (sigs[sig] = sigs[sig] || []).push(cls || h.Name);
    if (!D.abilities[sig]) err(`${tag}: Signature ${sig} is not in data.js`);
    const where = placed.get(sig);
    if (!where) err(`${tag}: Signature ${sig} is not placed on the board`);
    else if (!where.some(p => set.has(p.slice(1, -1).replace(', ', ',')))) err(`${tag}: Start does not hold its Signature ${sig} at ${where[0]}`);
    const others = [...(users[sig] || [])].filter(c => c !== cls);
    if (others.length) warn(`${tag}: Signature ${sig} is not unique to its class; also in ${others.sort().join(', ')}`);
  });
  Object.entries(sigs).forEach(([sig, owners]) => { if (owners.length > 1) err(`Signature ${sig} shared by ${owners.join(', ')}`); });
  return { errors: E, warnings: W };
}

// --- Save format: the layout regenerate.py writes (one Stat, Family, Node or Hero per line, Python json.dumps spacing).
const pj = v => Array.isArray(v) ? '[' + v.map(pj).join(', ') + ']'
  : v && typeof v === 'object' ? '{' + Object.entries(v).map(([k, x]) => JSON.stringify(k) + ': ' + pj(x)).join(', ') + '}' : JSON.stringify(v);
function dump(doc) {
  const keys = Object.keys(doc), lines = ['{'];
  keys.forEach((k, i) => {
    const v = doc[k], end = i < keys.length - 1 ? ',' : '';
    if (Array.isArray(v) && ['Nodes', 'Heroes', 'Stats', 'Families'].includes(k)) {
      lines.push(`  ${JSON.stringify(k)}: [`);
      v.forEach((x, j) => lines.push('    ' + pj(x) + (j < v.length - 1 ? ',' : '')));
      lines.push('  ]' + end);
    } else lines.push(`  ${JSON.stringify(k)}: ${pj(v)}${end}`);
  });
  lines.push('}');
  return lines.join('\n') + '\n';
}

// --- State
let hero, owned = [], history = [], showNames = true, preview = [], mode = 'plan', selN = -1;
let undoStack = [], dirty = false, fileName = 'skillhex.json', fileHandle = null, report = { errors: [], warnings: [] };
function setHero(h) { hero = h; owned = startPath(h); history = []; preview = []; $('hero').value = h.id; render(); }
const startPath = h => h.start.slice();
function addPath(target) { if (owned.includes(target)) return; const p = bfs(owned, target); history.push(p.length); owned = owned.concat(p); preview = []; render(); }

// Every edit goes through here: snapshot for Undo, mutate B, rebuild, revalidate, redraw.
function edit(fn) {
  undoStack.push(JSON.stringify(B)); if (undoStack.length > 50) undoStack.shift();
  fn(); dirty = true; refresh();
}
function refresh() {
  const k = hero ? hero.k : -1;
  build(); fillHeroes();
  hero = HEROES.find(h => h.k === k) || HEROES[0]; $('hero').value = hero.id;
  if (mode === 'path') { owned = startPath(hero); history = []; }
  else owned = owned.filter(i => i < pts.length);
  preview = []; render(); if (selN >= 0) editor(selN);
}
// Rebuild a node with the file's key order: Q, R, Kind, Ability | Stats, Note.
function setNode(i, f) {
  const o = B.Nodes[i], n = { Q: o.Q, R: o.R, Kind: f.Kind };
  if (f.Kind === 'Ability') n.Ability = f.Ability;
  if (f.Kind === 'Passive') n.Stats = f.Stats;
  const note = 'Note' in f ? f.Note : o.Note;
  if (note) n.Note = note;
  B.Nodes[i] = n;
}

// --- Render
function render() {
  svg.textContent = '';
  const ownedSet = new Set(owned), startSet = new Set(owned.slice(0, owned.length - history.reduce((s, n) => s + n, 0))), prevSet = new Set(preview);
  const gB = el('g', {}, svg), gE = el('g', {}, svg), gO = el('g', {}, svg), gN = el('g', {}, svg), gT = el('g', {}, svg);
  // wedges and corner labels
  for (let k = 0; k < 6; k++) {
    const A = CV[k], B = CV[(k + 1) % 6];
    el('path', { d: `M0 0 L${A[0] * R} ${A[1] * R} L${B[0] * R} ${B[1] * R} Z`, fill: sc(STATS[k]), 'fill-opacity': 0.035, stroke: css('--line'), 'stroke-width': 1 }, gB);
    const t = el('text', { x: A[0] * (R + 40), y: A[1] * (R + 40), fill: sc(STATS[k]), 'font-family': css('--display'), 'font-weight': 700, 'font-size': 26, 'text-anchor': 'middle', 'dominant-baseline': 'middle' }, gT); t.textContent = STATS[k];
    const m = [(A[0] + B[0]) / 2 * (R + 34), (A[1] + B[1]) / 2 * (R + 34)];
    const f = el('text', { x: m[0], y: m[1], fill: css('--muted'), 'font-family': css('--display'), 'font-weight': 600, 'font-size': 15, 'letter-spacing': 3, 'text-anchor': 'middle', 'dominant-baseline': 'middle' }, gT); f.textContent = (FAMS[k] || '').toUpperCase();
  }
  // lattice links: ability spokes brighter than passive-to-passive
  pts.forEach(p => p.nb.forEach(j => { if (j < p.i) return; const o = pts[j]; const ab = p.ab || o.ab;
    el('line', { x1: p.x, y1: p.y, x2: o.x, y2: o.y, stroke: css('--muted'), 'stroke-opacity': ab ? 0.13 : 0.06, 'stroke-width': 1 }, gE); }));
  // owned trail and preview
  const trail = (list, col, w) => { for (let i = 0; i < list.length; i++) { const p = pts[list[i]]; p.nb.forEach(j => { if (j > p.i && (list.includes(j))) el('line', { x1: p.x, y1: p.y, x2: pts[j].x, y2: pts[j].y, stroke: col, 'stroke-width': w, 'stroke-linecap': 'round', 'stroke-opacity': 0.9 }, gO); }); } };
  trail([...startSet], css('--hot'), 4); trail(owned.filter(n => !startSet.has(n)), css('--accent'), 4); if (preview.length) trail([...preview, ...owned.filter(n => pts[n].nb.some(j => prevSet.has(j)))], css('--fg'), 2);
  // nodes
  pts.forEach(p => {
    const own = ownedSet.has(p.i), pv = prevSet.has(p.i);
    if (p.ab) {
      const a = p.a, r = a ? 6.5 : 3;
      if (!a) { el('circle', { cx: p.x, cy: p.y, r, fill: own ? css('--hot') : 'none', stroke: p.note ? css('--accent') : css('--line'), 'stroke-width': 1, 'pointer-events': 'all', class: 'open', 'data-tip': 'n:' + p.i }, gN); return; }
      const wt = weights(p.x, p.y); const col = wt.o > 0.8 ? css('--fg') : sc(STATS[wt.a >= wt.b ? wt.k : (wt.k + 1) % 6]);
      const c = el('circle', { cx: p.x, cy: p.y, r: own ? 8 : r, fill: own ? col : css('--panel'), stroke: pv ? css('--fg') : col, 'stroke-width': own ? 2.5 : 2, class: 'node', 'data-tip': 'n:' + p.i }, gN);
      if (a.Kind === 'Spell') c.setAttribute('stroke-dasharray', '3 2');
      if (sigAt[p.i] && showNames) { const t = el('text', { x: p.x, y: p.y - 11, fill: sigAt[p.i].includes(hero) ? css('--hot') : css('--fg'), 'font-family': css('--body'), 'font-size': 9.5, 'font-weight': sigAt[p.i].includes(hero) ? 700 : 500, 'text-anchor': 'middle', 'pointer-events': 'none', opacity: 0.85 }, gT); t.textContent = sigAt[p.i].map(h => h.name).join(' / '); }
    } else {
      const s = Object.keys(p.pas)[0], col = s ? sc(s) : css('--muted');
      el('rect', { x: p.x - 2.6, y: p.y - 2.6, width: 5.2, height: 5.2, transform: `rotate(45 ${p.x} ${p.y})`, fill: own ? col : 'none', stroke: col, 'stroke-opacity': own ? 1 : 0.45, 'stroke-width': 1, class: 'node', 'data-tip': 'n:' + p.i }, gN);
    }
  });
  if (mode !== 'plan' && pts[selN]) el('circle', { cx: pts[selN].x, cy: pts[selN].y, r: 12, fill: 'none', stroke: css('--hot'), 'stroke-width': 2, 'pointer-events': 'none' }, gT);
  panel();
}
function totals(list) { const t = Object.fromEntries(STATS.map(s => [s, 0])); list.forEach(n => { const p = pts[n]; if (p.pas) Object.entries(p.pas).forEach(([s, v]) => t[s] += v); }); return t; }
function panel() {
  const abil = owned.filter(n => pts[n].a), pas = owned.filter(n => !pts[n].ab);
  const startLen = owned.length - history.reduce((s, n) => s + n, 0);
  const sigName = hero.sig ? `<b style="color:var(--hot)">${esc(ABI[hero.sig].Name)}</b>` : '<b>none yet</b>';
  $('ptitle').textContent = hero.name;
  $('pbody').innerHTML = mode === 'plan'
    ? `Joins with ${hero.kit.length} moves; signature: ${sigName}. Gold trail is the starting path, blue is what you have added. Click any ability or passive to walk to it.`
    : mode === 'path'
      ? `Editing the Start path (gold); signature: ${sigName}. Click points to add or remove them; pick an ability to make it the signature.`
      : 'Editing the board. Click an ability slot to assign, move or clear an ability or note an Open slot; click a passive to change its stats.';
  $('sum').textContent = `${owned.length} nodes · ${abil.length} abilities · ${pas.length} passives · ${owned.length - startLen} SP spent beyond the start`;
  const t = totals(owned), st = $('stats'); st.textContent = '';
  STATS.forEach(s => { const dv = document.createElement('div'); dv.innerHTML = `<span style="color:${sc(s)}">${s}</span><span>+${t[s]}</span>`; st.appendChild(dv); });
  const kit = $('kit'); kit.textContent = '';
  abil.forEach(n => { const a = pts[n].a, from = owned.indexOf(n) < startLen; const dv = document.createElement('div'); const wt = weights(pts[n].x, pts[n].y), st = wt.o > 0.8 ? 'ANY' : STATS[wt.a >= wt.b ? wt.k : (wt.k + 1) % 6];
    dv.innerHTML = `<span class="tag" style="background:${st === 'ANY' ? css('--fg') : sc(st)}">${st}</span><span>${esc(a.Name)}</span><span class="m">${from ? 'start' : 'learned'}${a.id === hero.sig ? ' · signature' : ''}</span>`; kit.appendChild(dv); });
  others(); validation();
}
function tipHtml(n) {
  const p = pts[n];
  if (!p.ab) { const w = weights(p.x, p.y); return `<div class="k">Passive · ${esc(FAMS[w.k])}</div><h3>${esc(pasLabel(p.pas))}</h3>${p.note ? `<p>${esc(p.note)}</p>` : ''}${owned.includes(n) ? '<p class="m">Owned</p>' : `<p class="m">${bfs(owned, n).length} SP from your build</p>`}`; }
  if (!p.a) return `<div class="k">${esc(FAMS[weights(p.x, p.y).k])} · Open slot</div><h3>Open</h3><p>${esc(p.note || 'No ability here yet: a gap kept for future content or DLC.')}</p>`;
  const a = p.a, req = Object.entries(a.Requires || {}).map(([k, v]) => `${FROM[k] || k} ${v}`).join(', ');
  const heroes = a.Classes.map(c => CLS[c] ? CLS[c].Name : c), sig = sigAt[n] ? sigAt[n].map(h => h.name).join(', ') : '';
  const path = owned.includes(n) ? null : bfs(owned, n), gain = path ? path.filter(i => pts[i].a && i !== n).map(i => pts[i].a.Name) : [];
  const t = path ? totals(path) : null;
  return `<div class="k">${esc(FAMS[weights(p.x, p.y).k])} · ${esc(a.Kind || 'Move')}${a.Cost ? ' · ' + a.Cost + ' orb' + (a.Cost > 1 ? 's' : '') : ''}${req ? ' · needs ' + esc(req) : ''}</div><h3>${esc(a.Name)}</h3>${sig ? `<p style="color:var(--hot)">Signature of ${esc(sig)}</p>` : ''}${a.Desc ? `<p>${esc(a.Desc)}</p>` : ''}${p.note ? `<p>${esc(p.note)}</p>` : ''}${path ? `<p><b>${path.length} SP</b> from your build${gain.length ? `, passing ${esc(gain.join(', '))}` : ''}; passives on the way ${esc(STATS.filter(s => t[s]).map(s => `+${t[s]} ${s}`).join(' ') || 'none')}.</p>` : '<p class="m">Owned</p>'}<p class="m">Starts in the kit of ${esc(heroes.slice(0, 6).join(', '))}${heroes.length > 6 ? ` and ${heroes.length - 6} more` : ''}.</p>`;
}

// --- Editors (in the detail box)
const abName = id => (D.abilities[id] && D.abilities[id].Name) || id;
function editor(n) {
  const p = pts[n], box = $('detail'), node = B.Nodes[n], where = `${p.q}, ${p.r} · ${esc(FAMS[weights(p.x, p.y).k])}`;
  if (mode === 'path') {
    const on = owned.includes(n), canSig = p.a && p.a.id !== hero.sig;
    box.innerHTML = `${tipHtml(n)}<div class="edit"><p class="m">${on ? 'On' : 'Not on'} ${esc(hero.name)}'s Start path.</p>
      <div class="row">${canSig ? `<button class="btn" id="e-sig" type="button">Make it ${esc(hero.name)}'s signature</button>` : ''}<button class="btn" id="e-clear" type="button">Clear Start path</button></div></div>`;
    if (canSig) $('e-sig').onclick = () => edit(() => { const h = hero.h; h.Signature = p.a.id; const s = h.Start || (h.Start = []); if (!s.some(c => c[0] === p.q && c[1] === p.r)) s.push([p.q, p.r]); });
    $('e-clear').onclick = () => edit(() => { hero.h.Start = []; });
    return;
  }
  if (p.ab) {
    const ids = Object.keys(D.abilities).sort((a, b) => abName(a).localeCompare(abName(b)));
    const opt = id => { const on = ABI[id], here = on && on.node === n; return `<option value="${esc(id)}"${here ? ' selected' : ''}>${esc(abName(id))}${here ? '' : on ? ` (at ${pts[on.node].q}, ${pts[on.node].r})` : ' (not on the board)'}</option>`; };
    box.innerHTML = `<div class="k">${where} · ability slot</div><h3>${p.a ? esc(p.a.Name) : 'Open'}</h3><div class="edit">
      <label>Ability <select id="e-ab"><option value="">Open (no ability)</option>${ids.map(opt).join('')}</select></label>
      <label>Note <textarea id="e-note" rows="2" placeholder="What this slot is for (future content, DLC)">${esc(node.Note || '')}</textarea></label>
      <p class="m">Picking an ability placed elsewhere moves it here; this slot's ability goes to its old place.</p>
      <div class="row"><button class="btn" id="e-apply" type="button">Apply</button></div></div>`;
    $('e-apply').onclick = () => { const aid = $('e-ab').value, note = $('e-note').value.trim();
      edit(() => {
        const from = aid && ABI[aid] ? ABI[aid].node : -1;
        if (from >= 0 && from !== n) setNode(from, node.Kind === 'Ability' && node.Ability ? { Kind: 'Ability', Ability: node.Ability } : { Kind: 'Open' });
        setNode(n, aid ? { Kind: 'Ability', Ability: aid, Note: note } : { Kind: 'Open', Note: note });
      }); };
    return;
  }
  box.innerHTML = `<div class="k">${where} · passive</div><h3>${esc(pasLabel(p.pas))}</h3><div class="edit">
    <div class="pas">${STATS.map(s => `<label style="color:${sc(s)}">${s}<input type="number" min="0" max="9" step="1" data-s="${s}" value="${p.pas[s] || 0}"></label>`).join('')}</div>
    <label>Note <textarea id="e-note" rows="2">${esc(node.Note || '')}</textarea></label>
    <p class="m" id="e-msg"></p><div class="row"><button class="btn" id="e-apply" type="button">Apply</button></div></div>`;
  $('e-apply').onclick = () => {
    const stats = {}; box.querySelectorAll('input[data-s]').forEach(i => { const v = Math.round(+i.value); if (v > 0) stats[i.dataset.s] = v; });
    if (!Object.keys(stats).length) { $('e-msg').textContent = 'A passive needs at least one stat above 0.'; return; }
    edit(() => setNode(n, { Kind: 'Passive', Stats: stats, Note: $('e-note').value.trim() }));
  };
}

// --- Side panels: heroes off the board and the validation list
function others() {
  const box = $('others'); box.textContent = '';
  B.Heroes.filter(h => h.Status !== 'Placed').forEach(h => {
    const dv = document.createElement('div'), sig = h.Signature && ABI[h.Signature] ? ABI[h.Signature].Name : '';
    const info = h.Status === 'New' ? [recipeLabel(h.Recipe), h.Note].filter(Boolean).join(' · ') : (sig ? 'signature ' + sig : '') + (h.Note ? ' · ' + h.Note : '');
    dv.innerHTML = `<span class="tag" style="background:${h.Status === 'New' ? css('--accent') : css('--muted')}">${esc(String(h.Status).toUpperCase())}</span><span style="flex:none">${esc(h.Name)}</span><span class="m">${esc(info)}</span>`;
    box.appendChild(dv);
  });
}
function validation() {
  report = validate(B);
  const { errors, warnings } = report, box = $('val');
  $('valsum').textContent = `${errors.length} error${errors.length === 1 ? '' : 's'}, ${warnings.length} warning${warnings.length === 1 ? '' : 's'}`;
  $('valsum').className = errors.length ? 'bad' : 'ok';
  box.innerHTML = errors.map(e => `<div class="bad">${esc(e)}</div>`).concat(warnings.map(w => `<div class="m">${esc(w)}</div>`)).join('');
  $('file').textContent = `${fileName}${dirty ? ' · unsaved edits' : ''}`;
  $('undoedit').disabled = !undoStack.length;
}

// --- Open and save
const readText = f => new Promise((ok, no) => { const r = new FileReader(); r.onload = () => ok(r.result); r.onerror = () => no(r.error); r.readAsText(f); });
const JSON_TYPES = [{ description: 'Skill hexagon board', accept: { 'application/json': ['.json'] } }];
function load(text, name, handle) {
  let doc;
  try { doc = JSON.parse(text.replace(/^﻿/, '')); } catch (e) { alert(`${name} is not valid JSON: ${e.message}`); return; }
  if (!doc || !Number.isInteger(doc.Radius) || ![doc.Stats, doc.Families, doc.Nodes, doc.Heroes].every(Array.isArray)) { alert(`${name} is not a skill hexagon board (needs Radius, Stats, Families, Nodes and Heroes).`); return; }
  B = doc; fileName = name; fileHandle = handle; undoStack = []; dirty = false; selN = -1; $('detail').innerHTML = '';
  build(); fillHeroes(); setHero(HEROES.find(h => h.cls === 'warden') || HEROES[0]);
}
async function openFile() {
  if (dirty && !confirm('Discard the unsaved edits and open another file?')) return;
  if (!window.showOpenFilePicker) { $('pick').click(); return; }
  try { const [h] = await showOpenFilePicker({ types: JSON_TYPES }); const f = await h.getFile(); load(await readText(f), f.name, h); }
  catch (e) { if (e.name !== 'AbortError') alert(e.message); }
}
$('pick').onchange = async () => { const f = $('pick').files[0]; $('pick').value = ''; if (f) load(await readText(f), f.name, null); };
async function save() {
  if (report.errors.length && !confirm(`The board has ${report.errors.length} validation error(s). Save anyway?`)) return;
  const text = dump(B);
  if (window.showSaveFilePicker) {
    try {
      if (!fileHandle) fileHandle = await showSaveFilePicker({ suggestedName: fileName, types: JSON_TYPES });
      const w = await fileHandle.createWritable(); await w.write(text); await w.close();
      fileName = fileHandle.name; dirty = false; validation(); return;
    } catch (e) { if (e.name === 'AbortError') return; fileHandle = null; }
  }
  const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([text], { type: 'application/json' })); a.download = fileName;
  document.body.appendChild(a); a.click(); a.remove(); setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  dirty = false; validation();
}
// Copy JSON: the claude.ai artifact viewer blocks downloads and the file pickers, so Save cannot write there.
// Copying does not count as saving: the edits stay marked unsaved until they are pasted into skillhex.json and saved.
async function copyJson() {
  if (report.errors.length && !confirm(`The board has ${report.errors.length} validation error(s). Copy anyway?`)) return;
  const text = dump(B);
  let ok = false;
  try { await navigator.clipboard.writeText(text); ok = true; } catch (e) { /* clipboard API blocked; fall back below */ }
  if (!ok) {
    const t = document.createElement('textarea'); t.value = text; t.setAttribute('readonly', ''); t.style.position = 'fixed'; t.style.opacity = '0';
    document.body.appendChild(t); t.select();
    try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
    t.remove();
  }
  const b = $('copy');
  if (!ok) { alert('Copying was blocked here. Open this page from docs/design/skill-hexagon/index.html and use Save instead.'); return; }
  b.textContent = 'Copied'; setTimeout(() => { b.textContent = 'Copy JSON'; }, 1500);
}
window.addEventListener('beforeunload', e => { if (dirty) { e.preventDefault(); e.returnValue = ''; } });

// --- Controls
const sel = $('hero');
function fillHeroes() {
  sel.textContent = '';
  HEROES.forEach(h => { const o = document.createElement('option'); o.value = h.id; o.textContent = h.status === 'Placed' ? h.name : `${h.name} (${h.status})`; sel.appendChild(o); });
}
sel.onchange = e => { setHero(HEROES.find(h => h.id === e.target.value)); if (mode === 'path' && selN >= 0) editor(selN); };
$('undo').onclick = () => { if (!history.length) return; const n = history.pop(); owned = owned.slice(0, owned.length - n); render(); };
$('reset').onclick = () => setHero(hero);
$('names').onchange = e => { showNames = e.target.checked; render(); };
$('mode').onchange = e => { mode = e.target.value; selN = -1; $('detail').innerHTML = ''; setHero(hero); };
$('open').onclick = openFile;
$('save').onclick = save;
$('copy').onclick = copyJson;
$('undoedit').onclick = () => { if (!undoStack.length) return; B = JSON.parse(undoStack.pop()); dirty = true; refresh(); };
$('savepath').textContent = CONTENT_PATH;
const place = e => { const pad = 14, w = tip.offsetWidth, h = tip.offsetHeight; let x = e.clientX + pad, y = e.clientY + pad; if (x + w > innerWidth - 8) x = e.clientX - w - pad; if (y + h > innerHeight - 8) y = e.clientY - h - pad; tip.style.left = Math.max(8, x) + 'px'; tip.style.top = Math.max(8, y) + 'px'; };
let hoverN = -1;
svg.addEventListener('pointermove', e => {
  if (e.pointerType === 'touch') return; const t = e.target.closest('[data-tip]');
  if (!t) { tip.hidden = true; if (preview.length) { preview = []; hoverN = -1; render(); } return; }
  const n = +t.getAttribute('data-tip').slice(2);
  tip.innerHTML = tipHtml(n); tip.hidden = false; place(e);
  if (mode === 'plan' && n !== hoverN) { hoverN = n; preview = owned.includes(n) || pts[n].kind === 'Open' ? [] : bfs(owned, n); render(); tip.hidden = false; }
});
svg.addEventListener('pointerleave', () => { tip.hidden = true; if (preview.length) { preview = []; hoverN = -1; render(); } });
svg.addEventListener('click', e => {
  const t = e.target.closest('[data-tip]'); if (!t) return; const n = +t.getAttribute('data-tip').slice(2); tip.hidden = true;
  if (mode === 'plan') { $('detail').innerHTML = tipHtml(n); if (pts[n].kind !== 'Open') addPath(n); return; }
  selN = n;
  if (mode === 'path') { const p = pts[n]; edit(() => { const h = hero.h, s = h.Start || (h.Start = []), j = s.findIndex(c => c[0] === p.q && c[1] === p.r); if (j >= 0) s.splice(j, 1); else s.push([p.q, p.r]); }); return; }
  render(); editor(n);
});

// Example on load: a Warden walking to White Ninja's signature.
build(); fillHeroes();
setHero(HEROES.find(h => h.cls === 'warden') || HEROES[0]);
const wn = HEROES.find(h => h.cls === 'whiteninja'); if (wn && wn.sig && hero.cls === 'warden') addPath(ABI[wn.sig].node);
})();
