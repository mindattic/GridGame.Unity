# Release builds (GG-T478, GG-T479)

How to produce the store builds. Everything runs from the command line through `tools/build.ps1`, which drives Unity
in batchmode through `Assets/GridGame/Editor/GridGameBuild.cs`. The Unity Editor must be closed while it runs.

| Command | Output | What it is |
|---|---|---|
| `pwsh tools/build.ps1` | `Build/Windows/GridGame.exe` | Windows development player (automation endpoint on) |
| `pwsh tools/build.ps1 -Release` | `Build/WindowsRelease/GridGame.exe` | Windows release player |
| `pwsh tools/build.ps1 -Platform Android` | `Build/AndroidRelease/GridGame.aab` | **Google Play bundle**: IL2CPP, ARM64 only, release, signed with the upload key |
| `pwsh tools/build.ps1 -Platform Android -Dev` | `Build/Android/GridGame.apk` | Emulator/test APK: ARM64 + x86_64, development, debug-signed (`tools/androidcheck.py`) |
| `pwsh tools/build.ps1 -Platform iOS` | `Build/iOS` | **Xcode project** for the App Store: IL2CPP, iPhone only, iOS 17+, release (`-Dev`: development); signed and archived in Xcode on a Mac |
| `pwsh tools/build.ps1 -Platform WebGL` | `Build/WebGL` | WebGL |

`-Platform` and `-Target` are the same switch. Add `-SkipProse` to build with the committed story text instead of
pulling the latest from Prose first.

## Android (Google Play)

