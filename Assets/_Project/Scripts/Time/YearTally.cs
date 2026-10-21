using System;
using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // What the household did this year: jars put up, eggs laid, firewood
    // split, and how many times someone went down. When the first day of
    // spring comes round after a winter, the host closes the year (it's
    // kept as LastYear for the card that greets you) and starts counting
    // again. Shared with friends (SharedState) and saved.
    public class YearTally : MonoBehaviour, ISaveable
    {
        public enum Count { Jar, Egg, Firewood, WentDown }

        [Serializable]
        public class Year
        {
            public int year;
            public int days;
            public int jars;
            public int eggs;
            public int firewood;
            public int wentDown;
        }

        [SerializeField] DayClock clock;
        [SerializeField] CalendarSettings calendar;

        public static YearTally Instance { get; private set; }
        public Year ThisYear { get; private set; } = new Year { year = 1 };
        public Year LastYear { get; private set; }
        // The last year closed: a spring card is owed for it.
        public int Closed { get; private set; }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() => clock.NewDay += NewDay;
        void OnDisable() => clock.NewDay -= NewDay;

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        public static void Add(Count what, int amount = 1)
        {
            if (Instance == null || amount <= 0) return;
            var y = Instance.ThisYear;
            switch (what)
            {
                case Count.Jar: y.jars += amount; break;
                case Count.Egg: y.eggs += amount; break;
                case Count.Firewood: y.firewood += amount; break;
                case Count.WentDown: y.wentDown += amount; break;
            }
        }

        void NewDay(int day)
        {
            if (Hosting) CheckSpring(day);
        }

        // The first morning of spring after a winter closes the year.
        public void CheckSpring(int day)
        {
            var (month, dayOfMonth, year) = calendar.DateOf(day);
            if (month != 0 || dayOfMonth != 1 || year < 2 || Closed >= year - 1) return;
            ThisYear.days = calendar.DaysInYear;
            ThisYear.year = year - 1;
            LastYear = ThisYear;
            ThisYear = new Year { year = year };
            Closed = year - 1;
        }

        [Serializable]
        class State
        {
            public Year thisYear;
            public Year lastYear;
            public int closed;
        }

        public string SaveKey => "year-tally";

        public string Capture() => JsonUtility.ToJson(new State { thisYear = ThisYear, lastYear = LastYear, closed = Closed });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            ThisYear = state.thisYear ?? new Year { year = 1 };
            LastYear = state.lastYear != null && state.lastYear.year > 0 ? state.lastYear : null;
            Closed = state.closed;
        }
    }
}
