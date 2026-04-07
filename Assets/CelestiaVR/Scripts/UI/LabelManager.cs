using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.Interaction;

namespace CelestiaVR.UI
{
    /// <summary>
    /// Manages a single reused ObjectLabel that follows the currently gazed CelestialBody.
    /// Only shows labels for named objects (stars with proper names, planets).
    ///
    /// Attach to the [UI] root. Assign labelPrefab (a world-space TextMeshPro with ObjectLabel).
    /// </summary>
    public class LabelManager : MonoBehaviour
    {
        public GameObject labelPrefab;

        private DwellSelector _dwellSelector;
        private ObjectLabel _activeLabel;
        private CelestialBody _labelledBody;

        private void Start()
        {
            _dwellSelector = FindFirstObjectByType<DwellSelector>();
            if (_dwellSelector != null)
            {
                _dwellSelector.OnGazeEnter += OnGazeEnter;
                _dwellSelector.OnGazeExit += OnGazeExit;
            }

            if (labelPrefab != null)
            {
                var go = Instantiate(labelPrefab, transform);
                _activeLabel = go.GetComponent<ObjectLabel>();
                go.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_dwellSelector != null)
            {
                _dwellSelector.OnGazeEnter -= OnGazeEnter;
                _dwellSelector.OnGazeExit -= OnGazeExit;
            }
        }

        private void OnGazeEnter(CelestialBody body)
        {
            if (_activeLabel == null) return;
            if (string.IsNullOrEmpty(body.objectName)) return;

            _labelledBody = body;
            _activeLabel.ShowFor(body);
        }

        private void OnGazeExit(CelestialBody body)
        {
            if (_activeLabel == null || _labelledBody != body) return;
            _labelledBody = null;
            _activeLabel.Hide();
        }
    }
}
