using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;

namespace GridGame.App.Dev
{
    public enum ScenarioKind { Battle, Screen, Camp }

    /// <summary>Where a scenario lands: a battle (a stage, from one wave), a screen, or Camp (on the road).</summary>
    public sealed class ScenarioTarget
    {
        public ScenarioKind Kind;
        /// <summary>The battle's stage, or for Screen/Camp the next stage to play (null past the campaign's end).</summary>
        public string StageId;
        /// <summary>Campaign index of <see cref="StageId"/>: the number of stages cleared before it.</summary>
        public int StageIndex;
        public int Wave;
        public bool IsBoss;
        public string BossClass;
        public string Screen;
    }

    /// <summary>What <see cref="ScenarioBuilder.Build"/> made: the save and how each gap was filled.</summary>
    public sealed class ScenarioResult
    {
        public SaveData Save;
        public ScenarioTarget Target;
        public int Level;
        public List<string> Story = new List<string>();
        public Dictionary<string, List<string>> Gear = new Dictionary<string, List<string>>();

        public object Summary(ContentDb c) => new
        {
            target = new { kind = Target.Kind.ToString(), stage = Target.StageId, stageIndex = Target.StageIndex, wave = Target.Wave, boss = Target.BossClass, screen = Target.Screen },
            level = Level,
            party = Save.Party.Select(id => new { hero = id, level = Progression.Level(Save.Hero(id)) }).ToList(),
            reserve = Save.Roster.Where(h => !Save.Party.Contains(h.ClassId)).Select(h => h.ClassId).ToList(),
            gear = Gear,
            consumables = Save.Party.ToDictionary(id => id, id => Save.Hero(id).Bar.Where(e => e != null && e.StartsWith("item:")).Select(e => e.Substring(5)).ToList()),
            cleared = Save.ClearedStages.Count,
            highestCleared = Save.HighestClearedStage,
            story = Story,
            caravan = Save.Caravan,
            caravanOpen = CampUnlocks.CaravanOpen(Save),
            gold = Save.Gold,
            food = Save.World.Food,
            onTheRoad = TravelRules.OnTheRoad(Save),
            carriedMana = Save.World.CarriedMana,
        };
    }

    /// <summary>
    /// GG-T1801: turns a <see cref="ScenarioDef"/> into a coherent save at its target, filling every gap the way real
    /// play would have: the starting party plus the story recruits due by then, levels at the target's recommended level
    /// (+1 for a boss) with the skill points and veteran moves that come with them, every stage before the target cleared
    /// and its story beats played headlessly, the best shop gear per slot, and the caravan scaled with progress.
    /// </summary>
    public static class ScenarioBuilder
    {
        public const string SaveName = "Scenario";
        /// <summary>Gold a save carries per stage cleared on "auto", on top of <see cref="Progression.StartingGold"/>.</summary>
        public const int GoldPerClear = 150;

        /// <summary>Bosses in campaign order: each campaign stage wave with a rule-breaking spawn, as (stage index, wave, class).</summary>
        public static List<(int index, int wave, string cls)> Bosses(ContentDb c)
        {
            var list = new List<(int, int, string)>();
            var stages = c.CampaignStages();
            for (int i = 0; i < stages.Count; i++)
                for (int w = 0; w < stages[i].Waves.Count; w++)
                    if (stages[i].Waves[w].Enemies.FirstOrDefault(e => e.RuleBreak != RuleBreak.None) is SpawnDef sp)
                        list.Add((i, w, sp.Class));
            return list;
        }

        private static bool BossLike(ContentDb c, SpawnDef sp)
        {
            if (sp.RuleBreak != RuleBreak.None) return true;
            var d = c.TryClass(sp.Class);
            return d != null && (d.HasTag("Boss") || (d.BossPhases != null && d.BossPhases.Count > 0));
        }

        /// <summary>The wave a stage's boss stands in: the last wave with a boss-like spawn, else the last wave.</summary>
        private static int BossWave(ContentDb c, StageDef st, out string cls)
        {
            cls = null;
            for (int w = st.Waves.Count - 1; w >= 0; w--)
                if (st.Waves[w].Enemies.FirstOrDefault(e => BossLike(c, e)) is SpawnDef sp) { cls = sp.Class; return w; }
            return Math.Max(0, st.Waves.Count - 1);
        }

