(() => {
const D = window.WHEEL_DATA;
// Corners clockwise from the top, as on the stat hexagon. Families are the wedges between them.
const STATS = ['VIT', 'STR', 'AGI', 'SPD', 'WIS', 'INT'];
const FAMS = ['Martial', 'Brawn', 'Shadow', 'Wild', 'Brains', 'Grace'];
const FROM = { Vitality: 'VIT', Strength: 'STR', Agility: 'AGI', Speed: 'SPD', Wisdom: 'WIS', Intelligence: 'INT', Stamina: 'VIT', Luck: 'AGI' };
const N = 16;                    // lattice radius: 817 points, a third of them ability slots
const R = 520, d = R / N;
const NS = 'http://www.w3.org/2000/svg';
const svg = document.getElementById('hex'), tip = document.getElementById('tip');
const el = (t, a, p) => { const e = document.createElementNS(NS, t); for (const k in a) e.setAttribute(k, a[k]); if (p) p.appendChild(e); return e; };
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[c]));
const css = v => getComputedStyle(document.documentElement).getPropertyValue(v).trim();
const sc = s => css('--s-' + s);
const CV = STATS.map((_, i) => { const a = (-90 + 60 * i) * Math.PI / 180; return [Math.cos(a), Math.sin(a)]; });

// --- Lattice: axial (q, r) inside a hexagon of radius N, rotated so a corner points up.
const pts = [], idx = new Map();
const rot = (x, y) => { const a = -Math.PI / 2; return [x * Math.cos(a) - y * Math.sin(a), x * Math.sin(a) + y * Math.cos(a)]; };
for (let q = -N; q <= N; q++) for (let r = -N; r <= N; r++) {
  if (Math.abs(q + r) > N) continue;
  const [x, y] = rot((q + r / 2) * d, (r * Math.sqrt(3) / 2) * d);
  const p = { i: pts.length, q, r, x, y, ab: ((q - r) % 3 + 3) % 3 === 0 };
  idx.set(q + ',' + r, p.i); pts.push(p);
}
const DIRS = [[1, 0], [0, 1], [-1, 1], [-1, 0], [0, -1], [1, -1]];
pts.forEach(p => { p.nb = DIRS.map(([a, b]) => idx.get((p.q + a) + ',' + (p.r + b))).filter(v => v !== undefined); });

// Stat weights at a point: barycentric in its wedge (corner A, corner B, centre).
function weights(x, y) {
  const ang = (Math.atan2(y, x) * 180 / Math.PI + 90 + 360) % 360, k = Math.floor(ang / 60) % 6;
  const A = CV[k], B = CV[(k + 1) % 6], det = A[0] * B[1] - A[1] * B[0];
  const a = (x / R * B[1] - y / R * B[0]) / det, b = (A[0] * y / R - A[1] * x / R) / det;
  return { k, a: Math.max(0, a), b: Math.max(0, b), o: Math.max(0, 1 - a - b) };
}
// Passive at each non-ability point: +2 of its leading stat, or +1/+1 where two stats are near even.
pts.filter(p => !p.ab).forEach(p => {
  const w = weights(p.x, p.y), A = STATS[w.k], B = STATS[(w.k + 1) % 6];
  if (w.o > 0.82) { p.pas = { [A]: 1, [B]: 1 }; return; }
  const t = w.a + w.b || 1, fa = w.a / t;
  p.pas = fa > 0.62 ? { [A]: 2 } : fa < 0.38 ? { [B]: 2 } : { [A]: 1, [B]: 1 };
});
const pasLabel = m => Object.entries(m).map(([s, v]) => `+${v} ${s}`).join(' ');

