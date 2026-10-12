using UnityEngine;

namespace GrandmasHouse.Data
{
    // Tuning for garden work: watering, and later growth and weeds.
    [CreateAssetMenu(fileName = "GardenSettings", menuName = "Grandma's House/Garden Settings")]
    public class GardenSettings : ScriptableObject
    {
        [Header("Watering can")]
        [Tooltip("How long a full can pours before it runs dry, in seconds.")]
        public float canPourSeconds = 12f;
        [Tooltip("How long filling an empty can at the spigot takes, in seconds.")]
        public float canFillSeconds = 1.6f;
        [Tooltip("How fast a spot soaks up water while you pour on it (1 = fully watered).")]
        public float waterPerSecond = 0.9f;
        [Tooltip("How far from where the water lands a spot still gets watered, in meters.")]
        public float pourRadius = 0.14f;
        [Tooltip("How high above the soil the spout is held while pouring, in meters.")]
        public float pourHeight = 0.22f;
        [Tooltip("How far the can tips forward to pour, in degrees.")]
        public float pourTilt = 45f;
        public float canFollowSharpness = 14f;

        [Header("Soil")]
        public Color wetSoilColor = new Color(0.17f, 0.11f, 0.07f);
    }
}
