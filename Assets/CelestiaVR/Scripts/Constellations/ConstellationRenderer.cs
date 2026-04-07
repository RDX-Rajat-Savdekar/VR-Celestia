using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Constellations
{
    /// <summary>
    /// Renders constellation line art using LineRenderers.
    /// Each constellation is defined by a .txt file listing star name pairs.
    ///
    /// Attach to [ConstellationRoot] child of [SkyManager].
    /// Uses the star positions from the HYG catalog via a lookup dictionary.
    /// </summary>
    public class ConstellationRenderer : MonoBehaviour
    {
        [System.Serializable]
        public class ConstellationData
        {
            public string name;
            [Tooltip("Text file with lines of STAR1,STAR2 pairs (Bayer/Flamsteed names)")]
            public TextAsset definitionFile;
        }

        [Header("Constellation Definitions")]
        public List<ConstellationData> constellations;

        [Header("Visuals")]
        public Material lineMaterial;
        [Range(0f, 1f)]
        public float lineOpacity = 0.3f;
        public Color lineColor = new Color(0.5f, 0.7f, 1f, 0.3f);
        [Range(0.01f, 1f)]
        public float lineWidth = 0.2f;

        [Header("Star Catalog Reference")]
        [Tooltip("Plain text file with star name-to-RA/Dec mapping (stars.txt from StarViewer3D)")]
        public TextAsset starPositionsFile;

        private Dictionary<string, Vector3> _starPositions = new();
        private readonly List<GameObject> _lineObjects = new();
        private bool _visible = true;
        private SkyManager _skyManager;

        private void Awake()
        {
            _skyManager = GetComponentInParent<SkyManager>();
        }

        private void Start()
        {
            if (starPositionsFile != null)
                ParseStarPositions(starPositionsFile.text);

            BuildLines();
        }

        /// <summary>
        /// Called by StarCatalogParser to provide star positions after loading.
        /// Key = proper name (lowercase), value = unit sphere position.
        /// </summary>
        public void SetStarPositions(Dictionary<string, Vector3> positions)
        {
            _starPositions = positions;
            // Rebuild if already initialized
            foreach (var go in _lineObjects) Destroy(go);
            _lineObjects.Clear();
            BuildLines();
        }

        private void ParseStarPositions(string text)
        {
            // stars.txt format: NAME RA_hours DEC_degrees (space separated)
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var parts = line.Split(new char[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float ra)) continue;
                if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float dec)) continue;

                string name = parts[0].ToLowerInvariant();
                _starPositions[name] = CelestialCoordinates.RADecToUnity(ra, dec, 1f);
            }
        }

        private void BuildLines()
        {
            float radius = _skyManager != null ? _skyManager.skyRadius : 500f;

            foreach (var constellation in constellations)
            {
                if (constellation.definitionFile == null) continue;

                var lineParent = new GameObject(constellation.name);
                lineParent.transform.SetParent(transform, false);
                _lineObjects.Add(lineParent);

                var pairs = ParsePairs(constellation.definitionFile.text);

                foreach (var (starA, starB) in pairs)
                {
                    if (!_starPositions.TryGetValue(starA.ToLowerInvariant(), out Vector3 posA)) continue;
                    if (!_starPositions.TryGetValue(starB.ToLowerInvariant(), out Vector3 posB)) continue;

                    var segGo = new GameObject($"{starA}-{starB}");
                    segGo.transform.SetParent(lineParent.transform, false);

                    var lr = segGo.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.positionCount = 2;
                    lr.SetPosition(0, posA * radius);
                    lr.SetPosition(1, posB * radius);
                    lr.startWidth = lineWidth;
                    lr.endWidth = lineWidth;
                    lr.material = lineMaterial;

                    var c = lineColor;
                    c.a = lineOpacity;
                    lr.startColor = c;
                    lr.endColor = c;
                }
            }
        }

        private List<(string, string)> ParsePairs(string text)
        {
            var pairs = new List<(string, string)>();
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                int comma = line.IndexOf(',');
                if (comma < 0) continue;

                string a = line.Substring(0, comma).Trim();
                string b = line.Substring(comma + 1).Trim();
                if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b))
                    pairs.Add((a, b));
            }
            return pairs;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            foreach (var go in _lineObjects)
                go.SetActive(visible);
        }

        public void Toggle() => SetVisible(!_visible);
    }
}
