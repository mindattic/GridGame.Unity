using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T679/GG-T680: the Manalith as a real 3D satellite, a self-contained layer any backdrop can show. One shared rig
    /// (off-screen, on its own layer) holds the Midnight Geode model tumbling slowly, the debris it sheds, its two lights
    /// and a camera that renders all of it, with bloom, into one transparent <see cref="Texture"/>. A layer shows that
    /// texture as its background (<see cref="Attach"/>); the rig renders only while an attached layer is on a panel.
    /// Every number comes from docs/MANALITH_VISUAL_SPEC.md, shared with SpaceGod (US-T458), in satellite diameters D
    /// (D = the model's longest bounding-box edge, scaled here to 1 unit). Reduce Motion holds pose and debris still.
    /// </summary>
    public sealed class ManalithRig : MonoBehaviour
    {
        public const int Layer = 30; // VfxPrefabLayer owns 31
        public const string ModelKey = "Manalith/manalith-satellite-00";
        /// <summary>The share of the frame's height the satellite's diameter fills (the layer sizes itself by this).</summary>
        public const float FillHeight = 0.7f;
        public const float FovDegrees = 30f;
        public const int Seed = 1337;

        public static readonly Color ManaBlue = Palette.OrbLit;
        /// <summary>The base texture's average colour (104, 92, 132) x 0.5.</summary>
        public static readonly Color RockColor = new Color32(0x34, 0x2E, 0x42, 0xFF);

        private static readonly Vector3 StartEuler = new Vector3(15f, 0f, 25f);
        private static readonly Vector3 TumbleRate = new Vector3(3f, 7f, 1.7f); // deg/s about local X, Y, Z
        private const float WobbleDegrees = 4f, WobbleSeconds = 40f;
        private const float ShardsPerSecond = 12f, GlowShare = 0.2f, ShellRadius = 0.45f;
        private const float ShardMin = 0.006f, ShardMax = 0.025f, ChunkMin = 0.03f, ChunkMax = 0.05f;
        private const float Speed = 0.02f, SpeedSpread = 0.5f, Fall = 0.05f, SpinMin = 30f, SpinMax = 180f;
        private const float Life = 6f, FadeSeconds = 1.5f, BurstMin = 8f, BurstMax = 12f;
        private const int BurstCount = 5;
        /// <summary>GG-T743: the fine dust: tiny shards on their own seed, shed faster and pushed harder down the wake
        /// the closer the Manalith comes (rate DustRateMin..DustRateMax/s, push DustPushMin..DustPushMax D/s^2).</summary>
        private const float DustMin = 0.002f, DustMax = 0.006f, DustRateMin = 15f, DustRateMax = 70f;
        private const float DustPushMin = 0.01f, DustPushMax = 0.08f;
        private const int DustSeed = 7331, DustMaxParticles = 512;
        /// <summary>The particle systems' loop. The chunk bursts are drawn once over it (8-12 s apart) and repeat with it;
        /// GG-T684: a 600 s loop of ~60 burst entries, stepped by hand, crashed the player inside ParticleSystem.Simulate.</summary>
        private const float LoopSeconds = 120f;
        private const float GlowIntensity = 2f, RimLightIntensity = 0.4f, BloomThreshold = 1f;
        /// <summary>View space: +x right, +y up, +z toward the viewer (the camera sits on +z looking at the satellite).</summary>
        /// <summary>The key light is shared with <see cref="MoonRig"/>, so both bodies are lit by the same sun.</summary>
        internal static readonly Vector3 KeyFrom = new Vector3(-0.5f, 0.7f, 0.5f), RimFrom = new Vector3(0.4f, 0.3f, -1f);
        private const int TexSize = 768;

        private static ManalithRig instance;
        private static readonly List<WeakReference<VisualElement>> users = new List<WeakReference<VisualElement>>();

        private Transform pivot;
        private Camera cam;
        private RenderTexture rt;
        private ParticleSystem debris, chunks, dust;
        private float t;
        private bool started;

        /// <summary>The rig's live render, or null when the model is missing (callers fall back to the flat art).</summary>
        public static Texture Texture => Ensure() != null ? instance.rt : null;

        /// <summary>Shows the live Manalith as <paramref name="e"/>'s background; the rig renders while it is on a panel.</summary>
        public static bool Attach(VisualElement e)
        {
            var rig = Ensure();
            if (rig == null) return false;
            e.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rig.rt));
            users.Add(new WeakReference<VisualElement>(e));
            return true;
        }

        private static ManalithRig Ensure()
        {
            if (instance != null) return instance;
            var model = Resources.Load<GameObject>(ModelKey);
            var satShader = Resources.Load<Shader>("Shaders/ManalithSatellite");
            if (model == null || satShader == null) return null;
            var go = new GameObject("ManalithRig") { hideFlags = HideFlags.DontSave };
            if (Application.isPlaying) DontDestroyOnLoad(go);
            go.transform.position = new Vector3(-50000f, 0f, 0f); // far from the VFX rig and anything else
            instance = go.AddComponent<ManalithRig>();
            instance.Build(model, satShader);
            return instance;
        }

        /// <summary>Builds everything in code (no prefabs, law 3).</summary>
        private void Build(GameObject model, Shader satShader)
        {
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            var inst = Instantiate(model, pivot, false);
            var mat = new Material(satShader);
            mat.SetTexture("_MainTex", Resources.Load<Texture2D>("Manalith/manalith-satellite-00"));
            mat.SetTexture("_MetallicGlossMap", Resources.Load<Texture2D>("Manalith/manalith-satellite-metallic-00"));
            mat.SetTexture("_BumpMap", Resources.Load<Texture2D>("Manalith/manalith-satellite-normal-00"));
            mat.SetFloat("_BumpScale", 1f);
            mat.SetColor("_RimColor", ManaBlue);
            mat.SetFloat("_RimPower", 3f);
            mat.SetFloat("_RimIntensity", 1.5f);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            SetLayer(inst.transform);
            // D = 1: scale the longest bounding-box edge to one unit and centre the bounds on the pivot.
            var b = Bounds(inst);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            inst.transform.localScale *= 1f / Mathf.Max(1e-5f, longest);
            b = Bounds(inst);
            inst.transform.position -= b.center - pivot.position;

            // Camera on +z (view space), looking back at the satellite. Unity is left-handed, so view-space +z = world -z.
            float dist = 1f / FillHeight / (2f * Mathf.Tan(FovDegrees * 0.5f * Mathf.Deg2Rad));
            cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.transform.localPosition = new Vector3(0f, 0f, -dist);
            cam.fieldOfView = FovDegrees;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = dist + 5f;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.depth = -60;
            rt = new RenderTexture(TexSize, TexSize, 24, RenderTextureFormat.ARGBHalf) { name = "Manalith", antiAliasing = 4 };
            rt.Create();
            cam.targetTexture = rt;
            var bloom = cam.gameObject.AddComponent<ManalithBloom>();
            bloom.Threshold = BloomThreshold;
            cam.enabled = false;

            Light(KeyFrom, Palette.White, 1f);
            Light(RimFrom, ManaBlue, RimLightIntensity);

            var shardShader = Resources.Load<Shader>("Shaders/ManalithShard");
            var shardMat = ShardMaterial(shardShader, ManaBlue * GlowIntensity);
            var rocks = RockMeshes(4);
            debris = SteadyDebris(shardMat, rocks);
            chunks = Chunks(shardMat, rocks);
            dust = Dust(shardMat, rocks);
            Pose();
        }

        private void SetLayer(Transform tr)
        {
            tr.gameObject.layer = Layer;
            foreach (Transform c in tr) SetLayer(c);
        }

        private static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>View-space direction (+z toward the viewer) to world (Unity's +z points away from this camera).</summary>
        internal static Vector3 ViewToWorld(Vector3 v) => new Vector3(v.x, v.y, -v.z).normalized;

        private void Light(Vector3 from, Color c, float intensity)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << Layer;
            l.transform.rotation = Quaternion.LookRotation(-ViewToWorld(from));
        }

        private Material ShardMaterial(Shader s, Color emission)
        {
            var m = new Material(s);
            m.SetColor("_Color", RockColor);
            m.SetColor("_EmissionColor", emission);
            m.SetVector("_KeyDir", ViewToWorld(KeyFrom));
            m.SetColor("_KeyColor", Palette.White);
            m.SetVector("_RimDir", ViewToWorld(RimFrom));
            m.SetColor("_RimLightColor", ManaBlue * RimLightIntensity);
            return m;
        }

        /// <summary>The steady debris: a fixed-step colour gradient sampled at random marks GlowShare of the shards
        /// (red 1 = glowing) and the shard shader adds the blue emission only to those.</summary>
        private ParticleSystem SteadyDebris(Material mat, Mesh[] rocks)
        {
            var flag = new Gradient { mode = GradientMode.Fixed };
            flag.SetKeys(new[] { new GradientColorKey(Color.black, 1f - GlowShare), new GradientColorKey(Palette.White, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var ps = Shards("Debris", mat, rocks, ShardMin, ShardMax,
                new ParticleSystem.MinMaxGradient(flag) { mode = ParticleSystemGradientMode.RandomColor });
            var em = ps.emission;
            em.rateOverTime = ShardsPerSecond;
            return ps;
        }

        /// <summary>The bigger chunks: BurstCount at a time, 8-12 s apart. The burst times come from the seed up front and
        /// go in as emission bursts (ParticleSystem.Emit from script stops a hand-simulated system's own emission).</summary>
        private ParticleSystem Chunks(Material mat, Mesh[] rocks)
        {
            var ps = Shards("Chunks", mat, rocks, ChunkMin, ChunkMax, new ParticleSystem.MinMaxGradient(Color.black));
            var em = ps.emission;
            em.rateOverTime = 0f;
            var rnd = new System.Random(Seed);
            var bursts = new List<ParticleSystem.Burst>();
            for (float at = Range(rnd, BurstMin, BurstMax); at < LoopSeconds; at += Range(rnd, BurstMin, BurstMax))
                bursts.Add(new ParticleSystem.Burst(at, (short)BurstCount));
            em.SetBursts(bursts.ToArray());
            return ps;
        }

        /// <summary>GG-T743: the fine dust, tiny unglowing shards on their own seed; <see cref="SetApproach"/> scales its
        /// rate and its push along the wake (it starts as if far off, still in the wake's default direction).</summary>
        private ParticleSystem Dust(Material mat, Mesh[] rocks)
        {
            var ps = Shards("Dust", mat, rocks, DustMin, DustMax, new ParticleSystem.MinMaxGradient(Color.black));
            ps.randomSeed = DustSeed;
            var main = ps.main;
            main.maxParticles = DustMaxParticles;
            ApplyDust(ps, 0f, new GridGame.Core.Content.DebrisDef().Angle);
            return ps;
        }

        /// <summary>Sets the dust's rate and its wake push for <paramref name="approach"/>; the wake's screen angle a
        /// (degrees clockwise from right) is the world direction (cos a, -sin a, 0) seen by this camera.</summary>
        private static void ApplyDust(ParticleSystem ps, float approach, float wakeAngle)
        {
            approach = Mathf.Clamp01(approach);
            var em = ps.emission;
            em.rateOverTime = Mathf.Lerp(DustRateMin, DustRateMax, approach);
            float a = wakeAngle * Mathf.Deg2Rad, push = Mathf.Lerp(DustPushMin, DustPushMax, approach);
            var force = ps.forceOverLifetime;
            force.x = Mathf.Cos(a) * push;
            force.y = -Mathf.Sin(a) * push - Fall;
            force.z = 0f;
        }

        private ParticleSystem Shards(string name, Material mat, Mesh[] rocks, float minSize, float maxSize, ParticleSystem.MinMaxGradient color)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = (uint)Seed;
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = LoopSeconds;
            main.loop = true;
            main.startLifetime = Life;
            main.startSpeed = new ParticleSystem.MinMaxCurve(Speed * (1f - SpeedSpread), Speed * (1f + SpeedSpread));
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.startRotation3D = true;
            var full = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startRotationX = full; main.startRotationY = full; main.startRotationZ = full;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true; // the sky keeps moving while the game is paused, like the rest of the backdrop
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 256;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = ShellRadius;
            shape.radiusThickness = 0f; // the shell itself
            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = 0f; force.y = -Fall; force.z = 0f;
            // Per-axis spin in ±SpinMax/√3: a random axis whose rate is up to about SpinMax (Unity has no "random axis,
            // rate in [min, max]" option; see the spec's engine notes).
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            float s = SpinMax / Mathf.Sqrt(3f) * Mathf.Deg2Rad;
            var spin = new ParticleSystem.MinMaxCurve(-s, s);
            rot.x = spin; rot.y = spin; rot.z = spin;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Palette.White, 0f), new GradientColorKey(Palette.White, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f - FadeSeconds / Life), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.SetMeshes(rocks);
            r.sharedMaterial = mat;
            r.alignment = ParticleSystemRenderSpace.World;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>Irregular low-poly rocks: icosahedra with jittered radii, flat-shaded, about 1 unit across.</summary>
        private static Mesh[] RockMeshes(int n)
        {
            var rnd = new System.Random(Seed);
            float p = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var meshes = new Mesh[n];
            for (int k = 0; k < n; k++)
            {
                var squash = new Vector3(1f, Range(rnd, 0.6f, 1f), Range(rnd, 0.7f, 1f));
                var pts = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++)
                    pts[i] = Vector3.Scale(v[i].normalized * 0.5f * Range(rnd, 0.75f, 1.15f), squash);
                var verts = new Vector3[f.Length];
                var tris = new int[f.Length];
                for (int i = 0; i < f.Length; i++) { verts[i] = pts[f[i]]; tris[i] = i; }
                var m = new Mesh { name = "rock-" + k, vertices = verts, triangles = tris };
                m.RecalculateNormals();
                m.RecalculateBounds();
                meshes[k] = m;
            }
            return meshes;
        }

        private static float Range(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        /// <summary>The tumble, closed-form so every engine and frame rate lands on the same pose at time t: a constant
        /// body-frame angular velocity (3, 7, 1.7) deg/s from the start pose, then a ±4° X wobble on a 40 s sine.</summary>
        public static Quaternion PoseAt(float t) =>
            Quaternion.Euler(StartEuler)
            * Quaternion.AngleAxis(TumbleRate.magnitude * t, TumbleRate.normalized)
            * Quaternion.Euler(WobbleDegrees * Mathf.Sin(2f * Mathf.PI * t / WobbleSeconds), 0f, 0f);

        private void Pose() => pivot.localRotation = PoseAt(t);

        /// <summary>Moves the satellite and its debris on by <paramref name="dt"/> seconds with hand-stepped particles, so
        /// a fixed step from t = 0 is repeatable (the screenshot tool only: GG-T684, at runtime the systems play).</summary>
        public void Advance(float dt)
        {
            t += dt;
            Pose();
            debris.Simulate(dt, true, !started, false);
            chunks.Simulate(dt, true, !started, false);
            dust.Simulate(dt, true, !started, false);
            started = true;
        }

        /// <summary>Renders one frame now into <see cref="rt"/> (the screenshot tool; at runtime the camera renders itself).</summary>
        public static RenderTexture RenderNow(float seconds, float step, Color background)
        {
            var rig = Ensure();
            if (rig == null) return null;
            int frames = Mathf.RoundToInt(seconds / step);
            int prev = 0;
            for (int i = 0; i < frames; i++)
            {
                rig.Advance(step);
                if (rig.chunks.particleCount > prev) Debug.Log($"[Build] Manalith chunk burst at t={rig.t:0.00}s");
                prev = rig.chunks.particleCount;
            }
            Debug.Log($"[Build] Manalith t={rig.t:0.00}s: {rig.debris.particleCount} shards, {rig.chunks.particleCount} chunks, {rig.dust.particleCount} dust alive");
            rig.cam.backgroundColor = background;
            rig.cam.Render();
            rig.cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            return rig.rt;
        }

        private void Update()
        {
            bool shown = false;
            for (int i = users.Count - 1; i >= 0; i--)
            {
                if (!users[i].TryGetTarget(out var e)) { users.RemoveAt(i); continue; }
                if (e.panel != null) shown = true;
            }
            cam.enabled = shown;
            var app = GameApp.I;
            bool hold = !shown || (app != null && app.Settings != null && app.Settings.ReduceMotion);
            Run(!hold);
            if (hold) return;
            t += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Pose();
        }

        /// <summary>GG-T734: how close the Manalith has come (0 far off .. 1 at its fullest, from story progress): the closer,
        /// the harder it sheds (up to 2.5x the steady shard rate). <paramref name="wakeAngle"/> is the backdrop's wake
        /// direction (screen degrees clockwise from right): GG-T743 streams the fine dust along it, faster and denser as it nears.</summary>
        public static void SetApproach(float approach, float wakeAngle)
        {
            var rig = Ensure();
            if (rig == null) return;
            var em = rig.debris.emission;
            em.rateOverTime = ShardsPerSecond * Mathf.Lerp(1f, 2.5f, Mathf.Clamp01(approach));
            ApplyDust(rig.dust, approach, wakeAngle);
        }

        /// <summary>GG-T684: at runtime the particle systems play on their own (unscaled) clock, paused while no layer shows
        /// the rig or Reduce Motion holds the sky still.</summary>
        private void Run(bool on)
        {
            if (on == debris.isPlaying) return;
            if (on) { debris.Play(true); chunks.Play(true); dust.Play(true); }
            else { debris.Pause(true); chunks.Pause(true); dust.Pause(true); }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (rt != null) rt.Release();
        }
    }

    /// <summary>The rig camera's bloom (Hidden/GridGame/ManalithBloom): threshold, four box down/up steps, combine.</summary>
    [ExecuteAlways] // the screenshot tool renders the rig from batchmode, outside play mode
    public sealed class ManalithBloom : MonoBehaviour
    {
        public float Threshold = 1f, Intensity = 1f;
        private const int Steps = 4;
        private Material mat;
        private readonly RenderTexture[] chain = new RenderTexture[Steps];

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null)
            {
                var s = Resources.Load<Shader>("Shaders/ManalithBloom");
                if (s == null) { Graphics.Blit(src, dst); return; }
                mat = new Material(s) { hideFlags = HideFlags.DontSave };
            }
            mat.SetFloat("_Threshold", Threshold);
            mat.SetFloat("_Intensity", Intensity);
            int w = src.width / 2, h = src.height / 2;
            var fmt = src.format;
            var cur = chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, mat, 0);
            int n = 1;
            for (; n < Steps; n++)
            {
                w /= 2; h /= 2;
                if (w < 2 || h < 2) break;
                chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(cur, chain[n], mat, 1);
                cur = chain[n];
            }
            for (int i = n - 2; i >= 0; i--)
            {
                Graphics.Blit(cur, chain[i], mat, 2);
                cur = chain[i];
            }
            mat.SetTexture("_BloomTex", cur);
            Graphics.Blit(src, dst, mat, 3);
            for (int i = 0; i < n; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
        }
    }
}
