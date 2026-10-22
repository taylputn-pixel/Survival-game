using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // A pot of soup in the real kitchen, ladled into a bowl and eaten.
    public class MealPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        // A pot of cooked potato soup: slices, onion and carrot in salted water.
        static CookingPot SoupPot(bool cooked)
        {
            var pot = Homestead.Find<CookingPot>();
            pot.Restore("{\"water\":0.4,\"salt\":1,\"heat\":" + (cooked ? "1" : "0") + ",\"potato\":8,\"onion\":6,\"carrot\":6}");
            return pot;
        }

        // A bowl, not one of the plates.
        static Bowl SoupBowl()
        {
            foreach (var b in Object.FindObjectsByType<Bowl>()) if (!b.IsPlate) return b;
            return null;
        }

        [UnityTest]
        public IEnumerator SoupGoesInBowlsNotPlates()
        {
            var pot = SoupPot(cooked: true);
            var hands = Homestead.Find<PlayerHands>();
            Bowl plate = null;
            foreach (var b in Object.FindObjectsByType<Bowl>()) if (b.IsPlate) plate = b;
            Assert.IsNotNull(plate, "Grandma's plates are on the kitchen table.");
            hands.Give(plate.GetComponent<Pickup>());
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(pot.CanInteract(hands), "No ladling soup onto a plate.");
            hands.TakeHeld().Drop();
            hands.Give(SoupBowl().GetComponent<Pickup>());
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(pot.CanInteract(hands));
        }

        [UnityTest]
        public IEnumerator SoupIsLadledIntoABowlAndEaten()
        {
            var pot = SoupPot(cooked: true);
            yield return null;
            Assert.IsTrue(pot.IsSoup);
            float before = pot.Fill;

            var bowl = SoupBowl();
            Assert.IsTrue(bowl.IsEmpty);
            pot.Serve(bowl);
            Assert.IsFalse(bowl.IsEmpty);
            Assert.AreEqual("Potato soup", bowl.Dish);
            Assert.Less(pot.Fill, before, "Ladling took nothing out of the pot.");

            var needs = Homestead.Find<PlayerNeeds>();
            needs.Set(20f, 100f);
            float food = bowl.Food;
            Assert.IsTrue(needs.CanEat(bowl));
            needs.Eat(bowl);
            Assert.AreEqual(20f + food, needs.State.hunger, 0.1f);
            Assert.IsTrue(bowl.IsEmpty, "The bowl should be empty after eating.");
        }

        [UnityTest]
        public IEnumerator ACookedBowlFeedsMoreThanTheSameVegetablesRaw()
        {
            var bowl = SoupBowl();
            var pot = SoupPot(cooked: true);
            yield return null;
            pot.Serve(bowl);
            float cooked = bowl.Food;
            bowl.Eaten(null);

            pot = SoupPot(cooked: false);
            yield return null;
            pot.Serve(bowl);
            Assert.Greater(cooked, bowl.Food);
            StringAssert.StartsWith("Uncooked", bowl.Dish);

            // The whole pot (about a potato, an onion and a carrot, chopped)
            // feeds well over twice what they would eaten raw.
            pot = SoupPot(cooked: true);
            yield return null;
            float raw = 0f;
            foreach (var key in new[] { "Potato", "Onion", "Carrot" })
            {
                var item = Homestead.Make<FreshFood>(key);
                raw += item.Nutrition;
                Object.Destroy(item.gameObject);
            }
            Assert.Greater(pot.PotFood, raw * 2f);
        }
    }
}
