using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using GrandmasHouse.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The short year from start to finish: why you came, what you did, and
    // making it to spring.
    public class YearTests
    {
        [TearDown]
        public void PutSavesBack()
        {
            SaveSlot.NewGame = false;
            Homestead.RestoreRealSaves();
        }

        IEnumerator Load(bool newGame)
        {
            Homestead.UseMemorySaves();
            SaveSlot.NewGame = newGame;
            yield return Homestead.Load();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ANewGameOpensWithWhyYouCame()
        {
            yield return Load(newGame: true);
            var card = Homestead.Find<StoryCard>();
            Assert.IsTrue(card.Showing, "A new game should open with the story card.");
            StringAssert.Contains("city", card.Text);
            Assert.IsFalse(SaveSlot.NewGame, "It's only shown once.");
        }

        [UnityTest]
        public IEnumerator ALoadedGameDoesNotRepeatIt()
        {
            yield return Load(newGame: false);
            Assert.IsFalse(Homestead.Find<StoryCard>().Showing);
        }

        [UnityTest]
        public IEnumerator SpringClosesTheYearAndShowsWhatYouDid()
        {
            yield return Load(newGame: false);
            var tally = Homestead.Find<YearTally>();
            var calendar = Homestead.Find<Weather>().Calendar;
            var clock = Homestead.Find<DayClock>();
            Assert.AreEqual(20, calendar.DaysInYear, "The playtest year is four five-day months.");

            YearTally.Add(YearTally.Count.Jar, 3);
            YearTally.Add(YearTally.Count.Egg, 12);
            YearTally.Add(YearTally.Count.Firewood, 8);
            Assert.AreEqual(3, tally.ThisYear.jars);

            // Midwinter: nothing closes yet.
            tally.CheckSpring(calendar.DaysInYear - 2);
            Assert.AreEqual(0, tally.Closed);

            // The first morning of the second spring.
            clock.Restore("{\"day\":" + calendar.DaysInYear + ",\"hour\":23.5}");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>()) if (mb is IAfterLoad a) a.AfterLoad();
            clock.SleepUntilMorning();
            yield return null;
            Assert.AreEqual(1, tally.Closed, "Spring should close the first year.");
            // The hens may lay a few more overnight.
            Assert.GreaterOrEqual(tally.LastYear.eggs, 12);
            Assert.AreEqual(0, tally.ThisYear.jars, "The new year starts counting again.");
            Assert.AreEqual(2, tally.ThisYear.year);

            yield return null;
            var card = Homestead.Find<StoryCard>();
            Assert.IsTrue(card.Showing, "You should be greeted by spring.");
            StringAssert.Contains("You made it through the winter", card.Text);
            StringAssert.Contains("3 jars", card.Text);
            StringAssert.Contains("8 firewood", card.Text);

            // Saved and loaded, the closed year stays closed.
            string saved = tally.Capture();
            tally.Restore("{}");
            tally.Restore(saved);
            Assert.AreEqual(1, tally.Closed);
            Assert.AreEqual(3, tally.LastYear.jars);
        }

        [UnityTest]
        public IEnumerator SplittingWoodIsCounted()
        {
            yield return Load(newGame: false);
            var tally = Homestead.Find<YearTally>();
            var block = Homestead.Find<ChoppingBlock>();
            var log = Homestead.Make<Pickup>("Log");
            log.transform.position = block.transform.position + Vector3.up * 1.5f;
            yield return null;
            log.AttachTo((Transform)typeof(ChoppingBlock).GetField("top", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(block), Vector3.up * log.RestHalfHeight, Quaternion.identity, 0f, collidersOn: true);
            yield return null;
            int before = tally.ThisYear.firewood;
            block.SplitHere();
            Assert.AreEqual(before + 4, tally.ThisYear.firewood);
        }
    }
}
