using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Saving;
using GrandmasHouse.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Moving goods about: a crate for jars, a sack for produce, a basket for
    // eggs, all found around the homestead, and all ride in the truck bed.
    public class TransportTests
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

        static T Find<T>(string key) where T : Component
        {
            foreach (var saved in Object.FindObjectsByType<SavedItem>()) if (saved.Key == key) return saved.GetComponent<T>();
            Assert.Fail("No " + key + " around the homestead.");
            return null;
        }

        [UnityTest]
        public IEnumerator EachHoldsItsOwnKindOfGoods()
        {
            var crate = Find<Container>("JarCrate");
            var sack = Find<Container>("BurlapSack");
            var basket = Find<Container>("EggBasket");
            var jar = Homestead.Make<Pickup>("PintJar");
            var cucumber = Homestead.Make<Pickup>("Cucumber");
            var egg = Homestead.Make<Pickup>("Egg");
            yield return null;

            Assert.IsTrue(crate.Accepts(jar), "Jars go in the crate.");
            Assert.IsFalse(crate.Accepts(cucumber));
            Assert.IsTrue(sack.Accepts(cucumber), "Produce goes in the sack.");
            Assert.IsFalse(sack.Accepts(jar));
            Assert.IsTrue(basket.Accepts(egg), "Eggs go in the egg basket.");
            Assert.IsFalse(basket.Accepts(jar));

            // A crate takes a dozen pints.
            for (int i = 0; i < 12; i++)
            {
                var another = Homestead.Make<Pickup>("PintJar");
                yield return null;
                Assert.IsTrue(crate.Accepts(another), "Room for jar " + (i + 1));
                crate.Add(another, 0f);
            }
            Assert.IsTrue(crate.IsFull);
        }

        [UnityTest]
        public IEnumerator ACrateOfJarsRidesInTheTruck()
        {
            var crate = Find<Container>("JarCrate");
            for (int i = 0; i < 4; i++)
            {
                var jar = Homestead.Make<Pickup>("PintJar");
                yield return null;
                crate.Add(jar, 0f);
            }
            yield return new WaitForSeconds(0.2f);
            var crateItem = crate.GetComponent<Pickup>();
            crateItem.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.6f;
            yield return null;
            hands.Give(crateItem);
            yield return null;

            var truck = Homestead.Find<Truck>();
            var bed = truck.GetComponentInChildren<TruckBed>();
            Assert.IsTrue(bed.CanInteract(hands), "A crate loads into the truck.");
            Assert.AreEqual("Load it in the truck", bed.PromptVerb(hands));
            bed.Interact(hands);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, bed.Load);

            truck.SitIn(hands);
            Vector3 start = truck.transform.position;
            for (int i = 0; i < 150; i++) truck.Drive(new Vector2(0f, 1f), 0.02f);
            yield return null;
            truck.Leave(hands);
            Assert.Greater(Vector3.Distance(start, truck.transform.position), 10f);
            Assert.Less(Vector3.Distance(crateItem.transform.position, truck.transform.position), 3f, "The crate rides in the bed.");
            foreach (var jar in crate.Contents)
            {
                Assert.Less(Vector3.Distance(jar.transform.position, crateItem.transform.position), 0.4f, "And the jars ride in the crate.");
            }
        }
    }
}
