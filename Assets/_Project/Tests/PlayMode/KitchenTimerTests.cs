using System.Collections;
using System.Reflection;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Grandma's kitchen timer follows the canner: wound back while jars
    // process, ringing when they're done.
    public class KitchenTimerTests
    {
        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [UnityTest]
        public IEnumerator TheTimerRunsWithTheCannerAndRingsWhenItsDone()
        {
            var canner = Homestead.Find<Canner>();
            var timer = Homestead.Find<KitchenTimer>();
            var clock = Homestead.Find<DayClock>();
            clock.Restore("{\"day\":1,\"hour\":10}");
            Assert.AreEqual(0f, timer.Showing, "Nothing on the timer to start.");

            // A lidded jar in the rack, the canner lid on, partway through.
            var jar = Homestead.Make<JarContents>("PintJar");
            var slots = (Transform[])typeof(Canner).GetField("rackSlots", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(canner);
            jar.GetComponent<Pickup>().AttachTo(slots[0], Vector3.zero, Quaternion.identity, 0f, collidersOn: true);
            yield return null;
            canner.Restore("{\"lidOn\":true,\"boiling\":false,\"done\":false,\"boilLeft\":20,\"processTotal\":40,\"water\":1}");
            canner.AfterLoad();
            yield return null;
            Assert.IsTrue(canner.Processing);
            Assert.AreEqual(0.5f, timer.Showing, 0.05f, "Half the time left, half wound.");
            Assert.IsFalse(timer.Ringing);

            // Slept through: the jars are done by morning, and the timer rings.
            clock.SleepUntilMorning();
            yield return null;
            yield return null;
            Assert.IsTrue(canner.Done);
            Assert.IsTrue(timer.Ringing, "Time's up: it rings.");
        }
    }
}
