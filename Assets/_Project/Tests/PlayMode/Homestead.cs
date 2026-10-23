using System.Collections;
using GrandmasHouse.Saving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandmasHouse.Tests
{
    // Saves made during a test stay in memory, never in the player's save file.
    public class MemorySaveStore : ISaveStore
    {
        public string Text;

        public bool Exists() => Text != null;
        public string Read() => Text;
        public void Write(string text) => Text = text;
    }

    // Loads the real Homestead scene for a test, with saves kept in memory.
    public static class Homestead
    {
        public const string Scene = "Homestead";

        static ISaveStore realStore;

        public static MemorySaveStore UseMemorySaves()
        {
            realStore ??= SaveSlot.Store;
            var store = new MemorySaveStore();
            SaveSlot.Store = store;
            SaveSlot.LoadOnStart = false;
            // Play mode stalls when the editor isn't focused.
            Application.runInBackground = true;
            // Host without Steam, so tests never open a Steam lobby.
            GrandmasHouse.Net.NetSession.Direct = true;
            return store;
        }

        public static void RestoreRealSaves()
        {
            if (realStore != null) SaveSlot.Store = realStore;
            SaveSlot.LoadOnStart = false;
        }

        // Loads the scene and waits until it's ready to play: set up, and
        // done loading a save if one was asked for. A new game starts with the
        // canning things packed away and an empty coop; most kitchen tests
        // want the canning things set up, and most tests want a grown flock.
        public static IEnumerator Load(bool canningSetUp = true, bool hens = true)
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            float giveUp = Time.realtimeSinceStartup + 10f;
            // Ready once the game is hosted and this computer's player has arrived.
            while (SaveManager.Instance == null || SaveManager.Instance.IsLoading || SaveSlot.LoadOnStart
                || Object.FindAnyObjectByType<GrandmasHouse.Carrying.PlayerHands>() == null)
            {
                Assert.Less(Time.realtimeSinceStartup, giveUp, "The Homestead never finished loading.");
                yield return null;
            }
            var kit = Object.FindAnyObjectByType<GrandmasHouse.Kitchen.CanningKit>();
            if (canningSetUp && kit != null && !kit.IsSetUp)
            {
                kit.Restore("{\"setUp\":true}");
                // The box it came out of goes too.
                foreach (var box in Object.FindObjectsByType<GrandmasHouse.Kitchen.CanningBox>()) Object.Destroy(box.gameObject);
                yield return null;
            }
            var flock = Object.FindAnyObjectByType<GrandmasHouse.Animals.Flock>();
            if (hens && flock != null && flock.Alive == 0)
            {
                // Every place in the coop holds a grown hen.
                int n = flock.Hens.Count;
                string Repeat(string v) => string.Join(",", System.Linq.Enumerable.Repeat(v, n));
                string grown = flock.Settings.daysToLay.ToString();
                flock.Restore("{\"feed\":" + flock.Feed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ",\"water\":" + flock.Water.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ",\"alive\":[" + Repeat("true") + "],\"hungry\":[" + Repeat("0") + "],\"ages\":[" + Repeat(grown) + "],\"feathersDay\":-1}");
                yield return null;
            }
        }

        // A fresh copy of an item, from the save catalog by its key.
        public static T Make<T>(string key) where T : Component
        {
            var prefab = SaveManager.Catalog.Item(key);
            Assert.IsNotNull(prefab, key + " isn't in the save catalog.");
            var item = Object.Instantiate(prefab);
            item.name = prefab.name;
            return item.GetComponent<T>();
        }

        public static T Find<T>() where T : Object
        {
            var found = Object.FindAnyObjectByType<T>();
            Assert.IsNotNull(found, "No " + typeof(T).Name + " in the Homestead.");
            return found;
        }
    }
}
