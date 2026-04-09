using UnityEngine;

namespace CelestiaVR.Stars
{
    /// <summary>
    /// Lightweight struct representing a single star from the HYG catalog.
    /// </summary>
    public struct StarData
    {
        public int hipId;           // Hipparcos catalog ID
        public string properName;   // Common name (e.g. "Sirius"), empty if none
        public float raHours;       // Right Ascension in hours
        public float decDegrees;    // Declination in degrees
        public float magnitude;     // Apparent visual magnitude
        public float colorIndex;    // B-V color index
        public float distancePc;    // Distance in parsecs (0 if unknown)

        public string spectralClass; // Spectral type string e.g. "G2V", "M5III"

        // Precomputed Unity position on unit sphere
        public Vector3 unitPosition;
        // Precomputed color from B-V
        public Color starColor;
        // Precomputed brightness [0,1]
        public float brightness;
    }
}
