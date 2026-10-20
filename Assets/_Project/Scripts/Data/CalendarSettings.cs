using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrandmasHouse.Data
{
    // The months of the homestead year, in order, and how long each is.
    // Day 1 of the game is day 1 of the first month; after the last month the
    // year starts over.
    [CreateAssetMenu(menuName = "Grandma's House/Calendar Settings")]
    public class CalendarSettings : ScriptableObject
    {
        [Serializable]
        public class Month
        {
            public string name = "Spring";
            [Min(1)] public int days = 30;
            [Tooltip("The band across the top of the calendar page.")]
            public Color color = Color.gray;
        }

        public List<Month> months = new List<Month>();

        public int DaysInYear
        {
            get
            {
                int total = 0;
                foreach (var m in months) total += Mathf.Max(1, m.days);
                return Mathf.Max(1, total);
            }
        }

        // Which month and day of the month a game day falls on (both from 1 for the
        // day; month is an index into `months`), and which year.
        public (int month, int dayOfMonth, int year) DateOf(int gameDay)
        {
            int daysIn = Mathf.Max(0, gameDay - 1);
            int year = daysIn / DaysInYear + 1;
            int left = daysIn % DaysInYear;
            for (int i = 0; i < months.Count; i++)
            {
                int length = Mathf.Max(1, months[i].days);
                if (left < length) return (i, left + 1, year);
                left -= length;
            }
            return (0, 1, year);
        }
    }
}
