// Family wheel model: one canonical ring of every hero class, cut into N equal arcs.
// Pure rule: S slots on the rim, N families, S = N * m. Arc j owns slots j*m .. (j+1)*m;
// the class sitting on slot j*m is on the border and belongs to both neighbouring families.
// Works in the browser (window.WheelModel) and in node (module.exports).
(function (root) {
  // Canonical ring, clockwise from the top. Neighbours play alike; Paladin sits between Knight and Cleric.
  const ORDER = [
    'paladin', 'knight', 'shieldmaiden', 'defender', 'tank', 'warden', 'myrmidon', 'warchief',
    'lancer', 'sellsword', 'duelist', 'fencer', 'swordmaster', 'odachi', 'ronin', 'slasher',
    'raider', 'barbarian', 'basher', 'bruiser', 'fighter', 'prizefighter', 'pugilist', 'streetfighter', 'martialartist', 'pandagirl',
    'striker', 'redninja', 'yellowninja', 'greenninja', 'blueninja', 'blackninja', 'chromaninja',
    'assassain', 'nighthunter', 'ripper', 'thief', 'speedster',
    'hexstalker', 'hexsniper', 'hexwarden', 'wildchild', 'bluelion', 'monstertamer',
    'drifter', 'operative', 'engineer', 'machinist', 'technician', 'tinkerer',
    'courier', 'tactician', 'dervish', 'redmage', 'harbinger',
    'ritualist', 'blackwitch', 'vampire', 'darktemplar', 'jadeknight',
    'whitewitch', 'alchemist', 'doctor', 'sage', 'sister', 'monk', 'whiteninja', 'herald', 'cleric'
  ];

  const famOf = (data) => Object.fromEntries(data.classes.map(c => [c.Id, c.Family]));

  // Arc composition and fit for a ring (array of class ids or 'new:k').
  function arcs(ring, N, fam) {
    const S = ring.length, m = S / N, out = [];
    for (let j = 0; j < N; j++) {
      const lo = j * m, hi = (j + 1) * m;
      const interior = [], border = [ring[lo % S], ring[hi % S]];
      for (let i = lo + 1; i < hi; i++) interior.push(ring[i]);
      const cnt = {};
      [...interior, ...border].forEach((id, k) => { const f = fam[id]; if (!f) return; cnt[f] = (cnt[f] || 0) + (k < interior.length ? 1 : 0.5); });
      const ranked = Object.entries(cnt).sort((a, b) => b[1] - a[1]);
      out.push({ j, lo, hi, interior, border, ranked, top: ranked[0] ? ranked[0][0] : null });
    }
    return out;
  }

  // Fit = share of real classes that land in an arc whose leading family is their own
  // (a border class counts when either neighbouring arc leads with its family).
  function fit(ring, N, fam) {
    const A = arcs(ring, N, fam), S = ring.length, m = S / N;
    let ok = 0, real = 0;
    ring.forEach((id, i) => {
      if (!fam[id]) return; real++;
      if (i % m === 0) { const j = i / m; const a = A[j % N], b = A[(j - 1 + N) % N]; if (a.top === fam[id] || b.top === fam[id]) ok++; }
      else { const a = A[Math.floor(i / m)]; if (a.top === fam[id]) ok++; }
    });
    // Each family should lead exactly one arc where it can: penalise a family that leads none.
    const fams = new Set(Object.values(fam)); const led = new Set(A.map(a => a.top));
    return { score: ok / real, led: led.size, fams: fams.size, arcs: A };
  }

  // Smallest change that makes the ring divide into N equal arcs: add k open slots or drop k classes.
  function plans(N, total) {
    const up = (N - total % N) % N, down = total % N;
    return { add: up, trim: down };
  }

  // Greedy: place each open slot (or drop each class) where it raises the fit most. Paladin stays on slot 0.
  function solve(N, mode, data) {
    const fam = famOf(data);
    let ring = ORDER.slice();
    const p = plans(N, ring.length), k = mode === 'trim' ? p.trim : p.add;
    const removed = [];
    for (let step = 0; step < k; step++) {
      let best = null;
      if (mode === 'trim') {
        for (let i = 1; i < ring.length; i++) {
          const r = ring.slice(0, i).concat(ring.slice(i + 1));
          if (r.length % N && step < k - 1) { /* still mid-way: evaluate with padding */ }
          const f = scoreFor(r, N, fam, k - step - 1, 'trim');
          if (!best || f > best.f) best = { f, r, id: ring[i] };
        }
        removed.push(best.id);
      } else {
        for (let i = 1; i <= ring.length; i++) {
          const r = ring.slice(0, i).concat(['new:' + step], ring.slice(i));
          const f = scoreFor(r, N, fam, k - step - 1, 'add');
          if (!best || f > best.f) best = { f, r };
        }
      }
      ring = best.r;
    }
    const F = fit(ring, N, fam);
    return { N, mode, ring, m: ring.length / N, removed, added: ring.filter(x => x.startsWith('new:')).length, ...F };
  }
  // While greedy is mid-way the ring is not divisible yet; score it as if the remaining slots were spread evenly.
  function scoreFor(r, N, fam, left, mode) {
    if (left === 0) return fit(r, N, fam).score;
    const pad = r.slice();
    for (let i = 0; i < left; i++) {
      if (mode === 'add') pad.push('pad:' + i); else pad.pop();
    }
    return fit(pad, N, fam).score;
  }

  const api = { ORDER, arcs, fit, plans, solve, famOf };
  if (typeof module !== 'undefined' && module.exports) module.exports = api; else root.WheelModel = api;
})(typeof window !== 'undefined' ? window : globalThis);
