using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

        private CanvasGroup  _canvasGroup;
        private Camera       _xrCamera;
        private Coroutine    _fadeCoroutine;
        private bool         _visible;
        private bool         _realScaleActive;
        private CelestialBody _currentBody;
        private RawImage      _constellationArtImage;

        // Ray interaction (mirrors ControlPanel approach)
        private Transform    _rightControllerTransform;
        private InputAction  _triggerAction;
        private bool         _triggerWasDown;
        private float        _btnDwellTimer;
        private bool         _btnHovered;
        private GameObject   _realScaleBtnColliderGO;
        private LineRenderer _rayLine;
        private Image        _realScaleBtnBg;   // saved in AutoCreateUI for collider sync

        private static readonly Color ColBtnIdle  = new Color(0.15f, 0.35f, 0.8f,  0.85f);
        private static readonly Color ColBtnHover = new Color(0.30f, 0.60f, 1.0f,  1.00f);

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

        private void Start()
        {
            _triggerAction = new InputAction("IPanelTrigger", InputActionType.Button,
                binding: "<XRController>{RightHand}/triggerButton");
            _triggerAction.Enable();

            BuildRayLine();
            BuildRealScaleCollider();
        }

        private void OnDestroy()
        {
            _triggerAction?.Disable();
            _triggerAction?.Dispose();
        }

        private void Update()
        {
            if (!_visible) return;
            HandleRayInteraction();
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

            SyncRealScaleCollider();
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

            // Enable collider only when body has physical size (same condition as button visibility)
            if (_realScaleBtnColliderGO != null)
                _realScaleBtnColliderGO.SetActive(body.physicalRadiusKm > 0f);

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeTo(1f));
        }

        public void Hide()
        {
            _visible = false;
            _currentBody = null;
            _realScaleActive = false;
            UpdateRealScaleButton();
            ClearBtnHover();
            if (_rayLine != null) _rayLine.enabled = false;
            if (_realScaleBtnColliderGO != null) _realScaleBtnColliderGO.SetActive(false);

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

            // Constellation artwork image
            if (_constellationArtImage != null)
            {
                if (body.bodyType == CelestialBodyType.Constellation)
                {
                    string pngName = body.objectName.ToLower().Replace(" ", "-");
                    var tex = Resources.Load<Texture2D>("ConstellationArt/" + pngName);
                    if (tex != null)
                    {
                        _constellationArtImage.texture = tex;
                        _constellationArtImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        _constellationArtImage.gameObject.SetActive(false);
                    }
                }
                else
                {
                    _constellationArtImage.gameObject.SetActive(false);
                }
            }

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

        // ── Ray interaction ───────────────────────────────────────────────────────

        private void HandleRayInteraction()
        {
            if (_realScaleBtnColliderGO == null || !_realScaleBtnColliderGO.activeSelf) return;

            Ray  ray;
            bool hasCtrl = TryGetControllerRay(out ray);
            if (!hasCtrl) ray = new Ray(_xrCamera.transform.position, _xrCamera.transform.forward);

            // Ray visual
            if (_rayLine != null)
            {
                _rayLine.enabled = hasCtrl;
                if (hasCtrl)
                {
                    _rayLine.SetPosition(0, ray.origin);
                    _rayLine.SetPosition(1, ray.origin + ray.direction * 3f);
                }
            }

            float    castR  = hasCtrl ? 0f : 0.08f;
            bool     didHit = false;
            RaycastHit h;
            if (castR > 0f)
                didHit = Physics.SphereCast(ray, castR, out h, 800f) && h.collider.gameObject == _realScaleBtnColliderGO;
            else
                didHit = Physics.Raycast(ray, out h, 800f) && h.collider.gameObject == _realScaleBtnColliderGO;

            if (hasCtrl && _rayLine != null && _rayLine.enabled)
                _rayLine.SetPosition(1, didHit ? h.point : ray.origin + ray.direction * 3f);

            // Hover state
            if (didHit != _btnHovered)
            {
                _btnHovered   = didHit;
                _btnDwellTimer = 0f;
                if (_realScaleBtnBg != null)
                    _realScaleBtnBg.color = didHit ? ColBtnHover : ColBtnIdle;
            }

            if (!_btnHovered) { _triggerWasDown = GetRightTrigger(); return; }

            // Trigger = instant press
            bool trig     = GetRightTrigger();
            bool tPressed = trig && !_triggerWasDown;
            _triggerWasDown = trig;
            if (tPressed) { OnRealScaleButtonPressed(); return; }

            // Dwell fallback (1.5 s)
            _btnDwellTimer += Time.deltaTime;
            float t = _btnDwellTimer / 1.5f;
            if (_realScaleBtnBg != null)
                _realScaleBtnBg.color = Color.Lerp(ColBtnIdle, ColBtnHover, t);
            if (_btnDwellTimer >= 1.5f) { _btnDwellTimer = 0f; OnRealScaleButtonPressed(); }
        }

        private void ClearBtnHover()
        {
            _btnHovered    = false;
            _btnDwellTimer = 0f;
            if (_realScaleBtnBg != null) _realScaleBtnBg.color = ColBtnIdle;
        }

        private void BuildRealScaleCollider()
        {
            // Canvas is 400 px wide × scale 0.001 → 0.40 m wide in world space.
            // Button height is 30 px = 0.030 m. Use slightly smaller box.
            var go = new GameObject("IPanelRealScaleBtn");
            go.transform.SetParent(transform, false);
            var bc   = go.AddComponent<BoxCollider>();
            bc.size  = new Vector3(0.36f, 0.026f, 0.01f);
            // Marker so SyncRealScaleCollider can find it easily
            go.AddComponent<IPanelBtnMarker>();
            _realScaleBtnColliderGO = go;
            go.SetActive(false); // enabled by Show()
        }

        private void SyncRealScaleCollider()
        {
            if (_realScaleBtnColliderGO == null || _realScaleBtnBg == null) return;
            if (!_realScaleBtnColliderGO.activeSelf) return;
            var rt = _realScaleBtnBg.rectTransform;
            Vector3 wc = rt.TransformPoint(rt.rect.center);
            _realScaleBtnColliderGO.transform.position = wc + transform.forward * -0.005f;
            _realScaleBtnColliderGO.transform.rotation = transform.rotation;
        }

        private void BuildRayLine()
        {
            var go = new GameObject("IPanelRayLine");
            go.transform.SetParent(transform, false);
            _rayLine = go.AddComponent<LineRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh); mat.color = new Color(0.35f, 0.80f, 1f, 0.85f);
            _rayLine.material     = mat;
            _rayLine.startWidth   = 0.004f;
            _rayLine.endWidth     = 0.001f;
            _rayLine.positionCount = 2;
            _rayLine.useWorldSpace = true;
            _rayLine.enabled       = false;
        }

        private bool TryGetControllerRay(out Ray ray)
        {
            ray = default;
            if (_rightControllerTransform == null)
                _rightControllerTransform = FindRightControllerTransform();
            if (_rightControllerTransform == null) return false;
            ray = new Ray(_rightControllerTransform.position, _rightControllerTransform.forward);
            return true;
        }

        private static Transform FindRightControllerTransform()
        {
            string[] candidates = {
                "Right Controller", "RightHand Controller", "Right Hand Controller",
                "Right Controller Stabilized", "Right Interaction Visual"
            };
            foreach (var n in candidates)
            {
                var go = GameObject.Find(n);
                if (go != null) return go.transform;
            }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var n = t.name.ToLower();
                if (n.Contains("right") && n.Contains("controller") && !n.Contains("teleport"))
                    return t;
            }
            return null;
        }

        private bool GetRightTrigger() => _triggerAction != null && _triggerAction.IsPressed();

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

            // 400 × 600 pixels at scale 0.001 = 0.40 m × 0.60 m physical panel
            // Extra height accommodates the constellation art image (160 px)
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta  = new Vector2(400, 600);
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

            // Constellation artwork image — hidden for non-constellation bodies
            var artGO = new GameObject("ConstellationArt");
            artGO.transform.SetParent(layout.transform, false);
            _constellationArtImage = artGO.AddComponent<RawImage>();
            _constellationArtImage.color = new Color(0.85f, 0.92f, 1f, 0.92f);  // cool blue-white tint

            // Additive blend so the black PNG background vanishes, matching the hologram quad material.
            // SrcAlpha + One: bright art glows, black (alpha≈0) disappears against the panel background.
            var artSh  = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var artMat = new Material(artSh);
            artMat.SetFloat("_Surface",  1f);  // Transparent
            artMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            artMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);  // additive dest
            artMat.SetFloat("_ZWrite",   0f);
            artMat.SetFloat("_Cull",     0f);
            artMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            artMat.renderQueue = 3000;
            _constellationArtImage.material = artMat;

            var artLE = artGO.AddComponent<LayoutElement>();
            artLE.preferredHeight = 160;
            artGO.SetActive(false); // shown only for constellations

            descriptionText = MakeTMP("Desc", 17, new Color(0.8f, 0.85f, 0.95f, 0.8f), TextAlignmentOptions.TopLeft);
            descriptionText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            descriptionText.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 110);

            // Real Scale button
            var btnGO = new GameObject("RealScaleBtn");
            btnGO.transform.SetParent(layout.transform, false);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = ColBtnIdle;
            _realScaleBtnBg = btnImg;
            var btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 30;

            realScaleButton = btnGO.AddComponent<Button>();
            realScaleButton.targetGraphic = btnImg;
            realScaleButton.onClick.AddListener(OnRealScaleButtonPressed);

            realScaleLabel = MakeChildTMP(btnGO.transform, "BtnLabel", 15,
                Color.white, TextAlignmentOptions.Center);

            realScaleLabel.text = "Real Scale";
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

    /// <summary>Marks the InspectionPanel Real Scale button collider GO for identification.</summary>
    internal class IPanelBtnMarker : MonoBehaviour { }
}
