using UnityEngine;

namespace GrandmasHouse.Data
{
    // Star Valley weather through the year, in degrees Fahrenheit. Each month
    // of the calendar (in order) has a typical high and low; days wander
    // around them, and a winter month can bring a cold snap that drops
    // everything well below. Temperatures blend from one month into the next.
    [CreateAssetMenu(menuName = "Grandma's House/Weather Settings")]
    public class WeatherSettings : ScriptableObject
    {
        [Tooltip("Typical afternoon high for each calendar month, in order.")]
        public float[] monthHigh = { 55f, 80f, 52f, 24f };
        [Tooltip("Typical early-morning low for each calendar month, in order.")]
        public float[] monthLow = { 30f, 50f, 26f, 0f };
        [Tooltip("How far a day can wander above or below the month's usual.")]
        public float dailyWander = 8f;

        [Header("Cold snaps")]
        [Tooltip("Chance each day of a cold snap starting, for each calendar month.")]
        public float[] snapChance = { 0f, 0f, 0.02f, 0.12f };
        [Tooltip("How many degrees colder a cold snap is.")]
        public float snapDrop = 25f;
        public int snapDaysMin = 2;
        public int snapDaysMax = 4;

        [Header("The day")]
        [Tooltip("Hour of the day's low (just before dawn) and high (mid-afternoon).")]
        public float coldestHour = 6f;
        public float warmestHour = 15f;

        [Header("Frost")]
        [Tooltip("At or below this, plants left out in the garden die.")]
        public float killingFrost = 28f;
        [Tooltip("A day whose high doesn't get above this has frozen ground: nothing can be planted.")]
        public float groundFrozenBelow = 36f;

        [Header("Shelter")]
        [Tooltip("How many degrees warmer than outside a house holds with no fire.")]
        public float houseHolds = 20f;
        [Tooltip("Degrees a well-fed wood stove adds to the house.")]
        public float stoveHeat = 45f;
        [Tooltip("Degrees warmer still, sitting close to a lit stove.")]
        public float besideStove = 20f;
        public float besideStoveRadius = 2.5f;
        [Tooltip("Degrees warmer under the blankets in bed.")]
        public float inBed = 15f;

        [Header("The radio")]
        public int forecastDays = 3;

        public float High(int month) => At(monthHigh, month);
        public float Low(int month) => At(monthLow, month);
        public float SnapChance(int month) => At(snapChance, month);

        static float At(float[] values, int index) =>
            values == null || values.Length == 0 ? 0f : values[Mathf.Clamp(index, 0, values.Length - 1)];
    }
}
