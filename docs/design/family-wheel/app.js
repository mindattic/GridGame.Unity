(() => {
const D = window.WHEEL_DATA;
const FAM = Object.fromEntries(D.families.map(f => [f.Id, f]));
const STAT = { Strength:'STR', Vitality:'VIT', Agility:'AGI', Speed:'SPD', Intelligence:'INT', Wisdom:'WIS', Stamina:'STA', Luck:'LCK' };

const fam = id => ({ id, name: FAM[id].Name, from: [id], theme: FAM[id].Theme, stats: [FAM[id].Primary, FAM[id].Secondary] });
const LAYOUTS = {
  8: [fam('vanguard'), fam('blade'), fam('brawn'), fam('shadow'), fam('wild'), fam('machina'),
      { id:'mystic', name:'Mystic', from:['arcane','occult'], stats:['Intelligence','Speed / Vitality'],
        theme:'Arcane and Occult as one wedge: raw spells and tempo weaving on the Machina side, blood, curses and night knights on the Grace side.' },
      fam('grace')],
  6: [fam('vanguard'),
      { id:'martial', name:'Martial', from:['blade','brawn'], stats:['Strength','Stamina / Vitality'],
        theme:'Blade and Brawn as one wedge: sword and spear discipline on the Vanguard side, fists, clubs and rage on the Shadow side.' },
      fam('shadow'),
      { id:'field', name:'Field', from:['wild','machina'], stats:['Strength / Intelligence','Wisdom'],
        theme:'Wild and Machina as one wedge: beasts, snares and hunters on the Shadow side, mines, turrets and drones on the Mystic side. Both control the ground.' },
      { id:'mystic', name:'Mystic', from:['arcane','occult'], stats:['Intelligence','Speed / Vitality'],
        theme:'Arcane and Occult as one wedge: raw spells and tempo weaving next to Field, blood, curses and night knights next to Grace.' },
      fam('grace')]
};

const R = 390, MIN_S = 0.40, MAX_S = 0.90, HUB = 0.17;
const svg = document.getElementById('wheel');
const tip = document.getElementById('tip');
const NS = 'http://www.w3.org/2000/svg';
const el = (tag, attrs, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[c]));
const css = v => getComputedStyle(document.documentElement).getPropertyValue(v).trim();

let N = 8, M = null, spec = null;

function build(n) {
  const groups = LAYOUTS[n];
  const gIndex = {}; groups.forEach((g, i) => g.from.forEach(f => gIndex[f] = i));
  const angle = i => -Math.PI / 2 + i * 2 * Math.PI / n;
  const half = Math.PI / n;
  const vtx = (i, side) => { const a = angle(i) + side * half; return [Math.cos(a) * R, Math.sin(a) * R]; };
  const toXY = (g, t, s) => { const a = vtx(g, -1), b = vtx(g, 1), u = (t + 1) / 2; return [s * (a[0] + (b[0] - a[0]) * u), s * (a[1] + (b[1] - a[1]) * u)]; };
  const toTS = (g, x, y) => { const a = vtx(g, -1), b = vtx(g, 1), dx = b[0] - a[0], dy = b[1] - a[1]; const det = a[0] * dy - a[1] * dx; const s = (x * dy - y * dx) / det; const st = (a[0] * y - a[1] * x) / det; return [2 * (st / s) - 1, s]; };
  const ringDist = (a, b) => { const d = Math.abs(a - b) % n; return Math.min(d, n - d); };

  const clsById = {};
  D.classes.forEach(c => clsById[c.Id] = c);
  const abil = {};
  for (const [id, a] of Object.entries(D.abilities)) {
    const cnt = {};
    a.Classes.forEach(c => { const g = gIndex[clsById[c].Family]; cnt[g] = (cnt[g] || 0) + 1; });
    const gs = Object.keys(cnt).map(Number);
    let home = gs[0]; gs.forEach(g => { if (cnt[g] > cnt[home]) home = g; });
    abil[id] = { id, ...a, cnt, gs, home, hub: gs.length >= 3 };
  }
  const homeOf = id => abil[id].home;
  const sub = c => { const g = groups[gIndex[c.Family]]; return g.from.length === 1 ? 0 : (g.from.indexOf(c.Family) === 0 ? -0.55 : 0.55); };

  const classes = D.classes.map(c => {
    const g = gIndex[c.Family];
    let lean = 0, k = 0;
    c.Abilities.forEach(id => { const h = homeOf(id); if (h === g || abil[id].hub) return; let off = (h - g + n) % n; if (off > n / 2) off -= n; lean += Math.sign(off) / Math.abs(off); k++; });
    const score = sub(c) * 2 + (k ? lean / k : 0) + (c.Abilities.filter(id => homeOf(id) !== g).length * 0) ;
    return { ...c, g, score };
  });
  groups.forEach((gr, gi) => {
    const list = classes.filter(c => c.g === gi).sort((a, b) => a.score - b.score || a.Name.localeCompare(b.Name));
    list.forEach((c, i) => { c.t = list.length === 1 ? 0 : -0.86 + 1.72 * i / (list.length - 1); });
    gr.size = list.length;
  });
  const cMap = Object.fromEntries(classes.map(c => [c.Id, c]));

  const nodes = Object.values(abil).map(a => {
    const nd = { a, g: a.home };
    if (a.hub) {
      let sx = 0, sy = 0; a.gs.forEach(g => { sx += Math.cos(angle(g)) * a.cnt[g]; sy += Math.sin(angle(g)) * a.cnt[g]; });
      const ang = Math.atan2(sy, sx) || 0, rr = R * HUB * (0.35 + 0.6 * ((a.id.length * 37) % 10) / 10);
      nd.x = Math.cos(ang) * rr; nd.y = Math.sin(ang) * rr; nd.g = -1;
      return nd;
    }
    const owners = a.Classes.map(id => cMap[id]).filter(c => c.g === a.home);
    let t = owners.reduce((s, c) => s + c.t, 0) / owners.length;
    if (a.gs.length === 2) { const other = a.gs.find(g => g !== a.home); const off = ((other - a.home + n) % n); if (off === 1) t = 0.9; else if (off === n - 1) t = -0.9; }
    const size = groups[a.home].size;
    const share = Math.min(1, (owners.length - 1) / Math.max(2, size * 0.6));
    const s = MAX_S - (MAX_S - MIN_S) * share;
    [nd.x, nd.y] = toXY(a.home, t, s);
    return nd;
  });

  // Relax overlaps inside each wedge (and the hub).
  for (let it = 0; it < 90; it++) {
    for (let i = 0; i < nodes.length; i++) for (let j = i + 1; j < nodes.length; j++) {
      const p = nodes[i], q = nodes[j]; if (p.g !== q.g) continue;
      let dx = q.x - p.x, dy = q.y - p.y, d = Math.hypot(dx, dy);
      const min = p.g === -1 ? 17 : 19;
      if (d < min) { if (d < 0.01) { dx = Math.cos(i + j); dy = Math.sin(i * j + 1); d = 1; } const push = (min - d) / 2; p.x -= dx / d * push; p.y -= dy / d * push; q.x += dx / d * push; q.y += dy / d * push; }
    }
    nodes.forEach(p => {
      if (p.g === -1) { const r = Math.hypot(p.x, p.y), lim = R * HUB * 1.05; if (r > lim) { p.x *= lim / r; p.y *= lim / r; } return; }
      let [t, s] = toTS(p.g, p.x, p.y);
      t = Math.max(-0.93, Math.min(0.93, t)); s = Math.max(MIN_S - 0.05, Math.min(MAX_S + 0.04, s));
      [p.x, p.y] = toXY(p.g, t, s); p.t = t;
    });
  }
  nodes.forEach(p => { if (p.g !== -1) p.t = toTS(p.g, p.x, p.y)[0]; });
  return { n, groups, angle, vtx, toXY, ringDist, classes, cMap, nodes, abil };
}

function tierFor(c, nd) {
  if (c.Abilities.includes(nd.a.id)) return 0;
  if (nd.g === -1) return 1;
  const xc = c.g + c.t / 2, x = nd.g + nd.t / 2;
  let d = Math.abs(x - xc) % M.n; d = Math.min(d, M.n - d);
  return Math.max(1, Math.round(d + 0.25));
}
const tierColor = t => css('--t' + Math.min(5, t));
const tierLabel = t => t === 0 ? 'Kit' : t === 1 ? 'Near' : 'Tier ' + t;
const tierCost = t => t === 0 ? 'kit price' : t === 1 ? 'base SP' : `base SP +${t - 1}, stat needs +${t - 1}`;

function familyColor(g) { return css('--f-' + M.groups[g].id); }

function render() {
  svg.textContent = '';
  const { n, groups, vtx, toXY } = M;
  const defs = el('defs', {}, svg);
  const glow = el('filter', { id: 'glow', x: '-50%', y: '-50%', width: '200%', height: '200%' }, defs);
  el('feGaussianBlur', { stdDeviation: '3', result: 'b' }, glow);
  const mg = el('feMerge', {}, glow); el('feMergeNode', { in: 'b' }, mg); el('feMergeNode', { in: 'SourceGraphic' }, mg);

  const gW = el('g', {}, svg), gE = el('g', {}, svg), gN = el('g', {}, svg), gC = el('g', {}, svg), gL = el('g', {}, svg);
  groups.forEach((gr, i) => {
    const a = vtx(i, -1), b = vtx(i, 1), col = familyColor(i);
    let tierShade = null;
    if (spec) { const d = M.ringDist(i, spec.g); tierShade = d; }
    const w = el('path', { d: `M0 0 L${a[0]} ${a[1]} L${b[0]} ${b[1]} Z`, fill: col, 'fill-opacity': spec ? (0.20 - Math.min(0.16, M.ringDist(i, spec.g) * 0.045)) : 0.11, stroke: col, 'stroke-opacity': .55, 'stroke-width': 1.2, class: 'wedge', 'data-tip': 'w:' + i }, gW);
    // sub-family divider for merged wedges
    if (gr.from.length === 2) { const m = toXY(i, 0, 1); el('line', { x1: m[0] * 0.3, y1: m[1] * 0.3, x2: m[0], y2: m[1], stroke: col, 'stroke-opacity': .35, 'stroke-dasharray': '3 5' }, gW); }
    const lp = toXY(i, 0, 0.29), ang = M.angle(i) * 180 / Math.PI + 90;
    const flip = Math.cos(M.angle(i)) < -0.01 || (Math.abs(Math.cos(M.angle(i))) < 0.01 && Math.sin(M.angle(i)) > 0);
    const t = el('text', { x: lp[0], y: lp[1], fill: col, 'font-family': css('--display'), 'font-weight': 700, 'font-size': 17, 'letter-spacing': 3, 'text-anchor': 'middle', 'dominant-baseline': 'middle', 'pointer-events': 'none', opacity: .9 }, gL);
    t.textContent = gr.name.toUpperCase();
    if (gr.from.length === 2) gr.from.forEach((f, k) => { const p = toXY(i, k ? 0.5 : -0.5, 0.36); const st = el('text', { x: p[0], y: p[1], fill: col, 'font-family': css('--mono'), 'font-size': 10, 'text-anchor': 'middle', 'dominant-baseline': 'middle', opacity: .7, 'pointer-events': 'none' }, gL); st.textContent = FAM[f].Name.toLowerCase(); });
  });
  el('circle', { cx: 0, cy: 0, r: R * HUB * 1.25, fill: css('--bg'), stroke: css('--line'), 'stroke-width': 1.2, 'data-tip': 'hub' }, gW);
  const hubT = el('text', { x: 0, y: R * HUB * 1.25 + 14, fill: css('--muted'), 'font-family': css('--mono'), 'font-size': 10, 'text-anchor': 'middle', 'pointer-events': 'none' }, gL); hubT.textContent = 'COMMON CORE';

  // Lattice: each node to its two nearest in the same wedge, plus classes to their two nearest native nodes.
  const nodes = M.nodes;
  const seen = new Set();
  const link = (p, q, op) => { const k = p.a.id < q.a.id ? p.a.id + q.a.id : q.a.id + p.a.id; if (seen.has(k)) return; seen.add(k); el('line', { x1: p.x, y1: p.y, x2: q.x, y2: q.y, stroke: css('--muted'), 'stroke-opacity': op, 'stroke-width': 1 }, gE); };
  nodes.forEach(p => { nodes.filter(q => q !== p && q.g === p.g).map(q => [q, Math.hypot(q.x - p.x, q.y - p.y)]).sort((a, b) => a[1] - b[1]).slice(0, 2).forEach(([q]) => link(p, q, 0.22)); });
  for (let i = 0; i < n; i++) { const j = (i + 1) % n; const A = nodes.filter(p => p.g === i), B = nodes.filter(p => p.g === j); const pairs = []; A.forEach(p => B.forEach(q => pairs.push([p, q, Math.hypot(p.x - q.x, p.y - q.y)]))); pairs.sort((a, b) => a[2] - b[2]).slice(0, 3).forEach(([p, q]) => link(p, q, 0.3)); }
  nodes.filter(p => p.g === -1).forEach(p => p.a.gs.forEach(g => { const inner = nodes.filter(q => q.g === g).sort((a, b) => Math.hypot(a.x, a.y) - Math.hypot(b.x, b.y))[0]; if (inner) link(p, inner, 0.14); }));

  nodes.forEach(p => {
    const shared = p.a.Classes.length;
    const r = p.g === -1 ? 5.5 : shared > 1 ? 6.5 : 5;
    let fill = p.g === -1 ? css('--fg') : familyColor(p.g), op = 0.95, stroke = css('--bg');
    if (spec) { const t = tierFor(spec, p); fill = tierColor(t); op = t === 0 ? 1 : 0.95; if (t === 0) stroke = css('--hot'); }
    const c = el('circle', { cx: p.x, cy: p.y, r, fill, 'fill-opacity': op, stroke, 'stroke-width': spec && tierFor(spec, p) === 0 ? 2 : 1.2, class: 'node', 'data-tip': 'a:' + p.a.id }, gN);
    if (p.a.Kind === 'Spell') c.setAttribute('stroke-dasharray', '2 1.5');
  });

  M.classes.forEach(c => {
    const [x, y] = toXY(c.g, c.t, 1.0), col = familyColor(c.g), sel = spec && spec.Id === c.Id;
    const s = sel ? 10 : 7;
    const d = el('rect', { x: x - s, y: y - s, width: s * 2, height: s * 2, transform: `rotate(45 ${x} ${y})`, fill: sel ? css('--hot') : col, stroke: css('--bg'), 'stroke-width': 2, class: 'cls', 'data-tip': 'c:' + c.Id, tabindex: 0, role: 'button', 'aria-label': `Spec from ${c.Name}` }, gC);
    if (sel) d.setAttribute('filter', 'url(#glow)');
    const ang = Math.atan2(y, x), deg = ang * 180 / Math.PI, flip = Math.cos(ang) < 0;
    const lx = x + Math.cos(ang) * 16, ly = y + Math.sin(ang) * 16;
    const t = el('text', { x: lx, y: ly, fill: sel ? css('--hot') : css('--fg'), 'font-family': css('--body'), 'font-size': 11.5, 'font-weight': sel ? 600 : 400, 'text-anchor': flip ? 'end' : 'start', 'dominant-baseline': 'middle', transform: `rotate(${flip ? deg + 180 : deg} ${lx} ${ly})`, opacity: spec && !sel ? 0.75 : 0.95, 'pointer-events': 'none' }, gL);
    t.textContent = c.Name;
  });
  panel();
}

function tipHtml(key) {
  const [kind, id] = key.includes(':') ? [key.slice(0, key.indexOf(':')), key.slice(key.indexOf(':') + 1)] : [key, ''];
  if (kind === 'hub') {
    const k = M.nodes.filter(p => p.g === -1).length;
    return `<div class="k">Common core</div><h3>${k} shared moves</h3><p>Moves that three or more wedges already use. Every class reaches them at base price.</p>`;
  }
  if (kind === 'w') {
    const i = +id, gr = M.groups[i], opp = M.groups[(i + M.n / 2) % M.n];
    const members = M.classes.filter(c => c.g === i).sort((a, b) => a.t - b.t).map(c => c.Name).join(', ');
    const moves = M.nodes.filter(p => p.g === i).length;
    let extra = '';
    if (spec) { const d = M.ringDist(i, spec.g); extra = `<p class="m">From ${esc(spec.Name)}: ${d === 0 ? 'home wedge' : d + (d === 1 ? ' wedge' : ' wedges') + ' round'}.</p>`; }
    return `<div class="k">Wedge · ${esc(gr.stats.map(s => s.split(' / ').map(x => STAT[x] || x).join('/')).join(' · '))}</div><h3>${esc(gr.name)}</h3><p>${esc(gr.theme)}</p><p class="m">${gr.size} classes · ${moves} moves · opposite ${esc(opp.name)}</p><p class="m">Rim order: ${esc(members)}</p>${extra}`;
  }
  if (kind === 'c') {
    const c = M.cMap[id]; const gr = M.groups[c.g];
    const left = M.groups[(c.g - 1 + M.n) % M.n].name, right = M.groups[(c.g + 1) % M.n].name;
    const lean = c.t < -0.3 ? `leans toward ${left}` : c.t > 0.3 ? `leans toward ${right}` : 'sits mid-wedge';
    return `<div class="k">${esc(gr.name)} · ${esc(c.Role || 'Hero')}</div><h3>${esc(c.Name)}</h3><p>Starts on the rim and ${lean}. ${c.Abilities.length} moves in its own kit.</p><p class="m">Click to price the whole wheel from here.</p>`;
  }
  if (kind === 'a') {
    const p = M.nodes.find(q => q.a.id === id), a = p.a;
    const where = p.g === -1 ? 'Common core' : M.groups[p.g].name;
    const req = Object.entries(a.Requires || {}).map(([k, v]) => `${STAT[k] || k} ${v}`).join(', ');
    const owners = a.Classes.map(x => M.cMap[x].Name);
    let price = '';
    if (spec) { const t = tierFor(spec, p); price = `<p><b style="color:${tierColor(t)}">${tierLabel(t)}</b> for ${esc(spec.Name)}: ${tierCost(t)}.</p>`; }
    return `<div class="k">${esc(where)} · ${esc(a.Kind || 'Move')}${a.Cost ? ' · ' + a.Cost + ' orb' + (a.Cost > 1 ? 's' : '') : ''}${req ? ' · needs ' + esc(req) : ''}</div><h3>${esc(a.Name)}</h3>${a.Desc ? `<p>${esc(a.Desc)}</p>` : ''}${price}<p class="m">Native to ${owners.length > 6 ? owners.slice(0, 6).map(esc).join(', ') + ` and ${owners.length - 6} more` : owners.map(esc).join(', ')}.</p>`;
  }
  return '';
}

function panel() {
  const legend = document.getElementById('legend'), bars = document.getElementById('bars');
  const title = document.getElementById('ptitle'), body = document.getElementById('pbody');
  legend.textContent = ''; bars.textContent = '';
  const maxT = Math.floor(M.n / 2) + 1;
  if (!spec) {
    title.textContent = 'How the wheel prices a move';
    body.textContent = 'Pick a class (or click its diamond on the rim) to shade every ability by what it costs that class. Hover any wedge, diamond or node for details. Dashed rings are spells.';
    M.groups.forEach((g, i) => { const d = document.createElement('div'); d.innerHTML = `<span class="sw" style="background:${familyColor(i)}"></span>${esc(g.name)}<span style="color:var(--muted)"> · ${g.size} classes</span>`; legend.appendChild(d); });
  } else {
    title.textContent = 'Spec from ' + spec.Name;
    body.textContent = `Price of every move for a ${spec.Name}, measured round the rim from where it starts. The ${M.n}-sided wheel tops out at tier ${maxT}.`;
    const counts = {}; M.nodes.forEach(p => { const t = tierFor(spec, p); counts[t] = (counts[t] || 0) + 1; });
    const top = Math.max(...Object.values(counts));
    for (let t = 0; t <= maxT; t++) {
      const d = document.createElement('div'); d.innerHTML = `<span class="sw" style="background:${tierColor(t)}"></span><b>${tierLabel(t)}</b><span style="color:var(--muted)"> · ${tierCost(t)}</span>`; legend.appendChild(d);
      const b = document.createElement('div'); b.className = 'bar'; const k = counts[t] || 0;
      b.innerHTML = `<span>${tierLabel(t)}</span><i style="width:${(k / top) * 100}%;background:${tierColor(t)}"></i><span style="text-align:right">${k}</span>`; bars.appendChild(b);
    }
  }
  const tb = document.getElementById('tbl'); tb.textContent = '';
  M.groups.forEach((g, i) => { const tr = document.createElement('tr'); const opp = M.groups[(i + M.n / 2) % M.n]; tr.innerHTML = `<td><span class="dot" style="background:${familyColor(i)}"></span>${esc(g.name)}</td><td>${esc(opp.name)}</td><td class="n">${g.size}</td><td class="n">${M.nodes.filter(p => p.g === i).length}</td>`; tb.appendChild(tr); });
  const hubRow = document.createElement('tr'); hubRow.innerHTML = `<td><span class="dot" style="background:var(--fg)"></span>Common core</td><td>—</td><td class="n">—</td><td class="n">${M.nodes.filter(p => p.g === -1).length}</td>`; tb.appendChild(hubRow);
}

function setShape(n) {
  N = n; M = build(n);
  document.getElementById('b8').setAttribute('aria-pressed', n === 8);
  document.getElementById('b6').setAttribute('aria-pressed', n === 6);
  if (spec) spec = M.cMap[spec.Id];
  const sel = document.getElementById('cls'); const keep = sel.value; sel.length = 1;
  M.groups.forEach((g, i) => { const og = document.createElement('optgroup'); og.label = g.name; M.classes.filter(c => c.g === i).sort((a, b) => a.Name.localeCompare(b.Name)).forEach(c => { const o = document.createElement('option'); o.value = c.Id; o.textContent = c.Name; og.appendChild(o); }); sel.appendChild(og); });
  sel.value = keep;
  render();
  try { localStorage.setItem('gg-wheel', JSON.stringify({ n, c: spec ? spec.Id : '' })); } catch (e) {}
}
function setSpec(id) {
  spec = id ? M.cMap[id] : null; document.getElementById('cls').value = id || '';
  document.getElementById('detail').textContent = '';
  render();
  try { localStorage.setItem('gg-wheel', JSON.stringify({ n: N, c: id || '' })); } catch (e) {}
}

document.getElementById('b8').onclick = () => setShape(8);
document.getElementById('b6').onclick = () => setShape(6);
document.getElementById('cls').onchange = e => setSpec(e.target.value);
document.getElementById('clear').onclick = () => setSpec('');

const place = (e) => { const pad = 14, w = tip.offsetWidth, h = tip.offsetHeight; let x = e.clientX + pad, y = e.clientY + pad; if (x + w > innerWidth - 8) x = e.clientX - w - pad; if (y + h > innerHeight - 8) y = e.clientY - h - pad; tip.style.left = Math.max(8, x) + 'px'; tip.style.top = Math.max(8, y) + 'px'; };
svg.addEventListener('pointermove', e => {
  if (e.pointerType === 'touch') return;
  const t = e.target.closest('[data-tip]'); if (!t) { tip.hidden = true; return; }
  tip.innerHTML = tipHtml(t.getAttribute('data-tip')); tip.hidden = false; place(e);
});
svg.addEventListener('pointerleave', () => tip.hidden = true);
svg.addEventListener('click', e => {
  const t = e.target.closest('[data-tip]'); if (!t) return;
  const key = t.getAttribute('data-tip');
  if (key.startsWith('c:')) { setSpec(key.slice(2)); return; }
  const det = document.getElementById('detail'); det.innerHTML = tipHtml(key);
  if (e.pointerType === 'touch' || matchMedia('(max-width: 900px)').matches) det.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
});
svg.addEventListener('keydown', e => { if ((e.key === 'Enter' || e.key === ' ') && e.target.classList.contains('cls')) { e.preventDefault(); setSpec(e.target.getAttribute('data-tip').slice(2)); } });

let saved = { n: 8, c: '' };
try { saved = JSON.parse(localStorage.getItem('gg-wheel')) || saved; } catch (e) {}
setShape(saved.n === 6 ? 6 : 8);
if (saved.c && M.cMap[saved.c]) setSpec(saved.c);
})();