        /// <summary>Resolves a target string against content. Throws ArgumentException on one that names nothing.</summary>
        public static ScenarioTarget Resolve(ContentDb c, string target)
        {
            var stages = c.CampaignStages();
            string t = string.IsNullOrWhiteSpace(target) ? "overworld" : target.Trim();
            int colon = t.IndexOf(':');
            string head = (colon < 0 ? t : t.Substring(0, colon)).ToLowerInvariant();
            string arg = colon < 0 ? null : t.Substring(colon + 1).Trim();
            var bosses = Bosses(c);

            ScenarioTarget Boss((int index, int wave, string cls) b) =>
                new ScenarioTarget { Kind = ScenarioKind.Battle, StageId = stages[b.index].Id, StageIndex = b.index, Wave = b.wave, IsBoss = true, BossClass = b.cls };
            ScenarioTarget Camp(int index) => new ScenarioTarget
            {
                Kind = ScenarioKind.Camp, StageIndex = Math.Max(0, Math.Min(index, stages.Count)),
                StageId = index >= 0 && index < stages.Count ? stages[index].Id : null,
            };

            switch (head)
            {
                case "first-boss":
                    if (bosses.Count == 0) throw new ArgumentException("no campaign stage has a rule-breaking boss");
                    return Boss(bosses[0]);
                case "boss" when arg != null && int.TryParse(arg, out int n):
                    if (n < 1 || n > bosses.Count) throw new ArgumentException($"boss:{n} — there are {bosses.Count} bosses");
                    return Boss(bosses[n - 1]);
                case "boss" when arg != null:
                {
                    var b = bosses.FirstOrDefault(x => string.Equals(x.cls, arg, StringComparison.OrdinalIgnoreCase));
                    if (b.cls != null) return Boss(b);
                    for (int i = 0; i < stages.Count; i++)
                        for (int w = 0; w < stages[i].Waves.Count; w++)
                            if (stages[i].Waves[w].Enemies.FirstOrDefault(e => string.Equals(e.Class, arg, StringComparison.OrdinalIgnoreCase) && BossLike(c, e)) is SpawnDef sp)
                                return Boss((i, w, sp.Class));
                    throw new ArgumentException($"no campaign boss '{arg}'");
                }
                case "final-boss":
                {
                    var id = c.Campaign.Ending?.Stage;
                    int i = stages.FindIndex(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (i < 0) throw new ArgumentException("campaign.json names no ending stage");
                    int w = BossWave(c, stages[i], out var cls);
                    return new ScenarioTarget { Kind = ScenarioKind.Battle, StageId = stages[i].Id, StageIndex = i, Wave = w, IsBoss = true, BossClass = cls };
                }
                case "stage":
                {
                    int i = stages.FindIndex(x => string.Equals(x.Id, arg, StringComparison.OrdinalIgnoreCase));
                    if (i < 0) throw new ArgumentException($"no campaign stage '{arg}'");
                    return new ScenarioTarget { Kind = ScenarioKind.Battle, StageId = stages[i].Id, StageIndex = i };
                }
                case "region":
                {
                    var theme = c.Campaign.Themes.FirstOrDefault(x => string.Equals(x.Id, arg, StringComparison.OrdinalIgnoreCase) && x.Stages.Count > 0);
                    if (theme == null) throw new ArgumentException($"no campaign region '{arg}'");
                    return Camp(stages.FindIndex(x => x.Id == theme.Stages[0]));
                }
                case "screen":
                {
                    var camp = Camp(CampUnlocks.CaravanAfter);
                    camp.Kind = ScenarioKind.Screen;
                    camp.Screen = arg;
                    return camp;
                }
                case "overworld":
                case "camp":
                    return Camp(CampUnlocks.CaravanAfter);
                default:
                    throw new ArgumentException($"unknown scenario target '{target}'");
            }
        }

        public static ScenarioResult Build(ContentDb c, ScenarioDef def)
        {
            def = def ?? new ScenarioDef();
            var target = Resolve(c, def.Target);
            var stages = c.CampaignStages();

            // Progress overrides where Camp/Screen targets stand (a battle target always clears exactly what precedes it).
            int cleared = target.StageIndex;
            if (target.Kind != ScenarioKind.Battle && !ScenarioDef.IsAuto(def.Progress))
            {
                if (ScenarioDef.IsWord(def.Progress, "none")) cleared = 0;
                else if (ScenarioDef.Int(def.Progress) is int n) cleared = n;
                else
                {
                    int i = stages.FindIndex(x => string.Equals(x.Id, (string)def.Progress, StringComparison.OrdinalIgnoreCase));
                    if (i < 0) throw new ArgumentException($"progress: no campaign stage '{def.Progress}'");
                    cleared = i;
                }
                cleared = Math.Max(0, Math.Min(cleared, stages.Count));
                target.StageIndex = cleared;
                target.StageId = cleared < stages.Count ? stages[cleared].Id : null;
            }

            var party = def.Party != null && def.Party.Count > 0 ? def.Party : (IReadOnlyList<string>)Progression.StartingRoster;
            foreach (var id in party)
                if (c.TryClass(id) == null) throw new ArgumentException($"party: unknown class '{id}'");
            var s = Progression.NewGame(c, string.IsNullOrWhiteSpace(def.Name) ? SaveName : def.Name, party.Take(Progression.MaxPartySize).ToList());
            foreach (var id in party.Skip(Progression.MaxPartySize)) Summon.Join(c, s, id);

            // TestLoop.NewSave's recipe, minus TestMode (this save sees story) and the up-front NPCs (the beats bring them).
            s.TutorialDone = true;
            Drills.MarkAllCleared(c, s);
            Onboarding.Finish(s);
            if (def.AllFeatures) CampNpcs.JoinAll(s);

            for (int i = 0; i < cleared; i++) { s.ClearedStages.Add(stages[i].Id); BestiaryRegions.RecordStage(s, stages[i]); }
            s.HighestClearedStage = cleared - 1;

            var result = new ScenarioResult { Save = s, Target = target };
            var nextStage = target.StageId != null ? c.Stage(target.StageId) : stages.LastOrDefault();
            int level = ScenarioDef.Int(def.Level)
                ?? (nextStage == null ? 1 : Progression.StageRecommendedLevel(c, s, nextStage) + (target.IsBoss ? 1 : 0));
            result.Level = level = Math.Max(1, level);

            foreach (var h in s.Roster) RaiseTo(c, h, level);
            if (nextStage != null) result.Story = DialogueRunner.PlayPendingBeatsHeadless(c, s, nextStage.Id);
            foreach (var h in s.Roster) { RaiseTo(c, h, level); h.VariantPending = false; }

            for (var ch = Story.ChapterDue(c, s); ch != null; ch = Story.ChapterDue(c, s)) Story.MarkChapterSeen(c, s, ch);
            var themeIndex = c.Campaign.Themes.FindIndex(th => nextStage != null && th.Stages.Contains(nextStage.Id));
            for (int i = 0; i <= themeIndex; i++)
                if (c.Campaign.Themes[i].HasCrawl) Story.MarkRegionSeen(s, c.Campaign.Themes[i]);

            s.Gold = ScenarioDef.Int(def.Gold) ?? Progression.StartingGold + GoldPerClear * cleared;
            if (!ScenarioDef.IsWord(def.Gear, "none")) EquipBest(c, s, result);
            EquipConsumables(c, s, def.Consumables);
            if (def.Bag != null)
                foreach (var kv in def.Bag)
                    s.AddItem((c.TryItem(kv.Key) ?? throw new ArgumentException($"bag: unknown item '{kv.Key}'")).Id, Math.Max(1, kv.Value));
            FillCaravan(c, s, def.Caravan, cleared, stages.Count);

            if (def.Food is int food) s.World.Food = Math.Max(0, food);
            if (def.OnTheRoad ?? true)
            {
                TravelRules.SetOut(s);
                s.World.CarriedMana = Math.Max(0, def.CarriedOrbs ?? 0) * BattleConfig.ManaPointsPerOrb;
            }
            else TravelRules.MakeCamp(s);
            return result;
        }

        /// <summary>Raises a hero to <paramref name="level"/> the way leveling would: its XP, the skill points for each
        /// level gained, and the veteran kit moves of that level. Never lowers a hero.</summary>
        public static void RaiseTo(ContentDb c, HeroSave h, int level)
        {
            int from = Progression.Level(h);
            if (from >= level) return;
            h.TotalXp = Formulas.TotalXpForLevel(level);
            h.SkillPoints += Formulas.SkillPointsForLevelUp(from, level);
            Progression.TrainTo(c, h);
        }

        private static readonly EquipmentSlot[] GearSlots =
            new[] { EquipmentSlot.Weapon }.Concat(BattleEngine.ArmorSlots).Concat(Equipment.JewelrySlots).ToArray();

        /// <summary>The weapon family a class is trained in (Martial, Arcane), or Simple for a class trained in neither.</summary>
        public static WeaponClass WeaponFamily(ContentDb c, HeroSave h) =>
            c.TryClass(h.ClassId)?.WeaponProficiencies.Where(w => w != WeaponClass.Simple).DefaultIfEmpty(WeaponClass.Simple).First() ?? WeaponClass.Simple;

        /// <summary>Per party hero and slot, the dearest piece on this save's shelf (the stock tiers its progress has
        /// opened) that the hero can wear, bought and equipped; the weapon of the hero's own family first.</summary>
        private static void EquipBest(ContentDb c, SaveData s, ScenarioResult result)
        {
            var shelf = Shop.Stock(c, s).Where(i => i.Type == ItemType.Equipment).ToList();
            foreach (var h in s.Party.Select(s.Hero).Where(h => h != null && !h.Domesticated))
            {
                var worn = new List<string>();
                foreach (var slot in GearSlots)
                {
                    var fits = shelf.Where(i => Equipment.Fits(c, h, i, slot)).ToList();
                    if (slot == EquipmentSlot.Weapon)
                    {
                        var own = WeaponFamily(c, h);
                        var family = fits.Where(i => i.WeaponClass == own).ToList();
                        if (family.Count > 0) fits = family;
                    }
                    var best = fits.OrderByDescending(i => i.BaseCost).ThenByDescending(i => i.Rarity).FirstOrDefault();
                    if (best == null) continue;
                    s.AddItem(best.Id, 1);
                    if (Equipment.Equip(c, s, h, slot, best.Id).Ok) worn.Add(best.Id);
                    else s.AddItem(best.Id, -1);
                }
                result.Gear[h.ClassId] = worn;
            }
        }

        /// <summary>Each hero's battle items (<see cref="ScenarioDef.Consumables"/>): the stack into the bag, then onto the bar
        /// through <see cref="Abilities.Assign"/>, in the first empty unlocked slot or else the last unlocked one.</summary>
        private static void EquipConsumables(ContentDb c, SaveData s, Dictionary<string, Dictionary<string, int>> spec)
        {
            if (spec == null) return;
            int unlocked = Progression.UnlockedSlots(s);
            foreach (var kv in spec)
            {
                var h = s.Hero(kv.Key) ?? throw new ArgumentException($"consumables: '{kv.Key}' is not in the roster");
                foreach (var stack in kv.Value ?? new Dictionary<string, int>())
                {
                    var item = c.TryItem(stack.Key);
                    if (item == null || item.Type != ItemType.Consumable) throw new ArgumentException($"consumables: '{stack.Key}' is not a battle item");
                    s.AddItem(item.Id, Math.Max(1, stack.Value));
                    int free = h.Bar.IndexOf(null);
                    int slot = free >= 0 && free < unlocked ? free : unlocked - 1;
                    var r = Abilities.Assign(c, s, h, slot, "item:" + item.Id);
                    if (!r.Ok) throw new ArgumentException($"consumables: {kv.Key} can't carry {item.Id}: {r.Message}");
                }
            }
        }

        /// <summary>"auto": once the Caravan is open, each upgrade at the share of its levels that the share of the campaign
        /// cleared would have paid for; "max" every level; a number that level each; "none" nothing.</summary>
        private static void FillCaravan(ContentDb c, SaveData s, Newtonsoft.Json.Linq.JToken spec, int cleared, int total)
        {
            if (ScenarioDef.IsWord(spec, "none")) return;
            bool auto = ScenarioDef.IsAuto(spec);
            if (auto && !CampUnlocks.CaravanOpen(s)) return;
            foreach (var kv in c.Caravan)
            {
                int max = kv.Value.Levels.Count;
                int n = ScenarioDef.IsWord(spec, "max") ? max
                    : ScenarioDef.Int(spec) is int fixedLevel ? fixedLevel
                    : total <= 0 ? 0 : (int)Math.Round(max * (double)cleared / total, MidpointRounding.AwayFromZero);
                n = Math.Max(0, Math.Min(n, max));
                if (n > 0) s.Caravan[kv.Key] = n;
            }
        }
    }
}
