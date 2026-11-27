// Tutorial drills (GG-T1315): five drill stages that carry the 18 coach-mark lessons of BattleScreen.Tutorial.cs.
// Coordinates are board tiles (X 0-5 left to right, Y 0-7 top to bottom); heroes stand on the default starts
// (BattleRunner.Setup DefaultHeroStarts) in starting-roster order: Tank (1,6), Striker (4,6), Healer (2,7).
"use strict";

const HEROES = [
  { role: "Tank", x: 1, y: 6 },
  { role: "Striker", x: 4, y: 6 },
  { role: "Healer", x: 2, y: 7 },
];

const FOES = {
  slime00: "Slime", slime01: "Slime", slime02: "Slime (glob)", bat00: "Bat", wolf00: "Wolf",
  goblinthug00: "Goblin Thug", soldier00: "Soldier", ganger00: "Ganger",
};

// Today's order on The Tithe Moors I (Lessons[] in BattleScreen.Tutorial.cs) with its wave gate.
const TODAY = [
  ["move", "Moving", 0], ["pincer", "Pincers", 0], ["read", "Tap to read", 0], ["shove", "Shoving", 0],
  ["hold", "The hold timer", 0], ["orbs", "Mana orbs", 0], ["mana", "Mana", 0], ["chain", "Chains", 1],
  ["support", "Supporters", 1], ["spell", "Spells", 2], ["buffs", "Buffs & debuffs", 2], ["timeline", "The timeline", 2],
  ["taunt", "Roles & Taunt", 3], ["guard", "Guard", 3], ["pickup", "Pickups", 3], ["interrupt", "Interrupting", 4],
  ["plate", "Armor plates", 4], ["ready", "Ready", 4],
];

