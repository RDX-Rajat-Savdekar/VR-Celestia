using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.UI;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Independent dwell detector that handles gaze interaction for the
    /// CelestialSearchPanel UI — separate from DwellSelector so the two
    /// pipelines don't interfere with each other.
    ///
    /// Attach this to any persistent GameObject (auto-added by StargazingSceneBootstrap).
    ///
    /// Detection targets:
    ///   • SearchPanelTrigger  — floating search button  → open / close panel
    ///   • SearchItemCollider  — item rows inside panel  → select item → show arrow
    /// </summary>
    public class SearchPanelDwellDetector : MonoBehaviour
    {
        [Header("Dwell settings")]
        [Tooltip("Seconds of continuous gaze needed to activate a trigger / select an item.")]
        public float triggerDwellTime = 2.5f;
        public float itemDwellTime    = 2.0f;

        [Header("Ray cast")]
        [Tooltip("Half-angle of the gaze cone (sphere-cast radius at 1 m).")]
        public float castRadius = 0.12f;
        public float castRange  = 800f;
        [Tooltip("Layer mask — leave as Everything unless you want to restrict.")]
        public LayerMask layerMask = ~0;

        // ── Runtime ──────────────────────────────────────────────────────────────

        private Camera _cam;

        // Current gaze target
        private SearchPanelTrigger  _hoveredTrigger;
        private SearchItemCollider  _hoveredItem;
        private float               _dwellTimer;

        // Visual feedback: the hovered item flashes
        private Renderer _hoveredRenderer;
        private Color    _hoveredBaseColor;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Start()
        {
            _cam = Camera.main;
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // ── Sphere-cast from camera forward ──────────────────────────────────
            Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

            SearchPanelTrigger  hitTrigger = null;
            SearchItemCollider  hitItem    = null;

            if (Physics.SphereCast(ray, castRadius, out RaycastHit hit, castRange, layerMask))
            {
                hitTrigger = hit.collider.GetComponent<SearchPanelTrigger>();
                if (hitTrigger == null)
                    hitItem = hit.collider.GetComponent<SearchItemCollider>();

                // Ignore inactive (filtered-out) items
                if (hitItem != null && !hitItem.isActive)
                    hitItem = null;
            }

            // ── Check if gaze target changed ─────────────────────────────────────
            bool sameTarget = (hitTrigger == _hoveredTrigger && hitItem == _hoveredItem);
            if (!sameTarget)
            {
                ClearHover();
                _hoveredTrigger = hitTrigger;
                _hoveredItem    = hitItem;
                _dwellTimer     = 0f;

                // Highlight the newly hovered item
                if (_hoveredItem != null)
                    StartHoverHighlight(_hoveredItem.gameObject);
                else if (_hoveredTrigger != null)
                    StartHoverHighlight(_hoveredTrigger.gameObject);
            }

            // ── Nothing in gaze → reset ───────────────────────────────────────────
            if (hitTrigger == null && hitItem == null) return;

            // ── Accumulate dwell ─────────────────────────────────────────────────
            float required = (hitTrigger != null) ? triggerDwellTime : itemDwellTime;
            _dwellTimer += Time.deltaTime;

            // Animate highlight toward white as dwell progresses
            UpdateHoverHighlight(_dwellTimer / required);

            if (_dwellTimer >= required)
            {
                _dwellTimer = 0f; // prevent re-firing every frame

                if (hitTrigger != null)
                {
                    // Toggle search panel
                    var panel = hitTrigger.panel;
                    if (panel != null)
                        panel.ToggleOpen();
                }
                else if (hitItem != null && hitItem.target != null)
                {
                    // Select item → activate directional arrow
                    var arrow = DirectionalArrow.Instance;
                    if (arrow != null)
                        arrow.SetTarget(hitItem.target);

                    // Close the panel after selection
                    var panel = CelestialSearchPanel.Instance;
                    if (panel != null)
                        panel.Close();
                }

                ClearHover();
            }
        }

        // ── Hover highlight helpers ───────────────────────────────────────────────

        private void StartHoverHighlight(GameObject go)
        {
            _hoveredRenderer = go.GetComponentInChildren<Renderer>();
            if (_hoveredRenderer != null)
                _hoveredBaseColor = _hoveredRenderer.material.color;
        }

        private void UpdateHoverHighlight(float t)
        {
            if (_hoveredRenderer == null) return;
            // Pulse toward a bright teal as dwell progresses
            Color target = new Color(0.3f, 0.85f, 1f, 1f);
            _hoveredRenderer.material.color = Color.Lerp(_hoveredBaseColor, target, t);
        }

        private void ClearHover()
        {
            if (_hoveredRenderer != null)
                _hoveredRenderer.material.color = _hoveredBaseColor;
            _hoveredRenderer = null;
            _hoveredTrigger  = null;
            _hoveredItem     = null;
        }
    }
}
