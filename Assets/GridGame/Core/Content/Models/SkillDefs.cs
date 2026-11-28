using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>
    /// One node in an archetype skill tree (§25.6.1, GG-T50). Unlocking it costs <see cref="Cost"/> skill
    /// points and grants the ability or passive named by <see cref="Grants"/>. Gated by
    /// <see cref="RequiredPointsInTree"/> (total points already spent anywhere in the tree) and
    /// <see cref="Prerequisites"/> (sibling node ids in the same tree that must already be unlocked).
    /// Spending points against a tree (and applying a granted passive to a hero in battle) is a later
    /// ticket; this is the content shape that work will read.
    /// </summary>
    public sealed class SkillNodeDef
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>Ability or passive id granted when this node unlocks. Exactly one of this and
        /// <see cref="GrantsStat"/> is set (<see cref="ContentLoader.Validate"/>).</summary>
        public string Grants;
        /// <summary>Attribute-bonus node (GG-T77): a flat permanent bonus to one stat instead of an ability.
        /// Mutually exclusive with <see cref="Grants"/>.</summary>
        public StatGrantDef GrantsStat;
        public int Cost = 1;
        public int RequiredPointsInTree;
        public List<string> Prerequisites = new List<string>();

        public bool IsStatNode => GrantsStat != null;
    }

    /// <summary>A skill-tree attribute bonus (GG-T77): <see cref="Amount"/> flat points of the stat named by
    /// <see cref="Stat"/> — one of <see cref="Names"/>, the real <see cref="Stats"/> field names.</summary>
    public sealed class StatGrantDef
    {
        public string Stat;
        public float Amount;

        public static readonly string[] Names = { "Strength", "Vitality", "Agility", "Speed", "Intelligence", "Wisdom", "Luck" };

        public static bool IsStatName(string s) => s != null && System.Array.IndexOf(Names, s) >= 0;

        /// <summary>This bonus as a <see cref="Stats"/> block (all zero but the named stat), ready to add to a hero's stats.</summary>
        public Stats ToStats()
        {
            var s = new Stats();
            switch (Stat)
            {
                case "Strength": s.Strength = Amount; break;
                case "Vitality": s.Vitality = Amount; break;
                case "Agility": s.Agility = Amount; break;
                case "Speed": s.Speed = Amount; break;
                case "Intelligence": s.Intelligence = Amount; break;
                case "Wisdom": s.Wisdom = Amount; break;
                case "Luck": s.Luck = Amount; break;
            }
            return s;
        }
    }

    /// <summary>One retired talent tree (§25.6.1, GG-T50). GG-T1093: the talent trees gave way to the skill hexagon
    /// (skillhex.json); skilltrees.json is empty and no class names a home tree. The shape stays so an old file or a
    /// test fixture still loads and validates.</summary>
    public sealed class SkillTreeDef
    {
        public string Id;
        public string Name;
        public string Description;
        public List<SkillNodeDef> Nodes = new List<SkillNodeDef>();
    }
}
