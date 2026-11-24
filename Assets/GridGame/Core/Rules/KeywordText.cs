using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Core.Rules
{
    /// <summary>
    /// GG-T907: the words the cards print for the battle-feel keywords (Heavy, Floating, Rooted, Shredded, Frightened,
    /// Linked, Volley, the tactical tiles). <see cref="Buff"/> is what a status does, built from its mechanics so it never
    /// drifts from the numbers (Ui.BuffEffect: the InspectCard, the battle tooltip): plain words, no [brackets], since
    /// some callers print it on a plain label. The Heavy lines are bracketed for <c>GlossaryText</c> (the unit card and
    /// the Bestiary). <see cref="Keywords"/> are the terms every content set with a glossary must define
    /// (<see cref="Glossary.Validate"/>).
    /// </summary>
    public static class KeywordText
    {
        /// <summary>Glossary terms the cards and generated sentences use that no buff name brings in on its own.</summary>
        public static readonly string[] Keywords = { "Heavy", "Floating", "Rooted", "Shredded", "Frightened", "Linked", "Volley", "Firewall", "Mine", "Hidden", "Oil", "Slime Glob" };

        /// <summary>GG-T892: the Heavy keyword line (§1.1.3, §14.2) the unit card and the Bestiary show, with its counters.</summary>
        public const string HeavyLine = "[Heavy]: no drag, shove, pull or swap moves it. Counters: [Floating] lifts it so it can be moved; [Rooted] stops its own legs; a barricade walls it out; Earthsplitter's cracks break under it and it falls.";

        /// <summary>GG-T892: a Heavy unit while it is <see cref="Unit.IsFloating"/>.</summary>
        public const string HeavyFloatingLine = "[Heavy], but [Floating]: while it hovers it can be dragged, shoved, pulled or swapped like anyone else.";

        /// <summary>What a buff does, in words, lower-case, no full stop ("can't move or be moved, but still acts").</summary>
        public static string Buff(BuffDef d)
        {
            if (d == null) return "";
            var parts = new List<string>();
            if (d.DamagePerTick > 0) parts.Add($"{d.DamagePerTick} damage each tick");
            if (d.HealPerTick > 0) parts.Add($"heals {d.HealPerTick} each tick");
            if (d.Immobile) parts.Add("can't move or act");
            if (d.Roots) parts.Add("can't move or be moved, but still acts"); // GG-T920
            if (d.Heavy) parts.Add("Heavy: no shove, pull or swap moves it"); // GG-T882
            if (d.Braces) parts.Add("allies beside it can't be moved by foes"); // GG-T882
            if (d.EndsOnAction) parts.Add("until its next action"); // GG-T882
            if (d.Frightens) parts.Add("flees what scared it; won't stop beside it"); // GG-T917
            if (string.Equals(d.Id, Unit.FloatingBuffId, StringComparison.OrdinalIgnoreCase))
                parts.Add("hovers: can be moved even if Heavy, passes over floor traps and mines, can't be Rooted; tall flames still burn it");
            if (d.Rages) parts.Add("counts as Heavy, can't be frightened, and Pushback can't set it back");
            if (d.Links) parts.Add($"{Pct(BattleConfig.LinkEchoFraction)} of any harm to it echoes to its linked partner, never back");
            if (d.Intercepts) parts.Add($"the next blow on it falls on its guardian within {BattleConfig.GuardReach} tiles");
            if (d.Taunts) parts.Add("foes may only attack taunting heroes");
            if (d.HaltsTimeline) parts.Add("timeline halted");
            if (d.BlocksSpells) parts.Add("can't cast spells");
            if (d.OutgoingDamageMultiplier > 1f) parts.Add($"deals {Pct(d.OutgoingDamageMultiplier - 1f)} more damage");
            if (d.OutgoingDamageMultiplier < 1f) parts.Add($"deals {Pct(1f - d.OutgoingDamageMultiplier)} less damage");
            if (d.IncomingDamageMultiplier < 1f) parts.Add($"takes {Pct(1f - d.IncomingDamageMultiplier)} less damage");
            if (d.IncomingDamageMultiplier > 1f) parts.Add($"takes {Pct(d.IncomingDamageMultiplier - 1f)} more damage");
            if (d.PhysicalDamageTakenBonus > 0f) parts.Add($"takes {Pct(d.PhysicalDamageTakenBonus)} more physical damage per stack"); // GG-T857
            if (d.PlateChip > 0) parts.Add($"chips {d.PlateChip} off its armor plate from any side");
            if (d.TimelineSpeedMultiplier < 1f) parts.Add("acts slower");
            if (d.TimelineSpeedMultiplier > 1f) parts.Add("acts faster");
            if (d.Tempo < 0) parts.Add($"on a hero: party Tempo {d.Tempo} (foes act sooner)");
            if (d.Tempo > 0) parts.Add($"on a hero: party Tempo +{d.Tempo}");
            if (d.AccuracyMultiplier < 1f) parts.Add("attacks miss more");
            if (d.EvasionBonus != 0f) parts.Add(FormattableString.Invariant($"Evasion {(d.EvasionBonus > 0f ? "+" : "")}{d.EvasionBonus:0}")); // GG-T297
            if (d.BreaksOnDamage) parts.Add("ends when hit");
            if (d.BreaksOnMove) parts.Add("ends when moved");
            return parts.Count == 0 ? (d.Kind == BuffKind.Buff ? "beneficial" : "harmful") : string.Join(", ", parts);
        }

        private static string Pct(float f) => $"{(int)Math.Round(f * 100f)}%";
    }
}
