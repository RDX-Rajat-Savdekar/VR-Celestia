using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.Stars;

namespace CelestiaVR.Constellations
{
    /// <summary>
    /// One-stop Stellarium constellation system.
    ///
    /// Replaces both ConstellationHIPRenderer and ConstellationArtRenderer.
    ///
    /// What it does:
    ///   1. Builds stick-figure LineRenderers for all 88 IAU constellations
    ///      using HIP pairs from ConstellationHIPData (generated from Stellarium JSON).
    ///   2. Places transparent art quads for 85 constellations using three
    ///      HIP anchor points from StellariumArtData — quads are positioned
    ///      and rotated precisely by solving the affine pixel→sky transform.
    ///
    /// SETUP:
    ///   1. Attach this to the [Constellations] child of [SkyManager].
    ///   2. Copy the PNG illustrations folder to:
    ///         Assets/Resources/ConstellationArt/
    ///      (all 88 PNGs must live directly in that folder — no subfolders.)
    ///   3. Press Play. Everything is automatic.
    ///
    /// If ConstellationHIPRenderer or ConstellationArtRenderer are also on this
    /// GameObject they are disabled automatically to prevent duplicates.
    /// </summary>
    public class StellariumLoader : MonoBehaviour
    {
        public static StellariumLoader Instance { get; private set; }

        [Header("Stick Figures")]
        public bool  showLines   = true;
        public Color lineColor   = new Color(0.35f, 0.55f, 1f, 0.45f);
        [Range(0.1f, 2f)]
        public float lineWidth   = 0.35f;

        [Header("Constellation Art")]
        public bool  showArt   = true;
        [Range(0f, 1f)]
        public float artOpacity = 0.55f;
        [Tooltip("Tint blended with the art texture (cool blue-white looks natural).")]
        public Color artTint   = new Color(0.75f, 0.85f, 1f, 1f);

        // ── Runtime ──────────────────────────────────────────────────────────────

        private SkyManager              _sky;
        private Dictionary<int,Vector3> _hipPos  = new();
        private readonly List<GameObject> _lineGOs = new();
        private readonly List<GameObject> _artGOs  = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _sky = GetComponentInParent<SkyManager>();

            // Disable ALL legacy constellation renderers scene-wide — they draw a second,
            // less-detailed line layer that shows through when StellariumLoader lines are toggled.
            foreach (var r in FindObjectsByType<ConstellationHIPRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                r.enabled = false;
            foreach (var r in FindObjectsByType<ConstellationArtRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                r.enabled = false;
        }

        private void Start()
        {
            var parser = FindFirstObjectByType<StarCatalogParser>();
            if (parser == null)
            {
                Debug.LogError("[StellariumLoader] No StarCatalogParser found in scene.");
                return;
            }
            parser.OnCatalogLoaded += OnCatalogLoaded;
        }

        private void OnCatalogLoaded(List<StarData> stars)
        {
            foreach (var s in stars)
                if (s.hipId > 0 && !_hipPos.ContainsKey(s.hipId))
                    _hipPos[s.hipId] = s.unitPosition;

            StartCoroutine(Build());
        }

        // ── Build ─────────────────────────────────────────────────────────────────

        private IEnumerator Build()
        {
            yield return null; // let frame settle

            Transform root   = _sky != null ? _sky.transform : transform;
            float     radius = _sky != null ? _sky.skyRadius  : 500f;

            if (showLines) BuildLines(root, radius);
            if (showArt)   BuildArt(root, radius);
            BuildConstellationMarkers(root, radius);
        }

        // ── Stick figures ─────────────────────────────────────────────────────────

        private void BuildLines(Transform root, float radius)
        {
            var lineMat = BuildLineMat();
            int built = 0, skipped = 0;

            foreach (var def in ConstellationHIPData.All)
            {
                var go = new GameObject($"Lines_{def.Abbreviation}");
                go.transform.SetParent(root, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.material       = lineMat;
                lr.startWidth     = lineWidth;
                lr.endWidth       = lineWidth;
                lr.useWorldSpace  = false;
                lr.startColor     = lineColor;
                lr.endColor       = lineColor;

                var pts = new List<Vector3>();
                int[] segs = def.Segments;
                for (int i = 0; i + 1 < segs.Length; i += 2)
                {
                    if (!_hipPos.TryGetValue(segs[i],   out Vector3 a)) continue;
                    if (!_hipPos.TryGetValue(segs[i+1], out Vector3 b)) continue;
                    pts.Add(a * radius);
                    pts.Add(b * radius);
                }

                if (pts.Count == 0) { Destroy(go); skipped++; continue; }

                lr.positionCount = pts.Count;
                lr.SetPositions(pts.ToArray());
                _lineGOs.Add(go);
                built++;
            }

        }

        private Material BuildLineMat()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh) { name = "ConstellationLineMat" };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite",  0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.enableInstancing = false;
            mat.renderQueue = 3001;
            mat.SetColor("_BaseColor", lineColor);
            mat.color = lineColor;
            return mat;
        }

