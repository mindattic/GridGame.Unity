(() => {
// Draws the board from skillhex.json (window.SKILLHEX, written by tools/skillhex/sync.py): node kinds and
// positions, passive stats, ability ids, Open slots, hero start paths and signatures. data.js (window.WHEEL_DATA)
// only supplies ability and class details for tooltips. Nothing here places anything; generate.js does that,
// and only when tools/skillhex/regenerate.py rebuilds skillhex.json from content.
// editor.js edits the board in place through window.SkillHexView (bottom of this file): every edit calls load(),
// which reruns build() on the edited document.
const D = window.WHEEL_DATA;
let B, STATS, FAMS, N, d, PASS;
const FROM = { Vitality: 'VIT', Strength: 'STR', Agility: 'AGI', Speed: 'SPD', Wisdom: 'WIS', Intelligence: 'INT', Stamina: 'VIT', Luck: 'AGI' };
const R = 520;
const NS = 'http://www.w3.org/2000/svg';
const svg = document.getElementById('hex'), tip = document.getElementById('tip');
const el = (t, a, p) => { const e = document.createElementNS(NS, t); for (const k in a) e.setAttribute(k, a[k]); if (p) p.appendChild(e); return e; };
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[c]));
const css = v => getComputedStyle(document.documentElement).getPropertyValue(v).trim();
const sc = s => css('--s-' + s);
const CV = [0, 1, 2, 3, 4, 5].map(i => { const a = (-90 + 60 * i) * Math.PI / 180; return [Math.cos(a), Math.sin(a)]; });
const CLS = Object.fromEntries(D.classes.map(c => [c.Id, c]));

// --- Board: every node of skillhex.json at its axial (Q, R), rotated so a corner points up.
const rot = (x, y) => { const a = -Math.PI / 2; return [x * Math.cos(a) - y * Math.sin(a), x * Math.sin(a) + y * Math.cos(a)]; };
const DIRS = [[1, 0], [0, 1], [-1, 1], [-1, 0], [0, -1], [1, -1]];
let idx, ABI, pts, HEROES, sigAt;
function build(doc) {
// Corners clockwise from the top. Families are the wedges between them.
B = doc; STATS = B.Stats.map(s => s.Id); FAMS = B.Families.map(f => f.Name);
B.Stats.forEach(s => document.documentElement.style.setProperty('--s-' + s.Id, s.Color));
N = B.Radius; d = R / N; PASS = (B.Costs && B.Costs.PlannerPassThroughAbility) || 4;
idx = new Map(); ABI = {};
pts = B.Nodes.map((n, i) => {
  const [x, y] = rot((n.Q + n.R / 2) * d, (n.R * Math.sqrt(3) / 2) * d);
  const p = { i, q: n.Q, r: n.R, x, y, kind: n.Kind, ab: n.Kind !== 'Passive', note: n.Note || '' };
  if (n.Kind === 'Ability') { p.a = ABI[n.Ability] = { Name: n.Ability, Classes: [], ...D.abilities[n.Ability], id: n.Ability, node: i }; }
  if (n.Kind === 'Passive') p.pas = n.Stats;
  idx.set(n.Q + ',' + n.R, i);
  return p;
});
pts.forEach(p => { p.nb = DIRS.map(([a, b]) => idx.get((p.q + a) + ',' + (p.r + b))).filter(v => v !== undefined); });
// --- Heroes: signature and start path straight from the file; the kit count comes from the class loadout in data.js.
HEROES = B.Heroes.filter(h => h.Class && h.Signature && ABI[h.Signature] && (h.Start || []).length).map(h => {
  const c = CLS[h.Class], kit = c ? c.Loadout.filter(id => ABI[id]) : [];
  return { id: h.Class, name: h.Name, status: h.Status, sig: h.Signature, kit: [...new Set([h.Signature, ...kit])], start: h.Start.map(at).filter(v => v !== undefined) };
}).sort((a, b) => a.name.localeCompare(b.name));
sigAt = {}; HEROES.forEach(h => { (sigAt[ABI[h.sig].node] = sigAt[ABI[h.sig].node] || []).push(h); });
}
const at = ([q, r]) => idx.get(q + ',' + r);