const DRILLS = [
  {
    id: "tutorial-01", name: "First Steps", seed: 13151,
    pitch: "Two slimes, one at a time. Learn to drag a hero, read a foe's card, then trap it between two heroes. Slimes are Sluggish: they rest every other turn, so nothing here punishes a slow first drag.",
    lessons: [
      { id: "move", wave: 1, why: "The bot's best move, on the drag rail. Any move completes it." },
      { id: "read", wave: 1, why: "Tap the slime: its card opens. Comes before the pincer so the player knows what they are trapping." },
      { id: "pincer", wave: 1, why: "Hero, Slime, Hero in a line. The pincer scene walks a slime on where one move makes the pincer if none is there." },
    ],
    waves: [
      [{ c: "slime00", x: 2, y: 4 }],
      [{ c: "slime00", x: 1, y: 3 }, { c: "slime01", x: 4, y: 3 }],
    ],
    blocks: [],
    scenes: [{ id: "pincer", c: "slime00", x: 3, y: 5 }],
    win: "Clear both waves.",
  },
  {
    id: "tutorial-02", name: "The Line", seed: 13152,
    pitch: "The board is a set of levers. Shove a foe into line, close two pincers with one drag, and see a third hero join in. Two rocks show the player that some tiles never move.",
    lessons: [
      { id: "shove", wave: 1, why: "Drag through a slime so it slides into the gap between two heroes. Scene: a slime walks on above the gap." },
      { id: "chain", wave: 2, why: "Two slimes line up between hero pairs; one drop closes both. Scene: up to two slimes walk into the gaps." },
      { id: "support", wave: 2, why: "A pincer with the third hero touching an attacker: blue link, combo-meter points. Scene: a slime walks on where that pincer exists." },
    ],
    waves: [
      [{ c: "slime00", x: 3, y: 4 }],
      [{ c: "slime00", x: 1, y: 3 }, { c: "slime01", x: 3, y: 3 }, { c: "slime00", x: 4, y: 2 }],
    ],
    blocks: [{ x: 0, y: 2 }, { x: 5, y: 2 }],
    scenes: [{ id: "shove", c: "slime00", x: 2, y: 5 }, { id: "chain", c: "slime00", x: 3, y: 6 }, { id: "support", c: "slime00", x: 2, y: 4 }],
    win: "Clear both waves.",
  },
  {
    id: "tutorial-03", name: "Mana", seed: 13153,
    pitch: "Pincers fill the orbs, orbs pay for spells. The bat drains life and the glob slime charges a shot, so the Healer has a hurt ally to mend by the time its spell is ready.",
    lessons: [
      { id: "orbs", wave: 1, why: "The orb row, then the first orb. Done on the first orb minted." },
      { id: "mana", wave: 1, why: "The globe fills from pincers; 100 makes an orb." },
      { id: "spell", wave: 2, role: "Healer", why: "Healer, Heal, the hurt ally, Cast. Scene: the bank tops up to the cheapest spell's cost." },
      { id: "buffs", wave: 2, why: "Pips on a token. Scene: the Tank is shielded (Protection) unless someone already wears a pip." },
    ],
    waves: [
      [{ c: "slime00", x: 2, y: 3 }, { c: "slime00", x: 3, y: 3 }],
      [{ c: "bat00", x: 1, y: 2 }, { c: "slime02", x: 4, y: 2 }],
    ],
    blocks: [],
    scenes: [],
    win: "Clear both waves.",
  },
  {
    id: "tutorial-04", name: "The Clock", seed: 13154,
    pitch: "Time moves only when you act. Lift and cancel for free, read the timeline, guard through a gap too short to use, and break a wolf's Howl with a pincer before it lands.",
    lessons: [
      { id: "hold", wave: 1, why: "Lift a hero and drop it off the board: the clock is real, the cancel is free." },
      { id: "timeline", wave: 1, why: "The next-actor diamond, the bar, the same foe on the board. Done on the first pushback." },
      { id: "guard", wave: 1, why: "Tap GUARD. The bat is fast, so a short gap before its turn comes up early." },
      { id: "interrupt", wave: 2, why: "Pincer the wolf while it howls. Scene: the wolf starts its charge where a pincer reaches it." },
    ],
    waves: [
      [{ c: "slime00", x: 1, y: 3 }, { c: "bat00", x: 4, y: 3 }],
      [{ c: "wolf00", x: 2, y: 2 }, { c: "slime01", x: 4, y: 3 }],
    ],
    blocks: [],
    scenes: [{ id: "interrupt", c: "wolf00", x: 3, y: 4 }],
    win: "Clear both waves.",
  },
  {
    id: "tutorial-05", name: "Roles", seed: 13155,
    pitch: "The graduation fight. The Tank taunts a goblin off the Healer, a fallen foe drops a tonic, a soldier's plate shows where not to strike, and the last wave is the player's own.",
    lessons: [
      { id: "taunt", wave: 1, role: "Tank", why: "Tank, Taunt, Go. Scene first: a goblin walks on two tiles from the Healer." },
      { id: "pickup", wave: 1, why: "Walk onto the tonic before its ring runs out. Scene: a potion lies where a hero's move ends." },
      { id: "plate", wave: 2, why: "The soldier's thick edge. Scene: a soldier walks on if no plate is whole." },
      { id: "ready", wave: 3, why: "The whole toolkit in one line, then the box on the board: clear the last wave." },
    ],
    waves: [
      [{ c: "goblinthug00", x: 2, y: 3 }, { c: "slime00", x: 4, y: 3 }],
      [{ c: "soldier00", x: 3, y: 2 }, { c: "wolf00", x: 1, y: 3 }],
      [{ c: "goblinthug00", x: 1, y: 2 }, { c: "ganger00", x: 4, y: 2 }, { c: "slime02", x: 2, y: 1 }],
    ],
    blocks: [{ x: 0, y: 4 }],
    scenes: [{ id: "taunt", c: "goblinthug00", x: 2, y: 5 }, { id: "pickup", pickup: "pk_potion", x: 3, y: 6 }, { id: "plate", c: "soldier00", x: 4, y: 4 }],
    win: "Clear all three waves. Graduating opens The Tithe Moors I.",
  },
];

