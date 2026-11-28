using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The guided first battle (US-331, expanded 2026-10-11 — owner: "there needs to be a full tutorial with
    /// explanations and examples, the first battle needs 5 waves of increasing difficulty"). Each lesson opens with a
    /// blocking popup and then coach marks (GG-T539, owner 2026-11-21: "it shows the user what to do with highlights and
    /// arrows and dotted lines ... wait for the user to click that exact area"): the stage dims except the one hero, tile,
    /// slot or button to use, a move's route is a dotted line the drag is held to, and the lesson completes when the player
    /// does exactly that. A lesson whose situation isn't on the board yet (no chain to make, no foe charging) keeps its
    /// popup text in the description panel and puts its marks up the moment the situation arrives, or completes the old way
    /// (the player does it, a few actions pass, or Next). Lessons that need particular foes wait for their wave.
    /// Under automation (no `-coach`) the marks are off and the description panel teaches alone.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private sealed class Lesson
        {
            /// <summary>The lesson image (Resources/Tutorial/&lt;Id&gt;.png) and its save flag.</summary>
            public string Id;
            public string Title, Text, Example;
            /// <summary>GG-T788 (§2.13.1): the lesson's text when the party's turn is one hero's one action
            /// (<see cref="TurnRules.OneHeroPerTurn"/>), for a lesson whose <see cref="Text"/> assumes every hero acts each turn.</summary>
            public string TextOneHero;
            /// <summary>GG-T1095 (§2.13.2): the lesson's text when the battle keeps rounds (<see cref="TurnRules.HasRounds"/>),
            /// for a lesson whose <see cref="TextOneHero"/> lets the same hero act again; wins over <see cref="TextOneHero"/>.</summary>
            public string TextRound;
            /// <summary>GG-T815 (§2.13.3): the lesson's text when heroes ride the timeline on their own icons
            /// (<see cref="TurnRules.HeroesOnTimeline"/>); wins over <see cref="TextRound"/> and <see cref="TextOneHero"/>.</summary>
            public string TextHeroTimeline;
            /// <summary>GG-T815: the popup's lines under HeroTimeline, for a lesson whose <see cref="Short"/> would mislead there.</summary>
            public string ShortHeroTimeline;
            /// <summary>The popup's one or two lines (GG-T539); the description panel keeps the longer <see cref="Text"/>.</summary>
            public string Short;
            /// <summary>The wave (0-based) the lesson needs on the board: the drill's (<see cref="StageLessonDef.Wave"/>).</summary>
            public int Wave;
            /// <summary>Show the bot's move as a glowing demo while idle (the description panel's mode, without coach marks).</summary>
            public bool DemoMove;
            /// <summary>Done when this returns true for a committed command's events.</summary>
            public Func<BattleScreen, BattleCommand, List<BattleEvent>, bool> Done;
            /// <summary>Highlight on show: the party's first hero of the drill's role (<see cref="StageLessonDef.FocusRole"/>;
            /// GG-T1405: no fixed lead to point at) and its bar slot to pulse.</summary>
            public string FocusRole; public int FocusSlot = -1;
            /// <summary>Actions after which the lesson moves on by itself.</summary>
            public int Patience = 5;
            /// <summary>Done the moment the player holds something to read it (GG-T252).</summary>
            public bool DoneOnInspect;
            /// <summary>GG-T539: the coach marks for this board, or null/empty when its situation isn't here yet.</summary>
            public Func<BattleScreen, List<CoachStep>> Marks;
            /// <summary>Without marks, the lesson stays (in the description panel) until done instead of ending on the popup's Next.</summary>
            public bool Waits;
            /// <summary>GG-T542: the tutorial battle sets this lesson's situation up (<see cref="TutorialSceneCommand"/>) when its
            /// marks find nothing to point at: a chain to close, a foe charging, a plated foe, a full spell bar.</summary>
            public string Scene;
            /// <summary>Stage the scene as the lesson opens even when marks exist (Taunt: a foe closes on the Cleric).</summary>
            public bool SceneFirst;

            /// <summary>GG-T1377: this catalogue lesson as a drill places it (its wave and focus role).</summary>
            public Lesson For(StageLessonDef d)
            {
                var l = (Lesson)MemberwiseClone();
                l.Wave = d.Wave;
                l.FocusRole = d.FocusRole;
                return l;
            }
        }

        /// <summary>GG-T1377: the battle lessons by id, in their first-battle order; a drill's stage picks and places them.</summary>
        private static readonly Lesson[] AllLessons =
        {
            new Lesson { Id = "move", Title = "Move us", DemoMove = true, Waits = true,
                Short = "Commander, drag any of us tile by tile. Whoever stands in the way swaps into the tile we leave, and moving never hurts.",
                Text = "Commander, send us where you want us: drag one of us across the board, tile by tile. Anyone in the way swaps into the tile we just left — moving never hurts anyone.",
                Example = "Example: follow the glowing path to the glowing tile.",
                Done = (b, c, e) => c is MoveCommand,
                Marks = b => b.MoveMarks(b.BotMove(), null) },
            new Lesson { Id = "pincer", Title = "Pincer it", DemoMove = true, Waits = true, Scene = "pincer",
                Short = "Commander, put a foe in a straight line between two of us and we PINCER it. Both of us strike.",
                Text = "Commander, put an enemy in a straight line between two of us and we PINCER it — both of us strike. Any of us linked to an attacker by an unbroken chain of allies joins in as a supporter, each with our own support action.",
                Example = "Example: Hero · Slime · Hero in a row or column.",
                Done = (b, c, e) => e.Exists(x => x is PincerFormedEvent p && p.Team == Team.Hero),
                Marks = b => b.MoveMarks(b.FindMove(pv => pv.Pincers.Exists(p => p.A.IsHero)), "Drag the {0} along the dotted line: the drop traps the foe between two heroes.") },
            // GG-T252: recognition over recall — anything on screen explains itself. GG-T450: a tap reads it too.
            new Lesson { Id = "read", Title = "Read the field", Patience = 3, DoneOnInspect = true,
                Short = "Commander, size them up first: tap any of us, a foe, a timeline icon or a spell slot to read its card. Holding reads it too.",
                Text = "Commander, size up the field before you order us: tap one of us, an enemy or an icon on the timeline to read its card; tap a spell slot that can't be used yet to see why. The card stays until your next tap. Holding anything — a spell slot or status pip too — reads it without acting.",
                Example = "Example: tap a slime to open its card.",
                Done = (b, c, e) => false,
                Marks = b => b.ReadMarks() },
            // GG-T190: shoving is a tactic, not an accident — the held hero moves other heroes and enemies into place.
            new Lesson { Id = "shove", Title = "Shove them into line", Patience = 4, Waits = true, Scene = "shove",
                Short = "Commander, drag one of us through a foe and it slides into the tile we left. Shove it between two of us and we pincer it.",
                Text = "Commander, use whoever you hold as a lever. Drag us through an enemy and the enemy slides into the tile we just left — so shove a foe into line between two of us and the drop pincers it. While you drag, arrows show where everyone will slide and gold outlines show the pincers the drop completes.",
                Example = "Example: a slime sits above the gap beside us. Step into the gap, then up through the slime: it drops into the gap, between two of us.",
                Done = (b, c, e) => ShovedIntoPincer(e),
                Marks = b => b.MoveMarks(b.FindMove(pv => ShovePincers(b.State, b.findHero, pv)), "Drag the {0} through the foe along the dotted line: it slides into line and the drop pincers it.") },
            new Lesson { Id = "hold", Title = "Take your time", Patience = 3, Waits = true,
                Short = "Commander, lifting one of us starts the clock. Changed your mind? Drop us off the board, or back on our own tile, to cancel for free.",
                Text = "Commander, the moment you lift one of us the clock starts: the bar under that hero drains until the next enemy reaches the left edge, then we drop where we stand. Each of us acts once before that enemy does, sharing the time — what one of us leaves, the next can use, and pushing the enemy back buys more. Changed your mind? Drop us back on our own tile, or off the board, to cancel — no time spent.",
                TextOneHero = "Commander, the moment you lift one of us the clock starts: the bar under that hero drains until the next enemy reaches the left edge, then we drop where we stand. Each turn you send one of us — any of us, the same one again if you like — and that action is the party's whole turn; pushing the enemy back buys more time. Changed your mind? Drop us back on our own tile, or off the board, to cancel — no time spent.",
                TextRound = "Commander, the moment you lift one of us the clock starts: the bar under that hero drains until the next enemy reaches the left edge, then we drop where we stand. Each turn you send one of us who hasn't moved this round, and that action is the party's whole turn; once all of us have moved, a new round frees us all. Pushing the enemy back buys more time. Changed your mind? Drop us back on our own tile, or off the board, to cancel — no time spent.",
                TextHeroTimeline = "Commander, each of us has our own icon on the timeline, and only the one whose icon reached the left edge can act: that one action is its whole turn. Lifting that hero starts the clock: the bar under it drains until the next icon, a foe's or another hero's, reaches the left edge, then it drops where it stands. Pushing a foe back buys more time. Changed your mind? Drop it back on its own tile, or off the board, to cancel — no time spent.",
                Example = "Example: lift one of us, then drag off the board and let go.",
                Done = (b, c, e) => c is MoveCommand,
                Marks = b => b.CancelMarks() },
            // GG-T731: what mana is, before Mana teaches how to earn it (each spell's own fill bar, §3.1.0, GG-T1821).
            new Lesson { Id = "orbs", Title = "Spell mana", Patience = 2,
                Short = "Commander, each of our spells has its own bar. Mana fills it as we fight; a full bar glows, ready to cast.",
                Text = "Commander, every spell on our bar has its own mana bar. As we earn mana, every spell's bar fills by the same amount, so a cheap spell fills sooner than a costly one. A full bar glows: that spell is ready to cast, and casting it empties only its own bar.",
                Example = "Example: watch the spell bars fill as our pincers land.",
                Done = (b, c, e) => e.Exists(x => x is ManaGainedEvent g && g.Amount > 0),
                Marks = b => b.OrbMarks() },
            new Lesson { Id = "mana", Title = "Earn mana",
                Short = "Commander, our pincers fill every spell's bar with mana.",
                Text = "Commander, our pincers pour mana into every spell's bar. Lean pincers earn the most: every extra one of us divides the mana, every chain link multiplies it.",
                Example = "Example: two of us taking half a foe's HP earn 60 mana for every spell.",
                Done = (b, c, e) => e.Exists(x => x is ManaGainedEvent m && m.Kind == ManaGainKind.Earned && m.Reason == "pincer"), Patience = 3,
                Marks = b => b.ManaMarks() },
            new Lesson { Id = "chain", Title = "Chain them", DemoMove = true, Waits = true, Scene = "chain",
                Short = "Commander, one order can set off several pincers at once: a CHAIN. Every link strikes and earns more mana.",
                Text = "Commander, one drag can set off several pincers in a row — a CHAIN. Every link strikes, and every link earns more mana than the last.",
                Example = "Example: line both foes up between us, then drag the last of us in.",
                Done = (b, c, e) => e.Exists(x => x is PincerFormedEvent p && p.Team == Team.Hero && p.ChainIndex >= 1),
                Marks = b => b.MoveMarks(b.FindMove(pv => pv.Pincers.Count(p => p.A.IsHero) >= 2), "Drag the {0} along the dotted line: the drop closes two pincers at once.") },
            new Lesson { Id = "support", Title = "Bring support", Waits = true, Patience = 4, Scene = "support",
                Short = "Commander, whoever of us touches an attacker joins the pincer as a supporter, linked in blue, adds a support action and earns a share of the combo.",
                Text = "Commander, any of us linked to a pincer's attacker by an unbroken line of allies joins in as a supporter: the blue links in the drag preview. Each adds a support action — the Healer mends, a Striker adds a blow — and its own share of the combo, counted on the results screen.",
                Example = "Example: pincer with a third one of us standing next to an attacker.",
                Done = (b, c, e) => e.Exists(x => x is PincerFormedEvent p && p.Team == Team.Hero && p.Supporters.Count > 0),
                Marks = b => b.MoveMarks(b.FindMove(pv => pv.Pincers.Exists(p => p.A.IsHero && p.SupportersA.Count + p.SupportersB.Count > 0)), "Drag the {0} along the dotted line: the third hero joins in as a supporter.") },
            new Lesson { Id = "spell", Title = "Call a spell", FocusSlot = 0, Waits = true, Scene = "spell",
                Short = "Commander, tap one of us, a lit spell, a target, then Cast. A spell charges on the timeline, then lands.",
                Text = "Commander, to call a spell tap one of us, a lit spell, then a target, then Cast. A spell charges first — its bar fills as time moves — then it flies and lands.",
                Example = "Example: the Healer → Heal → a hurt ally → Cast.",
                Done = (b, c, e) => e.Exists(x => x is CastQueuedEvent q && !q.IsCharge),
                Marks = b => b.SlotMarks(a => a.Kind == AbilityKind.Spell, preferHeal: true) },
            new Lesson { Id = "buffs", Title = "Buffs & debuffs", Patience = 3, Waits = true, Scene = "buffs",
                Short = "Commander, coloured pips on a token are its buffs (blue) and debuffs (purple). Tap it to read them.",
                Text = "Commander, watch the coloured pips on a token: its buffs (blue) and debuffs (purple), like poison, haste, shields, slows. Hold one of us or a foe, or a pip in the panel, to read exactly what each does and how long it lasts.",
                Example = "Example: hold one of us after the Healer's spell lands.",
                Done = (b, c, e) => e.Exists(x => x is BuffAppliedEvent),
                Marks = b => b.BuffMarks() },
            new Lesson { Id = "timeline", Title = "Watch the timeline", Patience = 3,
                Short = "Commander, time moves only when you give an order. Icons flow left, and whoever reaches the left edge acts. Speed decides who gets there first.",
                TextHeroTimeline = "Commander, time moves only when you give an order. Each of us and every foe has an icon that flows left; whoever reaches the left edge acts, and one of our icons arriving makes it that hero's turn. Speed sets where each icon enters. Spells ride the bar from their cast time. Pincers and Pushback skills shove a foe back anywhere on the bar. GUARD gives up the acting hero's turn, shielded.",
                Text = "Commander, time moves only when you give an order. Icons flow left; whoever reaches the left edge acts. The big diamond on the left shows who is next, ringed gold on the bar and on the board, and a foe glows (icon and tile alike) in its last 3 seconds. Spells ride the bar from their cast time. Pincers and Pushback skills shove a foe back anywhere on the bar. GUARD skips ahead with all of us shielded.",
                Example = "Example: pincer the gold-ringed foe before it reaches the left edge.",
                Done = (b, c, e) => e.Exists(x => x is PushedBackEvent),
                Marks = b => b.TimelineMarks() },
            new Lesson { Id = "taunt", Title = "Roles & Taunt", FocusSlot = 0, Waits = true, Scene = "taunt", SceneFirst = true,
                Short = "Commander, each of us has a role. Order the Tank to Taunt and every foe in reach comes for it, so our casters can finish their spells.",
                Text = "Commander, each of us has a role. The Tank taunts: Taunt makes every enemy in reach come for it, so our casters can finish their spells. Strikers hit hardest, the Healer mends.",
                Example = "Example: the Tank → Taunt → Go.",
                Done = (b, c, e) => e.Exists(x => x is BuffAppliedEvent a && a.BuffId == "taunting"),
                Marks = b => b.SlotMarks(a => (a.Id ?? "").Contains("taunt")) },
            new Lesson { Id = "guard", Title = "Guard us", Patience = 2,
                Short = "Commander, GUARD gives up our turn: time skips to the next enemy with all of us shielded. Call it when there's no good move or no time. Time we don't use is banked as mana in every spell's bar.",
                Text = "Commander, GUARD gives up what is left of our turn: the clock skips to the next enemy turn with all of us shielded. Call it when there is no good move, or when the gap before the next enemy is too short for any action. The turn time we leave unused is banked as mana in every spell's bar.",
                ShortHeroTimeline = "Commander, GUARD gives up the acting hero's turn: it is shielded and the clock runs on to the next hero's icon. Call it when there's no good move or no time. Time we don't use is banked as mana in every spell's bar.",
                TextHeroTimeline = "Commander, GUARD gives up the acting hero's turn: that hero is shielded and the clock runs on to the next hero's icon, foes acting on the way. Call it when there is no good move, or when the gap before the next icon is too short for any action.",
                Example = "Example: tap GUARD at the top right.",
                Done = (b, c, e) => c is ShieldCommand,
                Marks = b => b.GuardMarks() },
            new Lesson { Id = "pickup", Title = "Grab pickups", Waits = true, Scene = "pickup",
                Short = "Commander, fallen foes sometimes drop a pickup. Walk one of us over it before its ring runs out.",
                Text = "Commander, fallen enemies sometimes drop a pickup: a tonic, a mana shard, quicksilver or a might rune. Walk one of us over it before its ring runs out.",
                Example = "Example: drag any of us onto the glowing token.",
                Done = (b, c, e) => e.Exists(x => x is PickupCollectedEvent),
                Marks = b => b.MoveMarks(b.FindMove(pv => b.State.Pickups.Exists(k => pv.Positions.TryGetValue(b.findHero, out var at) && at == k.At)), "Drag the {0} along the dotted line onto the pickup.") },
            new Lesson { Id = "interrupt", Title = "Break their casts", Waits = true, Scene = "interrupt",
                Short = "Commander, a charging foe wears a ring and marks its target. Have us pincer it to stagger the cast; enough blows cancel it.",
                Text = "Commander, a charging enemy wears a ring and marks its target. Have us hit it with a pincer to stagger the cast — enough blows cancel it and earn mana.",
                Example = "Example: pincer the wolf while it howls.",
                Done = (b, c, e) => e.Exists(x => x is CastStaggeredEvent || (x is CastCancelledEvent cc && cc.Reason == "interrupted")),
                Marks = b => b.MoveMarks(b.FindMove(pv => pv.Pincers.Exists(p => p.A.IsHero && p.Opponents.Exists(o => b.State.CastEntryOf(o.Id) != null))), "Drag the {0} along the dotted line to pincer the charging foe.") },
            new Lesson { Id = "plate", Title = "Mind the plate", Patience = 2, Waits = true, Scene = "plate",
                Short = "Commander, a thick edge on a foe is an armor plate. Send us at its sides, break it head-on, or use spells, which ignore it.",
                Text = "Commander, some foes wear an armor plate on one side, drawn as a thick edge. Our blows into the plate chip it instead of the foe: send us at its sides, break it head-on, or use spells, which ignore it.",
                Example = "Example: hold an armored foe later on to see its plate and which way it faces.",
                Done = (b, c, e) => e.Exists(x => x is PlateBrokenEvent),
                Marks = b => b.PlateMarks() },
            new Lesson { Id = "ready", Title = "Ready, Commander", Patience = 99,
                Short = "That's all we know, Commander: move us, pincer, chain for mana, cast with care, let the Tank take the hits. Lead us through the last wave!",
                Text = "That's all we know, Commander: move us, pincer, chain for mana, cast with care, let the Tank take the hits. Lead us through the last wave!",
                Example = "Tip: Settings → How to play has all of this, and can replay the lessons.",
                Done = (b, c, e) => false,
                Marks = b => b.ReadyMarks() },
        };

        /// <summary>GG-T190: did this command's drag shove an enemy into a line that its drop then pincered?</summary>
        private static bool ShovedIntoPincer(List<BattleEvent> events)
        {
            var shoved = new HashSet<int>();
            foreach (var x in events)
            {
                if (x is DisplacedEvent d) shoved.Add(d.UnitId);
                else if (x is PincerFormedEvent p && p.Team == Team.Hero && p.Opponents.Exists(shoved.Contains)) return true;
                else if (x is TimelineAdvancedEvent) return false; // the move is over; later slides are enemy turns
            }
            return false;
        }

        /// <summary>GG-T1377: this drill's lessons, in its order; empty when the battle teaches none.</summary>
        private Lesson[] Lessons = new Lesson[0];

        /// <summary>GG-T1377: the lessons <paramref name="stage"/> (a drill) lists, each placed on its wave and focus role.</summary>
        private static Lesson[] DrillLessons(StageDef stage) => (stage?.Lessons ?? new List<StageLessonDef>())
            .Select(d => AllLessons.FirstOrDefault(l => l.Id == d.Id)?.For(d)).Where(l => l != null).ToArray();

        private static bool DrillLessonsSeen(SaveData s, StageDef stage) => stage?.Lessons == null || stage.Lessons.All(l => Coach.Seen(s, "battle-" + l.Id));

        /// <summary>GG-T1377: "Drill n of 5": this stage's place among campaign.json's Tutorial drills.</summary>
        private string DrillLabel()
        {
            var drills = App.Content.Campaign?.Tutorial ?? new List<string>();
            int n = drills.FindIndex(id => string.Equals(id, State.Stage?.Id, StringComparison.OrdinalIgnoreCase));
            return n < 0 ? "" : $"Drill {n + 1} of {drills.Count} · ";
        }

        public int TutorialStep { get; private set; } = -1;
        private int tutorialActions;
        private bool TutorialActive => TutorialStep >= 0;
        private Lesson CurrentLesson => TutorialActive ? Lessons[TutorialStep] : null;
        private bool LessonWaiting => CurrentLesson != null && State.Wave < CurrentLesson.Wave;
        private bool TutorialDemoMove => CurrentLesson != null && CurrentLesson.DemoMove && !LessonWaiting && !CoachOn;

        /// <summary>GG-T1377: this drill's lessons count as seen; the tutorial is done once every drill's lessons are.</summary>
        private void FinishTutorial()
        {
            TutorialStep = -1;
            if (Coach.Active && (Coach.LessonId ?? "").StartsWith("battle-")) Coach.Cancel();
            if (App.Save != null && !practice)
            {
                foreach (var l in Lessons) Coach.MarkSeen(App.Save, "battle-" + l.Id);
                if ((App.Content.Campaign?.Tutorial ?? new List<string>()).All(id => DrillLessonsSeen(App.Save, App.Content.TryStage(id))))
                    App.Save.TutorialDone = true;
                App.Persist();
            }
            SyncPanel();
        }

        /// <summary>GG-T1398: Skip tutorial (confirmed, lessons already off) in a drill leaves it for Camp's first visit;
        /// the drills count as skipped and the battle is never committed. A practice drill only stops its lessons.</summary>
        private void SkipDrills()
        {
            FinishTutorial();
            if (practice || App.Save == null || !Drills.IsDrill(State?.Stage)) return;
            Drills.Skip(App.Save);
            App.Persist();
            App.Audio.Sfx("ui.cancel");
            App.Router.ResetTo(new HubScreen());
        }

        private void AdvanceTutorial(int to)
        {
            if (!TutorialActive || to <= TutorialStep) return;
            if (to >= Lessons.Length) { FinishTutorial(); return; }
            TutorialStep = to;
            tutorialActions = 0;
            hintShown = false; idle = 0f;
            App.Audio.Sfx("ui.select", 0.6f);
            panelUnitId = -1;
            readSlot = -1;
            SyncPanel();
            FocusLesson();
        }

        private void NextLesson() => AdvanceTutorial(TutorialStep + 1);

        /// <summary>GG-T788 (§2.13.1): a lesson's full text for this battle's turn mode, as <see cref="TurnRules"/> says it is.
        /// GG-T1095 (§2.13.2): under rounds, <see cref="Lesson.TextRound"/> first. GG-T815 (§2.13.3): under HeroTimeline,
        /// <see cref="Lesson.TextHeroTimeline"/> before either (the other lessons' marks already go through MayAct/CanMove,
        /// which only the active hero passes, so a lesson simply waits for a hero whose turn it is).</summary>
        private string LessonText(Lesson l)
        {
            if (l.TextHeroTimeline != null && TurnRules.HeroesOnTimeline(State)) return l.TextHeroTimeline; // GG-T815 (§2.13.3)
            if (l.TextRound != null && TurnRules.HasRounds(State)) return l.TextRound;
            return l.TextOneHero != null && TurnRules.OneHeroPerTurn(State) ? l.TextOneHero : l.Text;
        }

        private void TutorialAfterCommit(BattleCommand cmd, List<BattleEvent> events)
        {
            commitCount++;
            if (cmd is TutorialSceneCommand) return; // a lesson's set-up is nobody's action
            if (Coach.InSteps) coachCommitted = true;
            if (events.Exists(x => x is ReinforcementsArrivedEvent)) reinforcementsArrived = events.OfType<ReinforcementsArrivedEvent>().SelectMany(r => r.Spawned).ToList();
            if (!TutorialActive) { NoteComboRank(events); return; }
            var l = CurrentLesson;
            if (LessonWaiting) return;
            // With coach marks, a lesson moves on when its marks are done; the old completion (the player did it, a few
            // actions passed) runs only once its popup has shown and it has no marks to put up.
            // Autoplay doesn't move lessons on either: they wait for the player to take the battle back.
            if (CoachAvailable && (AutoPlay || Coach.Active || coachPopupFor != TutorialStep)) return;
            tutorialActions++;
            if (l.Done(this, cmd, events) || tutorialActions >= l.Patience) NextLesson();
            if (CoachAvailable) return; // each lesson gets its own popup, even one the player already did
            // A lesson the player already did earlier (a chain in wave 1 …) is skipped as soon as it comes up.
            while (TutorialActive && !LessonWaiting && CurrentLesson.Done(this, cmd, events) && TutorialStep < Lessons.Length - 1) NextLesson();
        }

        /// <summary>A lesson that names a hero/slot selects the hero and pulses the slot (the "example" made visible). With
        /// coach marks the player does the selecting.</summary>
        private void FocusLesson()
        {
            var l = CurrentLesson;
            if (l == null || LessonWaiting || l.FocusRole == null || CoachOn) return;
            var hero = State.Units.FirstOrDefault(u => u.IsHero && u.Alive && u.Def.Role == l.FocusRole);
            if (hero == null) return;
            SelectHero(hero.Id);
            if (l.FocusSlot >= 0 && l.FocusSlot < BarSlots && abilitiesButton != null)
            {
                App.StartCoroutine(Tween.Pop(abilitiesButton, 1.15f, 0.4f));
                App.StartCoroutine(Tween.Pop(board.Units[hero.Id].Root, 1.12f, 0.4f));
            }
        }

        /// <summary>The lesson's recap in the description panel (GG-T449, GG-T1827): the popup's lesson in full, its example,
        /// and Next / Skip for a lesson whose situation hasn't come up yet.</summary>
        private void TutorialCard(VisualElement page)
        {
            var l = CurrentLesson;
            var row = Ui.El("row", page);
            row.style.alignItems = Align.Center;
            var col = Ui.El("col grow", row);
            col.style.flexShrink = 1;
            var title = Ui.Text($"{DrillLabel()}Lesson {TutorialStep + 1} of {Lessons.Length} · {l.Title}", "small gold", col);
            title.name = "tutorial-title";
            if (LessonWaiting)
            {
                var w = Ui.Text($"Next lesson arrives in wave {l.Wave + 1}. Keep fighting — clear this wave!", "tiny", col);
                w.style.whiteSpace = WhiteSpace.Normal;
            }
            else
            {
                var t = Ui.Text(LessonText(l), "tiny", col);
                t.name = "tutorial-text";
                t.style.whiteSpace = WhiteSpace.Normal;
                var ex = Ui.Text(l.Example, "tiny", col);
                ex.name = "tutorial-example";
                ex.style.whiteSpace = WhiteSpace.Normal;
                ex.style.color = Palette.Term;
            }
            // Next / Skip side by side at the top, so they never scroll out of the short panel; 24 px apart (GG-T656, POL-C3).
            var btns = Ui.El("row", row);
            btns.style.flexShrink = 0;
            btns.style.alignSelf = Align.FlexStart;
            bool last = TutorialStep == Lessons.Length - 1;
            Button first = null;
            if (last) (first = RowIcon.IconButton("Got it", RowKind.Confirm, FinishTutorial, "btn--small btn--primary", btns)).name = "tutorial-done";
            else if (!LessonWaiting) (first = RowIcon.IconButton("Next", RowKind.Enter, NextLesson, "btn--small btn--primary", btns)).name = "tutorial-next";
            if (first != null) Ui.Margin(first, right: Space.S24);
            RowIcon.IconButton("Skip", RowKind.Close, FinishTutorial, "btn--small", btns).name = "tutorial-skip";
        }

        /// <summary>When a wave starts, a lesson that was waiting for it comes on stage.</summary>
        private void TutorialWaveStarted()
        {
            if (!TutorialActive) return;
            // GG-T1149: a lesson whose popup showed but whose moment passed (a pickup that rang out) gives way once the next wave's lesson is due.
            while (TutorialStep < Lessons.Length - 1 && coachPopupFor == TutorialStep && !LessonWaiting && Lessons[TutorialStep + 1].Wave <= State.Wave && Lessons[TutorialStep].Waits)
            {
                if (Coach.Active) Coach.Cancel();
                DisarmCoach();
                NextLesson();
            }
            SyncPanel();
            if (!LessonWaiting) { App.Audio.Sfx("ui.select", 0.6f); FocusLesson(); }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Coach marks (GG-T539)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Blocking lessons with coach marks: not under automation (unless `-coach`), not on autoplay, not once the
        /// player has turned the tutorial off.</summary>
        private bool CoachOn => CoachAvailable && !AutoPlay;
        /// <summary>Coach marks would be on but for autoplay.</summary>
        private bool CoachAvailable => !Coach.Suppressed && App.Save != null && (practice || !Coach.Off(App.Save)) && setupOverride == null;

        private int commitCount, coachTriedAt = -1, coachPopupFor = -1, sceneStep = -1, sceneCommit = -1;
        /// <summary>GG-T1149: how often the current lesson's scene was staged again and its mark aborted, after its popup showed;
        /// a lesson that keeps failing (the pickup rings out, a foe keeps attacking) is skipped instead of repeating forever.</summary>
        private int stuckStep = -1, stuckScenes, stuckAborts;
        private const int StuckLimit = 3;
        private bool NoteStuck(int step, bool scene)
        {
            if (stuckStep != step) { stuckStep = step; stuckScenes = 0; stuckAborts = 0; }
            if (scene) stuckScenes++; else stuckAborts++;
            return stuckScenes >= StuckLimit || stuckAborts >= StuckLimit;
        }
        /// <summary>GG-T1149: skips the lesson in progress when its popup has shown and it is stuck or overdue (the next lesson's wave has begun).</summary>
        private bool SkipStuckLesson(int step)
        {
            if (step >= Lessons.Length - 1 || TutorialStep != step) return false;
            NextLesson();
            return true;
        }
        /// <summary>The armed drag rail: the hero a move mark is for, where it starts, and the only route it may take.</summary>
        private int coachHeroId = -1;
        private Coord coachStart;
        private List<Coord> coachPath;
        private bool coachMoveDone, coachCancelStep, coachCancelDone, coachCommitted;
        private List<int> reinforcementsArrived;
        /// <summary>GG-T853: the best combo rank of B or better that landed since the Combo lesson last looked.</summary>
        private ComboRank comboLanded;
        /// <summary>The hero <see cref="FindMove"/> is trying (read by a lesson's preview test).</summary>
        private int findHero = -1;

        private bool CoachRail => coachHeroId >= 0 && coachPath != null && coachPath.Count > 0 && dragHeroId == coachHeroId;
        private Coord CoachDest => coachPath[coachPath.Count - 1];
        /// <summary>A tap mark on a hero (Spells, Taunt) selects it; it doesn't start a drag.</summary>
        private bool CoachBlocksDrag => Coach.InSteps && coachHeroId < 0 && !coachCancelStep;

        /// <summary>Called every frame: when the player has control and nothing is teaching, the current lesson (or a
        /// battle system met for the first time: Guard, potions, reinforcements, Heavy foes, tactical tiles, hidden mines) opens.</summary>
        private void TickCoach()
        {
            if (Coach.Active)
            {
                if (AutoPlay || ended) Coach.Cancel();
                return;
            }
            if (!CoachOn || !CanCommand() || pressing || TargetingSlot >= 0 || State.IsOver) return;
            if (TutorialActive)
            {
                if (!LessonWaiting) StartTutorialCoach();
                return;
            }
            if (!practice) StartSystemLesson();
        }

        private void StartTutorialCoach()
        {
            int step = TutorialStep;
            if (step == 0 && !drillIntroShown && coachPopupFor < 0 && !string.IsNullOrEmpty(State.Stage?.Intro)) { DrillIntro(); return; }
            var l = CurrentLesson;
            bool popupDone = coachPopupFor == step;
            // A lesson waiting for its situation looks again only after the board changed.
            if (popupDone && coachTriedAt == commitCount) return;
            coachTriedAt = commitCount;
            var marks = SafeMarks(l.Marks);
            // GG-T542: the tutorial battle sets the lesson's situation up when there is nothing to point at (or, for
            // Taunt, as the lesson opens), once per board: the marks are looked for again after it plays.
            if (l.Scene != null && State.Tutorial && (marks.Count == 0 || (l.SceneFirst && !popupDone)) && !(sceneStep == step && sceneCommit == commitCount))
            {
                if (popupDone && NoteStuck(step, true) && SkipStuckLesson(step)) return;
                Commit(new TutorialSceneCommand { Scene = l.Scene });
                sceneStep = step;
                sceneCommit = commitCount;
                coachTriedAt = -1;
                return;
            }
            if (popupDone && marks.Count == 0) return;
            coachPopupFor = step;
            coachCommitted = false;
            ClearHint();
            bool hadMarks = marks.Count > 0;
            Coach.Run(new CoachLesson
            {
                Id = "battle-" + l.Id,
                Kicker = $"{DrillLabel()}Lesson {step + 1} of {Lessons.Length}",
                Title = l.Title,
                Text = (l.ShortHeroTimeline != null && TurnRules.HeroesOnTimeline(State) ? l.ShortHeroTimeline : l.Short) ?? LessonText(l), // GG-T815
                Image = "Tutorial/" + l.Id,
                Steps = marks,
                SkipPopup = popupDone,
                Hold = CoachHold,
                Practice = practice,
                Finished = how => TutorialCoachEnded(step, how, hadMarks),
            });
        }

        private bool drillIntroShown;

        /// <summary>GG-T1641: the drill's intro card (its stage's <see cref="StageDef.Intro"/>) before the first lesson: a coach
        /// popup with no marks, so it fades in with its chime (Reduce Motion drops the fade) and Next opens lesson 1.</summary>
        private void DrillIntro()
        {
            drillIntroShown = true;
            ClearHint();
            var stage = State.Stage;
            Coach.Run(new CoachLesson
            {
                Id = "drill-intro-" + stage.Id,
                Kicker = DrillLabel().TrimEnd(' ', '·'),
                Title = stage.Name,
                Text = stage.Intro,
                Hold = CoachHold,
                Practice = practice,
                Finished = how =>
                {
                    if (how == CoachEnd.TurnedOff) SkipDrills();
                    else if (how == CoachEnd.Cancelled) drillIntroShown = false;
                },
            });
        }

        private bool CoachHold() => Paused || playing || ended || State == null || State.IsOver;

        private void TutorialCoachEnded(int step, CoachEnd how, bool hadMarks)
        {
            DisarmCoach();
            if (how == CoachEnd.TurnedOff) { SkipDrills(); return; }
            if (how == CoachEnd.Cancelled) { coachPopupFor = -1; return; }
            if (TutorialStep != step) return;
            var l = Lessons[step];
            if (step == Lessons.Length - 1) { FinishTutorial(); return; }
            if (how == CoachEnd.Completed && (hadMarks || !l.Waits)) NextLesson();
            // Aborted, or no marks for a lesson that waits: the popup's words stay in the description panel and the lesson
            // completes the old way, or puts its marks up when its situation arrives.
            else
            {
                if (how == CoachEnd.Aborted && l.Waits && NoteStuck(step, false) && SkipStuckLesson(step)) return;
                SyncPanel();
            }
        }

        private void DisarmCoach()
        {
            coachHeroId = -1; coachPath = null; coachCancelStep = false;
        }

        private List<CoachStep> SafeMarks(Func<BattleScreen, List<CoachStep>> build)
        {
            if (build == null) return new List<CoachStep>();
            try { return build(this) ?? new List<CoachStep>(); }
            catch (Exception ex) { Debug.LogException(ex); return new List<CoachStep>(); }
        }

        // ── Lessons met outside the first battle (GG-T539: each fires the first time its system appears) ──

        private void StartSystemLesson()
        {
            var s = App.Save;
            if (Coach.Pending(s, "battle-guard") && BattleEngine.TooLittleTime(State))
            {
                RunSystem("battle-guard", "Guard", TurnRules.HeroesOnTimeline(State) // GG-T815 (§2.13.3): Guard shields only the acting hero
                    ? "Too little time for this hero to act before the next icon. GUARD gives its turn up, shielded."
                    : "Too little time for any hero to act before the next enemy. GUARD gives the turn up with every hero shielded.", GuardMarks());
                return;
            }
            if (Coach.Pending(s, "battle-potion") && PotionPick() is (Unit hero, int slot))
            {
                RunSystem("battle-potion", "Potions", "A potion on a hero's bar acts at once and uses no spell bar. Drink one before a hurt hero falls.", CastMarks(hero, slot));
                return;
            }
            if (comboLanded >= ComboRank.B)
            {
                // GG-T853: the first B-or-better combo opens the Combo lesson.
                var rank = comboLanded;
                comboLanded = ComboRank.None;
                if (Coach.Pending(s, "battle-combo"))
                {
                    RunSystem("battle-combo", "Combo", $"A {rank} combo! Every pincer, chain link, supporter action, follow-up, status and kill in one turn builds the combo. New heroes and new tricks climb highest, up to SSS.", ComboMarks(rank));
                    return;
                }
            }
            if (reinforcementsArrived != null && reinforcementsArrived.Count > 0)
            {
                var ids = reinforcementsArrived;
                reinforcementsArrived = null;
                if (Coach.Pending(s, "battle-reinforce"))
                    RunSystem("battle-reinforce", "Reinforcements", "More foes can walk on mid-wave. Each joins the timeline where its speed puts it, so read the bar again.", ReinforceMarks(ids));
                if (Coach.Active) return;
            }
            // GG-T908: the bombastic-battle systems, each the first time it is on the board (a lesson whose situation is
            // gone by the time the player has control simply waits for the next one).
            if (Coach.Pending(s, "battle-heavy") && HeavyFoe() is Unit heavy)
            {
                RunSystem("battle-heavy", "Heavy foes", "A [Heavy] foe won't be dragged, shoved, pulled or swapped. Make it [Floating] to move it, [Rooted] to hold it still, or wall it out with a barricade.", HeavyMarks(heavy));
                return;
            }
            if (Coach.Pending(s, "battle-tactical") && TacticalTile() is Coord laid)
            {
                RunSystem("battle-tactical", "Tactical tiles", "Heroes lay tiles too: a [Firewall] burns the foes who cross it, a [Mine] goes off under the first foe to step on it. The ring around the tile drains as its time runs out.", TacticalMarks(laid));
                return;
            }
            if (Coach.Pending(s, "battle-hidden") && HiddenMine() is Coord mine)
                RunSystem("battle-hidden", "Hidden mines", "A buried [Mine] shows for a moment, then fades: it is [Hidden]. Foes can't see yours and you can't see theirs, until someone steps on it and it pops.", HiddenMarks(mine));
        }

        // ── GG-T908: Heavy foes, tactical tiles, hidden mines ──

        /// <summary>A foe on the board that is Heavy now (not Floating), in plain sight (not a hidden mimic).</summary>
        private Unit HeavyFoe() => State.Units.FirstOrDefault(u => u.Alive && !u.IsHero && u.Team == Team.Enemy && u.IsHeavy && !RegionBoard.Hidden(State, u));

        private List<CoachStep> HeavyMarks(Unit u)
        {
            int id = u.Id;
            return new List<CoachStep>
            {
                new CoachStep
                {
                    Caption = $"The {u.Name} is Heavy: shoves and pulls won't move it (tap it).", Area = () => UnitRect(id), Info = true,
                    Valid = () => State.UnitById(id) is Unit x && x.Alive,
                },
            };
        }

        /// <summary>A timed harmful tile the party laid (a hero's firewall or mine), drawn with its life ring.</summary>
        private Coord? TacticalTile()
        {
            foreach (var kv in State.Region.Tiles)
            {
                var m = kv.Value;
                if (m.OwnerTeam != Team.Hero || !m.Hazard || m.LifeTicks <= 0 || !RegionBoard.Sees(m, Team.Hero)) continue;
                if ((State.Content?.TryTileState(m.State)?.LifeSeconds ?? 0f) > 0f) return kv.Key;
            }
            return null;
        }

        private List<CoachStep> TacticalMarks(Coord c)
        {
            var m = State.Region.At(c);
            if (m == null) return null;
            string state = m.State;
            return new List<CoachStep>
            {
                new CoachStep { Caption = $"Your {MarkWord(state)} holds this tile until its ring runs out (tap it).", Area = () => TileRect(c), Info = true, Valid = () => MarkStillAt(c, state) },
            };
        }

        /// <summary>A mark the party can see that fades from sight: still in its seen-by-all window, or the party's own
        /// already hidden (drawn faint for its own side).</summary>
        private Coord? HiddenMine()
        {
            foreach (var kv in State.Region.Tiles)
            {
                var m = kv.Value;
                if ((m.SightTicks > 0 || m.Hidden) && RegionBoard.Sees(m, Team.Hero)) return kv.Key;
            }
            return null;
        }

        private List<CoachStep> HiddenMarks(Coord c)
        {
            var m = State.Region.At(c);
            if (m == null) return null;
            string state = m.State, word = MarkWord(state);
            string caption = m.Hidden ? $"Your {word} has faded: only your side sees it (tap it)."
                : m.OwnerTeam == Team.Hero ? $"Your {word} fades from sight soon. Foes won't see it (tap it)."
                : $"This {word} fades from sight soon. Remember where it was (tap it).";
            return new List<CoachStep>
            {
                new CoachStep { Caption = caption, Area = () => TileRect(c), Info = true, Valid = () => MarkStillAt(c, state) },
            };
        }

        /// <summary>The marks' step stays only while the party still sees that mark on that tile (never a soft-lock).</summary>
        private bool MarkStillAt(Coord c, string state) =>
            State?.Region?.At(c) is TileMark m && string.Equals(m.State, state, StringComparison.OrdinalIgnoreCase) && RegionBoard.Sees(m, Team.Hero);

        /// <summary>A tile state's id as a word for a caption ("firewall", "mine", "buried mine").</summary>
        private static string MarkWord(string state)
        {
            if (string.IsNullOrEmpty(state)) return "tile";
            var w = state.ToLowerInvariant();
            if (w.Contains("firewall")) return "firewall";
            if (w.Contains("mine")) return "mine";
            return w.Replace('_', ' ').Replace('-', ' ');
        }

        private void RunSystem(string id, string title, string text, List<CoachStep> marks)
        {
            if (marks == null || marks.Count == 0) return;
            coachCommitted = false;
            ClearHint();
            Coach.Run(new CoachLesson
            {
                Id = id, Kicker = "New", Title = title, Text = text, Image = "Tutorial/" + id.Substring("battle-".Length),
                Steps = marks, Hold = CoachHold, Finished = how => { DisarmCoach(); if (how == CoachEnd.TurnedOff) SkipDrills(); },
            });
        }

        /// <summary>GG-T853: keeps the best combo rank of B or better a command's playback showed (outside the first battle).</summary>
        private void NoteComboRank(List<BattleEvent> events)
        {
            foreach (var x in events)
            {
                var r = x is ComboChangedEvent cc ? cc.Rank : x is ComboEndedEvent ce ? ce.Rank : ComboRank.None;
                if (r >= ComboRank.B && r > comboLanded) comboLanded = r;
            }
        }

        /// <summary>GG-T853: one mark on the hero with the largest share of the combo that landed (GG-T1828: the meter left
        /// the board), else on any hero still standing.</summary>
        private List<CoachStep> ComboMarks(ComboRank rank)
        {
            var u = State?.UnitById(comboTopUnit);
            if (u == null || !u.Alive || board == null || !board.Units.ContainsKey(u.Id))
                u = State?.Units.FirstOrDefault(x => x.IsCommandable && x.Alive && board != null && board.Units.ContainsKey(x.Id));
            if (u == null) return null;
            int id = u.Id;
            return new List<CoachStep>
            {
                new CoachStep
                {
                    Caption = $"That {rank} combo ranked your turn, D up to SSS. Supporters earn their own share; your best combo and each hero's points show on the results screen.",
                    Element = () => board != null && board.Units.TryGetValue(id, out var v) ? v.Root : null, Info = true,
                    Valid = () => board != null && board.Units.ContainsKey(id),
                },
            };
        }

        /// <summary>A hero with a ready item (a potion) on its bar while someone in the party is hurt.</summary>
        private (Unit, int)? PotionPick()
        {
            if (!State.Units.Exists(u => u.IsCommandable && u.Alive && u.HpFraction < 0.8f)) return null;
            foreach (var h in State.Units.Where(u => u.IsCommandable && u.Alive))
                for (int i = 0; i < BarSlots; i++)
                {
                    var a = BattleEngine.SlotAbility(State, h, i);
                    if (a != null && a.Kind == AbilityKind.Item && BattleEngine.GetSlotStatus(State, h.Id, i) == SlotStatus.Ready) return (h, i);
                }
            return null;
        }

        // ── Mark builders ──────────────────────────────────────────────────────────

        /// <summary>A cast mark for the first ready bar slot (any hero) whose ability passes <paramref name="want"/>; a heal first
        /// when <paramref name="preferHeal"/>.</summary>
        private List<CoachStep> SlotMarks(Func<AbilityDef, bool> want, bool preferHeal = false)
        {
            var found = new List<(Unit hero, int slot, AbilityDef a)>();
            foreach (var h in State.Units.Where(u => u.IsCommandable && u.Alive))
                for (int i = 0; i < BarSlots; i++)
                {
                    var a = BattleEngine.SlotAbility(State, h, i);
                    if (a != null && want(a) && BattleEngine.GetSlotStatus(State, h.Id, i) == SlotStatus.Ready) found.Add((h, i, a));
                }
            if (found.Count == 0) return null;
            var pick = preferHeal ? found.OrderByDescending(f => f.a.BaseHeal > 0).First() : found[0];
            return CastMarks(pick.hero, pick.slot);
        }

        private Rect? TileRect(Coord c) => board == null ? (Rect?)null
            : board.Root.LocalToWorld(new Rect(BoardView.PosOf(c), new Vector2(BoardView.Tile, BoardView.Tile)));

        private Rect? UnitRect(int id)
        {
            var u = State.UnitById(id);
            if (u == null || !u.Alive || board == null) return null;
            return board.Root.LocalToWorld(new Rect(BoardView.PosOf(u.Pos), new Vector2(BoardView.Tile * u.W, BoardView.Tile * u.H)));
        }

        private Vector2 TileCenter(Coord c) => board.Root.LocalToWorld(BoardView.CenterOf(c));

        private Rect? RouteRect(Coord start, List<Coord> route)
        {
            var all = new List<Coord>(route) { start };
            int x0 = all.Min(c => c.X), x1 = all.Max(c => c.X), y0 = all.Min(c => c.Y), y1 = all.Max(c => c.Y);
            return board.Root.LocalToWorld(new Rect(x0 * BoardView.Tile, y0 * BoardView.Tile, (x1 - x0 + 1) * BoardView.Tile, (y1 - y0 + 1) * BoardView.Tile));
        }

        private MoveCommand BotMove()
        {
            var bot = new HeroBot(7);
            return bot.BestMove(State) ?? bot.Decide(State) as MoveCommand;
        }

        /// <summary>The shortest move (any hero, an L-shaped route to any tile) whose preview passes <paramref name="want"/>;
        /// the bot's best move first.</summary>
        private MoveCommand FindMove(Func<MovePreview, bool> want)
        {
            var best = BotMove();
            if (best != null && best.Path.Count > 0 && Test(best.HeroId, best.Path)) return best;
            MoveCommand found = null;
            foreach (var h in State.Units.Where(u => u.IsCommandable && u.Alive && BattleEngine.CanMove(State, u.Id)))
                for (int y = 0; y < BattleConfig.Rows; y++)
                    for (int x = 0; x < BattleConfig.Cols; x++)
                    {
                        var to = new Coord(x, y);
                        if (to == h.Pos) continue;
                        foreach (var path in new[] { LPath(h.Pos, to, true), LPath(h.Pos, to, false) })
                        {
                            if (found != null && path.Count >= found.Path.Count) continue;
                            if (Test(h.Id, path)) found = new MoveCommand { HeroId = h.Id, Path = path };
                        }
                    }
            findHero = -1;
            return found;

            bool Test(int heroId, List<Coord> path)
            {
                findHero = heroId;
                var pv = BattleEngine.PreviewMove(State, heroId, path);
                return pv.StepsTaken.Count == path.Count && want(pv);
            }
        }

        private static List<Coord> LPath(Coord from, Coord to, bool xFirst)
        {
            var p = new List<Coord>();
            var c = from;
            void Walk(bool horizontal)
            {
                while (horizontal ? c.X != to.X : c.Y != to.Y)
                {
                    c = horizontal ? new Coord(c.X + Math.Sign(to.X - c.X), c.Y) : new Coord(c.X, c.Y + Math.Sign(to.Y - c.Y));
                    p.Add(c);
                }
            }
            Walk(xFirst); Walk(!xFirst);
            return p;
        }

        private static bool ShovePincers(BattleState s, int hero, MovePreview pv)
        {
            if (pv.Pincers.Count == 0 || hero < 0) return false;
            var shove = ShovePreview.Of(s, hero, pv);
            return pv.Pincers.Exists(p => p.A.IsHero && shove.CompletedByShove(p));
        }

        /// <summary>A move mark: box the hero, a dotted route to a dashed destination box, and a drag held to that route.</summary>
        private List<CoachStep> MoveMarks(MoveCommand m, string caption)
        {
            var hero = m == null ? null : State.UnitById(m.HeroId);
            if (hero == null || m.Path == null || m.Path.Count == 0) return null;
            var start = hero.Pos;
            var route = new List<Coord>(m.Path);
            var dest = route[route.Count - 1];
            int id = hero.Id;
            string name = hero.Name;
            return new List<CoachStep>
            {
                new CoachStep
                {
                    Caption = string.Format(caption ?? "Drag the {0} along the dotted line and drop it on the marked tile.", name),
                    // While the hero is lifted the whole route stays lit, so the held token isn't dimmed.
                    Area = () => dragging && dragHeroId == id ? RouteRect(start, route) : TileRect(start),
                    Path = () => { var pts = new List<Vector2> { TileCenter(start) }; foreach (var c in route) pts.Add(TileCenter(c)); return pts; },
                    Goal = () => TileRect(dest),
                    Done = () => coachMoveDone,
                    Dock = true,
                    Valid = () => coachMoveDone || dragging || (State.UnitById(id) is Unit u && u.Alive && u.Pos == start && BattleEngine.CanMove(State, id)),
                    Enter = () => { coachHeroId = id; coachStart = start; coachPath = route; coachMoveDone = false; },
                    Exit = () => { coachHeroId = -1; coachPath = null; },
                },
            };
        }

        /// <summary>The hold timer: lift a hero and drop it off the board (any drop cancels during this mark).</summary>
        private List<CoachStep> CancelMarks()
        {
            var hero = State.Units.FirstOrDefault(u => u.IsCommandable && u.Alive && BattleEngine.CanMove(State, u.Id));
            if (hero == null) return null;
            int id = hero.Id;
            var start = hero.Pos;
            return new List<CoachStep>
            {
                new CoachStep
                {
                    Caption = $"Lift the {hero.Name}, watch the timer drain, then drag it off the board and let go. No time is spent.",
                    Area = () => dragging && dragHeroId == id ? board.Root.worldBound : TileRect(start),
                    Done = () => coachCancelDone,
                    Dock = true,
                    Valid = () => coachCancelDone || dragging || BattleEngine.CanMove(State, id),
                    Enter = () => { coachCancelStep = true; coachCancelDone = false; },
                    Exit = () => coachCancelStep = false,
                },
            };
        }

        private List<CoachStep> ReadMarks()
        {
            var foe = State.Units.FirstOrDefault(u => u.Alive && !u.IsHero && !u.IsIndependent && !u.IsNeutral);
            if (foe == null) return null;
            int id = foe.Id;
            return new List<CoachStep>
            {
                new CoachStep { Caption = $"Tap the {foe.Name} to read its card.", Area = () => UnitRect(id),
                    Done = () => InspectCard.IsOpen, Valid = () => State.UnitById(id) is Unit u && u.Alive },
            };
        }

        /// <summary>A coach step on the commandable hero's first spell bar (§3.1.0: mana fills each spell's own bar,
        /// GG-T1821), or null when it carries no spell.</summary>
        private List<CoachStep> SpellBarMarks(Func<AbilityDef, string> caption)
        {
            var hero = State.Units.FirstOrDefault(u => u.IsCommandable && u.Alive);
            for (int i = 0; hero != null && i < BarSlots; i++)
            {
                var a = BattleEngine.SlotAbility(State, hero, i);
                if (a == null || State.ManaFull(hero, i) <= 0) continue;
                return new List<CoachStep> { new CoachStep { Caption = caption(a), Element = () => abilitiesButton, Info = true } };
            }
            return null;
        }

        /// <summary>GG-T731: what mana is, shown on a spell's own fill bar.</summary>
        private List<CoachStep> OrbMarks() =>
            SpellBarMarks(_ => "Each spell has its own bar. Mana fills it as we fight; a full bar glows: that spell is ready to cast.");

        private List<CoachStep> ManaMarks() =>
            SpellBarMarks(a => $"Pincers, strikes, kills and Clutch! moments fill every spell's bar: {a.Name} glows when its bar is full and ready to cast (tap the bar).");

        /// <summary>A cast mark: tap the hero, its slot, the target (when it has one), then Cast / Use / Go.</summary>
        private List<CoachStep> CastMarks(Unit hero, int slot)
        {
            if (hero == null || slot < 0 || slot >= BarSlots) return null;
            var a = BattleEngine.SlotAbility(State, hero, slot);
            if (a == null || BattleEngine.GetSlotStatus(State, hero.Id, slot) != SlotStatus.Ready) return null;
            int id = hero.Id;
            Coord? target = null;
            if (a.Mode != TargetMode.Auto)
            {
                var valid = BattleEngine.ValidTargets(State, id, slot);
                if (valid.Count == 0) return null;
                bool harmful = a.BaseDamage > 0 || a.Filter == TargetFilter.EnemyOnly;
                // A heal or a potion goes to the most hurt ally it can reach; anything harmful to a foe.
                var pick = harmful
                    ? valid.FirstOrDefault(t => State.UnitAt(t) is Unit u && !u.IsHero)
                    : valid.Where(t => State.UnitAt(t) is Unit u && u.IsHero).OrderBy(t => State.UnitAt(t).HpFraction).FirstOrDefault();
                target = valid.Contains(pick) ? pick : valid[0];
            }
            string verb = a.Kind == AbilityKind.Item ? "Use" : a.Kind == AbilityKind.Spell ? "Cast" : "Go";
            bool Aiming() => coachCommitted || (SelectedHeroId == id && TargetingSlot == slot);
            var marks = new List<CoachStep>
            {
                new CoachStep { Caption = $"Tap the {hero.Name}.", Area = () => UnitRect(id),
                    Done = () => coachCommitted || (SelectedHeroId == id && !pressing), Valid = () => State.UnitById(id) is Unit u && u.Alive },
                // Until GG-T1922 walks the sheet, the boxed Abilities button aims this slot directly (coachSlot).
                new CoachStep { Caption = $"Tap Abilities to ready {a.Name}.", Element = () => abilitiesButton,
                    Enter = () => coachSlot = slot, Exit = () => coachSlot = -1, Done = Aiming, Valid = () => coachCommitted || (SelectedHeroId == id && BattleEngine.GetSlotStatus(State, id, slot) == SlotStatus.Ready) },
            };
            if (target is Coord t)
            {
                var who = State.UnitAt(t);
                marks.Add(new CoachStep { Caption = who != null ? $"Tap the {who.Name} to aim {a.Name}." : $"Tap the marked tile to aim {a.Name}.", Area = () => TileRect(t),
                    Done = () => coachCommitted || (Aiming() && PendingTarget == t), Valid = Aiming });
            }
            marks.Add(new CoachStep { Caption = $"Tap {verb}.", Element = () => Root?.Q("confirm-action"), Done = () => coachCommitted,
                Valid = () => coachCommitted || (Aiming() && PendingTarget.HasValue) });
            return marks;
        }

        private List<CoachStep> BuffMarks()
        {
            var u = State.Units.FirstOrDefault(x => x.Alive && x.Buffs.Count > 0);
            if (u == null) return null;
            int id = u.Id;
            return new List<CoachStep>
            {
                new CoachStep { Caption = $"The pips on the {u.Name} are its buffs and debuffs. Tap it to read them.", Area = () => UnitRect(id),
                    Done = () => InspectCard.IsOpen || panelUnitId == id, Valid = () => State.UnitById(id) is Unit x && x.Alive },
            };
        }

        private List<CoachStep> TimelineMarks()
        {
            var marks = new List<CoachStep>
            {
                new CoachStep { Caption = "This big diamond is whoever acts next (tap it).", Element = () => timelineSlot, Info = true },
                new CoachStep { Caption = "Icons flow left as you act. Speed sets where each one enters: the faster, the nearer the left edge (tap the bar).", Element = () => Root?.Q("timeline"), Info = true },
            };
            var next = State.Units.Where(u => u.Alive && !u.IsHero && BattleEngine.ActionsUntilTurn(State, u.Id) >= 0)
                .OrderBy(u => BattleEngine.ActionsUntilTurn(State, u.Id)).FirstOrDefault();
            if (next != null)
            {
                int id = next.Id;
                marks.Add(new CoachStep { Caption = $"The same {next.Name} is ringed gold on the board. Pincer it before it reaches the left edge to push it back (tap it).", Area = () => UnitRect(id), Info = true });
            }
            return marks;
        }

        /// <summary>Ready: the last wave's foes, all of the board (GG-T542: every lesson points at something).</summary>
        private List<CoachStep> ReadyMarks() => board == null ? null : new List<CoachStep>
        {
            new CoachStep { Caption = "Clear the last wave: every foe on the board (tap to begin).", Element = () => board?.Root, Info = true },
        };

        private List<CoachStep> GuardMarks() => guardButton == null ? null : new List<CoachStep>
        {
            new CoachStep { Caption = "Tap GUARD.", Element = () => guardButton, Done = () => coachCommitted },
        };

        private List<CoachStep> PlateMarks()
        {
            var u = State.Units.FirstOrDefault(x => x.Alive && !x.IsHero && x.HasPlate);
            if (u == null) return null;
            int id = u.Id;
            return new List<CoachStep>
            {
                new CoachStep { Caption = $"The thick edge on the {u.Name} is its plate. Strike from the other sides (tap it).", Area = () => UnitRect(id), Info = true },
            };
        }

        private List<CoachStep> ReinforceMarks(List<int> ids)
        {
            var u = ids.Select(State.UnitById).FirstOrDefault(x => x != null && x.Alive);
            if (u == null) return null;
            int id = u.Id;
            var marks = new List<CoachStep>
            {
                new CoachStep { Caption = $"A {u.Name} just arrived (tap it).", Area = () => UnitRect(id), Info = true },
            };
            var entry = State.TurnEntryOf(id);
            if (entry != null)
                marks.Add(new CoachStep { Caption = "Its icon joined the timeline here (tap it).", Element = () => Root?.Q("tl-" + entry.Id), Info = true });
            return marks;
        }

        /// <summary>The nearest point to <paramref name="p"/> on the armed rail (the hero's start, then each route tile).</summary>
        private Vector2 OnRail(Vector2 p)
        {
            var pts = new List<Vector2> { BoardView.CenterOf(coachStart) };
            foreach (var c in coachPath) pts.Add(BoardView.CenterOf(c));
            Vector2 best = pts[0];
            float bestD = float.MaxValue;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var b = pts[i];
                var ab = b - a;
                float k = ab.sqrMagnitude <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                var q = a + ab * k;
                float d = (q - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = q; }
            }
            return best;
        }

        /// <summary>The drag rail: the path grows only along the marked route, one tile at a time, never back.</summary>
        private void ExtendOnRail(Coord over)
        {
            int idx = coachPath.IndexOf(over);
            while (idx >= 0 && dragPath.Count <= idx)
            {
                var step = coachPath[dragPath.Count];
                var candidate = new List<Coord>(dragPath) { step };
                var pv = BattleEngine.PreviewMove(State, dragHeroId, candidate);
                if (pv.StepsTaken.Count < candidate.Count) return;
                dragPath.Add(step);
                dragTile = step;
                preview = pv;
                App.Audio.Sfx("battle.move", 0.5f);
                ApplyPreview(pv, dragHeroId);
            }
        }
    }
}
