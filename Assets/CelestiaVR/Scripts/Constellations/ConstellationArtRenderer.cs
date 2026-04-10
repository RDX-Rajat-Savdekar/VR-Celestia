using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Constellations
{
    /// <summary>
    /// Renders mythological constellation artwork as transparent billboard quads on the sky sphere.
    ///
    /// HOW TO GET ARTWORK:
    ///   Option A — Buy "88 Constellations Procedural Sky" (Asset Store #314028), extract the
    ///              constellation textures from the package and assign them here.
    ///   Option B — Download the free IAU/Stellarium constellation art PNGs (CC BY-SA licence).
    ///              Search "stellarium constellation art textures free download".
    ///   Option C — Commission/generate custom artwork (PNG with transparency, dark background).
    ///
    /// TEXTURE REQUIREMENTS:
    ///   • PNG with alpha channel (white art on transparent black)
    ///   • Recommended size 512×512 or 1024×1024
    ///   • Import as: Texture Type = Sprite / Default, Alpha Is Transparency = ON
    ///
    /// SCENE SETUP:
    ///   1. Add this component to the [Constellations] child of [SkyManager].
    ///   2. In the inspector, expand Entries and add one entry per constellation.
    ///   3. Set RA/Dec to the constellation's visual centre (lookup table below in code).
    ///   4. Assign your texture.
    ///   5. Adjust angularSizeDegrees (typical: 15–35° for large constellations).
    ///
    /// The quads live in SkyManager local space and rotate with sidereal time automatically.
    /// </summary>
    public class ConstellationArtRenderer : MonoBehaviour
    {
        // ── Data ──────────────────────────────────────────────────────────────────

        [System.Serializable]
        public class ConstellationArtEntry
        {
            public string  constellationName;
            public Texture2D texture;            // transparent PNG artwork

            [Tooltip("Right Ascension of the artwork centre (hours).")]
            public float centreRA;
            [Tooltip("Declination of the artwork centre (degrees).")]
            public float centreDec;
            [Tooltip("Angular diameter of the artwork on the sky in degrees.")]
            [Range(5f, 60f)]
            public float angularSizeDegrees = 20f;
            [Tooltip("Clockwise rotation of the art quad (degrees). Use to upright the figure.")]
            [Range(-180f, 180f)]
            public float rollDegrees = 0f;

            [HideInInspector] public GameObject quad; // runtime instance
        }

        [Header("Constellation Art")]
        public List<ConstellationArtEntry> entries = new();

        [Header("Visuals")]
        [Range(0f, 1f)]
        [Tooltip("Overall opacity of the art overlay. Keep low (0.15–0.35) so stars shine through.")]
        public float artOpacity = 0.22f;
        public Color artTint = new Color(0.7f, 0.8f, 1f, 1f); // cool blue-white tint

        [Header("Toggle")]
        public bool showArt = true;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private SkyManager _sky;
        private Material   _artMaterial; // shared across all quads (instanced per-entry)

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Start()
        {
            _sky = GetComponentInParent<SkyManager>();
            BuildQuads();
        }

        private void BuildQuads()
        {
            float radius = _sky != null ? _sky.skyRadius - 5f : 495f;

            foreach (var entry in entries)
            {
                if (entry.texture == null) continue;

                // Convert constellation centre to Unity position on sky sphere
                Vector3 localPos = CelestialCoordinates.RADecToUnity(entry.centreRA, entry.centreDec, radius);

                // Size: angular diameter → chord length at sky radius
                float halfAngleRad = entry.angularSizeDegrees * 0.5f * Mathf.Deg2Rad;
                float worldSize    = 2f * radius * Mathf.Tan(halfAngleRad);

                // Build quad (Unity default Quad has local size 1×1)
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Art_{entry.constellationName}";
                go.transform.SetParent(transform, false);
                Destroy(go.GetComponent<Collider>());

                // Position and orient: face inward (toward observer at origin)
                go.transform.localPosition = localPos;
                go.transform.localRotation = Quaternion.LookRotation(-localPos.normalized)
                                           * Quaternion.Euler(0, 0, entry.rollDegrees);
                go.transform.localScale    = new Vector3(worldSize, worldSize, 1f);

                // Material — additive alpha so art doesn't block stars
                var mat = BuildArtMaterial(entry.texture);
                go.GetComponent<Renderer>().sharedMaterial = mat;

                go.SetActive(showArt);
                entry.quad = go;
            }
        }

        private Material BuildArtMaterial(Texture2D tex)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(sh) { name = "ConstellationArtMat", mainTexture = tex };
            mat.color = new Color(artTint.r, artTint.g, artTint.b, artOpacity);
            mat.SetFloat("_Surface",  1f);
            mat.SetFloat("_Blend",    0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); // additive → stars shine through
            mat.SetFloat("_ZWrite",   0f);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            return mat;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void SetVisible(bool visible)
        {
            showArt = visible;
            foreach (var e in entries)
                if (e.quad != null) e.quad.SetActive(visible);
        }

        public void Toggle() => SetVisible(!showArt);

        // ── IAU Centre Lookup Table ───────────────────────────────────────────────
        // Use this as reference when populating the Entries list in the Inspector.
        // RA in hours, Dec in degrees (J2000).
        //
        // Constellation   RA      Dec
        // Orion          5.58    +5.0
        // Ursa Major    10.67   +56.0
        // Ursa Minor    15.00   +75.0
        // Cassiopeia     1.00   +60.0
        // Cygnus        20.62   +44.0
        // Leo           10.67   +15.0
        // Scorpius      16.88  -28.0
        // Sagittarius   19.08  -28.5
        // Gemini         7.07   +22.0
        // Taurus         4.70   +15.0
        // Lyra          18.85   +36.0
        // Aquila        19.67   ++3.0
        // Perseus        3.17   +43.0
        // Boötes        14.70   +31.0
        // Virgo         13.40   -4.0
        // Crux          12.45  -60.0
        // Centaurus     13.08  -47.0
        // Hercules      17.38   +27.0
        // Pegasus       22.70   +19.0
        // Andromeda      0.83   +37.0
    }
}
