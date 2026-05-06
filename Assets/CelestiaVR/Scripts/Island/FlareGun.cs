using UnityEngine;
using UnityEngine.InputSystem;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Attached to the flare gun which is parented directly to the right hand controller.
    /// No grab interaction needed — the gun is always "held".
    ///
    /// • Parabolic aim line is drawn from the muzzle every frame.
    /// • Right trigger fires one flare projectile.
    /// • 3-second muzzle effect plays on fire (burst flash + sustained smoke).
    /// • After 3 s the projectile self-destructs and ResetFired() allows another shot.
    ///
    /// muzzlePoint and flareProjectilePrefab are injected by FireplaceBootstrap.
    /// </summary>
    public class FlareGun : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Tooltip("Child transform at the barrel tip — set by FireplaceBootstrap.")]
        public Transform muzzlePoint;

        [Tooltip("Launch speed in m/s.")]
        public float launchSpeed = 22f;

        [Tooltip("Prototype flare GameObject — set by FireplaceBootstrap.")]
        public GameObject flareProjectilePrefab;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private InputAction    _triggerAction;
        private bool           _hasFired = false;
        private LineRenderer   _aimLine;
        private ParticleSystem _firingEffect;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildAimLine();
        }

        private void Start()
        {
            // A button (primaryButton) fires the gun
            _triggerAction = new InputAction("FlareGunFire", InputActionType.Button,
                binding: "<XRController>{RightHand}/primaryButton");
            _triggerAction.performed += OnTriggerPerformed;
            _triggerAction.Enable();

            if (muzzlePoint != null)
                BuildFiringEffect();
        }

        private void OnDisable()
        {
            _triggerAction?.Disable();
        }

        private void OnDestroy()
        {
            _triggerAction?.Disable();
            _triggerAction?.Dispose();
        }

        private void Update()
        {
            if (_aimLine == null || muzzlePoint == null) return;

            bool showAim = !_hasFired;
            _aimLine.enabled = showAim;
            if (showAim) UpdateAimTrajectory();
        }

        // ── Trigger ───────────────────────────────────────────────────────────────

        private void OnTriggerPerformed(InputAction.CallbackContext _)
        {
            if (_hasFired || flareProjectilePrefab == null || muzzlePoint == null) return;
            _hasFired = true;
            Fire();
        }

        private void Fire()
        {
            if (_aimLine != null) _aimLine.enabled = false;

            // Play 3-second muzzle effect
            if (_firingEffect != null)
            {
                _firingEffect.transform.SetPositionAndRotation(
                    muzzlePoint.position, muzzlePoint.rotation);
                _firingEffect.Play();
            }

            var flareGO = Instantiate(flareProjectilePrefab, muzzlePoint.position, muzzlePoint.rotation);
            flareGO.SetActive(true);

            var proj = flareGO.GetComponent<FlareProjectile>();
            if (proj != null) proj.sourceGun = this;

            if (flareGO.TryGetComponent<Rigidbody>(out var rb))
                rb.AddForce(muzzlePoint.forward * launchSpeed, ForceMode.VelocityChange);

            Debug.Log("[FlareGun] Fired!");
        }

        /// <summary>Called by FlareProjectile when it expires without hitting the fireplace.</summary>
        public void ResetFired()
        {
            _hasFired = false;
            Debug.Log("[FlareGun] Ready to fire again.");
        }

        // ── Aim trajectory ────────────────────────────────────────────────────────

        private void BuildAimLine()
        {
            _aimLine               = gameObject.AddComponent<LineRenderer>();
            _aimLine.positionCount = 35;
            _aimLine.startWidth    = 0.016f;
            _aimLine.endWidth      = 0.003f;
            _aimLine.useWorldSpace = true;

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0f),
                         new GradientColorKey(new Color(1f, 0.3f, 0f),   1f) },
                new[] { new GradientAlphaKey(0.9f, 0f),
                         new GradientAlphaKey(0f,   1f) });
            _aimLine.colorGradient = grad;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.1f, 0.9f));
            _aimLine.material = mat;
            _aimLine.enabled  = false;
        }

        private void UpdateAimTrajectory()
        {
            const int   Steps    = 35;
            const float TimeStep = 0.07f;

            _aimLine.positionCount = Steps;
            var pos = muzzlePoint.position;
            var vel = muzzlePoint.forward * launchSpeed;

            for (int i = 0; i < Steps; i++)
            {
                _aimLine.SetPosition(i, pos);
                pos += vel * TimeStep + 0.5f * Physics.gravity * (TimeStep * TimeStep);
                vel += Physics.gravity * TimeStep;
            }
        }

        // ── Muzzle firing effect ──────────────────────────────────────────────────

        private void BuildFiringEffect()
        {
            var go = new GameObject("FiringEffect");
            go.transform.SetParent(muzzlePoint, false);
            go.transform.localPosition = Vector3.zero;

            _firingEffect = go.AddComponent<ParticleSystem>();

            // Unity auto-plays ParticleSystem on AddComponent — stop it before setting properties
            // otherwise "Setting duration while playing" error fires.
            _firingEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main             = _firingEffect.main;
            main.playOnAwake     = false;
            main.loop            = false;
            main.duration        = 3f;   // visual stops after 3 seconds
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.1f, 0.7f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.4f, 3f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startColor      = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.65f, 0.1f, 1f),
                new Color(0.55f, 0.55f, 0.55f, 0.5f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.15f);
            main.maxParticles    = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = _firingEffect.emission;
            em.rateOverTime = 30f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });

            var sh = _firingEffect.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 22f;
            sh.radius    = 0.01f;

            var col     = _firingEffect.colorOverLifetime;
            col.enabled = true;
            var grad    = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.7f, 0.1f),   0f),
                         new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 0.5f),
                         new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f),
                         new GradientAlphaKey(0.6f, 0.4f),
                         new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend        = _firingEffect.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            var mat         = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetColor("_BaseColor", Color.white);
            rend.material   = mat;
        }
    }
}
