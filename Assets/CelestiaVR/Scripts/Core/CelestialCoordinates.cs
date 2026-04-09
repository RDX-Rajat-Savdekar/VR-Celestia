using System;
using UnityEngine;

namespace CelestiaVR.Core
{
    /// <summary>
    /// Converts astronomical coordinates (RA/Dec) to Unity world positions
    /// and handles sidereal time rotation.
    /// </summary>
    public static class CelestialCoordinates
    {
        /// <summary>
        /// Converts Right Ascension (hours) and Declination (degrees) to a Unity Vector3
        /// on a sphere of given radius.
        /// </summary>
        public static Vector3 RADecToUnity(float raHours, float decDegrees, float radius = 1f)
        {
            float raRad = raHours * 15f * Mathf.Deg2Rad; // RA hours → degrees → radians
            float decRad = decDegrees * Mathf.Deg2Rad;

            float x = Mathf.Cos(decRad) * Mathf.Cos(raRad);
            float y = Mathf.Sin(decRad);
            float z = Mathf.Cos(decRad) * Mathf.Sin(raRad);

            return new Vector3(x, y, z) * radius;
        }

        /// <summary>
        /// Converts RA (decimal degrees) and Dec (decimal degrees) to Unity Vector3.
        /// </summary>
        public static Vector3 RADecDegreesToUnity(float raDegrees, float decDegrees, float radius = 1f)
        {
            float raRad = raDegrees * Mathf.Deg2Rad;
            float decRad = decDegrees * Mathf.Deg2Rad;

            float x = Mathf.Cos(decRad) * Mathf.Cos(raRad);
            float y = Mathf.Sin(decRad);
            float z = Mathf.Cos(decRad) * Mathf.Sin(raRad);

            return new Vector3(x, y, z) * radius;
        }

        /// <summary>
        /// Calculates Local Sidereal Time (LST) in hours for a given UTC DateTime and observer longitude.
        /// Uses the approximate formula accurate to ~0.1 seconds.
        /// </summary>
        public static float GetLocalSiderealTime(DateTime utc, float longitudeDegrees)
        {
            // Julian Date
            double jd = DateTimeToJulianDate(utc);

            // Greenwich Mean Sidereal Time in hours
            double T = (jd - 2451545.0) / 36525.0;
            double gmst = 6.697374558 + 2400.0513369 * T + 0.0000258622 * T * T - 1.7222e-9 * T * T * T;

            // Add UT hours
            double ut = utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0;
            gmst += ut * 1.00273790935;

            // Convert longitude offset to hours and add
            double lst = gmst + longitudeDegrees / 15.0;

            // Normalize to [0, 24)
            lst = lst % 24.0;
            if (lst < 0) lst += 24.0;

            return (float)lst;
        }

        /// <summary>
        /// Returns the Y-axis rotation angle (degrees) to apply to the sky sphere
        /// so that the star field matches the observer's local sidereal time.
        /// </summary>
        public static float GetSkyRotationDegrees(DateTime utc, float longitudeDegrees)
        {
            float lst = GetLocalSiderealTime(utc, longitudeDegrees);
            // LST 0h = RA 0h on meridian. Rotate sky so RA 0 aligns with south.
            return -lst * 15f; // 15 degrees per hour
        }

        public static double DateTimeToJulianDate(DateTime dt)
        {
            int y = dt.Year;
            int m = dt.Month;
            double d = dt.Day + dt.Hour / 24.0 + dt.Minute / 1440.0 + dt.Second / 86400.0;

            if (m <= 2)
            {
                y--;
                m += 12;
            }

            int A = y / 100;
            int B = 2 - A + A / 4;

            return (int)(365.25 * (y + 4716)) + (int)(30.6001 * (m + 1)) + d + B - 1524.5;
        }