const RULES = [
  ["Where they live.", " A new Tutorial list in campaign.json names the five stage ids; the stages themselves sit in stages.json like any other. Campaign order (CampaignStages), HighestClearedStage, CampUnlocks milestones, chapter crawls and the Codex never see them."],
  ["When they run.", " On a new profile, after the opening crawl and before The Tithe Moors I. Each drill ends on its results card with Next drill; drill 5 ends with To the Moors. A drill can be skipped from its first popup; Skip tutorial skips every drill left and sets TutorialDone."],
  ["One fight each.", " Two or three waves of level-1 foes, every placement fixed, a fixed seed per drill (StageDef.Seed, replacing the BattleScreen.TutorialSeed constant), no wanderer, no surprise speaker."],
  ["Lessons are stage data.", " A drill lists its lessons in order (StageDef.Lessons, each with the wave it waits for). The lesson code is unchanged; its Wave gate moves from the lesson to the drill. The popup reads Drill 2 · Lesson 1 of 3."],
  ["Roles, not classes.", " Lessons focus on a role (FocusRole: Tank, Healer), not a class id, so the Commander premise can retire the Warden lead without touching the drills. Heroes stand on the default starts in roster order."],
  ["Scenes stay.", " Each drill carries the StageDef.Scenes its lessons need. A scene foe is a prop at half HP, as today. The last wave waits for an unfinished lesson's scene before it can end."],
  ["Rewards.", " A drill pays no XP, no gold and no loot, and wears no durability, so the party reaches The Tithe Moors I exactly as a skipping player does. Bestiary defeats still count (the player met those foes)."],
  ["Defeat costs nothing.", " Losing a drill shows Retry drill (same seed, from wave 1) and Skip drill. No gold, XP or durability is lost, and no defeat is recorded."],
  ["Replay.", " Settings › How to play › Replay tutorials clears the lesson flags as today and lists the five drills to replay one by one."],
  ["The Tithe Moors I.", " Drops its Scenes and its tutorial flag: it becomes the first real stage, on a random seed, with the First-battles hint still running."],
];

const QUESTIONS = [
  "Should drills pay a small first-clear gold gift? Today the plan is none, so a skipping player is not behind.",
  "Does the hold-timer lesson belong in drill 1? Every drill-1 drag is coached and never forced by the timer, so it can wait for The Clock, but a free move in drill 1 already shows the bar.",
  "A smaller board was considered. Cols and Rows are fixed constants (BattleConfig, 6 x 8), so a drill board would need new Core support; fixed placements on the normal board keep one layout from drill 1 to the Moors. Rocks (Blocks) are used only as props in drills 2 and 5.",
  "The ticket counts 17 lessons; the code has 18 since GG-T731 added Mana orbs. All 18 are mapped here.",
];

const $ = (id) => document.getElementById(id);
let drill = 0, wave = 0;

function lessonTitle(id) { return (TODAY.find((t) => t[0] === id) || [id, id])[1]; }

function renderTabs() {
  $("tabs").innerHTML = "";
  DRILLS.forEach((d, i) => {
    const b = document.createElement("button");
    b.setAttribute("role", "tab");
    b.setAttribute("aria-selected", String(i === drill));
    b.innerHTML = `<b>0${i + 1}</b>${d.name}`;
    b.onclick = () => { drill = i; wave = 0; render(); };
    $("tabs").appendChild(b);
  });
}

function renderWaves() {
  const d = DRILLS[drill];
  $("waves").innerHTML = "";
  d.waves.forEach((_, i) => {
    const b = document.createElement("button");
    b.setAttribute("role", "tab");
    b.setAttribute("aria-selected", String(i === wave));
    b.textContent = `Wave ${i + 1}`;
    b.onclick = () => { wave = i; render(); };
    $("waves").appendChild(b);
  });
}

function renderBoard() {
  const d = DRILLS[drill];
  const at = {};
  const put = (x, y, html) => { at[`${x},${y}`] = html; };
  d.blocks.forEach((b) => put(b.x, b.y, `<div class="tok block">rock</div>`));
  const waveLessons = d.lessons.filter((l) => l.wave === wave + 1).map((l) => l.id);
  d.scenes.filter((s) => waveLessons.includes(s.id)).forEach((s) =>
    put(s.x, s.y, `<div class="tok ghost" title="Scene prop for ${lessonTitle(s.id)}">${s.pickup ? "tonic" : FOES[s.c]}</div>`));
  HEROES.forEach((h) => put(h.x, h.y, `<div class="tok hero ${h.role}">${h.role}</div>`));
  d.waves[wave].forEach((f) => put(f.x, f.y, `<div class="tok foe" title="${f.c} L1">${FOES[f.c]}</div>`));
  let html = "";
  for (let y = 0; y < 8; y++)
    for (let x = 0; x < 6; x++)
      html += `<div class="cell${y < 5 ? " eh" : ""}"><span class="xy">${x},${y}</span>${at[`${x},${y}`] || ""}</div>`;
  $("board").innerHTML = html;
}

