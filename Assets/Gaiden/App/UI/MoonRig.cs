using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T681: the ordinary Moon as a real 3D body, a stage layer like the Manalith. It shares the Manalith's render path
    /// (<see cref="ManalithRig"/>): the same PBR shader with its rim turned off, the same key light direction (one sun for
    /// both bodies), an off-screen camera into a transparent texture, and the bloom pass at zero intensity, which only
    /// un-premultiplies the edges for UI Toolkit. The sphere and its maps are built in code from a fixed seed: highland
    /// and dark maria albedo, craters with raised rims and a few bright ray systems, and a normal map from that height.
    /// It turns slowly on a tilted axis; Reduce Motion holds it still.
    /// </summary>
    public sealed class MoonRig : MonoBehaviour
    {
        public const int Layer = 29; // ManalithRig owns 30, VfxPrefabLayer 31
        /// <summary>The share of the frame's height the Moon's diameter fills (the layer sizes itself by this).</summary>
        public const float FillHeight = 0.9f;
        private const float FovDegrees = 20f, SpinDegreesPerSecond = 0.8f, AxisTilt = 6.7f;
        private const int Seed = 4217, TexSize = 256, MapW = 512, MapH = 256;

        private static MoonRig instance;
        private static readonly List<WeakReference<VisualElement>> users = new List<WeakReference<VisualElement>>();

        private Transform body;
        private Camera cam;
        private RenderTexture rt;
        private float t;

        /// <summary>Shows the live Moon as <paramref name="e"/>'s background; false when the shader is missing (callers
        /// fall back to the flat image).</summary>
        public static bool Attach(VisualElement e)
        {
            var rig = Ensure();
            if (rig == null) return false;
            e.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rig.rt));
            users.Add(new WeakReference<VisualElement>(e));
            return true;
        }

        private static MoonRig Ensure()
        {
            if (instance != null) return instance;
            var shader = Resources.Load<Shader>("Shaders/ManalithSatellite");
            if (shader == null) return null;
            var go = new GameObject("MoonRig") { hideFlags = HideFlags.DontSave };
            if (Application.isPlaying) DontDestroyOnLoad(go);
            go.transform.position = new Vector3(-60000f, 0f, 0f); // apart from the Manalith rig and the VFX rig
            instance = go.AddComponent<MoonRig>();
            instance.Build(shader);
            return instance;
        }

        private void Build(Shader shader)
        {
            BuildMaps(out var albedo, out var normal);
            var gloss = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            gloss.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.06f)); // regolith: not metallic, nearly matte
            gloss.Apply();
            var mat = new Material(shader);
            mat.SetTexture("_MainTex", albedo);
            mat.SetTexture("_MetallicGlossMap", gloss);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 0.5f); // soft relief: the Moon reads by its maria, the craters only texture it
            mat.SetFloat("_RimIntensity", 0f);

            body = new GameObject("Moon") { layer = Layer }.transform;
            body.SetParent(transform, false);
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = Sphere(64, 32);
            var r = body.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            float dist = 1f / FillHeight / (2f * Mathf.Tan(FovDegrees * 0.5f * Mathf.Deg2Rad));
            cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.transform.localPosition = new Vector3(0f, 0f, -dist);
            cam.fieldOfView = FovDegrees;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = dist + 2f;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.depth = -61;
            rt = new RenderTexture(TexSize, TexSize, 24, RenderTextureFormat.ARGBHalf) { name = "Moon", antiAliasing = 4 };
            rt.Create();
            cam.targetTexture = rt;
            var edge = cam.gameObject.AddComponent<ManalithBloom>();
            edge.Intensity = 0f;
            cam.enabled = false;

            var key = new GameObject("Light").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Directional;
            key.color = Color.white;
            key.intensity = 1f;
            key.shadows = LightShadows.None;
            key.cullingMask = 1 << Layer;
            key.transform.rotation = Quaternion.LookRotation(-ManalithRig.ViewToWorld(ManalithRig.KeyFrom));
            Pose();
        }

        private void Pose() =>
            body.localRotation = Quaternion.Euler(0f, 0f, AxisTilt) * Quaternion.Euler(0f, -SpinDegreesPerSecond * t, 0f);

        public void Advance(float dt)
        {
            t += dt;
            Pose();
        }

        /// <summary>Renders one frame now (the screenshot tool; at runtime the camera renders itself).</summary>
        public static RenderTexture RenderNow(float seconds, Color background)
        {
            var rig = Ensure();
            if (rig == null) return null;
            rig.Advance(seconds);
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
            if (!shown) return;
            var app = GameApp.I;
            if (app != null && app.Settings != null && app.Settings.ReduceMotion) return;
            Advance(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (rt != null) rt.Release();
        }

        /// <summary>Equirectangular direction for a texel or vertex: u = longitude 0-1, v = latitude 0 (south) to 1.</summary>
        private static Vector3 Dir(float u, float v)
        {
            float lon = u * 2f * Mathf.PI, lat = (v - 0.5f) * Mathf.PI;
            return new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
        }

        /// <summary>A UV sphere of diameter 1 whose UVs match <see cref="Dir"/>, wound so its faces point outward.</summary>
        private static Mesh Sphere(int segs, int rings)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= rings; j++)
                for (int i = 0; i <= segs; i++)
                {
                    float u = (float)i / segs, v = (float)j / rings;
                    verts.Add(Dir(u, v) * 0.5f);
                    uvs.Add(new Vector2(u, v));
                }
            var tris = new List<int>();
            void Tri(int a, int b, int c)
            {
                var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (n.sqrMagnitude < 1e-12f) return; // the pole rows' degenerate halves
                if (Vector3.Dot(n, verts[a] + verts[b] + verts[c]) > 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
                else { tris.Add(a); tris.Add(c); tris.Add(b); }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < segs; i++)
                {
                    int a = j * (segs + 1) + i, b = a + 1, c = a + segs + 1, d = c + 1;
                    Tri(a, b, d);
                    Tri(a, d, c);
                }
            var m = new Mesh { name = "moon" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private struct Crater { public Vector3 C; public float R, Depth, Rays; }

        /// <summary>Height and albedo on the equirectangular grid, then the normal map from the height's slopes.</summary>
        private static void BuildMaps(out Texture2D albedo, out Texture2D normal)
        {
            var rnd = new System.Random(Seed);
            float Rand(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Vector3 RandDir()
            {
                float z = Rand(-1f, 1f), a = Rand(0f, 2f * Mathf.PI), s = Mathf.Sqrt(1f - z * z);
                return new Vector3(s * Mathf.Cos(a), z, s * Mathf.Sin(a));
            }

            // Maria: a few broad dark basins, smooth-edged, mostly on one hemisphere like the real near side.
            var maria = new List<(Vector3 c, float r)>();
            var nearSide = new Vector3(0f, 0.1f, -1f).normalized;
            for (int k = 0; k < 9; k++)
            {
                var c = Vector3.Slerp(RandDir(), nearSide, Rand(0.3f, 0.7f)).normalized;
                maria.Add((c, Rand(0.18f, 0.42f)));
            }
            var craters = new List<Crater>();
            for (int k = 0; k < 300; k++)
            {
                float r = 0.02f * Mathf.Pow(1f - (float)rnd.NextDouble() * 0.985f, -0.9f); // many small, few large
                r = Mathf.Min(r, 0.22f);
                // Real craters are shallow (depth about a fifth of the diameter), big ones shallower still.
                craters.Add(new Crater { C = RandDir(), R = r, Depth = Rand(0.2f, 0.35f) * Mathf.Lerp(1f, 0.5f, r / 0.22f),
                    Rays = rnd.NextDouble() < 0.04 ? Rand(0.1f, 0.18f) : 0f });
            }

            var height = new float[MapW * MapH];
            var mare = new float[MapW * MapH];
            var bright = new float[MapW * MapH];
            var dirs = new Vector3[MapW * MapH];
            for (int j = 0; j < MapH; j++)
                for (int i = 0; i < MapW; i++)
                {
                    var d = Dir((i + 0.5f) / MapW, (j + 0.5f) / MapH);
                    dirs[j * MapW + i] = d;
                    float m = 0f;
                    foreach (var (c, r) in maria)
                    {
                        float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, c), -1f, 1f));
                        m = Mathf.Max(m, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(r * 0.7f, r * 1.15f, ang)));
                    }
                    mare[j * MapW + i] = m;
                    height[j * MapW + i] = -0.02f * m;
                }
            foreach (var cr in craters)
            {
                float reach = cr.R * (cr.Rays > 0f ? 4f : 1.6f);
                float latC = Mathf.Asin(Mathf.Clamp(cr.C.y, -1f, 1f));
                int j0 = Mathf.Max(0, Mathf.FloorToInt(((latC - reach) / Mathf.PI + 0.5f) * MapH));
                int j1 = Mathf.Min(MapH - 1, Mathf.CeilToInt(((latC + reach) / Mathf.PI + 0.5f) * MapH));
                float cosReach = Mathf.Cos(reach);
                var e1 = Vector3.Cross(cr.C, Mathf.Abs(cr.C.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var e2 = Vector3.Cross(cr.C, e1);
                for (int j = j0; j <= j1; j++)
                    for (int i = 0; i < MapW; i++)
                    {
                        int idx = j * MapW + i;
                        float dot = Vector3.Dot(dirs[idx], cr.C);
                        if (dot < cosReach) continue;
                        float x = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) / cr.R;
                        float h = 0f;
                        if (x < 1f) h -= cr.Depth * cr.R * (1f - x * x);
                        h += 0.1f * cr.R * Mathf.Exp(-((x - 1f) * (x - 1f)) / 0.04f);
                        height[idx] += h;
                        if (cr.Rays > 0f && x > 0.9f)
                        {
                            float az = Mathf.Atan2(Vector3.Dot(dirs[idx], e1), Vector3.Dot(dirs[idx], e2));
                            float spokes = Mathf.Pow(Mathf.Abs(Mathf.Sin(az * 7f)), 6f);
                            bright[idx] += cr.Rays * (0.4f + 0.6f * spokes) * Mathf.Exp(-(x - 1f) / 1.2f);
                        }
                        else if (x < 1.3f) bright[idx] += 0.05f; // fresh crater walls catch more light
                    }
            }

            albedo = new Texture2D(MapW, MapH, TextureFormat.RGB24, true, false) { name = "moon-albedo", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            normal = new Texture2D(MapW, MapH, TextureFormat.RGBA32, true, true) { name = "moon-normal", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            var a = new Color32[MapW * MapH];
            var n = new Color32[MapW * MapH];
            // Central differences span two texels of 2π/MapW radians.
            const float slope = MapW / (4f * Mathf.PI);
            for (int j = 0; j < MapH; j++)
            {
                float lonScale = Mathf.Max(0.15f, Mathf.Cos(((j + 0.5f) / MapH - 0.5f) * Mathf.PI)); // texels shrink toward the poles
                for (int i = 0; i < MapW; i++)
                {
                    int idx = j * MapW + i;
                    float grain = (Hash(i, j) - 0.5f) * 0.05f;
                    float g = Mathf.Clamp01(Mathf.Lerp(0.62f, 0.22f, mare[idx]) + bright[idx] + grain);
                    a[idx] = new Color(g, g * 0.985f, g * 0.95f);
                    float hl = height[j * MapW + (i + MapW - 1) % MapW], hr = height[j * MapW + (i + 1) % MapW];
                    float hd = height[Mathf.Max(0, j - 1) * MapW + i], hu = height[Mathf.Min(MapH - 1, j + 1) * MapW + i];
                    var v = new Vector3(-(hr - hl) * slope / lonScale, -(hu - hd) * slope, 1f).normalized;
                    n[idx] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
                }
            }
            albedo.SetPixels32(a);
            albedo.Apply(true, true);
            normal.SetPixels32(n);
            normal.Apply(true, true);
        }

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + Seed);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
