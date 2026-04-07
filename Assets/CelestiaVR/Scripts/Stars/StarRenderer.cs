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

        [Tooltip("Size of the billboard quad mesh (Unity units). Sky radius is 500, so 0.5–2 looks good.")]
        [Range(0.1f, 5f)]
        public float baseSize = 1.0f;

        [Tooltip("Scale brighter stars larger.")]
        [Range(0f, 5f)]
        public float sizeByBrightness = 2f;

        private SkyManager _skyManager;
        private List<StarData> _stars;
        private Mesh _quadMesh;

        // GPU instancing batches (DrawMeshInstanced max 1023 per call)
        private const int BatchSize = 1023;
        private Matrix4x4[][] _matrices;
        private MaterialPropertyBlock[] _propertyBlocks;
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
            _quadMesh = CreateQuadMesh();
            BuildBatches();
        }

        private void BuildBatches()
        {
            if (_stars == null || _stars.Count == 0) return;

            int batchCount = Mathf.CeilToInt((float)_stars.Count / BatchSize);
            _matrices = new Matrix4x4[batchCount][];
            _propertyBlocks = new MaterialPropertyBlock[batchCount];

            for (int b = 0; b < batchCount; b++)
            {
                int start = b * BatchSize;
                int count = Mathf.Min(BatchSize, _stars.Count - start);
                _matrices[b] = new Matrix4x4[count];

                var block = new MaterialPropertyBlock();
                var colors = new Vector4[count];
                var brightnesses = new float[count];

                for (int i = 0; i < count; i++)
                {
                    var star = _stars[start + i];
                    float size = baseSize + star.brightness * sizeByBrightness;
                    // Position on sky sphere in local space
                    Vector3 pos = star.unitPosition * _skyManager.skyRadius;
                    _matrices[b][i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * size);
                    colors[i] = star.starColor;
                    brightnesses[i] = star.brightness;
                }

                block.SetVectorArray(ColorProp, colors);
                block.SetFloatArray(BrightnessProp, brightnesses);
                _propertyBlocks[b] = block;
            }
        }

        private void Update()
        {
            if (_matrices == null || starMaterial == null) return;

            for (int b = 0; b < _matrices.Length; b++)
            {
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
