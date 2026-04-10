using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.Stars;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Extends the gaze-dwell system to cover all ~6000 GPU-instanced billboard stars.
    /// Billboard stars have no colliders (they're DrawMeshInstanced quads), so physics
    /// raycasts miss them entirely.
    ///
    /// Each frame this component computes angular distances between the gaze ray and
    /// every billboard star's sky direction (~6000 dot products, negligible on Quest 3).
    /// When a star is within the tolerance cone it injects into DwellSelector as a
    /// software target — reusing the existing dwell timer, events and highlight system
    /// with zero extra GameObjects per star.
    ///
    /// Attach to the same GameObject as DwellSelector.
    /// Auto-wires to StarCatalogParser and SkyManager.
    /// </summary>
    public class BillboardStarDwellDetector : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Auto-found if null.")]
        public StarCatalogParser catalogParser;
        [Tooltip("Auto-found if null.")]
        public DwellSelector dwellSelector;
        [Tooltip("Auto-found if null.")]
        public SkyManager skyManager;

        [Header("Detection")]
        [Tooltip("Half-angle of the gaze cone in degrees. 1.5° is a comfortable VR eye-tracking tolerance.")]
        [Range(0.5f, 5f)]
        public float gazeToleranceDegrees = 1.5f;
        [Tooltip("Skip named star spheres (they already have colliders). Must match NamedStarSpawner.magnitudeThreshold.")]
        [Range(0f, 4f)]
        public float namedStarMagThreshold = 3.0f;

        // ── State ─────────────────────────────────────────────────────────────────

        private struct BillboardStar
        {
            public Vector3 unitPos; // local to SkyManager (unit sphere)
            public StarData data;
        }

        private List<BillboardStar> _stars = new();
        private GameObject _proxyGO;
        private CelestialBody _proxyBody;
        private Renderer _proxyCoreRenderer;
        private Camera _gazeCamera;
        private float _cosThreshold;
        private int _currentHIP = -1; // HIP id of currently gazed billboard star

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (catalogParser == null) catalogParser = FindFirstObjectByType<StarCatalogParser>();
            if (dwellSelector   == null) dwellSelector  = FindFirstObjectByType<DwellSelector>();
            if (skyManager      == null) skyManager      = SkyManager.Instance ?? FindFirstObjectByType<SkyManager>();

            if (catalogParser != null)
                catalogParser.OnCatalogLoaded += OnCatalogLoaded;
            else
                Debug.LogError("[BillboardStarDwellDetector] StarCatalogParser not found.");
        }

        private void Start()
        {
            _gazeCamera    = Camera.main;
            _cosThreshold  = Mathf.Cos(gazeToleranceDegrees * Mathf.Deg2Rad);
            CreateProxy();
        }

        private void OnDestroy()
        {
            if (catalogParser != null)
                catalogParser.OnCatalogLoaded -= OnCatalogLoaded;
        }

        // ── Catalog ───────────────────────────────────────────────────────────────

        private void OnCatalogLoaded(List<StarData> stars)
        {
            _stars.Clear();
            foreach (var star in stars)
            {
                // Named-star sphere objects already have colliders; skip them here
                bool isNamedSphere = !string.IsNullOrEmpty(star.properName)
                                     && star.magnitude < namedStarMagThreshold;
                if (isNamedSphere) continue;

                _stars.Add(new BillboardStar { unitPos = star.unitPosition, data = star });
            }
            Debug.Log($"[BillboardStarDwellDetector] Tracking {_stars.Count} billboard stars for gaze detection.");
        }

        // ── Per-frame detection ───────────────────────────────────────────────────

        private void Update()
        {
            if (_stars.Count == 0 || dwellSelector == null || _gazeCamera == null) return;

            // If DwellSelector already locked on a real physics-collider target, step aside
            var physicsTarget = dwellSelector.CurrentTarget;
            if (physicsTarget != null && physicsTarget != _proxyBody)
            {
                if (_currentHIP != -1)
                {
                    _proxyGO.SetActive(false);
                    _currentHIP = -1;
                }
                return;
            }

            // Transform gaze direction to SkyManager-local space so we can compare
            // directly with star.unitPos (which is always in SkyManager local coords)
            Vector3 gazeWorld = _gazeCamera.transform.forward;
            Vector3 gazeLocal = skyManager != null
                ? skyManager.transform.InverseTransformDirection(gazeWorld)
                : gazeWorld;

            float bestDot = _cosThreshold;
            int   bestIdx = -1;

            for (int i = 0; i < _stars.Count; i++)
            {
                float dot = Vector3.Dot(gazeLocal, _stars[i].unitPos);
                if (dot > bestDot) { bestDot = dot; bestIdx = i; }
            }

            if (bestIdx >= 0)
            {
                var s = _stars[bestIdx];
                int hip = s.data.hipId;

                // Reconfigure proxy only when the target star changes
                if (hip != _currentHIP)
                {
                    _currentHIP = hip;
                    ConfigureProxy(s.data);
                    _proxyGO.SetActive(true);
                }

                // Keep proxy positioned at the rotating star (SkyManager local space)
                float radius = skyManager != null ? skyManager.skyRadius : 500f;
                _proxyGO.transform.localPosition = s.unitPos * radius;
                _proxyGO.transform.localRotation = Quaternion.LookRotation(-s.unitPos);

                // Hand off to DwellSelector's existing timer + event pipeline
                dwellSelector.InjectSoftwareTarget(_proxyBody);
            }
            else if (_currentHIP != -1)
            {
                _proxyGO.SetActive(false);
                _currentHIP = -1;
            }
        }

        // ── Proxy creation ────────────────────────────────────────────────────────

        private void CreateProxy()
        {
            _proxyGO = new GameObject("BillboardStarProxy");

            // Parent inside SkyManager so localPosition rotates with the sky
            if (skyManager != null)
                _proxyGO.transform.SetParent(skyManager.transform, false);

            _proxyGO.SetActive(false);

            // Core sphere — small, same additive style as NamedStarSpawner
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            core.transform.SetParent(_proxyGO.transform, false);
            core.transform.localScale = Vector3.one * 1.6f;
            _proxyCoreRenderer = core.GetComponent<Renderer>();
            ApplyAdditiveMat(_proxyCoreRenderer, new Color(1f, 0.95f, 0.8f, 1f));
            Destroy(core.GetComponent<Collider>());

            // Glow halo
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Glow";
            glow.transform.SetParent(_proxyGO.transform, false);
            glow.transform.localScale = Vector3.one * 4.8f;
            ApplyAdditiveMat(glow.GetComponent<Renderer>(), new Color(0.8f, 0.9f, 1f, 0.12f));
            Destroy(glow.GetComponent<Collider>());

            // Collider exists only so HighlightEffect's localScale.x reference is valid;
            // physics detection is off — we inject via software
            var col = _proxyGO.AddComponent<SphereCollider>();
            col.radius  = 2.4f;
            col.enabled = false;

            _proxyBody           = _proxyGO.AddComponent<CelestialBody>();
            _proxyBody.bodyType  = CelestialBodyType.Star;
        }

        private void ConfigureProxy(StarData star)
        {
            _proxyBody.objectName = !string.IsNullOrEmpty(star.properName)
                ? star.properName
                : star.hipId > 0 ? $"HIP {star.hipId}" : $"Star (mag {star.magnitude:F1})";

            _proxyBody.magnitude           = star.magnitude;
            _proxyBody.colorIndex          = star.colorIndex;
            _proxyBody.distanceLightYears  = star.distancePc > 0f ? star.distancePc * 3.26156f : 0f;
            _proxyBody.rightAscensionHours = star.raHours;
            _proxyBody.declinationDegrees  = star.decDegrees;
            _proxyBody.spectralType        = star.spectralClass;
            _proxyBody.description         = BuildDescription(star);

            // Tint core to star colour
            if (_proxyCoreRenderer != null)
                _proxyCoreRenderer.material.color = star.starColor;

            // Scale core by brightness so brighter stars look larger
            float r = Mathf.Lerp(0.8f, 2.2f, star.brightness);
            _proxyGO.transform.GetChild(0).localScale = Vector3.one * (r * 2f);
        }

        private static string BuildDescription(StarData star)
        {
            string colorDesc = star.colorIndex < 0f    ? "hot blue-white" :
                               star.colorIndex < 0.3f  ? "blue-white"     :
                               star.colorIndex < 0.6f  ? "white"          :
                               star.colorIndex < 1.0f  ? "yellow-orange"  : "deep red";

            string dist  = star.distancePc > 0f
                ? $"{star.distancePc * 3.26156f:F0} light-years away" : "distance unknown";
            string spect = !string.IsNullOrEmpty(star.spectralClass)
                ? $" ({star.spectralClass})" : "";

            return $"A {colorDesc} star{spect}, {dist}. Apparent magnitude {star.magnitude:F2}.";
        }

        private static void ApplyAdditiveMat(Renderer r, Color color)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(sh) { color = color };
            mat.SetFloat("_Surface",  1f);
            mat.SetFloat("_Blend",    0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite",   0f);
            mat.renderQueue = 2998;
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            r.material           = mat;
            r.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows     = false;
        }
    }
}
