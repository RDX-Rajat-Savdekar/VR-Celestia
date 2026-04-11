using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using CelestiaVR.Core;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Automated test/demo that cycles through every ControlPanel button in sequence.
    ///
    /// Usage (simulator testing):
    ///   • Attach to any persistent GO, or let Bootstrap auto-create it via
    ///     [Add Component] in the scene.
    ///   • Press SPACE (or set autoStart=true) to begin the sequence.
    ///
    /// Tutorial reuse:
    ///   The same sequence can drive a guided intro:
    ///   set isTutorial=true to show instructional overlay text instead of
    ///   "TEST:" prefixed HUD messages.
    ///
    /// Auto-waits for ControlPanel to finish building (up to 6 s) before starting.
    /// </summary>
    public class ControlPanelTester : MonoBehaviour
    {
        [Header("Behaviour")]
        [Tooltip("Start immediately on Start() instead of waiting for SPACE.")]
        public bool autoStart = false;

        [Tooltip("When true, HUD messages read as tutorial instructions.")]
        public bool isTutorial = false;

        [Tooltip("Seconds between each step.")]
        public float stepDelay = 2.0f;

        [Tooltip("Name of the celestial body to search for in the demo.")]
        public string searchTargetName = "Venus";

        // ── Runtime ───────────────────────────────────────────────────────────────

        private TextMeshPro _hud;
        private bool        _running;
        private InputAction _spaceAction;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Start()
        {
            _spaceAction = new InputAction("TesterTrigger", InputActionType.Button);
            _spaceAction.AddBinding("<Keyboard>/space");
            _spaceAction.Enable();

            BuildHud();
            if (autoStart)
                StartTest();
            else
                ShowHud(isTutorial ? "Welcome! Press SPACE to begin the tour."
                                   : "[ControlPanelTester] Press SPACE to run.");
        }

        private void OnDestroy()
        {
            _spaceAction?.Disable();
            _spaceAction?.Dispose();
        }

        private void Update()
        {
            if (!_running && _spaceAction != null && _spaceAction.WasPressedThisFrame())
                StartTest();
        }

        private void LateUpdate()
        {
            // Keep HUD anchored to camera each frame
            if (_hud == null || !_hud.gameObject.activeSelf) return;
            var cam = Camera.main;
            if (cam == null) return;
            _hud.transform.position =
                cam.transform.position
                + cam.transform.forward * 1.8f
                + cam.transform.up      * 0.35f;
            _hud.transform.rotation = Quaternion.LookRotation(
                _hud.transform.position - cam.transform.position);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void StartTest() => StartCoroutine(RunSequence());

        // ── Sequence ──────────────────────────────────────────────────────────────

        private IEnumerator RunSequence()
        {
            _running = true;

            // Wait until ControlPanel is built (spawners take ~3.5 s)
            ShowHud("Waiting for ControlPanel to build…");
            float waited = 0f;
            while (ControlPanel.Instance == null || !ControlPanel.Instance.IsOpen && waited < 6f)
            {
                // Panel is built but closed — that's fine, proceed
                if (ControlPanel.Instance != null && !ControlPanel.Instance.IsOpen && waited > 0.5f) break;
                yield return new WaitForSeconds(0.2f);
                waited += 0.2f;
            }
            yield return new WaitForSeconds(0.5f);

            var cp = ControlPanel.Instance;
            if (cp == null) { ShowHud("ControlPanel not found — aborting."); _running = false; yield break; }

            // ── Step 1: Open panel ────────────────────────────────────────────────
            yield return Step("Opening control panel…",
                () => { if (!cp.IsOpen) cp.Open(); });

            // ── Step 2: Set Observe mode ──────────────────────────────────────────
            yield return Step(isTutorial ? "This is OBSERVE mode — just look around."
                                         : "TEST: SetObserveMode",
                () => cp.SimulatePress(ControlPanel.ButtonAction.SetObserveMode));

            // ── Step 3: Set Inspect mode ──────────────────────────────────────────
            yield return Step(isTutorial ? "Switch to INSPECT mode to dwell on objects."
                                         : "TEST: SetInspectMode",
                () => cp.SimulatePress(ControlPanel.ButtonAction.SetInspectMode));

            // ── Step 4: Toggle constellation lines off ────────────────────────────
            yield return Step(isTutorial ? "Toggling constellation lines off…"
                                         : "TEST: ToggleConstellationLines (off)",
                () => cp.SimulatePress(ControlPanel.ButtonAction.ToggleConstellationLines));

            // ── Step 5: Toggle constellation lines back on ────────────────────────
            yield return Step(isTutorial ? "…and back on."
                                         : "TEST: ToggleConstellationLines (on)",
                () => cp.SimulatePress(ControlPanel.ButtonAction.ToggleConstellationLines));

            // ── Step 6: Toggle constellation art off ─────────────────────────────
            yield return Step(isTutorial ? "Toggling constellation art off…"
                                         : "TEST: ToggleConstellationArt (off)",
                () => cp.SimulatePress(ControlPanel.ButtonAction.ToggleConstellationArt));

            // ── Step 7: Toggle constellation art back on ─────────────────────────
            yield return Step(isTutorial ? "…and back on."
                                         : "TEST: ToggleConstellationArt (on)",
                () => cp.SimulatePress(ControlPanel.ButtonAction.ToggleConstellationArt));

            // ── Step 8: Toggle planet labels off ─────────────────────────────────
            yield return Step(isTutorial ? "Hiding planet labels…"
                                         : "TEST: TogglePlanetLabels (off)",
                () => cp.SimulatePress(ControlPanel.ButtonAction.TogglePlanetLabels));

            // ── Step 9: Toggle planet labels back on ─────────────────────────────
            yield return Step(isTutorial ? "…showing them again."
                                         : "TEST: TogglePlanetLabels (on)",
                () => cp.SimulatePress(ControlPanel.ButtonAction.TogglePlanetLabels));

            // ── Step 10: Select a search target ───────────────────────────────────
            var targets = cp.GetSearchTargets();
            CelestialBody pick = targets.Find(b =>
                b.objectName.ToLowerInvariant().Contains(searchTargetName.ToLowerInvariant()));
            if (pick == null && targets.Count > 0) pick = targets[0]; // fallback

            if (pick != null)
            {
                // Re-open panel (SelectSearchItem closes it)
                if (!cp.IsOpen) cp.Open();
                yield return new WaitForSeconds(0.4f);

                yield return Step(isTutorial ? $"Let's find {pick.objectName}!"
                                             : $"TEST: SelectSearchItem → {pick.objectName}",
                    () => cp.SimulatePress(ControlPanel.ButtonAction.SelectSearchItem, pick));
            }
            else
            {
                ShowHud($"No search target named '{searchTargetName}' found — skipping.");
                yield return new WaitForSeconds(stepDelay);
            }

            // ── Step 11: Close panel ──────────────────────────────────────────────
            // Panel may already be closed by SelectSearchItem — reopen to test Close button
            if (!cp.IsOpen) { cp.Open(); yield return new WaitForSeconds(0.4f); }

            yield return Step(isTutorial ? "Closing the panel. Press X to reopen."
                                         : "TEST: Close",
                () => cp.SimulatePress(ControlPanel.ButtonAction.Close));

            // ── Done ──────────────────────────────────────────────────────────────
            yield return new WaitForSeconds(stepDelay);
            ShowHud(isTutorial ? "Tour complete! Enjoy the stars."
                               : "All ControlPanel buttons tested.");

            yield return new WaitForSeconds(3f);
            HideHud();
            _running = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private IEnumerator Step(string message, System.Action action)
        {
            ShowHud(message);
            yield return new WaitForSeconds(0.15f); // brief pause so HUD is readable first
            action?.Invoke();
            yield return new WaitForSeconds(stepDelay);
        }

        // ── HUD ───────────────────────────────────────────────────────────────────

        private void BuildHud()
        {
            var go = new GameObject("ControlPanelTester_HUD");
            go.transform.SetParent(transform);
            go.transform.localScale = Vector3.one * 0.045f;
            _hud = go.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null)
                _hud.font = TMP_Settings.defaultFontAsset;
            _hud.fontSize        = 7f;
            _hud.color           = new Color(0.9f, 1f, 0.5f, 1f);
            _hud.alignment       = TextAlignmentOptions.Center;
            _hud.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _hud.outlineWidth    = 0.20f;
            _hud.outlineColor    = new Color(0f, 0f, 0f, 0.85f);
            _hud.sortingOrder    = 20;
            _hud.rectTransform.sizeDelta = new Vector2(200f, 40f);
            _hud.gameObject.SetActive(false);
        }

        private void ShowHud(string msg)
        {
            if (_hud == null) return;
            _hud.text = msg;
            _hud.gameObject.SetActive(true);
        }

        private void HideHud()
        {
            if (_hud != null) _hud.gameObject.SetActive(false);
        }
    }
}
