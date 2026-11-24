using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Core.Rules
{
    public sealed class ChargePlan
    {
        public Unit Target;
        public AbilityDef Ability;
        public ChargeShape Shape;
        public List<Coord> Line;
    }

    /// <summary>Enemy decision-making (bible §14). Pure: reads state, never commits.</summary>
    public static class EnemyPlanner
    {
        /// <summary>The enemy's personality; Auto → Weakest for clever foes (INT ≥ 14), Threat for other sentient foes, else Closest.</summary>
        public static Targeting TargetingOf(Unit enemy)
        {
            var t = enemy.Def.Targeting;
            if (t != Targeting.Auto) return t;
            if (enemy.Def.BaseStats.Intelligence >= 14) return Targeting.Weakest;
            return enemy.CanPincer ? Targeting.Threat : Targeting.Closest;
        }

        public static string Describe(Targeting t)
        {
            switch (t)
            {
                case Targeting.Weakest: return "Hunts the weakest hero";
                case Targeting.Threat: return "Goes after your hardest hitter";
                case Targeting.Squishy: return "Targets the most fragile hero";
                case Targeting.Erratic: return "Attacks at random";
                case Targeting.Caster: return "Hunts your spellcasters";
                default: return "Attacks whoever is closest";
            }
        }

        /// <summary>The hero this enemy wants, among all living heroes.</summary>
        public static Unit PickTarget(BattleState s, Unit enemy) => PickFrom(s, enemy, s.LivingList(Team.Hero));

        /// <summary>
        /// The hero this enemy wants among <paramref name="heroes"/>, by its personality. Ties fall back to
        /// distance, then lower HP, then id, so every choice is deterministic. Erratic picks a hero that
        /// stays fixed for the whole turn (hash of clock and id) without consuming the battle RNG.
        /// </summary>
        public static Unit PickFrom(BattleState s, Unit enemy, List<Unit> heroes)
        {
            if (heroes == null || heroes.Count == 0) return null;
            // Taunt (§14.1.1): a taunting hero in reach overrides every personality.
            var taunters = heroes.FindAll(h => h.IsTaunting);
            if (taunters.Count > 0) heroes = taunters;
            var mode = TargetingOf(enemy);
            if (mode == Targeting.Erratic)
            {
                var sorted = new List<Unit>(heroes);
                sorted.Sort((x, y) => x.Id.CompareTo(y.Id));
                int h = (int)((s.Clock * 31 + enemy.Id * 17) & 0x7fffffff);
                return sorted[h % sorted.Count];
            }
            if (mode == Targeting.Threat)
            {
                bool anyDamage = false;
                foreach (var hero in heroes) if (hero.DamageDealt > 0) anyDamage = true;
                if (!anyDamage) mode = Targeting.Closest; // nobody has hurt it yet
            }
            Unit best = null;
            foreach (var hero in heroes)
                if (best == null || Better(enemy, mode, hero, best)) best = hero;
            return best;
        }

        private static bool Better(Unit enemy, Targeting mode, Unit a, Unit b)
        {
            float ka = Key(enemy, mode, a), kb = Key(enemy, mode, b);
            if (ka != kb) return ka < kb;
            int da = enemy.DistanceTo(a), db = enemy.DistanceTo(b);
            if (da != db) return da < db;
            if (a.Hp != b.Hp) return a.Hp < b.Hp;
            return a.Id < b.Id;
        }

        private static float Key(Unit enemy, Targeting mode, Unit h)
        {
            switch (mode)
            {
                case Targeting.Weakest: return h.Hp;
                case Targeting.Threat: return -h.DamageDealt;
                case Targeting.Squishy: return h.Effective.Vitality * 1000f + h.MaxHp;
                case Targeting.Caster: return -(h.Effective.Intelligence + h.Effective.Wisdom);
                default: return enemy.DistanceTo(h);
            }
        }

        /// <summary>The brain in a few words (§14.6) — shown on the enemy card and in the bestiary.</summary>
        public static string DescribeBrain(Brain b)
        {
            switch (b)
            {
                case Brain.Pack: return "Hunts in packs, keeping close to its kin";
                case Brain.Ambusher: return "Lies in wait until a hero comes within 2 tiles";
                case Brain.Skirmisher: return "Strikes, then darts away";
                case Brain.Kiter: return "Keeps its distance to cast or shoot";
                case Brain.Guardian: return "Guards its most valuable ally";
                case Brain.Berserker: return "Charges headlong, fearing no flank";
                case Brain.Scavenger: return "Picks off the wounded and flees when hurt";
                case Brain.Trapper: return "Hangs back to mine the approach";
                case Brain.Sluggish: return "Moves only every other turn";
                case Brain.Hopper: return "Leaps two tiles at a time, over anything";
                case Brain.Blinker: return "Blinks beside its prey every other turn";
                case Brain.Burrower: return "Tunnels under the board and surfaces beside its target";
                case Brain.Bomber: return "Dives into crowds of heroes";
                case Brain.Medic: return "Stays by its wounded allies";
                case Brain.Commander: return "Directs from behind its line";
                case Brain.Duelist: return "Seeks one-on-one fights, avoids being outnumbered";
                case Brain.Stalker: return "Slips past the front line to reach your casters";
                case Brain.Sentinel: return "Never moves";
                case Brain.Relentless: return "Never retreats, whatever its wounds";
                default: return "Closes in on its prey";
            }
        }

        private static bool Evasive(Brain b) => b == Brain.Commander || b == Brain.Trapper || b == Brain.Kiter || b == Brain.Medic;

        /// <summary>
        /// Last stand: an evasive brain (Commander, Trapper, Kiter, Medic) with no fighting ally left stops hanging back and
        /// charges — the battle can't stall on a foe that only runs (found in the battle records, §22.6). The same holds
        /// against a lone hero (GG-T123): one hero can't pincer, so nothing on the board has anything to keep away from, nor any reason to lie in wait (Ambusher).
        /// </summary>
        public static Brain EffectiveBrain(BattleState s, Unit e)
        {
            var b = e.Def.Brain;
            if ((Evasive(b) || b == Brain.Ambusher) && LoneHero(s)) return Brain.Berserker; // nothing lies in wait for a lone hero either
            if (!Evasive(b)) return b;
            foreach (var a in s.Living(Team.Enemy))
                if (a != e && !a.IsRemains && !Evasive(a.Def.Brain) && a.Def.Brain != Brain.Sentinel) return b;
            return Brain.Berserker;
        }

        /// <summary>
        /// One hero left standing (§22.10, GG-T123). A pincer needs two heroes, so a lone hero can only hurt foes with its
        /// bar; a foe that fled or hung back from it would stall the battle forever (a wounded Flying Monkey kiting the
        /// last hero round the board was the Crucible soak's endless run). Against a lone hero no foe retreats when
        /// wounded and every evasive brain makes its last stand, so the fight always closes: the hero wins with its
        /// spells, items and skills, or falls.
        /// </summary>
        public static bool LoneHero(BattleState s)
        {
            int n = 0;
            foreach (var h in s.Living(Team.Hero)) if (!h.IsRemains && ++n > 1) return false;
            return n == 1;
        }

        private static float RetreatThreshold(Unit e)
        {
            switch (e.Def.Brain)
            {
                case Brain.Scavenger: return 0.5f;
                case Brain.Berserker: case Brain.Relentless: case Brain.Bomber: case Brain.Sentinel: return -1f;
                default: return BattleConfig.RetreatHpThreshold;
            }
        }

        private static int NearestHeroDistance(BattleState s, Unit e)
        {
            int best = int.MaxValue;
            foreach (var h in s.Living(Team.Hero)) best = Math.Min(best, e.DistanceTo(h));
            return best;
        }

        /// <summary>The ally a Guardian protects: a boss, caster, commander or medic, else the toughest other ally.</summary>
        private static Unit Ward(BattleState s, Unit e)
        {
            Unit best = null; float bestV = float.NegativeInfinity;
            foreach (var a in s.Living(Team.Enemy))
            {
                if (a == e || a.IsRemains || a.Def.Brain == Brain.Guardian) continue;
                float v = a.MaxHp * 0.01f;
                if (a.Def.HasTag("Boss")) v += 10f;
                if (!string.IsNullOrEmpty(a.Def.ChargeAbility)) v += 6f;
                if (a.Def.Brain == Brain.Commander || a.Def.Brain == Brain.Medic) v += 8f;
                if (v > bestV) { bestV = v; best = a; }
            }
            return best;
        }

        /// <summary>A free tile beside <paramref name="target"/> for a blink or a burrow, closest to the enemy's current spot.</summary>
        public static Coord? SurfaceBeside(BattleState s, Unit enemy, Unit target)
        {
            Coord? best = null; int bestD = int.MaxValue;
            foreach (var t in target.Tiles())
                foreach (var d in Coord.Cardinals)
                {
                    var c = t + d;
                    if (!s.FreeForSpawn(c) || s.TrapAt(c) != null) continue;
                    int dd = c.Manhattan(enemy.Pos);
                    if (dd < bestD) { bestD = dd; best = c; }
                }
            return best;
        }

        /// <summary>The tile that best breaks contact: farthest from every hero (Skirmisher after a strike).</summary>
        public static Coord PlanRetreat(BattleState s, Unit enemy)
        {
            if (enemy.IsRooted) return enemy.Pos; // GG-T920: held to its tile
            var best = enemy.Pos; int bestD = NearestHeroDistance(s, enemy);
            var original = enemy.Pos;
            foreach (var d in Coord.Cardinals)
            {
                var c = original + d;
                if (!s.Walkable(c) || s.UnitAt(c) != null) continue;
                enemy.Pos = c;
                int dist = NearestHeroDistance(s, enemy);
                enemy.Pos = original;
                if (dist > bestD) { bestD = dist; best = c; }
            }
            return best;
        }

        /// <summary>The single best next tile for this enemy (its anchor for a multi-tile actor).</summary>
        public static Coord PlanStep(BattleState s, Unit enemy)
        {
            if (enemy.IsImmobile || enemy.IsRooted || enemy.IsSnakeSegment) return enemy.Pos; // GG-T920: Rooted holds it
            var target = PickTarget(s, enemy);
            if (target == null) return enemy.Pos;
            var brain = EffectiveBrain(s, enemy);
            if (brain == Brain.Sentinel) return enemy.Pos;
            if (brain == Brain.Ambusher && NearestHeroDistance(s, enemy) > 2 && enemy.TurnsTaken < BattleConfig.AmbushPatienceTurns) return enemy.Pos;
            if (brain == Brain.Sluggish && enemy.TurnsTaken % 2 == 0) return enemy.Pos;

            var candidates = new List<Coord> { enemy.Pos };
            foreach (var d in Coord.Cardinals)
            {
                var c = enemy.Pos + d;
                if (enemy.IsMultiTile) { if (!FootprintStepLegal(s, enemy, c)) continue; }
                else
                {
                    if (!s.Walkable(c)) continue;
                    var occ = s.UnitAt(c);
                    if (occ != null && occ != enemy) continue;
                }
                if ((s.Region.Rules.Count > 0 || s.Region.Tiles.Count > 0) && RegionBoard.DangerAt(s, c, enemy) > 0f) continue; // GG-T359: foes don't walk into a rule's hazard either (GG-T842: nor a hero-laid one)
                candidates.Add(c);
            }
            if (brain == Brain.Hopper && !enemy.IsMultiTile)
                foreach (var d in Coord.Cardinals)
                {
                    var c = enemy.Pos + d + d; // a leap clears whatever is in between
                    if (!s.Walkable(c) || s.UnitAt(c) != null || s.MovableAt(c) != null) continue;
                    if ((s.Region.Rules.Count > 0 || s.Region.Tiles.Count > 0) && RegionBoard.DangerAt(s, c, enemy) > 0f) continue; // GG-T359: nor leap into one
                    candidates.Add(c);
                }
            var ward = brain == Brain.Guardian ? Ward(s, enemy) : null;
            Unit patient = null;
            if (brain == Brain.Medic)
                foreach (var a in s.Living(Team.Enemy)) if (a != enemy && !a.IsRemains && a.HpFraction < 0.9f && (patient == null || a.HpFraction < patient.HpFraction)) patient = a;

            bool wounded = enemy.HpFraction < RetreatThreshold(enemy) && !LoneHero(s);
            bool seeksPincer = enemy.CanPincer && !enemy.IsMultiTile;
            var original = enemy.Pos;
            Coord best = enemy.Pos; float bestScore = float.NegativeInfinity;
            foreach (var c in candidates)
            {
                enemy.Pos = c;
                float dist = enemy.DistanceTo(target);
                float score = wounded ? dist : -dist;
                if (enemy.IsRanged && !wounded)
                {
                    // Ranged: hold at its reach, prefer a clear firing lane, avoid melee contact.
                    score = -Math.Abs(dist - enemy.Def.AttackRange) * 1.5f;
                    if (RangedTarget(s, enemy) != null) score += 3f;
                    if (enemy.IsAdjacentTo(target)) score -= 2f;
                }
                if (!wounded && enemy.IsAdjacentTo(target)) score += 2f;
                // GG-T306: a fleeing foe never holes up in a corner, the one tile no pincer can reach (no axis has two
                // sides) — a cornered 8-HP phantom outlasted a buff-only party for 3000 actions.
                if (wounded && !enemy.IsMultiTile && IsCorner(c)) score -= BattleConfig.FleeCornerPenalty;
                if (!wounded) score += BrainScore(s, enemy, brain, target, ward, patient);
                if (!enemy.IsMultiTile && brain != Brain.Berserker && brain != Brain.Bomber && WouldBeFlanked(s, c)) score -= 100f;
                if (c == original) score -= 0.5f;
                // Clever foes shove the Power Amplifier away before the party can line up an amplified pincer.
                var mode = TargetingOf(enemy);
                if (!wounded && s.Amplifier.HasValue && c == s.Amplifier.Value && (mode == Targeting.Weakest || mode == Targeting.Threat)) score += 4f;
                if (!wounded && seeksPincer && WouldFormPincer(s, enemy)) score += 50f;
                if (!wounded && WouldSupportAllyPincer(s, enemy)) score += 25f;
                enemy.Pos = original;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            enemy.Pos = original;
            return best;
        }

        /// <summary>
        /// GG-T306 (§14.1.3): a pincer-capable foe (Sentient, or the Pincer keyword) engineers pincers. It asks the shared
        /// <see cref="PincerPlanner"/> for the best walk of up to its MoveRange steps — swapping through allies and heroes
        /// like a hero drag — that completes a pincer now, sets up an end an ally can close next, or joins an ally pincer
        /// as a supporter. Null when it can't plan (mindless, multi-tile, immobile, wounded and retreating) or nothing
        /// is worth it; an evasive brain (Kiter, Commander, Trapper, Medic) only takes a walk that completes a pincer, a
        /// Sentinel never moves, an Ambusher still lying in wait or a Sluggish foe on its rest turn only pincers in place.
        /// GG-T904: of two set-ups it prefers the one on a Rooted hero (<see cref="PincerPlanner.PinnedOpponentBonus"/>) - the
        /// hero a tree golem's Root Grasp pinned can't be dragged out before an ally closes the far end.
        /// </summary>
        public static PincerPlan PlanPincer(BattleState s, Unit e)
        {
            if (e == null || !e.Alive || e.Team != Team.Enemy || !e.CanPincer || e.IsMultiTile || e.IsSnakeMember || e.IsImmobile) return null;
            if (PickTarget(s, e) == null) return null;
            var brain = EffectiveBrain(s, e);
            if (e.HpFraction < RetreatThreshold(e) && !LoneHero(s)) return null; // wounded: it retreats (PlanStep)
            int steps = Math.Max(0, e.Def.MoveRange);
            if (brain == Brain.Sentinel || e.IsRooted) steps = 0; // GG-T920: a rooted foe pincers from where it stands
            if (brain == Brain.Ambusher && NearestHeroDistance(s, e) > 2 && e.TurnsTaken < BattleConfig.AmbushPatienceTurns) steps = 0;
            if (brain == Brain.Sluggish && e.TurnsTaken % 2 == 0) steps = 0;
            var plan = PincerPlanner.Plan(s, e, steps);
            if (plan.Kind == PincerPlanKind.None || plan.Score <= 0f) return null;
            if (plan.Kind != PincerPlanKind.Complete && (Evasive(brain) || !BattleConfig.EnemyPincerSetups)) return null;
            return plan;
        }

        /// <summary>Each brain's taste in tiles, scored with the enemy standing on the candidate tile (§14.6).</summary>
        private static float BrainScore(BattleState s, Unit e, Brain brain, Unit target, Unit ward, Unit patient)
        {
            int heroesAdjacent = 0;
            foreach (var h in s.Living(Team.Hero)) if (e.IsAdjacentTo(h)) heroesAdjacent++;
            int dist = e.DistanceTo(target), near = NearestHeroDistance(s, e);
            switch (brain)
            {
                case Brain.Pack:
                {
                    float sc = 0f;
                    foreach (var a in s.Living(Team.Enemy)) if (a != e && !a.IsRemains && e.IsAdjacentTo(a)) sc += 1.5f;
                    return sc;
                }
                case Brain.Guardian:
                    if (ward == null) return 0f;
                    return -1.4f * e.DistanceTo(ward) + (e.IsAdjacentTo(ward) ? 3f : 0f) + dist * 0.6f; // the ward first, the prey second
                case Brain.Berserker: return -0.8f * dist;
                case Brain.Trapper: return -1.4f * Math.Abs(near - 3) + dist * 1f - (heroesAdjacent > 0 ? 2f : 0f);
                case Brain.Bomber: return 2.5f * heroesAdjacent;
                case Brain.Medic:
                    if (patient == null) return -1.2f * Math.Abs(near - 3) + dist;
                    return -1.6f * Math.Abs(e.DistanceTo(patient) - 1) - 2f * Math.Max(0, 2 - near) + dist;
                case Brain.Commander:
                {
                    float sc = -1.3f * Math.Abs(near - 3) + dist;
                    foreach (var a in s.Living(Team.Enemy))
                        if (a != e && !a.IsRemains && e.IsAdjacentTo(a) && a.DistanceTo(target) < dist) sc += 2f; // an ally stands between it and the heroes
                    return sc;
                }
                case Brain.Duelist: return heroesAdjacent == 1 ? 2.5f : heroesAdjacent >= 2 ? -6f : 0f;
                case Brain.Stalker: return heroesAdjacent > 1 ? -1.5f * (heroesAdjacent - 1) : 0f;
                case Brain.Kiter: return e.Def.AttackRange > 1 ? 0f : -1.4f * Math.Abs(near - 3) + dist - (heroesAdjacent > 0 ? 3f : 0f);
                default: return 0f;
            }
        }

        /// <summary>Caster archetype: telegraph a charge when no hero is adjacent (or a boss phase prefers charging).</summary>
        public static ChargePlan PlanCast(BattleState s, Unit enemy, bool ignoreMeleeRange)
        {
            if (enemy.IsImmobile || enemy.IsSilenced) return null;
            var ability = s.Content.TryAbility(enemy.Def.ChargeAbility);
            if (ability == null) return null;
            if (enemy.Cooldowns.TryGetValue(ability.Id, out var cd) && cd > 0) return null; // recharging: act normally (GG-T283)
            var heroes = s.LivingList(Team.Hero);
            if (heroes.Count == 0) return null;
            if (!ignoreMeleeRange)
                foreach (var h in heroes) if (enemy.IsAdjacentTo(h)) return null;

            // Support charges (howl, rally, mend) aim at allies: the most wounded for heals, else the caster's pack.
            if (ability.Filter == TargetFilter.AllyOnly)
            {
                var allies = s.LivingList(Team.Enemy);
                Unit pick = null;
                if (ability.BaseHeal > 0)
                {
                    foreach (var al in allies) if (!al.IsRemains && al.HpFraction < 0.7f && (pick == null || al.HpFraction < pick.HpFraction)) pick = al;
                    if (pick == null) return null; // nobody needs mending: act normally instead
                }
                else if (ability.ApplyBuff != null)
                {
                    var buff = s.Content.TryBuff(ability.ApplyBuff);
                    bool anyWithout = false;
                    foreach (var al in allies) if (!al.IsRemains && (buff == null || !al.HasBuff(buff.Id))) anyWithout = true;
                    if (!anyWithout) return null;
                    pick = enemy;
                }
                else
                {
                    // A pure rush (Howl): only worth a turn when it hurries another ally that is already at a hero's
                    // throat. A pack spread across the board used to howl turn after turn — each howl hastening only
                    // the howler into its next howl — and never closed in (GG-T123's stalled Crucible soak).
                    bool rushesAStriker = false;
                    foreach (var al in allies)
                    {
                        if (al == enemy || al.IsRemains || Math.Max(Math.Abs(al.Pos.X - enemy.Pos.X), Math.Abs(al.Pos.Y - enemy.Pos.Y)) > ability.Radius) continue;
                        foreach (var h in heroes) if (al.IsAdjacentTo(h) || (al.IsRanged && al.DistanceTo(h) <= al.Def.AttackRange)) rushesAStriker = true;
                    }
                    if (!rushesAStriker) return null;
                    pick = enemy;
                }
                return new ChargePlan { Target = pick, Ability = ability, Shape = ChargeShape.Single };
            }
            // GG-T388: a mocking echo only spends its charge on a hero with a move to turn back on the party.
            if (ability.Echoes)
            {
                var echoable = new List<Unit>();
                foreach (var h in heroes) if (Echo.For(s, enemy, h) != null) echoable.Add(h);
                if (echoable.Count == 0) return null;
                return new ChargePlan { Target = PickFrom(s, enemy, echoable), Ability = ability, Shape = ChargeShape.Single };
            }
            // Tempo drains (Temporal Drag, GG-T275) are timed against the party's tempo: wasted while the party is
            // already behind, so the caster moves or shoots instead and saves the charge for when the party hastes.
            var drain = s.Content.TryBuff(ability.ApplyBuff);
            if (drain != null && drain.Tempo < 0 && s.NetTempo < 0) return null;
            var target = PickFrom(s, enemy, heroes);
            var plan = new ChargePlan { Target = target, Ability = ability, Shape = enemy.Def.ChargeShape };
            if (plan.Shape == ChargeShape.Line)
            {
                if (enemy.IsRooted) return null; // GG-T920: a line charge carries the caster; rooted, it can't
                plan.Line = LineToward(enemy, target, s);
                if (plan.Line.Count == 0) plan.Shape = ChargeShape.Single; // edge-pinned: fall back
            }
            return plan;
        }

        /// <summary>GG-T894: how far (Manhattan) a foe lobs a glob from where it stands.</summary>
        public const int LobRange = 3;
        /// <summary>GG-T894: no foe lobs while this many of its kind (same portrait family) and unhatched globs stand.</summary>
        public const int LobKinCap = 4;

        /// <summary>
        /// GG-T894 (BIBLE §14.23, the slime's glob): a lob is a PickTile, EmptyOnly ability in the foe's class kit with a
        /// MarkTiles rule effect on its Target. It is thrown in place of a step and a strike when no hero is adjacent,
        /// it is off cooldown, and fewer than <see cref="LobKinCap"/> of the foe's kin (living foes of its portrait family)
        /// plus marks of the state it paints are on the board. The tile is a free one (no unit, item, trap, block or
        /// mark) within <see cref="LobRange"/> of the foe, nearest the heroes (then nearest the foe, then row-major), so
        /// the party meets it on the way. Null = no lob this turn. Pure: reads state, never commits.
        /// </summary>
        public static (AbilityDef Ability, Coord At)? PlanLob(BattleState s, Unit enemy)
        {
            if (enemy?.Def?.Abilities == null || enemy.IsImmobile || enemy.IsSilenced || enemy.IsRemains || enemy.IsEgg || enemy.IsMultiTile) return null;
            AbilityDef lob = null; string state = null;
            foreach (var id in enemy.Def.Abilities)
            {
                var a = s.Content.TryAbility(id);
                if (a == null || a.Mode != TargetMode.PickTile || a.Filter != TargetFilter.EmptyOnly) continue;
                foreach (var e in a.RuleEffects)
                    if (e.Kind == RuleEffectKind.MarkTiles && e.Area == RuleArea.Target && e.State != null
                        && s.Content.TryTileState(e.State)?.Circle == null // GG-T902: a rune is no lob
                        && !Buries(s, e.State)) { lob = a; state = e.State; break; } // GG-T900: a buried charge is planted, not lobbed
                if (lob != null) break;
            }
            if (lob == null) return null;
            if (enemy.Cooldowns.TryGetValue(lob.Id, out var cd) && cd > 0) return null;
            var heroes = s.LivingList(Team.Hero);
            if (heroes.Count == 0) return null;
            foreach (var h in heroes) if (enemy.IsAdjacentTo(h)) return null;
            int kin = 0;
            foreach (var u in s.LivingList(Team.Enemy))
                if (!u.IsRemains && !u.IsEgg && !u.IsCorpse && u.Def?.Portrait != null && u.Def.Portrait == enemy.Def.Portrait) kin++;
            foreach (var m in s.Region.Tiles.Values) if (m.State == state) kin++;
            if (kin >= LobKinCap) return null;
            Coord? best = null; int bestHero = int.MaxValue, bestSelf = int.MaxValue;
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    int self = c.Manhattan(enemy.Pos);
                    if (self == 0 || self > LobRange) continue;
                    if (!s.FreeForSpawn(c) || s.TrapAt(c) != null || s.Region.At(c) != null) continue;
                    int near = int.MaxValue;
                    foreach (var h in heroes) foreach (var t in h.Tiles()) near = Math.Min(near, c.Manhattan(t));
                    if (near < bestHero || (near == bestHero && self < bestSelf)) { best = c; bestHero = near; bestSelf = self; }
                }
            return best.HasValue ? (lob, best.Value) : ((AbilityDef, Coord)?)null;
        }

        // GG-T902 (BIBLE §14.17): the Choir's rune circle.

        /// <summary>GG-T902: how far (Manhattan) a singer paints a rune from where it stands (the nearest ring first).</summary>
        public const int RuneRange = 2;

        /// <summary>GG-T902: the rune ability in a class kit, and the circle state it paints: a PickTile, EmptyOnly ability
        /// with a MarkTiles rule effect on its Target whose tile state has a <see cref="TileStateDef.Circle"/>. Null = none.</summary>
        public static AbilityDef RuneAbility(ContentDb c, ClassDef def, out TileStateDef state)
        {
            state = null;
            if (c == null || def?.Abilities == null) return null;
            foreach (var id in def.Abilities)
            {
                var a = c.TryAbility(id);
                if (a == null || a.Mode != TargetMode.PickTile || a.Filter != TargetFilter.EmptyOnly) continue;
                foreach (var e in a.RuleEffects)
                {
                    if (e.Kind != RuleEffectKind.MarkTiles || e.Area != RuleArea.Target) continue;
                    var st = c.TryTileState(e.State);
                    if (st?.Circle != null) { state = st; return a; }
                }
            }
            return null;
        }

        /// <summary>GG-T902: a mark of <paramref name="state"/> that counts for <paramref name="team"/>'s circle (its own, or the board's).</summary>
        public static bool IsRuneOf(TileMark m, string state, Team team) =>
            m != null && string.Equals(m.State, state, StringComparison.OrdinalIgnoreCase) && (m.OwnerTeam == team || m.OwnerTeam == Team.Neutral);

        /// <summary>GG-T902: the living units of <paramref name="team"/> standing on its runes of <paramref name="state"/>, in id order.</summary>
        public static List<Unit> RuneSingers(BattleState s, Team team, string state)
        {
            var list = new List<Unit>();
            foreach (var u in s.Units)
                if (u.Alive && u.OnBoard && u.Team == team && !u.IsCorpse && !u.IsRemains && !u.IsEgg && IsRuneOf(s.Region.At(u.Pos), state, team))
                    list.Add(u);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        /// <summary>
        /// GG-T902 (BIBLE §14.17): a Choir singer's ritual turn, or null when there is none (the turn goes on as usual).
        /// Only while at least the circle's Size of its side carry a rune ability for the same state (fewer can never close
        /// it). A singer already on a rune <see cref="RunePlanKind.Hold"/>s there. The free runes (no unit on them) go to the
        /// singers not on one, nearest pair first (ties by unit id, then row-major): a singer with a rune
        /// <see cref="RunePlanKind.Walk"/>s one cardinal step closer onto open floor. A singer with none, while its side's
        /// runes number fewer than Size, the rune is off cooldown, it isn't silenced and no hero is adjacent,
        /// <see cref="RunePlanKind.Paint"/>s one on a free tile (no unit, item, trap or mark) within <see cref="RuneRange"/>,
        /// the nearest ring first, then farthest from the party (so a hero has to cross the board to scuff it), then row-major.
        /// Pure: reads state, never commits.
        /// </summary>
        public static RunePlan PlanRune(BattleState s, Unit enemy)
        {
            if (enemy?.Def == null || enemy.IsImmobile || enemy.IsRemains || enemy.IsEgg || enemy.IsCorpse || enemy.IsMultiTile) return null;
            var rune = RuneAbility(s.Content, enemy.Def, out var state);
            if (rune == null) return null;
            var heroes = s.LivingList(Team.Hero);
            if (heroes.Count == 0) return null;
            var painters = new List<Unit>();
            foreach (var u in s.Units)
                if (u.Alive && u.OnBoard && u.Team == enemy.Team && !u.IsCorpse && !u.IsRemains && !u.IsEgg && !u.IsMultiTile
                    && RuneAbility(s.Content, u.Def, out var st) != null && st == state) painters.Add(u);
            if (painters.Count < state.Circle.Size) return null;
            if (IsRuneOf(s.Region.At(enemy.Pos), state.Id, enemy.Team)) return new RunePlan { Kind = RunePlanKind.Hold, Ability = rune, At = enemy.Pos, Rune = enemy.Pos };

            // The free runes go to the singers not yet on one, nearest pair first.
            int runes = 0;
            var free = new List<Coord>();
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    if (!IsRuneOf(s.Region.At(c), state.Id, enemy.Team)) continue;
                    runes++;
                    if (s.UnitAt(c) == null) free.Add(c);
                }
            var pairs = new List<(int d, int id, int idx)>();
            foreach (var u in painters)
            {
                if (IsRuneOf(s.Region.At(u.Pos), state.Id, enemy.Team)) continue;
                for (int i = 0; i < free.Count; i++) pairs.Add((free[i].Manhattan(u.Pos), u.Id, i));
            }
            pairs.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.id != b.id ? a.id.CompareTo(b.id) : a.idx.CompareTo(b.idx));
            var takenUnit = new HashSet<int>();
            var takenRune = new HashSet<int>();
            Coord? mine = null;
            foreach (var p in pairs)
            {
                if (takenUnit.Contains(p.id) || takenRune.Contains(p.idx)) continue;
                takenUnit.Add(p.id);
                takenRune.Add(p.idx);
                if (p.id == enemy.Id) { mine = free[p.idx]; break; }
            }
            if (mine.HasValue)
            {
                if (enemy.Def.MoveRange <= 0 || enemy.IsRooted) return null;
                var to = mine.Value;
                int dx = to.X - enemy.Pos.X, dy = to.Y - enemy.Pos.Y;
                var xs = new Coord(enemy.Pos.X + Math.Sign(dx), enemy.Pos.Y);
                var ys = new Coord(enemy.Pos.X, enemy.Pos.Y + Math.Sign(dy));
                var first = Math.Abs(dx) >= Math.Abs(dy) ? xs : ys;
                var second = first == xs ? ys : xs;
                foreach (var n in new[] { first, second })
                    if (n != enemy.Pos && s.Walkable(n) && s.UnitAt(n) == null && s.TrapAt(n) == null)
                        return new RunePlan { Kind = RunePlanKind.Walk, Ability = rune, At = n, Rune = to };
                return null; // the way is blocked: the turn goes on as usual
            }

            if (runes >= state.Circle.Size || enemy.IsSilenced) return null;
            if (enemy.Cooldowns.TryGetValue(rune.Id, out var cd) && cd > 0) return null;
            foreach (var h in heroes) if (enemy.IsAdjacentTo(h)) return null;
            Coord? best = null;
            int bestSelf = int.MaxValue, bestHero = -1;
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    int self = c.Manhattan(enemy.Pos);
                    if (self == 0 || self > RuneRange) continue;
                    if (!s.FreeForSpawn(c) || s.TrapAt(c) != null || s.Region.At(c) != null) continue;
                    int near = int.MaxValue;
                    foreach (var h in heroes) foreach (var t in h.Tiles()) near = Math.Min(near, c.Manhattan(t));
                    if (self < bestSelf || (self == bestSelf && near > bestHero)) { best = c; bestSelf = self; bestHero = near; }
                }
            return best.HasValue ? new RunePlan { Kind = RunePlanKind.Paint, Ability = rune, At = best.Value, Rune = best.Value } : null;
        }

        /// <summary>GG-T900: how far (Manhattan) a sapper plants a buried charge from where it stands.</summary>
        public const int BuryRange = 3;
        /// <summary>GG-T900: no sapper plants while its side already has this many of that charge in the ground.</summary>
        public const int BuryCap = 2;
        /// <summary>GG-T900: how far along a hero's approach (steps from the hero) a planted charge still counts as in its way.</summary>
        public const int BuryLaneReach = 4;

        /// <summary>GG-T900: is <paramref name="state"/> a buried hazard: a hazard that hides from the other side
        /// (<see cref="TileStateDef.VisibleSeconds"/>), the <c>buriedmine</c> of §14.23?</summary>
        public static bool Buries(BattleState s, string state) =>
            state != null && s.Content.TryTileState(state) is TileStateDef st && st.Hazard && st.VisibleSeconds > 0f;

        /// <summary>
        /// GG-T900 (BIBLE §14.23, the Spire Burrower's Buried Charge): a sapper's bury is a PickTile, EmptyOnly ability in
        /// its class kit whose MarkTiles rule effect paints a buried hazard (<see cref="Buries"/>) on its Target. It is
        /// planted in place of a step and a strike when no hero is adjacent, it is off cooldown, and fewer than
        /// <see cref="BuryCap"/> marks of that state painted by the foe's side are on the board. The tile is a free one (no
        /// unit, item, trap, block or mark) within <see cref="BuryRange"/> of the sapper, no foe on or beside it (the blast
        /// hits everyone), on a hero's approach lane: on one of that hero's shortest walks to its nearest foe, 1 to
        /// <see cref="BuryLaneReach"/> steps from the hero. Each hero whose lane crosses it is worth 6 for the first and 2
        /// for each further, plus (reach - steps) for the nearer (sooner) ones; the best tile wins (ties: nearer the sapper,
        /// then row-major). Null = no bury this turn. Pure: reads state, never commits.
        /// </summary>
        public static (AbilityDef Ability, Coord At)? PlanBury(BattleState s, Unit enemy)
        {
            if (enemy?.Def?.Abilities == null || enemy.IsImmobile || enemy.IsSilenced || enemy.IsRemains || enemy.IsEgg || enemy.IsMultiTile) return null;
            AbilityDef bury = null; string state = null;
            foreach (var id in enemy.Def.Abilities)
            {
                var a = s.Content.TryAbility(id);
                if (a == null || a.Mode != TargetMode.PickTile || a.Filter != TargetFilter.EmptyOnly) continue;
                foreach (var e in a.RuleEffects)
                    if (e.Kind == RuleEffectKind.MarkTiles && e.Area == RuleArea.Target && Buries(s, e.State)) { bury = a; state = e.State; break; }
                if (bury != null) break;
            }
            if (bury == null) return null;
            if (enemy.Cooldowns.TryGetValue(bury.Id, out var cd) && cd > 0) return null;
            var heroes = s.LivingList(Team.Hero);
            if (heroes.Count == 0) return null;
            foreach (var h in heroes) if (enemy.IsAdjacentTo(h)) return null;
            int planted = 0;
            foreach (var m in s.Region.Tiles.Values) if (m.State == state && m.OwnerTeam == enemy.Team) planted++;
            if (planted >= BuryCap) return null;
            var foes = new List<Unit>();
            foreach (var u in s.LivingList(enemy.Team)) if (!u.IsCorpse && u.OnBoard) foes.Add(u);
            // Each hero's approach: its nearest foe (ties: lower id) is where it is headed.
            var goals = new List<(Coord from, Coord to)>();
            foreach (var h in heroes)
            {
                Unit goal = null; int gd = int.MaxValue;
                foreach (var f in foes)
                {
                    if (f.IsRemains || f.IsEgg) continue;
                    int d = h.Pos.Manhattan(f.Pos);
                    if (d < gd || (d == gd && goal != null && f.Id < goal.Id)) { gd = d; goal = f; }
                }
                if (goal != null) goals.Add((h.Pos, goal.Pos));
            }
            Coord? best = null; float bestScore = 0f; int bestSelf = int.MaxValue;
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    int self = c.Manhattan(enemy.Pos);
                    if (self == 0 || self > BuryRange) continue;
                    if (!s.FreeForSpawn(c) || s.TrapAt(c) != null || s.Region.At(c) != null) continue;
                    bool nearFoe = false;
                    foreach (var f in foes) { foreach (var t in f.Tiles()) if (t.Chebyshev(c) <= 1) { nearFoe = true; break; } if (nearFoe) break; }
                    if (nearFoe) continue;
                    float score = 0f;
                    foreach (var (from, to) in goals)
                    {
                        int toHero = from.Manhattan(c);
                        if (toHero < 1 || toHero > BuryLaneReach || toHero + c.Manhattan(to) != from.Manhattan(to)) continue;
                        score += score == 0f ? 6f : 2f;
                        score += BuryLaneReach - toHero;
                    }
                    if (score <= 0f) continue;
                    if (score > bestScore || (score == bestScore && self < bestSelf)) { best = c; bestScore = score; bestSelf = self; }
                }
            return best.HasValue ? (bury, best.Value) : ((AbilityDef, Coord)?)null;
        }

        /// <summary>
        /// GG-T901 (BIBLE §14.23, the scorpion's burrow ambush): a burrow is an ability in the foe's class kit with a
        /// <see cref="RuleEffectKind.Burrow"/> rule effect. The foe goes under in place of a step and a strike when no hero
        /// is adjacent, it is off cooldown, it is free to move (not rooted, immobile, silenced, a snake, remains, an egg or
        /// more than one tile), holds no charge, and its tile bears no other mark. Null = no burrow this turn. Pure.
        /// </summary>
        public static AbilityDef PlanBurrow(BattleState s, Unit enemy)
        {
            if (enemy?.Def?.Abilities == null || !enemy.Alive || enemy.Burrowed) return null;
            if (enemy.IsImmobile || enemy.IsRooted || enemy.IsSilenced || enemy.IsRemains || enemy.IsEgg || enemy.IsCorpse || enemy.IsMultiTile || enemy.IsSnakeMember) return null;
            AbilityDef burrow = null;
            foreach (var id in enemy.Def.Abilities)
            {
                var a = s.Content.TryAbility(id);
                if (a != null && a.RuleEffects.Exists(e => e.Kind == RuleEffectKind.Burrow)) { burrow = a; break; }
            }
            if (burrow == null) return null;
            if (enemy.Cooldowns.TryGetValue(burrow.Id, out var cd) && cd > 0) return null;
            if (s.CastEntryOf(enemy.Id) != null || s.Region.At(enemy.Pos) != null) return null;
            bool anyHero = false;
            foreach (var h in s.Living(Team.Hero))
            {
                if (h.IsCorpse || h.IsRemains || h.IsEgg) continue;
                if (enemy.IsAdjacentTo(h)) return null;
                anyHero = true;
            }
            return anyHero ? burrow : null;
        }

        /// <summary>GG-T903: how far (Manhattan) a tech gremlin goes out of its way for a gadget it can see.</summary>
        public const int SabotageReach = 4;

        /// <summary>GG-T903 (§14.23): is <paramref name="m"/> a gadget <paramref name="team"/> can sabotage: a mark of a
        /// <see cref="TileStateDef.Gadget"/> state painted by another side (never the board's own)?</summary>
        public static bool IsGadgetOf(BattleState s, TileMark m, Team team) =>
            m != null && m.OwnerTeam != Team.Neutral && m.OwnerTeam != team && (s.Content?.TryTileState(m.State)?.Gadget ?? false);

        /// <summary>GG-T903: the first ability in <paramref name="def"/>'s kit with a <see cref="RuleEffectKind.Sabotage"/>
        /// rule effect (the tech gremlin's Sabotage), or null.</summary>
        public static AbilityDef SabotageAbility(ContentDb c, ClassDef def)
        {
            if (def?.Abilities == null || c == null) return null;
            foreach (var id in def.Abilities)
            {
                var a = c.TryAbility(id);
                if (a != null && a.RuleEffects.Exists(e => e.Kind == RuleEffectKind.Sabotage)) return a;
            }
            return null;
        }

        /// <summary>
        /// GG-T903: what turning the gadget <paramref name="m"/> is worth to the saboteur's side: 6 for one hidden from that
        /// side's foes once turned (a buried charge the party loses track of), 4 for any other hazard (a firewall), 3 for a
        /// wall (a barricade), else 2; plus 1 for each hero within 2 tiles of it (it is guarding someone now).
        /// </summary>
        public static float SabotageValue(BattleState s, Coord at, TileMark m)
        {
            if (m == null) return 0f;
            float v = m.Hidden || (s.Content?.TryTileState(m.State)?.VisibleSeconds ?? 0f) > 0f ? 6f : m.Hazard ? 4f : m.Blocks ? 3f : 2f;
            foreach (var h in s.Living(Team.Hero)) if (h.Pos.Manhattan(at) <= 2) v += 1f;
            return v;
        }

        /// <summary>
        /// GG-T903 (BIBLE §14.23, the tech gremlin's Sabotage): a foe with a Sabotage ability in its kit, off cooldown, not
        /// silenced (the EMP Pulse jams it), immobile, remains, an egg or more than one tile, and with no hero adjacent (then
        /// it strikes) goes for the other side's gadgets (<see cref="IsGadgetOf"/>). Beside one (a cardinal neighbour, or
        /// under it) it <see cref="SabotagePlanKind.Sabotage"/>s it: tech senses tech, so a hidden charge beside it counts;
        /// the most valuable (<see cref="SabotageValue"/>) first, then row-major. Otherwise, free to move, it picks a gadget
        /// its side can see (<see cref="RegionBoard.Sees"/>) within <see cref="SabotageReach"/>, best value less distance
        /// (ties: nearer, then row-major), and <see cref="SabotagePlanKind.Walk"/>s one cardinal step closer onto a free,
        /// safe tile (never onto the gadget). Null = no sabotage this turn (or the way is blocked). Pure: reads, never commits.
        /// </summary>
        public static SabotagePlan PlanSabotage(BattleState s, Unit enemy)
        {
            if (enemy?.Def == null || !enemy.Alive || !enemy.OnBoard || s.Region.Tiles.Count == 0) return null;
            if (enemy.IsImmobile || enemy.IsSilenced || enemy.IsRemains || enemy.IsEgg || enemy.IsCorpse || enemy.IsMultiTile || enemy.Burrowed) return null;
            var a = SabotageAbility(s.Content, enemy.Def);
            if (a == null) return null;
            if (enemy.Cooldowns.TryGetValue(a.Id, out var cd) && cd > 0) return null;
            foreach (var h in s.Living(Team.Hero)) if (!h.IsCorpse && enemy.IsAdjacentTo(h)) return null;

            Coord? beside = null; float besideV = 0f;
            Coord? goal = null; float goalScore = float.MinValue; int goalDist = int.MaxValue;
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    var m = s.Region.At(c);
                    if (!IsGadgetOf(s, m, enemy.Team)) continue;
                    int d = c.Manhattan(enemy.Pos);
                    float v = SabotageValue(s, c, m);
                    if (d <= 1)
                    {
                        if (v > besideV) { beside = c; besideV = v; }
                        continue;
                    }
                    if (d > SabotageReach || !RegionBoard.Sees(m, enemy.Team)) continue;
                    float score = v - d;
                    if (score > goalScore || (score == goalScore && d < goalDist)) { goal = c; goalScore = score; goalDist = d; }
                }
            if (beside.HasValue) return new SabotagePlan { Kind = SabotagePlanKind.Sabotage, Ability = a, At = beside.Value, Gadget = beside.Value };
            if (!goal.HasValue || enemy.Def.MoveRange <= 0 || enemy.IsRooted) return null;
            var to = goal.Value;
            int dx = to.X - enemy.Pos.X, dy = to.Y - enemy.Pos.Y;
            var xs = new Coord(enemy.Pos.X + Math.Sign(dx), enemy.Pos.Y);
            var ys = new Coord(enemy.Pos.X, enemy.Pos.Y + Math.Sign(dy));
            var first = Math.Abs(dx) >= Math.Abs(dy) ? xs : ys;
            var second = first == xs ? ys : xs;
            foreach (var n in new[] { first, second })
                if (n != enemy.Pos && n != to && s.Walkable(n) && s.UnitAt(n) == null && s.TrapAt(n) == null && RegionBoard.DangerAt(s, n, enemy) <= 0f)
                    return new SabotagePlan { Kind = SabotagePlanKind.Walk, Ability = a, At = n, Gadget = to };
            return null; // the way is blocked: the turn goes on as usual
        }

        /// <summary>Cardinal line from beside the caster to the board edge along the dominant axis toward the target (X on ties).</summary>
        public static List<Coord> LineToward(Unit caster, Unit target, BattleState state = null)
        {
            int dx = target.Pos.X - caster.Pos.X, dy = target.Pos.Y - caster.Pos.Y;
            Coord dir = Math.Abs(dx) >= Math.Abs(dy)
                ? new Coord(Math.Sign(dx == 0 ? 1 : dx), 0)
                : new Coord(0, Math.Sign(dy));
            var list = new List<Coord>();
            Coord start = caster.Pos;
            if (dir.X > 0) start = new Coord(caster.Pos.X + caster.W - 1, caster.Pos.Y);
            if (dir.Y > 0) start = new Coord(caster.Pos.X, caster.Pos.Y + caster.H - 1);
            // A block stops the charge, and so does a standing wall someone painted (GG-T934: the Machinist's barricade).
            for (var c = start + dir; BattleState.InBounds(c) && !(state?.IsBlock(c) ?? false) && !(state != null && RegionBoard.StopsCharge(state, c)); c = c + dir) list.Add(c);
            return list;
        }

        /// <summary>The hero it wants (by personality) among those in a clear straight row/column line within reach, or null.</summary>
        public static Unit RangedTarget(BattleState s, Unit enemy)
        {
            var inSight = new List<Unit>();
            foreach (var d in Coord.Cardinals)
            {
                var c = enemy.Pos + d;
                for (int i = 1; i <= enemy.Def.AttackRange && BattleState.InBounds(c); i++, c = c + d)
                {
                    if (s.IsBlock(c)) break; // blocks stop line of sight
                    var occ = s.UnitAt(c);
                    if (occ == null) continue;
                    if (occ.IsHero) inSight.Add(occ);
                    break; // line of sight stops at the first unit
                }
            }
            return PickFrom(s, enemy, inSight);
        }

        public static bool FootprintStepLegal(BattleState s, Unit enemy, Coord newAnchor)
        {
            for (int dy = 0; dy < enemy.H; dy++)
                for (int dx = 0; dx < enemy.W; dx++)
                {
                    var t = new Coord(newAnchor.X + dx, newAnchor.Y + dy);
                    if (!s.Walkable(t)) return false;
                    var occ = s.UnitAt(t);
                    if (occ != null && occ != enemy && !occ.IsHero) return false; // only heroes get shoved aside — never a neutral (GG-T132)
                }
            return true;
        }

        private static bool IsCorner(Coord c) =>
            (c.X == 0 || c.X == BattleConfig.Cols - 1) && (c.Y == 0 || c.Y == BattleConfig.Rows - 1);

        private static bool WouldBeFlanked(BattleState s, Coord loc)
        {
            bool HeroAt(Coord p) { var u = s.UnitAt(p); return u != null && u.Team == Team.Hero; }
            return (HeroAt(loc + Coord.Left) && HeroAt(loc + Coord.Right)) || (HeroAt(loc + Coord.Up) && HeroAt(loc + Coord.Down));
        }

        private static bool WouldFormPincer(BattleState s, Unit enemy)
        {
            foreach (var p in PincerDetector.Detect(s, Team.Enemy, enemy))
                if (p.A == enemy || p.B == enemy) return true;
            return false;
        }

        private static bool WouldSupportAllyPincer(BattleState s, Unit enemy)
        {
            foreach (var p in PincerDetector.Detect(s, Team.Enemy, null))
            {
                if (p.A == enemy || p.B == enemy) continue;
                if (p.SupportersA.Contains(enemy) || p.SupportersB.Contains(enemy)) return true;
            }
            return false;
        }
    }

    /// <summary>GG-T902: what a Choir singer does with its turn (<see cref="EnemyPlanner.PlanRune"/>).</summary>
    public enum SabotagePlanKind { Sabotage, Walk }

    /// <summary>GG-T903: a tech gremlin's sabotage turn: Sabotage the gadget on <see cref="At"/>, or Walk one step to
    /// <see cref="At"/> on the way to <see cref="Gadget"/>.</summary>
    public sealed class SabotagePlan
    {
        public SabotagePlanKind Kind;
        public AbilityDef Ability;
        public Coord At;
        public Coord Gadget;
    }

    public enum RunePlanKind { Paint, Walk, Hold }

    /// <summary>GG-T902: a singer's ritual turn: Paint the rune ability on <see cref="At"/>, Walk one step to <see cref="At"/>
    /// on the way to <see cref="Rune"/>, or Hold its rune.</summary>
    public sealed class RunePlan
    {
        public RunePlanKind Kind;
        public AbilityDef Ability;
        public Coord At;
        public Coord Rune;
    }
}
