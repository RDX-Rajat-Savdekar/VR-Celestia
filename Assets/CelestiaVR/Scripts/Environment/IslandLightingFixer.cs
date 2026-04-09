using UnityEngine;
using UnityEngine.Rendering;

namespace CelestiaVR.Environment
{
    /// <summary>
    /// Makes the island respond to the day/night ambient light cycle.
    ///
    /// GLB-imported materials use a custom shader that ignores lighting.
    /// This script swaps the shader to URP Lit at runtime, preserving
    /// all existing texture/color properties on the material instance.
    ///
    /// Attach to any persistent GameObject. Drag the island root into islandRoot.
    /// </summary>
    public class IslandLightingFixer : MonoBehaviour
    {
        [Tooltip("Root GameObject of the island (the tropical_island prefab instance).")]
        public GameObject islandRoot;

        [Tooltip("Smoothness for the replaced materials (0 = matte, 1 = mirror).")]
        [Range(0f, 1f)]
        public float smoothness = 0.25f;

        private void Start()
        {
            if (islandRoot == null)
            {
                Debug.LogWarning("[IslandLightingFixer] islandRoot not assigned.");
                return;
            }

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                Debug.LogWarning("[IslandLightingFixer] URP Lit shader not found.");
                return;
            }

            int swapped = 0;
            foreach (var r in islandRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                // r.materials returns per-renderer instances we can freely mutate.
                var mats = r.materials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    string shaderName = mat.shader != null ? mat.shader.name : "";

                    // Already a Lit shader — just tweak smoothness/metallic and move on.
                    if (shaderName == "Universal Render Pipeline/Lit")
                    {
                        mat.SetFloat("_Metallic",   0f);
                        mat.SetFloat("_Smoothness", smoothness);
                        changed = true;
                        continue;
                    }

                    // ── Snapshot every texture before the shader swap ──────────────
                    // We can't know the GLB shader's property names ahead of time, so
                    // we snapshot the Texture objects using shader reflection.
                    var textures = SnapshotTextures(mat);
                    Color baseColor = Color.white;
                    if (mat.HasProperty("_BaseColor"))       baseColor = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color"))      baseColor = mat.GetColor("_Color");
                    else if (mat.HasProperty("baseColorFactor")) baseColor = mat.GetColor("baseColorFactor");

                    // ── Swap shader (keeps material instance, clears unknown props) ─
                    mat.shader = litShader;
                    mat.SetFloat("_Metallic",   0f);
                    mat.SetFloat("_Smoothness", smoothness);
                    mat.SetColor("_BaseColor",  baseColor);

                    // ── Re-apply textures to URP Lit property names ────────────────
                    // Try common GLB / GLTF property names → URP Lit equivalents.
                    RestoreTexture(mat, textures, "_BaseMap",
                        "_MainTex", "baseColorTexture", "_baseColorTexture",
                        "_Albedo", "_Diffuse");

                    RestoreTexture(mat, textures, "_BumpMap",
                        "_NormalMap", "normalTexture", "_normalTexture",
                        "_Normal");

                    RestoreTexture(mat, textures, "_MetallicGlossMap",
                        "metallicRoughnessTexture", "_metallicRoughnessTexture");

                    RestoreTexture(mat, textures, "_OcclusionMap",
                        "occlusionTexture", "_occlusionTexture", "_AO");

                    RestoreTexture(mat, textures, "_EmissionMap",
                        "emissiveTexture", "_emissiveTexture", "_Emission");

                    changed = true;
                    swapped++;
                }

                if (changed) r.materials = mats;
            }

            Debug.Log($"[IslandLightingFixer] Shader-swapped {swapped} material(s) to URP Lit.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Returns a flat array of (propertyName, Texture) for all texture properties.</summary>
        private static (string name, Texture tex)[] SnapshotTextures(Material mat)
        {
            var shader = mat.shader;
            int count  = shader.GetPropertyCount();
            var list   = new System.Collections.Generic.List<(string, Texture)>();

            for (int p = 0; p < count; p++)
            {
                if (shader.GetPropertyType(p) != ShaderPropertyType.Texture) continue;
                string propName = shader.GetPropertyName(p);
                var    tex      = mat.GetTexture(propName);
                if (tex != null) list.Add((propName, tex));
            }
            return list.ToArray();
        }

        /// <summary>
        /// Sets <paramref name="dstProp"/> on <paramref name="mat"/> to the first texture
        /// found under any of <paramref name="srcNames"/> in the snapshot.
        /// </summary>
        private static void RestoreTexture(Material mat, (string name, Texture tex)[] snapshot,
            string dstProp, params string[] srcNames)
        {
            if (!mat.HasProperty(dstProp)) return;

            foreach (var srcName in srcNames)
            {
                foreach (var (name, tex) in snapshot)
                {
                    if (string.Equals(name, srcName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        mat.SetTexture(dstProp, tex);
                        return;
                    }
                }
            }
        }
    }
}
