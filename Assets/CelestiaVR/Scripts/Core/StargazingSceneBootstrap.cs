using UnityEngine;
using UnityEngine.Rendering;
using CelestiaVR.Interaction;
using CelestiaVR.Stars;
using CelestiaVR.UI;

namespace CelestiaVR.Core
{
    /// <summary>
    /// Quick-start helper — wires up system references in Play mode and enforces
    /// the correct render settings for a night-sky VR scene.
    ///
    /// Attach to any persistent root GameObject in StargazingScene.
    /// </summary>
    public class StargazingSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            FixRenderSettings();
            DisableSceneLights();
            FixCamera();
            WireInteraction();
            EnsureSkyLabels();
            EnsureViewingMode();

            Debug.Log("[StargazingSceneBootstrap] Scene wired up.");
        }

        /// <summary>
        /// Fog at density 0.05 (ExponentialSquared) completely covers the sky sphere
        /// at radius 500. Ambient in Skybox mode with no skybox defaults to a bright
        /// grey — both must be corrected before the scene looks right.
        /// </summary>
        private void FixRenderSettings()
        {
            // Fog buries the SkySphere. Turn it off — space has no fog.
            RenderSettings.fog = false;

            // Switch ambient to Flat so DayNightController can drive it via
            // RenderSettings.ambientLight. In Skybox mode that setter is ignored.
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.04f, 0.08f); // near-black night

            // No skybox material — Unity falls back to a grey procedural sky background.
            // Camera is already set to SolidColor/black, so just ensure no skybox leaks.
            RenderSettings.skybox = null;
        }

        /// <summary>
        /// The scene has two baked directional lights (both intensity 1) left over from
        /// the VR template. SunController creates and manages its own directional light,
        /// so disable all pre-existing ones to avoid double-illumination.
        /// </summary>
        private void DisableSceneLights()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                // "SunDirectionalLight" is created by SunController — leave it alone.
                if (l.name == "SunDirectionalLight") continue;
                l.enabled = false;
                Debug.Log($"[StargazingSceneBootstrap] Disabled pre-existing directional light: {l.name}");
            }
        }

        /// <summary>Camera must clear to solid black so the SkySphere isn't contaminated
        /// by any Unity default sky colour.</summary>
        private void FixCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = Color.black;
        }

        private void WireInteraction()
        {
            var dwell = FindFirstObjectByType<DwellSelector>();
            if (dwell != null && dwell.gazeCamera == null)
                dwell.gazeCamera = Camera.main;

            // Auto-add BillboardStarDwellDetector on the same GO as DwellSelector
            // so all GPU-instanced billboard stars are also dwellable.
            if (dwell != null && dwell.GetComponent<BillboardStarDwellDetector>() == null)
            {
                dwell.gameObject.AddComponent<BillboardStarDwellDetector>();
                Debug.Log("[Bootstrap] Auto-added BillboardStarDwellDetector to " + dwell.gameObject.name);
            }

            var selMgr    = FindFirstObjectByType<SelectionManager>();
            var inspector = FindFirstObjectByType<InspectionController>();
            if (selMgr != null && inspector != null && inspector.selectionManager == null)
                inspector.selectionManager = selMgr;

            // ConstellationHIPRenderer auto-wires itself but needs SkyManager reference
            // (handled in its own Awake — nothing to do here)

            // InspectionPanel must NEVER be inactive — its coroutines break on inactive GOs.
            // Force-activate any InspectionPanel found in the scene.
            var panel = FindFirstObjectByType<InspectionPanel>(FindObjectsInactive.Include);
            if (panel != null && !panel.gameObject.activeInHierarchy)
            {
                panel.gameObject.SetActive(true);
                Debug.Log("[Bootstrap] Force-activated InspectionPanel on " + panel.gameObject.name);
            }
        }

        private void EnsureSkyLabels()
        {
            // Auto-add SkyLabelManager if not already in the scene
            if (FindFirstObjectByType<SkyLabelManager>() == null)
            {
                var go = new GameObject("[SkyLabelManager]");
                go.AddComponent<SkyLabelManager>();
                Debug.Log("[Bootstrap] Auto-created SkyLabelManager.");
            }
        }

        private void EnsureViewingMode()
        {
            // Auto-add ViewingModeManager singleton if not already in the scene
            if (FindFirstObjectByType<ViewingModeManager>() == null)
            {
                var go = new GameObject("[ViewingModeManager]");
                go.AddComponent<ViewingModeManager>();
                Debug.Log("[Bootstrap] Auto-created ViewingModeManager (Observe mode by default, press M to toggle).");
            }
        }
    }
}
