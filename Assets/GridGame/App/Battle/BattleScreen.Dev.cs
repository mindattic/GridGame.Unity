using System;
using GridGame.App.Dev;
using GridGame.Core.Battle;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1938: the pause menu's Developer cheats, shown only while Settings &gt; Developer &gt; Cheats is on. Kill all enemies
    /// and Full heal go through the battle engine (<see cref="DevCheatCommand"/>) and play out like any command, so the
    /// last foe's fall runs the normal victory flow; Win battle is <see cref="DevWin"/>; Fill mana is <see cref="DevGiveMana"/>.
    /// Instant turns (GG-T1967) sets <see cref="Tween.Instant"/> for each playback beat (see Play): only the visuals skip, the
    /// engine's events are the same and the end-of-playback SyncAll draws the true state.
    /// </summary>
    public sealed partial class BattleScreen
    {
        /// <summary>Orbs Fill mana pours in: more than any meter holds, so every spell meter tops out.</summary>
        private const int DevFillOrbs = 999;

        private void DevCheatRows(VisualElement w, Action close)
        {
            Ui.Section("Developer", w).name = "pause-dev";
            void Row(string label, string value, string name, Action act)
            {
                var row = Ui.MenuRow(label, value, () => { close(); act(); }, w);
                row.name = name;
                row.KindIcon(RowKind.Settings);
            }
            void Engine(DevCheat cheat)
            {
                if (!CanCommand()) { App.Toast("Wait for the turn to play out"); return; }
                Commit(new DevCheatCommand { Cheat = cheat });
            }
            Row("Kill all enemies", "", "pause-dev-kill", () => Engine(DevCheat.KillAllEnemies));
            Row("Win battle", "", "pause-dev-win", () => { if (!DevWin()) App.Toast("This battle can't be won from here"); });
            Row("Full heal party", "", "pause-dev-heal", () => Engine(DevCheat.HealParty));
            Row("Fill mana", "", "pause-dev-mana", () => DevGiveMana(DevFillOrbs));
            float speed = Tween.Speed;
            int at = Array.FindIndex(DevOptions.CheatSpeeds, v => Math.Abs(v - speed) < 0.01f);
            float next = DevOptions.CheatSpeeds[(at + 1) % DevOptions.CheatSpeeds.Length];
            Row("Battle speed", $"×{speed:0.#} → ×{next:0.#}", "pause-dev-speed", () =>
            {
                DevOptions.CheatSpeed = next;
                Tween.Speed = DevOptions.BattleSpeed(App.Settings.BattleSpeed);
                OpenMenu();
            });
            bool instant = DevOptions.InstantTurns;
            Row("Instant turns", instant ? "On" : "Off", "pause-dev-instant", () =>
            {
                DevOptions.SetInstantTurns(!instant);
                OpenMenu();
            });
        }
    }
}
