using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Thin component on each collectible stick. Works with XRGrabInteractable.
    ///
    /// When the stick is released within snapRadius of the FireplaceSite, it
    /// snaps into position and registers the deposit.
    ///
    /// References are injected by FireplaceBootstrap at spawn time.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public class StickCollectible : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("References (set by FireplaceBootstrap)")]
        public FireplaceSite targetSite;

        [Tooltip("Local offset relative to site centre where this stick snaps — set by Bootstrap so sticks pile without overlapping.")]
        public Vector3 snapOffset;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private XRGrabInteractable _grab;
        private Rigidbody          _rb;
        private bool               _deposited = false;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _rb   = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            targetSite?.RegisterStick(this);
            _grab.selectExited.AddListener(OnReleased);
        }

        private void OnDestroy()
        {
            if (_grab != null)
                _grab.selectExited.RemoveListener(OnReleased);
        }

        private void Update()
        {
            // Backup: a rolling stick that comes to rest inside the zone still counts.
            if (_deposited || _rb.isKinematic || targetSite == null) return;
            if (targetSite.CurrentState >= FireplaceSite.State.Built) return;

            if (Vector3.Distance(transform.position, targetSite.transform.position)
                <= targetSite.snapRadius)
            {
                Deposit();
            }
        }

        // ── Handlers ──────────────────────────────────────────────────────────────

        private void OnReleased(SelectExitEventArgs _)
        {
            if (_deposited || targetSite == null) return;
            if (targetSite.CurrentState >= FireplaceSite.State.Built) return;

            if (Vector3.Distance(transform.position, targetSite.transform.position)
                <= targetSite.snapRadius)
            {
                Deposit();
            }
        }

        // ── Deposit ───────────────────────────────────────────────────────────────

        private void Deposit()
        {
            _deposited = true;

            // Lock in place
            _grab.enabled           = false;
            _rb.isKinematic         = true;
            _rb.linearVelocity      = Vector3.zero;
            _rb.angularVelocity     = Vector3.zero;

            // Snap to pile position
            transform.position = targetSite.transform.position + snapOffset;
            transform.rotation = Quaternion.Euler(
                Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-10f, 10f));

            targetSite.OnStickDeposited();
        }
    }
}
