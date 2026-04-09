using UnityEngine;
using UnityEngine.XR;
using CelestiaVR.Core;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Star Walk 2-style time scrubbing via the right thumbstick.
    ///
    /// - Push thumbstick left/right → time scrolls backward/forward
    /// - Release thumbstick → momentum carries time forward and decelerates (inertia)
    /// - Hold grip on either controller while scrolling → 10x speed multiplier
    ///
    /// Attach anywhere in the scene (e.g., [SkyManager] or [XR Origin]).
    /// Requires SkyManager.Instance to be present.
    /// </summary>
    public class TimeScrollController : MonoBehaviour
    {
        [Header("Time Speed")]
        [Tooltip("How many in-game hours advance per second when thumbstick is fully pushed.")]
        public float hoursPerSecondAtFullTilt = 1f;

        [Tooltip("Multiplier applied when the grip button is held — for jumping days quickly.")]
        public float gripSpeedMultiplier = 24f;

        [Header("Inertia")]
        [Tooltip("How quickly momentum decays after releasing the thumbstick. Higher = snappier stop.")]
        [Range(0.5f, 20f)]
        public float inertiaDamping = 4f;

        [Tooltip("Minimum momentum (hours/sec) below which motion fully stops.")]
        public float momentumDeadzone = 0.001f;

        [Header("Input Dead Zone")]
        [Range(0f, 0.5f)]
        public float thumbstickDeadzone = 0.15f;

        // Current momentum in hours per second (signed: positive = forward, negative = backward)
        private float _momentumHoursPerSec = 0f;

        // XR device references — fetched once and cached
        private InputDevice _rightController;
        private InputDevice _leftController;
        private bool _devicesFound = false;

        private void Start()
        {
            TryFindDevices();
        }

        private void TryFindDevices()
        {
            var rightHand = new System.Collections.Generic.List<InputDevice>();
            var leftHand  = new System.Collections.Generic.List<InputDevice>();

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, rightHand);
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Left  | InputDeviceCharacteristics.Controller, leftHand);

            if (rightHand.Count > 0) _rightController = rightHand[0];
            if (leftHand.Count  > 0) _leftController  = leftHand[0];

            _devicesFound = _rightController.isValid || _leftController.isValid;
        }

        private void Update()
        {
            if (SkyManager.Instance == null) return;

            // Retry device discovery if controllers haven't been found yet
            if (!_devicesFound) TryFindDevices();

            float thumbstickX = GetThumbstickX();
            bool gripHeld = GetGripHeld();

            float speedMultiplier = gripHeld ? gripSpeedMultiplier : 1f;

            if (Mathf.Abs(thumbstickX) > thumbstickDeadzone)
            {
                // Thumbstick is active — drive momentum directly from input
                float targetMomentum = thumbstickX * hoursPerSecondAtFullTilt * speedMultiplier;
                // Smooth the ramp-up so it doesn't feel instant
                _momentumHoursPerSec = Mathf.Lerp(_momentumHoursPerSec, targetMomentum, Time.deltaTime * 10f);
            }
            else
            {
                // Thumbstick released — apply inertia decay
                _momentumHoursPerSec = Mathf.Lerp(_momentumHoursPerSec, 0f, Time.deltaTime * inertiaDamping);
                if (Mathf.Abs(_momentumHoursPerSec) < momentumDeadzone)
                    _momentumHoursPerSec = 0f;
            }

            if (_momentumHoursPerSec != 0f)
            {
                double secondsToAdd = _momentumHoursPerSec * 3600.0 * Time.deltaTime;
                SkyManager.Instance.OffsetSimulatedTime(secondsToAdd);
            }
        }

        private float GetThumbstickX()
        {
            // Try right controller first, fall back to left
            if (_rightController.isValid)
            {
                if (_rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
                    return axis.x;
            }
            if (_leftController.isValid)
            {
                if (_leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
                    return axis.x;
            }
            return 0f;
        }

        private bool GetGripHeld()
        {
            bool grip = false;
            if (_rightController.isValid)
                _rightController.TryGetFeatureValue(CommonUsages.gripButton, out grip);
            if (!grip && _leftController.isValid)
                _leftController.TryGetFeatureValue(CommonUsages.gripButton, out grip);
            return grip;
        }

        /// <summary>
        /// Public API — lets a UI button auto-scroll time (e.g. play/pause button).
        /// Set to 0 to stop.
        /// </summary>
        public void SetAutoScrollSpeed(float hoursPerSec)
        {
            _momentumHoursPerSec = hoursPerSec;
        }
    }
}
