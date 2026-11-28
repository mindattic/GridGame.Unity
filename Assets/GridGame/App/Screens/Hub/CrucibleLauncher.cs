using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>The one way into a Crucible run (§22.10): generates the save's next gauntlet and opens its battle.</summary>
    public static class CrucibleLauncher
    {
        public static void Launch(bool replaceCurrent)
        {
            var app = GameApp.I;
            if (!Crucible.CanLaunch(app.Content, app.Save, out var why)) { app.Toast(why); app.Audio.Sfx("ui.error"); return; }
            if (!RestedGate.Check(() => Launch(replaceCurrent))) return;
            RestedGate.Spend();
            var stage = Crucible.Prepare(app.Content, app.Save);
            app.Audio.Sfx("battle.crucible_enter");
            GameScreen next = new BattleScreen(stage.Id);
            if (replaceCurrent) app.Router.Replace(next); else app.Router.Push(next);
        }
    }
}
