using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The bounty checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class BountyValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            void NeedClass(string id, string where) { if (id != null && !db.Classes.ContainsKey(id)) p.Add($"{where}: unknown class '{id}'"); }
            void NeedItem(string id, string where) { if (id != null && !db.Items.ContainsKey(id)) p.Add($"{where}: unknown item '{id}'"); }
            foreach (var b in db.Bounties.Values)
            {
                // GG-T431: a marked target is a real enemy class on a real campaign stage, in a wave that exists.
                NeedClass(b.TargetClass, $"bounty {b.Id}"); NeedItem(b.RewardItem, $"bounty {b.Id}");
                if (string.IsNullOrWhiteSpace(b.Name)) p.Add($"bounty {b.Id}: needs a title (Name)");
                if (db.Classes.TryGetValue(b.TargetClass ?? "", out var bc))
                {
                    if (bc.Team != Team.Enemy) p.Add($"bounty {b.Id}: target '{b.TargetClass}' is not an enemy class");
                    if (bc.SnakeSegments > 0) p.Add($"bounty {b.Id}: target '{b.TargetClass}' is a snake (segments can't carry the mark)");
                }
                if (b.Stage == null || !db.Stages.TryGetValue(b.Stage, out var bs) || bs.IsTest || db.ThemeOf(b.Stage) == null) p.Add($"bounty {b.Id}: unknown campaign stage '{b.Stage}' (must be a real stage in a theme)");
                else if (b.Wave < 0 || b.Wave >= bs.Waves.Count) p.Add($"bounty {b.Id}: wave {b.Wave} is not a wave of {b.Stage}");
                if (b.RewardSkillPoints < 0) p.Add($"bounty {b.Id}: skill points can't be negative");
            }
        }
    }
}
