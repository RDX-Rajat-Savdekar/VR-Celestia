using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using CelestiaVR.Core;
using CelestiaVR.UI;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Handles all interaction with the CelestialSearchPanel and its floating trigger button.
    ///
    /// TWO modes depending on whether the panel is open:
    ///
    ///   PANEL CLOSED — gaze SphereCast from camera forward.
    ///     • Dwell 2.5 s on the "Search Sky" trigger button → opens panel.
    ///
    ///   PANEL OPEN — right controller ray (like a laser pointer).
    ///     • Point at any item row → right trigger press selects it instantly,
    ///       OR hold gaze 1.5 s (fallback for sim).
    ///     • Point at trigger button → right trigger or 2.5 s dwell → closes panel.
    ///     • A thin blue LineRenderer shows the controller ray while panel is open.
    ///
    /// Selecting an item activates DirectionalArrow.SetTarget() and closes the panel.
    ///
    /// Auto-added by StargazingSceneBootstrap — no manual wiring needed.
    /// </summary>
    public class SearchPanelDwellDetector : MonoBehaviour
    {
        [Header("Dwell settings")]
        public float triggerDwellTime = 2.5f;
        public float itemDwellTime    = 1.5f;   // shorter when using controller ray

        [Header("Gaze ray (panel closed)")]
        public float castRadius = 0.12f;
        public float castRange  = 800f;
        public LayerMask layerMask = ~0;

        // ── Runtime ──────────────────────────────────────────────────────────────

        private Camera _cam;

        // XR right-hand device for trigger + position reads
        private readonly List<InputDevice> _rightDevices = new List<InputDevice>();
        private bool _devicesValid;
        private bool _triggerWasDown; // edge-detect for trigger press

        // Controller ray visual (only active when panel is open)
        private LineRenderer _rayLine;

        // Current hover state
        private SearchPanelTrigger _hoveredTrigger;
        private SearchItemCollider _hoveredItem;
        private float              _dwellTimer;
        private Renderer           _hoveredRenderer;
        private Color              _hoveredBaseColor;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Start()
        {
            _cam = Camera.main;
            BuildRayLine();
            RefreshDevices();
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            RefreshDevicesIfNeeded();

            bool panelOpen = CelestialSearchPanel.Instance != null
                          && CelestialSearchPanel.Instance.IsOpen;

            // Choose ray source
            Ray ray;
            if (panelOpen && TryGetControllerRay(out ray))
                UpdateControllerRay(ray);   // show blue laser
            else
            {
                HideRayLine();
                ray = new Ray(_cam.transform.position, _cam.transform.forward);
            }

            // Sphere-cast (gaze) or line-cast (controller)
            SearchPanelTrigger hitTrigger = null;
            SearchItemCollider hitItem    = null;
            Vector3            hitPoint   = ray.origin + ray.direction * 4f;

            if (panelOpen && TryGetControllerRay(out _))
            {
                // Precise line-cast from controller for panel interaction
                if (Physics.Raycast(ray, out RaycastHit hit, castRange, layerMask))
                {
                    hitPoint   = hit.point;
                    hitTrigger = hit.collider.GetComponent<SearchPanelTrigger>();
                    if (hitTrigger == null)
                        hitItem = hit.collider.GetComponent<SearchItemCollider>();
                    if (hitItem != null && !hitItem.isActive) hitItem = null;
                }
            }
            else
            {
                // Gaze sphere-cast for the floating trigger button
                if (Physics.SphereCast(ray, castRadius, out RaycastHit hit, castRange, layerMask))
                {
                    hitPoint   = hit.point;
                    hitTrigger = hit.collider.GetComponent<SearchPanelTrigger>();
                    if (hitTrigger == null)
                        hitItem = hit.collider.GetComponent<SearchItemCollider>();
                    if (hitItem != null && !hitItem.isActive) hitItem = null;
                }
            }

            // Update ray line end-point
            if (_rayLine != null && _rayLine.enabled)
                _rayLine.SetPosition(1, hitPoint);

            // Track gaze/controller target changes
            bool sameTarget = (hitTrigger == _hoveredTrigger && hitItem == _hoveredItem);
            if (!sameTarget)
            {
                ClearHover();
                _hoveredTrigger = hitTrigger;
                _hoveredItem    = hitItem;
                _dwellTimer     = 0f;
                if (_hoveredItem    != null) StartHoverHighlight(_hoveredItem.gameObject);
                else if (_hoveredTrigger != null) StartHoverHighlight(_hoveredTrigger.gameObject);
            }

            if (hitTrigger == null && hitItem == null)
            {
                _triggerWasDown = GetRightTrigger();
                return;
            }

            // ── Trigger press: instant selection ─────────────────────────────────
            bool triggerDown = GetRightTrigger();
            bool triggerPressed = triggerDown && !_triggerWasDown;
            _triggerWasDown = triggerDown;

            if (triggerPressed)
            {
                Activate(hitTrigger, hitItem);
                return;
            }

            // ── Dwell fallback ────────────────────────────────────────────────────
            float required = (hitTrigger != null) ? triggerDwellTime : itemDwellTime;
            _dwellTimer += Time.deltaTime;
            UpdateHoverHighlight(_dwellTimer / required);

            if (_dwellTimer >= required)
            {
                _dwellTimer = 0f;
                Activate(hitTrigger, hitItem);
            }
        }

        // ── Activation ────────────────────────────────────────────────────────────

        private void Activate(SearchPanelTrigger trigger, SearchItemCollider item)
        {
            if (trigger != null)
            {
                trigger.panel?.ToggleOpen();
            }
            else if (item != null && item.target != null)
            {
                DirectionalArrow.Instance?.SetTarget(item.target);
                CelestialSearchPanel.Instance?.Close();
            }
            ClearHover();
        }

        // ── Controller ray ────────────────────────────────────────────────────────

        private bool TryGetControllerRay(out Ray ray)
        {
            ray = default;
            foreach (var d in _rightDevices)
            {
                if (d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos) &&
                    d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                {
                    // Controller gives tracking-space pose; transform to world space via camera rig
                    Transform rig = _cam.transform.parent?.parent ?? _cam.transform.parent;
                    if (rig != null)
                    {
                        pos = rig.TransformPoint(pos);
                        rot = rig.rotation * rot;
                    }
                    ray = new Ray(pos, rot * Vector3.forward);
                    return true;
                }
            }
            return false;
        }

        private void UpdateControllerRay(Ray ray)
        {
            if (_rayLine == null) return;
            _rayLine.enabled = true;
            _rayLine.SetPosition(0, ray.origin);
            _rayLine.SetPosition(1, ray.origin + ray.direction * 4f); // updated to hit point in main loop
        }

        private void HideRayLine()
        {
            if (_rayLine != null) _rayLine.enabled = false;
        }

        // ── Trigger input ─────────────────────────────────────────────────────────

        private bool GetRightTrigger()
        {
            foreach (var d in _rightDevices)
                if (d.TryGetFeatureValue(CommonUsages.triggerButton, out bool v) && v) return true;
            return false;
        }

        // ── XR Devices ───────────────────────────────────────────────────────────

        private void RefreshDevices()
        {
            _rightDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, _rightDevices);
            _devicesValid = _rightDevices.Count > 0;
        }

        private void RefreshDevicesIfNeeded()
        {
            if (!_devicesValid) RefreshDevices();
        }

        // ── Ray line visual ───────────────────────────────────────────────────────

        private void BuildRayLine()
        {
            var go = new GameObject("SearchRayLine");
            go.transform.SetParent(null);
            _rayLine = go.AddComponent<LineRenderer>();

            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            _rayLine.material     = mat;
            _rayLine.startWidth   = 0.004f;
            _rayLine.endWidth     = 0.002f;
            _rayLine.positionCount = 2;
            _rayLine.useWorldSpace = true;
            _rayLine.enabled      = false;
        }

        // ── Hover highlight ───────────────────────────────────────────────────────

        private void StartHoverHighlight(GameObject go)
        {
            _hoveredRenderer = go.GetComponentInChildren<Renderer>();
            if (_hoveredRenderer != null)
                _hoveredBaseColor = _hoveredRenderer.material.color;
        }

        private void UpdateHoverHighlight(float t)
        {
            if (_hoveredRenderer == null) return;
            _hoveredRenderer.material.color = Color.Lerp(
                _hoveredBaseColor, new Color(0.3f, 0.85f, 1f, 1f), t);
        }

        private void ClearHover()
        {
            if (_hoveredRenderer != null)
                _hoveredRenderer.material.color = _hoveredBaseColor;
            _hoveredRenderer = null;
            _hoveredTrigger  = null;
            _hoveredItem     = null;
        }
    }
}
