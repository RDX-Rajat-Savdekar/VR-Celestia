using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CelestiaVR.Core;
using CelestiaVR.Interaction;
using CelestiaVR.Constellations;
using CelestiaVR.Audio;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Unified HUD control panel — replaces CelestialSearchPanel and the
    /// ViewingModeManager floating badge with a single world-space canvas.
    ///
    /// Sections:
    ///   • MODE       — Observe / Inspect toggle
    ///   • VISIBILITY — Constellation lines, art, planet labels on/off
    ///   • FIND IN SKY — Scrollable search list (all celestial bodies)
    ///
    /// Open/close with Left X button.
    /// Interact via right-controller ray + trigger (instant) or gaze + 0.6 s dwell.
    /// Scroll with right thumbstick Y.
    ///
    /// Auto-created by StargazingSceneBootstrap.
    /// </summary>
    public class ControlPanel : MonoBehaviour
    {
        public static ControlPanel Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Placement")]
        public float panelDistance = 1.20f;
        public float panelUpOffset = 0.05f;

        [Header("Search limits")]
        public int maxNamedStars     = 30;
        public int maxConstellations = 88;

        // ── Layout constants (all in canvas pixels) ───────────────────────────────
        private const float CanvasW     = 560f;
        private const float CanvasScale = 0.001f;   // 1 px = 1 mm  →  560 px = 56 cm
        private const float PadX        = 14f;
        private const float ContentW    = CanvasW - PadX * 2f;
        private const float RowH        = 34f;
        private const float CatH        = 26f;
        private const float ViewportH   = 240f;   // scroll viewport height

        // ── Colours ───────────────────────────────────────────────────────────────
        private static readonly Color ColBg         = new Color(0.00f, 0.00f, 0.00f, 0.97f);  // solid black
        private static readonly Color ColBorder      = new Color(0.25f, 0.55f, 1.00f, 0.60f);
        private static readonly Color ColSection     = new Color(0.40f, 0.60f, 1.00f, 0.70f);
        private static readonly Color ColItemBg      = new Color(0.08f, 0.14f, 0.28f, 0.70f);
        private static readonly Color ColItemBgHov   = new Color(0.20f, 0.55f, 1.00f, 0.95f);  // bright blue on hover
        private static readonly Color ColActiveModeBtn  = new Color(0.15f, 0.45f, 1.00f, 0.90f);
        private static readonly Color ColIdleModeBtn    = new Color(0.10f, 0.16f, 0.30f, 0.80f);
        private static readonly Color ColToggleOn    = new Color(0.20f, 0.80f, 1.00f, 1.00f);
        private static readonly Color ColToggleOff   = new Color(0.18f, 0.24f, 0.38f, 0.80f);
        private static readonly Color ColText        = new Color(0.88f, 0.93f, 1.00f, 1.00f);
        private static readonly Color ColCloseBg     = new Color(0.50f, 0.18f, 0.18f, 0.80f);

        // ── Button action enum ────────────────────────────────────────────────────
        public enum ButtonAction
        {
            Close, SetObserveMode, SetInspectMode,
            ToggleConstellationLines, ToggleConstellationArt, TogglePlanetLabels,
            ToggleSound,
            ToggleDwellStars, ToggleDwellPlanets, ToggleDwellDeepSky,
            SelectSearchItem,
        }

        // Per-button metadata
        private struct ButtonEntry
        {
            public ButtonAction  action;
            public CelestialBody searchTarget;
            public GameObject    colliderGO;
            public Image         bgImage;
            public float         contentY;     // pixels from top of scroll-content (search items only)
            public bool          matchFilter;  // set by search filter
        }

        // ── Runtime ───────────────────────────────────────────────────────────────
        private Camera  _cam;
        private bool    _isOpen, _built, _pendingOpen;

        private GameObject   _panelRoot;
        private Image        _observeBtnBg, _inspectBtnBg;
        private Image        _linesDot, _artDot, _labelsDot, _soundDot;
        private Image        _dwellStarsDot, _dwellPlanetsDot, _dwellDeepSkyDot;
        private TMP_InputField _searchInput;
        private RectTransform  _contentRT;
        private float _scrollOffset, _maxScroll;

        private readonly List<ButtonEntry> _buttons = new();

        // Interaction
        private Transform  _rightControllerTransform;
        private InputAction _triggerAction;
        private InputAction _scrollAction;
        private bool   _triggerWasDown;
        private float  _dwellTimer;
        private int    _hoveredIndex = -1;
        private LineRenderer _rayLine;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _cam = Camera.main;
            BuildRayLine();

            _triggerAction = new InputAction("CPanelTrigger", InputActionType.Button,
                binding: "<XRController>{RightHand}/triggerButton");
            _triggerAction.Enable();

            _scrollAction = new InputAction("CPanelScroll", InputActionType.Value,
                binding: "<XRController>{RightHand}/thumbstick");
            _scrollAction.Enable();

            StartCoroutine(DelayedBuild());
        }

        private void OnDestroy()
        {
            _triggerAction?.Disable(); _triggerAction?.Dispose();
            _scrollAction?.Disable();  _scrollAction?.Dispose();
        }

        private IEnumerator DelayedBuild()
        {
            yield return new WaitForSeconds(3.5f);
            BuildPanel();
            _built = true;
            if (_pendingOpen) { _pendingOpen = false; Open(); }
        }

        private void Update()
        {
            if (!_isOpen || _panelRoot == null) return;
            if (_cam == null) _cam = Camera.main;
            HandleScroll();
            HandleInteraction();
        }

        private void LateUpdate()
        {
            if (_isOpen) SyncColliderPositions();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public bool IsOpen => _isOpen;

        /// <summary>Activate a button by action type. Used by ControlPanelTester and tutorial.</summary>
        public void SimulatePress(ButtonAction action, CelestialBody target = null)
        {
            // For SelectSearchItem we need to find a matching entry by target name
            for (int i = 0; i < _buttons.Count; i++)
            {
                var b = _buttons[i];
                if (b.action != action) continue;
                if (action == ButtonAction.SelectSearchItem && target != null && b.searchTarget != target) continue;
                Activate(i);
                return;
            }
        }

        /// <summary>Returns all distinct search-item targets (used by tester to pick one).</summary>
        public List<CelestialBody> GetSearchTargets()
        {
            var result = new List<CelestialBody>();
            foreach (var b in _buttons)
                if (b.action == ButtonAction.SelectSearchItem && b.searchTarget != null)
                    result.Add(b.searchTarget);
            return result;
        }

        public void ToggleOpen()
        {
            if (_isOpen) Close();
            else if (_built) Open();
            else _pendingOpen = true;
        }

        public void Open()
        {
            if (_panelRoot == null) return;
            _isOpen = true;
            SoundManager.Instance?.Play(SoundEvent.PanelOpen);

            // Snap to camera
            _panelRoot.transform.position =
                _cam.transform.position
                + _cam.transform.forward * panelDistance
                + _cam.transform.up      * panelUpOffset;
            _panelRoot.transform.rotation = Quaternion.LookRotation(
                _panelRoot.transform.position - _cam.transform.position);

            _panelRoot.SetActive(true);
            _scrollOffset = 0f;
            ApplyScroll();

            if (_searchInput != null) _searchInput.text = "";
            ResetSearchFilter();
            RefreshModeButtons();
            RefreshToggleDots();

            Canvas.ForceUpdateCanvases();
            SyncColliderPositions();
        }

        public void Close()
        {
            _isOpen = false;
            SoundManager.Instance?.Play(SoundEvent.PanelClose);
            if (_panelRoot != null) _panelRoot.SetActive(false);
            ClearHover();
            if (_rayLine != null) _rayLine.enabled = false;
        }

        // ── Panel build ───────────────────────────────────────────────────────────

        private void BuildPanel()
        {
            // Collect bodies
            var planets = new List<CelestialBody>();
            var deepSky = new List<CelestialBody>();
            var stars   = new List<CelestialBody>();
            var consts  = new List<CelestialBody>();

            foreach (var b in FindObjectsByType<CelestialBody>(FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(b.objectName)) continue;
                switch (b.bodyType)
                {
                    case CelestialBodyType.Planet:        planets.Add(b); break;
                    case CelestialBodyType.DeepSkyObject: deepSky.Add(b); break;
                    case CelestialBodyType.Star:          stars.Add(b);   break;
                    case CelestialBodyType.Constellation: consts.Add(b);  break;
                }
            }
            planets.Sort((a,b) => string.Compare(a.objectName, b.objectName));
            deepSky.Sort((a,b) => string.Compare(a.objectName, b.objectName));
            stars.Sort(  (a,b) => a.magnitude.CompareTo(b.magnitude));
            consts.Sort( (a,b) => string.Compare(a.objectName, b.objectName));
            if (maxNamedStars     > 0 && stars.Count  > maxNamedStars)
                stars.RemoveRange(maxNamedStars,  stars.Count  - maxNamedStars);
            if (maxConstellations > 0 && consts.Count > maxConstellations)
                consts.RemoveRange(maxConstellations, consts.Count - maxConstellations);

            // ── Root GO ───────────────────────────────────────────────────────────
            _panelRoot = new GameObject("[ControlPanel]");

            // Fixed canvas height
            float staticH = 40f + 2f               // header + div
                          + 26f + 42f + 2f          // mode label + buttons + div
                          + 26f + 32f*4 + 2f        // visibility label + 4 toggles + div
                          + 26f + 32f*3 + 2f        // dwell label + 3 toggles + div
                          + 26f + 36f + ViewportH   // search label + input + list
                          + 14f;                    // bottom pad

            // ── Canvas ────────────────────────────────────────────────────────────
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(_panelRoot.transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = _cam;
            canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;
            canvasGO.AddComponent<CanvasGroup>();

            var canvasRT = (RectTransform)canvasGO.transform;
            canvasRT.sizeDelta  = new Vector2(CanvasW, staticH);
            canvasRT.localScale = Vector3.one * CanvasScale;

            // Background
            MakeStretchImage(canvasGO.transform, "BG",     ColBg);
            var bord = MakeStretchRaw(canvasGO.transform, "Border");
            bord.anchorMin = Vector2.zero; bord.anchorMax = Vector2.one;
            bord.offsetMin = new Vector2(-2,-2); bord.offsetMax = new Vector2(2,2);
            bord.gameObject.AddComponent<Image>().color = ColBorder;

            // Vertical layout container
            var layoutRT = UIChild(canvasGO.transform, "Layout");
            layoutRT.anchorMin = new Vector2(0,1); layoutRT.anchorMax = new Vector2(1,1);
            layoutRT.pivot     = new Vector2(0.5f,1f);
            layoutRT.offsetMin = new Vector2(PadX,0); layoutRT.offsetMax = new Vector2(-PadX,0);
            layoutRT.anchoredPosition = new Vector2(0,-8f);
            var vlg = layoutRT.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing=0; vlg.childControlWidth=true; vlg.childControlHeight=false;
            vlg.childForceExpandWidth=true; vlg.childForceExpandHeight=false;
            Transform L = layoutRT;

            // ── HEADER ────────────────────────────────────────────────────────────
            AddRow(L, "Header", 40f, row =>
            {
                var titleRT = UIChild(row, "Title"); SetStretch(titleRT, 0, 0);
                MkTMPUGUI(titleRT.gameObject, "CELESTIA VR", 20f, ColSection, FontStyles.Bold,
                    TextAlignmentOptions.Left);

                var closeRT = UIChild(row, "CloseCell");
                closeRT.anchorMin = new Vector2(0.70f,0.08f); closeRT.anchorMax = new Vector2(1f,0.92f);
                closeRT.offsetMin = closeRT.offsetMax = Vector2.zero;
                var closeBg = closeRT.gameObject.AddComponent<Image>(); closeBg.color = ColCloseBg;
                MkTMPUGUI(UIChild(closeRT,"T").gameObject, "X  CLOSE", 12f,
                    new Color(1f,0.7f,0.7f,1f), FontStyles.Normal, TextAlignmentOptions.Center,
                    stretch:true);
                float cw = ContentW * 0.30f * CanvasScale, ch = 36f * CanvasScale;
                AddBtn(ButtonAction.Close, null, closeRT, closeBg, -1f, cw, ch);
            });

            AddDiv(L);

            // ── MODE ─────────────────────────────────────────────────────────────
            AddRow(L, "ModeLbl", 26f, row =>
                MkLabel(UIChild(row,"L").gameObject, "MODE", 12f, ColSection));

            AddRow(L, "ModeRow", 42f, row =>
            {
                // OBSERVE
                var obsRT = UIChild(row,"ObsCell");
                obsRT.anchorMin=new Vector2(0,0.08f); obsRT.anchorMax=new Vector2(0.48f,0.92f);
                obsRT.offsetMin=obsRT.offsetMax=Vector2.zero;
                _observeBtnBg = obsRT.gameObject.AddComponent<Image>();
                MkTMPUGUI(UIChild(obsRT,"T").gameObject, "OBSERVE", 13f, ColText,
                    FontStyles.Bold, TextAlignmentOptions.Center, stretch:true);
                float bw = ContentW*0.46f*CanvasScale, bh = 36f*CanvasScale;
                AddBtn(ButtonAction.SetObserveMode, null, obsRT, _observeBtnBg, -1f, bw, bh);

                // INSPECT
                var insRT = UIChild(row,"InsCell");
                insRT.anchorMin=new Vector2(0.52f,0.08f); insRT.anchorMax=new Vector2(1f,0.92f);
                insRT.offsetMin=insRT.offsetMax=Vector2.zero;
                _inspectBtnBg = insRT.gameObject.AddComponent<Image>();
                MkTMPUGUI(UIChild(insRT,"T").gameObject, "INSPECT", 13f, ColText,
                    FontStyles.Bold, TextAlignmentOptions.Center, stretch:true);
                AddBtn(ButtonAction.SetInspectMode, null, insRT, _inspectBtnBg, -1f, bw, bh);
            });

            AddDiv(L);

            // ── VISIBILITY ────────────────────────────────────────────────────────
            AddRow(L, "VisLbl", 26f, row =>
                MkLabel(UIChild(row,"L").gameObject, "VISIBILITY", 12f, ColSection));

            _linesDot  = BuildToggleRow(L, "Lines",  "Constellation Lines",  ButtonAction.ToggleConstellationLines);
            _artDot    = BuildToggleRow(L, "Art",    "Constellation Art",    ButtonAction.ToggleConstellationArt);
            _labelsDot = BuildToggleRow(L, "Labels", "Planet Labels",        ButtonAction.TogglePlanetLabels);
            _soundDot  = BuildToggleRow(L, "Sound",  "Sound Effects",        ButtonAction.ToggleSound);

            AddDiv(L);

            // ── DWELL FILTER ──────────────────────────────────────────────────────
            AddRow(L, "DwellLbl", 26f, row =>
                MkLabel(UIChild(row,"L").gameObject, "DWELL SELECT", 12f, ColSection));

            _dwellStarsDot    = BuildToggleRow(L, "DwStar",  "Stars",       ButtonAction.ToggleDwellStars);
            _dwellPlanetsDot  = BuildToggleRow(L, "DwPlanet","Planets",     ButtonAction.ToggleDwellPlanets);
            _dwellDeepSkyDot  = BuildToggleRow(L, "DwDSO",   "Galaxies / DSO", ButtonAction.ToggleDwellDeepSky);

            AddDiv(L);

            // ── SEARCH ────────────────────────────────────────────────────────────
            AddRow(L, "SearchLbl", 26f, row =>
                MkLabel(UIChild(row,"L").gameObject, "FIND IN SKY", 12f, ColSection));

            // Search input field
            AddRow(L, "SearchInput", 36f, row =>
            {
                MakeStretchImage(row, "InputBG", new Color(0.08f,0.14f,0.26f,0.90f));

                var phRT = UIChild(row,"Placeholder"); SetStretch(phRT,10,4);
                var ph = phRT.gameObject.AddComponent<TextMeshProUGUI>();
                ph.text="Type to filter…"; ph.fontSize=13f; ph.fontStyle=FontStyles.Italic;
                ph.color=new Color(0.35f,0.45f,0.60f,0.75f);
                ph.alignment=TextAlignmentOptions.Left;
                ph.textWrappingMode=TMPro.TextWrappingModes.NoWrap;

                var itRT = UIChild(row,"InputText"); SetStretch(itRT,10,4);
                var itTmp = itRT.gameObject.AddComponent<TextMeshProUGUI>();
                itTmp.fontSize=13f; itTmp.color=ColText;
                itTmp.alignment=TextAlignmentOptions.Left;
                itTmp.textWrappingMode=TMPro.TextWrappingModes.NoWrap;

                _searchInput = row.gameObject.AddComponent<TMP_InputField>();
                _searchInput.textComponent = itTmp;
                _searchInput.placeholder   = ph;
                _searchInput.characterLimit= 40;
                _searchInput.lineType      = TMP_InputField.LineType.SingleLine;
                _searchInput.onValueChanged.AddListener(OnSearchChanged);
            });

            // Scroll viewport
            var vpRT = UIChild(L, "Viewport");
            vpRT.sizeDelta = new Vector2(0, ViewportH);
            var vpLE = vpRT.gameObject.AddComponent<LayoutElement>();
            vpLE.minHeight=ViewportH; vpLE.preferredHeight=ViewportH;
            vpRT.gameObject.AddComponent<RectMask2D>();

            // Content (grows downward inside viewport)
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(vpRT, false);
            _contentRT = (RectTransform)contentGO.transform;
            _contentRT.anchorMin=new Vector2(0,1); _contentRT.anchorMax=new Vector2(1,1);
            _contentRT.pivot=new Vector2(0.5f,1f);
            _contentRT.offsetMin=_contentRT.offsetMax=Vector2.zero;
            _contentRT.anchoredPosition=Vector2.zero;
            var cVLG = contentGO.AddComponent<VerticalLayoutGroup>();
            cVLG.spacing=0; cVLG.childControlWidth=true; cVLG.childControlHeight=false;
            cVLG.childForceExpandWidth=true; cVLG.childForceExpandHeight=false;

            // Populate categories
            float absY = 0f;
            float contentH = 0f;
            contentH += BuildSearchCategory(contentGO.transform,"PLANETS",
                new Color(1.00f,0.85f,0.35f,1f), planets, ref absY);
            contentH += BuildSearchCategory(contentGO.transform,"DEEP SKY",
                new Color(0.40f,0.80f,1.00f,1f), deepSky, ref absY);
            contentH += BuildSearchCategory(contentGO.transform,"NAMED STARS",
                new Color(1.00f,1.00f,0.65f,1f), stars, ref absY);
            contentH += BuildSearchCategory(contentGO.transform,"CONSTELLATIONS",
                new Color(0.55f,0.75f,1.00f,1f), consts, ref absY);

            _contentRT.sizeDelta = new Vector2(0, contentH);
            _maxScroll = Mathf.Max(0f, contentH - ViewportH);

            _panelRoot.SetActive(false);
        }

        // ── Search category builder ───────────────────────────────────────────────

        private float BuildSearchCategory(Transform content, string label, Color accent,
            List<CelestialBody> bodies, ref float absY)
        {
            if (bodies.Count == 0) return 0f;
            float h = 0f;

            // Category header row (not interactive)
            AddRow(content, $"Cat_{label}", CatH, row =>
            {
                var rt = UIChild(row,"L"); SetStretch(rt,4,2);
                var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
                t.text=$"> {label} ({bodies.Count})"; t.fontSize=12f;
                t.fontStyle=FontStyles.Bold; t.color=accent;
                t.alignment=TextAlignmentOptions.Left;
                t.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
            });
            absY += CatH; h += CatH;

            float itemW = ContentW * CanvasScale;
            float itemH = (RowH - 4f) * CanvasScale;

            foreach (var body in bodies)
            {
                float capturedY = absY;
                var   capturedB = body;

                AddRow(content, $"Item_{body.objectName}", RowH, row =>
                {
                    var rowBg = row.gameObject.AddComponent<Image>(); rowBg.color = ColItemBg;

                    // Colour dot
                    var dotRT = UIChild(row,"Dot");
                    dotRT.anchorMin=new Vector2(0,0.5f); dotRT.anchorMax=new Vector2(0,0.5f);
                    dotRT.sizeDelta=new Vector2(6,6); dotRT.anchoredPosition=new Vector2(9,0);
                    dotRT.gameObject.AddComponent<Image>().color = accent;

                    // Name
                    var nameRT = UIChild(row,"Name");
                    nameRT.anchorMin=Vector2.zero; nameRT.anchorMax=Vector2.one;
                    nameRT.offsetMin=new Vector2(20,0); nameRT.offsetMax=new Vector2(-70,0);
                    var nameTmp = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
                    nameTmp.text=capturedB.objectName; nameTmp.fontSize=13f;
                    nameTmp.color=ColText; nameTmp.alignment=TextAlignmentOptions.Left;
                    nameTmp.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
                    nameTmp.overflowMode=TextOverflowModes.Ellipsis;

                    // Type badge
                    var typeRT = UIChild(row,"Type");
                    typeRT.anchorMin=new Vector2(0.72f,0.1f); typeRT.anchorMax=new Vector2(1f,0.9f);
                    typeRT.offsetMin=typeRT.offsetMax=Vector2.zero;
                    var typeT = typeRT.gameObject.AddComponent<TextMeshProUGUI>();
                    typeT.text=ShortType(capturedB.bodyType); typeT.fontSize=10f;
                    typeT.color=new Color(accent.r,accent.g,accent.b,0.70f);
                    typeT.alignment=TextAlignmentOptions.Right;

                    AddBtn(ButtonAction.SelectSearchItem, capturedB,
                        (RectTransform)row, rowBg, capturedY, itemW, itemH);
                });
                absY += RowH; h += RowH;
            }

            absY += 6f; h += 6f;  // section gap
            return h;
        }

        // ── Scrolling ─────────────────────────────────────────────────────────────

        private void HandleScroll()
        {
            if (_maxScroll <= 0f || _contentRT == null) return;
            var stick = _scrollAction?.ReadValue<Vector2>() ?? Vector2.zero;
            if (Mathf.Abs(stick.y) > 0.12f)
            {
                _scrollOffset = Mathf.Clamp(
                    _scrollOffset - stick.y * Time.deltaTime * 260f, 0f, _maxScroll);
                ApplyScroll();
            }
        }

        private void ApplyScroll()
        {
            if (_contentRT != null)
                _contentRT.anchoredPosition = new Vector2(0f, _scrollOffset);
        }

        // ── Interaction ───────────────────────────────────────────────────────────

        private void HandleInteraction()
        {
            Ray ray;
            bool ctrl = TryGetControllerRay(out ray);
            if (ctrl) { if (_rayLine != null) { _rayLine.enabled=true; _rayLine.SetPosition(0,ray.origin); _rayLine.SetPosition(1,ray.origin+ray.direction*4f); } }
            else      { if (_rayLine != null) _rayLine.enabled=false; ray=new Ray(_cam.transform.position,_cam.transform.forward); }

            int     hitIdx  = -1;
            Vector3 hitPt   = ray.origin + ray.direction * 4f;
            float   castR   = ctrl ? 0f : 0.10f;

            RaycastHit h;
            bool didHit = castR > 0f
                ? Physics.SphereCast(ray, castR, out h, 800f)
                : Physics.Raycast(ray, out h, 800f);

            if (didHit)
            {
                hitPt  = h.point;
                hitIdx = FindBtnIndex(h.collider.gameObject);
            }
            if (_rayLine != null && _rayLine.enabled) _rayLine.SetPosition(1, hitPt);

            // Hover change
            if (hitIdx != _hoveredIndex)
            {
                ClearHover();
                _hoveredIndex = hitIdx;
                _dwellTimer   = 0f;
                if (_hoveredIndex >= 0) SetHoverColor(_hoveredIndex, ColItemBgHov);
            }

            if (_hoveredIndex < 0) { _triggerWasDown = GetRightTrigger(); return; }

            // Trigger = instant select
            bool trig = GetRightTrigger();
            bool tPressed = trig && !_triggerWasDown;
            _triggerWasDown = trig;

            if (tPressed) { Activate(_hoveredIndex); return; }

            // Dwell fallback
            _dwellTimer += Time.deltaTime;
            float t = _dwellTimer / 0.6f;
            var e = _buttons[_hoveredIndex];
            if (e.bgImage != null) e.bgImage.color = Color.Lerp(ColItemBg, ColItemBgHov, t);

            if (_dwellTimer >= 0.6f) { _dwellTimer=0f; Activate(_hoveredIndex); }
        }

        private void Activate(int index)
        {
            if (index < 0 || index >= _buttons.Count) return;
            var b = _buttons[index];
            ClearHover();

            // Play a click for all buttons except ToggleSound (which manages its own mute state)
            if (b.action != ButtonAction.ToggleSound)
                SoundManager.Instance?.Play(SoundEvent.ButtonPress);

            switch (b.action)
            {
                case ButtonAction.Close:
                    Close(); break;

                case ButtonAction.SetObserveMode:
                    ViewingModeManager.Instance?.SetMode(ViewingModeManager.Mode.Observe);
                    RefreshModeButtons(); break;

                case ButtonAction.SetInspectMode:
                    ViewingModeManager.Instance?.SetMode(ViewingModeManager.Mode.Inspect);
                    RefreshModeButtons(); break;

                case ButtonAction.ToggleConstellationLines:
                    if (StellariumLoader.Instance != null)
                    { StellariumLoader.Instance.SetLinesVisible(!StellariumLoader.Instance.AreLinesVisible); RefreshToggleDots(); }
                    break;

                case ButtonAction.ToggleConstellationArt:
                    if (StellariumLoader.Instance != null)
                    { StellariumLoader.Instance.SetArtVisible(!StellariumLoader.Instance.IsArtVisible); RefreshToggleDots(); }
                    break;

                case ButtonAction.TogglePlanetLabels:
                    if (SkyLabelManager.Instance != null)
                    { SkyLabelManager.Instance.SetPlanetLabelsVisible(!SkyLabelManager.Instance.ArePlanetLabelsVisible); RefreshToggleDots(); }
                    break;

                case ButtonAction.ToggleSound:
                    SoundManager.Instance?.ToggleMute();
                    RefreshToggleDots();
                    break;

                case ButtonAction.ToggleDwellStars:
                case ButtonAction.ToggleDwellPlanets:
                case ButtonAction.ToggleDwellDeepSky:
                {
                    var dwell = FindFirstObjectByType<DwellSelector>();
                    if (dwell != null)
                    {
                        if (b.action == ButtonAction.ToggleDwellStars)    dwell.dwellStars   = !dwell.dwellStars;
                        if (b.action == ButtonAction.ToggleDwellPlanets)  dwell.dwellPlanets = !dwell.dwellPlanets;
                        if (b.action == ButtonAction.ToggleDwellDeepSky)  dwell.dwellDeepSky = !dwell.dwellDeepSky;
                    }
                    RefreshToggleDots();
                    break;
                }

                case ButtonAction.SelectSearchItem:
                    if (b.searchTarget != null)
                    { DirectionalArrow.Instance?.SetTarget(b.searchTarget); Close(); }
                    break;
            }
        }

        // ── Visual refresh ────────────────────────────────────────────────────────

        private void RefreshModeButtons()
        {
            if (_observeBtnBg == null || _inspectBtnBg == null) return;
            bool ins = ViewingModeManager.Instance?.IsInspectMode ?? false;
            _observeBtnBg.color = ins ? ColIdleModeBtn : ColActiveModeBtn;
            _inspectBtnBg.color = ins ? ColActiveModeBtn : ColIdleModeBtn;
        }

        public void RefreshToggleDots()
        {
            var ldr   = StellariumLoader.Instance;
            var lmgr  = SkyLabelManager.Instance;
            var dwell = FindFirstObjectByType<DwellSelector>();
            if (_linesDot  != null) _linesDot.color  = (ldr  != null && ldr.AreLinesVisible) ? ColToggleOn : ColToggleOff;
            if (_artDot    != null) _artDot.color    = (ldr  != null && ldr.IsArtVisible)    ? ColToggleOn : ColToggleOff;
            if (_labelsDot != null) _labelsDot.color = (lmgr != null && lmgr.ArePlanetLabelsVisible) ? ColToggleOn : ColToggleOff;
            if (_soundDot  != null) _soundDot.color  = (SoundManager.Instance != null && !SoundManager.Instance.IsMuted) ? ColToggleOn : ColToggleOff;
            if (_dwellStarsDot   != null) _dwellStarsDot.color   = (dwell != null && dwell.dwellStars)   ? ColToggleOn : ColToggleOff;
            if (_dwellPlanetsDot != null) _dwellPlanetsDot.color = (dwell != null && dwell.dwellPlanets) ? ColToggleOn : ColToggleOff;
            if (_dwellDeepSkyDot != null) _dwellDeepSkyDot.color = (dwell != null && dwell.dwellDeepSky) ? ColToggleOn : ColToggleOff;
        }

        // ── Search filter ─────────────────────────────────────────────────────────

        private void OnSearchChanged(string query)
        {
            query = query.ToLowerInvariant().Trim();
            for (int i = 0; i < _buttons.Count; i++)
            {
                var b = _buttons[i];
                if (b.action != ButtonAction.SelectSearchItem) continue;
                bool show = string.IsNullOrEmpty(query)
                          || b.searchTarget.objectName.ToLowerInvariant().Contains(query)
                          || b.searchTarget.bodyType.ToString().ToLowerInvariant().Contains(query);
                var copy = _buttons[i]; copy.matchFilter = show; _buttons[i] = copy;
                if (b.bgImage    != null) b.bgImage.gameObject.SetActive(show);
                if (b.colliderGO != null) b.colliderGO.SetActive(show);
            }
            Canvas.ForceUpdateCanvases();
            SyncColliderPositions();
        }

        private void ResetSearchFilter()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var b = _buttons[i];
                if (b.action != ButtonAction.SelectSearchItem) continue;
                if (b.bgImage    != null) b.bgImage.gameObject.SetActive(true);
                if (b.colliderGO != null) b.colliderGO.SetActive(true);
                var copy = _buttons[i]; copy.matchFilter = true; _buttons[i] = copy;
            }
        }

        // ── Collider sync ─────────────────────────────────────────────────────────

        private void SyncColliderPositions()
        {
            if (_panelRoot == null) return;
            for (int i = 0; i < _buttons.Count; i++)
            {
                var b = _buttons[i];
                if (b.colliderGO == null || b.bgImage == null) continue;

                // Cull search items outside the visible scroll window
                if (b.action == ButtonAction.SelectSearchItem)
                {
                    bool inWindow = b.contentY + RowH > _scrollOffset
                                 && b.contentY        < _scrollOffset + ViewportH;
                    bool show = inWindow && b.matchFilter;
                    b.colliderGO.SetActive(show);
                    _buttons[i] = b;
                    if (!show) continue;
                }

                var rt = b.bgImage.rectTransform;
                if (rt == null) continue;
                Vector3 wc = rt.TransformPoint(rt.rect.center);
                b.colliderGO.transform.position = wc + _panelRoot.transform.forward * -0.005f;
                b.colliderGO.transform.rotation = _panelRoot.transform.rotation;
            }
        }

        // ── Hover helpers ─────────────────────────────────────────────────────────

        private void SetHoverColor(int index, Color col)
        {
            if (index >= 0 && index < _buttons.Count && _buttons[index].bgImage != null)
                _buttons[index].bgImage.color = col;
        }

        private void ClearHover()
        {
            if (_hoveredIndex >= 0 && _hoveredIndex < _buttons.Count)
            {
                var b = _buttons[_hoveredIndex];
                if (b.bgImage != null)
                    b.bgImage.color = DefaultBgColor(b.action);
            }
            _hoveredIndex = -1;
        }

        private Color DefaultBgColor(ButtonAction a) => a switch
        {
            ButtonAction.Close             => ColCloseBg,
            ButtonAction.SetObserveMode    => _observeBtnBg != null ? _observeBtnBg.color : ColIdleModeBtn,
            ButtonAction.SetInspectMode    => _inspectBtnBg != null ? _inspectBtnBg.color : ColIdleModeBtn,
            ButtonAction.SelectSearchItem  => ColItemBg,
            _                              => ColIdleModeBtn,
        };

        // ── Controller input ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns a ray from the right controller's position along its forward axis.
        /// Searches scene hierarchy once, then caches the Transform.
        /// Falls back to camera gaze if no controller is found.
        /// </summary>
        private bool TryGetControllerRay(out Ray ray)
        {
            ray = default;
            if (_rightControllerTransform == null)
                _rightControllerTransform = FindRightControllerTransform();
            if (_rightControllerTransform == null) return false;
            ray = new Ray(_rightControllerTransform.position, _rightControllerTransform.forward);
            return true;
        }

        /// <summary>Finds the right hand controller Transform by searching for known XRI3 names.</summary>
        private static Transform FindRightControllerTransform()
        {
            // XR Interaction Toolkit 3.x names the right controller object one of these
            string[] candidates = {
                "Right Controller", "RightHand Controller", "Right Hand Controller",
                "Right Controller Stabilized", "Right Interaction Visual"
            };
            foreach (var name in candidates)
            {
                var go = GameObject.Find(name);
                if (go != null) return go.transform;
            }
            // Fallback: find any Transform whose name contains "right" and "controller"
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var n = t.name.ToLower();
                if (n.Contains("right") && n.Contains("controller") && !n.Contains("teleport"))
                    return t;
            }
            return null;
        }

        private bool GetRightTrigger()
        {
            return _triggerAction != null && _triggerAction.IsPressed();
        }

        // ── Ray visual ────────────────────────────────────────────────────────────

        private void BuildRayLine()
        {
            var go = new GameObject("CPanelRayLine");
            _rayLine = go.AddComponent<LineRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh); mat.color = new Color(0.35f,0.80f,1f,0.85f);
            _rayLine.material=mat; _rayLine.startWidth=0.004f; _rayLine.endWidth=0.001f;
            _rayLine.positionCount=2; _rayLine.useWorldSpace=true; _rayLine.enabled=false;
        }

        // ── Button registration ───────────────────────────────────────────────────

        private void AddBtn(ButtonAction action, CelestialBody target,
            RectTransform cellRT, Image bgImage, float contentY, float w, float h)
        {
            var colGO = new GameObject($"CPBtn_{_buttons.Count}");
            colGO.transform.SetParent(_panelRoot.transform, false);
            var bc = colGO.AddComponent<BoxCollider>();
            bc.size = new Vector3(Mathf.Max(w, 0.01f), Mathf.Max(h, 0.008f), 0.005f);
            var m = colGO.AddComponent<CPBtnMarker>(); m.idx = _buttons.Count;

            _buttons.Add(new ButtonEntry
            {
                action       = action,
                searchTarget = target,
                colliderGO   = colGO,
                bgImage      = bgImage,
                contentY     = Mathf.Max(contentY, 0f),
                matchFilter  = true,
            });
        }

        private int FindBtnIndex(GameObject go)
        {
            var m = go.GetComponent<CPBtnMarker>();
            return m != null ? m.idx : -1;
        }

        // ── Toggle row helper ─────────────────────────────────────────────────────

        private Image BuildToggleRow(Transform parent, string id, string label, ButtonAction action)
        {
            Image dot = null;
            AddRow(parent, $"Toggle_{id}", 32f, row =>
            {
                var rowBg = row.gameObject.AddComponent<Image>(); rowBg.color = ColItemBg;

                var dotRT = UIChild(row,"Dot");
                dotRT.anchorMin=new Vector2(0,0.5f); dotRT.anchorMax=new Vector2(0,0.5f);
                dotRT.sizeDelta=new Vector2(14,14); dotRT.anchoredPosition=new Vector2(14,0);
                dot = dotRT.gameObject.AddComponent<Image>(); dot.color = ColToggleOn;

                var lRT = UIChild(row,"Lbl");
                lRT.anchorMin=Vector2.zero; lRT.anchorMax=Vector2.one;
                lRT.offsetMin=new Vector2(34,0); lRT.offsetMax=new Vector2(-4,0);
                MkTMPUGUI(lRT.gameObject, label, 13f, ColText, FontStyles.Normal,
                    TextAlignmentOptions.Left, stretch:false);

                float tw = ContentW*CanvasScale, th = 28f*CanvasScale;
                AddBtn(action, null, (RectTransform)row, rowBg, -1f, tw, th);
            });
            return dot;
        }

        // ── UI factory helpers ────────────────────────────────────────────────────

        private static RectTransform UIChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform MakeStretchRaw(Transform parent, string name)
        {
            var rt = UIChild(parent, name);
            return rt;
        }

        private static void AddRow(Transform parent, string name, float height,
            System.Action<Transform> build)
        {
            var rt = UIChild(parent, name);
            rt.sizeDelta = new Vector2(0, height);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight=height; le.preferredHeight=height;
            build(rt);
        }

        private static void AddDiv(Transform parent)
        {
            AddRow(parent, "Div", 2f, row => MakeStretchImage(row, "D", ColBorder));
        }

        private static void SetStretch(RectTransform rt, float px, float py)
        {
            rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
            rt.offsetMin=new Vector2(px,py); rt.offsetMax=new Vector2(-px,-py);
        }

        private static void MakeStretchImage(Transform parent, string name, Color col)
        {
            var rt = UIChild(parent, name);
            rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
            rt.offsetMin=rt.offsetMax=Vector2.zero;
            rt.gameObject.AddComponent<Image>().color = col;
        }

        private static void MkLabel(GameObject go, string text, float fs, Color col)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
            rt.offsetMin=new Vector2(0,2); rt.offsetMax=new Vector2(0,-2);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text=text; t.fontSize=fs; t.color=col;
            t.fontStyle=FontStyles.Bold; t.alignment=TextAlignmentOptions.Left;
        }

        private static void MkTMPUGUI(GameObject go, string text, float fs, Color col,
            FontStyles style, TextAlignmentOptions align, bool stretch = false)
        {
            if (stretch)
            {
                var rt = (RectTransform)go.transform;
                rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
                rt.offsetMin=rt.offsetMax=Vector2.zero;
            }
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text=text; t.fontSize=fs; t.color=col; t.fontStyle=style; t.alignment=align;
            t.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
        }

        private static string ShortType(CelestialBodyType t) => t switch
        {
            CelestialBodyType.Planet        => "planet",
            CelestialBodyType.Moon          => "moon",
            CelestialBodyType.DeepSkyObject => "dso",
            CelestialBodyType.Star          => "star",
            CelestialBodyType.Constellation => "const.",
            _                               => "",
        };
    }

    // ── Tiny marker component on each 3D BoxCollider ─────────────────────────────

    internal class CPBtnMarker : MonoBehaviour
    {
        public int idx;
    }
}
