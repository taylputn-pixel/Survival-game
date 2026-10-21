using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Garden;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Trade;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The wood stove, the house in winter, frost in the garden, and the forecast.
    public class WinterTests
    {
        PlayerHands hands;
        DayClock clock;
        Weather weather;
        WoodStove stove;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            clock = Homestead.Find<DayClock>();
            weather = Homestead.Find<Weather>();
            stove = Homestead.Find<WoodStove>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        IEnumerator WoodInHand(int logs)
        {
            for (int i = 0; i < logs; i++)
            {
                var log = Homestead.Make<Pickup>("Firewood");
                log.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
                yield return null;
                hands.Give(log);
                yield return null;
            }
        }

        void ToWinter(int dayOfWinter = 10, float hour = 10f) => clock.Restore("{\"day\":" + (90 + dayOfWinter) + ",\"hour\":" + hour + "}");

        [UnityTest]
        public IEnumerator TheStoveBurnsWoodAndWarmsTheHouse()
        {
            ToWinter();
            yield return null;
            Vector3 middle = new Vector3(2.5f, 1.5f, 2.5f);
            float cold = weather.FeltAt(middle, false);
            Assert.Less(cold, weather.Settings.houseHolds + weather.Outside + 0.5f);

            yield return WoodInHand(3);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(stove.CanInteract(hands));
                Assert.AreEqual("Put wood in the stove", stove.PromptVerb(hands));
                stove.Interact(hands);
                yield return null;
            }
            Assert.AreEqual(3, stove.Logs);
            Assert.IsTrue(hands.IsEmpty);
            Assert.AreEqual("Light the stove", stove.PromptVerb(hands));
            stove.Interact(hands);
            Assert.IsTrue(stove.Burning);
            Assert.AreEqual(2, stove.Logs);

            clock.Advance(1.5f);
            yield return null;
            Assert.AreEqual(1f, stove.Heat, 0.01f);
            Assert.Greater(weather.FeltAt(middle, false), cold + weather.Settings.stoveHeat - 1f, "A roaring stove should warm the whole house.");
            Assert.Greater(weather.FeltAt(stove.transform.position + Vector3.right, false), weather.FeltAt(middle, false), "Warmest right by the stove.");

            // Three logs burn about twelve hours, then it goes out and the house cools.
            for (int i = 0; i < 12; i++) { clock.Advance(1f); yield return null; }
            Assert.IsFalse(stove.Burning);
            Assert.AreEqual(0, stove.Logs);
            for (int i = 0; i < 4; i++) { clock.Advance(1f); yield return null; }
            Assert.AreEqual(0f, stove.Heat, 0.01f);
        }

        [UnityTest]
        public IEnumerator FrostKillsTheGardenAndTheGroundFreezes()
        {
            var bed = Homestead.Find<GardenBed>();
            var crop = SaveManager().Crop("Crop_Tomato");
            bed.Spots[0].Plant(crop, 0f);
            yield return null;
            Assert.IsFalse(bed.Spots[0].IsEmpty);

            weather.Frost();
            Assert.IsTrue(bed.Spots[0].IsEmpty, "A killing frost should take the plant.");

            ToWinter(15);
            yield return null;
            Assert.IsTrue(weather.GroundFrozen, "Midwinter ground should be frozen.");
            var packet = Homestead.Make<Pickup>("TomatoSeeds");
            packet.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            hands.Give(packet);
            yield return null;
            var soil = Object.FindAnyObjectByType<BedSoil>();
            Assert.IsFalse(soil.CanInteract(hands), "Nothing can be planted in frozen ground.");
        }

        static Saving.SaveCatalog SaveManager() => Saving.SaveManager.Catalog;

        [UnityTest]
        public IEnumerator TheRadioWarnsOfAColdSnapAhead()
        {
            // Find a seed where a cold snap starts two days after a clear day in winter.
            int seed = 0, day = 0;
            for (int s = 1; s < 500 && seed == 0; s++)
            {
                weather.Model.Seed = s;
                for (int d = 92; d < 115; d++)
                {
                    if (!weather.Model.DayOf(d).coldSnap && !weather.Model.DayOf(d + 1).coldSnap && weather.Model.DayOf(d + 2).coldSnap) { seed = s; day = d; break; }
                }
            }
            Assert.Greater(seed, 0, "No cold snap found in 500 winters.");
            clock.Restore("{\"day\":" + day + ",\"hour\":9}");
            yield return null;
            string report = weather.Forecast();
            StringAssert.Contains("cold snap is coming", report);
            StringAssert.Contains("Winter", report);

            var radio = Homestead.Find<Audio.Radio>();
            Assert.IsTrue(radio.CanAltInteract(hands));
            Assert.AreEqual("Hear the forecast", radio.AltVerb(hands));
            radio.AltInteract(hands);
            yield return null;
            Assert.AreEqual(report, Homestead.Find<UI.Caption>().Showing);
        }

        [Test]
        public void FrostIsForecastInFall()
        {
            bool warned = false;
            for (int s = 1; s < 50 && !warned; s++)
            {
                weather.Model.Seed = s;
                for (int d = 61; d < 90 && !warned; d++)
                {
                    clock.Restore("{\"day\":" + d + ",\"hour\":9}");
                    if (!weather.GroundFrozen && weather.Forecast().Contains("Killing frost")) warned = true;
                }
            }
            Assert.IsTrue(warned, "Fall should bring a killing frost the radio warns about.");
        }

        [Test]
        public void WaltIsTheOneWithWood()
        {
            foreach (var t in Object.FindObjectsByType<Trader>())
            {
                bool wood = false;
                foreach (var w in t.Wares) if (w.key == "Firewood") wood = true;
                Assert.AreEqual(t.Name == "Walt", wood, t.Name + (wood ? " shouldn't" : " should") + " have firewood.");
            }
        }

        [Test]
        public void TheCellarStaysAboveFreezing()
        {
            ToWinter();
            Assert.AreEqual(45f, weather.FeltAt(new Vector3(8.3f, -1.2f, 2.5f), false), 0.01f);
        }
    }
}
