using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The hero and enemy class checks: the class rows themselves, their Supporter effects and how each joins the roster. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class ClassValidator
    {
        internal static void Validate(ContentDb db, Func<string, bool> portraitExists, List<string> p)
        {
            void NeedClass(string id, string where) { if (id != null && !db.Classes.ContainsKey(id)) p.Add($"{where}: unknown class '{id}'"); }
            void NeedItem(string id, string where) { if (id != null && !db.Items.ContainsKey(id)) p.Add($"{where}: unknown item '{id}'"); }
            // An alias (GG-T973) is reported by ValidateAbilityAliases with the id to use instead.
            void NeedAbility(string id, string where) { if (id != null && !db.Abilities.ContainsKey(id) && !db.AbilityAliases.ContainsKey(id)) p.Add($"{where}: unknown ability '{id}'"); }
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }
            foreach (var c in db.Classes.Values)
            {
                if (c.DropTable != null && !db.DropTables.ContainsKey(c.DropTable)) p.Add($"class {c.Id}: unknown drop table '{c.DropTable}'");
                // GG-T1063: Luck is flat per level (Formulas.LuckAtLevel, GG-T1028), never a class stat.
                if (c.BaseStats != null && c.BaseStats.Luck != 0f) p.Add($"class {c.Id}: BaseStats Luck {c.BaseStats.Luck} (Luck is flat per level, not a class stat)");
                if (c.Growth != null && c.Growth.Luck != 0f) p.Add($"class {c.Id}: Growth Luck {c.Growth.Luck} (Luck is flat per level, not a class stat)");
                if (c.Milestones != null)
                    foreach (var m in c.Milestones)
                        if (m?.Growth != null && m.Growth.Luck != 0f) p.Add($"class {c.Id}: milestone {m.Level} Luck {m.Growth.Luck} (Luck is flat per level, not a class stat)");
                var th = c.Thumbnail;
                if (th == null) p.Add($"class {c.Id}: no Thumbnail (frame it in tools/ThumbnailStudio)");
                else if (!(th.Zoom >= Thumbnail.MinZoom && th.Zoom <= Thumbnail.MaxZoom) || !(th.X >= Thumbnail.MinOffset && th.X <= Thumbnail.MaxOffset)
                         || !(th.Y >= Thumbnail.MinOffset && th.Y <= Thumbnail.MaxOffset))
                    p.Add($"class {c.Id}: Thumbnail out of range (Zoom {Thumbnail.MinZoom}..{Thumbnail.MaxZoom}, X/Y {Thumbnail.MinOffset}..{Thumbnail.MaxOffset})");
                if (c.Figure == null) p.Add($"class {c.Id}: no Figure (tools/import_art.py writes it when the portrait is imported)");
                else if (!c.Figure.IsValid) p.Add($"class {c.Id}: Figure must be a box with area inside the portrait (fractions 0..1)");
                // GG-T532: the male art's own framing is optional (it falls back to the female one), but must be sane when set.
                if (c.MaleFigure != null && !c.MaleFigure.IsValid) p.Add($"class {c.Id}: MaleFigure must be a box with area inside the portrait (fractions 0..1)");
                // GG-T557/GG-T561: portrait files follow the asset naming rule, and exist when the caller can look.
                foreach (var male in c.HasMalePortrait ? new[] { false, true } : new[] { false })
                {
                    string file;
                    try { file = c.PortraitFile(male); }
                    catch (ArgumentOutOfRangeException) { p.Add($"class {c.Id}: PortraitVariant {c.PortraitVariant} out of 0..99"); continue; }
                    if (!AssetNames.IsValid(file)) p.Add($"class {c.Id}: portrait '{file}' doesn't follow the asset naming rule (word-word-00)");
                    else if (portraitExists != null && !portraitExists(file)) p.Add($"class {c.Id}: missing portrait Portraits/{file}");
                }
                var mth = c.MaleThumbnail;
                if (mth != null && (!(mth.Zoom >= Thumbnail.MinZoom && mth.Zoom <= Thumbnail.MaxZoom) || !(mth.X >= Thumbnail.MinOffset && mth.X <= Thumbnail.MaxOffset)
                                    || !(mth.Y >= Thumbnail.MinOffset && mth.Y <= Thumbnail.MaxOffset)))
                    p.Add($"class {c.Id}: MaleThumbnail out of range (Zoom {Thumbnail.MinZoom}..{Thumbnail.MaxZoom}, X/Y {Thumbnail.MinOffset}..{Thumbnail.MaxOffset})");
                foreach (var a in c.Abilities) NeedAbility(a, $"class {c.Id} abilities");
                foreach (var s in c.DefaultLoadout)
                {
                    if (s == null) { p.Add($"class {c.Id} loadout: null entry"); continue; }
                    if (s.StartsWith("item:")) NeedItem(s.Substring(5), $"class {c.Id} loadout");
                    else NeedAbility(s, $"class {c.Id} loadout");
                }
                NeedAbility(c.ChargeAbility, $"class {c.Id} charge");
                if (c.Trap != null && !db.Traps.ContainsKey(c.Trap)) p.Add($"class {c.Id}: unknown trap '{c.Trap}'");
                if (c.Trail != null && !db.Traps.ContainsKey(c.Trail)) p.Add($"class {c.Id}: unknown trail '{c.Trail}'");
                NeedBuff(c.OnHitBuff, $"class {c.Id} on-hit buff");
                if (c.OnHitTurns < 0 || (c.OnHitTurns > 0 && string.IsNullOrEmpty(c.OnHitBuff))) p.Add($"class {c.Id}: OnHitTurns {c.OnHitTurns} needs an OnHitBuff and can't be negative (GG-T904)");
                NeedClass(c.Remains, $"class {c.Id} remains");
                NeedClass(c.LaysEgg, $"class {c.Id} egg");
                if (c.TameBond < 0) p.Add($"class {c.Id}: TameBond can't be negative (GG-T303)");
                if (c.PlateUnyielding && c.ArmorPlate <= 0) p.Add($"class {c.Id}: PlateUnyielding needs an ArmorPlate (GG-T2069)");
                foreach (var ph in c.BossPhases) NeedAbility(ph.OnEnterAbility, $"class {c.Id} boss phase");
            }
        }

        internal static void ValidateSupports(ContentDb db, List<string> p)
        {
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }
            foreach (var c in db.Classes.Values)
            {
                if (c.Team == Team.Hero && (c.Role == null || !HeroRoles.Contains(c.Role))) p.Add($"class {c.Id}: hero needs a Role (Tank/Healer/Mage/Striker/Support/Ranged), has '{c.Role}'");
                // GG-T306 (§14.1.3): pincer-capable foes engineer pincers with their kin as supporters, and any monster can be
                // tamed into the party's chains, so every enemy class brings its own support (no silent default strike).
                if (c.Team == Team.Enemy && c.Support == null) p.Add($"class {c.Id}: an enemy needs its own Support (every monster supports a pincer)");
                // GG-T187 (§1.2.3): a class's support action must be playable.
                if (c.Support != null)
                {
                    // Reach 0 would read as unlimited to PincerDetector yet "reach 1" on the card: at least 1.
                    if (c.Support.Range < 1) p.Add($"class {c.Id}: support range must be at least 1");
                    if (c.Support.Magnitude < 0f) p.Add($"class {c.Id}: support magnitude can't be negative");
                    if (c.Support.Effect == SupportEffect.Buff)
                    {
                        if (string.IsNullOrEmpty(c.Support.Buff)) p.Add($"class {c.Id}: a Buff support needs a Buff id");
                        else NeedBuff(c.Support.Buff, $"class {c.Id} support");
                    }
                    if ((c.Support.Effect == SupportEffect.Heal || c.Support.Effect == SupportEffect.Haste || c.Support.Effect == SupportEffect.Mana) && c.Support.Magnitude <= 0f)
                        p.Add($"class {c.Id}: a {c.Support.Effect} support needs a positive magnitude");
                    ValidateSupport(db, c, p); // GG-T298: Crit / Cast / Status / Pushback, Haste in seconds
                }
            }
        }

        internal static void ValidateRoster(ContentDb db, List<string> p)
        {
            // GG-T1416: every hero class says how it joins the roster.
            foreach (var c in db.Classes.Values)
                if (c.Team == Team.Hero && c.Acquire == Acquire.None) p.Add($"class {c.Id}: a hero class needs Acquire set");
            // GG-T1037: a cut class becomes a placed hero, and nobody meets it again (summon pool, guests, recruitables).
            foreach (var c in db.Classes.Values)
            {
                if (!c.IsCut) continue;
                if (c.Team != Team.Hero) p.Add($"class {c.Id}: only a hero class can be cut");
                if (!db.Classes.TryGetValue(c.CutInto, out var into) || into.Team != Team.Hero || into.IsCut)
                    p.Add($"class {c.Id}: CutInto '{c.CutInto}' must be a hero class still in play");
            }
            bool Cut(string id) => id != null && db.Classes.TryGetValue(id, out var cd) && cd.IsCut;
            foreach (var c in db.Classes.Values) if (c.IsCut && c.Acquire == Acquire.Summon) p.Add($"class {c.Id}: a cut class can't be Acquire Summon (GG-T1037)");
            foreach (var st in db.Stages.Values)
                foreach (var w in st.Waves)
                {
                    foreach (var g in w.Guests) if (Cut(g?.Class)) p.Add($"stage {st.Id}: guest '{g.Class}' is a cut class (GG-T1037)");
                    foreach (var r in w.Recruitables) if (Cut(r?.Class)) p.Add($"stage {st.Id}: recruitable '{r.Class}' is a cut class (GG-T1037)");
                }
        }

        /// <summary>GG-T298 (§1.2.3): the widened support effects stay in their sane ranges, and the generated "As a
        /// Supporter" sentence only uses glossary terms.</summary>
        private static void ValidateSupport(ContentDb db, ClassDef c, List<string> p)
        {
            var s = c.Support;
            float m = s.Magnitude;
            string w = $"class {c.Id} support";
            switch (s.Effect)
            {
                case SupportEffect.Haste:
                    if (m > SupportDef.MaxHasteSeconds) p.Add($"{w}: Haste {m}s is over {SupportDef.MaxHasteSeconds}s (seconds sooner, not a bar fraction)");
                    break;
                case SupportEffect.Crit:
                    if (!(m > 0f && m <= SupportDef.MaxCritBonus)) p.Add($"{w}: Crit {m} outside (0, {SupportDef.MaxCritBonus}] (crit chance fraction)");
                    break;
                case SupportEffect.Cast:
                    if (!(m > 0f && m <= SupportDef.MaxCastPower)) p.Add($"{w}: Cast power {m} outside (0, {SupportDef.MaxCastPower}]");
                    bool any = false;
                    foreach (var id in c.Abilities)
                        if (db.TryAbility(id) is AbilityDef a && a.Kind == AbilityKind.Spell && a.BaseDamage > 0 && a.Filter != TargetFilter.AllyOnly) any = true;
                    if (!any) p.Add($"{w}: a Cast support needs a damaging spell in the class kit");
                    break;
                case SupportEffect.Status:
                    if (!(m > 0f && m <= 1f)) p.Add($"{w}: Status chance {m} outside (0, 1]");
                    if (string.IsNullOrEmpty(s.Buff)) p.Add($"{w}: a Status support needs a Buff id");
                    else if (db.TryBuff(s.Buff) is BuffDef b) { if (b.Kind != BuffKind.Debuff) p.Add($"{w}: Status {s.Buff} is not a debuff"); }
                    else p.Add($"{w}: unknown status {s.Buff}");
                    break;
                case SupportEffect.Volley:
                    // GG-T834: a thrown hit, plus an optional status rider that only rolls where the hit lands.
                    if (!(m > 0f && m <= 1f)) p.Add($"{w}: Volley damage {m} outside (0, 1]");
                    if (string.IsNullOrEmpty(s.Buff)) { if (s.Chance != 0f) p.Add($"{w}: a Volley Chance needs a Buff id"); }
                    else
                    {
                        if (!(s.Chance > 0f && s.Chance <= 1f)) p.Add($"{w}: Volley chance {s.Chance} outside (0, 1]");
                        if (db.TryBuff(s.Buff) is BuffDef vb) { if (vb.Kind != BuffKind.Debuff) p.Add($"{w}: Volley status {s.Buff} is not a debuff"); }
                        else p.Add($"{w}: unknown status {s.Buff}");
                    }
                    break;
                case SupportEffect.Pushback:
                    if (!(m > 0f && m <= Battle.BattleConfig.PushbackMaxSeconds)) p.Add($"{w}: Pushback {m}s outside (0, {Battle.BattleConfig.PushbackMaxSeconds}]s");
                    break;
                case SupportEffect.Mana:
                    // GG-T307: the mana meter is the party's; an enemy supporter would charge nothing.
                    if (c.Team != Team.Hero) p.Add($"{w}: a Mana support is hero-only (enemy {c.Id} would do nothing)");
                    break;
            }
            if (s.Chance != 0f && s.Effect != SupportEffect.Volley) p.Add($"{w}: Chance is only read by a Volley support");
            if (s.Projectile != null && db.VfxPrefabs != null && db.VfxPrefabs.Find(s.Projectile) == null)
                p.Add($"{w}: Projectile '{s.Projectile}' is not in vfx.json");
            if (s.Sfx != null && !SupportSfxCues.Contains(s.Sfx))
                p.Add($"{w}: Sfx '{s.Sfx}' is not a battle/element/status cue in the sound taxonomy (Resources/Audio/Sfx)");
            if (db.GlossaryIndex.Count > 0)
                foreach (var t in Glossary.Terms(Rules.SupportText.Describe(db, s)))
                    if (db.TryTerm(t) == null) p.Add($"{w}: unknown glossary term [{t}] in its supporter sentence");
        }
    }
}
