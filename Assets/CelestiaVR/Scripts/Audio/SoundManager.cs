using System.Collections.Generic;
using UnityEngine;

namespace CelestiaVR.Audio
{
    /// <summary>
    /// Procedural sound manager for CelestiaVR.
    ///
    /// All audio clips are synthesised at runtime from sine waves and noise — no
    /// audio asset files are required. This means the system works on Quest out of
    /// the box without any extra resource folders.
    ///
    /// 2-D (UI) sounds play through a child AudioSource (spatialBlend = 0).
    /// 3-D (world) sounds use AudioSource.PlayClipAtPoint so they attenuate with
    /// distance and give the user directional cues.
    ///
    /// Added to the scene by StargazingSceneBootstrap.EnsureSoundManager().
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        // ── Mute ──────────────────────────────────────────────────────────────────

        public bool IsMuted { get; private set; } = false;

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
            Debug.Log($"[SoundManager] Sound {(IsMuted ? "OFF" : "ON")}");
        }

        // ── Clip library ──────────────────────────────────────────────────────────

        private Dictionary<SoundEvent, AudioClip> _clips;
        private AudioSource _2dSource; // spatialBlend = 0

        private const int   SampleRate = 44100;

        // Volumes per event type — tweak here to balance the mix
        private static readonly Dictionary<SoundEvent, float> Volumes = new()
        {
            { SoundEvent.GazeEnter,      0.35f },
            { SoundEvent.Select,         0.65f },
            { SoundEvent.Deselect,       0.30f },
            { SoundEvent.InspectionOpen, 0.60f },
            { SoundEvent.InspectionClose,0.30f },
            { SoundEvent.PanelOpen,      0.50f },
            { SoundEvent.PanelClose,     0.40f },
            { SoundEvent.ButtonPress,    0.45f },
            { SoundEvent.ModeSwitch,     0.55f },
            { SoundEvent.StickPickup,    0.55f },
            { SoundEvent.StickDeposit,   0.60f },
            { SoundEvent.FireIgnite,     0.80f },
            { SoundEvent.FlareShot,      0.75f },
            { SoundEvent.TimeScroll,     0.20f },
            { SoundEvent.Movement,       0.18f },
        };

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // 2-D AudioSource (UI sounds, no positional attenuation)
            _2dSource              = gameObject.AddComponent<AudioSource>();
            _2dSource.spatialBlend = 0f;
            _2dSource.playOnAwake  = false;
            _2dSource.volume       = 1f;

            GenerateAllClips();
            Debug.Log($"[SoundManager] {_clips.Count} clips generated.");
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Play a 2-D (UI) sound — no positional attenuation.</summary>
        public void Play(SoundEvent ev)
        {
            if (IsMuted || _clips == null) return;
            if (!_clips.TryGetValue(ev, out var clip) || clip == null) return;
            float vol = Volumes.TryGetValue(ev, out float v) ? v : 0.5f;
            _2dSource.PlayOneShot(clip, vol);
        }

        /// <summary>Play a 3-D (world) sound at a world-space position.</summary>
        public void Play(SoundEvent ev, Vector3 worldPos)
        {
            if (IsMuted || _clips == null) return;
            if (!_clips.TryGetValue(ev, out var clip) || clip == null) return;
            float vol = Volumes.TryGetValue(ev, out float v) ? v : 0.5f;
            AudioSource.PlayClipAtPoint(clip, worldPos, vol);
        }

        // ── Clip generation ───────────────────────────────────────────────────────

        private void GenerateAllClips()
        {
            _clips = new Dictionary<SoundEvent, AudioClip>
            {
                { SoundEvent.GazeEnter,       MakeSweep("GazeEnter",      380f, 620f, 0.16f) },
                { SoundEvent.Select,          MakeChord("Select",          new[]{880f, 1320f}, 0.28f) },
                { SoundEvent.Deselect,        MakeSweep("Deselect",        620f, 380f, 0.14f) },
                { SoundEvent.InspectionOpen,  MakeSweep("InspOpen",        280f, 950f, 0.38f) },
                { SoundEvent.InspectionClose, MakeSweep("InspClose",       700f, 320f, 0.18f) },
                { SoundEvent.PanelOpen,       MakeSweep("PanelOpen",       650f, 950f, 0.20f) },
                { SoundEvent.PanelClose,      MakeSweep("PanelClose",      830f, 560f, 0.15f) },
                { SoundEvent.ButtonPress,     MakeTone ("ButtonPress",     1100f, 0.07f) },
                { SoundEvent.ModeSwitch,      MakeChord("ModeSwitch",      new[]{720f, 1020f}, 0.22f) },
                { SoundEvent.StickPickup,     MakeNoiseBurst("StickPickup",0.12f, 320f) },
                { SoundEvent.StickDeposit,    MakeNoiseBurst("StickDeposit",0.28f, 160f) },
                { SoundEvent.FireIgnite,      MakeSweep("FireIgnite",      240f, 1500f, 0.55f) },
                { SoundEvent.FlareShot,       MakeNoiseBurst("FlareShot",  0.25f, 1200f) },
                { SoundEvent.TimeScroll,      MakeTone ("TimeScroll",      700f, 0.055f) },
                { SoundEvent.Movement,        MakeTone ("Movement",        320f, 0.065f) },
            };
        }

        // ── Synthesis helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Sine wave that sweeps linearly from <paramref name="f0"/> to <paramref name="f1"/> Hz
        /// over <paramref name="dur"/> seconds, with an attack+decay envelope.
        /// </summary>
        private static AudioClip MakeSweep(string name, float f0, float f1, float dur)
        {
            int    n       = Mathf.RoundToInt(dur * SampleRate);
            float[] data   = new float[n];
            float  phase   = 0f;

            for (int i = 0; i < n; i++)
            {
                float t    = (float)i / n;
                float freq = Mathf.Lerp(f0, f1, t);
                phase     += freq / SampleRate;
                data[i]    = Mathf.Sin(phase * 2f * Mathf.PI) * Envelope(t);
            }

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Pure sine tone at a fixed frequency with attack+decay envelope.
        /// </summary>
        private static AudioClip MakeTone(string name, float freq, float dur)
        {
            int     n    = Mathf.RoundToInt(dur * SampleRate);
            float[] data = new float[n];
            float   phase = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase  += freq / SampleRate;
                data[i] = Mathf.Sin(phase * 2f * Mathf.PI) * Envelope(t);
            }

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Sum of sine waves (one per frequency in <paramref name="freqs"/>), normalised
        /// so the peak stays at ±1, with attack+decay envelope.
        /// </summary>
        private static AudioClip MakeChord(string name, float[] freqs, float dur)
        {
            int     n      = Mathf.RoundToInt(dur * SampleRate);
            float[] data   = new float[n];
            float[] phases = new float[freqs.Length];

            float norm = 1f / freqs.Length;

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / n;
                float sum = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    phases[k] += freqs[k] / SampleRate;
                    sum        += Mathf.Sin(phases[k] * 2f * Mathf.PI);
                }
                data[i] = sum * norm * Envelope(t);
            }

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// White-noise burst with a punch (sharp attack, exponential tail).
        /// If <paramref name="toneFreq"/> > 0 a sine wave at that frequency is blended in
        /// (50 % noise / 50 % tone) to give the sound a woody or metallic character.
        /// </summary>
        private static AudioClip MakeNoiseBurst(string name, float dur, float toneFreq = 0f)
        {
            int     n     = Mathf.RoundToInt(dur * SampleRate);
            float[] data  = new float[n];
            float   phase = 0f;
            bool    blend = toneFreq > 0f;

            for (int i = 0; i < n; i++)
            {
                float t    = (float)i / n;
                float env  = PunchEnvelope(t);
                float noise = (Random.value * 2f - 1f) * env;

                if (blend)
                {
                    phase   += toneFreq / SampleRate;
                    float tone = Mathf.Sin(phase * 2f * Mathf.PI) * env;
                    data[i] = (noise + tone) * 0.5f;
                }
                else
                {
                    data[i] = noise;
                }
            }

            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ── Envelope functions ────────────────────────────────────────────────────

        /// <summary>Attack 8% → exponential decay to silence. Range [0,1].</summary>
        private static float Envelope(float t)
        {
            const float attackFrac = 0.08f;
            if (t < attackFrac)
                return t / attackFrac;
            float decayT = (t - attackFrac) / (1f - attackFrac); // 0→1 in decay phase
            return Mathf.Exp(-4f * decayT);                       // fast-ish exponential
        }

        /// <summary>Sharp punch: instantaneous attack then exponential decay. Used for noise bursts.</summary>
        private static float PunchEnvelope(float t)
        {
            return Mathf.Exp(-6f * t);
        }
    }
}
