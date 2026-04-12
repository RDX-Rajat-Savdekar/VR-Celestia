using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Attach to the flare_gun GLB instance. Fires a flare projectile when
    /// the right controller trigger is pressed while the gun is held.
    ///
    /// Trigger action is only enabled while the gun is grabbed, avoiding
    /// conflicts with SelectionManager which also uses the right trigger.
    ///
    /// After firing, the gun is "spent". If the flare misses and self-destructs,
    /// FlareProjectile calls ResetFired() so the player can try again.
    ///
    /// References are injected by FireplaceBootstrap.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class FlareGun : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Tooltip("Child transform at the barrel tip — set by FireplaceBootstrap.")]
        public Transform muzzlePoint;

        [Tooltip("Launch speed in m/s (ForceMode.VelocityChange).")]
        public float launchSpeed = 12f;

        [Tooltip("Prototype flare GameObject — set by FireplaceBootstrap.")]
        public GameObject flareProjectilePrefab;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private XRGrabInteractable _grab;
        private InputAction        _triggerAction;
        private bool               _isHeld   = false;
        private bool               _hasFired = false;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            // Build trigger action — identical path to SelectionManager's trigger,
            // but this action is only .Enable()'d while the gun is held.
            _triggerAction = new InputAction("FlareGunFire", InputActionType.Button,
                binding: "<XRController>{RightHand}/triggerButton");
            _triggerAction.performed += OnTriggerPerformed;
            // Do NOT enable here — enabled inside OnGrabbed
        }

        private void Start()
        {
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnDropped);
        }

        private void OnDisable()
        {
            _triggerAction?.Disable();
        }

        private void OnDestroy()
        {
            if (_grab != null)
            {
                _grab.selectEntered.RemoveListener(OnGrabbed);
                _grab.selectExited.RemoveListener(OnDropped);
            }
            _triggerAction?.Disable();
            _triggerAction?.Dispose();
        }

        // ── Grab events ───────────────────────────────────────────────────────────

        private void OnGrabbed(SelectEnterEventArgs _)
        {
            _isHeld = true;
            _triggerAction.Enable();
        }

        private void OnDropped(SelectExitEventArgs _)
        {
            _isHeld = false;
            _triggerAction.Disable();
        }

        // ── Trigger ───────────────────────────────────────────────────────────────

        private void OnTriggerPerformed(InputAction.CallbackContext _)
        {
            if (!_isHeld || _hasFired) return;
            if (flareProjectilePrefab == null || muzzlePoint == null) return;

            _hasFired = true;
            Fire();
        }

        private void Fire()
        {
            // Activate a copy of the prototype
            var flareGO = Instantiate(flareProjectilePrefab, muzzlePoint.position, muzzlePoint.rotation);
            flareGO.SetActive(true);

            // Inject back-reference so the projectile can reset us on miss
            var proj = flareGO.GetComponent<FlareProjectile>();
            if (proj != null) proj.sourceGun = this;

            // Launch
            if (flareGO.TryGetComponent<Rigidbody>(out var rb))
                rb.AddForce(muzzlePoint.forward * launchSpeed, ForceMode.VelocityChange);

            Debug.Log("[FlareGun] Fired!");
        }

        // ── Public ────────────────────────────────────────────────────────────────

        /// <summary>Called by FlareProjectile when it self-destructs without hitting the fireplace.</summary>
        public void ResetFired()
        {
            _hasFired = false;
            Debug.Log("[FlareGun] Missed — ready to fire again.");
        }
    }
}
