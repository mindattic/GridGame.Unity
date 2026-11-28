using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The dialogue checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class DialogueValidator
    {
        internal static void Validate(ContentDb db, Func<string, bool> portraitExists, List<string> p)
        {
            foreach (var d in db.DialogueOrder) ValidateDialogue(db, d, portraitExists, p);
        }

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
                case DialogueTriggerKind.Trust:
                case DialogueTriggerKind.Cohesion:
                    // GG-T1298: a bond scene in the Talk row, opened by the hero's (or pair's) bond tier.
                    if (trigger.Kind == DialogueTriggerKind.Trust)
                    {
                        if (!IsHero(trigger.Id)) p.Add($"{w}: Trust needs a hero class Id, has '{trigger.Id}'");
                    }
                    else
                    {
                        // GG-T1623: either side may be a selector, making a generic scene any matching pair can play.
                        bool CohesionSide(string id, string side)
                        {
                            if (IsHero(id)) return true;
                            if (id == CohesionAny || id == CohesionSameFaction || id == CohesionOtherFaction) return false;
                            if (id != null && id.StartsWith(CohesionRolePrefix, StringComparison.Ordinal))
                            {
                                if (!HeroRoles.Contains(id.Substring(CohesionRolePrefix.Length))) p.Add($"{w}: Cohesion {side} names unknown Role '{id}'");
                            }
                            else if (id != null && id.StartsWith(CohesionFactionPrefix, StringComparison.Ordinal))
                            {
                                string f = id.Substring(CohesionFactionPrefix.Length);
                                if (!db.Factions.Exists(x => string.Equals(x.Id, f, StringComparison.OrdinalIgnoreCase))) p.Add($"{w}: Cohesion {side} names unknown faction '{id}'");
                            }
                            else p.Add($"{w}: Cohesion needs a hero class or a selector ({CohesionRolePrefix}<Role>, {CohesionFactionPrefix}<id>, {CohesionSameFaction}, {CohesionOtherFaction}, {CohesionAny}) {side}, has '{id}'");
                            return false;
                        }
                        if (CohesionSide(trigger.Id, "Id") & CohesionSide(trigger.Other, "Other") && string.Equals(trigger.Id, trigger.Other, StringComparison.OrdinalIgnoreCase))
                            p.Add($"{w}: Cohesion needs two different heroes, has '{trigger.Id}' twice");
                    }
                    if (trigger.Level < 1 || trigger.Level > MaxBondTier) p.Add($"{w}: {trigger.Kind} needs a tier Level of 1-{MaxBondTier}, has {trigger.Level}");
                    break;
                case DialogueTriggerKind.Founding:
                    // GG-T1318: once, after the opening crawl, with the starting party ("$party:N").
                    if (trigger.Id != null) p.Add($"{w}: Founding takes no Id, has '{trigger.Id}'");
                    break;
                case DialogueTriggerKind.Joined:
                    // GG-T1318: "$hero" is the hero who joined; the Id picks the scene like a Wanderer talk's.
                    if (trigger.Id != null && !IsHero(trigger.Id) && !db.Factions.Exists(f => string.Equals(f.Id, trigger.Id, StringComparison.OrdinalIgnoreCase)))
                        p.Add($"{w}: Joined needs a hero class, a faction or no Id, has '{trigger.Id}'");
                    break;
                case DialogueTriggerKind.Prologue:
                    // GG-T2066: a prologue onboarding step; a Drill names its drill.
                    if (Array.IndexOf(DialogueDef.PrologueSteps, trigger.Id) < 0) p.Add($"{w}: Prologue needs a step ({string.Join(", ", DialogueDef.PrologueSteps)}), has '{trigger.Id}'");
                    else if (trigger.Id == "Drill" ? trigger.Level < 1 || trigger.Level > DialogueDef.PrologueDrills : trigger.Level != 0)
                        p.Add($"{w}: Prologue {trigger.Id} needs {(trigger.Id == "Drill" ? $"a drill Level of 1-{DialogueDef.PrologueDrills}" : "no Level")}, has {trigger.Level}");
                    break;
            }
            bool bondScene = trigger.Kind == DialogueTriggerKind.Trust || trigger.Kind == DialogueTriggerKind.Cohesion;
            if (d.Crawl != null)
            {
                if (trigger.Kind == DialogueTriggerKind.Talk || trigger.Kind == DialogueTriggerKind.Recruit || bondScene) p.Add($"{w}: only a story beat can open with a Crawl");
                if (string.IsNullOrWhiteSpace(d.Crawl)) p.Add($"{w}: empty Crawl");
                if (string.IsNullOrWhiteSpace(d.Title)) p.Add($"{w}: a Crawl needs a Title to head it");
            }
            bool talkWithHero = (trigger.Kind == DialogueTriggerKind.Talk || trigger.Kind == DialogueTriggerKind.Recruit) && trigger.Id != null && !trigger.Id.StartsWith(NpcFlagPrefix)
                || trigger.Kind == DialogueTriggerKind.Wanderer || bondScene;
            bool wanderer = trigger.Kind == DialogueTriggerKind.Wanderer;
            bool bindsHero = wanderer || trigger.Kind == DialogueTriggerKind.Joined || IsGenericCohesion(d);
            bool joinsHero = false;
            foreach (var e in d.AllEffects()) joinsHero |= e?.Type == DialogueEffectType.JoinHero;

            // GG-T1293: a hero a bond effect or condition names: a hero class, or a speaker token this dialogue binds.
            bool BondHero(string id, string where, string what)
            {
                bool ok = id == "$with" ? talkWithHero : id == "$recruit" ? joinsHero : id == "$hero" ? bindsHero
                    : id == TrustedToken || PartySlot(id) > 0 || IsHero(id);
                if (!ok) p.Add($"{where}: {what} needs a hero class, \"$party:1\"-\"$party:{MaxPartySlots}\", \"$trusted\", or \"$with\" / \"$recruit\" / \"$hero\" where this dialogue binds it, has '{id}'");
                return ok;
            }
            void BondPair(string a, string b, string where, string what)
            {
                if (BondHero(a, where, what) & BondHero(b, where, what) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    p.Add($"{where}: {what} needs two different heroes, has '{a}' twice");
            }

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
                    case DialogueConditionType.TrustAtLeast:
                    case DialogueConditionType.CohesionAtLeast:
                        if (c.Type == DialogueConditionType.TrustAtLeast) BondHero(c.Id, where, "TrustAtLeast");
                        else BondPair(c.Id, c.Other, where, "CohesionAtLeast");
                        if (c.Amount <= 0 || c.Amount > 100) p.Add($"{where}: {c.Type} needs an Amount of 1..100, has {c.Amount}");
                        break;
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
                        else if (e.Id.StartsWith(PairSceneFlagPrefix)) p.Add($"{where}: flag '{e.Id}' is reserved — a generic Cohesion scene sets it");
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
                    case DialogueEffectType.AddTrust:
                    case DialogueEffectType.AddCohesion:
                        if (e.Type == DialogueEffectType.AddTrust) BondHero(e.Id, where, "AddTrust");
                        else BondPair(e.Id, e.Other, where, "AddCohesion");
                        if (e.Amount == 0 || Math.Abs(e.Amount) > DialogueBondMax) p.Add($"{where}: {e.Type} needs a non-zero Amount of -{DialogueBondMax}..{DialogueBondMax}, has {e.Amount}");
                        break;
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
                if (s.Class == "$lead" || s.Class == CommanderToken || s.Class == TrustedToken || PartySlot(s.Class) > 0) { }
                else if (s.Class != null && s.Class.StartsWith(PartyTokenPrefix, StringComparison.Ordinal)) p.Add($"{w} speaker {s.Id}: '{s.Class}' needs a party slot 1-{MaxPartySlots}");
                else if (s.Class == "$with") { if (!talkWithHero) p.Add($"{w} speaker {s.Id}: \"$with\" only fits a Talk with a party member"); }
                else if (s.Class == "$hero") { if (!bindsHero) p.Add($"{w} speaker {s.Id}: \"$hero\" only fits a Wanderer talk, a Joined scene or a generic Cohesion scene"); }
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
