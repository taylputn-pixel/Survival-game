using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Player;
using NUnit.Framework;
using UnityEngine;

namespace GrandmasHouse.Tests
{
    // The weather's seasons and cold snaps, and warmth as a need.
    public class WinterRuleTests
    {
        WeatherSettings weather;
        CalendarSettings calendar;
        SurvivalSettings settings;

        [SetUp]
        public void Make()
        {
            weather = ScriptableObject.CreateInstance<WeatherSettings>();
            calendar = ScriptableObject.CreateInstance<CalendarSettings>();
            foreach (var name in new[] { "Spring", "Summer", "Fall", "Winter" })
            {
                calendar.months.Add(new CalendarSettings.Month { name = name, days = 30 });
            }
            settings = ScriptableObject.CreateInstance<SurvivalSettings>();
        }

        [Test]
        public void TheSameSeedGivesTheSameWeather()
        {
            var a = new WeatherModel(weather, calendar, 1234);
            var b = new WeatherModel(weather, calendar, 1234);
            for (int day = 1; day < 120; day += 7)
            {
                Assert.AreEqual(a.At(day, 10f), b.At(day, 10f));
            }
            Assert.AreNotEqual(a.At(50, 10f), new WeatherModel(weather, calendar, 99).At(50, 10f));
        }

        [Test]
        public void WinterIsColdAndSummerIsWarm()
        {
            var model = new WeatherModel(weather, calendar, 7);
            float summer = 0f, winter = 0f;
            for (int d = 0; d < 20; d++)
            {
                summer += model.DayOf(36 + d).high;
                winter += model.DayOf(96 + d).high;
            }
            Assert.Greater(summer / 20f, 65f);
            Assert.Less(winter / 20f, 32f);
        }

        [Test]
        public void ColdestBeforeDawnWarmestInTheAfternoon()
        {
            var model = new WeatherModel(weather, calendar, 3);
            Assert.Less(model.At(40, 6f), model.At(40, 15f));
            Assert.AreEqual(model.DayOf(40).low, model.At(40, 6f), 0.01f);
            Assert.AreEqual(model.DayOf(40).high, model.At(40, 15f), 0.01f);
        }

        [Test]
        public void ColdSnapsComeOnlyInTheColdMonths()
        {
            int springSnaps = 0, winterSnaps = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var model = new WeatherModel(weather, calendar, seed);
                for (int day = 1; day <= 60; day++) if (model.DayOf(day).coldSnap) springSnaps++;
                for (int day = 91; day <= 120; day++) if (model.DayOf(day).coldSnap) winterSnaps++;
            }
            Assert.AreEqual(0, springSnaps);
            Assert.Greater(winterSnaps, 0);
        }

        [Test]
        public void ColdDrainsWarmthAndWarmthComesBack()
        {
            var needs = new Needs(settings);
            needs.Pass(4f, false, settings, 10f);
            Assert.Less(needs.warmth, 100f - 40f, "Four hours at 10 degrees should chill you a lot.");
            float chilled = needs.warmth;
            needs.Pass(1f, false, settings, 70f);
            Assert.AreEqual(Mathf.Min(100f, chilled + settings.warmUpPerHour), needs.warmth, 0.01f);
        }

        [Test]
        public void TooLongWithNoWarmthAndYouCollapse()
        {
            var needs = new Needs(settings);
            needs.Pass(20f, false, settings, -20f);
            Assert.AreEqual(0f, needs.warmth);
            Assert.IsTrue(needs.Collapsed(settings));
            Assert.AreEqual(Condition.Collapsed, needs.Condition(settings));
            Assert.AreEqual(WarmthLevel.Freezing, needs.Warmth(settings));

            // Food won't get you up from the cold; warming up will.
            needs.BringRound(settings);
            Assert.IsTrue(needs.Collapsed(settings));
            needs.WarmUp(settings);
            Assert.IsFalse(needs.Collapsed(settings));
            Assert.AreEqual(settings.warmedUpTo, needs.warmth, 0.01f);
        }

        [Test]
        public void TheColdSlowsYouDown()
        {
            var needs = new Needs(settings);
            Assert.AreEqual(1f, needs.SpeedMultiplier(settings), 0.001f);
            needs.warmth = 0f;
            Assert.AreEqual(settings.coldSpeed, needs.SpeedMultiplier(settings), 0.001f);
        }

        [Test]
        public void ItSnowsInWinterAndNeverInSummer()
        {
            int winterSnow = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var model = new WeatherModel(weather, calendar, seed);
                for (int d = 31; d <= 60; d++) Assert.IsFalse(model.DayOf(d).snow, "Snow in summer, day " + d);
                for (int d = 91; d <= 120; d++)
                {
                    var day = model.DayOf(d);
                    if (!day.snow) continue;
                    winterSnow++;
                    Assert.LessOrEqual(day.high, weather.snowBelow);
                    Assert.IsFalse(day.coldSnap, "A cold snap is clear and dry.");
                }
            }
            Assert.Greater(winterSnow, 100, "Winter should see plenty of snow.");
        }

        [Test]
        public void SnowPilesUpAndMeltsInSpring()
        {
            var model = new WeatherModel(weather, calendar, 11);
            float most = 0f;
            for (int d = 91; d <= 120; d++) most = Mathf.Max(most, model.SnowCover(d));
            Assert.Greater(most, 0.5f, "Winter ground should go white.");
            Assert.AreEqual(0f, model.SnowCover(50), "No snow on the ground in summer.");
            Assert.AreEqual(0f, model.SnowCover(150), "It's gone by mid-spring.");
        }
    }
}
