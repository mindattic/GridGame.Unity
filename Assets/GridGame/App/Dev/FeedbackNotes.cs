using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App.Dev
{
    /// <summary>
    /// GG-T1939: owner walkthrough notes. With Developer options on, a corner Note button (and the battle pause menu's Note row)
    /// captures the screen, asks for a comment and saves persistentDataPath/feedback/&lt;timestamp&gt;/shot.png + note.json.
    /// tools/import_feedback.py turns each folder into a fix ticket.
    /// </summary>
    public static class FeedbackNotes
    {
        public const string FolderName = "feedback";
        private static VisualElement button, editor;

        public static string Root => Path.Combine(Application.persistentDataPath, FolderName);

        /// <summary>Called once when the app's overlay exists: mounts the corner button and keeps it in step with the gate.</summary>
        public static void Install(GameApp app)
        {
            button = Ui.Button("Note", () => Begin(), "btn--small", app.Overlay);
            button.name = "feedback-note";
            button.style.position = Position.Absolute;
            button.style.left = Space.S8;
            button.style.top = Space.S8;
            button.style.opacity = 0.55f;
            button.style.display = DisplayStyle.None;
            app.Ticked += _ =>
            {
                bool want = DevOptions.Enabled && editor == null;
                var d = want ? DisplayStyle.Flex : DisplayStyle.None;
                if (button.style.display != d) button.style.display = d;
            };
        }

        /// <summary>Capture now, then open the comment box. <paramref name="hideWhileEditing"/> (the pause veil) is hidden while the box is up.</summary>
        public static void Begin(VisualElement hideWhileEditing = null)
        {
            if (editor != null || GameApp.I == null) return;
            GameApp.I.StartCoroutine(Capture(hideWhileEditing));
        }

        private static IEnumerator Capture(VisualElement hide)
        {
            if (button != null) button.style.display = DisplayStyle.None;
            yield return null;
            yield return new WaitForEndOfFrame();
            byte[] png = null;
            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); png = tex.EncodeToPNG(); }
            catch (Exception e) { Debug.LogWarning($"[Feedback] screenshot failed: {e.Message}"); }
            finally { if (tex != null) UnityEngine.Object.Destroy(tex); }
            Open(png, hide);
        }

        private static void Open(byte[] png, VisualElement hide)
        {
            var app = GameApp.I;
            var screen = app.Router.Current;
            var summary = Summary(screen);
            bool wasPaused = Tween.Paused;
            Tween.Paused = true;
            if (hide != null) hide.style.display = DisplayStyle.None;
            var veil = editor = Ui.El("fill veil col center", app.Overlay);
            veil.name = "feedback-veil";
            veil.pickingMode = PickingMode.Position;
            var w = Ui.Window("Note", veil);
            w.style.width = Length.Percent(88);
            var field = new TextField { name = "feedback-text", multiline = true, maxLength = 2000 };
            field.style.fontSize = TypeScale.Base;
            field.style.minHeight = Space.S96 * 3;
            field.style.marginTop = field.style.marginBottom = Space.S8;
            var input = field.Q(className: "unity-text-field__input") ?? field.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.backgroundColor = Palette.Ink.WithA(0.9f);
                input.style.color = Palette.White;
                input.style.whiteSpace = WhiteSpace.Normal;
                input.style.paddingLeft = input.style.paddingRight = Space.S16;
            }
            w.Add(field);
            void Done()
            {
                veil.RemoveFromHierarchy();
                editor = null;
                Tween.Paused = wasPaused;
                if (hide != null) hide.style.display = DisplayStyle.Flex;
            }
            var row = Ui.Actions(w);
            Ui.Button("Save", () =>
            {
                var text = (field.value ?? "").Trim();
                if (text.Length == 0) { app.Toast("Type a comment first."); return; }
                string saved = null;
                try { saved = Save(text, png, summary); }
                catch (Exception e) { Debug.LogWarning($"[Feedback] could not save the note: {e.Message}"); }
                Done();
                app.Toast(saved != null ? "Note saved." : "Could not save the note.");
            }, "btn--primary", row);
            Ui.Button("Cancel", Done, null, row);
            field.schedule.Execute(() => field.Focus()).ExecuteLater(200);
        }

        private static JObject Summary(GameScreen screen)
        {
            var o = new JObject { ["screen"] = screen?.Id ?? "none" };
            if (screen is BattleScreen b && b.State != null)
            {
                var s = b.State;
                var units = new JArray();
                foreach (var u in s.Units)
                    if (u.Alive) units.Add($"{u.Team} {u.Name} {u.Hp}/{u.MaxHp}");
                o["battle"] = new JObject
                {
                    ["stage"] = s.Stage.Id,
                    ["wave"] = Math.Min(s.Wave + 1, s.Stage.Waves.Count),
                    ["waves"] = s.Stage.Waves.Count,
                    ["phase"] = s.Phase.ToString(),
                    ["units"] = units,
                };
            }
            return o;
        }

        private static string Save(string comment, byte[] png, JObject summary)
        {
            var now = DateTime.Now;
            var dir = Path.Combine(Root, now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(dir);
            if (png != null) File.WriteAllBytes(Path.Combine(dir, "shot.png"), png);
            var note = new JObject
            {
                ["comment"] = comment,
                ["screen"] = summary["screen"],
                ["version"] = Application.version,
                ["device"] = new JObject { ["width"] = Screen.width, ["height"] = Screen.height, ["dpi"] = Screen.dpi, ["model"] = SystemInfo.deviceModel, ["platform"] = Application.platform.ToString() },
                ["time"] = now.ToString("o"),
            };
            if (summary["battle"] != null) note["battle"] = summary["battle"];
            File.WriteAllText(Path.Combine(dir, "note.json"), note.ToString());
            return dir;
        }
    }
}
