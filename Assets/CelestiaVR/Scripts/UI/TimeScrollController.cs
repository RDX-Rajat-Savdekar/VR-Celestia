using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using CelestiaVR.Core;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Scrolls simulated time automatically and/or via VR thumbstick.
    ///
    /// In the editor / without a headset: time auto-scrolls at autoScrollHoursPerSecond.
    /// On Quest: RIGHT thumbstick X scrubs time; right grip = 24x multiplier.
    /// Left thumbstick is reserved for movement (ContinuousMoveProvider).
    /// </summary>
    public class TimeScrollController : MonoBehaviour
    {
        [Header("Auto-Scroll (testing)")]
        [Tooltip("Hours of in-game time per real second when no thumbstick input.")]
        public float autoScrollHoursPerSecond = 2f;

        [Header("VR Thumbstick")]
        [Tooltip("Hours per second at full thumbstick deflection.")]
        public float hoursPerSecondAtFullTilt = 1f;
        [Tooltip("Speed multiplier while grip is held.")]
        public float gripSpeedMultiplier = 24f;

        [Header("Inertia")]
        [Range(0.5f, 20f)]
        public float inertiaDamping = 4f;
        public float momentumDeadzone = 0.001f;

        [Range(0f, 0.5f)]
        public float thumbstickDeadzone = 0.15f;

        private float _momentumHoursPerSec = 0f;
        private List<InputDevice> _rightHand = new List<InputDevice>();
        private List<InputDevice> _leftHand  = new List<InputDevice>();

        private void Start()
        {
            _momentumHoursPerSec = autoScrollHoursPerSecond;
            RefreshDevices();
        }

        private void Update()
        {
            if (SkyManager.Instance == null) return;

            RefreshDevicesIfNeeded();

            float thumbstickX = GetThumbstickX();
            bool  gripHeld    = GetGripHeld();

            if (Mathf.Abs(thumbstickX) > thumbstickDeadzone)
            {
                float target = thumbstickX * hoursPerSecondAtFullTilt * (gripHeld ? gripSpeedMultiplier : 1f);
                _momentumHoursPerSec = Mathf.Lerp(_momentumHoursPerSec, target, Time.deltaTime * 10f);
            }
            else
            {
                // No thumbstick — hold at auto-scroll rate (no decay)
                _momentumHoursPerSec = autoScrollHoursPerSecond;
            }

            if (_momentumHoursPerSec != 0f)
                SkyManager.Instance.OffsetSimulatedTime(_momentumHoursPerSec * 3600.0 * Time.deltaTime);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private bool _devicesValid = false;

        private void RefreshDevices()
        {
            _rightHand.Clear();
            _leftHand.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, _rightHand);
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Left  | InputDeviceCharacteristics.Controller, _leftHand);
            _devicesValid = _rightHand.Count > 0 || _leftHand.Count > 0;
        }

        private void RefreshDevicesIfNeeded()
        {
            if (!_devicesValid) RefreshDevices();
        }

        private float GetThumbstickX()
        {
            // Right hand only — left thumbstick is reserved for locomotion movement.
            foreach (var d in _rightHand)
                if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 a)) return a.x;
            return 0f;
        }

        private bool GetGripHeld()
        {
            // Right grip = fast mode. Left grip intentionally not used here.
            foreach (var d in _rightHand)
                if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool v) && v) return true;
            return false;
        }

        /// <summary>Set to 0 to pause, non-zero to resume from UI buttons.</summary>
        public void SetAutoScrollSpeed(float hoursPerSec) => autoScrollHoursPerSecond = hoursPerSec;
    }
}
