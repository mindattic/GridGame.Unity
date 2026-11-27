using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>A hero's place on the skill hexagon (GG-T1031, docs/design/skill-hexagon/PLAN.md): the nodes it owns
    /// (<see cref="HeroSave.HexNodes"/>), bought with its <see cref="HeroSave.SkillPoints"/> (1 per level, owner
    /// 2026-11-24; every node costs its board price, 1 today). Owning an ability node teaches the ability, so it can go on
    /// the bar. The path is the gate: an ability's Requires do not apply here.</summary>
    public static class SkillHexProgress
    {
        /// <summary>The loaded board, or null when the content has none.</summary>
        public static SkillHexBoard Board(ContentDb c) => c?.SkillHex == null ? null : new SkillHexBoard(c.SkillHex);

        public static string Key((int Q, int R) pt) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", pt.Q, pt.R);

        public static bool TryParse(string key, out (int Q, int R) pt)
        {
            pt = default;
            var parts = key?.Split(',');
            if (parts == null || parts.Length != 2 || !int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var q) || !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var r)) return false;
            pt = (q, r);
            return true;
        }

        /// <summary>The nodes this hero owns (unparsable entries skipped).</summary>
        public static SkillHexOwned Owned(HeroSave h)
        {
            var pts = new List<(int Q, int R)>();
            foreach (var k in h?.HexNodes ?? new List<string>())
                if (TryParse(k, out var pt)) pts.Add(pt);
            return new SkillHexOwned(pts);
        }

        /// <summary>Nodes this hero can buy next, sorted by (Q, R). With nothing owned yet, its class Start path is offered
        /// as one whole: buy it with <see cref="Join"/>.</summary>
        public static List<(int Q, int R)> Frontier(ContentDb c, HeroSave h)
        {
            var board = Board(c);
            if (board == null || h == null) return new List<(int, int)>();
            return board.Frontier(Owned(h).Nodes);
        }

        /// <summary>Gives a hero that owns nothing yet its class Start path, free (and learns every ability on it).</summary>
        public static Result Join(ContentDb c, HeroSave h)
        {
            var board = Board(c);
            if (board == null || h == null) return Result.Fail("No skill hexagon");
            if (h.HexNodes == null) h.HexNodes = new List<string>();
            if (h.HexNodes.Count > 0) return Result.Fail("Already on the board");
            var start = board.StartOf(h.ClassId);
            if (start.Count == 0) return Result.Fail("This hero has no start path");
            foreach (var pt in start.OrderBy(p => p)) Own(c, board, h, pt);
            return Result.Success("Joined the board");
        }

        /// <summary>Buys one node next to one the hero owns, for its price in skill points. Owning an ability node
        /// teaches the ability (its Requires don't gate it).</summary>
        public static Result Buy(ContentDb c, HeroSave h, (int Q, int R) pt)
        {
            var board = Board(c);
            if (board == null || h == null) return Result.Fail("No skill hexagon");
            var owned = Owned(h);
            if (owned.Owns(pt)) return Result.Fail("Already owned");
            if (owned.Count == 0) return Result.Fail("Not on the board yet");
            if (!board.CanTake(owned.Nodes, pt)) return Result.Fail("Not next to a node you own");
            int price = board.Price(pt);
            if (h.SkillPoints < price) return Result.Fail("Not enough skill points");
            h.SkillPoints -= price;
            var n = Own(c, board, h, pt);
            return Result.Success(n?.Kind == SkillHexNodeKind.Ability ? $"Learned {c.TryAbility(n.Ability)?.Name ?? n.Ability}" : "Bought");
        }

        /// <summary>Buys the cheapest route to <paramref name="target"/> when the hero can pay for all of it; nothing changes
        /// otherwise.</summary>
        public static Result BuyRoute(ContentDb c, HeroSave h, (int Q, int R) target)
        {
            var board = Board(c);
            if (board == null || h == null) return Result.Fail("No skill hexagon");
            var owned = Owned(h);
            var route = board.CheapestRoute(owned.Nodes, target);
            if (route == null) return Result.Fail("Can't reach that");
            if (route.Count == 0) return Result.Fail("Already owned");
            int cost = board.SpCost(route, owned.Nodes);
            if (h.SkillPoints < cost) return Result.Fail($"Needs {cost} skill points ({h.SkillPoints} now)");
            foreach (var pt in route)
            {
                var r = Buy(c, h, pt);
                if (!r.Ok) return r;
            }
            return Result.Success($"Bought {route.Count} node{(route.Count == 1 ? "" : "s")}");
        }

        // The Inn's Respec on the skill board (§25.6.3, GG-T53 → GG-T1088): same cost model as the retired talent respec.
        public const int RespecBaseGold = 100;
        public const int RespecGoldPerLevel = 50;

        /// <summary>Respec price at the Inn: grows with the hero's level.</summary>
        public static int RespecCost(HeroSave h) => RespecBaseGold + RespecGoldPerLevel * Progression.Level(h);

        /// <summary>Why the Inn can't respec right now (no Innkeeper yet / on the road without a Medic Wagon), or null.</summary>
        public static string RespecClosed(SaveData s)
        {
            if (!CampUnlocks.NpcOpen(s, CampNpcs.Innkeeper)) return "The Innkeeper hasn't joined yet";
            if (!Expeditions.InnOpen(s)) return "On the road — the Inn opens when the expedition ends";
            return null;
        }

        /// <summary>The owned nodes a respec gives back: every saved node off the hero's class Start path.</summary>
        public static List<(int Q, int R)> Refundable(ContentDb c, HeroSave h)
        {
            var board = Board(c);
            if (board == null || h?.HexNodes == null || h.HexNodes.Count == 0) return new List<(int, int)>();
            var start = board.StartOf(h.ClassId);
            return Owned(h).Nodes.Where(pt => !start.Contains(pt)).Distinct().OrderBy(p => p).ToList();
        }

        /// <summary>Skill points a respec refunds: the board price of every <see cref="Refundable"/> node.</summary>
        public static int RefundPoints(ContentDb c, HeroSave h)
        {
            var board = Board(c);
            return board == null ? 0 : Refundable(c, h).Sum(board.Price);
        }

        /// <summary>The abilities a respec forgets: those on refunded ability nodes, except any the class keeps anyway (on its
        /// Start path, or in its starting kit).</summary>
        public static HashSet<string> RespecForgets(ContentDb c, HeroSave h)
        {
            var forget = new HashSet<string>();
            var board = Board(c);
            if (board == null || h == null) return forget;
            foreach (var pt in Refundable(c, h))
            {
                var n = board.Node(pt);
                if (n != null && n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability)) forget.Add(n.Ability);
            }
            if (forget.Count == 0) return forget;
            foreach (var pt in board.StartOf(h.ClassId))
            {
                var n = board.Node(pt);
                if (n != null && n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability)) forget.Remove(n.Ability);
            }
            var def = c.TryClass(h.ClassId);
            if (def != null)
            {
                forget.ExceptWith(Progression.KitAbilities(c, def));
                forget.ExceptWith(Progression.StarterKit(c, def));
            }
            return forget;
        }

        /// <summary>GG-T566: the equipped weapon/armor pieces <paramref name="h"/> could no longer use after a
        /// <see cref="Respec"/> (a forgotten proficiency passive), slot and item id. Empty = the respec leaves all gear usable.</summary>
        public static List<(EquipmentSlot Slot, string ItemId)> GearLostOnRespec(ContentDb c, HeroSave h)
        {
            var lost = new List<(EquipmentSlot, string)>();
            if (h == null) return lost;
            var forget = RespecForgets(c, h);
            if (forget.Count == 0) return lost;
            var after = new HeroSave { ClassId = h.ClassId, Known = h.Known.Where(k => !forget.Contains(k)).ToList() };
            var gated = new List<(EquipmentSlot, string)>();
            if (h.WeaponId != null) gated.Add((EquipmentSlot.Weapon, h.WeaponId));
            foreach (var kv in h.Armor)
                if (kv.Value?.ItemId != null) gated.Add((kv.Key, kv.Value.ItemId));
            foreach (var (slot, id) in gated)
                if (Equipment.ProficiencyGate(c, h, c.TryItem(id)) == null && Equipment.ProficiencyGate(c, after, c.TryItem(id)) != null)
                    lost.Add((slot, id));
            return lost;
        }

        /// <summary>The Inn's Respec on the skill board: pays <see cref="RespecCost"/> gold, gives back every owned node off
        /// the class Start path as skill points, forgets the abilities only those nodes taught (<see cref="RespecForgets"/>)
        /// and clears them off the bar and the active passives. The hero keeps its Start path.</summary>
        /// <remarks>GG-T566: while the respec would forget a proficiency the hero's equipped gear needs
        /// (<see cref="GearLostOnRespec"/>) it refuses unless <paramref name="unequipUnusable"/> is true, in which case those
        /// pieces go to the bag.</remarks>
        public static Result Respec(ContentDb c, SaveData s, HeroSave h, bool unequipUnusable = false)
        {
            if (h == null) return Result.Fail("No hero");
            var board = Board(c);
            if (board == null) return Result.Fail("No skill hexagon");
            var closed = RespecClosed(s);
            if (closed != null) return Result.Fail(closed);
            var refunded = Refundable(c, h);
            if (refunded.Count == 0) return Result.Fail("No board nodes bought yet");
            int refund = refunded.Sum(board.Price);
            int cost = RespecCost(h);
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            var lost = GearLostOnRespec(c, h);
            if (lost.Count > 0 && !unequipUnusable)
                return Result.Fail("Respec would leave gear this hero can't use: " + string.Join(", ", lost.Select(g => c.TryItem(g.ItemId)?.Name ?? g.ItemId)));
            s.Gold -= cost;
            var forget = RespecForgets(c, h);
            var gone = new HashSet<string>(refunded.Select(Key));
            h.HexNodes.RemoveAll(gone.Contains);
            h.Known.RemoveAll(forget.Contains);
            h.ActivePassives?.RemoveAll(forget.Contains);
            for (int i = 0; i < h.Bar.Count; i++)
                if (h.Bar[i] != null && forget.Contains(h.Bar[i])) h.Bar[i] = null;
            h.SkillPoints += refund;
            int stowed = 0;
            foreach (var (slot, _) in lost) if (Equipment.Unequip(c, s, h, slot).Ok) stowed++;
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            return Result.Success($"{hero}: {refund} skill point{(refund == 1 ? "" : "s")} refunded" +
                (stowed > 0 ? $" · {stowed} piece{(stowed == 1 ? "" : "s")} of gear moved to the bag" : ""));
        }

        private static SkillHexNodeDef Own(ContentDb c, SkillHexBoard board, HeroSave h, (int Q, int R) pt)
        {
            var key = Key(pt);
            if (!h.HexNodes.Contains(key)) h.HexNodes.Add(key);
            var n = board.Node(pt);
            if (n != null && n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability) && !h.Known.Contains(n.Ability))
                h.Known.Add(n.Ability);
            return n;
        }
    }
}
