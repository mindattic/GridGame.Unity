(() => {
// Edit mode for the skill hexagon page. "Edit board" turns planning off and makes clicks edit skillhex.json in
// memory: place, move or swap abilities, turn slots Open and back, write Open slot notes, tune passives, and edit
// heroes (Start path, Signature, Recipe, Status, Name, Class, Note). Every edit goes through edit(), which keeps an
// undo snapshot, rebuilds the board through window.SkillHexView (app.js) and re-runs the checks.
// Save writes the file in the same one-node-or-hero-per-line layout as tools/skillhex/regenerate.py, so a diff
// shows only what changed. Then run tools/skillhex/sync.py and validate.py (tools/skillhex/README.md).
const V = window.SkillHexView, D = window.WHEEL_DATA;
const $ = id => document.getElementById(id);
const { esc } = V;
const STAT_IDS = ['VIT', 'STR', 'AGI', 'SPD', 'WIS', 'INT'];
const DIRS = [[1, 0], [0, 1], [-1, 1], [-1, 0], [0, -1], [1, -1]];
const RECIPE_POINTS = 4, UNDO_MAX = 200;
const CLASSES = new Set(D.classes.map(c => c.Id));
const USERS = {}; D.classes.forEach(c => (c.Abilities || []).concat(c.Loadout || []).forEach(id => { (USERS[id] = USERS[id] || new Set()).add(c.Id); }));
const ALL = Object.keys(D.abilities).sort((a, b) => abName(a).localeCompare(abName(b)) || a.localeCompare(b));
function abName(id) { return (D.abilities[id] && D.abilities[id].Name) || id; }
const doc = () => V.doc;

// --- State
let on = false, tool = 'ability', armed = null, sel = -1, focus = -1, heroI = 0, undo = [], dirty = false;
let fileName = 'skillhex.json', note = '', issues = [], badNodes = new Set();
const restore = {}; // "Q,R" -> the ability a "Make Open" took off that slot, for "Restore"
const key = n => n.Q + ',' + n.R;

// --- Writing the file: same layout as regenerate.py's dump() (json.dumps with ", " and ": ", no ASCII escaping).
function py(v) {
  if (v === null || typeof v !== 'object') return JSON.stringify(v);
  if (Array.isArray(v)) return '[' + v.map(py).join(', ') + ']';
  return '{' + Object.keys(v).map(k => JSON.stringify(k) + ': ' + py(v[k])).join(', ') + '}';
}
function dump(data) {
  const lines = ['{'], keys = Object.keys(data);
  keys.forEach((k, i) => {
    const v = data[k], end = i < keys.length - 1 ? ',' : '';
    if (Array.isArray(v) && ['Nodes', 'Heroes', 'Stats', 'Families'].includes(k)) {
      lines.push(`  "${k}": [`);
      v.forEach((x, j) => lines.push('    ' + py(x) + (j < v.length - 1 ? ',' : '')));
      lines.push('  ]' + end);
    } else lines.push(`  ${JSON.stringify(k)}: ` + py(v) + end);
  });
  lines.push('}');
  return lines.join('\n') + '\n';
}

// --- Checks: validate.py's rules for what the page can see. Abilities and classes come from data.js, which holds
// hero content only, so "not in data.js" is a warning here; validate.py checks abilities.json and classes.json.
// Not ported: validate.py's warning for a Signature far from its hero's stat-hexagon spot.
const isSlot = (q, r) => (((q - r) % 3) + 3) % 3 === 0;
function validate(d) {
  const out = [], E = (msg, node = -1, hero = -1) => out.push({ lvl: 'error', msg, node, hero }), W = (msg, node = -1, hero = -1) => out.push({ lvl: 'warn', msg, node, hero });
  const stats = (d.Stats || []).map(s => s && s.Id);
  if (stats.length !== 6) E(`Stats: expected 6, found ${stats.length}`);
  if (new Set(stats).size !== stats.length) E('Stats: duplicate ids');
  stats.forEach(s => { if (!STAT_IDS.includes(s)) E(`Stats: unknown stat ${s}`); });
  const fams = d.Families || [];
  if (fams.length !== 6) E(`Families: expected 6, found ${fams.length}`);
  if (stats.length === 6) fams.slice(0, 6).forEach((f, k) => { const a = stats[k], b = stats[(k + 1) % 6]; if (f.From !== a || f.To !== b) E(`Families[${k}] ${f.Id}: runs ${f.From}->${f.To}, corner order needs ${a}->${b}`); });
  const n = d.Radius, inLat = (q, r) => Math.abs(q) <= n && Math.abs(r) <= n && Math.abs(q + r) <= n;
  const seen = new Map(), placed = new Map(), nodeAt = new Map();
  (d.Nodes || []).forEach((node, i) => {
    const q = node.Q, r = node.R, p = `${q},${r}`;
    if (!Number.isInteger(q) || !Number.isInteger(r)) return E(`Nodes[${i}]: needs integer Q and R`, i);
    seen.set(p, (seen.get(p) || 0) + 1); nodeAt.set(p, i);
    if (!inLat(q, r)) return E(`node (${p}): outside the radius-${n} lattice`, i);
    const kind = node.Kind, slot = isSlot(q, r);
    if ((kind === 'Ability' || kind === 'Open') && !slot) E(`node (${p}): ${kind} on a passive point`, i);
    else if (kind === 'Passive' && slot) E(`node (${p}): Passive on an ability slot`, i);
    else if (!['Ability', 'Open', 'Passive'].includes(kind)) E(`node (${p}): unknown Kind ${kind}`, i);
    if (kind === 'Ability') {
      const id = node.Ability;
      if (!id) E(`node (${p}): Ability node without an Ability id`, i);
      else { (placed.get(id) || placed.set(id, []).get(id)).push(i); if (!D.abilities[id]) W(`node (${p}): ability ${id} is not in data.js (validate.py checks abilities.json)`, i); }
    } else if ('Ability' in node) E(`node (${p}): Ability id on a ${kind} node`, i);
    if (kind === 'Passive') {
      const st = node.Stats;
      if (!st || typeof st !== 'object' || !Object.keys(st).length) E(`node (${p}): Passive without Stats`, i);
      else Object.entries(st).forEach(([s, v]) => { if (!stats.includes(s)) E(`node (${p}): passive stat ${s} is not one of the six Stats`, i); if (!Number.isInteger(v) || v < 1) E(`node (${p}): passive ${s} value ${v} must be a positive integer`, i); });
    } else if ('Stats' in node) E(`node (${p}): Stats on a ${kind} node`, i);
    if ('Note' in node && typeof node.Note !== 'string') E(`node (${p}): Note must be text`, i);
  });
  seen.forEach((c, p) => { if (c > 1) E(`node (${p}): listed ${c} times`, nodeAt.get(p)); });
  let missing = 0; for (let q = -n; q <= n; q++) for (let r = -n; r <= n; r++) if (Math.abs(q + r) <= n && !seen.has(`${q},${r}`)) missing++;
  if (missing) E(`${missing} lattice point(s) missing`);
  placed.forEach((ns, id) => { if (ns.length > 1) E(`ability ${id} placed ${ns.length} times`, ns[1]); });
  const heroes = d.Heroes || [], byClass = {}, sigs = {};
  heroes.forEach(h => { if (h.Class) byClass[h.Class] = (byClass[h.Class] || 0) + 1; });
  Object.entries(byClass).forEach(([c, k]) => { if (k > 1) E(`hero class ${c} listed ${k} times`); });
  heroes.forEach((h, hi) => {
    const cls = h.Class, status = h.Status, tag = `hero ${cls || h.Name || '?'}`;
    if (!['Placed', 'Cut', 'New'].includes(status)) E(`${tag}: unknown Status ${status}`, -1, hi);
    if (cls == null) { if (status !== 'New') E(`${tag}: Class is empty but Status is ${status} (only New heroes may lack a class)`, -1, hi); }
    else if (!CLASSES.has(cls)) W(`${tag}: class ${cls} is not in data.js (validate.py checks classes.json)`, -1, hi);
    const rc = h.Recipe;
    if (rc == null) { if (status !== 'Cut') E(`${tag}: ${status} hero without a Recipe`, -1, hi); }
    else {
      const total = Object.values(rc).reduce((s, v) => s + (Number.isInteger(v) ? v : 0), 0);
      if (total !== RECIPE_POINTS) E(`${tag}: Recipe sums to ${total}, not ${RECIPE_POINTS}`, -1, hi);
      Object.keys(rc).forEach(s => { if (!STAT_IDS.includes(s)) E(`${tag}: Recipe stat ${s} is not one of the six Stats`, -1, hi); });
    }
    const cells = (h.Start || []).filter(c => Array.isArray(c) && c.length === 2 && c.every(Number.isInteger)).map(c => `${c[0]},${c[1]}`);
    if (cells.length !== (h.Start || []).length) E(`${tag}: bad Start entry`, -1, hi);
    const dup = cells.filter((c, i) => cells.indexOf(c) !== i);
    if (dup.length) W(`${tag}: Start lists ${[...new Set(dup)].join(' ')} more than once`, nodeAt.get(dup[0]) ?? -1, hi);
    const off = cells.filter(c => { const [q, r] = c.split(',').map(Number); return !inLat(q, r); });
    if (off.length) E(`${tag}: Start points outside the lattice: ${off.join(' ')}`, -1, hi);
    if (!cells.length && status !== 'New') E(`${tag}: ${status} hero with an empty Start`, -1, hi);
    const set = new Set(cells.filter(c => !off.includes(c)));
    if (set.size) {
      const start = set.values().next().value, got = new Set([start]), stack = [start];
      while (stack.length) { const [q, r] = stack.pop().split(',').map(Number); DIRS.forEach(([a, b]) => { const k = `${q + a},${r + b}`; if (set.has(k) && !got.has(k)) { got.add(k); stack.push(k); } }); }
      const stray = [...set].filter(c => !got.has(c));
      if (stray.length) E(`${tag}: Start is not connected; cut off: ${stray.slice(0, 8).join(' ')}${stray.length > 8 ? ' ...' : ''}`, nodeAt.get(stray[0]) ?? -1, hi);
    }
    const sig = h.Signature;
    if (sig == null) { if (status !== 'New') E(`${tag}: ${status} hero without a Signature`, -1, hi); }
    else {
      (sigs[sig] = sigs[sig] || []).push(cls || h.Name);
      if (!D.abilities[sig]) W(`${tag}: Signature ${sig} is not in data.js (validate.py checks abilities.json)`, -1, hi);
      const ns = placed.get(sig);
      if (!ns) E(`${tag}: Signature ${sig} is not placed on the board`, -1, hi);
      else if (!ns.some(i => set.has(key(d.Nodes[i])))) E(`${tag}: Start does not hold its Signature ${abName(sig)} at (${key(d.Nodes[ns[0]])})`, ns[0], hi);
      const others = [...(USERS[sig] || [])].filter(c => c !== cls).sort();
      if (others.length) W(`${tag}: Signature ${sig} is not unique to its class; also in ${others.join(', ')}`, ns ? ns[0] : -1, hi);
    }
  });
  Object.entries(sigs).forEach(([s, owners]) => { if (owners.length > 1) E(`Signature ${s} shared by ${owners.join(', ')}`, placed.has(s) ? placed.get(s)[0] : -1); });
  return out;
}

// --- Edits
function snapshot(label) { undo.push({ label, text: JSON.stringify(doc()) }); if (undo.length > UNDO_MAX) undo.shift(); }
function edit(label, fn) {
  const d = JSON.parse(JSON.stringify(doc())); // edit a copy so a failed edit leaves the board alone
  if (fn(d) === false) return;
  snapshot(label); V.load(d); dirty = true; note = label; refresh();
}
function undoEdit() {
  const u = undo.pop(); if (!u) return;
  V.load(JSON.parse(u.text)); dirty = true; note = 'Undid: ' + u.label;
  heroI = Math.min(heroI, doc().Heroes.length - 1); refresh();
}
const mkNode = (q, r, kind, ab, noteText) => { const n = { Q: q, R: r, Kind: kind }; if (kind === 'Ability') n.Ability = ab; if (noteText) n.Note = noteText; return n; };
// Put ability id on slot t. If it was placed elsewhere its old slot becomes Open, or takes t's ability (a swap).
function place(id, t) {
  const tn = doc().Nodes[t];
  if (tn.Kind === 'Passive') { note = 'Abilities go on ability slots, not passives.'; return refresh(); }
  const s = doc().Nodes.findIndex(n => n.Kind === 'Ability' && n.Ability === id);
  if (s === t) { armed = null; return refresh(); }
  const old = tn.Kind === 'Ability' ? tn.Ability : null;
  edit(old && s >= 0 ? `Swapped ${abName(id)} and ${abName(old)}` : `Placed ${abName(id)} at ${key(tn)}`, d => {
    d.Nodes[t] = mkNode(tn.Q, tn.R, 'Ability', id, tn.Note);
    if (s >= 0) { const sn = d.Nodes[s]; d.Nodes[s] = old ? mkNode(sn.Q, sn.R, 'Ability', old, sn.Note) : mkNode(sn.Q, sn.R, 'Open', null, sn.Note); }
  });
  armed = null; sel = t; refresh();
}
function makeOpen(t) {
  const n = doc().Nodes[t]; if (n.Kind !== 'Ability') return;
  restore[key(n)] = n.Ability;
  edit(`Opened ${key(n)} (took off ${abName(n.Ability)})`, d => { d.Nodes[t] = mkNode(n.Q, n.R, 'Open', null, n.Note); });
}
function setNote(t, text) {
  text = text.trim(); const n = doc().Nodes[t]; if ((n.Note || '') === text) return;
  edit(`Note on ${key(n)}`, d => { if (text) d.Nodes[t].Note = text; else delete d.Nodes[t].Note; });
}
function setStats(t, vals) {
  const n = doc().Nodes[t], st = {};
  Object.keys(n.Stats || {}).concat(STAT_IDS).forEach(s => { if (vals[s] > 0 && !(s in st)) st[s] = vals[s]; });
  if (!Object.keys(st).length) { note = 'A passive needs at least one stat above 0.'; return refresh(); }
  if (py(st) === py(n.Stats)) return;
  edit(`Passive ${key(n)} is now ${V.pasLabel(st)}`, d => { d.Nodes[t].Stats = st; });
}
function heroEdit(label, fn) { edit(label, d => fn(d.Heroes[heroI])); }
function togglePath(t) {
  const h = doc().Heroes[heroI], n = doc().Nodes[t]; if (!h) return;
  const has = (h.Start || []).some(c => c[0] === n.Q && c[1] === n.R);
  heroEdit(`${has ? 'Removed' : 'Added'} ${key(n)} ${has ? 'from' : 'to'} ${h.Name}'s Start`, x => {
    x.Start = x.Start || [];
    if (has) x.Start = x.Start.filter(c => !(c[0] === n.Q && c[1] === n.R)); else x.Start.push([n.Q, n.R]);
  });
}
function addHero() {
  edit('Added a New hero', d => { d.Heroes.push({ Class: null, Name: 'New hero', Recipe: null, Status: 'New', Signature: null, Start: [] }); });
  heroI = doc().Heroes.length - 1; tool = 'hero'; refresh();
}

// --- Open and save
function openFile(file) {
  const fr = new FileReader();
  fr.onload = () => {
    let d;
    try { d = JSON.parse(fr.result); } catch (err) { note = `${file.name} is not valid JSON: ${err.message}`; return refresh(); }
    if (!d || !Array.isArray(d.Nodes) || !Array.isArray(d.Heroes) || !Array.isArray(d.Stats) || !Array.isArray(d.Families) || !Number.isInteger(d.Radius)) { note = `${file.name} does not look like a skillhex.json (needs Radius, Stats, Families, Nodes and Heroes).`; return refresh(); }
    const before = JSON.stringify(doc());
    try { V.load(d); } catch (err) { note = `Could not draw ${file.name}: ${err.message}`; return refresh(); }
    undo.push({ label: 'Opened ' + file.name, text: before });
    fileName = file.name; dirty = false; sel = focus = -1; armed = null; heroI = 0; note = `Opened ${file.name}`; refresh();
  };
  fr.onerror = () => { note = 'Could not read ' + file.name; refresh(); };
  fr.readAsText(file);
}
function save() {
  const blob = new Blob([dump(doc())], { type: 'application/json' }), a = document.createElement('a');
  a.href = URL.createObjectURL(blob); a.download = fileName; document.body.appendChild(a); a.click(); a.remove();
  setTimeout(() => URL.revokeObjectURL(a.href), 2000);
  dirty = false; note = `Saved ${fileName}. Put it at Assets/GridGame/Resources/Content/skillhex.json, then run sync.py and validate.py.`; refresh();
}
async function copy() {
  const text = dump(doc());
  try { await navigator.clipboard.writeText(text); }
  catch (_) { const ta = document.createElement('textarea'); ta.value = text; ta.style.position = 'fixed'; ta.style.opacity = '0'; document.body.appendChild(ta); ta.select(); try { document.execCommand('copy'); } catch (e) { /* nothing more to try */ } ta.remove(); }
  note = `Copied ${text.length.toLocaleString()} characters of JSON. Paste over skillhex.json, then run sync.py and validate.py.`; refresh();
}

// --- Board hooks (called by app.js while attached)
const hooks = {
  click(n) {
    sel = n; focus = -1;
    const node = doc().Nodes[n];
    if (tool === 'hero') return togglePath(n);
    if (armed && node.Kind !== 'Passive') return place(armed, n);
    refresh();
  },
  tip(n) {
    const node = doc().Nodes[n], fam = V.fams[V.weights(V.pts[n].x, V.pts[n].y).k], where = `${esc(fam)} · ${node.Q}, ${node.R}`;
    const act = tool === 'hero' ? 'Click to add to or remove from the Start path' : armed && node.Kind !== 'Passive' ? `Click to put ${esc(abName(armed))} here` : 'Click to edit';
    const nt = node.Note ? `<p>${esc(node.Note)}</p>` : '';
    if (node.Kind === 'Passive') return `<div class="k">Passive · ${where}</div><h3>${esc(V.pasLabel(node.Stats || {}))}</h3>${nt}<p class="m">${act}</p>`;
    if (node.Kind === 'Open') return `<div class="k">Open slot · ${where}</div><h3>Open</h3>${nt}<p class="m">${act}</p>`;
    return `<div class="k">Ability · ${where}</div><h3>${esc(abName(node.Ability))}</h3><p class="m">${esc(node.Ability)}</p>${nt}<p class="m">${act}</p>`;
  },
  overlay(g, el) {
    const ring = (i, r, col, w, dash) => { if (i < 0 || !V.pts[i]) return; const p = V.pts[i]; const c = el('circle', { cx: p.x, cy: p.y, r, fill: 'none', stroke: col, 'stroke-width': w, 'pointer-events': 'none' }, g); if (dash) c.setAttribute('stroke-dasharray', dash); };
    badNodes.forEach(i => ring(i, 9.5, V.css('--bad'), 1.5));
    if (armed) ring(doc().Nodes.findIndex(n => n.Kind === 'Ability' && n.Ability === armed), 12, V.css('--accent'), 2, '4 3');
    ring(sel, 11, V.css('--hot'), 2);
    ring(focus, 16, V.css('--bad'), 2.5);
  },
};

// --- Panel
function refresh() {
  if (!on) return;
  const d = doc(), heroes = d.Heroes;
  heroI = Math.max(0, Math.min(heroI, heroes.length - 1));
  issues = validate(d);
  badNodes = new Set(issues.filter(x => x.lvl === 'error' && x.node >= 0).map(x => x.node));
  // board: the chosen hero's Start in gold
  const h = heroes[heroI], start = h ? (h.Start || []).map(c => V.at(c)).filter(v => v !== undefined) : [];
  V.showPath(start, h && h.Class);
  // status and tools
  $('edstatus').textContent = `${fileName}${dirty ? ' · unsaved changes' : ''} · ${undo.length} undo step${undo.length === 1 ? '' : 's'}`;
  $('ednote').textContent = note; $('ednote').hidden = !note;
  $('edundo').disabled = !undo.length;
  document.querySelectorAll('#editor .seg button').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.tool === tool)));
  $('tool-ability').hidden = tool !== 'ability'; $('tool-hero').hidden = tool !== 'hero';
  if (tool === 'ability') abilityList(); else heroForm();
  inspector(); checks();
}
function abilityList() {
  const placedAt = {}; doc().Nodes.forEach((n, i) => { if (n.Kind === 'Ability') placedAt[n.Ability] = i; });
  const q = $('absearch').value.trim().toLowerCase(), list = $('ablist');
  const rows = ALL.filter(id => !q || id.toLowerCase().includes(q) || abName(id).toLowerCase().includes(q));
  const un = ALL.filter(id => placedAt[id] === undefined).length;
  $('abcount').textContent = `${ALL.length - un} placed · ${un} unplaced${q ? ` · ${rows.length} match` : ''}`;
  list.innerHTML = rows.map(id => { const p = placedAt[id] !== undefined; return `<button type="button" class="abrow${armed === id ? ' on' : ''}" data-id="${esc(id)}" aria-pressed="${armed === id}"><span>${esc(abName(id))}</span><span class="m">${esc(id)}</span><span class="st ${p ? '' : 'un'}">${p ? 'placed' : 'unplaced'}</span></button>`; }).join('');
  $('abhint').innerHTML = armed ? `Placing <b>${esc(abName(armed))}</b>: click an ability or Open slot. Esc cancels.` : 'Pick an ability, then click a slot to put it there. Its old slot turns Open; an occupied target swaps.';
}
function heroForm() {
  const d = doc(), h = d.Heroes[heroI], s = $('ehero');
  s.innerHTML = d.Heroes.map((x, i) => `<option value="${i}">${esc(x.Name)}${x.Status !== 'Placed' ? ` (${esc(x.Status)})` : ''}</option>`).join('');
  s.value = String(heroI);
  if (!h) return;
  $('hname').value = h.Name || ''; $('hclass').value = h.Class || ''; $('hstatus').value = h.Status; $('hnote').value = h.Note || '';
  const rc = h.Recipe || {};
  STAT_IDS.forEach(st => { $('rc-' + st).value = rc[st] || 0; });
  const sum = Object.values(rc).reduce((a, v) => a + v, 0);
  $('rcsum').textContent = h.Recipe ? `${sum} of ${RECIPE_POINTS} points` : 'no Recipe (allowed for Cut heroes)';
  $('rcsum').className = sum === RECIPE_POINTS || (!h.Recipe && h.Status === 'Cut') ? 'm' : 'bad';
  const onPath = (h.Start || []).map(c => V.at(c)).filter(i => i !== undefined && d.Nodes[i].Kind === 'Ability').map(i => d.Nodes[i].Ability);
  const opts = ['<option value="">(none)</option>'].concat(onPath.map(id => `<option value="${esc(id)}">${esc(abName(id))}</option>`));
  if (h.Signature && !onPath.includes(h.Signature)) opts.push(`<option value="${esc(h.Signature)}">${esc(abName(h.Signature))} (not on the path)</option>`);
  $('hsig').innerHTML = opts.join(''); $('hsig').value = h.Signature || '';
  $('hpath').textContent = `${(h.Start || []).length} nodes on the Start path, ${onPath.length} of them abilities. Click nodes on the board to add or remove them.`;
}
function inspector() {
  const box = $('insp');
  if (sel < 0 || !doc().Nodes[sel]) { box.innerHTML = ''; return; }
  const n = doc().Nodes[sel], where = `${n.Q}, ${n.R} · ${esc(V.fams[V.weights(V.pts[sel].x, V.pts[sel].y).k])}`;
  const noteField = `<label class="fld">Note <input type="text" id="inote" value="${esc(n.Note || '')}" placeholder="${n.Kind === 'Open' ? 'What this slot is kept for' : 'Designer note'}"></label>`;
  if (n.Kind === 'Passive') {
    box.innerHTML = `<h3>Passive</h3><p class="m">${where}</p><div class="rc">${STAT_IDS.map(s => `<label><span style="color:${V.sc(s)}">${s}</span><input type="number" min="0" max="9" step="1" id="ps-${s}" value="${(n.Stats || {})[s] || 0}"></label>`).join('')}</div><button class="btn" type="button" id="iapply">Apply stats</button>${noteField}`;
    $('iapply').onclick = () => { const v = {}; STAT_IDS.forEach(s => { v[s] = Math.max(0, parseInt($('ps-' + s).value, 10) || 0); }); setStats(sel, v); };
  } else if (n.Kind === 'Open') {
    const r = restore[key(n)], free = r && !doc().Nodes.some(x => x.Kind === 'Ability' && x.Ability === r);
    box.innerHTML = `<h3>Open slot</h3><p class="m">${where}</p>${noteField}<div class="row">${free ? `<button class="btn" type="button" id="irestore">Restore ${esc(abName(r))}</button>` : ''}</div><p class="m">To fill it, pick an ability in the list and click this slot.</p>`;
    if (free) $('irestore').onclick = () => place(r, sel);
  } else {
    const owners = doc().Heroes.filter(h => h.Signature === n.Ability).map(h => h.Name);
    box.innerHTML = `<h3>${esc(abName(n.Ability))}</h3><p class="m">${esc(n.Ability)} · ${where}</p>${owners.length ? `<p style="color:var(--hot)">Signature of ${esc(owners.join(', '))}</p>` : ''}<div class="row"><button class="btn" type="button" id="imove">Move or swap</button><button class="btn" type="button" id="iopen">Make Open</button></div>${noteField}`;
    $('imove').onclick = () => { armed = n.Ability; tool = 'ability'; refresh(); };
    $('iopen').onclick = () => makeOpen(sel);
  }
  $('inote').onchange = e => setNote(sel, e.target.value);
}
function checks() {
  const errs = issues.filter(x => x.lvl === 'error').length, warns = issues.length - errs;
  $('chsum').textContent = `${errs} error${errs === 1 ? '' : 's'}, ${warns} warning${warns === 1 ? '' : 's'}`;
  $('chsum').className = errs ? 'bad' : 'm';
  $('checks').innerHTML = issues.slice(0, 200).map((x, i) => `<button type="button" class="chk ${x.lvl}" data-i="${i}"><b>${x.lvl === 'error' ? 'Error' : 'Warning'}</b> ${esc(x.msg)}</button>`).join('') + (issues.length > 200 ? `<p class="m">and ${issues.length - 200} more</p>` : '');
}

