using System;
using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>All loaded content, indexed by id. Immutable after load.</summary>
    public sealed class ContentDb
    {
        public readonly Dictionary<string, ClassDef> Classes = new Dictionary<string, ClassDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, AbilityDef> Abilities = new Dictionary<string, AbilityDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>GG-T973: merged ability id → the ability it was merged into (abilities.json rows with
        /// <see cref="AbilityDef.MergedInto"/>). Never in <see cref="Abilities"/>; a save still naming one is rewritten on load.</summary>
        public readonly Dictionary<string, string> AbilityAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, BuffDef> Buffs = new Dictionary<string, BuffDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, RecipeDef> Recipes = new Dictionary<string, RecipeDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, DropTableDef> DropTables = new Dictionary<string, DropTableDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, StageDef> Stages = new Dictionary<string, StageDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, TrapDef> Traps = new Dictionary<string, TrapDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, PickupDef> Pickups = new Dictionary<string, PickupDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, BountyDef> Bounties = new Dictionary<string, BountyDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>offers.json (GG-T1450): the Shop's offers, in file order (Offers tab order), and by id.</summary>
        public readonly List<OfferDef> OfferOrder = new List<OfferDef>();
        public readonly Dictionary<string, OfferDef> Offers = new Dictionary<string, OfferDef>(StringComparer.OrdinalIgnoreCase);
        public OfferDef TryOffer(string id) => id != null && Offers.TryGetValue(id, out var o) ? o : null;
        /// <summary>banners.json (GG-T1418, §25.10.3): the summon banners, in file order, and by id.</summary>
        public readonly List<BannerDef> BannerOrder = new List<BannerDef>();
        public readonly Dictionary<string, BannerDef> Banners = new Dictionary<string, BannerDef>(StringComparer.OrdinalIgnoreCase);
        public BannerDef TryBanner(string id) => id != null && Banners.TryGetValue(id, out var b) ? b : null;
        public readonly Dictionary<string, EnchantDef> Enchants = new Dictionary<string, EnchantDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>armorenchants.json (GG-T179): passive-granting armor enchants, in file order, and by id.</summary>
        public readonly List<ArmorEnchantDef> ArmorEnchantOrder = new List<ArmorEnchantDef>();
        public readonly Dictionary<string, ArmorEnchantDef> ArmorEnchants = new Dictionary<string, ArmorEnchantDef>(StringComparer.OrdinalIgnoreCase);
        public ArmorEnchantDef TryArmorEnchant(string id) => id != null && ArmorEnchants.TryGetValue(id, out var e) ? e : null;
        public readonly List<StageDef> StageOrder = new List<StageDef>();
        public CampaignDef Campaign = new CampaignDef();
        /// <summary>vfxstyle.json (GG-T253): the element/status look table.</summary>
        public VfxStyleSheet Vfx = new VfxStyleSheet();
        /// <summary>attacktypes.json (GG-T556): which hit sound a physical blow makes (weapon, monster, skill).</summary>
        public AttackTypeSheet AttackTypes = new AttackTypeSheet();
        /// <summary>vfx.json (GG-T269): the effect prefabs vfxstyle.json points at.</summary>
        public VfxCatalog VfxPrefabs = new VfxCatalog();
        /// <summary>sky.json (GG-T576): the living backdrop's Moon, Manalith (per region), shards and parallax.</summary>
        public SkyDef Sky = new SkyDef();
        /// <summary>backdroplayers.json (GG-T576, tools/split_backdrops.py): which backdrops are split into sky and
        /// foreground plates, keyed by file stem (<c>halcyon-00</c>).</summary>
        public Dictionary<string, BackdropLayerDef> BackdropLayers = new Dictionary<string, BackdropLayerDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>elements.json (GG-T371): the elemental interaction table (counters, statuses, tile reactions).</summary>
        public ElementTableDef Elements = new ElementTableDef();
        /// <summary>True when <see cref="ContentLoader.Load"/> found no elements.json (GG-T416): only then is a missing element
        /// table a content error; a hand-built ContentDb (tests, tools) has none and simply skips the GG-T371 checks.</summary>
        public bool ElementsFileMissing;
        /// <summary>What <see cref="ContentLoader.Load"/> had to drop while reading (e.g. a wave naming both a Group and its own
        /// Enemies keeps the Enemies); <see cref="ContentLoader.Validate"/> reports each.</summary>
        public List<string> LoadProblems = new List<string>();
        /// <summary>mercenaries.json (GG-T233), in file order.</summary>
        public readonly List<MercenaryDef> Mercenaries = new List<MercenaryDef>();
        public MercenaryDef TryMercenary(string classId) => Mercenaries.Find(m => string.Equals(m.Class, classId, StringComparison.OrdinalIgnoreCase));
        /// <summary>factions.json (GG-T342): the STORY.md factions and the hero classes in each, in file order.</summary>
        public readonly List<FactionDef> Factions = new List<FactionDef>();
        /// <summary>The faction a hero class belongs to, or null.</summary>
        /// <summary>families.json (GG-T971, BIBLE §25.6.6): the hero families, in file order.</summary>
        public readonly List<FamilyDef> Families = new List<FamilyDef>();
        /// <summary>True when <see cref="ContentLoader.Load"/> found no families.json; a hand-built ContentDb has none and
        /// simply skips the family checks.</summary>
        public bool FamiliesFileMissing;
        /// <summary>skillhex.json (GG-T1022, docs/design/skill-hexagon/PLAN.md): the skill hexagon board. Optional: null
        /// when the file is absent (test fixtures, tools), and then its checks are skipped. Nothing in the game reads it
        /// yet (GG-T1023).</summary>
        public SkillHexDef SkillHex;
        public FamilyDef TryFamily(string id) => id == null ? null : Families.Find(f => f != null && string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
        /// <summary>The family a hero class belongs to (its <see cref="ClassDef.Family"/>), or null.</summary>
        public FamilyDef FamilyOf(string classId) => TryFamily(TryClass(classId)?.Family);
        /// <summary>GG-T1061: a family's colours, read from skillhex.json rather than written twice: the
        /// <see cref="SkillHexStatDef.Color"/> of its wedge's <see cref="SkillHexFamilyDef.From"/> and
        /// <see cref="SkillHexFamilyDef.To"/> corners (#rrggbb). False when there is no board, the family isn't one of its
        /// wedges, or a corner has no colour; a UI then falls back to its own neutral tint.</summary>
        public bool TryFamilyColors(string familyId, out string from, out string to)
        {
            from = to = null;
            if (SkillHex == null || string.IsNullOrEmpty(familyId)) return false;
            var fam = SkillHex.Families?.Find(f => f != null && string.Equals(f.Id, familyId, StringComparison.OrdinalIgnoreCase));
            if (fam == null) return false;
            from = SkillHex.Stats?.Find(s => s != null && string.Equals(s.Id, fam.From, StringComparison.OrdinalIgnoreCase))?.Color;
            to = SkillHex.Stats?.Find(s => s != null && string.Equals(s.Id, fam.To, StringComparison.OrdinalIgnoreCase))?.Color;
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) { from = to = null; return false; }
            return true;
        }
        /// <summary>Every hero class id in a family, by id.</summary>
        public List<string> FamilyMembers(string familyId)
        {
            var ids = new List<string>();
            foreach (var c in Classes.Values)
                if (c.Team == Team.Hero && familyId != null && string.Equals(c.Family, familyId, StringComparison.OrdinalIgnoreCase)) ids.Add(c.Id);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }
        public FactionDef FactionOf(string classId) => classId == null ? null : Factions.Find(f => f.Heroes.Exists(h => string.Equals(h, classId, StringComparison.OrdinalIgnoreCase)));
        public readonly Dictionary<string, CaravanUpgradeDef> Caravan = new Dictionary<string, CaravanUpgradeDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, SkillTreeDef> SkillTrees = new Dictionary<string, SkillTreeDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>dialogues.json in file order (§27.1, GG-T117) — the first matching beat wins, so order matters.</summary>
        public readonly List<DialogueDef> DialogueOrder = new List<DialogueDef>();
        public readonly Dictionary<string, DialogueDef> Dialogues = new Dictionary<string, DialogueDef>(StringComparer.OrdinalIgnoreCase);

        /// <summary>armorsets.json (GG-T182), in file order (campaign level order).</summary>
        public readonly List<ArmorSetDef> ArmorSetOrder = new List<ArmorSetDef>();
        public readonly Dictionary<string, ArmorSetDef> ArmorSets = new Dictionary<string, ArmorSetDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Armor piece item id → the set it belongs to (built at load; a piece is in at most one set).</summary>
        public readonly Dictionary<string, ArmorSetDef> SetByItem = new Dictionary<string, ArmorSetDef>(StringComparer.OrdinalIgnoreCase);

        /// <summary>encounters.json (GG-T184), in file order, and by id.</summary>
        public readonly List<EncounterGroupDef> EncounterOrder = new List<EncounterGroupDef>();
        public readonly Dictionary<string, EncounterGroupDef> Encounters = new Dictionary<string, EncounterGroupDef>(StringComparer.OrdinalIgnoreCase);
        public EncounterGroupDef TryEncounter(string id) => id != null && Encounters.TryGetValue(id, out var g) ? g : null;

        /// <summary>quests.json (§22.11, GG-T237), in file order (journal order), and by id.</summary>
        public readonly List<QuestDef> QuestOrder = new List<QuestDef>();
        public readonly Dictionary<string, QuestDef> Quests = new Dictionary<string, QuestDef>(StringComparer.OrdinalIgnoreCase);
        public QuestDef TryQuest(string id) => id != null && Quests.TryGetValue(id, out var q) ? q : null;

        /// <summary>glossary.json (GG-T300), in file order, and by every Id/Term/Alias (case-insensitive).</summary>
        public readonly List<GlossaryEntry> GlossaryOrder = new List<GlossaryEntry>();
        public readonly Dictionary<string, GlossaryEntry> GlossaryIndex = new Dictionary<string, GlossaryEntry>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The entry a bracketed <c>[term]</c> names, or null.</summary>
        public GlossaryEntry TryTerm(string term) => term != null && GlossaryIndex.TryGetValue(term.Trim(), out var g) ? g : null;

        /// <summary>regionrules.json (GG-T359), in file order, and by id; their tile states and events by id (ids are
        /// shared across rules), each with the rule that owns it.</summary>
        public readonly List<RegionRuleDef> RegionRuleOrder = new List<RegionRuleDef>();
        public readonly Dictionary<string, RegionRuleDef> RegionRules = new Dictionary<string, RegionRuleDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, TileStateDef> TileStates = new Dictionary<string, TileStateDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, RuleEventDef> RuleEvents = new Dictionary<string, RuleEventDef>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> TileStateRule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> RuleEventRule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public RegionRuleDef TryRegionRule(string id) => id != null && RegionRules.TryGetValue(id, out var r) ? r : null;
        public TileStateDef TryTileState(string id) => id != null && TileStates.TryGetValue(id, out var t) ? t : null;
        public RuleEventDef TryRuleEvent(string id) => id != null && RuleEvents.TryGetValue(id, out var e) ? e : null;

        public ArmorSetDef TryArmorSetOf(string itemId) => itemId != null && SetByItem.TryGetValue(itemId, out var a) ? a : null;

        public ClassDef Class(string id) => Get(Classes, id, "class");
        public AbilityDef Ability(string id) => Get(Abilities, id, "ability");
        public BuffDef Buff(string id) => Get(Buffs, id, "buff");
        public ItemDef Item(string id) => Get(Items, id, "item");
        public StageDef Stage(string id) => Get(Stages, id, "stage");
        public RecipeDef Recipe(string id) => Get(Recipes, id, "recipe");

        public AbilityDef TryAbility(string id) => id != null && Abilities.TryGetValue(id, out var a) ? a : null;
        /// <summary>GG-T973: the surviving id for <paramref name="id"/>: its <see cref="AbilityAliases"/> target when it is a
        /// merged ability, else <paramref name="id"/> itself (null stays null).</summary>
        public string ResolveAbility(string id) => id != null && AbilityAliases.TryGetValue(id, out var to) && !string.IsNullOrEmpty(to) ? to : id;
        public ItemDef TryItem(string id) => id != null && Items.TryGetValue(id, out var a) ? a : null;
        public BuffDef TryBuff(string id) => id != null && Buffs.TryGetValue(id, out var a) ? a : null;
        public ClassDef TryClass(string id) => id != null && Classes.TryGetValue(id, out var a) ? a : null;
        public RecipeDef TryRecipe(string id) => id != null && Recipes.TryGetValue(id, out var a) ? a : null;
        public SkillTreeDef TrySkillTree(string id) => id != null && SkillTrees.TryGetValue(id, out var a) ? a : null;

        /// <summary>GG-T355: every story text key the join resolved (key → its Prose id, GUID, version, source), for
        /// debugging and telemetry. Empty when no Content/Text was loaded (a hand-built fixture).</summary>
        public readonly Dictionary<string, TextRef> Texts = new Dictionary<string, TextRef>(StringComparer.Ordinal);
        public TextRef TryText(string key) => key != null && Texts.TryGetValue(key, out var t) ? t : null;
        /// <summary>Whether Content/Text was joined onto the records (<see cref="ProseText.Join"/>).</summary>
        public bool TextJoined;
        /// <summary>What the join found wrong (missing keys, orphans, story text left in gameplay JSON, tag drift);
        /// <see cref="ContentLoader.Validate"/> reports them.</summary>
        public readonly List<string> TextProblems = new List<string>();
        /// <summary>The camp NPCs' blurbs (Prose "npc:&lt;id&gt;" descriptions), by NPC id.</summary>
        public readonly Dictionary<string, string> NpcBlurbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>GG-T570: a Camp shop keeper's barks by occasion ("aetherist/refine", <see cref="DialogueDef.ShopBarks"/>),
        /// the lines of the Prose bark <c>bark:shop/&lt;npc&gt;/&lt;occasion&gt;</c>; a missing text is one placeholder line.</summary>
        public readonly Dictionary<string, List<string>> ShopBarks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>GG-T374: each campaign region's and stage's description (Prose "place:&lt;theme|stage&gt;"), by theme or stage id.</summary>
        public readonly Dictionary<string, string> PlaceBlurbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>GG-T374: each hero faction's description (Prose "faction:&lt;id&gt;"), by faction id.</summary>
        public readonly Dictionary<string, string> FactionBlurbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>GG-T374: each caravan crew member's description (Prose "character:&lt;slug&gt;"), by caravan upgrade id.</summary>
        public readonly Dictionary<string, string> CrewBlurbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>A described thing's words, or null when the map has none (a hand-built fixture).</summary>
        public static string Blurb(Dictionary<string, string> map, string id) => id != null && map.TryGetValue(id, out var b) ? b : null;

        /// <summary>GG-T936: codex.json (the Codex's entries, GG-T924), in file order, and by key ("&lt;kind&gt;:&lt;id&gt;").</summary>
        public readonly List<CodexDef> CodexOrder = new List<CodexDef>();
        public readonly Dictionary<string, CodexDef> Codex = new Dictionary<string, CodexDef>(StringComparer.OrdinalIgnoreCase);
        public CodexDef TryCodex(string key) => key != null && Codex.TryGetValue(key.Trim(), out var c) ? c : null;

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private ThemeDef TryTheme(string id) => id == null ? null : Campaign.Themes.Find(t => Same(t?.Id, id));
        private FactionDef TryFaction(string id) => id == null ? null : Factions.Find(f => Same(f?.Id, id));
        private GlossaryEntry TryGlossary(string id) => id == null ? null : GlossaryOrder.Find(g => Same(g?.Id, id));
        private static bool IsNpc(string id) => id != null && Array.Exists(DialogueDef.Npcs, n => Same(n, id));
        private CaravanUpgradeDef TryCrew(string slug)
        {
            foreach (var u in Caravan.Values)
                if (!string.IsNullOrEmpty(u.Crew) && Same(ProseText.Slug(u.Crew), slug)) return u;
            return null;
        }
        private CrawlDef TryStory(string id)
        {
            if (id == null) return null;
            if (id.StartsWith("chapter/", StringComparison.OrdinalIgnoreCase))
                return Campaign.Chapters.Find(c => Same(c?.Id, id.Substring("chapter/".Length)));
            if (Same(id, "ending")) return Campaign.Ending;
            if (Same(id, "opening") || Same(id, "prologue")) return Campaign.Opening; // GG-T1363: the Codex's prologue is the Opening
            return null;
        }

        /// <summary>GG-T936: whether a Codex entry's record exists: a known <see cref="CodexKinds"/> kind naming a campaign
        /// region or stage, a faction, a hero class / Camp NPC / caravan crew slug, any class (a foe), an item, an ability, a
        /// glossary id, or a story beat ("chapter/&lt;id&gt;", "dialogue/&lt;id&gt;", "ending", "opening" or "prologue").</summary>
        public bool CodexRecordExists(string kind, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            id = id.Trim();
            switch (kind)
            {
                case CodexKinds.Place: return TryTheme(id) != null || TryStage(id) != null;
                case CodexKinds.Faction: return TryFaction(id) != null;
                case CodexKinds.Character: return TryClass(id)?.Team == Team.Hero || IsNpc(id) || TryCrew(id) != null;
                case CodexKinds.Foe: return TryClass(id) != null; // a Bestiary entry: any class met as a foe (a hero class can be a boss)
                case CodexKinds.Item: return Items.ContainsKey(id);
                case CodexKinds.Ability: return Abilities.ContainsKey(id);
                case CodexKinds.Keyword: return TryGlossary(id) != null;
                case CodexKinds.Story:
                    if (id.StartsWith("dialogue/", StringComparison.OrdinalIgnoreCase)) return Dialogues.ContainsKey(id.Substring("dialogue/".Length));
                    return TryStory(id) != null;
                default: return false;
            }
        }

        /// <summary>GG-T936: a Codex entry's title, body and links. codex.json's <see cref="CodexDef.Title"/> and its Prose
        /// body win; otherwise the record's own words: a foe or hero its class Name and Lore, a Camp NPC its blurb, a crew
        /// member theirs, an item or ability its Description, a keyword its glossary Term and Text, a place or faction its
        /// blurb, a chapter its crawl. A body still missing shows <see cref="ProseText.Placeholder"/>; never throws.</summary>
        public CodexEntryView CodexEntry(string kind, string id)
        {
            string key = CodexKinds.Key(kind, id);
            var v = new CodexEntryView { Kind = kind, Id = (id ?? "").Trim().ToLowerInvariant(), Key = key, Exists = CodexRecordExists(kind, id) };
            id = (id ?? "").Trim(); // the same trimmed id CodexRecordExists checked, so every Try* lookup below finds what Exists promised (GG-T1236)
            string title = null, body = null;
            if (v.Exists)
                switch (kind)
                {
                    case CodexKinds.Place:
                        title = TryTheme(id)?.Name ?? TryStage(id)?.Name;
                        body = Blurb(PlaceBlurbs, v.Id);
                        break;
                    case CodexKinds.Faction:
                        { var f = TryFaction(id); title = f.Name; body = Blurb(FactionBlurbs, f.Id); }
                        break;
                    case CodexKinds.Character:
                        if (TryClass(id) is ClassDef hero && hero.Team == Team.Hero) { title = hero.Name; body = hero.Lore; }
                        else if (IsNpc(id)) { title = char.ToUpperInvariant(v.Id[0]) + v.Id.Substring(1); body = Blurb(NpcBlurbs, v.Id); }
                        else { var u = TryCrew(id); title = u.Crew; body = Blurb(CrewBlurbs, u.Id); }
                        break;
                    case CodexKinds.Foe:
                        { var c = TryClass(id); title = c.Name; body = c.Lore; }
                        break;
                    case CodexKinds.Item:
                        { var i = TryItem(id); title = i.Name; body = i.Description; }
                        break;
                    case CodexKinds.Ability:
                        { var a = TryAbility(id); title = a.Name; body = a.Description; }
                        break;
                    case CodexKinds.Keyword:
                        { var g = TryGlossary(id); title = g.Term; body = g.Text; }
                        break;
                    case CodexKinds.Story:
                        if (v.Id.StartsWith("dialogue/", StringComparison.Ordinal))
                        {
                            var d = Dialogues[v.Id.Substring("dialogue/".Length)];
                            title = d.Id;
                            body = d.HasCrawl ? d.Crawl : null;
                        }
                        else { var cr = TryStory(id); title = cr.Title; body = cr.Crawl; }
                        break;
                }
            var def = TryCodex(key);
            if (def != null)
            {
                if (!string.IsNullOrWhiteSpace(def.Title)) title = def.Title;
                if (!string.IsNullOrWhiteSpace(def.Body)) body = def.Body;
                foreach (var r in def.Related)
                    if (CodexKinds.TrySplit(r, out var rk, out var rid) && CodexRecordExists(rk, rid)) v.Related.Add(CodexKinds.Key(rk, rid));
            }
            v.Title = string.IsNullOrWhiteSpace(title) ? v.Id : title;
            v.Body = string.IsNullOrWhiteSpace(body) ? ProseText.Placeholder(CodexDef.TextKey(key)) : body;
            return v;
        }

        private static T Get<T>(Dictionary<string, T> map, string id, string kind)
        {
            if (id != null && map.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"Unknown {kind} '{id}'");
        }

        /// <summary>Campaign stages in play order (themes in order, stages in order).</summary>
        public List<StageDef> CampaignStages()
        {
            var list = new List<StageDef>();
            foreach (var t in Campaign.Themes)
                foreach (var s in t.Stages)
                    if (s != null && Stages.TryGetValue(s, out var def)) list.Add(def); // a null entry is Validate's to report
            return list;
        }

        public StageDef TryStage(string id) => id != null && Stages.TryGetValue(id, out var st) ? st : null;

        public ThemeDef ThemeOf(string stageId)
        {
            // Stages/Validate both key stage ids case-insensitively (StringComparer.OrdinalIgnoreCase) —
            // List<string>.Contains here was ordinal-exact, so a theme's stage id differing only in case
            // from its StageDef.Id passed Validate but silently resolved to no theme (test/standalone stage
            // tracking: no expedition progress, no carried orbs, no post-battle bookkeeping for that stage).
            if (stageId == null) return null;
            foreach (var t in Campaign.Themes)
                foreach (var s in t.Stages)
                    if (string.Equals(s, stageId, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }
    }
}
