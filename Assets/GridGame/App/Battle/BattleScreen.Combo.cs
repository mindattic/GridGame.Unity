using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// The combo (GG-T851, BIBLE §1.2.8) off the board (GG-T1828, §9.0): no meter frame or contributor chips. It plays
    /// <see cref="ComboChangedEvent"/> without holding up the beat (a rank-up only plays its rising sting) and
    /// <see cref="ComboEndedEvent"/> as one brief rank callout ("Combo · A!", <see cref="CombatTextKind.Callout"/> in the
    /// rank's colour) with its <c>combo.rank.*</c> cue (GG-T852). The battle's best rank, score and each hero's points
    /// show on the results screen.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private ComboRank comboShownRank;
        private int comboHits;
        /// <summary>The unit with the largest share of the last closed combo, for the Combo lesson's mark.</summary>
        private int comboTopUnit = -1;

        private static Color ComboColor(ComboRank r)
        {
            switch (r)
            {
                case ComboRank.D: return new Color(0.62f, 0.7f, 0.82f);
                case ComboRank.C: return new Color(0.45f, 0.9f, 0.55f);
                case ComboRank.B: return new Color(0.35f, 0.8f, 1f);
                case ComboRank.A: return new Color(0.78f, 0.5f, 1f);
                case ComboRank.S: return new Color(1f, 0.84f, 0.25f);
                case ComboRank.SS: return new Color(1f, 0.55f, 0.15f);
                case ComboRank.SSS: return new Color(1f, 0.22f, 0.3f);
                default: return Palette.TextDim;
            }
        }

        /// <summary>One combo credit: the rank is tracked and a rank-up plays a sting that climbs with the rank.</summary>
        private IEnumerator ComboChangedAnim(ComboChangedEvent e)
        {
            comboHits++;
            if (e.Rank > comboShownRank)
            {
                int step = Mathf.Max(0, (int)e.Rank - 1);
                App.Audio.Sfx("battle.powerup", 0.6f + 0.05f * step, 0.9f + 0.08f * step);
            }
            comboShownRank = e.Rank;
            return null;
        }

        /// <summary>The window closed: the rank callout, then the tracker resets for the next combo.</summary>
        private IEnumerator ComboEndedAnim(ComboEndedEvent e)
        {
            comboHits = 0;
            comboShownRank = ComboRank.None;
            int best = 0;
            foreach (var s in e.Breakdown)
                if (s.Total > best) { best = s.Total; comboTopUnit = s.UnitId; }
            return ComboCallout(e);
        }

        /// <summary>The cue for a closing rank (GG-T852): <c>combo.rank.d</c> .. <c>combo.rank.sss</c>, placeholders listed in
        /// docs/SFX.md (silent until their files land; the rank-up sting and <c>ui.diamond</c> still play).</summary>
        private static string ComboRankCue(ComboRank r) => r == ComboRank.None ? null : "combo.rank." + r.ToString().ToLowerInvariant();

        /// <summary>The rank callout (GG-T1828): "Combo · A!" floats over the board's upper middle in the rank's colour,
        /// grown with the rank, as one brief beat.</summary>
        private IEnumerator ComboCallout(ComboEndedEvent e)
        {
            if (e.Rank == ComboRank.None) yield break;
            App.Audio.Sfx(ComboRankCue(e.Rank));
            if (e.Rank >= ComboRank.S) App.Audio.Sfx("ui.diamond", 0.7f);
            var at = new Vector2(BoardView.Tile * BattleConfig.Cols / 2f, BoardView.Tile * BattleConfig.Rows * 0.3f);
            if (board != null) Text.Show(at, CombatTextKind.Callout, $"Combo · {e.Rank}!", Mathf.Clamp01(((int)e.Rank - 1) / 6f), ComboColor(e.Rank));
            yield return Tween.Wait(Timing.Standard);
        }

        /// <summary>The combo for AutomationServer GET /combo (GG-T852): the rank playback has reached, the model's open window (the
        /// tracker on <see cref="State"/>, which may run ahead of playback) and the battle's best combo and top supporter.</summary>
        public object ComboReport()
        {
            var c = State?.Combo;
            var r = State?.Results;
            string top = null; int topPts = 0;
            if (r != null)
                foreach (var kv in r.ComboSupporterPoints)
                    if (kv.Value > topPts || (kv.Value == topPts && topPts > 0 && string.CompareOrdinal(kv.Key, top) < 0)) { top = kv.Key; topPts = kv.Value; }
            return new
            {
                meter = new
                {
                    shownRank = comboShownRank.ToString(),
                    hits = comboHits,
                },
                window = c == null ? null : new
                {
                    score = c.Score, rank = c.Rank.ToString(), hits = c.Hits,
                    shares = c.Shares.ConvertAll(x => new { unit = x.UnitId, classId = x.ClassId, attacker = x.AttackerPoints, supporter = x.SupporterPoints }),
                },
                best = r == null ? null : new { rank = r.BestComboRank.ToString(), score = r.BestComboScore },
                topSupporter = top == null ? null : new { classId = top, points = topPts },
                attackerPoints = r?.ComboAttackerPoints,
                supporterPoints = r?.ComboSupporterPoints,
            };
        }
    }
}
