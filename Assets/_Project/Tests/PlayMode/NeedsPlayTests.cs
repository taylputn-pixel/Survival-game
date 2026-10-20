using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Hunger, eating and spoiling in the real Homestead, in Survival mode.
    public class NeedsPlayTests
    {
        MemorySaveStore store;
        DayClock clock;
        PlayerNeeds needs;
        PlayerHands hands;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            store = Homestead.UseMemorySaves();
            yield return Homestead.Load();
            clock = Homestead.Find<DayClock>();
            needs = Homestead.Find<PlayerNeeds>();
            hands = Homestead.Find<PlayerHands>();
            Assume.That(clock.Mode.hungerHasPenalties, "The Homestead isn't in Survival mode.");
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        static Pickup Tomato(Vector3 at)
        {
            var tomato = Homestead.Make<Pickup>("Tomato");
            tomato.transform.position = at;
            return tomato;
        }

        // Waits for perishables to look at the clock again.
        static WaitForSeconds LetFoodAge() => new WaitForSeconds(1.3f);

        [UnityTest]
        public IEnumerator MidnightStartsANewDay()
        {
            int day = clock.Day;
            int started = 0;
            clock.NewDay += _ => started++;
            clock.Advance(24f - clock.Hour - 0.5f);
            Assert.AreEqual(day, clock.Day);
            clock.Advance(1f);
            yield return null;
            Assert.AreEqual(day + 1, clock.Day);
            Assert.AreEqual(1, started);
            Assert.AreEqual(0.5f, clock.Hour, 0.05f);
        }

        [UnityTest]
        public IEnumerator EatingATomatoFillsYouUp()
        {
            needs.Set(50f, 100f);
            var tomato = Tomato(hands.ViewPoint.position + hands.ViewPoint.forward * 0.5f);
            yield return null;
            hands.Give(tomato);
            Assert.IsTrue(hands.IsHolding(tomato));

            var food = tomato.GetComponent<FreshFood>();
            Assert.IsTrue(needs.CanEat(food));
            needs.Eat(food);
            yield return null;

            Assert.AreEqual(50f + food.Nutrition, needs.State.hunger, 0.1f);
            Assert.IsTrue(tomato == null, "The tomato is still there after eating it.");
            Assert.IsTrue(hands.IsEmpty);
        }

        [UnityTest]
        public IEnumerator ARottenTomatoMakesYouSick()
        {
            needs.Set(50f, 100f);
            var tomato = Tomato(hands.ViewPoint.position + hands.ViewPoint.forward * 0.5f);
            yield return null;
            tomato.GetComponent<Perishable>().Restore("{\"freshness\":0}");
            hands.Give(tomato);
            needs.Eat(tomato.GetComponent<FreshFood>());
            yield return null;

            Assert.IsTrue(needs.State.IsSick);
            Assert.Less(needs.State.hunger, 50f);
        }

        [UnityTest]
        public IEnumerator TheFridgeKeepsFoodLongerAndTheFreezerKeepsIt()
        {
            var cold = Object.FindObjectsByType<ColdStorage>();
            ColdStorage fridge = null, freezer = null;
            foreach (var c in cold)
            {
                if (c.SpoilRate > 0f) fridge = c; else freezer = c;
            }
            Assert.IsNotNull(fridge, "No refrigerator in the Homestead.");
            Assert.IsNotNull(freezer, "No freezer in the Homestead.");

            var onTable = Tomato(new Vector3(2.25f, 1.0f, 1.7f)).GetComponent<Perishable>();
            var inFridge = Tomato(fridge.transform.position).GetComponent<Perishable>();
            var inFreezer = Tomato(freezer.transform.position).GetComponent<Perishable>();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            clock.Advance(48f);
            yield return LetFoodAge();

            Assert.Less(onTable.Freshness, 1f, "Food at room temperature didn't spoil.");
            Assert.Greater(inFridge.Freshness, onTable.Freshness, "The fridge didn't slow spoiling.");
            Assert.Less(inFridge.Freshness, 1f, "Food in the fridge didn't spoil at all.");
            Assert.AreEqual(1f, inFreezer.Freshness, "Food in the freezer spoiled.");
        }

        [UnityTest]
        public IEnumerator ExhaustedYouWalkSlowerAndCantHurry()
        {
            needs.Set(100f, 3f);
            yield return null;
            var controller = hands.Controller;
            Assert.Less(controller.SpeedMultiplier, 0.8f);
            Assert.IsFalse(controller.BriskAllowed);

            needs.Set(100f, 100f);
            yield return null;
            Assert.AreEqual(1f, controller.SpeedMultiplier, 0.001f);
            Assert.IsTrue(controller.BriskAllowed);
        }

        [UnityTest]
        public IEnumerator HungerAndRestComeBackAfterLoading()
        {
            needs.Set(33f, 44f, 5f);
            SaveManager.Instance.Save();
            SaveSlot.LoadOnStart = true;
            yield return Homestead.Load();

            var loaded = Homestead.Find<PlayerNeeds>();
            Assert.AreEqual(33f, loaded.State.hunger, 0.5f);
            Assert.AreEqual(44f, loaded.State.rest, 0.5f);
            Assert.AreEqual(5f, loaded.State.starvingHours, 0.5f);
        }
    }
}
