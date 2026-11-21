using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Picks the music (GG-T456). Every stage (five waves) plays one looping track drawn from its region's playlist;
    /// each region keeps a shuffle bag, so no track repeats until the whole playlist has played, and a refilled bag
    /// never opens with the track that just ended. A region with fewer than <see cref="MinSize"/> tracks of its own
    /// tops its playlist up with its <c>MusicBorrow</c> list (the closest-sounding tracks elsewhere). The Camp draws a
    /// new peaceful track each time the player comes back from the field.
    /// </summary>
    public sealed class MusicPlaylist
    {
        public const int MinSize = 4;
        private readonly Dictionary<string, List<string>> bags = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> last = new Dictionary<string, string>();
        private readonly Random rng;
        private string hubTrack;
        private bool hubStale = true;

        public MusicPlaylist(int seed) { rng = new Random(seed); }

        /// <summary>The region's full playlist: its own tracks, then borrowed ones up to <see cref="MinSize"/>.</summary>
        public static List<string> PlaylistOf(ThemeDef theme)
        {
            var list = new List<string>();
            if (theme == null) return list;
            foreach (var t in theme.Music) if (!string.IsNullOrEmpty(t) && !list.Contains(t)) list.Add(t);
            foreach (var t in theme.MusicBorrow)
            {
                if (list.Count >= MinSize) break;
                if (!string.IsNullOrEmpty(t) && !list.Contains(t)) list.Add(t);
            }
            return list;
        }

        /// <summary>Every region's own tracks, for a battle outside the campaign (Crucible, test stages).</summary>
        public static List<string> AllBattleTracks(CampaignDef campaign)
        {
            var list = new List<string>();
            if (campaign != null)
                foreach (var th in campaign.Themes) foreach (var t in th.Music) if (!list.Contains(t)) list.Add(t);
            return list;
        }

        /// <summary>Draws the next track for a stage of <paramref name="theme"/> (null = any battle track).</summary>
        public string NextBattle(CampaignDef campaign, ThemeDef theme)
        {
            hubStale = true;
            var pool = PlaylistOf(theme);
            if (pool.Count == 0) pool = AllBattleTracks(campaign);
            return Draw(theme?.Id ?? "*", pool);
        }

        /// <summary>The Camp's track: the same one while the player moves between menus, a new draw after a battle.</summary>
        public string Hub(CampaignDef campaign)
        {
            if (hubStale || hubTrack == null)
            {
                hubTrack = Draw("hub", campaign?.HubMusic ?? new List<string>());
                hubStale = false;
            }
            return hubTrack;
        }

        public string Draw(string key, List<string> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            if (!bags.TryGetValue(key, out var bag) || bag.Count == 0)
            {
                bag = new List<string>(pool);
                for (int i = bag.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (bag[i], bag[j]) = (bag[j], bag[i]); }
                // The refill's first draw is the bag's end; keep the track that just played away from it.
                if (bag.Count > 1 && last.TryGetValue(key, out var prev) && bag[bag.Count - 1] == prev)
                    (bag[0], bag[bag.Count - 1]) = (bag[bag.Count - 1], bag[0]);
                bags[key] = bag;
            }
            // A playlist edited mid-session: drop bag entries no longer in it.
            bag.RemoveAll(t => !pool.Contains(t));
            if (bag.Count == 0) { bags.Remove(key); return Draw(key, pool); }
            string pick = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            last[key] = pick;
            return pick;
        }
    }
}
