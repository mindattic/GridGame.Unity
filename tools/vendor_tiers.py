"""
Vendor tiers (GG-T538, bible 25.0.10): the Merchant's stock and the Blacksmith's and Alchemist's recipe lists start
small and grow with progress (tiers that open on region clears) and with the station's own rank.

    python tools/vendor_tiers.py            # audit: every item's and recipe's gate, counts per tier, orphans
    python tools/vendor_tiers.py --list     # also print each Merchant item and each recipe with its gate
    python tools/vendor_tiers.py --assign   # (re)write ShopTier on the Merchant's items from MERCHANT below

The progress tiers, rarity bands, set-level bands and rank thresholds are read from Meta/VendorTiers.cs, the one tuning
class, so this audit can't drift from what the game does. The recipe-tier derivation mirrors VendorTiers.Derive: a
recipe opens at the latest tier any of its inputs (or its base item) can first be had at (dropped in a campaign
region, bought, or made), floored by its result's rarity band and, for a set piece, its set's level band. Exit code 1
when anything is orphaned (a Merchant tier past the last, a recipe or item that can never be had, a tutorial item
off the first shelf).
"""
import collections
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Content")
TUNING = os.path.join(ROOT, "Assets", "Gaiden", "Meta", "VendorTiers.cs")
METASERVICES = os.path.join(ROOT, "Assets", "Gaiden", "Meta", "MetaServices.cs")

# The Merchant's curated stock tiers. Tier 1 is the first visit (about 10-20 rows: basic potions, the two hazard tools,
# a starter weapon per weapon class, a light armor set and the crafting basics); later tiers follow the region the
# goods fit. A sold crafting material not named here joins the tier after the region where it first drops (the Merchant
# trades in a region's trophies once the party has cleared it).
MERCHANT = {
    1: ["healing_potion_basic", "antidote", "eye_drops", "cons_purifying_salts", "cons_disarm_kit",
        "empty_bottle", "mat_iron_ore", "mat_leather", "mat_cloth",
        "eq_sword_rusty", "eq_dagger_bronze", "eq_staff_quarter", "eq_wand_apprentice",
        "eq_armor_padded", "eq_legs_leather", "eq_boots_leather", "eq_wayfarer_head", "eq_gloves_leather"],
    # After The Tithe Moors III (the Lantern Road): the elemental flasks (no foe resists anything but Arcane at half
    # before the Shardfall), the Hi-Potion, the rest of the starter weapons, and the militia kit off the tithe levy.
    2: ["flask_fire", "flask_frost", "flask_storm", "flask_venom", "flask_holy", "flask_shadow", "flask_arcane",
        "hi_potion",
        "eq_sword_iron", "eq_bow_hunter", "eq_club_oak", "eq_lantern_tallow", "eq_rapier_training", "eq_tome_primer",
        "eq_armor_leather", "eq_helm_iron", "eq_wayfarer_gloves", "eq_wayfarer_boots",
        "eq_militia_head", "eq_militia_boots", "eq_militia_torso",
        "eq_ring_copper", "eq_earring_copper", "eq_amulet_iron"],
    # After The Lantern Road III (the Shardfall): every other weapon type, heavier mail and the trinkets.
    3: ["eq_sickle_farm", "eq_sling_leather", "eq_javelin_hunting", "eq_pitchfork_rustic", "eq_knuckles_copper",
        "eq_spear_militia", "eq_hatchet_hand", "eq_crossbow_light", "eq_orb_glass",
        "eq_armor_chain", "eq_boots_iron",
        "eq_amulet_wolfstooth", "eq_amulet_hearth", "eq_amulet_bone", "eq_ring_iron", "eq_ring_bone",
        "eq_earring_bead", "eq_earring_feather",
        "cons_grenade"],
    # After The Shardfall III (the Arkgrave): cures, the whistle, the Tent and the bramble and gutter boots.
    4: ["remedy", "dog_whistle", "tent", "eq_bramblehide_boots", "eq_gutterscrap_boots"],
    # After The Arkgrave III (the Anchor Wastes): the dear revivals.
    5: ["revival_charm", "x_potion"],
}


def load(name):
    return json.load(open(os.path.join(CONTENT, name), encoding="utf-8"))


