using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CelestiaVR.Core;
using CelestiaVR.Interaction;

namespace CelestiaVR.UI
{
    /// <summary>
    /// World-space browse-and-search panel for all celestial objects in the scene.
    ///
    /// • A floating "Search Sky" button always sits at the lower-right of the player's
    ///   field of view (repositioned every LateUpdate, like a persistent HUD element).
    ///   Gaze + dwell 2.5 s to open / close the panel.
    ///
    /// • On open: shows ALL CelestialBody objects grouped by type
    ///   (Planets → Deep Sky → Named Stars → Constellations) — fully pre-populated,
    ///   no typing needed.
    ///
    /// • Optional text filter: a TMP_InputField narrows the list.
    ///
    /// • Each item row has an invisible 3-D BoxCollider (SearchItemCollider) in front
    ///   of the canvas for gaze-dwell selection.
    ///
    /// • Selecting an item activates DirectionalArrow and closes the panel.
    ///
    /// • Auto-created by StargazingSceneBootstrap — no manual scene setup required.
    ///
    /// NOTE: Uses "new GameObject(name, typeof(RectTransform))" throughout to avoid
    ///       the Unity 6 bug where AddComponent&lt;RectTransform&gt;() returns null when
    ///       a RectTransform already exists after auto-conversion from SetParent.
    /// </summary>
    public class CelestialSearchPanel : MonoBehaviour
    {
        public static CelestialSearchPanel Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Panel placement")]
        public float panelDistance    = 2.5f;
        public float panelOffsetRight = 0f;
        public float panelOffsetUp    = 0.0f;

        [Header("Trigger button (always-visible HUD)")]
        public float triggerForward   = 1.6f;
        public float triggerRight     = 0.38f;   // ~13° right of centre at 1.6 m
        public float triggerDown      = 0.25f;   // ~9°  below centre at 1.6 m

        [Header("Item limits (0 = unlimited)")]
        public int maxNamedStars      = 25;  // shows brightest first
        public int maxConstellations  = 88;

        // ── Runtime ──────────────────────────────────────────────────────────────

        private Camera  _cam;
        private bool    _isOpen;

        private GameObject     _triggerGO;
        private GameObject     _panelRoot;
        private Canvas         _canvas;
        private TMP_InputField _searchInput;

        private struct ItemEntry
        {
            public CelestialBody body;
            public RectTransform cellRT;
            public GameObject    colliderGO;
        }
        private readonly List<ItemEntry> _items = new();

        // Category definitions: type, header label, accent colour
        private static readonly (CelestialBodyType type, string label, Color color)[] Categories =
        {
            (CelestialBodyType.Planet,        "PLANETS",        new Color(1.00f, 0.85f, 0.35f, 1f)),
            (CelestialBodyType.DeepSkyObject,  "DEEP SKY",       new Color(0.40f, 0.80f, 1.00f, 1f)),
            (CelestialBodyType.Star,           "NAMED STARS",    new Color(1.00f, 1.00f, 0.65f, 1f)),
            (CelestialBodyType.Constellation,  "CONSTELLATIONS", new Color(0.55f, 0.75f, 1.00f, 1f)),
        };

        // Layout constants (canvas pixels)
        private const float CanvasW   = 500f;
        private const float CanvasScale = 0.001f;   // 1 px = 1 mm  →  500 px = 0.5 m wide
        private const float RowH      = 32f;
        private const float CatH      = 28f;
        private const float PadX      = 12f;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _cam = Camera.main;
            StartCoroutine(DelayedBuild());
        }

        [Header("Auto-test on load (disable in production)")]
        [Tooltip("Open the search panel automatically when the scene starts.")]
        public bool autoOpenOnStart = true;
        [Tooltip("Automatically point the directional arrow at the first available planet.")]
        public bool autoTargetOnStart = true;

        // Wait for all spawners to finish, then build UI
        private IEnumerator DelayedBuild()
        {
            yield return new WaitForSeconds(3.5f);
            BuildTriggerButton();
            BuildPanel();

            // ── Auto-test: open panel + point arrow at first planet ───────────────
            if (autoOpenOnStart)
                Open();

            if (autoTargetOnStart)
                AutoTargetFirstBody();
        }

