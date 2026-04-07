using System.Collections;
using TMPro;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.UI
{
    /// <summary>
    /// World-space UI panel shown during inspection mode.
    /// Shows name, type, distance, magnitude, and description of the selected object.
    ///
    /// Attach to the [InspectionAnchor] world-space Canvas.
    /// The panel is initially hidden and follows the camera during inspection.
    /// </summary>
    public class InspectionPanel : MonoBehaviour
    {
        [Header("Text Fields")]
        public TextMeshProUGUI objectNameText;
        public TextMeshProUGUI objectTypeText;
        public TextMeshProUGUI magnitudeText;
        public TextMeshProUGUI distanceText;
        public TextMeshProUGUI descriptionText;

        [Header("Animation")]
        [Range(0.1f, 1f)]
        public float fadeDuration = 0.3f;

        [Header("Follow Camera")]
        [Tooltip("If true, panel faces and follows the XR camera.")]
        public bool billboardToCamera = true;
        [Tooltip("Distance in front of camera when shown.")]
        public float panelDistance = 1.5f;
        public float horizontalOffset = 0.4f;

        private CanvasGroup _canvasGroup;
        private Camera _xrCamera;
        private Coroutine _fadeCoroutine;
        private bool _visible;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _xrCamera = Camera.main;
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_visible || !billboardToCamera) return;
            if (_xrCamera == null) return;

            // Position panel to the right of the inspection object
            Vector3 targetPos = _xrCamera.transform.position
                + _xrCamera.transform.forward * panelDistance
                + _xrCamera.transform.right * horizontalOffset;

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);
            transform.rotation = Quaternion.LookRotation(transform.position - _xrCamera.transform.position);
        }

        public void Show(CelestialBody body)
        {
            gameObject.SetActive(true);
            _visible = true;
            PopulateData(body);

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeTo(1f));
        }

        public void Hide()
        {
            _visible = false;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutAndDisable());
        }

        private void PopulateData(CelestialBody body)
        {
            if (objectNameText != null)
                objectNameText.text = string.IsNullOrEmpty(body.objectName) ? "Unknown" : body.objectName;

            if (objectTypeText != null)
                objectTypeText.text = body.bodyType.ToString();

            if (magnitudeText != null)
            {
                if (body.magnitude != 0f)
                    magnitudeText.text = $"Magnitude: {body.magnitude:F1}";
                else
                    magnitudeText.text = "";
            }

            if (distanceText != null)
            {
                if (body.distanceLightYears > 0f)
                    distanceText.text = $"Distance: {body.distanceLightYears:F1} ly";
                else
                    distanceText.text = "";
            }

            if (descriptionText != null)
                descriptionText.text = body.description;
        }

        private IEnumerator FadeTo(float target)
        {
            float start = _canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = target;
        }

        private IEnumerator FadeOutAndDisable()
        {
            yield return FadeTo(0f);
            gameObject.SetActive(false);
        }
    }
}
