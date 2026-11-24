(() => {
const D = window.WHEEL_DATA, WM = window.WheelModel;
const FAM = Object.fromEntries(D.families.map(f => [f.Id, f]));
const CLS = Object.fromEntries(D.classes.map(c => [c.Id, c]));
const STAT = { Strength:'STR', Vitality:'VIT', Agility:'AGI', Speed:'SPD', Intelligence:'INT', Wisdom:'WIS', Stamina:'STA', Luck:'LCK' };
const SHAPE = { 5:'Pentagon', 6:'Hexagon', 7:'Heptagon', 8:'Octagon' };
const NS = 'http://www.w3.org/2000/svg';
const svg = document.getElementById('wheel'), tip = document.getElementById('tip');
const el = (tag, attrs, parent) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; };
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[c]));
const css = v => getComputedStyle(document.documentElement).getPropertyValue(v).trim();
const famColor = f => f ? css('--f-' + f) : css('--muted');
const name = id => id.startsWith('new:') ? 'Open slot' : CLS[id].Name;

const R = 470, ROW = 0.056, CORE = 0.25, PASSIVE_ROWS = [0, 3, 6, 9];
const SOL = {};
[5, 6, 7, 8].forEach(N => ['add', 'trim'].forEach(p => { SOL[N + p] = WM.solve(N, p, D); }));

let N = 8, PLAN = 'add', spec = null, showPas = true, M = null;

function arcName(a) {
  const total = a.ranked.reduce((s, r) => s + r[1], 0);
  const [f1, f2] = a.ranked;
  if (f2 && f2[1] / total >= 0.3) return FAM[f1[0]].Name + ' · ' + FAM[f2[0]].Name;
  return FAM[f1[0]].Name;
}

function build() {
  const s = SOL[N + PLAN], ring = s.ring, S = ring.length, m = s.m;
  const vtx = k => { const a = -Math.PI / 2 + 2 * Math.PI * k / N; return [Math.cos(a), Math.sin(a)]; };
  const at = (x, scale) => { const f = ((x / S) % 1 + 1) % 1 * N, k = Math.floor(f), u = f - k; const A = vtx(k), B = vtx(k + 1); return [(A[0] + (B[0] - A[0]) * u) * scale * R, (A[1] + (B[1] - A[1]) * u) * scale * R]; };
  const scaleOf = r => Math.max(CORE + 0.04, 1 - r * ROW);
  const slotOf = Object.fromEntries(ring.map((id, i) => [id, i]));
  const arcOfX = x => Math.floor((((x % S) + S) % S) / m) % N;
  const arcs = s.arcs.map(a => ({ ...a, name: arcName(a), fam: a.top }));
  const famAtSlot = i => { const id = ring[i]; return id.startsWith('new:') ? arcs[arcOfX(i)].fam : CLS[id].Family; };

  // Abilities: column = circular mean of their heroes' slots; single-hero moves stack in that hero's column.
  const cells = new Set(), nodes = [];
  const take = (col, row) => cells.add(col + ':' + row);
  const free = (col, row) => !cells.has(col + ':' + row);
  const abil = Object.entries(D.abilities).map(([id, a]) => ({ id, ...a, owners: a.Classes.filter(c => c in slotOf) }));
  abil.sort((a, b) => a.owners.length - b.owners.length || (a.Cost || 0) - (b.Cost || 0) || a.id.localeCompare(b.id));
  const orphans = [];
  abil.forEach(a => {
    if (!a.owners.length) { orphans.push(a); return; }
    let sx = 0, sy = 0; a.owners.forEach(o => { const t = 2 * Math.PI * slotOf[o] / S; sx += Math.cos(t); sy += Math.sin(t); });
    const spread = 1 - Math.hypot(sx, sy) / a.owners.length;
    const fams = new Set(a.owners.map(o => CLS[o].Family));
    if (fams.size >= 3 || spread > 0.25) { nodes.push({ kind: 'core', a }); return; }
    let col = Math.round(((Math.atan2(sy, sx) / (2 * Math.PI)) * S + S) % S) % S;
    let row = 1 + Math.min(3, a.owners.length - 1);
    let tries = 0;
    while (!free(col, row) && tries < 60) { row++; if (row > 12) { row = 1 + Math.min(3, a.owners.length - 1); col = (col + (tries % 2 ? tries : -tries) + S) % S; } tries++; }
    take(col, row);
    nodes.push({ kind: 'move', a, x: col, row });
  });
  const core = nodes.filter(n => n.kind === 'core');
  core.forEach((n, i) => { const t = -Math.PI / 2 + 2 * Math.PI * i / core.length, rr = (i % 2 ? 0.55 : 0.82) * CORE * R; n.px = Math.cos(t) * rr; n.py = Math.sin(t) * rr; });
  nodes.filter(n => n.kind === 'move').forEach(n => { [n.px, n.py] = at(n.x, scaleOf(n.row)); });

  // Stat passives on half-slots: the rim blends the two heroes either side; inner rings follow the family.
  const pas = [];
  for (let i = 0; i < S; i++) PASSIVE_ROWS.forEach((row, k) => {
    const x = i + 0.5, a = arcs[arcOfX(x)], F = FAM[a.fam];
    let label;
    if (row === 0) { const s1 = STAT[FAM[famAtSlot(i)].Primary], s2 = STAT[FAM[famAtSlot((i + 1) % S)].Primary]; label = s1 === s2 ? `+2 ${s1}` : `+1 ${s1} +1 ${s2}`; }
    else label = k === 1 ? `+2 ${STAT[F.Primary]}` : k === 2 ? `+2 ${STAT[F.Secondary]}` : `+3 ${STAT[F.Primary]}`;
    const [px, py] = at(x, scaleOf(row));
    pas.push({ kind: 'pas', x, row, label, fam: a.fam, px, py });
  });

  const heroes = ring.map((id, i) => { const [px, py] = at(i, 1); return { id, i, px, py, border: i % m === 0, fam: famAtSlot(i) }; });
  return { s, ring, S, m, arcs, at, scaleOf, vtx, nodes, pas, heroes, slotOf, orphans, arcOfX };
}

