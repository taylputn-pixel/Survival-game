using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Garden;
using GrandmasHouse.Saving;
using GrandmasHouse.Trade;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The first-year balance plan as the game has it: seeds that run a
    // little short, who sells more, a dear fallback food, and frost called
    // only the day before.
    public class FirstYearTests
    {
        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [Test]
        public void YouStartWithAboutFortySeeds()
        {
            var expected = new Dictionary<string, int>
            {
                { "SeedPotatoes", 6 }, { "TomatoSeeds", 4 }, { "CucumberSeeds", 4 }, { "DillSeeds", 4 }, { "GarlicSeeds", 4 },
                { "OnionSeeds", 4 }, { "CarrotSeeds", 6 }, { "SquashSeeds", 3 }, { "CabbageSeeds", 3 }, { "JalapenoSeeds", 2 }, { "MarigoldSeeds", 2 },
            };
            int total = 0;
            foreach (var packet in Object.FindObjectsByType<SeedPacket>())
            {
                string key = packet.GetComponent<SavedItem>().Key;
                Assert.IsTrue(expected.ContainsKey(key), "Unexpected packet " + key);
                Assert.AreEqual(expected[key], packet.Seeds, key);
                total += packet.Seeds;
            }
            Assert.AreEqual(42, total);
        }

        static Trader Called(string name)
        {
            foreach (var t in Object.FindObjectsByType<Trader>()) if (t.Name == name) return t;
            Assert.Fail("No " + name);
            return null;
        }

        static bool Sells(Trader t, string key)
        {
            foreach (var w in t.Wares) if (w.key == key) return true;
            return false;
        }

        [Test]
        public void EachNeighborSellsTheirOwnSeeds()
        {
            var june = Called("June");
            var ada = Called("Ada");
            var walt = Called("Walt");
            foreach (var k in new[] { "TomatoSeeds", "CucumberSeeds", "DillSeeds", "GarlicSeeds", "SquashSeeds", "CabbageSeeds", "JalapenoSeeds", "MarigoldSeeds" })
            {
                Assert.IsTrue(Sells(ada, k), "Ada sells " + k);
                Assert.IsFalse(Sells(june, k), "The store doesn't sell " + k);
            }
            Assert.IsTrue(Sells(june, "OnionSeeds") && Sells(june, "CarrotSeeds"));
            Assert.IsTrue(Sells(walt, "SeedPotatoes"));
            Assert.IsFalse(Sells(june, "SeedPotatoes"));
            var packet = Homestead.Make<SeedPacket>("TomatoSeeds");
            Assert.AreEqual(10, packet.Seeds, "A bought packet holds 10.");
        }

        [UnityTest]
        public IEnumerator TheStoreHasDearCannedFood()
        {
            var june = Called("June");
            Assert.IsTrue(Sells(june, "CannedBeans"));
            var beans = Homestead.Make<Pickup>("CannedBeans");
            yield return null;
            Assert.AreEqual("Canned Beans", beans.DisplayName);
            Assert.AreEqual(25f, beans.GetComponent<FreshFood>().Nutrition, 0.01f);
            Assert.GreaterOrEqual(beans.GetComponent<Perishable>().ShelfLifeDays, 300f, "It keeps all year.");
            var settings = Resources.FindObjectsOfTypeAll<Data.TradeSettings>()[0];
            Assert.AreEqual(15f, settings.WorthOf(beans, 0), 0.01f, "Three times a jar of pickles.");
        }

        [UnityTest]
        public IEnumerator TheHouseStartsWithOnlyShelfStableFood()
        {
            yield return null;
            // Grandma's fresh food went bad long ago: none is lying about.
            foreach (var fresh in Object.FindObjectsByType<FreshFood>())
            {
                Assert.AreEqual("Canned Beans", fresh.GetComponent<Pickup>().DisplayName, fresh.name + " would have spoiled.");
            }
            // Her own jars, sealed and labeled, and two cans of beans: about three days' food.
            float food = 0f;
            int jars = 0;
            foreach (var jar in Object.FindObjectsByType<Kitchen.GrandmasJar>())
            {
                var contents = jar.GetComponent<Kitchen.JarContents>();
                Assert.IsTrue(contents.IsSealed, jar.name);
                Assert.AreNotEqual("Don't eat!", contents.Recipe.displayName, jar.name);
                food += jar.GetComponent<JarFood>().Nutrition;
                jars++;
            }
            Assert.AreEqual(5, jars);
            foreach (var fresh in Object.FindObjectsByType<FreshFood>()) food += fresh.Nutrition;
            Assert.That(food, Is.InRange(150f, 220f), "About three days at ~60 a day.");
        }

        [Test]
        public void SeedsAndTheWateringCanAreInTheShed()
        {
            var shed = GameObject.Find("Yard/Garden Shed").transform;
            bool Inside(Vector3 p)
            {
                Vector3 local = shed.InverseTransformPoint(p);
                return Mathf.Abs(local.x) < 1f && Mathf.Abs(local.z) < 0.8f && local.y < 2.2f;
            }
            foreach (var packet in Object.FindObjectsByType<SeedPacket>()) Assert.IsTrue(Inside(packet.transform.position), packet.name);
            Assert.IsTrue(Inside(Object.FindAnyObjectByType<WateringCan>().transform.position));
        }

        [Test]
        public void FrostIsCalledOnlyTheDayBefore()
        {
            var weather = Homestead.Find<Weather>();
            var clock = Homestead.Find<DayClock>();
            int fall = 1 + weather.Calendar.months[0].days + weather.Calendar.months[1].days;
            for (int s = 1; s < 80; s++)
            {
                weather.Model.Seed = s;
                for (int d = fall + 2; d < fall + weather.Calendar.months[2].days; d++)
                {
                    if (weather.Model.DayOf(d).low > weather.Settings.killingFrost) continue;
                    // The frost falls on day d's morning: nothing said two days out ...
                    clock.Restore("{\"day\":" + (d - 2) + ",\"hour\":9}");
                    bool earlier = weather.Model.DayOf(d - 1).low <= weather.Settings.killingFrost;
                    if (!earlier && !weather.GroundFrozen) { StringAssert.DoesNotContain("Killing frost", weather.Forecast(), "Too early on seed " + s); StringAssert.DoesNotContain("Hard freeze", weather.Forecast(), "Too early on seed " + s); }
                    // ... and called the evening before.
                    clock.Restore("{\"day\":" + (d - 1) + ",\"hour\":19}");
                    if (!weather.GroundFrozen) Assert.That(weather.Forecast(), Does.Contain("Killing frost tonight").Or.Contain("Hard freeze tonight"));
                    return;
                }
            }
            Assert.Inconclusive("No fall frost in these seeds.");
        }
    }
}
