using UnityEngine;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Fired by FlareGun. Travels through the air and lights the FireplaceSite on contact.
    ///
    /// Injected with a back-reference to the FlareGun so it can reset the "hasFired"
    /// flag if it misses, allowing the player to try again.
    ///
    /// Created programmatically by FireplaceBootstrap as a disabled prototype.
    /// </summary>
    public class FlareProjectile : MonoBehaviour
    {
        [Header("Safety")]
        [Tooltip("Auto-destroy after this many seconds if no collision.")]
        public float lifetime = 8f;

        // Injected by FlareGun at Instantiate time
        [HideInInspector] public FlareGun sourceGun;

        private bool _hasTriggered = false;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }

        private void OnDestroy()
        {
            // If we're destroyed without triggering (miss), reset the gun so player can retry
            if (!_hasTriggered && sourceGun != null)
                sourceGun.ResetFired();
        }

        // ── Collision ─────────────────────────────────────────────────────────────

        private void OnCollisionEnter(Collision col)
        {
            if (_hasTriggered) return;

            // Find FireplaceSite on the collider or any parent
            var site = col.gameObject.GetComponent<FireplaceSite>()
                    ?? col.gameObject.GetComponentInParent<FireplaceSite>();

            if (site != null)
            {
                _hasTriggered = true;
                site.LightFire();
                SpawnImpactFlash(col.contacts[0].point);
                Destroy(gameObject);
            }
            else
            {
                // Hit something else — bounce but try again from ground
                // (projectile stays alive until lifetime expires)
            }
        }

        // ── Impact flash ──────────────────────────────────────────────────────────

        private static void SpawnImpactFlash(Vector3 pos)
        {
            var go = new GameObject("FlareImpact");
            go.transform.position = pos;

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop            = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor      = new Color(1f, 0.6f, 0.1f, 1f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.5f);
            main.maxParticles    = 30;

            var burst = ps.emission;
            burst.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            burst.rateOverTime = 0f;

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius    = 0.05f;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetColor("_BaseColor", new Color(1f, 0.6f, 0.1f, 1f));
            rend.material = mat;

            ps.Play();
            Destroy(go, 1.5f);
        }
    }
}
