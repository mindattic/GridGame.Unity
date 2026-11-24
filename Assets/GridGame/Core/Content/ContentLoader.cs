using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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

        /// <param name="readText">Returns the JSON text for a file name (no extension), or null if absent.</param>
        public static ContentDb Load(Func<string, string> readText)
        {
            var db = new ContentDb();
            AddAll(db.Classes, ReadList<ClassDef>(readText, "classes"), c => c.Id);
            AddAll(db.Abilities, ReadList<AbilityDef>(readText, "abilities"), c => c.Id);
            AddAll(db.Buffs, ReadList<BuffDef>(readText, "buffs"), c => c.Id);
            AddAll(db.Items, ReadList<ItemDef>(readText, "items"), c => c.Id);
            AddAll(db.Recipes, ReadList<RecipeDef>(readText, "recipes"), c => c.Id);
            AddAll(db.DropTables, ReadList<DropTableDef>(readText, "droptables"), c => c.Id);
            var encounters = ReadList<EncounterGroupDef>(readText, "encounters");
            AddAll(db.Encounters, encounters, x => x.Id);
            db.EncounterOrder.AddRange(encounters);
            var stages = ReadList<StageDef>(readText, "stages");
            // A wave that names a group (GG-T184) gets the group's foes, formation and level; Validate reports a bad id.
            foreach (var st in stages)
                for (int wi = 0; wi < st.Waves.Count; wi++)
                {
                    var w = st.Waves[wi];
                    if (w.Group != null && w.Enemies.Count > 0)
                        db.LoadProblems.Add($"stage {st.Id} wave {wi + 1}: names both Group '{w.Group}' and its own Enemies (the Group is ignored; use one)");
                    else if (w.Group != null && db.TryEncounter(w.Group) is EncounterGroupDef g)
                        w.Enemies = EncounterCatalog.Spawns(g, g.Level);
                }
            AddAll(db.Stages, stages, c => c.Id);
            db.StageOrder.AddRange(stages);
            AddAll(db.Traps, ReadList<TrapDef>(readText, "traps"), c => c.Id);
            AddAll(db.Bounties, ReadList<BountyDef>(readText, "bounties"), c => c.Id);
            AddAll(db.Enchants, ReadList<EnchantDef>(readText, "enchants"), c => c.Id);
            var armorEnchants = ReadList<ArmorEnchantDef>(readText, "armorenchants");
            AddAll(db.ArmorEnchants, armorEnchants, x => x.Id);
            db.ArmorEnchantOrder.AddRange(armorEnchants);
            db.Campaign = ReadOne<CampaignDef>(readText, "campaign") ?? new CampaignDef();
            db.Summon = ReadOne<SummonDef>(readText, "summon") ?? new SummonDef();
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

        private static List<T> ReadList<T>(Func<string, string> readText, string name)
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
            void NeedClass(string id, string where) { if (id != null && !db.Classes.ContainsKey(id)) p.Add($"{where}: unknown class '{id}'"); }
            void NeedItem(string id, string where) { if (id != null && !db.Items.ContainsKey(id)) p.Add($"{where}: unknown item '{id}'"); }
            void NeedAbility(string id, string where) { if (id != null && !db.Abilities.ContainsKey(id)) p.Add($"{where}: unknown ability '{id}'"); }
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }

            p.AddRange(db.LoadProblems);
            foreach (var c in db.Classes.Values)
            {
                if (c.DropTable != null && !db.DropTables.ContainsKey(c.DropTable)) p.Add($"class {c.Id}: unknown drop table '{c.DropTable}'");
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
                NeedClass(c.Remains, $"class {c.Id} remains");
                NeedClass(c.LaysEgg, $"class {c.Id} egg");
                if (c.TameBond < 0) p.Add($"class {c.Id}: TameBond can't be negative (GG-T303)");
                foreach (var ph in c.BossPhases) NeedAbility(ph.OnEnterAbility, $"class {c.Id} boss phase");
            }
            foreach (var a in db.Abilities.Values)
            {
                NeedBuff(a.ApplyBuff, $"ability {a.Id}"); NeedBuff(a.RemovesBuff, $"ability {a.Id} removes");
                if (!(a.Pushback >= 0f) || a.Pushback > Battle.BattleConfig.PushbackMaxSeconds)
                    p.Add($"ability {a.Id}: Pushback {a.Pushback}s outside 0..{Battle.BattleConfig.PushbackMaxSeconds}s (GG-T286)");
                if (!(a.CastStagger >= 0f)) p.Add($"ability {a.Id}: CastStagger {a.CastStagger}s can't be negative (GG-T879)");
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
                if (a.MaxRange < 0 || (a.MaxRange > 0 && a.Mode != TargetMode.PickActor))
                    p.Add($"ability {a.Id}: MaxRange is 0 (anywhere) or a positive reach on a PickActor ability");
                // GG-T861: a Smoke Vanish leaves from the caster's own tile (its smoke goes there) to a random one, never a teleport too.
                if (a.Vanishes && (a.Shape != TargetShape.Self || a.Mode != TargetMode.Auto || a.IsTeleport || a.Kind != AbilityKind.Skill))
                    p.Add($"ability {a.Id}: a Smoke Vanish (Vanishes) is a Self / Auto Skill and not also a Teleport");
                // GG-T868: a Pole Vault is a blink whose pick is the free tile beyond a unit beside the caster.
                if (a.Vaults && (!a.IsTeleport || a.Shape != TargetShape.SingleTile || a.Mode != TargetMode.PickTile || a.Filter != TargetFilter.EmptyOnly
                                 || (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Spell)))
                    p.Add($"ability {a.Id}: a Pole Vault (Vaults) is an IsTeleport Skill or Spell that picks an empty tile (SingleTile, PickTile, EmptyOnly)");
                // GG-T388: an Echo picks the one unit whose move it repeats, and carries nothing of its own.
                if (a.Echoes && (a.Shape != TargetShape.SingleActor || a.Mode != TargetMode.PickActor || a.Filter == TargetFilter.EmptyOnly
                                 || (a.Kind != AbilityKind.Skill && a.Kind != AbilityKind.Spell)))
                    p.Add($"ability {a.Id}: an Echo is a Skill or Spell that picks one unit (SingleActor, PickActor)");
                if (a.Echoes && (a.BaseDamage > 0 || a.BaseHeal > 0 || a.ApplyBuff != null || a.GrantsOrbs > 0 || a.Revives || a.IsTeleport || a.SwapsTurns
                                 || a.Knockback > 0 || a.Pull > 0 || a.SwapsPlaces || a.Pushback > 0f || a.HastenU > 0f || a.Cost > 0))
                    p.Add($"ability {a.Id}: an Echo carries no effect or cost of its own (the move it repeats does)");
                // GG-T302: the generated "When [attacked directly]: ..." sentence only uses glossary terms.
                if (a.Trigger != null && db.GlossaryIndex.Count > 0)
                    foreach (var t in Glossary.Terms(Rules.TriggerText.Describe(db, a)))
                        if (db.TryTerm(t) == null) p.Add($"ability {a.Id}: unknown glossary term [{t}] in its trigger sentence");
            }
            foreach (var b in db.Buffs.Values)
            {
                NeedBuff(b.OnExpireApply, $"buff {b.Id}");
                foreach (var c in b.Cancels ?? new List<string>())
                {
                    NeedBuff(c, $"buff {b.Id} Cancels");
                    if (string.Equals(c, b.Id, StringComparison.OrdinalIgnoreCase)) p.Add($"buff {b.Id}: Cancels itself (GG-T667)");
                }
                if (b.Intercepts && b.Kind != BuffKind.Buff) p.Add($"buff {b.Id}: Intercepts must be a Buff (GG-T533)");
                if (!(b.EvasionBonus >= -Rules.Formulas.EvasionCap && b.EvasionBonus <= Rules.Formulas.EvasionCap))
                    p.Add($"buff {b.Id}: EvasionBonus {b.EvasionBonus} outside -{Rules.Formulas.EvasionCap}..{Rules.Formulas.EvasionCap} (GG-T297)");
            }
            foreach (var i in db.Items.Values)
            {
                NeedAbility(i.OnUseAbility, $"item {i.Id}");
                if (!(i.EvasionBonus >= 0f && i.EvasionBonus <= 20f))
                    p.Add($"item {i.Id}: EvasionBonus {i.EvasionBonus} outside 0..20 (GG-T297)");
                NeedBuff(i.CuresBuff, $"item {i.Id} cures");
                NeedBuff(i.PincerApplyBuff, $"item {i.Id} pincer buff");
                if (i.Type == ItemType.RecipeScroll)
                {
                    if (i.TeachesRecipe == null) p.Add($"item {i.Id}: RecipeScroll needs TeachesRecipe");
                    else if (!db.Recipes.ContainsKey(i.TeachesRecipe)) p.Add($"item {i.Id}: unknown recipe '{i.TeachesRecipe}'");
                }
            }
            foreach (var r in db.Recipes.Values)
            {
                NeedItem(r.Result, $"recipe {r.Id}");
                NeedItem(r.BaseItem, $"recipe {r.Id} base");
                foreach (var ic in r.Inputs) NeedItem(ic.Item, $"recipe {r.Id}");
                NeedItem(r.PotencyResult, $"recipe {r.Id} potency");
                foreach (var ic in r.PotencyInputs) NeedItem(ic.Item, $"recipe {r.Id} potency");
                // GG-T177 (§25.0.6): a starter recipe is the guided first forge — always affordable, so no gold.
                if (r.Starter && (r.Station != CraftStation.Forge || r.BaseItem != null || r.Tier > 1 || r.Gold != 0 || r.ResultCount != 1
                                  || (db.Items.TryGetValue(r.Result ?? "", out var made) && made.Slot != EquipmentSlot.Weapon)))
                    p.Add($"recipe {r.Id}: a Starter recipe is an Apprentice Forge recipe for one weapon that costs no gold");
            }
            // GG-T432 (§25.12.2): a facility is a caravan upgrade whose levels name and gate a rising material tier.
            foreach (var u in db.Caravan.Values)
            {
                bool facilityId = u.Id == CaravanUpgradeDef.ForgeFacilityId || u.Id == CaravanUpgradeDef.AlchemyFacilityId;
                if (u.IsFacility != facilityId) { p.Add($"caravan {u.Id}: only '{CaravanUpgradeDef.ForgeFacilityId}' and '{CaravanUpgradeDef.AlchemyFacilityId}' are facilities, and each needs a BaseRarity"); continue; }
                if (!u.IsFacility) continue;
                if (string.IsNullOrEmpty(u.BaseName)) p.Add($"caravan {u.Id}: a facility needs a BaseName");
                var prev = u.BaseRarity.Value;
                for (int i = 0; i < u.Levels.Count; i++)
                {
                    var lv = u.Levels[i];
                    if (string.IsNullOrEmpty(lv.Name)) p.Add($"caravan {u.Id} level {i + 1}: a facility level needs a Name");
                    if (lv.Cost <= 0) p.Add($"caravan {u.Id} level {i + 1}: a facility level costs gold");
                    if (!lv.Rarity.HasValue || lv.Rarity.Value <= prev) p.Add($"caravan {u.Id} level {i + 1}: Rarity must rise above the level before it");
                    else prev = lv.Rarity.Value;
                }
            }
            foreach (var d in db.DropTables.Values)
                foreach (var e in d.Entries) NeedItem(e.Item, $"droptable {d.Id}");
            foreach (var s in db.Stages.Values)
                foreach (var sc in s.Scenes ?? new List<TutorialSceneDef>())
                {
                    if (string.IsNullOrEmpty(sc.Id)) p.Add($"stage {s.Id}: a tutorial scene needs an Id");
                    NeedClass(sc.Class, $"stage {s.Id} scene {sc.Id}");
                    if (sc.Pickup != null && !db.Pickups.ContainsKey(sc.Pickup)) p.Add($"stage {s.Id} scene {sc.Id}: unknown pickup '{sc.Pickup}'");
                }
            foreach (var s in db.Stages.Values)
                foreach (var w in s.Waves)
                {
                    foreach (var e in w.Enemies) NeedClass(e.Class, $"stage {s.Id}");
                    foreach (var g in w.Guests) NeedClass(g.Class, $"stage {s.Id} guest");
                    foreach (var r in w.Recruitables) RecruitableDef.Validate(db, r, $"stage {s.Id} recruitable", p);
                    NeedClass(w.SurpriseSpeaker, $"stage {s.Id} surprise speaker");
                    RuleBreaks.Validate(db, s, w, p);
                }
            // ContentDb.ThemeOf resolves a stage id to the *first* theme that lists it — a stage accidentally
            // listed in two themes would silently belong to the wrong one there (expedition tracking,
            // carried orbs) with no error here to catch it, so every stage id must be theme-unique.
            var stageThemeOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in db.Campaign.Themes)
                foreach (var s in t.Stages)
                {
                    if (s == null) p.Add($"theme {t.Id}: a null entry in Stages");
                    else if (!db.Stages.ContainsKey(s)) p.Add($"theme {t.Id}: unknown stage '{s}'");
                    else if (stageThemeOwner.TryGetValue(s, out var owner)) p.Add($"theme {t.Id}: stage '{s}' already belongs to theme '{owner}'");
                    else stageThemeOwner[s] = t.Id;
                }
            // GG-T677: every orb source is finite, so the most a save can ever own is fixed by content; it may not pass the cap.
            int orbSources = Battle.BattleConfig.StartOwnedOrbs + Battle.BattleConfig.MaxBrokenOrbs;
            foreach (var t in db.Campaign.Themes)
            {
                if (t.OrbFindChance < 0f || t.OrbFindChance > 1f) p.Add($"theme {t.Id}: OrbFindChance {t.OrbFindChance} outside 0..1 (GG-T677)");
                if (t.MilestoneOrb) orbSources++;
                if (t.OrbFindChance > 0f) orbSources += t.Stages.Count;
            }
            if (orbSources > Battle.BattleConfig.MaxOrbCapacity)
                p.Add($"campaign.json: {orbSources} Mana Orbs can be owned, past the cap of {Battle.BattleConfig.MaxOrbCapacity} (GG-T677)");
            SkyDef.Validate(db, p); // GG-T576
            ValidateCrawl(db, db.Campaign.Prologue, "campaign Prologue", false, p);
            ValidateCrawl(db, db.Campaign.Ending, "campaign Ending", true, p);
            ValidateChapters(db, p);
            foreach (var t in db.Traps.Values) NeedBuff(t.ApplyBuff, $"trap {t.Id}");
            ElementCounters.Validate(db, p); // GG-T371: there is always a way (campaign foes, rule-breakers, traps, GG-T359 hazard tiles)
            RegionRuleCatalog.Validate(db, p); // GG-T359
            foreach (var pk in db.Pickups.Values)
            {
                NeedBuff(pk.ApplyBuff, $"pickup {pk.Id}");
                if (pk.Weight <= 0) p.Add($"pickup {pk.Id}: weight must be positive");
            }
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
            foreach (var c in db.Summon.Pool) NeedClass(c, "summon pool");
            foreach (var e in db.Enchants.Values) NeedItem(e.Essence, $"enchant {e.Id}");
            // GG-T179: an armor enchant grants a real Passive ability and costs real materials.
            foreach (var e in db.ArmorEnchantOrder)
            {
                string w = $"armor enchant {e.Id}";
                if (string.IsNullOrWhiteSpace(e.Name)) p.Add($"{w}: needs a Name");
                if (!db.Abilities.TryGetValue(e.Passive ?? "", out var passive)) p.Add($"{w}: unknown passive '{e.Passive}'");
                else if (passive.Kind != AbilityKind.Passive && passive.Kind != AbilityKind.Reactive) p.Add($"{w}: '{e.Passive}' isn't a Passive or Reactive ability");
                if (e.Inputs.Count == 0) p.Add($"{w}: needs inputs");
                foreach (var ic in e.Inputs) { NeedItem(ic.Item, w); if (ic.Count <= 0) p.Add($"{w}: input counts must be positive"); }
                if (e.Gold < 0) p.Add($"{w}: gold can't be negative");
                if (e.Tier < 1 || e.Tier > 3) p.Add($"{w}: tier must be 1-3");
            }

            // Skill trees (§25.6.1, GG-T50): every node's grant and prerequisites must resolve, and every
            // hero class must name a home tree that exists.
            // GG-T213: node ids are global (a learned node is saved by id alone), so a clash across trees is rejected too.
            var allNodeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in db.SkillTrees.Values)
            {
                var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var n in t.Nodes)
                {
                    if (string.IsNullOrEmpty(n.Id)) { p.Add($"tree {t.Id}: node with empty Id"); continue; }
                    if (!nodeIds.Add(n.Id)) p.Add($"tree {t.Id}: duplicate node id '{n.Id}'");
                    else if (allNodeIds.TryGetValue(n.Id, out var otherTree)) p.Add($"tree {t.Id}: node id '{n.Id}' is already used in tree {otherTree}");
                    else allNodeIds[n.Id] = t.Id;
                }
                foreach (var n in t.Nodes)
                {
                    // GG-T77: a node grants either an ability/passive or a flat stat bonus, never both or neither.
                    if (n.GrantsStat != null)
                    {
                        if (!string.IsNullOrEmpty(n.Grants)) p.Add($"tree {t.Id} node {n.Id}: Grants and GrantsStat are mutually exclusive");
                        if (!StatGrantDef.IsStatName(n.GrantsStat.Stat)) p.Add($"tree {t.Id} node {n.Id}: unknown stat '{n.GrantsStat.Stat}' (use {string.Join("/", StatGrantDef.Names)})");
                        if (n.GrantsStat.Amount <= 0f) p.Add($"tree {t.Id} node {n.Id}: stat bonus must be positive");
                    }
                    else if (string.IsNullOrEmpty(n.Grants)) p.Add($"tree {t.Id} node {n.Id}: needs Grants (an ability) or GrantsStat (a stat bonus)");
                    else NeedAbility(n.Grants, $"tree {t.Id} node {n.Id}");
                    if (n.Cost <= 0) p.Add($"tree {t.Id} node {n.Id}: cost must be positive");
                    if (n.RequiredPointsInTree < 0) p.Add($"tree {t.Id} node {n.Id}: required points can't be negative");
                    foreach (var req in n.Prerequisites)
                    {
                        if (!nodeIds.Contains(req)) p.Add($"tree {t.Id} node {n.Id}: unknown prerequisite '{req}'");
                        else if (req == n.Id) p.Add($"tree {t.Id} node {n.Id}: can't require itself");
                    }
                }
                // Cycle check: walk each node's prerequisite chain and fail only if it revisits a node
                // already on the *current* path. Two branches sharing a common ancestor (e.g. a finale
                // node requiring two capstones that both trace back to the same root) is a valid diamond,
                // not a cycle — tracking "seen anywhere" instead of "seen on this path" flagged those too.
                foreach (var n in t.Nodes)
                {
                    var onPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (HasCycle(n)) p.Add($"tree {t.Id} node {n.Id}: prerequisite cycle");

                    bool HasCycle(SkillNodeDef cur)
                    {
                        if (!onPath.Add(cur.Id)) return true;
                        foreach (var req in cur.Prerequisites)
                        {
                            var reqNode = t.Nodes.Find(x => string.Equals(x.Id, req, StringComparison.OrdinalIgnoreCase));
                            if (reqNode != null && HasCycle(reqNode)) return true;
                        }
                        onPath.Remove(cur.Id);
                        return false;
                    }
                }
            }
            foreach (var c in db.Classes.Values)
            {
                if (c.Team != Team.Hero) continue;
                if (string.IsNullOrEmpty(c.HomeTree)) p.Add($"class {c.Id}: hero needs a HomeTree");
                else if (!db.SkillTrees.ContainsKey(c.HomeTree)) p.Add($"class {c.Id}: unknown home tree '{c.HomeTree}'");
            }
            foreach (var d in db.DialogueOrder) ValidateDialogue(db, d, portraitExists, p);
            foreach (var set in db.ArmorSetOrder) ValidateArmorSet(db, set, p);
            EncounterCatalog.Validate(db, p);
            MercenaryContent.Validate(db, p);
            WandererContent.ValidateFactions(db, p);
            QuestCatalog.Validate(db, p);
            RewardCatalog.Validate(db, p); // Tomes, Chests, campaign milestones (GG-T301)
            VfxStyleSheet.Validate(db, p);
            VfxCatalog.Validate(db, p);
            AttackTypeSheet.Validate(db, p); // GG-T556
            p.AddRange(GridGame.Core.Battle.DifficultyTable.Problems()); // the 1..10 dial (GG-T309)
            Glossary.Validate(db, p);
            ValidateCodex(db, p); // GG-T936
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

        /// <summary>Armor sets (GG-T182): five real armor pieces, one per armor slot, all the set's weight, no piece in
        /// two sets, and bonuses at distinct piece counts between 2 and 5.</summary>
        /// <summary>GG-T936: every codex.json Key and Related key names a known <see cref="CodexKinds"/> kind and a record
        /// that exists (<see cref="ContentDb.CodexRecordExists"/>), in its lower-case form, once.</summary>
        private static void ValidateCodex(ContentDb db, List<string> p)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string Check(string key, string where)
            {
                if (!CodexKinds.TrySplit(key?.Trim(), out var kind, out var id)) return $"{where}: '{key}' is not '<kind>:<id>'";
                if (Array.IndexOf(CodexKinds.All, kind) < 0) return $"{where}: unknown codex kind '{kind}' in '{key}'";
                if (!db.CodexRecordExists(kind, id)) return $"{where}: '{key}' names no {kind} record";
                if (key.Trim() != CodexKinds.Key(kind, id)) return $"{where}: '{key}' should be '{CodexKinds.Key(kind, id)}' (lower case)";
                return null;
            }
            foreach (var e in db.CodexOrder)
            {
                string where = $"codex {e.Key}";
                var bad = Check(e.Key, "codex entry");
                if (bad != null) { p.Add(bad); continue; }
                if (!seen.Add(e.Key.Trim())) p.Add($"{where}: listed twice");
                foreach (var r in e.Related)
                {
                    var rb = Check(r, where + " Related");
                    if (rb != null) p.Add(rb);
                    else if (string.Equals(r.Trim(), e.Key.Trim(), StringComparison.OrdinalIgnoreCase)) p.Add($"{where}: relates to itself");
                }
            }
        }

        private static void ValidateArmorSet(ContentDb db, ArmorSetDef set, List<string> p)
        {
            string w = $"armor set {set.Id}";
            if (string.IsNullOrWhiteSpace(set.Name)) p.Add($"{w}: needs a Name");
            if (set.Pieces.Count != 5) p.Add($"{w}: needs exactly 5 pieces, has {set.Pieces.Count}");
            var slots = new HashSet<EquipmentSlot>();
            foreach (var id in set.Pieces)
            {
                var item = db.TryItem(id);
                if (item == null) { p.Add($"{w}: unknown item '{id}'"); continue; }
                if (item.Type != ItemType.Equipment || !IsArmorSlot(item.Slot)) { p.Add($"{w}: '{id}' isn't a Head/Torso/Legs/Gloves/Boots piece"); continue; }
                if (!slots.Add(item.Slot)) p.Add($"{w}: two pieces for {item.Slot}");
                if (item.ArmorWeight != set.Weight) p.Add($"{w}: '{id}' is {item.ArmorWeight}, set is {set.Weight}");
                if (db.TryArmorSetOf(id) != set) p.Add($"{w}: '{id}' already belongs to set '{db.TryArmorSetOf(id)?.Id}'");
            }
            var counts = new HashSet<int>();
            foreach (var b in set.Bonuses)
            {
                if (b == null) { p.Add($"{w}: null bonus"); continue; }
                if (b.Pieces < 2 || b.Pieces > 5) p.Add($"{w}: bonus at {b.Pieces} pieces (must be 2-5)");
                if (!counts.Add(b.Pieces)) p.Add($"{w}: two bonuses at {b.Pieces} pieces");
            }
            if (set.Bonuses.Count == 0) p.Add($"{w}: has no bonuses");
        }

        private static bool IsArmorSlot(EquipmentSlot s) =>
            s == EquipmentSlot.Head || s == EquipmentSlot.Torso || s == EquipmentSlot.Legs || s == EquipmentSlot.Gloves || s == EquipmentSlot.Boots;

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

        /// <summary>GG-T231: a campaign crawl (Prologue/Ending) is optional, but one that exists needs text, a known
        /// backdrop theme, and (the Ending) a campaign stage to follow.</summary>
        private static void ValidateCrawl(ContentDb db, CrawlDef crawl, string w, bool needsStage, List<string> p)
        {
            if (crawl == null) return;
            if (string.IsNullOrWhiteSpace(crawl.Title)) p.Add($"{w}: needs a Title");
            if (string.IsNullOrWhiteSpace(crawl.Crawl)) p.Add($"{w}: needs Crawl text");
            if (crawl.Backdrop != null && !db.Campaign.Themes.Exists(t => string.Equals(t.Id, crawl.Backdrop, StringComparison.OrdinalIgnoreCase))) p.Add($"{w}: unknown backdrop theme '{crawl.Backdrop}'");
            if (needsStage && (crawl.Stage == null || db.ThemeOf(crawl.Stage) == null)) p.Add($"{w}: needs a campaign Stage, has '{crawl.Stage}'");
            if (!needsStage && crawl.Stage != null) p.Add($"{w}: only the Ending names a Stage");
        }

        /// <summary>Campaign stages per chapter crawl (GG-T386): a chapter scroll plays after every fifth stage.</summary>
        public const int StagesPerChapter = 5;

        /// <summary>GG-T386: the chapter crawls. Each has an id, a title, text and a known backdrop, and follows a campaign
        /// stage; in campaign order, every fifth campaign stage (5, 10, 15, ...) has exactly one, and no other stage has
        /// one. None may follow the Ending's stage (that one scrolls the ending), so two crawls never play back to back.</summary>
        private static void ValidateChapters(ContentDb db, List<string> p)
        {
            var stages = db.CampaignStages();
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < stages.Count; i++) index[stages[i].Id] = i;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var byStage = new Dictionary<int, string>();
            int last = -1;
            foreach (var ch in db.Campaign.Chapters)
            {
                string w = $"campaign chapter '{ch?.Id}'";
                if (ch == null) { p.Add("campaign Chapters: null entry"); continue; }
                if (string.IsNullOrWhiteSpace(ch.Id) || !System.Text.RegularExpressions.Regex.IsMatch(ch.Id, "^[a-z0-9]+(-[a-z0-9]+)*$"))
                    p.Add($"{w}: needs an Id of lowercase letters, digits and dashes");
                else if (!ids.Add(ch.Id)) p.Add($"{w}: duplicate id");
                if (string.IsNullOrWhiteSpace(ch.Title)) p.Add($"{w}: needs a Title");
                if (string.IsNullOrWhiteSpace(ch.Crawl)) p.Add($"{w}: needs Crawl text");
                if (ch.Backdrop != null && !db.Campaign.Themes.Exists(t => string.Equals(t.Id, ch.Backdrop, StringComparison.OrdinalIgnoreCase)))
                    p.Add($"{w}: unknown backdrop theme '{ch.Backdrop}'");
                if (ch.Stage == null || !index.TryGetValue(ch.Stage, out int i)) { p.Add($"{w}: needs a campaign Stage, has '{ch.Stage}'"); continue; }
                if ((i + 1) % StagesPerChapter != 0) p.Add($"{w}: follows '{ch.Stage}', campaign stage {i + 1}, which is not a multiple of {StagesPerChapter}");
                if (byStage.TryGetValue(i, out var other)) p.Add($"{w}: '{ch.Stage}' already has chapter '{other}'");
                else byStage[i] = ch.Id;
                if (i <= last) p.Add($"{w}: out of campaign order");
                last = Math.Max(last, i);
                if (db.Campaign.Ending?.Stage != null && string.Equals(db.Campaign.Ending.Stage, ch.Stage, StringComparison.OrdinalIgnoreCase))
                    p.Add($"{w}: follows the Ending's stage, which already scrolls the ending");
            }
            for (int i = StagesPerChapter - 1; i < stages.Count; i += StagesPerChapter)
                if (!byStage.ContainsKey(i)) p.Add($"campaign Chapters: campaign stage {i + 1} ('{stages[i].Id}') has no chapter crawl");
        }

        public const string NpcFlagPrefix = "npc:";

        /// <summary>GG-T570: a speaker Portrait that names a Portraits/ file rather than a class (<c>aetherist-00</c>: an
        /// asset stem plus its two-digit number), accepted as such when validation can't look on disk.</summary>
        public static readonly System.Text.RegularExpressions.Regex OwnPortraitName =
            new System.Text.RegularExpressions.Regex(@"^[a-z0-9]+(-[a-z0-9]+)*-\d\d$");

        /// <summary>The Merchant's cheap Empty Bottle (GG-T176): an Alchemy recipe that takes one is a "bottle recipe",
        /// brewed from it plus looted monster trophies.</summary>
        public const string EmptyBottle = "empty_bottle";

        /// <summary>Every hero Role a class (or a dialogue's JoinHero) may name.</summary>
        public static readonly HashSet<string> HeroRoles = new HashSet<string> { "Tank", "Healer", "Mage", "Striker", "Support", "Ranged" };

        /// <summary>One dialogue (§27.1, GG-T117): every reference resolves, every node is reachable from the start,
        /// and every choice list has a way out that no condition can hide — same severity as a dangling class id.</summary>
        private static void ValidateDialogue(ContentDb db, DialogueDef d, Func<string, bool> portraitExists, List<string> p)
        {
            string w = $"dialogue {d.Id}";
            bool IsNpc(string id) => id != null && Array.IndexOf(DialogueDef.Npcs, id) >= 0;
            bool IsHero(string id) => db.TryClass(id)?.Team == Team.Hero;
            var trigger = d.Trigger ?? new DialogueTrigger();
            switch (trigger.Kind)
            {
                case DialogueTriggerKind.ThemeStart:
                    if (!db.Campaign.Themes.Exists(t => string.Equals(t.Id, trigger.Id, StringComparison.OrdinalIgnoreCase))) p.Add($"{w}: unknown trigger theme '{trigger.Id}'");
                    break;
                case DialogueTriggerKind.StageCleared:
                    if (trigger.Id == null || !db.Stages.ContainsKey(trigger.Id)) p.Add($"{w}: unknown trigger stage '{trigger.Id}'");
                    break;
                case DialogueTriggerKind.Talk:
                    if (trigger.Id == "party") break;
                    if (trigger.Id != null && trigger.Id.StartsWith(NpcFlagPrefix)) { if (!IsNpc(trigger.Id.Substring(NpcFlagPrefix.Length))) p.Add($"{w}: unknown Talk NPC '{trigger.Id}'"); }
                    else if (!IsHero(trigger.Id)) p.Add($"{w}: Talk needs \"party\", \"npc:<id>\" or a hero class, has '{trigger.Id}'");
                    break;
                case DialogueTriggerKind.Recruit:
                    // GG-T132: spoken on the battle board, so no save effects or choices; "$with" is the hero talking.
                    if (!IsHero(trigger.Id)) p.Add($"{w}: Recruit needs the recruitable's hero class, has '{trigger.Id}'");
                    if (d.AllEffects().GetEnumerator().MoveNext()) p.Add($"{w}: a Recruit dialogue can't have effects (the battle applies none)");
                    if (d.Nodes.Exists(n => n != null && n.Choices.Count > 0)) p.Add($"{w}: a Recruit dialogue can't have choices");
                    if (!d.Nodes.Exists(n => n != null && string.Equals(n.Id, RecruitableDef.JoinNode, StringComparison.OrdinalIgnoreCase))) p.Add($"{w}: a Recruit dialogue needs a '{RecruitableDef.JoinNode}' node");
                    break;
                case DialogueTriggerKind.LevelReached:
                    if (trigger.Level < 2) p.Add($"{w}: LevelReached needs a Level of 2 or more, has {trigger.Level}");
                    break;
                case DialogueTriggerKind.StageStart:
                    if (trigger.Id == null || !db.Stages.ContainsKey(trigger.Id)) p.Add($"{w}: unknown StageStart stage '{trigger.Id}'");
                    break;
                case DialogueTriggerKind.Wanderer:
                    // GG-T342: "$hero" is the wanderer, "$with" the companion talking; the shape is WandererContent's.
                    WandererContent.ValidateDialogue(db, d, p);
                    break;
            }
            if (d.Crawl != null)
            {
                if (trigger.Kind == DialogueTriggerKind.Talk || trigger.Kind == DialogueTriggerKind.Recruit) p.Add($"{w}: only a story beat can open with a Crawl");
                if (string.IsNullOrWhiteSpace(d.Crawl)) p.Add($"{w}: empty Crawl");
                if (string.IsNullOrWhiteSpace(d.Title)) p.Add($"{w}: a Crawl needs a Title to head it");
            }
            bool talkWithHero = (trigger.Kind == DialogueTriggerKind.Talk || trigger.Kind == DialogueTriggerKind.Recruit) && trigger.Id != null && !trigger.Id.StartsWith(NpcFlagPrefix)
                || trigger.Kind == DialogueTriggerKind.Wanderer;
            bool wanderer = trigger.Kind == DialogueTriggerKind.Wanderer;
            bool joinsHero = false;
            foreach (var e in d.AllEffects()) joinsHero |= e?.Type == DialogueEffectType.JoinHero;

            void Cond(DialogueCondition c, string where)
            {
                if (c == null) { p.Add($"{where}: null condition"); return; }
                switch (c.Type)
                {
                    case DialogueConditionType.FlagSet:
                    case DialogueConditionType.FlagNotSet:
                        if (string.IsNullOrEmpty(c.Id)) p.Add($"{where}: {c.Type} needs a flag Id"); break;
                    case DialogueConditionType.HasItem:
                        if (!db.Items.ContainsKey(c.Id ?? "")) p.Add($"{where}: unknown item '{c.Id}'"); break;
                    case DialogueConditionType.GoldAtLeast:
                        if (c.Amount <= 0) p.Add($"{where}: GoldAtLeast needs a positive Amount"); break;
                    case DialogueConditionType.PartyHas:
                    case DialogueConditionType.RosterHas:
                    case DialogueConditionType.RosterLacks:
                        if (!IsHero(c.Id)) p.Add($"{where}: {c.Type} needs a hero class, has '{c.Id}'"); break;
                    case DialogueConditionType.NpcJoined:
                        if (!IsNpc(c.Id)) p.Add($"{where}: unknown NPC '{c.Id}'"); break;
                    case DialogueConditionType.QuestAvailable:
                    case DialogueConditionType.QuestActive:
                    case DialogueConditionType.QuestDone:
                        if (db.TryQuest(c.Id) == null) p.Add($"{where}: unknown quest '{c.Id}'"); break;
                }
            }
            void Effect(DialogueEffect e, string where)
            {
                if (e == null) { p.Add($"{where}: null effect"); return; }
                switch (e.Type)
                {
                    case DialogueEffectType.SetFlag:
                    case DialogueEffectType.ClearFlag:
                        if (string.IsNullOrEmpty(e.Id)) p.Add($"{where}: {e.Type} needs a flag Id");
                        else if (e.Id.StartsWith(NpcFlagPrefix)) p.Add($"{where}: flag '{e.Id}' is reserved — use JoinNpc");
                        else if (e.Id.StartsWith("sp:")) p.Add($"{where}: flag '{e.Id}' is reserved — use GrantSkillPoint");
                        break;
                    case DialogueEffectType.GrantSkillPoint:
                        if (e.Id == null || e.Id == "$with") { if (!talkWithHero) p.Add($"{where}: GrantSkillPoint to \"$with\" only fits a Talk with a party member — name a hero class otherwise"); }
                        else if (!IsHero(e.Id)) p.Add($"{where}: GrantSkillPoint needs a hero class or \"$with\", has '{e.Id}'");
                        if (e.Amount < 0) p.Add($"{where}: GrantSkillPoint can't take points away");
                        break;
                    case DialogueEffectType.GrantGold:
                        if (e.Amount == 0) p.Add($"{where}: GrantGold needs a non-zero Amount"); break;
                    case DialogueEffectType.GrantItem:
                        if (!db.Items.ContainsKey(e.Id ?? "")) p.Add($"{where}: unknown item '{e.Id}'");
                        if (e.Amount < 0) p.Add($"{where}: GrantItem can't take items away"); break;
                    case DialogueEffectType.RecruitHero:
                        if (!IsHero(e.Id)) p.Add($"{where}: RecruitHero needs a hero class, has '{e.Id}'"); break;
                    case DialogueEffectType.JoinNpc:
                        if (!IsNpc(e.Id)) p.Add($"{where}: unknown NPC '{e.Id}'"); break;
                    case DialogueEffectType.JoinHero:
                        if (e.Id == null && e.Role == null) p.Add($"{where}: JoinHero needs a hero class Id, a Role, or both");
                        if (e.Id != null && !IsHero(e.Id)) p.Add($"{where}: JoinHero needs a hero class, has '{e.Id}'");
                        if (e.Role != null && !HeroRoles.Contains(e.Role)) p.Add($"{where}: JoinHero has unknown Role '{e.Role}'");
                        break;
                    case DialogueEffectType.StartQuest:
                        if (db.TryQuest(e.Id) == null) p.Add($"{where}: unknown quest '{e.Id}'"); break;
                    case DialogueEffectType.OpenStation:
                        if (e.Id == null || Array.IndexOf(DialogueDef.Stations, e.Id) < 0) p.Add($"{where}: unknown Camp station '{e.Id}'"); break;
                }
                // GG-T537 (owner): a Camp vendor joins on a story beat of the campaign (clear a stage), never on an
                // incidental trigger, and always: the beat is a StageCleared one with no conditions.
                if ((e.Type == DialogueEffectType.JoinNpc || e.Type == DialogueEffectType.OpenStation)
                    && (trigger.Kind != DialogueTriggerKind.StageCleared || d.Conditions.Count > 0))
                    p.Add($"{where}: {e.Type} belongs in an unconditional StageCleared story beat (GG-T537)");
            }
            foreach (var c in d.Conditions) Cond(c, w);

            var speakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in d.Speakers)
            {
                if (s == null || string.IsNullOrEmpty(s.Id)) { p.Add($"{w}: speaker with empty Id"); continue; }
                if (!speakers.Add(s.Id)) p.Add($"{w}: duplicate speaker '{s.Id}'");
                if (s.Class == "$lead") { }
                else if (s.Class == "$with") { if (!talkWithHero) p.Add($"{w} speaker {s.Id}: \"$with\" only fits a Talk with a party member"); }
                else if (s.Class == "$hero") { if (!wanderer) p.Add($"{w} speaker {s.Id}: \"$hero\" only fits a Wanderer talk"); }
                else if (s.Class == "$recruit") { if (!joinsHero) p.Add($"{w} speaker {s.Id}: \"$recruit\" needs a JoinHero effect in this dialogue"); }
                else if (s.Class != null) { if (db.TryClass(s.Class) == null) p.Add($"{w} speaker {s.Id}: unknown class '{s.Class}'"); }
                else if (string.IsNullOrEmpty(s.Portrait) || string.IsNullOrEmpty(s.Name)) p.Add($"{w} speaker {s.Id}: needs a Class, or a Name and a Portrait");
                // A speaker's Portrait names the class whose art it shows (GG-T557), or (GG-T570) a Portraits/ file no class
                // draws: a Camp NPC with art of its own (the Aetherist, aetherist-00), which must then be on disk.
                var portrait = s.Portrait ?? (s.Class != null && !s.Class.StartsWith("$") ? s.Class : null);
                var pc = portrait == null ? null : db.TryClass(portrait);
                if (portrait != null && pc == null)
                {
                    bool ownFile = portraitExists != null ? portraitExists(portrait) : OwnPortraitName.IsMatch(portrait);
                    if (!ownFile) p.Add($"{w} speaker {s.Id}: portrait '{portrait}' is not a class or a portrait file");
                }
                else if (pc != null && portraitExists != null && !portraitExists(pc.PortraitFile())) p.Add($"{w} speaker {s.Id}: missing portrait '{pc.PortraitFile()}'");
            }

            if (d.Nodes.Count == 0) { p.Add($"{w}: has no nodes"); return; }
            var nodes = new Dictionary<string, DialogueNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in d.Nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.Id)) { p.Add($"{w}: node with empty Id"); continue; }
                if (nodes.ContainsKey(n.Id)) p.Add($"{w}: duplicate node '{n.Id}'"); else nodes[n.Id] = n;
            }
            string start = d.Start ?? d.Nodes[0]?.Id;
            if (start == null || !nodes.ContainsKey(start)) p.Add($"{w}: unknown start node '{start}'");
            foreach (var n in nodes.Values)
            {
                string nw = $"{w} node {n.Id}";
                if (n.Speaker == null || !speakers.Contains(n.Speaker)) p.Add($"{nw}: unknown speaker '{n.Speaker}'");
                if (string.IsNullOrWhiteSpace(n.Text)) p.Add($"{nw}: empty text");
                bool outcome = wanderer && string.Equals(n.Next, DialogueDef.OutcomeNext, StringComparison.OrdinalIgnoreCase);
                if (n.Next != null && !outcome && !nodes.ContainsKey(n.Next)) p.Add($"{nw}: unknown next node '{n.Next}'");
                if (n.Next != null && n.Choices.Count > 0) p.Add($"{nw}: has both Next and Choices");
                foreach (var e in n.Effects) Effect(e, nw);
                for (int i = 0; i < n.Choices.Count; i++)
                {
                    var ch = n.Choices[i];
                    string cw = $"{nw} choice {i + 1}";
                    if (ch == null) { p.Add($"{cw}: null"); continue; }
                    if (string.IsNullOrWhiteSpace(ch.Label)) p.Add($"{cw}: empty label");
                    if (ch.Next != null && !nodes.ContainsKey(ch.Next)) p.Add($"{cw}: unknown next node '{ch.Next}'");
                    foreach (var c in ch.Conditions) Cond(c, cw);
                    foreach (var e in ch.Effects) Effect(e, cw);
                }
                // A choice list where every option is conditional could offer nothing and strand the player.
                if (n.Choices.Count > 0 && !n.Choices.Exists(ch => ch != null && ch.Conditions.Count == 0)) p.Add($"{nw}: every choice is conditional — add one with no conditions");
            }
            if (start == null || !nodes.ContainsKey(start)) return;
            var reached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var open = new Stack<string>();
            open.Push(start);
            while (open.Count > 0)
            {
                var id = open.Pop();
                if (!reached.Add(id) || !nodes.TryGetValue(id, out var n)) continue;
                if (wanderer && string.Equals(n.Next, DialogueDef.OutcomeNext, StringComparison.OrdinalIgnoreCase)) { open.Push(DialogueDef.JoinNode); open.Push(DialogueDef.PartNode); }
                else if (n.Next != null) open.Push(n.Next);
                foreach (var ch in n.Choices) if (ch?.Next != null) open.Push(ch.Next);
            }
            foreach (var n in nodes.Values) if (!reached.Contains(n.Id)) p.Add($"{w}: node '{n.Id}' is unreachable");
        }
    }
}
