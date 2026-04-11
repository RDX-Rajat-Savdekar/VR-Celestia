using TMPro;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.UI
{
    /// <summary>
    /// HUD-style directional arrow that sits in front of the camera and points
    /// toward a selected CelestialBody.
    ///
    /// • A rotating "▲" character indicates the direction to look.
    /// • When the target is within <foundAngleDegrees> of the gaze centre
    ///   the arrow switches to a green "✓ In View" state.
    /// • Call SetTarget(body) to activate, Hide() to dismiss.
    /// • Auto-created by CelestialSearchPanel / StargazingSceneBootstrap.
    /// </summary>
    public class DirectionalArrow : MonoBehaviour
    {
        public static DirectionalArrow Instance { get; private set; }

        [Header("HUD position (relative to camera)")]
        [Tooltip("Metres in front of the camera.")]
        public float forwardDistance = 2f;
        [Tooltip("Metres up/down from camera centre. Negative = below centre.")]
        public float verticalOffset  = -0.32f;
        [Tooltip("Metres right/left from camera centre.")]
        public float horizontalOffset = 0f;

        [Header("Interaction")]
        [Tooltip("If target stays in-view for this many seconds, auto-dismiss.")]
        public float autoDismissFoundSeconds = 4f;
        [Tooltip("Degrees from gaze centre that counts as 'found'.")]
        public float foundAngleDegrees = 15f;

        // ── Runtime ──────────────────────────────────────────────────────────────

        private Camera        _cam;
        private Transform     _targetTransform;

        // Visual objects
        private GameObject    _root;
        private TextMeshPro   _arrowTMP;   // single ▲ character, rotated
        private TextMeshPro   _nameTMP;
        private TextMeshPro   _hintTMP;
        private GameObject    _ringGO;     // thin circle background

        private bool  _isVisible;
        private float _foundTimer;

        private static readonly Color ColNormal = new Color(0.35f, 0.75f, 1f,  0.95f);
        private static readonly Color ColFound  = new Color(0.2f,  1f,   0.4f, 1f);
        private static readonly Color ColHint   = new Color(0.7f,  0.7f, 0.7f, 0.75f);

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cam = Camera.main;
            BuildVisuals();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!_isVisible) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || _targetTransform == null) return;

            // ── Position in front of camera ─────────────────────────────────────
            _root.transform.position =
                _cam.transform.position
                + _cam.transform.forward  * forwardDistance
                + _cam.transform.up       * verticalOffset
                + _cam.transform.right    * horizontalOffset;

            // Billboard: face camera
            _root.transform.rotation = Quaternion.LookRotation(
                _root.transform.position - _cam.transform.position);

            // ── Compute bearing ─────────────────────────────────────────────────
            Vector3 toTarget = (_targetTransform.position - _cam.transform.position).normalized;
            float   angleDeg = Vector3.Angle(_cam.transform.forward, toTarget);
            bool    found    = angleDeg < foundAngleDegrees;

            if (found)
            {
                _foundTimer += Time.deltaTime;
                _arrowTMP.text  = "*";   // ASCII star = "found" marker
                _arrowTMP.color = ColFound;
                _nameTMP.color  = ColFound;
                _hintTMP.text   = $"In View! ({_foundTimer:F0}s)";
                _hintTMP.color  = ColFound;
                _arrowTMP.transform.localEulerAngles = Vector3.zero;

                if (_foundTimer >= autoDismissFoundSeconds)
                    Hide();
            }
            else
            {
                _foundTimer = 0f;
                _arrowTMP.text  = "^";   // ASCII caret — rotates to indicate direction
                _arrowTMP.color = ColNormal;
                _nameTMP.color  = ColNormal;
                _hintTMP.text   = $"{angleDeg:F0}° away";
                _hintTMP.color  = ColHint;

                // Rotate ▲ to point toward target in the camera's view plane
                Vector3 projected = Vector3.ProjectOnPlane(toTarget, _cam.transform.forward);
                if (projected.sqrMagnitude > 0.001f)
                {
                    float r = Vector3.Dot(projected, _cam.transform.right);
                    float u = Vector3.Dot(projected, _cam.transform.up);
                    float roll = Mathf.Atan2(r, u) * Mathf.Rad2Deg;
                    _arrowTMP.transform.localEulerAngles = new Vector3(0, 0, -roll);
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void SetTarget(CelestialBody body)
        {
            if (body == null) { Hide(); return; }
            _targetTransform = body.transform;
            _foundTimer      = 0f;
            _nameTMP.text    = body.objectName;
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
            _targetTransform = null;
        }

        public bool IsVisible => _isVisible;

        // ── Build ─────────────────────────────────────────────────────────────────

        private void SetVisible(bool v)
        {
            _isVisible = v;
            if (_root != null) _root.SetActive(v);
        }

        private void BuildVisuals()
        {
            _root = new GameObject("DirectionalArrow_Root");
            _root.transform.SetParent(null);

            // Background ring (thin transparent circle built from line renderer)
            // Ring radius 0.10 world units at 2m distance ≈ 2.9° — comfortably visible in VR.
            _ringGO = new GameObject("Ring");
            _ringGO.transform.SetParent(_root.transform, false);
            BuildRing(_ringGO, 0.10f, 48);

            // Arrow character — scale 0.05 gives ~0.07 world units tall = ~2° at 2m (readable in VR).
            _arrowTMP = MakeTMP(_root.transform, "ArrowChar", "^", 14f, 0.05f,
                ColNormal, TextAlignmentOptions.Center, new Vector3(0, 0.025f, 0));
            _arrowTMP.fontStyle = FontStyles.Bold;

            // Object name: fontSize=10 at scale 0.04 → ~0.04 world units ≈ 1.1°
            _nameTMP = MakeTMP(_root.transform, "NameLabel", "", 10f, 0.04f,
                ColNormal, TextAlignmentOptions.Center, new Vector3(0, -0.12f, 0));

            // Hint line (degrees / found): slightly smaller
            _hintTMP = MakeTMP(_root.transform, "HintLabel", "", 8f, 0.035f,
                ColHint, TextAlignmentOptions.Center, new Vector3(0, -0.155f, 0));
        }

        private static TextMeshPro MakeTMP(Transform parent, string goName, string text,
            float fontSize, float localScale, Color color, TextAlignmentOptions align, Vector3 localPos)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = Vector3.one * localScale;

            var tmp = go.AddComponent<TextMeshPro>();
            // Assign default font explicitly to prevent "No Font Asset" warning
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text             = text;
            tmp.fontSize         = fontSize;
            tmp.color            = color;
            tmp.alignment        = align;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.outlineWidth     = 0.18f;
            tmp.outlineColor     = new Color(0, 0, 0, 0.75f);
            tmp.sortingOrder     = 10;
            return tmp;
        }

        private static void BuildRing(GameObject go, float radius, int segments)
        {
            var lr = go.AddComponent<LineRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite", 0f);
            mat.color = new Color(0.35f, 0.75f, 1f, 0.2f);
            mat.renderQueue = 3003;
            lr.material     = mat;
            lr.startWidth   = 0.002f;
            lr.endWidth     = 0.002f;
            lr.loop         = true;
            lr.useWorldSpace = false;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, 0));
            }
        }
    }
}
