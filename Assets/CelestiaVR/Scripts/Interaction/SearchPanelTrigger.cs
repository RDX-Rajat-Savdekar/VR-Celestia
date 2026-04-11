using UnityEngine;
using CelestiaVR.UI;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Marker placed on the floating "Search" button sphere.
    /// SearchPanelDwellDetector detects gaze/dwell on this to open/close the panel.
    /// </summary>
    public class SearchPanelTrigger : MonoBehaviour
    {
        // Reference injected by CelestialSearchPanel at build time.
        [HideInInspector] public CelestialSearchPanel panel;
    }
}