def tuning():
    src = open(TUNING, encoding="utf-8").read()
    opens = re.search(r"OpensAfter\s*=\s*\{(.*?)\};", src, re.S).group(1)
    opens = [None if x.strip() == "null" else x.strip().strip('"') for x in opens.split(",") if x.strip()]
    rarity = dict(re.findall(r"case ItemRarity\.(\w+): return (\d+);", src))
    rarity = {k: int(v) for k, v in rarity.items()}
    ceil = [int(x) for x in re.search(r"SetLevelCeiling\s*=\s*\{(.*?)\}", src).group(1).split(",")]
    rank = [int(x) for x in re.search(r"RankXp\s*=\s*\{(.*?)\}", src).group(1).split(",")]
    meta = open(METASERVICES, encoding="utf-8").read()
    craft_xp = int(re.search(r"const int CraftXp = (\d+);", meta).group(1))
    repair_xp = int(re.search(r"const int RepairXp = (\d+);", meta).group(1))
    return opens, rarity, ceil, rank, craft_xp, repair_xp


class World:
    def __init__(self):
        self.items = {i["Id"]: i for i in load("items.json")}
        self.recipes = load("recipes.json")
        self.classes = {c["Id"]: c for c in load("classes.json")}
        self.tables = {t["Id"].lower(): t for t in load("droptables.json")}
        self.groups = {g["Id"]: g for g in load("encounters.json")}
        self.stages = {s["Id"].lower(): s for s in load("stages.json")}
        self.campaign = load("campaign.json")
        self.sets = {p: s for s in load("armorsets.json") for p in s["Pieces"]}
        self.order = [(t["Id"], sid) for t in self.campaign["Themes"] for sid in t["Stages"] if sid.lower() in self.stages]
        self.opens, self.rarity, self.ceil, self.rank, self.craft_xp, self.repair_xp = tuning()
        self.count = len(self.opens)
        index = {sid.lower(): i for i, (_, sid) in enumerate(self.order)}
        self.clears = [0] + [index[s.lower()] + 1 for s in self.opens[1:]]

    def stage_name(self, sid):
        return self.stages[sid.lower()]["Name"]

    def region_tier(self, theme):
        start = next(i for i, (t, _) in enumerate(self.order) if t == theme)
        return max(t + 1 for t, c in enumerate(self.clears) if c <= start)

    def rarity_tier(self, rarity):
        return self.rarity.get(rarity, 1)

    def floor(self, recipe):
        res = self.items[recipe["Result"]]
        tier = self.rarity_tier(res["Rarity"])
        if res["Id"] in self.sets:
            lvl = self.sets[res["Id"]].get("Level", 1)
            band = next((i + 1 for i, c in enumerate(self.ceil) if lvl <= c), len(self.ceil) + 1)
            tier = max(tier, band)
        return tier

    def shop_tier(self, item):
        return max(1, min(self.count, item.get("ShopTier") or 1))

    def derive(self):
        never = 10 ** 6
        item, source = {}, {}

        def offer(iid, tier, why):
            if iid is not None and tier < item.get(iid, never):
                item[iid], source[iid] = tier, why

        def table(tid, tier, seen, why):
            if tid is None or tid.lower() in seen or tid.lower() not in self.tables:
                return
            seen.add(tid.lower())
            for e in self.tables[tid.lower()]["Entries"]:
                offer(e["Item"], tier, why)
                chest = self.items.get(e["Item"], {}).get("OpensTable")
                if chest:
                    table(chest, tier, seen, why)

        for theme, sid in self.order:
            tier = self.region_tier(theme)
            for w in self.stages[sid.lower()]["Waves"]:
                enemies = w.get("Enemies") or (self.groups[w["Group"]]["Enemies"] if w.get("Group") in self.groups else [])
                for e in enemies:
                    dt = self.classes.get(e["Class"], {}).get("DropTable")
                    table(dt, tier, set(), "drops in " + self.stage_name(sid))
        for i in self.items.values():
            if i.get("SoldByVendor") and i.get("BaseCost", 0) > 0:
                offer(i["Id"], self.shop_tier(i), "Merchant")
        recipes = [r for r in self.recipes if r["Result"] in self.items]
        rt, changed = {}, True
        while changed:
            changed = False
            for r in recipes:
                ins = [x["Item"] for x in r["Inputs"]] + ([r["BaseItem"]] if r.get("BaseItem") else [])
                tier = max([self.floor(r)] + [item.get(x, never) for x in ins])
                if rt.get(r["Id"]) != tier:
                    rt[r["Id"]], changed = tier, True
                if tier < item.get(r["Result"], never):
                    item[r["Result"]], source[r["Result"]], changed = tier, "made: " + r["Id"], True
        self.recipe_tier = {k: (v if v <= self.count else None) for k, v in rt.items()}
        self.item_tier = {k: (v if v <= self.count else None) for k, v in item.items()}
        self.item_source = source

    def gate(self, tier):
        return "start" if tier == 1 else "after " + self.stage_name(self.opens[tier - 1])


