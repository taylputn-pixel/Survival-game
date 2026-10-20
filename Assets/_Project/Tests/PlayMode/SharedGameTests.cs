using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Net;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The Homestead as a shared game, hosted here with nobody else joined.
    public class SharedGameTests
    {
        MemorySaveStore store;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            store = Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [Test]
        public void OpeningTheHomesteadHostsAGame()
        {
            Assert.IsTrue(NetworkManager.Singleton.IsHost);
            Assert.AreEqual(1, PlayerNet.All.Count);
            Assert.IsTrue(PlayerNet.All[0].IsOwner);
            foreach (var item in Object.FindObjectsByType<ItemNet>())
            {
                Assert.IsTrue(item.IsSpawned, item.name + " isn't shared.");
            }
        }

        [Test]
        public void ThereIsABedForEachOfFourPlayers()
        {
            Assert.AreEqual(NetSession.MaxPlayers, Object.FindObjectsByType<Bed>().Length);
        }

        [UnityTest]
        public IEnumerator PickingUpGoesThroughTheHost()
        {
            var hands = Homestead.Find<PlayerHands>();
            var onion = Homestead.Make<Pickup>("Onion");
            onion.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.6f;
            onion.GetComponent<NetworkObject>().Spawn();
            yield return null;

            hands.Handle(onion);
            yield return null;
            Assert.IsTrue(hands.IsHolding(onion));
            Assert.AreEqual(NetworkManager.Singleton.LocalClientId, onion.GetComponent<ItemNet>().Holder);

            hands.TakeHeld().Drop();
            yield return null;
            Assert.AreEqual(ItemNet.NoOne, onion.GetComponent<ItemNet>().Holder);
        }

        [UnityTest]
        public IEnumerator TheNightHurriesOnlyOnceEveryoneIsAsleep()
        {
            var clock = Homestead.Find<DayClock>();
            Assume.That(!clock.Mode.clockStopsAtNight, "Only when the clock runs all night.");
            var needs = Homestead.Find<PlayerNeeds>();

            needs.Asleep = true;
            yield return null;
            yield return null;
            Assert.Greater(clock.FastForward, 0f, "Everyone (just you) is asleep, but the night isn't hurrying.");

            needs.Asleep = false;
            yield return null;
            yield return null;
            Assert.AreEqual(0f, clock.FastForward);
        }

        [UnityTest]
        public IEnumerator SleepersLieInTheirBedAndGetOutBesideIt()
        {
            var hands = Homestead.Find<PlayerHands>();
            var needs = hands.GetComponent<PlayerNeeds>();
            var bed = Homestead.Find<Bed>();
            needs.Set(80f, 20f);
            bed.Interact(hands);
            float giveUp = Time.time + 5f;
            while (!needs.Asleep && Time.time < giveUp) yield return null;
            Assert.IsTrue(needs.Asleep, "Never fell asleep.");
            Vector3 flat = hands.transform.position - bed.transform.position;
            flat.y = 0f;
            Assert.Less(flat.magnitude, 0.1f, "Not lying in the bed.");
            Assert.IsTrue(hands.Controller.LyingDown);

            needs.Set(80f, 100f);
            giveUp = Time.time + 5f;
            while (!hands.Controller.InputEnabled && Time.time < giveUp) yield return null;
            Assert.IsFalse(needs.Asleep);
            Assert.IsFalse(hands.Controller.LyingDown);
            Assert.IsTrue(hands.GetComponent<CharacterController>().enabled);
            flat = hands.transform.position - bed.transform.position;
            flat.y = 0f;
            Assert.Greater(flat.magnitude, 0.8f, "Got up inside the bed.");
        }

        [UnityTest]
        public IEnumerator CollapsingWithNobodyElseStandingIsGameOver()
        {
            var needs = Homestead.Find<PlayerNeeds>();
            bool over = false;
            needs.GameOver += () => over = true;
            needs.Set(0f, 80f, needs.Settings.collapseAfterHours);
            yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(needs.Collapsed);
            Assert.IsFalse(needs.GetComponent<FirstPersonController>().InputEnabled);
            Assert.IsTrue(over, "Alone and collapsed, but the game isn't over.");
        }

        [UnityTest]
        public IEnumerator BeingFedBringsYouRound()
        {
            var needs = Homestead.Find<PlayerNeeds>();
            needs.Set(0f, 80f, needs.Settings.collapseAfterHours);
            yield return null;
            Assert.IsTrue(needs.Collapsed);

            needs.Fed(10f, 0f);
            Assert.IsFalse(needs.Collapsed);
            Assert.IsTrue(needs.GetComponent<FirstPersonController>().InputEnabled);
            Assert.IsTrue(needs.State.IsMalnourished(needs.Settings), "Came round fully healed; should still be weak.");
        }

        [UnityTest]
        public IEnumerator AGuestsGardenFollowsTheHostsAndKeepsWhatsTheSame()
        {
            var bed = Homestead.Find<GrandmasHouse.Garden.GardenBed>();
            var spots = new System.Collections.Generic.List<GrandmasHouse.Garden.PlantSpot>(bed.Spots);
            var crop = SaveManager.Catalog.Crop("Crop_Tomato");
            Assert.IsNotNull(crop);

            // What the host would send: a tomato in the first spot, half watered.
            bed.Plant(spots[0], crop, 0f);
            spots[0].AddWater(0.5f);
            yield return null;
            string host = bed.Capture();
            int hostWeeds = bed.Weeds.Count;

            // The guest's copy differs: nothing planted, a different weed count.
            bed.Restore("{\"spots\":[],\"weeds\":[]}");
            spots[0].Restore(new GrandmasHouse.Garden.PlantSpot.State());
            Assert.IsTrue(spots[0].IsEmpty);
            bed.ApplyShared(host);
            Assert.IsFalse(spots[0].IsEmpty, "The host's tomato didn't show up.");
            Assert.AreEqual(0.5f, spots[0].Water, 0.01f);
            Assert.AreEqual(hostWeeds, bed.Weeds.Count);

            // Only the water changed: the plant isn't rebuilt.
            spots[0].AddWater(0.3f);
            string wetter = bed.Capture();
            spots[0].AddWater(-0.3f);
            bed.ApplyShared(wetter);
            Assert.AreEqual(0.8f, spots[0].Water, 0.01f);
        }

        [UnityTest]
        public IEnumerator EachPlayerIsSavedByTheirKey()
        {
            var needs = Homestead.Find<PlayerNeeds>();
            needs.Set(42f, 61f);
            SaveManager.Instance.Save();
            var data = JsonUtility.FromJson<SaveData>(store.Text);
            Assert.AreEqual(1, data.players.Count);
            Assert.AreEqual(NetSession.LocalHostKey, data.players[0].key);
            StringAssert.Contains("42", data.players[0].needs);

            SaveSlot.LoadOnStart = true;
            yield return Homestead.Load();
            Assert.AreEqual(42f, Homestead.Find<PlayerNeeds>().State.hunger, 0.5f);
        }
    }
}
