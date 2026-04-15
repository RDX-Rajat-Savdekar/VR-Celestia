using System.Collections;
using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.UI;
using CelestiaVR.Planets;
using CelestiaVR.Audio;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Handles the pull-out animation, inspection mode, and real-scale toggle.
    ///
    /// When a CelestialBody is selected, it:
    ///  1. Spawns a hologram copy in front of the camera.
    ///  2. Shows the InspectionPanel (auto-created if not assigned).
    ///  3. Optionally rescales the hologram to the object's true physical size
    ///     relative to Earth when the user presses the Real Scale button.
    ///
    /// Attach to a dedicated InspectionController GameObject.
    /// </summary>
    public class InspectionController : MonoBehaviour
    {
        [Header("References")]
        public InspectionPanel inspectionPanel;
        public SelectionManager selectionManager;
        [Tooltip("Optional Earth 3-D model prefab for the real-scale comparison sphere. " +
                 "If null a plain blue sphere is used.")]
        public GameObject earthPrefab;

        [Header("Inspection Position")]
        [Tooltip("Distance in front of the camera (metres).")]
        [Range(0.3f, 3f)]
        public float inspectionDistance = 1.5f;
        [Tooltip("Horizontal offset (positive = right).")]
        public float horizontalOffset = -0.2f;
        public float verticalOffset   = 0f;

        [Header("Inspection Scale")]
        [Tooltip("Hologram size multiplier relative to the object's sky scale. 0.05 = palm-sized.")]
        [Range(0.01f, 1f)]
        public float inspectionSize = 0.05f;

        [Header("Animation")]
        [Range(0.2f, 3f)]
        public float animationDuration = 0.8f;
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Hologram")]
        [Tooltip("Degrees per second the inspected copy rotates.")]
        public float hologramSpinSpeed = 30f;

        [Header("Real Scale")]
        [Tooltip("Earth radius in Unity metres used as the baseline for real-scale display.")]
        public float earthRadiusMetres = 0.08f;
        [Tooltip("Max hologram radius in metres — prevents huge objects from filling the room.")]
        public float maxRealScaleMetres = 0.15f;
        [Tooltip("Max hologram diameter (metres) for the default sky-proportional display. " +
                 "Clamps Jupiter/Sun so they don't fill the whole view.")]
        public float maxDefaultHologramDiameter = 0.22f;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private Camera _xrCamera;
        private CelestialBody _inspectedBody;
        private Coroutine _currentAnimation;
        private Coroutine _scaleAnimation;
        private GameObject _hologramCopy;
        private Vector3 _defaultHologramScale;
        private RealScaleComparison _realScaleComp;
        // Converts a desired world-space diameter (metres) → the localScale value that achieves it.
        // For simple spheres: factor = 1 (localScale == world diameter).
        // For prefab meshes: factor = 1 / meshBoundsSize (mesh is N units wide; localScale N = N-metre world size).
        private float _hologramNormFactor = 1f;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _xrCamera = Camera.main;

            if (selectionManager == null)
                selectionManager = FindFirstObjectByType<SelectionManager>();

            if (selectionManager != null)
            {
                selectionManager.OnObjectSelected += StartInspection;
                selectionManager.OnDeselect       += ExitInspection;
            }

            // Auto-create InspectionPanel if none assigned
            if (inspectionPanel == null)
            {
                var panelGO = new GameObject("[InspectionPanel]");
                inspectionPanel = panelGO.AddComponent<InspectionPanel>();
                inspectionPanel.inspectionController = this;
                Debug.Log("[InspectionController] Auto-created InspectionPanel.");
            }

            // RealScaleComparison lives on this same GO
            _realScaleComp = gameObject.AddComponent<RealScaleComparison>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (earthPrefab == null)
                earthPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/CelestiaVR_resources/PlanetTextures/Sphere/Earth_1_12756.prefab");
        }
