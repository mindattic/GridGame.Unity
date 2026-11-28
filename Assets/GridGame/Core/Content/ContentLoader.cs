using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace GridGame.Core.Content
{
    /// <summary>
    /// Builds a <see cref="ContentDb"/> from the JSON documents. The text source is injected
    /// (Unity: Resources TextAssets; tests: files on disk) so the loader stays engine-free.
    /// </summary>
    public static class ContentLoader
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore,
            // Replace (not append to) collections that have initializers, e.g. HeroSave.Bar's five empty slots.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        /// <summary>GG-T1037 (skill-hex, owner 2026-11-25; skillhex.json marks them Status Cut): the hero classes cut from the
        /// roster and the placed hero each becomes (<see cref="ClassDef.CutInto"/>). They stay in classes.json for the Codex
        /// and old saves, but leave recruit pools, stage guests and drops.</summary>
        public static readonly IReadOnlyDictionary<string, string> CutClasses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "basher", "bruiser" }, { "operative", "drifter" }, { "technician", "engineer" }, { "tinkerer", "engineer" },
        };

        /// <param name="readText">Returns the JSON text for a file name (no extension), or null if absent.</param>
        public static ContentDb Load(Func<string, string> readText)
        {
            var db = new ContentDb();
            AddAll(db.Classes, ReadList<ClassDef>(readText, "classes"), c => c.Id);
            foreach (var kv in CutClasses) // GG-T1037: a class row's own CutInto wins
                if (db.Classes.TryGetValue(kv.Key, out var cut) && !cut.IsCut) cut.CutInto = kv.Value;
            // GG-T973: a row with MergedInto is an alias stub, not an ability: it goes to AbilityAliases instead.
            var abilitiesText = readText("abilities");
            var abilities = ReadList<AbilityDef>(_ => abilitiesText, "abilities");
            AddAll(db.Abilities, abilities.FindAll(a => a == null || string.IsNullOrEmpty(a.MergedInto)), c => c.Id);
            IndexAbilityAliases(db, abilities, abilitiesText);
            AddAll(db.Buffs, ReadList<BuffDef>(readText, "buffs"), c => c.Id);
            AddAll(db.Items, ReadList<ItemDef>(readText, "items"), c => c.Id);
            AddAll(db.Recipes, ReadList<RecipeDef>(readText, "recipes"), c => c.Id);
            AddAll(db.DropTables, ReadList<DropTableDef>(readText, "droptables"), c => c.Id);
            var encounters = ReadList<EncounterGroupDef>(readText, "encounters");
            AddAll(db.Encounters, encounters, x => x.Id);
            db.EncounterOrder.AddRange(encounters);
            var stages = ReadList<StageDef>(readText, "stages");
            foreach (var st in stages) ResolveGroups(db, st, $"stage {st.Id}");
            AddAll(db.Stages, stages, c => c.Id);
            db.StageOrder.AddRange(stages);
            AddAll(db.Traps, ReadList<TrapDef>(readText, "traps"), c => c.Id);
            AddAll(db.Bounties, ReadList<BountyDef>(readText, "bounties"), c => c.Id);
            var offers = ReadList<OfferDef>(readText, "offers");
            AddAll(db.Offers, offers, x => x.Id);
            db.OfferOrder.AddRange(offers);
            var banners = ReadList<BannerDef>(readText, "banners"); // GG-T1418
            AddAll(db.Banners, banners, x => x.Id);
            db.BannerOrder.AddRange(banners);
            AddAll(db.Enchants, ReadList<EnchantDef>(readText, "enchants"), c => c.Id);
            var armorEnchants = ReadList<ArmorEnchantDef>(readText, "armorenchants");
            AddAll(db.ArmorEnchants, armorEnchants, x => x.Id);
            db.ArmorEnchantOrder.AddRange(armorEnchants);
            db.Campaign = ReadOne<CampaignDef>(readText, "campaign") ?? new CampaignDef();
            db.Vfx = ReadOne<VfxStyleSheet>(readText, "vfxstyle") ?? new VfxStyleSheet();
            db.VfxPrefabs = ReadOne<VfxCatalog>(readText, "vfx") ?? new VfxCatalog();
            db.AttackTypes = ReadOne<AttackTypeSheet>(readText, "attacktypes") ?? new AttackTypeSheet(); // GG-T556
            db.Sky = ReadOne<SkyDef>(readText, "sky") ?? new SkyDef(); // GG-T576
            var layers = ReadOne<Dictionary<string, BackdropLayerDef>>(readText, "backdroplayers");
            if (layers != null)
                foreach (var kv in layers) db.BackdropLayers[kv.Key] = kv.Value;
            var elements = ReadOne<ElementTableDef>(readText, "elements"); // GG-T371
            db.ElementsFileMissing = elements == null; // GG-T416: only a missing file is reported, not a hand-built ContentDb
            db.Elements = elements ?? new ElementTableDef();
            db.Mercenaries.AddRange(ReadList<MercenaryDef>(readText, "mercenaries"));
            db.Factions.AddRange(ReadList<FactionDef>(readText, "factions"));
            var breeds = ReadList<BreedDef>(readText, "breeds"); // GG-T1774
            AddAll(db.Breeds, breeds, x => x.Id);
            db.BreedOrder.AddRange(breeds);
            var families = readText("families"); // GG-T971: only a missing file is reported, not a hand-built ContentDb
            db.FamiliesFileMissing = string.IsNullOrWhiteSpace(families);
            db.Families.AddRange(ReadList<FamilyDef>(readText, "families"));
            db.SkillHex = ReadOne<SkillHexDef>(readText, "skillhex"); // GG-T1022: optional; null skips its checks
            db.World = ReadOne<WorldDef>(readText, "world"); // GG-T1667: optional; null skips its checks
            BattleLibrary.Load(db, readText); // GG-T1709
            AddAll(db.Caravan, ReadList<CaravanUpgradeDef>(readText, "caravan"), x => x.Id);
            AddAll(db.Pickups, ReadList<PickupDef>(readText, "pickups"), x => x.Id);
            AddAll(db.SkillTrees, ReadList<SkillTreeDef>(readText, "skilltrees"), x => x.Id);
            var dialogues = ReadList<DialogueDef>(readText, "dialogues");
            AddAll(db.Dialogues, dialogues, x => x.Id);
            db.DialogueOrder.AddRange(dialogues);
            var quests = ReadList<QuestDef>(readText, "quests");
            AddAll(db.Quests, quests, x => x.Id);
            db.QuestOrder.AddRange(quests);
            var sets = ReadList<ArmorSetDef>(readText, "armorsets");
            AddAll(db.ArmorSets, sets, x => x.Id);
            db.ArmorSetOrder.AddRange(sets);
            db.GlossaryOrder.AddRange(ReadList<GlossaryEntry>(readText, "glossary"));
            RegionRuleCatalog.Index(db, ReadList<RegionRuleDef>(readText, "regionrules")); // GG-T359
            // GG-T936: the Codex's entries; a key listed twice keeps the first, Validate reports the clash.
            foreach (var e in ReadList<CodexDef>(readText, "codex"))
            {
                if (e == null) continue;
                e.Related ??= new List<string>();
                db.CodexOrder.Add(e);
                if (!string.IsNullOrWhiteSpace(e.Key) && !db.Codex.ContainsKey(e.Key.Trim())) db.Codex[e.Key.Trim()] = e;
            }
            // GG-T355: the story words come from Prose (Content/Text/<kind>.json), joined onto the records by key.
            ProseText.Join(db, readText);
            Glossary.Index(db);
            // First set wins a piece listed twice; Validate reports the clash.
            foreach (var set in sets)
                foreach (var piece in set.Pieces)
                    if (piece != null && !db.SetByItem.ContainsKey(piece)) db.SetByItem[piece] = set;
            return db;
        }

        /// <summary>GG-T973: moves abilities.json's alias stubs (<see cref="AbilityDef.MergedInto"/>) into
        /// <see cref="ContentDb.AbilityAliases"/>. The shape check: an alias row carries only Id and MergedInto, and its id is
        /// neither a live ability nor another alias. Problems go to <see cref="ContentDb.LoadProblems"/>.</summary>
        public static void IndexAbilityAliases(ContentDb db, List<AbilityDef> abilities, string abilitiesText)
        {
            foreach (var a in abilities)
            {
                if (a == null || string.IsNullOrEmpty(a.MergedInto)) continue;
                if (string.IsNullOrEmpty(a.Id)) throw new FormatException("AbilityDef alias with empty Id");
                if (db.Abilities.ContainsKey(a.Id)) db.LoadProblems.Add($"ability alias {a.Id}: is also a live ability id (GG-T973)");
                else if (db.AbilityAliases.ContainsKey(a.Id)) db.LoadProblems.Add($"ability alias {a.Id}: listed twice (GG-T973)");
                else db.AbilityAliases[a.Id] = a.MergedInto.Trim();
            }
            if (string.IsNullOrWhiteSpace(abilitiesText) || db.AbilityAliases.Count == 0) return;
            JToken root;
            try { root = JToken.Parse(abilitiesText); }
            catch (JsonException) { return; } // ReadList has already rejected a file that doesn't parse
            if (!(root is JArray rows)) return;
            foreach (var o in rows.Children<JObject>())
            {
                var merged = o.Property("MergedInto", StringComparison.OrdinalIgnoreCase);
                if (merged == null || merged.Value.Type == JTokenType.Null) continue;
                var extra = new List<string>();
                foreach (var prop in o.Properties())
                    if (!string.Equals(prop.Name, "Id", StringComparison.OrdinalIgnoreCase) && !string.Equals(prop.Name, "MergedInto", StringComparison.OrdinalIgnoreCase))
                        extra.Add(prop.Name);
                if (extra.Count > 0)
                    db.LoadProblems.Add($"ability alias {o.Property("Id", StringComparison.OrdinalIgnoreCase)?.Value}: an alias row carries only Id and MergedInto (drop {string.Join(", ", extra)}) (GG-T973)");
            }
        }

        /// <summary>GG-T973: every alias names a live ability (no chains, no cycles, not itself), and no class, skill tree
        /// or item still names an alias (it should name the surviving ability).</summary>
        public static void ValidateAbilityAliases(ContentDb db, List<string> p)
        {
            foreach (var kv in db.AbilityAliases)
            {
                var w = $"ability alias {kv.Key}";
                var to = kv.Value;
                if (string.IsNullOrEmpty(to)) p.Add($"{w}: MergedInto is empty");
                else if (string.Equals(to, kv.Key, StringComparison.OrdinalIgnoreCase)) p.Add($"{w}: merged into itself");
                else if (db.AbilityAliases.ContainsKey(to)) p.Add($"{w}: merged into '{to}', which is itself an alias (no chains or cycles: name the surviving ability)");
                else if (!db.Abilities.ContainsKey(to)) p.Add($"{w}: merged into unknown ability '{to}'");
            }
            if (db.AbilityAliases.Count == 0) return;
            void NoAlias(string id, string where)
            {
                if (id != null && db.AbilityAliases.TryGetValue(id, out var to))
                    p.Add($"{where}: '{id}' was merged into '{to}'; name '{to}' (GG-T973)");
            }
            foreach (var c in db.Classes.Values)
            {
                foreach (var a in c.Abilities) NoAlias(a, $"class {c.Id} abilities");
                foreach (var s in c.DefaultLoadout)
                    if (s != null && !s.StartsWith("item:")) NoAlias(s, $"class {c.Id} loadout");
                NoAlias(c.ChargeAbility, $"class {c.Id} charge");
                foreach (var ph in c.BossPhases) NoAlias(ph?.OnEnterAbility, $"class {c.Id} boss phase");
            }
            foreach (var t in db.SkillTrees.Values)
                foreach (var n in t.Nodes)
                    if (n != null) NoAlias(n.Grants, $"tree {t.Id} node {n.Id}");
            foreach (var i in db.Items.Values)
            {
                NoAlias(i.OnUseAbility, $"item {i.Id}");
                NoAlias(i.TeachesAbility, $"item {i.Id} tome");
            }
        }

        /// <summary>GG-T1022: reads skillhex.json text the way <see cref="Load"/> does (null for empty text); a file that
        /// doesn't parse throws <see cref="FormatException"/>.</summary>
        public static SkillHexDef ParseSkillHex(string text) => ReadOne<SkillHexDef>(_ => text, "skillhex");

        /// <summary>GG-T1022: <see cref="ContentDb.SkillHex"/> against tools/skillhex/validate.py's rules
        /// (<see cref="SkillHexContent.Validate"/>); skipped when the board is absent.</summary>
        public static void ValidateSkillHex(ContentDb db, List<string> p) => SkillHexContent.Validate(db, db.SkillHex, p);

        /// <summary>A wave that names a group (GG-T184) gets the group's foes, formation and level; Validate reports a bad id.</summary>
        internal static void ResolveGroups(ContentDb db, StageDef st, string where)
        {
            for (int wi = 0; wi < st.Waves.Count; wi++)
            {
                var w = st.Waves[wi];
                if (w.Group != null && w.Enemies.Count > 0)
                    db.LoadProblems.Add($"{where} wave {wi + 1}: names both Group '{w.Group}' and its own Enemies (the Group is ignored; use one)");
                else if (w.Group != null && db.TryEncounter(w.Group) is EncounterGroupDef g)
                    w.Enemies = EncounterCatalog.Spawns(g, g.Level);
            }
        }

        internal static List<T> ReadList<T>(Func<string, string> readText, string name)
        {
            var text = readText(name);
            if (string.IsNullOrWhiteSpace(text)) return new List<T>();
            try { return JsonConvert.DeserializeObject<List<T>>(text, Settings) ?? new List<T>(); }
            catch (Exception e) { throw new FormatException($"Content file '{name}.json' is invalid: {e.Message}", e); }
        }

        private static T ReadOne<T>(Func<string, string> readText, string name) where T : class
        {
            var text = readText(name);
            if (string.IsNullOrWhiteSpace(text)) return null;
            try { return JsonConvert.DeserializeObject<T>(text, Settings); }
            catch (Exception e) { throw new FormatException($"Content file '{name}.json' is invalid: {e.Message}", e); }
        }

        private static void AddAll<T>(Dictionary<string, T> map, List<T> items, Func<T, string> key)
        {
            foreach (var item in items)
            {
                if (item == null) throw new FormatException($"{typeof(T).Name}: null entry");
                var k = key(item);
                if (string.IsNullOrEmpty(k)) throw new FormatException($"{typeof(T).Name} with empty Id");
                if (map.ContainsKey(k)) throw new FormatException($"Duplicate {typeof(T).Name} id '{k}'");
                map[k] = item;
            }
        }

        /// <summary>Cross-reference check; returns human-readable problems (empty = valid).</summary>
        /// <param name="portraitExists">Whether Resources/Portraits/&lt;file&gt; exists (a <see cref="ClassDef.PortraitFile"/>); null skips that
        /// art check (Core can't see the art — the app and the dotnet tests pass their own lookup).</param>
        public static List<string> Validate(ContentDb db, Func<string, bool> portraitExists = null)
        {
            var p = new List<string>();
            p.AddRange(db.LoadProblems);
            ClassValidator.Validate(db, portraitExists, p);
            AbilityValidator.Validate(db, p);
            BuffValidator.Validate(db, p);
            ItemValidator.Validate(db, p);
            StageValidator.Validate(db, p);
            CampaignValidator.Validate(db, p);
            TrapValidator.Validate(db, p);
            ElementCounters.Validate(db, p); // GG-T371: there is always a way (campaign foes, rule-breakers, traps, GG-T359 hazard tiles)
            RegionRuleCatalog.Validate(db, p); // GG-T359
            PickupValidator.Validate(db, p);
            ClassValidator.ValidateSupports(db, p);
            BountyValidator.Validate(db, p);
            ClassValidator.ValidateRoster(db, p);
            EnchantValidator.Validate(db, p);
            SkillTreeValidator.Validate(db, p);
            DialogueValidator.Validate(db, portraitExists, p);
            ArmorSetValidator.Validate(db, p);
            EncounterCatalog.Validate(db, p);
            MercenaryContent.Validate(db, p);
            BannerContent.Validate(db, p); // GG-T1418
            WandererContent.ValidateFactions(db, p);
            WorldContent.Validate(db, db.World, p); // GG-T1666
            BattleLibrary.Validate(db, p); // GG-T1709
            FamilyContent.Validate(db, p); // GG-T971
            ValidateAbilityAliases(db, p); // GG-T973
            ValidateSkillHex(db, p); // GG-T1022
            QuestCatalog.Validate(db, p);
            RewardCatalog.Validate(db, p); // Tomes, Chests, campaign milestones (GG-T301)
            VfxStyleSheet.Validate(db, p);
            VfxCatalog.Validate(db, p);
            AttackTypeSheet.Validate(db, p); // GG-T556
            p.AddRange(GridGame.Core.Battle.DifficultyTable.Problems()); // the 1..10 dial (GG-T309)
            Glossary.Validate(db, p);
            Glossary.ValidateKeywords(db, p); // GG-T907
            CodexValidator.Validate(db, p); // GG-T936
            p.AddRange(db.TextProblems); // GG-T355: every key resolves, no story text in gameplay JSON, right-answer tags
            return p;
        }

        /// <summary>GG-T837: the cues a class's <see cref="SupportDef.Sfx"/> may name — every battle/element/status cue that
        /// has a file under Resources/Audio/Sfx (App Audio maps <c>battle.crucible_wave</c> to <c>battle-crucible-wave-00</c>).
        /// A new cue goes here once its file is made (GridGame.Studio/tools/make_sfx.py).</summary>
        public static readonly HashSet<string> SupportSfxCues = new HashSet<string>
        {
            "battle.cast", "battle.channel", "battle.clang", "battle.crit", "battle.dash", "battle.explosion",
            "battle.fizzle", "battle.glass", "battle.glug", "battle.heal", "battle.hit", "battle.miss", "battle.orb",
            "battle.pickup", "battle.pincer", "battle.powerup", "battle.shatter", "battle.slide", "battle.taunt",
            "battle.trap", "battle.warcry", "battle.whoosh",
            "element.arcane", "element.fire", "element.ice", "element.lightning", "element.poison", "element.steam",
            "element.water",
            "status.bubble", "status.debuff", "status.sleep",
        };

        /// <summary>GG-T296 (§4.1): a trigger lives only on a Passive or Reactive ability, a Reactive must have one that
        /// reacts to an event, and every knob is in range and names real content. Public so a trigger built outside
        /// abilities.json (a reward-granted one, GG-T301) can be checked the same way.</summary>
        public static void ValidateTrigger(ContentDb db, AbilityDef a, List<string> p) => AbilityValidator.ValidateTrigger(db, a, p);

        public const string NpcFlagPrefix = "npc:";
        /// <summary>GG-T1575: the speaker class of the Commander (the player); a speaker only, never a bond hero.</summary>
        public const string CommanderToken = "$commander";
        /// <summary>GG-T1575: "$party:N" is the Nth party member (1-based) when the dialogue opens.</summary>
        public const string PartyTokenPrefix = "$party:";
        /// <summary>GG-T1575: the party hero who trusts the Commander most when the dialogue opens.</summary>
        public const string TrustedToken = "$trusted";
        /// <summary>The highest N a "$party:N" token may name: the party cap (Meta's Progression.MaxPartySize).</summary>
        public const int MaxPartySlots = 6;

        /// <summary>N for a "$party:N" token with N in 1..<see cref="MaxPartySlots"/>, else 0.</summary>
        public static int PartySlot(string token) =>
            token != null && token.StartsWith(PartyTokenPrefix, StringComparison.Ordinal)
            && int.TryParse(token.Substring(PartyTokenPrefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n)
            && n >= 1 && n <= MaxPartySlots ? n : 0;
        /// <summary>GG-T1293: the most one AddTrust / AddCohesion effect may move a bond, either way.</summary>
        public const int DialogueBondMax = 20;
        /// <summary>GG-T1298: the highest bond tier a Trust / Cohesion scene may need (Meta's BondsConfig.MaxTier).</summary>
        public const int MaxBondTier = 5;

        /// <summary>GG-T1623: Cohesion trigger selectors. "role:&lt;Role&gt;" a hero of that Role, "faction:&lt;id&gt;" a hero
        /// of that faction, "faction:same" / "faction:other" a pair that shares / doesn't share a faction, "any" anyone.</summary>
        public const string CohesionRolePrefix = "role:";
        public const string CohesionFactionPrefix = "faction:";
        public const string CohesionSameFaction = "faction:same";
        public const string CohesionOtherFaction = "faction:other";
        public const string CohesionAny = "any";
        /// <summary>GG-T1623: save-flag prefix marking a generic Cohesion scene told for one pair
        /// ("pair:&lt;dialogue&gt;:&lt;a|b&gt;"); ClearFlag can't touch it.</summary>
        public const string PairSceneFlagPrefix = "pair:";

        /// <summary>A Cohesion trigger Id / Other that's a selector rather than a hero class.</summary>
        public static bool IsCohesionSelector(string id) => id != null
            && (id == CohesionAny || id.StartsWith(CohesionRolePrefix, StringComparison.Ordinal) || id.StartsWith(CohesionFactionPrefix, StringComparison.Ordinal));

        /// <summary>GG-T1623: a Cohesion scene with a selector on either side, played by any matching pair ("$with" the
        /// first hero, "$hero" the second) rather than one named pair.</summary>
        public static bool IsGenericCohesion(DialogueDef d) => d?.Trigger?.Kind == DialogueTriggerKind.Cohesion
            && (IsCohesionSelector(d.Trigger.Id) || IsCohesionSelector(d.Trigger.Other));

        /// <summary>GG-T570: a speaker Portrait that names a Portraits/ file rather than a class (<c>aetherist-00</c>: an
        /// asset stem plus its two-digit number), accepted as such when validation can't look on disk.</summary>
        public static readonly System.Text.RegularExpressions.Regex OwnPortraitName =
            new System.Text.RegularExpressions.Regex(@"^[a-z0-9]+(-[a-z0-9]+)*-\d\d$");

        /// <summary>The Merchant's cheap Empty Bottle (GG-T176): an Alchemy recipe that takes one is a "bottle recipe",
        /// brewed from it plus looted monster trophies.</summary>
        public const string EmptyBottle = "empty_bottle";

        /// <summary>Every hero Role a class (or a dialogue's JoinHero) may name.</summary>
        public static readonly HashSet<string> HeroRoles = new HashSet<string> { "Tank", "Healer", "Mage", "Striker", "Support", "Ranged" };
    }
}
