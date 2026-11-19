namespace GridGame.Meta
{
    /// <summary>
    /// The release-scope switch table (GG-T392, docs/RELEASE_SCOPE.md). One place that says which finished systems the
    /// player can reach. A switch that is off hides the system's entry points in the App layer (its Camp row, side panel,
    /// Settings row, strips and result rows) and nothing else: the rules engine, content, save data, the bots
    /// (<see cref="CampRoutine"/>, Test Mode, the soaks) and the automation `goto` routes are untouched, so a save that
    /// already used a hidden system keeps every effect it bought and turning a switch back on restores the system as it was.
    /// To bring a system back, set its switch to true (or launch with `-allfeatures`, which calls <see cref="EnableAll"/>).
    /// </summary>
    public static class Features
    {
        /// <summary>The Crucible (§22.10): the "toilet play" horde mode, one team against endlessly harder waves, gold
        /// banked every wave, until the team falls. On since 2026-11-19 (owner, GG-T430).</summary>
        public static bool Crucible = true;

        /// <summary>The bounty board (§22.4): hunts for titled, tougher variants of known foes, with a stage clue, a skull
        /// marker in battle and the reward claimed at the board. On since 2026-11-19 (owner, GG-T431).</summary>
        public static bool Bounties = true;

        /// <summary>Caravan upgrades and the crew barks (§25.12), with the station facilities (§25.12.2): the necessary gold
        /// sink, where Forge and Still levels unlock better material tiers. On since 2026-11-19 (owner, GG-T432).</summary>
        public static bool Caravan = true;

        /// <summary>Settings → Menu theme, Font and Text wrapping (GG-T185, GG-T305): developer look presets. Off: the
        /// rows are hidden and the game draws with the default theme, font and word wrap.</summary>
        // Back on 2026-11-17 (owner, GG-T404): the themes and fonts are options to try and compare until the interface
        // is the best it can be, so Settings offers them again.
        public static bool LookPresets = true;

        /// <summary>Camp's separate "Inn: Respec" row (§25.6.3). Off: the Talents screen's per-hero Respec button is
        /// the one way to respec, so Camp loses a duplicate row.</summary>
        public static bool InnRespecRow = false;

        /// <summary>Taming and domestication (GG-T303). Off for release (owner 2026-11-19, GG-T433): the Summon Circle
        /// doesn't list the Monster Tamer and the Merchant doesn't stock the Dog Whistle, the only two ways in, so no tame
        /// offer can come up. A save that already tamed something keeps it.</summary>
        public static bool Taming = false;

        /// <summary>The Enchantress's station: armor and weapon enchants (§24.6, GG-T119). Off for release (owner 2026-11-19,
        /// GG-T436: "enough things going on without introducing slotting gems and enchantments on day one"). Her Camp row is
        /// hidden and the onboarding chain skips its enchant step (step 7, Training, follows the first armor instead).
        /// Recipes, content and enchants a save already bound stay. Plans: docs/plans/enchanting-and-gems.md.</summary>
        public static bool Enchanter = false;
        public const string TamerClass = "monstertamer", TamingItem = "dog_whistle";

        /// <summary>Whether the player may be offered this summon or shop entry: everything, unless it is a hidden
        /// system's way in.</summary>
        public static bool Offers(string id) => Taming || (id != TamerClass && id != TamingItem);

        /// <summary>Turns every switch on (the `-allfeatures` launch flag, for sweeps and the owner's review).</summary>
        public static void EnableAll()
        {
            Crucible = Bounties = Caravan = LookPresets = InnRespecRow = Taming = Enchanter = true;
        }
    }
}
