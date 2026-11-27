# GridGame: Age-rating questionnaire answers

> **GG-T694 (2026-11-23):** the working title dropped "Gaiden"; this document now says GridGame throughout.

> **DRAFT for the owner's review (GG-T472, 2026-11-20; refreshed GG-T1540, 2026-11-27).** Suggested answers for Google Play's IARC questionnaire
> (Play Console > App content > Content ratings) and Apple's age-rating questions (App Store Connect > App
> Information > Age Rating). They describe the game as built at the commit that adds this file; re-check after any
> story, art or monetisation change. The rating authorities decide the final rating from your answers.

## The game, in rating terms

- **Category:** Game (tactics RPG: Terra Battle-style pincer combat on a grid, a speed timeline, a camp hub).
- **Violence:** fantasy combat between stylised anime characters and monsters: sword/spell attacks, damage numbers,
  particle effects, defeated units fade out. No blood, gore, dismemberment, torture or realistic weapons. Humans
  fight humans as well as creatures (faction enemies). [Owner: confirm no blood/gore VFX or story text was added.]
- **Language:** no profanity. [Confirm the story text authored in Prose stays clean.]
- **Sexual content / nudity:** none. Character art is anime-style; [owner: confirm no outfit art is suggestive].
- **Fear/horror:** mild dark-fantasy themes (ruins, undead/monster enemies), no jump scares.
- **Drugs/alcohol/tobacco:** potions are magical items; [confirm no alcohol or tobacco references in item text].
- **Gambling:** no real-money gambling and no simulated casino games. **But see randomised paid items below.**
- **User interaction:** none: no chat, no user-generated content shared with others, no online play. The optional cloud save (Google Play Games, iCloud) and the copyable Transfer save code move only the player's own save; no other player sees it.
- **Location sharing / personal info:** none.
- **Digital purchases:** yes, Mana Shards (consumable in-app purchases, US$1 to US$25).
- **Ads:** none.

## Google Play (IARC) answers

| Question | Answer |
|---|---|
| Category | Game |
| Violence | Yes: fantasy violence, characters not realistic, no blood. Violence against human-like characters: yes (fantasy). Realistic/graphic: no. |
| Fear | Mild (dark-fantasy settings, monsters) |
| Sexuality / nudity | No |
| Language | No |
| Controlled substances | No [confirm] |
| Crude humour | No |
| Gambling: real money | No |
| Gambling: simulated | No |
| **Randomised items purchasable with real money or in-game currency bought with real money (loot boxes)** | **Yes**: the Shard summon (100 Mana Shards) brings a random hero; a lucky craft (50 Shards) raises a random Masterwork chance. Shards can be bought with real money (and also earned / exchanged for gold). The odds of both are shown in-game before purchase (see below). |
| Users interact / exchange content | No (cloud save and the Transfer save code are the player's own data) |
| Shares location | No |
| Digital purchases | Yes |
| Unrestricted internet | No |

Expected outcome: roughly **PEGI 7 / ESRB Everyone 10+ / USK 6**, with the "In-Game Purchases (Includes Random
Items)" descriptor because of the summon.

## Apple App Store answers

| Item | Answer |
|---|---|
| Cartoon or Fantasy Violence | **Frequent/Intense** (combat is the core loop, and Apple's scale counts frequency; this answer still rates 9+) |
| Realistic Violence | None |
| Prolonged Graphic or Sadistic Realistic Violence | None |
| Profanity or Crude Humor | None |
| Mature/Suggestive Themes | None |
| Horror/Fear Themes | Infrequent/Mild |
| Medical/Treatment Information | None |
| Alcohol, Tobacco, or Drug Use or References | None [confirm] |
| Simulated Gambling | None |
| Sexual Content or Nudity | None |
| Graphic Sexual Content and Nudity | None |
| Contests | None |
| Unrestricted Web Access | No |
| User-Generated Content / messaging | No |
| In-App Controls (parental) | Not applicable |
| Loot boxes (the newer age-rating form asks about them) | Yes |

Expected outcome: **9+** (cartoon/fantasy violence). The loot-box answer adds a notice but doesn't by itself raise
the age band.

## Odds disclosure (done, GG-T503)

Both stores require games that sell randomised items for real money, or for currency bought with real money, to
**disclose the odds before purchase** (App Store Review Guideline 3.1.1; Google Play Payments policy, "Loot boxes").
The game now shows them, computed by the same code that makes the roll (BIBLE §25.18.1):

- **Shard summon:** one hero from the wanderer pool (the unowned heroes with no other way in), each **equally
  likely**. The Summon Circle shows "Each of these N heroes: 1 in N (x%)" next to the Draw button, an **Odds** button
  opens the full list of heroes with each one's chance, and the purchase confirm repeats the odds.
- **Lucky craft:** the Blacksmith's lucky-craft window shows the Masterwork chance without and with the Shards (base
  chance + `Crafting.LuckyChanceBonus` 40 points, capped at `Crafting.LuckyMaxChance` 75%), and the purchase confirm
  repeats both percentages.

So the "loot box" answers above can add: odds are disclosed in-game before every purchase.

## Target audience (Play Console) and kids categories

Recommended: target **13+** (or 13-15, 16-17, 18+) and **do not** opt into the Families programme or Apple's Kids
category. Choosing an under-13 audience brings Families policy (stricter ads/SDK/data rules, checks on Unity IAP's
diagnostics) and would make the loot-box-style summon a harder review. If you do want under-13s, decide that first;
these answers would change.
