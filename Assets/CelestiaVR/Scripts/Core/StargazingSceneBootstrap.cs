using UnityEngine;
using UnityEngine.Rendering;
using CelestiaVR.Interaction;
using CelestiaVR.Stars;
using CelestiaVR.UI;
using CelestiaVR.Constellations;
using CelestiaVR.Island;
using CelestiaVR.Audio;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

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
            DisableGazeInteractor();
            DisableTurnAndTeleport();
            DisableXRInteractorVisuals();
            DisableAffordanceSystem();
            DisableGazeDebugger();
            DisableLegacyConstellationRenderers();
            WireInteraction();
            EnsureSkyLabels();
            EnsureViewingMode();
            EnsureDirectionalArrow();
            EnsureSearchSystem();
            EnsureInputManager();
            EnsureSoundManager();
            EnsureFireplaceMiniGame();
            EnsureIslandFloor();
            EnsureIslandBoundaryWalls();
            EnsureOnboarding();

            Debug.Log("[Bootstrap] Scene ready.");
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

        private void Awake()
        {
            // Destroy any DontDestroyOnLoad overlay left behind by DoorPortal (fade canvas)
            var fade = GameObject.Find("[PortalFade]");
            if (fade != null) Destroy(fade);
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
            }
        }

        /// <summary>Camera must clear to solid black so the SkySphere isn't contaminated
        /// by any Unity default sky colour.</summary>
        private void FixCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
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
            }
        }

        /// <summary>
        /// Removes rotation (snap/continuous turn) and teleportation from the XR Rig.
        /// This is a stationary stargazing experience — the user looks around with their head,
        /// not by thumbstick turning. Right thumbstick is repurposed for time scroll.
        /// The red teleport arc that appears on thumbstick-up is suppressed here.
        /// </summary>
        /// <summary>
        /// The XR Starter Assets GazeInteractor shows a small reticle circle in the
        /// simulator. We use our own DwellSelector, so disable the XR one entirely.
        /// </summary>
        private void DisableGazeInteractor()
        {
            // Disable by GazeInputManager component (Starter Assets)
            foreach (var gim in FindObjectsByType<
                UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.GazeInputManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                gim.gameObject.SetActive(false);
            }

            // Also catch any GO with "Gaze" in the name that has a renderer (the reticle)
            foreach (var t in FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.Contains("Gaze") && !t.name.Contains("gaze")) continue;
                var r = t.GetComponent<Renderer>();
                if (r != null) { r.enabled = false; }
                // Disable the GO if it's purely the gaze interactor
                if (t.name.Contains("Gaze Interactor") || t.name.Contains("Eye Gaze"))
                    t.gameObject.SetActive(false);
            }
        }

        private void DisableTurnAndTeleport()
        {
            // Snap turn — right thumbstick X rotates rig. We want that axis for time scroll.
            foreach (var t in FindObjectsByType<SnapTurnProvider>(FindObjectsSortMode.None))
            {
                t.enabled = false;
            }
            // Continuous turn (smooth) — disable for same reason.
            foreach (var t in FindObjectsByType<ContinuousTurnProvider>(FindObjectsSortMode.None))
            {
                t.enabled = false;
            }
            // Teleportation provider — disables the teleport action itself.
            foreach (var t in FindObjectsByType<TeleportationProvider>(FindObjectsSortMode.None))
            {
                t.enabled = false;
            }
            // Deactivate any GameObjects named "Teleport*" — these are the ray interactors
            // that draw the red arc when the thumbstick is pushed upward.
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name.Contains("Teleport") || t.name.Contains("teleport"))
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Disables all XR Interaction Toolkit ray interactor visuals — the white ray line
        /// and the cursor/reticle dot shown at ray endpoints.  We draw our own blue ray in
        /// ControlPanel, so the built-in visuals are unwanted noise.
        /// Also disables any locomotion cursor shown by the move provider.
        /// </summary>
        private void DisableXRInteractorVisuals()
        {
            // Disable line-renderer visuals on all ray/near-far interactors
            foreach (var c in FindObjectsByType<
                UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                c.enabled = false;
            }

            // Disable reticle visuals (cursor dot at end of ray)
            foreach (var c in FindObjectsByType<
                UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorReticleVisual>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                c.enabled = false;
            }

            // NearFarInteractor is intentionally left ENABLED — its near (physical) grab mode
            // is required for XRGrabInteractable objects (sticks, flare gun).
            // The ray line visual and reticle are already suppressed by the loops above,
            // so no visual artifacts appear even with the interactor active.

            // Disable any remaining LineRenderers on controller child GOs whose name
            // suggests they are ray/cursor visuals (catches custom visuals added by XRI3 template)
            foreach (var lr in FindObjectsByType<LineRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = lr.gameObject.name.ToLower();
                if (n.Contains("ray") || n.Contains("cursor") || n.Contains("reticle")
                    || n.Contains("line visual") || n.Contains("pointer"))
                    lr.enabled = false;
            }

            // Hide any small sprite/mesh renderers used as cursor dots on rig children
            foreach (var t in FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = t.name.ToLower();
                if (!n.Contains("reticle") && !n.Contains("cursor") && !n.Contains("dot")) continue;
                // Don't touch our own SearchTargetReticle
                if (t.GetComponent<CelestiaVR.UI.SearchTargetReticle>() != null) continue;
                var r = t.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
        }

        /// <summary>
        /// Disables all XRI AffordanceSystem receivers/providers.
        /// When NearFarInteractor is disabled its affordance state provider stops firing,
        /// but the receivers still run and throw NullReferenceException in HandleTween.
        /// Disabling every MonoBehaviour whose type name contains "Affordance" silences this.
        /// </summary>
        private void DisableAffordanceSystem()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var fullName = mb.GetType().FullName;
                if (fullName != null && fullName.Contains("Affordance"))
                    mb.enabled = false;
            }
        }

        /// <summary>Disables GazeDebugger — a dev-only component that floods the console.</summary>
        private void DisableGazeDebugger()
        {
            foreach (var gd in FindObjectsByType<GazeDebugger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                gd.enabled = false;
                // Also destroy the cursor sphere it may have already spawned
                var cursor = GameObject.Find("GazeCursor_DEBUG");
                if (cursor != null) Destroy(cursor);
            }
        }

        /// <summary>
        /// Belt-and-suspenders: StellariumLoader already disables these in its Awake,
        /// but Bootstrap runs in Start (after all Awakes) so this catches edge cases where
        /// execution order causes ConstellationHIPRenderer.Start to run first and build lines.
        /// </summary>
        private void DisableLegacyConstellationRenderers()
        {
            foreach (var r in FindObjectsByType<ConstellationHIPRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                r.enabled = false;
                // Destroy any LineRenderers it may have already built
                foreach (var lr in r.GetComponentsInChildren<LineRenderer>())
                    lr.enabled = false;
            }
            foreach (var r in FindObjectsByType<ConstellationArtRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                r.enabled = false;
        }

        private void EnsureSkyLabels()
        {
            // Auto-add SkyLabelManager if not already in the scene
            if (FindFirstObjectByType<SkyLabelManager>() == null)
            {
                var go = new GameObject("[SkyLabelManager]");
                go.AddComponent<SkyLabelManager>();
            }
        }

        private void EnsureViewingMode()
        {
            // Auto-add ViewingModeManager singleton if not already in the scene
            if (FindFirstObjectByType<ViewingModeManager>() == null)
            {
                var go = new GameObject("[ViewingModeManager]");
                go.AddComponent<ViewingModeManager>();
            }
        }

        private void EnsureDirectionalArrow()
        {
            if (FindFirstObjectByType<DirectionalArrow>() == null)
            {
                var go = new GameObject("[DirectionalArrow]");
                go.AddComponent<DirectionalArrow>();
            }
            if (FindFirstObjectByType<SearchTargetReticle>() == null)
            {
                var go = new GameObject("[SearchTargetReticle]");
                go.AddComponent<SearchTargetReticle>();
            }
        }

        private void EnsureSearchSystem()
        {
            // ControlPanel replaces CelestialSearchPanel + SearchPanelDwellDetector
            if (FindFirstObjectByType<ControlPanel>() == null)
            {
                var go = new GameObject("[ControlPanel]");
                go.AddComponent<ControlPanel>();
            }
        }

        private void EnsureInputManager()
        {
            if (FindFirstObjectByType<StargazingInputManager>() == null)
            {
                var go = new GameObject("[StargazingInputManager]");
                go.AddComponent<StargazingInputManager>();
            }
        }

        private void EnsureSoundManager()
        {
            if (FindFirstObjectByType<SoundManager>() != null) return;
            var go = new GameObject("[SoundManager]");
            go.AddComponent<SoundManager>();
        }

        private void EnsureFireplaceMiniGame()
        {
            if (FindFirstObjectByType<FireplaceBootstrap>() != null) return;

            var go = new GameObject("[FireplaceBootstrap]");
            go.AddComponent<FireplaceBootstrap>();
            // islandAnchor left null → positions are treated as world-space coordinates directly.
            // Assign islandAnchor in the Inspector if you want island-local offset coordinates.
        }

        /// <summary>
        /// The island GLB has no physics mesh, so XRI's CharacterController falls straight
        /// through it.  This creates a single large invisible BoxCollider that acts as the
        /// ground plane.  The Y position is derived from the scene's fireplace marker (the
        /// most reliable indicator of the island surface), falling back to the island root's
        /// renderer bounds min, and finally to Y=0.
        /// </summary>
        private void EnsureIslandBoundaryWalls()
        {
            if (FindFirstObjectByType<IslandBoundaryWalls>() != null) return;
            var go = new GameObject("[IslandBoundaryWalls]");
            go.AddComponent<IslandBoundaryWalls>();
        }

        private void EnsureIslandFloor()
        {
            if (GameObject.Find("[IslandFloor]") != null) return;

            float floorY = DetectIslandFloorY();

            // Invisible BoxCollider slab — catches CharacterController-based locomotion
            var floorGO = new GameObject("[IslandFloor]");
            floorGO.transform.position = new Vector3(0f, floorY, 0f);
            var col    = floorGO.AddComponent<BoxCollider>();
            col.size   = new Vector3(300f, 0.2f, 300f);
            col.center = new Vector3(0f, -0.1f, 0f);

            // Hard Y-clamp guard — catches direct transform.position locomotion that
            // bypasses CharacterController entirely (XRI3 can do both depending on config)
            var guardGO = new GameObject("[IslandFloorGuard]");
            var guard   = guardGO.AddComponent<IslandFloorGuard>();
            guard.overrideFloorY = floorY;

            Debug.Log($"[Bootstrap] Island floor collider + guard added at Y={floorY:F3}");
        }

        private void EnsureOnboarding()
        {
            if (FindFirstObjectByType<OnboardingManager>() != null) return;
            var go = new GameObject("[OnboardingManager]");
            go.AddComponent<OnboardingManager>();
        }

        private static float DetectIslandFloorY()
        {
            // 1. Designer-placed fireplace marker — most reliable signal for island surface
            var marker = GameObject.Find("To_Place_Fireplace");
            if (marker != null) return marker.transform.position.y;

            // 2. Scan for the island root by name and use the bottom of its renderer bounds
            foreach (var candidate in new[] { "Island", "island", "IslandRoot", "Floating Island" })
            {
                var go = GameObject.Find(candidate);
                if (go == null) continue;
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                return bounds.min.y;
            }

            // 3. Use XR Origin's current Y — wherever the designer placed the rig IS the floor
            foreach (var name in new[] {
                "XR Origin Hands (XR Rig)", "XR Origin (XR Rig)", "XR Origin" })
            {
                var go = GameObject.Find(name);
                if (go != null) return go.transform.position.y;
            }

            // 4. Last resort: world Y=0
            return 0f;
        }
    }
}