// Borders crossed going the short way round from x0 to x1 (slot units). A hero on a border sits in both families.
function crossings(x0, x1) {
  const S = M.S, m = M.m; let d = ((x1 - x0) % S + S) % S; let dir = 1; if (d > S / 2) { d = S - d; dir = -1; }
  let c = 0; for (let b = 0; b < M.s.N; b++) { const bx = b * m; let o = ((bx - x0) * dir % S + S) % S; if (o > 1e-9 && o < d - 1e-9) c++; }
  return c;
}
const tierCol = t => css('--t' + Math.min(5, t));
function priceOf(node) {
  if (!spec) return null;
  const x0 = M.slotOf[spec];
  if (node.kind === 'core') return { t: 1, txt: 'Common core: base price for everyone' };
  if (node.kind === 'move' && node.a.owners.includes(spec)) return { t: 0, native: true, txt: 'In the kit already' };
  const c = crossings(x0, node.x);
  return { t: c + 1, c, txt: c === 0 ? 'Own family: base price' : `Crosses ${c} border${c > 1 ? 's' : ''}: base +${c} SP, stat needs +${c}` };
}

function render() {
  M = build();
  svg.textContent = '';
  const { s, S, m, arcs, at, vtx } = M;
  const gA = el('g', {}, svg), gL = el('g', {}, svg), gP = el('g', {}, svg), gN = el('g', {}, svg), gH = el('g', {}, svg), gT = el('g', {}, svg);
  // Family wedges
  arcs.forEach((a, j) => {
    const A = vtx(j), B = vtx(j + 1), col = famColor(a.fam);
    el('path', { d: `M0 0 L${A[0] * R} ${A[1] * R} L${B[0] * R} ${B[1] * R} Z`, fill: col, 'fill-opacity': 0.09, stroke: 'none', 'data-tip': 'arc:' + j }, gA);
    el('line', { x1: 0, y1: 0, x2: A[0] * R, y2: A[1] * R, stroke: css('--fg'), 'stroke-opacity': 0.45, 'stroke-width': 1.4 }, gL);
    const mid = at((j + 0.5) * m, 1.13), t = el('text', { x: mid[0], y: mid[1], fill: col, 'font-family': css('--display'), 'font-weight': 700, 'font-size': 15, 'letter-spacing': 2, 'text-anchor': 'middle', 'dominant-baseline': 'middle', 'pointer-events': 'none' }, gT);
    t.textContent = a.name.toUpperCase();
  });
  // Rings and hero spokes
  PASSIVE_ROWS.forEach(r => { const sc = M.scaleOf(r); const pts = []; for (let k = 0; k <= N; k++) { const v = vtx(k); pts.push(v[0] * R * sc + ',' + v[1] * R * sc); } el('polyline', { points: pts.join(' '), fill: 'none', stroke: css('--muted'), 'stroke-opacity': r === 0 ? 0.5 : 0.22, 'stroke-width': r === 0 ? 1.4 : 1 }, gL); });
  M.heroes.forEach(h => { const deep = Math.max(9, ...M.nodes.filter(n => n.kind === 'move' && n.x === h.i).map(n => n.row)); const [x2, y2] = at(h.i, M.scaleOf(deep)); el('line', { x1: h.px, y1: h.py, x2, y2, stroke: famColor(h.fam), 'stroke-opacity': 0.25, 'stroke-width': 1 }, gL); });
  el('circle', { cx: 0, cy: 0, r: CORE * R, fill: css('--bg'), stroke: css('--line'), 'stroke-width': 1.2, 'data-tip': 'core' }, gL);
  // Passives
  if (showPas) M.pas.forEach((p, i) => {
    let fill = css('--panel'), stroke = famColor(p.fam), op = 0.9;
    if (spec) { const pr = priceOf(p); fill = tierCol(pr.t); stroke = css('--bg'); }
    el('rect', { x: p.px - 3.2, y: p.py - 3.2, width: 6.4, height: 6.4, fill, stroke, 'stroke-width': 1, opacity: op, 'data-tip': 'pas:' + i }, gP);
  });
  // Moves
  M.nodes.forEach((n, i) => {
    const big = n.a.owners.length > 1; let fill = n.kind === 'core' ? css('--fg') : famColor(CLS[n.a.owners[0]].Family), stroke = css('--bg'), sw = 1.2;
    if (spec) { const pr = priceOf(n); fill = pr.native ? css('--hot') : tierCol(pr.t); if (pr.native) { stroke = css('--fg'); sw = 1.6; } }
    const c = el('circle', { cx: n.px, cy: n.py, r: big ? 5.2 : 4.2, fill, stroke, 'stroke-width': sw, 'data-tip': 'mv:' + i }, gN);
    if (n.a.Kind === 'Spell') c.setAttribute('stroke-dasharray', '2 1.4');
  });
  // Heroes on the rim
  M.heroes.forEach(h => {
    const open = h.id.startsWith('new:'), sel = spec === h.id, sz = sel ? 9 : h.border ? 8 : 6;
    const col = open ? css('--bg') : famColor(CLS[h.id].Family);
    const d = el('rect', { x: h.px - sz, y: h.py - sz, width: sz * 2, height: sz * 2, transform: `rotate(45 ${h.px} ${h.py})`, fill: sel ? css('--hot') : col, stroke: open ? css('--hot') : h.border ? css('--fg') : css('--bg'), 'stroke-width': open ? 1.6 : h.border ? 2 : 1.6, 'stroke-dasharray': open ? '3 2' : 'none', class: 'cls', 'data-tip': 'h:' + h.i, tabindex: open ? -1 : 0, role: open ? 'img' : 'button', 'aria-label': open ? 'Open slot' : 'Spec from ' + name(h.id) }, gH);
    const ang = Math.atan2(h.py, h.px), deg = ang * 180 / Math.PI, flip = Math.cos(ang) < 0, lx = h.px + Math.cos(ang) * 14, ly = h.py + Math.sin(ang) * 14;
    const t = el('text', { x: lx, y: ly, fill: open ? css('--hot') : sel ? css('--hot') : css('--fg'), 'font-family': css('--body'), 'font-size': 10.5, 'font-weight': h.border || sel ? 600 : 400, 'font-style': open ? 'italic' : 'normal', 'text-anchor': flip ? 'end' : 'start', 'dominant-baseline': 'middle', transform: `rotate(${flip ? deg + 180 : deg} ${lx} ${ly})`, 'pointer-events': 'none', opacity: spec && !sel ? 0.7 : 0.95 }, gT);
    t.textContent = open ? '+ new hero' : name(h.id);
  });
  panel();
}

