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
        /// <summary>The Crucible (§22.10): endless waves from Camp. Off: replaying cleared stages ("hunts" on the
        /// Adventure screen) is the one XP/loot grind, so a second grind mode is a duplicate Camp row.</summary>
        public static bool Crucible = false;

        /// <summary>The bounty board (§22.4). Off: its kill-N contracts duplicate the side quests' Defeat objectives
        /// (§22.11) and add a Camp row, an Adventure strip and a results row. Kills still count in the rules, and the
        /// Mercenaries board still opens at the same milestone (<see cref="Mercenaries.Open"/>).</summary>
        public static bool Bounties = false;

        /// <summary>Caravan upgrades and the crew barks (§25.12). Off: a gold sink outside the core loop and a flavor
        /// panel on Camp. Upgrades a save already bought keep working.</summary>
        public static bool Caravan = false;

        /// <summary>Settings → Menu theme, Font and Text wrapping (GG-T185, GG-T305): developer look presets. Off: the
        /// rows are hidden and the game draws with the default theme, font and word wrap.</summary>
        public static bool LookPresets = false;

        /// <summary>Camp's separate "Inn: Respec" row (§25.6.3). Off: the Talents screen's per-hero Respec button is
        /// the one way to respec, so Camp loses a duplicate row.</summary>
        public static bool InnRespecRow = false;

        /// <summary>Turns every switch on (the `-allfeatures` launch flag, for sweeps and the owner's review).</summary>
        public static void EnableAll()
        {
            Crucible = Bounties = Caravan = LookPresets = InnRespecRow = true;
        }
    }
}
