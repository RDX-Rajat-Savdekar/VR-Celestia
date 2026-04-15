using System;
using UnityEngine;
using UnityEngine.InputSystem;
using CelestiaVR.Core;
using CelestiaVR.Audio;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Handles controller-based selection of celestial objects.
    /// Listens for the XRI trigger action, raycasts from the right controller,
    /// and fires OnObjectSelected when a CelestialBody is hit.
    ///
    /// Attach to XR Origin or a dedicated SelectionManager GameObject.
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Right controller trigger select action (XRI Default / RightHand/Select).")]
        public InputActionReference selectAction;

        [Tooltip("Button to dismiss the current inspection (B / Y button). Leave empty to disable.")]
        public InputActionReference dismissAction;

        [Header("Raycast")]
        [Tooltip("Transform used as raycast origin (right controller attach point).")]
        public Transform rayOrigin;
        public float maxRayDistance = 600f;
        public LayerMask celestialLayers = ~0;

        [Header("Visual Ray")]
        public LineRenderer rayVisual;
        [Range(0.01f, 0.5f)]
        public float rayWidth = 0.02f;

        public event Action<CelestialBody> OnObjectSelected;
        public event Action OnDeselect;

        private CelestialBody _selectedBody;
        private DwellSelector _dwellSelector;

        private void Awake()
        {
            _dwellSelector = FindFirstObjectByType<DwellSelector>();
            if (_dwellSelector != null)
            {
                _dwellSelector.OnDwellSelect += HandleDwellSelect;
            }
            else
            {
                Debug.LogWarning("[SelectionManager] No DwellSelector found — gaze-based selection disabled.");
            }
        }

        private void OnDestroy()
        {
            if (_dwellSelector != null)
                _dwellSelector.OnDwellSelect -= HandleDwellSelect;
        }

        private void HandleDwellSelect(CelestialBody body)
        {
            // In Observe mode the user is just looking around — dwell triggers nothing.
            var modeMgr = ViewingModeManager.Instance;
            if (modeMgr != null && !modeMgr.IsInspectMode)
                return;
            if (_selectedBody != null && _selectedBody.isInspecting)
            {
                OnDeselect?.Invoke();
                _selectedBody = null;
                return;
            }
            _selectedBody = body;
            OnObjectSelected?.Invoke(body);
        }

        private void OnEnable()
        {
            if (selectAction != null)
                selectAction.action.performed += HandleSelectPerformed;
            if (dismissAction != null)
                dismissAction.action.performed += HandleDismissPerformed;
        }

        private void OnDisable()
        {
            if (selectAction != null)
                selectAction.action.performed -= HandleSelectPerformed;
            if (dismissAction != null)
                dismissAction.action.performed -= HandleDismissPerformed;
        }

        private void HandleDismissPerformed(InputAction.CallbackContext ctx)
        {
            if (_selectedBody == null) return;
            SoundManager.Instance?.Play(SoundEvent.Deselect);
            OnDeselect?.Invoke();
            _selectedBody = null;
        }

        private void Update()
        {
            UpdateRayVisual();
        }

        private void HandleSelectPerformed(InputAction.CallbackContext ctx)
        {
            // If something is already inspecting, treat as deselect
            if (_selectedBody != null && _selectedBody.isInspecting)
            {
                OnDeselect?.Invoke();
                _selectedBody = null;
                return;
            }

            // Try dwell target first (prioritize what user is looking at)
            CelestialBody target = _dwellSelector != null ? _dwellSelector.CurrentTarget : null;

            // Fall back to controller raycast
            if (target == null && rayOrigin != null)
            {
                Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, celestialLayers))
                    target = hit.collider.GetComponentInParent<CelestialBody>();
            }

            if (target != null)
            {
                _selectedBody = target;
                // Only play select sound here for trigger-based selection.
                // Dwell-based selection already plays the sound in DwellSelector.
                SoundManager.Instance?.Play(SoundEvent.Select, target.transform.position);
                OnObjectSelected?.Invoke(target);
            }
        }

        private void UpdateRayVisual()
        {
            if (rayVisual == null || rayOrigin == null) return;

            rayVisual.startWidth = rayWidth;
            rayVisual.endWidth = rayWidth * 0.1f;

            CelestialBody hovered = _dwellSelector != null ? _dwellSelector.CurrentTarget : null;
            rayVisual.enabled = hovered != null;

            if (hovered != null)
            {
                rayVisual.SetPosition(0, rayOrigin.position);
                rayVisual.SetPosition(1, hovered.transform.position);
            }
        }
    }
}
