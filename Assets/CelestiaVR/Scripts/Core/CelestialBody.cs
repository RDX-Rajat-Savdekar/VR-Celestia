using UnityEngine;

namespace CelestiaVR.Core
{
    public enum CelestialBodyType
    {
        Star,
        Planet,
        Moon,
        DeepSkyObject,
        Constellation
    }

    /// <summary>
    /// Data component attached to any selectable celestial object.
    /// </summary>
    public class CelestialBody : MonoBehaviour
    {
        [Header("Identity")]
        public string objectName;
        public CelestialBodyType bodyType;
        [TextArea(2, 4)]
        public string description;

        [Header("Astronomical Data")]
        public float magnitude = 0f;
        public float distanceLightYears = 0f;
        public float colorIndex = 0f;     // B-V index for stars
        public string spectralType;
        [Tooltip("Physical radius in km. Set for Sun/Moon/planets. 0 = unknown. Used by real-scale mode.")]
        public float physicalRadiusKm = 0f;
        [Tooltip("Surface temperature in Kelvin. Estimated from B-V for stars.")]
        public float temperatureK = 0f;

        [Header("Positional Data")]
        public float rightAscensionHours;
        public float declinationDegrees;

        [Header("Inspection")]
        [Tooltip("Optional 3-D model prefab to use for the inspection hologram. " +
                 "If null, the body's own sky GameObject is cloned instead.")]
        public GameObject inspectionPrefab;

        // Runtime: original sky position for restoring after inspection
        [HideInInspector] public Vector3 skyPosition;
        [HideInInspector] public Quaternion skyRotation;
        [HideInInspector] public Vector3 skyScale;
        [HideInInspector] public bool isInspecting;

        private void Start()
        {
            CacheTransform();
        }

        public void CacheTransform()
        {
            skyPosition = transform.position;
            skyRotation = transform.rotation;
            skyScale = transform.localScale;
        }
    }
}