function tipHtml(key) {
  const [k, v] = [key.slice(0, key.indexOf(':') < 0 ? key.length : key.indexOf(':')), key.slice(key.indexOf(':') + 1)];
  if (k === 'core') return `<div class="k">Common core</div><h3>${M.nodes.filter(n => n.kind === 'core').length} shared moves</h3><p>Moves that heroes from three or more families already use. Every hero reaches them at base price.</p>`;
  if (k === 'arc') {
    const a = M.arcs[+v], who = a.interior.map(name).join(', '), b = a.border.map(name);
    const mix = a.ranked.map(([f, n]) => `${FAM[f].Name} ${n}`).join(' · ');
    return `<div class="k">Family ${+v + 1} of ${N} · ${STAT[FAM[a.fam].Primary]} / ${STAT[FAM[a.fam].Secondary]}</div><h3>${esc(a.name)}</h3><p>${esc(FAM[a.fam].Theme)}</p><p class="m">Borders: ${esc(b[0])} and ${esc(b[1])}</p><p class="m">Inside: ${esc(who)}</p><p class="m">Made of today's ${esc(mix)}</p>`;
  }
  if (k === 'h') {
    const h = M.heroes[+v];
    if (h.id.startsWith('new:')) { const a = M.arcs[M.arcOfX(h.i)]; return `<div class="k">Open slot · ${esc(a.name)}</div><h3>New hero here</h3><p>Fits between ${esc(name(M.ring[(h.i - 1 + M.S) % M.S]))} and ${esc(name(M.ring[(h.i + 1) % M.S]))}. Adding a hero here makes every family the same size.</p>`; }
    const c = CLS[h.id], arcsIn = h.border ? [M.arcs[(h.i / M.m) % N], M.arcs[(h.i / M.m - 1 + N) % N]] : [M.arcs[M.arcOfX(h.i)]];
    return `<div class="k">${esc(FAM[c.Family].Name)} today · ${esc(c.Role || 'Hero')}</div><h3>${esc(c.Name)}</h3><p>${h.border ? `Stands on the border: belongs to both <b>${esc(arcsIn[1].name)}</b> and <b>${esc(arcsIn[0].name)}</b>.` : `In <b>${esc(arcsIn[0].name)}</b>.`} Neighbours: ${esc(name(M.ring[(h.i - 1 + M.S) % M.S]))} and ${esc(name(M.ring[(h.i + 1) % M.S]))}.</p><p class="m">Click to price the wheel from here.</p>`;
  }
  if (k === 'mv') {
    const n = M.nodes[+v], a = n.a, req = Object.entries(a.Requires || {}).map(([s, x]) => `${STAT[s] || s} ${x}`).join(', ');
    const where = n.kind === 'core' ? 'Common core' : M.arcs[M.arcOfX(n.x)].name;
    const pr = priceOf(n);
    const owners = a.owners.map(name);
    return `<div class="k">${esc(where)} · ${esc(a.Kind || 'Move')}${a.Cost ? ' · ' + a.Cost + ' orb' + (a.Cost > 1 ? 's' : '') : ''}${req ? ' · needs ' + esc(req) : ''}</div><h3>${esc(a.Name)}</h3>${a.Desc ? `<p>${esc(a.Desc)}</p>` : ''}${pr ? `<p><b style="color:${pr.native ? css('--hot') : tierCol(pr.t)}">${esc(name(spec))}</b>: ${esc(pr.txt)}.</p>` : ''}<p class="m">In the kit of ${owners.length > 6 ? owners.slice(0, 6).map(esc).join(', ') + ` and ${owners.length - 6} more` : owners.map(esc).join(', ')}.</p>`;
  }
  if (k === 'pas') {
    const p = M.pas[+v], pr = priceOf(p), a = M.arcs[M.arcOfX(p.x)];
    return `<div class="k">Stat passive · ${esc(a.name)}${p.row === 0 ? ' · rim' : ''}</div><h3>${esc(p.label)}</h3><p>${p.row === 0 ? 'Rim passives blend the two heroes either side, so walking the rim is horizontal growth: stats now, a neighbour family\'s moves next.' : 'Inner passives deepen the family\'s own stats between its moves.'}</p>${pr ? `<p><b style="color:${tierCol(pr.t)}">${esc(name(spec))}</b>: ${esc(pr.txt)}.</p>` : ''}`;
  }
  return '';
}

