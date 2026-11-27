using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>One spoken line as the screen shows it: who (resolved name + portrait) and what.</summary>
    public sealed class DialogueLine
    {
        public string NodeId;
        public string SpeakerId;
        public string Name;
        /// <summary>The class whose portrait shows (Art.Portrait resolves its file, GG-T557), or null for none.</summary>
        public string Portrait;
        public string Text;
        /// <summary>The Commander (the player, <see cref="GridGame.Meta.Commander"/>) speaks this line.</summary>
        public bool Commander;
        /// <summary>A choice the player picked to leave this line (null when it auto-advanced).</summary>
        public string Answer;
    }

    /// <summary>The standing in a wanderer's game of wits (GG-T387): where the talk started (set by trust and the companion),
    /// where it is now, and the mark it must reach. Each reply moves it by its <see cref="DialogueChoice.Wit"/>; the screen
    /// draws it as a meter.</summary>
    public sealed class WitsMeter
    {
        public int Start { get; }
        public int Threshold { get; }
        /// <summary>The meter's top (the best start plus every point won).</summary>
        public int Max { get; }
        public int Standing { get; private set; }
        /// <summary>Every reply's move so far (+1, 0, -1), in order.</summary>
        public List<int> Moves { get; } = new List<int>();
        public bool Clears => Standing >= Threshold;

        public WitsMeter(int start, int threshold, int max)
        {
            Start = Standing = start; Threshold = threshold; Max = Math.Max(max, threshold);
        }

        /// <summary>Scores a reply; returns the move (clamped to -1..+1).</summary>
        public int Score(int wit)
        {
            int move = Math.Max(-1, Math.Min(1, wit));
            Standing += move;
            Moves.Add(move);
            return move;
        }
    }

    /// <summary>
    /// Plays one <see cref="DialogueDef"/> against a save (§27.1, GG-T117). A data interpreter, not a VM: it walks
    /// nodes, offers the choices whose conditions hold, and applies effects in the save's own vocabulary.
    /// Engine-free so it's covered by dotnet tests; DialogueScreen only draws what this says.
    /// </summary>
    public sealed class DialogueRunner
    {
        private readonly ContentDb c;
        private readonly SaveData s;
        private readonly Dictionary<string, DialogueNode> nodes;

        public DialogueDef Def { get; }
        /// <summary>The party member a Talk dialogue is with ("$with"); null otherwise.</summary>
        public string With { get; }
        /// <summary>The wanderer a Wanderer talk is with ("$hero", GG-T342); null otherwise.</summary>
        public string Hero { get; }
        /// <summary>Resolves <see cref="DialogueDef.OutcomeNext"/> to the node to go to (GG-T342: the wanderer's
        /// understanding rule, set by <c>Wanderers.Begin</c>); without one the marker ends the dialogue.</summary>
        public Func<DialogueRunner, string> Outcome;
        /// <summary>A wanderer talk's game of wits (GG-T387, set by <c>Wanderers.Begin</c>): each chosen reply scores its
        /// <see cref="DialogueChoice.Wit"/> here before the talk moves on. Null for every other dialogue.</summary>
        public WitsMeter Wits;
        public DialogueNode Node { get; private set; }
        public bool Finished => Node == null;
        /// <summary>Every line reached so far, in order (the current one last).</summary>
        public List<DialogueLine> Transcript { get; } = new List<DialogueLine>();
        /// <summary>What each effect did, in order ("Sellsword joins you!", "+50 G") — the screen toasts them.</summary>
        public List<string> Log { get; } = new List<string>();
        /// <summary>GG-T1297: the bond changes among <see cref="Log"/>, keyed by their line's index there, so the screen
        /// shows them as bond chips rather than toasts.</summary>
        public Dictionary<int, BondChange> BondChanges { get; } = new Dictionary<int, BondChange>();

        /// <summary>One Trust (<see cref="B"/> null) or cohesion change that landed: who, by how much, and the tier it moved
        /// from and to.</summary>
        public sealed class BondChange
        {
            public string A, B;
            public int Moved, Value, TierBefore, TierAfter;
            public bool TierUp => TierAfter > TierBefore;
        }

        /// <summary>The hero this dialogue's first JoinHero brings ("$recruit"), fixed when the dialogue opens so
        /// the face that speaks is the hero who joins (GG-T118); null if it has none or nobody is left to join.</summary>
        public string Recruit => recruits.Values.FirstOrDefault(v => v != null);
        private readonly Dictionary<DialogueEffect, string> recruits = new Dictionary<DialogueEffect, string>();
        /// <summary>The party as it stood when the dialogue opened ("$party:N" is its Nth member, 1-based).</summary>
        private readonly List<string> party;
        /// <summary>The party hero who trusts the Commander most ("$trusted"), fixed when the dialogue opens; null if none
        /// has Trust.</summary>
        public string Trusted { get; }

        public DialogueRunner(ContentDb c, SaveData s, DialogueDef def, string with = null, string hero = null)
        {
            this.c = c; this.s = s; Def = def ?? throw new ArgumentNullException(nameof(def)); With = with; Hero = hero;
            party = new List<string>(s.Party);
            Trusted = TrustedHero(s);
            nodes = def.Nodes.Where(n => n?.Id != null).GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var promised = new List<string>();
            foreach (var e in def.AllEffects().Where(e => e?.Type == DialogueEffectType.JoinHero))
            {
                if (recruits.ContainsKey(e)) continue;
                var pick = Summon.StoryPick(c, s, e.Id, e.Role, promised);
                recruits[e] = pick;
                if (pick != null) promised.Add(pick);
            }
            Enter(def.Start ?? def.Nodes.FirstOrDefault()?.Id);
        }

        public DialogueLine Current => Finished ? null : Transcript[Transcript.Count - 1];

        /// <summary>The current node's choices the player may pick now (conditions met), with their indexes in the node.</summary>
        public List<DialogueChoice> Choices => Finished ? new List<DialogueChoice>() : Node.Choices.Where(ch => All(c, s, ch.Conditions, Who)).ToList();

        public bool AwaitingChoice => !Finished && Node.Choices.Count > 0;

        /// <summary>Moves past a line that has no choices. Returns false if a choice is needed or it's over.</summary>
        public bool Advance()
        {
            if (Finished || AwaitingChoice) return false;
            Enter(Node.Next);
            return true;
        }

        /// <summary>Picks one of <see cref="Choices"/> (an index into that filtered list): applies its effects, then moves on.</summary>
        public Result Choose(int index)
        {
            var offered = Choices;
            if (!AwaitingChoice || index < 0 || index >= offered.Count) return Result.Fail("No such choice");
            var ch = offered[index];
            Current.Answer = ch.Label;
            Wits?.Score(ch.Wit);
            string place = $"{Node.Id}:{Node.Choices.IndexOf(ch)}";
            foreach (var e in ch.Effects) Apply(e, place);
            Enter(ch.Next);
            return Result.Success();
        }

        private void Enter(string id)
        {
            if (string.Equals(id, DialogueDef.OutcomeNext, StringComparison.OrdinalIgnoreCase)) id = Outcome?.Invoke(this);
            if (id == null || !nodes.TryGetValue(id, out var n))
            {
                Node = null;
                s.SeenDialogues.Add(Def.Id);
                Codex.Sync(c, s); // GG-T929: the dialogue told, and whoever and whatever it brought
                return;
            }
            Node = n;
            var sp = Def.Speakers.FirstOrDefault(x => string.Equals(x.Id, n.Speaker, StringComparison.OrdinalIgnoreCase));
            var (name, portrait) = Resolve(sp);
            Transcript.Add(new DialogueLine { NodeId = n.Id, SpeakerId = n.Speaker, Name = name, Portrait = portrait, Text = Commander.Fill(n.Text, s), Commander = sp?.Class == Commander.Token });
            foreach (var e in n.Effects) Apply(e, n.Id);
        }

        /// <summary>A speaker's display name and portrait id, with "$lead"/"$with"/"$party:N"/"$trusted" bound to this
        /// save's heroes and "$commander" to the <see cref="Commander"/>.</summary>
        public (string name, string portrait) Resolve(DialogueSpeaker sp)
        {
            if (sp == null) return ("", null);
            if (sp.Class == Commander.Token) return (sp.Name ?? Commander.Name(s), sp.Portrait ?? Commander.Portrait);
            string cls = sp.Class == "$lead" ? s.Party.FirstOrDefault() : Who(sp.Class);
            var def = c.TryClass(cls);
            return (sp.Name ?? def?.Name ?? sp.Id, sp.Portrait ?? def?.Id);
        }

        /// <summary>The hero a bond effect or condition names: "$with", "$recruit", "$hero", "$party:N" and "$trusted" bound
        /// like speakers, else the class id as written.</summary>
        public string Who(string id)
        {
            if (id == "$with") return With;
            if (id == "$recruit") return Recruit;
            if (id == "$hero") return Hero;
            if (id == ContentLoader.TrustedToken) return Trusted;
            int slot = ContentLoader.PartySlot(id);
            if (slot > 0) return slot <= party.Count ? party[slot - 1] : null;
            return id;
        }

        /// <summary>The party hero with the highest Trust among those that have it, ties to the earlier party slot; null if
        /// none has Trust.</summary>
        public static string TrustedHero(SaveData s)
        {
            string best = null;
            int top = int.MinValue;
            foreach (var h in s.Party)
            {
                if (!Bonds.HasTrust(s, h)) continue;
                int t = Bonds.Trust(s, h);
                if (t > top) { top = t; best = h; }
            }
            return best;
        }

        /// <summary>False when <paramref name="def"/> names a "$party:N" or "$trusted" hero (as a speaker or in a bond effect
        /// or condition) this save can't fill, so the dialogue isn't offered.</summary>
        public static bool CanBind(SaveData s, DialogueDef def)
        {
            if (s == null || def == null) return true;
            bool trusted = false, haveTrusted = false;
            bool Fits(string id)
            {
                if (id == ContentLoader.TrustedToken)
                {
                    if (!haveTrusted) { trusted = TrustedHero(s) != null; haveTrusted = true; }
                    return trusted;
                }
                int slot = ContentLoader.PartySlot(id);
                return slot == 0 || slot <= s.Party.Count;
            }
            if (def.Speakers.Any(sp => sp != null && !Fits(sp.Class))) return false;
            foreach (var e in def.AllEffects())
                if ((e?.Type == DialogueEffectType.AddTrust || e?.Type == DialogueEffectType.AddCohesion) && (!Fits(e.Id) || !Fits(e.Other))) return false;
            foreach (var n in def.Nodes)
                if (n?.Choices != null)
                    foreach (var ch in n.Choices)
                        if (ch?.Conditions != null && ch.Conditions.Any(x => x != null && (!Fits(x.Id) || !Fits(x.Other)))) return false;
            return true;
        }

        /// <summary><paramref name="place"/> is where the effect sits ("&lt;node&gt;" or "&lt;node&gt;:&lt;choice&gt;"), which guards a bond change.</summary>
        private void Apply(DialogueEffect e, string place)
        {
            string msg;
            BondChange bond = null;
            if (e?.Type == DialogueEffectType.JoinHero && recruits.TryGetValue(e, out var who)) msg = JoinStoryHero(c, s, who);
            else if (e?.Type == DialogueEffectType.AddTrust || e?.Type == DialogueEffectType.AddCohesion) msg = ApplyBond(c, s, e, Def.Id, place, Who(e.Id), Who(e.Other), out bond);
            else msg = ApplyEffect(c, s, e, Def.Id, With);
            if (msg == null) return;
            if (bond != null) BondChanges[Log.Count] = bond;
            Log.Add(msg);
        }

        /// <summary>Plays a dialogue to the end with no screen, taking the first choice offered at every turn —
        /// how the bots (balance sims, Test Mode) live through story beats. Returns the effect log.</summary>
        public static List<string> PlayHeadless(ContentDb c, SaveData s, DialogueDef def, string with = null)
        {
            var r = new DialogueRunner(c, s, def, with);
            for (int guard = 0; !r.Finished && guard < 500; guard++)
                if (!r.Advance()) r.Choose(0);
            if (!r.Finished) s.SeenDialogues.Add(def.Id); // a loop no first choice escapes — count it as told
            Codex.Sync(c, s); // GG-T929
            return r.Log;
        }

        // ── the vocabulary ──────────────────────────────────────────────────────────

        /// <summary><paramref name="who"/> binds the hero tokens of TrustAtLeast / CohesionAtLeast (<see cref="Who"/>); without
        /// it a token names nobody and the condition fails.</summary>
        public static bool All(ContentDb c, SaveData s, IEnumerable<DialogueCondition> conds, Func<string, string> who = null) => conds == null || conds.All(x => Check(c, s, x, who));

        public static bool Check(ContentDb c, SaveData s, DialogueCondition x, Func<string, string> who = null)
        {
            if (x == null) return true;
            string Bound(string id) => who != null ? who(id) : id;
            switch (x.Type)
            {
                case DialogueConditionType.FlagSet: return s.Flags.Contains(x.Id);
                case DialogueConditionType.FlagNotSet: return !s.Flags.Contains(x.Id);
                case DialogueConditionType.HasItem: return s.Count(x.Id) >= Math.Max(1, x.Amount);
                case DialogueConditionType.GoldAtLeast: return s.Gold >= x.Amount;
                case DialogueConditionType.PartyHas: return s.Party.Any(p => string.Equals(p, x.Id, StringComparison.OrdinalIgnoreCase));
                case DialogueConditionType.RosterHas: return s.Hero(x.Id) != null;
                case DialogueConditionType.RosterLacks: return s.Hero(x.Id) == null;
                case DialogueConditionType.NpcJoined: return NpcJoined(s, x.Id);
                case DialogueConditionType.QuestAvailable: return Quests.Available(c, s, x.Id);
                case DialogueConditionType.QuestActive: return Quests.Active(s, x.Id);
                case DialogueConditionType.QuestDone: return Quests.Done(s, x.Id);
                case DialogueConditionType.TrustAtLeast:
                {
                    string a = Bound(x.Id);
                    return Bonds.HasTrust(s, a) && Bonds.Trust(s, a) >= x.Amount;
                }
                case DialogueConditionType.CohesionAtLeast:
                {
                    string a = Bound(x.Id), b = Bound(x.Other);
                    return Bonds.PairKey(a, b) != null && Bonds.HasCohesion(s, a) && Bonds.HasCohesion(s, b) && Bonds.Cohesion(s, a, b) >= x.Amount;
                }
                default: return false;
            }
        }

        public static bool NpcJoined(SaveData s, string npcId) => npcId != null && s.Flags.Contains(ContentLoader.NpcFlagPrefix + npcId);

        /// <summary>Applies one effect; returns a short player-facing note, or null when there's nothing to say.
        /// Nothing here removes a hero (owner rule: nobody dies permanently). <paramref name="dialogueId"/> and
        /// <paramref name="with"/> are the dialogue the effect belongs to and its Talk partner ("$with") — GrantSkillPoint
        /// needs them to know who to reward and to guard the reward once per dialogue per hero.</summary>
        public static string ApplyEffect(ContentDb c, SaveData s, DialogueEffect e, string dialogueId = null, string with = null)
        {
            if (e == null) return null;
            switch (e.Type)
            {
                case DialogueEffectType.SetFlag: s.Flags.Add(e.Id); return null;
                case DialogueEffectType.ClearFlag: if (!e.Id.StartsWith(ContentLoader.NpcFlagPrefix) && !e.Id.StartsWith(SkillPointFlagPrefix)) s.Flags.Remove(e.Id); return null;
                case DialogueEffectType.GrantSkillPoint: return GrantSkillPoint(c, s, e, dialogueId, with);
                case DialogueEffectType.GrantGold:
                {
                    int delta = e.Amount < 0 ? -Math.Min(s.Gold, -e.Amount) : e.Amount; // never below zero
                    s.Gold += delta;
                    if (delta > 0) s.LifetimeCoins += delta;
                    return delta == 0 ? null : (delta > 0 ? $"+{delta} G" : $"{delta} G");
                }
                case DialogueEffectType.GrantItem:
                {
                    int n = Math.Max(1, e.Amount);
                    s.AddItem(e.Id, n);
                    return $"Received {c.TryItem(e.Id)?.Name ?? e.Id}" + (n > 1 ? $" ×{n}" : "");
                }
                case DialogueEffectType.RecruitHero:
                {
                    if (c.TryClass(e.Id) == null) return null;
                    bool had = s.Hero(e.Id) != null;
                    Summon.Join(c, s, e.Id);
                    return had ? null : $"{c.Class(e.Id).Name} joins you!";
                }
                case DialogueEffectType.JoinNpc:
                    // Named bare, like every archetype (canon, 2026-11-21): "Blacksmith joins the caravan!".
                    return s.Flags.Add(ContentLoader.NpcFlagPrefix + e.Id) ? $"{Npc(e.Id)} joins the caravan!" : null;
                case DialogueEffectType.OpenStation:
                    return $"{CampUnlocks.StationName(e.Id)} opens at Camp!";
                case DialogueEffectType.JoinHero:
                    return JoinStoryHero(c, s, Summon.StoryPick(c, s, e.Id, e.Role));
                case DialogueEffectType.StartQuest:
                {
                    var r = Quests.Start(c, s, e.Id);
                    return r.Ok ? r.Message : null;
                }
                case DialogueEffectType.AddTrust:
                case DialogueEffectType.AddCohesion:
                    return ApplyBond(c, s, e, dialogueId, null, e.Id == "$with" ? with : e.Id, e.Other == "$with" ? with : e.Other);
                default: return null;
            }
        }

        /// <summary>Save-flag prefix guarding GrantSkillPoint ("sp:&lt;dialogue&gt;:&lt;hero&gt;"); ClearFlag can't touch it.</summary>
        public const string SkillPointFlagPrefix = "sp:";

        /// <summary>The flag that records <paramref name="heroId"/> already earned <paramref name="dialogueId"/>'s skill point.</summary>
        public static string SkillPointFlag(string dialogueId, string heroId) => $"{SkillPointFlagPrefix}{dialogueId ?? "-"}:{heroId}";

        /// <summary>GG-T133: a dialogue's skill point, once per dialogue per hero. The recipient is the effect's class
        /// id, or the Talk partner for "$with"/null; it must be in the roster. A replay that picks the rewarded answer
        /// again only says so — the guard flag is set the first time and never cleared (ClearFlag can't touch "sp:").</summary>
        private static string GrantSkillPoint(ContentDb c, SaveData s, DialogueEffect e, string dialogueId, string with)
        {
            string heroId = e.Id == null || e.Id == "$with" ? with : e.Id;
            var h = heroId == null ? null : s.Hero(heroId);
            if (h == null) return null;
            string name = c.TryClass(heroId)?.Name ?? heroId;
            if (!s.Flags.Add(SkillPointFlag(dialogueId, heroId))) return $"{name}'s Skill Point was already earned";
            int n = Math.Max(1, e.Amount);
            h.SkillPoints += n;
            return n == 1 ? $"{name} gains a Skill Point!" : $"{name} gains {n} Skill Points!";
        }

        /// <summary>GG-T1293: an AddTrust / AddCohesion effect for the heroes <paramref name="a"/> (and <paramref name="b"/>),
        /// already bound from their tokens. It pays once per save at <paramref name="place"/> of <paramref name="dialogueId"/>
        /// for these heroes (<see cref="Bonds.PayChoiceOnce"/>, a list ClearFlag can't reach and New Game+ keeps), so a replayed
        /// Talk can't farm it. A hero without Trust, or a pair without cohesion, is skipped without spending the guard.
        /// Returns the toast ("The Cleric's Trust +5"), or null when nothing moved.</summary>
        public static string ApplyBond(ContentDb c, SaveData s, DialogueEffect e, string dialogueId, string place, string a, string b = null) =>
            ApplyBond(c, s, e, dialogueId, place, a, b, out _);

        /// <summary>As above; <paramref name="change"/> is what moved (GG-T1297), or null when nothing did.</summary>
        public static string ApplyBond(ContentDb c, SaveData s, DialogueEffect e, string dialogueId, string place, string a, string b, out BondChange change)
        {
            change = null;
            if (e == null || e.Amount == 0) return null;
            string Name(string id) => c.TryClass(id)?.Name ?? id;
            if (e.Type == DialogueEffectType.AddTrust)
            {
                if (!Bonds.HasTrust(s, a) || !Bonds.PayChoiceOnce(s, dialogueId ?? "-", $"{place ?? "-"}:trust:{Bonds.Key(a)}")) return null;
                int before = Bonds.Trust(s, a), after = Bonds.AddTrust(s, a, e.Amount), moved = after - before;
                if (moved == 0) return null;
                change = new BondChange { A = a, Moved = moved, Value = after, TierBefore = Bonds.Tier(before), TierAfter = Bonds.Tier(after) };
                return $"The {Name(a)}'s Trust {moved:+0;-0}";
            }
            if (e.Type == DialogueEffectType.AddCohesion)
            {
                var pair = Bonds.PairKey(a, b);
                if (pair == null || !Bonds.HasCohesion(s, a) || !Bonds.HasCohesion(s, b)
                    || !Bonds.PayChoiceOnce(s, dialogueId ?? "-", $"{place ?? "-"}:cohesion:{pair}")) return null;
                int before = Bonds.Cohesion(s, a, b), after = Bonds.AddCohesion(s, a, b, e.Amount), moved = after - before;
                if (moved == 0) return null;
                change = new BondChange { A = a, B = b, Moved = moved, Value = after, TierBefore = Bonds.Tier(before), TierAfter = Bonds.Tier(after) };
                return $"The {Name(a)} and the {Name(b)}: cohesion {moved:+0;-0}";
            }
            return null;
        }

        /// <summary>A story recruit joins (GG-T118): free, at the roster's average experience, into the party if
        /// there's room (else the reserve) — exactly like a Summon Circle recruit.</summary>
        private static string JoinStoryHero(ContentDb c, SaveData s, string classId)
        {
            if (classId == null || c.TryClass(classId) == null || s.Hero(classId) != null) return null;
            Summon.Join(c, s, classId, JoinRoute.StoryRecruit);
            return s.Party.Contains(classId) ? $"{c.Class(classId).Name} joins the party!" : $"{c.Class(classId).Name} joins you (in reserve)!";
        }

        /// <summary>A Camp NPC's display name ("blacksmith" → "Blacksmith").</summary>
        public static string Npc(string id) => string.IsNullOrEmpty(id) ? id : char.ToUpperInvariant(id[0]) + id.Substring(1);

        // ── triggers ────────────────────────────────────────────────────────────────

        /// <summary>The story beat to play before launching <paramref name="stageId"/>, or null: the first dialogue
        /// (file order) not yet seen whose ThemeStart names the stage's theme, whose StageCleared stage is
        /// cleared, whose LevelReached level is reached or whose StageStart is this stage, and whose conditions hold. Test
        /// Mode saves never get story (GG-T93: bot-only).</summary>
        public static DialogueDef PendingBeat(ContentDb c, SaveData s, string stageId) =>
            s == null || s.TestMode ? null : NextBeat(c, s, stageId);

        /// <summary>For the bots (balance sims and Test Mode, which never show story): plays every story beat that's
        /// pending before <paramref name="stageId"/> headlessly (first choice each time) so story recruits still
        /// join (GG-T118). Returns what the beats did ("Red Mage joins the party!").</summary>
        public static List<string> PlayPendingBeatsHeadless(ContentDb c, SaveData s, string stageId)
        {
            var log = new List<string>();
            if (s == null) return log;
            for (var d = NextBeat(c, s, stageId); d != null && !s.SeenDialogues.Contains(d.Id); d = NextBeat(c, s, stageId))
                log.AddRange(PlayHeadless(c, s, d));
            return log;
        }

        private static DialogueDef NextBeat(ContentDb c, SaveData s, string stageId)
        {
            var theme = c.ThemeOf(stageId);
            foreach (var d in c.DialogueOrder)
            {
                if (d.Trigger == null || s.SeenDialogues.Contains(d.Id) || !All(c, s, d.Conditions) || !CanBind(s, d)) continue;
                bool fires = d.Trigger.Kind == DialogueTriggerKind.ThemeStart && theme != null && string.Equals(d.Trigger.Id, theme.Id, StringComparison.OrdinalIgnoreCase)
                    || d.Trigger.Kind == DialogueTriggerKind.StageCleared && s.ClearedStages.Contains(d.Trigger.Id)
                    || d.Trigger.Kind == DialogueTriggerKind.LevelReached && LevelReached(s, d.Trigger.Level)
                    || d.Trigger.Kind == DialogueTriggerKind.StageStart && string.Equals(d.Trigger.Id, stageId, StringComparison.OrdinalIgnoreCase);
                if (fires) return d;
            }
            return null;
        }

        /// <summary>True once any roster hero is at <paramref name="level"/> or above (GG-T231).</summary>
        public static bool LevelReached(SaveData s, int level) => level > 0 && s.Roster.Any(h => Progression.Level(h) >= level);

        /// <summary>GG-T231: the beat to play on the way back to Camp after a battle (it would otherwise wait for the next
        /// launch), or null: a LevelReached interlude, or (GG-T537) a vendor's join beat: a StageCleared beat that joins a
        /// Camp NPC or opens a station, once its stage is cleared, so the vendor arrives at Camp with the party. Never on a
        /// Test Mode save.</summary>
        public static DialogueDef PendingInterlude(ContentDb c, SaveData s)
        {
            if (s == null || s.TestMode) return null;
            return c.DialogueOrder.FirstOrDefault(d => d.Trigger != null && !s.SeenDialogues.Contains(d.Id) && All(c, s, d.Conditions) && CanBind(s, d)
                && (d.Trigger.Kind == DialogueTriggerKind.LevelReached && LevelReached(s, d.Trigger.Level) || VendorBeatDue(s, d)));
        }

        /// <summary>GG-T537: the first vendor join beat due on the way to Camp (its stage cleared, not yet played), or null.
        /// Never on a Test Mode save.</summary>
        public static DialogueDef PendingVendorBeat(ContentDb c, SaveData s) =>
            s == null || s.TestMode ? null : c.DialogueOrder.FirstOrDefault(d => !s.SeenDialogues.Contains(d.Id) && All(c, s, d.Conditions) && CanBind(s, d) && VendorBeatDue(s, d));

        private static bool VendorBeatDue(SaveData s, DialogueDef d) => IsVendorBeat(d) && s.ClearedStages.Contains(d.Trigger.Id);

        /// <summary>GG-T537: a Camp vendor's join beat: a StageCleared story beat with a JoinNpc or OpenStation effect.</summary>
        public static bool IsVendorBeat(DialogueDef d) => d?.Trigger?.Kind == DialogueTriggerKind.StageCleared
            && d.AllEffects().Any(e => e != null && (e.Type == DialogueEffectType.JoinNpc || e.Type == DialogueEffectType.OpenStation));

        /// <summary>Who Camp's Talk row offers: the party, then every joined NPC ("npc:&lt;id&gt;").</summary>
        public static List<string> TalkPartners(SaveData s)
        {
            var list = new List<string>(s.Party);
            foreach (var n in DialogueDef.Npcs) if (NpcJoined(s, n)) list.Add(ContentLoader.NpcFlagPrefix + n);
            return list;
        }

        /// <summary>The conversation to have with <paramref name="partner"/> (a party hero's class id or "npc:&lt;id&gt;"),
        /// or null: unseen before seen, a dialogue written for them before a generic "party" one, file order otherwise.</summary>
        public static DialogueDef TalkTopic(ContentDb c, SaveData s, string partner)
        {
            bool isNpc = partner != null && partner.StartsWith(ContentLoader.NpcFlagPrefix);
            var options = c.DialogueOrder.Where(d => d.Trigger?.Kind == DialogueTriggerKind.Talk
                    && (string.Equals(d.Trigger.Id, partner, StringComparison.OrdinalIgnoreCase) || !isNpc && d.Trigger.Id == "party")
                    && !(d.Once && s.SeenDialogues.Contains(d.Id))
                    && All(c, s, d.Conditions)
                    && CanBind(s, d))
                .Select((d, i) => (d, i))
                .OrderBy(x => s.SeenDialogues.Contains(x.d.Id) ? 1 : 0)
                .ThenBy(x => x.d.Trigger.Id == "party" ? 1 : 0)
                .ThenBy(x => x.i);
            return options.Select(x => x.d).FirstOrDefault();
        }

        /// <summary>GG-T1298 (§27.1): the bond scenes Camp's Talk row offers, in file order: each unseen Trust scene whose hero
        /// is in the party and each unseen Cohesion scene whose pair both are, its conditions holding ("$with" is the trigger
        /// Id) and its tokens bindable. A scene is <see cref="BondScene.Unlocked"/> once the hero's Trust tier (the pair's
        /// cohesion tier) has reached the trigger Level; each plays once, so a seen one drops out.</summary>
        public static List<BondScene> BondScenes(ContentDb c, SaveData s)
        {
            var list = new List<BondScene>();
            if (c == null || s == null) return list;
            bool InParty(string id) => id != null && s.Party.Any(p => string.Equals(p, id, StringComparison.OrdinalIgnoreCase));
            foreach (var d in c.DialogueOrder)
            {
                var t = d.Trigger;
                if (t == null || t.Kind != DialogueTriggerKind.Trust && t.Kind != DialogueTriggerKind.Cohesion) continue;
                if (s.SeenDialogues.Contains(d.Id) || !InParty(t.Id) || t.Kind == DialogueTriggerKind.Cohesion && !InParty(t.Other)) continue;
                if (!All(c, s, d.Conditions, id => id == "$with" ? t.Id : id) || !CanBind(s, d)) continue;
                int reached = t.Kind == DialogueTriggerKind.Trust ? Bonds.TrustTierReached(s, t.Id) : Bonds.CohesionTierReached(s, t.Id, t.Other);
                list.Add(new BondScene { Def = d, Unlocked = reached >= t.Level });
            }
            return list;
        }

        /// <summary>The count Camp's Talk row shows as new: partners with an unseen topic, plus unlocked bond scenes (GG-T1598).</summary>
        public static int NewTalkCount(ContentDb c, SaveData s) =>
            TalkPartners(s).Count(p => { var t = TalkTopic(c, s, p); return t != null && !s.SeenDialogues.Contains(t.Id); })
            + BondScenes(c, s).Count(b => b.Unlocked);
    }

    /// <summary>A Trust / Cohesion scene on offer in the Talk row (<see cref="DialogueRunner.BondScenes"/>).</summary>
    public sealed class BondScene
    {
        public DialogueDef Def;
        /// <summary>The bond tier its trigger needs has been reached, so it can be played.</summary>
        public bool Unlocked;
    }

    /// <summary>
    /// The Camp's people (§25.0.1, GG-T119): the Merchant, Blacksmith, Alchemist, Enchantress and Innkeeper are met on
    /// the journey and join at story beats (a dialogue <c>JoinNpc</c> effect sets the flag <c>npc:&lt;id&gt;</c>). Until
    /// one joins, their Camp service is hidden and the camp routine doesn't use it.
    /// </summary>
    public static class CampNpcs
    {
        public const string Merchant = "merchant";
        public const string Blacksmith = "blacksmith";
        public const string Alchemist = "alchemist";
        public const string Enchantress = "enchantress";
        public const string Innkeeper = "innkeeper";
        /// <summary>GG-T570: the Aetherist, a Choir defector who refines Aetherite Dust into Aetherite Shards at Camp (the
        /// retired hero class's lore and art, BIBLE §25.0.1). Joins after The Shardfall II.</summary>
        public const string Aetherist = "aetherist";

        public static IReadOnlyList<string> All => DialogueDef.Npcs;

        public static bool Joined(SaveData s, string npcId) => s != null && DialogueRunner.NpcJoined(s, npcId);

        /// <summary>Every NPC joins at once — a Test Mode save (it never plays story), the sweep's preview save, and
        /// the v4 migration of a save from before NPCs had to be met (they'd been using every service already).</summary>
        public static void JoinAll(SaveData s)
        {
            foreach (var n in DialogueDef.Npcs) s.Flags.Add(ContentLoader.NpcFlagPrefix + n);
        }
    }
}
