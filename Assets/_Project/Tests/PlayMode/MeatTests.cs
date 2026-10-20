using System.Collections;
using GrandmasHouse.Animals;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // A hen for the pot, and keeping the meat: the smokehouse and the salt box.
    public class MeatTests
    {
        PlayerHands hands;
        DayClock clock;
        SurvivalSettings settings;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            clock = Homestead.Find<DayClock>();
            settings = ScriptableObject.CreateInstance<SurvivalSettings>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        IEnumerator ChickenInHand(System.Action<Pickup> got)
        {
            var bird = Homestead.Make<Pickup>("Chicken");
            bird.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            hands.Give(bird);
            yield return null;
            got(bird);
        }

        static int Chickens()
        {
            int n = 0;
            foreach (var p in Object.FindObjectsByType<Pickup>()) if (p.DisplayName == "Chicken") n++;
            return n;
        }

        [UnityTest]
        public IEnumerator AHenTakenForThePotGivesAChicken()
        {
            var flock = Homestead.Find<Flock>();
            Hen hen = null;
            foreach (var h in Object.FindObjectsByType<Hen>()) if (h.Alive && !h.Inside) hen = h;
            Assert.IsNotNull(hen);
            Assert.IsTrue(hen.CanInteract(hands));
            Assert.AreEqual("Take her for the pot", hen.PromptVerb(hands));
            int before = Chickens();
            hen.Interact(hands);
            yield return null;
            Assert.AreEqual(3, flock.Alive);
            Assert.AreEqual(before + 1, Chickens());
            Assert.IsFalse(hen.CanInteract(hands));
            // And it stays gone after a save.
            StringAssert.Contains("false", flock.Capture());
        }

        [UnityTest]
        public IEnumerator RawChickenMakesYouSickButSmokedDoesNot()
        {
            Pickup bird = null;
            yield return ChickenInHand(b => bird = b);
            var food = bird.GetComponent<FreshFood>();
            Assert.IsTrue(food.IsMeat);
            Assert.Greater(food.SickHours(settings), 0f, "Raw chicken should make you sick.");

            var smokehouse = Homestead.Find<Smokehouse>();
            Assert.IsTrue(smokehouse.CanInteract(hands));
            Assert.AreEqual("Hang it up", smokehouse.PromptVerb(hands));
            smokehouse.Interact(hands);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(hands.IsEmpty);
            Assert.AreEqual("Light the fire", smokehouse.PromptVerb(hands));
            smokehouse.Interact(hands);
            Assert.IsTrue(smokehouse.On);
            yield return null;

            // One fire isn't quite enough; it burns down and needs lighting again.
            clock.Advance(11f);
            yield return null;
            Assert.IsFalse(smokehouse.On, "The fire should have burned down.");
            Assert.IsFalse(bird.GetComponent<Perishable>().Cured);
            smokehouse.Interact(hands);
            yield return null;
            clock.Advance(8f);
            yield return null;
            var meat = bird.GetComponent<Perishable>();
            Assert.AreEqual(Perishable.Cure.Smoked, meat.CuredBy);
            Assert.AreEqual(0f, food.SickHours(settings));

            // Saved and loaded, it's still smoked.
            string saved = meat.Capture();
            meat.Restore("{\"freshness\":1}");
            meat.Restore(saved);
            Assert.AreEqual(Perishable.Cure.Smoked, meat.CuredBy);
        }

        [UnityTest]
        public IEnumerator TheSaltBoxCuresMeatOnlyWithSaltInIt()
        {
            var box = Homestead.Find<SaltBox>();
            Pickup bird = null;
            yield return ChickenInHand(b => bird = b);
            Assert.AreEqual("Pack it in the salt", box.PromptVerb(hands));
            box.Interact(hands);
            yield return new WaitForSeconds(0.4f);
            var meat = bird.GetComponent<Perishable>();

            clock.Advance(80f);
            yield return null;
            Assert.IsFalse(meat.Cured, "No salt in the box, and it cured anyway.");

            box.Restore("{\"salt\":2}");
            yield return null;
            clock.Advance(73f);
            yield return null;
            Assert.AreEqual(Perishable.Cure.Salted, meat.CuredBy);
            Assert.AreEqual(1, box.Salt, "Curing a piece should use up a scoop of salt.");
        }

        [UnityTest]
        public IEnumerator TheSaltBoxTakesOnlyMeat()
        {
            var box = Homestead.Find<SaltBox>();
            var tomato = Homestead.Make<Pickup>("Tomato");
            tomato.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            hands.Give(tomato);
            yield return null;
            Assert.IsFalse(box.CanInteract(hands));
        }

        [UnityTest]
        public IEnumerator CuredChickenGoesIntoTheSoupPot()
        {
            Pickup bird = null;
            yield return ChickenInHand(b => bird = b);
            bird.GetComponent<Perishable>().Restore("{\"freshness\":1,\"cure\":3}");
            var pot = Homestead.Find<CookingPot>();
            pot.Restore("{\"water\":0.5,\"salt\":0,\"heat\":1}");
            yield return null;
            Assert.IsTrue(pot.CanInteract(hands));
            pot.Interact(hands);
            yield return null;
            Assert.AreEqual(6, pot.Counts()["Chicken"]);
        }
    }
}
