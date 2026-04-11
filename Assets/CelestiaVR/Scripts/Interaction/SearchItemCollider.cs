using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Marker component attached to the invisible 3-D box collider that sits in front
    /// of each row in the CelestialSearchPanel.
    /// SearchPanelDwellDetector sphere-casts against these to drive dwell selection.
    /// </summary>
    public class SearchItemCollider : MonoBehaviour
    {
        /// <summary>The celestial body this search row represents.</summary>
        public CelestialBody target;

        /// <summary>Set to false when the row is hidden by the search filter.</summary>
        public bool isActive = true;
    }
}
