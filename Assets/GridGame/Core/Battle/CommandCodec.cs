using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Commands as plain JSON and back (multiplayer-ready rule 6, GG-LAW-13): the seat, the hero id and the picked tiles,
    /// nothing else. What a replay stores and what a networked session would send. Round-trips equal:
    /// <c>ToJson(FromJson(ToJson(c))) == ToJson(c)</c>.
    /// </summary>
    public static class CommandCodec
    {
        public static string ToJson(BattleCommand cmd) => ToJObject(cmd).ToString(Formatting.None);

        public static JObject ToJObject(BattleCommand cmd)
        {
            if (cmd == null) throw new ArgumentNullException(nameof(cmd));
            JObject o;
            switch (cmd)
            {
                case MoveCommand m:
                    var path = new JArray();
                    foreach (var c in m.Path) path.Add(new JArray(c.X, c.Y));
                    // "held" (GG-T188): a forced drop at the turn window ends the turn even when nothing moved, so a
                    // log without it replays a different battle.
                    o = new JObject { ["type"] = "move", ["hero"] = m.HeroId, ["path"] = path, ["timeLeft"] = m.TimeLeftSeconds, ["held"] = m.HeldSeconds };
                    break;
                case UseSlotCommand u:
                    o = new JObject { ["type"] = "use", ["hero"] = u.HeroId, ["slot"] = u.Slot, ["target"] = new JArray(u.Target.X, u.Target.Y) };
                    WriteTargets(o, u);
                    break;
                case ShieldCommand sh:
                    o = new JObject { ["type"] = "guard", ["held"] = sh.HeldSeconds }; // GG-T189: Guard's mana charge depends on it
                    break;
                case TalkCommand t:
                    o = new JObject { ["type"] = "talk", ["hero"] = t.HeroId, ["target"] = t.TargetId };
                    break;
                case TutorialSceneCommand sc:
                    o = new JObject { ["type"] = "scene", ["scene"] = sc.Scene };
                    break;
                case DevCheatCommand dc:
                    o = new JObject { ["type"] = "dev", ["cheat"] = dc.Cheat.ToString() };
                    break;
                default:
                    throw new NotSupportedException("No codec for command " + cmd.GetType().Name);
            }
            o["player"] = cmd.Player.Seat;
            return o;
        }

        public static BattleCommand FromJson(string json) => FromJObject(JObject.Parse(json));

        public static BattleCommand FromJObject(JObject o)
        {
            BattleCommand cmd;
            switch ((string)o["type"])
            {
                case "move":
                    var path = new List<Coord>();
                    foreach (var t in (JArray)o["path"]) path.Add(new Coord((int)t[0], (int)t[1]));
                    cmd = new MoveCommand { HeroId = (int)o["hero"], Path = path, TimeLeftSeconds = (float?)o["timeLeft"] ?? 0f, HeldSeconds = (float?)o["held"] ?? 0f };
                    break;
                case "use":
                    var target = (JArray)o["target"];
                    cmd = new UseSlotCommand { HeroId = (int)o["hero"], Slot = (int)o["slot"], Target = new Coord((int)target[0], (int)target[1]), Targets = ReadTargets(o) };
                    break;
                case "guard":
                    cmd = new ShieldCommand { HeldSeconds = (float?)o["held"] ?? 0f };
                    break;
                case "talk":
                    cmd = new TalkCommand { HeroId = (int)o["hero"], TargetId = (int)o["target"] };
                    break;
                case "scene":
                    cmd = new TutorialSceneCommand { Scene = (string)o["scene"] };
                    break;
                case "dev":
                    cmd = new DevCheatCommand { Cheat = (DevCheat)Enum.Parse(typeof(DevCheat), (string)o["cheat"]) };
                    break;
                default:
                    throw new NotSupportedException("Unknown command type " + (string)o["type"]);
            }
            cmd.Player = new PlayerId((int?)o["player"] ?? 0);
            return cmd;
        }

        /// <summary>GG-T2141: a PickMulti's picks under "targets", written only when there are any, so a single pick encodes
        /// as it always did and a log from before multi-pick (no key) reads as a single pick.</summary>
        public static void WriteTargets(JObject o, UseSlotCommand u)
        {
            if (u.Targets.Count == 0) return;
            var arr = new JArray();
            foreach (var c in u.Targets) arr.Add(new JArray(c.X, c.Y));
            o["targets"] = arr;
        }

        public static List<Coord> ReadTargets(JObject o)
        {
            var list = new List<Coord>();
            if (o["targets"] is JArray arr)
                foreach (var t in arr) list.Add(new Coord((int)t[0], (int)t[1]));
            return list;
        }

        // ── Turn mode (§2.13, GG-T767) ───────────────────────────────────────────
        // A battle's mode is part of its setup, not of any one command, but a command log replays a different battle under
        // a different mode, so whatever stores the log (BattleRecorder's document, a replay header) stores the mode with it.

        /// <summary>The key a battle record or replay header keeps its <see cref="TurnMode"/> under.</summary>
        public const string TurnModeKey = "turnMode";

        /// <summary>The mode by name (stable when values are added), e.g. "PartyWindow".</summary>
        public static string EncodeTurnMode(TurnMode mode) => mode.ToString();

        /// <summary>The mode stored under <see cref="TurnModeKey"/> in <paramref name="o"/>. A record from before GG-T767
        /// (no key) is <see cref="TurnMode.PartyWindow"/>, the only rule that existed; an unknown name throws.</summary>
        public static TurnMode ReadTurnMode(JObject o)
        {
            var name = (string)o?[TurnModeKey];
            if (string.IsNullOrEmpty(name)) return TurnMode.PartyWindow;
            if (Enum.TryParse(name, false, out TurnMode mode) && Enum.IsDefined(typeof(TurnMode), mode) && !int.TryParse(name, out _)) return mode;
            throw new NotSupportedException("Unknown turn mode " + name);
        }

        /// <summary>Stores <paramref name="mode"/> in <paramref name="o"/> under <see cref="TurnModeKey"/>.</summary>
        public static JObject WriteTurnMode(JObject o, TurnMode mode)
        {
            o[TurnModeKey] = EncodeTurnMode(mode);
            return o;
        }
    }
}
