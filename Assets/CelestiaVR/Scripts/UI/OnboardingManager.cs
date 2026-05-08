using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Head-locked sequential onboarding panels. Canvas is parented to the camera so
    /// it follows the player's view at all times. Press B (right controller secondary
    /// button) to advance through the five control-hint panels.
    /// </summary>
    public class OnboardingManager : MonoBehaviour
    {
        // ── Step data ─────────────────────────────────────────────────────────────

        private static readonly StepData[] Steps =
        {
            new StepData(
                new Color(0.22f, 0.56f, 1.00f),
                "L", "Left Joystick  —  Move",
                "Use the LEFT joystick\nto walk around the island."
            ),
            new StepData(
                new Color(1.00f, 0.76f, 0.18f),
                "R", "Right Joystick  —  Time",
                "Tilt the RIGHT joystick\nto cycle between Day and Night sky."
            ),
            new StepData(
                new Color(0.62f, 0.28f, 0.92f),
                "Y/X", "Y or X Button  —  Filters",
                "Press Y or X on your controller\nto open the star type filter menu."
            ),
            new StepData(
                new Color(1.00f, 0.28f, 0.58f),
                "★", "Star Highlight",
                "Look at any star in the sky.\nIt glows PINK when you\nare gazing directly at it."
            ),
            new StepData(
                new Color(0.18f, 0.82f, 0.48f),
                "A", "A Button  —  Select",
                "Press A while looking at\na glowing pink star\nto select and inspect it."
            ),
        };

        // ── Layout constants (canvas units; 1 unit = 1 mm at scale 0.001) ─────────

        private const float PanelW  = 660f;
        private const float PanelH  = 380f;
        private const float HeaderH = 104f;

        private static readonly Color BgColor  = new(0.04f, 0.08f, 0.16f, 0.96f);
        private static readonly Color BodyColor = new(0.84f, 0.93f, 1.00f, 1.00f);
        private static readonly Color HintColor = new(1.00f, 1.00f, 1.00f, 0.50f);

        // ── Runtime ───────────────────────────────────────────────────────────────

        private Canvas      _canvas;
        private GameObject  _panel;
        private int         _step;
        private Camera      _cam;
        private InputAction _bAction;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // B button on right Quest controller = secondaryButton on RightHand XRController
            _bAction = new InputAction(type: InputActionType.Button);
            _bAction.AddBinding("<XRController>{RightHand}/secondaryButton");
            _bAction.AddBinding("<Keyboard>/b"); // editor fallback
            _bAction.performed += _ => Advance();
            _bAction.Enable();
        }

        private void OnDisable()
        {
            if (_bAction == null) return;
            _bAction.performed -= _ => Advance();
            _bAction.Disable();
            _bAction.Dispose();
            _bAction = null;
        }

        private void Start()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                foreach (var n in new[] {
                    "XR Origin Hands (XR Rig)", "XR Origin (XR Rig)", "XR Origin" })
                {
                    var o = GameObject.Find(n);
                    if (o != null) { _cam = o.GetComponentInChildren<Camera>(); break; }
                }
            }

            EnsureEventSystem();
            SpawnCanvas();
            ShowStep(0);
        }

        // ── Canvas & panel builders ───────────────────────────────────────────────

        private void SpawnCanvas()
        {
            var go = new GameObject("[OnboardingCanvas]");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            go.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 500f);
            go.AddComponent<GraphicRaycaster>();

            // Parent to camera so the panel follows the player's head (head-locked HUD)
            if (_cam != null)
            {
                go.transform.SetParent(_cam.transform, false);
                go.transform.SetLocalPositionAndRotation(new Vector3(0f, -0.05f, 1.5f), Quaternion.identity);
            }

            go.transform.localScale = Vector3.one * 0.00104f;
        }

        private void ShowStep(int idx)
        {
            if (_panel != null) Destroy(_panel);

            if (idx >= Steps.Length)
            {
                Destroy(_canvas.gameObject);
                Destroy(gameObject);
                return;
            }

            _step  = idx;
            _panel = BuildPanel(Steps[idx]);
        }

        private void Advance() => ShowStep(_step + 1);

        private GameObject BuildPanel(StepData s)
        {
            float hw = PanelW / 2f;
            float hh = PanelH / 2f;

            // ── Accent border ──────────────────────────────────────────────────────
            var border = MkRect("Border", _canvas.transform, PanelW + 8, PanelH + 8, 0, 0);
            border.AddComponent<Image>().color = new Color(s.Accent.r, s.Accent.g, s.Accent.b, 0.65f);

            // ── Dark panel background ──────────────────────────────────────────────
            var root = MkRect("Panel", _canvas.transform, PanelW, PanelH, 0, 0);
            root.AddComponent<Image>().color = BgColor;

            // ── Coloured header strip ──────────────────────────────────────────────
            float hdrCY = hh - HeaderH / 2f;
            var header  = MkRect("Header", root.transform, PanelW, HeaderH, 0, hdrCY);
            header.AddComponent<Image>().color =
                new Color(s.Accent.r * 0.20f, s.Accent.g * 0.20f, s.Accent.b * 0.28f, 1f);

            // Icon badge
            var badge = MkRect("Badge", header.transform, 80, 80, -hw + 52, 0);
            badge.AddComponent<Image>().color = new Color(s.Accent.r, s.Accent.g, s.Accent.b, 0.35f);
            MkTmp(MkRect("BadgeTxt", badge.transform, 80, 80, 0, 0),
                  s.Icon, 34, Color.white, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

            // Title
            MkTmp(MkRect("Title", header.transform, PanelW - 130, HeaderH, 20, 0),
                  s.Title, 32, Color.white, TextAlignmentOptions.MidlineLeft).fontStyle = FontStyles.Bold;

            // Step counter — top-right of header
            MkTmp(MkRect("StepLbl", header.transform, 72, 28, hw - 42, HeaderH / 2f - 18),
                  $"{_step + 1} / {Steps.Length}", 20,
                  new Color(1, 1, 1, 0.50f), TextAlignmentOptions.Center);

            // ── Body text ──────────────────────────────────────────────────────────
            float bodyTop = hh - HeaderH;
            float bodyBot = -hh + 52f;
            float bodyCY  = (bodyTop + bodyBot) * 0.5f;
            float bodyH   = bodyTop - bodyBot;

            var bodyTmp = MkTmp(
                MkRect("Body", root.transform, PanelW - 60, bodyH, 0, bodyCY),
                s.Body, 30, BodyColor, TextAlignmentOptions.Center);
            bodyTmp.lineSpacing = 12;

            // ── Progress dots ──────────────────────────────────────────────────────
            for (int d = 0; d < Steps.Length; d++)
            {
                float dotX = (d - (Steps.Length - 1) * 0.5f) * 24f;
                var dot = MkRect($"Dot{d}", root.transform, 13, 13, dotX, -hh + 36f);
                dot.AddComponent<Image>().color = d == _step
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.28f);
            }

            // ── B-button hint ──────────────────────────────────────────────────────
            bool isLast = _step == Steps.Length - 1;
            MkTmp(MkRect("BHint", root.transform, PanelW - 40, 26, 0, -hh + 16f),
                  isLast ? "[ B ]  Finish" : "[ B ]  Next",
                  19, HintColor, TextAlignmentOptions.Center);

            return root;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static GameObject MkRect(string name, Transform parent, float w, float h, float x, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt              = go.AddComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            rt.localScale       = Vector3.one;
            return go;
        }

        private static TextMeshProUGUI MkTmp(GameObject go, string text, float size,
            Color color, TextAlignmentOptions alignment)
        {
            var t                = go.AddComponent<TextMeshProUGUI>();
            t.text               = text;
            t.fontSize           = size;
            t.color              = color;
            t.alignment          = alignment;
            t.enableWordWrapping = true;
            t.overflowMode       = TextOverflowModes.Overflow;
            return t;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("[EventSystem]");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // ── Step data struct ──────────────────────────────────────────────────────

        private readonly struct StepData
        {
            public readonly Color  Accent;
            public readonly string Icon;
            public readonly string Title;
            public readonly string Body;

            public StepData(Color accent, string icon, string title, string body)
            {
                Accent = accent; Icon = icon; Title = title; Body = body;
            }
        }
    }
}
