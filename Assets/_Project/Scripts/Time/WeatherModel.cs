using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // The weather worked out from a seed, so every computer in a shared game
    // (and every reload) gets the same days without sending them around.
    // Plain C#, so it can be tested without a scene.
    public class WeatherModel
    {
        public struct Day
        {
            public float high;
            public float low;
            public bool coldSnap;
        }

        readonly WeatherSettings settings;
        readonly CalendarSettings calendar;
        public int Seed { get; set; }

        public WeatherModel(WeatherSettings settings, CalendarSettings calendar, int seed)
        {
            this.settings = settings;
            this.calendar = calendar;
            Seed = seed;
        }

        // A number from 0 to 1 that's always the same for this seed, day and purpose.
        float Roll(int day, int purpose)
        {
            unchecked
            {
                uint h = (uint)Seed * 2654435761u ^ (uint)day * 2246822519u ^ (uint)purpose * 3266489917u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // The month's usual, blended toward the next (or last) month away from mid-month.
        void Usual(int gameDay, out float high, out float low, out int month)
        {
            var (m, dayOfMonth, _) = calendar.DateOf(gameDay);
            month = m;
            int length = Mathf.Max(1, calendar.months[m].days);
            float through = (dayOfMonth - 0.5f) / length - 0.5f; // -0.5 .. 0.5 across the month
            int other = through >= 0f ? (m + 1) % calendar.months.Count : (m - 1 + calendar.months.Count) % calendar.months.Count;
            float blend = Mathf.Abs(through) * 0.5f;
            high = Mathf.Lerp(settings.High(m), settings.High(other), blend);
            low = Mathf.Lerp(settings.Low(m), settings.Low(other), blend);
        }

        public bool InColdSnap(int gameDay)
        {
            for (int start = gameDay; start > gameDay - settings.snapDaysMax && start >= 1; start--)
            {
                Usual(start, out _, out _, out int month);
                if (Roll(start, 1) >= settings.SnapChance(month)) continue;
                int length = settings.snapDaysMin + Mathf.FloorToInt(Roll(start, 2) * (settings.snapDaysMax - settings.snapDaysMin + 1));
                if (gameDay - start < length) return true;
            }
            return false;
        }

        public Day DayOf(int gameDay)
        {
            Usual(gameDay, out float high, out float low, out _);
            float wander = (Roll(gameDay, 0) * 2f - 1f) * settings.dailyWander;
            bool snap = InColdSnap(gameDay);
            float drop = snap ? settings.snapDrop : 0f;
            return new Day { high = high + wander - drop, low = low + wander - drop, coldSnap = snap };
        }

        // The temperature outside at an hour of a day: coldest just before dawn,
        // warmest mid-afternoon, easing between.
        public float At(int gameDay, float hour)
        {
            float cold = settings.coldestHour, warm = settings.warmestHour;
            if (hour >= cold && hour <= warm)
            {
                var d = DayOf(gameDay);
                return Mathf.Lerp(d.low, d.high, Ease((hour - cold) / (warm - cold)));
            }
            // Cooling from this afternoon's high to tomorrow's low (or last night's).
            int fromDay = hour > warm ? gameDay : gameDay - 1;
            float since = hour > warm ? hour - warm : hour + 24f - warm;
            float span = 24f - (warm - cold);
            var from = DayOf(Mathf.Max(1, fromDay));
            var to = DayOf(fromDay + 1);
            return Mathf.Lerp(from.high, to.low, Ease(since / span));
        }

        static float Ease(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI);
    }
}
