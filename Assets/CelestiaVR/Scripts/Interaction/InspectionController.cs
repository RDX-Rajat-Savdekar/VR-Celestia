using System.Collections;
using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.UI;

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

        [Header("Real Scale Auto-trigger")]
        [Tooltip("Seconds after hologram appears before real scale shows automatically.")]
        public float realScaleDelay = 1f;
        [Tooltip("Earth radius in Unity metres used as the baseline for real-scale display.")]
        public float earthRadiusMetres = 0.08f;
        [Tooltip("Max hologram radius in metres — prevents huge objects from filling the room.")]
        public float maxRealScaleMetres = 4f;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private Camera _xrCamera;
        private CelestialBody _inspectedBody;
        private Coroutine _currentAnimation;
        private Coroutine _scaleAnimation;
        private Coroutine _autoRealScaleCoroutine;
        private GameObject _hologramCopy;
        private Vector3 _defaultHologramScale;
        private RealScaleComparison _realScaleComp;

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
            Debug.Log($"[InspectionController] StartInspection: {body.objectName}");

            if (_hologramCopy != null) DismissHologram();

            _inspectedBody = body;
            body.isInspecting = true;

            if (_currentAnimation != null) StopCoroutine(_currentAnimation);
            _currentAnimation = StartCoroutine(AnimateIn(body));
        }

        public void ExitInspection()
        {
            if (_inspectedBody == null) return;
            Debug.Log($"[InspectionController] ExitInspection: {_inspectedBody.objectName}");

            _inspectedBody.isInspecting = false;
            _inspectedBody = null;

            if (_currentAnimation       != null) StopCoroutine(_currentAnimation);
            if (_scaleAnimation         != null) StopCoroutine(_scaleAnimation);
            if (_autoRealScaleCoroutine != null) StopCoroutine(_autoRealScaleCoroutine);

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

            if (_inspectedBody != null && _inspectedBody.bodyType == CelestialBodyType.DeepSkyObject)
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
            _hologramCopy = Instantiate(body.gameObject);
            _hologramCopy.name = $"{body.objectName}_Hologram";

            var copyBody = _hologramCopy.GetComponent<CelestialBody>();
            if (copyBody != null) Destroy(copyBody);
            foreach (var col in _hologramCopy.GetComponentsInChildren<Collider>())
                Destroy(col);

            Vector3 finalScale = body.bodyType == CelestialBodyType.DeepSkyObject
                ? Vector3.one * 0.6f
                : body.transform.localScale * inspectionSize;

            _defaultHologramScale = finalScale;

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
            Debug.Log($"[InspectionController] {body.objectName} hologram materialised.");

            // Auto-trigger real scale comparison for bodies with known physical size
            if (body.physicalRadiusKm > 0f)
            {
                if (_autoRealScaleCoroutine != null) StopCoroutine(_autoRealScaleCoroutine);
                _autoRealScaleCoroutine = StartCoroutine(AutoTriggerRealScale(body));
            }
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

            Vector3 targetScale = isRealScale
                ? Vector3.one * (radiusMetres * 2f) // diameter
                : _defaultHologramScale;

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

                _realScaleComp.Show(_hologramCopy.transform, radiusMetres, earthRadiusMetres, scaleText);
                Debug.Log($"[InspectionController] Real scale: {_inspectedBody.objectName} = {scaleText}");
            }
            else
            {
                _realScaleComp?.Hide();
            }
        }

        private IEnumerator AutoTriggerRealScale(CelestialBody body)
        {
            yield return new WaitForSeconds(realScaleDelay);

            // Only proceed if still inspecting the same body
            if (_inspectedBody != body || _hologramCopy == null) yield break;

            float targetMetres = Mathf.Min(
                body.physicalRadiusKm / 6_371f * earthRadiusMetres,
                maxRealScaleMetres);

            SetHologramRadius(targetMetres);
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

        private Vector3 GetInspectionWorldPosition()
        {
            if (_xrCamera == null) _xrCamera = Camera.main;
            return _xrCamera.transform.position
                + _xrCamera.transform.forward * inspectionDistance
                + _xrCamera.transform.right   * horizontalOffset
                + _xrCamera.transform.up      * verticalOffset;
        }
    }
}
