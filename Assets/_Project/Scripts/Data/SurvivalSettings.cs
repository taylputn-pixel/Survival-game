using UnityEngine;

namespace GrandmasHouse.Data
{
    // The numbers behind hunger, rest, sickness and starving, all in
    // in-game hours. Hunger and rest run from 100 (full, rested) to 0.
    [CreateAssetMenu(fileName = "SurvivalSettings", menuName = "Grandma's House/Survival Settings")]
    public class SurvivalSettings : ScriptableObject
    {
        [Header("Starting a new game")]
        [Range(0f, 100f)] public float startHunger = 80f;
        [Range(0f, 100f)] public float startRest = 90f;

        [Header("Hunger")]
        [Tooltip("Hunger lost per hour awake. 100 / 24 empties a full stomach in a day.")]
        public float hungerPerHour = 100f / 24f;
        [Tooltip("Asleep, hunger drops this much slower.")]
        [Range(0f, 1f)] public float hungerAsleep = 0.5f;
        [Tooltip("Below this you're hungry: your stomach growls.")]
        public float hungryBelow = 25f;

        [Header("Rest")]
        [Tooltip("Rest lost per hour awake. 100 / 18 means tired after a long day.")]
        public float restPerHour = 100f / 18f;
        [Tooltip("Rest gained per hour asleep. 100 / 8 is a full night's sleep.")]
        public float restAsleepPerHour = 100f / 8f;
        [Tooltip("Below this you start slowing down.")]
        public float tiredBelow = 30f;
        [Tooltip("Below this you're exhausted: no brisk walking.")]
        public float exhaustedBelow = 10f;
        [Tooltip("Walking speed at 0 rest, as a share of normal.")]
        [Range(0.3f, 1f)] public float exhaustedSpeed = 0.7f;
        [Tooltip("Below this you get clumsy and can drop what you carry.")]
        public float clumsyBelow = 20f;
        [Tooltip("At 0 rest, the chance each second of dropping what you hold while walking.")]
        [Range(0f, 0.2f)] public float dropChancePerSecond = 1f / 30f;

        [Header("Starving")]
        [Tooltip("Hours with an empty stomach before you're malnourished (slow, no brisk walking).")]
        public float malnourishedAfterHours = 24f;
        [Tooltip("Hours with an empty stomach before you collapse. Someone has to feed you; if everyone collapses, the game is over.")]
        [UnityEngine.Serialization.FormerlySerializedAs("deathAfterHours")] public float collapseAfterHours = 72f;
        [Tooltip("Starving hours that heal per hour once you've eaten.")]
        public float starvingRecoveryPerHour = 0.5f;
        [Range(0.3f, 1f)] public float malnourishedSpeed = 0.75f;

        [Header("Sickness")]
        [Tooltip("Hours of sickness from spoiled food.")]
        public float spoiledSickHours = 12f;
        [Tooltip("Hours of sickness from a jar that wasn't safe to eat.")]
        public float unsafeJarSickHours = 24f;
        [Tooltip("Hunger lost straight away when food makes you sick.")]
        public float sickHungerLoss = 15f;
        [Tooltip("While sick, hunger drops this many times faster.")]
        public float sickHungerMultiplier = 1.5f;
        [Range(0.3f, 1f)] public float sickSpeed = 0.85f;

        [Header("Sleep")]
        [Tooltip("In-game hours that pass per real second while asleep.")]
        public float sleepHoursPerSecond = 1.5f;
    }
}
