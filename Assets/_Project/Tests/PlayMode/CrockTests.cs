using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Sauerkraut in the cellar crock.
    public class CrockTests
    {
        PlayerHands hands;
        Crock crock;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            crock = Homestead.Find<Crock>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        IEnumerator PackShreds(int count)
        {
            var pieces = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(piece.GetComponent<Collider>());
                piece.name = "Cabbage Slice";
                pieces.Add(piece.transform);
            }
            var handful = Handful.Make(pieces, count);
            handful.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            hands.Give(handful.Pickup);
            yield return null;
            Assert.IsTrue(crock.CanInteract(hands));
            Assert.AreEqual("Pack into the crock", crock.PromptVerb(hands));
            crock.Interact(hands);
            yield return null;
        }

        IEnumerator ScoopABowl(System.Action<Bowl> got)
        {
            var bowl = Homestead.Make<Pickup>("Bowl");
            bowl.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            hands.Give(bowl);
            yield return null;
            Assert.IsTrue(crock.CanInteract(hands));
            Assert.AreEqual("Scoop into the bowl", crock.PromptVerb(hands));
            crock.Interact(hands);
            got(bowl.GetComponent<Bowl>());
        }

        [UnityTest]
        public IEnumerator SaltedCabbageTurnsToSauerkrautInAWeek()
        {
            yield return PackShreds(24);
            Assert.AreEqual(24, crock.Shreds);
            Assert.IsTrue(hands.IsEmpty);
            crock.Restore("{\"shreds\":24,\"salt\":2,\"days\":0,\"spoiled\":false}");
            for (int night = 0; night < 6; night++) crock.Ferment();
            Assert.IsTrue(crock.Ready);

            Bowl bowl = null;
            yield return ScoopABowl(b => bowl = b);
            Assert.AreEqual("Sauerkraut", bowl.Dish);
            Assert.Greater(bowl.Food, 8f);
            Assert.IsFalse(bowl.GetComponent<Perishable>().IsRotten);
            Assert.AreEqual(16, crock.Shreds);
        }

        [UnityTest]
        public IEnumerator UnsaltedCabbageGoesBad()
        {
            yield return PackShreds(12);
            crock.Ferment();
            crock.Ferment();
            Assert.IsTrue(crock.Spoiled);
            Bowl bowl = null;
            yield return ScoopABowl(b => bowl = b);
            Assert.IsTrue(bowl.GetComponent<Perishable>().IsRotten, "Spoiled kraut should make you sick.");
        }

        [Test]
        public void TheCrockRemembersItsKraut()
        {
            crock.Restore("{\"shreds\":30,\"salt\":3,\"days\":4,\"spoiled\":false}");
            string saved = crock.Capture();
            crock.Restore("{\"shreds\":0,\"salt\":0,\"days\":0,\"spoiled\":false}");
            crock.Restore(saved);
            Assert.AreEqual(30, crock.Shreds);
            Assert.AreEqual(4, crock.Days);
        }
    }
}
