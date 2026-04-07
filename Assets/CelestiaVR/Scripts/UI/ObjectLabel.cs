using System.Collections;
using TMPro;
using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.Interaction;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Billboard label that fades in when the user dwells on a CelestialBody.
    /// Auto-positions above the target object.
    ///
    /// Attach one instance to a world-space TextMeshPro label prefab.
    /// The LabelManager creates/pools these at runtime.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class ObjectLabel : MonoBehaviour
    {
        [Range(0.1f, 1f)]
        public float fadeDuration = 0.2f;
        [Tooltip("Vertical offset above the target object center.")]
        public float verticalOffset = 3f;

        private TextMeshPro _tmp;
        private Camera _xrCamera;
        private Transform _target;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
            _xrCamera = Camera.main;
            SetAlpha(0f);
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            transform.position = _target.position + Vector3.up * verticalOffset;

            // Billboard: face camera
            if (_xrCamera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - _xrCamera.transform.position);
        }

        public void ShowFor(CelestialBody body)
        {
            _target = body.transform;
            _tmp.text = body.objectName;
            gameObject.SetActive(true);

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeTo(1f));
        }

        public void Hide()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutAndDeactivate());
        }

        private IEnumerator FadeTo(float target)
        {
            float start = GetAlpha();
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Lerp(start, target, elapsed / fadeDuration));
                yield return null;
            }
            SetAlpha(target);
        }

        private IEnumerator FadeOutAndDeactivate()
        {
            yield return FadeTo(0f);
            _target = null;
            gameObject.SetActive(false);
        }

        private void SetAlpha(float a)
        {
            var c = _tmp.color;
            c.a = a;
            _tmp.color = c;
        }

        private float GetAlpha() => _tmp.color.a;
    }
}
