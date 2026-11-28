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
        /// <summary>GG-T1206: the camp's mercenaries (non-party heroes) load into its own Screen art scope, released when it closes.</summary>
        public override bool HoldsScreenArt => true;
        protected override string Title => "Recruit";
        protected override string Backdrop => "desert";
        private bool haggling;

        protected override void BuildBody(VisualElement body)
        {
            SummonScreen.RecruitTabs(body, 1);
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
                    var top = Row(hired, k.ClassId, null, out var col);
                    Ui.Name(def.Name, top);
                    MercenaryCamp.Tag(S, k.ClassId, top);
                    Ui.Caption($"{k.BattlesLeft} battle{(k.BattlesLeft == 1 ? "" : "s")} left · {Ui.Gold(k.Rate)} a battle", col);
                }
            }

            Ui.Section("For hire", list);
            var board = Mercenaries.Board(C, S);
            if (board.Count == 0) Ui.Empty("No one's looking for work right now.", list);
            // GG-T2020: one 132-px row per mercenary (thumbnail, name and role, a short line); a tap opens the hire sheet.
            foreach (var m in board)
            {
                var merc = m;
                var def = C.Class(m.Class);
                var top = Row(list, m.Class, "merc-offer-" + m.Class, out var col);
                Ui.Name(def.Name, top);
                Ui.RoleBadge(def.Role, top).AddToClassList("merc-role");
                Ui.Caption(ShortLine(m), col, Mercenaries.Walked(S, m.Class) ? "bad" : "dim");
                Ui.Tappable(col.parent, () => OpenSheet(merc), "ui.select");
            }
            if (sheetClass != null) RenderSheet();
        }

        /// <summary>A list row <see cref="Kit.Row"/> tall: the baked thumbnail (R9), a top line, and <paramref name="col"/> for the line under it.</summary>
        private static VisualElement Row(VisualElement parent, string classId, string name, out VisualElement col)
        {
            var card = Ui.El("card kit-card merc-row", parent);
            if (name != null) card.name = name;
            Ui.Thumb(classId, card, Size.Avatar);
            col = Ui.El("col grow", card);
            col.style.flexShrink = 1; col.style.minWidth = 0;
            var top = Ui.El("row", col);
            top.style.alignItems = Align.Center;
            return top;
        }

        private string ShortLine(MercenaryDef m)
        {
            if (Mercenaries.Walked(S, m.Class)) return "Walked off. Back after the next battle.";
            int from = m.Terms.Min(t => Mercenaries.Price(C, S, m.Class, t));
            int adj = Mercenaries.HaggleAdjust(S, m.Class);
            string mood = adj < 0 ? $"Haggled down {-adj}%" : adj > 0 ? $"Price up {adj}%" : m.Temperament.ToString();
            return $"{mood} · from {Ui.Gold(from)}";
        }

        // ── The hire sheet (GG-T2020, GG-T2090): the kit's sheet (Ui.Sheet) with the pitch in full, the lengths as a
        // segmented pick, Haggle and Hire ──

        private VisualElement sheet;
        private string sheetClass;
        private int chosenTerm;

        private void OpenSheet(MercenaryDef m)
        {
            CloseSheet(false);
            sheetClass = m.Class;
            chosenTerm = 0;
            VisualElement s = null;
            s = sheet = Ui.Sheet("merc-sheet", () => { if (sheet == s) { sheet = null; sheetClass = null; } });
            RenderSheet();
        }

        private void CloseSheet(bool animate)
        {
            var s = sheet;
            sheet = null;
            sheetClass = null;
            if (s != null) Ui.CloseSheet(s, animate);
        }

        private void RenderSheet()
        {
            var sh = sheet;
            var m = sheetClass == null ? null : Mercenaries.Board(C, S).FirstOrDefault(x => x.Class == sheetClass);
            if (sh == null || sh.parent?.parent == null || m == null) { CloseSheet(false); return; }
            sh.Clear();
            var def = C.Class(m.Class);

            var head = Ui.El("row", sh);
            head.style.alignItems = Align.Center;
            Ui.Thumb(m.Class, head, Size.Avatar);
            var who = Ui.El("col grow", head);
            who.style.flexShrink = 1; who.style.minWidth = 0;
            var top = Ui.El("row", who);
            top.style.alignItems = Align.Center;
            Ui.Name(def.Name, top);
            Ui.RoleBadge(def.Role, top).AddToClassList("merc-role");
            Ui.Caption(m.Temperament.ToString(), who, "dim");
            Ui.IconButton(RowKind.Close, () => CloseSheet(true), head).name = "merc-sheet-close";

            var pitch = Ui.Para($"\"{m.Pitch}\"", sh, "dim");
            pitch.style.flexShrink = 0; pitch.style.minWidth = 0; pitch.style.whiteSpace = WhiteSpace.Normal; // GG-T1897: the full quote wraps to its height, never cut off
            GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, def.Support), "kit-text gold", sh); // GG-T298
            int adj = Mercenaries.HaggleAdjust(S, m.Class);
            if (adj != 0) Ui.Caption(adj < 0 ? $"Haggled down {-adj}%" : $"Price up {adj}%", sh, adj < 0 ? "gold" : "bad");
            if (Mercenaries.Walked(S, m.Class)) { Ui.Para("Walked off. Back after the next battle.", sh, "bad"); return; }

            // A segment picks a length (a pick is not an action); Hire names its price and is the one commit.
            var terms = m.Terms.OrderBy(x => x).ToArray();
            if (!terms.Contains(chosenTerm))
                chosenTerm = terms.Where(t => S.Gold >= Mercenaries.Price(C, S, m.Class, t)).DefaultIfEmpty(terms[0]).First();
            Button hire = null;
            var seg = Ui.Segmented(terms.Select(t => $"{t} battles").ToArray(), System.Array.IndexOf(terms, chosenTerm),
                i => { chosenTerm = terms[i]; SyncHire(hire, m); }, sh, "merc-terms");
            var segs = seg.Query<Button>().ToList();
            for (int i = 0; i < terms.Length && i < segs.Count; i++)
            {
                segs[i].name = $"merc-hire-{m.Class}-{terms[i]}";
                Ui.SetEnabled(segs[i], S.Gold >= Mercenaries.Price(C, S, m.Class, terms[i]) && !haggling);
            }
            Ui.Note("Paid now. The contract runs down every battle, used or not.", sh);

            var foot = Ui.El("kit-footer", sh);
            var haggle = RowIcon.IconButton(Mercenaries.Haggled(S, m.Class) ? "Haggled" : "Haggle", RowKind.Shop, () => App.StartCoroutine(Haggle(m)), null, foot, confirmTap: true);
            haggle.name = "merc-haggle-" + m.Class;
            Ui.SetEnabled(haggle, !Mercenaries.Haggled(S, m.Class) && !haggling);
            Ui.El("grow", foot);
            hire = RowIcon.IconButton("Hire", RowKind.Confirm, () => Hire(m, chosenTerm, Mercenaries.Price(C, S, m.Class, chosenTerm)), "btn--primary", foot, confirmTap: true);
            hire.name = "merc-hire";
            SyncHire(hire, m);
        }

        /// <summary>The commit names the picked length's price, and is off when it can't be paid.</summary>
        private void SyncHire(Button hire, MercenaryDef m)
        {
            int price = Mercenaries.Price(C, S, m.Class, chosenTerm);
            bool ready = m.Terms.Contains(chosenTerm) && S.Gold >= price && !haggling;
            hire.Q<Label>().text = ready ? $"Hire · {Ui.Gold(price)}" : "Hire"; // the IconButton's label, beside its icon
            Ui.SetEnabled(hire, ready);
        }

        private void Hire(MercenaryDef m, int term, int price)
        {
            // The price shown may be stale (a haggle landed since): never charge a different amount than shown.
            if (haggling || Mercenaries.Price(C, S, m.Class, term) != price) { App.Toast("The price has changed"); App.Audio.Sfx("ui.error"); RenderSheet(); return; }
            var r = Mercenaries.Hire(C, S, m.Class, term);
            if (r.Ok) CloseSheet(false);
            Done(r);
        }

        // The merc weighs the offer for a beat before answering (law 5: nothing lands in a single frame).
        private IEnumerator Haggle(MercenaryDef m)
        {
            if (haggling) yield break;
            haggling = true;
            RenderSheet(); // Hire and Haggle stay off while the merc thinks
            App.Audio.Sfx("ui.press");
            if (sheet != null)
            {
                var think = Ui.Heading("Hmm", sheet, "gold");
                yield return Tween.Run(Timing.Dramatic, k => { think.style.opacity = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)); });
            }
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
            tag.AddToClassList("merc-tag");
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
        /// <summary>GG-T1206: the renewing mercenary's portrait and backdrop load into its own Screen art scope, released when it closes.</summary>
        public override bool HoldsScreenArt => true;

        private static ContentDb C => GameApp.I.Content;
        private static SaveData S => GameApp.I.Save;

        protected override void Build(VisualElement root)
        {
            var def = C.Class(classId);
            var m = C.TryMercenary(classId);
            var k = Mercenaries.Contract(S, classId);
            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, HubScreen.ThemeFor(C, S), 2);
            bg.style.unityBackgroundImageTintColor = Palette.Backdrop.Mercenary;

            var portrait = Ui.El(null, root);
            portrait.name = "merc-portrait";
            portrait.style.position = Position.Absolute;
            portrait.style.left = portrait.style.right = Fit.Inset + Fit.Edge; portrait.style.top = Fit.H(6.3f); portrait.style.bottom = Fit.H(38.7f);
            portrait.style.backgroundImage = new StyleBackground(Art.PortraitFigure(classId));
            portrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            portrait.style.opacity = 0f;

            var box = Ui.Panel(root, null, false, "merc-renewal-box");
            box.style.position = Position.Absolute;
            box.style.left = box.style.right = Fit.Inset + Fit.Edge; box.style.bottom = Space.S64;
            var plate = Ui.El("row", box);
            Ui.Heading(def.Name, plate, "gold");
            MercenaryCamp.Tag(S, classId, plate);
            var line = Ui.Para("", box);
            line.style.minHeight = Size.Thumb; // the line types out into a box already its height
            string speech = m?.Renewal ?? "My contract's nearly up. Renew, or do we part ways?";

            var choices = Ui.El("col kit-gate", box);
            choices.style.opacity = 0f;
            if (m != null && k != null)
            {
                int loyalty = Mathf.RoundToInt(Mercenaries.Loyalty(k) * 100f);
                Ui.Caption($"Loyalty discount: {loyalty}%", choices, "gold");
                foreach (var t in m.Terms.OrderBy(x => x))
                {
                    int term = t, price = Mercenaries.RenewalPrice(C, S, classId, t);
                    var b = RowIcon.IconButton($"Renew {term} more battles · {Ui.Gold(price)}", RowKind.Confirm, () => Answer(Mercenaries.Renew(C, S, classId, term)), null, choices, confirmTap: false);
                    b.name = $"merc-renew-{term}";
                    Ui.SetEnabled(b, S.Gold >= price);
                }
            }
            var finish = RowIcon.IconButton("Finish the contract (1 battle left)", RowKind.Confirm, () => { Mercenaries.FinishContract(S, classId); Answer(Result.Success($"{def.Name} will see out the last battle")); }, null, choices, confirmTap: false);
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
