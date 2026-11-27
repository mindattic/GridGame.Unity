# GridGame: Apple App Privacy answers

> **DRAFT for the owner's review (GG-T1540, 2026-11-27).** The answers to enter in App Store Connect > App Privacy
> (the "nutrition label") and the privacy-policy URL field, based on what the iOS build does at the commit that adds
> this file. They are kept consistent with [play-data-safety.md](play-data-safety.md); where the two differ the reason is
> given. Apple holds the developer responsible for SDK behaviour too: confirm the Unity IAP item below before
> submitting, and re-check whenever a package, SDK or network feature is added.

## What the iOS build does

| Area | iOS build behaviour |
|---|---|
| App record | Name **GridGame**, bundle id `com.mindattic.gridgame` (same as the Play package; `GridGameBuild.IosBundleId`). |
| Accounts / sign-in | None. The iCloud save has no in-game sign-in; it uses the device's iCloud account. |
| Ads / tracking | None. No ad SDK, no IDFA, no App Tracking Transparency prompt. |
| Analytics / crash reporting | None of ours at present. Crash-reporting choice O_C2 is open (see "If a crash service is adopted"). Unity Analytics and Cloud Diagnostics are off. MindAttic.Telemetry and MindAttic.Media.Capture are inert in release builds. |
| Network | StoreKit (through Unity IAP) when the Shard Store connects and when you buy. iCloud key-value sync is done by the OS (`NSUbiquitousKeyValueStore`, GG-T609); the game opens no connection of its own for it. No server of ours. |
| Cloud save | Each save slot (the Transfer save code, play time, description) is written to the user's own iCloud key-value store, only when iCloud is on for the user and the game. Held by Apple under the user's Apple ID; the developer never receives it. |
| Transfer save code | Settings > Transfer save / Import save: text the user copies and pastes; never sent anywhere by the game. |
| Error log | Unhandled exceptions written to an on-device file only (`CrashReporter`); not uploaded. Crash logs Apple shares with developers (only for users who opted in) arrive through Xcode Organizer, not through our code. |
| Permissions / entitlements | iCloud key-value storage only. No location, camera, microphone, contacts, photos, tracking or push. |

## Questionnaire answers

**Do you or your third-party partners collect data from this app?**
Answer **Yes**, conservatively, only if the Unity IAP check below says purchase diagnostics leave the device. If Unity
IAP sends nothing to Unity beyond the StoreKit transaction itself, **"No, we do not collect data from this app"** is
defensible: Apple's own payment processing and the user's own iCloud storage are not collection by the developer.
Because Data safety (Android) is answered "Yes", answering "Yes" here keeps the two consistent.

### Data types (if answering "Yes")

| Apple category > type | Collected | Linked to user | Used for tracking | Purposes | Notes |
|---|---|---|---|---|---|
| Purchases > Purchase history | Yes | No | No | App functionality | Products bought and transaction ids, used on device to grant Shards once; may reach Unity via IAP diagnostics (matches Data safety "Purchase history"). |
| Diagnostics > Performance data / Other diagnostic data | Only if Unity IAP diagnostics are on | No | No | Analytics | Matches Data safety "Diagnostics". |
| Identifiers > Device ID / User ID | No | | | | No IDFA, no IDFV read by game code. [Confirm Unity IAP sends neither with your Developer Data settings.] |
| Contact info, Health and fitness, Financial info, Location, Sensitive info, Contacts, User content, Browsing history, Search history, Usage data, Surroundings, Body, Other data | No | | | | The iCloud save is the user's own data stored by Apple, not data we collect. |

Every collected type above is **not linked to the user and not used for tracking**: nothing is joined to an account
(we have none) and nothing goes to an ad network or a data broker.

### Differences from Data safety

| Android Data safety | Apple label | Why |
|---|---|---|
| Personal info > User IDs (Play Games player id): declared | Not declared | Play Games sign-in exists only on Android. iOS has no sign-in. |
| App activity > Other user-generated content (Saved Games): declared | Not declared | Google's Saved Games are declared conservatively; the iCloud key-value copy is stored by Apple in the user's own iCloud and is never received by the developer, which Apple does not count as collection. If you prefer to match Android, add **User Content > Other User Content**, collected, not linked, not tracking, App functionality. |
| Purchase history, Diagnostics (IAP): declared | Declared the same | Same Unity IAP behaviour on both stores. |

## If a crash service is adopted (O_C2, GG-T1546)

The answers above assume none. If the owner picks a service, forwarding scrubbed error records only while Settings
"Share crash reports" is on (off by default), add **Diagnostics > Crash data** and **Diagnostics > Other diagnostic
data** (collected, not linked, no tracking, purpose App functionality or Analytics as the service's terms say), and
update `privacy-policy.md` and `play-data-safety.md` in the same change. If the choice is none, nothing here changes.

## Other App Store Connect fields

- **Privacy policy URL:** required; host `privacy-policy.md` at the URL chosen in GG-T1558 [placeholder until then].
- **User privacy choices URL:** not needed (no accounts, nothing to opt out of in the web sense).
- **Tracking:** "No" for every question; do not add `NSUserTrackingUsageDescription`.
- **Age rating and kids:** see [age-ratings.md](age-ratings.md): target 13+, not the Kids category. The Kids category
  would forbid third-party analytics and require checking Unity IAP's diagnostics.
- **Export compliance:** the game uses only HTTPS from the OS and Unity; answer per the standard exemption [owner to confirm
  in App Store Connect].
- **Privacy manifest (`PrivacyInfo.xcprivacy`):** Apple requires the app and its third-party SDKs (Unity, Unity IAP) to
  declare required-reason API use and collected data types; check that the exported Xcode project carries Unity's
  manifests and that the declared data types match the table above.
