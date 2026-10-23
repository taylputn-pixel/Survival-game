using System.Collections;
using System.Reflection;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Garden;
using GrandmasHouse.Saving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // In survival a plant left dry wilts then dies, one left with hornworms
    // is chewed then dies, and pruning only raises the yield. Nights are
    // passed on the plant directly (sleeping the whole house is slow).
    public class CropNeglectTests
    {
        GardenBed bed;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            bed = Homestead.Find<GardenBed>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        PlantSpot EmptySpot()
        {
            foreach (var spot in bed.Spots) if (spot.IsEmpty) return spot;
            Assert.Fail("Every spot is planted.");
            return null;
        }

        static CropData Crop(string name) => SaveManager.Catalog.Crop(name);

        // Watered nights up to the given stage.
        IEnumerator Grown(PlantSpot spot, CropData crop, int stage)
        {
            spot.Plant(crop, 0f);
            yield return null;
            while (spot.Stage < stage)
            {
                spot.AddWater(1f);
                spot.GrowOvernight(true, canDie: true);
            }
        }

        [Test]
        public void SurvivalLetsCropsDie()
        {
            Assert.IsTrue(Homestead.Find<DayClock>().Mode.cropsCanDie);
        }

        [UnityTest]
        public IEnumerator ADryPlantWiltsThenDies()
        {
            var spot = EmptySpot();
            yield return Grown(spot, Crop("Crop_Cucumber"), 1);

            spot.GrowOvernight(true, canDie: true);
            Assert.IsTrue(spot.IsWilted, "One dry night wilts it.");
            Assert.IsFalse(spot.IsDead);
            Assert.AreEqual(1, spot.Stage, "And it doesn't grow.");

            // Watered again, it picks back up.
            spot.AddWater(1f);
            spot.GrowOvernight(true, canDie: true);
            Assert.IsFalse(spot.IsWilted);
            Assert.AreEqual(2, spot.Stage);

            spot.GrowOvernight(true, canDie: true);
            spot.GrowOvernight(true, canDie: true);
            Assert.IsTrue(spot.IsDead, "Two dry nights running kill it.");
            Assert.IsTrue(spot.IsOpen, "The spot can be planted again.");

            // Watering a dead plant does nothing.
            spot.AddWater(1f);
            spot.GrowOvernight(true, canDie: true);
            Assert.IsTrue(spot.IsDead);
            Assert.AreEqual(2, spot.Stage);
        }

        [UnityTest]
        public IEnumerator PlantingOverADeadPlantClearsIt()
        {
            var spot = EmptySpot();
            yield return Grown(spot, Crop("Crop_Cucumber"), 0);
            spot.GrowOvernight(true, canDie: true);
            spot.GrowOvernight(true, canDie: true);
            Assert.IsTrue(spot.IsDead);
            Assert.IsFalse(spot.IsEmpty);

            spot.Plant(Crop("Crop_Carrot"), 0f);
            yield return null;
            Assert.IsFalse(spot.IsDead);
            Assert.AreEqual(-1, spot.Stage);
            Assert.AreEqual("Crop_Carrot", spot.Crop.name);
        }

        [UnityTest]
        public IEnumerator ASeedWaitsAndAReadyRootCropKeeps()
        {
            var spot = EmptySpot();
            spot.Plant(Crop("Crop_Cucumber"), 0f);
            yield return null;
            for (int i = 0; i < 4; i++) spot.GrowOvernight(true, canDie: true);
            Assert.IsFalse(spot.IsDead, "A seed in dry soil just waits.");

            var crop = Crop("Crop_Potato");
            Assert.IsTrue(crop.singleHarvest);
            var other = EmptySpot();
            yield return Grown(other, crop, crop.stages.Length - 1);
            for (int i = 0; i < 4; i++) other.GrowOvernight(true, canDie: true);
            Assert.IsFalse(other.IsDead, "Ready potatoes wait to be dug.");
        }

        [UnityTest]
        public IEnumerator HornwormsLeftOnKillATomato()
        {
            var crop = Crop("Crop_Tomato");
            var spot = EmptySpot();
            // Hornworms find a tomato once it's grown leafy (stage 2).
            yield return Grown(spot, crop, 2);
            Random.InitState(3);
            int tries = 0;
            while (!HasPest(spot) && tries++ < 200) spot.TroubleOvernight(false);
            Assert.IsTrue(HasPest(spot), "A hornworm turned up.");

            spot.AddWater(1f);
            spot.GrowOvernight(true, canDie: true);
            Assert.IsTrue(spot.IsChewed);
            Assert.IsFalse(spot.IsDead);

            spot.AddWater(1f);
            spot.GrowOvernight(true, canDie: true);
            Assert.IsTrue(spot.IsDead, "Left two nights, it's eaten.");
            yield return null;
            Assert.IsFalse(HasPest(spot), "The worms are gone with it.");
        }

        [UnityTest]
        public IEnumerator PickingOffTheHornwormSavesIt()
        {
            var crop = Crop("Crop_Tomato");
            var spot = EmptySpot();
            // Hornworms find a tomato once it's grown leafy (stage 2).
            yield return Grown(spot, crop, 2);
            Random.InitState(3);
            int tries = 0;
            while (!HasPest(spot) && tries++ < 200) spot.TroubleOvernight(false);
            spot.AddWater(1f);
            spot.GrowOvernight(true, canDie: true);
            Assert.IsTrue(spot.IsChewed);

            foreach (var thing in new System.Collections.Generic.List<Pinchable>(spot.Pinchables))
            {
                if (spot.IsPest(thing)) bed.TakeOff(spot, thing);
            }
            spot.AddWater(1f);
            spot.GrowOvernight(true, canDie: true);
            Assert.IsFalse(spot.IsDead);
            Assert.IsFalse(spot.IsChewed, "It recovers.");
        }

        [UnityTest]
        public IEnumerator SuckersOnlyCostTheYield()
        {
            var crop = Crop("Crop_Tomato");
            var spot = EmptySpot();
            yield return Grown(spot, crop, 1);
            for (int i = 0; i < 6; i++)
            {
                spot.TroubleOvernight(true);
                foreach (var thing in new System.Collections.Generic.List<Pinchable>(spot.Pinchables))
                {
                    if (spot.IsPest(thing)) bed.TakeOff(spot, thing);
                }
                spot.AddWater(1f);
                spot.GrowOvernight(spot.IsPruned, canDie: true);
            }
            Assert.IsFalse(spot.IsPruned, "Suckers left on.");
            Assert.IsFalse(spot.IsDead, "Unpruned is fine, just less fruit.");
        }

        [UnityTest]
        public IEnumerator TheBedKillsDryPlantsOvernight()
        {
            var spot = EmptySpot();
            yield return Grown(spot, Crop("Crop_Cucumber"), 1);
            var overnight = typeof(GardenBed).GetMethod("Overnight", BindingFlags.NonPublic | BindingFlags.Instance);
            overnight.Invoke(bed, new object[] { 2 });
            Assert.IsTrue(spot.IsWilted);
            overnight.Invoke(bed, new object[] { 3 });
            Assert.IsTrue(spot.IsDead);

            // And it stays dead through a save.
            var saved = spot.Capture();
            spot.Restore(new PlantSpot.State());
            spot.Restore(saved);
            Assert.IsTrue(spot.IsDead);
        }

        static bool HasPest(PlantSpot spot)
        {
            foreach (var thing in spot.Pinchables) if (spot.IsPest(thing)) return true;
            return false;
        }
    }
}
