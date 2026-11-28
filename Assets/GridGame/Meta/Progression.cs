using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    // ─────────────────────────────────────────────────────────────────────────────
    // New game / progression
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Progression
    {
        /// <summary>
        /// The default starting party. Owner 2026-10-11 made it five ("a 5 player team seems about right for max");
        /// owner 2026-10-19 (GG-T118) shrank the start to a small party — Tank, Striker, Healer — with the rest of
        /// the party joining at story beats (dialogue JoinHero), on top of rescued guests and the Summon Circle.
        /// GG-T1405: there is no fixed lead any more (the player is the Commander), so the warden is an ordinary hero.
        /// </summary>
        public static readonly string[] StartingRoster = { "knight", "barbarian", "cleric" };
        /// <summary>Heroes a new game starts with (character select locks in this many; GG-T118).</summary>
        public const int StartingPartySize = 3;
        public const int StartingGold = 200;
        /// <summary>
        /// The party cap (§25.14). Owner 2026-11-06 (GG-T291): six — "two attackers and two supporters for each
        /// attacker". Slots fill at the story beats (GG-T118): the 4th after the first stage, the 5th at The
        /// Shardfall, the 6th at The Anchor Wastes; later recruits, guests and summons wait in reserve when it's full.
        /// </summary>
        public const int MaxPartySize = 6;
        public const int MaxBarSlots = 5;
        /// <summary>
        /// Slot unlocks (§4.7). Amended 2026-10-11 (owner: "give each character 2 stacks of full life potions and unlock
        /// the slots to put them in"): every slot is open from the start, so the thresholds no longer gate anything.
        /// </summary>
        public static readonly int[] UnlockThresholds = { };
        public const int StartingSlots = MaxBarSlots;

        public static SaveData NewGame(ContentDb c, string name) => NewGame(c, name, StartingRoster);

        /// <summary>A custom party (e.g. from the character-select screen) in place of the default <see cref="StartingRoster"/>.</summary>
        public static SaveData NewGame(ContentDb c, string name, IReadOnlyList<string> party)
        {
            var s = new SaveData { Name = string.IsNullOrWhiteSpace(name) ? "Lightbearer" : name.Trim(), CommanderName = Commander.Name(null, name), Gold = StartingGold };
            var ids = new List<string>();
            foreach (var id in party ?? Array.Empty<string>())
                if (id != null && !ids.Contains(id, StringComparer.OrdinalIgnoreCase)) ids.Add(id);
            foreach (var id in ids)
            {
                if (c.TryClass(id) == null || s.Party.Count >= MaxPartySize) continue;
                var h = NewHero(c, id);
                s.Roster.Add(h);
                s.Party.Add(id);
                Bonds.Joined(s, h.ClassId, JoinRoute.Starting);
            }
            // Every item slot on a starting bar is a full stack (two X-Potion stacks of 5 per hero).
            foreach (var h in s.Roster)
                foreach (var e in h.Bar)
                    if (e != null && e.StartsWith("item:")) s.AddItem(e.Substring(5), Math.Max(1, c.TryItem(e.Substring(5))?.MaxStack ?? 3));
            foreach (var h in s.Roster) Codex.UnlockHero(s, h); // GG-T929: the starting party and its starter kits
            return s;
        }

        /// <summary>
        /// A new hero (GG-T388, owner 2026-11-16: "start with basic characters … skills start as few but they are purchased
        /// or discovered over time"): it knows only its <see cref="StarterKit"/> — the role's baseline move and the class's
        /// own signature — and its bar is those two, then the default loadout's items, then empty slots. The rest of the
        /// default loadout (the archetype's two moves) comes from the skill board with skill points (GG-T1079: the gold
        /// trainer is gone).
        /// GG-T1032: a class with a skill-hexagon Start path joins on it instead and knows the abilities on that path.
        /// </summary>
        public static HeroSave NewHero(ContentDb c, string classId)
        {
            var def = c.Class(classId);
            // GG-T532: every hero who joins (new game, story, summon, Diamond draw, wanderer, freed guest, mercenary, quest)
            // waits for the player's female/male pick; the app asks at its next quiet moment (VariantPicker).
            var h = new HeroSave { ClassId = def.Id, VariantPending = true };
            int i = 0;
            // GG-T1032: a class on the skill hexagon joins on its skillhex.json Start path (SkillHexProgress.Join owns the
            // path and learns every ability node on it). Its bar is its default loadout in authored order (items may
            // repeat; a move only when the path taught it, once), then any further path ability while a slot is free.
            if (SkillHexProgress.Join(c, h).Ok)
            {
                foreach (var entry in def.DefaultLoadout)
                {
                    if (string.IsNullOrEmpty(entry) || i >= MaxBarSlots) continue;
                    if (!entry.StartsWith("item:") && (!h.Known.Contains(entry) || h.Bar.Take(i).Contains(entry))) continue;
                    h.Bar[i++] = entry;
                }
                foreach (var id in h.Known)
                    if (i < MaxBarSlots && !h.Bar.Take(i).Contains(id)) h.Bar[i++] = id;
                return h;
            }
            foreach (var id in StarterKit(def))
            {
                h.Known.Add(id);
                if (i < MaxBarSlots) h.Bar[i++] = id;
            }
            foreach (var entry in def.DefaultLoadout)
                if (entry.StartsWith("item:") && i < MaxBarSlots) h.Bar[i++] = entry; // items may repeat (two stacks)
            return h;
        }

        /// <summary>The abilities of a class's default loadout, in bar order (items left out).</summary>
        public static List<string> KitAbilities(ClassDef def) =>
            def == null ? new List<string>() : def.DefaultLoadout.Where(e => e != null && !e.StartsWith("item:")).Distinct().ToList();

        /// <summary>The class's kit with content in hand: <see cref="KitAbilities(ClassDef)"/> (GG-T1098: the family-tree
        /// start grants are retired).</summary>
        public static List<string> KitAbilities(ContentDb c, ClassDef def) => KitAbilities(def);

        /// <summary>What a new hero knows with content in hand: <see cref="StarterKit(ClassDef)"/>.</summary>
        public static List<string> StarterKit(ContentDb c, ClassDef def) => StarterKit(def);

        /// <summary>What a new hero knows (GG-T388): the kit's first ability (the role's baseline: Cleave, Taunt, Heal …) and
        /// its third, the class's own signature (§23.2.2's slot 3: Reckless Swing, Bulwark, Lifegift …). A kit of two or
        /// fewer is all starter.</summary>
        public static List<string> StarterKit(ClassDef def)
        {
            var kit = KitAbilities(def);
            if (kit.Count <= 2) return kit;
            return new List<string> { kit[0], kit[2] };
        }

        /// <summary>Hero level at which a hero who joins already trained (a story recruit, a wanderer, a summon) knows each
        /// further kit move past its <see cref="StarterKit"/>, in kit order: the archetype opener at 3, its capstone at 6.</summary>
        public static readonly int[] VeteranKitLevels = { 3, 6 };

        /// <summary>A hero who joins mid-campaign arrives trained to its level (GG-T388): every kit move a hero of that level
        /// would have had time to learn (<see cref="VeteranKitLevels"/>) is known and slotted into an empty bar slot. The
        /// starting party learns these itself, from the trainer or the tree.</summary>
        public static void TrainTo(ContentDb c, HeroSave h)
        {
            var def = c.TryClass(h?.ClassId);
            if (def == null || h.Domesticated) return;
            var starter = StarterKit(c, def);
            var rest = KitAbilities(c, def).Where(id => !starter.Contains(id)).ToList(); // GG-T978: none on a family tree
            int level = Level(h);
            while (h.Bar.Count < MaxBarSlots) h.Bar.Add(null);
            for (int k = 0; k < rest.Count && k < VeteranKitLevels.Length; k++)
            {
                if (level < VeteranKitLevels[k] || h.Known.Contains(rest[k])) continue;
                h.Known.Add(rest[k]);
                int free = h.Bar.IndexOf(null);
                if (free >= 0 && free < MaxBarSlots) h.Bar[free] = rest[k];
            }
        }

        public static int Level(HeroSave h) => Formulas.LevelFromTotalXp(h.TotalXp).level;

        public static int UnlockedSlots(SaveData s)
        {
            int n = StartingSlots;
            foreach (var t in UnlockThresholds) if (s.HighestClearedStage >= t) n++;
            return Math.Min(MaxBarSlots, n);
        }

        public static bool IsStageUnlocked(SaveData s, int campaignIndex) => campaignIndex <= s.HighestClearedStage + 1;

        public static int RecommendedLevel(int campaignIndex) => campaignIndex + 1;

        /// <summary>The party's level: the average of its heroes' levels, rounded (min 1). Small and shared on
        /// purpose — anything that scales enemies to the party (Test Mode, GG-T93; the Crucible) uses this.</summary>
        public static int PartyLevel(SaveData s)
        {
            var levels = s.Party.Select(s.Hero).Where(h => h != null).Select(Level).ToList();
            return levels.Count == 0 ? 1 : Math.Max(1, (int)Math.Round(levels.Average(), MidpointRounding.AwayFromZero));
        }

        /// <summary>A stage's recommended level for this save (the floor its enemies spawn at): the party's
        /// level on a Test Mode save (GG-T93, so difficulty rises as the party levels), otherwise the
        /// stage's LevelOverride or its flattened campaign index.</summary>
        public static int StageRecommendedLevel(ContentDb c, SaveData s, StageDef stage)
        {
            if (s.TestMode) return PartyLevel(s);
            int index = c.CampaignStages().FindIndex(x => x.Id == stage.Id);
            return stage.LevelOverride ?? (index >= 0 ? RecommendedLevel(index) : 1);
        }

        public static bool CanStartNewGamePlus(ContentDb c, SaveData s)
        {
            var all = c.CampaignStages();
            return all.Count > 0 && s.HighestClearedStage >= all.Count - 1;
        }

        /// <summary>New Game+ (US-332): replay the campaign from the start with everything you own; enemies +10 levels per cycle.</summary>
        public static Result StartNewGamePlus(ContentDb c, SaveData s)
        {
            if (!CanStartNewGamePlus(c, s)) return Result.Fail("Clear the whole campaign first");
            s.NewGamePlus++;
            s.HighestClearedStage = -1;
            s.ClearedStages.Clear();
            s.SeenCrawls.Clear();
            s.SeenDialogues.Clear(); // story beats replay with the crawls; flags (joined NPCs) carry over
            s.Flags.Remove(Story.EndingFlag);
            return Result.Success($"New Game+ {s.NewGamePlus} — enemies grow stronger");
        }

        public static Unit Preview(ContentDb c, HeroSave h)
        {
            var spec = BattleFactory.SpecFor(c, h);
            var setup = new BattleSetup
            {
                Stage = new StageDef { Id = "_preview", IsTest = true, Waves = { new WaveDef() } },
                Party = { spec },
            };
            var state = BattleEngine.Create(c, setup, new List<BattleEvent>());
            return state.Units.First(u => u.IsHero);
        }

        // ── Build screen read models (GG-T48): every number here is derived from data the hero already has
        // (Known, its class, the tree content GG-T50 shipped) — none of it invents a spend ledger ahead of the
        // real one (§25.6: spending skill points against a tree is still follow-up work, GG-T49/50).

        /// <summary>Proficiency-unlock passives staged by GG-T74 for GG-T52's gear gating. Granted by a tree
        /// node like any other, but GG-T52 hasn't landed yet, so nothing can unlock one through play today —
        /// <see cref="KnownProficiencies"/> simply reports whichever of these a hero's <c>Known</c> already
        /// has, ready to light up once GG-T52 wires the gate.</summary>
        public static readonly string[] ProficiencyPassiveIds =
            { "heavy_armor_proficiency", "martial_weapons_proficiency", "arcane_focus_proficiency" };

        /// <summary>Friendly names of every proficiency-unlock passive (<see cref="ProficiencyPassiveIds"/>)
        /// the hero already knows.</summary>
        public static List<string> KnownProficiencies(ContentDb c, HeroSave h) =>
            ProficiencyPassiveIds.Where(h.Known.Contains).Select(id => c.TryAbility(id)?.Name).Where(n => n != null).ToList();

        /// <summary>Weapon classes this hero can currently wield (GG-T52, gear-proficiency gating): every hero
        /// can always use <see cref="WeaponClass.Simple"/>; Martial/Arcane come from the class's own home-tree
        /// training (<see cref="ClassDef.WeaponProficiencies"/>) or from having learned that class's unlock
        /// passive (<c>martial_weapons_proficiency</c> / <c>arcane_focus_proficiency</c> — <see cref="ProficiencyPassiveIds"/>),
        /// e.g. a cleric earning Martial Weapons or a warrior earning Arcane Focus.</summary>
        public static HashSet<WeaponClass> EffectiveWeaponProficiencies(ContentDb c, HeroSave h)
        {
            var cls = c.TryClass(h.ClassId);
            var set = new HashSet<WeaponClass> { WeaponClass.Simple };
            if (cls != null) foreach (var w in cls.WeaponProficiencies) set.Add(w);
            if (h.Known.Contains("martial_weapons_proficiency")) set.Add(WeaponClass.Martial);
            if (h.Known.Contains("arcane_focus_proficiency")) set.Add(WeaponClass.Arcane);
            return set;
        }

        /// <summary>Heaviest armor this hero can currently wear (GG-T52): the class's own training
        /// (<see cref="ClassDef.ArmorProficiency"/>), bumped straight to Heavy if Heavy Armor Proficiency is
        /// known — the passive's own text ("regardless of class") skips Medium on purpose, matching how
        /// Light and Medium are never gated behind any passive today.</summary>
        public static ArmorWeight EffectiveArmorProficiency(ContentDb c, HeroSave h)
        {
            var cls = c.TryClass(h.ClassId);
            var max = cls?.ArmorProficiency ?? ArmorWeight.Medium;
            if (h.Known.Contains("heavy_armor_proficiency") && max < ArmorWeight.Heavy) max = ArmorWeight.Heavy;
            return max;
        }

        /// <summary>A one-line "what this hero is" summary for the Build screen, e.g. "Knight — a tank" (GG-T1098: the talent
        /// trees are retired, so no tree investment or cross-tree flavor): the class name and its <see cref="ClassDef.Role"/>.</summary>
        public static string BuildSummary(ContentDb c, HeroSave h)
        {
            var def = c.Class(h.ClassId);
            return $"{def.Name} — {RoleWord(def.Role)}";
        }

        private static string RoleWord(string role)
        {
            switch (role)
            {
                case "Tank": return "a tank";
                case "Healer": return "a healer";
                case "Mage": return "a mage";
                case "Striker": return "a striker";
                case "Support": return "a support";
                case "Ranged": return "a ranged attacker";
                default: return "a hero";
            }
        }
    }
}
