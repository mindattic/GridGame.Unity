# Store readiness (GG-T1509)

What still stands between `main` and Google Play and the App Store: one row per item, with the status found in the
repo on 2026-11-27. The final re-audit before submission is GG-T1510. Release-candidate gates stay in
[RELEASE_CHECKLIST](../RELEASE_CHECKLIST.md); how to build is [RELEASE_BUILD](../RELEASE_BUILD.md).
Filterable page: [design/store-readiness](../design/store-readiness/index.html) (regenerate its data with `python tools/store_readiness_page.py` after editing the tables here).

Status: **done** (in the repo, verified by reading it), **partial** (started, a named gap remains), **missing**
(nothing yet), **owner** (only the owner can do it: an account, a console, a device or a decision). Every row that is
not done names its ticket; "Re-check after" names the sibling slice of GG-T1256 that can change the answer.

Today: 52 rows, 12 missing, 19 partial, 11 owner, 10 done.

## Android (Google Play)

| # | Item | Status | Evidence | Gap ticket | Re-check after |
|---|---|---|---|---|---|
| A1 | Android App Bundle (.aab) | **done** | `GridGameBuild.BuildAndroidRelease` writes `Build/AndroidRelease/GridGame.aab` (IL2CPP, release, native symbols in the bundle); RELEASE_CHECKLIST gate 2; GG-T1513 wraps it in the one-command release with its gates | GG-T1513 | - |
| A2 | Target API level | **partial** | `BuildAndroidRelease` sets `targetSdkVersion = AndroidApiLevelAuto` (highest SDK installed; `ProjectSettings.asset` `AndroidTargetSdkVersion: 0`); nothing logs the resolved level or fails below Play's requirement; API 36 expected for new apps and updates from 31 Aug 2026; GG-T1515 adds the gate and confirms the number on Google's page | GG-T1515 | - |
| A3 | Minimum SDK 31 (Android 12) | **done** | `BuildAndroidRelease` sets `AndroidApiLevel31`; only the dev APK keeps 26 for the emulator | - | - |
| A4 | ARM64 only | **done** | `BuildAndroidRelease` sets `targetArchitectures = ARM64` (the dev APK adds x86_64); GG-T1516 verifies the built bundle carries only arm64-v8a libraries | GG-T1516 | - |
| A5 | 16 KB memory page size | **partial** | Unity 6000.4.3f1 builds 16 KB-aligned IL2CPP libraries, but nothing checks the built bundle's ELF segments; required for apps targeting Android 15+ | GG-T1516 | - |
| A6 | Play App Signing and upload key | **owner** | Release build signs with `GG_ANDROID_KEYSTORE` / `android-signing.env` and refuses an unsigned bundle (RELEASE_BUILD 'Signing'); the keystore does not exist yet; Play App Signing enrols on the first upload | GG-T1525, GG-T1526 | - |
| A7 | Play Billing (Unity IAP) | **partial** | `com.unity.purchasing` 5.4.4 in `Packages/manifest.json`, `App/Services/UnityIapDiamondStore.cs` (with `GG_IAP`); the Play Billing Library version it bundles is not confirmed against Play's minimum; products not created | GG-T1517, GG-T471 | GG-T1289 |
| A8 | Data safety form | **partial** | `docs/legal/play-data-safety.md` (GG-T472) predates Play Games sign-in, Saved Games cloud save and the Transfer code | GG-T1540, GG-T1529 | GG-T1289 |
| A9 | Content rating (IARC) | **partial** | `docs/legal/age-ratings.md` answers exist; to be refreshed for what shipped, then entered by the owner | GG-T1540, GG-T1529 | GG-T1257, GG-T1270, GG-T1289 |
| A10 | Target audience | **owner** | `docs/legal/privacy-policy.md` still asks the owner to confirm the target age group ('not directed at children under 13') | GG-T1541, GG-T1529 | - |
| A11 | Store icon 512x512 | **done** | `Assets/GridGame/AppIcon/app-icon-store-00.png` (512x512 PNG; Play accepts 32-bit with alpha) | - | - |
| A12 | Feature graphic 1024x500 | **missing** | No feature graphic in the repo | GG-T1521 | - |
| A13 | Phone screenshots | **partial** | `docs/store/screenshots/1080x1920/` (8 shots) predate the hub UX, front door and Summon slices | GG-T1522 | GG-T1258, GG-T1289 |
| A14 | Listing copy (name, short and full description) | **partial** | `docs/STORE_LISTING.md` has the copy and measured limits; the name is still a working title | GG-T1523, GG-T573, GG-T1530 | GG-T1258, GG-T1270, GG-T1289 |
| A15 | Internal and closed testing (12 testers x 14 days) | **owner** | Nothing uploaded yet; new personal developer accounts must run a closed test with 12+ opted-in testers for 14 days before applying for production | GG-T1526, GG-T1527, GG-T1123 | - |
| A16 | Android 12+ system splash | **missing** | Unity's splash is off (`EnsureScene`: `SplashScreen.show = false`), so the OS shows its default splash on every cold start | GG-T1519 | - |
| A17 | Adaptive icon | **done** | GG-T486 (closed): `GridGameBuild.ApplyAppIcons` fills every legacy, round and adaptive slot from `Assets/GridGame/AppIcon/` | - | - |
| A18 | Upload from the command line | **missing** | Uploads are manual through Play Console | GG-T1514, GG-T1528 | - |
| A19 | Production rollout | **owner** | Waits on A15 and production access | GG-T1531 | - |

