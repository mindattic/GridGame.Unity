// Flushes Unity's in-memory file system to IndexedDB so saves survive a page reload (WebGL only).
mergeInto(LibraryManager.library, {
  GridGameSyncFiles: function () {
    if (typeof FS !== "undefined" && FS.syncfs) {
      FS.syncfs(false, function (err) { if (err) console.warn("[GridGame] save sync failed", err); });
    }
  }
});
