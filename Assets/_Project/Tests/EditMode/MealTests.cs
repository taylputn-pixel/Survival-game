using System.Collections.Generic;
using GrandmasHouse.Data;
using NUnit.Framework;
using UnityEngine;

namespace GrandmasHouse.Tests
{
    // What a pot of soup is worth, and what it's called.
    public class MealTests
    {
        MealSettings meals;

        [SetUp]
        public void Begin() => meals = ScriptableObject.CreateInstance<MealSettings>();

        [TearDown]
        public void End() => Object.DestroyImmediate(meals);

        static Dictionary<string, int> Pot(params (string kind, int count)[] pieces)
        {
            var counts = new Dictionary<string, int>();
            foreach (var (kind, count) in pieces) counts[kind] = count;
            return counts;
        }

        [Test]
        public void CookingFeedsMoreThanLeavingItRaw()
        {
            var pot = Pot(("Potato", 10));
            Assert.AreEqual(meals.PotFood(pot, cooked: true, salted: false) * meals.uncookedShare, meals.PotFood(pot, cooked: false, salted: false), 0.01f);
        }

        [Test]
        public void MoreKindsFeedMore()
        {
            float one = meals.PotFood(Pot(("Potato", 12)), true, false);
            float three = meals.PotFood(Pot(("Potato", 8), ("Carrot", 2), ("Onion", 2)), true, false);
            float plain = 8 * meals.FoodPerPiece("Potato") + 2 * meals.FoodPerPiece("Carrot") + 2 * meals.FoodPerPiece("Onion");
            Assert.AreEqual(plain + 2 * meals.varietyBonus, three, 0.01f);
            Assert.Greater(one, 0f);
        }

        [Test]
        public void SaltHelps()
        {
            var pot = Pot(("Potato", 10));
            Assert.AreEqual(meals.PotFood(pot, true, false) * (1f + meals.saltBonus), meals.PotFood(pot, true, true), 0.01f);
        }

        [Test]
        public void AnEmptyPotFeedsNothing()
        {
            Assert.AreEqual(0f, meals.PotFood(Pot(), true, true));
        }

        [Test]
        public void DishesAreNamedForWhatsInThem()
        {
            Assert.AreEqual("Potato soup", MealSettings.DishName(Pot(("Potato", 8), ("Onion", 4)), true));
            Assert.AreEqual("Garden stew", MealSettings.DishName(Pot(("Potato", 4), ("Carrot", 4), ("Tomato", 4)), true));
            Assert.AreEqual("Root vegetable soup", MealSettings.DishName(Pot(("Potato", 4), ("Carrot", 4)), true));
            Assert.AreEqual("Squash soup", MealSettings.DishName(Pot(("Winter", 8)), true));
            Assert.AreEqual("Uncooked potato soup", MealSettings.DishName(Pot(("Potato", 8), ("Onion", 4)), false));
        }
    }
}
