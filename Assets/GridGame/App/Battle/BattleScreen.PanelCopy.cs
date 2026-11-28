using GridGame.Core.Content;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1953 (UX review #7): every instruction line the description panel shows, one verb-first line per state, in one
    /// table. The tips that used to ride on the idle line live in Info (Examine mode, GG-T1918) as <see cref="ExamineTips"/>.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private enum PanelState
        {
            NoHero, Trap, NoTrap, Acted,
            AimUnit, AimMulti, AimDirection, TapMove,
            Examine, ExamineCard,
            HintUse, HintGuard, HintDrag, HintTrap, InfoTip,
        }

        /// <summary>The panel's instruction for <paramref name="s"/>. <paramref name="hero"/> and <paramref name="a"/> fill
        /// the hero's and the move's names; <paramref name="n"/> is a count (tiles walked, picks allowed).</summary>
        private static string PanelCopy(PanelState s, string hero = null, AbilityDef a = null, int n = 0, string extra = "")
        {
            switch (s)
            {
                case PanelState.NoHero: return "Tap a hero to command them";
                case PanelState.Trap: return $"Drag {hero} to a gold dot to trap a foe";
                case PanelState.NoTrap: return $"Move {hero} to set up a trap, or open Abilities";
                case PanelState.Acted: return $"Tap a hero who hasn't acted; {hero} is done this turn";
                case PanelState.AimUnit: return $"Tap a highlighted {AimNoun(a)} for {a.Name}";
                case PanelState.AimMulti: return $"Tap up to {n} highlighted {(a.Filter == TargetFilter.AllyOnly ? "allies" : AimNoun(a) + "s")} for {a.Name}, then {CommitVerb(a)}";
                case PanelState.AimDirection: return $"Tap an arrow to aim {a.Name}";
                case PanelState.TapMove: return $"Move {hero} {n} tile{(n == 1 ? "" : "s")}{extra}";
                case PanelState.Examine: return "Tap anything to read it";
                case PanelState.ExamineCard: return "Tap anything else to read it, or tap Info to close";
                case PanelState.HintUse: return $"Open Abilities: {hero} can use {a.Name}";
                case PanelState.HintGuard: return "Press GUARD when no move helps";
                case PanelState.HintDrag: return $"Drag {hero} to the glowing tile";
                case PanelState.HintTrap: return "Trap a foe between two heroes";
                case PanelState.InfoTip: return "Tap Info, then tap anything to read it";
                default: return "";
            }
        }

        /// <summary>The tips Info shows under the turn rules while nothing is examined.</summary>
        private static readonly string[] ExamineTips =
        {
            "Hold Abilities to repeat a hero's last move",
            "Drag a hero, or tap a tile, to move them",
            "Hold any hero, foe or icon to read it without Info",
        };

        /// <summary>The confirm button's word for <paramref name="a"/>.</summary>
        private static string CommitVerb(AbilityDef a) => a.Kind == AbilityKind.Item ? "Use" : a.Kind == AbilityKind.Spell ? "Cast" : "Perform";
    }
}
