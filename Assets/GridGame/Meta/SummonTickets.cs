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
    /// Summon Tickets earned in play (BIBLE §25.10.3, GG-T1422): never sold on their own. A campaign stage's first clear pays
    /// <see cref="PerFirstClear"/>, finishing a chapter (its crawl's stage first cleared) <see cref="PerChapter"/>, and every
    /// <see cref="WinsPerTicket"/> battles won in any mode one more. Earned Tickets live in <see cref="SummonState.Tickets"/>
    /// beside the fragments that join into them; bought ones in the account wallet (<see cref="AccountWallet.Tickets"/>).
    /// A pull spends an earned Ticket first, then a bought one, then Shards.
    /// </summary>
    public static class SummonTickets
    {
        public const int PerFirstClear = 1;
        public const int PerChapter = 5;
        public const int WinsPerTicket = 5;

        /// <summary>Tickets this save can spend: its earned ones plus its wallet's bought ones.</summary>
        public static int Have(SaveData s) =>
            Math.Max(0, s?.Summon?.Tickets ?? 0) + (DiamondExchange.WalletOf(s) is AccountWallet w ? Math.Max(0, w.Tickets) : 0);

        public static void Earn(SaveData s, int n)
        {
            if (s == null || n <= 0) return;
            s.Summon ??= new SummonState();
            s.Summon.Tickets += n;
        }

        /// <summary>A battle won: one Ticket every <see cref="WinsPerTicket"/> wins; returns the Tickets it earned.</summary>
        public static int AfterWin(SaveData s)
        {
            s.Summon ??= new SummonState();
            if (++s.Summon.Wins % WinsPerTicket != 0) return 0;
            Earn(s, 1);
            return 1;
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
