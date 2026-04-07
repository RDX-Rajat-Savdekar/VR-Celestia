using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using CelestiaVR.Core;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Gaze-based dwell selection system.
    /// Casts a ray from the XR camera/headset forward. When the ray hits a CelestialBody
    /// collider and stays on it for <dwellTime> seconds, it fires OnDwellSelect.
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

        [Header("References")]
        [Tooltip("The camera to cast the gaze ray from (usually Main Camera / XR Camera).")]
        public Camera gazeCamera;

        // Events
        public event Action<CelestialBody> OnDwellSelect;
        public event Action<CelestialBody> OnGazeEnter;
        public event Action<CelestialBody> OnGazeExit;
        public event Action<CelestialBody, float> OnDwellProgress; // body, 0-1 progress

        private CelestialBody _currentTarget;
        private float _dwellAccumulator;

        private void Awake()
        {
            if (gazeCamera == null)
                gazeCamera = Camera.main;
        }

        private void Update()
        {
            if (gazeCamera == null) return;

            Ray ray = new Ray(gazeCamera.transform.position, gazeCamera.transform.forward);
            CelestialBody hit = null;

            if (Physics.Raycast(ray, out RaycastHit hitInfo, maxDistance, celestialLayers))
                hit = hitInfo.collider.GetComponentInParent<CelestialBody>();

            if (hit != _currentTarget)
            {
                if (_currentTarget != null)
                    OnGazeExit?.Invoke(_currentTarget);

                _currentTarget = hit;
                _dwellAccumulator = 0f;

                if (_currentTarget != null)
                    OnGazeEnter?.Invoke(_currentTarget);
            }

            if (_currentTarget != null)
            {
                _dwellAccumulator += Time.deltaTime;
                float progress = Mathf.Clamp01(_dwellAccumulator / dwellTime);
                OnDwellProgress?.Invoke(_currentTarget, progress);

                if (_dwellAccumulator >= dwellTime)
                {
                    OnDwellSelect?.Invoke(_currentTarget);
                    _dwellAccumulator = 0f; // prevent repeat firing
                }
            }
        }

        /// <summary>Returns the currently gazed CelestialBody (null if none).</summary>
        public CelestialBody CurrentTarget => _currentTarget;

        /// <summary>Returns dwell progress [0,1] for the current target.</summary>
        public float DwellProgress => _currentTarget != null ? Mathf.Clamp01(_dwellAccumulator / dwellTime) : 0f;
    }
}
