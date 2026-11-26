using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1204 (GG-T1202): every texture one stage needs, worked out before it starts: the party's portraits, figures,
    /// thumbnails and bar icons, every foe of every wave (its remains, eggs, bounty marks and guests too), the stage's
    /// board tile and backdrop set, and the status icons its foes inflict. <see cref="Art.Preload"/> loads it all into
    /// <see cref="ArtScope.Stage"/> through the same Art calls the battle draws with, so the keys are exactly Art's.
    /// </summary>
    public sealed class StageTextureSet
    {
        /// <summary>Class ids drawn as portraits, figures and thumbnails (heroes, foes, remains, eggs, guests).</summary>
        public readonly HashSet<string> Classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Abilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The stage's theme: its board tile and backdrop set.</summary>
        public string Theme;
        /// <summary>GG-T1211: the indexes into <see cref="Theme"/>'s backdrop set the stage shows (Art wraps each over the
        /// set's own count): 0 (the theme's first image, the pre-battle screens') and the battle's own pick,
        /// <see cref="BattleBackdropIndex"/>. The battle keeps one backdrop for all its waves.</summary>
        public readonly HashSet<int> BackdropIndexes = new HashSet<int>();

        // GG-T1211: region rules, tile states and rule events already walked (they can name each other in a loop).
        private readonly HashSet<string> seenRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> seenStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> seenEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The backdrop index <c>BattleScreen</c> shows for <paramref name="stageId"/> (keep the two in step).</summary>
        public static int BattleBackdropIndex(string stageId) =>
            string.IsNullOrEmpty(stageId) ? 0 : Math.Abs(stageId.GetHashCode() % 4096);

        /// <summary>Status ids every battle can show whatever the foes (the stun badge, the shield).</summary>
        public static readonly string[] CommonStatuses = { "stunned", BattleConfig.ShieldBuffId };

        /// <summary>The set for <paramref name="setup"/>: its stage and party (an endless encounter's
        /// <c>EndlessEncounter.Build</c> or a campaign battle's setup alike).</summary>
        public static StageTextureSet For(ContentDb db, BattleSetup setup) =>
            For(db, setup?.Stage, setup?.Party, setup);

        /// <summary>The set for <paramref name="stage"/> fought by <paramref name="party"/> (either may be null).</summary>
        public static StageTextureSet For(ContentDb db, StageDef stage, IEnumerable<HeroSpec> party, BattleSetup setup = null)
        {
            var set = new StageTextureSet();
            foreach (var s in CommonStatuses) set.Statuses.Add(s);
            if (party != null)
                foreach (var h in party)
                {
                    if (h == null || string.IsNullOrEmpty(h.ClassId)) continue;
                    set.AddClass(db, h.ClassId);
                    if (h.Bar != null)
                        foreach (var e in h.Bar)
                        {
                            if (string.IsNullOrEmpty(e)) continue;
                            if (e.StartsWith("item:", StringComparison.Ordinal)) set.Items.Add(e.Substring(5));
                            else set.AddAbility(db, e);
                        }
                    if (!string.IsNullOrEmpty(h.WeaponId)) set.Items.Add(h.WeaponId);
                }
            if (stage != null)
            {
                set.Theme = stage.Theme;
                set.BackdropIndexes.Add(0);
                set.BackdropIndexes.Add(BattleBackdropIndex(stage.Id));
                // GG-T1211: the region rules it plays under (its theme's, then its own): the foes their SpawnUnit
                // effects put on the board and the statuses they apply.
                foreach (var r in RegionRuleCatalog.ActiveFor(db, stage)) set.AddRule(db, r);
                foreach (var w in stage.Waves)
                {
                    if (w == null) continue;
                    foreach (var sp in w.Enemies) set.AddClass(db, sp?.Class);
                    foreach (var sp in w.Guests) set.AddClass(db, sp?.Class);
                    if (w.Recruitables != null) foreach (var r in w.Recruitables) set.AddClass(db, r?.Class);
                    set.AddClass(db, w.SurpriseSpeaker);
                }
                if (stage.Scenes != null) foreach (var sc in stage.Scenes) set.AddClass(db, sc?.Class);
            }
            if (setup != null)
            {
                if (setup.Bounties != null) foreach (var b in setup.Bounties) set.AddClass(db, b?.ClassId);
                set.AddClass(db, setup.Wanderer?.Class);
            }
            return set;
        }

        /// <summary>Adds a class and what it brings with it: its loadout and charge spell icons, the class ids it can put on
        /// the board (remains, eggs), and the status it inflicts.</summary>
        private void AddClass(ContentDb db, string classId)
        {
            if (string.IsNullOrEmpty(classId) || classId.StartsWith("$") || !Classes.Add(classId)) return;
            var def = db?.TryClass(classId);
            if (def == null) return;
            if (def.Team != Team.Hero)
            {
                foreach (var a in def.DefaultLoadout) AddAbility(db, a);
                AddAbility(db, def.ChargeAbility);
            }
            if (!string.IsNullOrEmpty(def.OnHitBuff)) Statuses.Add(def.OnHitBuff);
            // GG-T1211: a boss's phase-entry ability (a bounty target's too: it is the class's own) shows its icon.
            if (def.BossPhases != null) foreach (var ph in def.BossPhases) AddAbility(db, ph?.OnEnterAbility);
            // Snake segments (GrowSnake) are the head's own class (Def = head.Def), so the head covers them.
            AddEffects(db, def.OnDeathRule);
            AddEffects(db, def.TrailRule);
            AddClass(db, def.Remains);
            AddClass(db, def.LaysEgg);
        }

        private void AddAbility(ContentDb db, string id)
        {
            if (string.IsNullOrEmpty(id) || !Abilities.Add(id)) return;
            var a = db?.TryAbility(id);
            if (!string.IsNullOrEmpty(a?.ApplyBuff)) Statuses.Add(a.ApplyBuff);
            AddEffects(db, a?.RuleEffects);
        }

        /// <summary>GG-T1211: a region rule's tile states, events and hooks.</summary>
        private void AddRule(ContentDb db, RegionRuleDef rule)
        {
            if (rule == null || string.IsNullOrEmpty(rule.Id) || !seenRules.Add(rule.Id)) return;
            if (rule.TileStates != null) foreach (var t in rule.TileStates) AddState(db, t);
            if (rule.Events != null) foreach (var e in rule.Events) AddEvent(db, e);
            if (rule.Hooks != null) foreach (var h in rule.Hooks) AddEffects(db, h?.Effects);
        }

        private void AddState(ContentDb db, TileStateDef t)
        {
            if (t == null || string.IsNullOrEmpty(t.Id) || !seenStates.Add(t.Id)) return;
            AddEffects(db, t.OnMark);
            AddEffects(db, t.OnExpire);
            AddEffects(db, t.OnSquish);
            if (t.Circle != null) { AddEffects(db, t.Circle.OnSummon); AddEffects(db, t.Circle.OnEmpower); }
            AddState(db, db?.TryTileState(t.Becomes));
            AddState(db, db?.TryTileState(t.HeavyBecomes));
        }

        private void AddEvent(ContentDb db, RuleEventDef e)
        {
            if (e == null || string.IsNullOrEmpty(e.Id) || !seenEvents.Add(e.Id)) return;
            AddEffects(db, e.Effects);
        }

        /// <summary>GG-T1211: what rule effects bring: a SpawnUnit's class (regionrules.json <c>Class</c>), an ApplyBuff's
        /// status, a scheduled event's own effects, and a painted state with the rule it brings into play.</summary>
        private void AddEffects(ContentDb db, List<RuleEffectDef> effects)
        {
            if (effects == null) return;
            foreach (var fx in effects)
            {
                if (fx == null) continue;
                switch (fx.Kind)
                {
                    case RuleEffectKind.SpawnUnit: AddClass(db, fx.Class); break;
                    case RuleEffectKind.ApplyBuff: if (!string.IsNullOrEmpty(fx.Buff)) Statuses.Add(fx.Buff); break;
                    case RuleEffectKind.Schedule: AddEvent(db, db?.TryRuleEvent(fx.Event)); break;
                    case RuleEffectKind.MarkTiles:
                        if (string.IsNullOrEmpty(fx.State) || db == null) break;
                        AddState(db, db.TryTileState(fx.State));
                        if (db.TileStateRule.TryGetValue(fx.State, out var ruleId)) AddRule(db, db.TryRegionRule(ruleId));
                        break;
                }
            }
        }

        /// <summary>The Resources keys this set loads (diagnostics and tests): the same paths Art resolves.</summary>
        public IEnumerable<string> Keys(ContentDb db)
        {
            foreach (var c in Classes)
            {
                var def = db?.TryClass(c);
                string file = def != null ? def.PortraitFile() : c;
                if (string.IsNullOrEmpty(file)) continue;
                yield return Art.PortraitFolder + file;
                yield return Art.ThumbFolder + file;
            }
            foreach (var a in Abilities) yield return AssetNames.Path("AbilityIcons", a);
            foreach (var i in Items) yield return AssetNames.Path("ItemIcons", i);
            foreach (var s in Statuses) yield return AssetNames.Path("StatusIcons", s);
            if (!string.IsNullOrEmpty(Theme))
            {
                yield return AssetNames.Path("Tiles", Theme);
                foreach (var key in BackdropKeys(db)) yield return key;
            }
        }

        /// <summary>GG-T1211: the backdrop keys of <see cref="BackdropIndexes"/>, each once (two indexes can wrap onto one
        /// image), resolved as <see cref="Art.BackgroundKey"/> does.</summary>
        public IEnumerable<string> BackdropKeys(ContentDb db)
        {
            if (string.IsNullOrEmpty(Theme)) yield break;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in BackdropIndexes)
            {
                var key = Backdrops.Resolve(Theme, i, Art.BackgroundSet, x => Backdrops.SetOf(db, x));
                if (!string.IsNullOrEmpty(key) && seen.Add(key)) yield return key;
            }
        }

        public override string ToString() =>
            $"StageTextureSet({Classes.Count} classes, {Abilities.Count} abilities, {Items.Count} items, {Statuses.Count} statuses, theme {Theme}, {BackdropIndexes.Count} backdrops)";
    }
}
