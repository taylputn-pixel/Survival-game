using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Player;
using GrandmasHouse.Trade;
using GrandmasHouse.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Firewood the hard way: logs from Walt, home in the truck, split on the block.
    public class WoodTests
    {
        PlayerHands hands;
        Truck truck;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            truck = Homestead.Find<Truck>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        IEnumerator InHand(string key, System.Action<Pickup> got = null)
        {
            var item = Homestead.Make<Pickup>(key);
            item.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.5f;
            yield return null;
            hands.Give(item);
            yield return null;
            got?.Invoke(item);
        }

        static int Count<T>() where T : Component
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<T>()) if (t.gameObject.activeInHierarchy) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator WaltSellsLogsOnHisLogPile()
        {
            Trader walt = null;
            foreach (var t in Object.FindObjectsByType<Trader>()) if (t.Name == "Walt") walt = t;
            int log = -1;
            for (int i = 0; i < walt.Wares.Count; i++) if (walt.Wares[i].key == "Log") log = i;
            Assert.GreaterOrEqual(log, 0, "Walt should sell logs.");
            foreach (var w in walt.Wares) Assert.AreNotEqual("Firewood", w.key, "Firewood comes from splitting now.");

            Bounds counter = ((BoxCollider)typeof(Trader).GetField("counter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(walt)).bounds;
            for (int i = 0; i < 6; i++)
            {
                var egg = Homestead.Make<Pickup>("Egg");
                egg.transform.position = new Vector3(counter.min.x + 0.15f + i * 0.2f, counter.min.y + 0.1f, counter.center.z);
            }
            yield return new WaitForSeconds(0.8f);
            var counts = new int[walt.Wares.Count];
            counts[log] = 2;
            int before = Count<Log>();
            walt.Deal(counts);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(before + 2, Count<Log>());
            foreach (var l in Object.FindObjectsByType<Log>())
            {
                Assert.Less(Vector3.Distance(l.transform.position, new Vector3(60.3f, 0.2f, -146.6f)), 3f, "Logs should be set out on the log pile by the road.");
            }
        }

        [UnityTest]
        public IEnumerator ALogIsTooHeavyToHurry()
        {
            var needs = hands.GetComponent<PlayerNeeds>();
            yield return null;
            float free = hands.Controller.SpeedMultiplier;
            yield return InHand("Log");
            yield return null;
            Assert.IsTrue(hands.Held.NeedsBothHands);
            Assert.Less(hands.Controller.SpeedMultiplier, free * 0.5f, "Carrying a log should slow you to a shuffle.");
            Assert.IsFalse(hands.Controller.BriskAllowed);
        }

        [UnityTest]
        public IEnumerator LogsRideHomeInTheTruckAndSplitIntoFirewood()
        {
            var bed = truck.GetComponentInChildren<TruckBed>();
            for (int i = 0; i < 2; i++)
            {
                yield return InHand("Log");
                Assert.IsTrue(bed.CanInteract(hands));
                Assert.AreEqual("Load it in the truck", bed.PromptVerb(hands));
                bed.Interact(hands);
                yield return new WaitForSeconds(0.4f);
            }
            Assert.AreEqual(2, bed.Load);

            // Drive off with them.
            Assert.AreEqual("Drive the truck", truck.PromptVerb(hands));
            truck.Interact(hands);
            yield return null;
            Assert.IsTrue(hands.Controller.Seated);
            Assert.AreEqual("Get out", hands.Verb);
            Vector3 start = truck.transform.position;
            for (int i = 0; i < 150; i++) truck.Drive(new Vector2(0f, 1f), 0.02f);
            yield return null;
            Assert.Greater(Vector3.Distance(start, truck.transform.position), 10f);
            foreach (var l in Object.FindObjectsByType<Log>())
            {
                Assert.Less(Vector3.Distance(l.transform.position, truck.transform.position), 3f, "The logs should ride in the bed.");
            }
            truck.Leave(hands);
            Assert.IsFalse(hands.Controller.Seated);
            Assert.Greater(Vector3.Distance(hands.transform.position, truck.transform.position), 1f, "You get out beside the truck.");

            // Split one: on the block, then the axe.
            var block = Homestead.Find<ChoppingBlock>();
            Log aLog = Object.FindAnyObjectByType<Log>();
            hands.Give(aLog.GetComponent<Pickup>());
            yield return null;
            Assert.AreEqual("Stand it on the block", block.PromptVerb(hands));
            block.Interact(hands);
            yield return new WaitForSeconds(0.4f);
            Assert.IsNotNull(block.OnBlock);

            yield return InHand("Axe");
            int wood = Count<Firewood>();
            float rest = hands.GetComponent<PlayerNeeds>().State.rest;
            for (int swing = 0; swing < 3; swing++)
            {
                Assert.IsTrue(block.CanInteract(hands));
                Assert.AreEqual("Swing the axe", block.PromptVerb(hands));
                block.Interact(hands);
                yield return new WaitForSeconds(0.6f);
            }
            Assert.IsNull(block.OnBlock, "Three swings should split the log.");
            Assert.AreEqual(wood + 4, Count<Firewood>());
            Assert.Less(hands.GetComponent<PlayerNeeds>().State.rest, rest, "Splitting wood is work.");

            // And it burns.
            var stove = Homestead.Find<WoodStove>();
            Pickup piece = null;
            foreach (var f in Object.FindObjectsByType<Firewood>()) piece = f.GetComponent<Pickup>();
            hands.Take(hands.Held).Drop();
            hands.Give(piece);
            yield return null;
            Assert.IsTrue(stove.CanInteract(hands));
        }

        [UnityTest]
        public IEnumerator TheTruckStopsAtWalls()
        {
            truck.transform.SetPositionAndRotation(new Vector3(8f, 0f, -149f), Quaternion.Euler(0f, 180f, 0f));
            yield return null;
            for (int i = 0; i < 200; i++) truck.Drive(new Vector2(0f, 1f), 0.02f);
            Assert.Greater(truck.transform.position.z, -154.5f, "The truck drove into the store.");
        }

        [UnityTest]
        public IEnumerator TheTruckStopsAtFencesAndTrees()
        {
            // North into the yard fence (its south side runs along z = -8).
            truck.transform.SetPositionAndRotation(new Vector3(7f, 0f, -14f), Quaternion.identity);
            yield return null;
            for (int i = 0; i < 300; i++) truck.Drive(new Vector2(0f, 1f), 0.02f);
            Assert.Less(truck.transform.position.z, -8f - 2f, "The truck drove through the fence.");
            Assert.Greater(truck.transform.position.z, -12f, "It should get right up to the fence.");

            // North into a roadside tree.
            truck.transform.SetPositionAndRotation(new Vector3(11.3f, 0f, -50f), Quaternion.identity);
            yield return null;
            for (int i = 0; i < 300; i++) truck.Drive(new Vector2(0f, 1f), 0.02f);
            Assert.Less(truck.transform.position.z, -41.2f - 2f, "The truck drove through the tree.");
        }

        [UnityTest]
        public IEnumerator TheTruckIsParkedWhereItWasLeft()
        {
            truck.transform.SetPositionAndRotation(new Vector3(20f, 0f, -60f), Quaternion.Euler(0f, 90f, 0f));
            string saved = truck.Capture();
            truck.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            truck.Restore(saved);
            yield return null;
            Assert.Less(Vector3.Distance(truck.transform.position, new Vector3(20f, 0f, -60f)), 0.01f);
            Assert.AreEqual(90f, truck.transform.eulerAngles.y, 0.5f);
        }
    }
}
