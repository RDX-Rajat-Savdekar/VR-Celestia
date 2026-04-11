using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Stars
{
    /// <summary>
    /// Renders thousands of stars using GPU instancing (Graphics.DrawMeshInstanced).
    /// Requires StarBillboard shader and a StarCatalogParser sibling/parent.
    ///
    /// Attach to the [StarField] child of [SkyManager].
    /// </summary>
    [RequireComponent(typeof(StarCatalogParser))]
    public class StarRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        public Material starMaterial;

        [Tooltip("Size of the billboard quad mesh (Unity units). Sky radius is 500, so 0.3–0.8 looks realistic.")]
        [Range(0.05f, 5f)]
        public float baseSize = 0.6f;

        [Tooltip("Scale brighter stars larger.")]
        [Range(0f, 5f)]
        public float sizeByBrightness = 1.5f;

        [Tooltip("Overall brightness multiplier. Drag up to make all stars brighter on a monitor.")]
        [Range(0.1f, 5f)]
        public float brightnessMultiplier = 2f;

        private SkyManager _skyManager;
        private List<StarData> _stars;
        private Mesh _quadMesh;

        [Header("Twinkle")]
        [Tooltip("How much brightness varies (0 = none, 0.4 = noticeable twinkle).")]
        [Range(0f, 0.8f)]
        public float twinkleAmount = 0.5f;

        [Tooltip("Base twinkle speed. Stars get random offsets so they don't all pulse together.")]
        [Range(0.5f, 5f)]
        public float twinkleSpeed = 2f;

        [Header("Named Stars")]
        [Tooltip("Named stars brighter than this magnitude will be spawned as sphere objects by NamedStarSpawner — skip them here to avoid double-rendering.")]
        [Range(0f, 6f)]
        public float namedStarSphereThreshold = 3.0f;
        public bool skipNamedStarsInBillboards = true;

        /// <summary>
        /// Day/night fade multiplier (0 = all stars invisible, 1 = full night sky).
        /// Set each frame by DayNightController.
        /// </summary>
        public float GlobalBrightnessFade { get; set; } = 1f;

        // GPU instancing batches (DrawMeshInstanced max 1023 per call)
        private const int BatchSize = 1023;
        private Matrix4x4[][] _matrices;
        private MaterialPropertyBlock[] _propertyBlocks;
        private float[][] _baseBrightnesses;   // original per-star brightness
        private float[][] _twinklePhases;       // random per-star phase offset
        private Vector3[][] _basePositions;     // unrotated world-space positions
        private float[][] _baseSizes;           // per-star quad sizes
        private Quaternion _lastSkyRotation = Quaternion.identity;
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int BrightnessProp = Shader.PropertyToID("_Brightness");

        public void Initialize(SkyManager skyManager)
        {
            _skyManager = skyManager;
            var parser = GetComponent<StarCatalogParser>();
            parser.OnCatalogLoaded += OnStarsLoaded;
        }

        private void OnStarsLoaded(List<StarData> stars)
        {
            _stars = stars;
            Debug.Log($"[StarRenderer] Received {stars.Count} stars. Building render batches...");

            if (starMaterial == null)
            {
                Debug.LogWarning("[StarRenderer] starMaterial not assigned — creating a fallback Unlit/Additive white material.");
                starMaterial = CreateFallbackStarMaterial();
            }
            else
            {
                Debug.Log($"[StarRenderer] Using assigned material: {starMaterial.name}, shader: {starMaterial.shader.name}");
            }

            _quadMesh = CreateQuadMesh();
            BuildBatches();
        }

        /// Creates an Unlit additive material with a soft circular dot texture so stars look
        /// like glowing points of light rather than squares.
        private static Material CreateFallbackStarMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.name = "StarFallbackMaterial";
            mat.color = Color.white;

            // Additive blending — bright stars glow, dark sky stays dark
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 3);
            mat.SetFloat("_SrcBlend",  (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend",  (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.enableInstancing = true;

            // Assign a procedural soft circular dot as the base texture
            mat.mainTexture = CreateStarDotTexture(64);

            Debug.Log($"[StarRenderer] Fallback star material created (shader={mat.shader.name}, dot texture applied).");
            return mat;
        }

        /// Generates a 64x64 radial gradient texture: bright white centre fading to transparent.
        private static Texture2D CreateStarDotTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "StarDot";
            float half = size * 0.5f;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy); // 0=centre, 1=edge
                    // Smooth falloff: bright at centre, transparent at edge
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha; // squared = sharper centre, softer edge
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void BuildBatches()
        {
            if (_stars == null || _stars.Count == 0)
            {
                Debug.LogWarning("[StarRenderer] BuildBatches called but star list is empty.");
                return;
            }

            int batchCount = Mathf.CeilToInt((float)_stars.Count / BatchSize);
            Debug.Log($"[StarRenderer] Building {batchCount} GPU batches for {_stars.Count} stars (BatchSize={BatchSize}).");
            _matrices         = new Matrix4x4[batchCount][];
            _propertyBlocks   = new MaterialPropertyBlock[batchCount];
            _baseBrightnesses = new float[batchCount][];
            _twinklePhases    = new float[batchCount][];
            _basePositions    = new Vector3[batchCount][];
            _baseSizes        = new float[batchCount][];

            for (int b = 0; b < batchCount; b++)
            {
                int start = b * BatchSize;
                int count = Mathf.Min(BatchSize, _stars.Count - start);
                _matrices[b]         = new Matrix4x4[count];
                _baseBrightnesses[b] = new float[count];
                _twinklePhases[b]    = new float[count];
                _basePositions[b]    = new Vector3[count];
                _baseSizes[b]        = new float[count];

                var block = new MaterialPropertyBlock();
                var colors = new Vector4[count];
                var brightnesses = new float[count];

                int skipped = 0;
                for (int i = 0; i < count; i++)
                {
                    var star = _stars[start + i];

                    // Named bright stars are rendered as sphere objects — leave a zero-scale
                    // placeholder so batch indices stay stable, but make it invisible.
                    bool isNamedSphere = skipNamedStarsInBillboards
                        && !string.IsNullOrEmpty(star.properName)
                        && star.magnitude < namedStarSphereThreshold;
                    if (isNamedSphere) { skipped++; }

                    float size = isNamedSphere ? 0f : baseSize + star.brightness * sizeByBrightness;
                    Vector3 pos = star.unitPosition * _skyManager.skyRadius;
                    Quaternion rot = Quaternion.LookRotation(-pos.normalized);
                    _basePositions[b][i]    = pos;
                    _baseSizes[b][i]        = size;
                    _matrices[b][i]         = Matrix4x4.TRS(pos, rot, Vector3.one * size);
                    colors[i]               = star.starColor;
                    brightnesses[i]         = star.brightness;
                    _baseBrightnesses[b][i] = star.brightness;
                    _twinklePhases[b][i]    = Random.Range(0f, Mathf.PI * 2f); // random start phase
                }

                block.SetVectorArray(ColorProp, colors);
                block.SetFloatArray(BrightnessProp, brightnesses);
                _propertyBlocks[b] = block;
                if (skipped > 0)
                    Debug.Log($"[StarRenderer] Batch {b}: skipped {skipped} named-star billboard(s) (will be sphere objects).");
            }
        }

        private bool _renderLogDone = false;

        private void Update()
        {
            if (_matrices == null || starMaterial == null)
            {
                if (!_renderLogDone)
                {
                    Debug.LogWarning($"[StarRenderer] Update: not rendering — matrices={_matrices != null}, material={starMaterial != null}");
                    _renderLogDone = true;
                }
                return;
            }
            if (!_renderLogDone)
            {
                Debug.Log($"[StarRenderer] Now rendering {_matrices.Length} batches every frame.");
                _renderLogDone = true;
            }

            // Recompute star positions when SkyManager rotation changes (once per second)
            Quaternion skyRot = _skyManager.transform.rotation;
            if (skyRot != _lastSkyRotation)
            {
                _lastSkyRotation = skyRot;
                for (int b = 0; b < _matrices.Length; b++)
                {
                    for (int i = 0; i < _matrices[b].Length; i++)
                    {
                        Vector3 rotatedPos = skyRot * _basePositions[b][i];
                        Quaternion rot = Quaternion.LookRotation(-rotatedPos.normalized);
                        _matrices[b][i] = Matrix4x4.TRS(rotatedPos, rot, Vector3.one * _baseSizes[b][i]);
                    }
                }
            }

            float t = Time.time * twinkleSpeed;

            for (int b = 0; b < _matrices.Length; b++)
            {
                {
                    int count = _matrices[b].Length;
                    var animBrightness = new float[count];
                    for (int i = 0; i < count; i++)
                    {
                        float twinkle = 1f + Mathf.Sin(t + _twinklePhases[b][i]) * twinkleAmount;
                        animBrightness[i] = _baseBrightnesses[b][i] * twinkle * brightnessMultiplier * GlobalBrightnessFade;
                    }
                    _propertyBlocks[b].SetFloatArray(BrightnessProp, animBrightness);
                }

                Graphics.DrawMeshInstanced(
                    _quadMesh,
                    0,
                    starMaterial,
                    _matrices[b],
                    _matrices[b].Length,
                    _propertyBlocks[b],
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false
                );
            }
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.name = "StarQuad";

            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0)
            };

            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
