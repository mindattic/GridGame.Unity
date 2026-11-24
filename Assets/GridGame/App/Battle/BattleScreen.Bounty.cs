using System.Collections;
using GridGame.Core.Battle;
using UnityEngine;

namespace GridGame.App
{
    public sealed partial class BattleScreen
    {
        /// <summary>A bounty target (§22.4, GG-T431) arrives or falls. Arriving: a banner with its title, a war cry and a
        /// pop of the unit wearing the skull. Falling: a "BOUNTY SLAIN" float and the pickup cue, since its reward now waits at the board.</summary>
        private IEnumerator BountyAnim(BountyTargetEvent e)
        {
            if (!e.Slain)
            {
                ShowBanner($"☠ Bounty: {e.Title}");
                App.Audio.Sfx("battle.warcry");
                if (board.Units.TryGetValue(e.UnitId, out var v)) App.StartCoroutine(Tween.Pop(v.Root, 1.15f, Timing.Emphasis));
                yield return FloatText(e.UnitId, CombatTextKind.Callout, "BOUNTY", 0.9f, 0.8f, Palette.Bad);
                yield break;
            }
            App.Audio.Sfx("battle.pickup");
            ShowBanner($"☠ {e.Title} slain! Collect at the bounty board");
            yield return FloatText(e.UnitId, CombatTextKind.Callout, "BOUNTY SLAIN", 0.9f, 0.8f, Palette.Gold);
        }
    }
}
