using System;
using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    public enum PartOfDay { Morning, Afternoon, Evening, Night }

    // Keeps the time of day. It runs slowly from dawn to nightfall, then
    // stops and waits for you to go to bed. There are no deadlines.
    public class DayClock : MonoBehaviour, ISaveable
    {
        [SerializeField] TimeSettings settings;
        [SerializeField] ModeProfile mode;
        [Tooltip("The hour the game starts at.")]
        [SerializeField] float startHour = 7f;

        // Set by a station while you're working at it.
        public bool PausedForWork { get; set; }

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
            if (IsNight || (PausedForWork && mode.pauseClockAtStations))
            {
                return;
            }

            float daylightHours = settings.nightfallHour - settings.dawnHour;
            float hoursPerSecond = daylightHours / (settings.realMinutesPerDay * 60f);
            Hour = Mathf.Min(Hour + hoursPerSecond * settings.testSpeedMultiplier * Time.deltaTime, settings.nightfallHour);
        }

        [Serializable]
        class State
        {
            public int day;
            public float hour;
        }

        public string SaveKey => "clock";
        public string Capture() => JsonUtility.ToJson(new State { day = Day, hour = Hour });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Day = state.day;
            Hour = state.hour;
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
