using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Stars
{
    /// <summary>
    /// Spawns billboard sprites for deep sky objects (nebulae, galaxies, clusters)
    /// using the images in DeepSkyImages/.
    ///
    /// Attach to [StarField] or a [DeepSkyRoot] child of [SkyManager].
    /// </summary>
    public class DeepSkyObjectSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class DeepSkyObject
        {
            public string objectName;
            public Texture2D image;
            [Tooltip("Right Ascension in hours")]
            public float raHours;
            [Tooltip("Declination in degrees")]
            public float decDegrees;
            [Tooltip("Angular size on sky sphere (Unity units)")]
            public float displaySize = 5f;
            [TextArea(1, 3)]
            public string description;
        }

        public List<DeepSkyObject> deepSkyObjects;
        public Material billboardMaterial; // Unlit/Transparent

        // Default known RA/Dec for included images
        // Andromeda M31: RA 00h42m44s, Dec +41°16'
        // Orion Nebula M42: RA 05h35m17s, Dec −05°23'
        // Pleiades M45: RA 03h47m24s, Dec +24°07'
        // Hercules Cluster M13: RA 16h41m41s, Dec +36°28'

        private void Start()
        {
            var skyManager = GetComponentInParent<SkyManager>();
            float radius = skyManager != null ? skyManager.skyRadius - 5f : 495f;

            foreach (var obj in deepSkyObjects)
            {
                SpawnBillboard(obj, radius);
            }
        }

        private void SpawnBillboard(DeepSkyObject obj, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = obj.objectName;
            go.transform.SetParent(transform, false);

            // Position on sky sphere (local space, SkyManager will rotate)
            Vector3 localPos = CelestialCoordinates.RADecToUnity(obj.raHours, obj.decDegrees, radius);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * obj.displaySize;
            // Face inward (toward origin)
            go.transform.localRotation = Quaternion.LookRotation(-localPos.normalized);

            // Material + texture
            var renderer = go.GetComponent<MeshRenderer>();
            if (billboardMaterial != null && obj.image != null)
            {
                var mat = new Material(billboardMaterial);
                mat.mainTexture = obj.image;
                renderer.material = mat;
            }

            // Remove default collider, add sphere collider for interaction
            Destroy(go.GetComponent<MeshCollider>());
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;

            // CelestialBody for selection
            var body = go.AddComponent<CelestialBody>();
            body.objectName = obj.objectName;
            body.bodyType = CelestialBodyType.DeepSkyObject;
            body.description = obj.description;
            body.rightAscensionHours = obj.raHours;
            body.declinationDegrees = obj.decDegrees;
        }
    }
}
