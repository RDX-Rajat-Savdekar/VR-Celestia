using System;
using UnityEngine;
using CelestiaVR.Stars;
using CelestiaVR.Planets;

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
            _simulatedTime = useCurrentTime
                ? DateTime.UtcNow
                : DateTime.Parse(manualStartTimeUTC, null, System.Globalization.DateTimeStyles.RoundtripKind);

            _starRenderer = GetComponentInChildren<StarRenderer>();
            _planetController = GetComponentInChildren<PlanetController>();

            ApplySkyRotation();

            if (_starRenderer != null)
                _starRenderer.Initialize(this);

            if (_planetController != null)
                _planetController.Initialize(this);
        }

        private void Update()
        {
            _accumulatedSeconds += Time.deltaTime * timeScale;
            if (_accumulatedSeconds >= 1f)
            {
                int secondsToAdd = Mathf.FloorToInt(_accumulatedSeconds);
                _simulatedTime = _simulatedTime.AddSeconds(secondsToAdd);
                _accumulatedSeconds -= secondsToAdd;

                ApplySkyRotation();

                if (_planetController != null)
                    _planetController.UpdatePositions(_simulatedTime);
            }
        }

        private void ApplySkyRotation()
        {
            float rotY = CelestialCoordinates.GetSkyRotationDegrees(_simulatedTime, observerLongitude);
            // Tilt by latitude so the pole is at the correct elevation
            float tiltX = -(90f - observerLatitude);
            transform.rotation = Quaternion.Euler(tiltX, rotY, 0f);
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
    }
}
