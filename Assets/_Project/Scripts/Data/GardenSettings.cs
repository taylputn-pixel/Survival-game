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
        public float pourRadius = 0.18f;
        [Tooltip("How high above the soil the spout is held while pouring, in meters.")]
        public float pourHeight = 0.22f;
        [Tooltip("How far the can tips forward to pour, in degrees.")]
        public float pourTilt = 45f;
        public float canFollowSharpness = 14f;

        [Header("Growing")]
        [Tooltip("How watered a spot must be by nightfall for its plant to grow that night (1 = fully).")]
        public float wateredEnough = 0.5f;
        [Tooltip("Weeds closer than this crowd a plant and it loses its tended bonus, in meters.")]
        public float crowdingRadius = 0.25f;

        [Header("Weeds")]
        [Tooltip("Weeds already in the bed on the first day (the garden sat untended).")]
        public int startingWeeds = 4;
        [Tooltip("New weeds that come up each night.")]
        public int weedsPerNight = 2;
        public int maxWeeds = 10;
        [Tooltip("How far weeds keep from planting spots and each other, in meters.")]
        public float weedSpacing = 0.12f;
        [Tooltip("How far you pull back to get a young weed out by the root, in screen heights. Older weeds take a little more.")]
        public float pullDistance = 0.1f;
        [Tooltip("Weeds get bigger each night they're left, up to this many nights.")]
        public int weedMaxAge = 3;

        [Header("Soil")]
        public Color wetSoilColor = new Color(0.17f, 0.11f, 0.07f);
    }
}