def assign(world):
    tier_of = {}
    for t, ids in MERCHANT.items():
        for iid in ids:
            assert iid in world.items, iid
            assert iid not in tier_of, f"{iid} listed twice"
            tier_of[iid] = t
    # A sold material not named above: the tier after the region where it first drops.
    world.derive()
    first = {}
    for theme, sid in world.order:
        for w in world.stages[sid.lower()]["Waves"]:
            enemies = w.get("Enemies") or (world.groups[w["Group"]]["Enemies"] if w.get("Group") in world.groups else [])
            for e in enemies:
                dt = world.classes.get(e["Class"], {}).get("DropTable")
                if dt and dt.lower() in world.tables:
                    for x in world.tables[dt.lower()]["Entries"]:
                        first.setdefault(x["Item"], theme)
    items = load("items.json")
    unassigned = []
    for i in items:
        if not (i.get("SoldByVendor") and i.get("BaseCost", 0) > 0):
            i.pop("ShopTier", None)
            continue
        if i["Id"] in tier_of:
            t = tier_of[i["Id"]]
        elif i["Type"] == "CraftingMaterial" and i["Id"] in first:
            t = min(world.count, world.region_tier(first[i["Id"]]) + 1)
        else:
            unassigned.append(i["Id"])
            t = 1
        i["ShopTier"] = t
    if unassigned:
        print("NOT ASSIGNED (left at tier 1): " + ", ".join(unassigned))
    # Keep the key beside SoldByVendor, where the model declares it.
    out = []
    for i in items:
        if "ShopTier" in i:
            t = i.pop("ShopTier")
            j = {}
            for k, v in i.items():
                j[k] = v
                if k == "SoldByVendor":
                    j["ShopTier"] = t
            i = j
        out.append(i)
    with open(os.path.join(CONTENT, "items.json"), "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(out, indent=2, ensure_ascii=True) + "\n")
    print("wrote ShopTier on", sum(1 for i in out if "ShopTier" in i), "Merchant items")


def audit(world, listing):
    world.derive()
    problems = []
    print("Progress tiers (Meta/VendorTiers.cs OpensAfter):")
    for t in range(1, world.count + 1):
        print(f"  {t}: {world.gate(t)} ({world.clears[t - 1]} stages cleared)")
    print("Station ranks (RankXp): " + ", ".join(f"{n} {x} XP" for n, x in zip(["Apprentice", "Journeyman", "Master"], world.rank))
          + f"  (a forge/brew {world.craft_xp} XP, a repair {world.repair_xp} XP)")

    sold = [i for i in world.items.values() if i.get("SoldByVendor") and i.get("BaseCost", 0) > 0]
    print(f"\nMerchant ({len(sold)} items):")
    by = collections.Counter(world.shop_tier(i) for i in sold)
    cum = 0
    for t in range(1, world.count + 1):
        cum += by[t]
        print(f"  tier {t} ({world.gate(t)}): +{by[t]}  shelf {cum}")
    for i in sold:
        if (i.get("ShopTier") or 1) > world.count:
            problems.append(f"{i['Id']}: ShopTier {i.get('ShopTier')} is past the last tier")
    for iid in ("healing_potion_basic", "empty_bottle"):  # the Merchant's camp tip and the first brew's bottle
        if world.shop_tier(world.items[iid]) != 1:
            problems.append(f"{iid}: the tutorial needs it on the first shelf")
    if listing:
        for i in sorted(sold, key=lambda i: (world.shop_tier(i), i["Type"], i["BaseCost"])):
            print(f"    {world.shop_tier(i)}  {i['Type'][:5]:5} {i['BaseCost']:>4}  {i['Name']}")

    for station, label in (("Forge", "Blacksmith Forge"), ("ForgeUp", "Blacksmith Upgrade"), ("Alchemy", "Alchemist")):
        rs = [r for r in world.recipes if (r["Station"] == "Forge" and bool(r.get("BaseItem")) == (station == "ForgeUp"))
              or (station == "Alchemy" and r["Station"] == "Alchemy")]
        if station == "Alchemy":
            rs = [r for r in world.recipes if r["Station"] == "Alchemy"]
        print(f"\n{label} ({len(rs)} recipes) by progress tier x rank (Apprentice / Journeyman / Master):")
        grid = collections.Counter((world.recipe_tier.get(r["Id"]), r.get("Tier") or 1) for r in rs)
        for t in range(1, world.count + 1):
            row = [grid[(t, k)] for k in (1, 2, 3)]
            if sum(row):
                print(f"  tier {t} ({world.gate(t)}): {sum(row):>3}   {row[0]:>3} / {row[1]:>3} / {row[2]:>3}")
        orphans = [r["Id"] for r in rs if world.recipe_tier.get(r["Id"]) is None]
        for o in orphans:
            problems.append(f"recipe {o}: an input can never be had")
        sets = [r for r in rs if r["Result"] in world.sets]
        fresh = [r for r in rs if world.recipe_tier.get(r["Id"]) == 1 and (r.get("Tier") or 1) == 1]
        print(f"  a fresh {label} lists {len(fresh)}" + (f" ({len(fresh) - len([r for r in fresh if r['Result'] in world.sets])} before the first forge opens the set armor)" if sets else ""))
        if listing:
            for r in sorted(rs, key=lambda r: (world.recipe_tier.get(r["Id"]) or 99, r.get("Tier") or 1, r["Gold"])):
                t = world.recipe_tier.get(r["Id"])
                print(f"    {t or '-'}  rank {r.get('Tier') or 1}  {world.items[r['Result']]['Name']}  ({r['Id']})")

    # The guided first forge, armor and brew must sit in tier 1 at Apprentice.
    starters = [r for r in world.recipes if r.get("Starter")]
    for r in starters:
        if world.recipe_tier.get(r["Id"]) != 1:
            problems.append(f"starter recipe {r['Id']} is not in tier 1")
    first_armor = [r for r in world.recipes if r["Result"] in world.sets and (r.get("Tier") or 1) == 1 and world.recipe_tier.get(r["Id"]) == 1]
    if not first_armor:
        problems.append("no Apprentice set-armor recipe in tier 1 for the guided first armor")
    bottle = [r for r in world.recipes if r["Station"] == "Alchemy" and any(x["Item"] == "empty_bottle" for x in r["Inputs"])
              and (r.get("Tier") or 1) == 1 and world.recipe_tier.get(r["Id"]) == 1]
    if not bottle:
        problems.append("no Apprentice bottle recipe in tier 1 for the guided first brew")
    print(f"\nGuided flows: {len(starters)} starter weapons, {len(first_armor)} first-armor pieces, {len(bottle)} first-brew bottle recipes in tier 1")

    # Every item obtainable by the end: dropped, bought or made at some tier.
    never = [i for i in world.items.values() if world.item_tier.get(i["Id"]) is None]
    by_type = collections.Counter(i["Type"] for i in never)
    print(f"\nItems no tier reaches (not dropped in the campaign, sold or made; quests, bounties, paywall and"
          f" summon rewards aside): {len(never)} " + (str(dict(by_type)) if never else ""))
    gated_never = [i["Id"] for i in never if any(r["Result"] == i["Id"] for r in world.recipes if r["Station"] != "Enchant")
                   or (i.get("SoldByVendor") and i.get("BaseCost", 0) > 0)]
    for iid in gated_never:
        problems.append(f"{iid}: a vendor item or Forge/Alchemy result that no tier reaches")

    print("\n" + ("OK: nothing orphaned" if not problems else "PROBLEMS:\n  " + "\n  ".join(problems)))
    return 1 if problems else 0


def main():
    world = World()
    if "--assign" in sys.argv:
        assign(world)
        world = World()
    sys.exit(audit(world, "--list" in sys.argv))


if __name__ == "__main__":
    main()