#endif

        private void OnDestroy()
        {
            if (selectionManager != null)
            {
                selectionManager.OnObjectSelected -= StartInspection;
                selectionManager.OnDeselect       -= ExitInspection;
            }
        }

        // ── Inspection lifecycle ──────────────────────────────────────────────────

        public void StartInspection(CelestialBody body)
        {
            SoundManager.Instance?.Play(SoundEvent.InspectionOpen, body.transform.position);
            if (_hologramCopy != null) DismissHologram();

            _inspectedBody = body;
            body.isInspecting = true;

            if (_currentAnimation != null) StopCoroutine(_currentAnimation);
            _currentAnimation = StartCoroutine(AnimateIn(body));
        }

        public void ExitInspection()
        {
            if (_inspectedBody == null) return;
            SoundManager.Instance?.Play(SoundEvent.InspectionClose);

            _inspectedBody.isInspecting = false;
            _inspectedBody = null;

            if (_currentAnimation != null) StopCoroutine(_currentAnimation);
            if (_scaleAnimation   != null) StopCoroutine(_scaleAnimation);

            _realScaleComp?.Hide();
            DismissHologram();
            inspectionPanel?.Hide();
        }

        private void DismissHologram()
        {
            if (_hologramCopy != null)
            {
                Destroy(_hologramCopy);
                _hologramCopy = null;
            }
        }

        // ── Update ────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_hologramCopy == null) return;

            Vector3 target = GetInspectionWorldPosition();
            _hologramCopy.transform.position = Vector3.Lerp(
                _hologramCopy.transform.position, target, Time.deltaTime * 3f);

            bool billboard = _inspectedBody != null &&
                (_inspectedBody.bodyType == CelestialBodyType.DeepSkyObject ||
                 _inspectedBody.bodyType == CelestialBodyType.Constellation);

            if (billboard)
            {
                _hologramCopy.transform.rotation = Quaternion.LookRotation(
                    _hologramCopy.transform.position - _xrCamera.transform.position);
            }
            else
            {
                _hologramCopy.transform.Rotate(Vector3.up, hologramSpinSpeed * Time.deltaTime, Space.Self);
            }
        }

        // ── Animate in ────────────────────────────────────────────────────────────

        private IEnumerator AnimateIn(CelestialBody body)
        {
            Vector3 finalScale;

            if (body.bodyType == CelestialBodyType.Constellation)
            {
                // Build a glowing quad showing the constellation's artwork PNG
                _hologramCopy = BuildConstellationQuad(body);
                finalScale    = Vector3.one * 0.38f; // ~38 cm square in world space
                _hologramNormFactor = 1f;
                _defaultHologramScale = finalScale;

                Vector3 constSpawnPos = GetInspectionWorldPosition();
                _hologramCopy.transform.position   = constSpawnPos;
                _hologramCopy.transform.localScale = Vector3.zero;
                _hologramCopy.transform.rotation   = Quaternion.LookRotation(
                    constSpawnPos - _xrCamera.transform.position);

                float constElapsed = 0f;
                while (constElapsed < animationDuration)
                {
                    constElapsed += Time.deltaTime;
                    float ct = easeCurve.Evaluate(Mathf.Clamp01(constElapsed / animationDuration));
                    _hologramCopy.transform.position   = Vector3.Lerp(constSpawnPos, GetInspectionWorldPosition(), ct);
                    _hologramCopy.transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, ct);
                    constSpawnPos = _hologramCopy.transform.position;
                    yield return null;
                }
                _hologramCopy.transform.localScale = finalScale;
                inspectionPanel?.Show(body);
                yield break;
            }

            // Use the body's dedicated inspection prefab if available (e.g. the textured
            // sun or planet model); otherwise fall back to cloning the sky GameObject.
            GameObject source = body.inspectionPrefab != null ? body.inspectionPrefab : body.gameObject;
            _hologramCopy = Instantiate(source);
            _hologramCopy.name = $"{body.objectName}_Hologram";

            var copyBody = _hologramCopy.GetComponent<CelestialBody>();
            if (copyBody != null) Destroy(copyBody);
            foreach (var col in _hologramCopy.GetComponentsInChildren<Collider>())
                Destroy(col);

            // Scale the hologram by physical size relative to Earth, not by sky representation.
            if (body.bodyType == CelestialBodyType.DeepSkyObject)
            {
                finalScale = Vector3.one * 0.30f;
                _hologramNormFactor = 1f;
            }
            else
            {
                float physR = body.physicalRadiusKm > 0f ? body.physicalRadiusKm : 6_371f;
                float ratio = physR / 6_371f;
                float diam  = earthRadiusMetres * 2f * ratio;
                diam = Mathf.Clamp(diam, 0.15f, maxDefaultHologramDiameter); // 15 cm min, cap max

                // If using a prefab, measure its raw mesh bounds so we can convert
                // world-metres ↔ localScale. A Mercury_1_4878.prefab sphere is 4878 units
                // wide at localScale=1; we need localScale = desiredMetres / 4878.
                if (body.inspectionPrefab != null)
                {
                    _hologramCopy.transform.localScale = Vector3.one;
                    float boundsSize    = PlanetController.GetNormalisedBoundsSize(_hologramCopy);
                    _hologramNormFactor = boundsSize > 0f ? 1f / boundsSize : 1f;
                }
                else
                {
                    _hologramNormFactor = 1f; // simple sphere: localScale == world diameter
                }

                finalScale = Vector3.one * (diam * _hologramNormFactor);
            }

            _defaultHologramScale = finalScale;

            Debug.Log($"[InspectionController] Hologram '{body.objectName}': " +
                      $"diam={finalScale.x / _hologramNormFactor * 100f:F1} cm, " +
                      $"normFactor={_hologramNormFactor:G4}, localScale={finalScale.x:G4}");

            Vector3 spawnPos = GetInspectionWorldPosition();
            _hologramCopy.transform.position   = spawnPos;
            _hologramCopy.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = easeCurve.Evaluate(Mathf.Clamp01(elapsed / animationDuration));

                _hologramCopy.transform.position   = Vector3.Lerp(spawnPos, GetInspectionWorldPosition(), t);
                _hologramCopy.transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, t);
                spawnPos = _hologramCopy.transform.position;

                yield return null;
            }

            _hologramCopy.transform.localScale = finalScale;
            inspectionPanel?.Show(body);
        }

        // ── Real scale ────────────────────────────────────────────────────────────

        /// <summary>
        /// Animates the hologram to a specific radius in metres and shows the Earth comparison.
        /// Pass -1 to revert to the default sky-proportional scale and hide comparison.
        /// Called by InspectionPanel's Real Scale button and auto-triggered after hologram appears.
        /// </summary>
        public void SetHologramRadius(float radiusMetres)
        {
            if (_hologramCopy == null) return;

            bool isRealScale = radiusMetres >= 0f;

            // Convert world-metre radius → correct localScale using the normalisation factor
            // computed when the hologram was first instantiated.
            Vector3 targetScale = isRealScale
                ? Vector3.one * (radiusMetres * 2f * _hologramNormFactor) // diameter in localScale units
                : _defaultHologramScale;

            Debug.Log($"[InspectionController] SetHologramRadius: radiusM={radiusMetres:F4} " +
                      $"normFactor={_hologramNormFactor:G4} → localScale={targetScale.x:G4}");

            if (_scaleAnimation != null) StopCoroutine(_scaleAnimation);
            _scaleAnimation = StartCoroutine(AnimateScale(_hologramCopy.transform.localScale, targetScale));

            inspectionPanel?.SetRealScaleState(isRealScale);

            if (isRealScale && _inspectedBody != null && _realScaleComp != null)
            {
                float km      = _inspectedBody.physicalRadiusKm;
                float earthKm = 6_371f;
                string scaleText = km >= earthKm
                    ? $"{km / earthKm:F1}× Earth"
                    : $"1/{earthKm / km:F1} of Earth";

                _realScaleComp.Show(_hologramCopy.transform, radiusMetres, earthRadiusMetres,
                    scaleText, earthPrefab);
            }
            else
            {
                _realScaleComp?.Hide();
            }
        }

        private IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration = 0.5f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (_hologramCopy != null)
                    _hologramCopy.transform.localScale = Vector3.Lerp(from, to,
                        easeCurve.Evaluate(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            if (_hologramCopy != null)
                _hologramCopy.transform.localScale = to;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a world-space quad (plane) whose texture is the constellation's artwork PNG
        /// loaded from Resources/ConstellationArt/{name}.png.
        /// Falls back to a faint untextured quad when the PNG is not found.
        /// </summary>
        private static GameObject BuildConstellationQuad(CelestialBody body)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"{body.objectName}_Hologram";
            Object.Destroy(go.GetComponent<MeshCollider>());

            string pngName = body.objectName.ToLower().Replace(" ", "-");
            var tex = Resources.Load<Texture2D>("ConstellationArt/" + pngName);

            var mr  = go.GetComponent<MeshRenderer>();
            // Match StellariumLoader.BuildArtMat() exactly:
            // SrcAlpha + dest*One — black pixels (alpha≈0) vanish, bright art glows.
            var sh  = Shader.Find("Universal Render Pipeline/Unlit")
                   ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.SetFloat("_Surface",  1f);   // Transparent
            mat.SetFloat("_Blend",    0f);   // manual below
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); // additive dest
            mat.SetFloat("_ZWrite",   0f);
            mat.SetFloat("_Cull",     0f);   // double-sided
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
            }
            mat.SetColor("_BaseColor", new Color(0.85f, 0.92f, 1f, 1f));
            mr.sharedMaterial = mat;

            return go;
        }

        private Vector3 GetInspectionWorldPosition()
        {
            if (_xrCamera == null) _xrCamera = Camera.main;

            // Push hologram farther when it is large so the user can see it fully
            // without the model filling their entire field of view.
            float hologramExtent = _defaultHologramScale == Vector3.zero ? 0f
                : Mathf.Max(_defaultHologramScale.x, _defaultHologramScale.y, _defaultHologramScale.z) * 0.5f;
            // Keep the object at least 3.5× its own radius away; minimum = inspectionDistance
            float dynamicDist = Mathf.Max(inspectionDistance, hologramExtent * 3.5f + 0.3f);
            dynamicDist = Mathf.Min(dynamicDist, 4.0f); // never push beyond 4 m

            return _xrCamera.transform.position
                + _xrCamera.transform.forward * dynamicDist
                + _xrCamera.transform.right   * horizontalOffset
                + _xrCamera.transform.up      * verticalOffset;
        }
    }
}
