using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Runtime spawner for the fireplace mini-game.
    /// Instantiates and wires:
    ///   • FireplaceSite  — glowing ground marker + state machine
    ///   • 3× WoodLog     — grabbable logs scattered around the island
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
        public GameObject woodLogGlb;
        [Tooltip("Smoke/fire VFX prefab shown when the fire is lit (e.g. VFX_Fire_Floor_01_Smoke).")]
        public GameObject smokePrefab;

        [Header("Testing")]
        [Tooltip("Show fireplace model immediately, skipping stick collection.")]
        public bool spawnImmediately = true;

        [Header("Positions (world-space — leave islandAnchor empty to use directly)")]
        public Vector3 fireplaceOffset = new Vector3(4.11f, 0.144f, -0.16f);

        [Tooltip("How far in front of the player to spawn the fireplace when no scene marker is found.")]
        public float spawnDistanceMetres = 3.05f;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private Vector3[] woodLogOffsets;

        private static readonly Vector3[] SnapOffsets = new Vector3[]
        {
            new Vector3(-0.12f, 0.04f,  0.0f),
            new Vector3( 0.12f, 0.04f,  0.0f),
            new Vector3( 0.0f,  0.10f,  0.0f),
        };

        private FireplaceSite _site;
        private readonly List<StickCollectible> _logs = new();

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
            }
            else
            {
                // Priority 2: 10 feet in front of the XR Origin (player)
                islandAnchor    = null;
                fireplaceOffset = GetSpawnInFront(spawnDistanceMetres);
            }

            woodLogOffsets = new Vector3[]
            {
                fireplaceOffset + new Vector3(-1.2f, 0f,  0.5f),
                fireplaceOffset + new Vector3( 1.2f, 0f,  0.3f),
                fireplaceOffset + new Vector3( 0.0f, 0f, -1.3f),
            };

            // Auto-find VFX_Fire_Floor_01_Smoke already placed in the scene
            if (smokePrefab == null)
            {
                var vfxInScene = GameObject.Find("VFX_Fire_Floor_01_Smoke");
                if (vfxInScene != null) smokePrefab = vfxInScene;
            }

            SpawnSite();
            SpawnSticks();

            if (spawnImmediately && _site != null && _site.fireplaceModel != null)
                _site.fireplaceModel.SetActive(true);
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
            _site.requiredLogs = woodLogOffsets.Length;
            _site.smokePrefab  = smokePrefab;

            if (fireplaceGlb != null)
            {
                var fp = Instantiate(fireplaceGlb, siteGO.transform);
                fp.transform.localPosition = Vector3.zero;
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

                var rb              = logGO.AddComponent<Rigidbody>();
                rb.mass             = 1.2f;
                rb.linearDamping    = 0.6f;
                rb.angularDamping   = 1.2f;
                rb.useGravity       = true;
                rb.freezeRotation   = true;

                var col       = logGO.AddComponent<CapsuleCollider>();
                col.height    = 0.38f;
                col.radius    = 0.045f;
                col.direction = 2;

                var grab              = logGO.AddComponent<XRGrabInteractable>();
                grab.movementType     = XRBaseInteractable.MovementType.VelocityTracking;
                grab.throwOnDetach    = true;
                grab.useDynamicAttach = true;

                var log        = logGO.AddComponent<StickCollectible>();
                log.targetSite = _site;
                log.snapOffset = SnapOffsets[i % SnapOffsets.Length];

                _logs.Add(log);
            }

            Debug.Log($"[FireplaceBootstrap] {_logs.Count} wood logs spawned.");
        }

        // ── Procedural log mesh ───────────────────────────────────────────────────

        private static GameObject BuildProceduralLog()
        {
            var root = new GameObject("WoodLog");

            var logBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            logBody.name = "LogBody";
            logBody.transform.SetParent(root.transform, false);
            logBody.transform.localScale    = new Vector3(0.09f, 0.18f, 0.09f);
            logBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(logBody.GetComponent<CapsuleCollider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",  new Color(0.35f, 0.18f, 0.06f, 1f));
            mat.SetFloat("_Metallic",   0f);
            mat.SetFloat("_Smoothness", 0.08f);
            logBody.GetComponent<MeshRenderer>().material = mat;

            AddLogEndCap(root.transform, new Vector3(0f, 0f,  0.18f));
            AddLogEndCap(root.transform, new Vector3(0f, 0f, -0.18f));

            return root;
        }

        private static void AddLogEndCap(Transform parent, Vector3 localPos)
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "LogCap";
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
            if (fireplaceGlb == null) { Debug.LogError("[FireplaceBootstrap] fireplaceGlb not assigned."); return false; }
            return true;
        }
    }
}
