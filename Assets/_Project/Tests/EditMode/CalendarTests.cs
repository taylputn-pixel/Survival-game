using GrandmasHouse.Data;
using NUnit.Framework;
using UnityEngine;

namespace GrandmasHouse.Tests
{
    // Which month and day a game day falls on.
    public class CalendarTests
    {
        static CalendarSettings Year(params int[] lengths)
        {
            var settings = ScriptableObject.CreateInstance<CalendarSettings>();
            string[] names = { "Spring", "Summer", "Fall", "Winter", "Fifth" };
            for (int i = 0; i < lengths.Length; i++)
            {
                settings.months.Add(new CalendarSettings.Month { name = names[i], days = lengths[i] });
            }
            return settings;
        }

        [Test]
        public void TheFirstDayIsSpringFirst()
        {
            Assert.AreEqual((0, 1, 1), Year(30, 30, 30, 30).DateOf(1));
        }

        [Test]
        public void MonthsTurnOverAfterTheirLastDay()
        {
            var year = Year(30, 30, 30, 30);
            Assert.AreEqual((0, 30, 1), year.DateOf(30));
            Assert.AreEqual((1, 1, 1), year.DateOf(31));
            Assert.AreEqual((3, 30, 1), year.DateOf(120));
        }

        [Test]
        public void AfterWinterComesSpringOfTheNextYear()
        {
            Assert.AreEqual((0, 1, 2), Year(30, 30, 30, 30).DateOf(121));
        }

        [Test]
        public void MonthLengthsAndCountAreSettings()
        {
            var year = Year(10, 5, 20);
            Assert.AreEqual(35, year.DaysInYear);
            Assert.AreEqual((1, 5, 1), year.DateOf(15));
            Assert.AreEqual((2, 1, 1), year.DateOf(16));
            Assert.AreEqual((0, 1, 2), year.DateOf(36));
        }
    }
}
