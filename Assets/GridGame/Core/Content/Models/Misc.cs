using System.Collections.Generic;

namespace GridGame.Core.Content
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Dialogue (§27.1, GG-T117): dialogues.json. The engine (GridGame.Meta.DialogueRunner) is a data
    // interpreter over these shapes; conditions and effects are the save's own vocabulary, not a script.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>When a dialogue plays. Story beats (ThemeStart, StageCleared) play once, between battles,
    /// from StageLauncher; Talk dialogues are picked from Camp's Talk row and may repeat unless <see cref="DialogueDef.Once"/>.
    /// GG-T537 (owner, 2026-11-21): every Camp vendor joins on a story beat of the campaign, never on an incidental trigger,
    /// so a JoinNpc or OpenStation effect lives only in an unconditional StageCleared beat. That beat also plays on the way
    /// back to Camp after the clear (the join moment), not only at the next launch. The old GearWorn, CanBrew and
    /// FirstArmor triggers (GG-T175/T176/T179) were removed with it.
    /// Recruit (GG-T132) is never played between battles: a stage's <see cref="RecruitableDef.Dialogue"/> names it and the
    /// battle speaks it when a hero talks to the neutral (<see cref="DialogueTrigger.Id"/> = the recruitable's class).
    /// LevelReached (GG-T231) fires once any roster hero is at <see cref="DialogueTrigger.Level"/> or above — on the way back
    /// to Camp after the battle that did it, or at the next launch. StageStart (GG-T231) fires on launching the stage
    /// <see cref="DialogueTrigger.Id"/> (a boss's introduction).
    /// Wanderer (GG-T342; replaces the GG-T236 Rescue scene) is the talk with a wanderer standing at a victory, played from
    /// the results screen: "$hero" is the wanderer, "$with" the party member the player chose to talk, and
    /// <see cref="DialogueTrigger.Id"/> is the wanderer's hero class, a factions.json faction id, or null (the generic one).
    /// GG-T387 (the owner's "game of wits"): it is a contest of <see cref="DialogueDef.WitsExchanges"/> exchanges, each a
    /// question from the wanderer with three replies for the companion, one per <see cref="DialogueChoice.Wit"/> (+1 wins
    /// the point, 0 is neutral, -1 loses it); a later node goes on to <see cref="DialogueDef.OutcomeNext"/>, where the meta
    /// layer's rule (the standing) picks <see cref="DialogueDef.JoinNode"/> or <see cref="DialogueDef.PartNode"/>.
    /// Trust and Cohesion (GG-T1298, §27.1) are bond scenes in Camp's Talk row: Trust is the hero
    /// <see cref="DialogueTrigger.Id"/>'s, offered while they're in the party and open once their Trust tier reaches
    /// <see cref="DialogueTrigger.Level"/>; Cohesion is the pair Id + <see cref="DialogueTrigger.Other"/>, offered while both
    /// are in the party and open at that cohesion tier. Shown locked (with the tier they need) until then; each plays
    /// once, and "$with" is the hero Id.
    /// Founding (GG-T1318) plays once, after the opening crawl, with the starting party ("$party:N"); no Id.
    /// Joined (GG-T1318) is a hero's first talk at Camp after joining by a non-story route (a hero not in
    /// SaveData.MetHeroes): "$hero" is the joined hero, and <see cref="DialogueTrigger.Id"/> is their hero class, a
    /// factions.json faction id, or null (the generic one), picked like a Wanderer talk.</summary>
    public enum DialogueTriggerKind { ThemeStart, StageCleared, Talk, Recruit, LevelReached, StageStart, Wanderer, Trust, Cohesion, Founding, Joined }

    public sealed class DialogueTrigger
    {
        public DialogueTriggerKind Kind;
        /// <summary>ThemeStart: the theme id. StageCleared: the stage id. Talk: who it's with — a hero class id,
        /// "party" (any party member; speaker class "$with" is them) or "npc:&lt;id&gt;" (a joined Camp NPC).
        /// Trust / Cohesion: the hero class id (Cohesion: the pair's first hero). Joined: a hero class id, a faction id or null.</summary>
        public string Id;
        /// <summary>Cohesion only: the pair's second hero class id.</summary>
        public string Other;
        /// <summary>LevelReached: the hero level (2+) that fires it. Trust / Cohesion: the bond tier (1-5) that opens it.</summary>
        public int Level;
    }

    /// <summary>QuestAvailable / QuestActive / QuestDone (GG-T237, §22.11): <see cref="DialogueCondition.Id"/> is a quests.json id.
    /// Available = its unlock stage is cleared and it's neither under way nor handed in.
    /// TrustAtLeast / CohesionAtLeast (GG-T1293, §25.20): the hero <see cref="DialogueCondition.Id"/>'s Trust, or the cohesion of
    /// Id and <see cref="DialogueCondition.Other"/>, is at least <see cref="DialogueCondition.Amount"/>; each hero is a class id
    /// or a speaker token ("$with", "$recruit", "$hero", "$party:N", "$trusted"; not "$commander").</summary>
    public enum DialogueConditionType { FlagSet, FlagNotSet, HasItem, GoldAtLeast, PartyHas, RosterHas, RosterLacks, NpcJoined, QuestAvailable, QuestActive, QuestDone, TrustAtLeast, CohesionAtLeast }

    public sealed class DialogueCondition
    {
        public DialogueConditionType Type;
        /// <summary>Flag / item / class / NPC id, by type.</summary>
        public string Id;
        /// <summary>HasItem count (default 1), GoldAtLeast amount, or the Trust / cohesion to reach.</summary>
        public int Amount;
        /// <summary>CohesionAtLeast only: the second hero of the pair.</summary>
        public string Other;
    }

    /// <summary>Nothing here can remove a hero for good (owner rule: nobody dies permanently).
    /// RecruitHero hires exactly the named class (no-op if owned); JoinHero (GG-T118) is a story recruit that
    /// always brings someone new: the named class if it's free, else a hero of <see cref="DialogueEffect.Role"/>
    /// (or of a role the roster lacks) the player doesn't have yet.
    /// GrantSkillPoint (GG-T133) gives a hero — <see cref="DialogueEffect.Id"/> as a class id, or "$with"/null for the
    /// Talk partner — <see cref="DialogueEffect.Amount"/> skill points (default 1), once per dialogue per hero: the
    /// save flag <c>sp:&lt;dialogue&gt;:&lt;hero&gt;</c> guards it, so a replayed Talk can't farm it.
    /// StartQuest (GG-T237) puts the quests.json quest <see cref="DialogueEffect.Id"/> into the journal (no-op if it's under way or done).
    /// OpenStation (GG-T537) marks the story beat that opens a keeperless Camp station (<see cref="DialogueDef.Stations"/>):
    /// the station itself opens on the stage clear (Meta's CampUnlocks); the effect only announces it.
    /// AddTrust / AddCohesion (GG-T1293, §25.20) move the hero <see cref="DialogueEffect.Id"/>'s Trust, or the cohesion of Id
    /// and <see cref="DialogueEffect.Other"/>, by <see cref="DialogueEffect.Amount"/>; each hero is a class id or a speaker
    /// token ("$with", "$recruit", "$hero", "$party:N", "$trusted"; not "$commander"). Each pays once per save at its node (and choice): a replayed Talk can't farm it.</summary>
    public enum DialogueEffectType { SetFlag, ClearFlag, GrantGold, GrantItem, RecruitHero, JoinNpc, JoinHero, GrantSkillPoint, StartQuest, OpenStation, AddTrust, AddCohesion }

    public sealed class DialogueEffect
    {
        public DialogueEffectType Type;
        public string Id;
        /// <summary>GrantGold amount (negative = pay), GrantItem count (default 1), GrantSkillPoint count (default 1), or
        /// the Trust / cohesion change (either sign).</summary>
        public int Amount;
        /// <summary>JoinHero only: the role to fall back on when <see cref="Id"/> is missing or already owned.</summary>
        public string Role;
        /// <summary>AddCohesion only: the second hero of the pair.</summary>
        public string Other;
    }

    public sealed class DialogueSpeaker
    {
        public string Id;
        /// <summary>A class id (its name and portrait), or "$lead" (the first party member), "$with" (the
        /// party member a Talk dialogue is with) or "$recruit" (the hero this dialogue's first JoinHero brings, GG-T118), or
        /// "$hero" (the wanderer a Wanderer talk is with, GG-T342). GG-T1575: "$commander" (the player, named "the Commander"
        /// with a silhouette until the canon fills them), "$party:N" (the Nth party member, 1-based, up to the party cap) or
        /// "$trusted" (the party hero with the most Trust, ties to the earlier slot), bound when the dialogue opens; a
        /// dialogue whose "$party:N" or "$trusted" this save can't fill isn't offered.</summary>
        public string Class;
        /// <summary>Display name; defaults to the class's name.</summary>
        public string Name;
        /// <summary>The class whose portrait this speaker shows (an NPC borrowing a class's art, e.g. the Lantern Merchant
        /// shows the courier's); defaults to <see cref="Class"/>.</summary>
        public string Portrait;
    }

    public sealed class DialogueChoice
    {
        public string Label;
        /// <summary>Wanderer talks only (GG-T387, the game of wits): what this reply does to the standing: +1 wins the
        /// point, 0 is neutral, -1 loses it. Every exchange offers one of each. 0 everywhere else.</summary>
        public int Wit;
        /// <summary>Node id to go to; null ends the dialogue.</summary>
        public string Next;
        /// <summary>All must hold for the choice to be offered.</summary>
        public List<DialogueCondition> Conditions = new List<DialogueCondition>();
        public List<DialogueEffect> Effects = new List<DialogueEffect>();
    }

    public sealed class DialogueNode
    {
        public string Id;
        /// <summary>A <see cref="DialogueSpeaker.Id"/> of this dialogue.</summary>
        public string Speaker;
        public string Text;
        /// <summary>Auto-advance target when there are no choices; null ends the dialogue.</summary>
        public string Next;
        public List<DialogueChoice> Choices = new List<DialogueChoice>();
        /// <summary>Applied when the node is reached.</summary>
        public List<DialogueEffect> Effects = new List<DialogueEffect>();
    }

    public sealed class DialogueDef
    {
        /// <summary>Camp NPC ids a JoinNpc effect / NpcJoined condition / "npc:" Talk may name (GG-T119 gates services on them).
        /// GG-T570: the Aetherist (the retired hero class, now the keeper of the Aetherite refinery) joins at The Shardfall II.</summary>
        public static readonly string[] Npcs = { "blacksmith", "alchemist", "enchantress", "merchant", "innkeeper", "aetherist" };
        /// <summary>GG-T570: a Camp shop keeper's bark occasions, "&lt;npc&gt;/&lt;occasion&gt;". Each is the Prose bark
        /// <c>bark:shop/&lt;npc&gt;/&lt;occasion&gt;</c> (its lines, one said at a time), joined into
        /// <see cref="ContentDb.ShopBarks"/>: the Aetherist greets, refines Dust into Shards, is short of Dust, and says goodbye.</summary>
        public static readonly string[] ShopBarks = { "aetherist/greet", "aetherist/refine", "aetherist/short", "aetherist/leave" };
        /// <summary>GG-T537: the keeperless Camp stations an OpenStation effect may name (Summon Circle with the Diamond Store,
        /// Bounties with the Mercenaries, the Crucible, the Caravan).</summary>
        public static readonly string[] Stations = { "summon", "bounties", "crucible", "caravan" };

        /// <summary>Wanderer talks (GG-T342): the two endings the <see cref="OutcomeNext"/> marker resolves to.</summary>
        public const string JoinNode = "join", PartNode = "part", OutcomeNext = "$outcome";
        /// <summary>Wanderer talks (GG-T387): the exchanges in the game of wits (questions, each with a win, a neutral and
        /// a losing reply).</summary>
        public const int WitsExchanges = 3;

        public string Id;
        /// <summary>Talk row label (Talk dialogues); also a debugging name.</summary>
        public string Title;
        public DialogueTrigger Trigger = new DialogueTrigger();
        /// <summary>All must hold for the dialogue to be offered.</summary>
        public List<DialogueCondition> Conditions = new List<DialogueCondition>();
        /// <summary>Talk only: play once, then never offer again (story beats always play once).</summary>
        public bool Once;
        /// <summary>Backdrop theme id (defaults to the trigger's theme, else the camp's).</summary>
        public string Backdrop;
        /// <summary>Story beats only (GG-T231): a text crawl, titled <see cref="Title"/>, that scrolls before the conversation.
        /// Resolved from Prose ("crawl:dialogue/&lt;id&gt;", GG-T355); dialogues.json carries only <see cref="HasCrawl"/>.</summary>
        public string Crawl;
        /// <summary>GG-T355: this beat opens with a crawl, whose words live in Prose. The marker is what gives the key.</summary>
        public bool HasCrawl;
        public List<DialogueSpeaker> Speakers = new List<DialogueSpeaker>();
        /// <summary>First node id; defaults to the first node.</summary>
        public string Start;
        public List<DialogueNode> Nodes = new List<DialogueNode>();

        /// <summary>Every effect in the dialogue — nodes' then their choices', in node order (null nodes/choices skipped).</summary>
        public IEnumerable<DialogueEffect> AllEffects()
        {
            foreach (var n in Nodes)
            {
                if (n == null) continue;
                foreach (var e in n.Effects) yield return e;
                foreach (var ch in n.Choices)
                    if (ch != null) foreach (var e in ch.Effects) yield return e;
            }
        }
    }

    /// <summary>GG-T923: the kinds of Codex entry (the in-game Lore / Encyclopedia, GG-T922). An entry's key is
    /// "&lt;kind&gt;:&lt;id&gt;" (<see cref="Key"/>, Meta <c>Codex.Key</c>), ids lower-cased. Lives in Core so the content
    /// loader can check codex.json (GG-T936).</summary>
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

        public static string Key(string kind, string id) => kind + ":" + (id ?? "").Trim().ToLowerInvariant();

        /// <summary>Splits "&lt;kind&gt;:&lt;id&gt;"; false when there is no colon or either side is empty.</summary>
        public static bool TrySplit(string key, out string kind, out string id)
        {
            kind = id = null;
            int colon = key?.IndexOf(':') ?? -1;
            if (colon <= 0 || colon == key.Length - 1) return false;
            kind = key.Substring(0, colon);
            id = key.Substring(colon + 1);
            return true;
        }
    }

    /// <summary>GG-T936: one codex.json entry (GG-T924): which Codex entry exists, its title and what it links to. The
    /// body is Prose (description key "codex:&lt;kind&gt;/&lt;id&gt;", payload "long"), joined into <see cref="Body"/>; with
    /// no text yet the entry falls back to the record's own words (<see cref="ContentDb.CodexEntry"/>).</summary>
    public sealed class CodexDef
    {
        /// <summary>"&lt;kind&gt;:&lt;id&gt;" (<see cref="CodexKinds.Key"/>).</summary>
        public string Key;
        /// <summary>Null = the record's own name.</summary>
        public string Title;
        /// <summary>Other entries' keys this one links to.</summary>
        public List<string> Related = new List<string>();
        /// <summary>Filled by <see cref="ProseText.Join"/> from the Prose text; null when there is none yet.</summary>
        public string Body;

        /// <summary>The Prose key of this entry's body: "codex:&lt;kind&gt;/&lt;id&gt;".</summary>
        public static string TextKey(string key)
        {
            if (!CodexKinds.TrySplit(key, out var kind, out var id)) return ProseText.CodexKey + key;
            return ProseText.CodexKey + kind + "/" + id;
        }
    }

    /// <summary>GG-T936: one Codex entry as the screen shows it, resolved by <see cref="ContentDb.CodexEntry"/>.</summary>
    public sealed class CodexEntryView
    {
        public string Kind;
        public string Id;
        public string Key;
        public string Title;
        public string Body;
        /// <summary>Related keys that resolve to an entry.</summary>
        public List<string> Related = new List<string>();
        /// <summary>True when the kind is known and the record exists.</summary>
        public bool Exists;
    }
}
