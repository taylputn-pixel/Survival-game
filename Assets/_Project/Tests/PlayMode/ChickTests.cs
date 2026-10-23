using System.Collections;
using GrandmasHouse.Animals;
using GrandmasHouse.Carrying;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Trade;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // The coop starts empty. Ada gives you a chick when you first meet her
    // and sells more; let one out of its box and she joins the flock, eats
    // half rations, and grows into a laying hen.
    public class ChickTests
    {
        Flock flock;
        PlayerHands hands;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load(hens: false);
            flock = Homestead.Find<Flock>();
            hands = Homestead.Find<PlayerHands>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [Test]
        public void ANewGameHasAnEmptyCoop()
        {
            Assert.AreEqual(0, flock.Alive);
            Assert.IsTrue(flock.HasRoom);
        }

        [UnityTest]
        public IEnumerator AdaGivesAChickWhenYouFirstMeetHer()
        {
            Trader ada = null;
            foreach (var t in Object.FindObjectsByType<Trader>()) if (t.Name == "Ada") ada = t;
            Assert.IsFalse(ada.Welcomed);
            Assert.AreEqual(0, Object.FindObjectsByType<ChickBox>().Length);

            ada.Interact(hands);
            yield return null;
            Assert.IsTrue(ada.Welcomed);
            Assert.AreEqual(1, Object.FindObjectsByType<ChickBox>().Length, "A chick in a box, set out for you.");
            StringAssert.Contains("\"welcomed\":true", ada.Capture());

            ada.Interact(hands);
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<ChickBox>().Length, "Only the once.");
        }

        [UnityTest]
        public IEnumerator LettingAChickOutAddsHerToTheFlock()
        {
            var box = Homestead.Make<ChickBox>("ChickBox");
            yield return null;
            hands.Give(box.GetComponent<Pickup>());
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(box.CanAltInteract(hands), "Not while you're holding it.");
            hands.TakeHeld().PlaceOn(flock.SpotInRun(), 0f, 0.1f);
            yield return new WaitForSeconds(0.4f);

            Assert.IsTrue(box.CanAltInteract(hands));
            Assert.AreEqual("Let the chick out", box.AltVerb(hands));
            box.AltInteract(hands);
            yield return null;
            yield return null;
            Assert.AreEqual(1, flock.Alive);
            Hen chick = null;
            foreach (var h in flock.Hens) if (h.Alive) chick = h;
            Assert.IsTrue(chick.IsChick);
            Assert.IsFalse(chick.Loose, "Let out in the run, she's in the run.");
            Assert.Less(chick.transform.localScale.x, 0.8f, "Small.");
            Assert.AreEqual("Pick up the chick", chick.PromptVerb(hands));
            Assert.IsTrue(box == null, "The box is gone.");
        }

        [UnityTest]
        public IEnumerator AChickGrowsIntoAHenAndThenLays()
        {
            yield return null;
            Assert.IsTrue(flock.AdoptHere(flock.SpotInRun()));
            Hen chick = null;
            foreach (var h in flock.Hens) if (h.Alive) chick = h;
            string last = null;
            flock.Overnight += what => last = what;

            var settings = flock.Settings;
            for (int night = 0; night < settings.daysToLay; night++)
            {
                Assert.IsTrue(chick.IsChick, "Still a chick on night " + night);
                flock.AddFeed(1f);
                flock.AddWater(1f);
                float feed = flock.Feed;
                flock.Live(1f);
                StringAssert.StartsWith("laid 0", last, "A chick doesn't lay.");
                Assert.AreEqual(feed - settings.feedPerHen * settings.chickShare, flock.Feed, 0.001f, "Half rations.");
            }
            Assert.IsFalse(chick.IsChick, "Grown.");
            Assert.AreEqual(Vector3.one, chick.transform.localScale);

            // Kept through a save.
            StringAssert.Contains("\"ages\":[" + settings.daysToLay + ",", flock.Capture());

            flock.AddFeed(1f);
            flock.AddWater(1f);
            flock.Live(1f);
            StringAssert.StartsWith("laid 1", last, "A grown hen lays (spring).");
        }

        [UnityTest]
        public IEnumerator AChickIsntForThePot()
        {
            yield return null;
            flock.AdoptHere(flock.SpotInRun());
            Hen chick = null;
            foreach (var h in flock.Hens) if (h.Alive) chick = h;
            var block = Homestead.Find<ChoppingBlock>();
            flock.PickUp(chick, hands);
            yield return new WaitForSeconds(0.3f);
            Assert.IsNotNull(hands.Held.GetComponent<LiveHen>());
            Assert.AreEqual("Chick", hands.Held.DisplayName);
            Assert.IsFalse(block.CanInteract(hands), "No laying a chick on the block.");
        }

        [UnityTest]
        public IEnumerator AFullCoopKeepsTheBoxShut()
        {
            yield return null;
            while (flock.HasRoom) flock.AdoptHere(flock.SpotInRun());
            var box = Homestead.Make<ChickBox>("ChickBox");
            yield return null;
            hands.Give(box.GetComponent<Pickup>());
            yield return new WaitForSeconds(0.3f);
            hands.TakeHeld().PlaceOn(flock.SpotInRun(), 0f, 0.1f);
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(box.CanAltInteract(hands));
        }
    }
}
