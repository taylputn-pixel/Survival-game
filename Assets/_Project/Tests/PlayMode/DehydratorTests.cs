using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Drying food in the dehydrator, and what dried food is good for.
    public class DehydratorTests
    {
        PlayerHands hands;
        Dehydrator dehydrator;
        DayClock clock;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            dehydrator = Homestead.Find<Dehydrator>();
            clock = Homestead.Find<DayClock>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        IEnumerator InHand(string key, System.Action<Pickup> got)
        {
            var item = Homestead.Make<Pickup>(key);
            item.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            hands.Give(item);
            yield return null;
            got(item);
        }

        [UnityTest]
        public IEnumerator TomatoesDryOnTheTrayAndKeepFarLonger()
        {
            Pickup tomato = null;
            yield return InHand("Tomato", t => tomato = t);
            Assert.IsTrue(dehydrator.CanInteract(hands));
            Assert.AreEqual("Lay it on the tray", dehydrator.PromptVerb(hands));
            dehydrator.Interact(hands);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(hands.IsEmpty);
            Assert.IsNotNull(tomato.GetComponentInParent<Dehydrator>(), "The tomato isn't on the tray.");

            Assert.AreEqual("Turn on the dehydrator", dehydrator.PromptVerb(hands));
            dehydrator.Interact(hands);
            Assert.IsTrue(dehydrator.On);
            yield return null;
            clock.Advance(dehydrator.DryHours + 1f);
            yield return null;
            var food = tomato.GetComponent<Perishable>();
            Assert.IsTrue(food.Dried, "Still not dried after " + dehydrator.DryHours + " hours.");

            // Dried, a week costs it almost nothing.
            float before = food.Freshness;
            yield return new WaitForSeconds(1.2f);
            clock.Advance(24f * 7f);
            yield return new WaitForSeconds(1.3f);
            float fresh = Perishable.Spoil(1f, 24f * 7f, food.ShelfLifeDays, 1f);
            Assert.Greater(food.Freshness, fresh + 0.5f, "Dried food spoiled about as fast as fresh.");
            Assert.Less(before - food.Freshness, 0.05f);
        }

        [UnityTest]
        public IEnumerator DriedFoodGoesStraightIntoTheSoupPot()
        {
            Pickup potato = null;
            yield return InHand("Potato", p => potato = p);
            potato.GetComponent<Perishable>().Restore("{\"freshness\":1,\"dried\":true,\"drying\":12}");
            var pot = Homestead.Find<CookingPot>();
            pot.Restore("{\"water\":0.5,\"salt\":0,\"heat\":1}");
            yield return null;
            Assert.IsTrue(pot.CanInteract(hands));
            Assert.AreEqual("Add to the pot", pot.PromptVerb(hands));
            pot.Interact(hands);
            yield return null;
            Assert.AreEqual(8, pot.Counts()["Potato"]);
            Assert.IsTrue(hands.IsEmpty);
        }
    }
}
