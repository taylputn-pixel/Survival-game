using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Garden;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Save the Homestead, load it again the way Continue does, and check
    // it came back the same.
    public class SaveLoadTests
    {
        // A sealed, labeled jar of dill pickles, as a jar saves itself.
        const string SealedPickles =
            "{\"slices\":20,\"cloves\":0,\"wedges\":0,\"lemon\":false,\"salsa\":false,\"badBatch\":false," +
            "\"kinds\":[\"Cucumber\"],\"kindAmounts\":[20.0],\"peppercorns\":0,\"dill\":true,\"brine\":0.88," +
            "\"lid\":true,\"processed\":true,\"sealedUp\":true,\"labeled\":true}";

        MemorySaveStore store;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            store = Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        static JarContents MakeJar(string state, Vector3 at)
        {
            var jar = Homestead.Make<JarContents>("PintJar");
            jar.transform.position = at;
            jar.Restore(state);
            return jar;
        }

        static List<string> ItemKeys()
        {
            var keys = new List<string>();
            foreach (var item in Object.FindObjectsByType<SavedItem>()) keys.Add(item.Key);
            keys.Sort();
            return keys;
        }

        IEnumerator SaveAndContinue()
        {
            SaveManager.Instance.Save();
            Assert.IsTrue(store.Exists(), "Nothing was saved.");
            SaveSlot.LoadOnStart = true;
            yield return Homestead.Load();
        }

        [UnityTest]
        public IEnumerator TheDayAndHourComeBack()
        {
            var clock = Homestead.Find<DayClock>();
            clock.SleepUntilMorning();
            clock.SleepUntilMorning();
            int day = clock.Day;
            float hour = clock.Hour;

            yield return SaveAndContinue();

            clock = Homestead.Find<DayClock>();
            Assert.AreEqual(day, clock.Day);
            Assert.AreEqual(hour, clock.Hour, 0.01f);
        }

        [UnityTest]
        public IEnumerator PlantsComeBackAsTheyWere()
        {
            var bed = Homestead.Find<GardenBed>();
            var clock = Homestead.Find<DayClock>();
            int index = -1;
            for (int i = 0; i < bed.Spots.Count && index < 0; i++) if (bed.Spots[i].IsEmpty) index = i;
            Assert.GreaterOrEqual(index, 0, "No empty spot to plant.");
            var spot = bed.Spots[index];
            spot.Plant(SaveManager.Catalog.Crop("Crop_Tomato"), 0f);
            yield return null;
            spot.AddWater(1f);
            clock.SleepUntilMorning();
            spot.AddWater(0.6f);
            string bedName = bed.name;
            var before = spot.Capture();

            yield return SaveAndContinue();

            GardenBed loaded = null;
            foreach (var b in Object.FindObjectsByType<GardenBed>()) if (b.name == bedName) loaded = b;
            Assert.IsNotNull(loaded, bedName + " is gone.");
            var after = loaded.Spots[index].Capture();
            Assert.AreEqual("Crop_Tomato", after.crop);
            Assert.AreEqual(0, after.stage);
            Assert.AreEqual(0.6f, after.water, 0.001f);
            // The turn goes through a rotation and back, so it can shift by a hair.
            Assert.AreEqual(before.yaw, after.yaw, 0.01f);
            after.yaw = before.yaw;
            Assert.AreEqual(JsonUtility.ToJson(before), JsonUtility.ToJson(after));
        }

        [UnityTest]
        public IEnumerator ASealedJarComesBackWithItsPickles()
        {
            var jar = MakeJar(SealedPickles, new Vector3(0f, 50f, 0f));
            jar.GetComponent<Carrying.Pickup>().SetResting(true, true);
            yield return null;
            Assert.AreEqual("Dill Pickle Chips", jar.Contents);
            string before = jar.Capture();
            int jars = Object.FindObjectsByType<JarContents>().Length;

            yield return SaveAndContinue();

            Assert.AreEqual(jars, Object.FindObjectsByType<JarContents>().Length, "Jars went missing or doubled.");
            JarContents found = null;
            foreach (var j in Object.FindObjectsByType<JarContents>()) if (j.IsLabeled) found = j;
            Assert.IsNotNull(found, "The labeled jar didn't come back.");
            Assert.AreEqual("Dill Pickle Chips", found.Contents);
            Assert.IsTrue(found.IsSealed);
            Assert.AreEqual(before, found.Capture());
        }

        [UnityTest]
        public IEnumerator EverySavedItemComesBack()
        {
            var keys = ItemKeys();
            Assert.IsNotEmpty(keys);

            yield return SaveAndContinue();

            CollectionAssert.AreEqual(keys, ItemKeys());
        }
    }
}