// Stat weights at a point: barycentric in its wedge (corner A, corner B, centre). Only colours and family labels use it.
function weights(x, y) {
  const ang = (Math.atan2(y, x) * 180 / Math.PI + 90 + 360) % 360, k = Math.floor(ang / 60) % 6;
  const A = CV[k], B = CV[(k + 1) % 6], det = A[0] * B[1] - A[1] * B[0];
  const a = (x / R * B[1] - y / R * B[0]) / det, b = (A[0] * y / R - A[1] * x / R) / det;
  return { k, a: Math.max(0, a), b: Math.max(0, b), o: Math.max(0, 1 - a - b) };
}
const pasLabel = m => Object.entries(m).map(([s, v]) => `+${v} ${s}`).join(' ');
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

const startPath = h => h.start.slice();

// --- State
build(window.SKILLHEX);
let hero = HEROES.find(h => h.id === 'warden') || HEROES[0];
let owned = [], history = [], showNames = true, preview = [], ED = null;
function setHero(h) { hero = h; owned = startPath(h); history = []; preview = []; document.getElementById('hero').value = h.id; render(); }
function addPath(target) { if (owned.includes(target)) return; const p = bfs(owned, target); history.push(p.length); owned = owned.concat(p); preview = []; render(); }

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
    const f = el('text', { x: m[0], y: m[1], fill: css('--muted'), 'font-family': css('--display'), 'font-weight': 600, 'font-size': 15, 'letter-spacing': 3, 'text-anchor': 'middle', 'dominant-baseline': 'middle' }, gT); f.textContent = FAMS[k].toUpperCase();
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
      if (!a) { el('circle', { cx: p.x, cy: p.y, r, fill: 'none', stroke: css('--line'), 'stroke-width': 1, 'pointer-events': 'all', class: 'open', 'data-tip': 'n:' + p.i }, gN); return; }
      const wt = weights(p.x, p.y); const col = wt.o > 0.8 ? css('--fg') : sc(STATS[wt.a >= wt.b ? wt.k : (wt.k + 1) % 6]);
      const c = el('circle', { cx: p.x, cy: p.y, r: own ? 8 : r, fill: own ? col : css('--panel'), stroke: pv ? css('--fg') : col, 'stroke-width': own ? 2.5 : 2, class: 'node', 'data-tip': 'n:' + p.i }, gN);
      if (a.Kind === 'Spell') c.setAttribute('stroke-dasharray', '3 2');
      if (sigAt[p.i] && showNames) { const t = el('text', { x: p.x, y: p.y - 11, fill: sigAt[p.i].includes(hero) ? css('--hot') : css('--fg'), 'font-family': css('--body'), 'font-size': 9.5, 'font-weight': sigAt[p.i].includes(hero) ? 700 : 500, 'text-anchor': 'middle', 'pointer-events': 'none', opacity: 0.85 }, gT); t.textContent = sigAt[p.i].map(h => h.name).join(' / '); }
    } else {
      const s = Object.keys(p.pas)[0], col = sc(s);
      el('rect', { x: p.x - 2.6, y: p.y - 2.6, width: 5.2, height: 5.2, transform: `rotate(45 ${p.x} ${p.y})`, fill: own ? col : 'none', stroke: col, 'stroke-opacity': own ? 1 : 0.45, 'stroke-width': 1, class: 'node', 'data-tip': 'n:' + p.i }, gN);
    }
  });
  if (ED) ED.overlay(gT, el); else panel();
}
function totals(list) { const t = Object.fromEntries(STATS.map(s => [s, 0])); list.forEach(n => { const p = pts[n]; if (p.pas) Object.entries(p.pas).forEach(([s, v]) => t[s] += v); }); return t; }
function panel() {
  const abil = owned.filter(n => pts[n].a), pas = owned.filter(n => !pts[n].ab);
  const startLen = owned.length - history.reduce((s, n) => s + n, 0);
  document.getElementById('ptitle').textContent = hero.name;
  document.getElementById('pbody').innerHTML = `Joins with ${hero.kit.length} moves; signature: <b style="color:var(--hot)">${esc(ABI[hero.sig].Name)}</b>. Gold trail is the starting path, blue is what you have added. Click any ability or passive to walk to it.`;
  document.getElementById('sum').textContent = `${owned.length} nodes · ${abil.length} abilities · ${pas.length} passives · ${owned.length - startLen} SP spent beyond the start`;
  const t = totals(owned), st = document.getElementById('stats'); st.textContent = '';
  STATS.forEach(s => { const dv = document.createElement('div'); dv.innerHTML = `<span style="color:${sc(s)}">${s}</span><span>+${t[s]}</span>`; st.appendChild(dv); });
  const kit = document.getElementById('kit'); kit.textContent = '';
  abil.forEach((n, i) => { const a = pts[n].a, from = owned.indexOf(n) < startLen; const dv = document.createElement('div'); const wt = weights(pts[n].x, pts[n].y), st = wt.o > 0.8 ? 'ANY' : STATS[wt.a >= wt.b ? wt.k : (wt.k + 1) % 6];
    dv.innerHTML = `<span class="tag" style="background:${st === 'ANY' ? css('--fg') : sc(st)}">${st}</span><span>${esc(a.Name)}</span><span class="m">${from ? 'start' : 'learned'}${a.id === hero.sig ? ' · signature' : ''}</span>`; kit.appendChild(dv); });
}
function tipHtml(n) {
  const p = pts[n];
  if (!p.ab) { const w = weights(p.x, p.y); return `<div class="k">Passive · ${FAMS[w.k]}</div><h3>${esc(pasLabel(p.pas))}</h3>${owned.includes(n) ? '<p class="m">Owned</p>' : `<p class="m">${bfs(owned, n).length} SP from your build</p>`}`; }
  if (!p.a) return `<div class="k">${esc(FAMS[weights(p.x, p.y).k])} · Open slot</div><h3>Open</h3><p>${esc(p.note || 'No ability here yet: a gap kept for future content or DLC.')}</p>`;
  const a = p.a, req = Object.entries(a.Requires || {}).map(([k, v]) => `${FROM[k] || k} ${v}`).join(', ');
  const heroes = a.Classes.map(c => CLS[c].Name), sig = sigAt[n] ? sigAt[n].map(h => h.name).join(', ') : '';
  const path = owned.includes(n) ? null : bfs(owned, n), gain = path ? path.filter(i => pts[i].a && i !== n).map(i => pts[i].a.Name) : [];
  const t = path ? totals(path) : null;
  return `<div class="k">${esc(FAMS[weights(p.x, p.y).k])} · ${esc(a.Kind || 'Move')}${a.Cost ? ' · ' + a.Cost + ' orb' + (a.Cost > 1 ? 's' : '') : ''}${req ? ' · needs ' + esc(req) : ''}</div><h3>${esc(a.Name)}</h3>${sig ? `<p style="color:var(--hot)">Signature of ${esc(sig)}</p>` : ''}${a.Desc ? `<p>${esc(a.Desc)}</p>` : ''}${path ? `<p><b>${path.length} SP</b> from your build${gain.length ? `, passing ${esc(gain.join(', '))}` : ''}; passives on the way ${esc(STATS.filter(s => t[s]).map(s => `+${t[s]} ${s}`).join(' ') || 'none')}.</p>` : '<p class="m">Owned</p>'}<p class="m">Starts in the kit of ${esc(heroes.slice(0, 6).join(', '))}${heroes.length > 6 ? ` and ${heroes.length - 6} more` : ''}.</p>`;
}

