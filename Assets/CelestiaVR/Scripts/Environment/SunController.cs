using System;
using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Environment
{
    /// <summary>
    /// Positions the Sun on the sky sphere using the Jean Meeus low-precision solar
    /// position algorithm. Also manages:
    ///   • A directional light that tracks the sun's world-space direction.
    ///   • A dotted LineRenderer tracing today's solar arc (±12 h, 30-min samples).
    ///
    /// Attach to a [Sun] child of [SkyManager].
    /// The sun sphere and arc both sit in SkyManager local space and rotate with
    /// sidereal time automatically.
    /// </summary>
    public class SunController : MonoBehaviour
    {
        [Header("Visual — Sun Sphere")]
        [Tooltip("Radius of the sun disc (Unity units). At sky radius 500, ~2.5 gives roughly 0.5° angular size.")]
        [Range(0.5f, 10f)]
        public float sunRadius = 2.5f;
        [Tooltip("Outer glow radius multiplier around the core disc.")]
        [Range(1f, 8f)]
        public float glowMultiplier = 4f;

        [Header("Directional Light")]
        [Tooltip("Assign the scene's main directional light here (or leave null to auto-create one).")]
        public Light sunLight;
        [Tooltip("Maximum intensity of the directional light at solar noon.")]
        [Range(0f, 5f)]
        public float maxLightIntensity = 1.2f;

        [Header("Solar Arc")]
        [Tooltip("Draw the dotted arc of today's sun path across the sky.")]
        public bool showSolarArc = true;
        [Tooltip("How many hours each side of now to trace (24 = full day arc).")]
        [Range(6f, 24f)]
        public float arcHalfSpanHours = 12f;
        [Tooltip("Samples along the arc. More = smoother.")]
        [Range(12, 96)]
        public int arcSamples = 48;
        [Tooltip("Width of the arc line in Unity units.")]
        [Range(0.05f, 1f)]
        public float arcWidth = 0.15f;
        [Tooltip("Color of the arc. Alpha controls transparency.")]
        public Color arcColor = new Color(1f, 0.85f, 0.3f, 0.55f);

        // ── Private ───────────────────────────────────────────────────────────────

        private SkyManager    _sky;
        private GameObject    _sunObject;
        private LineRenderer  _arcLine;

        // Throttle: only rebuild arc when sim-time shifts by ≥ this many minutes.
        private double _lastArcUpdateJD = double.MinValue;
        private const double ArcRebuildIntervalDays = 1.0 / 1440.0 * 5.0; // every 5 sim-minutes

        // ── Public API ────────────────────────────────────────────────────────────

        public void Initialize(SkyManager sky)
        {
            _sky = sky;
            BuildSunVisuals();
            BuildArcRenderer();

            if (sunLight == null)
                sunLight = CreateDirectionalLight();

            UpdateSunPosition(sky.SimulatedTime);
        }

        /// <summary>Called by SkyManager every simulated-time tick.</summary>
        public void UpdateSunPosition(DateTime simTime)
        {
            if (_sky == null || _sunObject == null) return;

            // Guard: if simTime is too close to DateTime.MinValue the arc subtraction will
            // underflow. Clamp to a safe minimum (year 100 gives plenty of margin).
            if (simTime.Year < 100)
            {
                Debug.LogWarning("[SunController] simTime is near DateTime.MinValue — clamping to UtcNow.");
                simTime = DateTime.UtcNow;
            }

            var (ra, dec) = CelestialCoordinates.ComputeSunRADec(simTime);

            // Place the sun in SkyManager local space (sidereal rotation moves it correctly).
            Vector3 localPos = CelestialCoordinates.RADecToUnity(ra, dec, _sky.skyRadius - 2f);

            // Guard against NaN/Inf position (bad RA/Dec would corrupt the transform).
            if (!float.IsFinite(localPos.x) || !float.IsFinite(localPos.y) || !float.IsFinite(localPos.z))
                return;

            _sunObject.transform.localPosition = localPos;
            _sunObject.transform.localRotation = Quaternion.LookRotation(-localPos.normalized);

            // Point the directional light from the sun toward origin in world space.
            if (sunLight != null)
            {
                Vector3 sunWorldPos = _sunObject.transform.position;
                sunLight.transform.rotation = Quaternion.LookRotation(
                    (Vector3.zero - sunWorldPos).normalized);

                // Intensity: ramp up from 0 at horizon to maxLightIntensity above 10°.
                float alt = _sky.GetSunAltitudeDegrees();
                sunLight.intensity = Mathf.Clamp01(alt / 10f) * maxLightIntensity;

                // Hide the sphere when below horizon.
                _sunObject.SetActive(alt > -5f);
            }

            // Throttled arc rebuild.
            double jdNow = CelestialCoordinates.DateTimeToJulianDate(simTime);
            if (showSolarArc && Math.Abs(jdNow - _lastArcUpdateJD) >= ArcRebuildIntervalDays)
            {
                RebuildArc(simTime);
                _lastArcUpdateJD = jdNow;
            }
        }

        // ── Build helpers ─────────────────────────────────────────────────────────

        private void BuildSunVisuals()
        {
            _sunObject = new GameObject("SunDisc");
            _sunObject.transform.SetParent(transform, false);

            // Core disc.
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            core.transform.SetParent(_sunObject.transform, false);
            core.transform.localScale = Vector3.one * (sunRadius * 2f);
            ApplyAdditiveMat(core.GetComponent<Renderer>(),
                new Color(1f, 0.97f, 0.85f, 1f), renderQueue: 2997);
            Destroy(core.GetComponent<Collider>());

            // Inner soft glow.
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Glow";
            glow.transform.SetParent(_sunObject.transform, false);
            glow.transform.localScale = Vector3.one * (sunRadius * 2f * glowMultiplier);
            ApplyAdditiveMat(glow.GetComponent<Renderer>(),
                new Color(1f, 0.88f, 0.5f, 0.12f), renderQueue: 2996);
            Destroy(glow.GetComponent<Collider>());

            // CelestialBody for selection.
            var body = _sunObject.AddComponent<CelestialBody>();
            body.objectName        = "Sun";
            body.bodyType          = CelestialBodyType.Star;
            body.magnitude         = -26.74f;
            body.physicalRadiusKm  = 695_700f;
            body.temperatureK      = 5_778f;
            body.spectralType      = "G2V";
            body.distanceLightYears = 0.0000158f; // 8.3 light-minutes
            body.description       = "Our star, a G2V yellow dwarf. "
                                   + "Surface temperature ~5,778 K, 696,000 km radius — "
                                   + "109 times wider than Earth. "
                                   + "The source of all light and life on Earth.";

            // Generous collider for gaze selection.
            var col = _sunObject.AddComponent<SphereCollider>();
            col.radius = sunRadius * glowMultiplier;
        }

        private void BuildArcRenderer()
        {
            if (!showSolarArc) return;

            var arcGO = new GameObject("SolarArc");
            arcGO.transform.SetParent(transform, false);

            _arcLine = arcGO.AddComponent<LineRenderer>();
            _arcLine.useWorldSpace    = false; // local to SkyManager
            _arcLine.loop             = false;
            _arcLine.widthMultiplier  = arcWidth;
            _arcLine.positionCount    = arcSamples + 1;
            _arcLine.textureMode      = LineTextureMode.Tile;
            _arcLine.alignment        = LineAlignment.View;
            _arcLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _arcLine.receiveShadows   = false;

            // Build a simple dashed texture (4px on, 4px transparent) in code.
            _arcLine.material = BuildDashMaterial();
        }

        private Material BuildDashMaterial()
        {
            // 8×1 texture: left half white, right half clear = dash pattern when tiled.
            var tex = new Texture2D(8, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Repeat;
            for (int x = 0; x < 8; x++)
                tex.SetPixel(x, 0, x < 4
                    ? new Color(arcColor.r, arcColor.g, arcColor.b, arcColor.a)
                    : Color.clear);
            tex.Apply();

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            var mat = new Material(sh) { name = "SolarArcMat", mainTexture = tex };
            mat.SetFloat("_Surface",  1f);
            mat.SetFloat("_Blend",    0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite",   0f);
            mat.renderQueue = 3002;
            return mat;
        }

        private void RebuildArc(DateTime centerTime)
        {
            if (_arcLine == null) return;

            double totalSeconds = arcHalfSpanHours * 3600.0;
            double stepSeconds  = totalSeconds * 2.0 / arcSamples;

            DateTime start;
            try { start = centerTime.AddSeconds(-totalSeconds); }
            catch { return; } // DateTime out of range — skip this rebuild

            var positions = new Vector3[arcSamples + 1];
            for (int i = 0; i <= arcSamples; i++)
            {
                DateTime t;
                try { t = start.AddSeconds(i * stepSeconds); }
                catch { t = centerTime; } // fallback to center if step overflows

                var (ra, dec) = CelestialCoordinates.ComputeSunRADec(t);
                Vector3 p     = CelestialCoordinates.RADecToUnity(ra, dec, _sky.skyRadius - 4f);
                // Replace NaN with center position so the line stays drawable
                positions[i]  = float.IsFinite(p.x) ? p : _sunObject.transform.localPosition;
            }

            _arcLine.positionCount = arcSamples + 1;
            _arcLine.SetPositions(positions);
        }

        private Light CreateDirectionalLight()
        {
            var go = new GameObject("SunDirectionalLight");
            go.transform.SetParent(_sky.transform, false);
            var l = go.AddComponent<Light>();
            l.type      = LightType.Directional;
            l.intensity = 0f; // starts at night
            l.color     = new Color(1f, 0.96f, 0.88f);
            l.shadows   = LightShadows.Soft;
            return l;
        }

        private static void ApplyAdditiveMat(Renderer r, Color color, int renderQueue)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var mat = new Material(sh) { name = "SunMat" };
            mat.color = color;
            mat.SetFloat("_Surface",  1f);
            mat.SetFloat("_Blend",    0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite",   0f);
            mat.renderQueue = renderQueue;
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            r.material           = mat;
            r.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows     = false;
        }
    }
}