// --- Hero spots: the same recipes and slot geometry as the stat hexagon page.
const HERO_RECIPE = {
  // Corners: pure
  'VIT4': 'Tank', 'STR4': 'Bruiser', 'AGI4': 'Thief', 'SPD4': 'Speedster', 'WIS4': 'Sage', 'INT4': 'Harbinger',
  // Spokes: corner + its opposite (shared by both families at that corner)
  'VIT3 SPD1': 'Myrmidon', 'STR3 WIS1': 'Martial Artist', 'AGI3 INT1': 'Hex Sniper', 'SPD3 VIT1': 'Lancer', 'WIS3 STR1': 'Monster Tamer', 'INT3 AGI1': 'Vampire',
  // Spurs: corner + a stat two steps away
  'VIT3 WIS1': 'Warden', 'VIT3 AGI1': 'Shieldguard', 'STR3 INT1': 'Odachi', 'STR3 SPD1': 'Raider',
  'AGI3 VIT1': 'Ripper', 'AGI3 WIS1': 'Ronin', 'SPD3 INT1': 'Chroma Ninja', 'SPD3 STR1': 'Wild Child',
  'WIS3 VIT1': 'Hex Warden', 'WIS3 AGI1': 'Alchemist', 'INT3 SPD1': 'Lightweaver', 'INT3 STR1': 'Machinist',
  // Sides: A3B1, A2B2 (perfect hybrid), A1B3
  'VIT3 STR1': 'Knight', 'VIT2 STR2': 'Barbarian', 'VIT1 STR3': 'War Chief',
  'STR3 AGI1': 'Pugilist', 'STR2 AGI2': 'Sword Master', 'STR1 AGI3': 'Fencer',
  'AGI3 SPD1': 'Assassin', 'AGI2 SPD2': 'Monk', 'AGI1 SPD3': 'Striker',
  'SPD3 WIS1': 'Night Hunter', 'SPD2 WIS2': 'Hex Stalker', 'SPD1 WIS3': 'Blue Lion',
  'WIS3 INT1': 'Doctor', 'WIS2 INT2': 'Red Mage', 'WIS1 INT3': 'Tactician',
  'INT3 VIT1': 'Cleric', 'INT2 VIT2': 'Paladin', 'INT1 VIT3': 'Herald',
  // Inner row: two side stats plus one from the far side
  'VIT2 STR1 SPD1': 'Prize Fighter', 'VIT2 STR1 WIS1': 'Defender', 'VIT1 STR2 SPD1': 'Sellsword', 'VIT1 STR2 WIS1': 'Panda Keeper',
  'STR2 AGI1 WIS1': 'Fighter', 'STR2 AGI1 INT1': 'Duelist', 'STR1 AGI2 WIS1': 'Slasher', 'STR1 AGI2 INT1': 'Street Fighter',
  'AGI2 SPD1 INT1': 'Blue Ninja', 'AGI2 SPD1 VIT1': 'Black Ninja', 'AGI1 SPD2 INT1': 'Yellow Ninja', 'AGI1 SPD2 VIT1': 'Red Ninja',
  'SPD2 WIS1 VIT1': 'Green Ninja', 'SPD2 WIS1 STR1': 'Dervish', 'SPD1 WIS2 VIT1': 'White Ninja',
  'WIS2 INT1 STR1': 'Courier', 'WIS2 INT1 AGI1': 'Hexer', 'WIS1 INT2 STR1': 'Drifter', 'WIS1 INT2 AGI1': 'Engineer',
  'INT2 VIT1 AGI1': 'Jade Knight', 'INT2 VIT1 SPD1': 'Acolyte', 'INT1 VIT2 AGI1': 'Dark Templar', 'INT1 VIT2 SPD1': 'Ritualist'
};
const NAME2ID = Object.fromEntries(D.classes.map(c => [c.Name, c.Id]));
const heroSpot = {};
(() => {
  const S = i => STATS[((i % 6) + 6) % 6], L = (p, q, t) => [p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t], M = (p, f) => [p[0] * f * R, p[1] * f * R];
  const key = parts => parts.filter(([, w]) => w > 0).sort((a, b) => b[1] - a[1] || STATS.indexOf(a[0]) - STATS.indexOf(b[0])).map(([s, w]) => s + w).join(' ');
  const HN = {}; Object.entries(HERO_RECIPE).forEach(([k, n]) => { HN[key(k.split(' ').map(t => [t.slice(0, 3), +t.slice(3)]))] = n; });
  const put = (parts, pos) => { const n = HN[key(parts)]; if (n && NAME2ID[n]) heroSpot[NAME2ID[n]] = pos; };
  for (let k = 0; k < 6; k++) {
    const A = CV[k], B = CV[(k + 1) % 6], a = S(k), b = S(k + 1), dd = S(k + 3), e = S(k + 4);
    put([[a, 4]], M(A, 0.97));
    [[3, 1], [2, 2], [1, 3]].forEach(([x, y], i) => put([[a, x], [b, y]], M(L(A, B, (i + 1) / 4), 0.95)));
    [[2, 1, dd], [2, 1, e], [1, 2, dd], [1, 2, e]].forEach(([x, y, f], i) => put([[a, x], [b, y], [f, 1]], M(L(A, B, 0.2 + 0.2 * i), 0.62)));
    put([[a, 3], [dd, 1]], M(A, 0.36));
    put([[a, 3], [S(k - 2), 1]], M(L(A, B, 0.13), 0.8));
    put([[a, 3], [S(k + 2), 1]], M(L(A, CV[(k + 5) % 6], 0.13), 0.8));
  }
})();

