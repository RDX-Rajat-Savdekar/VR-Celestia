using UnityEngine;
using CelestiaVR.Interaction;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Visual + log debugger for the gaze system.
    /// Creates a cursor sphere at the gaze hit point so you can see exactly where you're looking.
    /// When a CelestialBody is hit: cursor turns GREEN and the planet scales up dramatically.
    /// When hitting nothing: cursor is RED at max distance.
    /// Attach to any persistent GameObject in the scene (e.g. SkyManager or XR Origin).
    /// </summary>
    public class GazeDebugger : MonoBehaviour
    {
        [Header("Cursor")]
        [Tooltip("Size of the gaze cursor sphere.")]
        public float cursorSize = 3f;

        [Tooltip("How often (seconds) to log gaze ray info.")]
        public float logInterval = 2f;

        private DwellSelector _dwell;
        private Camera _cam;
        private GameObject _cursor;
        private Renderer _cursorRenderer;
        private float _logTimer;

        // Track the last hit planet so we can un-scale it
        private Transform _scaledPlanet;
        private Vector3 _originalScale;

        private static readonly Color ColorNothing  = Color.red;
        private static readonly Color ColorSomething = Color.yellow;
        private static readonly Color ColorPlanet   = Color.green;

        private void Start()
        {
            _dwell = FindFirstObjectByType<DwellSelector>();
            _cam   = Camera.main;

            if (_dwell == null) { Debug.LogError("[GazeDebugger] No DwellSelector found!"); return; }
            if (_cam   == null) { Debug.LogError("[GazeDebugger] No Main Camera found!"); return; }

            // Create cursor sphere procedurally - no prefab needed
            _cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _cursor.name = "GazeCursor_DEBUG";
            Destroy(_cursor.GetComponent<Collider>());
            _cursor.transform.localScale = Vector3.one * cursorSize;
            _cursorRenderer = _cursor.GetComponent<Renderer>();
            SetCursorColor(ColorNothing);

            Debug.Log("[GazeDebugger] Cursor sphere created. RED=nothing  YELLOW=non-planet  GREEN=planet.");

            // Log all planet world positions so we know where to look
            Invoke(nameof(LogAllPlanetPositions), 1f);
        }

        private void LogAllPlanetPositions()
        {
            if (_cam == null) return;
            var bodies = FindObjectsByType<CelestiaVR.Core.CelestialBody>(FindObjectsSortMode.None);
            Debug.Log($"[GazeDebugger] === {bodies.Length} CelestialBodies in scene. Camera at {_cam.transform.position} ===");
            foreach (var b in bodies)
            {
                Vector3 worldPos = b.transform.position;
                Vector3 dirToBody = (worldPos - _cam.transform.position).normalized;
                float angle = Vector3.Angle(_cam.transform.forward, dirToBody);
                float dist = Vector3.Distance(_cam.transform.position, worldPos);
                Debug.Log($"[GazeDebugger]   {b.objectName,-10} worldPos={worldPos}  dist={dist:F0}u  angle-from-gaze={angle:F1}°");
            }
        }

        private void Update()
        {
            if (_cam == null || _dwell == null) return;

            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
            bool hitPlanet = false;

            // Mirror DwellSelector's SphereCast so cursor matches exactly what dwell detects
            if (Physics.SphereCast(ray, _dwell.gazeTolerance, out RaycastHit hit, _dwell.maxDistance))
            {
                // Move cursor to hit point
                _cursor.transform.position = hit.point;

                var body = hit.collider.GetComponentInParent<CelestiaVR.Core.CelestialBody>();
                if (body != null)
                {
                    hitPlanet = true;
                    SetCursorColor(ColorPlanet);

                    // Scale up the planet dramatically so you can't miss it
                    if (_scaledPlanet != body.transform)
                    {
                        RestoreScale();
                        _scaledPlanet    = body.transform;
                        _originalScale   = body.transform.localScale;
                        body.transform.localScale = _originalScale * 5f; // 5x size when gazed at
                        Debug.Log($"[GazeDebugger] *** GAZE HIT PLANET: {body.objectName} — scaled 5x! ***");
                    }
                }
                else
                {
                    SetCursorColor(ColorSomething);
                    RestoreScale();
                }

                _logTimer -= Time.deltaTime;
                if (_logTimer <= 0f)
                {
                    _logTimer = logInterval;
                    string bodyName = hitPlanet ? hit.collider.GetComponentInParent<CelestiaVR.Core.CelestialBody>()?.objectName : "non-planet";
                    Debug.Log($"[GazeDebugger] Ray HIT '{hit.collider.gameObject.name}' (body={bodyName}) at dist={hit.distance:F1} point={hit.point}");
                }
            }
            else
            {
                // No hit — place cursor at max distance along ray
                _cursor.transform.position = ray.GetPoint(Mathf.Min(_dwell.maxDistance, 100f));
                SetCursorColor(ColorNothing);
                RestoreScale();

                _logTimer -= Time.deltaTime;
                if (_logTimer <= 0f)
                {
                    _logTimer = logInterval;
                    Debug.Log($"[GazeDebugger] Ray MISS — nothing hit. Camera fwd={_cam.transform.forward}, pos={_cam.transform.position}");
                }
            }
        }

        private void RestoreScale()
        {
            if (_scaledPlanet != null)
            {
                _scaledPlanet.localScale = _originalScale;
                _scaledPlanet = null;
            }
        }

        private void SetCursorColor(Color c)
        {
            if (_cursorRenderer == null) return;
            _cursorRenderer.material.color = c;
        }

        private void OnDestroy()
        {
            RestoreScale();
            if (_cursor != null) Destroy(_cursor);
        }
    }
}