        /// <summary>
        /// Finds the first available CelestialBody in priority order
        /// (Moon → Planet → Star → DeepSky → Constellation) and activates
        /// the DirectionalArrow toward it so you can verify it works immediately.
        /// </summary>
        private void AutoTargetFirstBody()
        {
            var bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
            if (bodies.Length == 0) return;

            // Priority: Moon first (great test target), then planets, then anything else
            CelestialBodyType[] priority = {
                CelestialBodyType.Moon,
                CelestialBodyType.Planet,
                CelestialBodyType.Star,
                CelestialBodyType.DeepSkyObject,
                CelestialBodyType.Constellation,
            };

            foreach (var type in priority)
            {
                foreach (var b in bodies)
                {
                    if (b.bodyType != type) continue;
                    if (string.IsNullOrEmpty(b.objectName)) continue;

                    var arrow = DirectionalArrow.Instance;
                    if (arrow != null)
                        arrow.SetTarget(b);
                    return;
                }
            }
        }

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // ── Trigger button always follows camera (HUD-style) ─────────────────
            if (_triggerGO != null)
            {
                _triggerGO.transform.position =
                    _cam.transform.position
                    + _cam.transform.forward * triggerForward
                    + _cam.transform.right   * triggerRight
                    + _cam.transform.up      * -triggerDown;

                // Billboard toward camera
                _triggerGO.transform.rotation = Quaternion.LookRotation(
                    _triggerGO.transform.position - _cam.transform.position);
            }

            // ── Sync 3-D colliders to canvas rows (only while open) ───────────────
            if (_isOpen && _panelRoot != null)
                SyncColliderPositions();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public bool IsOpen => _isOpen;

        public void ToggleOpen()
        {
            if (_isOpen) Close(); else Open();
        }

        public void Open()
        {
            if (_panelRoot == null) return;
            _isOpen = true;

            // Snap panel to "directly in front of player" and freeze it there
            _panelRoot.transform.position =
                _cam.transform.position
                + _cam.transform.forward  * panelDistance
                + _cam.transform.right    * panelOffsetRight
                + _cam.transform.up       * panelOffsetUp;
            _panelRoot.transform.rotation = Quaternion.LookRotation(
                _panelRoot.transform.position - _cam.transform.position);

            _panelRoot.SetActive(true);   // must be active before ForceUpdateCanvases

            if (_searchInput != null) _searchInput.text = "";
            EnableAllItems();

            // Force canvas layout to recalculate so RectTransform.rect is valid
            Canvas.ForceUpdateCanvases();
            SyncColliderPositions();
        }

