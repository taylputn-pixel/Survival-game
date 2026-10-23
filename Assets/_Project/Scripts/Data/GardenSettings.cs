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

        [Header("Neglect (where crops can die)")]
        [Tooltip("Nights in a row a growing plant can go to bed dry: the first one wilts it, this many kills it.")]
        public int dryNightsToDie = 2;
        [Tooltip("Nights in a row a plant can go to bed with pests on it: the first one chews it, this many kills it.")]
        public int pestNightsToDie = 2;
        public Color wiltedColor = new Color(0.62f, 0.56f, 0.3f);
        [Range(0f, 1f)] public float wiltedTint = 0.45f;
        [Tooltip("How far a wilted plant leans over, in degrees.")]
        public float wiltedLean = 14f;
        public Color chewedColor = new Color(0.8f, 0.75f, 0.25f);
        [Range(0f, 1f)] public float chewedTint = 0.4f;
        public Color deadColor = new Color(0.33f, 0.23f, 0.13f);
        [Range(0f, 1f)] public float deadTint = 0.85f;
        public float deadLean = 30f;

        [Header("Suckers and pests")]
        [Tooltip("New suckers on each plant that grows them, each night.")]
        public int suckersPerNight = 1;
        [Tooltip("A companion plant this close draws pests away, in meters (reaches the spots beside and diagonal).")]
        public float companionRadius = 0.85f;

        [Header("Weeds")]
        [Tooltip("Weeds already in the bed on the first day (the garden sat untended).")]
        public int startingWeeds = 4;
        [Tooltip("New weeds that come up each night.")]
        public int weedsPerNight = 2;
        public int maxWeeds = 10;
        [Tooltip("How far weeds keep from planting spots and each other, in meters.")]
        public float weedSpacing = 0.12f;
        [Tooltip("How far you drag the mouse back (in pixels) to pull a young weed out by the root. Older weeds take a little more.")]
        public float pullMouseDistance = 260f;
        [Tooltip("How long the stick held fully back takes to pull a young weed, in seconds. Older weeds take a little longer.")]
        public float pullStickSeconds = 0.8f;
        [Tooltip("How fast a stretched weed settles back while you hold it without pulling (fraction of a full pull per second).")]
        public float pullSlackPerSecond = 0.3f;
        [Tooltip("How much a weed's leafy top stretches just before it pops free (1 = twice its height).")]
        public float pullStretch = 1.2f;
        [Tooltip("Weeds get bigger each night they're left, up to this many nights.")]
        public int weedMaxAge = 3;

        [Header("Soil")]
        public Color wetSoilColor = new Color(0.17f, 0.11f, 0.07f);
    }
}
