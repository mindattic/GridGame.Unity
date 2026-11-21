using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Base for hub sub-screens: background art, header with back + gold, body column.</summary>
    public abstract class HubPage : GameScreen
    {
        protected Label GoldLabel;
        protected VisualElement Body;
        protected abstract string Title { get; }
        protected virtual string Backdrop => "greenvalley";
        /// <summary>Optional line shown under the header title (e.g. station level + next unlock, GG-T35).</summary>
        protected virtual string Subtitle => null;
        /// <summary>The automation name the subtitle label takes (default "header-sub").</summary>
        protected virtual string SubtitleName => null;
        /// <summary>The Camp NPC who keeps this station (GG-T119), or null: the header gets a Talk button for them.</summary>
        protected virtual string Npc => null;
        /// <summary>The camp tip this page shows (GG-T173): its own Id, unless a tab has its own tip (the Blacksmith's Forge, GG-T177).</summary>
        protected virtual string TipStation => Id;

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(Backdrop, 1));
            bg.style.unityBackgroundImageTintColor = new Color(0.45f, 0.5f, 0.7f, 1f);
            var col = Ui.El("fill col", root);
            var header = Ui.Header(Title, () => App.Router.Back(), col, out GoldLabel, Subtitle);
            if (SubtitleName != null && header.Q<Label>("header-sub") is Label subLabel) subLabel.name = SubtitleName;
            // Every Camp NPC can be talked to from their own station as well as from Camp's Talk row (GG-T119).
            var topic = Npc != null && CampNpcs.Joined(S, Npc) ? DialogueRunner.TalkTopic(C, S, ContentLoader.NpcFlagPrefix + Npc) : null;
            if (topic != null)
            {
                var talk = Ui.Button(S.SeenDialogues.Contains(topic.Id) ? "Talk" : "Talk!", () => App.Router.Push(new DialogueScreen(topic, (string)null, (GameScreen)null)), "btn--small", null);
                talk.name = "npc-talk";
                header.Insert(header.IndexOf(GoldLabel), talk);
            }
            // GG-T454 (the menu kit): the kit's page body, the centred column with the kit's gutters (GG-T398).
            Body = Ui.PageBody(col);
            BuildBody(Body);
            Typography.Watch(Body);
            UpdateGold();
            CampTipOverlay.ShowIfPending(this, TipStation);
        }

        protected abstract void BuildBody(VisualElement body);

        protected void UpdateGold()
        {
            if (GoldLabel != null && App.Save != null) GoldLabel.text = Ui.Gold(App.Save.Gold);
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

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background("cityruins", 0));
            var shade = Ui.El("fill", root);
            shade.style.backgroundColor = new Color(0, 0, 0.05f, 0.45f);

            var col = Ui.El("fill col center", root);
            var title = Ui.Text("GRIDGAME", "title shadow", col);
            Fonts.UseHeavy(title);
            var sub = Ui.Text("GAIDEN", "title shadow", col);
            Fonts.UseHeavy(sub);
            sub.style.fontSize = 150;
            sub.style.color = Palette.Neutral;
            var tag = Ui.Text("Slide. Flank. Strike.", "h2 shadow dim", col);
            tag.style.marginTop = Space.S8;
            tag.style.marginBottom = Fit.H(6.3f);

            var menu = Ui.Window(null, col);
            menu.style.width = Fit.C(66f);
            int recent = App.Saves.MostRecent();
            Debug.Log($"[Title] most recent save slot: {recent}");
            if (recent >= 0) Ui.MenuRow("Continue", "", () => Continue(recent), menu);
            Ui.MenuRow("New Game", "", () => App.Router.Push(new SaveSlotScreen(true)), menu);
            Ui.MenuRow("Load Game", "", () => App.Router.Push(new SaveSlotScreen(false)), menu, null, recent >= 0);
            // GG-T99 (§15.4): the player profile owns the settings and these three slots; switch / create / rename / delete here.
            var profile = Ui.MenuRow("Profile", App.Profile?.Name ?? "", () => App.Router.Push(new ProfileSelectScreen()), menu);
            profile.name = "title-profile";
            profile.Q<Label>("value").AddToClassList("gold");
            Ui.MenuRow("Settings", "", () => App.Router.Push(new SettingsScreen()), menu).name = "title-settings";
            Ui.MenuRow("Credits", "", () => App.Router.Push(new CreditsScreen()), menu);
            Ui.SelectFirst(menu);

            var ver = Ui.Text($"v{Application.version}", "tiny dim", root);
            ver.style.position = Position.Absolute;
            ver.style.right = Space.S32; ver.style.bottom = Space.S24;
        }

        private void Continue(int slot)
        {
            Debug.Log($"[Title] continue slot {slot}");
            var s = App.Saves.Load(slot, App.Content);
            if (s == null) { App.Toast("That save is damaged"); return; }
            App.Save = s; App.Slot = slot;
            App.ApplySettings();
            App.Router.ResetTo(new HubScreen());
        }
    }

    public sealed class SaveSlotScreen : GameScreen
    {
        private readonly bool newGame;
        public SaveSlotScreen(bool newGame) { this.newGame = newGame; }
        public override string Id => newGame ? "newgame" : "load";

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background("cityruins", 0));
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col", root);
            Ui.Header(newGame ? "New Game" : "Load Game", () => App.Router.Back(), col, out _);
            var w = Ui.Panel(Ui.PageBody(col), newGame ? "Choose a slot" : "Choose a save");
            for (int i = 0; i < SaveStore.Slots; i++)
            {
                int slot = i;
                var s = App.Saves.Load(i, App.Content);
                string label = s == null ? $"Slot {i + 1} — Empty" : $"Slot {i + 1} — {s.Name}";
                string value = s == null ? "" : $"Stage {s.HighestClearedStage + 2}  {Ui.Gold(s.Gold)}";
                var row = Ui.MenuRow(label, value, () => Pick(slot, s), w, null, newGame || s != null);
                if (s != null)
                {
                    Ui.Note($"{s.Roster.Count} heroes · {TimeSpan.FromSeconds(s.PlaySeconds):h\\:mm} played · {s.UpdatedUtc.ToLocalTime():g}", w);
                }
            }
        }

        private void Pick(int slot, SaveData existing)
        {
            if (!newGame)
            {
                App.Save = existing; App.Slot = slot;
                App.ApplySettings();
                App.Router.ResetTo(new HubScreen());
                return;
            }
            if (existing != null)
            {
                Confirm.Show($"Overwrite slot {slot + 1}?", "Overwrite", () => Start(slot));
                return;
            }
            Start(slot);
        }

        private void Start(int slot)
        {
            App.Router.Push(new CharacterSelectScreen(party =>
            {
                App.Save = Progression.NewGame(App.Content, "Player", party);
                App.Slot = slot;
                App.ApplySettings();
                App.Persist();
                // GG-T532: the party picks its looks (the locked Warden too) before the story starts.
                VariantPicker.ShowPending(() => App.Router.ResetTo(StoryBeats.NewCampaign()));
            }));
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
        }

        public CharacterSelectScreen(Action<List<string>> onDone, bool auto = false)
        {
            this.onDone = onDone;
            this.auto = auto || DemoMode.Active;
        }

        /// <summary>GG-T533: the fixed lead, already locked into the first slot (null when the content has no such class).</summary>
        private string Lead => App.Content.TryClass(Progression.LeadClass) != null ? Progression.LeadClass : null;

        public override string Id => "charselect";
        public override string Music => music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.CharSelect) ?? "minstrel-guild-00";
        private string music;

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background("cityruins", 0));
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col", root);
            Ui.Header("Choose Your Party", () => App.Router.Back(), col, out _);
            // GG-T533: Warden, the First Sleeper, always leads; the player picks the rest.
            if (Lead != null && !locked.Contains(Lead)) locked.Insert(0, Lead);
            int picks = PartySize - (Lead != null ? 1 : 0);
            string leadName = Lead != null ? App.Content.Class(Lead).Name : null;
            var hint = Ui.Text(leadName != null
                ? $"{leadName} leads. Pick {picks} more to set out with: tap a hero to preview, tap again to lock in. More join along the road."
                : $"Pick {PartySize} to set out with: tap a hero to preview, tap again to lock in. More join along the road.", "small dim", col);
            hint.name = "charselect-hint";
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.whiteSpace = WhiteSpace.Normal; // the how-to lives here, not in the grid title (GG-T403)

            partyStrip = Ui.El("row", col);
            partyStrip.style.justifyContent = Justify.Center;
            partyStrip.style.marginTop = Space.S8; partyStrip.style.marginBottom = Space.S8;

            revealPanel = Ui.Window(null, col);
            revealPanel.style.minHeight = 420;
            Fit.Center(revealPanel);

            lockBtn = Ui.Button("Lock In", LockInHighlighted, "btn--primary", col);
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
            foreach (var def in App.Content.Classes.Values.Where(d => d.Team == Team.Hero).OrderBy(d => d.Name))
            {
                var id = def.Id;
                var t = Ui.Thumb(id, grid, 164);
                t.name = "tile-" + id;
                t.style.marginLeft = t.style.marginRight = t.style.marginTop = t.style.marginBottom = Space.S8;
                t.AddToClassList("card-tap");
                Ui.Tappable(t, () => Pick(id));
            }

            RefreshState();
            if (auto) App.StartCoroutine(AutoPlay());
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
                var pool = App.Content.Classes.Values.Where(d => d.Team == Team.Hero && !locked.Contains(d.Id)).ToList();
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
                if (showId == Lead) Ui.Text("Your lead: always in the party", "small good", info).style.marginTop = Space.S8;
                else if (locked.Contains(showId)) Ui.Text("Locked in", "small good", info).style.marginTop = Space.S8;
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
            var w = Ui.Window(null, veil);
            w.style.width = Length.Percent(76);
            var q = Ui.Heading(question, w);
            q.style.whiteSpace = WhiteSpace.Normal;
            var row = Ui.El("kit-footer", w);
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = Space.S16;
            Ui.Button("Cancel", () => { veil.RemoveFromHierarchy(); onNo?.Invoke(); }, null, row).name = "confirm-no";
            Ui.Button(yes, () => { veil.RemoveFromHierarchy(); onYes?.Invoke(); }, "btn--primary", row).name = "confirm-yes";
            return veil;
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
            bg.style.backgroundImage = new StyleBackground(Art.Background(ThemeFor(c, s), 2));
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
            // 24 px between the tappable cards (POL-C3).
            party.style.paddingLeft = party.style.paddingRight = Space.TargetHalfGap;
            var ids = s.Party;
            // Two cards abreast on a phone; three on a 2:3 / 3:4 tablet, which has the width for them (GG-T398).
            int perRow = Fit.Roomy ? 3 : 2;
            for (int i = 0; i < ids.Count; i += perRow)
            {
                var row = Ui.El("row", party);
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
                    Ui.RoleBadge(Ui.RoleOf(h, c.Class(id)), meta, TypeScale.Caption).style.marginLeft = Space.S0;
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

            var lower = Ui.El("row grow", col);
            lower.style.alignItems = Align.Stretch;

            var menu = Ui.Window(null, lower);
            menu.style.flexGrow = 1;
            var list = Ui.List(menu);
            VisualElement Item(string label, Func<GameScreen> screen, string value = "") => Ui.MenuRow(label, value, () => App.Router.Push(screen()), list.contentContainer);
            // Stations open one at a time (GG-T174, §25.0.3): a closed one is a dimmed row saying when it arrives.
            void Station(string label, bool open, string hint, Func<GameScreen> screen, string value = "")
            {
                // An open station's row is named for the Camp lesson that points at it when its keeper joins (GG-T539).
                if (open) { Item(label, screen, value).name = "camp-" + label.ToLowerInvariant().Replace(' ', '-'); return; }
                var row = Ui.MenuRow(label, "", null, list.contentContainer, null, false);
                row.name = "locked-" + label.ToLowerInvariant().Replace(' ', '-');
                Subline(row, hint);
            }
            // GG-T518: a hint ("Joins after The Tithe Moors III", "Opens after The Lantern Road II", GG-T537) is wider than a row's value
            // column; scrolled there as a marquee (GG-T448) it read as fragments ("oins at The") most of the time. It now
            // sits whole, wrapping if it must, in small type under the row's name.
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
            Item("Adventure", () => new StageSelectScreen(), $"Stage {Math.Min(s.HighestClearedStage + 2, c.CampaignStages().Count)}");
            // The Crucible (§22.10): endless waves for XP and loot. Closed mid-expedition — the row says why.
            // Hidden while Features.Crucible is off (GG-T392, docs/RELEASE_SCOPE.md): hunts on Adventure are the grind.
            if (Features.Crucible && CampUnlocks.CrucibleOpen(s))
            {
                bool crucibleOpen = Crucible.CanLaunch(c, s, out var crucibleWhy);
                var crucible = Ui.MenuRow("Crucible", crucibleOpen ? CrucibleScreen.RecordText(s) : "", () => App.Router.Push(new CrucibleScreen()), list.contentContainer, null, crucibleOpen);
                crucible.name = "camp-crucible";
                if (!crucibleOpen) Subline(crucible, crucibleWhy); // "Closed on an expedition", whole (GG-T518)
            }
            else if (Features.Crucible) Station("Crucible", false, CampUnlocks.MilestoneHint(c, CampUnlocks.CrucibleAfter), null);
            Item("Items", () => new ItemsScreen());
            // GG-T435 (§25.0): one Heroes row for Status / Equip / Abilities (+ Talents) / Party, the tabs of one screen.
            // GG-T180 (§25.0.9): while onboarding step 7 is ahead it reads "Train a skill!" and opens on Abilities (Learn);
            // otherwise it shows the party's unspent skill points (GG-T53) and opens on Status.
            int partyPoints = s.Party.Select(s.Hero).Where(h => h != null).Sum(h => h.SkillPoints);
            bool training = Training.Step(s);
            var heroes = Ui.MenuRow("Heroes", training ? "Train a skill!" : partyPoints > 0 ? $"{partyPoints} pts" : "",
                () => App.Router.Push(training ? new AbilitiesScreen(s.Party.FirstOrDefault()) : (GameScreen)new StatusScreen(s.Party.FirstOrDefault())), list.contentContainer);
            heroes.name = "camp-heroes";
            // Talk (§27.1, GG-T117): chat with party members and joined Camp NPCs.
            int newTalks = DialogueRunner.TalkPartners(s).Count(p => { var t = DialogueRunner.TalkTopic(c, s, p); return t != null && !s.SeenDialogues.Contains(t.Id); });
            Item("Talk", () => new TalkScreen(), newTalks > 0 ? $"{newTalks} new" : "");
            // Camp NPCs (GG-T119): each station opens once its keeper has joined the caravan at a story beat (GG-T537: a stage clear).
            Station("Merchant", CampUnlocks.NpcOpen(s, CampNpcs.Merchant), CampUnlocks.JoinHint(c, CampNpcs.Merchant), () => new ShopScreen());
            if (CampUnlocks.NpcOpen(s, CampNpcs.Innkeeper))
            {
                if (Expeditions.InnOpen(s)) Ui.MenuRow(s.Expedition != null ? "Inn (Medic Wagon)" : "Inn", Inn.Cost(c, s) > 0 ? Ui.Gold(Inn.Cost(c, s)) : "Rested", Rest, list.contentContainer);
                else Ui.MenuRow("Inn", "On the road", null, list.contentContainer, null, false);
                // GG-T53 (§25.6.3): the Innkeeper also sells a Respec that refunds every talent point.
                // GG-T392: off by default — the Talents screen's per-hero Respec button does the same job.
                if (Features.InnRespecRow && Expeditions.InnOpen(s)) Item("Inn: Respec", () => new RespecScreen());
            }
            else Station("Inn", false, CampUnlocks.JoinHint(c, CampNpcs.Innkeeper), null);
            Station("Blacksmith", CampUnlocks.NpcOpen(s, CampNpcs.Blacksmith), CampUnlocks.JoinHint(c, CampNpcs.Blacksmith), () => new BlacksmithScreen());
            Station("Alchemist", CampUnlocks.NpcOpen(s, CampNpcs.Alchemist), CampUnlocks.JoinHint(c, CampNpcs.Alchemist), () => new AlchemistScreen());
            if (Features.Enchanter)
                Station("Enchantress", CampUnlocks.NpcOpen(s, CampNpcs.Enchantress), CampUnlocks.JoinHint(c, CampNpcs.Enchantress), () => new EnchantressScreen());
            if (Features.Bounties)
                Station("Bounties", CampUnlocks.BountiesOpen(s), CampUnlocks.MilestoneHint(c, CampUnlocks.BountiesAfter), () => new BountyScreen(),
                    s.Bounty.Slain.Count > 0 ? $"☠ {s.Bounty.Slain.Count} to collect" : s.Bounty.Taken.Count > 0 ? $"Hunting {s.Bounty.Taken.Count}" : ""); // GG-T431
            // Optional side quests (§22.11, GG-T237): the journal, with a "!" when one is ready to hand in.
            int questsReady = Quests.ReadyCount(c, s);
            if (c.QuestOrder.Any(q => Quests.State(c, s, q) != QuestState.Locked))
                Item("Quests", () => new QuestScreen(), questsReady > 0 ? $"! {questsReady} to hand in" : $"{s.Quests.Active.Count} under way");
            Station("Summon Circle", CampUnlocks.SummonOpen(s), CampUnlocks.MilestoneHint(c, CampUnlocks.SummonAfter), () => new SummonScreen());
            // The Diamond Store (§25.18.1, GG-T455): the gold exchange and the real-money packs.
            // GG-T537: the Diamond Store's row opens with the Summon Circle (a Diamond's first use is a summon).
            if (Features.DiamondStore && CampUnlocks.DiamondStoreOpen(s))
                Ui.MenuRow("Diamond Store", $"{DiamondExchange.Have(s):N0} Diamonds", () => App.Router.Push(new DiamondStoreScreen()), list.contentContainer).name = "camp-diamonds";
            else if (Features.DiamondStore) Station("Diamond Store", false, CampUnlocks.MilestoneHint(c, CampUnlocks.SummonAfter), null);
            Station("Mercenaries", Mercenaries.Open(s), CampUnlocks.MilestoneHint(c, CampUnlocks.BountiesAfter), () => new MercenaryScreen(),
                s.Mercenaries.Contracts.Count > 0 ? $"{s.Mercenaries.Contracts.Count} hired" : "");
            int caravanLevels = c.Caravan.Keys.Sum(k => CaravanUpgrades.Level(s, k));
            if (Features.Caravan) Station("Caravan", CampUnlocks.CaravanOpen(s), CampUnlocks.MilestoneHint(c, CampUnlocks.CaravanAfter), () => new CaravanScreen(), caravanLevels > 0 ? $"{caravanLevels} upgrades" : "");
            Item("Bestiary", () => new BestiaryScreen());
            Item("Settings", () => new SettingsScreen());
            if (Progression.CanStartNewGamePlus(c, s))
                Ui.MenuRow($"New Game+ {s.NewGamePlus + 1}", "", () => Confirm.Show("Start New Game+? Keep your heroes, gear and gold; the campaign restarts with enemies 10 levels stronger.", "Begin", () =>
                {
                    var r = Progression.StartNewGamePlus(App.Content, App.Save);
                    App.Toast(r.Message);
                    if (r.Ok) { App.Persist(); Refresh(); }
                }), list.contentContainer).Q<Label>("label").AddToClassList("gold");
            Ui.MenuRow("Save & Quit", "", () => { App.Persist(); App.Save = null; App.Slot = -1; App.Router.ResetTo(new TitleScreen()); }, list.contentContainer);
            Ui.Select(list.contentContainer[0]);

            var side = Ui.El("col", lower);
            side.style.width = Length.Percent(29); side.style.minWidth = 300;
            if (s.Expedition != null)
            {
                var ex = Ui.Window(null, side);
                ex.name = "expedition-strip";
                var th = Expeditions.Theme(c, s.Expedition.ThemeId);
                Ui.Caption("Expedition", ex, "dim");
                Ui.Caption(th?.Name ?? "", ex, "gold");
                Ui.Caption($"Battle {s.Expedition.Next + 1}/{th?.Stages.Count ?? 0}", ex);
                Ui.Caption($"{s.Expedition.CarriedOrbs} orbs carried", ex, "dim");
            }
            RestedGate.Panel(side, Refresh);
            var goldW = Ui.Window(null, side);
            // Caption label over a body-size value, three times (kit, GG-T454).
            Ui.Caption("Gold", goldW, "dim");
            Ui.Name(Ui.Gold(s.Gold), goldW, "gold num");
            Ui.Caption("Time", goldW, "dim");
            Ui.Name(TimeSpan.FromSeconds(s.PlaySeconds).ToString(@"h\:mm\:ss"), goldW, "num");
            if (s.NewGamePlus > 0) Ui.Name($"New Game+ {s.NewGamePlus}", goldW, "gold");
            Ui.Caption("Cleared", goldW, "dim");
            Ui.Name($"{s.ClearedStages.Count(id => c.Stages.ContainsKey(id) && !c.Stage(id).IsTest)}/{c.CampaignStages().Count}", goldW, "num");
            // GG-T75 (§25.12.1): once the Caravan station is open, a rotating one-line crew bark — flavor, not dialogue. GG-T454:
            // full width under the menu, where a sentence reads as one or two lines, not a word a line in the side column.
            if (Features.Caravan && CampUnlocks.CaravanOpen(s))
            {
                var gossip = Ui.Panel(col, null, false, "camp-gossip");
                Ui.Caption("Around the wagons", gossip, "dim");
                CaravanScreen.BarkLine(c, s, gossip);
            }
            MercenaryCamp.OnCamp(this);
            CampCoach.OnHub(this); // GG-T539: a vendor who just joined, or a first piece of gear to put on, gets its lesson
        }

        private void Rest()
        {
            var r = Inn.Rest(App.Content, App.Save);
            App.Toast(r.Message);
            App.Audio.Sfx(r.Ok ? "battle.heal" : "ui.error");
            if (r.Ok) { App.Persist(); Refresh(); }
        }

        public static string ThemeFor(ContentDb c, SaveData s)
        {
            var list = c.CampaignStages();
            if (list.Count == 0) return "greenvalley";
            var st = list[Mathf.Clamp(s.HighestClearedStage + 1, 0, list.Count - 1)];
            return st.Theme ?? "greenvalley";
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
                foreach (var h in hunting) Ui.Para($"☠ {h.Name}: {Bounties.Clue(C, h)}", b, "gold");
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
            // Opens on the region under way: the active expedition's, else the only one there is.
            if (!opened)
            {
                opened = true;
                region = S.Expedition?.ThemeId ?? (regions.Count == 1 ? regions[0].Id : null);
            }
            var open = region == null ? null : regions.Find(t => t.Id == region);
            if (open == null) Regions(regions, locked, list);
            else Stages(open, stages, regions.Count > 1, list);
            Ui.SelectFirst(list);
        }

        /// <summary>Step one: every unlocked region with its state — the expedition under way, a stage not yet won (New), or
        /// every stage cleared (huntable).</summary>
        private void Regions(List<ThemeDef> regions, int locked, VisualElement list)
        {
            Ui.Para("Pick a region. Cleared stages stay open: hunt them for XP, loot and wanderers.", list, "dim");
            foreach (var theme in regions)
            {
                var t = theme;
                bool onIt = S.Expedition != null && S.Expedition.ThemeId == theme.Id;
                int cleared = theme.Stages.Count(id => S.ClearedStages.Contains(id));
                string status = onIt ? $"▶ Battle {S.Expedition.Next + 1}/{theme.Stages.Count}"
                    : cleared < theme.Stages.Count ? $"New · {cleared}/{theme.Stages.Count} cleared" : $"Cleared {cleared}/{theme.Stages.Count}";
                if (Features.Bounties && theme.Stages.Any(id => Bounties.OnStage(C, S, id).Count > 0)) status = "☠ " + status; // a hunted target roams here (GG-T431)
                var row = Ui.MenuRow((cleared == theme.Stages.Count ? "★ " : "☆ ") + theme.Name, status, () => { region = t.Id; Refresh(); }, list, null, true);
                row.name = "expedition-" + theme.Id;
                if (onIt || cleared < theme.Stages.Count) row.Q<Label>("value")?.AddToClassList("gold");
            }
            if (locked > 0) Ui.MenuRow($"◇ {locked} more stage{(locked == 1 ? "" : "s")} ahead…", "", null, list, null, false);
        }

        /// <summary>Step two: one region's stages, each Cleared or New. A new region is fought in order as an expedition;
        /// a cleared stage can be fought alone as a hunt (<see cref="Expeditions.IsHunt"/>) whenever no expedition is under way.</summary>
        private void Stages(ThemeDef theme, List<StageDef> stages, bool canGoBack, VisualElement list)
        {
            if (canGoBack)
            {
                var back = Ui.MenuRow("‹ All regions", "", () => { region = null; Refresh(); }, list);
                back.name = "regions-back";
            }
            bool onIt = S.Expedition != null && S.Expedition.ThemeId == theme.Id;
            bool done = theme.Stages.All(id => S.ClearedStages.Contains(id));
            string status = onIt ? $"Battle {S.Expedition.Next + 1}/{theme.Stages.Count} · {S.Expedition.CarriedOrbs} orbs carried" : done ? "★ Completed" : $"{theme.Stages.Count} battles";
            var head = Ui.MenuRow((onIt ? "▶ " : "") + theme.Name + " Expedition", status, null, list, null, true);
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
                bool can = Expeditions.CanLaunch(C, S, st.Id, out var why) && Progression.IsStageUnlocked(S, index);
                string label = (cleared ? "★ " : can ? "☆ " : "◇ ") + st.Name;
                // Same level BattleFactory.Build actually fights it at: the postgame bonus stages carry a
                // LevelOverride, so the index-based curve alone advertised the wrong level for them (GG-T70).
                int recommended = st.LevelOverride ?? Progression.RecommendedLevel(index);
                string state = cleared ? (k > 0 && Expeditions.IsHunt(S, st.Id) ? "Cleared · hunt" : "Cleared") : "New";
                if (Features.Bounties && Bounties.OnStage(C, S, st.Id).Count > 0) state = "☠ " + state; // a hunted target roams here (GG-T431)
                var row = Ui.MenuRow(label, can ? $"{state} · Lv {recommended} · {RestedPool.LaunchCost} Rested" : $"{state} · {why ?? "Locked"}", () => Launch(st, index), list, null, can);
                row.name = "stage-" + st.Id;
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
            if (onIt) Ui.MenuRow("Abandon expedition", "", () => Confirm.Show("Abandon the expedition? Progress on it is lost; wounds remain.", "Abandon", () => { App.Toast(Expeditions.Abandon(S).Message); App.Persist(); Refresh(); }), list);
        }

        private string DropsHint(StageDef st)
        {
            var names = st.Waves.SelectMany(w => w.Enemies).Select(e => C.TryClass(e.Class)?.DropTable).Where(t => t != null && C.DropTables.ContainsKey(t))
                .SelectMany(t => C.DropTables[t].Entries).Select(e => C.TryItem(e.Item)?.Name).Where(n => n != null).Distinct().Take(3);
            return string.Join(", ", names);
        }

        private void Launch(StageDef st, int index) => StageLauncher.Launch(st.Id, false);
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
                if (!Expeditions.CanLaunch(app.Content, app.Save, stageId, out var why)) { app.Toast(why); return; }
                if (!RestedGate.Check(() => Launch(stageId, replaceCurrent))) return;
                RestedGate.Spend();
                Expeditions.Begin(app.Content, app.Save, stageId);
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
            GameScreen next = BeatsThenBattle();
            // GG-T386: a chapter crawl the results screen didn't get to play ("Next stage ›", or a save from before
            // chapters) plays now, unless a beat opening with its own crawl is up next: it waits for the next stop, so
            // two scrolls never play back to back.
            var beatFirst = app.Save == null ? null : DialogueRunner.PendingBeat(app.Content, app.Save, stageId);
            if (string.IsNullOrEmpty(beatFirst?.Crawl)) next = StoryBeats.WithChapter(next);
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
        private readonly string title, crawl, backdrop;
        private readonly GameScreen next;
        private VisualElement text;
        private float y;
        private bool done;
        /// <summary>A campaign crawl (GG-T386: the prologue, a chapter, the ending) over its backdrop.</summary>
        public StoryCrawlScreen(CrawlDef crawl, GameScreen next) : this(crawl.Title, crawl.Crawl, crawl.Backdrop, next) { }
        /// <summary>Any crawl (GG-T231): the prologue, a story beat's interlude, the ending.</summary>
        public StoryCrawlScreen(string title, string crawl, string backdrop, GameScreen next)
        {
            this.title = title; this.crawl = crawl; this.next = next;
            this.backdrop = backdrop ?? HubScreen.ThemeFor(GameApp.I.Content, GameApp.I.Save);
        }
        public override string Id => "crawl";
        public override string Music => music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Story) ?? "teller-of-the-tales-00";
        private string music;
        public override bool RebuildOnResize => false; // a rebuild would restart the crawl

        protected override void Build(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(backdrop, 0));
            bg.style.unityBackgroundImageTintColor = new Color(0.35f, 0.35f, 0.45f);
            var clip = Ui.El("fill", root);
            clip.style.overflow = Overflow.Hidden;
            // The scrolling text ends above the Skip button row, so it never runs under the button (GG-T397).
            clip.style.bottom = Space.S48 + Size.Target + Space.S24;
            text = Ui.El("col", clip);
            text.style.position = Position.Absolute;
            text.style.left = text.style.right = Fit.Inset + Space.S96;
            var h = Ui.Text(title, "h1 shadow gold", text);
            Fonts.UseDisplay(h);
            h.style.unityTextAlign = TextAnchor.MiddleCenter;
            h.style.marginBottom = Space.S64;
            var body = Ui.Text(crawl, "h2 shadow", text);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.unityTextAlign = TextAnchor.UpperCenter;
            y = GameApp.StageHeight;
            text.style.top = y;
            var skip = Ui.Button("Skip ›", Finish, "btn--small", root);
            skip.style.position = Position.Absolute; skip.style.right = Space.S32; skip.style.bottom = Space.S48;
            root.RegisterCallback<ClickEvent>(_ => Finish());
            if (DemoMode.Active || TestMode.Active) root.schedule.Execute(Finish).ExecuteLater(600);
        }

        public override void Tick(float dt)
        {
            if (done || text == null) return;
            y -= dt * 95f;
            text.style.top = y;
            if (y < -text.layout.height - 100) Finish();
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            App.Router.Replace(next);
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
            Ui.Para(Resources.Load<TextAsset>("Credits")?.text ?? "GridGame Gaiden — MindAttic", list);
        }

        private void BuildRoll(VisualElement root)
        {
            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(App.Content.Campaign.Ending?.Backdrop ?? "cityruins", 3));
            bg.style.unityBackgroundImageTintColor = new Color(0.25f, 0.25f, 0.32f);
            var clip = Ui.El("fill", root);
            clip.style.overflow = Overflow.Hidden;
            // The scrolling text ends above the Skip button row, so it never runs under the button (GG-T397).
            clip.style.bottom = Space.S48 + Size.Target + Space.S24;
            roll = Ui.El("col", clip);
            roll.name = "credits-roll";
            roll.style.position = Position.Absolute;
            roll.style.left = roll.style.right = Fit.Inset + Space.S96;
            var title = Ui.Text("GridGame Gaiden", "h1 shadow gold", roll);
            Fonts.UseDisplay(title);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = Space.S96;
            var text = Resources.Load<TextAsset>("Credits")?.text ?? "GridGame Gaiden — MindAttic";
            // Credits.txt opens with the game's name; the display title above already says it.
            var lines = text.Replace("\r", "").Split('\n');
            if (lines.Length > 1 && lines[0].Trim().Equals("GridGame Gaiden", StringComparison.OrdinalIgnoreCase))
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
            var skip = Ui.Button("Skip ›", EndRoll, "btn--small", root);
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

        /// <summary>Camp, after any level interlude that's due (one after another, each chosen when the last ends).</summary>
        public static GameScreen Camp()
        {
            var app = GameApp.I;
            var beat = DialogueRunner.PendingInterlude(app.Content, app.Save);
            return beat == null ? new HubScreen() : Play(beat, Camp, null);
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
            var ch = Story.ChapterDue(app.Content, app.Save);
            if (ch == null) { app.Router.ResetTo(Camp()); return; }
            Story.MarkChapterSeen(app.Content, app.Save, ch);
            app.Persist();
            app.Router.ResetTo(new StoryCrawlScreen(ch, VendorsThenCamp()));
        }

        /// <summary>GG-T537: Camp, after every vendor join beat that's due (one after another).</summary>
        private static GameScreen VendorsThenCamp()
        {
            var app = GameApp.I;
            var beat = DialogueRunner.PendingVendorBeat(app.Content, app.Save);
            return beat == null ? new HubScreen() : Play(beat, VendorsThenCamp, null);
        }

        /// <summary>A new campaign's first screen: the prologue crawl (if any), then Camp.</summary>
        public static GameScreen NewCampaign()
        {
            var p = GameApp.I.Content.Campaign.Prologue;
            return p == null ? new HubScreen() : new StoryCrawlScreen(p.Title, p.Crawl, p.Backdrop, new HubScreen());
        }
    }
}