        public void Close()
        {
            _isOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        // ── Trigger button ────────────────────────────────────────────────────────

        private void BuildTriggerButton()
        {
            _triggerGO = new GameObject("[SearchTrigger]");
            _triggerGO.transform.SetParent(null);

            // Glow sphere — 8 cm diameter at 1.6 m forward ≈ visible blue dot in VR
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(_triggerGO.transform, false);
            sphere.transform.localScale = Vector3.one * 0.08f;
            Destroy(sphere.GetComponent<Collider>());
            // Opaque unlit material — transparent blend can fail to write depth in some URP configs
            var smat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Unlit/Color"));
            smat.color = new Color(0.3f, 0.6f, 1f, 1f);
            smat.renderQueue = 3002;
            sphere.GetComponent<Renderer>().material = smat;
            sphere.GetComponent<Renderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            // Text label to the right of sphere
            // Scale: TMP fontSize=5 at scale 1 ≈ 0.5 world units tall.
            // Target: ~0.075 world units (≈ 2.7° at 1.6 m) → scale = 0.075 / 0.5 = 0.15
            var labelGO = new GameObject("TriggerLabel");
            labelGO.transform.SetParent(_triggerGO.transform, false);
            labelGO.transform.localPosition = new Vector3(0.11f, 0, 0);
            labelGO.transform.localScale    = Vector3.one * 0.15f;
            var tmp = labelGO.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text             = "Search Sky";
            tmp.fontSize         = 5f;
            tmp.color            = new Color(0.3f, 0.85f, 1f, 1f);
            tmp.alignment        = TextAlignmentOptions.Left;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.outlineWidth     = 0.2f;
            tmp.outlineColor     = new Color(0, 0, 0, 0.8f);

            // Sphere collider for SearchPanelDwellDetector (generous radius for easy gaze hit)
            var col = _triggerGO.AddComponent<SphereCollider>();
            col.radius = 0.15f;

            // Marker for SearchPanelDwellDetector to identify this as the open trigger
            var trig  = _triggerGO.AddComponent<SearchPanelTrigger>();
            trig.panel = this;
        }

        // ── Panel ─────────────────────────────────────────────────────────────────

        private void BuildPanel()
        {
            // ── Collect + sort bodies ────────────────────────────────────────────
            var allBodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
            var byType    = new Dictionary<CelestialBodyType, List<CelestialBody>>();
            foreach (var cat in Categories)
                byType[cat.type] = new List<CelestialBody>();

            foreach (var b in allBodies)
            {
                if (!byType.ContainsKey(b.bodyType)) continue;
                if (string.IsNullOrEmpty(b.objectName)) continue;
                byType[b.bodyType].Add(b);
            }

            // Sort: stars by magnitude (brightest first), rest alphabetically
            byType[CelestialBodyType.Planet]
                .Sort((a,b) => string.Compare(a.objectName, b.objectName));
            byType[CelestialBodyType.DeepSkyObject]
                .Sort((a,b) => string.Compare(a.objectName, b.objectName));
            byType[CelestialBodyType.Star]
                .Sort((a,b) => a.magnitude.CompareTo(b.magnitude));
            byType[CelestialBodyType.Constellation]
                .Sort((a,b) => string.Compare(a.objectName, b.objectName));

            ApplyLimit(byType[CelestialBodyType.Star],          maxNamedStars);
            ApplyLimit(byType[CelestialBodyType.Constellation],  maxConstellations);

            // ── Compute required canvas height ───────────────────────────────────
            float listH = 0f;
            foreach (var cat in Categories)
            {
                var list = byType[cat.type];
                if (list.Count == 0) continue;
                listH += CatH + 4f;                              // category header + gap
                listH += Mathf.CeilToInt(list.Count / 2f) * RowH; // rows (2-column)
                listH += 10f;                                    // section gap
            }
            float canvasH = 36f    // title bar
                          + 38f    // search field
                          + 4f     // divider + gaps
                          + listH
                          + 14f;   // bottom padding

            // ── Root + Canvas ────────────────────────────────────────────────────
            _panelRoot = new GameObject("[SearchPanel]");
            _panelRoot.transform.SetParent(null);

            // canvasGO — will auto-receive RectTransform when Canvas is added
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(_panelRoot.transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.WorldSpace;
            _canvas.worldCamera = _cam;
            canvasGO.AddComponent<CanvasGroup>();
            canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;

            var canvasRT = (RectTransform)canvasGO.transform; // Canvas auto-adds RT
            canvasRT.sizeDelta  = new Vector2(CanvasW, canvasH);
            canvasRT.localScale = Vector3.one * CanvasScale;

            // Dark background + border (Image auto-adds RT)
            UIChild(canvasGO.transform, "BG")
                .With(rt => { StretchFull(rt); rt.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.16f, 0.93f); });
            UIChild(canvasGO.transform, "Border")
                .With(rt => {
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = new Vector2(-2,-2); rt.offsetMax = new Vector2(2,2);
                    rt.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.5f, 1f, 0.5f);
                });

            // ── Vertical layout container ────────────────────────────────────────
            var layoutRT = UIChild(canvasGO.transform, "Layout");
            layoutRT.anchorMin        = new Vector2(0, 1);
            layoutRT.anchorMax        = new Vector2(1, 1);
            layoutRT.pivot            = new Vector2(0.5f, 1f);
            layoutRT.offsetMin        = new Vector2(PadX, 0);
            layoutRT.offsetMax        = new Vector2(-PadX, 0);
            layoutRT.anchoredPosition = new Vector2(0, -8f);

            var vlg = layoutRT.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing             = 0;
            vlg.childControlWidth   = true;
            vlg.childControlHeight  = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            Transform L = layoutRT;   // convenience alias

            // ── Title row ────────────────────────────────────────────────────────
            AddRow(L, "TitleRow", 36f, row =>
            {
                var t = UIChild(row, "Title");
                SetStretchPad(t, 0, 0);
                var tmp = t.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.text = "Find in Sky"; tmp.fontSize = 22f;
                tmp.color = new Color(0.7f, 0.9f, 1f, 1f);
                tmp.alignment = TextAlignmentOptions.Left;

                var h = UIChild(row, "CloseHint");
                SetStretchPad(h, 0, 0);
                var htmp = h.gameObject.AddComponent<TextMeshProUGUI>();
                htmp.text = "Look away to close"; htmp.fontSize = 12f;
                htmp.color = new Color(0.45f, 0.55f, 0.65f, 0.75f);
                htmp.alignment = TextAlignmentOptions.Right;
            });

            // ── Search field ─────────────────────────────────────────────────────
            AddRow(L, "SearchRow", 38f, row =>
            {
                var bgRT = UIChild(row, "SearchBG");
                StretchFull(bgRT);
                bgRT.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.14f, 0.26f, 0.9f);

                // Placeholder
                var ph = UIChild(row, "Placeholder");
                SetStretchPad(ph, 10f, 4f);
                var phTmp = ph.gameObject.AddComponent<TextMeshProUGUI>();
                phTmp.text      = "Type to filter... (or just look at items)";
                phTmp.fontSize  = 14f;
                phTmp.fontStyle = FontStyles.Italic;
                phTmp.color     = new Color(0.35f, 0.45f, 0.6f, 0.75f);
                phTmp.alignment = TextAlignmentOptions.Left;
                phTmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

                // Input text
                var it = UIChild(row, "InputText");
                SetStretchPad(it, 10f, 4f);
                var itTmp = it.gameObject.AddComponent<TextMeshProUGUI>();
                itTmp.fontSize  = 14f;
                itTmp.color     = new Color(0.9f, 0.95f, 1f, 1f);
                itTmp.alignment = TextAlignmentOptions.Left;
                itTmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

                _searchInput               = row.gameObject.AddComponent<TMP_InputField>();
                _searchInput.textComponent = itTmp;
                _searchInput.placeholder   = phTmp;
                _searchInput.characterLimit= 40;
                _searchInput.lineType      = TMP_InputField.LineType.SingleLine;
                _searchInput.onValueChanged.AddListener(OnSearchChanged);
            });

