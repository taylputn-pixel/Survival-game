using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Interaction;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Trade;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The general store and the neighbors: weighing goods on the counter,
    // shaking on it, the store's ledger, and what runs out.
    public class TradeTests
    {
        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        static Trader Named(string name)
        {
            foreach (var t in Object.FindObjectsByType<Trader>()) if (t.Name == name) return t;
            Assert.Fail("No trader called " + name);
            return null;
        }

        static BoxCollider Counter(Trader t) =>
            (BoxCollider)typeof(Trader).GetField("counter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(t);

        // Sets goods down on a trader's counter and lets them settle.
        static IEnumerator OnCounter(Trader t, params string[] keys)
        {
            Bounds area = Counter(t).bounds;
            for (int i = 0; i < keys.Length; i++)
            {
                var item = Homestead.Make<Pickup>(keys[i]);
                float x = area.min.x + 0.15f + (i % 8) * 0.15f;
                item.transform.position = new Vector3(x, area.min.y + 0.12f + (i / 8) * 0.1f, area.center.z);
            }
            yield return new WaitForSeconds(0.8f);
        }

        static int Ware(Trader t, string key)
        {
            for (int i = 0; i < t.Wares.Count; i++) if (t.Wares[i].key == key) return i;
            Assert.Fail(t.Name + " doesn't have " + key);
            return -1;
        }

        static int CountNear(string displayName, Vector3 near, float within)
        {
            int n = 0;
            foreach (var p in Object.FindObjectsByType<Pickup>())
            {
                if (p.DisplayName == displayName && p.gameObject.activeInHierarchy && Vector3.Distance(p.transform.position, near) < within) n++;
            }
            return n;
        }

        [UnityTest]
        public IEnumerator TheStoreTakesEggsAndPutsTheRestOnTheAccount()
        {
            var june = Named("June");
            Assert.IsTrue(june.IsOpen, "The store should be open in the morning.");
            yield return OnCounter(june, "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg", "Egg");
            var offers = june.OnCounter();
            Assert.AreEqual(24, offers.Count, "Every egg on the counter should be weighed.");
            float yours = 0f;
            foreach (var o in offers) yours += o.worth;

            int salt = Ware(june, "Salt");
            var counts = new int[june.Wares.Count];
            counts[salt] = 1;
            float theirs = june.Price(june.Wares[salt]);
            Assert.Less(theirs, yours);
            int stockBefore = june.Wares[salt].stock;

            june.Deal(counts);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, june.OnCounter().Count, "The eggs should be gone.");
            Assert.AreEqual(stockBefore - 1, june.Wares[salt].stock);
            Assert.AreEqual(yours - theirs, june.Credit, 0.01f, "The rest goes on Grandma's account.");
            Assert.AreEqual(1, CountNear("Salt", Counter(june).bounds.center, 5f), "A box of salt should be set out at the end of the counter.");

            // Next time the account pays for it, with nothing on the counter.
            counts[salt] = 0;
            counts[Ware(june, "Vinegar")] = 1;
            float credit = june.Credit;
            Assert.IsTrue(june.CanDeal(0f, june.Price(june.Wares[Ware(june, "Vinegar")])) == credit >= june.Price(june.Wares[Ware(june, "Vinegar")]));
        }

        [UnityTest]
        public IEnumerator ANeighborOnlyTakesWhatTheyWantAndTradesRecipes()
        {
            var ada = Named("Ada");
            yield return OnCounter(ada, "Tomato", "Tomato", "Potato", "Onion", "Onion", "Onion", "Cabbage", "Cabbage", "Cabbage", "Cabbage", "Cabbage", "Cabbage");
            var offers = ada.OnCounter();
            foreach (var o in offers) Assert.AreNotEqual("Potato", o.item.DisplayName, "Ada doesn't want potatoes.");
            Assert.AreEqual(11, offers.Count);

            int recipe = -1;
            for (int i = 0; i < ada.Wares.Count; i++) if (ada.Wares[i].recipe != null) recipe = i;
            var counts = new int[ada.Wares.Count];
            counts[recipe] = 1;
            var discoveries = Homestead.Find<Discoveries>();
            Assert.IsFalse(discoveries.HasFound(ada.Wares[recipe].recipe));
            ada.Deal(counts);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(discoveries.HasFound(ada.Wares[recipe].recipe), "The recipe should be in the journal.");
            Assert.AreEqual(0, ada.Credit, "Neighbors keep no account.");
            Assert.AreEqual(1, CountNear("Potato", Counter(ada).bounds.center, 2f), "The potato she didn't want stays on the table.");
        }

        [UnityTest]
        public IEnumerator NoDealIfTheirSideOutweighsYours()
        {
            var walt = Named("Walt");
            yield return OnCounter(walt, "Egg");
            var counts = new int[walt.Wares.Count];
            counts[Ware(walt, "Log")] = 1;
            int stock = walt.Wares[Ware(walt, "Log")].stock;
            walt.Deal(counts);
            yield return null;
            Assert.AreEqual(stock, walt.Wares[Ware(walt, "Log")].stock, "One egg isn't worth a log.");
            Assert.AreEqual(1, walt.OnCounter().Count, "The egg stays.");
        }

        [UnityTest]
        public IEnumerator TradersGoInAtNight()
        {
            var june = Named("June");
            var clock = Homestead.Find<DayClock>();
            clock.Advance(Mathf.Max(0f, 22f - clock.Hour));
            yield return null;
            Assert.IsFalse(june.IsOpen);
            Assert.IsFalse(june.CanInteract(Homestead.Find<PlayerHands>()));
        }

        [Test]
        public void EggsAreDearInWinter()
        {
            var settings = Named("June").GetType().GetField("settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Named("June")) as TradeSettings;
            Assert.Greater(settings.BaseWorth("Egg", 3), settings.BaseWorth("Egg", 0) * 1.5f);
        }

        [UnityTest]
        public IEnumerator SaltRunsOut()
        {
            var hands = Homestead.Find<PlayerHands>();
            var salt = Homestead.Make<Pickup>("Salt");
            salt.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            var box = salt.GetComponent<Seasoning>();
            Assert.AreEqual(40f, box.Left, 0.01f);
            box.Restore("1");
            hands.Give(salt);
            yield return null;
            var crock = Homestead.Find<Crock>();
            Assert.IsTrue(crock.CanInteract(hands));
            crock.Interact(hands);
            Assert.IsTrue(box.IsEmpty);
            Assert.IsFalse(crock.CanInteract(hands), "An empty salt box salts nothing.");
        }
    }
}
