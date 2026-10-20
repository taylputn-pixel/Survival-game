using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Garden;
using GrandmasHouse.Saving;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The keeping crops (potatoes, carrots, winter squash) and the root
    // cellar that keeps them through the winter.
    public class CellarTests
    {
        DayClock clock;
        GardenBed bed;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            clock = Homestead.Find<DayClock>();
            bed = Homestead.Find<GardenBed>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [UnityTest]
        public IEnumerator EachKeepingCropGrowsAndIsDugUp([Values("Crop_Potato", "Crop_Carrot", "Crop_Squash")] string name)
        {
            CropData crop = SaveManager.Catalog.Crop(name);
            Assert.IsNotNull(crop, name + " isn't in the save catalog.");
            PlantSpot spot = null;
            foreach (var s in bed.Spots) if (s.IsEmpty) { spot = s; break; }
            spot.Plant(crop, 0f);
            yield return null;
            for (int night = 0; night < crop.stages.Length && !spot.IsReady; night++)
            {
                spot.AddWater(1f);
                clock.SleepUntilMorning();
                yield return null;
            }
            Assert.IsTrue(spot.IsReady, name + " never got ready.");
            Assert.Greater(spot.RipeCount, 0, name + " is ready but has nothing to dig up.");
            foreach (var item in spot.Ripe)
            {
                Assert.AreEqual(crop.harvest.DisplayName, item.DisplayName);
                Assert.IsTrue(item.GetComponent<NetworkObject>().IsSpawned, "The harvest isn't shared with other players.");
            }
        }

        [UnityTest]
        public IEnumerator PotatoesKeepFiveTimesLongerInTheCellar()
        {
            var cold = Object.FindObjectsByType<ColdStorage>();
            ColdStorage cellar = null;
            foreach (var c in cold) if (c.transform.parent != null && c.transform.parent.name == "Root Cellar") cellar = c;
            Assert.IsNotNull(cellar, "No root cellar.");
            Assert.AreEqual(0.2f, cellar.SpoilRate, 0.001f);

            var inCellar = Homestead.Make<Pickup>("Potato");
            inCellar.transform.position = cellar.transform.position;
            var inKitchen = Homestead.Make<Pickup>("Potato");
            inKitchen.transform.position = new Vector3(2.25f, 1f, 1.7f);
            yield return null;
            yield return new WaitForSeconds(0.5f);

            clock.Advance(24f * 10f);
            yield return new WaitForSeconds(1.3f);
            float cellarSpoiled = 1f - inCellar.GetComponent<Perishable>().Freshness;
            float kitchenSpoiled = 1f - inKitchen.GetComponent<Perishable>().Freshness;
            Assert.Greater(kitchenSpoiled, 0f);
            Assert.AreEqual(kitchenSpoiled * 0.2f, cellarSpoiled, 0.01f);
        }

        [Test]
        public void SeedsForTheKeepingCropsAreInTheHouse()
        {
            int found = 0;
            foreach (var packet in Object.FindObjectsByType<SeedPacket>())
            {
                if (packet.Crop.name == "Crop_Potato" || packet.Crop.name == "Crop_Carrot" || packet.Crop.name == "Crop_Squash") found++;
            }
            Assert.AreEqual(3, found);
        }
    }
}
