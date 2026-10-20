using System.Collections;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Garden;
using GrandmasHouse.Saving;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Seed to harvest in the real garden, one night at a time.
    public class GardenLoopTests
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

        PlantSpot EmptySpot()
        {
            foreach (var spot in bed.Spots)
            {
                if (spot.IsEmpty) return spot;
            }
            Assert.Fail("Every spot in " + bed.name + " is planted.");
            return null;
        }

        static CropData Crop(string name)
        {
            var crop = SaveManager.Catalog.Crop(name);
            Assert.IsNotNull(crop, name + " isn't in the save catalog.");
            return crop;
        }

        IEnumerator Plant(PlantSpot spot, CropData crop)
        {
            spot.Plant(crop, 0f);
            yield return null;
        }

        void WateredNight(PlantSpot spot)
        {
            spot.AddWater(1f);
            clock.SleepUntilMorning();
        }

        [UnityTest]
        public IEnumerator AWateredPlantGrowsOneStageANight()
        {
            var spot = EmptySpot();
            yield return Plant(spot, Crop("Crop_Cucumber"));
            Assert.AreEqual(-1, spot.Stage, "A fresh planting is a seed.");

            WateredNight(spot);
            Assert.AreEqual(0, spot.Stage);
            WateredNight(spot);
            Assert.AreEqual(1, spot.Stage);
        }

        [UnityTest]
        public IEnumerator ADryPlantWaits()
        {
            var spot = EmptySpot();
            yield return Plant(spot, Crop("Crop_Cucumber"));
            WateredNight(spot);
            int stage = spot.Stage;

            clock.SleepUntilMorning();
            Assert.AreEqual(stage, spot.Stage);
        }

        [UnityTest]
        public IEnumerator TheSoilDriesOvernight()
        {
            var spot = EmptySpot();
            yield return Plant(spot, Crop("Crop_Cucumber"));
            WateredNight(spot);
            Assert.AreEqual(0f, spot.Water);
        }

        [UnityTest]
        public IEnumerator ACucumberKeepsProducingOnceReady()
        {
            var crop = Crop("Crop_Cucumber");
            var spot = EmptySpot();
            yield return Plant(spot, crop);
            for (int night = 0; night < crop.stages.Length; night++) WateredNight(spot);
            yield return null;

            Assert.IsTrue(spot.IsReady, "Ready after one watered night per stage.");
            Assert.GreaterOrEqual(spot.RipeCount, crop.harvestPerNight);
            int ripe = spot.RipeCount;

            WateredNight(spot);
            yield return null;
            Assert.Greater(spot.RipeCount, ripe, "More ripen each watered night.");
        }

        [UnityTest]
        public IEnumerator DillIsReadyAllAtOnce()
        {
            var crop = Crop("Crop_Dill");
            Assert.IsTrue(crop.singleHarvest);
            var spot = EmptySpot();
            yield return Plant(spot, crop);
            for (int night = 0; night < crop.stages.Length; night++) WateredNight(spot);
            yield return null;

            Assert.IsTrue(spot.IsReady);
            Assert.Greater(spot.RipeCount, 0);
            int ripe = spot.RipeCount;
            WateredNight(spot);
            yield return null;
            Assert.AreEqual(ripe, spot.RipeCount, "A single harvest doesn't grow more.");
        }
    }
}
