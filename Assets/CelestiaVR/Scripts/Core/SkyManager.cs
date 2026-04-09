using System;
using UnityEngine;
using CelestiaVR.Stars;
using CelestiaVR.Planets;
using CelestiaVR.Environment;

namespace CelestiaVR.Core
{
    /// <summary>
    /// Root manager for the sky simulation.
    /// Controls time, sidereal rotation, and coordinates all sub-systems.
    /// Attach to the [SkyManager] root GameObject in the scene.
    /// </summary>
    public class SkyManager : MonoBehaviour
    {
        public static SkyManager Instance { get; private set; }

        [Header("Observer Location")]
        [Tooltip("Observer latitude in degrees. Positive = North.")]
        [Range(-90f, 90f)]
        public float observerLatitude = 40f;

        [Tooltip("Observer longitude in degrees. Positive = East.")]
        [Range(-180f, 180f)]
        public float observerLongitude = -74f;

        [Header("Time Simulation")]
        [Tooltip("Use system clock as starting time.")]
        public bool useCurrentTime = true;

        [Tooltip("Manual start time (UTC) if useCurrentTime is false.")]
        public string manualStartTimeUTC = "2026-04-06T00:00:00Z";

        [Tooltip("Speed multiplier. 1 = real time. 3600 = 1 hour per second.")]
        [Range(0f, 86400f)]
        public float timeScale = 1f;

        [Header("Sky Sphere")]
        [Tooltip("Radius of the sky sphere. Should be large to contain all content.")]
        public float skyRadius = 500f;

        // Cached sub-systems
        private StarRenderer _starRenderer;
        private PlanetController _planetController;
        private NamedStarSpawner _namedStarSpawner;
        private SunController _sunController;

        // Time tracking
        private DateTime _simulatedTime;
        private float _accumulatedSeconds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            try
            {
                _simulatedTime = useCurrentTime
                    ? DateTime.UtcNow
                    : DateTime.Parse(manualStartTimeUTC, null, System.Globalization.DateTimeStyles.RoundtripKind);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkyManager] Failed to parse manualStartTimeUTC '{manualStartTimeUTC}': {ex.Message}. Using UtcNow.");
                _simulatedTime = DateTime.UtcNow;
            }

            _starRenderer = GetComponentInChildren<StarRenderer>();
            _planetController = GetComponentInChildren<PlanetController>();
            _namedStarSpawner = GetComponentInChildren<NamedStarSpawner>();
            _sunController = GetComponentInChildren<SunController>();

            ApplySkyRotation();

            if (_starRenderer != null)
                _starRenderer.Initialize(this);

            if (_planetController != null)
                _planetController.Initialize(this);

            if (_namedStarSpawner != null)
                _namedStarSpawner.Initialize(this);

            if (_sunController != null)
                _sunController.Initialize(this);
        }

        private void Update()
        {
            _accumulatedSeconds += Time.deltaTime * timeScale;
            if (_accumulatedSeconds >= 1f)
            {
                int secondsToAdd = Mathf.FloorToInt(_accumulatedSeconds);
                try { _simulatedTime = _simulatedTime.AddSeconds(secondsToAdd); }
                catch { _simulatedTime = DateTime.UtcNow; }
                _accumulatedSeconds -= secondsToAdd;

                ApplySkyRotation();

                if (_planetController != null)
                    _planetController.UpdatePositions(_simulatedTime);

                if (_sunController != null)
                    _sunController.UpdateSunPosition(_simulatedTime);
            }
        }

        private void ApplySkyRotation()
        {
            float rotY = CelestialCoordinates.GetSkyRotationDegrees(_simulatedTime, observerLongitude);
            // Tilt by latitude so the pole is at the correct elevation
            float tiltX = -(90f - observerLatitude);
            transform.rotation = Quaternion.Euler(tiltX, rotY, 0f);

            // Keep the panoramic skybox in sync with sidereal rotation
            if (RenderSettings.skybox != null)
                RenderSettings.skybox.SetFloat("_Rotation", rotY);
        }

        /// <summary>
        /// Returns a Unity world position on the sky sphere for given RA (hours) / Dec (degrees).
        /// Position is in local space of SkyManager; the rotation is applied by the SkyManager transform.
        /// </summary>
        public Vector3 GetSkyPosition(float raHours, float decDegrees)
        {
            return CelestialCoordinates.RADecToUnity(raHours, decDegrees, skyRadius);
        }

        public DateTime SimulatedTime => _simulatedTime;

        // ── Sun helpers ───────────────────────────────────────────────────────────

        /// <summary>Returns the Sun's current RA (hours) and Dec (degrees).</summary>
        public (float raHours, float decDegrees) GetSunEquatorialCoords()
            => CelestialCoordinates.ComputeSunRADec(_simulatedTime);

        /// <summary>Returns the Sun's altitude above the observer's horizon in degrees.</summary>
        public float GetSunAltitudeDegrees()
        {
            var (ra, dec) = GetSunEquatorialCoords();
            return CelestialCoordinates.ComputeAltitudeDegrees(ra, dec,
                observerLatitude, observerLongitude, _simulatedTime);
        }

        /// <summary>
        /// Directly offset the simulated time by a given number of seconds.
        /// Call this from TimeScrollController to drive time-scrubbing.
        /// Forces an immediate sky + planet update.
        /// </summary>
        public void OffsetSimulatedTime(double seconds)
        {
            if (!double.IsFinite(seconds)) return;
            try { _simulatedTime = _simulatedTime.AddSeconds(seconds); }
            catch { _simulatedTime = DateTime.UtcNow; }
            _accumulatedSeconds = 0f; // reset accumulator so Update() doesn't double-advance
            ApplySkyRotation();
            if (_planetController != null)
                _planetController.UpdatePositions(_simulatedTime);
            if (_sunController != null)
                _sunController.UpdateSunPosition(_simulatedTime);
        }
    }
}
