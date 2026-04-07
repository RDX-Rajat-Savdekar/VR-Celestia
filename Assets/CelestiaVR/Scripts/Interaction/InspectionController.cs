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
        [Tooltip("World-space size of the object when inspected (meters).")]
        [Range(0.05f, 1f)]
        public float inspectionSize = 0.25f;

        [Header("Animation")]
        [Range(0.2f, 3f)]
        public float animationDuration = 0.8f;
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Camera _xrCamera;
        private CelestialBody _inspectedBody;
        private Coroutine _currentAnimation;

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
            if (_inspectedBody != null)
                SnapBackImmediate(_inspectedBody);

            _inspectedBody = body;
            body.isInspecting = true;

            if (_currentAnimation != null)
                StopCoroutine(_currentAnimation);
            _currentAnimation = StartCoroutine(AnimateIn(body));
        }

        public void ExitInspection()
        {
            if (_inspectedBody == null) return;

            var body = _inspectedBody;
            _inspectedBody = null;
            body.isInspecting = false;

            if (_currentAnimation != null)
                StopCoroutine(_currentAnimation);
            _currentAnimation = StartCoroutine(AnimateOut(body));

            inspectionPanel?.Hide();
        }

        private IEnumerator AnimateIn(CelestialBody body)
        {
            Vector3 startPos = body.transform.position;
            Quaternion startRot = body.transform.rotation;
            Vector3 startScale = body.transform.localScale;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = easeCurve.Evaluate(Mathf.Clamp01(elapsed / animationDuration));

                Vector3 targetPos = GetInspectionWorldPosition();
                Vector3 targetScale = Vector3.one * inspectionSize;

                body.transform.position = Vector3.Lerp(startPos, targetPos, t);
                body.transform.rotation = Quaternion.Slerp(startRot, Quaternion.identity, t);
                body.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                yield return null;
            }

            inspectionPanel?.Show(body);
        }

        private IEnumerator AnimateOut(CelestialBody body)
        {
            Vector3 startPos = body.transform.position;
            Quaternion startRot = body.transform.rotation;
            Vector3 startScale = body.transform.localScale;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = easeCurve.Evaluate(Mathf.Clamp01(elapsed / animationDuration));

                body.transform.position = Vector3.Lerp(startPos, body.skyPosition, t);
                body.transform.rotation = Quaternion.Slerp(startRot, body.skyRotation, t);
                body.transform.localScale = Vector3.Lerp(startScale, body.skyScale, t);

                yield return null;
            }

            // Snap to exact sky position
            body.transform.position = body.skyPosition;
            body.transform.rotation = body.skyRotation;
            body.transform.localScale = body.skyScale;
        }

        private void SnapBackImmediate(CelestialBody body)
        {
            body.isInspecting = false;
            body.transform.position = body.skyPosition;
            body.transform.rotation = body.skyRotation;
            body.transform.localScale = body.skyScale;
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
