using System.Collections;
using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.UI;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Handles the pull-out animation and inspection mode.
    /// When a CelestialBody is selected, it animates from the sky to a comfortable
    /// viewing position in front of the camera, then shows the inspection UI panel.
    ///
    /// Attach to a dedicated InspectionController GameObject.
    /// </summary>
    public class InspectionController : MonoBehaviour
    {
        [Header("References")]
        public InspectionPanel inspectionPanel;
        public SelectionManager selectionManager;

        [Header("Inspection Position")]
        [Tooltip("Distance in front of the camera (meters).")]
        [Range(0.3f, 3f)]
        public float inspectionDistance = 1.5f;
        [Tooltip("Horizontal offset (positive = right).")]
        public float horizontalOffset = -0.2f;
        [Tooltip("Vertical offset.")]
        public float verticalOffset = 0f;

        [Header("Inspection Scale")]
        [Tooltip("Hologram size multiplier relative to the planet's sky scale. 0.05 = palm-sized, 0.2 = basketball-sized.")]
        [Range(0.01f, 1f)]
        public float inspectionSize = 0.05f;

        [Header("Animation")]
        [Range(0.2f, 3f)]
        public float animationDuration = 0.8f;
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Hologram")]
        [Tooltip("Degrees per second the inspected copy rotates.")]
        public float hologramSpinSpeed = 30f;

        private Camera _xrCamera;
        private CelestialBody _inspectedBody;
        private Coroutine _currentAnimation;
        private GameObject _hologramCopy;

        private void Awake()
        {
            _xrCamera = Camera.main;

            if (selectionManager == null)
                selectionManager = FindFirstObjectByType<SelectionManager>();

            if (selectionManager != null)
            {
                selectionManager.OnObjectSelected += StartInspection;
                selectionManager.OnDeselect += ExitInspection;
            }
        }

        private void OnDestroy()
        {
            if (selectionManager != null)
            {
                selectionManager.OnObjectSelected -= StartInspection;
                selectionManager.OnDeselect -= ExitInspection;
            }
        }

        public void StartInspection(CelestialBody body)
        {
            Debug.Log($"[InspectionController] StartInspection: {body.objectName}");

            // Dismiss any current inspection first
            if (_hologramCopy != null)
                DismissHologram();

            _inspectedBody = body;
            body.isInspecting = true;

            if (_currentAnimation != null)
                StopCoroutine(_currentAnimation);
            _currentAnimation = StartCoroutine(AnimateIn(body));
        }

        public void ExitInspection()
        {
            if (_inspectedBody == null) return;
            Debug.Log($"[InspectionController] ExitInspection: {_inspectedBody.objectName}");

            _inspectedBody.isInspecting = false;
            _inspectedBody = null;

            if (_currentAnimation != null)
                StopCoroutine(_currentAnimation);

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

        private void Update()
        {
            if (_hologramCopy == null) return;

            // Smoothly follow the camera so it stays in front of the user
            Vector3 target = GetInspectionWorldPosition();
            _hologramCopy.transform.position = Vector3.Lerp(
                _hologramCopy.transform.position, target, Time.deltaTime * 3f);

            if (_inspectedBody != null && _inspectedBody.bodyType == CelestialBodyType.DeepSkyObject)
            {
                // Keep image facing the camera — spinning a flat quad just shows the edge
                _hologramCopy.transform.rotation = Quaternion.LookRotation(
                    _hologramCopy.transform.position - _xrCamera.transform.position);
            }
            else
            {
                _hologramCopy.transform.Rotate(Vector3.up, hologramSpinSpeed * Time.deltaTime, Space.Self);
            }
        }

        private IEnumerator AnimateIn(CelestialBody body)
        {
            // Spawn a copy — the original stays in the sky
            _hologramCopy = Instantiate(body.gameObject);
            _hologramCopy.name = $"{body.objectName}_Hologram";

            // Remove CelestialBody/Collider from copy so it doesn't interfere with selection
            var copyBody = _hologramCopy.GetComponent<CelestialBody>();
            if (copyBody != null) Destroy(copyBody);
            foreach (var col in _hologramCopy.GetComponentsInChildren<Collider>())
                Destroy(col);

            // DSOs are flat quads — use a fixed comfortable viewing size instead of
            // scaling down from their large sky size (which produces inconsistent results).
            Vector3 finalScale = body.bodyType == CelestialBodyType.DeepSkyObject
                ? Vector3.one * 0.6f
                : body.transform.localScale * inspectionSize;

            Vector3 spawnPos = GetInspectionWorldPosition();
            _hologramCopy.transform.position = spawnPos;
            _hologramCopy.transform.localScale = Vector3.zero;

            Debug.Log($"[InspectionController] Hologram spawned for {body.objectName} at {spawnPos}, finalScale={finalScale}");

            // Scale up from zero (sci-fi materialise effect)
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = easeCurve.Evaluate(Mathf.Clamp01(elapsed / animationDuration));

                // Gently follow camera so it stays in front if user moves slightly
                _hologramCopy.transform.position = Vector3.Lerp(spawnPos, GetInspectionWorldPosition(), t);
                _hologramCopy.transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, t);
                spawnPos = _hologramCopy.transform.position; // update so lerp doesn't snap

                yield return null;
            }

            _hologramCopy.transform.localScale = finalScale;
            inspectionPanel?.Show(body);
            Debug.Log($"[InspectionController] {body.objectName} hologram fully materialised.");
        }

        private Vector3 GetInspectionWorldPosition()
        {
            if (_xrCamera == null) _xrCamera = Camera.main;
            Vector3 forward = _xrCamera.transform.forward;
            Vector3 right = _xrCamera.transform.right;
            Vector3 up = _xrCamera.transform.up;

            return _xrCamera.transform.position
                + forward * inspectionDistance
                + right * horizontalOffset
                + up * verticalOffset;
        }
    }
}
