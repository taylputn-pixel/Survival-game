using GrandmasHouse.Carrying;
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
        [Tooltip("What you pick: a cucumber, a bunch of dill.")]
        public Pickup harvest;
        [Tooltip("Picked once and the plant is done (dill). Otherwise it keeps producing (cucumbers).")]
        public bool singleHarvest;
        [Tooltip("New pickings that ripen each watered night once the plant is ready.")]
        public int harvestPerNight = 1;
        [Tooltip("Extra pickings each night when no weeds are crowding the plant.")]
        public int tendedBonus = 1;
    }
}
