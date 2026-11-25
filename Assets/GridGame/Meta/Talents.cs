using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>Where a skill-tree node stands for one hero (§25.6.3, GG-T53).</summary>
    public enum TalentState
    {
        /// <summary>Already in the hero's kit without a point spent on it (starting loadout or gold-Learned; GG-T978: or one
        /// of its class's family-tree <see cref="ClassDef.StartNodes"/>, stat nodes included): counts toward the tree's
        /// points like any unlocked node, but Respec can't refund it.</summary>
        Innate,
        /// <summary>Bought with skill points (<see cref="HeroSave.TreeNodes"/>); Respec refunds it.</summary>
        Purchased,
        /// <summary>Every gate met; affordable or not, it can be bought once the hero has the points.</summary>
        Available,
        /// <summary>A prerequisite node or the tree's point requirement is still missing.</summary>
        Locked,
    }

    /// <summary>
    /// Cross-tree specialization tuning (§25.6.2, GG-T51). Static fields rather than consts so a balance pass (or a test)
    /// can retune them without touching the rules in <see cref="Talents"/>.
    /// </summary>
    public static class TalentConfig
    {
        /// <summary>Points in the home tree (<see cref="Talents.PointsIn"/>) that open each off-tree slot, in order:
        /// the first entry opens a second tree, the second a third. Its length is the most off-trees a hero can hold.</summary>
        public static int[] OpenTreeAt = { 10, 20 };

        /// <summary>Extra skill points every off-tree node costs on top of its <see cref="SkillNodeDef.Cost"/>.</summary>
        public static int OffTreePremium = 1;

        /// <summary>An off-tree node's <see cref="SkillNodeDef.RequiredPointsInTree"/> is multiplied by this (rounded up),
        /// so its deeper tiers need more points in that tree than the home tree's do; Start-tier nodes (0) stay at 0.</summary>
        public static float OffTreeGateScale = 1.5f;

        /// <summary>GG-T980 (owner decision pending, conservative default): does a sibling's signature node on a family tree
        /// (<see cref="SkillNodeDef.SignatureOf"/> another class) keep its grant's <see cref="AbilityDef.Requires"/> stat gate?
        /// True: a sibling may spec into the same move but must meet its stats; only the node's own class (and any hero's
        /// start nodes) is waived. False: every signature node is waived for every member of the family.</summary>
        public static bool SiblingSignatureStatGated = true;
    }

    /// <summary>
    /// Spending skill points on tree nodes, and the Inn's Respec (§25.6.3, GG-T53). A node is unlocked if it was bought
    /// (<see cref="HeroSave.TreeNodes"/>) or its grant is already Known — the GG-T50 rule that a hero starts its home tree
    /// with its default abilities pre-unlocked — so the points a tree counts (<see cref="PointsIn"/>) match the Build
    /// screen's <see cref="Progression.TreeInvestment"/> reading.
    /// Cross-tree specialization (§25.6.2, GG-T51): a hero opens a second tree once it has
    /// <see cref="TalentConfig.OpenTreeAt"/>[0] points in its home tree, and a third at [1]. Buying the first node of an
    /// off-tree is what opens it. Off-tree nodes cost <see cref="TalentConfig.OffTreePremium"/> more, and their points-in-tree
    /// gates are scaled by <see cref="TalentConfig.OffTreeGateScale"/>.
    /// GG-T979: a class with a family tree (<see cref="Progression.FamilyTree"/>) is at home there: its start nodes are
    /// Innate and count toward the slots, every other tree (role or family) is an off-tree with the same slots, premium and
    /// scaled gates, and adjacency is measured on the family board (<see cref="TreeAdjacency.FamilyRing"/>).
    /// </summary>
    public static class Talents
    {
        public const int RespecBaseGold = 100;
        public const int RespecGoldPerLevel = 50;

        public static string Key(SkillTreeDef t, SkillNodeDef n) => t.Id + "/" + n.Id;

        public static bool IsHome(ContentDb c, HeroSave h, SkillTreeDef t) => t != null && t == HomeTree(c, h);

        /// <summary>The home tree, or an off-tree the hero has bought at least one node in. (A tree that only holds a
        /// Known ability, e.g. a gold-Learned spell, is not opened: buying a node there still takes a slot.)</summary>
        public static bool Opened(ContentDb c, HeroSave h, SkillTreeDef t) =>
            IsHome(c, h, t) || (t != null && t.Nodes.Any(n => Bought(h, t, n)));

        /// <summary>GG-T978: is <paramref name="n"/> one of the nodes the hero's class starts with on its family tree
        /// (<see cref="Progression.StartNodes"/>)? Such a node is <see cref="TalentState.Innate"/> from creation, even a stat
        /// node with no grant, and never refunded.</summary>
        public static bool IsStartNode(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n)
        {
            if (c == null || h == null || t == null || n == null) return false;
            var def = c.TryClass(h.ClassId);
            if (def == null || def.StartNodes == null || def.StartNodes.Count == 0 || Progression.FamilyTree(c, def) != t) return false;
            return def.StartNodes.Any(id => string.Equals(id, n.Id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Off-trees the hero has opened (bought a node in), by name.</summary>
        public static List<SkillTreeDef> OpenedOffTrees(ContentDb c, HeroSave h) =>
            c.SkillTrees.Values.Where(t => !IsHome(c, h, t) && Opened(c, h, t))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>How many off-trees the hero's home-tree points allow (0, 1 or 2 with the default config).</summary>
        public static int OffTreeSlots(ContentDb c, HeroSave h)
        {
            var home = HomeTree(c, h);
            if (home == null) return 0;
            int pts = PointsIn(c, h, home);
            return TalentConfig.OpenTreeAt.Count(at => pts >= at);
        }

        /// <summary>Ids of the trees the hero holds for adjacency (§25.6.2.2, GG-T135): the home tree plus every opened off-tree.</summary>
        public static List<string> HeldTreeIds(ContentDb c, HeroSave h)
        {
            var ids = new List<string>();
            var home = HomeTree(c, h);
            if (home != null) ids.Add(home.Id);
            ids.AddRange(OpenedOffTrees(c, h).Select(t => t.Id));
            return ids;
        }

        /// <summary>GG-T979: the adjacency board this hero plays on: the family ring when its home is a family tree, else the
        /// role ring (<see cref="TreeAdjacency.BoardFor"/>).</summary>
        public static string[] Board(ContentDb c, HeroSave h) => TreeAdjacency.BoardFor(c, HomeTree(c, h)?.Id);

        /// <summary>Steps from the nearest other held tree to this one on <see cref="TreeAdjacency"/>'s board (0 for home).</summary>
        public static int Distance(ContentDb c, HeroSave h, SkillTreeDef t) =>
            t == null || IsHome(c, h, t) ? 0
                : TreeAdjacency.DistanceFrom(HeldTreeIds(c, h).Where(id => id != t.Id), t.Id, Board(c, h));

        /// <summary>Why the hero can't buy into this tree yet (no free off-tree slot, then too far from every held tree on
        /// the adjacency board — GG-T135), or null when it is opened or can be.</summary>
        public static string WhyClosed(ContentDb c, HeroSave h, SkillTreeDef t)
        {
            if (Opened(c, h, t)) return null;
            int opened = OpenedOffTrees(c, h).Count;
            if (opened < OffTreeSlots(c, h))
                return TreeAdjacency.WhyTooFar(HeldTreeIds(c, h), t.Id, id => c.TrySkillTree(id)?.Name ?? id, Board(c, h));
            var home = HomeTree(c, h);
            if (home == null) return "No home tree to specialize from";
            if (opened >= TalentConfig.OpenTreeAt.Length)
                return $"Already specialized in {opened} other tree{(opened == 1 ? "" : "s")}";
            int need = TalentConfig.OpenTreeAt[opened];
            string which = opened == 0 ? "a second tree" : opened == 1 ? "a third tree" : "another tree";
            return $"Opening {which} needs {need} pts in {home.Name} ({PointsIn(c, h, home)} now)";
        }

        /// <summary>Skill points this node costs this hero: its <see cref="SkillNodeDef.Cost"/>, plus the off-tree premium.</summary>
        public static int Cost(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n) => n.Cost + Premium(c, h, t);

        /// <summary>The per-node surcharge in this tree: 0 at home; off-tree, GG-T51's flat <see cref="TalentConfig.OffTreePremium"/>
        /// plus <see cref="TreeAdjacency.ExtraPremium(int)"/> for each step past a neighbour (GG-T135).</summary>
        public static int Premium(ContentDb c, HeroSave h, SkillTreeDef t) =>
            IsHome(c, h, t) ? 0 : Math.Max(0, TalentConfig.OffTreePremium) + TreeAdjacency.ExtraPremium(Distance(c, h, t));

        /// <summary>Points in the tree this node needs for this hero: <see cref="SkillNodeDef.RequiredPointsInTree"/>, scaled
        /// up (rounded up) in an off-tree.</summary>
        public static int Required(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n) =>
            IsHome(c, h, t) ? n.RequiredPointsInTree : ScaledRequirement(n.RequiredPointsInTree);

        /// <summary>An off-tree tier's points-in-tree gate.</summary>
        public static int ScaledRequirement(int required) =>
            required <= 0 ? 0 : (int)Math.Ceiling(required * (double)Math.Max(1f, TalentConfig.OffTreeGateScale) - 1e-9);

        public static bool Bought(HeroSave h, SkillTreeDef t, SkillNodeDef n) => h.TreeNodes != null && h.TreeNodes.Contains(Key(t, n));

        public static bool Unlocked(HeroSave h, SkillTreeDef t, SkillNodeDef n) => Bought(h, t, n) || Innate(h, n);

        /// <summary>GG-T978: <see cref="Unlocked(HeroSave, SkillTreeDef, SkillNodeDef)"/>, plus the class's family-tree start
        /// nodes (<see cref="IsStartNode"/>).</summary>
        public static bool Unlocked(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n) => Unlocked(h, t, n) || IsStartNode(c, h, t, n);

        private static bool Innate(HeroSave h, SkillNodeDef n) => !string.IsNullOrEmpty(n.Grants) && h.Known.Contains(n.Grants);

        /// <summary>Points the tree counts toward a node's <see cref="SkillNodeDef.RequiredPointsInTree"/>: every unlocked node's cost.</summary>
        public static int PointsIn(HeroSave h, SkillTreeDef t) => t.Nodes.Where(n => Unlocked(h, t, n)).Sum(n => n.Cost);

        /// <summary>GG-T978: <see cref="PointsIn(HeroSave, SkillTreeDef)"/> with the class's family-tree start nodes counted.</summary>
        public static int PointsIn(ContentDb c, HeroSave h, SkillTreeDef t) => t.Nodes.Where(n => Unlocked(c, h, t, n)).Sum(n => n.Cost);

        /// <summary>Points actually spent (refundable by Respec) across every tree, off-tree premiums included.</summary>
        public static int PointsSpent(ContentDb c, HeroSave h)
        {
            int total = 0;
            foreach (var t in c.SkillTrees.Values)
                foreach (var n in t.Nodes)
                    if (Bought(h, t, n))
                        total += h.TreeNodeCosts != null && h.TreeNodeCosts.TryGetValue(Key(t, n), out var paid) && paid >= 0
                            ? paid : Cost(c, h, t, n);
            return total;
        }

        /// <summary>The hero's home tree. GG-T979: its class's family tree (<see cref="Progression.FamilyTree"/>, named by
        /// <see cref="ClassDef.Family"/>) when the content has one, else the role tree named by <see cref="ClassDef.HomeTree"/>.</summary>
        public static SkillTreeDef HomeTree(ContentDb c, HeroSave h)
        {
            var def = c?.TryClass(h?.ClassId);
            if (def == null) return null;
            return Progression.FamilyTree(c, def) ?? c.TrySkillTree(def.HomeTree);
        }

        /// <summary>The trees the Talents screen shows: home first; then, by name, every off-tree the hero has opened or
        /// already holds a Known node in; then, while an off-tree slot is free (§25.6.2), every other tree it could open.</summary>
        public static List<SkillTreeDef> Trees(ContentDb c, HeroSave h)
        {
            var home = HomeTree(c, h);
            var list = new List<SkillTreeDef>();
            if (home != null) list.Add(home);
            var others = c.SkillTrees.Values.Where(t => t != home).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
            list.AddRange(others.Where(t => t.Nodes.Any(n => Unlocked(c, h, t, n))));
            if (OpenedOffTrees(c, h).Count < OffTreeSlots(c, h))
                list.AddRange(others.Where(t => !list.Contains(t)));
            return list;
        }

        /// <summary>The home tree plus every off-tree the hero has opened or holds a Known node in — the trees a hero is
        /// already "in", without the ones it could newly open. The camp routine spends only here (GG-T54).</summary>
        public static List<SkillTreeDef> HeldTrees(ContentDb c, HeroSave h)
        {
            var home = HomeTree(c, h);
            return Trees(c, h).Where(t => t == home || t.Nodes.Any(n => Unlocked(c, h, t, n))).ToList();
        }

        /// <summary>A node's state using only the tree's own gates (prerequisites and <see cref="SkillNodeDef.RequiredPointsInTree"/>):
        /// right for a hero's home tree. Use <see cref="State(ContentDb, HeroSave, SkillTreeDef, SkillNodeDef)"/> for any tree.</summary>
        public static TalentState State(HeroSave h, SkillTreeDef t, SkillNodeDef n)
        {
            if (Bought(h, t, n)) return TalentState.Purchased;
            if (Innate(h, n)) return TalentState.Innate;
            return WhyLocked(h, t, n) == null ? TalentState.Available : TalentState.Locked;
        }

        /// <summary>A node's state for this hero, with §25.6.2's cross-tree gates applied in an off-tree.</summary>
        public static TalentState State(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n)
        {
            if (Bought(h, t, n)) return TalentState.Purchased;
            if (Innate(h, n) || IsStartNode(c, h, t, n)) return TalentState.Innate; // GG-T978: a start node, stat nodes too
            return WhyLocked(c, h, t, n) == null ? TalentState.Available : TalentState.Locked;
        }

        /// <summary>What still blocks a node by the tree's own gates (missing prerequisites, then points in the tree), or null.
        /// Home-tree rules; see <see cref="WhyLocked(ContentDb, HeroSave, SkillTreeDef, SkillNodeDef)"/> for any tree.</summary>
        public static string WhyLocked(HeroSave h, SkillTreeDef t, SkillNodeDef n) => Gates(null, h, t, n, n.RequiredPointsInTree);

        /// <summary>What still blocks a node for this hero: a closed off-tree first (no free slot), then missing prerequisites,
        /// then points in the tree (scaled up in an off-tree), then (GG-T980) a family node's stat requirement
        /// (<see cref="WhyStatLocked"/>) — or null when every gate is met.</summary>
        public static string WhyLocked(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n) =>
            WhyClosed(c, h, t) ?? Gates(c, h, t, n, Required(c, h, t, n)) ?? WhyStatLocked(c, h, t, n);

        /// <summary>GG-T980: a family-tree node whose grant carries <see cref="AbilityDef.Requires"/> stays Locked until the
        /// hero's skill hexagon stat totals (<see cref="HeroBoard.BoardStats"/>, GG-T1058) meet it, with the same text as the Learn screen (GG-T592):
        /// "needs INT 8 (have 3)". Null when met, waived (<see cref="StatWaived"/>), off a family tree, or nothing is granted.</summary>
        public static string WhyStatLocked(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n)
        {
            if (c == null || h == null || t == null || n == null || string.IsNullOrEmpty(n.Grants) || c.TryFamily(t.Id) == null) return null;
            var a = c.TryAbility(n.Grants);
            if (a == null || !a.HasRequires || StatWaived(c, h, t, n)) return null;
            return Abilities.NeedsText(a.UnmetRequires(HeroBoard.BoardStats(c, h)));
        }

        /// <summary>GG-T980: is this family node free of its grant's stat requirement for this hero? Yes for one of its class's
        /// start nodes (<see cref="IsStartNode"/>), its class's own signature node (<see cref="SkillNodeDef.SignatureOf"/>), or a
        /// grant that is already one of its kit moves (<see cref="Abilities.IsKitMove"/>); a sibling's signature node only when
        /// <see cref="TalentConfig.SiblingSignatureStatGated"/> is off.</summary>
        public static bool StatWaived(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n)
        {
            if (IsStartNode(c, h, t, n)) return true;
            if (!string.IsNullOrEmpty(n.SignatureOf) &&
                (string.Equals(n.SignatureOf, h.ClassId, StringComparison.OrdinalIgnoreCase) || !TalentConfig.SiblingSignatureStatGated))
                return true;
            return Abilities.IsKitMove(c, h, c.TryAbility(n.Grants));
        }

        /// <summary>The tree's own gates; with content (<paramref name="c"/> not null) the class's start nodes count as unlocked (GG-T978).</summary>
        private static string Gates(ContentDb c, HeroSave h, SkillTreeDef t, SkillNodeDef n, int required)
        {
            var missing = n.Prerequisites
                .Select(id => t.Nodes.FirstOrDefault(x => x.Id == id))
                .Where(p => p != null && !Unlocked(c, h, t, p))
                .Select(p => p.Name ?? p.Id).ToList();
            if (missing.Count > 0) return "Needs " + string.Join(" + ", missing);
            int have = PointsIn(c, h, t);
            if (have < required) return $"Needs {required} pts in {t.Name} ({have} now)";
            return null;
        }

        /// <summary>Spends <see cref="Cost"/> skill points on a node: its grant becomes Known. The first node bought in an
        /// off-tree opens that tree (§25.6.2).</summary>
        public static Result Unlock(ContentDb c, SaveData s, HeroSave h, string treeId, string nodeId)
        {
            var t = c.TrySkillTree(treeId);
            var n = t?.Nodes.FirstOrDefault(x => x.Id == nodeId);
            if (h == null || n == null) return Result.Fail("No such talent");
            var state = State(c, h, t, n);
            if (state == TalentState.Purchased || state == TalentState.Innate) return Result.Fail("Already learned");
            if (state == TalentState.Locked) return Result.Fail(WhyLocked(c, h, t, n));
            int cost = Cost(c, h, t, n);
            if (h.SkillPoints < cost) return Result.Fail("Not enough skill points");
            h.SkillPoints -= cost;
            if (h.TreeNodes == null) h.TreeNodes = new List<string>();
            h.TreeNodes.Add(Key(t, n));
            if (h.TreeNodeCosts == null) h.TreeNodeCosts = new Dictionary<string, int>();
            h.TreeNodeCosts[Key(t, n)] = cost;
            if (!string.IsNullOrEmpty(n.Grants) && !h.Known.Contains(n.Grants)) { h.Known.Add(n.Grants); PassiveLoadout.OnLearned(c, h, n.Grants); Codex.Unlock(s, CodexKinds.Ability, n.Grants); }
            var a = c.TryAbility(n.Grants);
            return Result.Success($"Learned {a?.Name ?? n.Name ?? n.Id}");
        }

        /// <summary>Respec price at the Inn: grows with the hero's level.</summary>
        public static int RespecCost(HeroSave h) => RespecBaseGold + RespecGoldPerLevel * Progression.Level(h);

        /// <summary>Why the Inn can't respec right now (no Innkeeper yet / on the road without a Medic Wagon), or null.</summary>
        public static string RespecClosed(SaveData s)
        {
            if (!CampUnlocks.NpcOpen(s, CampNpcs.Innkeeper)) return "The Innkeeper hasn't joined yet";
            if (!Expeditions.InnOpen(s)) return "On the road — the Inn opens when the expedition ends";
            return null;
        }

        /// <summary>The Inn's Respec: pays <see cref="RespecCost"/> gold, refunds every bought node's points and forgets what
        /// those nodes granted (clearing them off the battle bar). Innate nodes — the starting kit, gold-Learned abilities — stay.</summary>
        /// <remarks>GG-T566: a respec can take away a proficiency passive (Heavy Armor, Martial Weapons, Arcane Focus) the
        /// hero's equipped gear depends on. <see cref="GearLostOnRespec"/> lists those pieces; while there are any, Respec
        /// refuses unless <paramref name="unequipUnusable"/> is true, in which case it takes them off into the bag. The UI
        /// warns and asks first — the hero never keeps gear it can no longer use.</remarks>
        public static Result Respec(ContentDb c, SaveData s, HeroSave h, bool unequipUnusable = false)
        {
            if (h == null) return Result.Fail("No hero");
            var closed = RespecClosed(s);
            if (closed != null) return Result.Fail(closed);
            int refund = PointsSpent(c, h);
            if (refund == 0) return Result.Fail("No talent points spent yet");
            int cost = RespecCost(h);
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            var lost = GearLostOnRespec(c, h);
            if (lost.Count > 0 && !unequipUnusable)
                return Result.Fail("Respec would leave gear this hero can't use: " + string.Join(", ", lost.Select(g => c.TryItem(g.ItemId)?.Name ?? g.ItemId)));
            s.Gold -= cost;
            var forget = RespecForgets(c, h);
            h.TreeNodes.Clear();
            h.TreeNodeCosts?.Clear();
            h.Known.RemoveAll(forget.Contains);
            h.ActivePassives?.RemoveAll(forget.Contains); // GG-T301
            for (int i = 0; i < h.Bar.Count; i++)
                if (h.Bar[i] != null && forget.Contains(h.Bar[i])) h.Bar[i] = null;
            h.SkillPoints += refund;
            int stowed = 0;
            foreach (var (slot, _) in lost) if (Equipment.Unequip(c, s, h, slot).Ok) stowed++;
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            return Result.Success($"{hero}: {refund} skill point{(refund == 1 ? "" : "s")} refunded" +
                (stowed > 0 ? $" · {stowed} piece{(stowed == 1 ? "" : "s")} of gear moved to the bag" : ""));
        }

        /// <summary>The abilities a respec would forget: everything a bought (non-innate) node granted. GG-T978: never what
        /// the class's family-tree start nodes grant, even if another tree's node granting the same move was bought.</summary>
        private static HashSet<string> RespecForgets(ContentDb c, HeroSave h)
        {
            var forget = new HashSet<string>();
            foreach (var t in c.SkillTrees.Values)
                foreach (var n in t.Nodes)
                    if (Bought(h, t, n) && !string.IsNullOrEmpty(n.Grants)) forget.Add(n.Grants);
            forget.ExceptWith(Progression.StartGrants(c, c.TryClass(h.ClassId)));
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
            foreach (var (slot, id) in EquippedGated(h))
                if (Equipment.ProficiencyGate(c, h, c.TryItem(id)) == null && Equipment.ProficiencyGate(c, after, c.TryItem(id)) != null)
                    lost.Add((slot, id));
            return lost;
        }

        private static List<(EquipmentSlot Slot, string ItemId)> EquippedGated(HeroSave h)
        {
            var list = new List<(EquipmentSlot, string)>();
            if (h.WeaponId != null) list.Add((EquipmentSlot.Weapon, h.WeaponId));
            foreach (var kv in h.Armor)
                if (kv.Value?.ItemId != null) list.Add((kv.Key, kv.Value.ItemId));
            return list;
        }
    }
}
