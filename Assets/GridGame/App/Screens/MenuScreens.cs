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
    /// <summary>Base for hub sub-screens: background art, header with back + gold, body column.</summary>
    public abstract class HubPage : GameScreen
    {
        /// <summary>The header's currency strip (<see cref="Ui.Currencies"/>, GG-T1361).</summary>
        protected VisualElement CurrencyBar;
        protected VisualElement Body;
        protected abstract string Title { get; }
        protected virtual string Backdrop => "greenvalley";
        /// <summary>Optional line shown under the header title (e.g. station level + next unlock, GG-T35).</summary>
        protected virtual string Subtitle => null;
        /// <summary>The automation name the subtitle label takes (default "header-sub").</summary>
        protected virtual string SubtitleName => null;
        /// <summary>True: the subtitle is the first line of the page body, not under the title, so a long line does not squeeze the header's title and currencies (GG-T1859).</summary>
        protected virtual bool SubtitleInBody => false;
        /// <summary>The Camp NPC who keeps this station (GG-T119), or null: the header gets a Talk button for them.</summary>
        protected virtual string Npc => null;
        /// <summary>The camp tip this page shows (GG-T173): its own Id, unless a tab has its own tip (the Blacksmith's Forge, GG-T177).</summary>
        protected virtual string TipStation => Id;
        public override bool ShowsNav => true;

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, Backdrop, 1);
            bg.style.unityBackgroundImageTintColor = new Color(0.45f, 0.5f, 0.7f, 1f);
            var col = Ui.El("fill col", root);
            // GG-T1335: a page more than one step from the hub also gets Home, back to the hub in one fade.
            var header = Ui.Header(Title, () => App.Router.Back(), col, out CurrencyBar, SubtitleInBody ? null : Subtitle,
                ShowsHubChrome && App.Router.PagesAboveHub != 1 && App.Router.PagesAboveHub != 0 ? () => App.Router.Home() : (Action)null);
            if (SubtitleName != null && header.Q<Label>("header-sub") is Label subLabel) subLabel.name = SubtitleName;
            // Every Camp NPC can be talked to from their own station as well as from Camp's Talk row (GG-T119).
            Button talk = null;
            var topic = Npc != null && CampNpcs.Joined(S, Npc) ? DialogueRunner.TalkTopic(C, S, ContentLoader.NpcFlagPrefix + Npc) : null;
            if (topic != null)
            {
                talk = RowIcon.IconButton(S.SeenDialogues.Contains(topic.Id) ? "Talk" : "Talk!", RowKind.Talk, () => App.Router.Push(new DialogueScreen(topic, (string)null, (GameScreen)null)), "btn--small", null);
                talk.name = "npc-talk";
            }
            // GG-T454 (the menu kit): the kit's page body, the centred column with the kit's gutters (GG-T398).
            Body = Ui.PageBody(col);
            if (SubtitleInBody && Subtitle != null) Ui.Note(Subtitle, Body).name = SubtitleName ?? "header-sub";
            BuildBody(Body);
            Typography.Watch(Body);
            // GG-T1590: the hub's bottom nav under the body.
            if (ShowsHubChrome) Ui.NavBar(col, this);
            // GG-T1361: Gold, Shards and Summon Tickets on every hub page; tapping Shards opens the Shard Store.
            Ui.Currencies(CurrencyBar, App.Save, ShardsTap, talk);
            CampTipOverlay.ShowIfPending(this, TipStation);
        }

        /// <summary>Home and the bottom nav show on the hub's trail; the Shard Store (GG-T1871) also shows them with no hub under it.</summary>
        private bool ShowsHubChrome => App.Save != null && (App.Router.OnHubTrail || this is DiamondStoreScreen);

        /// <summary>What tapping Shards on the currency strip does: open the Shard Store, or nothing on it.</summary>
        protected virtual Action ShardsTap => this is DiamondStoreScreen ? null : () => App.Router.PushOrReturn(new DiamondStoreScreen());

        protected abstract void BuildBody(VisualElement body);

        /// <summary>Builds this page's body inside <paramref name="host"/>'s, as one of its tabs (GG-T1637).</summary>
        internal void BuildAsTab(HubPage host, VisualElement body)
        {
            TabHost = host;
            BuildBody(body);
        }

        /// <summary>Toasts the result and voices it: <paramref name="cue"/> on success (the coin by default; equip, repair
        /// and caravan pass their own, GG-T465), <c>ui.error</c> on a refusal.</summary>
        protected void Done(Result r, bool persist = true, string cue = "ui.buy")
        {
            App.Toast(r.Message ?? (r.Ok ? "Done" : "Can't do that"));
            App.Audio.Sfx(r.Ok ? cue : "ui.error");
            if (r.Ok && persist) App.Persist();
            Refresh();
        }

        protected ContentDb C => App.Content;
        protected SaveData S => App.Save;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Title
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class TitleScreen : GameScreen
    {
        public override string Id => "title";
        /// <summary>GG-T458: campaign.json MomentMusic.title, drawn once per screen.</summary>
        public override string Music => music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Title) ?? "teller-of-the-tales-00";
        private string music;
        /// <summary>GG-T1794: the menu and the Continue card span this share of the content column, so the side margins stay
        /// inside POL-C9's 10% (66 left 17% each side on a phone-framed stage).</summary>
        private const float TitleColumnPct = 84f;

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, "cityruins", 0);
            var shade = Ui.El("fill", root);
            shade.style.backgroundColor = Palette.Ink.WithA(0.45f);

            staged.Clear();
            var col = Ui.El("fill col center", root);
            // GG-T1350 (§11.2): the Main Menu's code-drawn wordmark, Cinzel over a mana conduit underline.
            var title = Ui.Display(GameApp.Title.ToUpperInvariant(), col, "shadow");
            title.name = "title-wordmark";
            Stage(title);
            var conduit = Ui.El("title-conduit", col);
            conduit.name = "title-conduit";
            conduit.style.width = Fit.C(56f);
            Stage(conduit);
            var tag = Ui.Text("Slide. Flank. Strike.", "h2 shadow dim", col);
            Ui.Margin(tag, top: Space.S16, bottom: Fit.H(4f));
            Stage(tag);

            int recent = App.Saves.MostRecent();
            Debug.Log($"[Title] most recent save slot: {recent}");
            var save = recent >= 0 ? App.Saves.Load(recent, App.Content) : null;
            if (save != null) Stage(ContinueCard(save, recent, col));

            var menu = Ui.Panel(col);
            menu.style.width = Fit.C(TitleColumnPct); // GG-T1794
            Stage(menu);
            // GG-T738 (§26.7): each Title row leads with its kind icon.
            var start = Ui.MenuRow("Start Campaign", "", SaveSlotScreen.StartCampaign, menu);
            start.name = "title-start";
            start.KindIcon(RowKind.New);
            var load = Ui.MenuRow("Load", "", () => App.Router.Push(new SaveSlotScreen(false)), menu, null, recent >= 0 || App.Cloud.Available); // GG-T608: a new device restores from the cloud there
            load.name = "title-load";
            load.KindIcon(RowKind.Save);
            // GG-T99 (§15.4): the player profile owns the settings and these three slots; switch / create / rename / delete here.
            var profile = Ui.MenuRow("Profile", App.Profile?.Name ?? "", () => App.Router.Push(new ProfileSelectScreen()), menu);
            profile.name = "title-profile";
            profile.KindIcon(RowKind.Hero);
            profile.Q<Label>("value").AddToClassList("gold");
            var settings = Ui.MenuRow("Settings", "", () => App.Router.Push(new SettingsScreen()), menu);
            settings.name = "title-settings";
            settings.KindIcon(RowKind.Settings);
            var about = Ui.MenuRow("About", "", () => App.Router.Push(new AboutScreen()), menu);
            about.name = "title-about";
            about.KindIcon(RowKind.Bestiary);
            var credits = Ui.MenuRow("Credits", "", () => App.Router.Push(new CreditsScreen()), menu);
            credits.name = "title-credits";
            credits.KindIcon(RowKind.Bestiary);
            Ui.SelectFirst(menu);

            var ver = Ui.Text($"v{Application.version}", "tiny dim corner-tag", root); // GG-T1794: POL-C9 skips corner tags
            ver.style.position = Position.Absolute;
            ver.style.right = Space.S32; ver.style.bottom = Space.S24;
        }

        /// <summary>The Continue card (§11.2): the Commander, the stage Continue leads to, play time, when it was last
        /// played and the party's faces; one tap resumes.</summary>
        private VisualElement ContinueCard(SaveData s, int slot, VisualElement parent)
        {
            var card = Ui.El("card card-tap col title-continue", parent);
            card.name = "title-continue";
            card.style.width = Fit.C(TitleColumnPct);
            Fonts.UseHeavy(Ui.Text("Continue", "h2", card));
            Ui.Caption(Commander.Name(s, App.Profile?.Name), card, "gold");
            var next = HubScreen.NextStage(App.Content, s);
            var stage = next != null ? App.Content.TryStage(next)?.Name : null;
            if (stage != null) Ui.Caption(stage, card);
            Ui.Caption($"{TimeSpan.FromSeconds(s.PlaySeconds):h\\:mm} played · {s.UpdatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}", card, "dim"); // GG-T566: explicit under the invariant culture
            var faces = Ui.El("row", card);
            Ui.Margin(faces, top: Space.S8);
            foreach (var id in s.Party)
            {
                if (s.Hero(id) == null) continue;
                Ui.Margin(Ui.Thumb(id, faces, Kit.Avatar * 0.6f), right: Space.S8);
            }
            Ui.Tappable(card, () => Continue(slot), "ui.select");
            return card;
        }

        private readonly List<VisualElement> staged = new List<VisualElement>();
        private bool entered;

        private void Stage(VisualElement e)
        {
            staged.Add(e);
            if (!entered && !Tween.Calm) e.style.opacity = 0f;
        }

        public override void OnShown()
        {
            if (entered) return;
            entered = true;
            if (!Tween.Calm) App.StartCoroutine(Entrance(staged.ToList()));
        }

        /// <summary>The staged entrance (GG-T1350): the wordmark rises, the conduit charges outward with the orb cue, then the
        /// tagline, the Continue card and the rows follow each with a soft tick. Reduce Motion shows everything at once.</summary>
        private IEnumerator Entrance(List<VisualElement> parts)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                var e = parts[i];
                if (e.name == "title-conduit")
                {
                    App.Audio.Sfx("battle.orb");
                    yield return Tween.Run(Timing.Quick * 2f, k => { e.style.opacity = k; e.style.scale = new Scale(new Vector2(k, 1f)); }, Tween.EaseOut);
                    continue;
                }
                if (i > 0) App.Audio.Sfx("ui.click");
                yield return Tween.Run(Timing.Quick, k => { e.style.opacity = k; e.style.translate = new Translate(0, Mathf.Lerp(30f, 0f, k)); }, Tween.EaseOut);
            }
        }

        private void Continue(int slot)
        {
            Debug.Log($"[Title] continue slot {slot}");
            var s = App.Saves.Load(slot, App.Content);
            if (s == null) { App.Toast("That save is damaged"); return; }
            // GG-T608: a newer cloud copy of this slot is offered first (no-op without a signed-in cloud).
            App.Cloud.BeforeLoad(App, slot, s, chosen =>
            {
                App.MoveToWallet(slot, chosen); // GG-T702: every load (a cloud copy too) moves into the account wallet
                App.Save = chosen; App.Slot = slot;
                App.ApplySettings();
                App.Router.ResetTo(StoryBeats.Resume() ?? StoryBeats.WithChapter(new HubScreen())); // GG-T1480: e.g. ch05, held past the ending's credits
            });
        }
    }

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
                // A slot whose file (and backup) exist but won't read is damaged, not empty: it may hold paid Diamonds.
                bool damaged = s == null && App.Saves.Exists(i);
                string label = s != null ? $"Slot {i + 1} — {s.Name}" : damaged ? $"Slot {i + 1} — Damaged" : $"Slot {i + 1} — Empty";
                string value = s == null ? "" : $"Stage {s.HighestClearedStage + 2}  {Ui.Gold(s.Gold)}";
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
            bool any = c.Campaign?.Tutorial?.Any(id => c.TryStage(id) != null) == true;
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

    /// <summary>
    /// Fighting-game-style roster pick (owner request: "a grid at the bottom, select a tile, the full
    /// artwork appears on top, lock in that character"). Every hero class is open here regardless of
    /// normal unlocks; <see cref="PartySize"/> picks build the small starting party — the rest of the six join
    /// at story beats (GG-T118). <c>-demo</c> drives the same screen by tapping random tiles instead of a
    /// human, so each attract-mode run shows a different squad.
    /// </summary>
    public sealed class CharacterSelectScreen : GameScreen
    {
        public const int PartySize = Progression.StartingPartySize;
        private readonly Action<List<string>> onDone;
        private readonly bool auto;
        private readonly List<string> locked = new List<string>();
        private string highlighted;
        private bool finishing;
        private Coroutine finishRoutine;
        private VisualElement partyStrip, revealPanel, grid;
        private Button lockBtn;

        /// <summary>Without this, backing out during the 0.7s Lock-In fanfare (the header's Back button has no
        /// `finishing` guard) left Finish() running: it still fired onDone() afterward, silently starting/
        /// overwriting a save and jumping to the Hub out from under a player who'd just tried to cancel —
        /// the same failure class as LevelUpScreen's fanfare coroutine (GG-T127).</summary>
        public override void OnHidden()
        {
            if (finishRoutine != null) App.StopCoroutine(finishRoutine);
            finishRoutine = null;
            if (autoRoutine != null) App.StopCoroutine(autoRoutine);
            autoRoutine = null;
        }

        /// <summary>The -demo picker: one per screen (a resize rebuild must not start a second one), stopped on hide.</summary>
        private Coroutine autoRoutine;

        public CharacterSelectScreen(Action<List<string>> onDone, bool auto = false)
        {
            this.onDone = onDone;
            this.auto = auto || DemoMode.Active;
        }

        public override string Id => "charselect";
        public override string Music => music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.CharSelect) ?? "minstrel-guild-00";
        private string music;

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, "cityruins", 0);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col", root);
            Ui.Header("Choose Your Party", () => App.Router.Back(), col, out _);
            var hint = Ui.Text($"Pick {PartySize} to set out with: tap a hero to preview, tap again to lock in. More join along the road.", "small dim", col);
            hint.name = "charselect-hint";
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.whiteSpace = WhiteSpace.Normal; // the how-to lives here, not in the grid title (GG-T403)

            partyStrip = Ui.El("row", col);
            partyStrip.style.justifyContent = Justify.Center;
            partyStrip.style.marginTop = Space.S8; partyStrip.style.marginBottom = Space.S8;

            revealPanel = Ui.Window(null, col);
            revealPanel.style.minHeight = 420;
            Fit.Center(revealPanel);

            lockBtn = RowIcon.IconButton("Lock In", RowKind.Confirm, LockInHighlighted, "btn--primary", col);
            lockBtn.style.alignSelf = Align.Center;
            lockBtn.style.width = Size.Card;
            lockBtn.style.marginBottom = Space.S8;

            var gridWindow = Ui.Window("Every hero", col);
            gridWindow.style.flexGrow = 1;
            Fit.Center(gridWindow);
            grid = Ui.List(gridWindow).contentContainer;
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.Center;
            foreach (var def in App.Content.Classes.Values.Where(d => d.Team == Team.Hero && !d.IsCut).OrderBy(d => d.Name))
            {
                var id = def.Id;
                var t = Ui.Thumb(id, grid, 164, thumbOnly: true); // GG-T1210: 100+ cards, baked thumbs only
                t.name = "tile-" + id;
                t.style.marginLeft = t.style.marginRight = t.style.marginTop = t.style.marginBottom = Space.S8;
                t.AddToClassList("card-tap");
                Ui.Tappable(t, () => Pick(id));
            }

            RefreshState();
            if (auto && autoRoutine == null) autoRoutine = App.StartCoroutine(AutoPlay());
        }

        private void Pick(string id)
        {
            if (finishing || locked.Contains(id)) return;
            if (highlighted == id) { LockInHighlighted(); return; }
            highlighted = id;
            App.Audio.Sfx("ui.select");
            RefreshState();
        }

        private void LockInHighlighted()
        {
            if (finishing || highlighted == null || locked.Contains(highlighted)) return;
            locked.Add(highlighted);
            highlighted = null;
            App.Audio.Sfx("ui.buy");
            RefreshState();
            if (locked.Count >= PartySize) { finishing = true; finishRoutine = App.StartCoroutine(Finish()); }
        }

        private IEnumerator Finish()
        {
            yield return Tween.Wait(Timing.Dramatic);
            onDone(new List<string>(locked));
        }

        /// <summary>Bombastic reveal (owner request: "slam into place similar to the Portraits"): oversized and
        /// tilted, punches down past full size with a back-ease overshoot, then a small settle shake.</summary>
        private IEnumerator SlamIn(VisualElement target)
        {
            target.style.scale = new Scale(Vector2.one * 1.5f);
            target.style.rotate = new Rotate(-6f);
            App.Audio.Sfx("battle.shatter", 0.55f);
            yield return Tween.Run(Timing.Standard, k =>
            {
                float s = Mathf.LerpUnclamped(1.5f, 1f, Tween.Back(k));
                target.style.scale = new Scale(Vector2.one * s);
                target.style.opacity = Mathf.Clamp01(k * 2.2f);
                target.style.rotate = new Rotate(Mathf.LerpUnclamped(-6f, 0f, k));
            });
            target.style.scale = new Scale(Vector2.one);
            target.style.opacity = 1f;
            target.style.rotate = new Rotate(0f);
            yield return Tween.Shake(target, 6f, 0.14f);
        }

        private IEnumerator AutoPlay()
        {
            var rng = new System.Random();
            while (!finishing && locked.Count < PartySize)
            {
                yield return Tween.Wait(Timing.Emphasis);
                var pool = App.Content.Classes.Values.Where(d => d.Team == Team.Hero && !d.IsCut && !locked.Contains(d.Id)).ToList();
                if (pool.Count == 0) yield break;
                Pick(pool[rng.Next(pool.Count)].Id);
                yield return Tween.Wait(Timing.Dramatic);
                LockInHighlighted();
            }
        }

        private void RefreshState()
        {
            partyStrip.Clear();
            for (int i = 0; i < PartySize; i++)
            {
                var slot = Ui.El("col", partyStrip);
                slot.style.width = Size.PartySlot; slot.style.marginLeft = slot.style.marginRight = Space.S8; slot.style.alignItems = Align.Center;
                if (i < locked.Count)
                {
                    Ui.Thumb(locked[i], slot, 96);
                    var nm = Ui.Text(App.Content.Class(locked[i]).Name, "tiny", slot);
                    nm.style.unityTextAlign = TextAnchor.MiddleCenter;
                }
                else
                {
                    var empty = Ui.El("thumb", slot);
                    empty.style.width = empty.style.height = Size.Avatar;
                    empty.style.opacity = 0.3f;
                    // Reserve the name line so the strip doesn't grow (and shove the page down) on the first lock-in.
                    Ui.Text(" ", "tiny", slot);
                }
            }

            revealPanel.Clear();
            var showId = highlighted ?? (locked.Count > 0 && !finishing ? locked[locked.Count - 1] : null);
            if (finishing) { Ui.Text("Party complete!", "h1 shadow gold", revealPanel).style.unityTextAlign = TextAnchor.MiddleCenter; }
            else if (showId != null)
            {
                var def = App.Content.Class(showId);
                var row = Ui.El("row", revealPanel);
                row.style.opacity = 0f; // slammed into place below, not swapped in cold
                var frame = Ui.El(null, row);
                frame.style.width = Size.PortraitW; frame.style.height = Size.PortraitH;
                frame.style.borderTopWidth = frame.style.borderBottomWidth = frame.style.borderLeftWidth = frame.style.borderRightWidth = 4;
                var roleColor = Ui.RoleColor(def.Role);
                frame.style.borderTopColor = frame.style.borderBottomColor = frame.style.borderLeftColor = frame.style.borderRightColor = roleColor;
                frame.style.backgroundColor = Palette.Ink.WithA(0.6f);
                frame.style.backgroundImage = new StyleBackground(Art.PortraitFigure(showId));
                frame.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var info = Ui.El("col", row);
                info.style.marginLeft = Space.S24; info.style.flexGrow = 1;
                var nm = Ui.Text(def.Name, "h1 shadow", info);
                Fonts.UseDisplay(nm);
                Ui.RoleBadge(def.Role, info).style.marginBottom = Space.S8;
                var desc = GlossaryText.Create(def.Description ?? "", "small dim", info);
                desc.style.whiteSpace = WhiteSpace.Normal;
                var sup = GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(App.Content, def.Support), "small gold", info); // GG-T298
                sup.style.whiteSpace = WhiteSpace.Normal; sup.style.marginTop = Space.S8;
                if (locked.Contains(showId)) Ui.Text("Locked in", "small good", info).style.marginTop = Space.S8;
                App.StartCoroutine(SlamIn(row));
            }
            else Ui.Text("Tap a hero below to preview them.", "h2 dim", revealPanel).style.unityTextAlign = TextAnchor.MiddleCenter;

            foreach (var child in grid.Children())
            {
                if (!child.name.StartsWith("tile-")) continue;
                var id = child.name.Substring(5);
                bool isLocked = locked.Contains(id);
                child.SetEnabled(!isLocked && !finishing);
                child.style.opacity = isLocked ? 0.25f : (highlighted == id ? 1f : 0.85f);
                child.EnableInClassList("thumb--selected", highlighted == id);
            }
            Ui.SetEnabled(lockBtn, !finishing && highlighted != null && !locked.Contains(highlighted));
        }
    }

    /// <summary>Modal confirm dialog on the overlay layer.</summary>
    public static class Confirm
    {
        private const string VeilName = "confirm-veil";

        /// <summary>Returns the veil, so a caller can tell whether its question is still up.</summary>
        public static VisualElement Show(string question, string yes, Action onYes, Action onNo = null)
        {
            var overlay = GameApp.I.Overlay;
            var veil = Ui.El("fill veil col center", overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Panel(veil);
            w.style.width = Length.Percent(76);
            var q = Ui.Heading(question, w);
            q.style.whiteSpace = WhiteSpace.Normal;
            var row = Ui.Actions(w);
            row.style.justifyContent = Justify.FlexEnd;
            Action cancel = () => { veil.RemoveFromHierarchy(); onNo?.Invoke(); };
            veil.userData = cancel; // Android Back answers Cancel (CancelTop)
            // GG-T738 (§26.7): Cancel leads with the Close cross, the answer with the Confirm check.
            RowIcon.IconButton("Cancel", RowKind.Close, cancel, null, row).name = "confirm-no";
            RowIcon.IconButton(yes, RowKind.Confirm, () => { veil.RemoveFromHierarchy(); onYes?.Invoke(); }, "btn--primary", row).name = "confirm-yes";
            GameApp.I.Audio.Sfx("ui.select");
            if (!Tween.Calm) { veil.style.opacity = 0f; GameApp.I.StartCoroutine(Tween.Fade(veil, 0f, 1f, Timing.Quick)); }
            return veil;
        }

        /// <summary>GG-T1645: a question with two answers and no Cancel. <paramref name="secondIsDefault"/> picks which one is
        /// primary and which Android Back answers. It sounds as it opens and fades in, held still under Reduce Motion.</summary>
        public static VisualElement Choose(string question, string note, string first, RowKind firstKind, Action onFirst,
            string second, RowKind secondKind, Action onSecond, bool secondIsDefault, string name = null)
        {
            var app = GameApp.I;
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Panel(veil, null, false, name);
            w.style.width = Length.Percent(76);
            Ui.Heading(question, w).style.whiteSpace = WhiteSpace.Normal;
            if (!string.IsNullOrEmpty(note)) Ui.Note(note, w);
            var row = Ui.Actions(w);
            row.style.justifyContent = Justify.FlexEnd;
            Action a = () => { veil.RemoveFromHierarchy(); onFirst?.Invoke(); };
            Action b = () => { veil.RemoveFromHierarchy(); onSecond?.Invoke(); };
            veil.userData = secondIsDefault ? b : a;
            RowIcon.IconButton(first, firstKind, a, secondIsDefault ? null : "btn--primary", row).name = "choose-first";
            RowIcon.IconButton(second, secondKind, b, secondIsDefault ? "btn--primary" : null, row).name = "choose-second";
            app.Audio.Sfx("ui.select");
            if (!Tween.Calm) { veil.style.opacity = 0f; app.StartCoroutine(Tween.Fade(veil, 0f, 1f, Timing.Quick)); }
            return veil;
        }

        /// <summary>GG-T1401: a card with one answer, which Android Back also gives. It chimes as it opens, fades in and pops
        /// its title, held still under Reduce Motion.</summary>
        public static VisualElement Notice(string kicker, string title, string text, string ok, RowKind okKind, Action onOk, string name)
        {
            var app = GameApp.I;
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Panel(veil, null, false, name);
            w.style.width = Length.Percent(76);
            Ui.Caption(kicker, w, "gold");
            var head = Ui.Heading(title, w);
            head.style.whiteSpace = WhiteSpace.Normal;
            Ui.Text(text, "kit-text", w).style.whiteSpace = WhiteSpace.Normal;
            var row = Ui.Actions(w);
            row.style.justifyContent = Justify.FlexEnd;
            Action a = () => { veil.RemoveFromHierarchy(); onOk?.Invoke(); };
            veil.userData = a;
            RowIcon.IconButton(ok, okKind, a, "btn--primary", row).name = "notice-ok";
            app.Audio.Sfx("battle.powerup");
            if (!Tween.Calm)
            {
                veil.style.opacity = 0f;
                app.StartCoroutine(Tween.Fade(veil, 0f, 1f, Timing.Quick));
                app.StartCoroutine(Tween.Pop(head, 1.15f, Timing.Emphasis));
            }
            return veil;
        }

        /// <summary>Android Back with a question up: answers the topmost one Cancel. False when none is open.</summary>
        public static bool CancelTop()
        {
            var overlay = GameApp.I?.Overlay;
            if (overlay == null) return false;
            var veils = overlay.Query(VeilName).ToList();
            if (veils.Count == 0) return false;
            var top = veils[veils.Count - 1];
            if (top.userData is Action cancel) cancel(); else top.RemoveFromHierarchy();
            return true;
        }

        /// <summary>A confirm veil answers a question about the screen it was raised on — if that screen
        /// changes from under it (any navigation, or an automation call that swaps screens directly) with
        /// the veil never explicitly answered, it would otherwise stay in Overlay forever: a full-screen
        /// PickingMode.Position layer silently eating every future click with no error anywhere (the real
        /// root cause behind GG-T16 — not an Input System/UI Toolkit staleness bug as first suspected).
        /// ScreenRouter calls this on every navigation so a stale veil can never outlive its screen.</summary>
        public static void DismissStale()
        {
            var overlay = GameApp.I?.Overlay;
            if (overlay == null) return;
            foreach (var veil in overlay.Query(VeilName).ToList()) veil.RemoveFromHierarchy();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Camp hub — the FF-style main menu (merged hub, bible §25.9)
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class HubScreen : GameScreen
    {
        public override string Id => "hub";
        /// <summary>GG-T456: the Camp's peaceful track, redrawn after each trip to the field.</summary>
        public override string Music => App.Playlist.Hub(App.Content.Campaign) ?? "minstrel-guild-00";

        protected override void Build(VisualElement root)
        {
            var c = App.Content; var s = App.Save;
            // Paywall jobs left running when the app closed land now (§25.18), unless one is playing out on screen.
            if (App.Overlay.Q(TimedJobView.VeilName) == null)
                foreach (var done in Paywall.FinishReady(c, s, DateTime.UtcNow)) { App.Toast(done.Message ?? "Done"); App.Persist(); }
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, ThemeFor(c, s), 2);
            bg.style.unityBackgroundImageTintColor = Palette.TextDim;

            var col = Ui.El("fill col", root);
            // Camp has no header: its windows keep the kit's gutters (Kit.Gutter at the sides, Kit.Gap at the bottom).
            col.style.paddingTop = Space.S48; col.style.paddingLeft = col.style.paddingRight = Kit.Gutter - Space.S8; col.style.paddingBottom = Kit.Gap - Space.S8;

            // Party window (portraits, level, HP): a 2-column grid so a full party of six fits above the menu.
            // GG-T313: built as explicit two-card rows, not a flex-wrap of six cards sized by a reactive
            // GeometryChangedEvent pixel hack. That hack (party.contentRect.width-driven, with a 1px slack
            // fudge) measured correctly but still wrapped every card into a single column at the 2:3/3:4 tablet
            // frame (confirmed live: a hand-built 6-hero save showed 1 column there and 2 everywhere else on
            // an otherwise identical 1424x2532 stage) - some layout pass during that frame transform resolved
            // the wrap decision against different available width than the one the callback later measured.
            // A row that can only ever hold two cards has no wrap arithmetic to get wrong.
            var party = Ui.Window(null, col);
            party.name = "camp-party";
            // Edge = gap = 24 (row padding 12 + card margin 12): even gutters at every width (POL-C8), and
            // 24 px between the tappable cards (POL-C3). The 12 sits on each card row, not the window, so the row's
            // own box carries it.
            party.style.paddingLeft = party.style.paddingRight = Space.S0;
            var ids = s.Party;
            // Two cards abreast on a phone; three on a 2:3 / 3:4 tablet, which has the width for them (GG-T398).
            int perRow = Fit.Roomy ? 3 : 2;
            for (int i = 0; i < ids.Count; i += perRow)
            {
                var row = Ui.El("row", party);
                row.style.paddingLeft = row.style.paddingRight = Space.TargetHalfGap;
                for (int j = i; j < Math.Min(i + perRow, ids.Count); j++)
                {
                    var id = ids[j];
                    var h = s.Hero(id);
                    if (h == null) continue;
                    var u = Progression.Preview(c, h);
                    var card = Ui.El("card", row);
                    card.name = "camp-hero-" + id;
                    // Two cards split the row exactly in half; a lone trailing card keeps its half (a hidden twin, below).
                    card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = Length.Percent(0);
                    card.style.marginTop = card.style.marginBottom = card.style.marginLeft = card.style.marginRight = Space.TargetHalfGap;
                    card.style.paddingTop = card.style.paddingBottom = card.style.paddingLeft = card.style.paddingRight = Space.S8;
                    var thumb = Ui.Thumb(id, card, Kit.Avatar);
                    thumb.style.marginRight = Space.S16;
                    var info = Ui.El("col grow", card);
                    info.style.flexShrink = 1; info.style.minWidth = 0;
                    var top = Ui.El("row", info);
                    // A compact card (kit, GG-T454): name and meta caption size, the name heavy — the Heroes roster strip's look.
                    var name = Ui.Caption(c.Class(id).Name, top, "t-heavy");
                    Ui.Marquee(name);
                    // The name has its row to itself, so a long one is never cut by the badge (GG-T395).
                    name.style.flexGrow = 1; name.style.flexShrink = 1; name.style.minWidth = 0;
                    var meta = Ui.El("row", info);
                    meta.style.alignItems = Align.Center;
                    Ui.RoleBadge(Ui.RoleOf(h, c.Class(id)), meta).style.marginLeft = Space.S0;
                    Ui.Caption($"Lv {u.Level}", meta, "gold").style.marginLeft = Space.S8;
                    int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp;
                    Ui.Gauge(hp / (float)u.MaxHp, info, 300).style.marginTop = Space.S8;
                    Ui.Caption($"{hp}/{u.MaxHp}", info, "dim");
                    MercenaryCamp.Tag(s, id, info);
                    // A short last row's cards keep their column width: invisible twins hold the empty columns, so the
                    // window stays an even grid with even gutters (POL-C8) at every party size.
                    for (int gap = j == ids.Count - 1 ? (perRow - ids.Count % perRow) % perRow : 0; gap > 0; gap--)
                    {
                        var spacer = Ui.El("card", row);
                        spacer.style.flexGrow = 1; spacer.style.flexShrink = 1; spacer.style.flexBasis = Length.Percent(0);
                        spacer.style.marginTop = spacer.style.marginBottom = spacer.style.marginLeft = spacer.style.marginRight = Space.TargetHalfGap;
                        spacer.style.visibility = Visibility.Hidden;
                        spacer.pickingMode = PickingMode.Ignore;
                    }
                    card.AddToClassList("card-tap");
                    Ui.Tappable(card, () => App.Router.Push(new StatusScreen(id)), "ui.select");
                }
            }

            var dests = CampMenu.Build(c, s, App.Settings);
            CampDestination Dest(string id) => dests.FirstOrDefault(d => d.Id == id);
            // The Inn and the Respec sit with the party they heal and refund (GG-T1612).
            var inn = Dest(CampMenu.Inn);
            if (inn != null)
            {
                var innRow = Ui.MenuRow(inn.Label, inn.Value, inn.Open ? (Action)Rest : null, party, null, inn.Open);
                innRow.name = (inn.Open ? "camp-" : "locked-") + "inn";
                innRow.KindIcon(inn.Open ? RowKind.Keeper : RowKind.Locked, inn.Open ? CampNpcs.Innkeeper + "-00" : null);
                if (!inn.Open) Subline(innRow, inn.Hint);
            }
            var respec = Dest(CampMenu.Respec);
            if (respec != null)
            {
                var respecRow = Ui.MenuRow(respec.Label, respec.Value, () => App.Router.Push(new RespecScreen()), party);
                respecRow.name = "camp-respec";
                respecRow.KindIcon(RowKind.Keeper, CampNpcs.Innkeeper + "-00");
            }

            var body = new ScrollView();
            body.style.flexGrow = 1;
            col.Add(body);
            var page = body.contentContainer;

            // Campaign card: Continue launches the next stage straight away; the Adventure row opens Stage select.
            var campaign = Ui.Window(null, page);
            campaign.name = "camp-campaign";
            string next = NextStage(c, s);
            var cont = Ui.Button(next != null ? "Continue" : "Adventure", () =>
            {
                if (next != null) StageLauncher.Launch(next, false);
                else App.Router.Push(new StageSelectScreen());
            }, null, campaign);
            cont.name = "camp-continue";
            if (next != null)
            {
                var st = c.TryStage(next);
                if (st != null) Ui.Caption(st.Name, campaign, "gold");
                if (!RestedPool.Exempt(s)) Ui.Caption($"Costs {RestedPool.LaunchCost} Rested", campaign, "dim");
            }
            if (TravelRules.OnTheRoad(s)) // GG-T1786: travel (§28) replaces the expedition line
                Ui.Caption(TravelLine(s), campaign, "dim").name = "camp-travel";
            var adv = Dest(CampMenu.Campaign);
            var advRow = Ui.MenuRow(adv?.Label ?? "Adventure", adv?.Value ?? "", () => App.Router.Push(new StageSelectScreen()), campaign);
            advRow.name = "camp-adventure";
            advRow.KindIcon(RowKind.Place, ThemeFor(c, s));
            Ui.Caption($"{Ui.Gold(s.Gold)} gold · {TimeSpan.FromSeconds(s.PlaySeconds):h\\:mm\\:ss} · Cleared {s.ClearedStages.Count(id => c.Stages.ContainsKey(id) && !c.Stage(id).IsTest)}/{c.CampaignStages().Count}", campaign, "dim");
            if (s.NewGamePlus > 0) Ui.Caption($"New Game+ {s.NewGamePlus}", campaign, "gold");
            var ngp = Dest(CampMenu.NewGamePlus);
            if (ngp != null)
            {
                var ngpRow = Ui.MenuRow(ngp.Label, "", () => Confirm.Show("Start New Game+? Keep your heroes, gear and gold; the campaign restarts with enemies 10 levels stronger.", "Begin", () =>
                {
                    var r = Progression.StartNewGamePlus(App.Content, App.Save);
                    App.Toast(r.Message);
                    if (r.Ok) { App.Persist(); Refresh(); }
                }), campaign);
                ngpRow.name = "camp-newgameplus";
                ngpRow.KindIcon(RowKind.NewGamePlus).parent.Q<Label>("label").AddToClassList("gold");
            }
            RestedGate.PanelIn(campaign, Refresh);

            // Crucible card (§22.10): endless waves; closed mid-expedition or before its milestone, saying why.
            var crucible = Dest(CampMenu.Crucible);
            if (crucible != null)
            {
                var card = Ui.Window(null, page);
                var row = Ui.MenuRow(crucible.Label, crucible.Value, crucible.Open ? (Action)(() => App.Router.Push(new CrucibleScreen())) : null, card, null, crucible.Open);
                row.name = crucible.Open ? "camp-crucible" : "locked-crucible";
                row.KindIcon(crucible.Open ? RowKind.Crucible : RowKind.Locked);
                if (!crucible.Open) Subline(row, crucible.Hint);
            }

            // Every other destination is a tile, named for its old Camp row. Craft and Journal collapse into one group tile
            // each (camp-craft / camp-journal, locked-* when nothing in the group is open) that opens a CampGroupScreen of
            // their members (GG-T1613). The members' old names live on as hidden full-size markers inside the group tile, so
            // camp tips, the coach and the harness still find them on the hub:
            //   camp-craft:   camp-items, camp-blacksmith, camp-alchemist, camp-manasmith, camp-enchantress, camp-caravan
            //   camp-journal: camp-quests, camp-bounties, camp-mercenaries, camp-talk, camp-codex, camp-bestiary
            // (each locked-<name> while that member is closed).
            var grid = Ui.El("kit-pillar", page);
            grid.name = "camp-tiles";
            var groups = new Dictionary<CampGroup, VisualElement>();
            foreach (var d in dests)
            {
                switch (d.Id)
                {
                    case CampMenu.Campaign: case CampMenu.Crucible: case CampMenu.Inn: case CampMenu.Respec: case CampMenu.NewGamePlus: case CampMenu.Quit: continue;
                }
                if (d.Group == CampGroup.Craft || d.Group == CampGroup.Journal)
                {
                    if (!groups.TryGetValue(d.Group, out var groupTile)) groups[d.Group] = groupTile = GroupTile(grid, dests, d.Group);
                    var marker = Ui.El(null, groupTile);
                    marker.name = TileName(d);
                    marker.style.position = Position.Absolute;
                    marker.style.left = marker.style.top = marker.style.right = marker.style.bottom = 0;
                    marker.style.visibility = Visibility.Hidden;
                    marker.pickingMode = PickingMode.Ignore;
                    continue;
                }
                Tile(grid, d);
            }
            var quit = Ui.El("kit-tile", grid);
            quit.name = "camp-save-&-quit";
            quit.KindIcon(RowKind.Quit);
            Ui.Marquee(Ui.Text("Save & Quit", "kit-tile-label", quit));
            Ui.Tappable(quit, () => Confirm.Show("Save and return to the title?", "Quit", ToTitle), "ui.select");
            Ui.Select(cont);

            // GG-T75 (§25.12.1): once the Caravan station is open, a rotating one-line crew bark under the tiles.
            if (Features.Caravan && CampUnlocks.CaravanOpen(s))
            {
                var gossip = Ui.Panel(page, null, false, "camp-gossip");
                Ui.Caption("Around the wagons", gossip, "dim");
                CaravanScreen.BarkLine(c, s, gossip);
            }

            // GG-T518: a closed destination's hint sits whole, wrapping if it must, in small type under the row's name.
            void Subline(VisualElement row, string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                var name = row.Q<Label>("label");
                var stack = Ui.El("col", row);
                stack.style.flexGrow = 1f; stack.style.flexShrink = 1f;
                row.Insert(row.IndexOf(name), stack);
                stack.Add(name);
                var sub = Ui.Text(text, "small dim", stack);
                sub.name = "hint";
                sub.style.whiteSpace = WhiteSpace.Normal;
            }
            MercenaryCamp.OnCamp(this);
            CampCoach.OnHub(this); // GG-T539: a vendor who just joined, or a first piece of gear to put on, gets its lesson
        }

        /// <summary>The stage Continue launches: the first uncleared campaign stage. Travel (§28, GG-T1786) has no fixed
        /// battle order inside a region, so a stage mid-region is fought directly once it is unlocked.</summary>
        internal static string NextStage(ContentDb c, SaveData s) =>
            c.CampaignStages().FirstOrDefault(st => !s.ClearedStages.Contains(st.Id))?.Id;

        /// <summary>GG-T1786: "On the road · N00 mana carried": the caravan has set out and has not made camp.</summary>
        internal static string TravelLine(SaveData s)
        {
            int n = TravelRules.StartOrbs(s);
            return "On the road · " + (n == 0 ? "no mana carried" : $"{n * 100} mana to every spell carried");
        }

        /// <summary>A destination's tile name: camp-&lt;old row&gt; when open, locked-&lt;old row&gt; when closed.</summary>
        internal static string TileName(CampDestination d) =>
            (d.Open ? "camp-" : "locked-") + (d.Id == CampMenu.Diamonds ? "diamonds" : d.Label.ToLowerInvariant().Replace(' ', '-'));

        /// <summary>One destination's tile in <paramref name="grid"/>: opens its screen, or toasts why it's closed.</summary>
        internal static VisualElement Tile(VisualElement grid, CampDestination d)
        {
            var tile = Ui.El("kit-tile", grid);
            tile.name = TileName(d);
            tile.EnableInClassList("kit-tile--locked", !d.Open);
            var icon = d.Npc != null ? tile.KindIcon(RowKind.Keeper, d.Npc + "-00") : tile.KindIcon(TileKind(d.Id));
            if (!d.Open) RowIcon.Badge(icon, RowKind.Locked);
            else if (d.Badge) RowIcon.Badge(icon, RowKind.New);
            Ui.Marquee(Ui.Text(d.Label, "kit-tile-label", tile));
            if (!string.IsNullOrEmpty(d.Value)) Ui.Text(d.Value, "kit-tile-value", tile).name = "value";
            else if (!d.Open && !string.IsNullOrEmpty(d.Hint)) Ui.Text(d.Hint, "kit-tile-value", tile).name = "hint";
            string id = d.Id, hint = d.Hint;
            if (d.Open) Ui.Tappable(tile, () => { var dest = Ui.NavDestination(App.Save, id); if (dest != null) App.Router.PushOrReturn(dest); }, "ui.select");
            else Ui.Tappable(tile, () => App.Toast(hint), "ui.error");
            return tile;
        }

        /// <summary>The Craft or Journal tile: open if any member is, badged if any member waits, else dimmed with the
        /// group's first hint; it opens the group's <see cref="CampGroupScreen"/>.</summary>
        private static VisualElement GroupTile(VisualElement grid, List<CampDestination> dests, CampGroup group)
        {
            bool open = CampMenu.GroupOpen(dests, group);
            string label = group == CampGroup.Craft ? "Craft" : "Journal", hint = CampMenu.GroupHint(dests, group);
            var tile = Ui.El("kit-tile", grid);
            tile.name = (open ? "camp-" : "locked-") + label.ToLowerInvariant();
            tile.EnableInClassList("kit-tile--locked", !open);
            var icon = tile.KindIcon(group == CampGroup.Craft ? RowKind.Station : RowKind.Codex);
            if (!open) RowIcon.Badge(icon, RowKind.Locked);
            else if (CampMenu.GroupBadge(dests, group)) RowIcon.Badge(icon, RowKind.New);
            Ui.Marquee(Ui.Text(label, "kit-tile-label", tile));
            if (!open && !string.IsNullOrEmpty(hint)) Ui.Text(hint, "kit-tile-value", tile).name = "hint";
            if (open) Ui.Tappable(tile, () => App.Router.PushOrReturn(new CampGroupScreen(group)), "ui.select");
            else Ui.Tappable(tile, () => App.Toast(hint), "ui.error");
            return tile;
        }

        private static RowKind TileKind(string id)
        {
            switch (id)
            {
                case CampMenu.Heroes: case CampMenu.Mercenaries: return RowKind.Hero;
                case CampMenu.Items: return RowKind.Item;
                case CampMenu.Talk: return RowKind.Talk;
                case CampMenu.Bounties: return RowKind.Bounty;
                case CampMenu.Quests: return RowKind.Quest;
                case CampMenu.Diamonds: return RowKind.Diamond;
                case CampMenu.Bestiary: return RowKind.Bestiary;
                case CampMenu.Codex: return RowKind.Codex;
                case CampMenu.Settings: return RowKind.Settings;
                default: return RowKind.Station;
            }
        }

        private void Rest()
        {
            TravelRules.MakeCamp(App.Save); // GG-T1790: camp rest, free; on the road the caravan makes camp first
            var r = CampRestService.Rest(App.Save, out _);
            App.Toast(r.Message);
            App.Audio.Sfx(r.Ok ? "battle.heal" : "ui.error");
            if (r.Ok) { App.Persist(); Refresh(); }
        }

        /// <summary>GG-T1335: Back at the hub root asks before it saves and goes to the Title; a second Back answers Cancel.</summary>
        public override bool OnBack()
        {
            Confirm.Show("Return to the title? Your progress is saved.", "Title", ToTitle);
            return false;
        }

        internal static void ToTitle()
        {
            App.Persist(); App.Save = null; App.Slot = -1; App.Router.ResetTo(new TitleScreen());
        }

        public static string ThemeFor(ContentDb c, SaveData s)
        {
            var list = c.CampaignStages();
            if (list.Count == 0) return "greenvalley";
            var st = list[Mathf.Clamp(s.HighestClearedStage + 1, 0, list.Count - 1)];
            return st.Theme ?? "greenvalley";
        }
    }

    /// <summary>The hub's Craft or Journal group (GG-T1613): that group's CampMenu destinations as tiles, named and opened as
    /// they were on the hub's flat grid.</summary>
    public sealed class CampGroupScreen : HubPage
    {
        private readonly CampGroup group;
        public CampGroupScreen(CampGroup group) { this.group = group; }

        public override string Id => "camp-group-" + group.ToString().ToLowerInvariant();
        protected override string Title => group.ToString();
        protected override string Backdrop => HubScreen.ThemeFor(C, S);

        protected override void BuildBody(VisualElement body)
        {
            var grid = Ui.El("kit-pillar", body);
            grid.name = "camp-group-tiles";
            foreach (var d in CampMenu.In(CampMenu.Build(C, S, App.Settings), group)) HubScreen.Tile(grid, d);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Stage select (newest-on-top, replayable, drops hint — §22.3) + story crawl (§27)
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class StageSelectScreen : HubPage
    {
        public override string Id => "stages";
        protected override string Title => "Adventure";
        protected override string Backdrop => HubScreen.ThemeFor(C, S);

        /// <summary>GG-T388 (owner 2026-11-16: "a simple stage select which allows you to travel to and from previous battles
        /// so you can 'hunt' existing grounds"): two steps, region then stage. Null = the region list.</summary>
        private string region;
        private bool opened;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T431: each hunted bounty's clue, so the player knows which stage to play.
            var hunting = Features.Bounties ? Bounties.Hunting(C, S) : new List<BountyDef>();
            if (hunting.Count > 0)
            {
                var b = Ui.Panel(body, null, false, "bounty-strip");
                foreach (var h in hunting)
                {
                    var line = Ui.El("row", b);
                    line.style.alignItems = Align.Center;
                    Ui.Para($"{h.Name}: {Bounties.Clue(C, h)}", line, "gold").style.flexShrink = 1;
                    line.KindIcon(RowKind.Bounty); // the bounty skull leads each hunted target's clue (GG-T715)
                }
            }
            var list = Ui.ListPanel(body, null, out var scroll, "stage-list");
            var stages = C.CampaignStages();
            // Unlocked regions, newest first; every locked one collapses into the teaser row at the bottom.
            var regions = new List<ThemeDef>();
            int locked = 0;
            for (int ti = C.Campaign.Themes.Count - 1; ti >= 0; ti--)
            {
                var theme = C.Campaign.Themes[ti];
                int first = stages.FindIndex(x => theme.Stages.Count > 0 && x.Id == theme.Stages[0]);
                if (first < 0) continue;
                if (!Progression.IsStageUnlocked(S, first)) { locked += theme.Stages.Count; continue; }
                regions.Add(theme);
            }
            // Opens on the only region there is; with several, on the list.
            if (!opened)
            {
                opened = true;
                region = regions.Count == 1 ? regions[0].Id : null;
            }
            var open = region == null ? null : regions.Find(t => t.Id == region);
            if (open == null) Regions(regions, locked, list);
            else Stages(open, stages, regions.Count > 1, list);
            Ui.SelectFirst(list);
            // The hand rests on the next stage that can be entered, never a locked one (GG-T1155).
            if (open != null)
                foreach (var row in list.Children())
                    if (row.name != null && row.name.StartsWith("stage-") && !row.ClassListContains("menu-row--disabled")) { Ui.Select(row); break; }
        }

        /// <summary>Step one: every unlocked region with its state — a stage not yet won (New), or every stage cleared
        /// (huntable). Travel (GG-T1786) shows no per-region progress: the road is one journey, not a region's run.</summary>
        private void Regions(List<ThemeDef> regions, int locked, VisualElement list)
        {
            Ui.Para("Pick a region. Cleared stages stay open: hunt them for XP, loot and wanderers.", list, "dim");
            foreach (var theme in regions)
            {
                var t = theme;
                int cleared = theme.Stages.Count(id => S.ClearedStages.Contains(id));
                string status = cleared < theme.Stages.Count ? $"New · {cleared}/{theme.Stages.Count} cleared" : $"Cleared {cleared}/{theme.Stages.Count}";
                if (Features.Bounties && theme.Stages.Any(id => Bounties.OnStage(C, S, id).Count > 0)) status = "☠ " + status; // a hunted target roams here (GG-T431)
                var row = Ui.MenuRow(theme.Name, status, () => { region = t.Id; Refresh(); }, list, null, true);
                row.name = "expedition-" + theme.Id;
                // GG-T715: the region's board tile leads the row, badged Cleared (every stage won) or New (one still to win).
                RowIcon.Badge(row.KindIcon(RowKind.Place, theme.Id), cleared == theme.Stages.Count ? RowKind.Cleared : RowKind.New);
                if (cleared < theme.Stages.Count) row.Q<Label>("value")?.AddToClassList("gold");
            }
            if (locked > 0) Ui.MenuRow($"{locked} more stage{(locked == 1 ? "" : "s")} ahead", "Locked", null, list, null, false).KindIcon(RowKind.Locked);
        }

        /// <summary>Step two: one region's stages, each Cleared or New (a cleared one can be fought again as a hunt). Any
        /// unlocked stage launches; from camp that sets out on the road (<see cref="TravelRules.SetOut"/>, GG-T1786).</summary>
        private void Stages(ThemeDef theme, List<StageDef> stages, bool canGoBack, VisualElement list)
        {
            if (canGoBack)
            {
                var back = Ui.MenuRow("All regions", "", () => { region = null; Refresh(); }, list);
                back.name = "regions-back";
                back.KindIcon(RowKind.Back);
            }
            bool done = theme.Stages.All(id => S.ClearedStages.Contains(id));
            string status = done ? "Completed" : $"{theme.Stages.Count} battles";
            var head = Ui.MenuRow(theme.Name, status, null, list, null, true);
            var headIcon = head.KindIcon(RowKind.Place, theme.Id);
            if (done) RowIcon.Badge(headIcon, RowKind.Cleared);
            head.Q<Label>("label").AddToClassList("gold");
            head.name = "region-head-" + theme.Id;
            // GG-T374: the region's description (Prose "place:<theme>"), under its header.
            var blurb = ContentDb.Blurb(C.PlaceBlurbs, theme.Id);
            if (!string.IsNullOrEmpty(blurb))
            {
                Ui.Note(blurb, list).name = "region-" + theme.Id;
            }
            for (int k = 0; k < theme.Stages.Count; k++)
            {
                var st = C.TryStage(theme.Stages[k]);
                if (st == null) continue;
                int index = stages.FindIndex(x => x.Id == st.Id);
                bool cleared = S.ClearedStages.Contains(st.Id);
                bool can = Progression.IsStageUnlocked(S, index);
                // Same level BattleFactory.Build actually fights it at: the postgame bonus stages carry a
                // LevelOverride, so the index-based curve alone advertised the wrong level for them (GG-T70).
                int recommended = st.LevelOverride ?? Progression.RecommendedLevel(index);
                string state = cleared ? (k > 0 ? "Cleared · hunt" : "Cleared") : "New";
                if (Features.Bounties && Bounties.OnStage(C, S, st.Id).Count > 0) state = "☠ " + state; // a hunted target roams here (GG-T431)
                var row = Ui.MenuRow(st.Name, can ? $"{state} · Lv {recommended} · {RestedPool.LaunchCost} Rested" : $"{state} · Locked", () => Launch(st, index), list, null, can);
                row.name = "stage-" + st.Id;
                // GG-T715: the region's tile leads every stage, badged Cleared / New / Locked (was a star, open star or
                // diamond in the text); an enterable stage ends in the Enter arrow so it reads as a way in, not a line of text.
                RowIcon.Badge(row.KindIcon(RowKind.Stage, theme.Id), cleared ? RowKind.Cleared : can ? RowKind.New : RowKind.Locked);
                if (can) EnterMark(row);
                if (!cleared && can) row.Q<Label>("value")?.AddToClassList("gold");
                if (CaravanUpgrades.Level(S, "scouts") > 0)
                {
                    var foes = st.Waves.SelectMany(wv => wv.Enemies).Select(e => C.TryClass(e.Class)?.Name).Where(n => n != null).Distinct().Take(5);
                    Ui.Note("Scouts: " + string.Join(", ", foes), list, "warn");
                }
                var drops = DropsHint(st);
                if (can && drops.Length > 0)
                {
                    Ui.Note($"Drops: {drops}", list);
                }
            }
        }

        /// <summary>The Enter arrow at the right end of an enterable stage row (GG-T715), after its value.</summary>
        private static void EnterMark(VisualElement row)
        {
            var go = new KindEmblem(RowKind.Enter) { name = "enter-mark" };
            go.AddToClassList(RowIcon.Class);
            go.style.width = go.style.height = Size.IconS;
            go.style.flexShrink = 0f;
            go.AddToClassList("enter-mark");
            go.style.alignSelf = Align.Center;
            row.Add(go);
        }

        private string DropsHint(StageDef st)
        {
            var names = st.Waves.SelectMany(w => w.Enemies).Select(e => C.TryClass(e.Class)?.DropTable).Where(t => t != null && C.DropTables.ContainsKey(t))
                .SelectMany(t => C.DropTables[t].Entries).Select(e => C.TryItem(e.Item)?.Name).Where(n => n != null).Distinct().Take(3);
            return string.Join(", ", names);
        }

        /// <summary>GG-T723 (owner 2026-11-23: "yes confirmation step, show them whose going into battle"): a stage row opens
        /// the confirm screen, whose Enter replaces it with the battle.</summary>
        private void Launch(StageDef st, int index) => App.Router.Push(new StageConfirmScreen(st.Id));
    }

    /// <summary>
    /// The step between the stage list and the battle (GG-T723, owner 2026-11-23: "yes confirmation step, show them whose
    /// going into battle"): the stage (its region's tile, name, region, battle k/n, waves, enemy level, Cleared/New, the
    /// Rested it costs, a hunted bounty) and the party that will fight it (face, name, role, level, HP), with Change party,
    /// Back and Enter. Enter replaces this screen with the battle (<see cref="StageLauncher"/>), so the battle's results
    /// never come back here; the Rested refill retry still lands, since this screen stays current while the refill runs.
    /// Only the stage list comes through here: Next/Retry after a battle, -demo, Test Mode and automation launch directly.
    /// </summary>
    public sealed class StageConfirmScreen : HubPage
    {
        private readonly string stageId;
        public StageConfirmScreen(string stageId) { this.stageId = stageId; }

        public override string Id => "stage-confirm";
        protected override string Title => "Battle";
        protected override string Backdrop => C.ThemeOf(stageId)?.Id ?? HubScreen.ThemeFor(C, S);

        protected override void BuildBody(VisualElement body)
        {
            var st = C.TryStage(stageId);
            if (st == null) { Ui.Empty("That stage is gone.", Ui.Panel(body)); Foot(body, false, "That stage is gone."); return; }
            var theme = C.ThemeOf(stageId);
            var stages = C.CampaignStages();
            int index = stages.FindIndex(x => x.Id == stageId);
            int k = theme == null ? -1 : theme.Stages.IndexOf(stageId);
            bool cleared = S.ClearedStages.Contains(stageId);

            // The stage: the region's tile (badged Cleared / New, as on the list), its name and region, then the facts.
            var info = Ui.Panel(body, null, false, "stage-confirm-stage");
            var top = Ui.El("row", info);
            top.style.alignItems = Align.Center;
            var titles = Ui.El("col grow", top);
            titles.style.flexShrink = 1; titles.style.minWidth = 0;
            Ui.Heading(st.Name, titles, "gold").name = "stage-confirm-name";
            if (theme != null) Ui.Caption(theme.Name, titles, "dim");
            RowIcon.Badge(top.KindIcon(RowKind.Stage, theme?.Id), cleared ? RowKind.Cleared : RowKind.New);
            if (theme != null && k >= 0) Ui.Field("Battle", $"{k + 1} of {theme.Stages.Count}", info);
            Ui.Field("Waves", st.Waves.Count.ToString(), info);
            // The level BattleFactory.Build fights it at (a postgame stage's LevelOverride, else the curve; GG-T70).
            Ui.Field("Enemy level", $"Lv {st.LevelOverride ?? Progression.RecommendedLevel(Math.Max(0, index))}", info);
            Ui.Field("State", cleared ? (k > 0 ? "Cleared · hunt" : "Cleared") : "New", info, cleared ? null : "gold", "stage-confirm-state");
            if (theme != null) Ui.Field("Travel", TravelRules.OnTheRoad(S) ? HubScreen.TravelLine(S) : "Sets out from camp", info, null, "stage-confirm-travel"); // GG-T1786
            if (!RestedPool.Exempt(S)) Ui.Field("Rested", $"Costs {RestedPool.LaunchCost} (have {S.Rested.Points}/{RestedPool.Max})", info, null, "stage-confirm-rested");
            if (Features.Bounties)
            {
                var bounties = Bounties.OnStage(C, S, stageId);
                if (bounties.Count > 0) Ui.Field("Bounty", string.Join(", ", bounties.Select(b => b.Name)), info, "gold", "stage-confirm-bounty");
            }

            // Who is going into battle: the active party, each with face, name, role, level and HP.
            var party = Ui.ListPanel(body, $"Into battle {S.Party.Count}/{Progression.MaxPartySize}", "stage-confirm-party");
            foreach (var id in S.Party)
            {
                var h = S.Hero(id);
                var def = h == null ? null : C.TryClass(id);
                if (def == null) continue;
                var u = Progression.Preview(C, h);
                int hp = h.Hp > 0 ? Math.Min(h.Hp, u.MaxHp) : u.MaxHp; // 0 = full (§15.1)
                Ui.Card(party, id, def.Name, out var col, "stage-hero-" + id);
                var meta = Ui.El("row", col);
                meta.style.alignItems = Align.Center;
                Ui.Margin(Ui.RoleBadge(Ui.RoleOf(h, def), meta), left: Space.S0);
                Ui.Margin(Ui.Caption($"Lv {u.Level}  HP {hp}/{u.MaxHp}", meta, hp < u.MaxHp ? "warn" : "dim"), left: Space.S8);
            }
            if (S.Party.Count == 0) Ui.Empty("No one is in the party.", party);
            RowIcon.IconButton("Change party", RowKind.Hero, () => App.Router.Push(new PartyScreen()), "btn--small", Ui.Actions(party)).name = "stage-party-change";

            bool can = index < 0 || Progression.IsStageUnlocked(S, index);
            string why = can ? null : "Locked";
            if (can && S.Party.Count == 0) { can = false; why = "Put someone in the party first."; }
            Foot(body, can, why);
        }

        /// <summary>The footer: Back on the left, the primary Enter on the right, the reason above them when Enter is off.</summary>
        private void Foot(VisualElement body, bool can, string why)
        {
            var foot = Ui.Footer(body, "stage-confirm-footer");
            RowIcon.IconButton("Back", RowKind.Back, () => App.Router.Back(), "btn--small", foot).name = "stage-back";
            var mid = Ui.El("col grow", foot);
            mid.style.flexShrink = 1; mid.style.minWidth = 0;
            if (!can && !string.IsNullOrEmpty(why)) Ui.Para(why, mid, "bad").name = "stage-enter-why";
            var enter = RowIcon.IconButton("Enter", RowKind.Enter, () => StageLauncher.Launch(stageId, true), "btn--primary", foot);
            enter.name = "stage-enter";
            Ui.SetEnabled(enter, can);
        }
    }

    /// <summary>The one way into a battle: plays a chapter crawl that is still due (§27, GG-T386: one after every fifth
    /// stage, normally on the way back from its results), then any pending story-beat dialogue (§27.1), then the stage.</summary>
    public static class StageLauncher
    {
        public static void Launch(string stageId, bool replaceCurrent)
        {
            var app = GameApp.I;
            if (app.Save != null)
            {
                // GG-T1379: a drill is practice, so launching or retrying one costs no Rested.
                if (!Drills.IsDrill(app.Content.TryStage(stageId)))
                {
                    if (!RestedGate.Check(() => Launch(stageId, replaceCurrent))) return;
                    RestedGate.Spend();
                }
                if (app.Content.ThemeOf(stageId) != null) TravelRules.SetOut(app.Save); // GG-T1790: a campaign battle sets out from camp
                app.Persist();
            }
            var theme = app.Content.ThemeOf(stageId);
            // Story beats between battles (§27.1, GG-T117): play after the crawl, before the battle, once per save —
            // every one pending, one after another (GG-T119: a recruit and a Camp NPC can share a stage), each
            // picked when the last ends so later beats see what earlier ones did; the same set the bots play headless.
            GameScreen BeatsThenBattle()
            {
                var beat = DialogueRunner.PendingBeat(app.Content, app.Save, stageId);
                return beat == null ? new BattleScreen(stageId) : StoryBeats.Play(beat, BeatsThenBattle, theme?.Id);
            }
            var beatFirst = app.Save == null ? null : DialogueRunner.PendingBeat(app.Content, app.Save, stageId);
            // GG-T1478: the region-entry crawl the results screen didn't get to play plays before the theme's beats, in
            // one scroll with any due chapter crawl and the first beat's own crawl.
            var region = Story.RegionDue(app.Content, app.Save, theme?.Id);
            GameScreen next;
            if (region != null) next = StoryBeats.WithRegion(region, beatFirst, BeatsThenBattle);
            else
            {
                next = BeatsThenBattle();
                // GG-T386: a chapter crawl the results screen didn't get to play ("Next stage ›", or a save from before
                // chapters) plays now, unless a beat opening with its own crawl is up next: it waits for the next stop, so
                // two scrolls never play back to back.
                if (string.IsNullOrEmpty(beatFirst?.Crawl)) next = StoryBeats.WithChapter(next);
            }
            if (replaceCurrent) app.Router.Replace(next); else app.Router.Push(next);
        }
    }

    /// <summary>The one way into a Crucible run (§22.10): generates the save's next gauntlet and opens its battle.</summary>
    public static class CrucibleLauncher
    {
        public static void Launch(bool replaceCurrent)
        {
            var app = GameApp.I;
            if (!Crucible.CanLaunch(app.Content, app.Save, out var why)) { app.Toast(why); app.Audio.Sfx("ui.error"); return; }
            if (!RestedGate.Check(() => Launch(replaceCurrent))) return;
            RestedGate.Spend();
            var stage = Crucible.Prepare(app.Content, app.Save);
            app.Audio.Sfx("battle.crucible_enter");
            GameScreen next = new BattleScreen(stage.Id);
            if (replaceCurrent) app.Router.Replace(next); else app.Router.Push(next);
        }
    }

    public sealed class StoryCrawlScreen : GameScreen
    {
        /// <summary>GG-T1366: the text scrolls this many design px a second, times <see cref="FastForward"/> while held.</summary>
        public const float ScrollSpeed = 95f, FastForward = 4f;
        /// <summary>A page hands on to the next once its last line has risen above this share of the screen, so the
        /// next page's text enters from below while the last one leaves.</summary>
        public const float PageTurnAt = 0.3f;

        private readonly string title;
        private readonly List<CrawlPage> pages;
        private readonly Func<GameScreen> next;
        private readonly bool firstViewing;
        private VisualElement backdrops, clip, text, leaving;
        private float y, leavingY;
        private int page;
        private bool done, fast, skipArmed;
        /// <summary>A campaign crawl (GG-T386: the prologue, a chapter, the ending) over its backdrop; one with
        /// <see cref="CrawlDef.Pages"/> plays them in order, each over its own backdrop (GG-T1366).</summary>
        public StoryCrawlScreen(CrawlDef crawl, GameScreen next, bool firstViewing = true) : this(crawl, () => next, firstViewing) { }
        /// <summary>GG-T1394: a crawl whose next screen is built only once it ends (the onboarding's next step writes its
        /// checkpoint as it starts).</summary>
        public StoryCrawlScreen(CrawlDef crawl, Func<GameScreen> next, bool firstViewing = true)
            : this(crawl.Title, crawl.Pages.Count > 0 ? crawl.Pages : new List<CrawlPage> { new CrawlPage { Text = crawl.Crawl, Backdrop = crawl.Backdrop } },
                next, firstViewing) { }
        /// <summary>Any crawl (GG-T231): the prologue, a story beat's interlude, the ending.</summary>
        public StoryCrawlScreen(string title, string crawl, string backdrop, GameScreen next, bool firstViewing = true)
            : this(title, new List<CrawlPage> { new CrawlPage { Text = crawl, Backdrop = backdrop } }, next, firstViewing) { }
        /// <summary>A paged crawl (GG-T1366): <paramref name="title"/> heads the first page; each page's text scrolls
        /// over its own backdrop, cross-fading as the pages turn. A first viewing's Skip asks twice.</summary>
        public StoryCrawlScreen(string title, IEnumerable<CrawlPage> pages, GameScreen next, bool firstViewing = true)
            : this(title, pages, () => next, firstViewing) { }
        private StoryCrawlScreen(string title, IEnumerable<CrawlPage> pages, Func<GameScreen> next, bool firstViewing)
        {
            var app = GameApp.I;
            var fallback = HubScreen.ThemeFor(app.Content, app.Save);
            this.title = Commander.Fill(title, app.Save, app.Profile?.Name);
            this.pages = pages.Select(p => new CrawlPage
            {
                Name = p.Name,
                Text = Commander.Fill(p.Text, app.Save, app.Profile?.Name),
                Backdrop = p.Backdrop ?? fallback,
            }).ToList();
            this.next = next; this.firstViewing = firstViewing;
        }
        public override string Id => "crawl";
        public override string Music => music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Story) ?? "teller-of-the-tales-00";
        private string music;
        public override bool RebuildOnResize => false; // a rebuild would restart the crawl

        protected override void Build(VisualElement root)
        {
            backdrops = Ui.El("fill", root);
            backdrops.style.top = -GameApp.SafeTop;
            backdrops.style.bottom = -GameApp.SafeBottom;
            clip = Ui.El("fill", root);
            clip.style.overflow = Overflow.Hidden;
            // The scrolling text ends above the Skip button row, so it never runs under the button (GG-T397).
            clip.style.bottom = Space.S48 + Size.Target + Space.S24;
            page = -1;
            TurnPage();
            // Hold anywhere to fast-forward (as the credits roll does); only Skip ends it.
            root.RegisterCallback<PointerDownEvent>(_ => fast = true);
            root.RegisterCallback<PointerUpEvent>(_ => fast = false);
            root.RegisterCallback<PointerLeaveEvent>(_ => fast = false);
            var skip = RowIcon.IconButton("Skip", RowKind.Enter, Skip, "btn--small", root);
            skip.name = "crawl-skip";
            skip.style.position = Position.Absolute; skip.style.right = Space.S32; skip.style.bottom = Space.S48;
            if (DemoMode.Active || TestMode.Active) root.schedule.Execute(Finish).ExecuteLater(600);
        }

        /// <summary>The next page: its backdrop cross-fades in over the last (a cut under Reduce Motion), and its text
        /// starts below the screen while the last page's text scrolls on out.</summary>
        private void TurnPage()
        {
            page++;
            var p = pages[page];
            var old = backdrops.childCount > 0 ? backdrops[backdrops.childCount - 1] : null;
            if (old == null || p.Backdrop != pages[page - 1].Backdrop)
            {
                var bg = Ui.El("fill", backdrops);
                LivingBackdrop.Show(bg, p.Backdrop, 0);
                bg.style.unityBackgroundImageTintColor = Palette.ManaIndigo;
                if (old != null)
                {
                    if (App.Settings.ReduceMotion) old.RemoveFromHierarchy();
                    else App.StartCoroutine(CrossFade(bg, old));
                }
            }
            if (text != null) { leaving?.RemoveFromHierarchy(); leaving = text; leavingY = y; }
            text = Ui.El("col", clip);
            text.style.position = Position.Absolute;
            text.style.left = text.style.right = Fit.Inset + Space.S96;
            if (page == 0 && !string.IsNullOrEmpty(title)) Ui.Display(title, text, "shadow gold crawl-title", false);
            else if (page > 0) App.Audio.Sfx("ui.select", 0.5f);
            var body = Ui.Text(p.Text ?? "", "h2 shadow", text);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.unityTextAlign = TextAnchor.UpperCenter;
            y = GameApp.StageHeight;
            text.style.top = y;
        }

        private static IEnumerator CrossFade(VisualElement incoming, VisualElement outgoing)
        {
            incoming.style.opacity = 0;
            yield return Tween.Fade(incoming, 0, 1, Timing.Cinematic);
            outgoing.RemoveFromHierarchy();
        }

        public override void Tick(float dt)
        {
            if (done || text == null) return;
            float step = dt * ScrollSpeed * (fast ? FastForward : 1f);
            y -= step;
            text.style.top = y;
            if (leaving != null)
            {
                leavingY -= step;
                leaving.style.top = leavingY;
                if (leavingY < -leaving.layout.height - 100) { leaving.RemoveFromHierarchy(); leaving = null; }
            }
            if (text.layout.height <= 0) return;
            if (page < pages.Count - 1) { if (y + text.layout.height < GameApp.StageHeight * PageTurnAt) TurnPage(); }
            else if (y < -text.layout.height - 100) Finish();
        }

        /// <summary>Skip: a first viewing asks for a second tap, so a stray tap never throws the story away.</summary>
        private void Skip()
        {
            if (firstViewing && !skipArmed) { skipArmed = true; App.Toast("Tap again to skip"); return; }
            Finish();
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            App.Router.Replace(next());
        }

        /// <summary>Back skips the crawl like Skip ›. A crawl always hands on to what follows it (a launch's battle that
        /// has already spent Rested, the credits, Camp); popping it went back to the stage list, and relaunching charged
        /// again.</summary>
        public override bool OnBack()
        {
            Finish();
            return false;
        }
    }

    public sealed class CreditsScreen : GameScreen
    {
        private readonly Func<GameScreen> rollThen;
        private VisualElement roll;
        private float rollY;
        private bool fast, rolled;
        public CreditsScreen() { }
        /// <summary>GG-T231: the end-of-story roll — the credits scroll up over the last battlefield, hold-to-speed, then
        /// <paramref name="then"/> (the Title).</summary>
        public CreditsScreen(Func<GameScreen> then) { rollThen = then; }
        public override string Id => rollThen != null ? "credits-roll" : "credits";
        public override string Music => rollThen == null ? null
            : music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Credits) ?? "heroic-age-00";
        private string music;
        public override bool RebuildOnResize => rollThen == null; // a rebuild would restart the roll

        protected override void Build(VisualElement root)
        {
            if (rollThen != null) { BuildRoll(root); return; }
            var col = Ui.El("fill col", root);
            Ui.Header("Credits", () => App.Router.Back(), col, out _);
            var list = Ui.ListPanel(Ui.PageBody(col), null, "credits-list");
            Ui.Para(Resources.Load<TextAsset>("Credits")?.text ?? $"{GameApp.Title} — MindAttic", list);
        }

        private void BuildRoll(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, App.Content.Campaign.Ending?.Backdrop ?? "cityruins", 3);
            bg.style.unityBackgroundImageTintColor = new Color(0.25f, 0.25f, 0.32f);
            var clip = Ui.El("fill", root);
            clip.style.overflow = Overflow.Hidden;
            // The scrolling text ends above the Skip button row, so it never runs under the button (GG-T397).
            clip.style.bottom = Space.S48 + Size.Target + Space.S24;
            roll = Ui.El("col", clip);
            roll.name = "credits-roll";
            roll.style.position = Position.Absolute;
            roll.style.left = roll.style.right = Fit.Inset + Space.S96;
            var title = Ui.Text(GameApp.Title, "h1 shadow gold", roll);
            Fonts.UseDisplay(title);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = Space.S96;
            var text = Resources.Load<TextAsset>("Credits")?.text ?? $"{GameApp.Title} — MindAttic";
            // Credits.txt opens with the game's name; the display title above already says it.
            var lines = text.Replace("\r", "").Split('\n');
            if (lines.Length > 1 && lines[0].Trim().Equals(GameApp.Title, StringComparison.OrdinalIgnoreCase))
                text = string.Join("\n", lines.Skip(1)).TrimStart('\n');
            var body = Ui.Text(text, "h2 shadow", roll);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.unityTextAlign = TextAnchor.UpperCenter;
            var coda = Ui.Text("Thank you for playing.", "h2 shadow dim", roll);
            coda.style.whiteSpace = WhiteSpace.Normal;
            coda.style.unityTextAlign = TextAnchor.MiddleCenter;
            coda.style.marginTop = Fit.H(8.7f);
            rollY = GameApp.StageHeight;
            roll.style.top = rollY;
            // Hold anywhere to fast-forward; Skip ends it.
            root.RegisterCallback<PointerDownEvent>(_ => fast = true);
            root.RegisterCallback<PointerUpEvent>(_ => fast = false);
            root.RegisterCallback<PointerLeaveEvent>(_ => fast = false);
            var skip = RowIcon.IconButton("Skip", RowKind.Enter, EndRoll, "btn--small", root);
            skip.name = "credits-skip";
            skip.style.position = Position.Absolute; skip.style.right = Space.S32; skip.style.bottom = Space.S48;
            App.Audio.Sfx("ui.confirm");
            if (DemoMode.Active || TestMode.Active) root.schedule.Execute(EndRoll).ExecuteLater(600);
        }

        public override void Tick(float dt)
        {
            if (rollThen == null || rolled || roll == null) return;
            rollY -= dt * (fast ? 480f : 110f);
            roll.style.top = rollY;
            // Ends once the coda has scrolled up to the middle of the screen and held there a moment.
            if (roll.layout.height > 0 && rollY < -roll.layout.height + GameApp.StageHeight * 0.5f - 300f) EndRoll();
        }

        private void EndRoll()
        {
            if (rolled) return;
            rolled = true;
            App.Audio.Sfx("ui.select");
            App.Router.ResetTo(rollThen());
        }
    }

    /// <summary>
    /// Story beats as screens (§27, GG-T231): a beat with a <see cref="DialogueDef.Crawl"/> scrolls it before its
    /// conversation; the way back to Camp after a battle plays a pending level interlude; the final boss's first
    /// defeat rolls the ending crawl, the credits, then the Title.
    /// </summary>
    public static class StoryBeats
    {
        /// <summary>The beat's crawl (if it has one), then its conversation, then <paramref name="next"/>.</summary>
        public static GameScreen Play(DialogueDef beat, Func<GameScreen> next, string backdrop)
        {
            GameScreen talk = new DialogueScreen(beat, next, backdrop);
            return string.IsNullOrEmpty(beat.Crawl) ? talk : new StoryCrawlScreen(beat.Title, beat.Crawl, beat.Backdrop ?? backdrop, talk);
        }

        /// <summary>Camp, after any level interlude that's due (one after another, each chosen when the last ends), then
        /// any Joined scene (GG-T1594).</summary>
        public static GameScreen Camp()
        {
            var app = GameApp.I;
            var beat = DialogueRunner.PendingInterlude(app.Content, app.Save);
            return beat == null ? JoinedThenCamp() : Play(beat, Camp, null);
        }

        /// <summary>GG-T386: <paramref name="next"/>, preceded by the chapter crawl that is due (if any), which is marked
        /// played (with every chapter before it) and saved as it starts.</summary>
        public static GameScreen WithChapter(GameScreen next)
        {
            var app = GameApp.I;
            var ch = Story.ChapterDue(app.Content, app.Save);
            if (ch == null) return next;
            Story.MarkChapterSeen(app.Content, app.Save, ch);
            app.Persist();
            return new StoryCrawlScreen(ch, next);
        }

        /// <summary>The results screen's Continue: the ending if this victory earned it; else the chapter crawl this
        /// victory earned (GG-T386), then any vendor join beat (GG-T537), then Camp; else Camp via any interlude. A level interlude due with a chapter waits for
        /// the next launch or results screen, so two scrolls never play back to back.</summary>
        public static void AfterResults(bool victory, string stageId)
        {
            var app = GameApp.I;
            if (Story.EndingDue(app.Content, app.Save, victory, stageId))
            {
                Story.MarkEndingSeen(app.Save);
                app.Persist();
                var e = app.Content.Campaign.Ending;
                app.Router.ResetTo(new StoryCrawlScreen(e.Title, e.Crawl, e.Backdrop ?? app.Content.ThemeOf(stageId)?.Id,
                    new CreditsScreen(() => new TitleScreen())));
                return;
            }
            // GG-T386: the chapter crawl this victory earned, then Camp. GG-T537: a vendor's join beat (no crawl of its own)
            // still plays between the scroll and Camp, so the vendor arrives with the party; a level interlude waits.
            // GG-T1478: the first victory over a region's last stage also plays the next region's entry crawl, inside the
            // chapter's scroll (same stop) or alone.
            var ch = Story.ChapterDue(app.Content, app.Save);
            var region = victory ? Story.RegionDue(app.Content, app.Save, Story.RegionOpenedBy(app.Content, stageId)?.Id) : null;
            if (ch == null && region == null) { app.Router.ResetTo(Camp()); return; }
            if (ch != null) Story.MarkChapterSeen(app.Content, app.Save, ch);
            if (region != null) Story.MarkRegionSeen(app.Save, region);
            app.Persist();
            app.Router.ResetTo(Scroll(ch, region, null, VendorsThenCamp()));
        }

        /// <summary>GG-T1478: the region-entry crawl due on a launch into <paramref name="region"/>, as one scroll with the
        /// chapter crawl due (if any) and the first beat's own crawl (if it has one), so two scrolls never play back to
        /// back; then <paramref name="beats"/> (that beat's conversation first). Marks the crawls played and saves.</summary>
        public static GameScreen WithRegion(ThemeDef region, DialogueDef beatFirst, Func<GameScreen> beats)
        {
            var app = GameApp.I;
            var ch = Story.ChapterDue(app.Content, app.Save);
            if (ch != null) Story.MarkChapterSeen(app.Content, app.Save, ch);
            Story.MarkRegionSeen(app.Save, region);
            app.Persist();
            bool beatCrawl = !string.IsNullOrEmpty(beatFirst?.Crawl);
            return Scroll(ch, region, beatCrawl ? beatFirst : null, beatCrawl ? new DialogueScreen(beatFirst, beats, region.Id) : beats());
        }

        /// <summary>One scroll of the given crawls in order (chapter, region, beat), each after the first headed by its
        /// title, over the region's backdrop when there is one.</summary>
        private static GameScreen Scroll(CrawlDef chapter, ThemeDef region, DialogueDef beat, GameScreen next)
        {
            string title = null, text = null;
            void Add(string heading, string body)
            {
                if (string.IsNullOrEmpty(body)) return;
                if (text == null) { title = heading; text = body; }
                else text += string.IsNullOrEmpty(heading) ? "\n\n" + body : "\n\n" + heading + "\n\n" + body;
            }
            Add(chapter?.Title, chapter?.Crawl);
            Add(region?.Name, region?.Crawl);
            Add(beat?.Title, beat?.Crawl);
            return text == null ? next : new StoryCrawlScreen(title, text, region?.Id ?? chapter?.Backdrop ?? beat?.Backdrop, next);
        }

        /// <summary>GG-T537: Camp, after every vendor join beat that's due (one after another), then any Joined scene (GG-T1594).</summary>
        private static GameScreen VendorsThenCamp()
        {
            var app = GameApp.I;
            var beat = DialogueRunner.PendingVendorBeat(app.Content, app.Save);
            return beat == null ? JoinedThenCamp() : Play(beat, VendorsThenCamp, null);
        }

        /// <summary>GG-T1594: Camp, after a Joined scene for each new hero who hasn't been greeted (one after another, each
        /// chosen when the last ends). Reached from the results screen's Continue, so the VariantPicker has already asked
        /// each joiner's look on the results screen.</summary>
        private static GameScreen JoinedThenCamp()
        {
            var app = GameApp.I;
            var joined = DialogueRunner.PendingJoined(app.Content, app.Save);
            return joined == null ? (GameScreen)new HubScreen() : DialogueScreen.ForRunner(joined.Begin(app.Content, app.Save), JoinedThenCamp);
        }

        /// <summary>The onboarding after the team pick (SaveSlotScreen.Start), in order: each step returns its screen,
        /// leading to the next step's, or the next step's screen when it has nothing to play. After the last, Camp.</summary>
        private static readonly Func<Func<GameScreen>, GameScreen>[] NewCampaignSteps = { OpeningCrawl, Founding, Drill };

        /// <summary>A new campaign's first screen (GG-T1358, the one onboarding sequencer): the opening crawl (if any),
        /// then the Founding scene (GG-T1594, if one is due), then the first tutorial drill when drills exist, else Camp.</summary>
        public static GameScreen NewCampaign() => NewCampaignStep(0);

        private static GameScreen NewCampaignStep(int i) =>
            i >= NewCampaignSteps.Length ? new HubScreen() : NewCampaignSteps[i](() => NewCampaignStep(i + 1));

        private static string OpeningBackdrop(CrawlDef p) =>
            p == null ? null : p.Backdrop ?? (p.Pages.Count > 0 ? p.Pages[0].Backdrop : null);

        /// <summary>GG-T1394: records the Crawl checkpoint as the crawl starts; the steps after it are built as it ends, so
        /// each writes its own.</summary>
        private static GameScreen OpeningCrawl(Func<GameScreen> next)
        {
            var app = GameApp.I;
            var p = app.Content.Campaign.Opening;
            if (p == null) return next();
            Onboarding.Begin(app.Save, OnboardingStep.Crawl);
            app.Persist();
            return new StoryCrawlScreen(p, next);
        }

        private static GameScreen Founding(Func<GameScreen> next)
        {
            var app = GameApp.I;
            var founding = DialogueRunner.PendingFounding(app.Content, app.Save);
            return founding == null ? next() : Play(founding, next, OpeningBackdrop(app.Content.Campaign.Opening));
        }

        /// <summary>The first tutorial drill (campaign.json Tutorial, GG-T1374) when its stage exists.</summary>
        private static GameScreen Drill(Func<GameScreen> next)
        {
            var app = GameApp.I;
            var c = app.Content;
            if (Drills.Skipped(app.Save))
            {
                Onboarding.Begin(app.Save, OnboardingStep.HubIntro); // GG-T1645: Start Campaign's Skip to Camp
                app.Persist();
                return next();
            }
            var first = c.Campaign.Tutorial.Count > 0 ? c.TryStage(c.Campaign.Tutorial[0]) : null;
            return first == null ? next() : DrillAt(0, first.Id);
        }

        /// <summary>Tutorial drill <paramref name="n"/>, its checkpoint written as it starts. A drill always starts from
        /// the top (a battle is never saved mid-fight, BIBLE 26.8).</summary>
        private static GameScreen DrillAt(int n, string stageId)
        {
            var app = GameApp.I;
            Onboarding.Begin(app.Save, OnboardingStep.Drill, n);
            app.Persist();
            return new BattleScreen(stageId);
        }

        /// <summary>GG-T1394: where Continue or Load resumes a campaign whose onboarding isn't done (<see cref="Onboarding.Next"/>):
        /// the opening crawl from the top (then the rest of the sequence), the drill it stopped in, or Camp's first
        /// visit, whose lesson finishes the onboarding (<see cref="CampCoach"/>). Null once onboarding is done.</summary>
        public static GameScreen Resume()
        {
            var app = GameApp.I;
            var r = Onboarding.Next(app.Save, app.Content);
            switch (r.Step)
            {
                case OnboardingStep.Crawl: return NewCampaign();
                case OnboardingStep.Drill: return DrillAt(r.Drill, r.StageId);
                case OnboardingStep.HubIntro:
                    Onboarding.Begin(app.Save, OnboardingStep.HubIntro);
                    app.Persist();
                    return new HubScreen();
                default: return null;
            }
        }
    }
}