// --- Controls
const sel = document.getElementById('hero');
function fillHeroes() { sel.textContent = ''; HEROES.forEach(h => { const o = document.createElement('option'); o.value = h.id; o.textContent = h.name; sel.appendChild(o); }); }
fillHeroes();
sel.onchange = e => setHero(HEROES.find(h => h.id === e.target.value));
document.getElementById('undo').onclick = () => { if (!history.length) return; const n = history.pop(); owned = owned.slice(0, owned.length - n); render(); };
document.getElementById('reset').onclick = () => setHero(hero);
document.getElementById('names').onchange = e => { showNames = e.target.checked; render(); };
const place = e => { const pad = 14, w = tip.offsetWidth, h = tip.offsetHeight; let x = e.clientX + pad, y = e.clientY + pad; if (x + w > innerWidth - 8) x = e.clientX - w - pad; if (y + h > innerHeight - 8) y = e.clientY - h - pad; tip.style.left = Math.max(8, x) + 'px'; tip.style.top = Math.max(8, y) + 'px'; };
let hoverN = -1;
svg.addEventListener('pointermove', e => {
  if (e.pointerType === 'touch') return; const t = e.target.closest('[data-tip]');
  if (!t) { tip.hidden = true; if (preview.length) { preview = []; hoverN = -1; render(); } return; }
  const n = +t.getAttribute('data-tip').slice(2);
  tip.innerHTML = ED ? ED.tip(n) : tipHtml(n); tip.hidden = false; place(e);
  if (ED) return;
  if (n !== hoverN) { hoverN = n; preview = owned.includes(n) || pts[n].kind === 'Open' ? [] : bfs(owned, n); render(); tip.hidden = false; }
});
svg.addEventListener('pointerleave', () => { tip.hidden = true; if (preview.length) { preview = []; hoverN = -1; render(); } });
svg.addEventListener('click', e => { const t = e.target.closest('[data-tip]'); if (!t) return; const n = +t.getAttribute('data-tip').slice(2); if (ED) { ED.click(n); tip.hidden = true; return; } document.getElementById('detail').innerHTML = tipHtml(n); if (pts[n].kind !== 'Open') addPath(n); tip.hidden = true; });