        /// <summary>
        /// Maps B-V color index to an approximate RGB star color.
        /// B-V range: ~-0.4 (hot blue) to ~2.0 (cool red).
        /// </summary>
        public static Color BVToColor(float bv)
        {
            bv = Mathf.Clamp(bv, -0.4f, 2.0f);
            float t = (bv + 0.4f) / 2.4f; // normalize to [0,1]

            // Approximate stellar color ramp: blue → white → yellow → orange → red
            if (t < 0.25f)
            {
                float s = t / 0.25f;
                return Color.Lerp(new Color(0.6f, 0.7f, 1.0f), Color.white, s);
            }
            else if (t < 0.5f)
            {
                float s = (t - 0.25f) / 0.25f;
                return Color.Lerp(Color.white, new Color(1.0f, 0.95f, 0.7f), s);
            }
            else if (t < 0.75f)
            {
                float s = (t - 0.5f) / 0.25f;
                return Color.Lerp(new Color(1.0f, 0.95f, 0.7f), new Color(1.0f, 0.65f, 0.3f), s);
            }
            else
            {
                float s = (t - 0.75f) / 0.25f;
                return Color.Lerp(new Color(1.0f, 0.65f, 0.3f), new Color(1.0f, 0.3f, 0.1f), s);
            }
        }

        /// <summary>
        /// Computes the Sun's equatorial coordinates (RA hours, Dec degrees) using the
        /// simplified Jean Meeus low-precision solar position algorithm (~0.01° accuracy).
        /// </summary>
        public static (float raHours, float decDegrees) ComputeSunRADec(DateTime utc)
        {
            double jd = DateTimeToJulianDate(utc);
            double n  = jd - 2451545.0;                              // days from J2000.0

            double L = (280.460 + 0.9856474 * n) % 360.0;           // mean longitude (deg)
            if (L < 0) L += 360.0;
            double g = (357.528 + 0.9856003 * n) * Math.PI / 180.0; // mean anomaly (rad)

            double lambda = L + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2.0 * g); // ecliptic lon (deg)
            double epsilon = (23.439 - 0.0000004 * n) * Math.PI / 180.0;          // obliquity (rad)
            double lambdaRad = lambda * Math.PI / 180.0;

            double alpha = Math.Atan2(Math.Cos(epsilon) * Math.Sin(lambdaRad), Math.Cos(lambdaRad));
            double delta = Math.Asin(Math.Sin(epsilon) * Math.Sin(lambdaRad));

            float raHours = (float)(alpha * 12.0 / Math.PI);
            if (raHours < 0) raHours += 24f;
            float decDeg  = (float)(delta * 180.0 / Math.PI);

            return (raHours, decDeg);
        }

        /// <summary>
        /// Computes the altitude (degrees above horizon) of an object at given RA/Dec
        /// for an observer at the specified latitude and the given UTC time.
        /// Returns positive when above horizon, negative when below.
        /// </summary>
        public static float ComputeAltitudeDegrees(float raHours, float decDegrees,
            float latitudeDegrees, float longitudeDegrees, DateTime utc)
        {
            float lst = GetLocalSiderealTime(utc, longitudeDegrees); // hours
            float hourAngle = (lst - raHours) * 15f * Mathf.Deg2Rad; // radians
            float decRad    = decDegrees   * Mathf.Deg2Rad;
            float latRad    = latitudeDegrees * Mathf.Deg2Rad;

            float sinAlt = Mathf.Sin(latRad) * Mathf.Sin(decRad)
                         + Mathf.Cos(latRad) * Mathf.Cos(decRad) * Mathf.Cos(hourAngle);
            return Mathf.Asin(Mathf.Clamp(sinAlt, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Converts apparent magnitude to a normalized brightness value [0,1].
        /// Using the standard formula: flux ∝ 2.512^(-mag).
        /// </summary>
        public static float MagnitudeToBrightness(float magnitude, float referenceMinMag = -1.5f, float referenceMaxMag = 6.5f)
        {
            float flux = Mathf.Pow(2.512f, -magnitude);
            float fluxMin = Mathf.Pow(2.512f, -referenceMinMag);
            float fluxMax = Mathf.Pow(2.512f, -referenceMaxMag);
            return Mathf.Clamp01((flux - fluxMax) / (fluxMin - fluxMax));
        }
    }
}
