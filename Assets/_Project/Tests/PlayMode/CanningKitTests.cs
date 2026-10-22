using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The canning things put away to free the counter, and set up again.
    public class CanningKitTests
    {
        PlayerHands hands;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [UnityTest]
        public IEnumerator ANewGameFindsItPackedInThePantry()
        {
            yield return Homestead.Load(canningSetUp: false);
            hands = Homestead.Find<PlayerHands>();
            var kit = Homestead.Find<CanningKit>();
            Assert.IsFalse(kit.IsSetUp, "Packed away to start with.");
            Assert.IsFalse(Homestead.Find<CanningPutAway>().GetComponentInChildren<Collider>().enabled, "The counter is clear.");
            var box = Homestead.Find<CanningBox>();
            Assert.Less(Vector3.Distance(box.transform.position, new Vector3(5.6f, 0f, 5.2f)), 0.3f, "The box is on the pantry floor.");

            // Carry it to the counter and set up.
            hands.Give(box.GetComponent<Pickup>());
            yield return new WaitForSeconds(0.3f);
            var counter = Homestead.Find<CanningCounter>();
            Assert.IsTrue(counter.CanInteract(hands));
            counter.Interact(hands);
            yield return null;
            Assert.IsTrue(kit.IsSetUp);
        }

        [UnityTest]
        public IEnumerator PutAwayAndSetUpAgain()
        {
            var kit = Homestead.Find<CanningKit>();
            var towel = Homestead.Find<CanningPutAway>();
            var counter = Homestead.Find<CanningCounter>();
            var towelSolid = towel.GetComponentInChildren<Collider>();
            Assert.IsTrue(kit.IsSetUp, "Set up to start with.");

            // A jar left on the towel stays on the counter.
            var packing = Homestead.Find<Stations.JarPackingStation>();
            var jar = Homestead.Make<Pickup>("PintJar");
            yield return null;
            hands.Give(jar);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(packing.CanInteract(hands));
            packing.Interact(hands);
            yield return new WaitForSeconds(0.4f);
            Assert.IsNotNull(jar.transform.parent, "The jar sits on the towel.");

            Assert.IsTrue(towel.CanAltInteract(hands));
            Assert.AreEqual("Put the canning things away", towel.AltVerb(hands));
            towel.AltInteract(hands);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(kit.IsSetUp);
            Assert.IsFalse(towelSolid.enabled, "The towel's gone from the counter.");
            Assert.IsTrue(CanningKit.IsBox(hands.Held), "The box is in your hands.");
            Assert.IsNull(jar.transform.parent, "The jar stays where it was.");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(jar.CollidersOn, "And can be picked up.");
            StringAssert.Contains("false", kit.Capture());

            Assert.IsTrue(counter.CanInteract(hands));
            Assert.AreEqual("Set up for canning", counter.PromptVerb(hands));
            counter.Interact(hands);
            yield return null;
            Assert.IsTrue(kit.IsSetUp);
            Assert.IsTrue(hands.IsEmpty);
            Assert.IsTrue(towelSolid.enabled);
            Assert.IsFalse(counter.CanInteract(hands));
            Assert.AreEqual(0, Object.FindObjectsByType<CanningBox>().Length, "The box is unpacked.");

            // Loaded put away, it's put away.
            kit.Restore("{\"setUp\":false}");
            Assert.IsFalse(kit.IsSetUp);
            Assert.IsFalse(towelSolid.enabled);
        }
    }
}
