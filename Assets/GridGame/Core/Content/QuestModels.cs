using System;
using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>What an optional side quest asks for (§22.11, GG-T237) — only things the save already tracks. GG-T434 added
    /// <see cref="DefeatFamily"/>, <see cref="Recruit"/>, <see cref="ReachLevel"/> and <see cref="LearnSkill"/>.</summary>
    public enum QuestObjectiveKind { ClearStage, Defeat, Bring, Flag, DefeatFamily, Recruit, ReachLevel, LearnSkill }

    public sealed class QuestObjective
    {
        public QuestObjectiveKind Kind;
        /// <summary>ClearStage: the stage id. Defeat: the enemy class id. Bring: the item id. Flag: the save flag.
        /// DefeatFamily: the enemy family (the drop table its classes share, e.g. "slime"). Recruit: the hero class.
        /// ReachLevel: unused (see <see cref="Hero"/>). LearnSkill: the ability id.</summary>
        public string Id;
        /// <summary>Defeat/DefeatFamily: foes to fell (after accepting). Bring: items handed over (consumed on turn-in).
        /// ReachLevel: the level to reach. Default 1.</summary>
        public int Count = 1;
        /// <summary>ReachLevel/LearnSkill only (GG-T434): the hero class that must do it; null = any hero in the roster.</summary>
        public string Hero;
        /// <summary>ClearStage only: win with at most this many heroes of your own in the fight (0 = any).</summary>
        public int MaxHeroes;
        /// <summary>ClearStage only: every hero of your own must be standing at the victory.</summary>
        public bool NoneFallen;
    }

    /// <summary>One optional side quest (quests.json, §22.11, GG-T237): offered by a Camp conversation (a dialogue
    /// <c>StartQuest</c> effect), tracked in the journal, turned in from Camp for its rewards. Never gates the campaign.</summary>
    public sealed class QuestDef
    {
        public string Id;
        public string Name;
        /// <summary>Who hands it out, as the journal shows it ("Innkeeper").</summary>
        public string Giver;
        /// <summary>The campaign theme it belongs to (journal grouping and backdrop).</summary>
        public string Theme;
        /// <summary>Offered only once this stage is cleared; null = from the start.</summary>
        public string Unlock;
        /// <summary>The journal's one-paragraph ask, in the giver's voice.</summary>
        public string Summary;
        /// <summary>What the giver says when it's handed in (the turn-in toast).</summary>
        public string TurnIn;
        public QuestObjective Objective = new QuestObjective();
        public int RewardGold;
        public string RewardItem;
        public int RewardItemCount = 1;
        /// <summary>Skill points each hero in the active party gains on turn-in.</summary>
        public int RewardSkillPoints;
        /// <summary>Summon Tickets paid on turn-in (GG-T1607), into the save's earned Tickets.</summary>
        public int RewardTickets;
        /// <summary>A save flag set on turn-in — opens a follow-up Talk dialogue gated on it.</summary>
        public string RewardFlag;
        /// <summary>A hero class who joins the roster on turn-in (GG-T434; like a dialogue RecruitHero, nothing if owned).</summary>
        public string RewardHero;
        /// <summary>Quest chains (GG-T434): offered only once this quest is done. The journal shows the chain's steps.</summary>
        public string Requires;
    }

    /// <summary>quests.json validation (§22.11): run from <see cref="ContentLoader.Validate"/>.</summary>
    public static class QuestCatalog
    {
        public static void Validate(ContentDb db, List<string> p)
        {
            var given = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in db.DialogueOrder)
                foreach (var e in d.AllEffects())
                    if (e?.Type == DialogueEffectType.StartQuest && e.Id != null) given.Add(e.Id);
            foreach (var q in db.QuestOrder)
            {
                string w = $"quest {q.Id}";
                if (string.IsNullOrWhiteSpace(q.Name)) p.Add($"{w}: needs a Name");
                if (string.IsNullOrWhiteSpace(q.Summary)) p.Add($"{w}: needs a Summary");
                if (q.Theme != null && !db.Campaign.Themes.Exists(t => string.Equals(t.Id, q.Theme, StringComparison.OrdinalIgnoreCase))) p.Add($"{w}: unknown theme '{q.Theme}'");
                if (q.Unlock != null && !db.Stages.ContainsKey(q.Unlock)) p.Add($"{w}: unknown unlock stage '{q.Unlock}'");
                if (q.Requires != null)
                {
                    if (db.TryQuest(q.Requires) == null) p.Add($"{w}: requires unknown quest '{q.Requires}'");
                    else if (InCycle(db, q)) p.Add($"{w}: its Requires chain loops back on itself");
                }
                if (q.RewardHero != null && db.TryClass(q.RewardHero)?.Team != Team.Hero) p.Add($"{w}: RewardHero needs a hero class, has '{q.RewardHero}'");
                var o = q.Objective;
                if (o == null) { p.Add($"{w}: needs an Objective"); continue; }
                if (o.Count <= 0) p.Add($"{w}: objective Count must be positive");
                switch (o.Kind)
                {
                    case QuestObjectiveKind.ClearStage:
                        if (o.Id == null || !db.Stages.ContainsKey(o.Id)) p.Add($"{w}: unknown stage '{o.Id}'");
                        if (o.MaxHeroes < 0) p.Add($"{w}: MaxHeroes can't be negative");
                        break;
                    case QuestObjectiveKind.Defeat:
                        if (db.TryClass(o.Id)?.Team != Team.Enemy) p.Add($"{w}: Defeat needs an enemy class, has '{o.Id}'");
                        break;
                    case QuestObjectiveKind.Bring:
                        if (db.TryItem(o.Id) == null) p.Add($"{w}: unknown item '{o.Id}'");
                        break;
                    case QuestObjectiveKind.Flag:
                        if (string.IsNullOrEmpty(o.Id)) p.Add($"{w}: Flag needs a flag Id");
                        break;
                    case QuestObjectiveKind.DefeatFamily:
                        if (FamilyClasses(db, o.Id).Count == 0) p.Add($"{w}: unknown enemy family '{o.Id}'");
                        break;
                    case QuestObjectiveKind.Recruit:
                        if (db.TryClass(o.Id)?.Team != Team.Hero) p.Add($"{w}: Recruit needs a hero class, has '{o.Id}'");
                        break;
                    case QuestObjectiveKind.ReachLevel:
                        if (o.Count < 2 || o.Count > 99) p.Add($"{w}: ReachLevel needs a level from 2 to 99, has {o.Count}");
                        if (o.Id != null) p.Add($"{w}: ReachLevel names its hero in Hero, not Id");
                        break;
                    case QuestObjectiveKind.LearnSkill:
                        if (db.TryAbility(o.Id) == null) p.Add($"{w}: unknown skill '{o.Id}'");
                        break;
                }
                if (o.Hero != null && o.Kind != QuestObjectiveKind.ReachLevel && o.Kind != QuestObjectiveKind.LearnSkill) p.Add($"{w}: Hero only fits a ReachLevel or LearnSkill objective");
                if (o.Hero != null && db.TryClass(o.Hero)?.Team != Team.Hero) p.Add($"{w}: unknown hero '{o.Hero}'");
                if (o.Kind != QuestObjectiveKind.ClearStage && (o.MaxHeroes != 0 || o.NoneFallen)) p.Add($"{w}: MaxHeroes/NoneFallen only fit a ClearStage objective");
                if (q.RewardItem != null && db.TryItem(q.RewardItem) == null) p.Add($"{w}: unknown reward item '{q.RewardItem}'");
                if (q.RewardGold < 0 || q.RewardSkillPoints < 0 || q.RewardTickets < 0 || q.RewardItemCount <= 0) p.Add($"{w}: rewards can't be negative");
                if (q.RewardGold == 0 && q.RewardItem == null && q.RewardSkillPoints == 0 && q.RewardTickets == 0 && q.RewardFlag == null && q.RewardHero == null) p.Add($"{w}: needs a reward");
                if (q.RewardFlag != null && (q.RewardFlag.StartsWith(ContentLoader.NpcFlagPrefix) || q.RewardFlag.StartsWith("sp:"))) p.Add($"{w}: reward flag '{q.RewardFlag}' is reserved");
                if (!given.Contains(q.Id)) p.Add($"{w}: no dialogue offers it (needs a StartQuest effect)");
            }
        }

        /// <summary>The enemy classes of a family (their shared drop table, as <c>EncounterCatalog.Family</c> reads it).</summary>
        public static List<ClassDef> FamilyClasses(ContentDb db, string family)
        {
            var list = new List<ClassDef>();
            if (string.IsNullOrEmpty(family)) return list;
            foreach (var c in db.Classes.Values)
                if (c.Team == Team.Enemy && string.Equals(c.DropTable, family, StringComparison.OrdinalIgnoreCase)) list.Add(c);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>Does walking <paramref name="q"/>'s <see cref="QuestDef.Requires"/> links come back to a quest already seen?</summary>
        public static bool InCycle(ContentDb db, QuestDef q)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var at = q; at != null; at = db.TryQuest(at.Requires))
                if (!seen.Add(at.Id)) return true;
            return false;
        }

        /// <summary>The chain <paramref name="q"/> belongs to (GG-T434), first step to last: back along Requires to the
        /// first quest, then forward through the quest that requires each step (the first in file order, if several do).
        /// A quest outside any chain, or on a side branch, gives a list of just itself.</summary>
        public static List<QuestDef> Chain(ContentDb db, QuestDef q)
        {
            var chain = new List<QuestDef>();
            if (q == null) return chain;
            if (InCycle(db, q)) { chain.Add(q); return chain; }
            var root = q;
            while (db.TryQuest(root.Requires) is QuestDef prev) root = prev;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var at = root; at != null && seen.Add(at.Id);)
            {
                chain.Add(at);
                var cur = at;
                at = db.QuestOrder.Find(x => string.Equals(x.Requires, cur.Id, StringComparison.OrdinalIgnoreCase));
            }
            if (!chain.Contains(q)) { chain.Clear(); chain.Add(q); }
            return chain;
        }
    }
}
