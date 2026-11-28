using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The skill tree checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class SkillTreeValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            // An alias (GG-T973) is reported by ValidateAbilityAliases with the id to use instead.
            void NeedAbility(string id, string where) { if (id != null && !db.Abilities.ContainsKey(id) && !db.AbilityAliases.ContainsKey(id)) p.Add($"{where}: unknown ability '{id}'"); }
            // Skill trees (§25.6.1, GG-T50): every node's grant and prerequisites must resolve. GG-T1093: the talent trees
            // are retired (skilltrees.json is empty, no class names a home tree); a tree in an old file still checks.
            // GG-T213: node ids are global (a learned node is saved by id alone), so a clash across trees is rejected too.
            var allNodeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in db.SkillTrees.Values)
            {
                var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var n in t.Nodes)
                {
                    if (string.IsNullOrEmpty(n.Id)) { p.Add($"tree {t.Id}: node with empty Id"); continue; }
                    if (!nodeIds.Add(n.Id)) p.Add($"tree {t.Id}: duplicate node id '{n.Id}'");
                    else if (allNodeIds.TryGetValue(n.Id, out var otherTree)) p.Add($"tree {t.Id}: node id '{n.Id}' is already used in tree {otherTree}");
                    else allNodeIds[n.Id] = t.Id;
                }
                foreach (var n in t.Nodes)
                {
                    // GG-T77: a node grants either an ability/passive or a flat stat bonus, never both or neither.
                    if (n.GrantsStat != null)
                    {
                        if (!string.IsNullOrEmpty(n.Grants)) p.Add($"tree {t.Id} node {n.Id}: Grants and GrantsStat are mutually exclusive");
                        if (!StatGrantDef.IsStatName(n.GrantsStat.Stat)) p.Add($"tree {t.Id} node {n.Id}: unknown stat '{n.GrantsStat.Stat}' (use {string.Join("/", StatGrantDef.Names)})");
                        if (n.GrantsStat.Amount <= 0f) p.Add($"tree {t.Id} node {n.Id}: stat bonus must be positive");
                    }
                    else if (string.IsNullOrEmpty(n.Grants)) p.Add($"tree {t.Id} node {n.Id}: needs Grants (an ability) or GrantsStat (a stat bonus)");
                    else NeedAbility(n.Grants, $"tree {t.Id} node {n.Id}");
                    if (n.Cost <= 0) p.Add($"tree {t.Id} node {n.Id}: cost must be positive");
                    if (n.RequiredPointsInTree < 0) p.Add($"tree {t.Id} node {n.Id}: required points can't be negative");
                    foreach (var req in n.Prerequisites)
                    {
                        if (!nodeIds.Contains(req)) p.Add($"tree {t.Id} node {n.Id}: unknown prerequisite '{req}'");
                        else if (req == n.Id) p.Add($"tree {t.Id} node {n.Id}: can't require itself");
                    }
                }
                // Cycle check: walk each node's prerequisite chain and fail only if it revisits a node
                // already on the *current* path. Two branches sharing a common ancestor (e.g. a finale
                // node requiring two capstones that both trace back to the same root) is a valid diamond,
                // not a cycle — tracking "seen anywhere" instead of "seen on this path" flagged those too.
                foreach (var n in t.Nodes)
                {
                    var onPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (HasCycle(n)) p.Add($"tree {t.Id} node {n.Id}: prerequisite cycle");

                    bool HasCycle(SkillNodeDef cur)
                    {
                        if (!onPath.Add(cur.Id)) return true;
                        foreach (var req in cur.Prerequisites)
                        {
                            var reqNode = t.Nodes.Find(x => string.Equals(x.Id, req, StringComparison.OrdinalIgnoreCase));
                            if (reqNode != null && HasCycle(reqNode)) return true;
                        }
                        onPath.Remove(cur.Id);
                        return false;
                    }
                }
            }
        }
    }
}