## iOS (App Store)

| # | Item | Status | Evidence | Gap ticket | Re-check after |
|---|---|---|---|---|---|
| I1 | Bundle id `com.mindattic.gridgame` | **done** | `GridGameBuild.IosBundleId` (= `AndroidPackage`), set by `BuildIOS` | - | - |
| I2 | Signing and provisioning | **owner** | `BuildIOS` turns on automatic signing for `GG_IOS_TEAM_ID` when set (RELEASE_BUILD 'iOS'); needs an Apple Developer account and a Mac | GG-T1535, GG-T479 | - |
| I3 | App Store Connect record, Paid Apps agreement | **owner** | No App ID or app record yet | GG-T1537, GG-T1536 | - |
| I4 | App Privacy labels | **missing** | `docs/legal/app-privacy-labels.md` does not exist yet | GG-T1540, GG-T1539 | GG-T1289 |
| I5 | App Tracking Transparency | **done** | Not needed: no ads, attribution or analytics SDK (`Packages/manifest.json`; no AdSupport, Analytics, Crashlytics or Sentry in code), so nothing tracks across apps and no `NSUserTrackingUsageDescription` is declared; becomes required if ads, an attribution SDK or a crash/analytics service that shares an identifier is added (watch GG-T1545); GG-T1532 fails the export if AdSupport or AppTrackingTransparency get linked | GG-T1532 | - |
| I6 | Sign in with Apple | **done** | Not needed: guideline 4.8 applies only when an app offers third-party login; iOS has none (cloud save is the iCloud key-value store, `App/Services/ICloudCloudBackend.cs`; Play Games sign-in is Android-only); becomes required if iOS ever offers Google, Play Games, Facebook or any other third-party sign-in | - | - |
| I7 | StoreKit through Unity IAP | **partial** | `UnityIapDiamondStore` drives StoreKit on iOS; the In-App Purchase capability is added in Xcode (RELEASE_BUILD 'Signing, archiving and uploading'); no products, no sandbox purchase yet | GG-T471, GG-T1538 | GG-T1289 |
| I8 | TestFlight | **owner** | No build uploaded yet | GG-T1538 | - |
| I9 | Privacy manifest (PrivacyInfo.xcprivacy) | **missing** | No `.xcprivacy` under `Assets/`; Unity's engine manifest does not cover the game's code or `GGICloud.mm` | GG-T1532 | - |
| I10 | Export compliance (ITSAppUsesNonExemptEncryption) | **missing** | Not set in the exported Info.plist, so App Store Connect asks on every upload | GG-T1532 | - |
| I11 | App icon without alpha | **missing** | `Assets/GridGame/AppIcon/app-icon-00.png` is 1024x1024 RGBA and is the default icon for iOS (`ApplyAppIcons`); the App Store rejects a marketing icon with alpha | GG-T1533 | - |
| I12 | Launch screen | **missing** | `ProjectSettings.asset` `iOSLaunchScreenType: 0` (Unity's default); nothing set from `BuildIOS` | GG-T1533 | - |
| I13 | Review guidelines, incl. loot-box odds | **partial** | Odds shown before purchase for the Diamond summon (`Meta/DiamondSummon.cs` `Odds`, GG-T503 closed); no full guideline walk yet; the Summon slice must keep the odds disclosure | GG-T1534 | GG-T1289 |
| I14 | Xcode project compiles, archives, passes processing | **owner** | `BuildIOS` exports `Build/iOS` from Windows; nothing has compiled it on a Mac yet | GG-T479, GG-T1128 | - |
| I15 | App Store submission | **owner** | Privacy, age rating, accessibility labels, screenshots, review notes | GG-T1539 | - |
| I16 | App Store screenshots (6.9-inch and 6.5-inch) | **partial** | `docs/store/screenshots/1284x2778/` (6.5-inch, 8 shots); no 6.9-inch 1320x2868 set; all predate the GG-T1256 slices | GG-T1522 | GG-T1258, GG-T1289 |

## Shared

| # | Item | Status | Evidence | Gap ticket | Re-check after |
|---|---|---|---|---|---|
| S1 | Privacy policy and terms at public URLs | **missing** | `docs/legal/privacy-policy.md` and `docs/legal/terms.md` carry `[privacy contact email]` / `[URL]` placeholders and are not hosted (`docs/STORE_LISTING.md` 7b) | GG-T1540, GG-T1541, GG-T1542 | - |
| S2 | GDPR / COPPA (target audience) | **partial** | Policy says the game is not directed at under-13s, pending the owner's confirmation; no personal data leaves the device except through the stores' purchase and cloud-save services | GG-T1541, GG-T1534 | GG-T1289 |
| S3 | Account deletion / data erasure | **done** | No accounts, so the account-deletion rules do not apply. Settings > Data > **Delete all game data** (GG-T1580, BIBLE §15.4) erases, after two confirms, every profile, save slot and setting on the device, the account wallet (Shards, owned products, cosmetics; not refunded) and every slot's cloud copy (Play Games snapshot delete, iCloud key removal, GG-T1544). GDPR erasure is self-service in the game; the privacy policy should name this row as the way to erase data | - | - |
| S4 | Crash reporting and analytics consent | **partial** | `App/Services/CrashReporter.cs` writes C# exceptions to `Diagnostics/errors.jsonl` on the device only; native crashes reach Android vitals and the Xcode Organizer; no analytics, so no consent toggle yet | GG-T1545, GG-T1546, GG-T1543 | - |
| S5 | Cloud save | **partial** | GG-T500 chain: `App/Services/PlayGamesCloudBackend.cs` (GG-T608) and `ICloudCloudBackend.cs` (GG-T609) are built and inert until the console and Apple steps are done | GG-T500, GG-T1115, GG-T1127, GG-T1128, GG-T1129 | - |
| S6 | Localization | **missing** | English only: no Localization package, strings in code, content JSON and Prose; launch languages undecided | GG-T1551, GG-T1552 | GG-T1258, GG-T1270 |
| S7 | Accessibility | **partial** | Settings > Accessibility in `App/Screens/Settings/SettingsScreen.cs` (Text size GG-T495, colorblind palette, reduce motion); no Accessibility Nutrition Labels answers | GG-T1553, GG-T1539 | - |
| S8 | Performance, battery and thermal on target and low-end devices | **partial** | No idle frame-rate drop (no `OnDemandRendering`), no thermal response (`com.unity.modules.adaptiveperformance` is in the manifest but unused), no on-device profile recorded | GG-T481, GG-T1202, GG-T1547, GG-T1548, GG-T1123 | - |
| S9 | App size | **done** | RELEASE_BUILD 'Size budget' (GG-T499): 163 MB of Play's 200 MB compressed download (`bundletool get-size total`); GG-T1513 turns it into a gate that fails over 190 MB | GG-T1513 | GG-T1289 |
| S10 | Icons and splash per platform | **partial** | Android icons done (A17); Android 12 splash (A16), iOS icon (I11) and launch screen (I12) open | GG-T1519, GG-T1533 | - |
| S11 | Offline behaviour and interruptions | **partial** | The game plays offline; boot, Shard Store, cloud-save offer/Restore and Play Games sign-in are not audited with no network or a captive portal | GG-T1549 | GG-T1258 |
| S12 | Versioning | **partial** | `version.txt` = `1` becomes `bundleVersion`; build numbers are the commit count (`GridGameBuild.AndroidVersionCode`, iOS `buildNumber`); no semantic version or release notes | GG-T1512 | - |
| S13 | Release pipeline | **partial** | `tools/build.ps1` builds each platform; no one-command release with gates and no scripted upload | GG-T1513, GG-T1514 | - |
| S14 | Support contact | **missing** | `docs/STORE_LISTING.md` 7b: support email, support URL and privacy contact are 'owner to supply'; no About section in Settings | GG-T1541, GG-T1542, GG-T1543 | GG-T1258 |
| S15 | Stale `Editor/GaidenBuild.cs` duplicate | **missing** | `Assets/GridGame/Editor/GaidenBuild.cs` still exists beside `GridGameBuild.cs` (a second AssetPostprocessor and Prose pre-build hook) | GG-T1511 | - |
| S16 | Game name | **owner** | Working titles GridGame/Gaiden in the listing, app name and docs | GG-T573 | - |
| S17 | Story text published from Prose | **owner** | The slice's crawls, descriptions and barks wait for the owner's approval in Prose | GG-T1520 | GG-T1270 |

## Owner-only

Every OWNER ticket in this epic (GG-T1476) plus the existing ones. None can be closed by an agent.

| Ticket | What | Unblocks rows |
|---|---|---|
| GG-T1524 | Play developer account and payments profile | A6, A7, A15 |
| GG-T1525 | Android upload keystore | A6 |
| GG-T1526 | Play app record and first internal upload | A6, A15 |
| GG-T1527 | Play closed test, 12 testers x 14 days | A15, A19 |
| GG-T1528 | Play API service account | A18 |
| GG-T1529 | Play App content declarations | A8, A9, A10 |
| GG-T1530 | Enter Play store listing | A14 |
| GG-T1531 | Play production rollout | A19 |
| GG-T1535 | Apple Developer Program | I2 |
| GG-T1536 | Paid Apps agreement, tax, banking | I3, I7 |
| GG-T1537 | App ID and App Store Connect record | I3 |
| GG-T1538 | TestFlight build and sandbox purchase | I7, I8 |
| GG-T1539 | App Store submission | I4, I15, S7 |
| GG-T1541 | Legal details and sign-off | A10, S1, S2, S14 |
| GG-T1545 | Pick crash reporting service | S4 |
| GG-T1552 | Launch languages and countries | S6 |
| GG-T1520 | Publish Commander texts in Prose | S17 |
| GG-T471 | Store products setup (diamonds_100/500/1000/2500) | A7, I7 |
| GG-T479 | iOS build pipeline (Mac, Xcode) | I2, I14 |
| GG-T573 | Choose the game's name | A14, S16 |
| GG-T1115 | Play Games Console setup | S5 |
| GG-T1123 | Release phone check | A15, S8 |
| GG-T1127 | iCloud App ID setup | S5 |
| GG-T1128 | iCloud Xcode build check | I14, S5 |
| GG-T1129 | iCloud save phone check | S5 |
| GG-T462 | Generate missing Suno tracks | content |
| GG-T463 | Pick and buy SFX library | content |
| GG-T492 | Generate gap art | content |

## Depends on sibling slices (GG-T1256)

Re-check these rows after each slice lands; GG-T1510 re-runs the whole list before submission.

| Slice | Rows to re-check | Why |
|---|---|---|
| GG-T1289 Hub, progression, roster and monetization (Summon) | A7, A8, A9, A13, A14, I4, I7, I13, I16, S2, S9 | The odds disclosure must survive the Summon rework; new purchases change Data safety, Privacy labels and products; new screens need screenshots; new art changes size |
| GG-T1258 Front door and onboarding | A13, A14, I16, S6, S11, S14 | New first screens change screenshots and copy; the About screen hosts the support links; boot paths join the offline audit; more strings to localise |
| GG-T1270 Commander and world lore | A9, A14, S6, S17 | Story text can move the age rating and feeds the store copy and the string count |
| GG-T1257 Forming the team: Trust and cohesion | A9 | New dialogue is re-read against the content-rating answers |
