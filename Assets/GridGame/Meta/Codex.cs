using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>GG-T923: the kinds of Codex entry (the in-game Lore / Encyclopedia, GG-T922). A save's entry key is
    /// "&lt;kind&gt;:&lt;id&gt;" (<see cref="Codex.Key"/>), ids lower-cased.</summary>
    public static class CodexKinds
    {
        /// <summary>A campaign region (theme id) or stage id.</summary>
        public const string Place = "place";
        /// <summary>A factions.json faction id.</summary>
        public const string Faction = "faction";
        /// <summary>A hero class id, a Camp NPC id (merchant, blacksmith, …) or a caravan crew member's Prose slug.</summary>
        public const string Character = "character";
        /// <summary>An enemy class id (its Bestiary entry is Seen).</summary>
        public const string Foe = "foe";
        public const string Item = "item";
        public const string Ability = "ability";
        /// <summary>A glossary entry id.</summary>
        public const string Keyword = "keyword";
        /// <summary>"chapter/&lt;id&gt;" (a chapter crawl played), "dialogue/&lt;id&gt;" (a dialogue played to the end), or the
        /// rest of a "story:" flag ("ending").</summary>
        public const string Story = "story";

        public static readonly string[] All = { Place, Faction, Character, Foe, Item, Ability, Keyword, Story };
    }

    /// <summary>GG-T923: which Codex entries this save has unlocked (<see cref="SaveData.Codex"/>) and read
    /// (<see cref="SaveData.CodexRead"/>). An entry unlocks once and never locks again; unlocked and unread is "new" (the
    /// screen's badge). <see cref="Sync"/> derives every entry the save's own state already shows (cleared stages, Bestiary,
    /// roster, bag, joined NPCs, crawls, flags), so it is both the save-v16 back-fill and the catch-all after a battle or a
    /// dialogue. Never throws on a partial save.</summary>
    public static class Codex
    {
        public static string Key(string kind, string id) => kind + ":" + (id ?? "").Trim().ToLowerInvariant();

        /// <summary>Unlocks one entry; true when it is newly unlocked.</summary>
        public static bool Unlock(SaveData s, string kind, string id)
        {
            if (s == null || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id)) return false;
            s.Codex ??= new HashSet<string>();
            return s.Codex.Add(Key(kind, id));
        }

        public static bool IsUnlocked(SaveData s, string kind, string id) =>
            s?.Codex != null && !string.IsNullOrWhiteSpace(id) && s.Codex.Contains(Key(kind, id));

        public static bool IsRead(SaveData s, string kind, string id) =>
            s?.CodexRead != null && !string.IsNullOrWhiteSpace(id) && s.CodexRead.Contains(Key(kind, id));

        /// <summary>Unlocked and not yet read: the "new" badge.</summary>
        public static bool IsNew(SaveData s, string kind, string id) => IsUnlocked(s, kind, id) && !IsRead(s, kind, id);

        /// <summary>Marks an unlocked entry read; a locked one stays unread (false).</summary>
        public static bool MarkRead(SaveData s, string kind, string id)
        {
            if (!IsUnlocked(s, kind, id)) return false;
            s.CodexRead ??= new HashSet<string>();
            return s.CodexRead.Add(Key(kind, id));
        }

        /// <summary>Unlocked ids of one kind.</summary>
        public static List<string> Unlocked(SaveData s, string kind)
        {
            var list = new List<string>();
            if (s?.Codex == null) return list;
            string prefix = kind + ":";
            foreach (var k in s.Codex)
                if (k.StartsWith(prefix, StringComparison.Ordinal)) list.Add(k.Substring(prefix.Length));
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Unlocked, unread entries of one kind (null = every kind).</summary>
        public static int NewCount(SaveData s, string kind = null)
        {
            if (s?.Codex == null) return 0;
            int n = 0;
            foreach (var k in s.Codex)
                if ((kind == null || k.StartsWith(kind + ":", StringComparison.Ordinal)) && !(s.CodexRead?.Contains(k) ?? false)) n++;
            return n;
        }

        /// <summary>Unlocks everything the save already shows it has reached. <paramref name="markRead"/> (the v16 back-fill)
        /// also takes what it unlocks as read, so a returning save doesn't light every entry as new. Content-free entries
        /// (foes, items, abilities, NPCs, story) unlock without <paramref name="c"/>; places and factions need it.
        /// Idempotent; returns how many entries it newly unlocked.</summary>
        public static int Sync(ContentDb c, SaveData s, bool markRead = false)
        {
            if (s == null) return 0;
            int n = 0;
            void Add(string kind, string id)
            {
                if (!Unlock(s, kind, id)) return;
                n++;
                if (markRead) MarkRead(s, kind, id);
            }
            try
            {
                if (s.Bestiary != null)
                    foreach (var kv in s.Bestiary)
                        if (kv.Value != null && (kv.Value.Seen || BestiaryKnowledge.Tier(kv.Value) > BestiaryKnowledge.None)) Add(CodexKinds.Foe, kv.Key);
                if (s.Inventory != null) foreach (var kv in s.Inventory) if (kv.Value > 0) Add(CodexKinds.Item, kv.Key);
                if (s.Broken != null) foreach (var kv in s.Broken) if (kv.Value > 0) Add(CodexKinds.Item, kv.Key);
                if (s.Roster != null)
                    foreach (var h in s.Roster)
                    {
                        if (h?.ClassId == null) continue;
                        Add(CodexKinds.Character, h.ClassId);
                        if (h.Known != null) foreach (var a in h.Known) Add(CodexKinds.Ability, a);
                        Add(CodexKinds.Item, h.WeaponId);
                        if (h.Armor != null) foreach (var p in h.Armor.Values) Add(CodexKinds.Item, p?.ItemId);
                        if (h.Jewelry != null) foreach (var j in h.Jewelry.Values) Add(CodexKinds.Item, j);
                        var f = c?.FactionOf(h.ClassId);
                        if (f != null) Add(CodexKinds.Faction, f.Id);
                    }
                if (s.Flags != null)
                    foreach (var flag in s.Flags)
                    {
                        if (flag == null) continue;
                        if (flag.StartsWith(ContentLoader.NpcFlagPrefix, StringComparison.Ordinal)) Add(CodexKinds.Character, flag.Substring(ContentLoader.NpcFlagPrefix.Length));
                        else if (flag.StartsWith("story:", StringComparison.Ordinal)) Add(CodexKinds.Story, flag.Substring("story:".Length));
                    }
                if (s.SeenCrawls != null)
                    foreach (var mark in s.SeenCrawls)
                        if (mark != null && mark.StartsWith("chapter:", StringComparison.Ordinal)) Add(CodexKinds.Story, "chapter/" + mark.Substring("chapter:".Length));
                if (s.SeenDialogues != null) foreach (var d in s.SeenDialogues) if (d != null) Add(CodexKinds.Story, "dialogue/" + d);
                if (c != null)
                {
                    var stages = c.CampaignStages();
                    for (int i = 0; i < stages.Count; i++)
                    {
                        var st = stages[i];
                        if (st?.Id == null || st.IsTest) continue;
                        if (i > s.HighestClearedStage && !(s.ClearedStages?.Contains(st.Id) ?? false)) continue;
                        Add(CodexKinds.Place, st.Id);
                        var theme = c.ThemeOf(st.Id);
                        if (theme?.Id != null) Add(CodexKinds.Place, theme.Id);
                    }
                    if (s.Caravan != null)
                        foreach (var kv in s.Caravan)
                            if (kv.Value > 0 && c.Caravan.TryGetValue(kv.Key, out var u) && !string.IsNullOrEmpty(u.Crew)) Add(CodexKinds.Character, ProseText.Slug(u.Crew));
                }
            }
            catch (Exception e) { SaveMigrations.Notes.Add($"couldn't sync the Codex: {e.Message}"); }
            return n;
        }
    }
}
