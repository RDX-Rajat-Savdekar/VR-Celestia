using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CelestiaVR.Core;
using CelestiaVR.Interaction;

namespace CelestiaVR.UI
{
    /// <summary>
    /// World-space info panel displayed during inspection mode.
    ///
    /// If the inspector fields are left empty this script auto-builds its own
    /// Canvas, background and TMP labels at runtime — zero scene setup required.
    /// Just add this component to any empty GameObject and it works.
    ///
    /// Rich info shown per object type:
    ///   Stars   — magnitude, spectral type, temperature, distance, description
    ///   Planets — magnitude, temperature, physical size, distance, description
    ///   DSO     — type, description
    ///
    /// A "Real Scale" toggle button animates the hologram to show the object's
    /// actual size relative to Earth (requires InspectionController reference).
    /// </summary>
    public class InspectionPanel : MonoBehaviour
    {
        [Header("Optional — leave all null to auto-create UI")]
        public TextMeshProUGUI objectNameText;
        public TextMeshProUGUI objectTypeText;
        public TextMeshProUGUI stat1Text;       // magnitude / brightness
        public TextMeshProUGUI stat2Text;       // distance
        public TextMeshProUGUI stat3Text;       // temperature / size
        public TextMeshProUGUI descriptionText;
        public Button realScaleButton;
        public TextMeshProUGUI realScaleLabel;

        [Header("Follow Camera")]
        public float panelDistance   = 1.6f;
        public float horizontalOffset = 0.45f;
        public float verticalOffset   = 0f;

        [Header("Animation")]
        [Range(0.1f, 1f)]
        public float fadeDuration = 0.3f;

        [Header("Real Scale")]
        [Tooltip("Reference to InspectionController so the button can trigger scale toggle.")]
        public InspectionController inspectionController;
        [Tooltip("Earth radius in Unity metres used as the baseline for real-scale display (1 = 1 m per Earth radius).")]
        public float earthRadiusMetres = 0.08f; // 8 cm = comfortable for VR
        [Tooltip("Max hologram radius in metres — prevents Sun from filling the room.")]
        public float maxRealScaleMetres = 4f;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private CanvasGroup _canvasGroup;
        private Camera _xrCamera;
        private Coroutine _fadeCoroutine;
        private bool _visible;
        private bool _realScaleActive;
        private CelestialBody _currentBody;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _xrCamera = Camera.main;

            if (objectNameText == null)
                AutoCreateUI();

            _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Keep GO always active — never call SetActive(false).
            // Visibility is controlled purely via CanvasGroup so coroutines always work.
            SetCanvasVisible(false);

            if (inspectionController == null)
                inspectionController = FindFirstObjectByType<InspectionController>();
        }