function renderPanel() {
  const d = DRILLS[drill];
  $("dkicker").textContent = `Drill ${drill + 1} of 5 · ${d.id}`;
  $("dtitle").textContent = d.name;
  $("dpitch").textContent = d.pitch;
  $("lessons").innerHTML = d.lessons.map((l) =>
    `<li><b>${lessonTitle(l.id)}</b><span class="id">${l.id} · wave ${l.wave}</span>${l.role ? `<span class="role ${l.role}">${l.role}</span>` : ""}<p class="m">${l.why}</p></li>`).join("");
  const foes = d.waves.map((w, i) => `W${i + 1}: ` + w.map((f) => `${f.c} L1 (${f.x},${f.y})`).join(", ")).join("<br>");
  const scenes = d.scenes.length ? d.scenes.map((s) => s.pickup ? `${s.id}: ${s.pickup}` : `${s.id}: ${s.c} L1`).join(", ")
    : "none (spell and buffs scenes need no foe)";
  const facts = [
    ["Stage id", d.id], ["Board", "6 x 8, fixed placements"], ["Seed", d.seed], ["Waves", foes],
    ["Rocks", d.blocks.length ? d.blocks.map((b) => `(${b.x},${b.y})`).join(" ") : "none"],
    ["Scenes", scenes], ["Win", d.win], ["Defeat", "Retry drill or Skip drill; nothing lost"], ["Rewards", "none (no XP, gold, loot or wear)"],
  ];
  $("facts").innerHTML = facts.map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`).join("");
}

function renderMap() {
  const where = {};
  DRILLS.forEach((d, i) => d.lessons.forEach((l, j) => { where[l.id] = { i, j, n: d.lessons.length, wave: l.wave, role: l.role }; }));
  let html = "<tr><th>#</th><th>Lesson</th><th>Today (Tithe Moors I)</th><th>Drill</th><th>Place</th></tr>";
  TODAY.forEach(([id, title, w], k) => {
    const p = where[id];
    html += `<tr class="${p.i === drill ? "sel" : ""}"><td class="n">${k + 1}</td><td>${title} <span class="m">(${id})</span></td><td>wave ${w + 1}</td>` +
      `<td>${p.i + 1} · ${DRILLS[p.i].name}</td><td>lesson ${p.j + 1} of ${p.n}, wave ${p.wave}${p.role ? ", " + p.role : ""}</td></tr>`;
  });
  $("map").innerHTML = html;
}

function renderStatic() {
  $("rules").innerHTML = RULES.map(([b, t]) => `<li><b>${b}</b><span>${t}</span></li>`).join("");
  $("questions").innerHTML = QUESTIONS.map((q) => `<li>${q}</li>`).join("");
  $("json").textContent = [
    "// campaign.json",
    '"Tutorial": ["tutorial-01", "tutorial-02", "tutorial-03", "tutorial-04", "tutorial-05"],',
    "",
    "// stages.json (new fields: Seed, Lessons; FocusRole lives on the lesson)",
    "{",
    '  "Id": "tutorial-02", "Name": "The Line", "Theme": "greenvalley",',
    '  "Seed": 13152,',
    '  "Lessons": [ { "Id": "shove", "Wave": 0 }, { "Id": "chain", "Wave": 1 }, { "Id": "support", "Wave": 1 } ],',
    '  "Blocks": [ { "X": 0, "Y": 2 }, { "X": 5, "Y": 2 } ],',
    '  "Scenes": [ { "Id": "shove", "Class": "slime00", "Level": 1 }, ... ],',
    '  "Waves": [ { "Enemies": [ { "Class": "slime00", "Level": 1, "X": 3, "Y": 4 } ] }, ... ]',
    "}",
  ].join("\n");
}

function render() { renderTabs(); renderWaves(); renderBoard(); renderPanel(); renderMap(); }

renderStatic();
render();
