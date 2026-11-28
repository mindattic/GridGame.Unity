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
            var tag = Ui.Heading("Slide. Flank. Strike.", col, "dim");
            Ui.Margin(tag, top: Space.S16, bottom: Fit.H(4f));
            Ui.Scrim(tag); // GG-T1887
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
            about.KindIcon(RowKind.Codex);
            var credits = Ui.MenuRow("Credits", "", () => App.Router.Push(new CreditsScreen()), menu);
            credits.name = "title-credits";
            credits.KindIcon(RowKind.Lore);
            Ui.SelectFirst(menu);

            var ver = Ui.Caption($"v{Application.version}", root, "dim corner-tag"); // GG-T1794: POL-C9 skips corner tags
            ver.style.position = Position.Absolute;
            ver.style.right = Space.S32; ver.style.bottom = Space.S24;
            Ui.Scrim(ver);
        }

        /// <summary>The Continue card (§11.2): the Commander, the stage Continue leads to, play time, when it was last
        /// played and the party's faces; one tap resumes.</summary>
        private VisualElement ContinueCard(SaveData s, int slot, VisualElement parent)
        {
            var card = Ui.El("card card-tap col title-continue", parent);
            card.name = "title-continue";
            card.style.width = Fit.C(TitleColumnPct);
            Ui.Heading("Continue", card);
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
            if (s == null) { App.Toast("That save can't be read. Try another slot."); return; }
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
}
