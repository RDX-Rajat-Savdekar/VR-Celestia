using UnityEngine;

namespace CelestiaVR.Core
{
    /// <summary>
    /// Creates and manages the sky sphere mesh with inverted normals.
    /// Renders the Milky Way / skybox texture on the inside of a large sphere.
    ///
    /// Attach to the [SkySphere] child of [SkyManager].
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SkySphere : MonoBehaviour
    {
        [Header("Sphere")]
        [Tooltip("Should match SkyManager.skyRadius.")]
        [Range(100f, 1000f)]
        public float radius = 500f;
        public int longitudeSegments = 64;
        public int latitudeSegments = 32;

        [Header("Material")]
        public Material skyMaterial; // Assign a Milky Way / SpaceScape texture material

        private void Awake()
        {
            GetComponent<MeshFilter>().mesh = CreateInvertedSphere(radius, longitudeSegments, latitudeSegments);
            if (skyMaterial != null)
                GetComponent<MeshRenderer>().material = skyMaterial;

            // Sky sphere should not cast/receive shadows
            var mr = GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static Mesh CreateInvertedSphere(float r, int lonSegs, int latSegs)
        {
            var mesh = new Mesh();
            mesh.name = "InvertedSphere";

            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs      = new System.Collections.Generic.List<Vector2>();
            var tris     = new System.Collections.Generic.List<int>();

            for (int lat = 0; lat <= latSegs; lat++)
            {
                float phi = Mathf.PI * lat / latSegs;
                float sinPhi = Mathf.Sin(phi);
                float cosPhi = Mathf.Cos(phi);

                for (int lon = 0; lon <= lonSegs; lon++)
                {
                    float theta = 2f * Mathf.PI * lon / lonSegs;
                    float x = sinPhi * Mathf.Cos(theta);
                    float y = cosPhi;
                    float z = sinPhi * Mathf.Sin(theta);
                    vertices.Add(new Vector3(x, y, z) * r);
                    uvs.Add(new Vector2((float)lon / lonSegs, 1f - (float)lat / latSegs));
                }
            }

            for (int lat = 0; lat < latSegs; lat++)
            {
                for (int lon = 0; lon < lonSegs; lon++)
                {
                    int curr = lat * (lonSegs + 1) + lon;
                    int next = curr + lonSegs + 1;

                    // Invert winding order for inside rendering
                    tris.Add(curr);
                    tris.Add(curr + 1);
                    tris.Add(next);

                    tris.Add(next);
                    tris.Add(curr + 1);
                    tris.Add(next + 1);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // Flip normals inward
            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = -normals[i];
            mesh.normals = normals;

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
