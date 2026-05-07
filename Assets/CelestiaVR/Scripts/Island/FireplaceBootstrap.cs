using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Runtime spawner for the fireplace mini-game.
    ///
    /// Instantiates and wires:
    ///   • FireplaceSite  — glowing ground marker + state machine
    ///   • 3× WoodLog     — grabbable logs scattered around the island
    ///   • FlareGun       — spawned at scene start, positioned near the fireplace site
    ///
    /// The flare gun is always present. It only ignites the fire when
    /// the wood logs have been deposited at the site (Built state).
    ///
    /// All positions are expressed in island-local space and transformed by
    /// islandAnchor.TransformPoint() so they follow the island's rotation/scale.
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
        public GameObject woodLogGlb;   // Optional dedicated log model; falls back to procedural
        public GameObject flareGunGlb;

        [Header("Testing")]
        [Tooltip("Spawn gun and show fireplace model immediately, skipping stick collection.")]
        public bool spawnImmediately = true;

        [Header("Positions (world-space — leave islandAnchor empty to use directly)")]
        public Vector3 fireplaceOffset = new Vector3(4.11f, 0.144f, -0.16f);
        public Vector3 flareGunOffset  = new Vector3(0.8f,  0.9f,   0f);

        [Tooltip("How far in front of the player to spawn the fireplace when no scene marker is found.")]
        public float spawnDistanceMetres = 3.05f; // ≈ 10 feet

        // ── Runtime ───────────────────────────────────────────────────────────────

        private Vector3[] woodLogOffsets;

        // Log pile offsets so deposited logs don't all overlap at the site centre
        private static readonly Vector3[] SnapOffsets = new Vector3[]
        {
            new Vector3(-0.12f, 0.04f,  0.0f),
            new Vector3( 0.12f, 0.04f,  0.0f),
            new Vector3( 0.0f,  0.10f,  0.0f),
        };

        private FireplaceSite              _site;
        private readonly List<StickCollectible> _logs = new();
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
            if (flareGunGlb == null)
                flareGunGlb  = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(root + "flare_gun.prefab");
            if (woodLogGlb == null)
                woodLogGlb   = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(root + "lowpoly_stick_-_01.prefab");
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

        private void TryLoadFromResources()
        {
            const string root = "FirePlaceEnv/";
            if (fireplaceGlb == null) fireplaceGlb = Resources.Load<GameObject>(root + "small_fire_place");
            if (flareGunGlb  == null) flareGunGlb  = Resources.Load<GameObject>(root + "flare_gun");
            if (woodLogGlb   == null) woodLogGlb   = Resources.Load<GameObject>(root + "lowpoly_stick_-_01");
        }

        private void Start()
        {
            if (FindFirstObjectByType<FireplaceSite>() != null) return;

            TryLoadFromResources();

            if (!ValidateCore()) return;

            // Priority 1: designer-placed To_Place_Fireplace marker
            var marker = GameObject.Find("To_Place_Fireplace");
            if (marker != null)
            {
                islandAnchor    = null;
                fireplaceOffset = marker.transform.position;
                Debug.Log($"[FireplaceBootstrap] Using To_Place_Fireplace position: {fireplaceOffset}");
            }
            else
            {
                // Priority 2: 10 feet in front of the XR Origin (player)
                islandAnchor    = null;
                fireplaceOffset = GetSpawnInFront(spawnDistanceMetres);
                Debug.Log($"[FireplaceBootstrap] Spawning 10 ft in front of player at: {fireplaceOffset}");
            }

            // Flare gun: 1 m to the right of the fireplace, at grab height
            flareGunOffset = fireplaceOffset + new Vector3(1.0f, 0.9f, 0f);

            // Logs: scattered in a loose ring around the fireplace
            woodLogOffsets = new Vector3[]
            {
                fireplaceOffset + new Vector3(-1.2f, 0f,  0.5f),
                fireplaceOffset + new Vector3( 1.2f, 0f,  0.3f),
                fireplaceOffset + new Vector3( 0.0f, 0f, -1.3f),
            };

            SpawnSite();
            SpawnSticks();

            if (spawnImmediately)
            {
                // Show fireplace model right away and spawn the gun without needing sticks
                if (_site != null && _site.fireplaceModel != null)
                    _site.fireplaceModel.SetActive(true);
                SpawnFlareGun();
            }
        }

        // ── Spawners ──────────────────────────────────────────────────────────────

        private void SpawnSite()
        {
            var siteGO = new GameObject("[FireplaceSite]");

            var marker = GameObject.Find("To_Place_Fireplace");
            if (marker != null)
                siteGO.transform.SetParent(marker.transform, false);

            siteGO.transform.position = AnchorPos(fireplaceOffset);

            _site = siteGO.AddComponent<FireplaceSite>();
            _site.requiredLogs = woodLogOffsets.Length;  // match spawned count

            if (fireplaceGlb != null)
            {
                var fp = Instantiate(fireplaceGlb, siteGO.transform);
                fp.transform.localPosition = Vector3.zero;
                // Do NOT reset localRotation — the GLB importer bakes an axis-correction
                // rotation into the root (typically -90° X). Overriding it tilts the model.
                FireplaceSite.FixGLBShaders(fp, 0.5f);
                fp.SetActive(false);
                _site.fireplaceModel = fp;
            }

            Debug.Log("[FireplaceBootstrap] Site spawned at " + siteGO.transform.position);
        }

        private void SpawnSticks()
        {
            for (int i = 0; i < woodLogOffsets.Length; i++)
            {
                GameObject logGO;
                if (woodLogGlb != null)
                {
                    logGO = Instantiate(woodLogGlb);
                    // The lowpoly_stick GLB exports at ~0.006 scale; scale up to grabbable size
                    logGO.transform.localScale = Vector3.one * 0.012f;
                    FireplaceSite.FixGLBShaders(logGO, 0.15f);
                }
                else
                {
                    logGO = BuildProceduralLog();
                }

                logGO.name = $"[WoodLog_{i}]";
                logGO.transform.position = AnchorPos(woodLogOffsets[i]);
                logGO.transform.rotation = Quaternion.Euler(
                    Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-5f, 5f));

                // Physics — no gravity so logs stay at spawn height
                var rb              = logGO.AddComponent<Rigidbody>();
                rb.mass             = 1.2f;
                rb.linearDamping    = 0.6f;
                rb.angularDamping   = 1.2f;
                rb.useGravity       = false;

                // Collider — capsule along Z (length of the log)
                var col             = logGO.AddComponent<CapsuleCollider>();
                col.height          = 0.38f;
                col.radius          = 0.045f;
                col.direction       = 2; // Z axis

                // XR Grab
                var grab            = logGO.AddComponent<XRGrabInteractable>();
                grab.movementType   = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach  = true;
                grab.useDynamicAttach = true;

                // Collectible component (same logic as sticks)
                var log             = logGO.AddComponent<StickCollectible>();
                log.targetSite      = _site;
                log.snapOffset      = SnapOffsets[i % SnapOffsets.Length];

                _logs.Add(log);
            }

            Debug.Log($"[FireplaceBootstrap] {_logs.Count} wood logs spawned.");
        }

        /// <summary>
        /// Spawns the flare gun and parents it directly to the right hand controller
        /// so it's always held — no grabbing needed.
        /// The muzzle point is parented to the same hand anchor so it always
        /// fires in the controller's forward direction regardless of gun model orientation.
        /// </summary>
        public void SpawnFlareGun()
        {
            if (flareGunGlb == null) return;

            var rightHand = FindRightHandAnchor();
            if (rightHand == null)
            {
                Debug.LogWarning("[FireplaceBootstrap] Could not find right hand anchor — gun not spawned.");
                return;
            }

            var gunGO = Instantiate(flareGunGlb);
            gunGO.name = "[FlareGun]";
            FireplaceSite.FixGLBShaders(gunGO, 0.3f);

            // Parent to right hand — no Rigidbody or XRGrabInteractable needed
            gunGO.transform.SetParent(rightHand, false);

            // Position: slightly forward and down so the grip sits in the palm
            // The barrel is in controller +Y after import (gun stands upright when arm is forward).
            // Rotating -90° around X maps +Y → +Z, so the barrel now points forward with the arm.
            gunGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            gunGO.transform.localPosition = new Vector3(0f, -0.02f, 0.05f);

            // Muzzle point
            var muzzleGO          = new GameObject("MuzzlePoint");
            muzzleGO.transform.SetParent(gunGO.transform, false);
            muzzleGO.transform.localPosition = new Vector3(0f, 0.04f, 0.18f);

            // FlareGun component
            var gun               = gunGO.AddComponent<FlareGun>();
            gun.muzzlePoint       = muzzleGO.transform;
            gun.launchSpeed       = 22f;
            gun.flareProjectilePrefab = BuildFlarePrototype();

            Debug.Log("[FireplaceBootstrap] Flare gun attached to right hand: " + rightHand.name);
        }

        /// <summary>
        /// Finds the right-hand controller transform in the XR rig.
        /// Tries ActionBasedController components first, then falls back to name search.
        /// </summary>
        private static Transform FindRightHandAnchor()
        {
            // 1. ActionBasedController (XRI3 standard)
            foreach (var c in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var n = c.name.ToLowerInvariant();
                if (n.Contains("right") && !n.Contains("left"))
                    return c.transform;
            }

            // 2. Name search inside XR Origin hierarchy
            var xrOrigin = GameObject.Find("XR Origin Hands (XR Rig)");
            if (xrOrigin == null) xrOrigin = GameObject.Find("XR Origin (XR Rig)");
            if (xrOrigin == null) xrOrigin = GameObject.Find("XR Origin");
            if (xrOrigin != null)
            {
                foreach (var t in xrOrigin.GetComponentsInChildren<Transform>(true))
                {
                    var n = t.name.ToLowerInvariant();
                    if (n.Contains("right") && !n.Contains("left")
                        && (n.Contains("controller") || n.Contains("hand") || n.Contains("anchor")))
                        return t;
                }
            }

            // 3. Global GameObject.Find fallback
            foreach (var candidate in new[] {
                "Right Controller", "RightHand Controller", "Right Hand Controller",
                "Right Hand", "RightHand", "Right Interactor" })
            {
                var go = GameObject.Find(candidate);
                if (go != null) return go.transform;
            }

            return null;
        }

        // ── Procedural log mesh ───────────────────────────────────────────────────

        /// <summary>
        /// Creates a brown cylinder scaled to look like a wooden log (~8cm radius, ~36cm long).
        /// Used as fallback when no woodLogGlb is assigned.
        /// </summary>
        private static GameObject BuildProceduralLog()
        {
            var root = new GameObject("WoodLog");

            // Cylinder primitive (Unity default: height=2, radius=0.5 in local space)
            var logBody       = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            logBody.name      = "LogBody";
            logBody.transform.SetParent(root.transform, false);
            // Scale: x/z = diameter 0.09m, y = half-height → 0.18 = 36cm long when rotated 90°
            logBody.transform.localScale    = new Vector3(0.09f, 0.18f, 0.09f);
            logBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat along Z

            // Remove collider — we add CapsuleCollider on the root for better interaction
            Object.Destroy(logBody.GetComponent<CapsuleCollider>());

            // Brown wood material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",   new Color(0.35f, 0.18f, 0.06f, 1f));
            mat.SetFloat("_Metallic",    0f);
            mat.SetFloat("_Smoothness",  0.08f);
            logBody.GetComponent<MeshRenderer>().material = mat;

            // End caps (slightly darker discs to show cross-section)
            AddLogEndCap(root.transform,  new Vector3(0f, 0f,  0.18f));
            AddLogEndCap(root.transform,  new Vector3(0f, 0f, -0.18f));

            return root;
        }

        private static void AddLogEndCap(Transform parent, Vector3 localPos)
        {
            var cap       = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name      = "LogCap";
            cap.transform.SetParent(parent, false);
            cap.transform.localPosition = localPos;
            cap.transform.localScale    = new Vector3(0.09f, 0.005f, 0.09f);
            cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Object.Destroy(cap.GetComponent<CapsuleCollider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",  new Color(0.55f, 0.32f, 0.12f, 1f));
            mat.SetFloat("_Metallic",   0f);
            mat.SetFloat("_Smoothness", 0.05f);
            cap.GetComponent<MeshRenderer>().material = mat;
        }

        // ── Flare prototype ───────────────────────────────────────────────────────

        private GameObject BuildFlarePrototype()
        {
            if (_flarePrototype != null) return _flarePrototype;

            _flarePrototype = new GameObject("[FlareProjectile_Proto]");
            _flarePrototype.SetActive(false);
            DontDestroyOnLoad(_flarePrototype);

            // Visual — large bright glowing sphere (impossible to miss)
            // CreatePrimitive gets the sphere mesh without relying on builtin resource names
            // (Resources.GetBuiltinResource<Mesh>("Sphere.fbx") returns null in Unity 6)
            var sphereTemp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mf         = _flarePrototype.AddComponent<MeshFilter>();
            mf.sharedMesh  = sphereTemp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphereTemp);
            var mr         = _flarePrototype.AddComponent<MeshRenderer>();
            var mat       = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",     new Color(1f, 0.35f, 0f));
            mat.SetColor("_EmissionColor", new Color(4f, 1.2f, 0f));   // very hot glow
            mat.EnableKeyword("_EMISSION");
            mr.sharedMaterial = mat;
            _flarePrototype.transform.localScale = Vector3.one * 0.12f; // 12 cm — clearly visible

            // Point light so it illuminates surroundings as it flies
            var lightGO       = new GameObject("FlareLight");
            lightGO.transform.SetParent(_flarePrototype.transform, false);
            var fl            = lightGO.AddComponent<Light>();
            fl.type           = LightType.Point;
            fl.color          = new Color(1f, 0.5f, 0.05f);
            fl.intensity      = 3f;
            fl.range          = 4f;

            // Physics
            var rb                    = _flarePrototype.AddComponent<Rigidbody>();
            rb.mass                   = 0.05f;
            rb.linearDamping          = 0.01f;
            rb.useGravity             = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var sphereCol             = _flarePrototype.AddComponent<SphereCollider>();
            sphereCol.radius          = 0.5f;

            // Thick bright trail — clearly visible arc in flight
            var trail        = _flarePrototype.AddComponent<TrailRenderer>();
            trail.time       = 0.6f;
            trail.startWidth = 0.12f;
            trail.endWidth   = 0f;
            var trailMat     = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            trailMat.SetFloat("_Surface", 1f);
            trailMat.SetColor("_BaseColor", new Color(1f, 0.5f, 0f, 1f));
            trail.material   = trailMat;

            // Logic
            _flarePrototype.AddComponent<FlareProjectile>();

            return _flarePrototype;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Vector3 GetSpawnInFront(float distanceMetres)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var xrOrigin = GameObject.Find("XR Origin Hands (XR Rig)");
                if (xrOrigin == null) xrOrigin = GameObject.Find("XR Origin (XR Rig)");
                if (xrOrigin == null) xrOrigin = GameObject.Find("XR Origin");
                if (xrOrigin != null) cam = xrOrigin.GetComponentInChildren<Camera>();
            }
            if (cam == null) return Vector3.zero;

            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            var pos = cam.transform.position + forward * distanceMetres;
            pos.y = 0f;
            return pos;
        }

        private Vector3 AnchorPos(Vector3 offset) =>
            islandAnchor != null ? islandAnchor.TransformPoint(offset) : offset;

        private bool ValidateCore()
        {
            bool ok = true;
            if (fireplaceGlb == null) { Debug.LogError("[FireplaceBootstrap] fireplaceGlb not assigned."); ok = false; }
            if (flareGunGlb  == null) { Debug.LogError("[FireplaceBootstrap] flareGunGlb not assigned.");  ok = false; }
            return ok;
        }
    }
}
