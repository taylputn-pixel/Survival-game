using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Grandma's skillet: eggs and potatoes fried on a burner and served on
    // a plate; left on too long, it burns.
    public class SkilletTests
    {
        PlayerHands hands;
        Skillet pan;
        Burner burner;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            pan = Homestead.Find<Skillet>();
            // Quicker for the test: fried in a second, burnt in three.
            var f = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(Skillet).GetField("fryAfter", f).SetValue(pan, 1f);
            typeof(Skillet).GetField("burnAfter", f).SetValue(pan, 3f);
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        IEnumerator InHand(string key)
        {
            var item = Homestead.Make<Pickup>(key);
            item.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.5f;
            yield return null;
            hands.Give(item);
            yield return new WaitForSeconds(0.2f);
        }

        static List<Transform> Slices(string kind, int count)
        {
            var list = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(s.GetComponent<Collider>());
                s.name = kind + " Slice";
                list.Add(s.transform);
            }
            return list;
        }

        IEnumerator OnTheStove(bool lit)
        {
            hands.Give(pan.GetComponent<Pickup>());
            yield return new WaitForSeconds(0.2f);
            foreach (var b in Object.FindObjectsByType<Burner>()) if (b.Occupant == null) burner = b;
            Assert.IsTrue(burner.CanInteract(hands));
            burner.Interact(hands);
            yield return new WaitForSeconds(0.4f);
            burner.SetOn(lit);
        }

        [UnityTest]
        public IEnumerator FriedEggsAndPotatoesMakeAPlate()
        {
            Assert.IsFalse(pan.HasFood);
            for (int i = 0; i < 2; i++)
            {
                yield return InHand("Egg");
                Assert.IsTrue(pan.CanInteract(hands));
                Assert.AreEqual("Crack an egg into the skillet", pan.PromptVerb(hands));
                pan.Interact(hands);
                yield return null;
                Assert.IsTrue(hands.IsEmpty);
            }
            pan.AddPieces(Slices("Potato", 6));
            yield return InHand("Salt");
            Assert.AreEqual("Add a pinch of salt", pan.PromptVerb(hands));
            pan.Interact(hands);
            hands.TakeHeld().Drop();
            Assert.AreEqual("Uncooked fried eggs and potatoes", pan.Dish);
            float raw = pan.FullFood;

            yield return OnTheStove(true);
            yield return new WaitForSeconds(1.4f);
            Assert.IsTrue(pan.Fried);
            Assert.IsFalse(pan.Burnt);
            Assert.AreEqual("Fried eggs and potatoes", pan.Dish);
            Assert.Greater(pan.FullFood, raw, "Frying makes it go further than raw.");
            burner.SetOn(false);

            yield return InHand("Bowl");
            Assert.IsFalse(pan.CanInteract(hands), "Fry-ups go on a plate, not in a soup bowl.");
            hands.TakeHeld().Drop();
            yield return InHand("Plate");
            Assert.IsTrue(pan.CanInteract(hands));
            Assert.AreEqual("Serve a plate", pan.PromptVerb(hands));
            float food = pan.FullFood;
            Assert.AreEqual(1, pan.Plates, "Two eggs and a potato feed one.");
            var bowl = hands.Held.GetComponent<Bowl>();
            Assert.IsTrue(bowl.IsPlate);
            pan.Interact(hands);
            Assert.AreEqual(food, bowl.Food, 0.01f);
            Assert.AreEqual("Fried eggs and potatoes", bowl.Dish);
            Assert.That(bowl.Food, Is.InRange(18f, 30f), "About a meal.");
            Assert.IsFalse(pan.HasFood, "The pan's empty once served.");
        }

        [UnityTest]
        public IEnumerator LeftOnTheHeatItBurns()
        {
            pan.AddPieces(Slices("Potato", 12));
            pan.AddPieces(Slices("Onion", 4));
            pan.AddEgg();
            yield return OnTheStove(true);
            yield return new WaitForSeconds(1.4f);
            Assert.AreEqual("Potato and onion hash", pan.Dish);
            float good = pan.FullFood;
            Assert.AreEqual(2, pan.Plates, "A hash feeds two.");
            yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(pan.Burnt);
            StringAssert.StartsWith("Burnt", pan.Dish);
            Assert.Less(pan.FullFood, good * 0.6f);

            // It keeps what's in it through a save.
            string saved = pan.Capture();
            pan.Empty();
            pan.Restore(saved);
            Assert.IsTrue(pan.Burnt);
            Assert.AreEqual(3, pan.Counts().Count);
        }

        static Color Look(Transform piece, string part)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                if (r.name != part) continue;
                r.GetPropertyBlock(block);
                return block.GetColor("_BaseColor");
            }
            Assert.Fail("No " + part);
            return default;
        }

        [UnityTest]
        public IEnumerator YouCanSeeAnEggCook()
        {
            pan.AddEgg();
            Transform egg = null;
            foreach (var t in pan.GetComponentsInChildren<Transform>()) if (t.name == "Egg") egg = t;
            Assert.AreEqual(Skillet.Doneness.Raw, pan.Stage);
            Color rawWhite = Look(egg, "White"), rawYolk = Look(egg, "Yolk"), rawEdge = Look(egg, "Edge");
            Assert.Less(rawWhite.r, 0.8f, "A raw white is still clear gray.");

            yield return OnTheStove(true);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(Skillet.Doneness.Cooking, pan.Stage);
            StringAssert.StartsWith("Half-cooked", pan.Dish);

            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(Skillet.Doneness.Done, pan.Stage);
            Color white = Look(egg, "White"), yolk = Look(egg, "Yolk"), edge = Look(egg, "Edge");
            Assert.Greater(white.r, 0.95f, "Set whites are white.");
            Assert.Greater(yolk.g, rawYolk.g + 0.15f, "The yolk goes from deep orange to yellow.");
            Assert.Less(edge.b, white.b - 0.3f, "The edges crisp golden.");

            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(Skillet.Doneness.Burning, pan.Stage, "Smoking before it's burnt.");
            var smoke = pan.GetComponentInChildren<ParticleSystem>();
            Assert.IsTrue(smoke != null && smoke.isEmitting, "It smokes as it starts to burn.");
            Assert.Less(Look(egg, "White").r, white.r - 0.2f, "Browning.");

            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(Skillet.Doneness.Burnt, pan.Stage);
            Assert.Less(Look(egg, "White").r, 0.3f, "Black when burnt.");
            burner.SetOn(false);
        }

        [Test]
        public void TheSkilletStartsOnTheStove()
        {
            var cookware = pan.GetComponent<Cookware>();
            Assert.IsTrue(cookware.OnStove, "Grandma kept it on a back burner.");
            Assert.IsFalse(cookware.IsHot);
        }

        [Test]
        public void FourSkilletCardsSitByTheRecipeBox()
        {
            var titles = new List<string>();
            foreach (var r in Object.FindObjectsByType<Interaction.Readable>())
            {
                var note = (Data.NoteData)typeof(Interaction.Readable).GetField("note", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(r);
                if (note != null) titles.Add(note.title);
            }
            foreach (var t in new[] { "Fried Eggs and Potatoes", "Potato and Onion Hash", "Fried Chicken", "Fried Squash and Onions" })
            {
                CollectionAssert.Contains(titles, t);
            }
        }
    }
}
