# GridGame: Google Play Data safety answers

> **DRAFT for the owner's review (GG-T472, 2026-11-20; refreshed GG-T1540, 2026-11-27).** The answers to enter in Play Console > App content > Data
> safety, based on what the release build does at the commit that adds this file. Google holds the developer
> responsible for SDK behaviour too: before submitting, confirm the Unity IAP item below against the project's Unity
> Dashboard Developer Data settings and Unity's own Data safety guidance for the IAP package version in
> `Packages/manifest.json` (com.unity.purchasing 5.4.4).

## What the release build does

| Area | Release build behaviour |
|---|---|
| Accounts / sign-in | No account of our own. Google Play Games sign-in (v2 plugin, silent at launch, the Google account on the phone) for Saved Games only (GG-T608); inert until the plugin and Play Console app id are in. |
| Ads | None (no ad SDK, no advertising id) |
| Analytics / crash reporting | None of ours at present (crash-reporting choice O_C2 is open; see the declarations below). Unity Analytics, Cloud Diagnostics and performance reporting are off. MindAttic.Telemetry and MindAttic.Media.Capture are inert in release builds (editor/development only, local files only). |
| Network | Google Play Billing (through Unity IAP) when the Shard Store connects and when you buy, and Google Play Games (sign-in, Saved Games upload and download). No other network code and no server of ours. |
| Error log | Unhandled exceptions written to an on-device file (GG-T484 `CrashReporter`: time, version, platform, OS, device model, scrubbed error text). Not uploaded, so not "collected". Android vitals crash/ANR data is collected by Google Play itself, not declared by the developer. |
| Saves | On device (app-private storage), included in Android Auto Backup. Also uploaded to Google Play Games Saved Games when signed in: one snapshot per slot holding the Transfer save code (hero, item, progress and Shard data, purchase transaction ids, profile name), play time and a description. |
| Transfer save code | Settings > Transfer save / Import save: text the user copies and pastes; the game sends it nowhere. |
| Permissions | `INTERNET`, `ACCESS_NETWORK_STATE` and `com.android.vending.BILLING` (added by Unity IAP) and the app's own `com.mindattic.gridgamegaiden.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION` (AndroidX, unexported receivers). No location, camera, microphone, contacts or storage permissions. `tools/check_aab.py` reads the backticked names in this row as its allowlist and fails a build whose merged manifest asks for anything else. |

## Questionnaire answers

**Does your app collect or share any of the required user data types?**
Answer **Yes**: the Play Games cloud save sends save data and the player id off the device, and, conservatively, Unity
IAP's purchase diagnostics. If, after checking Unity's guidance and
the Developer Data settings, Unity IAP sends nothing off-device beyond the Google Play Billing transaction itself,
"No" is defensible: payment data handled by Google Play Billing on Google's behalf doesn't need to be declared by the
developer.

**Is all of the user data collected by your app encrypted in transit?** Yes (Google Play Billing and Unity endpoints
use HTTPS/TLS).

**Do you provide a way for users to request that their data be deleted?** We hold no user data on our side. Local data
goes away when save slots are deleted or the app is uninstalled; Saved Games copies are held by Google and the user
deletes them in Google Play Games (the privacy policy says how). Answer **"No"** unless Play requires a deletion
mechanism for the declared types; if it does, give the privacy contact email from the privacy policy (placeholder,
GG-T1558) as the request channel.

### Data types (only if answering "Yes" above)

| Data type | Collected | Shared | Purpose | Optional? | Notes |
|---|---|---|---|---|---|
| Personal info > User IDs (Play Games player id) | Yes | No | App functionality (cloud save sign-in) | Yes, the user can decline sign-in | Handled by Google Play Games Services; never stored or read by us. Declare conservatively; confirm against Google's Play Games Services Data safety guidance. |
| App activity > Other user-generated content (save data) | Yes | No | App functionality (cloud save) | Yes | Slot contents uploaded to Saved Games, held by Google for the user. A transfer to Google as the user's cloud host is not "sharing". |
| Financial info > Purchase history | Yes | No | App functionality (granting purchases); Analytics, only if Unity IAP diagnostics are on | Required to buy | The products bought and transaction ids. Processed on device; may reach Unity via IAP diagnostics. |
| App info and performance > Diagnostics | Only if Unity IAP diagnostics are on | No | Analytics | No | IAP library diagnostics. |
| Device or other IDs | No | No | | | No advertising id, no Android id read by game code. [Confirm Unity IAP doesn't send one with your settings.] |
| Personal info, Location, Messages, Photos/videos, Audio, Files, Calendar, Contacts, App activity, Web browsing, Health | No | No | | | |

"Shared" means transferred to a third party for their own use; a service provider processing data on our behalf
(Unity as IAP provider) is not "sharing" under Play's definitions.

## Other Play Console declarations

- **Ads:** "No, my app does not contain ads."
- **Crash reporting (open, O_C2):** the answers above assume none. If the owner adopts a service (GG-T1546: opt-in, off
  by default, scrubbed like the local log), add **App info and performance > Crash logs** and **Diagnostics** (collected,
  not shared, purpose Analytics, optional) and update the privacy policy and `app-privacy-labels.md` first. Android
  vitals alone needs no declaration.
- **Target audience and content:** [owner's choice; see age-ratings.md]. If you include children under 13, Families
  policy requirements apply (and the IAP diagnostics must be checked against them).
- **Financial features:** none.
- **Government app / news app / health:** no.
- **In-app purchases:** yes (declared automatically once billing products exist).
- **Privacy policy URL:** required, host `privacy-policy.md` at the URL chosen in GG-T1558 (placeholder until then).
