using TMPro;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Elegant HUD compass that guides the player toward a searched CelestialBody.
    ///
    /// Design:
    ///   • A thin compass ring sits in the lower-centre of the HUD.
    ///   • A bright chevron (▶) sits on the ring perimeter and rotates to the bearing.
    ///   • Object name + degrees-away float just outside the chevron.
    ///   • When the target enters the "in-view" angle, the HUD fades out smoothly —
    ///     the SearchTargetReticle (world-space) takes over visual guidance.
    ///
    /// Call SetTarget(body) to activate, Hide() to dismiss.
    /// Auto-created by StargazingSceneBootstrap.
    /// </summary>
    public class DirectionalArrow : MonoBehaviour
    {
        public static DirectionalArrow Instance { get; private set; }

        [Header("HUD Position")]
        public float forwardDistance  = 2.0f;
        public float verticalOffset   = -0.30f;
        public float horizontalOffset =  0.0f;

        [Header("Fade behaviour")]
        [Tooltip("Within this angle the HUD fades out (world-space reticle takes over).")]
        public float fadeStartAngle = 20f;
        [Tooltip("Below this angle the HUD is fully transparent.")]
        public float fadeEndAngle   = 8f;
        [Tooltip("Alpha fade speed (units per second).")]
        public float fadeSpeed      = 3f;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private Camera      _cam;
        private Transform   _targetTransform;
        private GameObject  _root;
        private LineRenderer _ringLR;
        private LineRenderer _arrowLR;  // stem + arrowhead pointing outward
        private GameObject  _chevRoot; // parent of arrow — rotated to bearing
        private TextMeshPro _nameTMP;
        private TextMeshPro _hintTMP;

        private bool  _isVisible;
        private float _alpha = 1f;

        private const float RingRadius = 0.080f;
        private const int   RingSegs   = 64;

        private static readonly Color ColRing  = new Color(0.30f, 0.65f, 1.00f, 1f);
        private static readonly Color ColChev  = new Color(0.55f, 0.90f, 1.00f, 1f);
        private static readonly Color ColName  = new Color(0.90f, 0.95f, 1.00f, 1f);
        private static readonly Color ColHint  = new Color(0.60f, 0.70f, 0.82f, 0.80f);

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

            // ── HUD placement ─────────────────────────────────────────────────────
            _root.transform.position =
                _cam.transform.position
                + _cam.transform.forward  * forwardDistance
                + _cam.transform.up       * verticalOffset
                + _cam.transform.right    * horizontalOffset;
            _root.transform.rotation = Quaternion.LookRotation(
                _root.transform.position - _cam.transform.position);

            // ── Bearing ───────────────────────────────────────────────────────────
            Vector3 toTarget = (_targetTransform.position - _cam.transform.position).normalized;
            float   angleDeg = Vector3.Angle(_cam.transform.forward, toTarget);

            // ── Fade when target is in view (reticle takes over) ──────────────────
            float targetAlpha = angleDeg <= fadeEndAngle   ? 0f
                              : angleDeg >= fadeStartAngle ? 1f
                              : Mathf.InverseLerp(fadeEndAngle, fadeStartAngle, angleDeg);
            _alpha = Mathf.MoveTowards(_alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            ApplyAlpha(_alpha);

            if (_alpha < 0.01f) return;

            // ── Rotate chevron to bearing direction ───────────────────────────────
            Vector3 projected = Vector3.ProjectOnPlane(toTarget, _cam.transform.forward);
            float   roll      = 0f;
            if (projected.sqrMagnitude > 0.001f)
            {
                float r = Vector3.Dot(projected, _cam.transform.right);
                float u = Vector3.Dot(projected, _cam.transform.up);
                roll = Mathf.Atan2(r, u) * Mathf.Rad2Deg;
            }

            // Position chevronRoot ON the ring at the bearing angle
            float rad = -roll * Mathf.Deg2Rad;
            Vector3 onRing = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f) * RingRadius;
            _chevRoot.transform.localPosition    = onRing;
            _chevRoot.transform.localEulerAngles = new Vector3(0f, 0f, -roll);

            // Name + hint offset further out from ring in the bearing direction
            float outDist = RingRadius * 1.9f;
            Vector3 outDir = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
            _nameTMP.transform.localPosition = outDir * outDist;
            _hintTMP.transform.localPosition = outDir * outDist - new Vector3(0f, 0.022f, 0f);

            _hintTMP.text = $"{angleDeg:F0}°";
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void SetTarget(CelestialBody body)
        {
            if (body == null) { Hide(); return; }
            _targetTransform = body.transform;
            _nameTMP.text    = body.objectName;
            _alpha           = 1f;
            SetVisible(true);

            // Also show world-space reticle on the target
            SearchTargetReticle.Instance?.SetTarget(body);
        }

        public void Hide()
        {
            SetVisible(false);
            _targetTransform = null;
            SearchTargetReticle.Instance?.ClearTarget();
        }

        public bool IsVisible => _isVisible;

        // ── Build ─────────────────────────────────────────────────────────────────

        private void SetVisible(bool v)
        {
            _isVisible = v;
            if (_root != null) _root.SetActive(v);
        }

        private void ApplyAlpha(float a)
        {
            SetLRColor(_ringLR,  new Color(ColRing.r, ColRing.g, ColRing.b, ColRing.a * a * 0.55f));
            SetLRColor(_arrowLR, new Color(ColChev.r, ColChev.g, ColChev.b, ColChev.a * a));
            _nameTMP.color = new Color(ColName.r, ColName.g, ColName.b, a);
            _hintTMP.color = new Color(ColHint.r, ColHint.g, ColHint.b, ColHint.a * a);
        }

        private static void SetLRColor(LineRenderer lr, Color c)
        {
            if (lr == null) return;
            lr.startColor = c; lr.endColor = c;
        }

        private void BuildVisuals()
        {
            _root = new GameObject("DirectionalArrow_Root");

            // ── Compass ring ───────────────────────────────────────────────────────
            var ringGO = new GameObject("Ring");
            ringGO.transform.SetParent(_root.transform, false);
            _ringLR = ringGO.AddComponent<LineRenderer>();
            SetupLR(_ringLR, RingSegs, loop: true, ColRing, 0.0016f);
            for (int i = 0; i < RingSegs; i++)
            {
                float a = i / (float)(RingSegs - 1) * Mathf.PI * 2f;
                _ringLR.SetPosition(i, new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * RingRadius);
            }

            // Small tick marks at N/S/E/W on the ring (orientation cues)
            float tickInner = RingRadius * 0.82f;
            float tickOuter = RingRadius * 1.00f;
            Vector3[] cardinalDirs = { Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            foreach (var dir in cardinalDirs)
            {
                var tGO = new GameObject("CardinalTick");
                tGO.transform.SetParent(_root.transform, false);
                var tlr = tGO.AddComponent<LineRenderer>();
                SetupLR(tlr, 2, loop: false, new Color(ColRing.r, ColRing.g, ColRing.b, 0.5f), 0.0014f);
                tlr.SetPosition(0, dir * tickInner);
                tlr.SetPosition(1, dir * tickOuter);
            }

            // ── Arrow on ring edge ────────────────────────────────────────────────
            // Shape: a vertical stem starting at ring surface, with a clear arrowhead tip.
            // The whole arrow points AWAY from ring centre = toward the target.
            // Upward in local space = outward (away from ring centre) after roll rotation.
            //
            //     /\     ← arrowhead tip
            //    /  \
            //     ||     ← stem
            //     ||
            //  (ring edge, origin)
            //
            _chevRoot = new GameObject("ArrowRoot");
            _chevRoot.transform.SetParent(_root.transform, false);

            // Combined path: stem bottom → stem top → head-left → tip → head-right → stem-top
            // This draws a stem with a clean arrowhead in one line strip.
            float stemH  = 0.0280f;  // stem height above ring edge
            float headH  = 0.0180f;  // arrowhead height above stem top
            float headW  = 0.0140f;  // arrowhead half-width
            float stemW  = 0.0028f;  // line width

            _arrowLR = _chevRoot.AddComponent<LineRenderer>();
            SetupLR(_arrowLR, 6, loop: false, ColChev, stemW);
            _arrowLR.SetPosition(0, new Vector3(  0f,        0f,           0f)); // ring edge
            _arrowLR.SetPosition(1, new Vector3(  0f,        stemH,        0f)); // stem top
            _arrowLR.SetPosition(2, new Vector3(-headW,      stemH,        0f)); // head left
            _arrowLR.SetPosition(3, new Vector3(  0f,        stemH+headH,  0f)); // tip
            _arrowLR.SetPosition(4, new Vector3( headW,      stemH,        0f)); // head right
            _arrowLR.SetPosition(5, new Vector3(  0f,        stemH,        0f)); // back to stem top

            // ── Object name ───────────────────────────────────────────────────────
            _nameTMP = MakeTMP(_root.transform, "NameLabel", "", 8f, 0.042f, ColName);

            // ── Degrees-away hint ─────────────────────────────────────────────────
            _hintTMP = MakeTMP(_root.transform, "HintLabel", "", 6f, 0.034f, ColHint);
        }

        private static void SetupLR(LineRenderer lr, int count, bool loop, Color color, float width)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite",  0f);
            mat.color       = color;
            mat.renderQueue = 3003;
            lr.material      = mat;
            lr.startWidth    = width;
            lr.endWidth      = width;
            lr.useWorldSpace = false;
            lr.loop          = loop;
            lr.positionCount = count;
        }

        private static TextMeshPro MakeTMP(Transform parent, string goName, string text,
            float fontSize, float scale, Color color)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var tmp = go.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text             = text;
            tmp.fontSize         = fontSize;
            tmp.color            = color;
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.outlineWidth     = 0.18f;
            tmp.outlineColor     = new Color(0f, 0f, 0f, 0.75f);
            tmp.sortingOrder     = 10;
            return tmp;
        }
    }
}
