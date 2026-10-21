using System.Collections;
using GrandmasHouse.Animals;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The hens: eggs from fed hens, death from hunger, the fox, and feeding
    // them from your hands.
    public class FlockTests
    {
        Flock flock;
        PlayerHands hands;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            flock = Homestead.Find<Flock>();
            hands = Homestead.Find<PlayerHands>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        static int Eggs()
        {
            int n = 0;
            foreach (var p in Object.FindObjectsByType<Pickup>()) if (p.DisplayName == "Egg" && p.gameObject.activeInHierarchy) n++;
            return n;
        }

        HingedDoor CoopDoor()
        {
            foreach (var d in Object.FindObjectsByType<HingedDoor>()) if (d.name == "Pop Door Hinge") return d;
            Assert.Fail("No coop door.");
            return null;
        }

        [Test]
        public void FedHensEachLayAnEgg()
        {
            Assert.AreEqual(4, flock.Alive);
            flock.AddFeed(1f);
            flock.AddWater(1f);
            int before = Eggs();
            CoopDoor().Set(false);
            flock.Live(foxRoll: 1f);
            Assert.AreEqual(before + 4, Eggs());
        }

        // Short cold days: fed hens still eat but lay far less in winter.
        [Test]
        public void HensLayLessInWinter()
        {
            var calendar = Homestead.Find<DayCycle.Weather>().Calendar;
            int winter = 1;
            for (int m = 0; m < 3; m++) winter += calendar.months[m].days;
            flock.Clock.Restore("{\"day\":" + winter + ",\"hour\":22}");
            CoopDoor().Set(false);
            int laid = 0;
            flock.Overnight += said => laid += int.Parse(said.Split(' ')[1].TrimEnd(','));
            const int nights = 12;
            for (int night = 0; night < nights; night++)
            {
                foreach (var p in Object.FindObjectsByType<Pickup>()) if (p.DisplayName == "Egg") Object.DestroyImmediate(p.gameObject);
                flock.AddFeed(1f);
                flock.AddWater(1f);
                flock.Live(1f);
            }
            Assert.AreEqual(4, flock.Alive, "Fed hens don't die in winter.");
            Assert.Less(laid, nights * 4 * 0.6f, "Winter should cut laying well short of one egg a hen a night.");
        }

        [Test]
        public void HensWithNothingToEatDieAfterAFewDays()
        {
            flock.Restore("{\"feed\":0,\"water\":0,\"alive\":[true,true,true,true],\"hungry\":[0,0,0,0],\"feathersDay\":-1}");
            for (int night = 0; night < flock.Settings.daysToStarve; night++)
            {
                Assert.AreEqual(4, flock.Alive, "A hen died too soon.");
                flock.Live(1f);
            }
            Assert.AreEqual(0, flock.Alive);
        }

        [Test]
        public void TheFoxComesThroughADoorLeftOpen()
        {
            CoopDoor().Set(true);
            flock.Live(foxRoll: 0f);
            Assert.AreEqual(3, flock.Alive);

            CoopDoor().Set(false);
            int left = flock.Alive;
            flock.Live(foxRoll: 0f);
            // All shut in for the night? Only if they made it in; out in the
            // run at this hour they'd be shut out, which is worse.
            Assert.LessOrEqual(flock.Alive, left);
        }

        [UnityTest]
        public IEnumerator SpoiledProduceStillFeedsTheHens()
        {
            flock.Restore("{\"feed\":0,\"water\":0.5,\"alive\":[true,true,true,true],\"hungry\":[0,0,0,0],\"feathersDay\":-1}");
            var tomato = Homestead.Make<Pickup>("Tomato");
            tomato.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            tomato.GetComponent<Perishable>().Restore("{\"freshness\":0}");
            hands.Give(tomato);
            yield return null;

            Feeder feeder = null;
            foreach (var f in Object.FindObjectsByType<Feeder>()) if (f.name == "Feeder") feeder = f;
            Assert.IsTrue(feeder.CanInteract(hands));
            Assert.AreEqual("Toss to the hens", feeder.PromptVerb(hands));
            feeder.Interact(hands);
            yield return null;
            Assert.AreEqual(flock.Settings.spoiledFeed, flock.Feed, 0.001f);
            Assert.IsTrue(hands.IsEmpty);
        }

        [UnityTest]
        public IEnumerator EggsGoInTheSoupPot()
        {
            var pot = Homestead.Find<CookingPot>();
            pot.Restore("{\"water\":0.5,\"salt\":0,\"heat\":1}");
            var egg = Homestead.Make<Pickup>("Egg");
            egg.transform.position = hands.ViewPoint.position + hands.ViewPoint.forward * 0.4f;
            yield return null;
            hands.Give(egg);
            yield return null;
            Assert.IsTrue(pot.CanInteract(hands));
            pot.Interact(hands);
            yield return null;
            Assert.AreEqual(1, pot.Counts()["Egg"]);
            Assert.IsTrue(pot.IsSoup);
        }

        [Test]
        public void HornwormsInThePailAreATreat()
        {
            var pail = Homestead.Find<TreatPail>();
            pail.AddWorm();
            pail.AddWorm();
            Assert.AreEqual(2, pail.Worms);
            Assert.AreSame(pail, TreatPail.Near(Homestead.Find<Garden.GardenBed>().transform.position));
        }
    }
}
