# Plan: enchanting and gem slotting after launch

Status: plan, parked for after release (owner 2026-11-19, GG-T436). Nothing here is started.

Bring the Enchantress back as a post-launch update: armor and weapon enchants (the 600 weapon-enchant recipes stay in
the content, owner: "I like that there are 600 recipes") plus a gem-slotting layer, introduced once players know the
core loop, not on day one.

## Why it's parked

Owner, 2026-11-19: "there are enough things going on without introducing slotting gems and enchantments on day one".
At launch the Enchantress's Camp row is hidden (`Features.Enchanter = false`) and the onboarding chain skips its enchant
step: the guided first skill (step 7) follows the first armor instead.

## What already exists (kept, just hidden)

- The Enchantress's station (`EnchantressScreen`): armor enchants (`ArmorEnchanting`, 40–150 g) and the weapon-enchant
  tab (`CraftStation.Enchant`, 100 base weapons × 6 elements, re-tiered by rarity in GG-T215: 60/90/130/180/250 g).
- Recipe tiers unlocked by the Alchemist's station level; the guided free first enchant (onboarding step 6).
- Saves that already bound enchants keep them in battle.

## Open questions for the owner

1. **Gems:** what a gem is (a drop? a crafted item from the facility material tiers, GG-T432?), how many sockets a
   piece has (by rarity?), and whether gems replace or sit beside enchants.
2. **The 600 weapon recipes:** keep the full tab, or present them as one "pick an element" step per weapon so the list
   reads short?
3. **When it unlocks:** a story beat (the Enchantress joins), a campaign stage, or a facility upgrade?
4. **Monetization:** none planned; would the Diamond lucky craft extend to a lucky enchant?

## Rough shape when picked up

- Turn `Features.Enchanter` back on; restore the onboarding step's gate in `Training.Step`.
- Gem items plus a socket count on equipment, validated in `ContentLoader`; a slotting screen on the hero screen
  (GG-T435's tabbed hero screen) rather than a new Camp row.
- Balance against measured stage pay (≈210–270 g a stage early, 400–600 g late).
