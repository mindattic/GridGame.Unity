// GG-T1783: the WebGL name-tap bridge (development builds; AutomationServer.WebGL.cs). Puts
// window.gridgameAutomation.click(name[, timeoutMs]) on the page: it SendMessages AutomationServer.WebClick, which
// taps the UI Toolkit element of that name the way the HTTP automation `click` op does, and resolves with its reply
// ({ ok, target } or { ok: false, error }).
mergeInto(LibraryManager.library, {
  GGWebBridge_Register: function (objectNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    var bridge = window.gridgameAutomation || { pending: {}, seq: 0 };
    bridge.objectName = objectName;
    bridge.click = function (name, timeoutMs) {
      return new Promise(function (resolve) {
        var id = String(++bridge.seq);
        bridge.pending[id] = resolve;
        setTimeout(function () {
          if (bridge.pending[id]) { delete bridge.pending[id]; resolve({ ok: false, error: "no reply within " + (timeoutMs || 10000) + " ms" }); }
        }, timeoutMs || 10000);
        var send = Module["SendMessage"] || SendMessage;
        send(objectName, "WebClick", id + "|" + name);
      });
    };
    window.gridgameAutomation = bridge;
  },

  GGWebBridge_Reply: function (idPtr, jsonPtr) {
    var id = UTF8ToString(idPtr);
    var json = UTF8ToString(jsonPtr);
    var bridge = window.gridgameAutomation;
    var resolve = bridge && bridge.pending[id];
    if (!resolve) return;
    delete bridge.pending[id];
    var reply;
    try { reply = JSON.parse(json); } catch (e) { reply = { ok: false, error: "bad reply: " + json }; }
    resolve(reply);
  }
});
