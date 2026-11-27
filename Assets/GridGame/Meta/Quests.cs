using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>Where a quest stands for this save (§22.11, GG-T237).</summary>
    public enum QuestState { Locked, Available, Active, Ready, Done }

    /// <summary>
    /// Optional side quests (§22.11, GG-T237): offered by Camp conversations (dialogue <c>StartQuest</c>), tracked in the
    /// journal, progressed by battles (<see cref="RecordBattle"/>, next to the bounty in <c>BattleFactory.Commit</c>) and
    /// handed in from Camp. Separate from the bounty board: any number may run at once and none ever gates the campaign.
    /// </summary>
    public static class Quests
    {
        /// <summary>Offered once its <see cref="QuestDef.Unlock"/> stage is cleared and, in a chain (GG-T434), the quest it
        /// <see cref="QuestDef.Requires"/> is done.</summary>
        public static bool Unlocked(SaveData s, QuestDef q) =>
            (q.Unlock == null || s.ClearedStages.Contains(q.Unlock)) && (q.Requires == null || s.Quests.Done.Contains(q.Requires));

        public static QuestState State(ContentDb c, SaveData s, QuestDef q)
        {
            if (s.Quests.Done.Contains(q.Id)) return QuestState.Done;
            if (s.Quests.Active.ContainsKey(q.Id)) return IsReady(c, s, q) ? QuestState.Ready : QuestState.Active;
            return Unlocked(s, q) ? QuestState.Available : QuestState.Locked;
        }

        public static bool Available(ContentDb c, SaveData s, string id) { var q = c.TryQuest(id); return q != null && State(c, s, q) == QuestState.Available; }
        public static bool Active(SaveData s, string id) => id != null && s.Quests.Active.ContainsKey(id);
        public static bool Done(SaveData s, string id) => id != null && s.Quests.Done.Contains(id);

        /// <summary>How far along it is, out of <see cref="Goal"/>. Bring, Flag, Recruit, ReachLevel and LearnSkill read the
        /// save live (a hero already recruited, levelled or taught counts the moment the quest is taken).</summary>
        public static int Progress(SaveData s, QuestDef q)
        {
            if (s.Quests.Done.Contains(q.Id)) return Goal(q);
            var o = q.Objective;
            switch (o.Kind)
            {
                case QuestObjectiveKind.Bring: return Math.Min(Goal(q), s.Count(o.Id));
                case QuestObjectiveKind.Flag: return s.Flags.Contains(o.Id) ? 1 : 0;
                case QuestObjectiveKind.Recruit: return s.Hero(o.Id) is HeroSave r && !r.Domesticated ? 1 : 0;
                case QuestObjectiveKind.ReachLevel:
                    return Math.Min(Goal(q), Heroes(s, o.Hero).Select(Progression.Level).DefaultIfEmpty(0).Max());
                case QuestObjectiveKind.LearnSkill:
                    return Heroes(s, o.Hero).Any(h => h.Known.Contains(o.Id, StringComparer.OrdinalIgnoreCase)) ? 1 : 0;
                default: return s.Quests.Active.TryGetValue(q.Id, out var n) ? Math.Min(Goal(q), n) : 0;
            }
        }

        /// <summary>The heroes an objective's <see cref="QuestObjective.Hero"/> means: that one, or every owned hero
        /// (never a domesticated monster).</summary>
        private static IEnumerable<HeroSave> Heroes(SaveData s, string hero) =>
            s.Roster.Where(h => h != null && !h.Domesticated && (hero == null || string.Equals(h.ClassId, hero, StringComparison.OrdinalIgnoreCase)));

        public static int Goal(QuestDef q)
        {
            switch (q.Objective.Kind)
            {
                case QuestObjectiveKind.Defeat:
                case QuestObjectiveKind.DefeatFamily:
                case QuestObjectiveKind.Bring:
                case QuestObjectiveKind.ReachLevel:
                    return Math.Max(1, q.Objective.Count);
                default: return 1;
            }
        }

        public static bool IsReady(ContentDb c, SaveData s, QuestDef q) => s.Quests.Active.ContainsKey(q.Id) && Progress(s, q) >= Goal(q);

        /// <summary>Quests ready to hand in — Camp's "!" marker.</summary>
        public static int ReadyCount(ContentDb c, SaveData s) => c.QuestOrder.Count(q => State(c, s, q) == QuestState.Ready);

        /// <summary>Quests whose offer the player has heard (the giver's conversation is seen) but didn't take up, so the
        /// journal can still accept them — a "Not now" in the conversation never loses a quest.</summary>
        public static List<QuestDef> Heard(ContentDb c, SaveData s) =>
            c.QuestOrder.Where(q => State(c, s, q) == QuestState.Available && c.DialogueOrder.Any(d => s.SeenDialogues.Contains(d.Id)
                && d.AllEffects().Any(e => e?.Type == DialogueEffectType.StartQuest && string.Equals(e.Id, q.Id, StringComparison.OrdinalIgnoreCase)))).ToList();

        public static Result Start(ContentDb c, SaveData s, string id)
        {
            var q = c.TryQuest(id);
            if (q == null) return Result.Fail("No such quest");
            var st = State(c, s, q);
            if (st == QuestState.Done) return Result.Fail("Already done");
            if (st == QuestState.Active || st == QuestState.Ready) return Result.Fail("Already under way");
            s.Quests.Active[q.Id] = 0;
            return Result.Success($"Quest: {q.Name}");
        }

        public static Result Abandon(ContentDb c, SaveData s, string id)
        {
            if (id == null || !s.Quests.Active.Remove(id)) return Result.Fail("Not under way");
            return Result.Success($"Set aside: {c.TryQuest(id)?.Name ?? id}");
        }

        /// <summary>Progress from one finished stage battle (victories only, like the bounty board). Returns a note per
        /// quest it moved ("Quest — Wolves at the Wells: 4/6") for the results screen.</summary>
        public static List<string> RecordBattle(ContentDb c, SaveData s, BattleState battle)
        {
            var notes = new List<string>();
            if (battle == null || battle.Phase != BattlePhase.Victory) return notes;
            var own = battle.Units.Where(u => u.IsHero && !u.IsGuest && !u.IsVisitor).ToList();
            foreach (var q in c.QuestOrder)
            {
                if (!s.Quests.Active.TryGetValue(q.Id, out var have) || IsReady(c, s, q)) continue;
                var o = q.Objective;
                int next = have;
                if (o.Kind == QuestObjectiveKind.Defeat && battle.Results.Kills.TryGetValue(o.Id, out var n)) next = Math.Min(Goal(q), have + n);
                else if (o.Kind == QuestObjectiveKind.DefeatFamily)
                {
                    int fell = battle.Results.Kills.Where(k => string.Equals(c.TryClass(k.Key)?.DropTable, o.Id, StringComparison.OrdinalIgnoreCase)).Sum(k => k.Value);
                    if (fell > 0) next = Math.Min(Goal(q), have + fell);
                }
                else if (o.Kind == QuestObjectiveKind.ClearStage && string.Equals(battle.Stage.Id, o.Id, StringComparison.OrdinalIgnoreCase)
                         && (o.MaxHeroes <= 0 || own.Count <= o.MaxHeroes) && (!o.NoneFallen || own.All(u => u.Alive)))
                    next = 1;
                if (next == have) continue;
                s.Quests.Active[q.Id] = next;
                notes.Add(next >= Goal(q) ? $"{q.Name}: ready to hand in!" : $"{q.Name}: {next}/{Goal(q)}");
            }
            return notes;
        }

        /// <summary>Hands a finished quest in: Bring items are given over, then gold, item, skill points (each active-party
        /// hero) and the follow-up flag are paid.</summary>
        public static Result TurnIn(ContentDb c, SaveData s, string id)
        {
            var q = c.TryQuest(id);
            if (q == null || !s.Quests.Active.ContainsKey(q.Id)) return Result.Fail("Not under way");
            if (!IsReady(c, s, q)) return Result.Fail("Not finished yet");
            if (q.Objective.Kind == QuestObjectiveKind.Bring) s.AddItem(q.Objective.Id, -Goal(q));
            var paid = new List<string>();
            if (q.RewardGold > 0) { s.Gold += q.RewardGold; s.LifetimeCoins += q.RewardGold; paid.Add($"{q.RewardGold}g"); }
            if (q.RewardItem != null)
            {
                int n = Math.Max(1, q.RewardItemCount);
                s.AddItem(q.RewardItem, n);
                paid.Add((c.TryItem(q.RewardItem)?.Name ?? q.RewardItem) + (n > 1 ? $" ×{n}" : ""));
            }
            if (q.RewardSkillPoints > 0)
            {
                foreach (var hid in s.Party) { var h = s.Hero(hid); if (h != null) h.SkillPoints += q.RewardSkillPoints; }
                paid.Add($"{q.RewardSkillPoints} SP each");
            }
            if (q.RewardTickets > 0) { SummonTickets.Earn(s, q.RewardTickets); paid.Add(TicketText.Of(q.RewardTickets)); } // GG-T1607
            if (q.RewardFlag != null) s.Flags.Add(q.RewardFlag);
            if (q.RewardHero != null && s.Hero(q.RewardHero) == null && c.TryClass(q.RewardHero) != null)
            {
                Summon.Join(c, s, q.RewardHero);
                paid.Add($"{c.Class(q.RewardHero).Name} joins you");
            }
            s.Quests.Active.Remove(q.Id);
            s.Quests.Done.Add(q.Id);
            return Result.Success($"{q.Name} complete!" + (paid.Count > 0 ? " " + string.Join(" + ", paid) : ""));
        }

        /// <summary>The objective in one line ("Defeat 6× Wolf", "Clear &lt;stage&gt; with 3 heroes or fewer").</summary>
        public static string Describe(ContentDb c, QuestDef q)
        {
            var o = q.Objective;
            switch (o.Kind)
            {
                case QuestObjectiveKind.Defeat: return $"Defeat {Goal(q)}× {c.TryClass(o.Id)?.Name ?? o.Id}";
                case QuestObjectiveKind.Bring: return $"Bring {Goal(q)}× {c.TryItem(o.Id)?.Name ?? o.Id}";
                case QuestObjectiveKind.Flag: return "See it through";
                case QuestObjectiveKind.DefeatFamily:
                {
                    var fam = QuestCatalog.FamilyClasses(c, o.Id);
                    string name = fam.Count > 0 ? fam[0].Name : o.Id;
                    return $"Defeat {Goal(q)}× {name} or kin";
                }
                case QuestObjectiveKind.Recruit: return $"Recruit {c.TryClass(o.Id)?.Name ?? o.Id}";
                case QuestObjectiveKind.ReachLevel: return $"{HeroName(c, o.Hero)} reaches Lv {Goal(q)}";
                case QuestObjectiveKind.LearnSkill: return $"{HeroName(c, o.Hero)} learns {c.TryAbility(o.Id)?.Name ?? o.Id}";
                default:
                {
                    var line = $"Clear {c.TryStage(o.Id)?.Name ?? o.Id}";
                    var terms = new List<string>();
                    if (o.MaxHeroes > 0) terms.Add($"with {o.MaxHeroes} hero{(o.MaxHeroes == 1 ? "" : "es")} or fewer");
                    if (o.NoneFallen) terms.Add("without anyone falling");
                    return terms.Count > 0 ? line + " " + string.Join(" and ", terms) : line;
                }
            }
        }

        /// <summary>The reward in one line ("300g + Hi-Potion + 1 SP each").</summary>
        public static string RewardText(ContentDb c, QuestDef q)
        {
            var parts = new List<string>();
            if (q.RewardGold > 0) parts.Add($"{q.RewardGold}g");
            if (q.RewardItem != null) parts.Add((c.TryItem(q.RewardItem)?.Name ?? q.RewardItem) + (q.RewardItemCount > 1 ? $" ×{q.RewardItemCount}" : ""));
            if (q.RewardSkillPoints > 0) parts.Add($"{q.RewardSkillPoints} SP each");
            if (q.RewardTickets > 0) parts.Add(TicketText.Of(q.RewardTickets));
            if (q.RewardHero != null) parts.Add($"{c.TryClass(q.RewardHero)?.Name ?? q.RewardHero} joins");
            if (q.RewardFlag != null) parts.Add("a new conversation");
            return string.Join(" + ", parts);
        }

        private static string HeroName(ContentDb c, string hero) => hero == null ? "Any hero" : c.TryClass(hero)?.Name ?? hero;

        /// <summary>The chain a quest belongs to (GG-T434), or null for a lone quest: "Part 2 of 3: Iron for the Forge
        /// (done) › Plate from the Wrecks › Quenched in Frost". A step not offered yet shows as "?".</summary>
        public static string ChainLine(ContentDb c, SaveData s, QuestDef q)
        {
            var chain = QuestCatalog.Chain(c, q);
            if (chain.Count < 2) return null;
            var steps = chain.Select(x =>
            {
                var st = State(c, s, x);
                return st == QuestState.Done ? $"{x.Name} (done)" : st == QuestState.Locked && x != q ? "?" : x.Name;
            });
            return $"Part {chain.IndexOf(q) + 1} of {chain.Count}: " + string.Join(" › ", steps);
        }
    }
}
