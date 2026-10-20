using System;
using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    public enum PartOfDay { Morning, Afternoon, Evening, Night }

    // Keeps the time of day. It runs slowly from dawn to nightfall; then,
    // depending on the mode, it either stops and waits for you to go to bed,
    // or keeps running through the night into a new day at midnight.
    public class DayClock : MonoBehaviour, ISaveable
    {
        [SerializeField] TimeSettings settings;
        [SerializeField] ModeProfile mode;
        [Tooltip("The hour the game starts at.")]
        [SerializeField] float startHour = 7f;

        // Set by a station while you're working at it.
        public bool PausedForWork { get; set; }

        // Set by the bed while you sleep: in-game hours per real second.
        public float FastForward { get; set; }

        public float Hour { get; private set; }
        public int Day { get; private set; } = 1;
        // Hours since the game began, for things that age (hunger, food).
        public float TotalHours => (Day - 1) * 24f + Hour;
        public ModeProfile Mode => mode;
        public bool IsNight => Hour >= settings.nightfallHour || (!mode.clockStopsAtNight && Hour < settings.dawnHour);
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
            if (FastForward > 0f)
            {
                Advance(FastForward * Time.deltaTime);
                return;
            }
            if (PausedForWork && mode.pauseClockAtStations)
            {
                return;
            }
            if (mode.clockStopsAtNight)
            {
                if (!IsNight)
                {
                    Hour = Mathf.Min(Hour + HoursPerSecond * Time.deltaTime, settings.nightfallHour);
                }
                return;
            }
            Advance(HoursPerSecond * Time.deltaTime);
        }

        float HoursPerSecond => (settings.nightfallHour - settings.dawnHour) / (settings.realMinutesPerDay * 60f) * settings.testSpeedMultiplier;

        // Moves time on, into the next day (or several) past midnight.
        public void Advance(float hours)
        {
            Hour += hours;
            while (Hour >= 24f)
            {
                Hour -= 24f;
                Day++;
                NewDay?.Invoke(Day);
            }
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
