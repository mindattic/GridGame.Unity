using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed class SaveSlotScreen : GameScreen
    {
        private readonly bool newGame;
        /// <summary>New Game's turn-mode choice (§2.13, GG-T774): null follows the profile setting, a mode locks the save
        /// to it (<see cref="TurnModeSetting.Lock"/>). Only <see cref="TurnModes.Available"/> modes are offered.</summary>
        private readonly TurnMode? turnModeLock;
        /// <summary>Whether Start Campaign's More options fold (the turn-mode row) is open.</summary>
        private readonly bool moreOptions;
        public SaveSlotScreen(bool newGame, TurnMode? turnModeLock = null, bool moreOptions = false)
        { this.newGame = newGame; this.turnModeLock = turnModeLock; this.moreOptions = moreOptions; }
        public override string Id => newGame ? "newgame" : "load";

        /// <summary>The Main Menu's Start Campaign (GG-T1358): with every slot empty (none saved, none damaged) the
        /// campaign starts in slot 1 without the picker; otherwise the slot picker opens.</summary>
        public static void StartCampaign()
        {
            for (int i = 0; i < SaveStore.Slots; i++)
                if (App.Saves.Exists(i)) { App.Router.Push(new SaveSlotScreen(true)); return; }
            Start(0, null);
        }

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, "cityruins", 0);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col", root);
            Ui.Header(newGame ? "Start Campaign" : "Load", () => App.Router.Back(), col, out _);
            var body = Ui.PageBody(col);
            body.style.justifyContent = Justify.FlexEnd; // GG-T1853: the slot rows sit in the thumb zone, the header above
            var w = Ui.Panel(body, newGame ? "Choose a slot" : "Choose a save");
            for (int i = 0; i < SaveStore.Slots; i++)
            {
                int slot = i;
                var s = App.Saves.Load(i, App.Content);
                // A slot whose file (and backup) exist but won't read is damaged, not empty: it may hold paid Mana Shards.
                bool damaged = s == null && App.Saves.Exists(i);
                string label = s != null ? $"Slot {i + 1} · {s.Name}" : damaged ? $"Slot {i + 1} · Can't be read" : $"Slot {i + 1} · Empty";
                string value = s == null ? "" : $"Stage {s.HighestClearedStage + 2} · {Ui.Gold(s.Gold)}";
                var row = Ui.MenuRow(label, value, () => Pick(slot, s, damaged), w, null, newGame || s != null);
                row.KindIcon(RowKind.Save); // GG-T738 (§26.7)
                if (damaged) row.Q<Label>("label")?.AddToClassList("bad");
                if (s != null)
                {
                    Ui.Note($"{s.Roster.Count} heroes · {TimeSpan.FromSeconds(s.PlaySeconds):h\\:mm} played · {s.UpdatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}", w); // GG-T566: explicit under the invariant culture
                }
            }
            if (newGame) MoreOptions(w);
            // GG-T608: on a new device (or after a reinstall) the empty slots fill from the Play Games cloud.
            if (!newGame && App.Cloud.Available)
            {
                var restore = Ui.MenuRow("Restore from cloud", "", RestoreFromCloud, w);
                restore.name = "load-cloud-restore";
                restore.KindIcon(RowKind.Save);
            }
        }

        /// <summary>GG-T1358: the turn-mode row, folded under a More options row that opens and closes it.</summary>
        private void MoreOptions(VisualElement w)
        {
            VisualElement fold = null;
            Label value = null;
            void Set(bool open)
            {
                fold.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                if (value != null) value.text = open ? "Hide" : "Show";
            }
            var row = Ui.MenuRow("More options", "", () => Set(fold.style.display == DisplayStyle.None), w);
            row.name = "newgame-more-options";
            row.KindIcon(RowKind.Settings);
            value = row.Q<Label>("value");
            fold = Ui.El("col", w);
            TurnModeChoice(fold);
            Set(moreOptions);
        }

        /// <summary>Start Campaign's turn mode (§2.13, GG-T774): "Settings" (the save follows Settings → Turn mode) or one
        /// built mode the save keeps for good. Tapping cycles Settings, then each of <see cref="TurnModes.Available"/>.</summary>
        private void TurnModeChoice(VisualElement w)
        {
            var c = App.Content;
            var profile = App.Settings?.TurnMode ?? TurnMode.PartyWindow;
            if (!TurnModes.IsBuilt(profile)) profile = TurnModes.Available[0];
            TurnMode? locked = turnModeLock is TurnMode m && TurnModes.IsBuilt(m) ? m : (TurnMode?)null;
            TurnMode? next = locked == null ? TurnModes.Available[0]
                : TurnModes.Next(locked.Value) == TurnModes.Available[0] ? (TurnMode?)null : TurnModes.Next(locked.Value);
            string value = locked == null ? $"Settings ({TurnModes.Name(c, profile)})" : TurnModes.Name(c, locked.Value);
            var row = Ui.MenuRow("Turn mode", value, () => App.Router.Replace(new SaveSlotScreen(true, next, true)), w);
            row.name = "newgame-turn-mode";
            row.KindIcon(RowKind.Settings);
            var shown = locked ?? profile;
            var note = Ui.Note(TurnModes.Describe(c, shown) + (locked == null
                ? " Follows your Settings; change it there any time."
                : " Locked: this save plays it until you change Settings → Turn mode."), w);
            note.name = "newgame-turn-mode-text";
        }

        private void RestoreFromCloud()
        {
            App.Toast("Checking the cloud");
            App.Cloud.RestoreEmptySlots(App, restored =>
            {
                App.Toast(restored == 0 ? "No cloud saves for the empty slots" : restored == 1 ? "Restored 1 save from the cloud" : $"Restored {restored} saves from the cloud");
                if (restored > 0) App.Router.Replace(new SaveSlotScreen(false));
            });
        }

        private void Pick(int slot, SaveData existing, bool damaged)
        {
            if (!newGame)
            {
                if (existing == null) return;
                App.Cloud.BeforeLoad(App, slot, existing, chosen =>
                {
                    App.MoveToWallet(slot, chosen); // GG-T702
                    App.Save = chosen; App.Slot = slot;
                    App.ApplySettings();
                    App.Router.ResetTo(StoryBeats.Resume() ?? new HubScreen());
                });
                return;
            }
            if (existing != null)
            {
                Confirm.Show($"Overwrite slot {slot + 1}?", "Overwrite", () => Start(slot, turnModeLock));
                return;
            }
            if (damaged)
            {
                Confirm.Show($"Slot {slot + 1}'s save can't be read. Starting a new campaign here replaces it for good, along with anything it held. Overwrite it?", "Overwrite", () => Start(slot, turnModeLock));
                return;
            }
            Start(slot, turnModeLock);
        }

        /// <summary>A new campaign in <paramref name="slot"/>: the team pick (CharacterSelect, then each hero's look in the
        /// VariantPicker) is the team slice's step, then <see cref="StoryBeats.NewCampaign"/>. The save is named for the
        /// Commander.</summary>
        private static void Start(int slot, TurnMode? turnModeLock)
        {
            App.Router.Push(new CharacterSelectScreen(party =>
            {
                App.Save = Progression.NewGame(App.Content, Commander.Name(null, App.Profile?.Name), party);
                TurnModeSetting.Lock(App.Save, turnModeLock is TurnMode m && TurnModes.IsBuilt(m) ? m : (TurnMode?)null); // §2.13 (GG-T774)
                App.Slot = slot;
                App.MoveToWallet(-1, App.Save); // GG-T702: a new game spends from the account wallet from its first Shard
                App.ApplySettings();
                App.Persist();
                // GG-T532: the party picks its looks before the story starts.
                VariantPicker.ShowPending(() => AskDrills(() => App.Router.ResetTo(StoryBeats.NewCampaign())));
            }));
        }

        /// <summary>GG-T1645: asked once per new campaign, when drills exist: play them, or skip to Camp (every lesson off,
        /// the drills skipped, <see cref="Drills.Skip"/>). Skip is the default once this profile has finished the drills
        /// on any slot (<see cref="SettingsSave.DrillsFinished"/>); Android Back answers the default.</summary>
        private static void AskDrills(Action go)
        {
            var c = App.Content;
            bool any = Drills.NewGameOrder(c).Any(id => c.TryStage(id) != null);
            if (!any || DemoMode.Active) { go(); return; }
            bool veteran = App.Settings?.DrillsFinished == true;
            void Skip()
            {
                Coach.TurnOff(App.Save);
                Drills.Skip(App.Save);
                App.Persist();
                go();
            }
            Confirm.Choose("New to GridGame?",
                veteran ? "You've run the drills before, Commander. Skip them and go straight to Camp, or run them again."
                        : "A few short drills teach the board before the campaign begins. Skip them if you know the game.",
                "Play the drills", RowKind.Enter, go, "Skip to Camp", RowKind.Station, Skip, veteran, "newgame-drills");
        }
    }
}