        // ── Art quads ─────────────────────────────────────────────────────────────

        private void BuildArt(Transform root, float radius)
        {
            var artMat = BuildArtMat();
            int built = 0, skipped = 0;

            foreach (var def in StellariumArtData.All)
            {
                // Load texture from Resources/ConstellationArt/
                var tex = Resources.Load<Texture2D>($"ConstellationArt/{def.PngName}");
                if (tex == null) { skipped++; continue; }

                if (!_hipPos.TryGetValue(def.A1.Hip, out Vector3 p1)) { skipped++; continue; }
                if (!_hipPos.TryGetValue(def.A2.Hip, out Vector3 p2)) { skipped++; continue; }
                if (!_hipPos.TryGetValue(def.A3.Hip, out Vector3 p3)) { skipped++; continue; }

                if (!SolveAffine(
                    p1, new Vector2(def.A1.Px, def.A1.Py),
                    p2, new Vector2(def.A2.Px, def.A2.Py),
                    p3, new Vector2(def.A3.Px, def.A3.Py),
                    def.ImageW, def.ImageH,
                    out Vector3 center, out float worldSizeX, out float worldSizeY, out float roll))
                { skipped++; continue; }

                center = center.normalized;

                // Physical size on sky sphere
                float sizeX = worldSizeX * radius;
                float sizeY = worldSizeY * radius;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Art_{def.Abbreviation}";
                go.transform.SetParent(root, false);
                Destroy(go.GetComponent<Collider>());

                go.transform.localPosition = center * radius;
                go.transform.localRotation = Quaternion.LookRotation(-center)
                                           * Quaternion.Euler(0, 0, roll);
                go.transform.localScale    = new Vector3(sizeX, sizeY, 1f);

                var mat = new Material(artMat);
                mat.SetTexture("_BaseMap", tex);    // URP Unlit uses _BaseMap
                mat.mainTexture = tex;              // legacy fallback
                var tintColor = new Color(artTint.r, artTint.g, artTint.b, artOpacity);
                mat.SetColor("_BaseColor", tintColor);
                mat.color = tintColor;
                go.GetComponent<Renderer>().material = mat;

                go.SetActive(showArt);
                _artGOs.Add(go);
                built++;
            }

        }

        private Material BuildArtMat()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(sh) { name = "ConstellationArtMat" };

            // Transparent surface, additive-style blend: src*srcAlpha + dest
            // This makes black pixels (grayscale=0) fully transparent and
            // bright art pixels add their colour to the sky background.
            mat.SetFloat("_Surface",  1f);  // Transparent
            mat.SetFloat("_Blend",    0f);  // manual control below
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); // additive
            mat.SetFloat("_ZWrite",   0f);
            mat.SetFloat("_Cull",     0f);  // Cull Off — viewed from inside sky sphere

