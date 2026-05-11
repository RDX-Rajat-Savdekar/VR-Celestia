using UnityEngine;
using CelestiaVR.Core;
using CelestiaVR.Island;

namespace CelestiaVR.Environment
{
    /// <summary>
    /// Spawns the Gray Volume Fog prefab when the fireplace is lit at night.
    /// Destroys it when the fire goes out or when it becomes daytime.
    /// </summary>
    public class FireCampGlowDimmer : MonoBehaviour
    {
        [Tooltip("The Gray Volume Fog prefab from Assets/Vefects/Free Fire VFX URP/Fog.")]
        public GameObject fogPrefab;

        private FireplaceSite _fireplaceSite;
        private GameObject    _fogInstance;

        private void Update()
        {
            if (_fireplaceSite == null)
                _fireplaceSite = FindFirstObjectByType<FireplaceSite>();

            bool isNight = SkyManager.Instance == null
                || SkyManager.Instance.GetSunAltitudeDegrees() < 0f;
            bool fireLit = _fireplaceSite != null
                && _fireplaceSite.CurrentState == FireplaceSite.State.Lit;

            bool shouldShow = isNight && fireLit;
            bool fogExists  = _fogInstance != null;

            if (shouldShow && !fogExists)
            {
                if (fogPrefab != null)
                    _fogInstance = Instantiate(fogPrefab);
            }
            else if (!shouldShow && fogExists)
            {
                Destroy(_fogInstance);
                _fogInstance = null;
            }
        }
    }
}
