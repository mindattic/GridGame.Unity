using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The abilities.json checks, triggers included. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class AbilityValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }
            foreach (var a in db.Abilities.Values)
            {
                NeedBuff(a.ApplyBuff, $"ability {a.Id}"); NeedBuff(a.RemovesBuff, $"ability {a.Id} removes");
                if (!(a.Pushback >= 0f) || a.Pushback > Battle.BattleConfig.PushbackMaxSeconds)
                    p.Add($"ability {a.Id}: Pushback {a.Pushback}s outside 0..{Battle.BattleConfig.PushbackMaxSeconds}s (GG-T286)");
                if (a.BonusVsBuffs == null) p.Add($"ability {a.Id}: BonusVsBuffs can't be null (GG-T1220)");
                else
                {
                    foreach (var b in a.BonusVsBuffs) NeedBuff(b ?? "(null)", $"ability {a.Id} BonusVsBuffs");
                    if (!(a.BonusVsBuffsMultiplier > 0f)) p.Add($"ability {a.Id}: BonusVsBuffsMultiplier {a.BonusVsBuffsMultiplier} must be above 0 (GG-T1220)");
                    if (!(a.BonusVsBuffsPushback >= 0f) || a.Pushback + a.BonusVsBuffsPushback > Battle.BattleConfig.PushbackMaxSeconds)
                        p.Add($"ability {a.Id}: Pushback + BonusVsBuffsPushback {a.Pushback + a.BonusVsBuffsPushback}s outside 0..{Battle.BattleConfig.PushbackMaxSeconds}s (GG-T1220)");
                    if (a.BonusVsBuffs.Count == 0 && (a.BonusVsBuffsMultiplier != 1f || a.BonusVsBuffsPushback > 0f))
                        p.Add($"ability {a.Id}: BonusVsBuffsMultiplier/BonusVsBuffsPushback need a BonusVsBuffs list (GG-T1220)");
                    else if (a.BonusVsBuffs.Count > 0 && a.BaseDamage <= 0 && a.BonusVsBuffsMultiplier != 1f)
                        p.Add($"ability {a.Id}: BonusVsBuffsMultiplier needs BaseDamage (GG-T1220)");
                }
                if (!(a.CastStagger >= 0f)) p.Add($"ability {a.Id}: CastStagger {a.CastStagger}s can't be negative (GG-T879)");
                if (a.ShattersPlate && (a.Kind == AbilityKind.Passive || a.Kind == AbilityKind.Reactive || a.Filter == TargetFilter.AllyOnly || a.Filter == TargetFilter.EmptyOnly))
                    p.Add($"ability {a.Id}: ShattersPlate is a Skill, Spell or Item that can pick a foe (GG-T2069)");
                if (a.OnlyVsTag != null && !System.Linq.Enumerable.Any(db.Classes.Values, c => c.Team == Team.Enemy && c.HasTag(a.OnlyVsTag)))
                    p.Add($"ability {a.Id}: OnlyVsTag '{a.OnlyVsTag}' is carried by no foe (GG-T879)");
                if (!(a.RecoverSeconds >= 0f) || a.RecoverSeconds > Battle.BattleConfig.RecoverMaxSeconds)
                    p.Add($"ability {a.Id}: RecoverSeconds {a.RecoverSeconds}s outside 0..{Battle.BattleConfig.RecoverMaxSeconds}s (GG-T318)");
                if (a.DrainsOrbs < 0 || a.DrainsOrbs > AbilityDef.MaxDrainsOrbs)
                    p.Add($"ability {a.Id}: DrainsOrbs {a.DrainsOrbs} outside 0..{AbilityDef.MaxDrainsOrbs} (GG-T332)");
                else if (a.DrainsOrbs > 0 && (a.Id == null || !a.Id.StartsWith("charge_")))
                    p.Add($"ability {a.Id}: DrainsOrbs is an enemy charge's siphon; name it charge_… (GG-T332)");
                // GG-T373: a spell has to be payable from a full base bank (bank gear only adds headroom), and skills,
                // passives and items never spend orbs.
                // GG-T673: a spell costs at most MaxSpellOrbCost orbs, few enough to show as pips on its slot.
                if (a.Cost < 0 || a.Cost > Battle.BattleConfig.MaxSpellOrbCost)
                    p.Add($"ability {a.Id}: Cost {a.Cost} outside 0..{Battle.BattleConfig.MaxSpellOrbCost} orbs (GG-T373, GG-T673)");
                else if (a.Cost > 0 && a.Kind != AbilityKind.Spell)
                    p.Add($"ability {a.Id}: only a Spell spends orbs; a {a.Kind} has Cost 0 (GG-T373)");
                ValidateTrigger(db, a, p);
                // GG-T303: a Tame is a picked single foe from a Skill or a consumable, with a sane chance and a duration.
                if (a.Tame != 0f)
                {
                    if (!(a.Tame > 0f && a.Tame <= 1f)) p.Add($"ability {a.Id}: Tame {a.Tame} outside (0, 1]");
                    if (a.TameTurns < 1) p.Add($"ability {a.Id}: a Tame needs TameTurns >= 1");
                    if (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Item) p.Add($"ability {a.Id}: a Tame is a Skill or an Item ability");
                    if (a.Shape != TargetShape.SingleActor || a.Mode != TargetMode.PickActor || a.Filter != TargetFilter.EnemyOnly)
                        p.Add($"ability {a.Id}: a Tame picks one foe (SingleActor, PickActor, EnemyOnly)");
                }
                // GG-T333: Essence is what the Herald pays for a heal, so it belongs on an ally heal, in a sane range.
                if (a.LifeCost != 0f)
                {
                    if (!(a.LifeCost > 0f && a.LifeCost <= AbilityDef.MaxLifeCost)) p.Add($"ability {a.Id}: LifeCost {a.LifeCost} outside (0, {AbilityDef.MaxLifeCost}]");
                    if (a.BaseHeal <= 0 || a.Filter != TargetFilter.AllyOnly) p.Add($"ability {a.Id}: a LifeCost pays for an ally heal (BaseHeal > 0, AllyOnly)");
                    if (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Spell) p.Add($"ability {a.Id}: a LifeCost is a Skill or a Spell");
                }
                // GG-T336: a Countermarch reorders one foe's turn, so it picks exactly one foe.
                if (a.SwapsTurns && (a.Shape != TargetShape.SingleActor || a.Mode != TargetMode.PickActor || a.Filter != TargetFilter.EnemyOnly
                                     || (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Spell)))
                    p.Add($"ability {a.Id}: a Countermarch (SwapsTurns) is a Skill or Spell that picks one foe (SingleActor, PickActor, EnemyOnly)");
                // GG-T887: a Redirect Flow picks the one ally whose debuffs it moves.
                if (a.RedirectsDebuffs && (a.Shape != TargetShape.SingleActor || a.Mode != TargetMode.PickActor || a.Filter != TargetFilter.AllyOnly
                                           || (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Spell)))
                    p.Add($"ability {a.Id}: a Redirect Flow (RedirectsDebuffs) is a Skill or Spell that picks one ally (SingleActor, PickActor, AllyOnly)");
                // GG-T866: a Swipe Boon picks the one foe it robs.
                if (a.StealsBoon && (a.Shape != TargetShape.SingleActor || a.Mode != TargetMode.PickActor || a.Filter != TargetFilter.EnemyOnly
                                     || (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Spell)))
                    p.Add($"ability {a.Id}: a Swipe Boon (StealsBoon) is a Skill or Spell that picks one foe (SingleActor, PickActor, EnemyOnly)");
                // GG-T865: a range limit only means something on a pick of one unit.
                if (a.MaxRange < 0 || (a.MaxRange > 0 && !Rules.TargetResolver.PicksUnit(a)))
                    p.Add($"ability {a.Id}: MaxRange is 0 (anywhere) or a positive reach on a PickActor or PickMulti ability");
                // GG-T2141: a multi-pick taps 2 or more units, one at a time, and resolves at once (a timed cast carries one target).
                if (a.Mode == TargetMode.PickMulti && (a.MaxPicks < 2 || a.Shape != TargetShape.SingleActor || a.Filter == TargetFilter.EmptyOnly || a.CastTime > 0f))
                    p.Add($"ability {a.Id}: a PickMulti picks 2 or more units (MaxPicks >= 2, SingleActor, not EmptyOnly) with no CastTime");
                if (a.Mode != TargetMode.PickMulti && a.MaxPicks != 0)
                    p.Add($"ability {a.Id}: MaxPicks only means something on a PickMulti ability");
                // GG-T2141: a direction pick anchors a board shape on the caster's neighbour tile.
                if (a.Mode == TargetMode.PickDirection && (a.Shape == TargetShape.Self || a.Shape == TargetShape.SingleActor
                                                           || a.Shape == TargetShape.AllEnemies || a.Shape == TargetShape.AllAllies))
                    p.Add($"ability {a.Id}: a PickDirection anchors a tile shape (Row, Column, Wall, an area or SingleTile) beside the caster");
                // GG-T861: a Smoke Vanish leaves from the caster's own tile (its smoke goes there) to a random one, never a teleport too.
                if (a.Vanishes && (a.Shape != TargetShape.Self || a.Mode != TargetMode.Auto || a.IsTeleport || a.Kind != AbilityKind.Skill))
                    p.Add($"ability {a.Id}: a Smoke Vanish (Vanishes) is a Self / Auto Skill and not also a Teleport");
                // GG-T868: a Pole Vault is a blink whose pick is the free tile beyond a unit beside the caster.
                if (a.Vaults && (!a.IsTeleport || a.Shape != TargetShape.SingleTile || a.Mode != TargetMode.PickTile || a.Filter != TargetFilter.EmptyOnly
                                 || (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Spell)))
                    p.Add($"ability {a.Id}: a Pole Vault (Vaults) is an IsTeleport Skill or Spell that picks an empty tile (SingleTile, PickTile, EmptyOnly)");
                // GG-T867: an Afterimage dash picks the empty tile it lands on.
                if (a.Afterimage < 0 || (a.Afterimage > 0 && (a.Mode != TargetMode.PickTile || a.Filter != TargetFilter.EmptyOnly || a.Kind != AbilityKind.Skill || a.IsTeleport)))
                    p.Add($"ability {a.Id}: an Afterimage dash is a Skill that picks an empty tile (PickTile, EmptyOnly), not a Teleport");
                // GG-T388: an Echo picks the one unit whose move it repeats, and carries nothing of its own.
                if (a.Echoes && (a.Shape != TargetShape.SingleActor || a.Mode != TargetMode.PickActor || a.Filter == TargetFilter.EmptyOnly
                                 || (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Spell)))
                    p.Add($"ability {a.Id}: an Echo is a Skill or Spell that picks one unit (SingleActor, PickActor)");
                if (a.Echoes && (a.BaseDamage > 0 || a.BaseHeal > 0 || a.ApplyBuff != null || a.GrantsOrbs > 0 || a.Revives || a.IsTeleport || a.SwapsTurns
                                 || a.Knockback > 0 || a.Pull > 0 || a.SwapsPlaces || a.Pushback > 0f || a.HastenU > 0f || a.Cost > 0))
                    p.Add($"ability {a.Id}: an Echo carries no effect or cost of its own (the move it repeats does)");
                // GG-T1134: an ability that takes its caster's element, and its per-element rows.
                if (!a.ElementFromCaster && a.ElementVariants != null && a.ElementVariants.Count > 0)
                    p.Add($"ability {a.Id}: ElementVariants only mean something on an ElementFromCaster ability (GG-T1134)");
                if (a.ElementFromCaster)
                {
                    bool active = a.Kind == AbilityKind.Skill || a.Kind == AbilityKind.Spell;
                    if (active && a.BaseDamage <= 0)
                        p.Add($"ability {a.Id}: an ElementFromCaster Skill or Spell deals damage (its caster's element) (GG-T1134)");
                    if (!active && (a.Trigger == null || !a.Trigger.Effects.Exists(e => e != null && e.Kind == TriggerEffectKind.ApplyBuff)))
                        p.Add($"ability {a.Id}: an ElementFromCaster passive or reactive carries an ApplyBuff trigger effect (its element's rider) (GG-T1134)");
                    foreach (var kv in a.ElementVariants ?? new Dictionary<DamageType, ElementVariantDef>())
                    {
                        string wv = $"ability {a.Id} ElementVariants {kv.Key}";
                        var v = kv.Value;
                        if (v == null) { p.Add($"{wv}: empty row"); continue; }
                        if (v.BaseDamage != null && (!active || v.BaseDamage.Value <= 0)) p.Add($"{wv}: BaseDamage is a positive hit on a Skill or Spell");
                        if (!string.IsNullOrEmpty(v.Buff)) NeedBuff(v.Buff, wv);
                        if (v.Chance != null && !(v.Chance.Value > 0f && v.Chance.Value <= 1f)) p.Add($"{wv}: Chance {v.Chance} outside (0, 1]");
                        if (v.Description != null && db.GlossaryIndex.Count > 0)
                            foreach (var t in Glossary.Terms(v.Description))
                                if (db.TryTerm(t) == null) p.Add($"{wv}: unknown glossary term [{t}] in its description");
                        if (a.Trigger != null && db.GlossaryIndex.Count > 0)
                            foreach (var t in Glossary.Terms(Rules.TriggerText.Describe(db, a.ForElement(kv.Key))))
                                if (db.TryTerm(t) == null) p.Add($"{wv}: unknown glossary term [{t}] in its trigger sentence");
                    }
                }
                // GG-T302: the generated "When [attacked directly]: ..." sentence only uses glossary terms.
                if (a.Trigger != null && db.GlossaryIndex.Count > 0)
                    foreach (var t in Glossary.Terms(Rules.TriggerText.Describe(db, a)))
                        if (db.TryTerm(t) == null) p.Add($"ability {a.Id}: unknown glossary term [{t}] in its trigger sentence");
            }
        }

        /// <summary>GG-T296 (§4.1): a trigger lives only on a Passive or Reactive ability, a Reactive must have one that
        /// reacts to an event, and every knob is in range and names real content. Public so a trigger built outside
        /// abilities.json (a reward-granted one, GG-T301) can be checked the same way.</summary>
        public static void ValidateTrigger(ContentDb db, AbilityDef a, List<string> p)
        {
            string w = $"ability {a.Id} trigger";
            // A status a trigger matches on is compared with the buff's own id exactly (Triggers.Matches/ImmunityTo,
            // Unit.HasBuff), so it must be spelled in the buff's exact case, not just resolve case-insensitively.
            bool KnownStatus(string id) => id != null && db.Buffs.TryGetValue(id, out var bd) && string.Equals(bd.Id, id, StringComparison.Ordinal);
            var t = a.Trigger;
            if (t == null)
            {
                if (a.Kind == AbilityKind.Reactive) p.Add($"ability {a.Id}: a Reactive ability needs a Trigger");
                return;
            }
            if (a.Kind != AbilityKind.Passive && a.Kind != AbilityKind.Reactive) { p.Add($"{w}: only Passive and Reactive abilities carry a Trigger"); return; }
            if (a.Kind == AbilityKind.Reactive && t.On == TriggerOn.Always) p.Add($"{w}: a Reactive ability reacts to an event, not Always");
            if (!(t.Chance > 0f && t.Chance <= 1f)) p.Add($"{w}: Chance {t.Chance} outside (0, 1]");
            if (t.ChancePerLuck < 0f || t.ChancePerLuck > 0.1f) p.Add($"{w}: ChancePerLuck {t.ChancePerLuck} outside 0..0.1");
            if (t.Cooldown < 0) p.Add($"{w}: Cooldown can't be negative");
            if (t.OncePerBattle && t.Cooldown > 0) p.Add($"{w}: OncePerBattle already outlasts any Cooldown"); // GG-T862
            if (t.On == TriggerOn.OnSurrounded && (t.Count < 1 || t.Count > 4)) p.Add($"{w}: OnSurrounded needs Count 1..4");
            if (t.On != TriggerOn.OnSurrounded && t.Count != 0) p.Add($"{w}: Count is only for OnSurrounded");
            if (t.On == TriggerOn.OnLowHp && !(t.Threshold > 0f && t.Threshold < 1f)) p.Add($"{w}: OnLowHp needs Threshold in (0, 1)");
            if (t.On != TriggerOn.OnLowHp && t.Threshold != 0f) p.Add($"{w}: Threshold is only for OnLowHp");
            if (t.Status != null && t.On != TriggerOn.OnStatusGained) p.Add($"{w}: Status is only for OnStatusGained");
            if (t.Status != null && !KnownStatus(t.Status)) p.Add($"{w}: unknown status '{t.Status}'");
            var c = t.Condition;
            if (c != null)
            {
                if (c.SelfHpBelow < 0f || c.SelfHpBelow > 1f || c.SelfHpAbove < 0f || c.SelfHpAbove >= 1f) p.Add($"{w}: condition HP fractions outside 0..1");
                if (c.HasStatus != null && !KnownStatus(c.HasStatus)) p.Add($"{w}: condition unknown status '{c.HasStatus}'");
                if (c.LacksStatus != null && !KnownStatus(c.LacksStatus)) p.Add($"{w}: condition unknown status '{c.LacksStatus}'");
                if (c.AdjacentFoesAtLeast < 0 || c.AdjacentFoesAtLeast > 4) p.Add($"{w}: condition AdjacentFoesAtLeast outside 0..4");
                if (c.OtherTag != null && c.OtherTag.Trim().Length == 0) p.Add($"{w}: condition OtherTag is blank");
                if (c.OtherHasStatus != null && !KnownStatus(c.OtherHasStatus)) p.Add($"{w}: condition unknown status '{c.OtherHasStatus}'");
                if (c.OtherHpBelow < 0f || c.OtherHpBelow >= 1f) p.Add($"{w}: condition OtherHpBelow outside 0..1");
                if (c.FallenFamily != null && t.On != TriggerOn.OnAllyKO) p.Add($"{w}: condition FallenFamily is only for OnAllyKO"); // GG-T897
                if (c.FallenFamily != null && c.FallenFamily.Trim().Length == 0) p.Add($"{w}: condition FallenFamily is blank");
            }
            if (t.Effects == null || t.Effects.Count == 0) { p.Add($"{w}: needs at least one effect"); return; }
            foreach (var e in t.Effects)
            {
                if (e == null) { p.Add($"{w}: null effect"); continue; }
                string we = $"{w} {e.Kind}";
                bool modifier = e.Kind == TriggerEffectKind.DamageMultiplier || e.Kind == TriggerEffectKind.ReductionMultiplier || e.Kind == TriggerEffectKind.Immunity;
                if (modifier != (t.On == TriggerOn.Always)) p.Add($"{we}: DamageMultiplier/ReductionMultiplier/Immunity are exactly the Always effects");
                switch (e.Kind)
                {
                    case TriggerEffectKind.Heal:
                    case TriggerEffectKind.Lifesteal:
                    case TriggerEffectKind.Reflect:
                        if (!(e.Amount > 0f && e.Amount <= 1f)) p.Add($"{we}: Amount (a fraction) must be in (0, 1]");
                        break;
                    case TriggerEffectKind.Counterstrike:
                    case TriggerEffectKind.FollowUp:
                        if (!(e.Amount > 0f && e.Amount <= 2f)) p.Add($"{we}: Amount (a multiplier) must be in (0, 2]");
                        break;
                    case TriggerEffectKind.DamageMultiplier:
                        if (!(e.Amount > 0f && e.Amount <= 3f)) p.Add($"{we}: Amount must be in (0, 3]");
                        break;
                    case TriggerEffectKind.ReductionMultiplier:
                        if (!(e.Amount >= 0f && e.Amount <= 1f)) p.Add($"{we}: Amount must be in 0..1");
                        break;
                    case TriggerEffectKind.ManaCharge:
                        if (!(e.Amount >= 1f && e.Amount <= 100f) || e.Amount != (float)Math.Floor(e.Amount)) p.Add($"{we}: Amount must be a whole 1..100");
                        break;
                    case TriggerEffectKind.Pushback:
                        if (!(e.Amount > 0f && e.Amount <= Battle.BattleConfig.PushbackMaxSeconds)) p.Add($"{we}: Amount {e.Amount}s outside (0, {Battle.BattleConfig.PushbackMaxSeconds}]s");
                        break;
                    case TriggerEffectKind.ApplyBuff:
                        if (string.IsNullOrEmpty(e.Buff)) p.Add($"{we}: needs a Buff");
                        else if (!db.Buffs.ContainsKey(e.Buff)) p.Add($"{we}: unknown buff '{e.Buff}'");
                        break;
                    case TriggerEffectKind.Immunity:
                        if (e.Statuses == null || e.Statuses.Count == 0) p.Add($"{we}: needs Statuses");
                        else foreach (var s in e.Statuses) if (!KnownStatus(s)) p.Add($"{we}: unknown status '{s}'");
                        break;
                    case TriggerEffectKind.CastAbility:
                        if (!db.Abilities.TryGetValue(e.Ability ?? "", out var cast)) p.Add($"{we}: unknown ability '{e.Ability}'");
                        else if (cast.Kind == AbilityKind.Passive || cast.Kind == AbilityKind.Reactive) p.Add($"{we}: '{e.Ability}' must be an Active ability");
                        break;
                    case TriggerEffectKind.Substitute: // GG-T862
                        if (t.On != TriggerOn.OnLethalBlow) p.Add($"{we}: Substitute answers OnLethalBlow only");
                        if (e.State == null) p.Add($"{we}: needs a State (the decoy left behind)");
                        else if (!(db.TryTileState(e.State) is TileStateDef st)) p.Add($"{we}: unknown tile state '{e.State}'");
                        else if (!st.Blocks || st.Pit) p.Add($"{we}: the decoy state '{e.State}' must Block (an obstacle, not a hole)");
                        break;
                }
                if (e.Kind != TriggerEffectKind.Substitute && e.State != null) p.Add($"{we}: State is only for Substitute");
                if (e.Kind != TriggerEffectKind.ApplyBuff && e.Buff != null) p.Add($"{we}: Buff is only for ApplyBuff");
                if (e.Kind != TriggerEffectKind.CastAbility && e.Ability != null) p.Add($"{we}: Ability is only for CastAbility");
                if (e.Kind != TriggerEffectKind.Immunity && e.Statuses != null && e.Statuses.Count > 0) p.Add($"{we}: Statuses are only for Immunity");
            }
        }
    }
}
