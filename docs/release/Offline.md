# Offline and interruption audit (GG-T1549)

Read-only audit by code; no device run. Paths are under `Assets/GridGame/App/`.

| Scenario | Expected | Code evidence | Status |
|---|---|---|---|
| Boot with no network | Title shows at once | `GameApp` calls `Cloud.Start()` (`Boot/GameApp.cs`); `CloudSaves.Start` is a callback sign-in, nothing waits on it. IAP `Connect()` is async and retried with backoff in `UnityIapDiamondStore.Tick`. No boot step blocks on a network call | OK |
| Boot behind a captive portal | Same | Same callbacks; a sign-in that never answers leaves `Active` false and every cloud call a no-op | OK |
| Shard Store, store unreachable | Screen opens, says unavailable, exchange still works | `UnityIapDiamondStore.Status` text; connect failure sets `Link.Lost` and retries 5s..300s | OK |
| Shard Store, purchase sheet never answers | Packs freed | `InFlightTimeout` 120s releases the in-flight purchase as Pending; the order is re-fetched later | OK |
| Shard Store, Restore purchases never answers | Row leaves "Checking..." | Had no timeout. Fixed: `RestoreTimeout` 30s in `Tick` answers once with a Failed result | Fixed |
| Cloud-save offer before Load | Plays the local save after a wait | `CloudSaves.BeforeLoad` times out after `LoadWaitSeconds` (8s) and settles to the local save; once the offer is up the player answers | OK |
| Restore from cloud, no network or portal | Reports instead of waiting forever | Had no timeout (interactive sign-in or a read could never call back). Fixed: `RestoreWaitSeconds` 45s answers once with what was restored | Fixed |
| Play Games silent sign-in, no network | No block | `PlayGamesCloudBackend.SignIn(false)` is callback based and only called from `Start` | OK |
| Pause/resume, home button, phone call mid-battle | Save written, battle on its pause menu | `GameApp.OnApplicationPause(true)` runs `Suspend(true)`: `Persist` then `Cloud.Flush`, then `OnAppSuspended` and audio hold; focus loss holds without a write | OK |
| OS process kill while backgrounded | No lost progress up to last save | Same pause write; `OnApplicationQuit` also persists; saves are temp + `File.Replace` with a rolling `.bak` that loads when the slot file is bad (`SaveStore`) | OK |
| OS process kill mid-battle | Battle progress | The save holds campaign state, not the battle board; a kill mid-battle returns to the last save, so the battle restarts | Known limit; not a loss of campaign progress. Resuming a battle board would be a new feature, not filed here |
| Full disk or locked file on save | Reported | `SaveStore.Write` catches every exception, cleans the temp file and returns false; `GameApp.Persist` toasts "Couldn't save!" and the wallet write failure does the same | OK (the toast does not say why; a separate disk-full message was judged not worth a ticket) |
| Corrupt or half-written slot | Backup loads | `SaveStore.Load` restores from `.bak` and keeps `.corrupt` | OK |