        private void LateUpdate()
        {
            if (!_visible) return;
            if (_xrCamera == null) _xrCamera = Camera.main;
            if (_xrCamera == null) return;

            Vector3 target = _xrCamera.transform.position
                + _xrCamera.transform.forward * panelDistance
                + _xrCamera.transform.right   * horizontalOffset
                + _xrCamera.transform.up      * verticalOffset;

            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 10f);
            transform.rotation = Quaternion.LookRotation(
                transform.position - _xrCamera.transform.position);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Show(CelestialBody body)
        {
            _currentBody = body;
            _realScaleActive = false;
            UpdateRealScaleButton();
            _visible = true;
            PopulateData(body);
            SetCanvasVisible(true);

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeTo(1f));
        }

        public void Hide()
        {
            _visible = false;
            _currentBody = null;
            _realScaleActive = false;
            UpdateRealScaleButton();

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeAndHide());
        }

        /// <summary>Called by InspectionController when real-scale is toggled externally.</summary>
        public void SetRealScaleState(bool active)
        {
            _realScaleActive = active;
            UpdateRealScaleButton();
        }

        // ── Data population ───────────────────────────────────────────────────────

        private void PopulateData(CelestialBody body)
        {
            if (objectNameText != null)
                objectNameText.text = string.IsNullOrEmpty(body.objectName) ? "Unknown" : body.objectName;

            if (objectTypeText != null)
                objectTypeText.text = FriendlyType(body);

            // stat1 — magnitude
            if (stat1Text != null)
                stat1Text.text = body.magnitude != 0f
                    ? $"Magnitude  {body.magnitude:F2}"
                    : "";

            // stat2 — distance
            if (stat2Text != null)
            {
                if (body.bodyType == CelestialBodyType.Planet || body.bodyType == CelestialBodyType.Moon)
                    stat2Text.text = ""; // varies daily — skip
                else if (body.distanceLightYears > 0f)
                    stat2Text.text = $"Distance  {FormatDistance(body.distanceLightYears)}";
                else
                    stat2Text.text = "";
            }

            // stat3 — temperature or size
            if (stat3Text != null)
            {
                string s3 = "";
                if (body.temperatureK > 0f)
                    s3 = $"Temp  {body.temperatureK:F0} K";
                else if (body.physicalRadiusKm > 0f)
                    s3 = $"Radius  {FormatKm(body.physicalRadiusKm)}";
                stat3Text.text = s3;
            }

            if (descriptionText != null)
                descriptionText.text = body.description;

            // Show real-scale button only when we have physical size data
            if (realScaleButton != null)
                realScaleButton.gameObject.SetActive(body.physicalRadiusKm > 0f);
        }

        private void UpdateRealScaleButton()
        {
            if (realScaleLabel != null)
                realScaleLabel.text = _realScaleActive ? "Sky View" : "Real Scale";
        }

        // ── Real scale ────────────────────────────────────────────────────────────

        private void OnRealScaleButtonPressed()
        {
            if (inspectionController == null || _currentBody == null) return;
            _realScaleActive = !_realScaleActive;
            UpdateRealScaleButton();

            float targetMetres = Mathf.Min(_currentBody.physicalRadiusKm / 6_371f * earthRadiusMetres,
                                           maxRealScaleMetres);
            inspectionController.SetHologramRadius(_realScaleActive ? targetMetres : -1f);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string FriendlyType(CelestialBody body) => body.bodyType switch
        {
            CelestialBodyType.Star         => "Star",
            CelestialBodyType.Planet       => "Planet",
            CelestialBodyType.Moon         => "Moon",
            CelestialBodyType.DeepSkyObject => "Deep Sky Object",
            _                              => body.bodyType.ToString()
        };

        private static string FormatDistance(float ly)
        {
            if (ly < 0.001f) return $"{ly * 63241f:F0} AU";
            if (ly < 100f)   return $"{ly:F1} light-years";
            return $"{ly:F0} light-years";
        }

        private static string FormatKm(float km)
        {
            if (km >= 100_000f) return $"{km / 1000f:F0}k km";
            if (km >= 1_000f)   return $"{km:F0} km";
            return $"{km:F0} km";
        }

        // ── Fade animation ────────────────────────────────────────────────────────

        private IEnumerator FadeTo(float target)
        {
            float start = _canvasGroup.alpha, elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = target;
        }

        private IEnumerator FadeAndHide()
        {
            yield return FadeTo(0f);
            SetCanvasVisible(false); // block raycasts but keep GO active
        }

        private void SetCanvasVisible(bool visible)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.interactable   = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        // ── Auto-create UI ────────────────────────────────────────────────────────

        private void AutoCreateUI()
        {
            // World-space Canvas
            var canvasGO = new GameObject("InspectionCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = Vector3.zero;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            var cg = canvasGO.AddComponent<CanvasGroup>();

            // 400 × 520 pixels at scale 0.001 = 0.40 m × 0.52 m physical panel
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta  = new Vector2(400, 520);
            rt.localScale = Vector3.one * 0.001f; // 1 px = 1 mm

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 1f;

            // Dark background
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.08f, 0.18f, 0.88f);
            SetRectFull(bgImg.GetComponent<RectTransform>());

            // Thin blue border
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(canvasGO.transform, false);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(0.2f, 0.5f, 1f, 0.4f);
            var brt = borderImg.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-2, -2);
            brt.offsetMax = new Vector2( 2,  2);

            // Top-most vertical layout
            var layout = new GameObject("Layout");
            layout.transform.SetParent(canvasGO.transform, false);
            var lrt = layout.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(16, 12); lrt.offsetMax = new Vector2(-16, -12);
            var vlg = layout.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childControlHeight = false;
            vlg.childControlWidth  = true;
            vlg.childForceExpandHeight = false;

            // Text helper
            System.Func<string, int, Color, TextAlignmentOptions, TextMeshProUGUI> MakeTMP =
                (name, size, col, align) =>
            {
                var go = new GameObject(name);
                go.transform.SetParent(layout.transform, false);
                var t = go.AddComponent<TextMeshProUGUI>();
                t.fontSize         = size;
                t.color            = col;
                t.alignment        = align;
                t.textWrappingMode = TMPro.TextWrappingModes.Normal;
                var trt = go.GetComponent<RectTransform>();
                trt.sizeDelta = new Vector2(0, size * 1.35f);
                return t;
            };

            // Font sizes: at 400px wide canvas, 0.001 scale → 36px ≈ 36mm at viewing distance
            objectNameText = MakeTMP("Name",  36, new Color(1f, 1f, 1f, 1f),      TextAlignmentOptions.TopLeft);
            objectTypeText = MakeTMP("Type",  22, new Color(0.6f, 0.8f, 1f, 0.9f), TextAlignmentOptions.TopLeft);

            // Divider
            var divGO = new GameObject("Divider");
            divGO.transform.SetParent(layout.transform, false);
            var divImg = divGO.AddComponent<Image>();
            divImg.color = new Color(0.2f, 0.5f, 1f, 0.3f);
            divImg.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
            var divLE = divGO.AddComponent<LayoutElement>();
            divLE.preferredHeight = 1;

            stat1Text = MakeTMP("Stat1", 20, new Color(0.9f, 0.9f, 0.9f, 0.85f), TextAlignmentOptions.TopLeft);
            stat2Text = MakeTMP("Stat2", 20, new Color(0.9f, 0.9f, 0.9f, 0.85f), TextAlignmentOptions.TopLeft);
            stat3Text = MakeTMP("Stat3", 20, new Color(0.9f, 0.9f, 0.9f, 0.85f), TextAlignmentOptions.TopLeft);

            // Second divider
            var div2GO = new GameObject("Divider2");
            div2GO.transform.SetParent(layout.transform, false);
            var div2Img = div2GO.AddComponent<Image>();
            div2Img.color = new Color(0.2f, 0.5f, 1f, 0.3f);
            div2Img.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
            var div2LE = div2GO.AddComponent<LayoutElement>();
            div2LE.preferredHeight = 1;

            descriptionText = MakeTMP("Desc", 17, new Color(0.8f, 0.85f, 0.95f, 0.8f), TextAlignmentOptions.TopLeft);
            descriptionText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            descriptionText.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 110);

            // Real Scale button
            var btnGO = new GameObject("RealScaleBtn");
            btnGO.transform.SetParent(layout.transform, false);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.35f, 0.8f, 0.85f);
            var btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 30;

            realScaleButton = btnGO.AddComponent<Button>();
            realScaleButton.targetGraphic = btnImg;
            realScaleButton.onClick.AddListener(OnRealScaleButtonPressed);

            realScaleLabel = MakeChildTMP(btnGO.transform, "BtnLabel", 15,
                Color.white, TextAlignmentOptions.Center);

            realScaleLabel.text = "Real Scale";

            Debug.Log("[InspectionPanel] Auto-created world-space panel UI.");
        }

        private static TextMeshProUGUI MakeChildTMP(Transform parent, string name,
            int size, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize  = size;
            t.color     = col;
            t.alignment = align;
            return t;
        }

        private static void SetRectFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
