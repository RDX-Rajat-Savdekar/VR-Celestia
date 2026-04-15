using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CelestiaVR.Audio;

namespace CelestiaVR.Island
{
    /// <summary>
    /// State machine for the fireplace mini-game site.
    ///
    /// States:
    ///   Empty      — glowing ring marks the spot; no sticks yet
    ///   Gathering  — 1–3 sticks dropped; ring pulses faster
    ///   Built      — all 4 sticks placed; fireplace model visible; flare gun spawned
    ///   Lit        — flare has hit; fire particles + light active; crackling audio
    ///
    /// FireplaceBootstrap creates this component at runtime and wires all references.
    /// </summary>
    public class FireplaceSite : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("References (set by FireplaceBootstrap)")]
        public GameObject fireplaceModel;   // small_fire_place GLB instance, inactive until Built

        [Header("Site Zone")]
        [Tooltip("Radius within which a released stick is accepted.")]
        public float snapRadius = 1.0f;

        [Header("Appearance")]
        public Color  markerColor        = new Color(1f, 0.75f, 0.2f, 1f);
        [Range(0f, 5f)]
        public float  markerGlowMax      = 2.5f;

        [Header("Fire Light")]
        public float  fireLightRange     = 5f;
        public float  fireLightIntensity = 2.5f;
        public Color  fireLightColor     = new Color(1f, 0.55f, 0.15f);

        // ── State ─────────────────────────────────────────────────────────────────

        public enum State { Empty, Gathering, Built, Lit }
        public State CurrentState { get; private set; } = State.Empty;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private const int RequiredSticks = 4;
        private int _sticksPlaced = 0;
        private readonly List<StickCollectible> _sticks = new();

        // Visuals
        private GameObject       _markerRing;
        private MeshRenderer     _ringRenderer;
        private Material         _ringMat;

        // Fire
        private ParticleSystem[] _fireSystems;  // Flames, Sparks, Embers
        private Light            _fireLight;

        // Audio
        private AudioSource _audio;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Note: FlareProjectile detects this site via GetComponent<FireplaceSite>(), not by tag.

            // Solid box collider for flare projectile physics collision
            var box    = gameObject.AddComponent<BoxCollider>();
            box.size   = new Vector3(0.8f, 0.6f, 0.8f);
            box.center = new Vector3(0f, 0.3f, 0f);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.loop         = true;
            _audio.playOnAwake  = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance  = 1f;
            _audio.maxDistance  = 8f;
            _audio.rolloffMode  = AudioRolloffMode.Logarithmic;
            _audio.volume       = 0.7f;

            BuildMarkerRing();
            BuildFireParticles();
        }

        private void Update()
        {
            PulseMarkerRing();
            FlickerFireLight();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void RegisterStick(StickCollectible stick)
        {
            if (!_sticks.Contains(stick))
                _sticks.Add(stick);
        }

        public void OnStickDeposited()
        {
            _sticksPlaced++;
            CurrentState = _sticksPlaced < RequiredSticks ? State.Gathering : State.Built;
            Debug.Log($"[FireplaceSite] Sticks: {_sticksPlaced}/{RequiredSticks}  State: {CurrentState}");

            if (CurrentState == State.Built)
                StartCoroutine(TransitionToBuilt());
        }

        public void LightFire()
        {
            if (CurrentState != State.Built) return;
            CurrentState = State.Lit;
            SoundManager.Instance?.Play(SoundEvent.FireIgnite, transform.position);
            Debug.Log("[FireplaceSite] Fire lit!");

            foreach (var ps in _fireSystems)
                if (ps != null) { ps.gameObject.SetActive(true); ps.Play(); }

            if (_fireLight != null) _fireLight.enabled = true;
            if (_audio.clip != null) _audio.Play();
        }

        // ── Transition ────────────────────────────────────────────────────────────

        private IEnumerator TransitionToBuilt()
        {
            // 1. Fade out sticks
            float t = 0f;
            var renderers = new List<(Renderer r, Color baseCol)>();
            foreach (var s in _sticks)
            {
                if (s == null) continue;
                foreach (var r in s.GetComponentsInChildren<Renderer>())
                    renderers.Add((r, r.material.color));
            }

            while (t < 1f)
            {
                t += Time.deltaTime / 0.5f;
                foreach (var (r, col) in renderers)
                {
                    if (r == null) continue;
                    var c = col;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    r.material.color = c;
                }
                yield return null;
            }

            // 2. Disable sticks
            foreach (var s in _sticks)
                if (s != null) s.gameObject.SetActive(false);

            // 3. Show fireplace model — destroy any Rigidbodies so it doesn't fall
            //    (island GLB has no physics mesh; gravity would send it to the ocean floor)
            if (fireplaceModel != null)
            {
                foreach (var rb in fireplaceModel.GetComponentsInChildren<Rigidbody>())
                    Destroy(rb);
                fireplaceModel.SetActive(true);
            }

            // 4. Hide ring
            if (_ringRenderer != null) _ringRenderer.enabled = false;

            // 5. Spawn flare gun
            FireplaceBootstrap.Instance?.SpawnFlareGun();
        }

        // ── Visuals ───────────────────────────────────────────────────────────────

        private void PulseMarkerRing()
        {
            if (_ringMat == null || CurrentState >= State.Built) return;
            float speed   = CurrentState == State.Empty ? 1.5f : 3f;
            float pulse   = Mathf.PingPong(Time.time * speed, 1f);
            float glow    = Mathf.Lerp(0.5f, markerGlowMax, pulse);
            _ringMat.SetColor("_EmissionColor", markerColor * glow);
        }

        private void FlickerFireLight()
        {
            if (_fireLight == null || !_fireLight.enabled) return;
            _fireLight.intensity = fireLightIntensity
                + (Mathf.PerlinNoise(Time.time * 8f, 0f) - 0.5f) * 0.8f;
        }

        private void BuildMarkerRing()
        {
            _markerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _markerRing.name = "MarkerRing";
            _markerRing.transform.SetParent(transform, false);
            _markerRing.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            _markerRing.transform.localScale    = new Vector3(snapRadius * 2f, 0.01f, snapRadius * 2f);
            Destroy(_markerRing.GetComponent<CapsuleCollider>());

            _ringRenderer = _markerRing.GetComponent<MeshRenderer>();
            _ringMat      = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _ringMat.EnableKeyword("_EMISSION");
            _ringMat.SetColor("_BaseColor",     new Color(markerColor.r, markerColor.g, markerColor.b, 0.6f));
            _ringMat.SetColor("_EmissionColor", markerColor * markerGlowMax);
            _ringMat.SetFloat("_Surface", 1f);  // transparent
            _ringRenderer.material = _ringMat;
        }

        private void BuildFireParticles()
        {
            _fireSystems = new ParticleSystem[3];
            _fireSystems[0] = BuildFlames();
            _fireSystems[1] = BuildSparks();
            _fireSystems[2] = BuildEmbers();
            BuildFireLight();
        }

        private ParticleSystem BuildFlames()
        {
            var go = new GameObject("PS_Flames");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            go.SetActive(false);

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop            = true;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.20f);
            main.startColor      = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.3f, 0f, 0.9f), new Color(1f, 0.75f, 0.1f, 0.7f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 80;

            var em = ps.emission;
            em.rateOverTime = 25f;

            var sh = ps.shape;
            sh.enabled    = true;
            sh.shapeType  = ParticleSystemShapeType.Cone;
            sh.angle      = 15f;
            sh.radius     = 0.08f;

            var sol  = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var col    = ps.colorOverLifetime;
            col.enabled = true;
            var grad    = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.2f, 0f), 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(0.9f, 0.9f, 0.5f), 1f) },
                new[] {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.7f, 0.5f),
                    new GradientAlphaKey(0f,   1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetColor("_BaseColor", Color.white);
            rend.material = mat;

            return ps;
        }

        private ParticleSystem BuildSparks()
        {
            var go = new GameObject("PS_Sparks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            go.SetActive(false);

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop            = true;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
            main.startColor      = new Color(1f, 0.85f, 0.3f, 1f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 40;

            var em = ps.emission;
            em.rateOverTime = 8f;

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 25f;
            sh.radius    = 0.05f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.3f, 1f));
            rend.material = mat;

            return ps;
        }

        private ParticleSystem BuildEmbers()
        {
            var go = new GameObject("PS_Embers");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            go.SetActive(false);

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop            = true;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
            main.startColor      = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.05f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 30;

            var em = ps.emission;
            em.rateOverTime = 4f;

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 20f;
            sh.radius    = 0.06f;

            var noise = ps.noise;
            noise.enabled      = true;
            noise.strength     = new ParticleSystem.MinMaxCurve(0.3f);
            noise.frequency    = 0.5f;
            noise.scrollSpeed  = new ParticleSystem.MinMaxCurve(0.1f);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetColor("_BaseColor", new Color(0.3f, 0.3f, 0.3f, 0.4f));
            rend.material = mat;

            return ps;
        }

        private void BuildFireLight()
        {
            var go = new GameObject("FireLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            _fireLight           = go.AddComponent<Light>();
            _fireLight.type      = LightType.Point;
            _fireLight.color     = fireLightColor;
            _fireLight.intensity = fireLightIntensity;
            _fireLight.range     = fireLightRange;
            _fireLight.enabled   = false;
        }

        // ── GLB Shader Fix (same logic as IslandLightingFixer) ────────────────────

        internal static void FixGLBShaders(GameObject root, float smoothness)
        {
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) return;

            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats    = r.materials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    string shaderName = mat.shader != null ? mat.shader.name : "";
                    if (shaderName == "Universal Render Pipeline/Lit")
                    {
                        mat.SetFloat("_Metallic",   0f);
                        mat.SetFloat("_Smoothness", smoothness);
                        changed = true;
                        continue;
                    }

                    var textures  = SnapshotTextures(mat);
                    Color baseCol = Color.white;
                    if      (mat.HasProperty("_BaseColor"))       baseCol = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color"))           baseCol = mat.GetColor("_Color");
                    else if (mat.HasProperty("baseColorFactor"))  baseCol = mat.GetColor("baseColorFactor");

                    mat.shader = litShader;
                    mat.SetFloat("_Metallic",   0f);
                    mat.SetFloat("_Smoothness", smoothness);
                    mat.SetColor("_BaseColor",  baseCol);

                    RestoreTexture(mat, textures, "_BaseMap",
                        "_MainTex", "baseColorTexture", "_baseColorTexture", "_Albedo", "_Diffuse");
                    RestoreTexture(mat, textures, "_BumpMap",
                        "_NormalMap", "normalTexture", "_normalTexture", "_Normal");
                    RestoreTexture(mat, textures, "_MetallicGlossMap",
                        "metallicRoughnessTexture", "_metallicRoughnessTexture");
                    RestoreTexture(mat, textures, "_OcclusionMap",
                        "occlusionTexture", "_occlusionTexture", "_AO");

                    changed = true;
                }

                if (changed) r.materials = mats;
            }
        }

        private static (string name, Texture tex)[] SnapshotTextures(Material mat)
        {
            var shader = mat.shader;
            int count  = shader.GetPropertyCount();
            var list   = new List<(string, Texture)>();
            for (int p = 0; p < count; p++)
            {
                if (shader.GetPropertyType(p) != ShaderPropertyType.Texture) continue;
                string prop = shader.GetPropertyName(p);
                var    tex  = mat.GetTexture(prop);
                if (tex != null) list.Add((prop, tex));
            }
            return list.ToArray();
        }

        private static void RestoreTexture(Material mat, (string name, Texture tex)[] snapshot,
            string dstProp, params string[] srcNames)
        {
            if (!mat.HasProperty(dstProp)) return;
            foreach (var src in srcNames)
                foreach (var (name, tex) in snapshot)
                    if (string.Equals(name, src, System.StringComparison.OrdinalIgnoreCase))
                    { mat.SetTexture(dstProp, tex); return; }
        }
    }
}
