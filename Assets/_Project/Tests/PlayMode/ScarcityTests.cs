using System.Collections;
using System.Reflection;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Garden;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using GrandmasHouse.Trade;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Hard times: the store waits on deliveries, a frost cloth saves the
    // garden from a light frost only, lids don't always seal, and winter
    // is the longest month.
    public class ScarcityTests
    {
        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        static Trader Store()
        {
            foreach (var t in Object.FindObjectsByType<Trader>()) if (t.IsStore) return t;
            Assert.Fail("No store.");
            return null;
        }

        [Test]
        public void WinterIsTheLongestMonth()
        {
            var calendar = Homestead.Find<Weather>().Calendar;
            Assert.AreEqual(7, calendar.months[3].days);
            for (int m = 0; m < 3; m++) Assert.Less(calendar.months[m].days, calendar.months[3].days);
        }

        [Test]
        public void TheStoreOnlyRestocksWhenTheTruckComes()
        {
            var store = Store();
            Assert.IsTrue(store.HasDeliveries);
            foreach (var w in store.Wares) w.stock = 0;
            var restock = typeof(Trader).GetMethod("Restock", BindingFlags.NonPublic | BindingFlags.Instance);
            var calendar = Homestead.Find<Weather>().Calendar;
            int winter = 1 + calendar.months[0].days + calendar.months[1].days + calendar.months[2].days;

            // A winter's worth of days brings few deliveries, if any.
            Random.InitState(11);
            int deliveries = 0;
            for (int i = 0; i < 40; i++)
            {
                int before = store.LastDelivery;
                restock.Invoke(store, new object[] { winter });
                if (store.LastDelivery != before) deliveries++;
                store.Restore(store.Capture().Replace("\"lastDelivery\":" + store.LastDelivery, "\"lastDelivery\":-1"));
            }
            Assert.Less(deliveries, 12, "Deliveries are rare in winter.");

            // A delivery brings some of each, never past what they keep.
            foreach (var w in store.Wares) w.stock = 0;
            store.Deliver(3);
            Assert.AreEqual(3, store.LastDelivery);
            foreach (var w in store.Wares) Assert.LessOrEqual(w.stock, w.maxStock, w.key);
            StringAssert.Contains("\"lastDelivery\":3", store.Capture());
        }

        PlantSpot Planted(GardenBed bed)
        {
            foreach (var spot in bed.Spots)
            {
                if (!spot.IsEmpty) continue;
                spot.Plant(SaveManager.Catalog.Crop("Crop_Cucumber"), 0f);
                spot.AddWater(1f);
                spot.GrowOvernight(true);
                return spot;
            }
            Assert.Fail("No room.");
            return null;
        }

        [UnityTest]
        public IEnumerator AFrostClothSavesTheBedFromALightFrostOnly()
        {
            var beds = Object.FindObjectsByType<GardenBed>();
            Assert.GreaterOrEqual(beds.Length, 2);
            var covered = beds[0];
            var open = beds[1];
            yield return null;
            var under = Planted(covered);
            var bare = Planted(open);

            var cloth = covered.GetComponentInChildren<BedCover>();
            var hands = Homestead.Find<PlayerHands>();
            Assert.IsTrue(cloth.CanInteract(hands));
            Assert.AreEqual("Pull the frost cloth over", cloth.PromptVerb(hands));
            cloth.Interact(hands);
            Assert.IsTrue(covered.Covered);
            Assert.AreEqual("Roll back the frost cloth", cloth.PromptVerb(hands));

            // Nothing can be done under it.
            var can = Object.FindAnyObjectByType<WateringCan>();
            typeof(WateringCan).GetField("level", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(can, 1f);
            hands.Give(can.GetComponent<Pickup>());
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(open.GetComponent<BedSoil>().CanInteract(hands), "The open bed can be watered.");
            Assert.IsFalse(covered.GetComponent<BedSoil>().CanInteract(hands), "Can't water through the cloth.");

            var weather = Homestead.Find<Weather>();
            weather.Frost(false);
            Assert.IsFalse(under.IsEmpty, "Covered, it lives through a light frost.");
            Assert.IsTrue(bare.IsEmpty, "Left open, it's gone.");

            // Kept over a save.
            string saved = covered.Capture();
            covered.Cover(false);
            covered.Restore(saved);
            Assert.IsTrue(covered.Covered);

            weather.Frost(true);
            Assert.IsTrue(under.IsEmpty, "A hard freeze takes it anyway.");
        }

        [UnityTest]
        public IEnumerator ALidDoesntAlwaysSeal()
        {
            Assert.IsTrue(Homestead.Find<DayClock>().Mode.jarsCanFailToSeal);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var good = typeof(JarContents).GetField("sealFailChance", flags);
            var bad = typeof(JarContents).GetField("badHeadspaceSealFail", flags);

            var failing = Homestead.Make<JarContents>("PintJar");
            good.SetValue(failing, 1f);
            bad.SetValue(failing, 1f);
            var sealing = Homestead.Make<JarContents>("PintJar");
            good.SetValue(sealing, 0f);
            bad.SetValue(sealing, 0f);
            yield return null;

            failing.CoolAndSeal(0f);
            sealing.CoolAndSeal(0f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(failing.IsSealed, "No ping.");
            Assert.IsFalse(failing.IsProcessed, "It can go through the canner again.");
            Assert.IsTrue(sealing.IsSealed);
        }

        [Test]
        public void HeadspaceOffTheLineIsTheUsualCulprit()
        {
            var dill = Resources.FindObjectsOfTypeAll<RecipeData>();
            RecipeData pickles = null;
            foreach (var r in dill) if (r.name == "Recipe_DillPickleChips") pickles = r;
            Assert.IsNotNull(pickles);
            Assert.IsFalse(JarQuality.HeadspaceOff(pickles, new JarMakings { level = pickles.brineLine }));
            Assert.IsTrue(JarQuality.HeadspaceOff(pickles, new JarMakings { level = pickles.brineLine - pickles.lineTolerance - 0.05f }));
            Assert.IsTrue(JarQuality.HeadspaceOff(pickles, new JarMakings { level = 1f }));
        }
    }
}
