using System;
using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>A hero family (families.json, GG-T971, BIBLE §25.6.6): a loose collection of hero classes that share a
    /// fantasy, a role and a kit, so two heroes of one family can spec into the same moves. Every
    /// <see cref="Team.Hero"/> class names exactly one family in <see cref="ClassDef.Family"/>; the class list of a
    /// family is derived from that field (<see cref="ContentDb.FamilyMembers"/>), never written twice.</summary>
    public sealed class FamilyDef
    {
        public string Id;
        /// <summary>As the game says it: "Shadow".</summary>
        public string Name;
        /// <summary>One line: what every member of the family does.</summary>
        public string Theme;
        /// <summary>GG-T1060: the skill-hexagon wedge the family owns, as hex stat ids
        /// (<see cref="SkillHexContent.StatIds"/>): it runs from corner From to the next corner To, the same pair as the
        /// skillhex.json family of the same Id (<see cref="SkillHexFamilyDef"/>).</summary>
        public string From;
        public string To;
        /// <summary>1-based place on the family ring (neighbours are kin; the last wraps to the first), like the skill
        /// tree ring of §25.6.2.2. Every family has its own slot and the slots run 1..N with no gaps; Ring k is
        /// skillhex.json's Families[k-1].</summary>
        public int Ring;
        /// <summary>Why a family with a single member is allowed to stand alone; required then, ignored otherwise.</summary>
        public string SoloReason;
    }

    public static class FamilyContent
    {
        /// <summary>families.json and every hero's <see cref="ClassDef.Family"/>: no duplicate or empty ids, a name and
        /// a theme, hex stat ids From/To matching skillhex.json's family at the same ring slot, ring slots 1..N each used once, every hero in exactly one
        /// known family, no enemy naming one, no family without members, and a single-member family only with a
        /// <see cref="FamilyDef.SoloReason"/>. A hand-built ContentDb with no families (tests, tools) skips the checks;
        /// a real load without families.json is reported (<see cref="ContentDb.FamiliesFileMissing"/>).</summary>
        public static void Validate(ContentDb db, List<string> p)
        {
            if (db.FamiliesFileMissing) { p.Add("families.json is missing (every hero class names a family, GG-T971)"); return; }
            if (db.Families.Count == 0) return;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rings = new HashSet<int>();
            foreach (var f in db.Families)
            {
                if (f == null || string.IsNullOrWhiteSpace(f.Id)) { p.Add("family: missing Id"); continue; }
                var w = $"family {f.Id}";
                if (!ids.Add(f.Id)) p.Add($"{w}: duplicate id");
                if (string.IsNullOrWhiteSpace(f.Name)) p.Add($"{w}: needs a Name");
                if (string.IsNullOrWhiteSpace(f.Theme)) p.Add($"{w}: needs a Theme line");
                if (Array.IndexOf(SkillHexContent.StatIds, f.From) < 0) p.Add($"{w}: unknown From stat '{f.From}' (use {string.Join("/", SkillHexContent.StatIds)})");
                if (Array.IndexOf(SkillHexContent.StatIds, f.To) < 0) p.Add($"{w}: unknown To stat '{f.To}' (use {string.Join("/", SkillHexContent.StatIds)})");
                else if (string.Equals(f.From, f.To, StringComparison.Ordinal)) p.Add($"{w}: From and To are the same stat");
                if (f.Ring < 1 || f.Ring > db.Families.Count) p.Add($"{w}: Ring {f.Ring} outside 1..{db.Families.Count}");
                else if (!rings.Add(f.Ring)) p.Add($"{w}: Ring {f.Ring} already taken");
                else MatchHex(db.SkillHex, f, w, p);
                int members = db.FamilyMembers(f.Id).Count;
                if (members == 0) p.Add($"{w}: no hero class belongs to it");
                else if (members == 1 && string.IsNullOrWhiteSpace(f.SoloReason)) p.Add($"{w}: only one member; merge it or give a SoloReason");
            }
            foreach (var c in db.Classes.Values)
            {
                if (c.Team != Team.Hero)
                {
                    if (!string.IsNullOrEmpty(c.Family)) p.Add($"class {c.Id}: only a hero class names a Family");
                    continue;
                }
                if (string.IsNullOrEmpty(c.Family)) p.Add($"class {c.Id}: hero needs a Family");
                else if (db.TryFamily(c.Family) == null) p.Add($"class {c.Id}: unknown family '{c.Family}'");
            }
        }

        /// <summary>GG-T1060: a family at Ring k is skillhex.json's Families[k-1]: same Id, same From and To. Skipped when
        /// the board is absent (hand-built ContentDb, tools).</summary>
        private static void MatchHex(SkillHexDef hex, FamilyDef f, string w, List<string> p)
        {
            if (hex?.Families == null || hex.Families.Count == 0) return;
            if (f.Ring > hex.Families.Count) { p.Add($"{w}: Ring {f.Ring} has no skillhex.json family (it has {hex.Families.Count})"); return; }
            var h = hex.Families[f.Ring - 1];
            if (h == null) return;
            if (!string.Equals(h.Id, f.Id, StringComparison.OrdinalIgnoreCase))
                p.Add($"{w}: Ring {f.Ring} is skillhex.json family '{h.Id}'; families.json must list the hex families in the same order");
            else if (!string.Equals(h.From, f.From, StringComparison.Ordinal) || !string.Equals(h.To, f.To, StringComparison.Ordinal))
                p.Add($"{w}: runs {f.From}->{f.To}, skillhex.json has {h.From}->{h.To}");
        }
    }
}
