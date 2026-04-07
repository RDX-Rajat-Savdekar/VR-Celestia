using System;
using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Planets
{
    /// <summary>
    /// Manages all planet GameObjects: loads ephemeris data, positions planets on the
    /// sky sphere each time step, and provides CelestialBody components for selection.
    ///
    /// Attach to the [PlanetRoot] child of [SkyManager].
    /// Assign planet prefabs and ephemeris TextAssets in the inspector.
    /// </summary>
    public class PlanetController : MonoBehaviour
    {
        [Serializable]
        public class PlanetEntry
        {
            public string planetName;
            public GameObject prefab;               // Pre-imported GLB model as prefab
            public TextAsset ephemerisFile;          // horizons_results_*.txt
            [TextArea(1, 3)]
            public string description;
            public float apparentMagnitude = 1f;
            [Tooltip("Per-planet scale multiplier on top of planetDisplaySize. Use to correct GLB import size differences.")]
            [Range(0.01f, 5f)]
            public float scaleMultiplier = 1f;
        }

        [Header("Planets")]
        public List<PlanetEntry> planets;

        [Header("Display")]
        [Tooltip("Visual size of planets on the sky sphere (Unity units). 0.3 = small realistic dots, 2 = large visible spheres.")]
        [Range(0.05f, 10f)]
        public float planetDisplaySize = 0.3f;

        private SkyManager _skyManager;
        private List<(PlanetEntry config, GameObject go, List<PlanetEphemerisParser.EphemerisEntry> ephemeris)> _planetInstances
            = new();

        public void Initialize(SkyManager skyManager)
        {
            _skyManager = skyManager;
            SpawnPlanets();
        }

        private void SpawnPlanets()
        {
            Debug.Log($"[PlanetController] SpawnPlanets: {planets.Count} entries configured.");
            foreach (var entry in planets)
            {
                if (entry.prefab == null)
                {
                    Debug.LogWarning($"[PlanetController] No prefab assigned for {entry.planetName}");
                    continue;
                }

                var go = Instantiate(entry.prefab, transform);
                go.name = entry.planetName;

                // Normalize to planetDisplaySize regardless of prefab's internal child scales.
                // Compute the combined renderer bounds at scale=1, then derive the right scale.
                go.transform.localScale = Vector3.one; // reset first so bounds are in model units
                float modelSize = GetMaxBoundsSize(go);
                float multiplier = Mathf.Max(0.01f, entry.scaleMultiplier); // guard serialized zero
                float effectiveScale = modelSize > 0f
                    ? (planetDisplaySize * multiplier) / modelSize
                    : planetDisplaySize * multiplier;
                go.transform.localScale = Vector3.one * effectiveScale;
                Debug.Log($"[PlanetController] {entry.planetName}: modelSize={modelSize:F2} → scale={effectiveScale:F4}");
                Debug.Log($"[PlanetController] Spawned {entry.planetName} (prefab ok, ephemeris file: {(entry.ephemerisFile != null ? entry.ephemerisFile.name : "MISSING")})");

                // Add CelestialBody component
                var body = go.AddComponent<CelestialBody>();
                body.objectName = entry.planetName;
                body.bodyType = entry.planetName.Equals("Moon", StringComparison.OrdinalIgnoreCase)
                    ? CelestialBodyType.Moon
                    : CelestialBodyType.Planet;
                body.description = entry.description;
                body.magnitude = entry.apparentMagnitude;

                // Always add a root SphereCollider for reliable gaze raycasting.
                // Child mesh colliders on complex prefabs are unreliable for sky-distance gaze.
                // Remove any existing root collider first to avoid duplicates.
                var existingCol = go.GetComponent<Collider>();
                if (existingCol != null) Destroy(existingCol);
                var col = go.AddComponent<SphereCollider>();
                col.radius = 2f;  // generous hit radius; world-size = 2 * planetDisplaySize
                Debug.Log($"[PlanetController] Added SphereCollider (r=2) to {entry.planetName} root.");

                // Parse ephemeris
                var ephemeris = entry.ephemerisFile != null
                    ? PlanetEphemerisParser.Parse(entry.ephemerisFile)
                    : new List<PlanetEphemerisParser.EphemerisEntry>();

                if (ephemeris.Count == 0)
                    Debug.LogWarning($"[PlanetController] No ephemeris data for {entry.planetName}");
                else
                    Debug.Log($"[PlanetController] {entry.planetName}: loaded {ephemeris.Count} ephemeris entries.");

                _planetInstances.Add((entry, go, ephemeris));
            }
        }

        /// Returns the size of the smallest individual renderer on the object (i.e. the planet sphere,
        /// not the rings). Using the minimum avoids Saturn's rings (which are huge in the GLB)
        /// blowing up the normalization and making the planet microscopic.
        private static float GetMaxBoundsSize(GameObject go)
        {
            var meshFilters = go.GetComponentsInChildren<MeshFilter>();
            if (meshFilters.Length == 0) return 1f;

            float smallestSize = float.MaxValue;
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                // Use local mesh bounds multiplied by the child's own lossyScale
                // (root scale is already set to 1 before this call)
                Vector3 ls = mf.transform.lossyScale;
                Vector3 meshSize = mf.sharedMesh.bounds.size;
                float worldSize = Mathf.Max(
                    meshSize.x * Mathf.Abs(ls.x),
                    meshSize.y * Mathf.Abs(ls.y),
                    meshSize.z * Mathf.Abs(ls.z));

                if (worldSize > 0.001f && worldSize < smallestSize)
                    smallestSize = worldSize;
            }

            if (smallestSize == float.MaxValue) return 1f;

            // No clamping — km-scale GLBs (e.g. Saturn rings at 1000u) must be divided correctly.
            // effectiveScale = planetDisplaySize / smallestSize gives the right root scale.
            Debug.Log($"[PlanetController] {go.name} smallest mesh size = {smallestSize:F2}");
            return smallestSize;
        }

        private bool _firstUpdateLogged = false;

        public void UpdatePositions(DateTime simulatedTime)
        {
            foreach (var (config, go, ephemeris) in _planetInstances)
            {
                if (ephemeris.Count == 0) continue;

                var (ra, dec) = PlanetEphemerisParser.Interpolate(ephemeris, simulatedTime);

                var body = go.GetComponent<CelestialBody>();
                if (body != null)
                {
                    body.rightAscensionHours = ra;
                    body.declinationDegrees = dec;
                }

                // Position in SkyManager's local space
                go.transform.localPosition = _skyManager.GetSkyPosition(ra, dec);
                // Face outward from center
                go.transform.localRotation = Quaternion.LookRotation(go.transform.localPosition.normalized);

                if (!_firstUpdateLogged)
                    Debug.Log($"[PlanetController] {config.planetName} positioned at RA={ra:F2}h Dec={dec:F2}° → localPos={go.transform.localPosition}");

                // Cache for inspection system
                if (body != null) body.CacheTransform();
            }
            _firstUpdateLogged = true;
        }
    }
}
