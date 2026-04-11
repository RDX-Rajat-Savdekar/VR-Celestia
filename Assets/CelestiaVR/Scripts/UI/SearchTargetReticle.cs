using TMPro;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.UI
{
    /// <summary>
    /// World-space Stellarium-style reticle that appears directly on the searched target.
    ///
    /// • A ring + four crosshair tick marks (N/S/E/W) centred on the target body.
    /// • Scales every frame to maintain a constant angular size from the camera.
    /// • Pulses in alpha to attract attention.
    /// • Turns bright green when the user is gazing within 5° of the target.
    ///
    /// Activated automatically by DirectionalArrow.SetTarget() / Hide().
    /// Auto-created by StargazingSceneBootstrap.
    /// </summary>
    public class SearchTargetReticle : MonoBehaviour
    {
        public static SearchTargetReticle Instance { get; private set; }

        [Header("Appearance")]
        [Tooltip("Visual ring diameter in degrees of arc.")]
        public float angularSizeDeg  = 3.2f;
        [Tooltip("Tick marks extend this fraction of the ring radius beyond the ring edge.")]
        public float tickOutset      = 0.30f;
        [Tooltip("Pulses per second when idle.")]
        public float pulseFrequency  = 1.0f;

        // Colors
        private static readonly Color ColIdle  = new Color(0.30f, 0.70f, 1.00f, 1f);
        private static readonly Color ColFound = new Color(0.20f, 1.00f, 0.45f, 1f);

        // ── Runtime ──────────────────────────────────────────────────────────────

        private Camera         _cam;
        private Transform      _targetTransform;

        private GameObject    _root;
        private LineRenderer  _ringLR;
        private LineRenderer[] _tickLRs = new LineRenderer[4];
        private TextMeshPro   _nameTMP;
        private TextMeshPro   _distTMP;

        private bool _isActive;

        private static readonly Vector3[] TickDirs =
            { Vector3.up, Vector3.down, Vector3.right, Vector3.left };

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
            if (!_isActive) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || _targetTransform == null) return;

            Vector3 camPos    = _cam.transform.position;
            Vector3 targetPos = _targetTransform.position;
            float   dist      = Vector3.Distance(camPos, targetPos);

            // Constant angular size: radius in world units = dist * tan(halfAngle)
            float radius = dist * Mathf.Tan(angularSizeDeg * 0.5f * Mathf.Deg2Rad);

            // Billboard toward camera
            _root.transform.position = targetPos;
            _root.transform.rotation = Quaternion.LookRotation(targetPos - camPos);

            // Gaze angle
            float angleDeg = Vector3.Angle(_cam.transform.forward,
                                           (targetPos - camPos).normalized);
            bool found = angleDeg < 5f;

            // Pulsing alpha (faster when found)
            float freq  = found ? pulseFrequency * 2.5f : pulseFrequency;
            float pulse = 0.50f + 0.50f * Mathf.Sin(Time.time * freq * Mathf.PI * 2f);
            Color col   = Color.Lerp(ColIdle, ColFound, found ? 1f : 0f);
            Color colA  = new Color(col.r, col.g, col.b, pulse);

            // Update ring
            float lw = Mathf.Max(0.0015f, radius * 0.06f);
            _ringLR.startWidth = lw; _ringLR.endWidth = lw;
            _ringLR.startColor = colA; _ringLR.endColor = colA;
            int ringN = _ringLR.positionCount;
            for (int i = 0; i < ringN; i++)
            {
                float a = i / (float)(ringN - 1) * Mathf.PI * 2f;
                _ringLR.SetPosition(i, new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * radius);
            }

            // Update ticks
            float innerR = radius;
            float outerR = radius * (1f + tickOutset);
            for (int i = 0; i < 4; i++)
            {
                _tickLRs[i].startWidth = lw; _tickLRs[i].endWidth = lw;
                _tickLRs[i].startColor = colA; _tickLRs[i].endColor = colA;
                _tickLRs[i].SetPosition(0, TickDirs[i] * innerR);
                _tickLRs[i].SetPosition(1, TickDirs[i] * outerR);
            }

            // Name label — above ring
            float nameScale = Mathf.Clamp(dist * 0.010f, 0.004f, 0.060f);
            _nameTMP.transform.localPosition = new Vector3(0f, outerR * 1.5f, 0f);
            _nameTMP.transform.localScale    = Vector3.one * nameScale;
            _nameTMP.color = new Color(col.r, col.g, col.b, 0.85f + 0.15f * pulse);

            // Distance label — below ring
            _distTMP.transform.localPosition = new Vector3(0f, -outerR * 1.5f, 0f);
            _distTMP.transform.localScale    = Vector3.one * nameScale * 0.75f;
            _distTMP.text  = found ? "◉" : $"{angleDeg:F1}°";
            _distTMP.color = new Color(col.r, col.g, col.b, 0.70f * pulse);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void SetTarget(CelestialBody body)
        {
            if (body == null) { ClearTarget(); return; }
            _targetTransform = body.transform;
            _nameTMP.text    = body.objectName;
            SetVisible(true);
        }

        public void ClearTarget()
        {
            _targetTransform = null;
            SetVisible(false);
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void SetVisible(bool v)
        {
            _isActive = v;
            if (_root != null) _root.SetActive(v);
        }

        private void BuildVisuals()
        {
            _root = new GameObject("SearchTargetReticle_Root");

            // Ring
            var ringGO = new GameObject("Ring");
            ringGO.transform.SetParent(_root.transform, false);
            _ringLR = ringGO.AddComponent<LineRenderer>();
            SetupLR(_ringLR, 64, loop: true, ColIdle, 0.002f);

            // Four tick marks
            for (int i = 0; i < 4; i++)
            {
                var tGO = new GameObject($"Tick{i}");
                tGO.transform.SetParent(_root.transform, false);
                _tickLRs[i] = tGO.AddComponent<LineRenderer>();
                SetupLR(_tickLRs[i], 2, loop: false, ColIdle, 0.002f);
                _tickLRs[i].SetPosition(0, Vector3.zero);
                _tickLRs[i].SetPosition(1, Vector3.up);
            }

            // Name
            var nameGO = new GameObject("NameLabel");
            nameGO.transform.SetParent(_root.transform, false);
            _nameTMP = nameGO.AddComponent<TextMeshPro>();
            ConfigureTMP(_nameTMP, 10f, new Color(0.90f, 0.95f, 1f, 1f));

            // Degrees
            var distGO = new GameObject("DistLabel");
            distGO.transform.SetParent(_root.transform, false);
            _distTMP = distGO.AddComponent<TextMeshPro>();
            ConfigureTMP(_distTMP, 8f, new Color(0.60f, 0.75f, 0.90f, 0.80f));
        }

        private static void SetupLR(LineRenderer lr, int count, bool loop, Color color, float width)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite",  0f);
            mat.color        = color;
            mat.renderQueue  = 3005;
            lr.material      = mat;
            lr.startWidth    = width;
            lr.endWidth      = width;
            lr.useWorldSpace = false;
            lr.loop          = loop;
            lr.positionCount = count;
        }

        private static void ConfigureTMP(TextMeshPro tmp, float fontSize, Color color)
        {
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text             = "";
            tmp.fontSize         = fontSize;
            tmp.color            = color;
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.outlineWidth     = 0.22f;
            tmp.outlineColor     = new Color(0f, 0f, 0f, 0.85f);
            tmp.sortingOrder     = 12;
        }
    }
}
