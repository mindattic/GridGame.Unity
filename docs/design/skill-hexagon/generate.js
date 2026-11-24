// Layout generator for the skill hexagon: builds the whole board (lattice, passives, ability placement from the
// hero spots, signatures and hero start paths) from content in data.js. The page never runs this; it draws
// skillhex.json. Only tools/skillhex/regenerate.py loads it, to rebuild skillhex.json from content.
// Exposes window.SkillHexExport(): the board in the skill-hex schema (skillhex.schema.json).
(() => {
const D = window.WHEEL_DATA;
// Corners clockwise from the top, as on the stat hexagon. Families are the wedges between them.
const STATS = ['VIT', 'STR', 'AGI', 'SPD', 'WIS', 'INT'];
const FAMS = ['Martial', 'Brawn', 'Shadow', 'Wild', 'Brains', 'Grace'];
const COLORS = { VIT: '#5b8def', STR: '#e0784a', AGI: '#a07cf0', SPD: '#6fd6c4', WIS: '#f1e3a6', INT: '#d35fb0' };
const FROM = { Vitality: 'VIT', Strength: 'STR', Agility: 'AGI', Speed: 'SPD', Wisdom: 'WIS', Intelligence: 'INT', Stamina: 'VIT', Luck: 'AGI' };
const N = 16;                    // lattice radius: 817 points, a third of them ability slots
const R = 520, d = R / N;
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

// Export the board in the skill-hex schema (docs/design/skill-hexagon/skillhex.schema.json).
window.SkillHexExport = () => ({
  "$schema": "./skillhex.schema.json",
  Version: 1,
  Radius: N,
  Stats: STATS.map(s => ({ Id: s, Color: COLORS[s] })),
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
})();
