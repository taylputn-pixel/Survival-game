using UnityEngine;

namespace GrandmasHouse.Data
{
    // How the hens live: what they eat and drink, how long they last
    // without it, what the fox does, and how they move about the run.
    [CreateAssetMenu(fileName = "FlockSettings", menuName = "Grandma's House/Flock Settings")]
    public class FlockSettings : ScriptableObject
    {
        [Header("Each night")]
        [Tooltip("Feeder a hen eats in a day (the feeder holds 1).")]
        public float feedPerHen = 0.1f;
        [Tooltip("Waterer a hen drinks in a day (the waterer holds 1).")]
        public float waterPerHen = 0.1f;
        [Tooltip("Days a hen goes without food or water before she dies.")]
        public int daysToStarve = 3;
        [Tooltip("Eggs a nest box holds before the hens stop laying in it.")]
        public int eggsPerNest = 3;
        [Tooltip("Chance a fed hen lays overnight, by month (spring, summer, fall, winter). Short cold days mean fewer eggs.")]
        public float[] layChance = { 1f, 1f, 0.6f, 0.25f };

        [Header("Chicks")]
        [Tooltip("Fed nights before a chick is a grown hen and starts laying.")]
        public int daysToLay = 5;
        [Tooltip("A chick's share of a hen's feed and water each night.")]
        [Range(0f, 1f)] public float chickShare = 0.5f;
        [Tooltip("How big a chick is next to a hen.")]
        public float chickScale = 0.5f;
        public Color chickColor = new Color(1f, 0.86f, 0.35f);

        [Header("The fox")]
        [Tooltip("Chance each night that the fox takes a hen when the coop door is left open.")]
        [Range(0f, 1f)] public float foxChanceDoorOpen = 0.35f;
        [Tooltip("Chance each night that the fox takes a hen shut outside the coop.")]
        [Range(0f, 1f)] public float foxChanceShutOut = 0.5f;

        [Header("Feeding")]
        [Tooltip("Feeder filled by one scoop from the feed sack.")]
        public float scoop = 0.25f;
        [Tooltip("Feeder filled by produce tossed in, per point of hunger it would fill a person.")]
        public float feedPerFood = 0.025f;
        [Tooltip("Feeder filled by produce gone bad (it's all the same to a hen).")]
        public float spoiledFeed = 0.1f;
        [Tooltip("Feeder filled per hornworm from the treat pail.")]
        public float feedPerWorm = 0.03f;
        [Tooltip("Waterer filled by a full watering can.")]
        public float waterPerCan = 1f;

        [Header("Moving about")]
        public float walkSpeed = 0.35f;
        [Tooltip("Chance, each time a hen picks somewhere new, that she heads out through the run gate if it's open.")]
        [Range(0f, 1f)] public float wanderOutChance = 0.2f;
        [Tooltip("How far (m) a loose hen strays from where she was set down or got out.")]
        public float looseRange = 3f;
        [Tooltip("Seconds a hen stands pecking before she wanders on, at random in this range.")]
        public Vector2 peckSeconds = new Vector2(1.5f, 5f);

        public float LayChance(int month) =>
            layChance == null || layChance.Length == 0 ? 1f : layChance[Mathf.Clamp(month, 0, layChance.Length - 1)];
    }
}