// --- Abilities: each sits by the heroes that start with it (a hero's own moves cluster round its spot,
// shared moves sit between their heroes, moves many families use sit near the centre). Nearest free ability slot wins.
const CLS = Object.fromEntries(D.classes.map(c => [c.Id, c]));
const AB = Object.entries(D.abilities).map(([id, a]) => ({ id, ...a }));
const vecOf = req => { const v = {}; Object.entries(req || {}).forEach(([k, n]) => { const s = FROM[k]; if (s) v[s] = (v[s] || 0) + n; }); return v; };
const posOf = v => { let x = 0, y = 0, t = 0; Object.entries(v).forEach(([s, n]) => { const c = CV[STATS.indexOf(s)]; x += c[0] * n; y += c[1] * n; t += n; }); return t ? [x / t * R * 0.9, y / t * R * 0.9] : [0, 0]; };
AB.forEach(a => {
  a.vec = vecOf(a.Requires); a.fams = new Set(a.Classes.map(c => CLS[c].Family));
  const spots = a.Classes.map(c => heroSpot[c]).filter(Boolean);
  if (a.fams.size >= 4) a.target = [0, 0];
  else if (spots.length) { const x = spots.reduce((s, p) => s + p[0], 0) / spots.length, y = spots.reduce((s, p) => s + p[1], 0) / spots.length; a.target = [x, y]; }
  else a.target = posOf(a.vec);
});
const slots = pts.filter(p => p.ab);
const SIG = {}; D.classes.forEach(c => { const kit = c.Loadout.filter(id => D.abilities[id]); const sig = kit.find(id => D.abilities[id].Classes.length === 1) || c.Abilities.find(id => D.abilities[id] && D.abilities[id].Classes.length === 1); if (sig) SIG[sig] = c.Id; });
const rank = a => (SIG[a.id] ? 0 : a.Classes.length === 1 ? 1 : 2);
AB.sort((x, y) => rank(x) - rank(y) || Math.hypot(...y.target) - Math.hypot(...x.target) || x.id.localeCompare(y.id));
AB.forEach(a => { let best = null, bd = 1e9; slots.forEach(s => { if (s.a) return; const dd = Math.hypot(s.x - a.target[0], s.y - a.target[1]); if (dd < bd) { bd = dd; best = s; } }); best.a = a; a.node = best.i; });
const ABI = Object.fromEntries(AB.map(a => [a.id, a]));

