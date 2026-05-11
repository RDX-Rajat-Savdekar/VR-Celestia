using UnityEngine;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Prevents the XR Origin from sinking below the island surface.
    ///
    /// The island GLB has no physics mesh, so CharacterController and any direct
    /// transform-position locomotion can both fall through.  This guard records the
    /// XR Origin's Y at scene start (i.e. wherever the designer placed it — that IS
    /// the island floor) and hard-clamps it every LateUpdate, after all XRI locomotion
    /// has been applied for the frame.
    /// </summary>
    public class IslandFloorGuard : MonoBehaviour
    {
        // Set by StargazingSceneBootstrap — overrides auto-detection when non-zero.
        [HideInInspector] public float overrideFloorY = float.MinValue;

        private Transform _xrOrigin;
        private float     _floorY;

        private const float SpawnHeightAboveFloor = 1.5f;

        private void Start()
        {
            _xrOrigin = FindXROriginTransform();

            if (overrideFloorY > float.MinValue)
            {
                _floorY = overrideFloorY;
            }
            else if (_xrOrigin != null)
            {
                _floorY = _xrOrigin.position.y;
            }

            // Lift the player above the island surface so they aren't buried in the ground
            if (_xrOrigin != null)
            {
                var spawnPos = _xrOrigin.position;
                spawnPos.y = _floorY + SpawnHeightAboveFloor;
                _xrOrigin.position = spawnPos;
            }

            Debug.Log($"[IslandFloorGuard] Floor at Y={_floorY:F3}, spawn at Y={_floorY + SpawnHeightAboveFloor:F3}");
        }

        private void LateUpdate()
        {
            if (_xrOrigin == null) return;
            var pos = _xrOrigin.position;
            if (pos.y < _floorY)
            {
                pos.y = _floorY;
                _xrOrigin.position = pos;
            }
        }

        private static Transform FindXROriginTransform()
        {
            foreach (var name in new[] {
                "XR Origin Hands (XR Rig)", "XR Origin (XR Rig)", "XR Origin" })
            {
                var go = GameObject.Find(name);
                if (go != null) return go.transform;
            }
            return null;
        }
    }
}