function panel() {
  const s = M.s, legend = document.getElementById('legend'), bars = document.getElementById('bars');
  legend.textContent = ''; bars.textContent = '';
  document.getElementById('ptitle').textContent = spec ? 'Spec from ' + name(spec) : 'Reading the wheel';
  document.getElementById('pbody').textContent = spec
    ? `Every node priced for a ${name(spec)}: its own kit, its own family at base price, then +1 SP and +1 to stat needs for each family border crossed. A hero on a border starts in both families.`
    : `${SHAPE[N]}: ${N} families of ${s.m} heroes each (${s.ring.length} slots). Diamonds on the rim are heroes; outlined ones stand on a border. Dots are moves (dashed = spell), small squares are stat passives. Pick a hero to price the wheel from it.`;
  if (spec) {
    const all = [...M.nodes, ...(showPas ? M.pas : [])], cnt = {};
    all.forEach(n => { const p = priceOf(n); const key = p.native ? 'kit' : p.t; cnt[key] = (cnt[key] || 0) + 1; });
    const keys = ['kit', ...Array.from({ length: Math.floor(N / 2) + 1 }, (_, i) => i + 1)];
    const top = Math.max(...keys.map(k => cnt[k] || 0), 1);
    keys.forEach(k => {
      const lab = k === 'kit' ? 'Kit' : k === 1 ? 'Own family' : `${k - 1} border${k > 2 ? 's' : ''}`;
      const col = k === 'kit' ? css('--hot') : tierCol(k);
      const b = document.createElement('div'); b.className = 'bar'; b.innerHTML = `<span>${lab}</span><i style="width:${((cnt[k] || 0) / top) * 100}%;background:${col}"></i><span style="text-align:right">${cnt[k] || 0}</span>`; bars.appendChild(b);
    });
  } else {
    const seen = new Set(); M.arcs.forEach(a => { if (seen.has(a.fam)) return; seen.add(a.fam); const d = document.createElement('div'); d.innerHTML = `<span class="sw" style="background:${famColor(a.fam)}"></span>${esc(FAM[a.fam].Name)} today`; legend.appendChild(d); });
  }
  const ar = document.getElementById('arcs'); ar.textContent = '';
  M.arcs.forEach((a, j) => { const d = document.createElement('div'); d.className = 'arc'; d.innerHTML = `<b style="color:${famColor(a.fam)}">${esc(a.name)}</b><span>${esc(name(a.border[0]))} ▸ ${esc(a.interior.map(name).join(', '))} ◂ ${esc(name(a.border[1]))}</span>`; ar.appendChild(d); });
  const ch = document.getElementById('changes'); ch.textContent = '';
  if (s.mode === 'add') {
    if (!s.added) ch.innerHTML = '<div>No change: the ring already divides evenly.</div>';
    M.heroes.filter(h => h.id.startsWith('new:')).forEach(h => { const d = document.createElement('div'); d.innerHTML = `<b>+ New hero</b> in ${esc(M.arcs[M.arcOfX(h.i)].name)} <span class="m">between ${esc(name(M.ring[(h.i - 1 + M.S) % M.S]))} and ${esc(name(M.ring[(h.i + 1) % M.S]))}</span>`; ch.appendChild(d); });
  } else {
    if (!s.removed.length) ch.innerHTML = '<div>No change: the ring already divides evenly.</div>';
    s.removed.forEach(id => { const d = document.createElement('div'); d.innerHTML = `<b>− ${esc(name(id))}</b> <span class="m">(${esc(FAM[CLS[id].Family].Name)} today)</span>`; ch.appendChild(d); });
    if (M.orphans.length) { const d = document.createElement('div'); d.className = 'm'; d.textContent = `${M.orphans.length} moves only the dropped heroes had leave the wheel.`; ch.appendChild(d); }
  }
}

