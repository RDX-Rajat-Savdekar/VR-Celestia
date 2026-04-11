using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Creates permanent floating name labels for planets, moons, and deep sky objects.
    /// Labels are root-level GameObjects (not children of the bodies) so they never
    /// inherit body scale — they always appear at a fixed readable world-size.
    ///
    /// Add this component to any persistent GameObject in the scene (e.g. on
    /// SelectionManager or InspectionController). It auto-finds everything it needs.
    ///
    /// Label size: controlled by labelWorldHeight (Unity units). At sky radius 500,
    /// set ~6–10 for clearly readable names.
    /// </summary>
    public class SkyLabelManager : MonoBehaviour
    {
        public static SkyLabelManager Instance { get; private set; }

        [Header("Which bodies get labels")]
        public bool labelPlanets        = true;
        public bool labelMoons          = true;
        public bool labelDeepSky        = true;
        public bool labelNamedStars     = false; // named stars already have their own glow
        public bool labelConstellations = true;

        [Header("Deep Sky / Planet (most prominent)")]
        public Color deepSkyColor         = new Color(1.00f, 0.92f, 0.75f, 0.95f); // warm white
        public float deepSkyWorldHeight   = 12f;
        public Color planetColor          = new Color(1.00f, 1.00f, 1.00f, 0.90f); // bright white
        public float planetWorldHeight    = 10f;

        [Header("Moon")]
        public Color moonColor            = new Color(0.85f, 0.85f, 0.85f, 0.65f);
        public float moonWorldHeight      = 7f;

        [Header("Constellation (most subtle)")]
        public Color constellationLabelColor  = new Color(0.40f, 0.55f, 0.90f, 0.28f); // faint blue
        public float constellationWorldHeight = 6f;

        [Header("Appearance")]
        [Tooltip("Vertical offset above the object center, in world units.")]
        public float verticalOffset    = 6f;
        [Tooltip("Outline colour for contrast against bright stars.")]
        public Color outlineColor      = new Color(0f, 0f, 0f, 0.6f);

        // ── Runtime ───────────────────────────────────────────────────────────────

        private struct LabelEntry
        {
            public Transform bodyTransform;
            public GameObject labelGO;
            public TextMeshPro tmp;
        }

        private readonly List<LabelEntry> _labels = new();
        private Camera _cam;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _cam = Camera.main;
            // Wait one frame so PlanetController, DeepSkyObjectSpawner etc. have all run Start()
            StartCoroutine(InitLabels());
        }

        private IEnumerator InitLabels()
        {
            yield return null; // wait one frame
            yield return null; // wait a second frame for safety (catalog load is async)

            // Give the catalog a moment — star parser uses coroutines
            float waited = 0f;
            while (waited < 1.5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            BuildLabels();
        }

        private void BuildLabels()
        {
            var bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
            int created = 0;

            foreach (var body in bodies)
            {
                if (!ShouldLabel(body)) continue;

                var label = CreateLabel(body);
                _labels.Add(label);
                created++;
            }

        }

        private bool ShouldLabel(CelestialBody body)
        {
            return body.bodyType switch
            {
                CelestialBodyType.Planet        => labelPlanets,
                CelestialBodyType.Moon          => labelMoons,
                CelestialBodyType.DeepSkyObject => labelDeepSky,
                CelestialBodyType.Star          => labelNamedStars,
                CelestialBodyType.Constellation => labelConstellations,
                _                               => false
            };
        }

        private (Color color, float worldHeight) StyleForType(CelestialBodyType t) => t switch
        {
            CelestialBodyType.DeepSkyObject => (deepSkyColor,          deepSkyWorldHeight),
            CelestialBodyType.Planet        => (planetColor,           planetWorldHeight),
            CelestialBodyType.Moon          => (moonColor,             moonWorldHeight),
            CelestialBodyType.Constellation => (constellationLabelColor, constellationWorldHeight),
            _                               => (planetColor,           planetWorldHeight),
        };

        private LabelEntry CreateLabel(CelestialBody body)
        {
            var go = new GameObject($"Label_{body.objectName}");
            // Root-level so it inherits NO scale from the body
            go.transform.SetParent(null, false);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text           = body.objectName;
            tmp.alignment      = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            // Outline for sky contrast — constellations get no outline (too faint to need it)
            bool isConstellation = body.bodyType == CelestialBodyType.Constellation;
            tmp.outlineWidth  = isConstellation ? 0f : 0.2f;
            tmp.outlineColor  = outlineColor;

            var (color, worldH) = StyleForType(body.bodyType);
            tmp.color    = color;
            tmp.fontSize = 5f;   // base; actual world size set via transform.localScale

            // Scale so the text appears worldH units tall in world space.
            // TMP fontSize=5 ≈ 0.5 world units height at scale 1.
            float scale = worldH * 2f;
            go.transform.localScale = Vector3.one * scale;

            // Initial position
            if (body.transform != null)
                go.transform.position = LabelWorldPos(body.transform);

            return new LabelEntry
            {
                bodyTransform = body.transform,
                labelGO       = go,
                tmp           = tmp,
            };
        }

        // ── Per-frame update ──────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;

            for (int i = _labels.Count - 1; i >= 0; i--)
            {
                var e = _labels[i];
                if (e.bodyTransform == null || e.labelGO == null)
                {
                    _labels.RemoveAt(i);
                    continue;
                }

                // Position: above the body from the camera's perspective
                e.labelGO.transform.position = LabelWorldPos(e.bodyTransform);

                // Billboard: always face camera
                if (_cam != null)
                {
                    Vector3 dir = e.labelGO.transform.position - _cam.transform.position;
                    if (dir.sqrMagnitude > 0.001f)
                        e.labelGO.transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        private Vector3 LabelWorldPos(Transform body)
        {
            // Offset in camera-up direction so the label appears "above" from the player's view
            Vector3 up = _cam != null ? _cam.transform.up : Vector3.up;
            return body.position + up * verticalOffset;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Rebuild all labels — call after new bodies are spawned at runtime.</summary>
        public void Refresh()
        {
            foreach (var e in _labels)
                if (e.labelGO != null) Destroy(e.labelGO);
            _labels.Clear();
            BuildLabels();
        }

        /// <summary>Show or hide all planet (and moon) labels.</summary>
        public void SetPlanetLabelsVisible(bool v)
        {
            labelPlanets = v;
            labelMoons   = v;
            foreach (var e in _labels)
            {
                if (e.labelGO == null || e.bodyTransform == null) continue;
                var body = e.bodyTransform.GetComponent<CelestialBody>();
                if (body == null) continue;
                if (body.bodyType == CelestialBodyType.Planet || body.bodyType == CelestialBodyType.Moon)
                    e.labelGO.SetActive(v);
            }
        }

        public bool ArePlanetLabelsVisible => labelPlanets;
    }
}
