using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>
    /// One save slot (bible §15). Plain data, JSON-serialized by the app's save store.
    /// Bump <see cref="CurrentVersion"/> and add a step to <see cref="SaveMigrations"/> when the shape changes.
    /// </summary>
    /// <summary>An expedition in progress: a theme's battles fought in order, wounds and mana carried between them (§22.5).</summary>
    public sealed class ExpeditionSave
    {
        public string ThemeId;
        /// <summary>Index (into the theme's stages) of the next battle.</summary>
        public int Next;
        /// <summary>Orbs left in the bank at the end of the last battle; they start the next one.</summary>
        public int CarriedOrbs;
        /// <summary>Coins earned so far on this expedition (the completion bonus is a share of it).</summary>
        public int Coins;
    }

    public sealed class SaveData
    {
        public const int CurrentVersion = 14;

        public int Version = CurrentVersion;
        public string Name = "Lightbearer";
        public DateTime CreatedUtc = DateTime.UtcNow;
        public DateTime UpdatedUtc = DateTime.UtcNow;
        public double PlaySeconds;

        public int Gold;
        public int LifetimeCoins;
        /// <summary>Campaign index of the furthest cleared stage (-1 = none).</summary>
        public int HighestClearedStage = -1;
        public HashSet<string> ClearedStages = new HashSet<string>();
        public HashSet<string> SeenCrawls = new HashSet<string>();
        /// <summary>Story flags set by dialogue (§27.1, GG-T117) — the same idea as SeenCrawls. A joined Camp NPC
        /// is the flag "npc:&lt;id&gt;" (JoinNpc), which ClearFlag can't touch.</summary>
        public HashSet<string> Flags = new HashSet<string>();
        /// <summary>Dialogue ids played to the end: story beats never replay; Talk dialogues marked Once don't either.</summary>
        public HashSet<string> SeenDialogues = new HashSet<string>();
        public string LastStage;

        public List<HeroSave> Roster = new List<HeroSave>();
        public List<string> Party = new List<string>();
        public Dictionary<string, int> Inventory = new Dictionary<string, int>();
        public int Recruits;
        /// <summary>Caravan upgrade id → level bought (§25.12).</summary>
        public Dictionary<string, int> Caravan = new Dictionary<string, int>();
        /// <summary>Crafting station id ("blacksmith"/"alchemist") → accumulated XP (§24.6.1, GG-T35).</summary>
        public Dictionary<string, int> StationXp = new Dictionary<string, int>();
        /// <summary>Guest allies lost for good (§25.11) — only ever recorded or honored with <see cref="Permadeath"/> on
        /// (a future Ironman mode); in the normal game it stays empty, and entries from older saves are ignored (GG-T114).</summary>
        public HashSet<string> LostGuests = new HashSet<string>();
        /// <summary>Recipe ids discovered early, before the crafting station reached their tier — by reading a
        /// recipe scroll or first-ever pickup of the recipe's rarest input (§24.6.2, GG-T36). A recipe whose
        /// tier the station has already reached is always discovered regardless of this set.</summary>
        public HashSet<string> DiscoveredRecipes = new HashSet<string>();
        /// <summary>GG-T538: Merchant items this save has bought. One stays on the shelf whatever its stock tier
        /// (<see cref="VendorTiers.OnShelf"/>), so nothing a player once bought goes away.</summary>
        public HashSet<string> ShopBought = new HashSet<string>();
        /// <summary>GG-T538: Forge and Alchemy recipes this save has made. One stays in its station's list whatever its
        /// progress tier (<see cref="VendorTiers.RecipeOpen"/>).</summary>
        public HashSet<string> CraftedRecipes = new HashSet<string>();
        /// <summary>Spare equipment that has been used: item id → its worn copies (per-instance durability, US-246).</summary>
        public Dictionary<string, List<WornGear>> Worn = new Dictionary<string, List<WornGear>>();
        /// <summary>Shattered/broken gear kept as salvage fodder instead of lost outright: item id → count of
        /// Broken copies. Broken pieces can't be equipped or repaired — only scrapped at the Blacksmith (GG-T41).</summary>
        public Dictionary<string, int> Broken = new Dictionary<string, int>();
        /// <summary>Advances once per finished battle; seeds the Merchant's "Used / Bargain" shelf so it
        /// turns over on every return to camp but replays identically from the same save (GG-T38).</summary>
        public int BargainRotation;
        /// <summary>Chests opened so far (GG-T301): seeds each chest's roll, so a save replays its chests identically.</summary>
        public int ChestsOpened;
        /// <summary>Themes whose campaign milestone reward has been paid (GG-T301, <see cref="ThemeDef.MilestoneReward"/>).</summary>
        public HashSet<string> Milestones = new HashSet<string>();
        public bool TutorialDone;
        /// <summary>Camp stations whose first-use tip has been shown (GG-T173, <see cref="CampTips"/>).</summary>
        public HashSet<string> SeenCampTips = new HashSet<string>();
        /// <summary>A `-testmode` throwaway save (GG-T93): enemy levels follow the party (Progression.StageRecommendedLevel).</summary>
        public bool TestMode;
        /// <summary>New Game+ cycle (0 = first run). Unlocked by clearing the last campaign stage (US-332).</summary>
        public int NewGamePlus;

        /// <summary>The expedition under way (null = at the caravan between expeditions) — §22.5.</summary>
        public ExpeditionSave Expedition;
        public BountySave Bounty = new BountySave();
        /// <summary>Optional side quests (§22.11, GG-T237): under way (with progress) and handed in.</summary>
        public QuestSave Quests = new QuestSave();
        /// <summary>Crucible record (§22.10): best waves survived and runs fought on this save.</summary>
        public CrucibleSave Crucible = new CrucibleSave();
        /// <summary>Mercenary contracts and the hiring board (GG-T233, <see cref="Mercenaries"/>).</summary>
        public MercenarySave Mercenaries = new MercenarySave();
        /// <summary>Party-wide energy spent per stage launch (§25.18, <see cref="RestedPool"/>).</summary>
        public RestedSave Rested = new RestedSave();
        /// <summary>Paid, timed paywall jobs still running (§25.18, <see cref="Paywall"/>).</summary>
        public List<PaywallJob> Jobs = new List<PaywallJob>();
        /// <summary>Real-money purchases already granted, by store transaction id (§25.18.1, GG-T469, <see cref="PurchaseLedger"/>).
        /// Save v11: an older build refuses this save rather than drop the ledger and pay a re-delivered order twice.</summary>
        public PurchaseSave Purchases = new PurchaseSave();
        public Dictionary<string, BestiaryEntry> Bestiary = new Dictionary<string, BestiaryEntry>();
        // Settings no longer live in a save slot (GG-T99): they belong to the player profile's settings.json
        // (PlayerProfiles.cs). An older slot's "Settings" block is ignored on load; LegacySaveMigration reads it once.

        public HeroSave Hero(string classId) => Roster.Find(h => string.Equals(h.ClassId, classId, StringComparison.OrdinalIgnoreCase));

        public int Count(string itemId) => itemId != null && Inventory.TryGetValue(itemId, out var n) ? n : 0;

        public void AddItem(string itemId, int n)
        {
            if (itemId == null || n == 0) return;
            int v = Count(itemId) + n;
            if (v <= 0) Inventory.Remove(itemId); else Inventory[itemId] = v;
            // Worn copies can't outnumber owned copies; when copies leave the bag the most-worn go first (an enchanted
            // copy, GG-T179, last).
            if (Worn.TryGetValue(itemId, out var worn))
            {
                worn.Sort((a, b) => { int e = (b.Enchant != null).CompareTo(a.Enchant != null); return e != 0 ? e : b.Durability.CompareTo(a.Durability); });
                while (worn.Count > Count(itemId)) worn.RemoveAt(worn.Count - 1);
                if (worn.Count == 0) Worn.Remove(itemId);
            }
        }

        public int BrokenCount(string itemId) => itemId != null && Broken.TryGetValue(itemId, out var n) ? n : 0;

        public void AddBroken(string itemId, int n)
        {
            if (itemId == null || n == 0) return;
            int v = BrokenCount(itemId) + n;
            if (v <= 0) Broken.Remove(itemId); else Broken[itemId] = v;
        }
    }

    public sealed class HeroSave
    {
        public string ClassId;
        public int TotalXp;
        /// <summary>Current HP; 0 = full (wounds carry between battles, §15.1).</summary>
        public int Hp;
        public List<string> Known = new List<string>();
        /// <summary>Five ability-bar entries: ability id, "item:id", or null.</summary>
        public List<string> Bar = new List<string> { null, null, null, null, null };
        /// <summary>Unspent skill points (§15.1, GG-T49), earned per level plus milestone bonuses
        /// (<see cref="Formulas.SkillPointsForLevelUp"/>). Spent on skill-tree nodes (GG-T50).</summary>
        public int SkillPoints;
        /// <summary>Skill-tree nodes bought with points, as "treeId/nodeId" (§25.6.3, GG-T53, <see cref="Talents"/>);
        /// the Inn's Respec clears it and refunds their cost. An older save simply has none.</summary>
        public List<string> TreeNodes = new List<string>();
        /// <summary>Skill points actually paid for each bought node, keyed like <see cref="TreeNodes"/> (premium included), so
        /// Respec refunds what was paid even if the off-tree premium has since changed. A node bought before this was recorded
        /// (an older save) has no entry and refunds its current <see cref="Talents.Cost"/>. Null-safe: may be missing.</summary>
        public Dictionary<string, int> TreeNodeCosts;
        /// <summary>Learned passives the player has switched on (GG-T301, <see cref="PassiveLoadout"/>), read only once
        /// <see cref="PassivesChosen"/> is set; until then the first <see cref="PassiveLoadout.Slots"/> learned are on.</summary>
        public List<string> ActivePassives = new List<string>();
        /// <summary>The player has picked which learned passives are on (GG-T301). An older save has not.</summary>
        public bool PassivesChosen;
        /// <summary>A domesticated monster (GG-T303, <see cref="Domestication"/>): <see cref="ClassId"/> is an enemy class. It
        /// fights on its own AI, wears no gear, has no skill tree, and grows slowly up to <see cref="Domestication.LevelCap"/>.</summary>
        public bool Domesticated;
        /// <summary>GG-T532: which version of the class this hero is: the female art (the default) or the male art
        /// (the class's MalePortrait, GG-T557). Looks only: stats, kit and story are the same either way.</summary>
        public HeroVariant Variant;
        /// <summary>GG-T532: the player hasn't picked <see cref="Variant"/> yet. Every new hero joins with it set
        /// (<see cref="Progression.NewHero"/>); the game asks at its next quiet moment and clears it, or clears it silently
        /// (Female) when the class has no male art yet. A save from before v14 has it clear.</summary>
        public bool VariantPending;

        public string WeaponId;
        public int WeaponDurability;
        public int WeaponRepairs;
        /// <summary>Masterwork stat multiplier (§24.6.2, GG-T46); 1 = normal gear.</summary>
        public float WeaponMastery = 1f;
        /// <summary>Armor equipped in Head/Torso/Legs/Gloves/Boots, each with its own durability/repairs/mastery —
        /// the same per-piece tracking Weapon already had (§24.2, GG-T89).</summary>
        public Dictionary<EquipmentSlot, EquippedArmor> Armor = new Dictionary<EquipmentSlot, EquippedArmor>();
        /// <summary>Jewelry equipped in Necklace/Earring1/Earring2/Ring1/Ring2 — plain item ids, no durability
        /// (the same no-wear rule the old Relic slots had, GG-T89).</summary>
        public Dictionary<EquipmentSlot, string> Jewelry = new Dictionary<EquipmentSlot, string>();

        // ── v2-and-earlier save shape, kept only so SaveMigrations can drain them into Armor/Jewelry above.
        // Never read anywhere else — a v3+ save always has these empty/default. ──
        public string ArmorId;
        public int ArmorDurability;
        public int ArmorRepairs;
        public float ArmorMastery = 1f;
        public List<string> Relics = new List<string> { null, null, null };
    }

    /// <summary>GG-T532: a hero's version, picked when they join (owner 2026-11-21: "when they finally join your party you
    /// get to choose the gender ... no change to story or mechanics makes everything equal").</summary>
    public enum HeroVariant
    {
        Female = 0,
        Male = 1,
    }

    /// <summary>GG-T532: the rules of hero variants that need no art: the portrait id a variant reads, the heroes still
    /// waiting for the player's pick, and picking. Whether the art exists is the app's question (Art.HasMaleArt).</summary>
    public static class HeroVariants
    {
        /// <summary>The Portraits/ file <paramref name="def"/> draws as <paramref name="v"/> (GG-T557,
        /// <see cref="GridGame.Core.Content.ClassDef.PortraitFile"/>): its male art when male and it has one, else its
        /// default art; <paramref name="variant"/> picks a numbered alternate (a costume), default the class's own.</summary>
        public static string PortraitFile(GridGame.Core.Content.ClassDef def, HeroVariant v, int? variant = null) =>
            def?.PortraitFile(v == HeroVariant.Male, variant);

        /// <summary>The owned hero's variant; Female for a class the save doesn't hold.</summary>
        public static HeroVariant Of(SaveData s, string classId) =>
            s?.Roster == null || string.IsNullOrEmpty(classId) ? HeroVariant.Female : s.Hero(classId)?.Variant ?? HeroVariant.Female;

        /// <summary>Roster heroes still waiting for the player's pick, in join order.</summary>
        public static List<HeroSave> Pending(SaveData s) =>
            s?.Roster == null ? new List<HeroSave>() : s.Roster.FindAll(h => h != null && h.VariantPending && !h.Domesticated);

        /// <summary>Sets the hero's variant and ends the wait. Picking again later (the hero screen) works the same way.</summary>
        public static void Choose(HeroSave h, HeroVariant v)
        {
            if (h == null) return;
            h.Variant = v == HeroVariant.Male ? HeroVariant.Male : HeroVariant.Female;
            h.VariantPending = false;
        }

        /// <summary>A wanderer's look on the board (either, evenly), from a seed so a battle replays the same.</summary>
        public static HeroVariant Roll(int seed) => new Random(seed).Next(2) == 0 ? HeroVariant.Female : HeroVariant.Male;
    }

    /// <summary>One equipped armor piece (§24.2, GG-T89): mirrors <see cref="WornGear"/>'s shape plus the item id,
    /// since (unlike the bag's Worn list, keyed by item id already) a hero's Armor dict is keyed by slot.</summary>
    public sealed class EquippedArmor
    {
        public string ItemId;
        public int Durability;
        public int Repairs;
        public float Mastery = 1f;
        /// <summary>Armor enchant id (armorenchants.json, GG-T179) bound into this piece by the Enchantress; null = none.
        /// It grants its passive while the piece is worn and intact, and is lost if the piece breaks.</summary>
        public string Enchant;
    }

    public sealed class WornGear
    {
        public int Durability;
        public int Repairs;
        /// <summary>Masterwork stat multiplier this specific copy rolled at craft time (§24.6.2, GG-T46); 1 = normal.</summary>
        public float Mastery = 1f;
        /// <summary>Armor enchant this copy carries (GG-T179), kept while it sits in the bag; null = none.</summary>
        public string Enchant;
    }

    /// <summary>The bounty board (§22.4, GG-T431): marked targets taken, slain and awaiting their reward, and paid.</summary>
    public sealed class BountySave
    {
        /// <summary>Bounties taken and still roaming their stage.</summary>
        public List<string> Taken = new List<string>();
        /// <summary>Targets killed whose reward waits at the board.</summary>
        public List<string> Slain = new List<string>();
        /// <summary>Bounties paid out; they never post again.</summary>
        public HashSet<string> Claimed = new HashSet<string>();
        /// <summary>How many times the board's open offers have turned over (seeds them).</summary>
        public int Refresh;
        /// <summary>Stage battles fought since the last turnover.</summary>
        public int Battles;
        /// <summary>The board has been used (a bounty taken, or an old kill contract held), so its Camp row never locks again.</summary>
        public bool Used;
        /// <summary>Retired pre-GG-T431 kill contract fields: read from old saves, then cleared by the migration.</summary>
        public string ActiveId;
        public int Progress;
    }

    public sealed class QuestSave
    {
        /// <summary>Quest id → progress (Defeat kills so far; ClearStage 1 once met).</summary>
        public Dictionary<string, int> Active = new Dictionary<string, int>();
        public HashSet<string> Done = new HashSet<string>();
    }

    /// <summary>The save's Crucible record (§22.10). <see cref="Runs"/> also seeds the next run, so a save replays
    /// the same gauntlet until it fights it.</summary>
    public sealed class CrucibleSave
    {
        public int Best;
        public int Runs;
        public int TotalWaves;
        /// <summary>The most gold one run has banked (wave pay plus drops, GG-T430); a worse run never lowers it.</summary>
        public int BestGold;
    }

    /// <summary>What the save knows about one enemy class (§25.11). How much the Bestiary and the battle inspect
    /// reveal is <see cref="BestiaryKnowledge.Tier"/>, earned by <see cref="Defeated"/>.</summary>
    public sealed class BestiaryEntry
    {
        public bool Seen;
        /// <summary>Retired with the Scan ability (GG-T290). Never set any more; an older save that scanned this enemy
        /// keeps at least the stats tier through <see cref="BestiaryKnowledge.Tier"/>.</summary>
        public bool Scanned;
        public int Defeated;
    }

    /// <summary>The Bestiary's knowledge tiers (§25.11, GG-T290): the more of an enemy the party defeats, the more it
    /// learns. Seen gives its name and description; then Speed, then HP and every base stat, then its elemental
    /// weaknesses and resistances, and last its abilities and how it picks targets.</summary>
    public static class BestiaryKnowledge
    {
        public const int None = 0, Description = 1, Speed = 2, Stats = 3, Elements = 4, Abilities = 5;
        public const int DefeatsForSpeed = 1, DefeatsForStats = 3, DefeatsForElements = 6, DefeatsForAbilities = 10;

        /// <summary>The highest tier <paramref name="e"/> has reached. A pre-GG-T290 Scanned entry counts as at least Stats.</summary>
        public static int Tier(BestiaryEntry e)
        {
            if (e == null) return None;
            int d = Math.Max(0, e.Defeated);
            int t = d >= DefeatsForAbilities ? Abilities : d >= DefeatsForElements ? Elements : d >= DefeatsForStats ? Stats
                : d >= DefeatsForSpeed ? Speed : e.Seen ? Description : None;
            if (e.Scanned && t < Stats) t = Stats;
            return t;
        }

        public static int Tier(SaveData s, string classId) =>
            s?.Bestiary != null && classId != null && s.Bestiary.TryGetValue(classId, out var e) ? Tier(e) : None;

        public static bool Knows(BestiaryEntry e, int tier) => Tier(e) >= tier;
        public static bool Knows(SaveData s, string classId, int tier) => Tier(s, classId) >= tier;

        /// <summary>The defeat count that unlocks <paramref name="tier"/> (0 for Description and below).</summary>
        public static int DefeatsFor(int tier) =>
            tier >= Abilities ? DefeatsForAbilities : tier == Elements ? DefeatsForElements : tier == Stats ? DefeatsForStats : tier == Speed ? DefeatsForSpeed : 0;

        /// <summary>The next tier still to learn, or -1 when everything is known.</summary>
        public static int NextTier(BestiaryEntry e)
        {
            int t = Tier(e);
            return t >= Abilities ? -1 : Math.Max(Speed, t + 1);
        }

        /// <summary>Defeats still needed for the next tier (0 when everything is known).</summary>
        public static int DefeatsToNext(BestiaryEntry e)
        {
            int next = NextTier(e);
            return next < 0 ? 0 : Math.Max(0, DefeatsFor(next) - Math.Max(0, e?.Defeated ?? 0));
        }

        /// <summary>What a tier reveals, as the Bestiary's "Defeat N more to learn …" phrase.</summary>
        public static string Label(int tier) => tier switch
        {
            Speed => "its speed",
            Stats => "its HP and stats",
            Elements => "its weaknesses",
            Abilities => "its abilities",
            _ => "about it",
        };

        /// <summary>Its elemental weaknesses and resistances from the class's multipliers, e.g. "Immune to Poison ·
        /// weak to Fire (×1.5)", or "No elemental weakness or resistance".</summary>
        public static string ElementLine(ClassDef d)
        {
            var parts = new List<string>();
            if (d?.Resistances != null)
                foreach (var kv in d.Resistances)
                {
                    if (Math.Abs(kv.Value - 1f) < 0.001f) continue;
                    parts.Add(kv.Value <= 0f ? $"immune to {kv.Key}"
                        : kv.Value > 1f ? $"weak to {kv.Key} (×{kv.Value:0.##})" : $"resists {kv.Key} (×{kv.Value:0.##})");
                }
            if (parts.Count == 0) return "No elemental weakness or resistance";
            var s = string.Join(" · ", parts);
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>"Defeat 2 more to learn its HP and stats", or null when everything is known.</summary>
        public static string Hint(BestiaryEntry e)
        {
            int next = NextTier(e);
            if (next < 0) return null;
            int n = DefeatsToNext(e);
            return $"Defeat {n} more to learn {Label(next)}.";
        }
    }

    /// <summary>The player's settings — one set per profile, stored in Profiles/&lt;Name&gt;/settings.json through
    /// <see cref="GaidenSettings"/> (GG-T99), not in a save slot.</summary>
    public sealed class SettingsSave
    {
        public float MusicVolume = 0.6f;
        public float SfxVolume = 0.85f;
        public bool MuteMusic;
        public bool MuteSfx;
        public bool ColorblindMode;
        public bool ReduceMotion;
        /// <summary>Terra Battle-style drag timer (§2, act-advanced amendment). Off by default.</summary>
        public bool DragTimer;
        /// <summary>Battle animation speed multiplier (1 = normal).</summary>
        public float BattleSpeed = 1f;
        /// <summary>Close-up portrait cut-in on pincers (ported from GridGame2026).</summary>
        public bool PincerCutIns = true;
        /// <summary>Pick a target, then confirm (owner direction 2026-10-11). Off = commit on the pick, for veterans.</summary>
        public bool ConfirmActions = true;
        /// <summary>Menu window theme by name: a preset from Resources/UI/themes.json or one of <see cref="CustomThemes"/>
        /// (GG-T407); unknown/missing names fall back to the default: Manapunk since 2026-11-22 (GG-T678), Dark before
        /// (see <see cref="DefaultMenuTheme"/>).</summary>
        public string MenuTheme = DefaultMenuTheme;
        /// <summary>The player's own window themes from Settings → Windows (GG-T407), at most
        /// <see cref="WindowThemeCatalog.MaxCustom"/>, each named apart from every preset.</summary>
        public List<WindowThemeDef> CustomThemes = new List<WindowThemeDef>();
        /// <summary>The player's Brightness/Contrast/Gamma for each preset theme, by theme name (GG-T563): kept here, on the
        /// device, so themes.json stays pristine and an update never wipes the tuning. A preset not listed is at 0.5/0.5/0.5;
        /// a custom theme carries its own values.</summary>
        public Dictionary<string, WindowToneDef> ThemeTones = new Dictionary<string, WindowToneDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Body font by name (App.Fonts.Presets); unknown/missing names fall back to Rounded (GG-T385).</summary>
        public string FontName = DefaultFontName;
        // Owner 2026-11-21: "we tried FF, then Chrono, then back to FF - none of these where right; but the colors
        // in all these apps is better" (GridGame.Gallery's own look, see Theme.uss's .theme-dark retint) - "Dark" is
        // the base default now, not "Classic" (the Final Fantasy blue it was reverted to under GG-T453).
        // GG-T678 (owner 2026-11-22): "Manapunk", between Dark and the FF blue, is the default; Dark stays selectable.
        public const string DefaultMenuTheme = "Manapunk", DefaultFontName = "Rounded";
        /// <summary>Settings → Text wrapping (GG-T305): false = Word wrap (default), true = Hyphenate (ragged lines and
        /// overlong words break at <see cref="Hyphenation"/> points).</summary>
        public bool Hyphenate;
        /// <summary>Settings → Text size (GG-T495): 100, 115 or 130 percent of the menu type scale; anything else reads as 100.</summary>
        public int TextScale = 100;
        /// <summary>The difficulty dial 1..10 (§22.9, GG-T309), 10 by default; applies from the next battle.</summary>
        public DifficultySettings Difficulty = new DifficultySettings();
    }

    /// <summary>
    /// The difficulty dial (owner, GG-T309: "On a 1-10 scale you would start on 10 and let players adjust the game if
    /// they feel it is too easy or hard … the higher the difficulty the greater the rewards"). One number replaces the
    /// §22.9 scaffold's Easy/Normal/Hard/Custom preset and its six inert multipliers; what each level does is
    /// <see cref="DifficultyTable"/>. It is a profile setting (settings.json), so every save slot of the profile plays it.
    /// </summary>
    public sealed class DifficultySettings
    {
        public int Level = DifficultyTable.Default;

        /// <summary>The level an old §22.9 preset becomes. Its multipliers were pinned at 1.0 (inert), so every player
        /// actually fought the designed game — level 10 — except one who chose a lower setting on purpose: Easy → 5, and
        /// a Custom whose enemy HP/damage averaged below ×1 → the level whose enemy side is nearest that average.
        /// Normal, Hard, a Custom at ×1 or above, and anything unknown → 10.</summary>
        public static int FromLegacyPreset(string preset, float enemyHp = 1f, float enemyDamage = 1f)
        {
            switch ((preset ?? "").Trim().ToLowerInvariant())
            {
                case "easy": return 5;
                case "custom":
                    float want = (enemyHp + enemyDamage) / 2f;
                    if (!(want < 1f)) return DifficultyTable.Default;
                    int best = DifficultyTable.Min;
                    float bestGap = float.MaxValue;
                    foreach (var r in DifficultyTable.Rows)
                    {
                        float gap = Math.Abs((r.EnemyHp + r.EnemyDamage) / 2f - want);
                        if (gap < bestGap) { bestGap = gap; best = r.Level; }
                    }
                    return best;
                default: return DifficultyTable.Default;
            }
        }
    }

    public static class SaveMigrations
    {
        /// <summary>Ability ids removed from content; a save still holding one drops it on load.</summary>
        /// <summary>GG-T344: where The Still went into the campaign order, and how many stages it brought (save v5).</summary>
        public const int StillInsertedAt = 12, StillStageCount = 3;

        /// <summary>GG-T345: where The Lantern Road went into the campaign order (between the Tithe Moors and the Shardfall),
        /// and how many stages it brought (save v6).</summary>
        public const int LanternRoadInsertedAt = 3, LanternRoadStageCount = 3;

        /// <summary>GG-T350: where The Choir's Cathedral went into the campaign order (after Halcyon, the new finale), and how
        /// many stages it brought (save v7).</summary>
        public const int CathedralInsertedAt = 21, CathedralStageCount = 3;

        /// <summary>GG-T347: where The Bloom went into the campaign order (after the Cathedral, the first postgame chapter),
        /// and how many stages it brought (save v8).</summary>
        public const int BloomInsertedAt = 24, BloomStageCount = 3;

        /// <summary>GG-T357: the ten stages of the five older postgame chapters were reordered in place (save v9). Old order
        /// from <see cref="PostgameReorderedFrom"/>: the Free Holds, Deep Shardfall, the Drowned Arks, Under the Anchors, the
        /// Underspire; new order: the Free Holds, the Underspire, the Drowned Arks, Deep Shardfall, Under the Anchors.
        /// <see cref="PostgameReorderMap"/>[i] is the new campaign index of the stage that sat at old index
        /// <c>PostgameReorderedFrom + i</c>. The Bloom before them and Morrow's Shadow after them did not move.</summary>
        public const int PostgameReorderedFrom = 27;
        public static readonly int[] PostgameReorderMap = { 27, 28, 33, 34, 31, 32, 35, 36, 29, 30 };

        /// <summary>The v9 frontier: the furthest new index among every stage at or below the old
        /// <paramref name="highestCleared"/>, so each save keeps its furthest clear and nothing it cleared locks again.</summary>
        public static int ReorderedPostgameFrontier(int highestCleared)
        {
            int frontier = highestCleared;
            for (int i = 0; i < PostgameReorderMap.Length && PostgameReorderedFrom + i <= highestCleared; i++)
                frontier = Math.Max(frontier, PostgameReorderMap[i]);
            return frontier;
        }

        /// <summary>The save version current when GG-T52's gear-proficiency gate shipped; the grandfather step runs only up to it.</summary>
        public const int GearGateShippedAtVersion = 4;

        public static readonly HashSet<string> RetiredAbilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scan" };

        /// <summary>Upgrades an older save in place. Unknown future versions are rejected. <paramref name="c"/>
        /// is optional (version-shape fixups below don't need content) but drives the GG-T52 proficiency
        /// grandfather clause at the end: without it that step is simply skipped.</summary>
        public static SaveData Migrate(SaveData s, ContentDb c = null)
        {
            if (s == null) return null;
            if (s.Version > SaveData.CurrentVersion) throw new InvalidOperationException($"Save version {s.Version} is newer than this build");
            // GG-T52's gear gate shipped while saves were v4: only a save that old can hold gear its hero was never gated on.
            bool beforeGearGate = s.Version <= GearGateShippedAtVersion;
            // v1 is the first shape.
            if (s.Version < 2)
            {
                // GG-T49: skill points didn't exist before v2, so a hero already past level 1 is
                // backfilled once with the points they'd have earned leveling up to here — otherwise
                // every existing save would start the new economy at a permanent deficit.
                foreach (var h in s.Roster)
                    h.SkillPoints += Formulas.SkillPointsForLevelUp(1, Formulas.LevelFromTotalXp(h.TotalXp).level);
                s.Version = 2;
            }
            s.Bounty ??= new BountySave();
            s.Bounty.Taken ??= new List<string>();
            s.Bounty.Slain ??= new List<string>();
            s.Bounty.Claimed ??= new HashSet<string>();
            if (s.Bounty.ActiveId != null)
            {
                // GG-T431: the kill-N contracts are retired. An old save's active contract is dropped (its kills meant
                // nothing in the new board); the board stays open for it.
                s.Bounty.ActiveId = null;
                s.Bounty.Progress = 0;
                s.Bounty.Used = true;
            }
            s.Quests ??= new QuestSave();
            s.Quests.Active ??= new Dictionary<string, int>();
            s.Quests.Done ??= new HashSet<string>();
            s.Crucible ??= new CrucibleSave();
            s.Mercenaries ??= new MercenarySave();
            s.Worn ??= new Dictionary<string, List<WornGear>>();
            s.Broken ??= new Dictionary<string, int>();
            s.StationXp ??= new Dictionary<string, int>();
            s.DiscoveredRecipes ??= new HashSet<string>();
            s.Flags ??= new HashSet<string>();
            s.SeenDialogues ??= new HashSet<string>();
            s.SeenCampTips ??= new HashSet<string>();
            s.Rested ??= new RestedSave();
            s.Jobs ??= new List<PaywallJob>();
            foreach (var h in s.Roster) while (h.Bar.Count < 5) h.Bar.Add(null);
            // GG-T290: the Scan ability was retired (the Bestiary now learns from defeats), so it leaves every
            // hero's known list and ability bar. Idempotent.
            foreach (var h in s.Roster)
            {
                h.Known?.RemoveAll(id => RetiredAbilities.Contains(id));
                if (h.Bar != null)
                    for (int i = 0; i < h.Bar.Count; i++)
                        if (h.Bar[i] != null && RetiredAbilities.Contains(h.Bar[i])) h.Bar[i] = null;
            }
            if (s.Version < 3)
            {
                // GG-T89: one Armor slot split into five (Head/Torso/Legs/Gloves/Boots) and Relic1-3 became
                // named jewelry (Necklace/Earring1/Earring2). Best-effort carry-over so nobody's gear vanishes:
                // the old Armor piece becomes Torso (the closest match), and relics fill in order — a relic's
                // own category may not match the slot it lands in, but it still contributes its stats there.
                var jewelrySlots = new[] { EquipmentSlot.Necklace, EquipmentSlot.Earring1, EquipmentSlot.Earring2 };
                foreach (var h in s.Roster)
                {
                    h.Armor ??= new Dictionary<EquipmentSlot, EquippedArmor>();
                    h.Jewelry ??= new Dictionary<EquipmentSlot, string>();
                    if (h.ArmorId != null)
                        h.Armor[EquipmentSlot.Torso] = new EquippedArmor { ItemId = h.ArmorId, Durability = h.ArmorDurability, Repairs = h.ArmorRepairs, Mastery = h.ArmorMastery };
                    for (int i = 0; i < jewelrySlots.Length && i < (h.Relics?.Count ?? 0); i++)
                        if (h.Relics[i] != null) h.Jewelry[jewelrySlots[i]] = h.Relics[i];
                    h.ArmorId = null; h.ArmorDurability = 0; h.ArmorRepairs = 0; h.ArmorMastery = 1f;
                    h.Relics = new List<string>();
                }
                s.Version = 3;
            }
            if (s.Version < 4)
            {
                // GG-T119: Camp NPCs are now met on the journey and their services stay hidden until they join.
                // A save from before that has been using every service all along, so everyone has already joined.
                CampNpcs.JoinAll(s);
                s.Version = 4;
            }
            if (s.Version < 5)
            {
                // GG-T344: The Still (three stages) was inserted between the Anchor Wastes and Halcyon, at campaign index
                // StillInsertedAt. HighestClearedStage is a campaign index, so a save already past the Anchor Wastes moves
                // up by the Still's stage count and keeps the same frontier (its Halcyon/postgame clears stay cleared;
                // the Still opens behind it, replayable like any earlier stage).
                if (s.HighestClearedStage >= StillInsertedAt) s.HighestClearedStage += StillStageCount;
                s.Version = 5;
            }
            if (s.Version < 6)
            {
                // GG-T345: The Lantern Road (three stages) was inserted between the Tithe Moors and the Shardfall, at campaign
                // index LanternRoadInsertedAt. Same rule as v5: a save already past the Tithe Moors moves up by its stage
                // count and keeps the same frontier; the Lantern Road opens behind it, replayable like any earlier stage.
                if (s.HighestClearedStage >= LanternRoadInsertedAt) s.HighestClearedStage += LanternRoadStageCount;
                s.Version = 6;
            }
            if (s.Version < 7)
            {
                // GG-T350: The Choir's Cathedral (three stages) was inserted after Halcyon as the new finale, at campaign
                // index CathedralInsertedAt. Same rule as v5/v6: a save already into the postgame moves up by its stage
                // count and keeps the same frontier. A save whose furthest clear is Halcyon III finds the cathedral next.
                if (s.HighestClearedStage >= CathedralInsertedAt) s.HighestClearedStage += CathedralStageCount;
                s.Version = 7;
            }
            if (s.Version < 8)
            {
                // GG-T347: The Bloom (three stages) was inserted after the Cathedral as the first postgame chapter, at campaign
                // index BloomInsertedAt. Same rule as v5-v7: a save already into the old postgame moves up by its stage count
                // and keeps the same frontier. A save whose furthest clear is the Cathedral III finds the Bloom next.
                if (s.HighestClearedStage >= BloomInsertedAt) s.HighestClearedStage += BloomStageCount;
                s.Version = 8;
            }
            if (s.Version < 9)
            {
                // GG-T357: the five older postgame chapters were put in story order and onto a rising level curve, which
                // moved six of their ten stages (no stage was added or removed). HighestClearedStage is a single frontier,
                // so it becomes the furthest new index among everything the save had cleared: its furthest clear is
                // still cleared, and any chapter that moved behind it opens there, replayable like any earlier stage.
                s.HighestClearedStage = ReorderedPostgameFrontier(s.HighestClearedStage);
                s.Version = 9;
            }
            if (s.Version < 10)
            {
                // GG-T432: crafting stations gained gold-bought facility levels that gate material tiers (§25.12.2). A save
                // from before keeps roughly what its station skill had reached: Journeyman -> the first level, Master -> the second.
                s.Caravan ??= new Dictionary<string, int>();
                s.StationXp ??= new Dictionary<string, int>();
                Facilities.Grandfather(s);
                s.Version = 10;
            }
            if (s.Version < 11)
            {
                // GG-T469: the purchase ledger. No older save bought anything with real money (release builds had no store
                // before GG-T468), so it starts empty.
                s.Purchases = new PurchaseSave();
                s.Version = 11;
            }
            // GG-T537: Camp vendors now join on stage clears. A save from before (v11 or older) already past a vendor's stage
            // takes that beat as told (below, once content is at hand), so it doesn't get a run of join scenes at once.
            bool beforeStoryVendors = s.Version < 12;
            if (s.Version < 12) s.Version = 12;
            // GG-T538: vendor stock and recipe lists grow with progress and station rank. A save from before keeps what it
            // owns on the shelf and in the lists (VendorTiers.Grandfather, below, once content is at hand).
            bool beforeVendorTiers = s.Version < 13;
            if (s.Version < 13) s.Version = 13;
            if (s.Version < 14)
            {
                // GG-T532: heroes have a female or male version now. Everyone already in an older save is the female art they
                // have always been, and nobody is asked again (a hero who joins from here on is asked when they join).
                foreach (var h in s.Roster)
                {
                    if (h == null) continue;
                    h.Variant = HeroVariant.Female;
                    h.VariantPending = false;
                }
                s.Version = 14;
            }
            // Any version: a variant the enum doesn't know (a hand-edited or future save) reads as the female default.
            foreach (var h in s.Roster)
                if (h != null && h.Variant != HeroVariant.Female && h.Variant != HeroVariant.Male) h.Variant = HeroVariant.Female;
            s.ShopBought ??= new HashSet<string>();
            s.CraftedRecipes ??= new HashSet<string>();
            s.Purchases ??= new PurchaseSave();
            s.Purchases.Granted ??= new Dictionary<string, PurchaseRecord>(StringComparer.Ordinal);
            foreach (var h in s.Roster)
            {
                h.Armor ??= new Dictionary<EquipmentSlot, EquippedArmor>();
                h.Jewelry ??= new Dictionary<EquipmentSlot, string>();
            }
            // GG-T52 (gear-proficiency gating): a save written before this shipped may already have a hero
            // wearing gear their class isn't nominally trained for (there was no gate to stop them). Rather
            // than strip currently-equipped gear out from under a returning player, auto-grant the matching
            // proficiency-unlock passive so Equipment.Fits keeps considering them proficient. Re-running this
            // on an already-fine save is a no-op (every check is already satisfied). No-op entirely without a
            // ContentDb (c == null), e.g. the version-only unit tests above that never touch gear/content.
            // Only for saves from before the gate (beforeGearGate): on a newer save it would re-grant, free, a proficiency
            // the Inn's Respec just refunded while the gear stayed on.
            Notes.Clear();
            if (c != null) DropRetiredHeroes(s, c);
            if (c != null) EnsureLead(s, c);
            if (c != null) EnsureVendors(s, c, beforeStoryVendors);
            JoinNewKeepers(s);
            if (c != null && beforeVendorTiers) VendorTiers.Grandfather(c, s);
            if (c != null && beforeGearGate)
                foreach (var h in s.Roster)
                {
                    if (h == null) continue;
                    GrandfatherGearProficiency(c, h, h.WeaponId);
                    foreach (var piece in h.Armor.Values) GrandfatherGearProficiency(c, h, piece.ItemId);
                }
            return s;
        }

        /// <summary>What the last <see cref="Migrate"/> did that the player should hear about (e.g. a retired hero leaving,
        /// GG-T531). Core/Meta can't log, so the save store logs these after a load.</summary>
        public static readonly List<string> Notes = new List<string>();

        /// <summary>GG-T531: a hero whose class no longer exists (the Echo was cut; the Aetherist, GG-T570, became a Camp
        /// NPC) can't be built into a battle, so it leaves the roster and the party. Its gear goes back to the bag with its
        /// wear (as the v3 armor split did, nobody's gear vanishes); its levels and skill points can't move to a class it
        /// never was. Idempotent: a clean roster is untouched. Never throws: a hero it can't clear is still removed.</summary>
        public static void DropRetiredHeroes(SaveData s, ContentDb c)
        {
            if (s?.Roster == null || c == null) return;
            for (int i = s.Roster.Count - 1; i >= 0; i--)
            {
                var h = s.Roster[i];
                if (h == null) { s.Roster.RemoveAt(i); continue; }
                if (h.ClassId != null && c.TryClass(h.ClassId) != null) continue;
                try
                {
                    h.Armor ??= new Dictionary<EquipmentSlot, EquippedArmor>();
                    h.Jewelry ??= new Dictionary<EquipmentSlot, string>();
                    var slots = new List<EquipmentSlot> { EquipmentSlot.Weapon };
                    slots.AddRange(h.Armor.Keys);
                    slots.AddRange(h.Jewelry.Keys);
                    foreach (var slot in slots)
                    {
                        try { Equipment.Unequip(c, s, h, slot); }
                        catch (Exception e) { Notes.Add($"retired hero '{h.ClassId}': couldn't return {slot} gear ({e.Message})"); }
                    }
                }
                catch (Exception e) { Notes.Add($"retired hero '{h.ClassId}': couldn't clear its gear ({e.Message})"); }
                s.Roster.RemoveAt(i);
                s.Party?.RemoveAll(id => string.Equals(id, h.ClassId, StringComparison.OrdinalIgnoreCase));
                Notes.Add($"hero class '{h.ClassId ?? "(none)"}' no longer exists; removed from the roster, gear returned to the bag");
            }
        }

        /// <summary>GG-T570: a save that had already met every Camp keeper before a new one existed (the Aetherist) is a
        /// veteran Camp (<see cref="CampUnlocks"/>): it meets the newcomer on load too, so nothing it had open closes. A save
        /// past the newcomer's join stage gets them from <see cref="EnsureVendors"/> anyway. Idempotent; never throws.</summary>
        public static void JoinNewKeepers(SaveData s)
        {
            if (s == null) return;
            try
            {
                s.Flags ??= new HashSet<string>();
                if (CampNpcs.Joined(s, CampNpcs.Aetherist)) return;
                foreach (var npc in CampNpcs.All)
                    if (npc != CampNpcs.Aetherist && !CampNpcs.Joined(s, npc)) return;
                s.Flags.Add(ContentLoader.NpcFlagPrefix + CampNpcs.Aetherist);
                Notes.Add("the Aetherist joined (the save had met every other Camp keeper, GG-T570)");
            }
            catch (Exception e) { Notes.Add($"couldn't bring the Aetherist: {e.Message}"); }
        }

        /// <summary>GG-T533: every save holds the fixed lead (<see cref="Progression.LeadClass"/>), first in the party. A save
        /// from before the Warden existed gets them on load the way a summon joins (the roster's average experience, trained
        /// to that level, <see cref="Summon.Join"/>); they then go to the front of the party, and if that makes it more than
        /// the cap the last member steps back to the reserve (no one leaves the roster). Idempotent; never throws on a bad
        /// save (a content set without the class, or a roster that can't take them, is left as it is).</summary>
        public static void EnsureLead(SaveData s, ContentDb c)
        {
            if (s == null || c == null || c.TryClass(Progression.LeadClass) == null) return;
            try
            {
                s.Roster ??= new List<HeroSave>();
                s.Party ??= new List<string>();
                if (s.Hero(Progression.LeadClass) == null)
                {
                    Summon.Join(c, s, Progression.LeadClass);
                    Notes.Add("Warden (the fixed lead, GG-T533) joined this save's roster");
                }
                if (s.Party.Count > 0 && Progression.IsLead(s.Party[0])) return;
                s.Party.RemoveAll(Progression.IsLead);
                s.Party.Insert(0, Progression.LeadClass);
                while (s.Party.Count > Progression.MaxPartySize) s.Party.RemoveAt(s.Party.Count - 1);
            }
            catch (Exception e) { Notes.Add($"couldn't add the lead: {e.Message}"); }
        }

        /// <summary>GG-T537: every Camp vendor whose story beat's stage this save has cleared is with the caravan: a save past
        /// the beat (made before vendors joined on stage clears, or one that skipped the scene) gets the vendor on load, never
        /// locked out. Nobody is ever removed: a vendor met earlier under the old triggers stays. <paramref name="markTold"/>
        /// (a save from before GG-T537) also takes every vendor beat it is past as told, so the scenes don't all play at
        /// the next Camp. Idempotent; never throws.</summary>
        public static void EnsureVendors(SaveData s, ContentDb c, bool markTold)
        {
            if (s == null || c == null) return;
            try
            {
                s.Flags ??= new HashSet<string>();
                s.SeenDialogues ??= new HashSet<string>();
                var stages = c.CampaignStages();
                foreach (var d in c.DialogueOrder)
                {
                    if (!DialogueRunner.IsVendorBeat(d)) continue;
                    string stage = d.Trigger.Id;
                    int at = stages.FindIndex(st => string.Equals(st.Id, stage, StringComparison.OrdinalIgnoreCase));
                    if (!(s.ClearedStages != null && s.ClearedStages.Contains(stage)) && !(at >= 0 && at <= s.HighestClearedStage)) continue;
                    foreach (var e in d.AllEffects())
                        if (e?.Type == DialogueEffectType.JoinNpc && s.Flags.Add(ContentLoader.NpcFlagPrefix + e.Id))
                            Notes.Add($"{DialogueRunner.Npc(e.Id)} joined (the save is past {stage}, GG-T537)");
                    if (markTold) s.SeenDialogues.Add(d.Id);
                }
            }
            catch (Exception e) { Notes.Add($"couldn't bring the Camp vendors: {e.Message}"); }
        }

        /// <summary>GG-T52: if <paramref name="itemId"/> is equipped but <paramref name="h"/> isn't nominally
        /// proficient in it, grant whichever proficiency-unlock passive would explain that (idempotent — a
        /// hero who's already proficient, or has nothing in that slot, is untouched).</summary>
        private static void GrandfatherGearProficiency(ContentDb c, HeroSave h, string itemId)
        {
            var item = itemId != null ? c.TryItem(itemId) : null;
            if (item == null) return;
            if (item.Slot == EquipmentSlot.Weapon && item.WeaponClass != WeaponClass.Simple &&
                !Progression.EffectiveWeaponProficiencies(c, h).Contains(item.WeaponClass))
            {
                var passive = item.WeaponClass == WeaponClass.Arcane ? "arcane_focus_proficiency" : "martial_weapons_proficiency";
                if (!h.Known.Contains(passive)) h.Known.Add(passive);
            }
            else if (Array.IndexOf(BattleEngine.ArmorSlots, item.Slot) >= 0 &&
                     item.ArmorWeight > Progression.EffectiveArmorProficiency(c, h))
            {
                if (!h.Known.Contains("heavy_armor_proficiency")) h.Known.Add("heavy_armor_proficiency");
            }
        }
    }
}