**What the bundle is.** Package `com.mindattic.gridgame`, min SDK 31 (Android 12; the dev APK stays at 26), target SDK = the highest
platform installed with Unity (36 with Unity 6000.4.3f1; Play's minimum target rises every August), IL2CPP with
"optimize speed" code generation, ARM64 only (every Play device since 2019; x86_64 is only for the emulator APK),
Unity's default graphics APIs (Vulkan with a GLES3 fallback), portrait.

**Target devices (GG-T724, BIBLE §26.3).** Flagship iPhone, Samsung Galaxy (S, Z Fold, Z Flip) and Google Pixel
phones from 2021 on: a Snapdragon 8 Gen 1 / Exynos 2200 / Tensor G1 or newer. The chosen Android floor is **API 31
(Android 12)** for the release bundle; `BuildAndroidRelease` sets 31 (GG-T725; the
development APK keeps 26 for BlueStacks 5 and the emulator suite). Restricting who can install it is a **Play Console
device catalog** job (Release > Device catalog): include only Samsung and Google, exclude Galaxy A/M/F if wanted, RAM
< 6 GB, chips older than the floor, tablets and Chromebooks.

**Version.** `versionName` is `version.txt`. `versionCode` is the number of commits on the checked-out history
(`git rev-list --count HEAD`): it only grows on main, so a build of a newer commit always outranks the last upload and
nothing has to be remembered or committed. Set `GG_ANDROID_VERSION_CODE=<n>` to force one (for example a hotfix built
from an older commit, which must still be higher than anything uploaded). The build puts the project's own version code
back afterwards, so `ProjectSettings.asset` doesn't change. On a **shallow clone** (`git rev-parse
--is-shallow-repository` is true, as in a default CI checkout) the count is wrong, so the release build stops unless
`GG_ANDROID_VERSION_CODE` is set; fetch the full history or set it.

**The first build is slow.** Switching the project to Android re-imports every texture as ASTC and IL2CPP compiles the
whole game to C++: expect 20-40 minutes and several GB of RAM the first time, much less after. The next Windows build
switches back (also slower than usual once).

**Size budget (GG-T499).** Google Play caps the base module at **200 MB compressed download**. Measure a bundle
with bundletool (ships with Unity) rather than the .aab file size, which also carries the native debug symbols
(~22 MB in `BUNDLE-METADATA`, never downloaded):

```powershell
$t = "C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Data\PlaybackEngines\AndroidPlayer"
& "$t\OpenJDK\bin\java.exe" -jar "$t\Tools\bundletool-all-1.17.2.jar" build-apks --bundle=Build\AndroidRelease\GridGame.aab --output=gg.apks --overwrite
& "$t\OpenJDK\bin\java.exe" -jar "$t\Tools\bundletool-all-1.17.2.jar" get-size total --apks=gg.apks
```

The first bundle (2026-11-20) was 547 MB (545 MB download): Unity's automatic Android format had left the 81 JPEG
backdrops as raw RGB24, 13.5 MB each. `GridGameImport` (`Assets/GridGame/Editor/GridGameBuild.cs`) now gives Android its own
explicit settings, and Standalone/iOS/WebGL keep the defaults:

| Asset (Android only) | Setting | Compressed in the bundle |
|---|---|---|
| Backdrops, 81 | max 2048 (1024x2048), ASTC 8x8, best quality | 398 MB -> 30 MB |
| Portraits, 213 | max 1024, ASTC 6x6, best quality | 46 MB -> 43 MB |
| Music, 24 tracks (56 min) | Streaming Vorbis quality 0.35 (~75 kbps; Standalone 0.6) | 43 MB -> 31 MB |
| Engine + IL2CPP code (`lib/arm64-v8a`) | - | 28 MB |
| Everything else (fonts, VFX, data, icons, dex) | unchanged | ~30 MB |
| **Download (bundletool get-size total)** | | **545 MB -> 163 MB** |

Headroom is about 37 MB. Every new backdrop costs ~0.37 MB, a portrait ~0.2 MB, a minute of music ~0.55 MB. ASTC
barely shrinks in the zip (portraits only zip well because of their transparent margins), so these per-asset costs add
up linearly. Visual check (2026-11-20, astcenc via `EditorUtility.CompressTexture`, viewed at 2x zoom): ASTC 8x8 on a
1024x2048 backdrop is slightly softer than 4x4 in fine detail but clean on the painted skies (PSNR 31-37 dB); a phone
shows a backdrop about 1080 px wide, so 1024x2048 is close to screen resolution. Portrait ASTC 6x6 looks the same as 4x4
(PSNR 38-42 dB); 4x4 would add only ~2.5 MB to the download but double the texture memory.

If the budget runs out, the next levers in order: portraits to ASTC 8x8 (~-10 MB, visibly softer line art), music to
Vorbis 0.25 or 32 kHz (~-5 MB), then **Play Asset Delivery** install-time asset packs (up to 1 GB each, outside the
200 MB base cap) for music or backdrops. PAD changes how those assets load (Addressables or AssetBundles instead of
`Resources.Load`), so it is a code change, not a setting.

Changing the import rules: bump `GridGameImport.GetVersion()` so Unity re-imports what they touch, then build Android
once and commit the rewritten `.meta` files (they record the Android override) with the rule change.

### Signing (one-time setup, the owner)

Google Play uses **Play App Signing**: Google holds the key that signs what players download; you sign uploads with
your own **upload key**. If the upload key is lost, Google can reset it (Play Console > Setup > App signing), so it
is replaceable, but keep it backed up and keep its passwords out of git.

1. Create the upload keystore once (keytool ships with Unity's OpenJDK):

   ```powershell
   & "C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" `
     -genkeypair -v -keystore D:\Secrets\gridgame-upload.jks -alias upload `
     -keyalg RSA -keysize 4096 -validity 10000
   ```

   It asks for a keystore password and your name/organisation. Keep the file **outside the repo** (or anywhere: git
   ignores `*.jks` and `*.keystore`). Back it up somewhere safe (a password manager's file attachment works).

2. Create `android-signing.env` in the repo root (git-ignored; never commit it):

   ```
   GG_ANDROID_KEYSTORE=D:\Secrets\gridgame-upload.jks
   GG_ANDROID_KEYSTORE_PASS=<keystore password>
   GG_ANDROID_KEY_ALIAS=upload
   GG_ANDROID_KEY_PASS=<key password; omit if it is the same as the keystore password>
   ```

   The same four can be environment variables instead (a CI secret store, for example); the file wins when both
   exist. `tools/build.ps1` passes them to Unity's process only. Unity keeps passwords in memory and never writes them
   to ProjectSettings; the build also puts the keystore path back afterwards, so no machine's key path is committed.

3. `pwsh tools/build.ps1 -Platform Android`. The log (`Logs/build-Android.log`) says
   `[Build] Android: signing with the upload key 'upload' from gridgame-upload.jks`.

**Without a keystore** (unset, or the file is missing) the release build stops before changing any settings, so a
scripted release can't go green with a bundle Play would refuse. For a local test bundle signed with Unity's debug key
(fine for `bundletool` testing on your own phone, not for upload), opt in with `-allowDebugSigning` or
`GG_ANDROID_ALLOW_DEBUG_SIGNING=1` (it can go in `android-signing.env`). An empty `GG_ANDROID_KEY_PASS` counts as unset
and falls back to the keystore password.

### Testing a bundle on a phone

An `.aab` doesn't install directly. Either upload it to an **internal testing** track in Play Console (fastest real
test, installs from the Play Store on your devices), or use `bundletool`:

```powershell
java -jar bundletool.jar build-apks --bundle=Build\AndroidRelease\GridGame.aab --output=gg.apks --connected-device `
  --ks=D:\Secrets\gridgame-upload.jks --ks-key-alias=upload
java -jar bundletool.jar install-apks --apks=gg.apks
```

For day-to-day testing the `-Dev` APK is simpler: `adb install -r Build\Android\GridGame.apk`.

### Profiling on a real phone (GG-T481, the owner)

Targets are flagship iPhone, Galaxy and Pixel ([BIBLE 26.3](BIBLE.md#263-adaptive-stage-gg-t169)). Static finding: the
backdrop memory item is already handled by the Android ASTC import above (81 backdrops, 30 MB, not 4.5 MB each); no
oversized textures found. What needs a device (install the `-Dev` APK, `adb devices` must list the phone):

1. Frame time in a big battle: start a late-stage battle with 4 heroes and many foes, then
   `adb shell dumpsys gfxinfo com.mindattic.gridgame framestats` (reset first with `... reset`) after a minute of play;
   read 50th/90th/99th percentile frame time and janky-frame %. Target: 90th percentile under the display refresh
   interval (16.7 ms at 60 Hz). The in-game `fps` value is also on the automation endpoint (`state`).
2. Memory: `adb shell dumpsys meminfo com.mindattic.gridgame` at title, hub, and after 10 battles in a row (watch
   Graphics, Native, TOTAL PSS for growth); note the peak.
3. Load times: `adb shell am start -W -n <package>/<activity>` gives cold-start TotalTime; time hub to battle by eye
   or screen recording.
4. Battery: `adb shell dumpsys batterystats --reset`, play 30 minutes, then `dumpsys batterystats` and note the
   app's mAh and screen-on drain per hour.
5. For hot spots, attach the Unity Profiler over USB to a development build (Autoconnect Profiler), or capture a
   Perfetto trace. File each finding as its own fix ticket with the numbers.

### Playing on emulated devices: Android Emulator Suite (GG-T547)

To play the whole game with the mouse as it looks on a given phone or tablet, use the **Android Emulator Suite**
(its own repo, `D:\Projects\MindAttic\Android.Emulator.Suite`; deployed to
`C:\Apps\Android.Emulator.Suite\AndroidEmulatorSuite.exe`, Desktop shortcut "Android Emulator Suite").

1. Publish the APK: `powershell -ExecutionPolicy Bypass -File tools\deploy-apk.ps1 -Build` builds the `-Dev` APK
   and copies it to `C:\Apps\GridGame.Unity\GridGame.apk` (with `GridGame.txt`: version, commit, build
   time). Without `-Build` it publishes the APK already in `Build\Android`. The suite's **Build APK** button runs the
   same command.
2. Open the suite: app **GridGame**, pick a device from the gallery (about 100 real phones, foldables, tablets
   and iPhone/iPad aspect profiles, with photo and specs; search and filter by form), pick an emulator (Android
   Emulator, BlueStacks 5 or LDPlayer; only installed ones show), press **Launch**.
3. The emulator boots shaped like that device (the Android Emulator gets the device's exact panel: resolution and
   density, so the window has its aspect), the APK installs when missing or newer, and the game starts. Click = tap,
   drag = drag. **Reinstall APK** installs again keeping the save; **Reset to native** restores the emulator's own
   display; **Stop** closes it.

Notes: the Android Emulator is the reference (x86_64 image, real GPU). The suite hides the emulator's Vulkan, so the
game runs on OpenGL ES 3 there: on the emulator's Vulkan driver the development APK crashed about three minutes in
(a development build labels every Vulkan object). The development APK is now OpenGL ES 3 first with Vulkan as the
fallback, so any emulator runs it without hiding Vulkan; `-vulkan` still builds a Vulkan-only APK for diagnostics,
and the release bundle keeps Unity's default list (GG-T549). Verified 2026-11-21 on Pixel 7 (20:9), Galaxy Z Flip6 (22:9), iPad 9th gen (4:3), a 480 x 800 mdpi phone
and BlueStacks 5; findings filed as GG-T548 (telemetry error console), GG-T550 (4:3 list screens), GG-T551 (tiny text
at 480 x 800). LDPlayer 4 is Android 7.1 and can't install
GridGame (min SDK 26); BlueStacks 5 (Android 9) can. Run one emulator at a time. The development APK shows Unity's
"Development Build" mark and has the automation endpoint (`tools/androidcheck.py` drives it over `adb forward`).

### Google Play Console (the owner)

1. A Google Play developer account (one-time US$25), identity verification.
2. Create the app: name, default language, "Game", free (with in-app purchases).
3. App signing: accept Play App Signing on the first upload; the first bundle's signature registers your upload key.
4. Before production: store listing (icon 512x512, feature graphic 1024x500, phone screenshots), content rating
   questionnaire, data-safety form, privacy policy URL (GG-T472 drafts these), target audience, ads declaration (none).
5. New personal developer accounts must run a **closed test with at least 12 testers for 14 days** before they can
   publish to production.
6. In-app products (`diamonds_100/500/1000/2500`, GG-T471) can only be created after a bundle with the billing
   permission has been uploaded to some track. **Display titles (GG-T568):** the currency is **Mana Shards**, so
   create the products with these titles (the product ids keep the old word and must not change): `diamonds_100`
   "100 Aetherite Shards", `diamonds_500` "500 Aetherite Shards", `diamonds_1000` "1,000 Aetherite Shards",
   `diamonds_2500` "2,500 Aetherite Shards"; description "Mana Shards for GridGame: a random summon, an
   instant Rested refill or a lucky craft." Use the same titles on App Store Connect.

**Play Billing Library (GG-T1517).** Play requires Billing Library 8 or newer for new apps and updates (version 7 ran out its extension on 1 Nov 2026; check the Play Console deprecation page for the current date). `com.unity.purchasing` 5.4.4 bundles **9.0.0** (`Plugins/UnityPurchasing/Android/IAPResolver/IAPAndroidDependencies.cs`, `com.android.billingclient:billing:9.0.0`), so no package upgrade is needed; re-check the bundled version on every Unity IAP bump. `GG_IAP` is defined by `GridGame.App.asmdef`'s `versionDefines` whenever `com.unity.purchasing` >= 5.0.0 is installed, in every build including the Android release, not by Player Settings.

**Scripted upload (GG-T1514).** `python tools/play_upload.py` sends `Build/AndroidRelease/GridGame.aab` to Play through
the Developer API (edit, bundle, track with the release notes, commit). Setup, once: `pip install
google-api-python-client google-auth`; in Play Console > Setup > API access link a Google Cloud project, create a
service account and give it release permission on the app; download its JSON key and keep it outside git
(`play-service-account*.json` is ignored); point `GG_PLAY_SERVICE_ACCOUNT` at it or put
`GG_PLAY_SERVICE_ACCOUNT=<path>` in the git-ignored `play-publisher.env`. `--validate` does everything but commit (run
it first); `--track closed|production` and `--rollout 0.1` pick another track and a staged rollout. It refuses when
`release.json`'s sha256 does not match the .aab, and names the release `v<version>+<code>`.

### Cloud save: Play Games Saved Games (GG-T608)

The game side is built and inert (BIBLE §15.2): `Meta/CloudSave` (names, snapshot body, which copy wins, the upload
debounce), `App/Services/CloudSaves` (upload after `Persist`, the "Use the cloud save from …?" offer before a load,
Load Game's **Restore from cloud**) and `App/Services/PlayGamesCloudBackend` (compiled only with the `GG_PLAY_GAMES`
define in an Android player). Turning it on needs, in order:

1. **The owner, Play Console:** the app exists (above) → *Grow > Play Games Services > Setup and management >
   Configuration*: create the Play Games Services project for the app, add an **Android OAuth credential** for
   package `com.mindattic.gridgame` with the SHA-1 of the **app signing key** (Play App Signing page) and a second one
   with the **upload key**'s SHA-1 (for sideloaded test builds), turn on **Saved Games** (*Configuration > Properties*),
   add the testers under *Testers*, and publish the Play Games configuration. Send the agent the **application id**
   (the 12-digit number shown at the top of that page) — nothing else is secret or needed.
2. **An agent, with that id:** add the Google Play Games plugin for Unity (v11 or newer, the
   `com.google.play.games` package from github.com/playgameservices/play-games-plugin-for-unity) to
   `Packages/manifest.json`; write the id into the plugin's Android setup (`GooglePlayGames/.../GameInfo.cs` and
   `Assets/Plugins/Android/GooglePlayGamesManifest.androidlib`, which the plugin's setup step generates from the id —
   do it from a script, not the Editor window); add the plugin's runtime assembly to `GridGame.App.asmdef`
   `references`; add `GG_PLAY_GAMES` to the Android scripting define symbols in `ProjectSettings/ProjectSettings.asset`;
   then build the Android bundle and confirm `PlayGamesCloudBackend` compiles against the installed plugin version
   (it was written against the v11 `ISavedGameClient` API without the package present).
3. **Check on a phone** signed into a tester account: play a minute, background the app, reinstall (or a second phone),
   Load Game > **Restore from cloud** brings the slot back; then play further on one phone and Continue on the other
   offers the newer cloud save.

## Crash and error reporting (GG-T484)

What a release build does today, and what is still the owner's call:

- **Native crashes and ANRs (Android): Google Play's Android vitals**, with no SDK. Play Console > Quality > Android
  vitals lists crashes and "app not responding" freezes from every player who opted into usage sharing. The release
  bundle embeds IL2CPP's native symbol table (`symbols` in the .aab), so the stacks there are readable without
  uploading anything by hand.
- **C# exceptions don't crash a Unity game** (Unity logs them and carries on), so vitals never sees them. Every one is
  written, in every build, to the device's own error log by `CrashReporter` (`App/Services/CrashReporter.cs`):
  `<persistentDataPath>/Diagnostics/errors.jsonl`, one JSON line per exception (time, app version, platform, OS,
  device model, type, message, stack), rolled at 256 KB, at most 50 records a launch and 3 of the same exception.
  Paths are scrubbed: the data folder becomes `<data>`, a user's home folder `<user>`, a profile name `<profile>`. No
  save data, device id, account or IP is in it. On Android it's under
  `/sdcard/Android/data/com.mindattic.gridgame/files/Diagnostics/` (`adb pull` it from a test phone).
- **MindAttic.Telemetry** gets the same record as an `exception` event, but only where telemetry runs at all: the
  editor and development builds (it is gated off in release builds and writes to the local disk, `R:\Telemetry`, so
  it can't carry reports from players' phones). That is why it isn't the release sink. On phones and tablets
  (development builds), `GameTelemetry` (`App/Services/GameTelemetry.cs`, GG-T548) writes it under
  `<persistentDataPath>/Telemetry/GridGame/` instead (Android: `/sdcard/Android/data/com.mindattic.gridgame/files/Telemetry/`),
  keeps the newest 20 sessions, and logs a failure as a plain line, never a red error over the battle.
- **Not built: sending managed exceptions home from players' phones.** It needs a service and a privacy-policy line,
  so it waits for the owner's pick. Options, cheapest first: (1) **Unity Cloud Diagnostics** (free tier; link the
  project to a Unity Cloud project id, add `com.unity.services.cloud-diagnostics`); (2) **Firebase Crashlytics** (free;
  needs a Firebase project and its `google-services.json`, adds the Firebase SDK); (3) **Sentry** (free tier, one DSN
  string). Whichever is picked, `CrashReporter.Record` is the one place to forward from, and the Play data-safety form
  (GG-T472) must then declare "crash logs" and "diagnostics", collected, not shared, not linked to the user.

## iOS (GG-T479; the Xcode steps need a Mac and an Apple account, the owner)

**The export (any machine, Windows included).** `pwsh tools/build.ps1 -Platform iOS` (or the Editor menu **GridGame >
Export iOS Xcode Project**) runs `GridGameBuild.BuildIOS` and writes an **Xcode project folder**, `Build/iOS`
(`Unity-iPhone.xcodeproj` inside). Windows can export it (the iOSSupport module is installed with Unity 6000.4.3f1);
only Xcode on a Mac can compile, sign, archive and upload it. What the export sets:

| Setting | Value | Why |
|---|---|---|
| Bundle id | `com.mindattic.gridgame` (`GridGameBuild.IosBundleId`, the same as the Play package) | the App Store Connect record must match |
| Scripting backend | IL2CPP, "optimize speed" code generation | iOS's only backend; ARM64 is the only iOS architecture |
| Devices | iPhone only (`targetDevice` set to iPhone only by the build) | GG-T724: iPads would only letterbox |
| Minimum iOS | **17.0** (`GridGameBuild.IosMinimumVersion`) | GG-T724, BIBLE §26.3: iPhone 13 (A15) and newer flagships |
| Orientation | portrait, full screen, status bar hidden | the stage is portrait-only |
| Version | `CFBundleShortVersionString` = `version.txt`; `CFBundleVersion` (build number) = the commit count, like the Play version code | App Store Connect refuses an upload whose build number isn't higher than the last one for that version; `GG_IOS_BUILD_NUMBER=<n>` forces one (shallow clones stop the build, as on Android) |
| Signing | automatic signing for `GG_IOS_TEAM_ID` when that environment variable is set; otherwise none (pick the team in Xcode) | the team id is not a secret, but the build puts the project's own signing settings and build number back afterwards so `ProjectSettings.asset` never records a team |
| Release / development | release; `-Dev` (or `GG_IOS_DEV=1`) exports a development build | |

The first export is slow (the project switches to iOS and re-imports textures; IL2CPP converts the game to C++), as
with Android; the next Windows build switches back.

**Not checked from Windows (2026-11-24).** No Mac or Apple account was available when `BuildIOS` was written, so the
exported project has never been compiled, signed or run on an iPhone. The first Mac build is the real test; file what
breaks against GG-T479's epic (GG-T477).

### Owner checklist (one time)

1. **Apple Developer Program** (US$99/year), enrolled as an individual or as an organisation (an organisation needs a
   D-U-N-S number and shows the company as the seller). Note your **Team ID** (developer.apple.com > Account >
   Membership details; 10 characters).
2. **A Mac with the current Xcode** from the Mac App Store (open it once to install its components). Unity isn't
   needed on the Mac if the export is made on Windows and copied over; copy it as a zip so file permissions survive,
   and if Xcode reports a "Permission denied" build phase script, run `chmod +x` on the scripts it names in the
   exported folder.
3. **App Store Connect** (appstoreconnect.apple.com) > Apps > New App: platform iOS, name, primary language, bundle
   id `com.mindattic.gridgame` (register it first under Certificates, Identifiers & Profiles > Identifiers if it isn't
   offered; enable the **In-App Purchase** capability there), SKU (any unique string, e.g. `gridgame`).
4. **Agreements, Tax and Banking**: accept the Paid Apps agreement and fill in banking and tax; in-app purchases can't
   be sold (or even tested in the sandbox reliably) until it is active.
5. **In-app purchases** (GG-T471): Monetization > In-App Purchases, four **consumables** with product ids
   `diamonds_100`, `diamonds_500`, `diamonds_1000`, `diamonds_2500` and the display titles listed in the Play
   Console section above, prices matching $0.99/$4.99/$9.99/$24.99 tiers (BIBLE: $1/$5/$10/$25), a review screenshot
   each. Add a **sandbox tester** (Users and Access > Sandbox) for purchase tests.

### Signing, archiving and uploading (each release)

1. Export: on Windows `pwsh tools/build.ps1 -Platform iOS`, optionally with `$env:GG_IOS_TEAM_ID = "<Team ID>"` set
   first so the project already has automatic signing for your team. Copy `Build/iOS` to the Mac.
2. Open `Unity-iPhone.xcodeproj` in Xcode. Select the **Unity-iPhone** target > **Signing & Capabilities**: tick
   **Automatically manage signing**, pick your **Team** (skip if `GG_IOS_TEAM_ID` was set). Xcode creates the
   distribution certificate and App Store provisioning profile itself. Do the same Team pick for the
   **UnityFramework** target if Xcode flags it.
3. Still in Signing & Capabilities, add **+ Capability > In-App Purchase** if it isn't listed (Unity IAP usually adds
   it). **iCloud** (Key-value storage only) is added by the export itself for cloud save (GG-T609, "Cloud save:
   iCloud" below); leave Game Center and Push off until a feature needs them.
4. Scheme destination **Any iOS Device (arm64)**, then **Product > Archive**. The Organizer opens with the archive.
5. **Distribute App > App Store Connect > Upload** (keep "Upload your app's symbols" on so crash reports in Xcode's
   Organizer and App Store Connect are readable). Processing takes 5-30 minutes; you get an email.
6. **TestFlight**: the build appears under the app's TestFlight tab. Answer the export-compliance question (the game
   uses only Apple's standard HTTPS: "None of the algorithms mentioned above"), add yourself as an internal tester, and
   install through the TestFlight app on the iPhone. Test a sandbox purchase there.
7. **App Store submission**: App Privacy answers (the "nutrition label": declare what the game actually sends; today
   the error log stays on the device, so revisit this if a crash service is added (GG-T484), and keep it consistent
   with the Play data-safety form, GG-T472), age
   rating questionnaire, screenshots for the largest iPhone size App Store Connect asks for (currently 6.9-inch, 1320 x
   2868 portrait), description, keywords, support and privacy
   policy URLs, then pick the build and **Submit for Review**.

### Cloud save: iCloud (GG-T609)

The game side is built (BIBLE §15.2) and inert until the steps below are done. iOS uses the **iCloud key-value store**
(NSUbiquitousKeyValueStore; no CloudKit, no iCloud Documents): `App/Services/ICloudCloudBackend` (compiled only into an
iOS player) behind the same `CloudSaves` layer as Play Games, the native bridge `Plugins/iOS/GGICloud.mm`, and
`Editor/ICloudEntitlements`, a post-build step that adds the **iCloud > Key-value storage** capability and its
entitlement (`com.apple.developer.ubiquity-kvstore-identifier`) to the exported Xcode project, so no one ticks it by
hand. Without an iCloud account on the phone, with iCloud off for the game, or without the entitlement, the backend
reports itself signed out and cloud save does nothing. The store holds 1 MB for the whole app (every profile's slots
together; a slot is tens of KB packed). Each owner step is its own ticket with the exact checklist:

1. **The owner, Apple developer account (GG-T1127):** Identifiers > App ID `com.mindattic.gridgame` > tick **iCloud**
   with "Include CloudKit support" **off** > Save; regenerate manual provisioning profiles (automatic signing does it
   itself). Nothing to send back: there is no container id.
2. **The owner, Mac (GG-T1128):** export (`pwsh tools/build.ps1 -Platform iOS`), confirm Signing & Capabilities shows
   **iCloud** with **Key-value storage** ticked, build onto an iPhone (this is the first compile of `GGICloud.mm`) and
   read `[Cloud] sign-in ok` in Xcode's console.
3. **The owner, iPhones on one Apple ID (GG-T1129):** upload, Load Game > **Restore from cloud** after a reinstall, the
   newer-copy offer on Continue, the longest-play-time conflict, and nothing happening with iCloud off.

**Target devices (GG-T724, BIBLE §26.3).** iPhone 13 (A15) and newer flagships. App Store Connect can't exclude
models, so the iPhone SE and 12/13 mini are "not designed for" rather than blocked; the iOS 17.0 floor already drops
anything older than the iPhone XS/XR.

## Release player audit (2026-11-25, GG-T1120)

A code read of `origin/main` for what a non-development player (no `DEVELOPMENT_BUILD`, no editor) exposes. No player
was built or launched for it (build-only checks, no Unity); run `pwsh tools/build.ps1 -Release -SkipProse` as part of
the owner's phone check (GG-T1123).

- **Release switches** (`Meta/Features.cs`): Enchanter, Taming and InnRespecRow default off; Crucible, Bounties, Caravan
  and DiamondStore on, as RELEASE_SCOPE decided. `LookPresets` is on in code but RELEASE_SCOPE still says DISABLE (GG-T1124).
- **Launch flags** (`GameApp.cs`, `CommandLine`): in a release player `Has`/`Value` return false/null without
  `-enableDebugOptions`, so `-allfeatures`, `-start`, `-saveRoot`, `-profile`, `-testmode`, `-demo`, `-endless`,
  `-automation`, `-turnMode`, `-skill` and the headless session do nothing. The profile folder is pinned to
  `persistentDataPath/Profiles` (`OpenProfiles`). Unity.Shared's own `--headless`, `-muted` and `-profiles-root`
  readers are not behind that gate (GG-T1125).
- **AutomationServer** (all three files) is inside `#if DEVELOPMENT_BUILD || UNITY_EDITOR`, and so is its one call in
  `GameApp.Awake`; it also needs `-automation PORT`. Not in a release binary.
- **Diamond store**: `DiamondStores.Choose` uses the free `DevDiamondStore` only for the editor or `Debug.isDebugBuild`;
  release is `UnityIapDiamondStore` on Android/iOS (with `GG_IAP`), else `UnconnectedDiamondStore` ("Store not
  connected yet"). No free stub in release.
- **CrashReporter** is installed first in `GameApp.Awake` in every build; telemetry is a no-op in release
  (`TelemetryUnity.IsTelemetryBuild`). Invariant culture is set at `SubsystemRegistration` and again in `Boot`.
- **Build options**: `BuildWindowsRelease` passes `BuildOptions.None` (development only adds `Development | AllowDebugging`).
