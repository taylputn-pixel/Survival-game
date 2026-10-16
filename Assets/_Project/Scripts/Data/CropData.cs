using GrandmasHouse.Carrying;
using GrandmasHouse.Garden;
using UnityEngine;

namespace GrandmasHouse.Data
{
    // One kind of plant: what it's called and how it looks, grows, and yields.
    [CreateAssetMenu(fileName = "Crop", menuName = "Grandma's House/Crop")]
    public class CropData : ScriptableObject
    {
        public string displayName = "Cucumber";
        [Tooltip("The seed's color as it drops into the soil.")]
        public Color seedColor = new Color(0.93f, 0.88f, 0.7f);
        [Tooltip("Seed size, in meters.")]
        public float seedSize = 0.008f;

        [Header("Growing")]
        [Tooltip("How the plant looks at each stage, from sprout to ready. It moves up one stage each watered night. The last stage holds the harvest at children named \"HarvestPoint\".")]
        public GameObject[] stages;

        [Header("Harvest")]
        [Tooltip("What you pick: a cucumber, a bunch of dill. Leave empty for a plant grown for itself (marigolds).")]
        public Pickup harvest;
        [Tooltip("Picked once and the plant is done (dill). Otherwise it keeps producing (cucumbers).")]
        public bool singleHarvest;
        [Tooltip("New pickings that ripen each watered night once the plant is ready.")]
        public int harvestPerNight = 1;
        [Tooltip("Extra pickings each night when the plant is tended: no weeds crowding it, no suckers, no pests.")]
        public int tendedBonus = 1;

        [Header("Tending")]
        [Tooltip("A side shoot that sprouts overnight at stage children named \"SuckerPoint\", to pinch off (tomatoes). Leave empty if the crop doesn't need it.")]
        public Pinchable sucker;
        [Tooltip("Most suckers on one plant at a time.")]
        public int maxSuckers = 3;
        [Tooltip("A pest that turns up overnight at stage children named \"PestPoint\" (hornworms on tomatoes). Leave empty if none.")]
        public Pinchable pest;
        [Tooltip("Chance each night that a pest turns up on the plant.")]
        [Range(0f, 1f)] public float pestChance = 0.4f;
        [Tooltip("Plants that draw this crop's pests away when blooming nearby (marigolds by tomatoes).")]
        public CropData[] companions;
        [Tooltip("Chance of a pest each night with a companion blooming nearby.")]
        [Range(0f, 1f)] public float companionPestChance = 0.05f;
    }
}
