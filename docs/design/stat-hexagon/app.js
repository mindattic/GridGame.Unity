(() => {
// Corners clockwise from the top, as in the owner's sketch. Opposites: VIT-SPD, STR-WIS, AGI-INT.
const STATS = ['VIT', 'STR', 'AGI', 'SPD', 'WIS', 'INT'];
const FAMS = [
  { name: 'Martial', theme: 'Knights and warlords: armour that learns to hit back.' },
  { name: 'Brawn', theme: 'Fists and blades: raw power sharpened into technique.' },
  { name: 'Shadow', theme: 'Ninjas and killers: precision that becomes pure speed.' },
  { name: 'Wild', theme: 'Hunters, beasts and rangers: speed that settles into instinct.' },
  { name: 'Brains', theme: 'Mages, witches, tacticians and builders: insight turned into intellect.' },
  { name: 'Grace', theme: 'Healers and holy guardians: intellect hardening into a shield.' }
];
// Family k runs from corner k (A) to corner k+1 (B). Its far side is corners k+3 (D, across from A) and k+4 (E, across from B).
// Heroes by recipe: four points each. Key order does not matter.
const H = {
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
const DROPPED = [['Basher', 'Brawn'], ['Operative', 'Machina'], ['Tinkerer', 'Machina'], ['Technician', 'Machina']];
const MOVED_NOTE = 'Four heroes leave and one new hero is needed, so the hexagon is exactly 66: Machina had more builders than Brains needs, and Basher overlaps Bruiser. Machina’s heroes join Brains; Arcane and Occult split between Brains and Grace.';
const NEW_SLOTS = { 'SPD1 WIS2 STR1': 'a Wild druid-brawler: quick, wise, hits hard' };

const R = 400, NS = 'http://www.w3.org/2000/svg';
const svg = document.getElementById('hex'), tip = document.getElementById('tip');
const el = (t, a, p) => { const e = document.createElementNS(NS, t); for (const k in a) e.setAttribute(k, a[k]); if (p) p.appendChild(e); return e; };
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[c]));
const css = v => getComputedStyle(document.documentElement).getPropertyValue(v).trim();
const sc = i => css('--s-' + STATS[((i % 6) + 6) % 6]);
const V = i => { const a = (-90 + 60 * i) * Math.PI / 180; return [Math.cos(a), Math.sin(a)]; };
const lerp = (p, q, t) => [p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t];
const mul = (p, s) => [p[0] * s * R, p[1] * s * R];
const key = parts => parts.filter(([, w]) => w > 0).sort((a, b) => b[1] - a[1] || STATS.indexOf(a[0]) - STATS.indexOf(b[0])).map(([s, w]) => s + w).join(' ');
const S = i => STATS[((i % 6) + 6) % 6];

// Build every slot with its position, recipe and owning families.
const slots = [], pas = [];
for (let k = 0; k < 6; k++) {
  const A = V(k), B = V(k + 1), a = S(k), b = S(k + 1), d = S(k + 3), e = S(k + 4);
  slots.push({ kind: 'corner', fams: [(k + 5) % 6, k], parts: [[a, 4]], p: mul(A, 1) });
  [[3, 1], [2, 2], [1, 3]].forEach(([x, y], i) => slots.push({ kind: 'side', fams: [k], parts: [[a, x], [b, y]], p: mul(lerp(A, B, (i + 1) / 4), 1), hybrid: x === y }));
  [[2, 1, d], [2, 1, e], [1, 2, d], [1, 2, e]].forEach(([x, y, f], i) => slots.push({ kind: 'inner', fams: [k], parts: [[a, x], [b, y], [f, 1]], p: mul(lerp(A, B, 0.2 + 0.2 * i), 0.66) }));
  slots.push({ kind: 'spoke', fams: [(k + 5) % 6, k], parts: [[a, 3], [d, 1]], p: mul(A, 0.38) });
  slots.push({ kind: 'spur', fams: [k], parts: [[a, 3], [S(k - 2), 1]], p: mul(lerp(A, B, 0.13), 0.83) });
  slots.push({ kind: 'spur', fams: [(k + 5) % 6], parts: [[a, 3], [S(k + 2), 1]], p: mul(lerp(A, V(k - 1), 0.13), 0.83) });
  // Passives: three along each side between heroes, plus the corner passives each family shares with its neighbour
  [[0.125, `+3 ${a}`], [0.375, `+2 ${a} +1 ${b}`], [0.625, `+1 ${a} +2 ${b}`], [0.875, `+3 ${b}`]].forEach(([t, l]) => pas.push({ p: mul(lerp(A, B, t), 1), label: l, fam: k, ring: 'side', shared: t === 0.125 || t === 0.875 }));
  [[0.3, `+1 ${a} +1 ${b} +1 ${d}`], [0.5, `+2 ${a} +2 ${b}`], [0.7, `+1 ${a} +1 ${b} +1 ${e}`]].forEach(([t, l]) => pas.push({ p: mul(lerp(A, B, t), 0.66), label: l, fam: k, ring: 'inner' }));
  pas.push({ p: mul(A, 0.62), label: `+2 ${a} +1 ${d}`, fam: k, ring: 'spoke', shared: true });
}
const HN = {}; Object.entries(H).forEach(([k, n]) => { HN[key(k.split(' ').map(t => [t.slice(0, 3), +t.slice(3)]))] = n; });
slots.forEach(s => { s.key = key(s.parts); s.name = HN[s.key] || null; });
const NN = {}; Object.entries(NEW_SLOTS).forEach(([k, n]) => { NN[key(k.split(' ').map(t => [t.slice(0, 3), +t.slice(3)]))] = n; });
const missing = slots.filter(s => !s.name).map(s => s.key + (NN[s.key] ? ' (' + NN[s.key] + ')' : ''));
slots.forEach(s => { if (!s.name && NN[s.key]) s.open = NN[s.key]; });
const unused = Object.keys(HN).filter(k => !slots.some(s => s.key === k));

let showPas = true, showLab = true;
function render() {
  svg.textContent = '';
  const gF = el('g', {}, svg), gL = el('g', {}, svg), gP = el('g', {}, svg), gH = el('g', {}, svg), gT = el('g', {}, svg);
  for (let k = 0; k < 6; k++) {
    const A = V(k), B = V(k + 1), col = sc(k);
    const grad = el('linearGradient', { id: 'g' + k, gradientUnits: 'userSpaceOnUse', x1: A[0] * R, y1: A[1] * R, x2: B[0] * R, y2: B[1] * R }, gF);
    el('stop', { offset: '0', 'stop-color': sc(k) }, grad); el('stop', { offset: '1', 'stop-color': sc(k + 1) }, grad);
    el('path', { d: `M0 0 L${A[0] * R} ${A[1] * R} L${B[0] * R} ${B[1] * R} Z`, fill: `url(#g${k})`, 'fill-opacity': 0.08, 'data-tip': 'fam:' + k }, gF);
    el('line', { x1: A[0] * R, y1: A[1] * R, x2: B[0] * R, y2: B[1] * R, stroke: `url(#g${k})`, 'stroke-width': 3, 'stroke-opacity': 0.8 }, gL);
    const i0 = mul(A, 0.66), i1 = mul(B, 0.66);
    el('line', { x1: i0[0], y1: i0[1], x2: i1[0], y2: i1[1], stroke: css('--muted'), 'stroke-opacity': 0.3 }, gL);
    el('line', { x1: 0, y1: 0, x2: A[0] * R, y2: A[1] * R, stroke: sc(k), 'stroke-opacity': 0.35, 'stroke-dasharray': '4 5' }, gL);
    // side to inner-row links
    for (let i = 0; i < 3; i++) { const s = mul(lerp(A, B, (i + 1) / 4), 1); [0.2 + 0.2 * i, 0.4 + 0.2 * i].forEach(t => { const q = mul(lerp(A, B, t), 0.66); el('line', { x1: s[0], y1: s[1], x2: q[0], y2: q[1], stroke: css('--muted'), 'stroke-opacity': 0.16 }, gL); }); }
    // corner stat label and family name
    const cl = mul(A, 1.16), t = el('text', { x: cl[0], y: cl[1], fill: col, 'font-family': css('--display'), 'font-weight': 700, 'font-size': 26, 'text-anchor': 'middle', 'dominant-baseline': 'middle' }, gT); t.textContent = S(k);
    const mid = mul(lerp(A, B, 0.5), 0.5), ang = (-60 + 60 * k) , rot = ((ang % 360) + 360) % 360, flip = rot > 90 && rot < 270;
    const ft = el('text', { x: mid[0], y: mid[1], fill: css('--fg'), opacity: 0.85, 'font-family': css('--display'), 'font-weight': 700, 'font-size': 17, 'letter-spacing': 3, 'text-anchor': 'middle', 'dominant-baseline': 'middle', transform: `rotate(${flip ? ang + 180 : ang} ${mid[0]} ${mid[1]})`, 'pointer-events': 'none' }, gT);
    ft.textContent = FAMS[k].name.toUpperCase();
    const st = el('text', { x: mid[0], y: mid[1] + 0, dy: 18, fill: css('--muted'), 'font-family': css('--mono'), 'font-size': 10.5, 'text-anchor': 'middle', transform: `rotate(${flip ? ang + 180 : ang} ${mid[0]} ${mid[1]})`, 'pointer-events': 'none' }, gT);
    st.textContent = `${S(k)} → ${S(k + 1)}`;
  }
  el('circle', { cx: 0, cy: 0, r: 7, fill: css('--muted'), opacity: 0.5 }, gL);
  if (showPas) pas.forEach((p, i) => el('rect', { x: p.p[0] - 4, y: p.p[1] - 4, width: 8, height: 8, fill: css('--panel'), stroke: p.shared ? css('--fg') : css('--muted'), 'stroke-width': p.shared ? 1.6 : 1, transform: `rotate(45 ${p.p[0]} ${p.p[1]})`, 'data-tip': 'pas:' + i }, gP));
  slots.forEach((s, i) => {
    const r = s.kind === 'corner' ? 12 : s.hybrid ? 10 : 8;
    const g = el('g', { class: 'hero', 'data-tip': 'h:' + i, tabindex: 0, role: 'button', 'aria-label': (s.name || 'Open slot') + ', ' + s.key }, gH);
    // pie by recipe
    let a0 = -Math.PI / 2; const total = 4;
    s.parts.forEach(([st, w]) => { const a1 = a0 + 2 * Math.PI * w / total; const large = w / total > 0.5 ? 1 : 0;
      if (w === total) el('circle', { cx: s.p[0], cy: s.p[1], r, fill: sc(STATS.indexOf(st)) }, g);
      else el('path', { d: `M${s.p[0]} ${s.p[1]} L${s.p[0] + r * Math.cos(a0)} ${s.p[1] + r * Math.sin(a0)} A${r} ${r} 0 ${large} 1 ${s.p[0] + r * Math.cos(a1)} ${s.p[1] + r * Math.sin(a1)} Z`, fill: sc(STATS.indexOf(st)) }, g);
      a0 = a1; });
    el('circle', { cx: s.p[0], cy: s.p[1], r: r + 1.5, fill: 'none', stroke: s.fams.length > 1 ? css('--fg') : css('--bg'), 'stroke-width': s.fams.length > 1 ? 2 : 1.5 }, g);
    if (showLab && (s.name || s.open)) {
      const out = s.kind === 'corner' || s.kind === 'side', dir = Math.atan2(s.p[1], s.p[0]);
      let lx, ly, anchor, base = 'middle';
      if (out && s.kind === 'side') { lx = s.p[0] + Math.cos(dir) * 18; ly = s.p[1] + Math.sin(dir) * 18; anchor = Math.abs(Math.cos(dir)) < 0.3 ? 'middle' : Math.cos(dir) > 0 ? 'start' : 'end'; base = Math.abs(Math.cos(dir)) < 0.3 ? (Math.sin(dir) > 0 ? 'hanging' : 'auto') : 'middle'; }
      else if (s.kind === 'corner') { lx = s.p[0] - Math.cos(dir) * 30; ly = s.p[1] - Math.sin(dir) * 30; anchor = 'middle'; }
      else { lx = s.p[0]; ly = s.p[1] + r + 11; anchor = 'middle'; }
      const t = el('text', { x: lx, y: ly, fill: s.hybrid ? css('--hot') : css('--fg'), 'font-family': css('--body'), 'font-size': s.kind === 'corner' ? 12.5 : 10.5, 'font-weight': s.kind === 'corner' || s.hybrid ? 600 : 400, 'text-anchor': anchor, 'dominant-baseline': base, 'pointer-events': 'none', opacity: 0.95 }, gT);
      t.textContent = s.name || '+ new hero'; if (!s.name) { t.setAttribute('fill', css('--hot')); t.setAttribute('font-style', 'italic'); }
    }
  });
}

const role = { corner: 'Corner: pure stat, shared by both families', side: 'Side', inner: 'Inner row: side stats plus one from the far side', spoke: 'Spoke: corner stat plus its opposite, shared by both families', spur: 'Spur: corner stat plus a far-side stat' };
function tipHtml(k) {
  const [t, v] = [k.slice(0, k.indexOf(':')), +k.slice(k.indexOf(':') + 1)];
  if (t === 'h') {
    const s = slots[v], fam = s.fams.map(f => FAMS[f].name).join(' and ');
    const pairs = []; for (let i = 0; i < s.parts.length; i++) for (let j = i + 1; j < s.parts.length; j++) pairs.push(s.parts[i][0] + '–' + s.parts[j][0]);
    return `<div class="k">${esc(fam)} · ${esc(s.hybrid ? 'Perfect hybrid' : role[s.kind])}</div><h3>${esc(s.name || 'New hero')}</h3>${s.open ? `<p>${esc(s.open)}</p>` : ''}<p class="recipe">${s.parts.map(([st, w]) => `<span style="color:${sc(STATS.indexOf(st))}">${st} ${w}</span>`).join(' · ')}</p>${pairs.length ? `<p class="m">Covers ${esc(pairs.join(', '))}</p>` : ''}`;
  }
  if (t === 'pas') { const p = pas[v]; return `<div class="k">Passive · ${esc(FAMS[p.fam].name)}${p.shared ? ' · shared with the next family' : ''}</div><h3>${esc(p.label)}</h3><p>${p.ring === 'side' ? 'Walk the side to trade one stat for the next.' : p.ring === 'inner' ? 'Inner passives add a point from the far side.' : 'Spoke passive: the corner stat plus its opposite.'}</p>`; }
  if (t === 'fam') { const k2 = v, mem = slots.filter(s => s.fams.includes(k2)); return `<div class="k">Family · ${S(k2)} → ${S(k2 + 1)} · far side ${S(k2 + 3)}, ${S(k2 + 4)}</div><h3>${esc(FAMS[k2].name)}</h3><p>${esc(FAMS[k2].theme)}</p><p class="m">${mem.length} heroes (${mem.filter(s => s.fams.length > 1).length} shared with neighbours)</p>`; }
  return '';
}

function panel() {
  const f = document.getElementById('fams'); f.textContent = '';
  FAMS.forEach((fm, k) => { const own = slots.filter(s => s.fams.length === 1 && s.fams[0] === k).map(s => s.name), sh = slots.filter(s => s.fams.length > 1 && s.fams.includes(k)).map(s => s.name);
    const d = document.createElement('div'); d.innerHTML = `<b style="color:${sc(k)}">${esc(fm.name)}</b> <span class="recipe">${S(k)}→${S(k + 1)}</span><br><span>${esc(own.join(', '))}</span><br><span class="m">Shared: ${esc(sh.join(', '))}</span>`; f.appendChild(d); });
  const t = document.getElementById('pairs'); t.textContent = '';
  const hr = document.createElement('tr'); hr.innerHTML = '<th></th>' + STATS.map(s => `<th style="color:${sc(STATS.indexOf(s))}">${s}</th>`).join(''); t.appendChild(hr);
  STATS.forEach((a, i) => { const tr = document.createElement('tr'); tr.innerHTML = `<th style="color:${sc(i)}">${a}</th>` + STATS.map((b, j) => { if (i === j) { const n = slots.filter(s => s.parts.length === 1 && s.parts[0][0] === a).length; return `<td class="d">${n}</td>`; } const n = slots.filter(s => s.parts.some(p => p[0] === a) && s.parts.some(p => p[0] === b)).length; return `<td title="${a}–${b}">${n}</td>`; }).join(''); t.appendChild(tr); });
  const d = document.getElementById('drops');
  d.innerHTML = `<p>${esc(MOVED_NOTE)}</p>` + DROPPED.map(([n, fm]) => `<div>− ${esc(n)} <span class="m">(${esc(fm)} today)</span></div>`).join('') + (missing.length ? missing.map(m => `<div>+ New hero ${esc(m)}</div>`).join('') : '') + (unused.length ? `<div class="m">Unplaced recipes: ${esc(unused.join(', '))}</div>` : '') + `<p>Stamina and Luck leave hero identity. Luck can stay as a gear and item stat if wanted.</p>`;
}

const place = e => { const pad = 14, w = tip.offsetWidth, h = tip.offsetHeight; let x = e.clientX + pad, y = e.clientY + pad; if (x + w > innerWidth - 8) x = e.clientX - w - pad; if (y + h > innerHeight - 8) y = e.clientY - h - pad; tip.style.left = Math.max(8, x) + 'px'; tip.style.top = Math.max(8, y) + 'px'; };
svg.addEventListener('pointermove', e => { if (e.pointerType === 'touch') return; const t = e.target.closest('[data-tip]'); if (!t) { tip.hidden = true; return; } tip.innerHTML = tipHtml(t.getAttribute('data-tip')); tip.hidden = false; place(e); });
svg.addEventListener('pointerleave', () => tip.hidden = true);
svg.addEventListener('click', e => { const t = e.target.closest('[data-tip]'); if (!t) return; document.getElementById('detail').innerHTML = tipHtml(t.getAttribute('data-tip')); });
svg.addEventListener('keydown', e => { if ((e.key === 'Enter' || e.key === ' ') && e.target.closest('[data-tip]')) { e.preventDefault(); document.getElementById('detail').innerHTML = tipHtml(e.target.closest('[data-tip]').getAttribute('data-tip')); } });
document.getElementById('pas').onchange = e => { showPas = e.target.checked; render(); };
document.getElementById('lab').onchange = e => { showLab = e.target.checked; render(); };
render(); panel();
})();
