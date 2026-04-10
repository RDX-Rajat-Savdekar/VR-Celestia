using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Global toggle between two sky-viewing modes.
    ///
    /// OBSERVE  — gaze around freely; dwell highlight ring still shows so you know
    ///            what you're looking at, but no hologram pops out.
    /// INSPECT  — full dwell: 3-second gaze pulls the object out as a hologram
    ///            with the info panel and real-scale comparison.
    ///
    /// Switching:
    ///   • Press M on keyboard (simulator)
    ///   • Left controller thumbstick press (XRI default: LeftHand/Primary2DAxisClick)
    ///   • OR gaze at the floating badge and hold for 1.5 s (dwell-on-badge)
    ///
    /// Auto-created by StargazingSceneBootstrap. No manual scene setup needed.
    /// </summary>
    public class ViewingModeManager : MonoBehaviour
    {
        public static ViewingModeManager Instance { get; private set; }

        public enum Mode { Observe, Inspect }

        [Header("Default")]
        public Mode startingMode = Mode.Observe;

        [Header("Input — optional controller action")]
        [Tooltip("Assign 'LeftHand / Primary2DAxisClick' XRI action for thumbstick-press toggle. Leave empty — auto-bound to left thumbstick click.")]
        public InputActionReference toggleAction;

        [Header("Badge HUD")]
        [Tooltip("Distance in front of camera the badge appears (metres).")]
        public float badgeDistance = 1.8f;
        [Tooltip("Degrees below camera forward the badge sits.")]
        public float badgePitchDown = 25f;
        [Tooltip("Degrees right of camera forward.")]
        public float badgeYawRight = 28f;

        // ── Events ────────────────────────────────────────────────────────────────
        public event Action<Mode> OnModeChanged;
        public Mode CurrentMode  { get; private set; }
        public bool IsInspectMode => CurrentMode == Mode.Inspect;

        // ── Runtime ───────────────────────────────────────────────────────────────
        private Camera      _cam;
        private GameObject  _badgeGO;
        private TextMeshPro _badgeTMP;
        private InputAction _controllerToggle; // auto-bound thumbstick click

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            CurrentMode = startingMode;
        }

        private void Start()
        {
            _cam = Camera.main;
            BuildBadge();
            RefreshBadge();

            if (toggleAction != null)
                toggleAction.action.performed += _ => Toggle();

            // Auto-bind left OR right thumbstick click — no inspector wiring needed.
            // Works on Meta Quest 3 without assigning an InputActionReference.
            _controllerToggle = new InputAction("ViewModeToggle", InputActionType.Button);
            _controllerToggle.AddBinding("<XRController>{LeftHand}/thumbstickClicked");
            _controllerToggle.AddBinding("<XRController>{LeftHand}/Primary2DAxisClick");
            _controllerToggle.AddBinding("<XRController>{RightHand}/thumbstickClicked");
            _controllerToggle.AddBinding("<XRController>{RightHand}/Primary2DAxisClick");
            _controllerToggle.performed += _ => Toggle();
            _controllerToggle.Enable();
        }

        private void OnDestroy()
        {
            if (toggleAction != null)
                toggleAction.action.performed -= _ => Toggle();

            _controllerToggle?.Disable();
            _controllerToggle?.Dispose();
        }

        private void Update()
        {
            // Keyboard shortcut for simulator
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                Toggle();

            // Keep badge anchored in HUD position
            MoveBadge();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void SetMode(Mode mode)
        {
            if (mode == CurrentMode) return;
            CurrentMode = mode;
            RefreshBadge();
            OnModeChanged?.Invoke(mode);
            Debug.Log($"[ViewingMode] → {mode}");
        }

        public void Toggle() => SetMode(IsInspectMode ? Mode.Observe : Mode.Inspect);

        // ── Badge ─────────────────────────────────────────────────────────────────

        private void BuildBadge()
        {
            _badgeGO = new GameObject("[ModeHUD]");

            _badgeTMP = _badgeGO.AddComponent<TextMeshPro>();
            _badgeTMP.fontSize       = 5f;
            _badgeTMP.alignment      = TextAlignmentOptions.Center;
            _badgeTMP.enableWordWrapping = false;

            // Outline for sky contrast
            _badgeTMP.outlineWidth  = 0.3f;
            _badgeTMP.outlineColor  = new Color(0, 0, 0, 0.8f);

            // fontSize=5 at scale=1 → ~0.5m tall; target ~0.03m (3cm) at 1.8m distance
            // scale = 0.03 / 0.5 = 0.06
            _badgeGO.transform.localScale = Vector3.one * 0.06f;
        }

        private void RefreshBadge()
        {
            if (_badgeTMP == null) return;

            if (IsInspectMode)
            {
                _badgeTMP.text  = "[ INSPECT ]";
                _badgeTMP.color = new Color(0.3f, 0.8f, 1f, 1f);
            }
            else
            {
                _badgeTMP.text  = "[ OBSERVE ]";
                _badgeTMP.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            }
        }

        private void MoveBadge()
        {
            if (_badgeGO == null || _cam == null) return;

            // Compute direction from camera: yaw right then pitch down
            Quaternion yaw   = Quaternion.AngleAxis(badgeYawRight,  _cam.transform.up);
            Quaternion pitch = Quaternion.AngleAxis(badgePitchDown, _cam.transform.right);
            Vector3 dir = (yaw * pitch) * _cam.transform.forward;

            _badgeGO.transform.position = _cam.transform.position + dir * badgeDistance;
            _badgeGO.transform.rotation = Quaternion.LookRotation(
                _badgeGO.transform.position - _cam.transform.position);
        }
    }
}
