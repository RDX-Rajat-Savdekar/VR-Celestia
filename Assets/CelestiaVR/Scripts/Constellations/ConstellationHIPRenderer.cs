using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.Stars;

namespace CelestiaVR.Constellations
{
    /// <summary>
    /// Renders constellation stick figures using Hipparcos (HIP) catalog numbers.
    /// Uses the HYG catalog loaded by StarCatalogParser to resolve star positions —
    /// no external text files needed.
    ///
    /// Attach to the [Constellations] child of [SkyManager].
    /// Assign a reference to the [StarField] StarCatalogParser in the inspector.
    /// </summary>
    public class ConstellationHIPRenderer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The StarCatalogParser on [StarField]. It fires OnCatalogLoaded which triggers constellation build.")]
        public StarCatalogParser catalogParser;

        [Header("Visuals")]
        [Range(0f, 1f)]
        public float lineOpacity = 0.35f;
        public Color lineColor = new Color(0.5f, 0.75f, 1f, 0.35f);
        [Range(0.05f, 1f)]
        public float lineWidth = 0.18f;

        [Header("Filter")]
        [Tooltip("Toggle all constellation lines on/off at runtime.")]
        public bool showConstellations = true;

        [Header("Sky")]
        [Tooltip("Auto-found from parent SkyManager if left null.")]
        public SkyManager skyManager;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private readonly List<GameObject> _lineObjects = new();
        private Material _lineMaterial;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (skyManager == null)
                skyManager = GetComponentInParent<SkyManager>();

            if (catalogParser == null)
                catalogParser = FindFirstObjectByType<StarCatalogParser>();

            if (catalogParser != null)
                catalogParser.OnCatalogLoaded += OnCatalogLoaded;
            else
                Debug.LogError("[ConstellationHIPRenderer] No StarCatalogParser found. Assign it in the inspector.");
        }

        private void OnDestroy()
        {
            if (catalogParser != null)
                catalogParser.OnCatalogLoaded -= OnCatalogLoaded;
        }

        // ── Catalog loaded callback ───────────────────────────────────────────────

        private void OnCatalogLoaded(List<StarData> stars)
        {
            // Build HIP → unit-sphere position lookup
            var hipToPos = new Dictionary<int, Vector3>(stars.Count);
            foreach (var star in stars)
            {
                if (star.hipId > 0 && !hipToPos.ContainsKey(star.hipId))
                    hipToPos[star.hipId] = star.unitPosition;
            }

            Debug.Log($"[ConstellationHIPRenderer] Catalog loaded with {hipToPos.Count} HIP entries. Building constellation lines...");
            BuildLines(hipToPos);
        }

        // ── Line building ─────────────────────────────────────────────────────────

        private void BuildLines(Dictionary<int, Vector3> hipToPos)
        {
            // Clean up any previous build
            foreach (var go in _lineObjects) Destroy(go);
            _lineObjects.Clear();

            _lineMaterial = BuildLineMaterial();

            float radius = skyManager != null ? skyManager.skyRadius : 500f;

            int totalSegments = 0;
            int skippedSegments = 0;

            foreach (var constellation in ConstellationHIPData.All)
            {
                var parent = new GameObject(constellation.Name);
                parent.transform.SetParent(transform, false);
                parent.SetActive(showConstellations);
                _lineObjects.Add(parent);

                int[] segs = constellation.Segments;
                for (int i = 0; i + 1 < segs.Length; i += 2)
                {
                    int hipA = segs[i];
                    int hipB = segs[i + 1];
                    totalSegments++;

                    if (!hipToPos.TryGetValue(hipA, out Vector3 posA) ||
                        !hipToPos.TryGetValue(hipB, out Vector3 posB))
                    {
                        skippedSegments++;
                        continue; // star not in catalog (below mag limit or no HIP entry)
                    }

                    var segGO = new GameObject($"{hipA}-{hipB}");
                    segGO.transform.SetParent(parent.transform, false);

                    var lr = segGO.AddComponent<LineRenderer>();
                    lr.useWorldSpace    = false; // local to SkyManager, rotates with sidereal time
                    lr.positionCount    = 2;
                    lr.SetPosition(0, posA * radius);
                    lr.SetPosition(1, posB * radius);
                    lr.startWidth       = lineWidth;
                    lr.endWidth         = lineWidth;
                    lr.material         = _lineMaterial;
                    lr.startColor       = new Color(lineColor.r, lineColor.g, lineColor.b, lineOpacity);
                    lr.endColor         = new Color(lineColor.r, lineColor.g, lineColor.b, lineOpacity);
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows   = false;
                    lr.alignment        = LineAlignment.View;
                }
            }

            int drawn = totalSegments - skippedSegments;
            Debug.Log($"[ConstellationHIPRenderer] Built {drawn}/{totalSegments} segments across " +
                      $"{ConstellationHIPData.All.Length} constellations. " +
                      $"({skippedSegments} segments skipped — stars below mag limit or not in HIP.)");
        }

        private Material BuildLineMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");

            var mat = new Material(sh) { name = "ConstellationLineMat" };
            mat.color = new Color(lineColor.r, lineColor.g, lineColor.b, lineOpacity);
            mat.SetFloat("_Surface",  1f); // Transparent
            mat.SetFloat("_Blend",    0f); // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite",   0f);
            mat.renderQueue = 3001;
            return mat;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void SetVisible(bool visible)
        {
            showConstellations = visible;
            foreach (var go in _lineObjects)
                go.SetActive(visible);
        }

        public void Toggle() => SetVisible(!showConstellations);
    }
}
