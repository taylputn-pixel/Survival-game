using System;
using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.Garden;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // The weather outside, and how cold it is wherever someone stands. The
    // days come from a seed saved with the game (shared with friends), so
    // there's nothing to send but that. On the host, a killing frost takes
    // whatever is still growing in the garden.
    public class Weather : MonoBehaviour, ISaveable
    {
        [SerializeField] DayClock clock;
        [SerializeField] CalendarSettings calendar;
        [SerializeField] WeatherSettings settings;

        public static Weather Instance { get; private set; }
        public WeatherSettings Settings => settings;
        public WeatherModel Model { get; private set; }
        public float Outside => Model.At(clock.Day, clock.Hour);
        public WeatherModel.Day Today => Model.DayOf(clock.Day);
        public bool GroundFrozen => Today.high < settings.groundFrozenBelow;
        public bool Snowing => Today.snow;
        public float SnowCover => Model.SnowCover(clock.Day);
        public DayClock Clock => clock;
        public CalendarSettings Calendar => calendar;

        // A killing frost came through the garden (for the radio and tests).
        public event Action Frosted;

        float nextCheck;
        bool frostedTonight;
        bool hardTonight;

        void Awake()
        {
            Instance = this;
            Model = new WeatherModel(settings, calendar, UnityEngine.Random.Range(1, int.MaxValue));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            float outside = Outside;
            bool frost = outside <= settings.killingFrost;
            bool hard = outside <= HardFreeze;
            if (Hosting && ((frost && !frostedTonight) || (hard && !hardTonight))) Frost(hard);
            frostedTonight = frost;
            hardTonight = hard;
        }

        // Colder than a frost cloth keeps off.
        public float HardFreeze => settings.killingFrost - settings.coverHolds;

        // Everything still growing outside dies, except under a frost cloth
        // when it isn't a hard freeze.
        public void Frost() => Frost(Outside <= HardFreeze);

        public void Frost(bool hardFreeze)
        {
            int killed = 0;
            foreach (var bed in FindObjectsByType<GardenBed>(FindObjectsSortMode.None)) killed += bed.Frost(hardFreeze);
            if (killed > 0) Frosted?.Invoke();
        }

        // How cold it feels at a spot: outside, or in a shelter (and its stove),
        // beside a lit stove, and under the blankets.
        public float FeltAt(Vector3 position, bool inBed)
        {
            float outside = Outside;
            float felt = outside;
            foreach (var shelter in Shelter.All)
            {
                if (shelter.Contains(position))
                {
                    felt = shelter.Temperature(outside, settings);
                    break;
                }
            }
            foreach (var stove in WoodStove.All)
            {
                if (stove.Heat > 0f && Vector3.Distance(stove.transform.position, position) <= settings.besideStoveRadius)
                {
                    felt += settings.besideStove * stove.Heat;
                }
            }
            if (inBed) felt += settings.inBed;
            return felt;
        }

        // ---- The farm report ----

        static string Degrees(float f)
        {
            int d = Mathf.RoundToInt(f);
            return d < 0 ? -d + " below" : d.ToString();
        }

        static readonly string[] When = { "Today", "Tomorrow", "The day after", "In three days", "In four days" };

        // What the radio says: now, today, the days ahead, and warnings
        // about frost and cold snaps in plain words.
        public string Forecast()
        {
            var date = calendar.DateOf(clock.Day);
            var text = new System.Text.StringBuilder();
            text.Append("Farm report for " + calendar.months[date.month].name + ", day " + date.dayOfMonth + ". ");
            int now = Mathf.RoundToInt(Outside);
            text.Append("It's " + (now < 0 ? -now + " below zero" : now + " degrees") + " right now. ");
            bool warnedFrost = false, warnedSnap = Today.coldSnap;
            if (Today.coldSnap) text.Append("This cold snap is holding on. ");
            if (Today.snow) text.Append("Snow coming down today. ");
            for (int i = 0; i <= settings.forecastDays && i < When.Length; i++)
            {
                var d = Model.DayOf(clock.Day + i);
                text.Append(When[i] + ", a high of " + Degrees(d.high) + " and a low of " + Degrees(d.low) + (i > 0 && d.snow ? ", with snow" : "") + ". ");
                if (i > 0 && d.coldSnap && !warnedSnap)
                {
                    text.Append("A cold snap is coming in. Bring in wood and stay by the stove. ");
                    warnedSnap = true;
                }
                // Frost is only called the day before: this coming dawn, or tomorrow's.
                bool soon = i == 1 || (i == 0 && clock.Hour < settings.coldestHour);
                if (!warnedFrost && soon && d.low <= settings.killingFrost && !GroundFrozen)
                {
                    bool hard = d.low <= HardFreeze;
                    string kind = hard ? "Hard freeze" : "Killing frost";
                    string when = i == 0 ? kind + " before dawn" : clock.Hour >= 12f ? kind + " tonight" : kind + " tomorrow morning";
                    text.Append(when + (hard ? ": pick what's left in your garden. " : ": cover the garden or pick what's left. "));
                    warnedFrost = true;
                }
            }
            return text.ToString().Trim();
        }

        [Serializable]
        class State
        {
            public int seed;
        }

        public string SaveKey => "weather";
        public string Capture() => JsonUtility.ToJson(new State { seed = Model.Seed });
        public void Restore(string json) => Model.Seed = JsonUtility.FromJson<State>(json).seed;
    }
}
