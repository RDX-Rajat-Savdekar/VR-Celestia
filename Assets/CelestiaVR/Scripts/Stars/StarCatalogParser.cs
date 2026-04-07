using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Stars
{
    /// <summary>
    /// Parses the HYG v4.2 star catalog CSV and produces a list of StarData.
    ///
    /// HYG CSV columns (0-indexed, from header):
    ///   0:id, 1:hip, 2:hd, 3:hr, 4:gl, 5:bf, 6:proper,
    ///   7:ra, 8:dec, 9:dist, 10:pmra, 11:pmdec,
    ///   12:rv, 13:mag, 14:absmag, 15:spect, 16:ci, ...
    /// </summary>
    public class StarCatalogParser : MonoBehaviour
    {
        [Tooltip("Drag hyg_v42.csv TextAsset here, or leave null to load from StreamingAssets.")]
        public TextAsset catalogTextAsset;

        [Tooltip("Only load stars brighter than this apparent magnitude.")]
        [Range(1f, 10f)]
        public float magnitudeLimit = 6.5f;

        [Tooltip("Maximum number of stars to load (0 = unlimited).")]
        public int maxStars = 0;

        public event Action<List<StarData>> OnCatalogLoaded;

        // Column indices in HYG v4.2 CSV header
        private const int COL_HIP = 1;
        private const int COL_PROPER = 6;
        private const int COL_RA = 7;
        private const int COL_DEC = 8;
        private const int COL_DIST = 9;
        private const int COL_MAG = 13;
        private const int COL_CI = 16;

        private void Start()
        {
            StartCoroutine(LoadCatalog());
        }

        private IEnumerator LoadCatalog()
        {
            string csvText;

            if (catalogTextAsset != null)
            {
                csvText = catalogTextAsset.text;
            }
            else
            {
                // StreamingAssets fallback
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "HYG/hyg_v42.csv");
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogError($"[StarCatalogParser] CSV not found at {path}. Assign catalogTextAsset in inspector.");
                    yield break;
                }
                csvText = System.IO.File.ReadAllText(path);
            }

            yield return null; // let frame breathe

            var stars = ParseCSV(csvText);
            Debug.Log($"[StarCatalogParser] Loaded {stars.Count} stars (mag ≤ {magnitudeLimit}).");
            OnCatalogLoaded?.Invoke(stars);
        }

        private List<StarData> ParseCSV(string csv)
        {
            var result = new List<StarData>(10000);
            var lines = csv.Split('\n');

            // Skip header (line 0)
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] cols = line.Split(',');
                if (cols.Length < 17) continue;

                // Parse magnitude first for early rejection
                if (!float.TryParse(cols[COL_MAG], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float mag))
                    continue;

                if (mag > magnitudeLimit) continue;

                if (!float.TryParse(cols[COL_RA], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float ra))
                    continue;

                if (!float.TryParse(cols[COL_DEC], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float dec))
                    continue;

                float.TryParse(cols[COL_DIST], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float dist);

                float ci = 0f;
                if (cols.Length > COL_CI)
                    float.TryParse(cols[COL_CI], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out ci);

                int.TryParse(cols[COL_HIP], out int hip);

                string properName = cols.Length > COL_PROPER ? cols[COL_PROPER].Trim().Trim('"') : "";

                var star = new StarData
                {
                    hipId = hip,
                    properName = properName,
                    raHours = ra,
                    decDegrees = dec,
                    magnitude = mag,
                    colorIndex = ci,
                    distancePc = dist,
                    unitPosition = CelestialCoordinates.RADecToUnity(ra, dec, 1f),
                    starColor = CelestialCoordinates.BVToColor(ci),
                    brightness = CelestialCoordinates.MagnitudeToBrightness(mag)
                };

                result.Add(star);

                if (maxStars > 0 && result.Count >= maxStars)
                    break;
            }

            return result;
        }
    }
}
