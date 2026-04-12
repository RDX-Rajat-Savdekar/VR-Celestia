using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Runtime spawner for the fireplace mini-game.
    ///
    /// Instantiates and wires:
    ///   • FireplaceSite  — glowing ground marker + state machine
    ///   • 4× StickCollectible — grabbable sticks scattered around the island
    ///   • FlareGun (deferred) — appears once all 4 sticks are placed
    ///
    /// All positions are expressed in island-local space and transformed by
    /// islandAnchor.TransformPoint() so they follow the island's rotation/scale.
    ///
    /// GLB references are auto-loaded via AssetDatabase in the editor. On device,
    /// the inspector-assigned references (populated by OnValidate) are used.
    ///
    /// Added to the scene by StargazingSceneBootstrap.EnsureFireplaceMiniGame().
    /// </summary>
    public class FireplaceBootstrap : MonoBehaviour
    {
        public static FireplaceBootstrap Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Island Anchor")]
        [Tooltip("Root transform of the island model. All offsets are in island-local space.")]
        public Transform islandAnchor;

        [Header("Prefabs (auto-loaded in editor via OnValidate)")]
        public GameObject fireplaceGlb;
        public GameObject stickGlb;
        public GameObject flareGunGlb;

        [Header("Positions (world-space — leave islandAnchor empty to use directly)")]
        public Vector3 fireplaceOffset = new Vector3(4.11f, 0.144f, -0.16f);
        public Vector3 flareGunOffset  = new Vector3(0.8f,  0.6f,   0.3f);

        // Sticks scattered around the fireplace in world space (near To_Place_Fireplace).
        // These are overridden at runtime if To_Place_Fireplace is found.
        // Y = 0.5 keeps them above ground until you tune per-scene.
        public Vector3[] stickOffsets = new Vector3[]
        {
            new Vector3(-24.0f, 0.5f, -27.0f),
            new Vector3(-29.5f, 0.5f, -26.5f),
            new Vector3(-25.5f, 0.5f, -30.5f),
            new Vector3(-30.0f, 0.5f, -29.0f),
        };

        // Stick pile offsets so deposited sticks don't all overlap at the site centre
        private static readonly Vector3[] SnapOffsets = new Vector3[]
        {
            new Vector3(-0.1f, 0.05f,  0.05f),
            new Vector3( 0.1f, 0.05f, -0.05f),
            new Vector3(-0.05f, 0.1f, -0.1f),
            new Vector3( 0.05f, 0.1f,  0.1f),
        };

        // ── Runtime ───────────────────────────────────────────────────────────────

        private FireplaceSite              _site;
        private readonly List<StickCollectible> _sticks = new();
        private GameObject                 _flarePrototype;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryLoadGLBsInEditor();
        }

        private void TryLoadGLBsInEditor()
        {
            const string root = "Assets/CelestiaVR_resources/FirePlace_Env/Prefabs_FireENv/";
            if (fireplaceGlb == null)
                fireplaceGlb = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(root + "small_fire_place.prefab");
            if (stickGlb == null)
                stickGlb     = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(root + "lowpoly_stick_-_01.prefab");
            if (flareGunGlb == null)
                flareGunGlb  = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(root + "flare_gun.prefab");
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

#if UNITY_EDITOR
            TryLoadGLBsInEditor();
#endif
        }

        private void Start()
        {
            // Idempotent — StargazingSceneBootstrap already guards before creating us,
            // but double-check in case Bootstrap is re-run or manually placed.
            if (FindFirstObjectByType<FireplaceSite>() != null) return;

            if (!ValidateGLBs()) return;

            // If the designer placed a "To_Place_Fireplace" empty GO in the scene,
            // use its world position as the authoritative fireplace location.
            var marker = GameObject.Find("To_Place_Fireplace");
            if (marker != null)
            {
                fireplaceOffset = marker.transform.position;
                islandAnchor    = null; // positions are already world-space
                Debug.Log($"[FireplaceBootstrap] Using To_Place_Fireplace position: {fireplaceOffset}");
            }

            SpawnSite();
            SpawnSticks();
        }

        // ── Spawners ──────────────────────────────────────────────────────────────

        private void SpawnSite()
        {
            var siteGO = new GameObject("[FireplaceSite]");

            // Parent under To_Place_Fireplace if it exists, for clean hierarchy
            var marker = GameObject.Find("To_Place_Fireplace");
            if (marker != null)
                siteGO.transform.SetParent(marker.transform, false); // worldPositionStays = false → localPos zero

            siteGO.transform.position = AnchorPos(fireplaceOffset);

            _site = siteGO.AddComponent<FireplaceSite>();

            // Instantiate fireplace model as inactive child (enabled by state machine)
            if (fireplaceGlb != null)
            {
                var fp = Instantiate(fireplaceGlb, siteGO.transform);
                fp.SetActive(false);
                _site.fireplaceModel = fp;
            }

            Debug.Log("[FireplaceBootstrap] Site spawned at " + siteGO.transform.position);
        }

        private void SpawnSticks()
        {
            if (stickGlb == null) return;

            for (int i = 0; i < stickOffsets.Length; i++)
            {
                var stickGO = Instantiate(stickGlb);
                stickGO.name = $"[Stick_{i}]";
                stickGO.transform.position = AnchorPos(stickOffsets[i]);
                stickGO.transform.rotation = Quaternion.Euler(
                    Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-5f, 5f));

                // Prefab already has correct materials — no shader fix needed

                // Physics — no gravity so sticks stay at their spawn position.
                // The island GLB has no physics mesh, so gravity would send them to the ocean floor.
                var rb              = stickGO.AddComponent<Rigidbody>();
                rb.mass             = 0.3f;
                rb.linearDamping    = 0.5f;
                rb.angularDamping   = 1f;
                rb.useGravity       = false;

                // Collider — capsule oriented along Z (length of the stick)
                var col             = stickGO.AddComponent<CapsuleCollider>();
                col.height          = 0.4f;
                col.radius          = 0.025f;
                col.direction       = 2; // Z

                // XR Grab
                var grab            = stickGO.AddComponent<XRGrabInteractable>();
                grab.movementType   = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach  = true;
                grab.useDynamicAttach = true;

                // Collectible
                var stick           = stickGO.AddComponent<StickCollectible>();
                stick.targetSite    = _site;
                stick.snapOffset    = SnapOffsets[i % SnapOffsets.Length];

                _sticks.Add(stick);
            }

            Debug.Log($"[FireplaceBootstrap] {_sticks.Count} sticks spawned.");
        }

        /// <summary>Called by FireplaceSite.TransitionToBuilt() once all sticks are placed.</summary>
        public void SpawnFlareGun()
        {
            if (flareGunGlb == null) return;

            var gunGO             = Instantiate(flareGunGlb);
            gunGO.name            = "[FlareGun]";
            gunGO.transform.position = _site != null
                ? _site.transform.position + flareGunOffset
                : AnchorPos(fireplaceOffset + flareGunOffset);

            // Prefab already has correct materials — no shader fix needed

            // Physics
            var rb                = gunGO.AddComponent<Rigidbody>();
            rb.mass               = 0.6f;
            rb.linearDamping      = 0.4f;
            rb.angularDamping     = 0.8f;

            // Collider
            var col               = gunGO.AddComponent<BoxCollider>();
            col.size              = new Vector3(0.06f, 0.12f, 0.22f);
            col.center            = new Vector3(0f, 0.04f, 0.03f);

            // XR Grab
            var grab              = gunGO.AddComponent<XRGrabInteractable>();
            grab.movementType     = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach    = true;
            grab.useDynamicAttach = true;

            // Muzzle point
            var muzzleGO          = new GameObject("MuzzlePoint");
            muzzleGO.transform.SetParent(gunGO.transform, false);
            muzzleGO.transform.localPosition = new Vector3(0f, 0.04f, 0.18f);

            // FlareGun component
            var gun               = gunGO.AddComponent<FlareGun>();
            gun.muzzlePoint       = muzzleGO.transform;
            gun.launchSpeed       = 12f;
            gun.flareProjectilePrefab = BuildFlarePrototype();

            Debug.Log("[FireplaceBootstrap] Flare gun spawned at " + gunGO.transform.position);
        }

        // ── Flare prototype ───────────────────────────────────────────────────────

        private GameObject BuildFlarePrototype()
        {
            if (_flarePrototype != null) return _flarePrototype;

            _flarePrototype = new GameObject("[FlareProjectile_Proto]");
            _flarePrototype.SetActive(false);
            DontDestroyOnLoad(_flarePrototype);

            // Visual — small glowing sphere
            var mf     = _flarePrototype.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var mr     = _flarePrototype.AddComponent<MeshRenderer>();
            var mat    = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",    new Color(1f, 0.5f, 0.1f));
            mat.SetColor("_EmissionColor", new Color(2f, 0.8f, 0.1f));
            mat.EnableKeyword("_EMISSION");
            mr.sharedMaterial = mat;
            _flarePrototype.transform.localScale = Vector3.one * 0.04f;

            // Physics
            var rb             = _flarePrototype.AddComponent<Rigidbody>();
            rb.mass            = 0.05f;
            rb.linearDamping   = 0.01f;
            rb.useGravity      = true;
            var col            = _flarePrototype.AddComponent<SphereCollider>();
            col.radius         = 0.5f; // world radius = 0.5 × scale 0.04 = 0.02 m

            // Trail
            var trail          = _flarePrototype.AddComponent<TrailRenderer>();
            trail.time         = 0.4f;
            trail.startWidth   = 0.04f;
            trail.endWidth     = 0f;
            var trailMat       = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            trailMat.SetFloat("_Surface", 1f);
            trailMat.SetColor("_BaseColor", new Color(1f, 0.4f, 0.1f, 0.8f));
            trail.material     = trailMat;

            // Logic
            _flarePrototype.AddComponent<FlareProjectile>();

            return _flarePrototype;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private Vector3 AnchorPos(Vector3 offset) =>
            islandAnchor != null ? islandAnchor.TransformPoint(offset) : offset;

        private bool ValidateGLBs()
        {
            bool ok = true;
            if (fireplaceGlb == null) { Debug.LogError("[FireplaceBootstrap] fireplaceGlb not assigned."); ok = false; }
            if (stickGlb     == null) { Debug.LogError("[FireplaceBootstrap] stickGlb not assigned.");     ok = false; }
            if (flareGunGlb  == null) { Debug.LogError("[FireplaceBootstrap] flareGunGlb not assigned.");  ok = false; }
            return ok;
        }
    }
}