// --- Heroes off the board: Cut (leaving the roster; saves convert them) and New (a slot waiting for a hero).
const offBoard = document.getElementById('others');
const fillOthers = () => { offBoard.textContent = ''; B.Heroes.filter(h => h.Status !== 'Placed').forEach(h => {
  const dv = document.createElement('div'), sig = h.Signature && ABI[h.Signature] ? ABI[h.Signature].Name : '';
  const info = h.Status === 'New' ? [recipeLabel(h.Recipe), h.Note].filter(Boolean).join(' · ') : (sig ? 'signature ' + sig : '') + (h.Note ? ' · ' + h.Note : '');
  dv.innerHTML = `<span class="tag" style="background:${h.Status === 'New' ? css('--accent') : css('--muted')}">${esc(h.Status.toUpperCase())}</span><span style="flex:none">${esc(h.Name)}</span><span class="m">${esc(info)}</span>`;
  offBoard.appendChild(dv);
}); };
fillOthers();

// Example on load: a Warden walking to White Ninja's signature.
setHero(hero);
const wn = HEROES.find(h => h.id === 'whiteninja'); if (wn && hero.id === 'warden') addPath(ABI[wn.sig].node);

// --- Hooks for editor.js. load() swaps in an edited or opened document and rebuilds the board; while an editor is
// attached, render() draws its overlay instead of the build panel, hover and click go to it, and planning is off.
window.SkillHexView = {
  get doc() { return B; }, get pts() { return pts; }, get stats() { return STATS; }, get fams() { return FAMS; },
  at, weights, esc, css, sc, pasLabel, render,
  load(doc) {
    const prev = B; try { build(doc); } catch (err) { build(prev); throw err; }
    hero = HEROES.find(h => h.id === (hero && hero.id)) || HEROES.find(h => h.id === 'warden') || HEROES[0];
    fillHeroes(); fillOthers(); if (!ED && hero) setHero(hero);
  },
  showPath(list, heroId) { owned = list; history = []; preview = []; hoverN = -1; const h = HEROES.find(x => x.id === heroId); if (h) hero = h; render(); },
  attach(ed) { ED = ed; tip.hidden = true; owned = []; history = []; preview = []; hoverN = -1; },
  detach() { ED = null; hero = HEROES.find(h => h.id === (hero && hero.id)) || HEROES[0]; if (hero) setHero(hero); },
};
})();
