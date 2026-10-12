using UnityEngine;

namespace GrandmasHouse.Data
{
    // The shape of a day: when it starts, when night falls, and how long the
    // daylight hours last in real time. Kept in an asset for tuning.
    [CreateAssetMenu(fileName = "TimeSettings", menuName = "Grandma's House/Time Settings")]
    public class TimeSettings : ScriptableObject
    {
        [Header("Day")]
        [Tooltip("Real minutes from dawn to nightfall.")]
        [Range(5f, 120f)] public float realMinutesPerDay = 45f;
        [Range(4f, 8f)] public float dawnHour = 6f;
        [Tooltip("Time stops here and waits until you go to bed.")]
        [Range(19f, 23f)] public float nightfallHour = 21f;
        [Tooltip("The hour you wake up after sleeping.")]
        [Range(5f, 9f)] public float wakeHour = 6.5f;

        [Header("Parts of the day (start hours)")]
        public float afternoonHour = 12f;
        public float eveningHour = 17f;

        [Header("Sun")]
        [Tooltip("Highest angle of the sun at noon, in degrees above the horizon.")]
        [Range(30f, 80f)] public float noonElevation = 58f;
        public Gradient sunColor = DefaultSunColor();
        [Tooltip("Sunlight strength across the day (0 = dawn, 1 = nightfall).")]
        public AnimationCurve sunIntensity = new AnimationCurve(
            new Keyframe(0f, 0.15f), new Keyframe(0.15f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.1f));
        [Tooltip("When twilight begins, as a share of the daylight hours (0.88 is about 19:15). From here the light blends smoothly into night.")]
        [Range(0.7f, 0.98f)] public float twilightStart = 0.88f;
        public Color moonColor = new Color(0.45f, 0.55f, 0.85f);
        [Range(0f, 0.5f)] public float moonIntensity = 0.12f;

        [Header("Sky and ambient light")]
        [Tooltip("Sky brightness across the day (0 = dawn, 1 = nightfall).")]
        public AnimationCurve skyExposure = new AnimationCurve(
            new Keyframe(0f, 0.45f), new Keyframe(0.15f, 1.3f), new Keyframe(0.8f, 1.3f), new Keyframe(1f, 0.35f));
        [Range(0f, 1f)] public float nightSkyExposure = 0.06f;
        [Tooltip("Overall fill light across the day (0 = dawn, 1 = nightfall).")]
        public AnimationCurve ambientIntensity = new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0.5f));
        [Range(0f, 1f)] public float nightAmbientIntensity = 0.3f;

        [Header("Testing")]
        [Tooltip("Speeds the clock up for testing. Keep at 1 for real play.")]
        [Range(1f, 120f)] public float testSpeedMultiplier = 1f;

        static Gradient DefaultSunColor()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.62f, 0.38f), 0f),
                    new GradientColorKey(new Color(1f, 0.93f, 0.82f), 0.2f),
                    new GradientColorKey(new Color(1f, 0.97f, 0.92f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.65f), 0.8f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.32f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
