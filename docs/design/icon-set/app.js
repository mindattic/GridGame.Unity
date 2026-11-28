// Icon set page (GG-T2046): renders window.ICON_SET from data.js. No fetch, so it opens from file://.
(function () {
  const S = window.ICON_SET;
  const el = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; };
  const img = (b64, size, title) => { const i = el('img', 'px'); i.src = 'data:image/png;base64,' + b64; i.width = i.height = size; i.alt = title || ''; if (title) i.title = title; return i; };
  const byName = {};
  S.groups.forEach(g => g.rows.forEach(r => { byName[g.id + '/' + r.concept] = r; }));
  const find = to => to.split('; ').map(h => { const [gid, rest] = h.split(/\/(.+)/); return (gid === 'itemcat' && rest.includes(', ') ? rest.split(', ') : [rest]).map(c => byName[gid + '/' + c]).filter(Boolean); }).flat();

  const roles = [['inline', 32, 'text, prices, chips, badges, feed'], ['button', 48, 'header buttons, battle actions'], ['row', 64, 'menu rows, tiles, settings, markers'], ['nav', 72, 'bottom nav tabs']];
  const sample = [byName['nav/Home'], byName['hub/Crucible'], byName['currency/Gold']];
  roles.forEach(([name, px, where]) => {
    const c = el('div', 'card role');
    c.append(el('h3', null, name + ' ' + px));
    const demo = el('div', 'demo');
    sample.forEach(r => demo.append(img(r.png, px, r.concept)));
    c.append(demo, el('span', 'm', where));
    document.getElementById('roles').append(c);
  });

  const pals = document.getElementById('pals');
  Object.keys(S.newPalettes).concat(Object.keys(S.palettes).filter(k => !S.newPalettes[k])).forEach(k => {
    const p = el('div', 'pal' + (S.newPalettes[k] ? ' new' : ''));
    const sw = el('div', 'sw');
    S.palettes[k].forEach(c => { const i = el('i'); i.style.background = c; sw.append(i); });
    p.append(sw, el('code', null, k));
    pals.append(p);
  });

  const groups = document.getElementById('groups'), sheet = document.getElementById('sheet'), count = document.getElementById('count');
  const cards = [];
  S.groups.forEach(g => {
    const box = el('div', 'group');
    const h = el('header');
    h.append(el('h3', null, g.title), el('span', 'm', g.role + ' \u00b7 ' + g.where));
    const grid = el('div', 'grid');
    g.rows.forEach(r => {
      const row = el('div', 'row');
      const t = el('div', 't');
      t.append(el('b', null, r.concept), el('span', 'mono', r.id), el('span', null, 'today: ' + r.today), el('span', 'mono', r.src + (r.opts ? ' (' + r.opts + ')' : '') + ' \u00b7 ' + r.palette));
      row.append(img(r.png, 64, r.concept), t);
      grid.append(row);
      const thumb = img(r.png, 40, g.title + ': ' + r.concept);
      sheet.append(thumb);
      cards.push({ row, thumb, box, text: [g.title, r.concept, r.id, r.src, r.palette, r.today].join(' ').toLowerCase() });
    });
    box.append(h, grid);
    groups.append(box);
  });
  const total = cards.length;
  const filter = q => {
    q = q.trim().toLowerCase();
    let n = 0;
    const shown = new Set();
    cards.forEach(c => { const on = !q || c.text.includes(q); c.row.hidden = c.thumb.hidden = !on; if (on) { n++; shown.add(c.box); } });
    cards.forEach(c => { c.box.hidden = !shown.has(c.box); });
    count.textContent = n + ' of ' + total + ' concepts';
  };
  document.getElementById('q').addEventListener('input', e => filter(e.target.value));
  filter('');

  const em = document.getElementById('emblems');
  S.emblems.forEach(e => {
    const r = byName[e.to];
    const c = el('div', 'em');
    c.append(img(r.png, 32, r.concept), el('span', null, e.kind + ' \u2192 ' + r.concept));
    em.append(c);
  });

  const t = document.getElementById('glyphs');
  const hr = el('tr');
  ['Glyph', 'Used for', 'Where', 'Becomes'].forEach(x => hr.append(el('th', null, x)));
  t.append(hr);
  S.glyphs.forEach(g => {
    const tr = el('tr');
    const a = el('td'); a.append(el('span', 'glyph', g.glyph), el('div', 'mono m', g.code));
    const to = el('td'); const w = el('div', 'to');
    find(g.to).forEach(r => w.append(img(r.png, 32, r.concept), el('span', null, r.concept)));
    to.append(w);
    tr.append(a, el('td', null, g.use), el('td', 'mono', g.where), to);
    t.append(tr);
  });
  document.getElementById('keep').textContent = 'Kept as type, not icons: ' + S.keep.map(k => k.code + ' (' + k.why + ')').join('; ') + '.';

  const board = document.getElementById('board');
  S.board.forEach(b => {
    const c = el('div', 'em');
    c.append(el('span', 'glyph', b.glyph), img(b.png, 32, b.src), el('span', null, b.ids + ' \u00b7 ' + b.src));
    board.append(c);
  });
})();