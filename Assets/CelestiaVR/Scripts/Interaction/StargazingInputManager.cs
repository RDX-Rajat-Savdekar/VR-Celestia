using UnityEngine;
using UnityEngine.InputSystem;
using CelestiaVR.UI;
using CelestiaVR.Interaction;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Centralised controller input for the stargazing experience.
    ///
    /// Button map (Meta Quest):
    /// ─────────────────────────────────────────────────────────────
    /// Left  X (primaryButton)   → Toggle search panel open/close
    /// Left  Y (secondaryButton) → (reserved — currently unused)
    /// Left  thumbstick X        → Time scroll  (handled by TimeScrollController)
    /// Left  thumbstick click    → Viewing mode toggle (ViewingModeManager)
    /// Left  grip                → Time scroll fast (handled by TimeScrollController)
    ///
    /// Right A (primaryButton)   → Select / call hologram (SelectionManager _buttonSelectAction)
    /// Right B (secondaryButton) → Dismiss inspection (SelectionManager dismissAction)
    /// Right trigger             → Select / inspect (SelectionManager selectAction — Inspector)
    /// Right thumbstick X        → Time scroll  (handled by TimeScrollController)
    /// Right thumbstick click    → Viewing mode toggle (ViewingModeManager)
    /// ─────────────────────────────────────────────────────────────
    ///
    /// This component owns bindings that are NOT already owned by SelectionManager,
    /// ViewingModeManager, or TimeScrollController.
    ///
    /// Auto-created by StargazingSceneBootstrap — no manual wiring needed.
    /// </summary>
    public class StargazingInputManager : MonoBehaviour
    {
        public static StargazingInputManager Instance { get; private set; }

        // ── Runtime actions ───────────────────────────────────────────────────────

        private InputAction _searchToggle;  // Left X button

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Left X button — toggle search panel
            _searchToggle = new InputAction("SearchToggle", InputActionType.Button);
            _searchToggle.AddBinding("<XRController>{LeftHand}/primaryButton");   // X on Quest
            _searchToggle.AddBinding("<XRController>{LeftHand}/secondaryButton"); // Y fallback
            _searchToggle.performed += _ => ToggleSearch();
            _searchToggle.Enable();
        }

        private void OnDestroy()
        {
            _searchToggle?.Disable();
            _searchToggle?.Dispose();
        }

        // ── Handlers ──────────────────────────────────────────────────────────────

        private void ToggleSearch()
        {
            if (ControlPanel.Instance != null)
                ControlPanel.Instance.ToggleOpen();
        }
    }
}
