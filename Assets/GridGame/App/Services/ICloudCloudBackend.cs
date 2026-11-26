#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GridGame.Meta;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// iCloud cloud save (§15.2, GG-T609): the iCloud key-value store (NSUbiquitousKeyValueStore) through the small native
    /// plugin <c>Plugins/iOS/GGICloud.mm</c>. Compiled only into an iOS player. Each snapshot is one value under
    /// <see cref="CloudSave.ICloudKey"/> holding <see cref="CloudSave.ICloudPack"/> (the save's own <c>UpdatedUtc</c>, play
    /// time, description and transfer code together, so a slot never syncs half-written). The store is a local cache that
    /// iOS syncs in the background, so every call answers at once, on the main thread.
    /// <para><b>Unavailable</b> (no iCloud account on the phone, iCloud turned off for the game, or a build without the
    /// iCloud key-value entitlement): <see cref="SignedIn"/> is false, sign-in answers false (iOS has no in-game iCloud
    /// prompt; the player signs in under Settings), reads answer null and writes fail, so <see cref="CloudSaves"/> does
    /// nothing and loading goes straight on.</para>
    /// <para><b>Conflicts</b> match Play Games' <c>UseLongestPlaytime</c> on Android: iCloud itself keeps the last write
    /// to reach its server, so when a change from another device replaces a value this device wrote this session, the
    /// longer play time is written back (<see cref="CloudSave.KeepMineOnConflict"/>).</para>
    /// </summary>
    public sealed class ICloudCloudBackend : ICloudSaveBackend
    {
        [DllImport("__Internal")] private static extern void GGICloud_Start(string listenerObject, string listenerMethod);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool GGICloud_Available();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool GGICloud_Synchronize();
        [DllImport("__Internal")] private static extern string GGICloud_Get(string key);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool GGICloud_Set(string key, string value);

        /// <summary>NSUbiquitousKeyValueStoreChangeReasonKey values.</summary>
        private const int ServerChange = 0, InitialSyncChange = 1, QuotaViolationChange = 2, AccountChange = 3;

        /// <summary>What this device wrote this session, by key: the copy a conflict may write back.</summary>
        private readonly Dictionary<string, string> written = new Dictionary<string, string>();
        private readonly bool broken;

        public ICloudCloudBackend()
        {
            var go = new GameObject(ICloudListener.ObjectName); // a plain active object: UnitySendMessage finds it by name
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<ICloudListener>().Changed = OnExternalChange;
            try { GGICloud_Start(ICloudListener.ObjectName, nameof(ICloudListener.OnICloudChanged)); }
            catch (Exception e) { Debug.LogWarning("[Cloud] iCloud plugin missing: " + e.Message); broken = true; }
        }

        public bool SignedIn
        {
            get
            {
                if (broken) return false;
                try { return GGICloud_Available(); }
                catch (Exception) { return false; }
            }
        }

        /// <summary>iOS has no in-game iCloud sign-in: this asks iCloud to sync now and reports whether it is usable.
        /// <paramref name="interactive"/> only adds a log line pointing the player at Settings.</summary>
        public void SignIn(bool interactive, Action<bool> done)
        {
            bool ok = SignedIn;
            if (ok) { try { GGICloud_Synchronize(); } catch (Exception) { } }
            else if (interactive) Debug.Log("[Cloud] iCloud unavailable: sign into iCloud and allow GridGame to use iCloud in Settings");
            done(ok);
        }

        private static string Get(string key)
        {
            try { return GGICloud_Get(key); }
            catch (Exception e) { Debug.LogWarning("[Cloud] iCloud read failed: " + e.Message); return null; }
        }

        private static bool Set(string key, string value)
        {
            try { return GGICloud_Set(key, value); }
            catch (Exception e) { Debug.LogWarning("[Cloud] iCloud write failed: " + e.Message); return false; }
        }

        public void ReadInfo(string name, Action<CloudSnapshotInfo> done)
        {
            if (!SignedIn) { done(null); return; }
            var raw = Get(CloudSave.ICloudKey(name));
            if (CloudSave.ICloudTryUnpack(name, raw, out var info, out _)) { done(info); return; }
            if (!string.IsNullOrEmpty(raw)) Debug.LogWarning($"[Cloud] iCloud value for {name} is unreadable");
            done(new CloudSnapshotInfo { Name = name, HasData = false }); // never written (or unreadable): nothing to offer
        }

        public void Read(string name, Action<byte[]> done)
        {
            if (!SignedIn) { done(null); return; }
            done(CloudSave.ICloudTryUnpack(name, Get(CloudSave.ICloudKey(name)), out _, out var body) ? body : null);
        }

        public void Write(string name, byte[] data, string description, TimeSpan playTime, DateTime savedUtc, Action<bool> done)
        {
            if (!SignedIn || data == null) { done(false); return; }
            var key = CloudSave.ICloudKey(name);
            var value = CloudSave.ICloudPack(data, savedUtc, playTime.TotalSeconds, description);
            // Key plus value (UTF-8) is what iCloud charges against the 1 MB store.
            if (System.Text.Encoding.UTF8.GetByteCount(value) + key.Length > CloudSave.MaxICloudStoreBytes)
            {
                Debug.LogError($"[Cloud] {name} is over the iCloud key-value limit");
                done(false);
                return;
            }
            bool ok = Set(key, value);
            if (ok) written[key] = value;
            done(ok);
        }

        /// <summary>The native observer of NSUbiquitousKeyValueStoreDidChangeExternallyNotification: "reason|key\tkey…".</summary>
        private void OnExternalChange(string message)
        {
            int bar = message == null ? -1 : message.IndexOf('|');
            if (bar < 0 || !int.TryParse(message.Substring(0, bar), out var reason)) return;
            var keys = message.Substring(bar + 1).Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            switch (reason)
            {
                case AccountChange:
                    written.Clear(); // a different iCloud account: nothing this device wrote belongs to it
                    Debug.Log("[Cloud] iCloud account changed");
                    return;
                case QuotaViolationChange:
                    Debug.LogWarning("[Cloud] iCloud key-value store is over its 1 MB quota; uploads are not syncing");
                    return;
                case ServerChange:
                case InitialSyncChange:
                    foreach (var key in keys) Resolve(key);
                    return;
            }
        }

        /// <summary>Another device's write replaced <paramref name="key"/>: the longer play time stays in the cloud.</summary>
        private void Resolve(string key)
        {
            if (!written.TryGetValue(key, out var mine)) return; // not written here this session: the load-time offer covers it
            var theirs = Get(key);
            if (theirs == mine) return;
            CloudSave.ICloudTryUnpack(key, mine, out var mineInfo, out _);
            CloudSave.ICloudTryUnpack(key, theirs, out var theirInfo, out _);
            if (!CloudSave.KeepMineOnConflict(mineInfo, theirInfo))
            {
                written.Remove(key); // the other device's copy stands; this device's save is offered against it on load
                return;
            }
            bool ok = Set(key, mine);
            Debug.Log($"[Cloud] {key}: iCloud conflict, kept this device's longer play time ({(ok ? "rewritten" : "rewrite failed")})");
        }
    }

    /// <summary>Receives the native plugin's <c>UnitySendMessage</c> on the main thread.</summary>
    internal sealed class ICloudListener : MonoBehaviour
    {
        public const string ObjectName = "GGICloudListener";
        public Action<string> Changed;

        public void OnICloudChanged(string message)
        {
            try { Changed?.Invoke(message); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
#endif