// --- Wiring
function setOn(v) {
  on = v; document.body.classList.toggle('editing', on);
  $('edit').setAttribute('aria-pressed', String(on)); $('edit').textContent = on ? 'Done editing' : 'Edit board';
  if (on) {
    const w = doc().Heroes.findIndex(h => h.Class === 'warden'); heroI = w >= 0 ? w : 0;
    V.attach(hooks); refresh();
  } else { V.detach(); sel = focus = -1; armed = null; }
}
$('edit').onclick = () => setOn(!on);
$('edopen').onclick = () => $('edfile').click();
$('edfile').onchange = e => { const f = e.target.files[0]; if (f) openFile(f); e.target.value = ''; };
$('edsave').onclick = save;
$('edcopy').onclick = copy;
$('edundo').onclick = undoEdit;
document.querySelectorAll('#editor .seg button').forEach(b => { b.onclick = () => { tool = b.dataset.tool; armed = null; refresh(); }; });
$('absearch').oninput = abilityList;
$('ablist').onclick = e => { const b = e.target.closest('.abrow'); if (!b) return; armed = armed === b.dataset.id ? null : b.dataset.id; refresh(); };
$('ehero').onchange = e => { heroI = +e.target.value; sel = -1; refresh(); };
$('addhero').onclick = addHero;
$('hname').onchange = e => { const v = e.target.value.trim(); if (v) heroEdit(`Renamed hero to ${v}`, h => { h.Name = v; }); else refresh(); };
$('hclass').onchange = e => { const v = e.target.value.trim() || null; heroEdit(`Class of ${doc().Heroes[heroI].Name} is now ${v || 'empty'}`, h => { h.Class = v; }); };
$('hstatus').onchange = e => heroEdit(`${doc().Heroes[heroI].Name} is now ${e.target.value}`, h => { h.Status = e.target.value; });
$('hsig').onchange = e => heroEdit(`Signature of ${doc().Heroes[heroI].Name} is now ${e.target.value ? abName(e.target.value) : 'none'}`, h => { h.Signature = e.target.value || null; });
$('hnote').onchange = e => heroEdit(`Note on ${doc().Heroes[heroI].Name}`, h => { const v = e.target.value.trim(); if (v) h.Note = v; else delete h.Note; });
STAT_IDS.forEach(s => { $('rc-' + s).onchange = () => heroEdit(`Recipe of ${doc().Heroes[heroI].Name}`, h => {
  const vals = {}; STAT_IDS.forEach(t => { vals[t] = Math.max(0, Math.min(RECIPE_POINTS, parseInt($('rc-' + t).value, 10) || 0)); });
  const rc = {}; Object.keys(h.Recipe || {}).concat(STAT_IDS).forEach(t => { if (vals[t] > 0 && !(t in rc)) rc[t] = vals[t]; });
  h.Recipe = Object.keys(rc).length ? rc : null;
}); });
$('checks').onclick = e => {
  const b = e.target.closest('.chk'); if (!b) return; const x = issues[+b.dataset.i];
  if (x.hero >= 0) { heroI = x.hero; tool = 'hero'; }
  focus = x.node; if (x.node >= 0) sel = x.node;
  refresh(); document.querySelector('.board').scrollIntoView({ block: 'nearest', behavior: 'smooth' });
};
document.addEventListener('keydown', e => {
  if (!on || /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName)) return;
  if (e.key === 'Escape' && armed) { armed = null; refresh(); }
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') { e.preventDefault(); undoEdit(); }
});
addEventListener('beforeunload', e => { if (on && dirty) { e.preventDefault(); e.returnValue = ''; } });

// For scripted checks and the console: the exact text Save writes, and the page's checks.
window.SkillHexEditor = { dump: () => dump(doc()), validate: () => validate(doc()), place: (id, q, r) => place(id, V.at([q, r])) };
})();
