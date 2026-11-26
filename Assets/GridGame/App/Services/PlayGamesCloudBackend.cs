#if GG_PLAY_GAMES && UNITY_ANDROID && !UNITY_EDITOR
using System;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using GridGame.Meta;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// Play Games Services Saved Games (§15.2, GG-T608), through the Google Play Games plugin for Unity (v11+, Play Games
    /// v2 SDK: sign-in is automatic at launch). Compiled only into an Android player with the <c>GG_PLAY_GAMES</c> define,
    /// which is set together with installing the plugin package and adding its assembly to GridGame.App.asmdef — see
    /// docs/RELEASE_BUILD.md "Cloud save". The plugin delivers Saved Games callbacks on the game thread.
    /// Conflicts between two devices' unsynced writes resolve to the longer play time (the most progress), the same rule
    /// that makes a newer cloud copy an offer rather than a silent overwrite.
    /// </summary>
    public sealed class PlayGamesCloudBackend : ICloudSaveBackend
    {
        private static ISavedGameClient Client => PlayGamesPlatform.Instance.SavedGame;

        public PlayGamesCloudBackend() { PlayGamesPlatform.Activate(); }

        public bool SignedIn => PlayGamesPlatform.Instance.IsAuthenticated();

        public void SignIn(bool interactive, Action<bool> done)
        {
            if (interactive) PlayGamesPlatform.Instance.ManuallyAuthenticate(status => done(status == SignInStatus.Success));
            else PlayGamesPlatform.Instance.Authenticate(status => done(status == SignInStatus.Success));
        }

        private void Open(string name, Action<ISavedGameMetadata> done)
        {
            if (!SignedIn) { done(null); return; }
            Client.OpenWithAutomaticConflictResolution(name, DataSource.ReadCacheOrNetwork,
                ConflictResolutionStrategy.UseLongestPlaytime, (status, meta) =>
                {
                    if (status != SavedGameRequestStatus.Success || meta == null)
                    {
                        Debug.LogWarning($"[Cloud] open {name}: {status}");
                        done(null);
                        return;
                    }
                    done(meta);
                });
        }

        public void ReadInfo(string name, Action<CloudSnapshotInfo> done)
        {
            Open(name, meta =>
            {
                if (meta == null) { done(null); return; }
                // Opening a name that was never written creates an empty snapshot: no play time, no description.
                bool hasData = meta.TotalTimePlayed > TimeSpan.Zero || !string.IsNullOrEmpty(meta.Description);
                // The save's own time rides in the description (GG-T1132): LastModifiedTimestamp is the upload time, up to
                // a minute after the save. Snapshots written before the stamp fall back to the modified time.
                var updated = meta.LastModifiedTimestamp.ToUniversalTime();
                var text = meta.Description;
                if (CloudSave.TryReadStamp(meta.Description, out var plain, out var savedUtc)) { text = plain; updated = savedUtc; }
                done(new CloudSnapshotInfo
                {
                    Name = name,
                    UpdatedUtc = updated,
                    PlaySeconds = meta.TotalTimePlayed.TotalSeconds,
                    Description = text,
                    HasData = hasData,
                });
            });
        }

        public void Read(string name, Action<byte[]> done)
        {
            Open(name, meta =>
            {
                if (meta == null) { done(null); return; }
                Client.ReadBinaryData(meta, (status, data) => done(status == SavedGameRequestStatus.Success ? data : null));
            });
        }

        public void Write(string name, byte[] data, string description, TimeSpan playTime, DateTime savedUtc, Action<bool> done)
        {
            // Play Games stamps LastModifiedTimestamp with the upload time, so the save's own time goes in the description.
            Open(name, meta =>
            {
                if (meta == null) { done(false); return; }
                var update = new SavedGameMetadataUpdate.Builder()
                    .WithUpdatedDescription(CloudSave.StampDescription(description, savedUtc))
                    .WithUpdatedPlayedTime(playTime)
                    .Build();
                Client.CommitUpdate(meta, update, data, (status, _) => done(status == SavedGameRequestStatus.Success));
            });
        }
    }
}
#endif
