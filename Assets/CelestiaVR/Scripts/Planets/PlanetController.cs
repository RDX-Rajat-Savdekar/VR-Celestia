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
            [Tooltip("If > 0, overrides the global planetDisplaySize for this body. " +
                     "Set to ~4.5 for Moon (= 0.5° angular size at sky radius 500).")]
            public float overrideDisplaySize = 0f;
            [Tooltip("Physical equatorial radius in km. Used for real-scale mode. Leave 0 to use built-in table.")]
            public float physicalRadiusKm = 0f;
            [Tooltip("Temperature in Kelvin (shown in info panel).")]
            public float temperatureK = 0f;
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
            foreach (var entry in planets)
            {
                if (entry.prefab == null)
                {
                    Debug.LogWarning($"[PlanetController] No prefab assigned for {entry.planetName}");
                    continue;
                }

                // Sun is handled entirely by SunController — skip it here to avoid
                // spawning a duplicate sun object at the origin of PlanetRoot.
                if (entry.planetName.Equals("Sun", StringComparison.OrdinalIgnoreCase))
                    continue;

                var go = Instantiate(entry.prefab, transform);
                go.name = entry.planetName;

                // Normalize to planetDisplaySize regardless of prefab's internal child scales.
                // Compute the combined renderer bounds at scale=1, then derive the right scale.
                go.transform.localScale = Vector3.one; // reset first so bounds are in model units
                float modelSize = GetMaxBoundsSize(go);
                float multiplier = Mathf.Max(0.01f, entry.scaleMultiplier); // guard serialized zero
                float displaySize = entry.overrideDisplaySize > 0f ? entry.overrideDisplaySize : planetDisplaySize;
                float effectiveScale = modelSize > 0f
                    ? (displaySize * multiplier) / modelSize
                    : displaySize * multiplier;
                go.transform.localScale = Vector3.one * effectiveScale;

                // Add CelestialBody component
                var body = go.AddComponent<CelestialBody>();
                // Store the source prefab so InspectionController can instantiate a fresh,
                // unscaled copy for the hologram instead of cloning the sky-scaled object.
                body.inspectionPrefab = entry.prefab;
                body.objectName = entry.planetName;
                body.bodyType = entry.planetName.Equals("Moon", StringComparison.OrdinalIgnoreCase)
                    ? CelestialBodyType.Moon
                    : CelestialBodyType.Planet;
                body.description = entry.description;
                body.magnitude = entry.apparentMagnitude;

                // Physical data — use inspector value if set, else built-in table
                body.physicalRadiusKm = entry.physicalRadiusKm > 0f
                    ? entry.physicalRadiusKm
                    : GetBuiltInRadiusKm(entry.planetName);
                body.temperatureK = entry.temperatureK > 0f
                    ? entry.temperatureK
                    : GetBuiltInTemperatureK(entry.planetName);

                // Enable emission on all renderers so planets appear as bright points at
                // night (ambient is near-black; without emission they'd be invisible).
                foreach (var rend in go.GetComponentsInChildren<Renderer>())
                {
                    foreach (var mat in rend.materials)
                    {
                        mat.EnableKeyword("_EMISSION");
                        // Use the albedo colour as the emission tint at moderate brightness
                        Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                                      : mat.HasProperty("_Color")    ? mat.GetColor("_Color")
                                      : Color.white;
                        mat.SetColor("_EmissionColor", baseCol * 0.35f);
                    }
                }

                // Always add a root SphereCollider for reliable gaze raycasting.
                // Child mesh colliders on complex prefabs are unreliable for sky-distance gaze.
                // Remove any existing root collider first to avoid duplicates.
                var existingCol = go.GetComponent<Collider>();
                if (existingCol != null) Destroy(existingCol);
                var col = go.AddComponent<SphereCollider>();
                col.radius = 2f;  // generous hit radius; world-size = 2 * planetDisplaySize

                // Parse ephemeris
                var ephemeris = entry.ephemerisFile != null
                    ? PlanetEphemerisParser.Parse(entry.ephemerisFile)
                    : new List<PlanetEphemerisParser.EphemerisEntry>();

                if (ephemeris.Count == 0)
                    Debug.LogWarning($"[PlanetController] No ephemeris data for {entry.planetName}");

                _planetInstances.Add((entry, go, ephemeris));
            }
        }

        /// Returns the size of the smallest individual renderer on the object (i.e. the planet sphere,
        /// not the rings). Using the minimum avoids Saturn's rings (which are huge in the GLB)
        /// blowing up the normalization and making the planet microscopic.
        /// <summary>Also used by InspectionController to normalise hologram prefab bounds.</summary>
        internal static float GetNormalisedBoundsSize(GameObject go) => GetMaxBoundsSize(go);

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
            return smallestSize;
        }

        // ── Built-in physical data ────────────────────────────────────────────────

        private static float GetBuiltInRadiusKm(string name) => name.ToLowerInvariant() switch
        {
            "mercury" =>  2_439.7f,
            "venus"   =>  6_051.8f,
            "earth"   =>  6_371.0f,
            "moon"    =>  1_737.4f,
            "mars"    =>  3_389.5f,
            "jupiter" => 71_492.0f,
            "saturn"  => 60_268.0f,
            "uranus"  => 25_362.0f,
            "neptune" => 24_622.0f,
            "pluto"   =>  1_188.3f,
            _         =>  0f
        };

        private static float GetBuiltInTemperatureK(string name) => name.ToLowerInvariant() switch
        {
            "mercury" =>   440f,
            "venus"   =>   737f,
            "earth"   =>   288f,
            "moon"    =>   220f,
            "mars"    =>   210f,
            "jupiter" =>   165f,
            "saturn"  =>   134f,
            "uranus"  =>    76f,
            "neptune" =>    72f,
            _         =>     0f
        };

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


                // Cache for inspection system
                if (body != null) body.CacheTransform();
            }
            _firstUpdateLogged = true;
        }
    }
}
