using UnityEngine;
using UnityEngine.Rendering;
using CelestiaVR.Core;

namespace CelestiaVR.Environment
{
    /// <summary>
    /// Drives the day/night sky transition based on the Sun's altitude above the horizon.
    ///
    /// Altitude thresholds (degrees):
    ///   < −18  → astronomical night   (full stars, dark ambient)
    ///   −18→0  → twilight             (stars fade, horizon glows)
    ///     0→10 → sunrise/sunset       (stars nearly gone, warm light)
    ///   > 10   → full day             (stars invisible, blue sky)
    ///
    /// Attach to any persistent object in the scene (e.g. the [SkyManager] root).
    /// </summary>
    public class DayNightController : MonoBehaviour
    {
        // StarRenderer reference removed — stars are always visible regardless of time of day.
        // Day/night affects only ambient light and sky dome colour (pure atmosphere ambience).

        [Header("Ambient Light")]
        [Tooltip("Sky ambient color at full night.")]
        public Color nightAmbient  = new Color(0.05f, 0.06f, 0.15f);
        [Tooltip("Sky ambient color at midday.")]
        public Color dayAmbient    = new Color(0.55f, 0.62f, 0.72f);

        [Header("Sky Dome (day)")]
        [Tooltip("Tick to spawn a blue dome that fades in as the sun rises, covering the star field.")]
        public bool useDaySkyDome  = true;
        [Tooltip("Day sky color at the dome zenith.")]
        public Color daySkyColor   = new Color(0.35f, 0.55f, 0.85f, 0f); // alpha starts at 0

        [Header("Tweaks")]
        [Tooltip("How many degrees above the horizon the sky reaches full brightness.")]
        [Range(5f, 30f)]
        public float twilightSpanDegrees = 25f;

        // ── Private ───────────────────────────────────────────────────────────────

        private SkyManager   _sky;
        private MeshRenderer _dayDomeRenderer;
        private Material     _dayDomeMat;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            _sky = SkyManager.Instance;

            // Ensure ambient mode is Flat so our per-frame ambientLight assignments work.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog         = false;

            if (useDaySkyDome)
                BuildDaySkyDome();
        }

        private void Update()
        {
            if (_sky == null) return;

            float alt = _sky.GetSunAltitudeDegrees();
            // t = 0 → full night,  t = 1 → full day
            float t   = Mathf.Clamp01((alt + twilightSpanDegrees) / twilightSpanDegrees);

            // Stars: always fully visible — GlobalBrightnessFade intentionally not touched.
            // Day/night is purely atmospheric ambience, not a visibility gating.

            // ── Ambient light ─────────────────────────────────────────────────────
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, t);

            // ── Day sky dome alpha ────────────────────────────────────────────────
            if (_dayDomeMat != null)
            {
                Color c = daySkyColor;
                c.a     = Mathf.Lerp(0f, 0.88f, Mathf.Clamp01((t - 0.3f) / 0.7f));
                _dayDomeMat.color = c;
            }
        }

        // ── Day sky dome ──────────────────────────────────────────────────────────

        private void BuildDaySkyDome()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DaySkyDome";
            go.transform.SetParent(_sky.transform, false);
            // Slightly inside the Milky Way SkySphere so it renders in front of it
            // but behind named-star spheres (which are at radius 500−2 = 498).
            go.transform.localScale = Vector3.one * (_sky.skyRadius - 8f) * 2f;
            Destroy(go.GetComponent<Collider>());

            // Invert normals so it's visible from inside.
            var mf   = go.GetComponent<MeshFilter>();
            var mesh = Instantiate(mf.sharedMesh);
            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
            mesh.normals  = normals;
            // Reverse winding.
            var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            { int tmp = tris[i]; tris[i] = tris[i + 2]; tris[i + 2] = tmp; }
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mf.sharedMesh  = mesh;

            // Alpha-blend material, rendered ON TOP of star billboards (renderQueue > 3000).
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            _dayDomeMat = new Material(sh) { name = "DaySkyDomeMat" };
            _dayDomeMat.color = new Color(daySkyColor.r, daySkyColor.g, daySkyColor.b, 0f);
            _dayDomeMat.SetFloat("_Surface",  1f);
            _dayDomeMat.SetFloat("_Blend",    0f);
            _dayDomeMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _dayDomeMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _dayDomeMat.SetFloat("_ZWrite",   0f);
            _dayDomeMat.renderQueue = 3010; // after star billboards (3000) and named stars (2999)
            _dayDomeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            _dayDomeRenderer = go.GetComponent<MeshRenderer>();
            _dayDomeRenderer.material          = _dayDomeMat;
            _dayDomeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _dayDomeRenderer.receiveShadows    = false;
        }
    }
}
