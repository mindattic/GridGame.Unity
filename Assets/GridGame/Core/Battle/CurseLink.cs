using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Curse Link (§7, §8.1, GG-T873, the Hexer's spell): the picked foe and its partner both carry Linked for its
    /// duration, each instance naming the other (<see cref="BuffInstance.LinkedTo"/>). While both still hold it, damage
    /// dealt to either echoes <see cref="BattleConfig.LinkEchoFraction"/> of itself to the other (min 1); the echo itself
    /// never echoes back. The partner is the picked foe's farthest living teammate (a spread-out pair, or the boss behind
    /// its adds), ties to the one with more HP, then the lower id. Deterministic: no Rng.
    /// </summary>
    public static class CurseLink
    {
        /// <summary>Who a link cast on <paramref name="picked"/> would bind it to, or null when it stands alone.</summary>
        public static Unit Partner(BattleState s, Unit picked)
        {
            if (picked == null || !picked.Alive || !Linkable(picked)) return null;
            Unit best = null;
            int bestDist = -1;
            foreach (var o in s.Units)
            {
                if (o == picked || !o.Alive || o.Team != picked.Team || !Linkable(o)) continue;
                int d = o.DistanceTo(picked);
                if (best == null || d > bestDist || (d == bestDist && (o.Hp > best.Hp || (o.Hp == best.Hp && o.Id < best.Id))))
                {
                    best = o;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>A real foe can be linked: not a corpse, remains or egg left on the board.</summary>
        public static bool Linkable(Unit u) => u != null && !u.IsCorpse && !u.IsRemains && !u.IsEgg;

        /// <summary><paramref name="u"/>'s live link partner: alive, and still Linked back to <paramref name="u"/>. Null otherwise.</summary>
        public static Unit PartnerOf(BattleState s, Unit u)
        {
            if (u == null || !u.Alive) return null;
            var mine = u.Buffs.Find(b => b.Def.Links && b.LinkedTo >= 0);
            if (mine == null) return null;
            var p = s.UnitById(mine.LinkedTo);
            if (p == null || p == u || !p.Alive) return null;
            return p.Buffs.Exists(b => b.Def.Links && b.LinkedTo == u.Id) ? p : null;
        }

        /// <summary>
        /// What HeroBot pays for linking <paramref name="picked"/> (GG-T873): a pair where one foe is a pincer target (a
        /// party hero already stands beside it) and the other is out of the party's reach (no hero within 2 tiles) is worth
        /// a turn: the pincer on the first lands half on the second. Any other pair is worth almost nothing, and a pair
        /// already linked to each other nothing at all.
        /// </summary>
        public static float Value(BattleState s, Unit picked)
        {
            var partner = Partner(s, picked);
            if (partner == null || PartnerOf(s, picked) == partner) return 0f;
            bool pairs = (Flanked(s, picked) && !InReach(s, partner)) || (Flanked(s, partner) && !InReach(s, picked));
            if (!pairs) return 0.5f;
            var far = Flanked(s, picked) ? partner : picked;
            return 10f + System.Math.Min(far.Hp, 40) * 0.2f;
        }

        /// <summary>A party hero stands right beside <paramref name="f"/>: one more and it is pincered.</summary>
        public static bool Flanked(BattleState s, Unit f)
        {
            foreach (var h in s.Living(Team.Hero)) if (h.IsCommandable && h.DistanceTo(f) <= 1) return true;
            return false;
        }

        /// <summary>A party hero stands within 2 tiles of <paramref name="f"/>.</summary>
        public static bool InReach(BattleState s, Unit f)
        {
            foreach (var h in s.Living(Team.Hero)) if (h.IsCommandable && h.DistanceTo(f) <= 2) return true;
            return false;
        }

        /// <summary>The echo a blow of <paramref name="amount"/> on a Linked unit sends to its partner.</summary>
        public static int Echo(int amount) =>
            amount <= 0 ? 0 : System.Math.Max(1, (int)System.Math.Round(amount * BattleConfig.LinkEchoFraction));
    }
}
