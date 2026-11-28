using System;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Core.Rules
{
    /// <summary>
    /// How an ability targets and who it targets, as two generated sentences (BIBLE §9.0, GG-T1816), e.g.
    /// "Pick a unit [adjacent] to you (beside, not diagonal); it strikes that unit alone." and "One foe.". Built from the
    /// targeting data alone (mode, shape, radius, reach, filter and the special picks <see cref="TargetResolver"/> obeys),
    /// so the words never drift from the rules. Keyword terms sit in [brackets] for the glossary.
    /// </summary>
    public static class TargetText
    {
        /// <summary>The aim: no aim, a unit, several units, a way or a tile, where it may be, and the area it covers.</summary>
        public static string How(AbilityDef a)
        {
            if (a == null) return "";
            if (a.Trigger != null || a.Kind == AbilityKind.Passive || a.Kind == AbilityKind.Reactive)
                return "No aim: it fires on its own when its trigger is met.";
            if (a.Escape) return "No aim: the party flees the battle at once.";
            if (a.Revives) return "No aim: the fallen ally rises on a free tile beside you.";
            if (a.Shape == TargetShape.Self || a.Vanishes)
                return a.Vanishes ? "No aim: smoke spreads around your own tile, then you reappear on a random safe tile at least 2 away." : "No aim: it works on you.";
            if (a.Shape == TargetShape.AllEnemies || a.Shape == TargetShape.AllAllies) return "No aim: it reaches the whole board.";

            if (a.Mode == TargetMode.Auto)
            {
                if (IsSingle(a.Shape)) return "No aim: it works on your own tile.";
                if (a.Shape == TargetShape.Wall) return $"No aim: a row of {2 * a.Radius + 1} tiles through your own tile.";
                return $"No aim: {Area(a.Shape, a.Radius)} centred on you ({Cells(a.Shape, a.Radius)} tiles).";
            }

            if (a.Mode == TargetMode.PickMulti)
            {
                string reach = a.AdjacentOnly ? "[adjacent] to you (beside, not diagonal)"
                    : a.MaxRange > 0 ? $"within {a.MaxRange} tile{S(a.MaxRange)} of you"
                    : "anywhere on the board";
                return $"Pick up to {a.MaxPicks} different units {reach}, then Confirm; it works on each one alone, in the order picked.";
            }

            if (a.Mode == TargetMode.PickDirection)
                return IsSingle(a.Shape) ? "Pick a way (one of 4 arrows around you); it works on the tile beside you that way."
                    : $"Pick a way (one of 4 arrows around you); {Area(a.Shape, a.Radius)} through the tile beside you that way, {Cells(a.Shape, a.Radius)} tiles.";

            if (a.Mode == TargetMode.PickActor)
            {
                string reach = a.AdjacentOnly ? "[adjacent] to you (beside, not diagonal)"
                    : a.MaxRange > 0 ? $"within {a.MaxRange} tile{S(a.MaxRange)} of you"
                    : "anywhere on the board";
                string hit = IsSingle(a.Shape) ? "it works on that unit alone" : $"{Area(a.Shape, a.Radius)} centred on it, {Cells(a.Shape, a.Radius)} tiles";
                return $"Pick a unit {reach}; {hit}.";
            }

            if (a.Filter == TargetFilter.EmptyOnly)
            {
                if (a.Afterimage > 0) return $"Pick an empty tile up to {a.Afterimage} tiles away in a straight line (no diagonals), past anything in between.";
                if (a.Vaults) return "Pick the empty tile straight beyond a unit [adjacent] to you.";
                string tile = a.AdjacentOnly ? "Pick an empty, trap-free tile [adjacent] to you" : "Pick any empty, trap-free tile";
                return IsSingle(a.Shape) ? tile + "." : $"{tile}; it covers {Area(a.Shape, a.Radius)} through it, {Cells(a.Shape, a.Radius)} tiles.";
            }
            if (a.Magnetizes > 0) return $"Pick a tile; foes within {a.Radius} tile{S(a.Radius)} of it are dragged up to {a.Magnetizes} toward it.";
            if (IsSingle(a.Shape))
                return a.AdjacentOnly ? "Pick a tile [adjacent] to you; it works on that tile alone." : "Pick a tile; it works on that tile alone.";
            string where = a.AdjacentOnly ? "a tile [adjacent] to you" : "any tile";
            return $"Pick {where}; {Area(a.Shape, a.Radius)} centred on it, {Cells(a.Shape, a.Radius)} tiles, must catch a target.";
        }

        /// <summary>The targets: you, an ally, a foe, anyone or every one of a side, with any special condition, and how many
        /// it can catch at most.</summary>
        public static string Who(AbilityDef a)
        {
            if (a == null) return "";
            if (a.Trigger != null || a.Kind == AbilityKind.Passive || a.Kind == AbilityKind.Reactive) return "The unit that has it, or whoever its trigger names.";
            if (a.Escape) return "The whole party.";
            if (a.Revives) return "One fallen ally.";
            if (a.Vanishes) return "You, and anyone caught in the smoke.";
            if (a.IsTeleport || a.Afterimage > 0 || a.Vaults) return "Only you: you move to the tile.";
            if (a.Shape == TargetShape.Self) return "Only you.";
            if (a.Shape == TargetShape.AllEnemies) return $"Every foe on the board{Only(a)}.";
            if (a.Shape == TargetShape.AllAllies) return $"Every ally on the board, you included{Only(a)}.";
            if (a.GrantsOrbs > 0 && a.BaseDamage <= 0 && a.BaseHeal <= 0 && string.IsNullOrEmpty(a.ApplyBuff)) return "The party's mana; no unit.";
            if (a.Tame > 0f) return "One tameable monster: not Sentient, not a boss.";
            if (a.StealsBoon) return "One foe beside you that wears a boon; you take the boon.";
            if (a.RedirectsDebuffs) return "One ally beside you that has a debuff; a foe beside you takes it.";
            if (a.Magnetizes > 0) return $"Plated or Tech foes in reach{Only(a)}, up to {Cells(a.Shape, a.Radius)}.";

            if (a.Mode == TargetMode.PickMulti)
                return $"Up to {a.MaxPicks} {(a.Filter == TargetFilter.EnemyOnly ? "foes" : a.Filter == TargetFilter.AllyOnly ? "allies, you included" : "units, friend or foe")}, one each{Only(a)}.";

            bool single = IsSingle(a.Shape);
            bool youInArea = a.Mode == TargetMode.Auto;
            string who;
            switch (a.Filter)
            {
                case TargetFilter.EnemyOnly:
                    who = single ? "One foe" : $"Every foe in the area, up to {Cells(a.Shape, a.Radius)}";
                    break;
                case TargetFilter.AllyOnly:
                    who = single ? (a.AdjacentOnly ? "One ally beside you" : "One ally, you included")
                        : $"Every ally in the area, up to {Cells(a.Shape, a.Radius)}, {(youInArea ? "you included" : "you too if you stand in it")}";
                    break;
                case TargetFilter.EmptyOnly:
                    who = single ? "No unit: it needs an empty tile" : "No unit at the pick: it works on the tiles, and on whoever later stands on them";
                    break;
                default:
                    who = single ? "One unit, friend or foe"
                        : $"Everyone in the area, friend or foe, up to {Cells(a.Shape, a.Radius)}, {(youInArea ? "you included" : "you too if you stand in it")}";
                    break;
            }
            if (a.SwapsPlaces) who += "; you trade places with it";
            return who + Only(a) + ".";
        }

        private static string Only(AbilityDef a) => string.IsNullOrEmpty(a.OnlyVsTag) ? "" : $" (only {a.OnlyVsTag} units)";

        private static bool IsSingle(TargetShape s) => s == TargetShape.SingleActor || s == TargetShape.SingleTile;

        private static string S(int n) => n == 1 ? "" : "s";

        private static string Area(TargetShape shape, int r)
        {
            switch (shape)
            {
                case TargetShape.Square: return $"an [AoE] {2 * r + 1}×{2 * r + 1} square";
                case TargetShape.Diamond: return $"an [AoE] diamond of every tile within {r} step{S(r)}";
                case TargetShape.Cross: return $"an [AoE] cross reaching {r} tile{S(r)} each way";
                case TargetShape.Plus: return "the whole row and column";
                case TargetShape.Row: return "the whole row";
                case TargetShape.Column: return "the whole column";
                case TargetShape.Wall: return $"a wall of {2 * r + 1} tiles across your line to it";
                default: return "one tile";
            }
        }

        /// <summary>The most board tiles the shape can cover from any anchor (a wall either way round): the most units it
        /// can catch, one tile each.</summary>
        public static int Cells(TargetShape shape, int r)
        {
            int best = 0;
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var at = new Coord(x, y);
                    best = Math.Max(best, TargetResolver.Tiles(at, shape, r).Count);
                    if (shape == TargetShape.Wall) best = Math.Max(best, TargetResolver.Tiles(at, shape, r, new Coord(x - 1, y)).Count);
                }
            return best;
        }
    }
}
