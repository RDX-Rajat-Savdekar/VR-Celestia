using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CelestiaVR.Planets
{
    /// <summary>
    /// Parses NASA JPL Horizons ephemeris output files for planet RA/Dec data.
    ///
    /// Expected file format (horizons_results_*.txt):
    ///   - Data block between $$SOE and $$EOE markers
    ///   - Columns: Date___, RA_(ICRF), DEC_(ICRF), dRA*cosD, d(DEC)/dt, ...
    ///   - RA in HH MM SS.ff format, Dec in +DD MM SS.f format
    /// </summary>
    public static class PlanetEphemerisParser
    {
        public struct EphemerisEntry
        {
            public DateTime time;
            public float raHours;
            public float decDegrees;
        }

        /// <summary>
        /// Parses a Horizons result TextAsset and returns time-indexed entries.
        /// </summary>
        public static List<EphemerisEntry> Parse(TextAsset textAsset)
        {
            if (textAsset == null) return new List<EphemerisEntry>();
            return ParseText(textAsset.text);
        }

        /// <summary>
        /// Parses a Horizons result file at the given path.
        /// </summary>
        public static List<EphemerisEntry> ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[PlanetEphemerisParser] File not found: {filePath}");
                return new List<EphemerisEntry>();
            }
            return ParseText(File.ReadAllText(filePath));
        }

        private static List<EphemerisEntry> ParseText(string text)
        {
            var entries = new List<EphemerisEntry>();
            bool inDataBlock = false;
            string[] lines = text.Split('\n');

            // Find column positions from the header line after $$SOE
            // Horizons format has fixed-width columns; we use regex on each data line.
            // Pattern: date string, optional visibility flags, then RA HH MM SS.ff, then Dec ±DD MM SS.f
            // Example line: " 2026-Apr-06 00:00 Am  03 45 09.97 +24 28 03.1 ..."
            // [^\d]+ skips any visibility flags (e.g. "Am", "A", "m", "C") between time and RA
            var raDecPattern = new Regex(
                @"(\d{4}-\w{3}-\d{2}\s+\d{2}:\d{2})[^\d]+" +  // date + skip flags
                @"(\d{2})\s+(\d{2})\s+(\d{2}\.\d+)\s+" +       // RA HH MM SS
                @"([+-]\d{2})\s+(\d{2})\s+(\d{2}\.\d+)"        // Dec ±DD MM SS
            );

            foreach (string line in lines)
            {
                if (line.Contains("$$SOE")) { inDataBlock = true; continue; }
                if (line.Contains("$$EOE")) break;
                if (!inDataBlock) continue;

                var match = raDecPattern.Match(line);
                if (!match.Success) continue;

                // Parse date
                if (!DateTime.TryParse(match.Groups[1].Value.Replace("-Apr-", "-04-")
                    .Replace("-Jan-", "-01-").Replace("-Feb-", "-02-").Replace("-Mar-", "-03-")
                    .Replace("-May-", "-05-").Replace("-Jun-", "-06-").Replace("-Jul-", "-07-")
                    .Replace("-Aug-", "-08-").Replace("-Sep-", "-09-").Replace("-Oct-", "-10-")
                    .Replace("-Nov-", "-11-").Replace("-Dec-", "-12-"),
                    out DateTime dt))
                    continue;

                float raH = float.Parse(match.Groups[2].Value);
                float raM = float.Parse(match.Groups[3].Value);
                float raS = float.Parse(match.Groups[4].Value);
                float raHours = raH + raM / 60f + raS / 3600f;

                float decD = float.Parse(match.Groups[5].Value);
                float decM = float.Parse(match.Groups[6].Value);
                float decS = float.Parse(match.Groups[7].Value);
                float sign = decD < 0 ? -1f : 1f;
                float decDeg = sign * (Mathf.Abs(decD) + decM / 60f + decS / 3600f);

                entries.Add(new EphemerisEntry { time = dt, raHours = raHours, decDegrees = decDeg });
            }

            return entries;
        }

        /// <summary>
        /// Linearly interpolates RA/Dec for a given time from the entry list.
        /// Returns the nearest entry if time is out of range.
        /// </summary>
        public static (float raHours, float decDegrees) Interpolate(List<EphemerisEntry> entries, DateTime time)
        {
            if (entries == null || entries.Count == 0)
                return (0f, 0f);

            if (entries.Count == 1 || time <= entries[0].time)
                return (entries[0].raHours, entries[0].decDegrees);

            if (time >= entries[entries.Count - 1].time)
                return (entries[entries.Count - 1].raHours, entries[entries.Count - 1].decDegrees);

            // Binary search for bracket
            int lo = 0, hi = entries.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (entries[mid].time <= time) lo = mid;
                else hi = mid;
            }

            double span = (entries[hi].time - entries[lo].time).TotalSeconds;
            double elapsed = (time - entries[lo].time).TotalSeconds;
            float t = span > 0 ? (float)(elapsed / span) : 0f;

            float ra = Mathf.Lerp(entries[lo].raHours, entries[hi].raHours, t);
            float dec = Mathf.Lerp(entries[lo].decDegrees, entries[hi].decDegrees, t);
            return (ra, dec);
        }
    }
}
