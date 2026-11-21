using System;
using System.Collections;
using System.Collections.Generic;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T532: which version of a class the game draws. An owned hero is the variant the player picked
    /// (<see cref="HeroSave.Variant"/>); a hero not in the roster is the female art, unless the battle gave it a look to show
    /// (a wanderer on the board: either version, at random, when the male art exists). <see cref="Art.Portrait(string)"/>
    /// reads this, so every portrait, thumbnail and figure in the game resolves through one place.
    /// </summary>
    public static class HeroLooks
    {
        private static readonly Dictionary<string, HeroVariant> shown = new Dictionary<string, HeroVariant>(StringComparer.OrdinalIgnoreCase);

        public static HeroVariant Of(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return HeroVariant.Female;
            var h = GameApp.I?.Save?.Hero(classId);
            if (h != null) return h.Variant;
            return shown.TryGetValue(classId, out var v) ? v : HeroVariant.Female;
        }

        /// <summary>Gives a hero the player doesn't own a look for now (a wanderer met in battle, and its talk after).</summary>
        public static void Show(string classId, HeroVariant v)
        {
            if (!string.IsNullOrEmpty(classId)) shown[classId] = v;
        }

        /// <summary>Forgets every look given to heroes the player doesn't own (each battle rolls its own).</summary>
        public static void ClearShown() => shown.Clear();
    }

    /// <summary>
    /// GG-T532: the female/male pick (owner 2026-11-21: "when they finally join your party you get to choose the gender, same
    /// with summoning ... no change to story or mechanics makes everything equal"). A hero who joins waits with
    /// <see cref="HeroSave.VariantPending"/>; <see cref="ShowPending"/> asks for each in turn with the two portraits side by
    /// side. A class with no male art yet (most of them until GG-T535's art lands) is skipped silently and stays female.
    /// Asked: after the new-game party pick (the locked Warden too), after a Diamond draw's reveal, and whenever Camp, the
    /// results screen or a Camp page comes up with someone waiting (story joins, summons, wanderers, mercenaries, quest
    /// heroes). The hero screen's Look button opens the same picker to change it later (<see cref="Change"/>).
    /// </summary>
    public static class VariantPicker
    {
        public const string VeilName = "variant-picker";

        /// <summary>Screens the picker may open over when someone is waiting (never the battle, a story scene or a crawl).</summary>
        private static readonly HashSet<string> QuietScreens = new HashSet<string>
            { "hub", "postbattle", "summon", "mercenaries", "quests", "party", "status", "bounties" };

        private static readonly List<Action> waiting = new List<Action>();

        /// <summary>Whether the picker is on screen (Camp's coach marks wait for it).</summary>
        public static bool Open => GameApp.I?.Overlay?.Q(VeilName) != null;

        /// <summary>Bots, -demo, Test Mode and harness runs don't stop to ask (a waiting hero stays female) unless the run
        /// passes <c>-variants</c> — the same opt-in the Rested gate uses (<c>-rested</c>).</summary>
        public static bool Asks => !(DemoMode.Active || TestMode.Active || CommandLine.Has("-autobattle") ||
                                     (CommandLine.Has("-automation") && !CommandLine.Has("-variants")));

        /// <summary>The router's hook: a quiet screen with someone waiting opens the picker.</summary>
        public static void OnScreen(GameScreen screen)
        {
            if (screen == null || !QuietScreens.Contains(screen.Id)) return;
            var s = GameApp.I?.Save;
            if (s == null || HeroVariants.Pending(s).Count == 0) return;
            ShowPending();
        }

        /// <summary>Asks for every waiting hero in join order, then calls <paramref name="done"/> (at once when nobody waits
        /// or every waiting class lacks male art). Safe to call while it's already open: <paramref name="done"/> runs when
        /// the whole queue is answered.</summary>
        public static void ShowPending(Action done = null)
        {
            if (done != null) waiting.Add(done);
            if (Open) return;
            Next();
        }

        private static void Next()
        {
            var app = GameApp.I;
            var s = app?.Save;
            HeroSave ask = null;
            bool settled = false;
            if (s != null)
                foreach (var h in HeroVariants.Pending(s))
                {
                    if (Asks && Art.HasMaleArt(h.ClassId) && app.Content.TryClass(h.ClassId) != null) { ask = h; break; }
                    HeroVariants.Choose(h, HeroVariant.Female); // no male art yet (or nobody to ask): the female art, silently
                    settled = true;
                }
            if (settled) app.Persist();
            if (ask == null)
            {
                var then = waiting.ToArray();
                waiting.Clear();
                foreach (var cb in then) cb?.Invoke();
                return;
            }
            var pick = ask;
            Build(pick.ClassId, pick.Variant, true, v =>
            {
                HeroVariants.Choose(pick, v);
                app.Persist();
                Next();
            }, null);
        }

        /// <summary>The hero screen's Look button: the same two portraits, the current one marked, with a Cancel. Does
        /// nothing for a class with no male art.</summary>
        public static void Change(HeroSave h, Action changed = null)
        {
            if (h == null || Open || !Art.HasMaleArt(h.ClassId)) return;
            Build(h.ClassId, h.Variant, false, v =>
            {
                bool differs = v != h.Variant;
                HeroVariants.Choose(h, v);
                GameApp.I.Persist();
                if (differs) changed?.Invoke();
            }, () => { });
        }

        /// <summary>The picker: a dimmed veil over the page, the hero's name, the two figures side by side (female left,
        /// male right) each a tap target with its label under it, and a Cancel when <paramref name="cancel"/> is set.</summary>
        private static void Build(string classId, HeroVariant current, bool joining, Action<HeroVariant> onPick, Action cancel)
        {
            var app = GameApp.I;
            var def = app.Content.TryClass(classId);
            string name = def?.Name ?? classId;
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = VeilName;
            veil.pickingMode = PickingMode.Position;
            veil.style.backgroundColor = Palette.Ink.WithA(0.9f);
            veil.style.opacity = 0f;
            var w = Ui.Window(null, veil);
            w.name = "variant-window";
            w.style.width = Fit.Column - 2 * Kit.Gutter;
            var title = Ui.Text(joining ? $"{name} joins you!" : name, "h1 shadow gold", w);
            Fonts.UseDisplay(title);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            var hint = Ui.Para("Choose a look. Same skills, same story.", w, "dim");
            hint.name = "variant-hint";
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginBottom = Space.S16;
            var row = Ui.El("row", w);
            row.style.justifyContent = Justify.Center;
            bool picked = false;
            var cards = new Dictionary<HeroVariant, VisualElement>();
            foreach (var v in new[] { HeroVariant.Female, HeroVariant.Male })
            {
                var card = Ui.El("card", row);
                card.style.flexDirection = FlexDirection.Column; // the kit card is a row; here the label sits under the art
                card.name = "variant-" + v.ToString().ToLowerInvariant();
                card.style.flexGrow = 1; card.style.flexShrink = 1; card.style.flexBasis = Length.Percent(0); card.style.minWidth = 0;
                card.style.marginLeft = card.style.marginRight = Space.TargetHalfGap;
                card.style.alignItems = Align.Center;
                card.style.paddingTop = card.style.paddingBottom = Space.S16;
                bool on = !joining && v == current;
                var accent = on ? Palette.Gold : Ui.RoleColor(def?.Role);
                card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = on ? 6 : 3;
                card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = accent;
                var art = Ui.El(null, card);
                art.pickingMode = PickingMode.Ignore;
                art.style.width = Length.Percent(100);
                art.style.height = Size.ArtH;
                art.style.backgroundImage = new StyleBackground(Art.PortraitFigure(classId, v));
                art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var label = Ui.Name(v == HeroVariant.Female ? "Female" : "Male", card, on ? "gold" : null);
                label.pickingMode = PickingMode.Ignore;
                label.style.marginTop = Space.S8;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                cards[v] = card;
                var choice = v;
                Ui.Tappable(card, () =>
                {
                    if (picked) return;
                    picked = true;
                    app.Audio.Sfx("ui.confirm");
                    app.StartCoroutine(Close(veil, cards[choice], () => onPick(choice)));
                }, null);
            }
            if (cancel != null)
            {
                var foot = Ui.El("kit-footer", w);
                foot.style.justifyContent = Justify.FlexEnd;
                foot.style.marginTop = Space.S16;
                Ui.Button("Cancel", () =>
                {
                    if (picked) return;
                    picked = true;
                    app.Audio.Sfx("ui.cancel");
                    app.StartCoroutine(Close(veil, null, cancel));
                }, null, foot).name = "variant-cancel";
            }
            app.Audio.Sfx(joining ? "camp.summon" : "ui.select", joining ? 0.7f : 0.6f);
            app.StartCoroutine(Tween.Run(Timing.Standard, k => veil.style.opacity = k, Tween.EaseOut));
        }

        /// <summary>The chosen card pops, then the veil fades out and <paramref name="then"/> runs.</summary>
        private static IEnumerator Close(VisualElement veil, VisualElement chosen, Action then)
        {
            if (chosen != null) yield return Tween.Pop(chosen, 1.08f, Timing.Standard);
            yield return Tween.Run(Timing.Quick, k => veil.style.opacity = 1f - k);
            veil.RemoveFromHierarchy();
            then?.Invoke();
        }
    }
}
