using System.Collections.Generic;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>Music + SFX by key. Every battle event maps to a cue (bible §12.0: every event gets a sound).</summary>
    public sealed class Audio : MonoBehaviour
    {
        private AudioSource music, oldMusic; // music = the current track; oldMusic = the one fading out
        private readonly List<AudioSource> sfx = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private string currentTrack;
        public string CurrentTrack => currentTrack;
        /// <summary>Recent cues (for automation checks that every event is voiced).</summary>
        public readonly List<string> CueLog = new List<string>();
        private float musicVolume = 0.6f, sfxVolume = 0.85f;
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        public static Audio Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<Audio>();
            a.music = go.AddComponent<AudioSource>();
            a.music.loop = true;
            a.music.playOnAwake = false;
            a.oldMusic = go.AddComponent<AudioSource>();
            a.oldMusic.loop = true;
            a.oldMusic.playOnAwake = false;
            for (int i = 0; i < 10; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                a.sfx.Add(s);
            }
            return a;
        }

        public void Apply(Meta.SettingsSave s)
        {
            musicVolume = s.MuteMusic ? 0f : s.MusicVolume;
            sfxVolume = s.MuteSfx ? 0f : s.SfxVolume;
            FadeMusic();
        }

        // ── Ducking (POL-F3): music dips under cut-ins, fanfares and dialogue, then comes back ──
        public const float DuckDb = -8f;
        private float duck = 1f, duckUntil;

        /// <summary>Holds the music ducked for at least <paramref name="seconds"/> from now. Time-boxed rather than
        /// push/pop so a stopped coroutine can never leave the music stuck quiet.</summary>
        public void DuckFor(float seconds) => duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);

        public bool Ducked => Time.unscaledTime < duckUntil;

        private void Update()
        {
            float target = Ducked ? Mathf.Pow(10f, DuckDb / 20f) : 1f;
            if (!Mathf.Approximately(duck, target))
            {
                // Into the duck over Timing.Standard, back out over Timing.Dramatic (eased in dB, so it sounds even).
                float span = Ducked ? Timing.Standard : Timing.Dramatic;
                float db = 20f * Mathf.Log10(Mathf.Max(duck, 1e-4f)), goal = 20f * Mathf.Log10(target);
                db = Mathf.MoveTowards(db, goal, Mathf.Abs(DuckDb) * Time.unscaledDeltaTime / span);
                duck = Mathf.Pow(10f, db / 20f);
            }
            FadeMusic();
        }

        private AudioClip Clip(string path)
        {
            if (clips.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>(path);
            clips[path] = c;
            return c;
        }

        // ── Crossfades (GG-T460): two music sources; the new track fades in while the old one fades out ──
        /// <summary>Seconds a track change takes (unscaled time, so it runs through pauses and slow-mo).</summary>
        public const float CrossfadeSeconds = Timing.Cinematic;
        private float musicGain, oldGain;

        /// <summary>Changes the music with a crossfade; null fades it out. Switching back to the track still fading
        /// out turns it around in place instead of restarting it.</summary>
        public void Music(string track)
        {
            if (track == currentTrack) return;
            currentTrack = track;
            var c = track == null ? null : Clip("Audio/Music/" + track);
            if (c != null && oldMusic.isPlaying && oldMusic.clip == c)
            {
                (music, oldMusic) = (oldMusic, music);
                (musicGain, oldGain) = (oldGain, musicGain);
                return;
            }
            // The outgoing track fades from wherever it is; one already fading out (a third quick change) is cut.
            if (music.isPlaying)
            {
                oldMusic.Stop();
                (music, oldMusic) = (oldMusic, music);
                oldGain = musicGain;
            }
            musicGain = 0f;
            music.Stop();
            if (c == null) { music.clip = null; return; }
            music.clip = c;
            music.volume = 0f;
            music.Play();
        }

        /// <summary>Moves the two music sources' fade gains along and applies volume × duck × gain.</summary>
        private void FadeMusic()
        {
            if (music == null) return;
            float step = Time.unscaledDeltaTime / CrossfadeSeconds;
            if (music.clip != null) musicGain = Mathf.MoveTowards(musicGain, 1f, step);
            if (oldMusic.isPlaying)
            {
                oldGain = Mathf.MoveTowards(oldGain, 0f, step);
                if (oldGain <= 0f) oldMusic.Stop();
            }
            // Equal-power-ish curve: a linear gain ramp dips in the middle, the square root keeps loudness even.
            music.volume = musicVolume * duck * Mathf.Sqrt(musicGain);
            oldMusic.volume = musicVolume * duck * Mathf.Sqrt(oldGain);
        }

        /// <summary>Plays a cue. Rapid repeats of the same cue are thinned so chains don't clip into noise.</summary>
        public void Sfx(string key, float volume = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(key)) return;
            key = Canonical(key);
            CueLog.Add(key);
            if (CueLog.Count > 300) CueLog.RemoveAt(0);
            if (sfxVolume <= 0f) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(key, out var last) && now - last < 0.04f) return;
            lastPlayed[key] = now;
            var c = Clip("Audio/Sfx/" + Cue(key));
            if (c == null) return;
            AudioSource src = null;
            foreach (var s in sfx) if (!s.isPlaying && !held.Contains(s)) { src = s; break; }
            if (src == null) foreach (var s in sfx) if (!held.Contains(s)) { src = s; break; }
            if (src == null) return;
            src.pitch = pitch;
            src.PlayOneShot(c, volume * sfxVolume);
        }

        private readonly HashSet<AudioSource> held = new HashSet<AudioSource>();

        /// <summary>
        /// Battle pause (GG-T126): freezes every sound effect still ringing from the frozen beat, mid-clip, and
        /// resumes them from the same sample on Resume. Music keeps playing, and new cues (the pause menu's own
        /// clicks) take a source that isn't held.
        /// </summary>
        public void HoldSfx(bool hold)
        {
            if (hold)
            {
                foreach (var s in sfx) if (s.isPlaying) { s.Pause(); held.Add(s); }
                return;
            }
            foreach (var s in held) if (s != null) s.UnPause();
            held.Clear();
        }

        /// <summary>Silences every sound effect still ringing (held or playing) and drops the hold set — the endless
        /// loop's between-encounter teardown (GG-T61), so nothing from the last battle bleeds into the next. The pool
        /// itself is fixed (10 sources, never grown), and music is left alone.</summary>
        public void StopSfx()
        {
            foreach (var s in sfx) if (s != null) s.Stop();
            held.Clear();
        }

        /// <summary>Number of AudioSources this service owns (the two crossfading music sources + the fixed SFX pool),
        /// for leak checks.</summary>
        public int SourceCount => sfx.Count + (music != null ? 1 : 0) + (oldMusic != null ? 1 : 0);

        /// <summary>Pre-taxonomy cue names (POL-F1, GG-T254) → their taxonomy key, so an old name still plays.</summary>
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            ["move"] = "battle.move", ["hit"] = "battle.hit", ["crit"] = "battle.crit", ["miss"] = "battle.miss",
            ["dash"] = "battle.dash", ["slide"] = "battle.slide", ["pincer"] = "battle.pincer", ["turn"] = "battle.turn",
            ["taunt"] = "battle.taunt", ["warcry"] = "battle.warcry", ["death"] = "battle.death", ["pickup"] = "battle.pickup",
            ["orb"] = "battle.orb", ["trap"] = "battle.trap", ["shatter"] = "battle.shatter", ["clang"] = "battle.clang",
            ["whoosh"] = "battle.whoosh", ["explosion"] = "battle.explosion", ["cast"] = "battle.cast",
            ["channel"] = "battle.channel", ["heal"] = "battle.heal", ["powerup"] = "battle.powerup", ["glug"] = "battle.glug",
            ["glass"] = "battle.glass", ["victory"] = "battle.victory", ["defeat"] = "battle.defeat",
            ["crucible_enter"] = "battle.crucible_enter", ["crucible_wave"] = "battle.crucible_wave",
            ["crucible_best"] = "battle.crucible_best",
            ["fire"] = "element.fire", ["ice"] = "element.ice", ["lightning"] = "element.lightning",
            ["poison"] = "element.poison", ["magic"] = "element.arcane", ["splash"] = "element.water",
            ["debuff"] = "status.debuff", ["bubble"] = "status.bubble",
            ["quest_accept"] = "story.quest_accept", ["quest_complete"] = "story.quest_complete",
        };

        public static string Canonical(string key) => Aliases.TryGetValue(key, out var k) ? k : key;

        /// <summary>Taxonomy key → file: dots become underscores (battle.dash → battle_dash), except the variant
        /// pools (battle.move, battle.hit, battle.hit.&lt;type&gt;) and the cues that share a file. Keep game code speaking
        /// in cues, not filenames.</summary>
        private string Cue(string key)
        {
            switch (key)
            {
                case "ui.back": return "ui_click";
                case "battle.move": return "battle_move_" + Random.Range(0, 6);
                case "battle.hit": return GenericHit();
                default:
                    if (key.StartsWith(HitPrefix)) return TypedHit(key.Substring(HitPrefix.Length));
                    return key.Replace('.', '_'); // every other cue has a same-named file (tools/make_sfx.py)
            }
        }

        // ── Attack-type hits (GG-T556): "battle.hit.<type>" plays a random battle_hit_<type>_<n> that exists ──
        /// <summary>Cue prefix for a typed hit; the types and their file names are Content/attacktypes.json.</summary>
        public const string HitPrefix = "battle.hit.";
        /// <summary>Highest variant number looked for per type (battle_hit_&lt;type&gt;_0 .. _15).</summary>
        private const int MaxHitVariants = 16;
        private readonly Dictionary<string, List<string>> hitPools = new Dictionary<string, List<string>>();

        private static string GenericHit() => "battle_hit_" + Random.Range(0, 7);

        /// <summary>A random existing battle_hit_&lt;type&gt;_&lt;n&gt;; the generic pool while the type has no files yet,
        /// so no hit goes silent before its assets land.</summary>
        private string TypedHit(string type)
        {
            if (!hitPools.TryGetValue(type, out var pool))
            {
                pool = new List<string>();
                for (int n = 0; n < MaxHitVariants; n++)
                {
                    var name = "battle_hit_" + type + "_" + n;
                    if (Clip("Audio/Sfx/" + name) != null) pool.Add(name);
                }
                hitPools[type] = pool;
            }
            return pool.Count == 0 ? GenericHit() : pool[Random.Range(0, pool.Count)];
        }
    }
}
