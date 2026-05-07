using UnityEngine;
using UnityEditor;

/// <summary>
/// One-shot tool: patches every material in Assets/Vefects to use URP-compatible shaders.
///
/// The "Free Fire VFX" package ships CGPROGRAM surface shaders (Built-in RP).
/// All of them show pink in a URP project and the heat-haze shader additionally
/// throws "GrabPass can't be called from a job thread" at runtime.
///
/// Run via:  Tools > Fix Vefects Fire Shaders for URP
/// Safe to run multiple times — already-URP materials are skipped.
/// </summary>
public static class VefectsURPFixer
{
    [MenuItem("Tools/Fix Vefects Fire Shaders for URP")]
    public static void Fix()
    {
        var particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleUnlit == null)
        {
            Debug.LogError("[VefectsURPFixer] 'Universal Render Pipeline/Particles/Unlit' not found. " +
                           "Make sure the URP package is installed.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Vefects" });
        int fixedCount = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Skip materials already on a URP shader
            if (mat.shader != null && mat.shader.name.StartsWith("Universal Render Pipeline/"))
                continue;

            // Snapshot properties before the shader swap clears them
            Color   baseColor = Color.white;
            Texture mainTex   = null;

            if      (mat.HasProperty("_TintColor")) baseColor = mat.GetColor("_TintColor");
            else if (mat.HasProperty("_Color"))     baseColor = mat.GetColor("_Color");
            else if (mat.HasProperty("_BaseColor")) baseColor = mat.GetColor("_BaseColor");

            if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");

            // Heat-haze materials use GrabPass (screen distortion) — not possible in URP.
            // Make them nearly invisible so they don't cause errors or show pink.
            bool isHeatHaze = path.ToLower().Contains("heat_haze") || path.ToLower().Contains("heathaze");
            if (isHeatHaze)
                baseColor.a = 0.04f;

            mat.shader = particleUnlit;

            // Restore surface mode: transparent + additive-friendly
            mat.SetFloat("_Surface",  1f);  // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend",    0f);  // 0 = Alpha, 1 = Premultiply, 2 = Additive
            mat.SetFloat("_ZWrite",   0f);
            mat.SetColor("_BaseColor", baseColor);

            if (mainTex != null)
                mat.SetTexture("_BaseMap", mainTex);

            EditorUtility.SetDirty(mat);
            fixedCount++;
            Debug.Log($"[VefectsURPFixer] Fixed: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[VefectsURPFixer] Done — {fixedCount} material(s) patched.");
    }
}
