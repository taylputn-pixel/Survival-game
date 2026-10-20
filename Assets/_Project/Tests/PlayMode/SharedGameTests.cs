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