            // IMPORTANT: do NOT enable _ALPHAPREMULTIPLY_ON — it would square the alpha
            // (opacity × opacity) making art nearly invisible.
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            mat.renderQueue = 3000;
            return mat;
        }

        // ── Affine solver ─────────────────────────────────────────────────────────

        /// <summary>
        /// Given three HIP star unit-sphere positions and their pixel coordinates in the art image,
        /// solves for the image centre direction, physical world size, and roll angle.
        ///
        /// Pixel y increases downward in image space. The solved roll compensates for this
        /// so the art figure appears right-side up in the sky.
        /// </summary>
        private static bool SolveAffine(
            Vector3 p1, Vector2 px1,
            Vector3 p2, Vector2 px2,
            Vector3 p3, Vector2 px3,
            int imgW, int imgH,
            out Vector3 center, out float worldSizeX, out float worldSizeY, out float rollDeg)
        {
            center = Vector3.zero; worldSizeX = worldSizeY = rollDeg = 0f;

            // Affine map: P(u,v) = origin + u*du + v*dv
            // Subtracting p1: dp2 = (dx2)*du + (dy2)*dv,  dp3 = (dx3)*du + (dy3)*dv
            float dx2 = px2.x - px1.x, dy2 = px2.y - px1.y;
            float dx3 = px3.x - px1.x, dy3 = px3.y - px1.y;
            float det = dx2 * dy3 - dy2 * dx3;
            if (Mathf.Abs(det) < 1e-4f) return false;

            Vector3 dp2 = p2 - p1, dp3 = p3 - p1;
            Vector3 du  = (dy3 * dp2 - dy2 * dp3) / det;  // unit-sphere units / pixel
            Vector3 dv  = (dx2 * dp3 - dx3 * dp2) / det;
            Vector3 org = p1 - px1.x * du - px1.y * dv;

            // Centre of the image (pixel W/2, H/2)
            center = org + (imgW * 0.5f) * du + (imgH * 0.5f) * dv;
            if (center.sqrMagnitude < 1e-8f) return false;

            // Angular size: image covers imgW pixels; each pixel = |du| radians on unit sphere
            worldSizeX = imgW * du.magnitude;  // radians
            worldSizeY = imgH * dv.magnitude;  // radians

            // Roll: angle between the quad's default right (from LookRotation(-center)) and
            // the image's +x direction (du), projected onto the quad's tangent plane.
            Vector3 centerNorm = center.normalized;
            Quaternion faceRot   = Quaternion.LookRotation(-centerNorm);
            Vector3 defaultRight = faceRot * Vector3.right;

            // In image space, +x is right and +y is DOWN; visual up = -dv direction.
            Vector3 imageRight = Vector3.ProjectOnPlane(du.normalized, centerNorm);
            if (imageRight.sqrMagnitude < 1e-6f) return false;
            imageRight.Normalize();

            rollDeg = Vector3.SignedAngle(defaultRight, imageRight, -centerNorm);
            return true;
        }

        // ── Constellation markers (for labels + search) ───────────────────────────

        /// <summary>
        /// Creates an invisible centroid marker for each constellation so that
        /// SkyLabelManager can place a floating name label, and the search panel
        /// can list and navigate to constellations.
        /// </summary>
        private void BuildConstellationMarkers(Transform root, float radius)
        {
            int built = 0;
            foreach (var def in ConstellationHIPData.All)
            {
                // Compute centroid of all unique HIP stars used by this constellation
                var hipSet = new System.Collections.Generic.HashSet<int>(def.Segments);
                var centroid = Vector3.zero;
                int valid = 0;
                foreach (int hip in hipSet)
                {
                    if (_hipPos.TryGetValue(hip, out Vector3 p))
                    { centroid += p; valid++; }
                }
                if (valid == 0) continue;

                centroid = (centroid / valid).normalized;

                var go = new GameObject($"ConstellationMarker_{def.Abbreviation}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = centroid * radius;

                var body = go.AddComponent<CelestialBody>();
                body.objectName   = def.Name;
                body.bodyType     = CelestialBodyType.Constellation;
                body.description  = $"{def.Name} ({def.Abbreviation}) — one of the 88 IAU constellations.";

                // Large sphere collider so dwell-gaze can hit it even with star-sphere tolerance
                var col = go.AddComponent<SphereCollider>();
                col.radius    = 20f;
                col.isTrigger = true;

                built++;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void SetLinesVisible(bool v)
        {
            showLines = v;
            foreach (var g in _lineGOs) if (g != null) g.SetActive(v);
        }

        public void SetArtVisible(bool v)
        {
            showArt = v;
            foreach (var g in _artGOs) if (g != null) g.SetActive(v);
        }

        public bool AreLinesVisible => showLines;
        public bool IsArtVisible    => showArt;
    }
}