// --- Heroes: signature = a move only that hero has (in its loadout if possible); starting path = tree through its loadout moves.
function bfs(from, to) {
  // Cheapest walk: passives cost 1, passing through another ability costs 4, so paths snake through passives.
  const n = pts.length, dist = new Float64Array(n).fill(Infinity), prev = new Int32Array(n).fill(-1), done = new Uint8Array(n);
  const src = new Set(from); from.forEach(f => dist[f] = 0);
  for (;;) {
    let u = -1, best = Infinity; for (let i = 0; i < n; i++) if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
    if (u < 0 || u === to) break; done[u] = 1;
    for (const v of pts[u].nb) { const w = v === to ? 1 : pts[v].a ? 4 : 1; if (dist[u] + w < dist[v]) { dist[v] = dist[u] + w; prev[v] = u; } }
  }
  const path = []; let u = to; while (u !== -1 && !src.has(u)) { path.push(u); u = prev[u]; } return path.reverse();
}
const HEROES = D.classes.map(c => {
  const kit = c.Loadout.filter(id => ABI[id]);
  const own = c.Abilities.filter(id => ABI[id] && ABI[id].Classes.length === 1);
  const sig = (kit.find(id => ABI[id].Classes.length === 1) || own[0] || kit.slice().sort((x, y) => ABI[x].Classes.length - ABI[y].Classes.length)[0]);
  return { id: c.Id, name: c.Name, fam: c.Family, kit: [...new Set([sig, ...kit])].filter(Boolean), sig };
}).filter(h => h.sig).sort((a, b) => a.name.localeCompare(b.name));
function startPath(h) {
  const nodes = h.kit.map(id => ABI[id].node);
  const owned = new Set([nodes[0]]), order = [nodes[0]];
  const rest = nodes.slice(1);
  while (rest.length) { // grow a tree: always attach the nearest remaining move
    let bi = 0, bp = null; rest.forEach((n, i) => { const p = bfs([...owned], n); if (!bp || p.length < bp.length) { bp = p; bi = i; } });
    bp.forEach(n => { owned.add(n); order.push(n); }); rest.splice(bi, 1);
  }
  return order;
}

// --- State
let hero = HEROES.find(h => h.id === 'warden') || HEROES[0];
let owned = [], history = [], showNames = true, preview = [];
function setHero(h) { hero = h; owned = startPath(h); history = []; preview = []; document.getElementById('hero').value = h.id; render(); }
function addPath(target) { if (owned.includes(target)) return; const p = bfs(owned, target); history.push(p.length); owned = owned.concat(p); preview = []; render(); }

// --- Render
const sigAt = {}; HEROES.forEach(h => { (sigAt[ABI[h.sig].node] = sigAt[ABI[h.sig].node] || []).push(h); });
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
      if (!a) { el('circle', { cx: p.x, cy: p.y, r, fill: 'none', stroke: css('--line'), 'stroke-width': 1 }, gN); return; }
      const wt = weights(p.x, p.y); const col = wt.o > 0.8 ? css('--fg') : sc(STATS[wt.a >= wt.b ? wt.k : (wt.k + 1) % 6]);
      const c = el('circle', { cx: p.x, cy: p.y, r: own ? 8 : r, fill: own ? col : css('--panel'), stroke: pv ? css('--fg') : col, 'stroke-width': own ? 2.5 : 2, class: 'node', 'data-tip': 'n:' + p.i }, gN);
      if (a.Kind === 'Spell') c.setAttribute('stroke-dasharray', '3 2');
      if (sigAt[p.i] && showNames) { const t = el('text', { x: p.x, y: p.y - 11, fill: sigAt[p.i].includes(hero) ? css('--hot') : css('--fg'), 'font-family': css('--body'), 'font-size': 9.5, 'font-weight': sigAt[p.i].includes(hero) ? 700 : 500, 'text-anchor': 'middle', 'pointer-events': 'none', opacity: 0.85 }, gT); t.textContent = sigAt[p.i].map(h => h.name).join(' / '); }
    } else {
      const s = Object.keys(p.pas)[0], col = sc(s);
      el('rect', { x: p.x - 2.6, y: p.y - 2.6, width: 5.2, height: 5.2, transform: `rotate(45 ${p.x} ${p.y})`, fill: own ? col : 'none', stroke: col, 'stroke-opacity': own ? 1 : 0.45, 'stroke-width': 1, class: 'node', 'data-tip': 'n:' + p.i }, gN);
    }
  });
  panel();
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
  const a = p.a, req = Object.entries(a.Requires || {}).map(([k, v]) => `${FROM[k] || k} ${v}`).join(', ');
  const heroes = a.Classes.map(c => CLS[c].Name), sig = sigAt[n] ? sigAt[n].map(h => h.name).join(', ') : '';
  const path = owned.includes(n) ? null : bfs(owned, n), gain = path ? path.filter(i => pts[i].a && i !== n).map(i => pts[i].a.Name) : [];
  const t = path ? totals(path) : null;
  return `<div class="k">${esc(FAMS[weights(p.x, p.y).k])} · ${esc(a.Kind || 'Move')}${a.Cost ? ' · ' + a.Cost + ' orb' + (a.Cost > 1 ? 's' : '') : ''}${req ? ' · needs ' + esc(req) : ''}</div><h3>${esc(a.Name)}</h3>${sig ? `<p style="color:var(--hot)">Signature of ${esc(sig)}</p>` : ''}${a.Desc ? `<p>${esc(a.Desc)}</p>` : ''}${path ? `<p><b>${path.length} SP</b> from your build${gain.length ? `, passing ${esc(gain.join(', '))}` : ''}; passives on the way ${esc(STATS.filter(s => t[s]).map(s => `+${t[s]} ${s}`).join(' ') || 'none')}.</p>` : '<p class="m">Owned</p>'}<p class="m">Starts in the kit of ${esc(heroes.slice(0, 6).join(', '))}${heroes.length > 6 ? ` and ${heroes.length - 6} more` : ''}.</p>`;
}

