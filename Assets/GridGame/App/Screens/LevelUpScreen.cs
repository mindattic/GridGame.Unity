using System.Collections;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// One hero at a time: portrait slam, an XP bar that ticks through every level crossed, then stat
    /// deltas — fanfare between a battle and PostBattleScreen (owner direction 2026-10-11: "pomp and
    /// circumstance... the same tempo and pause that makes video games enjoyable"). Reuses
    /// CharacterSelectScreen's SlamIn reveal and CutIns.ChargeAnim's tween-driven bar-fill idiom.
    /// Only ever reached via <see cref="PostBattleScreen.RouteFrom"/>, which skips it entirely when
    /// nobody leveled up. Each hero's <see cref="NextAbility"/> shows beneath the stat deltas with a Learn button.
    /// </summary>
    public sealed class LevelUpScreen : GameScreen
    {
        private readonly BattleReport report;
        private readonly string stageId;
        private int index;
        private Coroutine running;
        private readonly List<Coroutine> reveals = new List<Coroutine>();
        private VisualElement portraitRow, xpBar, statColumn;
        private Label levelLabel;
        private bool skipHandlerBound;
        /// <summary>Set once the router pops this screen. The fanfare coroutine runs on GameApp, not on this screen, so
        /// without this it played on after a ResetTo (the harness's randombattle calls one mid-fanfare): it rebuilt a
        /// popped screen against a generated stage already pruned ("Unknown stage 'skirmish-…'") and its Advance()
        /// Replace()d whatever screen had just been mounted (GG-T127).</summary>
        private bool hidden;

        public LevelUpScreen(BattleReport report, string stageId) { this.report = report; this.stageId = stageId; }
        public override string Id => "levelup";
        public override string Music => music ??= App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.LevelUp) ?? "heroic-age-00";
        private string music;

        public override void OnHidden()
        {
            hidden = true;
            if (running != null) App.StopCoroutine(running);
            running = null;
            foreach (var co in reveals) if (co != null) App.StopCoroutine(co);
            reveals.Clear();
        }

        protected override void Build(VisualElement root)
        {
            var c = App.Content;
            var (classId, from, _) = report.LevelUps[index];
            var def = c.Class(classId);

            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, PostBattleScreen.ThemeOf(c, report, stageId), 1);
            bg.style.unityBackgroundImageTintColor = Palette.BackdropDim;
            var col = Ui.El("fill col center", root);

            var head = Ui.Display("LEVEL UP", col, "shadow");
            head.name = "levelup-title";
            head.style.fontSize = TypeScale.Celebration;
            head.style.unityTextAlign = TextAnchor.MiddleCenter;
            head.style.color = Palette.Gold;
            Ui.Margin(head, bottom: Space.S32);

            portraitRow = Ui.El("row", col);
            portraitRow.style.opacity = 0f;
            var frame = Ui.El(null, portraitRow);
            frame.style.width = Size.PortraitW; frame.style.height = Size.PortraitH;
            frame.style.borderTopWidth = frame.style.borderBottomWidth = frame.style.borderLeftWidth = frame.style.borderRightWidth = Size.Hairline;
            var roleColor = Ui.RoleColor(def.Role);
            frame.style.borderTopColor = frame.style.borderBottomColor = frame.style.borderLeftColor = frame.style.borderRightColor = roleColor;
            frame.style.backgroundColor = Palette.Ink.WithA(0.6f);
            frame.style.backgroundImage = new StyleBackground(Art.PortraitFigure(classId));
            frame.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);

            var info = Ui.El("col", portraitRow);
            Ui.Margin(info, left: Space.S24); info.style.width = Fit.C(27f);
            Ui.Heading(def.Name, info, "shadow").name = "levelup-name";
            Ui.Margin(Ui.RoleBadge(def.Role, info), bottom: Space.S8);
            levelLabel = Ui.Heading($"Lv {from}", info, "shadow gold");
            levelLabel.name = "levelup-level";
            xpBar = Ui.ProgressBar(0f, info, null, "levelup-xp");
            Ui.Margin(xpBar, top: Space.S8);

            statColumn = Ui.El("col", col);
            Ui.Margin(statColumn, top: Space.S24);

            // GG-T1556 (§22.8): the hero's next ability on the skill hexagon, with Learn to its Board route. Learn leaves the
            // fanfare for the results and opens the Board on top of them, so Back from the Board lands on the results.
            var heroSave = App.Save.Hero(classId);
            var next = NextAbility.For(c, heroSave);
            if (next != null)
            {
                var nextRow = Ui.El("row", col);
                nextRow.name = "levelup-next-ability";
                Ui.Margin(nextRow, top: Space.S24); nextRow.style.alignItems = Align.Center;
                var line = Ui.Para(NextAbility.Line(c, heroSave), nextRow, "gold");
                line.style.flexShrink = 1;
                Ui.Margin(line, right: Space.S16);
                RowIcon.IconButton("Learn", RowKind.Ability, () => Learn(classId, next), "btn--small", nextRow).name = "levelup-learn";
            }

            // Root is reused across Refresh() calls (one per hero that leveled up) — Root.Clear() drops
            // child elements but not callbacks registered on Root itself, so this must only ever bind once
            // or each additional hero stacks another handler and a single tap fires Skip() that many times.
            // A tap on the Skip button's own ClickEvent also bubbles up to root — without the Button filter one
            // tap on "Skip ›" ran Skip() twice and jumped past the next hero's fanfare too (GG-T70).
            if (!skipHandlerBound) { root.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) Skip(); }); skipHandlerBound = true; }
            var skip = RowIcon.IconButton("Skip", RowKind.Enter, Skip, "btn--small", root);
            skip.style.position = Position.Absolute; skip.style.right = Space.S32; skip.style.bottom = Space.S48;

            // A rebuild mid-fanfare (a resize) restarts this hero's fanfare: stop the one already playing, or two ran at
            // once and each Advance()d (a hero skipped, PostBattle Replace()d twice).
            if (running != null) App.StopCoroutine(running);
            foreach (var co in reveals) if (co != null) App.StopCoroutine(co);
            reveals.Clear();
            running = App.StartCoroutine(PlayHero());
        }

        /// <summary>Android Back skips the rest of the fanfare to the results, like Skip on the last hero — it must not
        /// pop past them (the results' Continue runs the ending, offers and story beats).</summary>
        public override bool OnBack()
        {
            if (hidden || leaving) return false;
            leaving = true;
            if (running != null) App.StopCoroutine(running);
            running = null;
            App.Router.Replace(new PostBattleScreen(report, stageId));
            return false;
        }

        private bool leaving;

        private void Learn(string heroId, NextAbility.Pick next)
        {
            if (hidden || leaving) return;
            leaving = true;
            if (running != null) App.StopCoroutine(running);
            running = null;
            App.Router.Replace(new PostBattleScreen(report, stageId));
            App.Router.Push(HeroScreen.Learn(heroId, next));
        }

        private void Skip()
        {
            if (hidden) return;
            if (running != null) App.StopCoroutine(running);
            running = null;
            Advance();
        }

        private void Advance()
        {
            if (hidden || leaving) return;
            index++;
            if (index >= report.LevelUps.Count) { leaving = true; App.Router.Replace(new PostBattleScreen(report, stageId)); return; }
            Refresh();
        }

        private IEnumerator PlayHero()
        {
            var (classId, from, to) = report.LevelUps[index];
            var def = App.Content.Class(classId);
            var hero = App.Save.Hero(classId);

            App.Audio.DuckFor(Timing.Cinematic * 3f); // POL-F3: the fanfare plays over a ducked track
            yield return SlamIn(portraitRow);
            yield return Tween.Wait(Timing.Standard);

            // A battle can carry a hero past more than one level; tick fully through each one crossed
            // (bar fills, "Lv N" pops, resets) before a true partial fill for the level landed on.
            for (int lvl = from; lvl < to; lvl++)
            {
                App.Audio.Sfx("battle.crit", 0.6f, 0.8f + 0.1f * (lvl - from));
                yield return Tween.Run(Timing.Standard, k => Ui.SetProgress(xpBar, k));
                levelLabel.text = $"Lv {lvl + 1}";
                reveals.Add(App.StartCoroutine(Tween.Pop(levelLabel, 1.3f, 0.2f)));
                yield return Tween.Wait(Timing.Quick);
                Ui.SetProgress(xpBar, 0f);
            }
            int intoFinal = hero != null ? hero.TotalXp - Formulas.TotalXpForLevel(to) : 0;
            float finalFrac = Mathf.Clamp01(intoFinal / (float)Formulas.XpToNext(to));
            App.Audio.Sfx("battle.orb", 0.5f);
            yield return Tween.Run(Timing.Emphasis, k => Ui.SetProgress(xpBar, Mathf.Lerp(0f, finalFrac, k)));
            yield return Tween.Wait(Timing.Standard);

            var before = Formulas.StatsAtLevel(def, from);
            var after = Formulas.StatsAtLevel(def, to);
            foreach (var (label, b, a) in StatRows(before, after))
            {
                var row = Ui.Field(label, $"{b:0} to {a:0} · +{a - b:0}", statColumn, "good", "levelup-stat-" + label.ToLowerInvariant());
                reveals.Add(App.StartCoroutine(Tween.Pop(row, 1.15f, 0.15f)));
                yield return Tween.Wait(Timing.Instant);
            }
            yield return Tween.Wait(Timing.Cinematic); // the fanfare hold
            running = null;
            Advance();
        }

        private static IEnumerable<(string label, float before, float after)> StatRows(Stats before, Stats after)
        {
            if (after.Strength > before.Strength) yield return ("STR", before.Strength, after.Strength);
            if (after.Vitality > before.Vitality) yield return ("VIT", before.Vitality, after.Vitality);
            if (after.Agility > before.Agility) yield return ("AGI", before.Agility, after.Agility);
            if (after.Speed > before.Speed) yield return ("SPD", before.Speed, after.Speed);
            if (after.Intelligence > before.Intelligence) yield return ("INT", before.Intelligence, after.Intelligence);
            if (after.Wisdom > before.Wisdom) yield return ("WIS", before.Wisdom, after.Wisdom);
            if (after.Luck > before.Luck) yield return ("LCK", before.Luck, after.Luck);
        }

        /// <summary>Bombastic reveal, verbatim from CharacterSelectScreen.SlamIn: oversized and tilted,
        /// punches down past full size with a back-ease overshoot, then a small settle shake.</summary>
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
    }
}
