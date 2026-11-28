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
}
