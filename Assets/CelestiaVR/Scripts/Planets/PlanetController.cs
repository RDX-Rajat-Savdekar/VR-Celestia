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
        }

        [Header("Planets")]
        public List<PlanetEntry> planets;

        [Header("Display")]
        [Tooltip("Visual size of planets on the sky sphere (Unity units).")]
        [Range(0.5f, 10f)]
        public float planetDisplaySize = 2f;

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

                var go = Instantiate(entry.prefab, transform);
                go.name = entry.planetName;
                go.transform.localScale = Vector3.one * planetDisplaySize;

                // Add CelestialBody component
                var body = go.AddComponent<CelestialBody>();
                body.objectName = entry.planetName;
                body.bodyType = entry.planetName.Equals("Moon", StringComparison.OrdinalIgnoreCase)
                    ? CelestialBodyType.Moon
                    : CelestialBodyType.Planet;
                body.description = entry.description;
                body.magnitude = entry.apparentMagnitude;

                // Add sphere collider for raycasting
                if (go.GetComponent<Collider>() == null)
                {
                    var col = go.AddComponent<SphereCollider>();
                    col.radius = 0.5f;
                }

                // Parse ephemeris
                var ephemeris = entry.ephemerisFile != null
                    ? PlanetEphemerisParser.Parse(entry.ephemerisFile)
                    : new List<PlanetEphemerisParser.EphemerisEntry>();

                if (ephemeris.Count == 0)
                    Debug.LogWarning($"[PlanetController] No ephemeris data for {entry.planetName}");

                _planetInstances.Add((entry, go, ephemeris));
            }
        }

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
        }
    }
}
