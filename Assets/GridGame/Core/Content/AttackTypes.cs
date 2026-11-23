using System;
using System.Collections.Generic;
using System.Linq;

namespace GridGame.Core.Content
{
    /// <summary>
    /// One attack type in attacktypes.json (GG-T556): what a physical hit sounds like. <see cref="Sfx"/> names every
    /// file the type wants (Resources/Audio/Sfx/&lt;name&gt;.ogg, played at random among the ones that exist); while none
    /// exists the hit falls back to the generic battle-hit-00..06. docs/AUDIO_ATTACK_TYPES.md is the sound brief.
    /// </summary>
    public sealed class AttackTypeDef
    {
        public string Id;
        /// <summary>One-line sound-design brief for the asset author.</summary>
        public string Brief;
        public List<string> Sfx = new List<string>();
    }

    /// <summary>
    /// attacktypes.json (GG-T556): the attack-type set and who uses which. A hero's hit sounds like its weapon
    /// (<see cref="Weapons"/>, by <see cref="ItemDef.WeaponType"/>); a monster's like its natural attack
    /// (<see cref="Enemies"/>, by class id, or by family = the id without its trailing digits); a physical skill may
    /// carry its own (<see cref="Abilities"/>); a collision note ("Slam!") its own (<see cref="Notes"/>). Elemental
    /// hits and spells keep their element's sound; only Physical hits use these.
    /// </summary>
    public sealed class AttackTypeSheet
    {
        /// <summary>Type for anything unmapped (an unarmed hero).</summary>
        public string Default = "blunt";
        public List<AttackTypeDef> Types = new List<AttackTypeDef>();
        // Keyed case-insensitively, like Validate (ContentDb's ids) reads them; the loader's Replace handling builds
        // plain dictionaries, so they're rebuilt after reading.
        public Dictionary<string, string> Weapons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Enemies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Abilities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [System.Runtime.Serialization.OnDeserialized]
        private void IgnoreKeyCase(System.Runtime.Serialization.StreamingContext _)
        {
            Weapons = IgnoreCase(Weapons); Enemies = IgnoreCase(Enemies); Abilities = IgnoreCase(Abilities); Notes = IgnoreCase(Notes);
        }

        private static Dictionary<string, string> IgnoreCase(Dictionary<string, string> d)
        {
            var ci = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (d != null) foreach (var kv in d) if (!ci.ContainsKey(kv.Key)) ci[kv.Key] = kv.Value;
            return ci;
        }

        public AttackTypeDef Type(string id) => id == null ? null : Types.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>A class id without its trailing variant digits (wolf03 → wolf).</summary>
        public static string Family(string id) => id?.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

        /// <summary>The class's natural attack type (exact id first, then its family), or null.</summary>
        public string ForClass(string classId)
        {
            if (classId == null) return null;
            if (Enemies.TryGetValue(classId, out var t)) return t;
            var fam = Family(classId);
            return fam != classId && fam.Length > 0 && Enemies.TryGetValue(fam, out t) ? t : null;
        }

        public string ForWeapon(string weaponType) => weaponType != null && Weapons.TryGetValue(weaponType, out var t) ? t : null;

        /// <summary>The attack type of one physical hit: the collision note's, else the resolving ability's, else the
        /// attacker's weapon's, else its class's natural one, else <see cref="Default"/>.</summary>
        public string ForHit(string classId, string weaponType, string abilityId, string note)
        {
            if (note != null && Notes.TryGetValue(note, out var n)) return n;
            if (abilityId != null && Abilities.TryGetValue(abilityId, out var a)) return a;
            return ForWeapon(weaponType) ?? ForClass(classId) ?? Default;
        }

        public static void Validate(ContentDb db, List<string> p)
        {
            var s = db.AttackTypes;
            if (s == null || s.Types.Count == 0) return; // fixtures without the table
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in s.Types)
            {
                if (string.IsNullOrEmpty(t.Id)) { p.Add("attacktypes: a type with no Id"); continue; }
                if (!ids.Add(t.Id)) p.Add($"attacktypes: duplicate type '{t.Id}'");
                if (t.Sfx.Count == 0) p.Add($"attacktypes: type '{t.Id}' names no Sfx files");
                var stem = AssetNames.Stem("battle", "hit", t.Id);
                foreach (var f in t.Sfx)
                    if (!AssetNames.TryParse(f, out var fs, out _) || fs != stem)
                        p.Add($"attacktypes: type '{t.Id}' sfx '{f}' must be named {stem}-NN (GG-T562)");
            }
            void Need(string type, string where) { if (!ids.Contains(type ?? "")) p.Add($"attacktypes {where}: unknown type '{type}'"); }
            Need(s.Default, "Default");
            foreach (var kv in s.Weapons) Need(kv.Value, $"weapon {kv.Key}");
            foreach (var kv in s.Abilities)
            {
                Need(kv.Value, $"ability {kv.Key}");
                if (!db.Abilities.ContainsKey(kv.Key)) p.Add($"attacktypes: unknown ability '{kv.Key}'");
            }
            foreach (var kv in s.Notes) Need(kv.Value, $"note {kv.Key}");
            var families = new HashSet<string>(db.Classes.Keys.Select(Family), StringComparer.OrdinalIgnoreCase);
            foreach (var kv in s.Enemies)
            {
                Need(kv.Value, $"enemy {kv.Key}");
                if (!db.Classes.ContainsKey(kv.Key) && !families.Contains(kv.Key)) p.Add($"attacktypes: '{kv.Key}' is no class id or family");
            }
            // Every weapon in the game and every foe has a sound.
            foreach (var it in db.Items.Values)
                if (!string.IsNullOrEmpty(it.WeaponType) && s.ForWeapon(it.WeaponType) == null)
                    p.Add($"attacktypes: weapon type '{it.WeaponType}' (item {it.Id}) has no attack type");
            foreach (var c in db.Classes.Values)
                if (c.Team == Team.Enemy && s.ForClass(c.Id) == null)
                    p.Add($"attacktypes: enemy class '{c.Id}' has no attack type");
        }
    }
}
