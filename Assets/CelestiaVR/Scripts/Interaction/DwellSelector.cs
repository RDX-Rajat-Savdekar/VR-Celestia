using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using CelestiaVR.Core;
using CelestiaVR.Audio;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Gaze-based dwell selection system.
    ///
    /// Selection strategy:
    ///   • Collect all CelestialBodies whose colliders fall within the acquisition
    ///     sphere-cast (radius = gazeTolerance). Pick the one whose world-space centre
    ///     is angularly closest to the gaze forward — so nearby competing planets never
    ///     steal focus from the one you're actually looking at.
    ///   • Hysteresis: once a target is acquired, it is kept as long as gaze stays
    ///     within gazeExitTolerance (> acquisition radius). This prevents the dwell
    ///     accumulator from resetting due to slight head wobble.
    ///
    /// Attach to XR Origin or Camera Offset.
    /// </summary>
    public class DwellSelector : MonoBehaviour
    {
        [Header("Gaze Settings")]
        [Tooltip("How long (seconds) the user must look at an object to trigger selection.")]
        [Range(0.5f, 5f)]
        public float dwellTime = 3f;

        [Tooltip("Maximum raycast distance for dwell detection.")]
        public float maxDistance = 600f;

        [Tooltip("Layer mask for celestial objects.")]
        public LayerMask celestialLayers = ~0;

        [Tooltip("Acquisition sphere-cast radius (Unity units). Larger = more forgiving initial lock-on.")]
        [Range(1f, 80f)]
        public float gazeTolerance = 30f;

        [Tooltip("Exit sphere-cast radius. Gaze must move this far from the target centre before the " +
                 "lock is released. Should be larger than gazeTolerance to avoid accumulator resets.")]
        [Range(1f, 120f)]
        public float gazeExitTolerance = 55f;

        [Header("References")]
        [Tooltip("The camera to cast the gaze ray from (usually Main Camera / XR Camera).")]
        public Camera gazeCamera;

        [Header("Dwell Filters")]
        [Tooltip("Allow dwell selection of billboard stars.")]
        public bool dwellStars = true;
        [Tooltip("Allow dwell selection of planets and moons.")]
        public bool dwellPlanets = true;
        [Tooltip("Allow dwell selection of deep-sky objects and constellations.")]
        public bool dwellDeepSky = true;
        [Tooltip("Objects whose direction from the camera dips below this elevation (degrees) are ignored. " +
                 "0 = strict horizon, positive = must be above horizon. Prevents selecting planets through the island floor.")]
        [Range(-10f, 20f)]
        public float minElevationDeg = 5f;

        // Events
        public event Action<CelestialBody> OnDwellSelect;
        public event Action<CelestialBody> OnGazeEnter;
        public event Action<CelestialBody> OnGazeExit;
        public event Action<CelestialBody, float> OnDwellProgress; // body, 0-1 progress

        private CelestialBody _currentTarget;
        private float _dwellAccumulator;

        // Software injection (for billboard stars without colliders)
        private CelestialBody _softwareTarget;
        private bool _softwareInjectedThisFrame;

        private ActionBasedController _rightController;

        /// <summary>
        /// Called by BillboardStarDwellDetector each frame when a billboard star is
        /// within gaze tolerance. If no physics target is found, this becomes the active target.
        /// </summary>
        public void InjectSoftwareTarget(CelestialBody body)
        {
            if (!dwellStars) return; // stars filtered out in menu
            _softwareTarget = body;
            _softwareInjectedThisFrame = true;
        }

        private void Awake()
        {
            if (gazeCamera == null)
                gazeCamera = Camera.main;

            if (gazeCamera == null)
                Debug.LogError("[DwellSelector] No gaze camera found! Assign Main Camera in Inspector.");

            foreach (var ctrl in FindObjectsByType<ActionBasedController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ctrl.name.ToLower().Contains("right"))
                {
                    _rightController = ctrl;
                    break;
                }
            }
        }

        private void Update()
        {
            if (gazeCamera == null) return;

            Ray ray = new Ray(gazeCamera.transform.position, gazeCamera.transform.forward);

            // 1. Find best candidate in acquisition cone
            CelestialBody hit = FindBestCandidate(ray);

            // 2. If no physics candidate, accept software-injected target (billboard stars)
            if (hit == null && _softwareInjectedThisFrame)
                hit = _softwareTarget;
            _softwareInjectedThisFrame = false;

            // 3. Hysteresis: if we already have a target, keep it as long as gaze hasn't
            //    moved beyond the exit tolerance — prevents wobble-induced resets.
            if (hit == null && _currentTarget != null && IsWithinExitTolerance(ray, _currentTarget))
                hit = _currentTarget;

            // 4. Target change
            if (hit != _currentTarget)
            {
                if (_currentTarget != null)
                    OnGazeExit?.Invoke(_currentTarget);

                _currentTarget = hit;
                _dwellAccumulator = 0f;

                if (_currentTarget != null)
                {
                    OnGazeEnter?.Invoke(_currentTarget);
                    SoundManager.Instance?.Play(SoundEvent.GazeEnter, _currentTarget.transform.position);
                }
            }

            // 5. Dwell accumulation
            if (_currentTarget != null)
            {
                _dwellAccumulator += Time.deltaTime;
                float progress = Mathf.Clamp01(_dwellAccumulator / dwellTime);
                OnDwellProgress?.Invoke(_currentTarget, progress);

                if (_dwellAccumulator >= dwellTime)
                {
                    SoundManager.Instance?.Play(SoundEvent.Select, _currentTarget.transform.position);
                    _rightController?.SendHapticImpulse(0.4f, 0.15f);
                    OnDwellSelect?.Invoke(_currentTarget);
                    _dwellAccumulator = 0f; // prevent repeat firing
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// SphereCastAll within acquisition radius, then returns the CelestialBody whose
        /// world-space centre is angularly closest to the gaze direction.
        ///
        /// Priority boost: planets/moons and DSOs get their scoring angle multiplied by a
        /// factor < 1 so they win over nearby stars even when slightly off-centre. This
        /// compensates for planets having small colliders compared to billboard stars.
        ///
        /// Type filters and horizon culling are applied before scoring.
        /// </summary>
        private CelestialBody FindBestCandidate(Ray ray)
        {
            var hits = Physics.SphereCastAll(ray, gazeTolerance, maxDistance, celestialLayers);

            CelestialBody best      = null;
            float         bestScore = float.MaxValue;

            foreach (var h in hits)
            {
                var body = h.collider.GetComponentInParent<CelestialBody>();
                if (body == null) continue;

                // ── Type filter ──────────────────────────────────────────────────
                if (!IsTypeAllowed(body)) continue;

                Vector3 dirToBody = (body.transform.position - ray.origin).normalized;

                // ── Horizon culling ───────────────────────────────────────────────
                // Elevation = angle above/below the horizontal plane (world Y).
                float elevDeg = Mathf.Asin(Mathf.Clamp(dirToBody.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (elevDeg < minElevationDeg) continue;

                // ── Priority scoring ──────────────────────────────────────────────
                // Angle between gaze forward and direction to this body's world centre.
                float angle = Vector3.Angle(ray.direction, dirToBody);

                // Planets/moons: score × 0.15 → strong priority so they always win nearby stars.
                // DSOs/constellations: score × 0.45 → moderate boost.
                float score = body.bodyType switch
                {
                    CelestialBodyType.Planet         => angle * 0.15f,
                    CelestialBodyType.Moon           => angle * 0.15f,
                    CelestialBodyType.DeepSkyObject  => angle * 0.45f,
                    CelestialBodyType.Constellation  => angle * 0.45f,
                    _                                => angle,
                };

                if (score < bestScore)
                {
                    bestScore = score;
                    best      = body;
                }
            }

            return best;
        }

        private bool IsTypeAllowed(CelestialBody body) => body.bodyType switch
        {
            CelestialBodyType.Star           => dwellStars,
            CelestialBodyType.Planet         => dwellPlanets,
            CelestialBodyType.Moon           => dwellPlanets,
            CelestialBodyType.DeepSkyObject  => dwellDeepSky,
            CelestialBodyType.Constellation  => dwellDeepSky,
            _                                => true,
        };

        /// <summary>
        /// Returns true if the gaze ray is still close enough to <paramref name="target"/>
        /// to maintain the hysteresis lock (uses gazeExitTolerance, not acquisition radius).
        /// </summary>
        private bool IsWithinExitTolerance(Ray ray, CelestialBody target)
        {
            Vector3 dirToTarget = (target.transform.position - ray.origin).normalized;

            // Drop the hysteresis lock immediately if target is below the elevation threshold
            float elevDeg = Mathf.Asin(Mathf.Clamp(dirToTarget.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (elevDeg < minElevationDeg) return false;

            float dist     = Vector3.Distance(ray.origin, target.transform.position);
            float angle    = Vector3.Angle(ray.direction, dirToTarget);
            float perpDist = Mathf.Sin(angle * Mathf.Deg2Rad) * dist;

            return perpDist <= gazeExitTolerance;
        }

        /// <summary>Returns the currently gazed CelestialBody (null if none).</summary>
        public CelestialBody CurrentTarget => _currentTarget;

        /// <summary>Returns dwell progress [0,1] for the current target.</summary>
        public float DwellProgress => _currentTarget != null ? Mathf.Clamp01(_dwellAccumulator / dwellTime) : 0f;
    }
}
