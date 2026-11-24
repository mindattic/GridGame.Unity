using System.Collections;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // ── Mercenaries board (GG-T233, §25.10.1) ────────────────────────────────────

    public sealed class MercenaryScreen : HubPage
    {
        public override string Id => "mercenaries";
        protected override string Title => "Mercenaries";
        protected override string Backdrop => "desert";
        private bool haggling;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T454 (the menu kit): one scrolling list — the terms, who's under contract, then who's for hire.
            var list = Ui.ListPanel(body, null, "merc-board");
            Ui.Para("Swords for hire, paid upfront for a set number of battles. The count runs down every battle, whether they fight or wait in reserve. " +
                    "One haggle each until the next battle.", list, "dim");

            if (S.Mercenaries.Contracts.Count > 0)
            {
                var hired = Ui.El("col", list);
                hired.name = "merc-contracts";
                Ui.Section("Under contract", hired);
                foreach (var k in S.Mercenaries.Contracts)
                {
                    var def = C.TryClass(k.ClassId);
                    if (def == null) continue;
                    Ui.Card(hired, k.ClassId, null, out var col);
                    var top = Ui.El("row", col);
                    Ui.Name(def.Name, top);
                    MercenaryCamp.Tag(S, k.ClassId, top);
                    Ui.Caption($"{k.BattlesLeft} battle{(k.BattlesLeft == 1 ? "" : "s")} left · {Ui.Gold(k.Rate)} a battle", col);
                }
            }

            Ui.Section("For hire", list);
            var board = Mercenaries.Board(C, S);
            if (board.Count == 0) Ui.Empty("No one's looking for work right now.", list);
            foreach (var m in board)
            {
                var merc = m;
                var def = C.Class(m.Class);
                bool walked = Mercenaries.Walked(S, m.Class);
                var card = Ui.Card(list, m.Class, null, out var col, "merc-offer-" + m.Class);
                var top = Ui.El("row", col);
                Ui.Name(def.Name, top);
                Ui.RoleBadge(def.Role, top);
                Ui.Caption(m.Temperament.ToString(), top, "dim");
                var pitch = Ui.Para($"\"{m.Pitch}\"", col);
                pitch.style.flexShrink = 0; pitch.style.minWidth = 0; pitch.style.whiteSpace = WhiteSpace.Normal; // GG-T700: the quote wraps to its full height inside the card, never clipped
                GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, def.Support), "kit-text gold", col); // GG-T298
                int adj = Mercenaries.HaggleAdjust(S, m.Class);
                if (adj != 0) Ui.Caption(adj < 0 ? $"Haggled down {-adj}%" : $"Price up {adj}%", col, adj < 0 ? "gold" : "bad");
                if (walked) { Ui.Para("Walked off. Back after the next battle.", col, "bad"); continue; }
                var terms = Ui.Actions(col);
                terms.style.flexShrink = 0; terms.style.marginTop = Space.TargetHalfGap; // GG-T700: a clear 24 px (12 + the card's gap) above the hire/Haggle row, and the row keeps its height
                foreach (var t in m.Terms.OrderBy(x => x))
                {
                    int term = t, price = Mercenaries.Price(C, S, m.Class, t);
                    var b = RowIcon.IconButton($"{term} battles · {Ui.Gold(price)}", RowKind.Confirm, () => Confirm.Show(
                        $"Hire {def.Name} for {term} battles at {Ui.Gold(price)}, paid now? The contract runs down every battle, used or not.", "Hire",
                        () =>
                        {
                            // The price shown may be stale (a haggle landed since): never charge a different amount than asked.
                            if (haggling || Mercenaries.Price(C, S, merc.Class, term) != price) { App.Toast("The price has changed"); App.Audio.Sfx("ui.error"); Refresh(); return; }
                            Done(Mercenaries.Hire(C, S, merc.Class, term));
                        }), "btn--small", terms);
                    b.name = $"merc-hire-{m.Class}-{term}";
                    b.style.marginTop = Space.TargetHalfGap; b.style.marginBottom = Space.TargetHalfGap; b.style.marginLeft = Space.TargetHalfGap; b.style.marginRight = Space.TargetHalfGap; // GG-T700: 24 px to the next target, wrapped or not
                    Ui.SetEnabled(b, S.Gold >= price && !haggling);
                }
                var haggle = RowIcon.IconButton(Mercenaries.Haggled(S, m.Class) ? "Haggled" : "Haggle", RowKind.Shop, () => App.StartCoroutine(Haggle(merc, card)), "btn--small", terms);
                haggle.name = "merc-haggle-" + m.Class;
                haggle.style.marginTop = Space.TargetHalfGap; haggle.style.marginBottom = Space.TargetHalfGap; haggle.style.marginLeft = Space.TargetHalfGap; haggle.style.marginRight = Space.TargetHalfGap;
                Ui.SetEnabled(haggle, !Mercenaries.Haggled(S, m.Class) && !haggling);
            }
        }

        // The merc weighs the offer for a beat before answering (law 5: nothing lands in a single frame).
        private IEnumerator Haggle(MercenaryDef m, VisualElement card)
        {
            if (haggling) yield break;
            haggling = true;
            // Hire and Haggle stay off while the merc thinks (the page isn't rebuilt until the answer lands).
            Body?.Query<Button>().ForEach(btn => { if (btn.name != null && btn.name.StartsWith("merc-")) Ui.SetEnabled(btn, false); });
            App.Audio.Sfx("ui.press");
            var think = Ui.Heading("...", card, "gold");
            yield return Tween.Run(Timing.Dramatic, k => { think.style.opacity = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)); });
            var o = Mercenaries.Haggle(C, S, m.Class, out var message);
            haggling = false;
            App.Audio.Sfx(o == HaggleOutcome.Dropped ? "ui.buy" : o == HaggleOutcome.Held ? "ui.select" : "ui.error");
            App.Toast(message);
            App.Persist();
            Refresh();
        }
    }

    /// <summary>Camp's side of the mercenary contracts (GG-T233): announce departures, raise the "nearly up" reoffer,
    /// and the MERC tag on hero cards.</summary>
    public static class MercenaryCamp
    {
        /// <summary>A small "MERC · N left" tag after a contracted hero's name; nothing for anyone else.</summary>
        public static void Tag(SaveData s, string classId, VisualElement parent)
        {
            var k = Mercenaries.Contract(s, classId);
            if (k == null) return;
            var tag = Ui.Caption($"MERC · {k.BattlesLeft} left", parent, "gold");
            tag.name = "merc-tag-" + classId;
            tag.style.marginLeft = Space.S8;
        }

        /// <summary>Called from Camp's Build: settle spent contracts, toast who left, then open the reoffer for a merc
        /// on their last battle once the hub has settled.</summary>
        public static void OnCamp(GameScreen hub)
        {
            var app = GameApp.I;
            var c = app.Content; var s = app.Save;
            if (s == null || hub.Root == null) return;
            Mercenaries.Settle(c, s);
            var gone = Mercenaries.TakeDepartureNotices(s);
            var pending = Mercenaries.PendingRenewal(s);
            if (gone.Count == 0 && pending == null) return;
            if (gone.Count > 0) app.Persist();
            // Every Camp rebuild (a Refresh, a resize) runs this: only one announcement/reoffer may be in flight, or two
            // rebuilds inside the delay stacked two renewal screens.
            if (gone.Count == 0 && (Time.realtimeSinceStartup < scheduledUntil || RenewalShown(app))) return;
            int delay = gone.Count > 0 ? 900 : 400;
            // Held past the router's 0.2 s fade, so the pushed screen is on the stack before the window closes.
            scheduledUntil = Time.realtimeSinceStartup + delay / 1000f + 0.5f;
            hub.Root.schedule.Execute(() =>
            {
                if (app.Router.Current != hub || app.Save != s) return;
                if (gone.Count > 0)
                {
                    app.Audio.Sfx("ui.cancel");
                    app.Toast(string.Join(", ", gone.Select(id => c.TryClass(id)?.Name ?? id)) + (gone.Count == 1 ? "'s contract is up. They've moved on." : ": contracts up. They've moved on."), 2.6f);
                }
                if (pending != null && Mercenaries.PendingRenewal(s) == pending && !RenewalShown(app)) app.Router.Push(new MercenaryRenewalScreen(pending.ClassId));
            }).ExecuteLater(delay);
        }

        private static float scheduledUntil;

        private static bool RenewalShown(GameApp app) => app.Router.Stack.Any(x => x is MercenaryRenewalScreen);
    }

    /// <summary>The reoffer (GG-T233): a merc on the last battle of their contract asks to renew. Renew at a loyalty
    /// discount, finish the contract, or part ways now.</summary>
    public sealed class MercenaryRenewalScreen : GameScreen
    {
        private readonly string classId;
        private bool answered;
        public MercenaryRenewalScreen(string classId) { this.classId = classId; }
        public override string Id => "merc-renewal";

        private static ContentDb C => GameApp.I.Content;
        private static SaveData S => GameApp.I.Save;

        protected override void Build(VisualElement root)
        {
            var def = C.Class(classId);
            var m = C.TryMercenary(classId);
            var k = Mercenaries.Contract(S, classId);
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, HubScreen.ThemeFor(C, S), 2);
            bg.style.unityBackgroundImageTintColor = new Color(0.4f, 0.42f, 0.55f);

            var portrait = Ui.El(null, root);
            portrait.name = "merc-portrait";
            portrait.style.position = Position.Absolute;
            portrait.style.left = portrait.style.right = Fit.Inset + Fit.Edge; portrait.style.top = Fit.H(6.3f); portrait.style.bottom = Fit.H(38.7f);
            portrait.style.backgroundImage = new StyleBackground(Art.PortraitFigure(classId));
            portrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            portrait.style.opacity = 0f;

            var box = Ui.Window(null, root);
            box.name = "merc-renewal-box";
            box.style.position = Position.Absolute;
            box.style.left = box.style.right = Fit.Inset + Fit.Edge; box.style.bottom = Space.S64;
            var plate = Ui.El("row", box);
            Ui.Heading(def.Name, plate, "gold");
            MercenaryCamp.Tag(S, classId, plate);
            var line = Ui.Para("", box);
            line.style.minHeight = Size.Thumb; // the line types out into a box already its height
            string speech = m?.Renewal ?? "My contract's nearly up. Renew, or do we part ways?";

            var choices = Ui.El("col", box);
            choices.style.marginTop = Space.S16;
            choices.style.opacity = 0f;
            if (m != null && k != null)
            {
                int loyalty = Mathf.RoundToInt(Mercenaries.Loyalty(k) * 100f);
                Ui.Caption($"Loyalty discount: {loyalty}%", choices, "gold");
                foreach (var t in m.Terms.OrderBy(x => x))
                {
                    int term = t, price = Mercenaries.RenewalPrice(C, S, classId, t);
                    var b = RowIcon.IconButton($"Renew {term} more battles · {Ui.Gold(price)}", RowKind.Confirm, () => Answer(Mercenaries.Renew(C, S, classId, term)), null, choices);
                    b.name = $"merc-renew-{term}";
                    Ui.SetEnabled(b, S.Gold >= price);
                }
            }
            var finish = RowIcon.IconButton("Finish the contract (1 battle left)", RowKind.Confirm, () => { Mercenaries.FinishContract(S, classId); Answer(Result.Success($"{def.Name} will see out the last battle")); }, null, choices);
            finish.name = "merc-finish";
            var part = RowIcon.IconButton("Part ways now", RowKind.Close, () => Answer(Mercenaries.PartWays(C, S, classId)), null, choices);
            part.name = "merc-part";

            // The merc steps in, the line types out, then the answers rise (law 5: every beat plays over time).
            GameApp.I.StartCoroutine(Enter(portrait, line, speech, choices));
        }

        private IEnumerator Enter(VisualElement portrait, Label line, string speech, VisualElement choices)
        {
            App.Audio.Sfx("ui.select");
            yield return Tween.Run(Timing.Emphasis, k => { portrait.style.opacity = k; portrait.style.translate = new Translate(-120f * (1f - k), 0); }, Tween.EaseOut);
            yield return Tween.Run(Mathf.Clamp(speech.Length / 60f, 0.4f, 1.6f), k => line.text = speech.Substring(0, Mathf.RoundToInt(speech.Length * k)));
            App.Audio.Sfx("ui.click");
            yield return Tween.Run(Timing.Standard, k => choices.style.opacity = k);
        }

        private void Answer(Result r)
        {
            if (answered) return;
            if (!r.Ok) { App.Audio.Sfx("ui.error"); App.Toast(r.Message); return; }
            answered = true;
            App.Audio.Sfx("ui.buy");
            App.Toast(r.Message);
            App.Persist();
            App.Router.Back();
        }

        public override bool OnBack()
        {
            // Backing out is an answer too: they see out the contract, so the reoffer doesn't loop.
            if (!answered) { Mercenaries.FinishContract(S, classId); App.Persist(); answered = true; }
            return true;
        }
    }
}
