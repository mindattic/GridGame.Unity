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
            AddClass(db, def.Remains);
            AddClass(db, def.LaysEgg);
        }

        private void AddAbility(ContentDb db, string id)
        {
            if (string.IsNullOrEmpty(id) || !Abilities.Add(id)) return;
            var a = db?.TryAbility(id);
            if (!string.IsNullOrEmpty(a?.ApplyBuff)) Statuses.Add(a.ApplyBuff);
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
            if (!string.IsNullOrEmpty(Theme)) yield return AssetNames.Path("Tiles", Theme);
        }

        public override string ToString() =>
            $"StageTextureSet({Classes.Count} classes, {Abilities.Count} abilities, {Items.Count} items, {Statuses.Count} statuses, theme {Theme})";
    }
}
