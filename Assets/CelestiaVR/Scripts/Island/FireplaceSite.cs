using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CelestiaVR.Audio;
using System;

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
        [Tooltip("Smoke/fire VFX prefab to instantiate when the fire is lit (e.g. VFX_Fire_Floor_01_Smoke).")]
        public GameObject smokePrefab;

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

        [Header("Gameplay")]
        [Tooltip("How many wood logs must be deposited before the site can be lit.")]
        public int requiredLogs = 3;
        private int _sticksPlaced = 0;
        private readonly List<StickCollectible> _sticks      = new();
        private readonly List<Vector3>          _stickOrigins = new();

        // Visuals
        private GameObject       _markerRing;
        private MeshRenderer     _ringRenderer;
        private Material         _ringMat;

        // Fire
        private ParticleSystem[] _fireSystems;  // particles from the smoke prefab (or empty if none)
        private GameObject       _smokeVfxInstance;
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
        }

        private void Start()
        {
            // Delayed until Start so smokePrefab can be assigned by FireplaceBootstrap after AddComponent
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
            {
                _sticks.Add(stick);
                _stickOrigins.Add(stick.transform.position);
            }
        }

        public void OnStickDeposited()
        {
            _sticksPlaced++;
            CurrentState = _sticksPlaced < requiredLogs ? State.Gathering : State.Built;
            Debug.Log($"[FireplaceSite] Logs: {_sticksPlaced}/{requiredLogs}  State: {CurrentState}");

            if (CurrentState == State.Built)
                StartCoroutine(TransitionToBuilt());
            if (CurrentState == State.Built)
                StartCoroutine(AutoIgniteAfterDelay());
        }

        private IEnumerator AutoIgniteAfterDelay()
        {
            // Wait for the sticks to finish fading out (TransitionToBuilt takes 0.5s)
            yield return new WaitForSeconds(0.2f);
            
            // Automatically light the fire once the model is ready
            LightFire();
        }

        [Header("Reset")]
        [Tooltip("Seconds the fire burns before auto-resetting so logs can be collected again.")]
        public float fireDuration = 10f;

        public void LightFire()
        {
            if (CurrentState == State.Lit) return; // already burning
            CurrentState = State.Lit;
            SoundManager.Instance?.Play(SoundEvent.FireIgnite, transform.position);
            Debug.Log("[FireplaceSite] Fire lit!");

            if (_smokeVfxInstance != null)
            {
                _smokeVfxInstance.SetActive(true);
                foreach (var ps in _smokeVfxInstance.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Play();
            }
            else
            {
                foreach (var ps in _fireSystems)
                    if (ps != null) { ps.gameObject.SetActive(true); ps.Play(); }
            }

            if (_fireLight != null) _fireLight.enabled = true;
            if (_audio.clip != null) _audio.Play();

            StartCoroutine(ResetAfterDelay());
        }

        private IEnumerator ResetAfterDelay()
        {
            yield return new WaitForSeconds(fireDuration);
            ResetSite();
        }

        private void ResetSite()
        {
            Debug.Log("[FireplaceSite] Resetting site.");

            // Stop fire VFX
            if (_smokeVfxInstance != null)
            {
                foreach (var ps in _smokeVfxInstance.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _smokeVfxInstance.SetActive(false);
            }
            foreach (var ps in _fireSystems)
                if (ps != null) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.gameObject.SetActive(false); }

            // Stop light and audio
            if (_fireLight != null) _fireLight.enabled = false;
            if (_audio.isPlaying) _audio.Stop();

            // Hide fireplace model
            if (fireplaceModel != null) fireplaceModel.SetActive(false);

            // Reset state BEFORE re-enabling logs so StickCollectible.Update can't auto-deposit
            _sticksPlaced = 0;
            CurrentState  = State.Empty;

            // Respawn logs at their original positions, fully reset
            for (int i = 0; i < _sticks.Count; i++)
            {
                var s = _sticks[i];
                if (s == null) continue;
                s.gameObject.SetActive(true);
                s.ResetStick(_stickOrigins[i]);
                foreach (var r in s.GetComponentsInChildren<Renderer>())
                {
                    var c = r.material.color;
                    c.a = 1f;
                    r.material.color = c;
                }
            }

            // Show marker ring again
            if (_ringRenderer != null) _ringRenderer.enabled = true;
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

            // Flare gun is already in the scene from the start (spawned by FireplaceBootstrap).
            // Fire can now be lit by shooting the site with the flare gun.
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
            if (smokePrefab != null)
            {
                // If it's already a scene object, reparent it; otherwise instantiate from prefab asset
                bool isSceneObject = smokePrefab.scene.IsValid();
                _smokeVfxInstance = isSceneObject
                    ? smokePrefab
                    : Instantiate(smokePrefab, transform);

                _smokeVfxInstance.transform.SetParent(transform, false);
                _smokeVfxInstance.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                _smokeVfxInstance.SetActive(false);
                // Stop all child particle systems so they don't auto-play before LightFire()
                foreach (var ps in _smokeVfxInstance.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _fireSystems = Array.Empty<ParticleSystem>();
            }
            else
            {
                _fireSystems = Array.Empty<ParticleSystem>();
            }
            BuildFireLight();
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
            var litShader      = Shader.Find("Universal Render Pipeline/Lit");
            var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (litShader == null) return;

            // ── MeshRenderers ─────────────────────────────────────────────────────
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats    = r.materials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    string shaderName = mat.shader != null ? mat.shader.name : "";

                    // Already URP Lit — just tune properties, leave textures alone
                    if (shaderName == "Universal Render Pipeline/Lit")
                    {
                        mat.SetFloat("_Metallic",   0f);
                        mat.SetFloat("_Smoothness", smoothness);
                        changed = true;
                        continue;
                    }

                    // Any non-URP shader (including GrabPass shaders) → replace with URP Lit
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

            // ── ParticleSystemRenderers ───────────────────────────────────────────
            // Fire/smoke effects often use GrabPass shaders (Built-in RP only) for
            // heat-distortion.  GrabPass silently breaks in URP — replace every
            // non-URP particle material with URP Particles/Unlit.
            if (particleShader == null) return;

            foreach (var psr in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var mats    = psr.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    string sn = mat.shader != null ? mat.shader.name : "";
                    if (sn.StartsWith("Universal Render Pipeline/")) continue;

                    // Preserve base colour before swapping shader
                    Color col = Color.white;
                    if      (mat.HasProperty("_BaseColor")) col = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color"))     col = mat.GetColor("_Color");
                    else if (mat.HasProperty("_TintColor")) col = mat.GetColor("_TintColor");

                    mat.shader = particleShader;
                    mat.SetFloat("_Surface", 1f); // transparent blending
                    mat.SetColor("_BaseColor", col);
                    changed = true;
                }

                if (changed) psr.sharedMaterials = mats;
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
