using System.Collections;
using GrandmasHouse.DayCycle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The calendar on the kitchen wall keeps up with the days.
    public class WallCalendarTests
    {
        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        static int Crossed(WallCalendar calendar)
        {
            int n = 0;
            foreach (Transform t in calendar.GetComponentsInChildren<Transform>()) if (t.name == "Cross") n++;
            return n;
        }

        [UnityTest]
        public IEnumerator ItShowsTodayAndTurnsThePage()
        {
            var calendar = Homestead.Find<WallCalendar>();
            var clock = Homestead.Find<DayClock>();
            yield return null;
            Assert.AreEqual("Spring", calendar.MonthName);
            Assert.AreEqual(clock.Day, calendar.DayOfMonth);

            var months = Homestead.Find<DayCycle.Weather>().Calendar.months;
            int spring = months[0].days;
            clock.Restore("{\"day\":" + spring + ",\"hour\":10}");
            yield return null;
            Assert.AreEqual("Spring", calendar.MonthName);
            Assert.AreEqual(spring, calendar.DayOfMonth);
            Assert.AreEqual(spring - 1, Crossed(calendar), "Every day before today should be crossed off.");

            clock.SleepUntilMorning();
            yield return null;
            Assert.AreEqual("Summer", calendar.MonthName);
            Assert.AreEqual(1, calendar.DayOfMonth);
            Assert.AreEqual(0, Crossed(calendar), "A new page has nothing crossed off.");

            int winterDay2 = months[0].days + months[1].days + months[2].days + 2;
            clock.Restore("{\"day\":" + winterDay2 + ",\"hour\":10}");
            yield return null;
            Assert.AreEqual("Winter", calendar.MonthName);
            Assert.AreEqual(2, calendar.DayOfMonth);
        }
    }
}
