using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Attach to the door GameObject (which has a Box Collider set to Is Trigger).
    /// When the XR rig walks into the collider, fades to black and loads StargazingScene.
    /// </summary>
    public class DoorPortal : MonoBehaviour
    {
        [Tooltip("Exact name of the scene to load (must be in Build Settings).")]
        public string targetScene = "StargazingScene";

        [Tooltip("Seconds for the fade-to-black before scene loads.")]
        public float fadeDuration = 1.2f;

        private bool _triggered = false;

        // ── Trigger ───────────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            // Accept the XR rig root, any child of it, or anything tagged Player
            if (!IsPlayer(other)) return;

            _triggered = true;
            StartCoroutine(FadeAndLoad());
        }

        private static bool IsPlayer(Collider col)
        {
            // Tag check
            if (col.CompareTag("Player")) return true;

            // XR rig has the main Camera as a child — any collider on the rig or its children qualifies
            if (col.GetComponentInParent<Camera>() != null) return true;

            // Name-based fallback — XR Origin root is usually called "XR Origin" or "XR Rig"
            Transform t = col.transform;
            while (t != null)
            {
                if (t.name.Contains("XR Origin") || t.name.Contains("XR Rig")) return true;
                t = t.parent;
            }
            return false;
        }

        // ── Fade & load ───────────────────────────────────────────────────────────

        private IEnumerator FadeAndLoad()
        {
            // Build a full-screen black overlay on top of everything
            var overlay = BuildFadeOverlay();
            var cg      = overlay.GetComponent<CanvasGroup>();

            // Fade in (black)
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed   += Time.deltaTime;
                cg.alpha   = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            cg.alpha = 1f;

            // Load the target scene
            SceneManager.LoadScene(targetScene);
        }

        private static GameObject BuildFadeOverlay()
        {
            var go     = new GameObject("[PortalFade]");
            DontDestroyOnLoad(go);

            var canvas          = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            go.AddComponent<UnityEngine.UI.CanvasScaler>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform, false);

            var img   = panel.AddComponent<UnityEngine.UI.Image>();
            img.color = Color.black;

            var rt         = panel.GetComponent<RectTransform>();
            rt.anchorMin   = Vector2.zero;
            rt.anchorMax   = Vector2.one;
            rt.sizeDelta   = Vector2.zero;

            var cg   = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            return go;
        }
    }
}
