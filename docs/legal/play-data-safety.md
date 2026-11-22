# GridGame Gaiden: Google Play Data safety answers

> **DRAFT for the owner's review (GG-T472, 2026-11-20).** The answers to enter in Play Console > App content > Data
> safety, based on what the release build does at the commit that adds this file. Google holds the developer
> responsible for SDK behaviour too: before submitting, confirm the Unity IAP item below against the project's Unity
> Dashboard Developer Data settings and Unity's own Data safety guidance for the IAP package version in
> `Packages/manifest.json` (com.unity.purchasing 5.4.4).

## What the release build does

| Area | Release build behaviour |
|---|---|
| Accounts / sign-in | None |
| Ads | None (no ad SDK, no advertising id) |
| Analytics / crash reporting | None of ours. Unity Analytics, Cloud Diagnostics and performance reporting are off. MindAttic.Telemetry and MindAttic.Capture are inert in release builds (editor/development only, local files only). |
| Network | Only Google Play Billing (through Unity IAP) when the Shard Store connects and when you buy. The game has no other network code. |
| Error log | Unhandled exceptions written to an on-device file (GG-T484 `CrashReporter`: time, version, platform, OS, device model, scrubbed error text). Not uploaded, so not "collected". Android vitals crash/ANR data is collected by Google Play itself, not declared by the developer. |
| Saves | On device (app-private storage), included in Android Auto Backup |
| Permissions | `INTERNET` and `com.android.vending.BILLING` (added by Unity IAP). No location, camera, microphone, contacts or storage permissions. [Owner: confirm against the merged AndroidManifest of a release build.] |

## Questionnaire answers

**Does your app collect or share any of the required user data types?**
Answer **Yes**, conservatively, because of Unity IAP's purchase diagnostics. If, after checking Unity's guidance and
the Developer Data settings, Unity IAP sends nothing off-device beyond the Google Play Billing transaction itself,
"No" is defensible: payment data handled by Google Play Billing on Google's behalf doesn't need to be declared by the
developer.

**Is all of the user data collected by your app encrypted in transit?** Yes (Google Play Billing and Unity endpoints
use HTTPS/TLS).

**Do you provide a way for users to request that their data be deleted?** We hold no user data on our side; everything
the game keeps is on the device and goes away when save slots are deleted or the app is uninstalled. Answer **"No"**
unless Play requires a deletion mechanism for the declared types; if it does, give the privacy contact email from the
privacy policy as the request channel.

### Data types (only if answering "Yes" above)

| Data type | Collected | Shared | Purpose | Optional? | Notes |
|---|---|---|---|---|---|
| Financial info > Purchase history | Yes | No | App functionality (granting purchases); Analytics, only if Unity IAP diagnostics are on | Required to buy | The products bought and transaction ids. Processed on device; may reach Unity via IAP diagnostics. |
| App info and performance > Diagnostics | Only if Unity IAP diagnostics are on | No | Analytics | No | IAP library diagnostics. |
| Device or other IDs | No | No | | | No advertising id, no Android id read by game code. [Confirm Unity IAP doesn't send one with your settings.] |
| Personal info, Location, Messages, Photos/videos, Audio, Files, Calendar, Contacts, App activity, Web browsing, Health | No | No | | | |

"Shared" means transferred to a third party for their own use; a service provider processing data on our behalf
(Unity as IAP provider) is not "sharing" under Play's definitions.

## Other Play Console declarations

- **Ads:** "No, my app does not contain ads."
- **Target audience and content:** [owner's choice; see age-ratings.md]. If you include children under 13, Families
  policy requirements apply (and the IAP diagnostics must be checked against them).
- **Financial features:** none.
- **Government app / news app / health:** no.
- **In-app purchases:** yes (declared automatically once billing products exist).
- **Privacy policy URL:** required, host `privacy-policy.md`.
