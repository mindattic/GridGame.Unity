using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Battle recorder (bible §22.6, owner direction 2026-10-11: "record everything so you can learn how to balance and
    /// synergize, keep the records of all moves as JSON so that we can build ML around it in the future").
    /// One JSON document per battle: who played (player / bot), the setup, the full state before every command, the
    /// command, every event it produced, and the outcome. Pure C# so the game, the bot soak and tests share it.
    /// Schema is versioned; add fields, never repurpose them.
    /// </summary>
    public sealed class BattleRecorder
    {
        /// <summary>v2 (2026-10-12): added optional per-turn "skillLevel"/"perfectCommand" (BotSkillProfile demo mode).
        /// v3 (2026-10-19, GG-T98): PincerFormed events carry formation-time footprints ("Attacker1At", "Attacker2At",
        /// "OpponentsAt", "SupportersAt"); WaveStarted/ReinforcementsArrived carry "SpawnedAt".
        /// v4 (2026-11-23, GG-T767): top-level "turnMode" (§2.13; <see cref="CommandCodec.ReadTurnMode"/> reads a record
        /// without it as PartyWindow).
        /// v5 (2026-11-27, GG-T1812): top-level "rulesVersion"; "startOrbs" becomes "startMana" (points granted to every
        /// meter before wave 1), and every state's "bank", "bankCapacity" and "charge" become "meters" (§3.1.0).
        /// v6 (2026-11-27, GG-T1844): OrbMinted, OrbsSpent and ManaCharged events are gone; ManaGained carries "Kind"
        /// (Grant / Earned / Siphon), "Orbs", "Participants" and "Chain", and a grant that found every meter full is a
        /// ManaGained with Amount 0 and no meters.
        /// v7 (2026-11-28, GG-T1926): one party mana pool (§3.1.0). Every state's "meters" becomes "mana"
        /// ({"points", "capacity"}); "startMana" is the points in the pool before wave 1; ManaGained carries "Before",
        /// "After", "Capacity" and "Wasted" and no longer "Meters" or "Readied".</summary>
        public const int SchemaVersion = 7;

        /// <summary>The battle rules a record was made under. 3 (GG-T1926): one party mana pool (§3.1.0). 2 (GG-T1812):
        /// per-spell mana meters. A record without it was made under the shared orb bank (1).</summary>
        public const int RulesVersion = 3;
        public const string RulesVersionKey = "rulesVersion";

        /// <summary>A record made under older rules: shown as it was, never re-simulated (§3.1.0).</summary>
        public static bool IsLegacy(JObject record) => ((int?)record?[RulesVersionKey] ?? 1) < RulesVersion;

        private static readonly JsonSerializer Json = JsonSerializer.Create(new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });

        private readonly JObject root;
        private readonly JArray turns = new JArray();
        public int Turns => turns.Count;

        /// <param name="source">"player", "bot:greedy", "bot:casual" … — who made the decisions.</param>
        public BattleRecorder(BattleState s, BattleSetup setup, string source, List<BattleEvent> openingEvents = null)
        {
            root = new JObject
            {
                ["schema"] = SchemaVersion,
                [RulesVersionKey] = RulesVersion,
                ["recordedAtUtc"] = DateTime.UtcNow.ToString("o"),
                ["source"] = source,
                ["stage"] = s.Stage?.Id,
                ["stageName"] = s.Stage?.Name,
                ["waves"] = s.Stage?.Waves.Count ?? 0,
                ["seed"] = setup?.Seed ?? 0,
                ["recommendedLevel"] = setup?.RecommendedLevel ?? 0,
                ["newGamePlus"] = setup?.NewGamePlus ?? 0,
                ["difficulty"] = setup?.Difficulty ?? 0, // the dial (GG-T309); 0 = unscaled
                [CommandCodec.TurnModeKey] = CommandCodec.EncodeTurnMode(s.TurnMode), // §2.13 (GG-T767): a replay reproduces it
                ["startMana"] = setup?.StartMana ?? 0,
                ["party"] = new JArray(s.Units.Where(u => u.IsHero).Select(u => new JObject
                {
                    ["id"] = u.Id, ["class"] = u.Def.Id, ["role"] = u.Def.Role, ["level"] = u.Level, ["maxHp"] = u.MaxHp,
                    ["stats"] = Stats(u.Effective), ["weapon"] = u.WeaponId,
                    ["armor"] = new JObject(u.Gear.Select(kv => new JProperty(kv.Key.ToString(), kv.Value.ItemId))),
                    ["bar"] = Bar(u), ["guest"] = u.IsGuest,
                })),
                ["opening"] = new JObject { ["state"] = Snapshot(s), ["events"] = Events(openingEvents) },
                ["turns"] = turns,
            };
        }

        /// <summary>Apply a command through the engine and record it (state before, command, events, state after).</summary>
        public List<BattleEvent> Apply(BattleState s, BattleCommand cmd)
        {
            var before = Snapshot(s);
            var events = BattleEngine.Apply(s, cmd);
            Add(before, cmd, events, s);
            return events;
        }

        /// <summary>
        /// Record a command that was applied elsewhere (the battle screen applies it itself).
        /// <paramref name="perfectCmd"/>, <paramref name="skillLevel"/>: demo/capture only (owner request,
        /// BotSkillProfile) — when a simulated imperfect player is driving, the strict-best move the same
        /// decision point would have picked at full skill, recorded alongside what was actually played, so ML
        /// can learn the cost of imperfect play instead of only ever seeing optimal or only ever seeing noisy.
        /// </summary>
        public void Add(JObject before, BattleCommand cmd, List<BattleEvent> events, BattleState after, BattleCommand perfectCmd = null, float? skillLevel = null)
        {
            bool acted = events.Exists(e => e is PlayerControlEvent || e is BattleEndedEvent);
            var turn = new JObject
            {
                ["index"] = turns.Count,
                ["wave"] = before["wave"],
                ["before"] = before,
                ["command"] = Command(cmd),
                ["accepted"] = acted,
                ["events"] = Events(events),
                ["after"] = new JObject
                {
                    ["phase"] = after.Phase.ToString(), ["wave"] = after.Wave, ["clock"] = after.Clock,
                    ["mana"] = Mana(after),
                    ["heroHp"] = new JArray(after.Units.Where(u => u.IsHero).Select(u => u.Alive ? u.Hp : 0)),
                    ["enemiesAlive"] = after.Units.Count(u => u.Alive && u.Team == Team.Enemy && !u.IsRemains),
                },
            };
            if (skillLevel.HasValue) turn["skillLevel"] = skillLevel.Value;
            if (perfectCmd != null) turn["perfectCommand"] = Command(perfectCmd);
            turns.Add(turn);
        }

        /// <summary>Close the record with the outcome; returns the finished document.</summary>
        public JObject Finish(BattleState s)
        {
            var r = s.Results;
            root["outcome"] = new JObject
            {
                ["phase"] = s.Phase.ToString(),
                ["waveReached"] = s.Wave + 1,
                ["actions"] = r.ActionsTaken,
                ["turns"] = r.PartyTurns,
                ["ticks"] = r.TicksElapsed,
                ["coins"] = r.Coins,
                ["xp"] = r.Xp,
                ["kills"] = JObject.FromObject(r.Kills),
                ["itemsUsed"] = JObject.FromObject(r.ItemsConsumed),
                ["heroesStanding"] = s.Units.Count(u => u.IsHero && u.Alive),
            };
            return root;
        }

        public string ToJson(bool indented = false) => root.ToString(indented ? Formatting.Indented : Formatting.None);

        // ── Pieces ────────────────────────────────────────────────────────────────

        public static JObject Snapshot(BattleState s) => new JObject
        {
            ["phase"] = s.Phase.ToString(),
            ["wave"] = s.Wave,
            ["clock"] = s.Clock,
            ["mana"] = Mana(s),
            ["units"] = new JArray(s.Units.Where(u => u.Alive).Select(u =>
            {
                var turn = s.TurnEntryOf(u.Id);
                var cast = s.CastEntryOf(u.Id);
                var o = new JObject
                {
                    ["id"] = u.Id, ["class"] = u.Def.Id, ["team"] = u.Team.ToString(), ["x"] = u.Pos.X, ["y"] = u.Pos.Y,
                    ["w"] = u.W, ["h"] = u.H, ["hp"] = u.Hp, ["maxHp"] = u.MaxHp, ["level"] = u.Level,
                    ["buffs"] = new JArray(u.Buffs.Select(b => new JObject { ["id"] = b.Def.Id, ["remaining"] = b.Remaining })),
                };
                if (u.IsHero)
                {
                    o["role"] = u.Def.Role;
                    o["cooldowns"] = JObject.FromObject(u.Cooldowns.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value));
                    o["bar"] = Bar(u);
                }
                else
                {
                    o["brain"] = u.Def.Brain.ToString();
                    o["remains"] = u.IsRemains;
                    if (u.IsCorpse) { o["corpseOf"] = u.CorpseOf; o["decayIn"] = u.DecayIn; }
                    if (u.PlateMax > 0) { o["plate"] = u.PlateHp; o["plateMax"] = u.PlateMax; o["facing"] = new JArray(u.Facing.X, u.Facing.Y); }
                    if (u.PlateMax > 0 && u.Def.PlateUnyielding) o["plateUnyielding"] = true; // GG-T2069
                }
                if (turn != null) { o["turnU"] = Math.Round(BattleEngine.BarU(s, turn), 4); o["stun"] = turn.StunTicks; o["in_recovery"] = BattleEngine.InRecovery(turn); o["recover_seconds"] = Math.Round(Math.Max(0, turn.RecoverTicks) / (double)BattleConfig.TicksPerSecond, 2); }
                if (cast != null) o["casting"] = new JObject { ["ability"] = cast.AbilityId, ["u"] = Math.Round(BattleEngine.BarU(s, cast), 4), ["target"] = cast.TargetUnitId };
                return o;
            })),
            ["pickups"] = new JArray(s.Pickups.Select(p => new JObject { ["id"] = p.PickupId, ["x"] = p.At.X, ["y"] = p.At.Y, ["left"] = p.ActionsLeft })),
            ["traps"] = new JArray(s.Traps.Select(t => new JObject { ["id"] = t.TrapId, ["x"] = t.At.X, ["y"] = t.At.Y })),
            ["blocks"] = new JArray(s.Blocks.Select(b => new JArray(b.X, b.Y))),
            ["amplifier"] = s.Amplifier.HasValue ? new JArray(s.Amplifier.Value.X, s.Amplifier.Value.Y) : null,
        };

        /// <summary>The party mana pool (§3.1.0): its points and capacity.</summary>
        public static JObject Mana(BattleState s) =>
            new JObject { ["points"] = s.PartyMana, ["capacity"] = BattleConfig.PartyManaCapacity };

        private static JArray Bar(Unit u) => new JArray(u.Bar.Select(sl =>
            sl == null || sl.IsEmpty ? (JToken)JValue.CreateNull()
            : sl.IsItem ? new JObject { ["item"] = sl.ItemId, ["charges"] = sl.Charges }
            : sl.IsWeapon ? new JObject { ["weapon"] = sl.WeaponId }
            : new JObject { ["ability"] = sl.AbilityId }));

        private static JObject Stats(Stats st) => new JObject
        {
            ["str"] = st.Strength, ["vit"] = st.Vitality, ["agi"] = st.Agility, ["spd"] = st.Speed,
            ["int"] = st.Intelligence, ["wis"] = st.Wisdom, ["lck"] = st.Luck,
        };

        public static JObject Command(BattleCommand cmd)
        {
            switch (cmd)
            {
                case MoveCommand m: return new JObject { ["type"] = "move", ["hero"] = m.HeroId, ["path"] = new JArray(m.Path.Select(c => new JArray(c.X, c.Y))), ["timeLeft"] = m.TimeLeftSeconds, ["held"] = m.HeldSeconds };
                case UseSlotCommand u: return new JObject { ["type"] = "use", ["hero"] = u.HeroId, ["slot"] = u.Slot, ["target"] = new JArray(u.Target.X, u.Target.Y) };
                case ShieldCommand sh: return new JObject { ["type"] = "guard", ["held"] = sh.HeldSeconds }; // as CommandCodec keeps it
                case TalkCommand t: return new JObject { ["type"] = "talk", ["hero"] = t.HeroId, ["target"] = t.TargetId };
                case TutorialSceneCommand sc: return new JObject { ["type"] = "scene", ["scene"] = sc.Scene };
                case DevCheatCommand dc: return new JObject { ["type"] = "dev", ["cheat"] = dc.Cheat.ToString() };
                default: return new JObject { ["type"] = cmd?.GetType().Name ?? "none" };
            }
        }

        public static JArray Events(List<BattleEvent> events)
        {
            var a = new JArray();
            if (events == null) return a;
            foreach (var e in events)
            {
                JObject o;
                try { o = JObject.FromObject(e, Json); }
                catch (Exception ex) { o = new JObject { ["serializeError"] = ex.Message }; }
                if (e is ManaGainedEvent) { o.Remove("Meters"); o.Remove("Readied"); }
                var name = e.GetType().Name;
                o.AddFirst(new JProperty("type", name.EndsWith("Event") ? name.Substring(0, name.Length - 5) : name));
                a.Add(o);
            }
            return a;
        }
    }
}
