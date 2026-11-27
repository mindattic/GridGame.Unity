using System;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>The Summon Ticket's player-facing names (BIBLE §25.10.3, GG-T1422), as <see cref="ShardText"/> is the Shard's.</summary>
    public static class TicketText
    {
        public const string Name = "Summon Ticket", Plural = "Summon Tickets";
        public const string Fragment = "Ticket fragment", Fragments = "Ticket fragments";

        /// <summary>"1 Summon Ticket", "10 Summon Tickets".</summary>
        public static string Of(int n) => $"{n:N0} {(n == 1 ? Name : Plural)}";
    }

    /// <summary>
    /// Summon Tickets earned in play (BIBLE §25.10.3, GG-T1422): never sold on their own. The first Summon Circle visit gives <see cref="StarterTickets"/>, a campaign stage's first clear pays
    /// <see cref="PerFirstClear"/>, finishing a chapter (its crawl's stage first cleared) <see cref="PerChapter"/>, and every
    /// <see cref="WinsPerTicket"/> battles won in any mode one more; each UTC day's first win <see cref="PerDailyFirstWin"/>,
    /// each <see cref="CrucibleWavesPerTicket"/>-wave Crucible best one, and some quests pay Tickets on turn-in (GG-T1607). Earned Tickets live in <see cref="SummonState.Tickets"/>
    /// beside the fragments that join into them; bought ones in the account wallet (<see cref="AccountWallet.Tickets"/>).
    /// A pull spends an earned Ticket first, then a bought one, then Shards.
    /// </summary>
    public static class SummonTickets
    {
        public const int PerFirstClear = 1;
        public const int PerChapter = 5;
        public const int WinsPerTicket = 5;
        public const int StarterTickets = 10;
        public const int PerDailyFirstWin = 1;
        public const int CrucibleWavesPerTicket = 5;

        /// <summary>Tickets this save can spend: its earned ones plus its wallet's bought ones.</summary>
        public static int Have(SaveData s) =>
            Math.Max(0, s?.Summon?.Tickets ?? 0) + (DiamondExchange.WalletOf(s) is AccountWallet w ? Math.Max(0, w.Tickets) : 0);

        public static void Earn(SaveData s, int n)
        {
            if (s == null || n <= 0) return;
            s.Summon ??= new SummonState();
            s.Summon.Tickets += n;
        }

        /// <summary>The first Summon Circle visit: <see cref="StarterTickets"/> once per save (<see cref="SummonState.StarterGiven"/>);
        /// returns the Tickets it gave, 0 on every later visit.</summary>
        public static int Starter(SaveData s)
        {
            if (s == null) return 0;
            s.Summon ??= new SummonState();
            if (s.Summon.StarterGiven) return 0;
            s.Summon.StarterGiven = true;
            Earn(s, StarterTickets);
            return StarterTickets;
        }

        /// <summary>A battle won: one Ticket every <see cref="WinsPerTicket"/> wins; returns the Tickets it earned.</summary>
        public static int AfterWin(SaveData s)
        {
            s.Summon ??= new SummonState();
            if (++s.Summon.Wins % WinsPerTicket != 0) return 0;
            Earn(s, 1);
            return 1;
        }

        /// <summary>A battle won: <see cref="PerDailyFirstWin"/> for the first win of <paramref name="utcNow"/>'s UTC day
        /// (<see cref="SummonState.FirstWinDay"/>, GG-T1607); returns the Tickets it earned.</summary>
        public static int DailyFirstWin(SaveData s, DateTime utcNow)
        {
            s.Summon ??= new SummonState();
            if (s.Summon.FirstWinDay == utcNow.Date) return 0;
            s.Summon.FirstWinDay = utcNow.Date;
            Earn(s, PerDailyFirstWin);
            return PerDailyFirstWin;
        }

        /// <summary>A Crucible run's best waves rose from <paramref name="bestBefore"/> to <paramref name="bestAfter"/>: one
        /// Ticket for each <see cref="CrucibleWavesPerTicket"/>-wave milestone newly reached (GG-T1607).</summary>
        public static int AfterCrucibleBest(SaveData s, int bestBefore, int bestAfter)
        {
            int n = Math.Max(0, bestAfter / CrucibleWavesPerTicket - Math.Max(0, bestBefore) / CrucibleWavesPerTicket);
            Earn(s, n);
            return n;
        }

        /// <summary>A campaign stage first cleared: <see cref="PerFirstClear"/>, plus <see cref="PerChapter"/> when it ends a chapter.</summary>
        public static int AfterFirstClear(ContentDb c, SaveData s, string stageId)
        {
            int n = PerFirstClear + (c?.Campaign?.Chapters?.Any(ch => ch?.Stage == stageId) == true ? PerChapter : 0);
            Earn(s, n);
            return n;
        }

        /// <summary>Takes one Ticket (earned first, then the wallet's); false with none.</summary>
        public static bool Spend(SaveData s)
        {
            if (s?.Summon?.Tickets > 0) { s.Summon.Tickets--; return true; }
            if (DiamondExchange.WalletOf(s) is AccountWallet w && w.Tickets > 0)
            {
                w.Tickets--;
                w.UpdatedUtc = DateTime.UtcNow;
                DiamondExchange.WalletDirty = true;
                return true;
            }
            return false;
        }
    }
}
