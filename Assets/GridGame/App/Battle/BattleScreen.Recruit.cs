using System.Collections;
using System.Linq;
using GridGame.Core.Battle;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Mid-battle recruitment on the board (§25.11.1, GG-T132): a neutral recruitable stands apart in an amber
    /// TALK frame; tap it with a hero beside it to talk. Unmet terms: it says what it wants (no time passes). Met: it
    /// speaks its join line and turns guest-green — standing at a victory, it joins the caravan.</summary>
    public sealed partial class BattleScreen
    {
        private static readonly Color RecruitAmber = Palette.Neutral;

        /// <summary>A tap on a neutral: talk with the selected hero if it stands beside it, else any hero that does.</summary>
        private void TapRecruitable(Unit neutral)
        {
            var hero = State.UnitById(SelectedHeroId);
            if (!Recruitment.CanTalk(State, hero, neutral)) hero = State.Units.FirstOrDefault(h => Recruitment.CanTalk(State, h, neutral));
            App.Audio.Sfx("ui.click");
            if (hero == null)
            {
                var wants = neutral.Recruitable?.Conditions.Where(c => c != null).Select(c => Recruitment.Describe(App.Content, c)) ?? Enumerable.Empty<string>();
                ShowBanner($"Move a hero beside {neutral.Name} to talk — wants: {string.Join(", ", wants)}");
                return;
            }
            Commit(new TalkCommand { HeroId = hero.Id, TargetId = neutral.Id });
        }

        /// <summary>A recruitable steps onto the board (amber frame), and the billboard says what it will ask for.</summary>
        private IEnumerator RecruitableAppearedAnim(RecruitableAppearedEvent e)
        {
            var u = State.UnitById(e.UnitId);
            if (u == null) yield break;
            var v = board.Units.TryGetValue(u.Id, out var ex) ? ex : board.Add(u);
            board.Place(v, e.At);
            v.SetHp(u.Hp, u.MaxHp);
            v.SetTeamLook(u);
            App.Audio.Sfx("battle.pickup", 0.7f, 0.85f);
            // GG-T448: e.Wants is one readable phrase per unmet condition, so a recruit with several can run long —
            // the banner shows at most two and tallies the rest, rather than relying on a tiny floor-size font.
            string wants = e.Wants.Count <= 2 ? string.Join(", ", e.Wants) : $"{string.Join(", ", e.Wants.Take(2))} (+{e.Wants.Count - 2} more)";
            Billboard($"{u.Name} stands apart. Talk to them! Wants: {wants}", null, RecruitAmber);
            v.Root.style.opacity = 0f;
            yield return Tween.Run(Timing.Standard, k => { v.Root.style.opacity = k; v.Root.style.translate = new Translate(0, -60f * (1f - k)); }, Tween.EaseOut);
            v.Root.style.opacity = 1f;
        }

        /// <summary>Talked to, terms unmet: its lines, then what it still wants over its head. No time passed.</summary>
        private IEnumerator RecruitOfferedAnim(RecruitOfferedEvent e)
        {
            App.Audio.Sfx("ui.click", 0.8f);
            foreach (var line in e.Lines) yield return SurpriseAnnounce(line.SpeakerClassId, line.Text);
            var u = State.UnitById(e.UnitId);
            ShowBanner($"{u?.Name} wants: {string.Join(", ", e.Wants)}");
            yield return FloatText(e.UnitId, CombatTextKind.Callout, "?", 0.35f, 0.65f, RecruitAmber);
        }

        /// <summary>Talked round: its join line, the gold handed over, and its frame turns guest-green as it joins.</summary>
        private IEnumerator RecruitJoinedAnim(RecruitJoinedEvent e)
        {
            foreach (var line in e.Lines) yield return SurpriseAnnounce(line.SpeakerClassId, line.Text);
            var u = State.UnitById(e.UnitId);
            if (u == null || !board.Units.TryGetValue(u.Id, out var v)) yield break;
            App.Audio.Sfx("battle.victory", 0.6f);
            if (e.GoldPaid > 0) App.StartCoroutine(FloatText(e.UnitId, CombatTextKind.Support, $"-{e.GoldPaid} G", 0.6f, 0f, Palette.Gold));
            Billboard($"{u.Name} joins the fight! Keep them standing to recruit them.", null, new Color(0.6f, 1f, 0.75f));
            if (ReduceMotion) { v.SetTeamLook(u); yield break; }
            // GG-T1072: the tile turns over like a card, showing its back past 90°, and comes round guest-green.
            App.Audio.Sfx("battle.whoosh", 0.6f, 1.1f);
            yield return v.FlipCard(Timing.Dramatic, 2, () => v.SetTeamLook(u));
            v.Root.style.scale = new Scale(Vector2.one);
        }
    }
}