// --- Controls
const sel = document.getElementById('hero');
FAMS.forEach(() => {});
HEROES.forEach(h => { const o = document.createElement('option'); o.value = h.id; o.textContent = h.name; sel.appendChild(o); });
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
  tip.innerHTML = tipHtml(n); tip.hidden = false; place(e);
  if (n !== hoverN) { hoverN = n; preview = owned.includes(n) ? [] : bfs(owned, n); render(); tip.hidden = false; }
});
svg.addEventListener('pointerleave', () => { tip.hidden = true; if (preview.length) { preview = []; hoverN = -1; render(); } });
svg.addEventListener('click', e => { const t = e.target.closest('[data-tip]'); if (!t) return; const n = +t.getAttribute('data-tip').slice(2); document.getElementById('detail').innerHTML = tipHtml(n); addPath(n); tip.hidden = true; });


// Export the board in the skill-hex schema (docs/design/skill-hexagon/skillhex.schema.json).
window.SkillHexExport = () => ({
  "$schema": "./skillhex.schema.json",
  Version: 1,
  Radius: N,
  Stats: STATS.map(s => ({ Id: s, Color: sc(s) })),
  Families: FAMS.map((f, k) => ({ Id: f.toLowerCase(), Name: f, From: STATS[k], To: STATS[(k + 1) % 6] })),
  Costs: { Passive: 1, Ability: 1, PlannerPassThroughAbility: 4 },
  Nodes: pts.map(p => p.ab
    ? (p.a ? { Q: p.q, R: p.r, Kind: 'Ability', Ability: p.a.id } : { Q: p.q, R: p.r, Kind: 'Open' })
    : { Q: p.q, R: p.r, Kind: 'Passive', Stats: p.pas }),
  Heroes: D.classes.map(c => {
    const h = HEROES.find(x => x.id === c.Id), recipe = Object.entries(HERO_RECIPE).find(([, n]) => n === c.Name);
    const rec = recipe ? Object.fromEntries(recipe[0].split(' ').map(t => [t.slice(0, 3), +t.slice(3)])) : null;
    return { Class: c.Id, Name: c.Name, Recipe: rec, Status: rec ? 'Placed' : 'Cut', Signature: h ? h.sig : null,
      Start: h ? startPath(h).map(i => [pts[i].q, pts[i].r]) : [] };
  })
});

// Example on load: a Warden walking to White Ninja's signature.
setHero(hero);
const wn = HEROES.find(h => h.id === 'whiteninja'); if (wn && hero.id === 'warden') addPath(ABI[wn.sig].node);
})();