function compare() {
  const tb = document.querySelector('#cmp tbody'); tb.textContent = '';
  let best = null; Object.values(SOL).forEach(s => { if (!best || s.score > best.score) best = s; });
  [5, 6, 7, 8].forEach(n => ['add', 'trim'].forEach(p => {
    const s = SOL[n + p], ch = p === 'add' ? (s.added ? `+${s.added} new hero${s.added > 1 ? 'es' : ''}` : 'none') : (s.removed.length ? `−${s.removed.length} hero${s.removed.length > 1 ? 'es' : ''}` : 'none');
    const tr = document.createElement('tr'); if (n === N && p === PLAN) tr.className = 'on'; if (s === best) tr.classList.add('best');
    const fams = s.arcs.map(arcName).join(', ');
    tr.innerHTML = `<td>${SHAPE[n]} · ${n}</td><td>${ch}</td><td class="n">${s.m}</td><td class="n"><span class="fitbar" style="width:${Math.round(s.score * 60)}px"></span>${(s.score * 100).toFixed(0)}%</td><td>${esc(fams)}</td>`;
    tr.onclick = () => { N = n; PLAN = p; setup(); };
    tb.appendChild(tr);
  }));
}

function setup() {
  const segN = document.getElementById('segN'); segN.textContent = '';
  [5, 6, 7, 8].forEach(n => { const b = document.createElement('button'); b.type = 'button'; b.textContent = `${SHAPE[n]} · ${n}`; b.setAttribute('aria-pressed', n === N); b.onclick = () => { N = n; setup(); }; segN.appendChild(b); });
  const segP = document.getElementById('segP'); segP.textContent = '';
  ['add', 'trim'].forEach(p => { const s = SOL[N + p]; const b = document.createElement('button'); b.type = 'button'; b.textContent = p === 'add' ? `Add ${s.added}` : `Drop ${s.removed.length}`; b.setAttribute('aria-pressed', p === PLAN); b.onclick = () => { PLAN = p; setup(); }; segP.appendChild(b); });
  const s = SOL[N + PLAN]; if (spec && !(spec in Object.fromEntries(s.ring.map(x => [x, 1])))) spec = null;
  const sel = document.getElementById('cls'); sel.length = 1;
  s.arcs.forEach(a => { const og = document.createElement('optgroup'); og.label = arcName(a); a.interior.concat(a.border.slice(0, 1)).filter(id => !id.startsWith('new:')).sort((x, y) => name(x).localeCompare(name(y))).forEach(id => { const o = document.createElement('option'); o.value = id; o.textContent = name(id); og.appendChild(o); }); sel.appendChild(og); });
  sel.value = spec || '';
  compare(); render();
  try { localStorage.setItem('gg-wheel2', JSON.stringify({ N, PLAN, spec, showPas })); } catch (e) {}
}
function setSpec(id) { spec = id || null; document.getElementById('cls').value = spec || ''; document.getElementById('detail').textContent = ''; render(); try { localStorage.setItem('gg-wheel2', JSON.stringify({ N, PLAN, spec, showPas })); } catch (e) {} }