            // Thin divider
            AddRow(L, "DivRow", 4f, row =>
            {
                var d = UIChild(row, "Div");
                StretchFull(d);
                d.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.5f, 1f, 0.35f);
            });

            // ── Item list ────────────────────────────────────────────────────────
            foreach (var cat in Categories)
            {
                var list = byType[cat.type];
                if (list.Count == 0) continue;

                // Category header
                AddRow(L, $"Cat_{cat.type}", CatH, row =>
                {
                    var ct = UIChild(row, "CatLabel");
                    SetStretchPad(ct, 4f, 2f);
                    var tmp = ct.gameObject.AddComponent<TextMeshProUGUI>();
                    tmp.text      = $"> {cat.label} ({list.Count})";
                    tmp.fontSize  = 13f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color     = cat.color;
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                });

                // Items two per row
                for (int i = 0; i < list.Count; i += 2)
                {
                    var left  = list[i];
                    var right = (i + 1 < list.Count) ? list[i + 1] : null;
                    int capturedI = i;
                    var capturedLeft  = left;
                    var capturedRight = right;

                    AddRow(L, $"ItemRow_{i}", RowH, row =>
                    {
                        BuildItemCell(row, capturedLeft,  cat.color, isLeft: true);
                        if (capturedRight != null)
                            BuildItemCell(row, capturedRight, cat.color, isLeft: false);
                    });
                }

                // Gap after section
                AddRow(L, $"Gap_{cat.type}", 8f, _ => { });
            }

            _panelRoot.SetActive(false);
        }

        // ── Build one item cell (left or right column) ────────────────────────────

        private void BuildItemCell(Transform rowParent, CelestialBody body,
            Color accentColor, bool isLeft)
        {
            var cellRT = UIChild(rowParent, $"Cell_{body.objectName}");
            // Left half: 0 → 0.5,  Right half: 0.5 → 1
            cellRT.anchorMin        = isLeft ? new Vector2(0, 0) : new Vector2(0.5f, 0);
            cellRT.anchorMax        = isLeft ? new Vector2(0.5f, 1) : new Vector2(1, 1);
            cellRT.offsetMin        = new Vector2(isLeft ? 0 : 2, 2);
            cellRT.offsetMax        = new Vector2(isLeft ? -2 : 0, -2);

            // Cell background
            var bg = cellRT.gameObject.AddComponent<Image>();
            bg.color = new Color(0.09f, 0.16f, 0.30f, 0.65f);

            // Type-colour dot on the left
            var dotRT = UIChild(cellRT, "Dot");
            dotRT.anchorMin        = new Vector2(0, 0.5f);
            dotRT.anchorMax        = new Vector2(0, 0.5f);
            dotRT.sizeDelta        = new Vector2(6, 6);
            dotRT.anchoredPosition = new Vector2(8, 0);
            dotRT.gameObject.AddComponent<Image>().color = accentColor;

            // Name text
            var nameRT = UIChild(cellRT, "Name");
            nameRT.anchorMin = Vector2.zero; nameRT.anchorMax = Vector2.one;
            nameRT.offsetMin = new Vector2(18f, 0); nameRT.offsetMax = new Vector2(-4f, 0);
            var nameTMP = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
            nameTMP.text               = body.objectName;
            nameTMP.fontSize           = 13f;
            nameTMP.color              = new Color(0.88f, 0.93f, 1f, 1f);
            nameTMP.alignment          = TextAlignmentOptions.Left;
            nameTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            nameTMP.overflowMode       = TextOverflowModes.Ellipsis;

            // Invisible 3-D BoxCollider (positioned by SyncColliderPositions each frame)
            var colGO = new GameObject($"Collider_{body.objectName}");
            colGO.transform.SetParent(_panelRoot.transform, false);
            var bc    = colGO.AddComponent<BoxCollider>();
            // Approximate physical size of one cell at canvas scale
            float cellW = (CanvasW / 2f - 6f) * CanvasScale;
            float cellH = (RowH - 4f) * CanvasScale;
            bc.size = new Vector3(cellW, cellH, 0.005f);

            var marker   = colGO.AddComponent<SearchItemCollider>();
            marker.target   = body;
            marker.isActive = true;

            _items.Add(new ItemEntry { body = body, cellRT = cellRT, colliderGO = colGO });
        }

        // ── Keep 3-D colliders aligned with canvas cells ──────────────────────────

        private void SyncColliderPositions()
        {
            foreach (var e in _items)
            {
                if (e.cellRT == null || e.colliderGO == null) continue;
                Vector3 worldCenter = e.cellRT.TransformPoint(e.cellRT.rect.center);
                // Place collider slightly in front of canvas (toward viewer)
                e.colliderGO.transform.position = worldCenter
                    + _panelRoot.transform.forward * -0.004f;
                e.colliderGO.transform.rotation = _panelRoot.transform.rotation;
            }
        }

        // ── Search filter ─────────────────────────────────────────────────────────

        private void OnSearchChanged(string query)
        {
            query = query.ToLowerInvariant().Trim();
            for (int i = 0; i < _items.Count; i++)
            {
                var e    = _items[i];
                bool show = string.IsNullOrEmpty(query)
                          || e.body.objectName.ToLowerInvariant().Contains(query)
                          || e.body.bodyType.ToString().ToLowerInvariant().Contains(query);

                if (e.cellRT    != null) e.cellRT.gameObject.SetActive(show);
                if (e.colliderGO != null)
                {
                    e.colliderGO.SetActive(show);
                    var sm = e.colliderGO.GetComponent<SearchItemCollider>();
                    if (sm != null) sm.isActive = show;
                }
            }
            Canvas.ForceUpdateCanvases();
            SyncColliderPositions();
        }

        private void EnableAllItems()
        {
            foreach (var e in _items)
            {
                if (e.cellRT    != null) e.cellRT.gameObject.SetActive(true);
                if (e.colliderGO != null)
                {
                    e.colliderGO.SetActive(true);
                    var sm = e.colliderGO.GetComponent<SearchItemCollider>();
                    if (sm != null) sm.isActive = true;
                }
            }
        }

        // ── UI factory helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Creates a child RectTransform safely, regardless of parent type.
        /// Uses typeof(RectTransform) at construction so no AddComponent is needed.
        /// </summary>
        private static RectTransform UIChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Adds a fixed-height row with a LayoutElement to the vertical layout.</summary>
        private static void AddRow(Transform parent, string name, float height,
            System.Action<Transform> buildContent)
        {
            var rt = UIChild(parent, name);
            rt.sizeDelta = new Vector2(0, height);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height;
            buildContent(rt);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetStretchPad(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        private static void ApplyLimit(List<CelestialBody> list, int max)
        {
            if (max > 0 && list.Count > max)
                list.RemoveRange(max, list.Count - max);
        }
    }

    // ── Small extension to reduce lambda verbosity ────────────────────────────────

    internal static class RectTransformExtensions
    {
        internal static RectTransform With(this RectTransform rt,
            System.Action<RectTransform> configure)
        {
            configure(rt); return rt;
        }
    }
}
