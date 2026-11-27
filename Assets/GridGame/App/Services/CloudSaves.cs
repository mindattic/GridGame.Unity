using System;
using System.Collections;
using System.Collections.Generic;
using GridGame.Meta;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>A platform's cloud save store: it only moves snapshot bytes and metadata; every decision is
    /// <see cref="CloudSave"/>'s. Callbacks arrive on the main thread, exactly once each.</summary>
    public interface ICloudSaveBackend
    {
        bool SignedIn { get; }
        /// <summary>Signs in; <c>true</c> when signed in. <paramref name="interactive"/> may show the platform's own
        /// sign-in prompt (a player tapping Restore from cloud); otherwise it is silent (boot).</summary>
        void SignIn(bool interactive, Action<bool> done);
        /// <summary>The snapshot's metadata, or null when it can't be reached (offline, signed out).</summary>
        void ReadInfo(string name, Action<CloudSnapshotInfo> done);
        /// <summary>The snapshot's bytes, or null on any failure.</summary>
        void Read(string name, Action<byte[]> done);
        /// <summary>Uploads <paramref name="data"/>; <paramref name="savedUtc"/> is the save's own <c>UpdatedUtc</c>, kept
        /// by a store that holds its own metadata (iCloud, GG-T609) so the same save never reads back as newer.</summary>
        void Write(string name, byte[] data, string description, TimeSpan playTime, DateTime savedUtc, Action<bool> done);
        /// <summary>Removes the snapshot from the cloud (GG-T1544, Delete all game data); <c>true</c> when it is gone or was
        /// never there.</summary>
        void Delete(string name, Action<bool> done);
    }

    /// <summary>
    /// Cloud save (§15.2, GG-T608): after each <c>Persist</c> the slot is uploaded (debounced to one upload a minute, and
    /// flushed on pause/quit); before a save is played, a newer cloud copy is offered — "Use the cloud save from &lt;date&gt;,
    /// &lt;play time&gt;?" — never merged; on a new device, Load Game's <b>Restore from cloud</b> fills empty slots from the
    /// cloud. With no backend (every platform but an Android build with Play Games compiled in and an iOS player, see
    /// <see cref="PlatformBackend"/>), or with iCloud unavailable on the iPhone, every call is a no-op and loading goes
    /// straight on.
    /// </summary>
    public sealed class CloudSaves
    {
        /// <summary>How long a load waits on the cloud before playing the local save anyway.</summary>
        public const float LoadWaitSeconds = 8f;
        /// <summary>How long Restore from cloud waits (sign-in sheet included) before it reports what it has.</summary>
        public const float RestoreWaitSeconds = 45f;

        private readonly ICloudSaveBackend backend;
        private readonly CloudUploadThrottle throttle = new CloudUploadThrottle();
        private string pendingName;
        private SaveData pending;
        private bool uploading;

        public CloudSaves(ICloudSaveBackend backend) { this.backend = backend; }

        public static CloudSaves Create() => new CloudSaves(PlatformBackend());

        /// <summary>Play Games Saved Games in an Android player built with the <c>GG_PLAY_GAMES</c> define (set once the
        /// Play Games plugin and the Play Console app id are in, GG-T608); the iCloud key-value store in an iOS player
        /// (GG-T609; it reports itself signed out, so every call is a no-op, until the iCloud entitlement is in and the
        /// player is signed into iCloud); nothing anywhere else.</summary>
        private static ICloudSaveBackend PlatformBackend()
        {
#if GG_PLAY_GAMES && UNITY_ANDROID && !UNITY_EDITOR
            return new PlayGamesCloudBackend();
#elif UNITY_IOS && !UNITY_EDITOR
            return new ICloudCloudBackend();
#else
            return null;
#endif
        }

        /// <summary>A platform cloud exists in this build (signed in or not).</summary>
        public bool Available => backend != null;
        public bool Active => backend != null && backend.SignedIn;

        /// <summary>Silent sign-in at boot; uploads anything that waited for it.</summary>
        public void Start()
        {
            if (backend == null) return;
            backend.SignIn(false, ok =>
            {
                Debug.Log($"[Cloud] sign-in {(ok ? "ok" : "unavailable")}");
                if (ok) Pump(true);
            });
        }

        /// <summary>The slot was written to disk (<c>GameApp.Persist</c>). <paramref name="flush"/> (pause, quit) uploads
        /// now instead of waiting out the minute.</summary>
        public void Saved(string profile, int slot, SaveData s, bool flush = false)
        {
            if (backend == null || s == null || slot < 0) return;
            var name = CloudSave.SnapshotName(profile, slot);
            // Another slot or profile still waiting: it goes up now, before this one takes its place.
            if (pending != null && pendingName != name && throttle.Dirty) Upload(pendingName, pending);
            pendingName = name;
            pending = s;
            throttle.MarkDirty();
            Pump(flush);
        }

        /// <summary>Pause or quit: whatever is waiting goes up now.</summary>
        public void Flush() => Pump(true);

        private void Pump(bool flush)
        {
            if (!Active || pending == null || uploading) return;
            var now = DateTime.UtcNow;
            if (!throttle.Due(now, flush)) return;
            throttle.Started(now);
            Upload(pendingName, pending);
        }

        private void Upload(string name, SaveData s)
        {
            if (!Active) { throttle.Failed(); return; }
            byte[] data;
            try { data = CloudSave.Encode(s); }
            catch (Exception e) { Debug.LogError("[Cloud] couldn't pack the save: " + e.Message); return; }
            if (data.Length > CloudSave.MaxSnapshotBytes) { Debug.LogError($"[Cloud] {name} is {data.Length} bytes, over the snapshot limit"); return; }
            uploading = true;
            backend.Write(name, data, CloudSave.Description(s), TimeSpan.FromSeconds(Math.Max(0, s.PlaySeconds)), s.UpdatedUtc, ok =>
            {
                uploading = false;
                if (ok) Debug.Log($"[Cloud] uploaded {name} ({data.Length} bytes)");
                else { Debug.LogWarning($"[Cloud] upload of {name} failed; retrying at the next save"); if (name == pendingName) throttle.Failed(); }
            });
        }

        /// <summary>Before slot <paramref name="slot"/> is played: a newer cloud copy is offered, and <paramref name="play"/>
        /// gets the save chosen (the cloud one is written to the slot first). Without a signed-in cloud, or when the cloud
        /// doesn't answer within <see cref="LoadWaitSeconds"/>, <paramref name="play"/> gets <paramref name="local"/> at once.</summary>
        public void BeforeLoad(GameApp app, int slot, SaveData local, Action<SaveData> play)
        {
            if (!Active || app?.Profile == null) { play(local); return; }
            bool settled = false;
            void Settle(SaveData s) { if (settled) return; settled = true; play(s); }
            app.StartCoroutine(Timeout(() => { if (!settled) Debug.LogWarning("[Cloud] no answer; playing the local save"); Settle(local); }, LoadWaitSeconds));

            var name = CloudSave.SnapshotName(app.Profile.Name, slot);
            backend.ReadInfo(name, info =>
            {
                if (settled) return;
                var choice = CloudSave.Decide(local, info);
                Debug.Log($"[Cloud] {name}: {choice}");
                if (choice != CloudChoice.OfferCloud) { Settle(local); return; }
                backend.Read(name, data =>
                {
                    if (settled) return;
                    if (!CloudSave.TryDecode(data, app.Content, out var cloud, out var why, app.Profile?.Name))
                    {
                        Debug.LogWarning($"[Cloud] {name} unreadable: {why}");
                        Settle(local);
                        return;
                    }
                    if (SharesPurchasesElsewhere(app, cloud, slot))
                    {
                        Debug.LogWarning($"[Cloud] {name} holds purchases already in another slot on this device; not offered");
                        Settle(local);
                        return;
                    }
                    settled = true; // the player answers now; the timeout no longer applies
                    Confirm.Show(CloudSave.OfferQuestion(info, info.UpdatedUtc.ToLocalTime(), local), "Use cloud save",
                        () =>
                        {
                            if (!app.Saves.Write(slot, cloud)) app.Toast("Couldn't save!");
                            play(cloud);
                        },
                        () => play(local));
                });
            });
        }

        /// <summary>GG-T469, the same rule as Import save (GG-T607): a cloud save whose purchase ledger shares a store
        /// transaction id with a save file in any <i>other</i> slot on this device (every profile, and each file's .bak) is
        /// never written here, or one purchase would pay its Shards in two slots. That happens when a transfer code was
        /// imported on another device and uploaded from a different slot or profile. Slot <paramref name="slot"/>'s own
        /// file is left out: replacing a slot's save with its own newer copy is the point.</summary>
        private static bool SharesPurchasesElsewhere(GameApp app, SaveData cloud, int slot)
        {
            var own = System.IO.Path.GetFullPath(SaveFiles.PathFor(app.Saves.Root, slot));
            var others = new List<string>();
            foreach (var p in SaveTransferUi.AllSlotPaths(app))
                if (!string.Equals(System.IO.Path.GetFullPath(p), own, StringComparison.OrdinalIgnoreCase)) others.Add(p);
            return SaveTransfer.SharesPurchasesWith(cloud, others);
        }

        private static IEnumerator Timeout(Action onTimeout, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            onTimeout();
        }

        /// <summary>Delete all game data (GG-T1544): every slot of every profile in <paramref name="profiles"/> is removed
        /// from the cloud, and nothing waiting is uploaded afterwards. <paramref name="done"/> gets how many deletes failed
        /// (0 also when there is no signed-in cloud, so nothing is held there).</summary>
        public void DeleteAll(IEnumerable<string> profiles, Action<int> done)
        {
            pending = null; pendingName = null;
            if (!Active || profiles == null) { done(0); return; }
            var names = new Queue<string>();
            foreach (var p in profiles)
                for (int i = 0; i < SaveStore.Slots; i++) names.Enqueue(CloudSave.SnapshotName(p, i));
            int failed = 0;
            void Next()
            {
                if (names.Count == 0) { done(failed); return; }
                var name = names.Dequeue();
                backend.Delete(name, ok =>
                {
                    if (!ok) { failed++; Debug.LogWarning($"[Cloud] couldn't delete {name}"); }
                    Next();
                });
            }
            Next();
        }

        /// <summary>A new device: every empty slot of the active profile that has a save in the cloud gets it. A slot
        /// with a save on this device is never touched here. <paramref name="done"/> gets how many were restored.</summary>
        public void RestoreEmptySlots(GameApp app, Action<int> done)
        {
            if (backend == null || app?.Profile == null || app.Saves == null) { done(0); return; }
            // No network or a captive portal: sign-in and reads may never answer, so the screen gets an answer anyway.
            bool answered = false;
            int restoredSoFar = 0;
            var final = done;
            done = n => { if (answered) return; answered = true; final(n); };
            app.StartCoroutine(Timeout(() => { if (!answered) Debug.LogWarning("[Cloud] restore: no answer"); done(restoredSoFar); }, RestoreWaitSeconds));
            if (!backend.SignedIn)
            {
                backend.SignIn(true, ok => { if (ok) RestoreEmptySlots(app, done); else done(0); });
                return;
            }
            var empty = new Queue<int>();
            for (int i = 0; i < SaveStore.Slots; i++) if (!app.Saves.Exists(i)) empty.Enqueue(i);
            int restored = 0;
            void Next()
            {
                if (empty.Count == 0) { done(restored); return; }
                int slot = empty.Dequeue();
                var name = CloudSave.SnapshotName(app.Profile.Name, slot);
                backend.ReadInfo(name, info =>
                {
                    if (info == null || !info.HasData) { Next(); return; }
                    backend.Read(name, data =>
                    {
                        if (CloudSave.TryDecode(data, app.Content, out var save, out var why, app.Profile?.Name))
                        {
                            if (SharesPurchasesElsewhere(app, save, slot))
                                Debug.LogWarning($"[Cloud] {name} holds purchases already in another slot on this device; not restored");
                            else if (app.Saves.Write(slot, save)) restoredSoFar = ++restored;
                        }
                        else Debug.LogWarning($"[Cloud] {name} unreadable: {why}");
                        Next();
                    });
                });
            }
            Next();
        }
    }
}
