# Release builds (GG-T478, GG-T479)

How to produce the store builds. Everything runs from the command line through `tools/build.ps1`, which drives Unity
in batchmode through `Assets/Gaiden/Editor/GaidenBuild.cs`. The Unity Editor must be closed while it runs.

| Command | Output | What it is |
|---|---|---|
| `pwsh tools/build.ps1` | `Build/Windows/GridGameGaiden.exe` | Windows development player (automation endpoint on) |
| `pwsh tools/build.ps1 -Release` | `Build/WindowsRelease/GridGameGaiden.exe` | Windows release player |
| `pwsh tools/build.ps1 -Platform Android` | `Build/AndroidRelease/GridGameGaiden.aab` | **Google Play bundle**: IL2CPP, ARM64 only, release, signed with the upload key |
| `pwsh tools/build.ps1 -Platform Android -Dev` | `Build/Android/GridGameGaiden.apk` | Emulator/test APK: ARM64 + x86_64, development, debug-signed (`tools/androidcheck.py`) |
| `pwsh tools/build.ps1 -Platform WebGL` | `Build/WebGL` | WebGL |

`-Platform` and `-Target` are the same switch. Add `-SkipProse` to build with the committed story text instead of
pulling the latest from Prose first.

## Android (Google Play)

**What the bundle is.** Package `com.mindattic.gridgamegaiden`, min SDK 26 (Android 8), target SDK = the highest
platform installed with Unity (36 with Unity 6000.4.3f1; Play's minimum target rises every August), IL2CPP with
"optimize speed" code generation, ARM64 only (every Play device since 2019; x86_64 is only for the emulator APK),
Unity's default graphics APIs (Vulkan with a GLES3 fallback), portrait.

**Version.** `versionName` is `version.txt`. `versionCode` is the number of commits on the checked-out history
(`git rev-list --count HEAD`): it only grows on main, so a build of a newer commit always outranks the last upload and
nothing has to be remembered or committed. Set `GG_ANDROID_VERSION_CODE=<n>` to force one (for example a hotfix built
from an older commit, which must still be higher than anything uploaded). The build puts the project's own version code
back afterwards, so `ProjectSettings.asset` doesn't change.

**The first build is slow.** Switching the project to Android re-imports every texture as ASTC and IL2CPP compiles the
whole game to C++: expect 20-40 minutes and several GB of RAM the first time, much less after. The next Windows build
switches back (also slower than usual once).

**Size: too big for Play today (GG-T499).** The first build (2026-11-20, version code 587, 10 minutes on a warm
Library) produced a 547 MB bundle. Google Play caps the base module at 200 MB compressed download, so it has to shrink
before upload: the backdrops (1536x3072 ASTC 4x4, about 4.5 MB each) and portraits are most of it. The fix is
Android texture overrides (ASTC 6x6/8x8) and/or Play Asset Delivery install-time packs.

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

**Without a keystore** the bundle is signed with Unity's debug key and the build warns
`DEBUG-signed and Google Play will refuse it`. That bundle is fine for `bundletool` testing on your own phone, not for
upload.

### Testing a bundle on a phone

An `.aab` doesn't install directly. Either upload it to an **internal testing** track in Play Console (fastest real
test, installs from the Play Store on your devices), or use `bundletool`:

```powershell
java -jar bundletool.jar build-apks --bundle=Build\AndroidRelease\GridGameGaiden.aab --output=gg.apks --connected-device `
  --ks=D:\Secrets\gridgame-upload.jks --ks-key-alias=upload
java -jar bundletool.jar install-apks --apks=gg.apks
```

For day-to-day testing the `-Dev` APK is simpler: `adb install -r Build\Android\GridGameGaiden.apk`.

### Google Play Console (the owner)

1. A Google Play developer account (one-time US$25), identity verification.
2. Create the app: name, default language, "Game", free (with in-app purchases).
3. App signing: accept Play App Signing on the first upload; the first bundle's signature registers your upload key.
4. Before production: store listing (icon 512x512, feature graphic 1024x500, phone screenshots), content rating
   questionnaire, data-safety form, privacy policy URL (GG-T472 drafts these), target audience, ads declaration (none).
5. New personal developer accounts must run a **closed test with at least 12 testers for 14 days** before they can
   publish to production.
6. In-app products (`diamonds_100/500/1000/2500`, GG-T471) can only be created after a bundle with the billing
   permission has been uploaded to some track.

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
  `/sdcard/Android/data/com.mindattic.gridgamegaiden/files/Diagnostics/` (`adb pull` it from a test phone).
- **MindAttic.Telemetry** gets the same record as an `exception` event, but only where telemetry runs at all: the
  editor and development builds (it is gated off in release builds and writes to the local disk, `R:\Telemetry`, so
  it can't carry reports from players' phones). That is why it isn't the release sink.
- **Not built: sending managed exceptions home from players' phones.** It needs a service and a privacy-policy line,
  so it waits for the owner's pick. Options, cheapest first: (1) **Unity Cloud Diagnostics** (free tier; link the
  project to a Unity Cloud project id, add `com.unity.services.cloud-diagnostics`); (2) **Firebase Crashlytics** (free;
  needs a Firebase project and its `google-services.json`, adds the Firebase SDK); (3) **Sentry** (free tier, one DSN
  string). Whichever is picked, `CrashReporter.Record` is the one place to forward from, and the Play data-safety form
  (GG-T472) must then declare "crash logs" and "diagnostics", collected, not shared, not linked to the user.

## iOS (GG-T479, needs a Mac — the owner)

Unity on Windows can export the iOS player (the iOSSupport module is installed) but only Xcode on a Mac can compile,
sign and upload it. What the owner needs:

1. A Mac with the current Xcode (from the Mac App Store) and Unity 6000.4.3f1 with iOS Build Support, or just Xcode if
   the Xcode project is exported on Windows and copied over.
2. An Apple Developer Program membership (US$99/year), then in App Store Connect: the app record with bundle id
   `com.mindattic.gridgamegaiden`, and the `diamonds_*` in-app purchases (GG-T471).
3. Export: a `BuildIOS` entry in `GaidenBuild.cs` (IL2CPP is the only backend, ARM64, minimum iOS version, the bundle
   id) writes an Xcode project folder; not written yet because nothing can verify it without a Mac.
4. In Xcode: open `Unity-iPhone.xcodeproj`, Signing & Capabilities > Team = your account, "Automatically manage
   signing", add the In-App Purchase capability, Product > Archive, then Distribute App > App Store Connect >
   Upload. TestFlight makes it installable on your iPhone within minutes of processing.
5. App Store review needs the privacy "nutrition label" answers and screenshots for 6.7" and 6.5" iPhones.
