# GridGame Gaiden: Privacy Policy

> **DRAFT for the owner's review (GG-T472, 2026-11-20). Not legal advice.** Written from what the code does at the
> commit that adds it. Fill in the bracketed fields, have it reviewed, then host it at a public URL (both stores need
> one). Re-check it whenever a package, SDK or network feature is added.

**Effective date:** [date of first release]
**Developer:** MindAttic ("we", "us")
**Contact:** [privacy contact email]
**App:** GridGame Gaiden (Android package `com.mindattic.gridgamegaiden`; iOS bundle id [to be set])

## The short version

GridGame Gaiden is a single-player game. It has **no accounts, no ads, no analytics SDK and no online play.** Your
progress is saved on your device. We don't collect your name, email, location, contacts, photos or device
identifiers. When you buy Mana Shards, Google Play or the Apple App Store handles the payment; we never see your card
or bank details.

## What stays on your device

- **Save files and settings.** Your save slots (heroes, items, progress, Mana Shards, and the list of store transaction
  ids already granted, so a purchase is never paid out twice), your player-profile name and your settings are written
  to the app's private storage on your device. They are not sent to us.
- **Error log.** If the game hits an unexpected error, it writes a short record to a log file in its private storage:
  the time, app version, platform, OS version, device model and the error's technical text, with folder paths and your
  profile name removed. It never contains your save, a device id or an account. It stays on the device; the release
  game doesn't upload it. (Separately, Google Play may collect crash and "app not responding" reports through Android
  vitals, and Apple may share crash logs if you opted in to share analytics with developers, under their policies.)
- **Device backups.** Android's Auto Backup and iCloud/iTunes device backups may copy the app's private storage as part
  of your own device backup, under your Google or Apple account and their policies. We cannot read those backups. This
  is also how a reinstall can bring your progress (and purchased Mana Shards) back.

## In-app purchases

Mana Shards can be bought with real money through **Google Play Billing** (Android) or **Apple's App Store / StoreKit**
(iOS), using Unity's In-App Purchasing library.

- The store collects and processes your payment information under its own privacy policy
  ([Google](https://policies.google.com/privacy), [Apple](https://www.apple.com/legal/privacy/)). We receive only
  what the store gives the app to complete the order: the product bought, a transaction id / purchase token and a
  receipt. The app uses them on your device to add the Shards once and records the transaction id in your save.
- **Unity In-App Purchasing** may send purchase-related diagnostics (for example, that a purchase of a product
  succeeded, its price and currency, the store, and app and device attributes) to Unity Technologies, depending on the
  Unity project's Developer Data settings. See Unity's privacy policy: <https://unity.com/legal/privacy-policy>.
  [Owner: confirm the project's Unity Dashboard > Project Settings > Developer Data choices before release and update
  this paragraph to match.]

## What the release game does NOT do

- No advertising, ad SDKs or advertising identifiers.
- No analytics or crash-reporting SDK. (Unity Analytics, Cloud Diagnostics crash reporting and performance reporting are
  turned off in the project.)
- No sign-in, social features, chat or user-generated content shared with others.
- No location, camera, microphone, contacts or photo access.
- Our own developer tools are compiled out of, or switched off in, release builds: the automation server exists only in
  development builds; MindAttic.Telemetry (battle logs) and MindAttic.Capture (screen recording) only run in the editor
  and development builds, write only to the developer's own computer, and never transmit anything.

## Children

The game is not directed at children under 13 [owner: confirm the target age group you declare in Play Console's
Target audience and App Store Connect]. We don't knowingly collect personal information from anyone, including
children. Purchases on a child's device are subject to the store's parental controls (Google Play's purchase
approval / Family Link, Apple's Ask to Buy); a purchase waiting for approval shows as pending in the game and arrives
when approved.

## Your choices and rights

Because we don't collect personal data, there is nothing for us to access, correct, export or delete on our side.
You can delete all game data by deleting save slots in the game or uninstalling the app (and removing it from your
device backups). Purchase history is kept by Google or Apple; refunds and purchase records are handled through them.
If you have questions, or believe we hold data about you, contact [privacy contact email].

## Changes

If the game starts collecting anything (for example, cloud saves or analytics), we will update this policy and the
store listings before that version ships, and change the effective date above.
