using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// GG-T542: the guided first battle's set pieces. Each coach-mark lesson points at something on the board (a two-pincer
    /// chain, a foe charging a cast, a plated foe ...); when the lesson opens and its situation isn't there, the battle
    /// screen sends a <see cref="TutorialSceneCommand"/> and the situation is set up here: a foe from the stage's
    /// <see cref="StageDef.Scenes"/> walks on at the one tile that makes the lesson's move possible (found by previewing the
    /// party's moves against every free tile), a charger starts its charge, an orb or a pickup appears. Only a tutorial
    /// battle accepts it; it takes no time and no hero's turn, and a scene whose situation is already on the board does
    /// nothing. Deterministic like every other command.
    /// </summary>
    internal sealed partial class BattleRunner
    {
        /// <summary>The longest route (tiles) a staged move may ask for.</summary>
        private const int SceneRoute = 6;

        private void DoScene(TutorialSceneCommand cmd)
        {
            if (!S.Tutorial || PreviewOnly) { Emit(new AbilityRefusedEvent { CasterId = -1, Reason = "Not a tutorial battle" }); return; }
            switch (cmd.Scene)
            {
                case "pincer": StageOne("pincer", (s, h, pv, id) => pv.Pincers.Exists(p => p.Opponents.Exists(o => o.Id == id))); break;
                case "shove": StageOne("shove", (s, h, pv, id) => ShovePreview.Of(s, h, pv) is ShovePreview sp && pv.Pincers.Exists(p => p.Opponents.Exists(o => o.Id == id) && sp.CompletedByShove(p))); break;
                case "support": StageOne("support", (s, h, pv, id) => pv.Pincers.Exists(p => p.Opponents.Exists(o => o.Id == id) && p.SupportersA.Count + p.SupportersB.Count > 0)); break;
                case "chain": StageChain(); break;
                case "interrupt": StageInterrupt(); break;
                case "pickup": StagePickup(); break;
                case "plate": StagePlate(); break;
                case "taunt": StageTaunt(); break;
                case "spell": StageOrbs(); break;
                case "buffs": StageBuff(); break;
                default: Emit(new AbilityRefusedEvent { CasterId = -1, Reason = "No such scene" }); break;
            }
        }

        private TutorialSceneDef SceneDef(string id) => S.Stage?.Scenes?.Find(x => x.Id == id);

        private int SceneLevel(TutorialSceneDef sc) =>
            S.Stage.IsTest ? sc.Level : Math.Max(sc.Level, S.RecommendedLevel) + S.NewGamePlus * BattleConfig.NgPlusLevelStep + DifficultyTable.At(S.Difficulty).EnemyLevelOffset;

        /// <summary>A scene's foe walks on at <paramref name="at"/>, played like any reinforcement. It is a lesson's prop, not
        /// a new threat: it arrives at half its HP, so the set pieces don't tip the first battle against the party.</summary>
        private Unit SceneSpawn(ClassDef def, int level, Coord at)
        {
            var u = SpawnEnemy(def, level, at.X, at.Y);
            if (u == null) return null;
            u.Hp = Math.Max(1, u.MaxHp / 2);
            var arrived = new ReinforcementsArrivedEvent { Wave = S.Wave };
            arrived.Spawned.Add(u.Id);
            arrived.SpawnedAt.Add(UnitFootprint.Of(u));
            Emit(arrived);
            return u;
        }

        /// <summary>Every move a lesson may point at: each hero that can move now, an L-shaped route to any tile up to
        /// <see cref="SceneRoute"/> away, shortest first (the battle screen's own search, FindMove, makes the same routes).</summary>
        private static List<(int hero, List<Coord> path)> SceneMoves(BattleState s)
        {
            var heroes = new List<Unit>();
            foreach (var u in s.Units) if (u.IsCommandable && u.Alive && u.OnBoard && BattleEngine.CanMove(s, u.Id)) heroes.Add(u);
            var moves = new List<(int, List<Coord>)>();
            for (int d = 1; d <= SceneRoute; d++)
                foreach (var h in heroes)
                    for (int y = 0; y < BattleConfig.Rows; y++)
                        for (int x = 0; x < BattleConfig.Cols; x++)
                        {
                            var to = new Coord(x, y);
                            if (to.Manhattan(h.Pos) != d || !s.Walkable(to)) continue;
                            moves.Add((h.Id, LPath(h.Pos, to, true)));
                            if (to.X != h.Pos.X && to.Y != h.Pos.Y) moves.Add((h.Id, LPath(h.Pos, to, false)));
                        }
            return moves;
        }

        private static List<Coord> LPath(Coord from, Coord to, bool xFirst)
        {
            var p = new List<Coord>();
            var c = from;
            void Walk(bool horizontal)
            {
                while (horizontal ? c.X != to.X : c.Y != to.Y)
                {
                    c = horizontal ? new Coord(c.X + Math.Sign(to.X - c.X), c.Y) : new Coord(c.X, c.Y + Math.Sign(to.Y - c.Y));
                    p.Add(c);
                }
            }
            Walk(xFirst); Walk(!xFirst);
            return p;
        }

        /// <summary>The first move (shortest) whose whole route walks and whose preview passes <paramref name="want"/>.</summary>
        private static (int hero, List<Coord> path)? FindSceneMove(BattleState s, Func<int, MovePreview, bool> want)
        {
            foreach (var m in SceneMoves(s))
            {
                var pv = BattleEngine.PreviewMove(s, m.hero, m.path);
                if (pv.StepsTaken.Count == m.path.Count && want(m.hero, pv)) return m;
            }
            return null;
        }

        /// <summary>Free tiles a scene foe may appear on, nearest the party first.</summary>
        private List<Coord> SceneTiles(BattleState s)
        {
            var heroes = s.LivingList(Team.Hero);
            var tiles = new List<(Coord c, int d)>();
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    if (!s.FreeForSpawn(c) || s.TrapAt(c) != null) continue;
                    int d = int.MaxValue;
                    foreach (var h in heroes) d = Math.Min(d, h.Pos.Manhattan(c));
                    tiles.Add((c, d));
                }
            tiles.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : (a.c.Y != b.c.Y ? a.c.Y.CompareTo(b.c.Y) : a.c.X.CompareTo(b.c.X)));
            return tiles.ConvertAll(t => t.c);
        }

        /// <summary>One scene foe: the nearest free tile where, with that foe standing there, some move passes
        /// <paramref name="want"/> (state, hero, preview, the new foe's id). False when the board allows none.</summary>
        private Unit StageOne(string id, Func<BattleState, int, MovePreview, int, bool> want)
        {
            var sc = SceneDef(id);
            var def = C.TryClass(sc?.Class);
            if (def == null) return null;
            int level = SceneLevel(sc);
            foreach (var t in SceneTiles(S))
            {
                var copy = S.Clone();
                var ghost = new BattleRunner(copy, new List<BattleEvent>()) { PreviewOnly = true }.SpawnEnemy(def, level, t.X, t.Y);
                if (ghost == null || ghost.Pos != t) continue;
                int gid = ghost.Id;
                if (FindSceneMove(copy, (h, pv) => want(copy, h, pv, gid)) == null) continue;
                return SceneSpawn(def, level, t);
            }
            return null;
        }

        // ── Chains: one drop closes two pincers ──────────────────────────────────────────

        private void StageChain()
        {
            if (FindSceneMove(S, (h, pv) => pv.Pincers.Count >= 2) != null) return;
            var sc = SceneDef("chain");
            var def = C.TryClass(sc?.Class);
            if (def == null) return;
            int level = SceneLevel(sc);
            if (ChainSpots(S, def, level, out var spots))
            {
                foreach (var t in spots) SceneSpawn(def, level, t);
                return;
            }
            // The party stands too tight for any chain: one hero steps aside (a short blink) to make room, then the foes.
            foreach (var x in S.LivingList(Team.Hero))
            {
                if (!x.OnBoard || x.IsMultiTile) continue;
                foreach (var t in SceneTiles(S))
                {
                    if (t.Manhattan(x.Pos) > 3) continue;
                    var copy = S.Clone();
                    copy.UnitById(x.Id).Pos = t;
                    if (!ChainSpots(copy, def, level, out spots)) continue;
                    var from = x.Pos;
                    if (!Relocate(x, t)) return;
                    Emit(new TeleportedEvent { UnitId = x.Id, From = from, To = t });
                    foreach (var c in spots) SceneSpawn(def, level, c);
                    return;
                }
            }
        }

        /// <summary>Where up to two foes must stand so a move's drop leaves two pincers on the board: every hero pair that
        /// lines up after the move, with its gap filled by foes already there or new ones on free tiles off the route.</summary>
        private bool ChainSpots(BattleState s, ClassDef def, int level, out List<Coord> spots)
        {
            spots = null;
            foreach (var m in SceneMoves(s))
            {
                var pv = BattleEngine.PreviewMove(s, m.hero, m.path);
                if (pv.StepsTaken.Count != m.path.Count) continue;
                var start = s.UnitById(m.hero).Pos;
                var route = new HashSet<Coord>(m.path) { start };
                // Who stands where once the move is over.
                var at = new Dictionary<Coord, Unit>();
                foreach (var u in s.Units)
                {
                    if (!u.Alive || !u.OnBoard || !pv.Positions.TryGetValue(u.Id, out var p)) continue;
                    for (int dy = 0; dy < u.H; dy++)
                        for (int dx = 0; dx < u.W; dx++) at[new Coord(p.X + dx, p.Y + dy)] = u;
                }
                var heroes = new List<(Unit u, Coord p)>();
                foreach (var u in s.Units)
                    if (u.Alive && u.OnBoard && u.Team == Team.Hero && !u.IsMultiTile && u.CanPincer && pv.Positions.TryGetValue(u.Id, out var p)) heroes.Add((u, p));
                var pairs = new List<(List<Coord> need, bool mover)>();
                for (int i = 0; i < heroes.Count; i++)
                    for (int j = i + 1; j < heroes.Count; j++)
                    {
                        var a = heroes[i].p; var b = heroes[j].p;
                        if ((a.X != b.X && a.Y != b.Y) || a.Manhattan(b) < 2) continue;
                        var step = new Coord(Math.Sign(b.X - a.X), Math.Sign(b.Y - a.Y));
                        var need = new List<Coord>();
                        bool ok = true;
                        for (var c = a + step; c != b && ok; c = c + step)
                        {
                            if (at.TryGetValue(c, out var occ)) { ok = occ.Team == Team.Enemy && !occ.IsNeutral; continue; }
                            if (route.Contains(c) || !s.FreeForSpawn(c) || s.TrapAt(c) != null) ok = false;
                            else need.Add(c);
                        }
                        if (ok && need.Count <= 2) pairs.Add((need, heroes[i].u.Id == m.hero || heroes[j].u.Id == m.hero));
                    }
                // Two pairs, at most two new foes between them; pairs the dropped hero closes first.
                var combos = new List<(List<Coord> tiles, int rank)>();
                for (int i = 0; i < pairs.Count; i++)
                    for (int j = i + 1; j < pairs.Count; j++)
                    {
                        var tiles = new List<Coord>(pairs[i].need);
                        foreach (var c in pairs[j].need) if (!tiles.Contains(c)) tiles.Add(c);
                        if (tiles.Count == 0 || tiles.Count > 2) continue;
                        int rank = tiles.Count * 2 + (pairs[i].mover ? 0 : 1) + (pairs[j].mover ? 0 : 1);
                        combos.Add((tiles, rank));
                    }
                combos.Sort((x, y) => x.rank.CompareTo(y.rank));
                foreach (var combo in combos)
                {
                    var copy = s.Clone();
                    var r = new BattleRunner(copy, new List<BattleEvent>()) { PreviewOnly = true };
                    bool placed = true;
                    foreach (var t in combo.tiles) if (r.SpawnEnemy(def, level, t.X, t.Y)?.Pos != t) placed = false;
                    if (!placed) continue;
                    var check = BattleEngine.PreviewMove(copy, m.hero, m.path);
                    if (check.StepsTaken.Count != m.path.Count || check.Pincers.Count < 2) continue;
                    spots = combo.tiles;
                    return true;
                }
            }
            return false;
        }

        // ── Interrupting: a foe charging a cast, and a pincer that reaches it ─────────────

        private void StageInterrupt()
        {
            if (FindSceneMove(S, (h, pv) => pv.Pincers.Exists(p => p.Opponents.Exists(o => S.CastEntryOf(o.Id) != null))) != null) return;
            // A foe that can charge and that a pincer already reaches starts its charge now.
            foreach (var f in S.LivingList(Team.Enemy))
            {
                if (f.IsMultiTile || f.IsSnakeMember || f.IsRemains || f.IsEgg || S.CastEntryOf(f.Id) != null || C.TryAbility(f.Def.ChargeAbility) == null) continue;
                int fid = f.Id;
                if (FindSceneMove(S, (h, pv) => pv.Pincers.Exists(p => p.Opponents.Exists(o => o.Id == fid))) == null) continue;
                SceneCharge(f);
                return;
            }
            // Or one steps (a short blink) to where a pincer reaches it, and starts its charge.
            foreach (var f in S.LivingList(Team.Enemy))
            {
                if (f.IsMultiTile || f.IsSnakeMember || f.IsRemains || f.IsEgg || f.IsImmobile || C.TryAbility(f.Def.ChargeAbility) == null) continue;
                int fid = f.Id;
                foreach (var t in SceneTiles(S))
                {
                    var copy = S.Clone();
                    copy.UnitById(fid).Pos = t;
                    if (copy.CastEntryOf(fid) == null) copy.Timeline.Add(new TimelineEntry { Id = copy.NextEntryId++, Kind = TimelineKind.Cast, OwnerId = fid, IsCharge = true, CasterTeam = Team.Enemy, RemainingTicks = 1, TotalCastTicks = 1 });
                    if (FindSceneMove(copy, (h, pv) => pv.Pincers.Exists(p => p.Opponents.Exists(o => o.Id == fid))) == null) continue;
                    var from = f.Pos;
                    if (!Relocate(f, t)) break;
                    Emit(new TeleportedEvent { UnitId = f.Id, From = from, To = t });
                    ShovePickupsUnder(f, from);
                    if (S.CastEntryOf(fid) == null) SceneCharge(f);
                    return;
                }
            }
            // Otherwise a charger walks on where a pincer reaches it, and starts charging.
            var u = StageOne("interrupt", (s, h, pv, id) => pv.Pincers.Exists(p => p.Opponents.Exists(o => o.Id == id)));
            if (u != null) SceneCharge(u);
        }

        private void SceneCharge(Unit foe)
        {
            var a = C.TryAbility(foe.Def.ChargeAbility);
            if (a == null) return;
            var target = a.Filter == TargetFilter.AllyOnly ? foe : EnemyPlanner.PickTarget(S, foe);
            if (target == null) return;
            foe.Cooldowns.Remove(a.Id);
            QueueCharge(foe, new ChargePlan { Target = target, Ability = a, Shape = ChargeShape.Single });
        }

        // ── Pickups, plates, Taunt, Spells, Buffs ─────────────────────────────────────────

        /// <summary>A pickup lies where a hero's move ends, unless one already does.</summary>
        private void StagePickup()
        {
            if (S.Pickups.Count > 0 && FindSceneMove(S, (h, pv) => pv.Positions.TryGetValue(h, out var end) && S.Pickups.Exists(k => k.At == end)) != null) return;
            var sc = SceneDef("pickup");
            PickupDef def = null;
            if (sc?.Pickup != null) C.Pickups.TryGetValue(sc.Pickup, out def);
            if (def == null) foreach (var pk in C.Pickups.Values) { def = pk; break; }
            if (def == null || S.Pickups.Count >= BattleConfig.MaxPickups) return;
            foreach (var m in SceneMoves(S))
            {
                if (m.path.Count < 2) continue;
                var end = m.path[m.path.Count - 1];
                if (!S.FreeForItem(end)) continue;
                var pv = BattleEngine.PreviewMove(S, m.hero, m.path);
                if (pv.StepsTaken.Count != m.path.Count || !pv.Positions.TryGetValue(m.hero, out var landed) || landed != end) continue;
                var p = new Pickup { Id = S.NextPickupId++, PickupId = def.Id, At = end, ActionsLeft = BattleConfig.PickupLifetimeActions };
                S.Pickups.Add(p);
                Emit(new PickupDroppedEvent { PickupEntryId = p.Id, PickupId = def.Id, At = p.At, FromUnitId = -1 });
                return;
            }
        }

        /// <summary>A plated foe on the board, unless one already stands there with its plate whole.</summary>
        private void StagePlate()
        {
            foreach (var f in S.LivingList(Team.Enemy)) if (f.HasPlate) return;
            var sc = SceneDef("plate");
            var def = C.TryClass(sc?.Class);
            if (def == null) return;
            var tiles = SceneTiles(S);
            // Not touching a hero: the plate reads before anyone swings at it.
            foreach (var t in tiles)
            {
                bool touching = false;
                foreach (var h in S.LivingList(Team.Hero)) if (h.Pos.Manhattan(t) <= 1) touching = true;
                if (touching) continue;
                SceneSpawn(def, SceneLevel(sc), t);
                return;
            }
            if (tiles.Count > 0) SceneSpawn(def, SceneLevel(sc), tiles[0]);
        }

        /// <summary>A moment for Taunt: a foe close to a hero who isn't the Tank (the Cleric first), unless one already is.</summary>
        private void StageTaunt()
        {
            var soft = new List<Unit>();
            foreach (var h in S.LivingList(Team.Hero)) if (h.OnBoard && h.Def.Role != "Tank") soft.Add(h);
            if (soft.Count == 0) return;
            foreach (var f in S.LivingList(Team.Enemy))
                foreach (var h in soft) if (f.DistanceTo(h) <= 2) return;
            var sc = SceneDef("taunt");
            var def = C.TryClass(sc?.Class);
            if (def == null) return;
            soft.Sort((a, b) => (a.Def.Role == "Healer" ? 0 : 1).CompareTo(b.Def.Role == "Healer" ? 0 : 1));
            var near = soft[0];
            foreach (var t in SceneTiles(S))
                if (t.Manhattan(near.Pos) == 2) { SceneSpawn(def, SceneLevel(sc), t); return; }
            foreach (var t in SceneTiles(S))
                if (t.Manhattan(near.Pos) == 1) { SceneSpawn(def, SceneLevel(sc), t); return; }
        }

        /// <summary>Spells: enough orbs in the bank for the cheapest spell on a hero's bar.</summary>
        private void StageOrbs()
        {
            int cost = int.MaxValue;
            foreach (var h in S.LivingList(Team.Hero))
            {
                if (!h.IsCommandable) continue;
                for (int i = 0; i < h.Bar.Count && i < h.UnlockedSlots; i++)
                {
                    var a = C.TryAbility(h.Bar[i].AbilityId);
                    if (a != null && a.Kind == AbilityKind.Spell && a.Cost > 0) cost = Math.Min(cost, a.Cost);
                }
            }
            if (cost == int.MaxValue) return;
            if (S.PartyMana < cost * BattleConfig.ManaPointsPerOrb) GrantOrbs(cost, -1, "lesson");
        }

        /// <summary>Buffs &amp; debuffs: a pip to read: the Tank (or the first hero) is shielded, unless someone already wears one.</summary>
        private void StageBuff()
        {
            foreach (var u in S.Units) if (u.Alive && u.Buffs.Count > 0) return;
            var prot = C.TryBuff(BattleConfig.ShieldBuffId);
            if (prot == null) return;
            Unit pick = null;
            foreach (var h in S.LivingList(Team.Hero)) if (h.OnBoard && (pick == null || h.Def.Role == "Tank")) pick = h;
            if (pick != null) ApplyBuff(pick, prot, prot.Duration);
        }
    }
}
