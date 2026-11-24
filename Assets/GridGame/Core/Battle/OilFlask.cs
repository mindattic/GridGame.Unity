using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Oil Flask (§7, §14.23, GG-T886, the Alchemist's skill): a <see cref="RuleEffectKind.MarkTiles"/> of a "kindling" tile
    /// state, one some element's elements.json <c>Tiles</c> row turns into a hazard (Fire turns <c>oil</c> into a
    /// <c>firewall</c>). The oil itself slows a foe that steps in; lit, it burns like the Red Mage's wall, so the Alchemist
    /// and a Fire ally combo. The runner paints and reacts it through the region rules as any mark; this class names the
    /// kindling and prices a throw for the hero bot. Pure: it reads the state, never changes it or draws from the Rng.
    /// </summary>
    public static class OilFlask
    {
        /// <summary>What each foe standing on or beside the pool is worth while a ready ally could set it alight.</summary>
        public const float LightValue = 4f;
        /// <summary>What a hero standing in the pool costs when it would burn the party too once lit.</summary>
        public const float HeroInPool = 6f;

        /// <summary>The kindling an ability paints: the state, the element that lights it and the hazard it becomes; null when
        /// the ability paints none.</summary>
        public static (RuleEffectDef Mark, TileStateDef State, DamageType Element, TileStateDef Becomes)? Kindling(ContentDb c, AbilityDef a)
        {
            if (c?.Elements == null || a?.RuleEffects == null) return null;
            foreach (var fx in a.RuleEffects)
            {
                if (fx == null || fx.Kind != RuleEffectKind.MarkTiles || !(c.TryTileState(fx.State) is TileStateDef st)) continue;
                foreach (var e in c.Elements.Elements)
                {
                    var r = ElementCounters.Match(e.Tiles, st.Id);
                    if (r != null && r.Kind == TileReactionKind.Becomes && c.TryTileState(r.Into) is TileStateDef into && into.Hazard)
                        return (fx, st, e.Type, into);
                }
            }
            return null;
        }

        public static bool Is(ContentDb c, AbilityDef a) => Kindling(c, a) != null;

        /// <summary>Can <paramref name="a"/> set a kindling of <paramref name="element"/> alight? A hit of that element (its
        /// row's tile reactions run on what it covers) or a paint of a state carrying it (the Firewall).</summary>
        public static bool Lights(ContentDb c, AbilityDef a, DamageType element)
        {
            if (a == null) return false;
            if (a.BaseDamage > 0 && a.DamageType == element && a.Filter != TargetFilter.AllyOnly) return true;
            if (a.RuleEffects != null)
                foreach (var fx in a.RuleEffects)
                    if (fx != null && fx.Kind == RuleEffectKind.MarkTiles && c?.TryTileState(fx.State)?.Element == element) return true;
            return false;
        }

        /// <summary>A living hero other than <paramref name="caster"/> with a ready slot that <see cref="Lights"/> the
        /// element, or null.</summary>
        public static Unit Lighter(BattleState s, Unit caster, DamageType element)
        {
            if (s == null) return null;
            foreach (var h in s.LivingList(Team.Hero))
            {
                if (h == caster) continue;
                for (int slot = 0; slot < h.Bar.Count; slot++)
                {
                    if (BattleEngine.GetSlotStatus(s, h.Id, slot) != SlotStatus.Ready) continue;
                    if (Lights(s.Content, BattleEngine.SlotAbility(s, h, slot), element)) return h;
                }
            }
            return null;
        }

        /// <summary>The tiles a throw at <paramref name="anchor"/> paints.</summary>
        public static List<Coord> Pool(AbilityDef a, RuleEffectDef mark, Unit caster, Coord anchor) =>
            mark.Area == RuleArea.Target ? new List<Coord> { anchor } : TargetResolver.Tiles(anchor, a.Shape, a.Radius, caster?.Pos);

        /// <summary>
        /// What a throw at <paramref name="anchor"/> is worth for the combo (the oil on a foe's way in is
        /// <c>HeroBot.WallValue</c>'s): only while another hero has a ready slot that lights it, <see cref="LightValue"/> for
        /// each foe on or beside the pool (the fire will catch it there), less <see cref="HeroInPool"/> for each hero inside
        /// it when the lit state burns everyone. 0 when nothing qualifies, so it is never thrown for its own sake.
        /// </summary>
        public static float Value(BattleState s, AbilityDef a, Unit caster, Coord anchor)
        {
            if (s?.Content == null || caster == null) return 0f;
            var k = Kindling(s.Content, a);
            if (k == null || Lighter(s, caster, k.Value.Element) == null) return 0f;
            var tiles = Pool(a, k.Value.Mark, caster, anchor).FindAll(c => BattleState.InBounds(c) && !s.IsBlock(c));
            if (tiles.Count == 0) return 0f;
            float gain = 0f, cost = 0f;
            foreach (var e in s.LivingList(Team.Enemy))
            {
                if (e.IsCorpse || e.IsRemains || e.IsEgg || RegionBoard.Immune(e, k.Value.Becomes.Id)) continue;
                foreach (var c in tiles)
                    if (e.Occupies(c) || e.Pos.Manhattan(c) <= 1) { gain += LightValue; break; }
            }
            if (k.Value.Becomes.Affects == TileAffects.Any)
                foreach (var h in s.LivingList(Team.Hero))
                    foreach (var c in tiles)
                        if (h.Occupies(c)) { cost += HeroInPool; break; }
            return gain > 0f ? gain - cost : 0f;
        }
    }
}
