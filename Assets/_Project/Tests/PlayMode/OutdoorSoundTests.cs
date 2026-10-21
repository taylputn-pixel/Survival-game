using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Saving;
using GrandmasHouse.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // What you hear around the homestead: the wind through the year, birds
    // by day, crickets on summer nights, and the truck's engine.
    public class OutdoorSoundTests
    {
        PlayerHands hands;
        DayClock clock;
        Weather weather;
        Outdoors outdoors;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            clock = Homestead.Find<DayClock>();
            weather = Homestead.Find<Weather>();
            outdoors = Homestead.Find<Outdoors>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        IEnumerator At(int day, float hour)
        {
            clock.Restore("{\"day\":" + day + ",\"hour\":" + hour + "}");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (mb is IAfterLoad a) a.AfterLoad();
            yield return new WaitForSeconds(0.7f);
        }

        void Stand(Vector3 at)
        {
            var body = hands.GetComponent<CharacterController>();
            body.enabled = false;
            hands.transform.position = at;
            body.enabled = true;
        }

        int FirstDayOf(int month)
        {
            int day = 1;
            for (int m = 0; m < month; m++) day += weather.Calendar.months[m].days;
            return day;
        }

        [UnityTest]
        public IEnumerator BirdsByDayCricketsOnSummerNights()
        {
            Stand(new Vector3(1.7f, 0.1f, -6f));
            yield return At(FirstDayOf(1), 12f);
            Assert.IsTrue(outdoors.Birds, "Birds on a summer day.");
            Assert.AreEqual(0f, outdoors.Crickets, "No crickets at noon.");

            yield return At(FirstDayOf(1), 23f);
            Assert.IsFalse(outdoors.Birds, "No birds at night.");
            Assert.Greater(outdoors.Crickets, 0f, "Crickets on a summer night.");
        }

        [UnityTest]
        public IEnumerator WinterIsWindierAndQuieterIndoors()
        {
            Stand(new Vector3(1.7f, 0.1f, -6f));
            yield return At(FirstDayOf(1), 12f);
            float summer = outdoors.Wind;
            Assert.Greater(summer, 0f, "Always a little wind.");

            int winter = FirstDayOf(3) + 1;
            yield return At(winter, 12f);
            Assert.Greater(outdoors.Wind, summer, "Winter wind is harder.");
            Assert.IsFalse(outdoors.Birds, "No birdsong in winter.");
            float outside = outdoors.Wind;

            Stand(new Vector3(2.25f, 0.1f, 3f));
            yield return new WaitForSeconds(0.7f);
            Assert.Less(outdoors.Wind, outside, "Muffled in the kitchen.");
        }

        [UnityTest]
        public IEnumerator TheEngineRunsWithSomeoneInTheTruck()
        {
            var truck = Homestead.Find<Truck>();
            var engine = truck.GetComponentInChildren<SoundLoop>();
            Assert.IsNotNull(engine, "The truck should have an engine sound.");
            Stand(truck.transform.position + truck.transform.right * -2f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(engine.Playing, "Quiet while parked.");

            truck.SitIn(hands);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(engine.Playing, "Running with a driver.");
            truck.Leave(hands);
        }
    }
}
