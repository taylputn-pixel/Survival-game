using System;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    public enum PartOfDay { Morning, Afternoon, Evening, Night }

    // Keeps the time of day. It runs slowly from dawn to nightfall, then
    // stops and waits for you to go to bed. There are no deadlines.
    public class DayClock : MonoBehaviour
    {
        [SerializeField] TimeSettings settings;
        [Tooltip("The hour the game starts at.")]
        [SerializeField] float startHour = 7f;

        public float Hour { get; private set; }
        public int Day { get; private set; } = 1;
        public bool IsNight => Hour >= settings.nightfallHour;
        // 0 at dawn, 1 at nightfall.
        public float DaylightProgress => Mathf.InverseLerp(settings.dawnHour, settings.nightfallHour, Hour);
        public TimeSettings Settings => settings;

        public PartOfDay Part =>
            IsNight ? PartOfDay.Night
            : Hour >= settings.eveningHour ? PartOfDay.Evening
            : Hour >= settings.afternoonHour ? PartOfDay.Afternoon
            : PartOfDay.Morning;

        public event Action<int> NewDay;

        void Awake() => Hour = startHour;

        void Update()
        {
            if (IsNight)
            {
                return;
            }

            float daylightHours = settings.nightfallHour - settings.dawnHour;
            float hoursPerSecond = daylightHours / (settings.realMinutesPerDay * 60f);
            Hour = Mathf.Min(Hour + hoursPerSecond * settings.testSpeedMultiplier * Time.deltaTime, settings.nightfallHour);
        }

        // Called by the bed: a new day starts at wake-up time.
        public void SleepUntilMorning()
        {
            Day++;
            Hour = settings.wakeHour;
            NewDay?.Invoke(Day);
        }
    }
}
