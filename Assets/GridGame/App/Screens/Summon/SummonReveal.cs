using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>A banner pull's reveal (GG-T343, GG-T1429): each result in pull order plays over time — the portrait swells
    /// out of the light, a <b>New</b> or <b>Echo</b> badge lands with its own cue, and a featured hero gets a gold flash
    /// and a bigger fanfare. A tap finishes the playing result, the next tap moves on. <b>Skip</b> (multi-pulls) and the
    /// end of a multi-pull open a summary grid of every result; Continue closes it. Under Reduce Motion each result
    /// fades in at full size with no swell, pop or flash. A new hero whose pull fizzled (refunded) isn't shown.</summary>
    internal static class SummonReveal
    {
        private const float PortraitSize = 420;

        public static void Show(IReadOnlyList<PullRecord> pulls, BannerDef banner, Action onClose)
        {
            var app = GameApp.I;
            var shown = pulls.Where(p => app.Content.TryClass(p.ClassId) != null && (p.Result != PullResult.New || app.Save.Hero(p.ClassId) != null)).ToList();
            if (shown.Count == 0) { onClose?.Invoke(); return; }
            bool calm = app.Settings.ReduceMotion;
            var dim = Palette.Ink.WithA(0.9f);
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = "summon-reveal";
            veil.pickingMode = PickingMode.Position;
            // The reveal's name and role are bare text, not a window: the plain veil left the Summon Circle's own text
            // showing through them. Dim the page almost out so the hero carries the moment.
            veil.style.backgroundColor = dim;
            var stage = Ui.El("col center", veil);
            stage.name = "summon-reveal-stage";
            int at = -1;
            bool ready = false, summary = false;
            Coroutine playing = null;
            Action settle = null;
            Button skip = null;
            if (shown.Count > 1)
            {
                skip = Ui.Button("Skip", () => Summary(), "btn--small", veil);
                skip.name = "summon-reveal-skip";
                skip.style.position = Position.Absolute;
                skip.style.top = skip.style.right = Space.S24;
                skip.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            }

            veil.RegisterCallback<ClickEvent>(_ =>
            {
                if (summary) return;
                if (!ready) { Stop(); settle?.Invoke(); ready = true; return; }
                Next();
            });
            Next();

            void Stop()
            {
                if (playing != null) app.StopCoroutine(playing);
                playing = null;
            }

            void Close()
            {
                Stop();
                veil.RemoveFromHierarchy();
                onClose?.Invoke();
            }

            void Next()
            {
                if (++at >= shown.Count) { if (shown.Count > 1) Summary(); else Close(); return; }
                ready = false;
                stage.Clear();
                veil.style.backgroundColor = dim;
                var p = shown[at];
                var def = app.Content.TryClass(p.ClassId);
                bool featured = IsFeatured(banner, p);
                var thumb = Ui.Thumb(def.Id, stage, PortraitSize);
                thumb.name = "summon-reveal-hero";
                var badge = Ui.Text(Badge(p, featured), "h2 shadow " + (p.Result == PullResult.New ? "gold" : "good"), stage);
                badge.name = "summon-reveal-badge";
                badge.style.marginTop = Space.S16;
                var name = Ui.Text(def.Name, "title shadow" + (featured ? " gold" : ""), stage);
                name.style.marginTop = Space.S8;
                var role = Ui.Text(Line(def, p), "h2 shadow", stage);
                var tap = Ui.Text(shown.Count > 1 ? $"{at + 1} of {shown.Count} · Tap to continue" : "Tap to continue", "small dim", stage);
                tap.name = "summon-reveal-tap";
                tap.style.marginTop = Space.S48;
                settle = () =>
                {
                    thumb.style.scale = new Scale(Vector2.one);
                    thumb.style.opacity = badge.style.opacity = name.style.opacity = role.style.opacity = tap.style.opacity = 1f;
                    badge.style.scale = name.style.scale = new Scale(Vector2.one);
                    veil.style.backgroundColor = dim;
                };
                thumb.style.scale = new Scale(calm ? Vector2.one : Vector2.zero);
                thumb.style.opacity = calm ? 0f : 1f;
                badge.style.opacity = name.style.opacity = role.style.opacity = tap.style.opacity = 0f;
                playing = app.StartCoroutine(Play(p, featured, thumb, badge, name, role, tap));
            }

            System.Collections.IEnumerator Play(PullRecord p, bool featured, VisualElement thumb, Label badge, Label name, Label role, Label tap)
            {
                bool isNew = p.Result == PullResult.New;
                app.Audio.Sfx(isNew ? "camp.summon" : "ui.select", isNew ? 0.9f : 0.7f);
                if (calm) yield return Tween.Run(Timing.Quick, k => thumb.style.opacity = k);
                else
                {
                    float peak = featured ? 1.2f : 1.12f;
                    yield return Tween.Run(featured ? Timing.Emphasis : Timing.Standard,
                        k => thumb.style.scale = new Scale(Vector2.one * (k < 0.7f ? k / 0.7f * peak : peak - (peak - 1f) * (k - 0.7f) / 0.3f)), Tween.EaseOut);
                }
                thumb.style.scale = new Scale(Vector2.one);
                if (featured)
                {
                    app.Audio.Sfx("battle.victory");
                    app.Audio.Sfx("battle.warcry", 0.6f);
                    if (!calm) yield return Tween.Run(Timing.Dramatic, k => veil.style.backgroundColor = Color.Lerp(Palette.Gold.WithA(0.55f), dim, k), Tween.EaseOut);
                }
                else app.Audio.Sfx(isNew ? "battle.powerup" : "ui.diamond", isNew ? 0.7f : 0.6f);
                yield return Tween.Run(Timing.Quick, k => badge.style.opacity = name.style.opacity = role.style.opacity = k);
                if (!calm) yield return Tween.Pop(isNew ? name : badge, featured ? 1.3f : 1.15f, featured ? Timing.Standard : 0.2f);
                tap.style.opacity = 1f;
                playing = null;
                ready = true;
            }

            void Summary()
            {
                if (summary) return;
                summary = true;
                Stop();
                stage.Clear();
                veil.style.backgroundColor = dim;
                skip?.RemoveFromHierarchy();
                stage.name = "summon-reveal-summary";
                Ui.Text("The circle answered", "h2 shadow gold", stage).style.marginBottom = Space.S16;
                var grid = Ui.El("row", stage);
                grid.style.flexWrap = Wrap.Wrap;
                grid.style.justifyContent = Justify.Center;
                for (int i = 0; i < shown.Count; i++)
                {
                    var p = shown[i];
                    bool featured = IsFeatured(banner, p);
                    var cell = Ui.El("col center", grid);
                    cell.name = "summon-summary-" + i;
                    cell.style.marginLeft = cell.style.marginRight = cell.style.marginBottom = Space.S8;
                    var t = Ui.Thumb(p.ClassId, cell, Kit.Thumb);
                    if (featured)
                    {
                        t.style.borderTopWidth = t.style.borderBottomWidth = t.style.borderLeftWidth = t.style.borderRightWidth = 3;
                        t.style.borderTopColor = t.style.borderBottomColor = t.style.borderLeftColor = t.style.borderRightColor = Palette.Gold;
                    }
                    Ui.Caption(app.Content.TryClass(p.ClassId)?.Name ?? p.ClassId, cell, featured ? "gold" : null);
                    Ui.Caption(Badge(p, featured), cell, p.Result == PullResult.New ? "gold" : "dim");
                }
                var done = Ui.Button("Continue", Close, "btn--primary", Ui.Actions(stage));
                done.name = "summon-reveal-done";
                done.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                if (!calm) app.StartCoroutine(Tween.Fade(grid, 0f, 1f, Timing.Standard));
            }
        }

        internal static bool IsFeatured(BannerDef banner, PullRecord p) =>
            banner?.Kind == BannerKind.Featured && banner.Featured.Contains(p.ClassId, StringComparer.OrdinalIgnoreCase);

        /// <summary>"New", "New · Featured", "Echo +1 SP", "+5 Ticket fragments".</summary>
        internal static string Badge(PullRecord p, bool featured) => p.Result switch
        {
            PullResult.New => featured ? "New · Featured" : "New",
            PullResult.Echo => $"{Gacha.EchoName} +{Gacha.EchoSp} SP",
            _ => $"+{p.Fragments} {TicketText.Fragments}",
        };

        private static string Line(ClassDef def, PullRecord p) =>
            p.Result == PullResult.New ? $"{def.Role} · answers the circle" : $"{def.Role} · answers again";
    }
}
