using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Interaction
{
    /// <summary>
    /// Visual highlight effect for gazed/dwelled celestial objects.
    /// Pulses a glow ring around the target using a simple scale animation.
    ///
    /// Attach to a persistent manager object. Assign glowRingPrefab in inspector.
    /// </summary>
    public class HighlightEffect : MonoBehaviour
    {
        [Header("Glow Ring")]
        [Tooltip("A simple quad/sphere with an additive glow material.")]
        public GameObject glowRingPrefab;
        [Range(0.5f, 3f)]
        public float ringBaseScale = 1.5f;
        [Range(0f, 1f)]
        public float pulseAmount = 0.15f;
        [Range(0.5f, 5f)]
        public float pulseSpeed = 2f;

        private DwellSelector _dwell;
        private GameObject _glowInstance;
        private CelestialBody _currentBody;
        private float _pulsePhase;

        private void Start()
        {
            _dwell = FindFirstObjectByType<DwellSelector>();
            if (_dwell != null)
            {
                _dwell.OnGazeEnter += OnGazeEnter;
                _dwell.OnGazeExit += OnGazeExit;
            }

            if (glowRingPrefab != null)
            {
                _glowInstance = Instantiate(glowRingPrefab);
                // Match the pink used by BillboardStarDwellDetector's proxy glow
                var rend = _glowInstance.GetComponentInChildren<Renderer>(true);
                if (rend != null)
                    rend.material.color = new Color(1f, 0.3f, 0.7f, 0.18f);
                _glowInstance.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_dwell != null)
            {
                _dwell.OnGazeEnter -= OnGazeEnter;
                _dwell.OnGazeExit -= OnGazeExit;
            }
        }

        private void Update()
        {
            if (_glowInstance == null || _currentBody == null) return;

            _pulsePhase += Time.deltaTime * pulseSpeed;
            float pulse = 1f + Mathf.Sin(_pulsePhase) * pulseAmount;
            float dwellProgress = _dwell != null ? _dwell.DwellProgress : 0f;

            // Grow the ring as dwell progresses
            float scale = ringBaseScale * pulse * (1f + dwellProgress * 0.5f);
            _glowInstance.transform.position = _currentBody.transform.position;
            _glowInstance.transform.localScale = Vector3.one * scale * _currentBody.transform.localScale.x;

            // Face camera
            var cam = Camera.main;
            if (cam != null)
                _glowInstance.transform.rotation = Quaternion.LookRotation(
                    _glowInstance.transform.position - cam.transform.position);
        }

        private void OnGazeEnter(CelestialBody body)
        {
            _currentBody = body;
            _pulsePhase = 0f;
            if (_glowInstance != null)
                _glowInstance.SetActive(true);
        }

        private void OnGazeExit(CelestialBody body)
        {
            _currentBody = null;
            if (_glowInstance != null)
                _glowInstance.SetActive(false);
        }
    }
}
