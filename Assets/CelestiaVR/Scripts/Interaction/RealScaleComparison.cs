using TMPro;
using UnityEngine;
using CelestiaVR.Planets;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Shown alongside the inspected hologram when real-scale mode is active.
    ///
    /// Creates:
    ///   • A blue Earth sphere at the correct relative size.
    ///   • A thin line from the body hologram to Earth.
    ///   • A TMP label at the midpoint showing the scale ratio.
    ///   • A tiny "Earth" label above the Earth sphere.
    ///
    /// Lifetime: created/destroyed by InspectionController alongside SetHologramRadius().
    /// </summary>
    public class RealScaleComparison : MonoBehaviour
    {
        // ── Runtime state ──────────────────────────────────────────────────────────

        private Transform    _hologram;      // the spinning hologram copy
        private float        _bodyR;         // hologram radius in world-metres
        private float        _earthR;        // Earth sphere radius in world-metres

        private GameObject   _earthGO;       // Earth model (prefab or fallback primitive)
        private GameObject   _earthPrefab;   // optional prefab reference
        private LineRenderer _line;          // line between the two spheres
        private TextMeshPro  _scaleLabel;    // e.g. "11.2× Earth"
        private TextMeshPro  _earthLabel;    // "Earth" above the sphere

        private Camera       _cam;
        private bool         _active;

        private const float  SurfaceGap  = 0.07f;   // gap between surfaces (metres)
        private const float  LineWidth   = 0.003f;

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <param name="hologram">Transform of the spinning hologram copy.</param>
        /// <param name="bodyRadiusM">Target world-space radius of the hologram (metres).</param>
        /// <param name="earthRadiusM">World-space radius of the Earth sphere (metres).</param>
        /// <param name="scaleText">Label string, e.g. "11.2× Earth".</param>
        public void Show(Transform hologram, float bodyRadiusM, float earthRadiusM,
            string scaleText, GameObject earthPrefab = null)
        {
            Hide(); // clean up previous if any

            _hologram    = hologram;
            _bodyR       = bodyRadiusM;
            _earthR      = earthRadiusM;
            _earthPrefab = earthPrefab;
            _cam         = Camera.main;
            _active      = true;

            BuildEarth();
            BuildLine();
            BuildScaleLabel(scaleText);
        }

        public void Hide()
        {
            _active = false;
            if (_earthGO    != null) { Destroy(_earthGO);             _earthGO    = null; }
            if (_line       != null) { Destroy(_line.gameObject);     _line       = null; }
            if (_scaleLabel != null) { Destroy(_scaleLabel.gameObject); _scaleLabel = null; }
            if (_earthLabel != null) { Destroy(_earthLabel.gameObject); _earthLabel = null; }
        }

        // ── Per-frame update ───────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_active) return;
            if (_hologram == null) { Hide(); return; }

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Vector3 camRight  = _cam.transform.right;
            Vector3 camUp     = _cam.transform.up;
            float separation  = _bodyR + SurfaceGap + _earthR;
            Vector3 earthPos  = _hologram.position + camRight * separation;

            // Earth sphere
            if (_earthGO != null)
                _earthGO.transform.position = earthPos;

            // Connecting line
            if (_line != null)
            {
                _line.SetPosition(0, _hologram.position);
                _line.SetPosition(1, earthPos);
            }

            // Scale label — above midpoint
            if (_scaleLabel != null)
            {
                Vector3 mid = (_hologram.position + earthPos) * 0.5f + camUp * (_earthR + 0.06f);
                _scaleLabel.transform.position = mid;
                Vector3 toCam = mid - _cam.transform.position;
                if (toCam.sqrMagnitude > 0.001f)
                    _scaleLabel.transform.rotation = Quaternion.LookRotation(toCam);
            }

            // "Earth" label — above Earth sphere
            if (_earthLabel != null)
            {
                Vector3 pos = earthPos + camUp * (_earthR + 0.04f);
                _earthLabel.transform.position = pos;
                Vector3 toCam = pos - _cam.transform.position;
                if (toCam.sqrMagnitude > 0.001f)
                    _earthLabel.transform.rotation = Quaternion.LookRotation(toCam);
            }
        }

        // ── Builders ───────────────────────────────────────────────────────────────

        private void BuildEarth()
        {
            if (_earthPrefab != null)
            {
                _earthGO      = Object.Instantiate(_earthPrefab);
                _earthGO.name = "EarthComparison";
            }
            else
            {
                _earthGO      = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _earthGO.name = "EarthComparison";
                var rend = _earthGO.GetComponent<Renderer>();
                var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader.name == "Hidden/InternalErrorShader")
                    mat = new Material(Shader.Find("Standard"));
                mat.color    = new Color(0.20f, 0.50f, 0.90f, 1f);
                rend.material = mat;
            }

            // No collider — we don't want gaze to latch onto it
            foreach (var c in _earthGO.GetComponentsInChildren<Collider>())
                Destroy(c);

            // Normalise prefab mesh bounds (e.g. Earth_1_12756.prefab has a 12756-unit mesh).
            // Without this, localScale=0.16 on a 12756-unit mesh = 2040m world sphere.
            float earthDiamM = _earthR * 2f;
            if (_earthPrefab != null)
            {
                _earthGO.transform.localScale = Vector3.one;
                float boundsSize = PlanetController.GetNormalisedBoundsSize(_earthGO);
                float normFactor = boundsSize > 0f ? 1f / boundsSize : 1f;
                _earthGO.transform.localScale = Vector3.one * (earthDiamM * normFactor);
                Debug.Log($"[RealScaleComparison] Earth prefab bounds={boundsSize:G4}, " +
                          $"normFactor={normFactor:G4}, localScale={earthDiamM * normFactor:G4}");
            }
            else
            {
                _earthGO.transform.localScale = Vector3.one * earthDiamM;
            }

            // "Earth" label above the sphere
            var labelGO = new GameObject("EarthLabel");
            _earthLabel = labelGO.AddComponent<TextMeshPro>();
            _earthLabel.text           = "Earth";
            _earthLabel.fontSize       = 5f;
            _earthLabel.alignment      = TextAlignmentOptions.Center;
            _earthLabel.color          = new Color(0.8f, 0.9f, 1f, 0.95f);
            _earthLabel.outlineWidth   = 0.2f;
            _earthLabel.outlineColor   = new Color(0f, 0f, 0f, 0.7f);
            labelGO.transform.localScale = Vector3.one * 0.07f;
        }

        private void BuildLine()
        {
            var go = new GameObject("ComparisonLine");
            _line = go.AddComponent<LineRenderer>();
            _line.positionCount  = 2;
            _line.startWidth     = LineWidth;
            _line.endWidth       = LineWidth;
            _line.useWorldSpace  = true;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Sprites/Default"));
            mat.SetColor("_BaseColor", new Color(0.65f, 0.80f, 1f, 0.55f));
            mat.color = new Color(0.65f, 0.80f, 1f, 0.55f);
            _line.material = mat;
        }

        private void BuildScaleLabel(string text)
        {
            var go = new GameObject("ScaleLabel");
            _scaleLabel = go.AddComponent<TextMeshPro>();
            _scaleLabel.text           = text;
            _scaleLabel.fontSize       = 5f;
            _scaleLabel.alignment      = TextAlignmentOptions.Center;
            _scaleLabel.color          = new Color(1f, 0.90f, 0.65f, 1f);
            _scaleLabel.outlineWidth   = 0.25f;
            _scaleLabel.outlineColor   = new Color(0f, 0f, 0f, 0.85f);
            go.transform.localScale    = Vector3.one * 0.08f;
        }
    }
}
