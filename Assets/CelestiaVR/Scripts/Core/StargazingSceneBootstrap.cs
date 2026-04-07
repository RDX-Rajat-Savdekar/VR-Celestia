using UnityEngine;
using CelestiaVR.Interaction;

namespace CelestiaVR.Core
{
    /// <summary>
    /// Quick-start helper — wires up system references in Play mode when
    /// you haven't yet configured everything in the Inspector.
    ///
    /// Attach to any persistent root GameObject in StargazingScene.
    /// This is optional — proper inspector wiring is preferred for production.
    /// </summary>
    public class StargazingSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            // Auto-find camera if not set on DwellSelector
            var dwell = FindFirstObjectByType<DwellSelector>();
            if (dwell != null && dwell.gazeCamera == null)
                dwell.gazeCamera = Camera.main;

            // Auto-find SelectionManager → InspectionController link
            var selMgr = FindFirstObjectByType<SelectionManager>();
            var inspector = FindFirstObjectByType<InspectionController>();
            if (selMgr != null && inspector != null && inspector.selectionManager == null)
                inspector.selectionManager = selMgr;

            Debug.Log("[StargazingSceneBootstrap] Scene wired up.");
        }
    }
}
