using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>What one target is likely to take from a command (GG-T252, POL-D1): ranges over several sampled rolls.</summary>
    public sealed class OutcomeTarget
    {
        public int UnitId;
        public int DamageMin, DamageMax, HealMin, HealMax;
        /// <summary>Share of samples in which the target dies during the action (0..1).</summary>
        public float KillChance;
        /// <summary>GG-T1946: seconds the action pushes the target's turn back (min/max over samples; 0 when it isn't pushed).</summary>
        public float PushMin, PushMax;
        /// <summary>Buff/debuff id → share of samples in which the action applies it to this target.</summary>
        public Dictionary<string, float> StatusChance = new Dictionary<string, float>();
    }

    public sealed class OutcomePreview
    {
        public List<OutcomeTarget> Targets = new List<OutcomeTarget>();
        /// <summary>Mana points the action would add to the party bar, after damping and the capacity cap (§3.1.0; min/max
        /// over samples).</summary>
        public int ManaMin, ManaMax;
        /// <summary>Spells the action would newly make affordable from the pool (min/max over samples).</summary>
        public int FillsMin, FillsMax;
        public int Samples;
        public bool Refused;

        public OutcomeTarget Of(int unitId)
        {
            foreach (var t in Targets) if (t.UnitId == unitId) return t;
            return null;
        }
    }

    /// <summary>GG-T1947: one foe's coming turn as the party window sees it: who it will hurt, and by how much.</summary>
    public sealed class EnemyIntent
    {
        public int ActorId;
        public List<OutcomeTarget> Targets = new List<OutcomeTarget>();
    }

    /// <summary>
    /// The outcome preview behind the confirm card and the drag preview (GG-T252). Pure: every sample runs the command
    /// on its own <see cref="BattleState.Clone"/>, with its own rolls, so the real battle and its RNG are never touched.
    /// Only the acting side's resolution counts — the window closes when the next turn starts, and reopens for the
    /// moment the actor's own charged cast resolves.
    /// </summary>
    public static class OutcomePreviewer
    {
        public static OutcomePreview Preview(BattleState s, BattleCommand cmd, int samples = 6)
        {
            var result = new OutcomePreview { Samples = samples };
            int actor = BattleEngine.CommandHeroId(cmd);
            var perSample = new List<Dictionary<int, Sample>>(samples);
            var mana = new List<int>(samples);
            var fills = new List<int>(samples);
            for (int i = 0; i < samples; i++)
            {
                var copy = s.Clone();
                if (i > 0) copy.Rng = new Rng(unchecked((int)(s.Clock * 31 + i * 7919 + 17)));
                List<BattleEvent> events;
                try { events = BattleEngine.Apply(copy, cmd); }
                catch (TileConflictException) { result.Refused = true; return result; }
                var hits = new Dictionary<int, Sample>();
                int gained = 0, filled = 0;
                bool open = true;
                foreach (var e in events)
                {
                    switch (e)
                    {
                        case AbilityRefusedEvent rf when rf.CasterId == actor && hits.Count == 0: result.Refused = true; return result;
                        case TurnStartedEvent _: open = false; break;
                        case AbilityResolvedEvent ar: open = ar.CasterId == actor; break;
                    }
                    if (!open) continue;
                    switch (e)
                    {
                        case DamagedEvent d: Get(hits, d.TargetId).Damage += d.Amount; break;
                        case HealedEvent h: Get(hits, h.TargetId).Heal += h.Amount; break;
                        case DiedEvent dd: Get(hits, dd.UnitId).Died = true; break;
                        case PushedBackEvent pb: Get(hits, pb.UnitId).PushTicks += pb.Ticks; break;
                        case BuffAppliedEvent b: Get(hits, b.UnitId).Buffs.Add(b.BuffId); break;
                        case ManaGainedEvent mg when mg.Amount > 0: gained += mg.Amount; break;
                        case SpellReadyEvent _: filled++; break;
                    }
                }
                perSample.Add(hits);
                mana.Add(gained);
                fills.Add(filled);
            }

            result.Targets = Aggregate(perSample, samples);
            Span(mana, out result.ManaMin, out result.ManaMax);
            Span(fills, out result.FillsMin, out result.FillsMax);
            return result;
        }

        /// <summary>
        /// GG-T1947 (UX review §1.4 #4): what the next <paramref name="actors"/> foes to arrive on the bar would do to the
        /// party if the window closed now. Each sample runs the shared clock forward on its own clone, with its own rolls,
        /// to just past the last of those foes' arrivals, so each foe's own brain picks and strikes exactly as it will; only
        /// the damage each one deals to a hero counts. Foes in bar order; one that hits nobody has no targets.
        /// </summary>
        public static List<EnemyIntent> Intent(BattleState s, int actors = 2, int samples = 6)
        {
            var result = new List<EnemyIntent>();
            var next = new List<(int ticks, TimelineEntry e)>();
            foreach (var e in s.Timeline)
            {
                if (e.Kind == TimelineKind.Hazard || !(s.UnitById(e.OwnerId) is Unit o) || !o.Alive || o.Team != Team.Enemy) continue;
                int t = BattleEngine.TicksUntilAct(s, e);
                if (t >= 0 && t < BattleConfig.MaxAdvanceTicks) next.Add((t, e));
            }
            next.Sort((a, b) => a.ticks != b.ticks ? a.ticks.CompareTo(b.ticks)
                : a.e.Kind != b.e.Kind ? BattleRunner.ArrivalRank(a.e.Kind).CompareTo(BattleRunner.ArrivalRank(b.e.Kind)) : a.e.Id.CompareTo(b.e.Id));
            int horizon = 0;
            foreach (var (t, e) in next)
            {
                if (result.Count >= actors) break;
                if (result.Exists(x => x.ActorId == e.OwnerId)) continue;
                result.Add(new EnemyIntent { ActorId = e.OwnerId });
                horizon = t;
            }
            if (result.Count == 0) return result;

            var perActor = new Dictionary<int, List<Dictionary<int, Sample>>>();
            foreach (var x in result) perActor[x.ActorId] = new List<Dictionary<int, Sample>>(samples);
            for (int i = 0; i < samples; i++)
            {
                var copy = s.Clone();
                if (i > 0) copy.Rng = new Rng(unchecked((int)(s.Clock * 31 + i * 7919 + 17)));
                List<BattleEvent> events;
                try { events = BattleEngine.RunClock(copy, horizon + 1); }
                catch (TileConflictException) { return new List<EnemyIntent>(); }
                var hits = new Dictionary<int, Dictionary<int, Sample>>();
                foreach (var id in perActor.Keys) hits[id] = new Dictionary<int, Sample>();
                foreach (var e in events)
                {
                    if (!(e is DamagedEvent d) || !hits.TryGetValue(d.SourceId, out var mine) || !(s.UnitById(d.TargetId) is Unit t) || !t.IsHero) continue;
                    var x = Get(mine, d.TargetId);
                    x.Damage += d.Amount;
                    if (d.HpAfter <= 0) x.Died = true;
                }
                foreach (var kv in hits) perActor[kv.Key].Add(kv.Value);
            }
            foreach (var x in result) x.Targets = Aggregate(perActor[x.ActorId], samples);
            return result;
        }

        /// <summary>Folds per-sample hits into one range per unit hit in any sample.</summary>
        private static List<OutcomeTarget> Aggregate(List<Dictionary<int, Sample>> perSample, int samples)
        {
            var targets = new List<OutcomeTarget>();
            var ids = new HashSet<int>();
            foreach (var hs in perSample) foreach (var id in hs.Keys) ids.Add(id);
            foreach (var id in ids)
            {
                var t = new OutcomeTarget { UnitId = id, DamageMin = int.MaxValue, HealMin = int.MaxValue, PushMin = float.MaxValue };
                int kills = 0;
                var statusCounts = new Dictionary<string, int>();
                foreach (var hs in perSample)
                {
                    hs.TryGetValue(id, out var x);
                    int dmg = x?.Damage ?? 0, heal = x?.Heal ?? 0;
                    if (dmg < t.DamageMin) t.DamageMin = dmg;
                    if (dmg > t.DamageMax) t.DamageMax = dmg;
                    if (heal < t.HealMin) t.HealMin = heal;
                    if (heal > t.HealMax) t.HealMax = heal;
                    float push = (x?.PushTicks ?? 0) / (float)BattleConfig.TicksPerSecond;
                    if (push < t.PushMin) t.PushMin = push;
                    if (push > t.PushMax) t.PushMax = push;
                    if (x != null && x.Died) kills++;
                    if (x != null) foreach (var b in x.Buffs) statusCounts[b] = (statusCounts.TryGetValue(b, out var n) ? n : 0) + 1;
                }
                t.KillChance = kills / (float)samples;
                foreach (var kv in statusCounts) t.StatusChance[kv.Key] = kv.Value / (float)samples;
                targets.Add(t);
            }
            return targets;
        }

        private static void Span(List<int> values, out int min, out int max)
        {
            min = values.Count == 0 ? 0 : int.MaxValue;
            max = 0;
            foreach (var n in values) { if (n < min) min = n; if (n > max) max = n; }
        }

        private sealed class Sample
        {
            public int Damage, Heal, PushTicks;
            public bool Died;
            public HashSet<string> Buffs = new HashSet<string>();
        }

        private static Sample Get(Dictionary<int, Sample> d, int id)
        {
            if (!d.TryGetValue(id, out var x)) d[id] = x = new Sample();
            return x;
        }
    }
}
