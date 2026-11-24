using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>Combo ranks (§1.2.8, GG-T849), Devil May Cry style. None = no combo running.</summary>
    public enum ComboRank { None, D, C, B, A, S, SS, SSS }

    /// <summary>What a combo credit was for (§1.2.8).</summary>
    public enum ComboKind { Pincer, ChainLink, Support, FollowUp, Status, Kill, TileKill }

    /// <summary>One hero's share of a combo: points earned as a pincer attacker and as a supporter.</summary>
    public sealed class ComboShare
    {
        public int UnitId;
        public string ClassId;
        public int AttackerPoints;
        public int SupporterPoints;
        public int Total => AttackerPoints + SupporterPoints;
        public ComboShare Clone() => (ComboShare)MemberwiseClone();
        public override string ToString() => $"{UnitId}:{AttackerPoints}a+{SupporterPoints}s";
    }

    /// <summary>
    /// The hero combo (§1.2.8, GG-T849): builds within one combo window — the party's turn as <see cref="TurnRules"/>
    /// defines it, closed at <see cref="TurnRules.IsTurnBoundary"/> or the battle's end — from pincers, chain links, each
    /// supporter action, follow-ups, statuses inflicted, kills and tile-effect kills. A contributor, kind or effect new to
    /// the window earns a variety bonus; the same unit repeating the same effect halves each time. Enemy pincers never
    /// feed it (an enemy chain gets no combo callout of its own: its pincer floats already show it). Display and stats
    /// only — no reward. Lives on <see cref="BattleState.Combo"/> and is cloned with it; deterministic (no Rng, ordered lists).
    /// </summary>
    public sealed class ComboTracker
    {
        public int Score;
        public ComboRank Rank;
        /// <summary>Contributions credited this window.</summary>
        public int Hits;
        /// <summary>Each contributor's share, in the order they first contributed.</summary>
        public List<ComboShare> Shares = new List<ComboShare>();
        private Dictionary<string, int> repeats = new Dictionary<string, int>();
        private HashSet<ComboKind> kinds = new HashSet<ComboKind>();
        private HashSet<string> effects = new HashSet<string>();

        // The pincer now resolving (from PincerFormedEvent/PincerPhaseEvent): its side, attackers and phase.
        public bool InHeroPincer;
        public int PincerA = -1, PincerB = -1;
        public PincerPhase? Phase;

        public bool Active => Hits > 0;

        public static int BasePoints(ComboKind k)
        {
            switch (k)
            {
                case ComboKind.Pincer: return BattleConfig.ComboPincerPoints;
                case ComboKind.ChainLink: return BattleConfig.ComboChainLinkPoints;
                case ComboKind.Support: return BattleConfig.ComboSupportPoints;
                case ComboKind.FollowUp: return BattleConfig.ComboFollowUpPoints;
                case ComboKind.Status: return BattleConfig.ComboStatusPoints;
                case ComboKind.Kill: return BattleConfig.ComboKillPoints;
                case ComboKind.TileKill: return BattleConfig.ComboTileKillPoints;
                default: return 0;
            }
        }

        /// <summary>The rank a score reaches on <see cref="BattleConfig.ComboRankThresholds"/> (D first).</summary>
        public static ComboRank RankFor(int score)
        {
            var r = ComboRank.None;
            var t = BattleConfig.ComboRankThresholds;
            for (int i = 0; i < t.Length; i++) if (score >= t[i]) r = (ComboRank)(i + 1);
            return r;
        }

        /// <summary>Is <paramref name="unitId"/> crediting as a supporter here? Support always; Pincer and ChainLink never;
        /// anything else when it happens inside a hero pincer by someone other than its two attackers.</summary>
        private bool AsSupporter(int unitId, ComboKind kind)
        {
            if (kind == ComboKind.Support) return true;
            if (kind == ComboKind.Pincer || kind == ComboKind.ChainLink) return false;
            return InHeroPincer && unitId != PincerA && unitId != PincerB;
        }

        /// <summary>Credits <paramref name="u"/> (a hero-side unit) with one contribution and returns the change to emit.</summary>
        public ComboChangedEvent Credit(Unit u, ComboKind kind, string effect, string label)
        {
            if (u == null || u.Team != Team.Hero) return null;
            effect = effect ?? "";
            int pts = BasePoints(kind);
            string key = u.Id + "|" + kind + "|" + effect;
            repeats.TryGetValue(key, out int n);
            repeats[key] = n + 1;
            pts = Math.Max(1, pts >> Math.Min(n, 30));
            var share = Shares.Find(x => x.UnitId == u.Id);
            if (share == null)
            {
                share = new ComboShare { UnitId = u.Id, ClassId = u.Def?.Id };
                Shares.Add(share);
                if (Hits > 0) pts += BattleConfig.ComboNewContributorBonus;
            }
            if (kinds.Add(kind) && Hits > 0) pts += BattleConfig.ComboNewKindBonus;
            if (effects.Add(kind + "|" + effect) && n == 0 && Hits > 0) pts += BattleConfig.ComboNewEffectBonus;
            if (AsSupporter(u.Id, kind)) share.SupporterPoints += pts; else share.AttackerPoints += pts;
            Hits++;
            Score += pts;
            var before = Rank;
            Rank = RankFor(Score);
            return new ComboChangedEvent { Score = Score, Rank = Rank, RankUp = Rank > before, ContributorId = u.Id, Kind = kind, Points = pts, Label = label };
        }

        /// <summary>Closes the window: records the best rank and each hero's points into <paramref name="results"/>, resets,
        /// and returns the summary to emit (null when nothing was credited).</summary>
        public ComboEndedEvent End(BattleResults results)
        {
            ComboEndedEvent done = null;
            if (Hits > 0)
            {
                done = new ComboEndedEvent { Rank = Rank, Score = Score, Hits = Hits };
                foreach (var sh in Shares) done.Breakdown.Add(sh.Clone());
                if (results != null)
                {
                    if (Score > results.BestComboScore) { results.BestComboScore = Score; results.BestComboRank = Rank; }
                    foreach (var sh in Shares)
                    {
                        if (sh.ClassId == null) continue;
                        results.ComboAttackerPoints.TryGetValue(sh.ClassId, out int a);
                        results.ComboAttackerPoints[sh.ClassId] = a + sh.AttackerPoints;
                        results.ComboSupporterPoints.TryGetValue(sh.ClassId, out int s);
                        results.ComboSupporterPoints[sh.ClassId] = s + sh.SupporterPoints;
                    }
                }
            }
            Score = 0; Rank = ComboRank.None; Hits = 0;
            Shares.Clear(); repeats.Clear(); kinds.Clear(); effects.Clear();
            return done;
        }

        public ComboTracker Clone()
        {
            var c = (ComboTracker)MemberwiseClone();
            c.Shares = Shares.ConvertAll(x => x.Clone());
            c.repeats = new Dictionary<string, int>(repeats);
            c.kinds = new HashSet<ComboKind>(kinds);
            c.effects = new HashSet<string>(effects);
            return c;
        }
    }

    /// <summary>The combo moved (§1.2.8): <see cref="ContributorId"/> earned <see cref="Points"/> for <see cref="Kind"/>.</summary>
    public sealed class ComboChangedEvent : BattleEvent
    {
        public int Score; public ComboRank Rank; public bool RankUp; public int ContributorId; public ComboKind Kind; public int Points; public string Label;
        public override string ToString() => $"Combo +{Points} {Kind} by {ContributorId} '{Label}' = {Score} {Rank}";
    }

    /// <summary>The combo window closed (§1.2.8) with its final rank and each contributor's share.</summary>
    public sealed class ComboEndedEvent : BattleEvent
    {
        public ComboRank Rank; public int Score; public int Hits;
        public List<ComboShare> Breakdown = new List<ComboShare>();
        public override string ToString() => $"ComboEnded {Rank} {Score} x{Hits} [{string.Join(",", Breakdown)}]";
    }

    /// <summary>Runner glue for the combo (§1.2.8, GG-T849): event observation, kills, statuses and the window close.</summary>
    internal sealed partial class BattleRunner
    {
        /// <summary>Called for every emitted event: pincers, chain links, supporter actions and follow-ups.</summary>
        private void ObserveCombo(BattleEvent e)
        {
            var c = S.Combo;
            switch (e)
            {
                case PincerFormedEvent pf:
                    c.InHeroPincer = pf.Team == Team.Hero;
                    c.PincerA = pf.Attacker1; c.PincerB = pf.Attacker2; c.Phase = null;
                    if (!c.InHeroPincer) return;
                    var kind = pf.ChainIndex > 0 ? ComboKind.ChainLink : ComboKind.Pincer;
                    string label = pf.ChainIndex > 0 ? "Chain " + (pf.ChainIndex + 1) : "Pincer";
                    // GG-T867: an afterimage end is its Speedster's, credited apart from the Speedster's own end.
                    EmitCombo(c.Credit(S.UnitById(pf.Attacker1), kind, pf.ChainIndex + (pf.Afterimage1 ? ":afterimage" : ""), pf.Afterimage1 ? "Afterimage" : label));
                    EmitCombo(c.Credit(S.UnitById(pf.Attacker2), kind, pf.ChainIndex + (pf.Afterimage2 ? ":afterimage" : ""), pf.Afterimage2 ? "Afterimage" : label));
                    break;
                case PincerPhaseEvent ph:
                    c.Phase = ph.End ? (PincerPhase?)null : ph.Phase;
                    if (ph.End && ph.Phase == PincerPhase.FollowUp) { c.InHeroPincer = false; c.PincerA = c.PincerB = -1; }
                    break;
                case SupportActionEvent sa when c.InHeroPincer:
                    EmitCombo(c.Credit(S.UnitById(sa.SupporterId), c.Phase == PincerPhase.FollowUp ? ComboKind.FollowUp : ComboKind.Support,
                        sa.Effect.ToString(), sa.Label ?? sa.Effect.ToString()));
                    break;
                case TriggeredEvent t when c.InHeroPincer && c.Phase == PincerPhase.FollowUp && (t.UnitId == c.PincerA || t.UnitId == c.PincerB):
                    EmitCombo(c.Credit(S.UnitById(t.UnitId), ComboKind.FollowUp, t.AbilityId, t.Callout ?? "Follow-up"));
                    break;
            }
        }

        private void EmitCombo(BattleEvent e) { if (e != null) Emit(e); }

        /// <summary>A foe fell: a hero's blow is a Kill; a hazard kill on a foe a hero shoved is that hero's TileKill.</summary>
        private void ComboKill(Unit u, Unit killer)
        {
            if (PreviewOnly || u.Team != Team.Enemy || u.IsCorpse || u.IsSnakeSegment || u.IsRemains || u.IsEgg) return;
            if (killer != null && killer.Team == Team.Hero) EmitCombo(S.Combo.Credit(killer, ComboKind.Kill, u.Def?.Id, "Kill"));
            else if (killer == null && hazardDamageDepth > 0 && shovedByHero.TryGetValue(u.Id, out var shover))
                EmitCombo(S.Combo.Credit(shover, ComboKind.TileKill, u.Def?.Id, "Tile kill"));
        }

        /// <summary>A hero inflicted a debuff on a foe.</summary>
        private void ComboStatus(Unit u, BuffDef def, int sourceId)
        {
            if (PreviewOnly || u.Team != Team.Enemy || def.Kind != BuffKind.Debuff || sourceId < 0) return;
            if (S.UnitById(sourceId) is Unit src && src.Team == Team.Hero) EmitCombo(S.Combo.Credit(src, ComboKind.Status, def.Id, def.Name ?? def.Id));
        }

        /// <summary>The combo window closes (a turn boundary or the battle's end).</summary>
        private void CloseCombo()
        {
            if (PreviewOnly) return;
            EmitCombo(S.Combo.End(S.Results));
        }
    }
}
