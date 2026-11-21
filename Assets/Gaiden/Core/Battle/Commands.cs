using System.Collections.Generic;

namespace GridGame.Core.Battle
{
    /// <summary>A player decision. Exactly one command is applied per committed action. Plain data only (ids, tiles,
    /// numbers) and stamped with the seat that issued it (GG-LAW-13); <see cref="CommandCodec"/> serializes it.</summary>
    public abstract class BattleCommand
    {
        /// <summary>Who issued this command. Seat 0 (the default) is the single-player game's only seat.</summary>
        public PlayerId Player;
    }

    /// <summary>Drag a hero along a path of cardinal one-tile steps (the first entry is the next tile, not the start).</summary>
    public sealed class MoveCommand : BattleCommand
    {
        public int HeroId;
        public List<Coord> Path = new List<Coord>();
        /// <summary>Drag-timer seconds left at drop (0 when the timer is off).</summary>
        public float TimeLeftSeconds;
        /// <summary>
        /// Real seconds the hero was held (GG-T188, §2.0). At or past <see cref="BattleEngine.TurnWindowSeconds"/> the
        /// drop was forced by the next enemy icon reaching ACT: the turn ends even if the hero never left its tile.
        /// </summary>
        public float HeldSeconds;
        public override string ToString() => $"Move {HeroId} [{string.Join(" ", Path)}]";
    }

    /// <summary>Use the ability/item in a bar slot. Target is the picked tile (actor or tile picks); ignored for Auto.</summary>
    public sealed class UseSlotCommand : BattleCommand
    {
        public int HeroId;
        public int Slot;
        public Coord Target;
        public override string ToString() => $"Use {HeroId} slot{Slot} @{Target}";
    }

    /// <summary>Raise shields (Guard): every hero gains Protection, the unused turn time fills the mana bubble
    /// (GG-T189) and the clock runs to the next enemy turn (§2.5).</summary>
    public sealed class ShieldCommand : BattleCommand
    {
        /// <summary>Real seconds of the turn already used before Guard (0 for a Guard pressed at the turn's start);
        /// only the rest of the turn window converts to mana charge.</summary>
        public float HeldSeconds;
        public override string ToString() => "Shield";
    }

    /// <summary>GG-T542: the tutorial battle sets up a lesson's situation (a two-pincer chain, a charging foe, a plated
    /// foe ...) the moment that lesson opens, so every lesson has something on the board to point at. Refused outside a
    /// tutorial battle (<see cref="BattleState.Tutorial"/>); takes no time and no hero's turn. <see cref="Scene"/> is a
    /// lesson id (BattleRunner.Tutorial.cs lists them); the stage's Scenes say which foe each one brings.</summary>
    public sealed class TutorialSceneCommand : BattleCommand
    {
        public string Scene;
        public override string ToString() => $"Scene {Scene}";
    }
}
