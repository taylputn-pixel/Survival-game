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

        IEnumerator WoodIn(Smokehouse smokehouse, int pieces)
        {
            for (int i = 0; i < pieces; i++)
            {
                var wood = Homestead.Make<Pickup>("Firewood");
                wood.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
                yield return null;
                hands.Give(wood);
                yield return null;
                Assert.AreEqual("Put wood in the fire", smokehouse.PromptVerb(hands));
                smokehouse.Interact(hands);
                yield return null;
            }
            Assert.IsTrue(hands.IsEmpty);
        }

        static int Chickens()
        {
            int n = 0;
            foreach (var p in Object.FindObjectsByType<Pickup>()) if (p.DisplayName == "Chicken") n++;
            return n;
        }

        Hen HenInTheRun()
        {
            foreach (var h in Object.FindObjectsByType<Hen>()) if (h.Alive && !h.Inside) return h;
            Assert.Fail("No hen out in the run.");
            return null;
        }

        // Carried to the chopping block, laid on it, and the axe.
        [UnityTest]
        public IEnumerator AHenOnTheBlockGivesAChicken()
        {
            var flock = Homestead.Find<Flock>();
            var block = Homestead.Find<ChoppingBlock>();
            Hen hen = HenInTheRun();
            Assert.IsTrue(hen.CanInteract(hands));
            Assert.AreEqual("Pick her up", hen.PromptVerb(hands));
            hen.Interact(hands);
            yield return null;
            Assert.IsTrue(hen.Carried);
            Assert.AreEqual("Hen", hands.Held.DisplayName);
            Assert.IsFalse(hen.CanInteract(hands));
            Assert.AreEqual(4, flock.Alive, "Picking her up isn't the end of her.");

            Assert.IsTrue(block.CanInteract(hands));
            Assert.AreEqual("Lay her on the block", block.PromptVerb(hands));
            block.Interact(hands);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(hands.IsEmpty);
            Assert.IsNotNull(block.HenOnBlock, "She should lie on the block.");
            Assert.IsTrue(hen.Carried, "Still out of the run while she's on the block.");

            var axe = Homestead.Make<Pickup>("Axe");
            axe.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.5f;
            yield return null;
            hands.Give(axe);
            yield return null;
            int before = Chickens();
            Assert.AreEqual("Swing the axe", block.PromptVerb(hands));
            block.Interact(hands);
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(3, flock.Alive);
            Assert.AreEqual(before + 1, Chickens());
            Assert.IsNull(block.HenOnBlock);
            Assert.IsFalse(hen.CanInteract(hands));
            // And it stays gone after a save.
            StringAssert.Contains("false", flock.Capture());
        }

        // Set her down anywhere but the block and she's back in the run.
        [UnityTest]
        public IEnumerator AHenLetGoGoesBackToTheRun()
        {
            var flock = Homestead.Find<Flock>();
            Hen hen = HenInTheRun();
            hen.Interact(hands);
            yield return null;
            hands.TakeHeld().Drop();
            yield return null;
            yield return null;
            Assert.IsFalse(hen.Carried);
            Assert.IsTrue(hen.GetComponentInChildren<Renderer>().enabled, "She should be back to see.");
            Assert.AreEqual(0, Object.FindObjectsByType<LiveHen>().Length, "No hen left in your arms.");
            Assert.AreEqual(4, flock.Alive);
        }

        // In a shared game the flock's state says who is carrying which hen,
        // so everyone sees her gone from the run; a hen no one really holds
        // (a game saved with her in someone's arms) goes back.
        [UnityTest]
        public IEnumerator ACarriedHenIsSharedAndNeverLost()
        {
            var flock = Homestead.Find<Flock>();
            Hen hen = HenInTheRun();
            hen.Interact(hands);
            yield return null;
            string carrying = flock.Capture();
            StringAssert.Contains("\"carried\":[", carrying);

            // Another computer hears of it.
            hands.TakeHeld().Drop();
            yield return null;
            yield return null;
            Assert.IsFalse(hen.Carried);
            flock.Restore(carrying);
            Assert.IsTrue(hen.Carried, "A friend has her: she's out of the run here too.");
            Assert.IsFalse(hen.GetComponentInChildren<Renderer>().enabled);

            // But no one here holds her (this is the host, carrier 0): back she goes.
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(hen.Carried, "A hen no one holds goes back to the run.");
            Assert.IsTrue(hen.GetComponentInChildren<Renderer>().enabled);
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
            Assert.IsFalse(smokehouse.CanInteract(hands), "No fire without wood.");

            // Two pieces of firewood aren't quite enough; it burns down and needs more.
            yield return WoodIn(smokehouse, 2);
            Assert.AreEqual(2, smokehouse.Wood);
            Assert.AreEqual("Light the fire", smokehouse.PromptVerb(hands));
            smokehouse.Interact(hands);
            Assert.IsTrue(smokehouse.On);
            yield return null;
            clock.Advance(smokehouse.HoursPerLog * 2f + 1f);
            yield return null;
            Assert.IsFalse(smokehouse.On, "The fire should have burned down.");
            Assert.AreEqual(0, smokehouse.Wood);
            Assert.IsFalse(bird.GetComponent<Perishable>().Cured);

            yield return WoodIn(smokehouse, 3);
            smokehouse.Interact(hands);
            yield return null;
            clock.Advance(smokehouse.HoursPerLog * 3f + 1f);
            yield return null;
            var meat = bird.GetComponent<Perishable>();
            Assert.AreEqual(Perishable.Cure.Smoked, meat.CuredBy, $"cured {meat.CuringHours}h of {smokehouse.CureHours}, fresh {meat.Freshness}, on {smokehouse.On}, wood {smokehouse.Wood}, left {smokehouse.FireLeft}");
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
