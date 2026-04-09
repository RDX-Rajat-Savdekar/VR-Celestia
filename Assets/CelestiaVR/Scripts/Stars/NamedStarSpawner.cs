using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Stars
{
    /// <summary>
    /// Spawns glowing sphere GameObjects for named stars (those with a proper name in HYG)
    /// bright enough to be recognisable. Each sphere gets a CelestialBody component so
    /// the gaze / dwell selection system can identify and inspect it.
    ///
    /// Attach to the same GameObject as StarCatalogParser ([StarField] child of [SkyManager]).
    /// Spheres are parented to SkyManager so they rotate with sidereal time.
    /// </summary>
    [RequireComponent(typeof(StarCatalogParser))]
    public class NamedStarSpawner : MonoBehaviour
    {
        [Header("Filter")]
        [Tooltip("Only spawn sphere objects for named stars brighter than this magnitude.")]
        [Range(0f, 6f)]
        public float magnitudeThreshold = 3.0f;

        [Header("Visual Size")]
        [Tooltip("Sphere radius (Unity units) for the dimmest named star in the set.")]
        [Range(0.2f, 4f)]
        public float minSphereRadius = 0.8f;
        [Tooltip("Sphere radius for the brightest (magnitude ~ -1.5).")]
        [Range(0.5f, 6f)]
        public float maxSphereRadius = 2.2f;
        [Tooltip("Outer glow sphere is this many times the core radius.")]
        [Range(1.5f, 6f)]
        public float glowRadiusMultiplier = 3f;

        private SkyManager _skyManager;

        // ── Public API ────────────────────────────────────────────────────────────

        public void Initialize(SkyManager skyManager)
        {
            _skyManager = skyManager;
            GetComponent<StarCatalogParser>().OnCatalogLoaded += OnCatalogLoaded;
        }

        // ── Internal ─────────────────────────────────────────────────────────────

        private void OnCatalogLoaded(List<StarData> stars)
        {
            int count = 0;
            foreach (var star in stars)
            {
                if (string.IsNullOrEmpty(star.properName)) continue;
                if (star.magnitude >= magnitudeThreshold) continue;
                SpawnStar(star);
                count++;
            }
            Debug.Log($"[NamedStarSpawner] Spawned {count} named-star sphere objects (mag < {magnitudeThreshold}).");
        }

        private void SpawnStar(StarData star)
        {
            // Guard against corrupt catalog entries (NaN RA/Dec gives NaN unitPosition).
            if (!float.IsFinite(star.unitPosition.x) || !float.IsFinite(star.unitPosition.y) || !float.IsFinite(star.unitPosition.z))
            {
                Debug.LogWarning($"[NamedStarSpawner] Skipping '{star.properName}' — non-finite unitPosition.");
                return;
            }

            // Root object lives in SkyManager local space so it rotates with sidereal time.
            var go = new GameObject($"Star_{star.properName}");
            go.transform.SetParent(_skyManager.transform, false);
            go.transform.localPosition = star.unitPosition * _skyManager.skyRadius;
            go.transform.localRotation = Quaternion.LookRotation(-star.unitPosition);

            float radius = Mathf.Lerp(minSphereRadius, maxSphereRadius, star.brightness);

            // ── Core sphere ──────────────────────────────────────────────────────
            var core = CreateSphere("Core", go.transform, radius * 2f, star.starColor, 1.0f,
                renderQueue: 2999, castShadows: false);
            Destroy(core.GetComponent<Collider>());

            // ── Glow halo (larger, very transparent) ─────────────────────────────
            float glowDiameter = radius * 2f * glowRadiusMultiplier;
            var glow = CreateSphere("Glow", go.transform, glowDiameter,
                new Color(star.starColor.r, star.starColor.g, star.starColor.b, 0.15f),
                alpha: 0.15f, renderQueue: 2998, castShadows: false);
            Destroy(glow.GetComponent<Collider>());

            // ── Collider on root (generous, matches glow sphere) ──────────────────
            var col = go.AddComponent<SphereCollider>();
            col.radius = radius * glowRadiusMultiplier;

            // ── CelestialBody ─────────────────────────────────────────────────────
            var body = go.AddComponent<CelestialBody>();
            body.objectName          = star.properName;
            body.bodyType            = CelestialBodyType.Star;
            body.magnitude           = star.magnitude;
            body.colorIndex          = star.colorIndex;
            body.distanceLightYears  = star.distancePc * 3.26156f;
            body.rightAscensionHours = star.raHours;
            body.declinationDegrees  = star.decDegrees;
            body.spectralType        = star.spectralClass;
            body.description         = BuildDescription(star);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static GameObject CreateSphere(string name, Transform parent,
            float diameter, Color color, float alpha, int renderQueue, bool castShadows)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * diameter;

            var r = go.GetComponent<Renderer>();
            r.material           = BuildAdditiveMat(color, alpha, renderQueue);
            r.shadowCastingMode  = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows     = false;
            return go;
        }

        private static Material BuildAdditiveMat(Color color, float alpha, int renderQueue)
        {
            // URP Unlit with additive blending — same technique as the billboard stars.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var mat = new Material(shader) { name = "StarSphereMat" };
            mat.color = new Color(color.r, color.g, color.b, alpha);

            // Force additive blending so overlapping glows accumulate naturally.
            mat.SetFloat("_Surface",   1f);  // Transparent
            mat.SetFloat("_Blend",     0f);  // Alpha (we override src/dst below)
            mat.SetFloat("_SrcBlend",  (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend",  (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite",    0f);
            mat.renderQueue = renderQueue;
            mat.enableInstancing = false;
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            return mat;
        }

        private static string BuildDescription(StarData star)
        {
            string colorDesc = star.colorIndex < 0.0f  ? "hot blue-white" :
                               star.colorIndex < 0.3f  ? "blue-white"     :
                               star.colorIndex < 0.6f  ? "white"          :
                               star.colorIndex < 1.0f  ? "yellow-orange"  : "deep red";

            string sizeDesc  = !string.IsNullOrEmpty(star.spectralClass) && star.spectralClass.Length > 1
                ? (star.spectralClass.Contains("I") ? " giant" :
                   star.spectralClass.Contains("V") ? " dwarf" : "") : "";

            string distStr   = star.distancePc > 0f
                ? $"{star.distancePc * 3.26156f:F0} light-years away"
                : "distance unknown";

            string spectStr  = !string.IsNullOrEmpty(star.spectralClass)
                ? $" ({star.spectralClass})" : "";

            return $"A {colorDesc}{sizeDesc} star{spectStr}, {distStr}. " +
                   $"Apparent magnitude {star.magnitude:F2}.";
        }
    }
}
