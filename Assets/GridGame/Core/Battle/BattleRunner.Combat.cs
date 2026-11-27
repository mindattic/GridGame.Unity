using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // Movement (§1.1) — one cardinal tile at a time; displaced occupant takes the tile just left.
        // ─────────────────────────────────────────────────────────────────────────────

        private bool DoMove(MoveCommand m)
        {
            var hero = S.UnitById(m.HeroId);
            if (hero == null || !hero.Alive || !hero.IsHero) { Emit(new AbilityRefusedEvent { CasterId = m.HeroId, Reason = "No such hero" }); return false; }
            AutoEquipBackupWeapon(hero);
            if (hero.IsImmobile) { Emit(new AbilityRefusedEvent { CasterId = hero.Id, Reason = "Can't move" }); return false; }
            if (hero.IsFrightened && m.Path != null && m.Path.Count > 0) // GG-T916: won't end a drag beside what it fears
            {
                var src = FearSourceOf(hero);
                var end = m.Path[m.Path.Count - 1];
                bool beside = false;
                if (src != null) foreach (var t in src.Tiles()) if (!src.Occupies(end) && t.IsCardinalNeighbor(end)) beside = true;
                if (beside) { Emit(new AbilityRefusedEvent { CasterId = hero.Id, Reason = "Too frightened" }); return false; }
            }
            if (hero.IsIndependent) { Emit(new AbilityRefusedEvent { CasterId = hero.Id, Reason = "Acts on its own" }); return false; } // GG-T303

            var before = SnapshotPositions();
            int trapsBefore = S.Traps.Count, pickupsBefore = S.Pickups.Count;
            int hpBefore = hero.Hp;
            int tilesBefore = S.Region.Tiles.Count, chargeBefore = S.ManaCharge, bankBefore = S.Bank.Count; // GG-T1249
            int eventsBefore = ev.Count;
            // GG-T188 (§2.0): a hold that ran the whole turn window was a forced drop — the turn ends either way.
            bool timeUp = BattleEngine.IsTimeUp(S, m.HeldSeconds);
            Emit(new ActionBeganEvent { ActorId = hero.Id, Label = "Move" });
            if (timeUp) Emit(new TurnTimeUpEvent { HeroId = hero.Id, HeldSeconds = m.HeldSeconds });
            var taken = new List<Coord>();
            WalkPath(hero, m.Path, taken);
            bool nothingHappened = taken.Count == 0
                || (hero.Alive && hero.Hp == hpBefore && S.Traps.Count == trapsBefore && S.Pickups.Count == pickupsBefore && SamePositions(before)
                    && S.Region.Tiles.Count == tilesBefore && S.ManaCharge == chargeBefore && S.Bank.Count == bankBefore
                    && !HadEffectEvent(eventsBefore) && !WouldFormPincer(hero));
            if (nothingHappened && timeUp) return true; // dropped where it stood: no move, but the turn is spent
            if (nothingHappened)
            {
                // A drag that changes nothing is a cancel, not a turn (no clock cost) — unless walking out and
                // back onto the start tile re-forms a pincer (a deliberate, skillful tactic): that always counts.
                Emit(new AbilityRefusedEvent { CasterId = hero.Id, Reason = "No move" });
                return false;
            }
            if (BreaksOnMove(hero)) RemoveBuffsWhere(hero, b => b.Def.BreaksOnMove, "moved");

            S.PendingTimeBankSeconds = Math.Max(0f, Math.Min(BattleConfig.DragTimerSeconds, m.TimeLeftSeconds));
            if (CheckEnd()) return true; // a trap can kill the last hero
            ResolvePincers(Team.Hero, hero);
            CheckEnd();
            return true;
        }

        /// <summary>GG-T1249: did the walk emit anything beyond plain movement (a squish, a rune, a trap...)? Such a drag spends the turn.</summary>
        private bool HadEffectEvent(int fromIndex)
        {
            for (int i = fromIndex; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e is ActionBeganEvent || e is SteppedEvent || e is DisplacedEvent || e is StepBlockedEvent || e is TurnTimeUpEvent
                    || e is AbilityRefusedEvent || e is ComboChangedEvent || e is ComboEndedEvent) continue;
                return true;
            }
            return false;
        }

        /// <summary>Pure geometry check (no mutation): would resolving pincers right now actually strike something?</summary>
        private bool WouldFormPincer(Unit hero) =>
            PincerDetector.Detect(S, Team.Hero, hero).Exists(p => p.A.Alive && p.B.Alive && p.Opponents.Exists(o => o.Alive));

        private static bool BreaksOnMove(Unit u)
        {
            foreach (var b in u.Buffs) if (b.Def.BreaksOnMove) return true;
            return false;
        }

        private Dictionary<int, Coord> SnapshotPositions()
        {
            var d = new Dictionary<int, Coord>();
            foreach (var u in S.Units) if (u.Alive) d[u.Id] = u.Pos;
            return d;
        }

        private bool SamePositions(Dictionary<int, Coord> before)
        {
            foreach (var u in S.Units)
                if (u.Alive && (!before.TryGetValue(u.Id, out var p) || p != u.Pos)) return false;
            return true;
        }

        /// <summary>Walks the hero step by step; returns false if a step was refused (the rest of the path is dropped).</summary>
        public bool WalkPath(Unit hero, List<Coord> path, List<Coord> taken)
        {
            foreach (var next in path)
            {
                if (!hero.Alive) return false;
                bool diagonal = hero.Pos.IsDiagonalNeighbor(next);
                if (!hero.Pos.IsCardinalNeighbor(next) && !(diagonal && BattleConfig.DiagonalSlipEnabled))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "not adjacent" });
                    return false;
                }
                if (!BattleState.InBounds(next))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "edge" });
                    return false;
                }
                if (!S.WalkableFor(next, hero)) // a stage block, or a blocking region tile (a barricade, an open hole: GG-T359/T368; a floater hovers over a hole, GG-T859)
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "block" });
                    return false;
                }
                // A diagonal slip glides into open space only — never a swap, never onto anything at all.
                if (diagonal && (S.UnitAt(next) != null || S.MovableAt(next) != null))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "occupied" });
                    return false;
                }
                if (RootsHold(hero, next)) return false; // GG-T912: a rooted walker can't take a step
                var occ = S.UnitAt(next);
                if (occ != null && HeldFast(occ, next, hero)) return false; // GG-T912: nor drag a rooted occupant off its tile (GG-T882: nor a braced foe)
                if (occ != null && occ.IsHeavy && !occ.IsMultiTile && !occ.IsSnakeMember && !occ.IsRemains) // GG-T891
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "heavy" });
                    return false;
                }
                if (occ != null && (occ.IsMultiTile || occ.IsSnakeMember || occ.IsRemains))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "immovable" });
                    return false;
                }
                var from = hero.Pos;
                var pushed = S.MovableAt(next);
                // The occupant (or movable) of the entered tile swaps into the tile just left — the only tile this
                // step frees, so the pair can never land on a third actor (§1.1, GG-T97).
                if (!Relocate(hero, next, o => o == occ, movablesYield: true))
                {
                    Emit(new StepBlockedEvent { UnitId = hero.Id, At = next, Reason = "occupied" });
                    return false;
                }
                Emit(new SteppedEvent { UnitId = hero.Id, From = from, To = next });
                taken.Add(next);
                if (pushed != null) ShoveMovable(pushed, from, hero);
                if (occ != null && Relocate(occ, from))
                {
                    Emit(new DisplacedEvent { UnitId = occ.Id, From = next, To = from, ByUnitId = hero.Id });
                    ShovePickupsUnder(occ, next);
                    if (!PreviewOnly && BreaksOnMove(occ)) RemoveBuffsWhere(occ, b => b.Def.BreaksOnMove, "moved");
                    if (!PreviewOnly) TriggerTrap(occ);
                }
                if (!PreviewOnly) TriggerTrap(hero);
                if (!PreviewOnly && !hero.Alive) return false;
                if (!PreviewOnly && hero.Pos != next) return false; // fell through the floor (GG-T368) or was moved: the walk ends

            }
            return true;
        }

        /// <summary>GG-T912 (§8.1): a Rooted unit (<see cref="Unit.IsRooted"/>) is held to its tile - it can't step, leap or
        /// blink, and no drag, shove, pull, swap or rule drag moves it. True (with a StepBlocked "rooted") when <paramref name="u"/>
        /// is rooted; <paramref name="by"/> is the mover a rooted occupant stops, when that is not the rooted unit itself.</summary>
        private bool RootsHold(Unit u, Coord at, Unit by = null)
        {
            if (u == null || !u.IsRooted) return false;
            Emit(new StepBlockedEvent { UnitId = (by ?? u).Id, At = at, Reason = "rooted" });
            return true;
        }

        /// <summary>GG-T882: is <paramref name="u"/> held to its tile against a displacement by <paramref name="by"/> (null
        /// for a rule's drag)? Rooted (<see cref="RootsHold"/>), or braced by an ally's Anchor Stance beside it
        /// (<see cref="AnchorStance.BracedBy"/>: a StepBlocked "braced" and a "Braced!" call). Only for being moved by
        /// someone else - a unit's own steps ask <see cref="RootsHold"/>.</summary>
        private bool HeldFast(Unit u, Coord at, Unit by = null)
        {
            if (RootsHold(u, at, by)) return true;
            if (AnchorStance.BracedBy(S, u, by) == null) return false;
            Emit(new StepBlockedEvent { UnitId = (by ?? u).Id, At = at, Reason = "braced" });
            Announce("Braced!", u.Id);
            return true;
        }

        /// <summary>A unit entered a tile: a hero collects and springs what lies there, then (any unit) the region rules'
        /// EnterTile hooks run (GG-T359).</summary>
        private void TriggerTrap(Unit u)
        {
            SpringTrap(u);
            RegionEnter(u);
        }

        private void SpringTrap(Unit u)
        {
            if (!u.Alive || u.Team != Team.Hero) return;
            CollectPickup(u);
            if (!u.Alive || u.IsFloating) return; // GG-T859: a floater passes over a floor trap
            var trap = S.TrapAt(u.Pos);
            if (trap == null) return;
            S.Traps.Remove(trap);
            var def = C.Traps.TryGetValue(trap.TrapId, out var t) ? t : null;
            Emit(new TrapTriggeredEvent { UnitId = u.Id, At = u.Pos, TrapId = trap.TrapId });
            if (def == null) return;
            if (def.Damage > 0) Damage(u, def.Damage, null, HitKind.Normal, def.DamageType, def.Name);
            var buff = C.TryBuff(def.ApplyBuff);
            if (buff != null && u.Alive) ApplyBuff(u, buff, buff.Duration);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Pincers (§1.2, §13.1.1)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Board-wide pincer scan for <paramref name="team"/>; resolves every pair in chain order.</summary>
        private void ResolvePincers(Team team, Unit dropped, Unit mustInclude = null)
        {
            var pincers = PincerDetector.Detect(S, team, dropped);
            supportTurnOwner = team == Team.Enemy ? mustInclude : null; // GG-T307: whose own turn this enemy pincer is
            // GG-T571: a hero's move (a drop or a teleport) that closes ClutchPincersInOneMove pincers is a DoublePincer,
            // awarded as the pincer that reaches the count forms.
            bool heroMove = team == Team.Hero && mustInclude == null && dropped != null && dropped.IsHero;
            int clutchAt = BattleConfig.ClutchPincersInOneMove;
            int chain = 0;
            foreach (var p in pincers)
            {
                if (S.IsOver) return;
                if (mustInclude != null && p.A != mustInclude && p.B != mustInclude) continue;
                if (!p.A.Alive || !p.B.Alive) continue;
                bool anyAlive = false;
                foreach (var o in p.Opponents) if (o.Alive) anyAlive = true;
                if (!anyAlive)
                {
                    if (chain > 0) Emit(new RespiteEvent { Attacker1 = p.A.Id, Attacker2 = p.B.Id });
                    continue;
                }
                if (heroMove && clutchAt >= 2 && chain + 1 == clutchAt) Clutch(dropped, ClutchKind.DoublePincer);
                ResolvePincer(p, team, chain++);
            }
        }

        /// <summary>
        /// Chain depth of the pincer currently resolving (0 outside one), read by <see cref="AwardLoot"/> so a kill
        /// mid-chain earns bonus XP the same way <see cref="BattleConfig.ChargeChainBonus"/> already rewards mana
        /// (owner direction 2026-10-11: deeper combos should pay out more of both, not just mana).
        /// </summary>
        private int chainBonusDepth;

        private void ResolvePincer(Pincer p, Team team, int chainIndex)
        {
            chainBonusDepth = team == Team.Hero ? chainIndex : 0;
            var supporters = new List<Unit>();
            foreach (var x in p.SupportersA) if (!supporters.Contains(x)) supporters.Add(x);
            foreach (var x in p.SupportersB) if (!supporters.Contains(x)) supporters.Add(x);
            var formed = new PincerFormedEvent { Team = team, Attacker1 = p.A.Id, Attacker2 = p.B.Id, ChainIndex = chainIndex };
            // Snapshot the geometry now: by the time the view plays this back the state is the end of the whole
            // command, and any member may have moved since (enemy turns, knockback, a later chain link — GG-T98).
            formed.Attacker1At = UnitFootprint.Of(p.A); formed.Attacker2At = UnitFootprint.Of(p.B);
            // GG-T867: an afterimage end stands on its own tile; the view draws the pincer from there.
            if (p.AAfterimage is Coord ga) { formed.Attacker1At.At = ga; formed.Afterimage1 = true; }
            if (p.BAfterimage is Coord gb) { formed.Attacker2At.At = gb; formed.Afterimage2 = true; }
            foreach (var o in p.Opponents) { formed.Opponents.Add(o.Id); formed.OpponentsAt.Add(UnitFootprint.Of(o)); }
            foreach (var su in supporters) { formed.Supporters.Add(su.Id); formed.SupportersAt.Add(UnitFootprint.Of(su)); formed.SupporterLinks.Add(p.LinkOf(su)?.Id ?? p.AttackerOf(su).Id); }
            Emit(formed);
            if (team == Team.Hero) MarkPincerWitnesses(p); // a recruitable beside a striking hero fought with them (GG-T132)
            if (team == Team.Hero) TrustFromPincer(p, supporters); // a wanderer in a hero pincer trusts the party more (GG-T342)
            WardPincer(p, team);

            // Power Amplifier: a hero attacker standing on it strikes amplified and uses it up (§1.2.6).
            Unit amplified = null;
            if (team == Team.Hero && S.Amplifier.HasValue)
                foreach (var attacker in new[] { p.A, p.B })
                    if (amplified == null && attacker.Alive && TouchesTile(attacker, S.Amplifier.Value)) amplified = attacker;
            if (amplified != null)
            {
                Emit(new AmplifierUsedEvent { At = S.Amplifier.Value, HeroId = amplified.Id });
                Announce("Amplified!", amplified.Id);
                S.Amplifier = null;
            }

            var hpBefore = new Dictionary<Unit, int>();
            foreach (var o in p.Opponents) hpBefore[o] = o.Hp;
            // GG-T833 (§1.2.4): three phases. Prep — supporters that help the attacker (a Crit edge, a buff, a heal,
            // haste, cleanse, mana) act first, so an empowered or healed attacker strikes with it.
            var supportPush = new Dictionary<Unit, float>();
            PincerPhaseBegin(PincerPhase.Prep, chainIndex);
            PerformSupports(p, supporters, team, prep: true, supportPush);
            PincerPhaseEnd(PincerPhase.Prep, chainIndex);

            // Blows — both attackers strike the line. Double Attack's OnAttack is held back for the Follow-up phase.
            var followUps = new List<PincerFollowUp>();
            PincerPhaseBegin(PincerPhase.Blows, chainIndex);
            pincerFollowUps = followUps;
            try
            {
                for (int end = 0; end < 2; end++)
                {
                    var attacker = end == 0 ? p.A : p.B;
                    if (!attacker.Alive) continue;
                    // GG-T867: the Speedster strikes from its afterimage too (its plate side is the afterimage's), and its
                    // weapon wears once however many ends it holds.
                    strikeFromAfterimage = p.IsAfterimageEnd(end == 0) ? (end == 0 ? p.AAfterimage : p.BAfterimage) : null;
                    float mult = 1f + attacker.PincerDamageBonus;
                    if (attacker == amplified) mult *= BattleConfig.AmplifierDamageMultiplier;
                    bool shatters = attacker.IsHero && attacker.WeaponId != null && attacker.WeaponDurability == 1;
                    if (shatters) mult *= BattleConfig.ShatterTargetMultiplier;
                    foreach (var target in p.Opponents)
                    {
                        if (!target.Alive || !attacker.Alive || wardedThisPincer.Contains(target)) continue;
                        Strike(attacker, target, mult, BlowKind.Pincer); // its OnAttack (Double Attack) is queued in followUps
                        if (target.Alive) TryWeaponPincerBuff(attacker, target);
                    }
                    if (end == 1 && p.A == p.B) continue;
                    if (attacker.IsHero && attacker.WeaponId != null && attacker.WeaponDurability > 0) WearWeapon(attacker);
                }
            }
            finally { pincerFollowUps = null; strikeFromAfterimage = null; }
            p.A.SupportCritBonus = p.B.SupportCritBonus = 0f;
            PincerPhaseEnd(PincerPhase.Blows, chainIndex);

            // Follow-up — Double Attack, the attackers' OnPincerAsAttacker triggers, then the supporters that hit the
            // trapped foes (Strike, Cast, Status, Pushback) and Self buffs.
            PincerPhaseBegin(PincerPhase.FollowUp, chainIndex);
            foreach (var f in followUps)
                if (!S.IsOver && f.Attacker.Alive && f.Target.Alive)
                    Fire(TriggerOn.OnAttack, f.Attacker, new TriggerContext { Target = f.Target, Blow = BlowKind.Pincer, StrikeMultiplier = f.Mult, Type = DamageType.Physical });
            foreach (var attacker in p.A == p.B ? new[] { p.A } : new[] { p.A, p.B }) // GG-T867: one Speedster, one trigger
                if (attacker.Alive && !S.IsOver)
                {
                    Unit trapped = null;
                    foreach (var o in p.Opponents) if (trapped == null && o.Alive) trapped = o;
                    Fire(TriggerOn.OnPincerAsAttacker, attacker, new TriggerContext { Target = trapped });
                }
            PerformSupports(p, supporters, team, prep: false, supportPush);
            PincerPhaseEnd(PincerPhase.FollowUp, chainIndex);

            // The pincer attack carries Pushback (GG-T286): every trapped foe a hero pincer landed a blow on acts later.
            // A Pushback supporter's shove adds to that one push (GG-T298) — for either team — never a second push.
            foreach (var o in p.Opponents)
            {
                if (!o.Alive) continue;
                float secs = team == Team.Hero && o.Hp < hpBefore[o] ? BattleConfig.PincerPushbackSeconds : 0f;
                if (supportPush.TryGetValue(o, out var extra)) secs += extra;
                if (secs > 0f) Pushback(o, secs);
            }

            if (team == Team.Hero)
            {
                // Mana charge (§3.1.9): the share of enemy HP this pincer took, divided by how many heroes it took to do
                // it, multiplied up the deeper it sits in a chain. Lean, chained pincers farm mana; dog-piles don't.
                float share = 0f;
                foreach (var o in p.Opponents)
                    if (o.MaxHp > 0 && !o.IsCorpse) share += Math.Min(1f, Math.Max(0, hpBefore[o] - o.Hp) / (float)o.MaxHp);
                int participants = 2 + supporters.Count;
                float divisor = 1f + BattleConfig.ChargeParticipantPenalty * Math.Max(0, participants - 2);
                float chainMult = 1f + BattleConfig.ChargeChainBonus * chainIndex;
                if (share > 0f)
                    AddCharge(Math.Max(BattleConfig.MinPincerCharge, (int)Math.Round(BattleConfig.PincerChargePerHp * share * chainMult / divisor)), p.A.Id, "pincer", participants, chainIndex + 1);
                if (amplified != null && amplified.Alive) AddCharge(BattleConfig.AmplifierCharge, amplified.Id, "amplifier");
            }
            chainBonusDepth = 0;
        }

        /// <summary>A pincer blow's held-back OnAttack (Double Attack), fired in the Follow-up phase (GG-T833).</summary>
        private struct PincerFollowUp { public Unit Attacker; public Unit Target; public float Mult; }

        /// <summary>GG-T867: set while a pincer blow comes from an afterimage end - the tile the blow meets the plate from.</summary>
        private Coord? strikeFromAfterimage;

        /// <summary>Non-null while a pincer's Blows phase runs: <see cref="Strike"/> queues each pincer blow's OnAttack here.</summary>
        private List<PincerFollowUp> pincerFollowUps;

        private void PincerPhaseBegin(PincerPhase phase, int chainIndex) => Emit(new PincerPhaseEvent { Phase = phase, PincerIndex = chainIndex });
        private void PincerPhaseEnd(PincerPhase phase, int chainIndex) => Emit(new PincerPhaseEvent { Phase = phase, PincerIndex = chainIndex, End = true });

        /// <summary>A passive the unit has makes it immune to <paramref name="buffId"/> (Poison Ward, Steadfast: an Always
        /// Immunity trigger, GG-T296).</summary>
        private bool WardedAgainst(Unit u, string buffId) => Triggers.ImmunityTo(S, u, buffId) != null;

        /// <summary>Named weapon passive (§24.6.1, GG-T47): a landed pincer strike (not a supporter or counter hit)
        /// may apply the wielder's weapon's signature buff to the target, e.g. Flamebrand's Burn.</summary>
        private void TryWeaponPincerBuff(Unit attacker, Unit target)
        {
            if (!attacker.IsHero || attacker.WeaponId == null) return;
            var weapon = C.TryItem(attacker.WeaponId);
            if (weapon?.PincerApplyBuff == null || !S.Rng.Chance(weapon.PincerApplyBuffChance)) return;
            var buff = C.TryBuff(weapon.PincerApplyBuff);
            if (buff != null) ApplyBuff(target, buff, buff.Duration);
        }

        /// <summary>Evasion (GG-T297, §13.1.0): a pincer is the game's core payoff, so its blows, the supporters' and the
        /// follow-ups riding them can't be dodged; melee swings, shots and counterstrikes can.</summary>
        private static bool Dodgeable(BlowKind kind) => kind != BlowKind.Pincer && kind != BlowKind.Support;

        /// <summary>
        /// One weapon blow (a pincer strike, a supporter's, a counter, an enemy's melee swing or shot). The trigger engine
        /// rides it (GG-T296): OnDodge on a miss or an Evasion dodge (a DodgedEvent stands in for the 0-damage miss); on a landing blow OnLandedHit for the striker (Lifesteal, Mana Siphon,
        /// a weapon's lifesteal), the class on-hit buff, then OnAttackedDirect for an adjacent victim (Counter Attack);
        /// a primary blow then fires OnAttack (Double Attack's follow-up).
        /// </summary>
        /// <returns>GG-T834: true when the blow landed on the body (not a miss, a dodge or a plate).</returns>
        private bool Strike(Unit attacker, Unit target, float mult, BlowKind kind, bool followUp = false, bool thrown = false)
        {
            bool landed = false;
            Emit(new AttackEvent { AttackerId = attacker.Id, TargetId = target.Id, IsSupport = kind == BlowKind.Support, IsMelee = kind == BlowKind.Melee, IsRanged = kind == BlowKind.Ranged, IsThrown = thrown });
            // GG-T888 (§14.23): a foe standing in the party's flare light is easier to crit; the edge rides the roll's
            // support crit for this one blow (same draw count), then the lent value comes back exactly.
            float lent = attacker.SupportCritBonus, lit = RegionBoard.CritTakenBonus(S, attacker, target);
            if (lit > 0f) attacker.SupportCritBonus = lent + lit;
            attacker.AimMultiplier = RegionBoard.ObscuredAim(S, attacker, target); // GG-T861: a blow aimed into smoke is Blind
            var roll = Formulas.Physical(attacker, target, S.Rng, mult * KitDamageMultiplier(attacker, target) * attacker.OutgoingDamageMultiplier, Dodgeable(kind));
            attacker.AimMultiplier = 1f;
            attacker.SupportCritBonus = lent;
            if (!(roll.Hit != HitKind.Miss && PlateAbsorbs(target, attacker, roll.Damage)))
            {
                int before = target.Hp;
                if (roll.Dodged) Emit(new DodgedEvent { UnitId = target.Id, AttackerId = attacker.Id });
                else Damage(target, roll.Damage, attacker, roll.Hit, DamageType.Physical);
                if (roll.Hit == HitKind.Critical && attacker.IsHero) AddCharge(BattleConfig.CritCharge, attacker.Id, "critical");
                // A crit landed through Blind (its crit chance was cut with its accuracy) is a Clutch (GG-T566).
                if (roll.Hit == HitKind.Critical && attacker.IsHero && attacker.IsBlinded) Clutch(attacker, ClutchKind.BlindCrit);
                if (roll.Hit == HitKind.Miss)
                {
                    if (Triggers.Foes(target, attacker)) Fire(TriggerOn.OnDodge, target, new TriggerContext { Attacker = attacker, Blow = kind, Type = DamageType.Physical });
                }
                else
                {
                    landed = !roll.Dodged;
                    int dealt = before - target.Hp;
                    if (attacker.Alive) Fire(TriggerOn.OnLandedHit, attacker, new TriggerContext { Target = target, Damage = dealt, Blow = kind, StrikeMultiplier = mult, Type = DamageType.Physical });
                    AfterLandedHit(attacker, target);
                    if (target.Alive && attacker.Alive && Triggers.Foes(target, attacker) && target.IsAdjacentTo(attacker))
                        Fire(TriggerOn.OnAttackedDirect, target, new TriggerContext { Attacker = attacker, Damage = dealt, Blow = kind, Type = DamageType.Physical });
                }
            }
            if (!followUp && kind == BlowKind.Pincer && pincerFollowUps != null && attacker.Alive && target.Alive)
                pincerFollowUps.Add(new PincerFollowUp { Attacker = attacker, Target = target, Mult = mult }); // GG-T833: after the blows
            else if (!followUp && (kind == BlowKind.Pincer || kind == BlowKind.Melee) && attacker.Alive && target.Alive)
                Fire(TriggerOn.OnAttack, attacker, new TriggerContext { Target = target, Blow = kind, StrikeMultiplier = mult, Type = DamageType.Physical });
            return landed;
        }

        // ── Armor plates (§13.5) ──

        /// <summary>The side of <paramref name="u"/> that <paramref name="from"/> stands on (dominant axis; ties go to the facing axis).</summary>
        public static Coord SideOf(Unit u, Unit from) =>
            SideOf(u, from.Pos.X + (from.W - 1) / 2f, from.Pos.Y + (from.H - 1) / 2f);

        /// <summary>The side of <paramref name="u"/> a one-tile blow from <paramref name="from"/> lands on (GG-T867: an afterimage).</summary>
        public static Coord SideOf(Unit u, Coord from) => SideOf(u, from.X, from.Y);

        private static Coord SideOf(Unit u, float fx, float fy)
        {
            float cx = u.Pos.X + (u.W - 1) / 2f, cy = u.Pos.Y + (u.H - 1) / 2f;
            float dx = fx - cx, dy = fy - cy;
            if (Math.Abs(dx) > Math.Abs(dy) || (Math.Abs(dx) == Math.Abs(dy) && u.Facing.X != 0))
                return new Coord(Math.Sign(dx), 0);
            return new Coord(0, Math.Sign(dy));
        }

        /// <summary>A physical blow from the plated side dents the plate instead of the body; the plate breaks off at 0.</summary>
        private bool PlateAbsorbs(Unit target, Unit attacker, int amount)
        {
            if (!target.HasPlate || attacker == null || attacker.Team == target.Team || amount <= 0) return false;
            var side = strikeFromAfterimage is Coord ghost ? SideOf(target, ghost) : SideOf(target, attacker); // GG-T867
            if (side != target.Facing) return false;
            target.PlateHp = Math.Max(0, target.PlateHp - amount);
            Emit(new PlateHitEvent { UnitId = target.Id, AttackerId = attacker.Id, Amount = amount, Left = target.PlateHp, Max = target.PlateMax });
            if (target.PlateHp == 0)
            {
                Emit(new PlateBrokenEvent { UnitId = target.Id, AttackerId = attacker.Id });
                Announce($"{target.Name}'s armor breaks off!", target.Id);
            }
            return true;
        }

        /// <summary>An armored unit turns its plate toward the hero it wants.</summary>
        private void FaceTarget(Unit u, bool emit = true)
        {
            if (u.PlateMax <= 0 || !u.Alive) return;
            var t = EnemyPlanner.PickTarget(S, u);
            if (t == null) return;
            var f = SideOf(u, t);
            if (f.X == 0 && f.Y == 0) return;
            if (f == u.Facing) return;
            u.Facing = f;
            if (emit) Emit(new FacingChangedEvent { UnitId = u.Id, Facing = f });
        }

        /// <summary>Is <paramref name="c"/> cardinally next to any tile of <paramref name="u"/>?</summary>
        private static bool TouchesTile(Unit u, Coord c)
        {
            foreach (var d in Coord.Cardinals) if (u.Occupies(c + d) && !u.Occupies(c)) return true;
            return false;
        }

        // ── Knockback (§6.3): shield bash and friends ──

        /// <summary>
        /// Shove <paramref name="t"/> up to <paramref name="tiles"/> straight away from the caster. Hitting another unit is
        /// a crash (both take damage); hitting stone or the board edge is a slam (heavy damage). Bosses and snakes don't budge.
        /// </summary>
        private void Knockback(Unit caster, Unit t, int tiles, int baseDamage)
        {
            if (!t.Alive || t.IsMultiTile || t.IsSnakeMember || t.IsRemains) return;
            if (HeldFast(t, t.Pos, caster)) return; // GG-T912; GG-T882: braced beside an Anchor Stance
            if (t.IsHeavy) { Announce("Too heavy!", t.Id); return; } // GG-T891
            var d = new Coord(t.Pos.X - caster.Pos.X, t.Pos.Y - caster.Pos.Y);
            var dir = Math.Abs(d.X) >= Math.Abs(d.Y) ? new Coord(Math.Sign(d.X), 0) : new Coord(0, Math.Sign(d.Y));
            if (dir.X == 0 && dir.Y == 0) return;
            for (int i = 0; i < tiles && t.Alive && !S.IsOver; i++)
            {
                var next = t.Pos + dir;
                if (TryShoveIntoPit(t, next, caster)) { Announce("Into the pit!", t.Id); return; } // GG-T368: shoved into an open hole
                if (!S.WalkableFor(next, t)) // GG-T859: a floater is carried over a hole, not slammed into it
                {
                    Announce("Slam!", t.Id);
                    Damage(t, BattleConfig.KnockbackSlamDamage + baseDamage, caster, HitKind.Normal, DamageType.Physical, "Slam!");
                    return;
                }
                var other = S.UnitAt(next);
                if (other != null)
                {
                    Announce("Crash!", t.Id);
                    int crash = BattleConfig.KnockbackCrashDamage + baseDamage / 2;
                    Damage(t, crash, caster, HitKind.Normal, DamageType.Physical, "Crash!");
                    if (other.Alive && other.Team != caster.Team && !other.IsNeutral) Damage(other, crash, caster, HitKind.Normal, DamageType.Physical, "Crash!");
                    return;
                }
                if (S.MovableAt(next) is MovableTile mv)
                {
                    var beyond = next + dir;
                    // A pickup beyond blocks the amplifier like a wall (two items never share a tile, §1.1.3, GG-T129).
                    if (!S.FreeToEnter(beyond) || PickupBlocksMovable(beyond) || !MoveMovable(mv, beyond, t)) { Damage(t, BattleConfig.KnockbackCrashDamage, caster, HitKind.Normal, DamageType.Physical, "Crash!"); return; }
                }
                var from = t.Pos;
                if (!Relocate(t, next)) return;
                Emit(new DisplacedEvent { UnitId = t.Id, From = from, To = next, ByUnitId = caster.Id });
                NoteShove(caster, t); // GG-T571: a region danger that kills it now is a HazardKill
                ShovePickupsUnder(t, from); // knocked onto a pickup: a non-hero shoves it back to the tile it left (GG-T228)
                TriggerTrap(t); // a hero springs traps; anyone runs the region rules' EnterTile hooks (GG-T368: a foe cracks the floor too)
            }
        }

        // ── Enemy kits (§14.4): passives and on-hit effects shared by every enemy attack ──

        /// <summary>An enemy's blows scale with the kit multiplier (and the skirmish discount); anyone's with their
        /// Always DamageMultiplier passives (Berserk: ×1.35 below half HP, GG-T296).</summary>
        private float KitDamageMultiplier(Unit attacker, Unit target) =>
            (attacker.IsHero ? 1f : BattleConfig.EnemyStrikeMultiplier * BattleEngine.PartyScale(S) * (skirmishing ? 0.8f : 1f)) * Triggers.OutgoingMultiplier(S, attacker, target);

        /// <summary>After an enemy's attack lands: its class on-hit buff may stick, for <see cref="ClassDef.OnHitTurns"/> when set.</summary>
        private void AfterLandedHit(Unit attacker, Unit target)
        {
            if (attacker.IsHero || !attacker.Alive) return;
            var buff = C.TryBuff(attacker.Def.OnHitBuff);
            if (buff != null && target.Alive && S.Rng.Chance(attacker.Def.OnHitChance)) ApplyBuff(target, buff, attacker.Def.OnHitTurns > 0 ? attacker.Def.OnHitTurns : buff.Duration); // GG-T904
        }

        /// <summary>Every equipped armor piece loses one durability per landing enemy hit; each breaks (and its
        /// bonuses go) independently at 0 (US-243, GG-T89 five-slot armor).</summary>
        private void WearArmor(Unit hero)
        {
            var slots = new List<EquipmentSlot>(hero.Gear.Keys);
            foreach (var slot in slots)
            {
                var piece = hero.Gear[slot];
                if (piece.Durability <= 0) continue;
                piece.Durability--;
                if (piece.Durability > 0) continue;
                var armor = C.TryItem(piece.ItemId);
                if (armor != null) RemoveGearBonuses(hero, armor, piece.Mastery);
                Emit(new ArmorBrokenEvent { UnitId = hero.Id, ArmorId = piece.ItemId });
                Announce($"{armor?.Name ?? "Armor"} broken!", hero.Id);
                RecordBroken(S.Results.BrokenArmors, hero.Def.Id, piece.ItemId);
                hero.Gear.Remove(slot);
                // GG-T182: a broken piece no longer counts toward its set, so any tier it was holding up goes too.
                var set = C.TryArmorSetOf(piece.ItemId);
                if (set != null && ResolveSetBonuses(hero).Count > 0) Announce($"{set.Name} set bonus lost", hero.Id);
            }
        }

        /// <summary>Everything an equipped item grants for the whole battle (as SpawnHero applies it): stats × mastery,
        /// Evasion, resistances, regen and pincer bonus. Battle-start-only perks (start orbs, bank size, Protect) are not.</summary>
        private static void AddGearBonuses(Unit hero, ItemDef item, float mastery)
        {
            hero.GearStats = hero.GearStats + item.Stats * mastery;
            hero.PincerDamageBonus += item.PincerDamageBonus;
            hero.GearEvasion += item.EvasionBonus;
            hero.RegenPerTurn += item.RegenPerTurn;
            foreach (var kv in item.Resistances)
            {
                float cur = hero.GearResist.TryGetValue(kv.Key, out var existing) ? existing : 1f;
                hero.GearResist[kv.Key] = cur * kv.Value;
            }
        }

        /// <summary>Undoes <see cref="AddGearBonuses"/> when a piece breaks, shatters or is swapped out.</summary>
        private static void RemoveGearBonuses(Unit hero, ItemDef item, float mastery)
        {
            hero.GearStats = hero.GearStats + item.Stats * -mastery;
            hero.PincerDamageBonus -= item.PincerDamageBonus;
            hero.GearEvasion -= item.EvasionBonus; // its Evasion goes with it (GG-T297)
            hero.RegenPerTurn -= item.RegenPerTurn;
            foreach (var kv in item.Resistances)
            {
                if (!hero.GearResist.TryGetValue(kv.Key, out var cur)) continue;
                // A piece granting 0 resistance (full immunity) can't be divided back out exactly —
                // once it breaks, its own contribution simply reverts to neutral (owner direction:
                // broken armor should leave 1x effectiveness, not a permanent 0x/immune stuck value).
                hero.GearResist[kv.Key] = kv.Value != 0f ? cur / kv.Value : 1f;
            }
        }

        /// <summary>One durability per pincer; at 0 the weapon shatters: the strike already dealt ×1.5, the wielder takes 15% MaxHP (§24.5).</summary>
        private void WearWeapon(Unit hero)
        {
            hero.WeaponDurability--;
            if (hero.WeaponDurability > 0)
            {
                Emit(new WeaponWornEvent { UnitId = hero.Id, WeaponId = hero.WeaponId, Durability = hero.WeaponDurability });
                return;
            }
            int self = Math.Max(1, (int)Math.Round(hero.MaxHp * BattleConfig.ShatterSelfDamageFraction));
            var weapon = C.TryItem(hero.WeaponId);
            Emit(new WeaponShatteredEvent { UnitId = hero.Id, WeaponId = hero.WeaponId, SelfDamage = self });
            if (weapon != null) RemoveGearBonuses(hero, weapon, hero.WeaponMastery);
            RecordBroken(S.Results.BrokenWeapons, hero.Def.Id, hero.WeaponId);
            hero.WeaponId = null;
            hero.WeaponPower = 0f;
            hero.WeaponMastery = 1f;
            Damage(hero, self, null, HitKind.Normal, DamageType.Physical, "Shatter!");
        }

        /// <summary>Appends a shattered/broken item id to this hero's list, so <c>BattleFactory.Commit</c>
        /// can turn it into a Broken fragment even if a bar backup is auto-equipped afterward (GG-T41).</summary>
        private static void RecordBroken(Dictionary<string, List<string>> map, string heroId, string itemId)
        {
            if (!map.TryGetValue(heroId, out var list)) map[heroId] = list = new List<string>();
            list.Add(itemId);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Timeline reactions to hits (§2.3, §13.4)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Pushback (§2.3, GG-T286): only an ability/item carrying the Pushback keyword, or a pincer, calls this — plain
        /// hits never push. It works anywhere on the bar: the target's turn icon acts <paramref name="seconds"/> later
        /// (capped at <see cref="BattleConfig.PushbackMaxSeconds"/>) — IP lost at its current rate, and whatever IP 0
        /// can't absorb stands it still for the rest. A target holding a cast has the cast slip instead (never cancelled).
        /// One push per target per action, so a chained pincer or a multi-hit doesn't stack it.
        /// </summary>
        private void Pushback(Unit target, float seconds)
        {
            if (!(seconds > 0f) || !target.Alive) return;
            if (target.IsRaging) { Announce("Immune!", target.Id); return; } // GG-T897: a raging bull won't be set back
            if (!pushedThisAction.Add(target.Id)) return;
            int ticks = Math.Max(1, Formulas.SecondsToTicks(Math.Min(seconds, BattleConfig.PushbackMaxSeconds)));
            var entry = S.TurnEntryOf(target.Id);
            if (entry != null)
            {
                float from = BattleEngine.BarU(S, entry);
                int recoverAdded = entry.RecoverTicks > 0 ? ticks : 0;
                if (entry.RecoverTicks > 0) { entry.RecoverTicks += ticks; entry.RecoverTotalTicks += ticks; } // GG-T318: still off the bar, so it recovers longer and its entry keeps reading its Speed
                else
                {
                    int rate = BattleEngine.TurnRatePerTick(S, target);
                    int ip = entry.Ip - ticks * rate;
                    if (ip < 0) { entry.StunTicks += (-ip + rate - 1) / rate; ip = 0; }
                    entry.Ip = ip;
                }
                Emit(new PushedBackEvent { UnitId = target.Id, FromU = from, ToU = BattleEngine.BarU(S, entry), StunTicks = entry.StunTicks, RecoverTicks = recoverAdded });
                return;
            }
            var cast = S.CastEntryOf(target.Id);
            if (cast != null)
            {
                cast.RemainingTicks += ticks;
                Emit(new CastStaggeredEvent { EntryId = cast.Id, CasterId = target.Id, DelayTicks = ticks });
            }
        }

        /// <summary>
        /// Clutch! (GG-T566): the one path for every "really cool" moment. Emits <see cref="ClutchEvent"/> (the white flash and
        /// CLUTCH! float), announces "Clutch!" and grants the shared bonus, <see cref="BattleConfig.ClutchCharge"/> mana
        /// charge. No Rng draw of its own: whatever triggers it has already rolled. Heroes only, a switched-off kind
        /// (<see cref="BattleConfig.ClutchDisabled"/>) does nothing, and GG-T571: at most one Clutch per action — the first
        /// kind to qualify wins (§13.4.1 gives the order). Returns whether it was awarded.
        /// </summary>
        private bool Clutch(Unit unit, ClutchKind kind, int entryId = -1)
        {
            if (unit == null || !unit.IsHero || clutchThisAction || !BattleConfig.ClutchEnabled(kind)) return false;
            clutchThisAction = true;
            Emit(new ClutchEvent { EntryId = entryId, CasterId = unit.Id, Kind = kind });
            Announce("Clutch!", unit.Id);
            if (BattleConfig.ClutchCharge > 0) AddCharge(BattleConfig.ClutchCharge, unit.Id, "clutch");
            return true;
        }

        // ── Clutch! bookkeeping (GG-T571). The runner lives for one command, so these start clear every command. ──

        /// <summary>A Clutch was already awarded in this action. An action is the hero's command itself, then each arrival on
        /// the timeline it runs (a cast resolving, a turn, a rule event): <see cref="BeginClutchAction"/>.</summary>
        private bool clutchThisAction;
        /// <summary>HazardKill: foes a hero's knockback/pull moved this command, with that hero.</summary>
        private readonly Dictionary<int, Unit> shovedByHero = new Dictionary<int, Unit>();
        /// <summary>HazardKill: &gt; 0 while a hole's fall or a region rule's damage is landing (the "hazard" in the kill).</summary>
        private int hazardDamageDepth;
        /// <summary>Finisher: the hero whose blow killed the latest real foe this action (null after any other foe death).</summary>
        private Unit finisherHero;

        private void BeginClutchAction() { clutchThisAction = false; finisherHero = null; }

        /// <summary>A hero's knockback or pull is about to move <paramref name="t"/>: remember who, for HazardKill.</summary>
        private void NoteShove(Unit caster, Unit t)
        {
            if (caster != null && caster.IsHero && t != null && t.Team == Team.Enemy) shovedByHero[t.Id] = caster;
        }

        /// <summary>GG-T571: a foe just died (from <see cref="Kill"/>). Corpses and snake segments change nothing; remains,
        /// eggs and a revived foe's second death (already looted) earn nothing and clear the Finisher. A real foe's death:
        /// LastStand when a low-HP hero dealt it, HazardKill when a hole or region danger did it to a foe a hero shoved, and
        /// the Finisher candidate is set (the wave check awards it).</summary>
        private void NoteFoeDeathForClutch(Unit u, Unit killer, bool alreadyLooted)
        {
            if (u.Team != Team.Enemy || u.IsCorpse || u.IsSnakeSegment) return;
            bool real = !u.IsRemains && !u.IsEgg && !alreadyLooted;
            bool byHero = killer != null && killer.IsHero;
            finisherHero = real && byHero ? killer : null;
            if (!real) return;
            if (byHero)
            {
                float f = BattleConfig.ClutchLastStandHpFraction;
                if (f > 0f && killer.Alive && killer.Hp <= killer.MaxHp * f) Clutch(killer, ClutchKind.LastStand);
            }
            else if (killer == null && hazardDamageDepth > 0 && shovedByHero.TryGetValue(u.Id, out var shover))
                Clutch(shover, ClutchKind.HazardKill);
        }

        /// <summary>GG-T571: the wave just cleared (or the battle was won): a Finisher when a hero's blow felled the last foe.</summary>
        private void ClutchFinisher()
        {
            var h = finisherHero;
            finisherHero = null;
            if (h != null) Clutch(h, ClutchKind.Finisher);
        }

        /// <summary>Cast-stagger interrupt: Clutch (hero only) → WIS shrug → delay; cancel once delay ≥ cast time.</summary>
        private void StaggerCasts(Unit caster, Unit attacker)
        {
            var cast = S.CastEntryOf(caster.Id);
            if (cast == null) return;
            var cs = caster.Effective;
            bool allowClutch = caster.IsHero;
            if (allowClutch)
            {
                float clutch = Math.Min(BattleConfig.ClutchMaxChance, cs.Luck * BattleConfig.ClutchChancePerLuck);
                if (S.Rng.Chance(clutch))
                {
                    Clutch(caster, ClutchKind.CastThroughInterrupt, cast.Id);
                    ResolveCast(cast);
                    return;
                }
            }
            float resist = Math.Min(BattleConfig.MaxResistChance, cs.Wisdom * BattleConfig.WisdomResistPerPoint);
            if (S.Rng.Chance(resist))
            {
                Emit(new CastStaggeredEvent { EntryId = cast.Id, CasterId = caster.Id, DelayTicks = 0, Resisted = true });
                return;
            }
            float delaySec = BattleConfig.BaseInterruptDelaySeconds
                * (1f + attacker.Effective.Strength / BattleConfig.InterruptStrengthScale)
                / (1f + cs.Wisdom / BattleConfig.InterruptWisdomDelayScale);
            int delay = Formulas.SecondsToTicks(delaySec);
            int leftBefore = cast.RemainingTicks; // GG-T571: how close it was to landing when this hit came in
            cast.AccumulatedDelayTicks += delay;
            cast.RemainingTicks += delay;
            Emit(new CastStaggeredEvent { EntryId = cast.Id, CasterId = caster.Id, DelayTicks = delay });
            if (cast.AccumulatedDelayTicks >= cast.TotalCastTicks)
            {
                S.Timeline.Remove(cast);
                Emit(new CastCancelledEvent { EntryId = cast.Id, CasterId = caster.Id, AbilityId = cast.AbilityId, Reason = "interrupted" });
                Announce("Interrupted!", caster.Id);
                if (cast.CasterTeam == Team.Enemy)
                {
                    AddCharge(BattleConfig.InterruptCharge, caster.Id, "interrupt");
                    // GG-T571: cancelled within the last tick window of landing — a LastTickInterrupt Clutch for the hero.
                    float window = BattleConfig.ClutchLastTickSeconds;
                    if (window > 0f && attacker.IsHero && leftBefore <= Formulas.SecondsToTicks(window))
                        Clutch(attacker, ClutchKind.LastTickInterrupt, cast.Id);
                }
            }
        }
    }
}
