using System.Collections.Generic;
using UnityEngine;

namespace CelestiaVR.Island
{
    /// <summary>
    /// Finds every Wall_N GameObject in the scene, hides its renderer, and enforces
    /// solid collision so the player can never walk through.
    ///
    /// Two-layer defence:
    ///  1. FattenCollider  — expands any thin BoxCollider to ≥1 m world-space thickness
    ///                       so fast movement can't tunnel through a 0.11 m slab.
    ///  2. LateUpdate push — after all XRI locomotion has run, ComputePenetration
    ///                       checks whether the XR Origin's CharacterController overlaps
    ///                       any wall and pushes it out.  Works even when XRI moves the
    ///                       rig via transform.position directly (bypassing CharacterController).
    /// </summary>
    public class IslandBoundaryWalls : MonoBehaviour
    {
        private Transform         _xrOrigin;
        private CharacterController _cc;
        private BoxCollider[]     _walls;

        private void Start()
        {
            _xrOrigin = FindXROrigin();
            if (_xrOrigin != null)
                _cc = _xrOrigin.GetComponent<CharacterController>();

            var found = new List<BoxCollider>();

            for (int i = 1; i <= 20; i++)           // scan Wall_1 … Wall_20
            {
                var go = GameObject.Find($"Wall_{i}");
                if (go == null) continue;

                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;

                var col = go.GetComponent<BoxCollider>();
                if (col == null) col = go.AddComponent<BoxCollider>();

                FattenCollider(col);
                found.Add(col);
            }

            _walls = found.ToArray();
            Debug.Log($"[IslandBoundaryWalls] {_walls.Length} walls active (invisible, collision-enforced).");
        }

        private void LateUpdate()
        {
            if (_xrOrigin == null || _cc == null || _walls.Length == 0) return;

            foreach (var wall in _walls)
            {
                if (wall == null) continue;

                if (Physics.ComputePenetration(
                        _cc,   _xrOrigin.position, _xrOrigin.rotation,
                        wall,  wall.transform.position, wall.transform.rotation,
                        out Vector3 pushDir, out float pushDist))
                {
                    _xrOrigin.position += pushDir * pushDist;
                }
            }
        }

        // Ensures the world-space thickness of the collider is at least 1 m on every axis.
        // A cube scaled to (35, 6, 0.11) has a 0.11 m thick Z face — below the
        // CharacterController's movement step, so it gets tunnelled at walking speed.
        private static void FattenCollider(BoxCollider col)
        {
            const float minWorldThickness = 1f;
            var  scale = col.transform.lossyScale;
            var  size  = col.size;

            if (Mathf.Abs(scale.x) > 0) size.x = Mathf.Max(size.x, minWorldThickness / Mathf.Abs(scale.x));
            if (Mathf.Abs(scale.y) > 0) size.y = Mathf.Max(size.y, minWorldThickness / Mathf.Abs(scale.y));
            if (Mathf.Abs(scale.z) > 0) size.z = Mathf.Max(size.z, minWorldThickness / Mathf.Abs(scale.z));

            col.size = size;
        }

        private static Transform FindXROrigin()
        {
            foreach (var n in new[] {
                "XR Origin Hands (XR Rig)", "XR Origin (XR Rig)", "XR Origin" })
            {
                var go = GameObject.Find(n);
                if (go != null) return go.transform;
            }
            return null;
        }
    }
}
