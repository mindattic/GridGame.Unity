using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
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
    /// popup text in the Tutorial tab and puts its marks up the moment the situation arrives, or completes the old way
    /// (the player does it, a few actions pass, or Next). Lessons that need particular foes wait for their wave.
    /// Under automation (no `-coach`) the marks are off and the Tutorial tab teaches alone, as before.
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
            /// <summary>The popup's one or two lines (GG-T539); the Tutorial tab keeps the longer <see cref="Text"/>.</summary>
            public string Short;
            /// <summary>The wave (0-based) the lesson needs on the board.</summary>
            public int Wave;
            /// <summary>Show the bot's move as a glowing demo while idle (the Tutorial tab mode, without coach marks).</summary>
            public bool DemoMove;
            /// <summary>Done when this returns true for a committed command's events.</summary>
            public Func<BattleScreen, BattleCommand, List<BattleEvent>, bool> Done;
            /// <summary>Highlight on show: (hero class, bar slot) to pulse.</summary>
            public string FocusHero; public int FocusSlot = -1;
            /// <summary>Actions after which the lesson moves on by itself.</summary>
            public int Patience = 5;
            /// <summary>Done the moment the player holds something to read it (GG-T252).</summary>
            public bool DoneOnInspect;
            /// <summary>GG-T539: the coach marks for this board, or null/empty when its situation isn't here yet.</summary>
            public Func<BattleScreen, List<CoachStep>> Marks;
            /// <summary>Without marks, the lesson stays (in the Tutorial tab) until done instead of ending on the popup's Next.</summary>
            public bool Waits;
            /// <summary>GG-T542: the tutorial battle sets this lesson's situation up (<see cref="TutorialSceneCommand"/>) when its
            /// marks find nothing to point at: a chain to close, a foe charging, a plated foe, an orb for a spell.</summary>
            public string Scene;
            /// <summary>Stage the scene as the lesson opens even when marks exist (Taunt: a foe closes on the Cleric).</summary>
            public bool SceneFirst;
        }

        private static readonly Lesson[] Lessons =
        {
            new Lesson { Id = "move", Title = "Moving", DemoMove = true, Waits = true,
                Short = "Drag a hero tile by tile. Anyone in the way swaps into the tile you leave, and moving never hurts.",
                Text = "Drag a hero across the board, tile by tile. Anyone in the way swaps into the tile you just left — moving never hurts anyone.",
                Example = "Example: follow the glowing path to the glowing tile.",
                Done = (b, c, e) => c is MoveCommand,
                Marks = b => b.MoveMarks(b.BotMove(), null) },
            new Lesson { Id = "pincer", Title = "Pincers", DemoMove = true, Waits = true, Scene = "pincer",
                Short = "Trap a foe in a straight line between two heroes to PINCER it. Both heroes strike.",
                Text = "Trap an enemy in a straight line between two heroes to PINCER it — both heroes strike. Heroes linked to an attacker by an unbroken chain of allies join in as supporters, each with its class's support action.",
                Example = "Example: Hero · Slime · Hero in a row or column.",
                Done = (b, c, e) => e.Exists(x => x is PincerFormedEvent p && p.Team == Team.Hero),
                Marks = b => b.MoveMarks(b.FindMove(pv => pv.Pincers.Exists(p => p.A.IsHero)), "Drag the {0} along the dotted line: the drop traps the foe between two heroes.") },
            // GG-T252: recognition over recall — anything on screen explains itself. GG-T450: a tap reads it too.
            new Lesson { Id = "read", Title = "Tap to read", Patience = 3, DoneOnInspect = true,
                Short = "Tap any hero, foe, timeline icon or spell slot to read its card. Holding reads it too.",
                Text = "Tap a hero, an enemy or an icon on the timeline to read its card; tap a spell slot that can't be used yet to see why. The card stays until your next tap. Holding anything — a spell slot or status pip too — reads it without acting.",
                Example = "Example: tap a slime to open its card.",
                Done = (b, c, e) => false,
                Marks = b => b.ReadMarks() },
            // GG-T190: shoving is a tactic, not an accident — the held hero moves other heroes and enemies into place.
            new Lesson { Id = "shove", Title = "Shoving", Patience = 4, Waits = true, Scene = "shove",
                Short = "Drag your hero through a foe and the foe slides into the tile you just left. Shove it between two heroes to pincer it.",
                Text = "Your held hero is a lever. Drag it through an enemy and the enemy slides into the tile you just left — so shove a foe into line between two heroes and the drop pincers it. While you drag, arrows show where everyone will slide and gold outlines show the pincers the drop completes.",
                Example = "Example: a slime sits above the gap beside your hero. Step into the gap, then up through the slime: it drops into the gap, between your heroes.",
                Done = (b, c, e) => ShovedIntoPincer(e),
                Marks = b => b.MoveMarks(b.FindMove(pv => ShovePincers(b.State, b.findHero, pv)), "Drag the {0} through the foe along the dotted line: it slides into line and the drop pincers it.") },
            new Lesson { Id = "hold", Title = "The hold timer", Patience = 3, Waits = true,
                Short = "Lifting a hero starts the clock. Changed your mind? Drop it off the board, or back on its own tile, to cancel for free.",
                Text = "Picking a hero up starts the clock: the bar under it drains until the next enemy reaches the left edge, then the hero drops where it is. Each hero acts once before that enemy does, sharing the time — what one hero leaves, the next can use, and pushing the enemy back buys more. Changed your mind? Drop it back on its own tile, or off the board, to cancel — no time spent.",
                TextOneHero = "Picking a hero up starts the clock: the bar under it drains until the next enemy reaches the left edge, then the hero drops where it is. Each turn one hero acts — any hero, the same one again if you like — and that action is the party's whole turn; pushing the enemy back buys more time. Changed your mind? Drop it back on its own tile, or off the board, to cancel — no time spent.",
                TextRound = "Picking a hero up starts the clock: the bar under it drains until the next enemy reaches the left edge, then the hero drops where it is. Each turn one hero who hasn't moved this round acts, and that action is the party's whole turn; once every hero has moved, a new round frees them all. Pushing the enemy back buys more time. Changed your mind? Drop it back on its own tile, or off the board, to cancel — no time spent.",
                TextHeroTimeline = "Each hero has its own icon on the timeline, and only the hero whose icon reached the left edge acts: that one action is its whole turn. Picking it up starts the clock: the bar under it drains until the next icon, a foe's or another hero's, reaches the left edge, then the hero drops where it is. Pushing a foe back buys more time. Changed your mind? Drop it back on its own tile, or off the board, to cancel — no time spent.",
                Example = "Example: lift a hero, then drag it off the board and let go.",
                Done = (b, c, e) => c is MoveCommand,
                Marks = b => b.CancelMarks() },
            // GG-T731: what an orb is, before Mana teaches how to earn one (the orbs as GG-T730 draws them).
            new Lesson { Id = "orbs", Title = "Mana orbs", Patience = 2,
                Short = "These glass orbs hold your mana. Mana dust flows in and fills them with glowing light; a full orb breathes, ready to spend on an ability.",
                Text = "The row of glass orbs under your abilities is your mana. An empty orb is clear glass. As your heroes earn mana, glowing mana dust flows into the next orb and fills it with light from the bottom up. A full orb glows and gently breathes: it is ready to spend. Abilities cost whole orbs, and casting one drains them back to empty glass.",
                Example = "Example: watch the next orb fill as your pincers land.",
                Done = (b, c, e) => e.Exists(x => x is OrbMintedEvent),
                Marks = b => b.OrbMarks() },
            new Lesson { Id = "mana", Title = "Mana",
                Short = "Pincers fill the mana globe. Every 100 mana makes an orb, and spells spend orbs.",
                Text = "Pincers fill the mana bubble at the end of your orb row; at 100 it becomes an orb. Lean pincers earn the most: every extra hero divides the mana, every chain link multiplies it.",
                Example = "Example: two heroes taking half a foe's HP earn 60 of the 100.",
                Done = (b, c, e) => e.Exists(x => x is OrbMintedEvent o && o.Reason == "pincer"), Patience = 3,
                Marks = b => b.ManaMarks() },
            new Lesson { Id = "chain", Title = "Chains", Wave = 1, DemoMove = true, Waits = true, Scene = "chain",
                Short = "One drag can set off several pincers at once: a CHAIN. Every link strikes and earns more mana.",
                Text = "One drag can set off several pincers in a row — a CHAIN. Every link strikes, and every link earns more mana than the last.",
                Example = "Example: line both foes up between your heroes, then drag the last hero in.",
                Done = (b, c, e) => e.Exists(x => x is PincerFormedEvent p && p.Team == Team.Hero && p.ChainIndex >= 1),
                Marks = b => b.MoveMarks(b.FindMove(pv => pv.Pincers.Count(p => p.A.IsHero) >= 2), "Drag the {0} along the dotted line: the drop closes two pincers at once.") },
            new Lesson { Id = "support", Title = "Supporters", Wave = 1, Waits = true, Patience = 4, Scene = "support",
                Short = "A hero touching an attacker joins the pincer as a supporter, linked in blue, adds its class's support and earns its own share on the combo meter.",
                Text = "Heroes linked to a pincer's attacker by an unbroken line of allies join in as supporters: the blue links in the drag preview. Each adds its class's support action — a Cleric heals, a Barbarian adds a blow — and its own points on the combo meter at the board's top right, in mana blue.",
                Example = "Example: pincer with a third hero standing next to one of the attackers.",
                Done = (b, c, e) => e.Exists(x => x is PincerFormedEvent p && p.Team == Team.Hero && p.Supporters.Count > 0),
                Marks = b => b.MoveMarks(b.FindMove(pv => pv.Pincers.Exists(p => p.A.IsHero && p.SupportersA.Count + p.SupportersB.Count > 0)), "Drag the {0} along the dotted line: the third hero joins in as a supporter.") },
            new Lesson { Id = "spell", Title = "Spells", Wave = 2, FocusHero = "cleric", FocusSlot = 0, Waits = true, Scene = "spell",
                Short = "Tap a hero, a lit spell, a target, then Cast. A spell charges on the timeline, then lands.",
                Text = "Tap a hero, a lit spell, then a target, then Cast. A spell charges first — its bar fills as time moves — then it flies and lands.",
                Example = "Example: Cleric → Heal → a hurt ally → Cast.",
                Done = (b, c, e) => e.Exists(x => x is CastQueuedEvent q && !q.IsCharge),
                Marks = b => b.SlotMarks(a => a.Kind == AbilityKind.Spell, preferHeal: true) },
            new Lesson { Id = "buffs", Title = "Buffs & debuffs", Wave = 2, Patience = 3, Waits = true, Scene = "buffs",
                Short = "Coloured pips on a token are its buffs (blue) and debuffs (purple). Tap the unit to read them.",
                Text = "Coloured pips on a token are its buffs (blue) and debuffs (purple): poison, haste, shields, slows. Hold a unit, or a pip in the panel, to read exactly what each does and how long it lasts.",
                Example = "Example: hold a hero after the Cleric's spell lands.",
                Done = (b, c, e) => e.Exists(x => x is BuffAppliedEvent),
                Marks = b => b.BuffMarks() },
            new Lesson { Id = "timeline", Title = "The timeline", Wave = 2, Patience = 3,
                Short = "Time moves only when you act. Icons flow left, and whoever reaches the left edge acts. Speed decides who gets there first.",
                TextHeroTimeline = "Time moves only when you act. Every hero and foe has an icon that flows left; whoever reaches the left edge acts, and a hero's icon arriving makes it that hero's turn. Speed sets where each icon enters. Spells ride the bar from their cast time. Pincers and Pushback skills shove a foe back anywhere on the bar. GUARD gives up the acting hero's turn, shielded.",
                Text = "Time moves only when you act. Icons flow left; whoever reaches the left edge acts. The big diamond on the left shows who is next, ringed gold on the bar and on the board, and a foe glows (icon and tile alike) in its last 3 seconds. Spells ride the bar from their cast time. Pincers and Pushback skills shove a foe back anywhere on the bar. GUARD skips ahead with everyone shielded.",
                Example = "Example: pincer the gold-ringed foe before it reaches the left edge.",
                Done = (b, c, e) => e.Exists(x => x is PushedBackEvent),
                Marks = b => b.TimelineMarks() },
            new Lesson { Id = "taunt", Title = "Roles & Taunt", Wave = 3, FocusHero = "warden", FocusSlot = 0, Waits = true, Scene = "taunt", SceneFirst = true,
                Short = "Each hero has a role. A Tank's Taunt pulls every foe in reach onto it, so your casters can finish their spells.",
                Text = "Each hero has a role. A Tank (your Warden) taunts: Taunt makes every enemy in reach come for it, so your casters can finish their spells. Strikers hit hardest, Healers mend.",
                Example = "Example: Warden → Taunt → Go.",
                Done = (b, c, e) => e.Exists(x => x is BuffAppliedEvent a && a.BuffId == "taunting"),
                Marks = b => b.SlotMarks(a => (a.Id ?? "").Contains("taunt")) },
            new Lesson { Id = "guard", Title = "Guard", Wave = 3, Patience = 2,
                Short = "GUARD gives up the party's turn: time skips to the next enemy with every hero shielded. Use it when there's no good move or no time.",
                Text = "GUARD gives up what is left of the party's turn: the clock skips to the next enemy turn with every hero shielded. Use it when there is no good move, or when the gap before the next enemy is too short for any action.",
                ShortHeroTimeline = "GUARD gives up the acting hero's turn: it is shielded and the clock runs on to the next hero's icon. Use it when there's no good move or no time.",
                TextHeroTimeline = "GUARD gives up the acting hero's turn: that hero is shielded and the clock runs on to the next hero's icon, foes acting on the way. Use it when there is no good move, or when the gap before the next icon is too short for any action.",
                Example = "Example: tap GUARD at the top right.",
                Done = (b, c, e) => c is ShieldCommand,
                Marks = b => b.GuardMarks() },
            new Lesson { Id = "pickup", Title = "Pickups", Wave = 3, Waits = true, Scene = "pickup",
                Short = "Fallen foes sometimes drop a pickup. Walk a hero over it before its ring runs out.",
                Text = "Fallen enemies sometimes drop a pickup: a tonic, a mana shard, quicksilver or a might rune. Walk a hero over it before its ring runs out.",
                Example = "Example: drag any hero onto the glowing token.",
                Done = (b, c, e) => e.Exists(x => x is PickupCollectedEvent),
                Marks = b => b.MoveMarks(b.FindMove(pv => b.State.Pickups.Exists(k => pv.Positions.TryGetValue(b.findHero, out var at) && at == k.At)), "Drag the {0} along the dotted line onto the pickup.") },
            new Lesson { Id = "interrupt", Title = "Interrupting", Wave = 4, Waits = true, Scene = "interrupt",
                Short = "A charging foe wears a ring and marks its target. Pincer it to stagger the cast; enough blows cancel it.",
                Text = "A charging enemy wears a ring and marks its target. Hit it with a pincer to stagger the cast — enough blows cancel it and earn mana.",
                Example = "Example: pincer the wolf while it howls.",
                Done = (b, c, e) => e.Exists(x => x is CastStaggeredEvent || (x is CastCancelledEvent cc && cc.Reason == "interrupted")),
                Marks = b => b.MoveMarks(b.FindMove(pv => pv.Pincers.Exists(p => p.A.IsHero && p.Opponents.Exists(o => b.State.CastEntryOf(o.Id) != null))), "Drag the {0} along the dotted line to pincer the charging foe.") },
            new Lesson { Id = "plate", Title = "Armor plates", Wave = 4, Patience = 2, Waits = true, Scene = "plate",
                Short = "A thick edge on a foe is an armor plate. Strike from the sides, break it head-on, or use spells, which ignore it.",
                Text = "Some foes wear an armor plate on one side, drawn as a thick edge. Blows into the plate chip it instead of the foe: strike from the sides, break it head-on, or use spells, which ignore it.",
                Example = "Example: hold an armored foe later on to see its plate and which way it faces.",
                Done = (b, c, e) => e.Exists(x => x is PlateBrokenEvent),
                Marks = b => b.PlateMarks() },
            new Lesson { Id = "ready", Title = "Ready", Wave = 4, Patience = 99,
                Short = "That's the whole toolkit: move, pincer, chain for mana, cast with care, let your tank take the hits. Clear the last wave!",
                Text = "That's the whole toolkit: move, pincer, chain for mana, cast with care, let your tank take the hits. Clear the last wave!",
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

        public int TutorialStep { get; private set; } = -1;
        private int tutorialActions;
        private bool TutorialActive => TutorialStep >= 0;
        private Lesson CurrentLesson => TutorialActive ? Lessons[TutorialStep] : null;
        private bool LessonWaiting => CurrentLesson != null && State.Wave < CurrentLesson.Wave;
        private bool TutorialDemoMove => CurrentLesson != null && CurrentLesson.DemoMove && !LessonWaiting && !CoachOn;

        private void FinishTutorial()
        {
            TutorialStep = -1;
            if (Coach.Active && (Coach.LessonId ?? "").StartsWith("battle-")) Coach.Cancel();
            if (App.Save != null) { App.Save.TutorialDone = true; App.Persist(); }
            SyncPanel();
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
            if (l == null || LessonWaiting || l.FocusHero == null || CoachOn) return;
            var hero = State.Units.FirstOrDefault(u => u.IsHero && u.Alive && u.Def.Id == l.FocusHero);
            if (hero == null) return;
            SelectHero(hero.Id);
            if (l.FocusSlot >= 0 && l.FocusSlot < slotViews.Count)
            {
                var slot = slotViews[l.FocusSlot];
                App.StartCoroutine(Tween.Pop(slot, 1.15f, 0.4f));
                App.StartCoroutine(Tween.Pop(board.Units[hero.Id].Root, 1.12f, 0.4f));
            }
        }

        /// <summary>The Tutorial tab (GG-T449) stays as the lesson's recap: the popup's lesson in full, its example, and
        /// Next / Skip for a lesson whose situation hasn't come up yet.</summary>
        private void TutorialCard(VisualElement page)
        {
            var l = CurrentLesson;
            var row = Ui.El("row", page);
            row.style.alignItems = Align.Center;
            var col = Ui.El("col grow", row);
            col.style.flexShrink = 1;
            var title = Ui.Text($"Tutorial {TutorialStep + 1}/{Lessons.Length} · {l.Title}", "small gold", col);
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
                ex.style.color = new Color(0.62f, 0.9f, 1f);
            }
            var btns = Ui.El("col", row);
            btns.style.flexShrink = 0;
            // 24 px from the tab hit areas above (window padding 8 + 16) and between the two buttons (GG-T656, POL-C3).
            btns.style.paddingTop = Space.S16;
            bool last = TutorialStep == Lessons.Length - 1;
            Button first = null;
            if (last) (first = RowIcon.IconButton("Got it", RowKind.Confirm, FinishTutorial, "btn--small btn--primary", btns)).name = "tutorial-done";
            else if (!LessonWaiting) (first = RowIcon.IconButton("Next", RowKind.Enter, NextLesson, "btn--small btn--primary", btns)).name = "tutorial-next";
            if (first != null) first.style.marginBottom = Space.S24;
            RowIcon.IconButton("Skip", RowKind.Close, FinishTutorial, "btn--small", btns).name = "tutorial-skip";
        }

        /// <summary>When a wave starts, a lesson that was waiting for it comes on stage.</summary>
        private void TutorialWaveStarted()
        {
            if (!TutorialActive) return;
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
        private bool CoachAvailable => !Coach.Suppressed && App.Save != null && !Coach.Off(App.Save) && setupOverride == null;

        private int commitCount, coachTriedAt = -1, coachPopupFor = -1, sceneStep = -1, sceneCommit = -1;
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
            StartSystemLesson();
        }

        private void StartTutorialCoach()
        {
            int step = TutorialStep;
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
                Kicker = $"Lesson {step + 1} of {Lessons.Length}",
                Title = l.Title,
                Text = (l.ShortHeroTimeline != null && TurnRules.HeroesOnTimeline(State) ? l.ShortHeroTimeline : l.Short) ?? LessonText(l), // GG-T815
                Image = "Tutorial/" + l.Id,
                Steps = marks,
                SkipPopup = popupDone,
                Hold = CoachHold,
                Finished = how => TutorialCoachEnded(step, how, hadMarks),
            });
        }

        private bool CoachHold() => Paused || playing || ended || State == null || State.IsOver;

        private void TutorialCoachEnded(int step, CoachEnd how, bool hadMarks)
        {
            DisarmCoach();
            if (how == CoachEnd.TurnedOff) { FinishTutorial(); return; }
            if (how == CoachEnd.Cancelled) { coachPopupFor = -1; return; }
            if (TutorialStep != step) return;
            var l = Lessons[step];
            if (step == Lessons.Length - 1) { FinishTutorial(); return; }
            if (how == CoachEnd.Completed && (hadMarks || !l.Waits)) NextLesson();
            // Aborted, or no marks for a lesson that waits: the popup's words stay in the Tutorial tab and the lesson
            // completes the old way, or puts its marks up when its situation arrives.
            else SyncPanel();
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
                RunSystem("battle-potion", "Potions", "A potion on a hero's bar acts at once and costs no orbs. Drink one before a hurt hero falls.", CastMarks(hero, slot));
                return;
            }
            if (comboLanded >= ComboRank.B)
            {
                // GG-T853: the first B-or-better combo opens the Combo lesson on the meter that showed it.
                var rank = comboLanded;
                comboLanded = ComboRank.None;
                if (Coach.Pending(s, "battle-combo"))
                {
                    RunSystem("battle-combo", "Combo", $"A {rank} combo! Every pincer, chain link, supporter action, follow-up, status and kill in one turn builds the meter. New heroes and new tricks climb highest, up to SSS.", ComboMarks(rank));
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
                Steps = marks, Hold = CoachHold, Finished = how => { DisarmCoach(); if (how == CoachEnd.TurnedOff) FinishTutorial(); },
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

        /// <summary>GG-T853: one mark on the combo meter, held up at the rank that landed while the lesson points at it, then
        /// let go to fade as a closing summary does (unless a combo is still running).</summary>
        private List<CoachStep> ComboMarks(ComboRank rank)
        {
            EnsureComboMeter();
            if (comboFrame == null) return null;
            return new List<CoachStep>
            {
                new CoachStep
                {
                    Caption = "The combo meter ranks your turn, D up to SSS. Supporters earn their own share, in blue; the bar drains between hits (tap it).",
                    Element = () => comboFrame, Info = true,
                    Valid = () => comboFrame != null && comboFrame.panel != null,
                    Enter = () => HoldComboMeter(rank), Exit = ReleaseComboMeter,
                },
            };
        }

        private void HoldComboMeter(ComboRank rank)
        {
            if (comboRoot == null || comboRoot.panel == null) return;
            comboClosing = false; // a closing summary's fade stops while the lesson points at the meter
            comboRoot.style.opacity = 1f;
            if (comboShownRank == ComboRank.None)
            {
                comboShownRank = rank;
                App.StartCoroutine(ComboRankSlam(rank, false));
            }
            comboFrame.MarkDirtyRepaint();
        }

        private void ReleaseComboMeter()
        {
            if (comboRoot == null || comboRoot.panel == null || State?.Combo?.Active == true) return;
            comboClosing = true;
            App.StartCoroutine(ComboLessonFade());
        }

        private IEnumerator ComboLessonFade()
        {
            float from = comboRoot.style.opacity.value;
            yield return Tween.Run(Timing.Standard, k => { if (comboClosing && comboRoot != null) comboRoot.style.opacity = from * (1f - k); });
            if (!comboClosing || comboRoot == null) yield break; // a new combo started meanwhile
            comboChips.Clear();
            comboShownRank = ComboRank.None;
            comboRankLabel.text = comboWordLabel.text = comboScoreLabel.text = "";
            comboFrame.MarkDirtyRepaint();
        }

        /// <summary>A hero with a ready item (a potion) on its bar while someone in the party is hurt.</summary>
        private (Unit, int)? PotionPick()
        {
            if (!State.Units.Exists(u => u.IsCommandable && u.Alive && u.HpFraction < 0.8f)) return null;
            foreach (var h in State.Units.Where(u => u.IsCommandable && u.Alive))
                for (int i = 0; i < slotViews.Count; i++)
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
                for (int i = 0; i < slotViews.Count; i++)
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

        /// <summary>GG-T731: the orb row (empty glass that mana dust fills), then one orb (full, it breathes and is ready).</summary>
        private List<CoachStep> OrbMarks()
        {
            if (pool == null) return null;
            var marks = new List<CoachStep>
            {
                new CoachStep { Caption = "Each glass orb holds mana. Empty orbs are clear glass; mana dust flows in and fills them with glowing light.", Element = () => pool, Info = true },
            };
            if (pool.Q("orb0") != null)
                marks.Add(new CoachStep { Caption = "A full orb glows and breathes: it's ready to spend on an ability.", Element = () => pool?.Q("orb0"), Info = true });
            return marks;
        }

        private List<CoachStep> ManaMarks()
        {
            var marks = new List<CoachStep>
            {
                new CoachStep { Caption = "Pincers, strikes, kills and Clutch! moments fill these mana orbs; spells spend them (tap the orbs).", Element = () => pool, Info = true },
            };
            var hero = State.Units.FirstOrDefault(u => u.IsCommandable && u.Alive);
            for (int i = 0; hero != null && i < slotViews.Count; i++)
            {
                var a = BattleEngine.SlotAbility(State, hero, i);
                if (a == null || a.Cost <= 0) continue;
                int k = i;
                marks.Add(new CoachStep { Caption = $"A spell's pips are its cost in orbs: {a.Name} costs {a.Cost} (tap the pips).", Element = () => k < slotViews.Count ? slotViews[k].Q("cost") : null, Info = true });
                break;
            }
            return marks;
        }

        /// <summary>A cast mark: tap the hero, its slot, the target (when it has one), then Cast / Use / Go.</summary>
        private List<CoachStep> CastMarks(Unit hero, int slot)
        {
            if (hero == null || slot < 0 || slot >= slotViews.Count) return null;
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
                new CoachStep { Caption = $"Tap {a.Name}.", Element = () => slot < slotViews.Count ? slotViews[slot] : null,
                    Done = Aiming, Valid = () => coachCommitted || (SelectedHeroId == id && BattleEngine.GetSlotStatus(State, id, slot) == SlotStatus.Ready) },
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