document.getElementById('cls').onchange = e => setSpec(e.target.value);
document.getElementById('clear').onclick = () => setSpec('');
document.getElementById('pas').onchange = e => { showPas = e.target.checked; render(); };
const place = e => { const pad = 14, w = tip.offsetWidth, h = tip.offsetHeight; let x = e.clientX + pad, y = e.clientY + pad; if (x + w > innerWidth - 8) x = e.clientX - w - pad; if (y + h > innerHeight - 8) y = e.clientY - h - pad; tip.style.left = Math.max(8, x) + 'px'; tip.style.top = Math.max(8, y) + 'px'; };
svg.addEventListener('pointermove', e => { if (e.pointerType === 'touch') return; const t = e.target.closest('[data-tip]'); if (!t) { tip.hidden = true; return; } tip.innerHTML = tipHtml(t.getAttribute('data-tip')); tip.hidden = false; place(e); });
svg.addEventListener('pointerleave', () => tip.hidden = true);
svg.addEventListener('click', e => {
  const t = e.target.closest('[data-tip]'); if (!t) return; const key = t.getAttribute('data-tip');
  if (key.startsWith('h:')) { const h = M.heroes[+key.slice(2)]; if (!h.id.startsWith('new:')) { setSpec(h.id); return; } }
  const det = document.getElementById('detail'); det.innerHTML = tipHtml(key);
  if (e.pointerType === 'touch' || matchMedia('(max-width: 900px)').matches) det.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
});
svg.addEventListener('keydown', e => { if ((e.key === 'Enter' || e.key === ' ') && e.target.classList.contains('cls')) { e.preventDefault(); const h = M.heroes[+e.target.getAttribute('data-tip').slice(2)]; if (!h.id.startsWith('new:')) setSpec(h.id); } });

try { const sv = JSON.parse(localStorage.getItem('gg-wheel2')); if (sv) { N = [5, 6, 7, 8].includes(sv.N) ? sv.N : 8; PLAN = sv.PLAN === 'trim' ? 'trim' : 'add'; spec = sv.spec || null; showPas = sv.showPas !== false; } } catch (e) {}
document.getElementById('pas').checked = showPas;
setup();
})();
