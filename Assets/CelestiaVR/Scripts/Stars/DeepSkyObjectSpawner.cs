using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Stars
{
    /// <summary>
    /// Spawns billboard quads for deep sky objects at their correct RA/Dec positions
    /// on the sky sphere. Each object gets a CelestialBody so gaze/dwell inspection works.
    ///
    /// Attach to [StarField] (child of [SkyManager]).
    /// Assign billboardMaterial + drag the 5 textures in order (see tooltip).
    /// </summary>
    public class DeepSkyObjectSpawner : MonoBehaviour
    {
        [Tooltip("URP Unlit, Surface = Transparent, Blend = Alpha.")]
        public Material billboardMaterial;

        [Tooltip("Drag textures in this order:\n" +
                 "0 = andromeda.jpg\n" +
                 "1 = orion nebula.jpg\n" +
                 "2 = plesaides.jpg\n" +
                 "3 = HerculesCluster.jpg")]
        public Texture2D[] images = new Texture2D[4];

        // ── Hardcoded catalog ─────────────────────────────────────────────────────

        private struct DSO
        {
            public string name;
            public int    imageIndex;
            public float  raHours;
            public float  decDegrees;
            public float  displaySize;
            public string description;
        }

        private static readonly DSO[] Catalog = new DSO[]
        {
            new DSO
            {
                name        = "Andromeda Galaxy (M31)",
                imageIndex  = 0,
                raHours     = 0.712f,
                decDegrees  = 41.27f,
                displaySize = 30f,
                description = "The nearest large galaxy to our own, 2.5 million light-years away. " +
                              "Visible to the naked eye as a faint smudge — it contains roughly one trillion stars."
            },
            new DSO
            {
                name        = "Orion Nebula (M42)",
                imageIndex  = 1,
                raHours     = 5.590f,
                decDegrees  = -5.38f,
                displaySize = 18f,
                description = "A stellar nursery 1,344 light-years away where new stars are being born right now. " +
                              "One of the most photographed and studied objects in the sky."
            },
            new DSO
            {
                name        = "Pleiades (M45)",
                imageIndex  = 2,
                raHours     = 3.790f,
                decDegrees  = 24.12f,
                displaySize = 22f,
                description = "An open star cluster 444 light-years away, also called the Seven Sisters. " +
                              "Most people can spot 6 stars with the naked eye on a clear night."
            },
            new DSO
            {
                name        = "Hercules Cluster (M13)",
                imageIndex  = 3,
                raHours     = 16.694f,
                decDegrees  = 36.46f,
                displaySize = 12f,
                description = "A globular cluster 25,000 light-years away containing roughly 300,000 stars " +
                              "packed into a sphere 150 light-years across."
            },
        };

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            var skyManager = GetComponentInParent<SkyManager>();
            float radius   = skyManager != null ? skyManager.skyRadius - 5f : 495f;

            foreach (var dso in Catalog)
                SpawnBillboard(dso, radius);
        }

        // ── Internal ─────────────────────────────────────────────────────────────

        private void SpawnBillboard(DSO dso, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = dso.name;
            go.transform.SetParent(transform, false);

            Vector3 localPos = CelestialCoordinates.RADecToUnity(dso.raHours, dso.decDegrees, radius);
            go.transform.localPosition = localPos;
            go.transform.localScale    = Vector3.one * dso.displaySize;
            go.transform.localRotation = Quaternion.LookRotation(-localPos.normalized);

            Destroy(go.GetComponent<MeshCollider>());
            var col    = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;

            var r = go.GetComponent<MeshRenderer>();
            {
                // Use additive blending: black pixels = 0 contribution = invisible.
                // This makes the dark background of nebula/galaxy images disappear naturally.
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
                var mat = new Material(sh) { name = dso.name + "_Mat" };
                if (dso.imageIndex >= 0 && dso.imageIndex < images.Length && images[dso.imageIndex] != null)
                    mat.mainTexture = images[dso.imageIndex];
                mat.SetFloat("_Surface",  1f);
                mat.SetFloat("_Blend",    0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_ZWrite",   0f);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                r.material = mat;
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows    = false;

            var body = go.AddComponent<CelestialBody>();
            body.objectName           = dso.name;
            body.bodyType             = CelestialBodyType.DeepSkyObject;
            body.description          = dso.description;
            body.rightAscensionHours  = dso.raHours;
            body.declinationDegrees   = dso.decDegrees;
        }
    }
}
