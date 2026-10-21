using System.Collections;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // A jar on its own: saving what's in it and rebuilding it.
    public class JarTests
    {
        const string Salsa =
            "{\"slices\":0,\"cloves\":0,\"wedges\":0,\"lemon\":false,\"salsa\":true,\"badBatch\":false," +
            "\"kinds\":[\"Tomato\",\"Onion\",\"Jalape\\u00f1o\"],\"kindAmounts\":[0.6,0.2,0.2],\"peppercorns\":0," +
            "\"dill\":false,\"brine\":0.9,\"lid\":true,\"processed\":false,\"sealedUp\":false,\"labeled\":false}";

        const string Pickles =
            "{\"slices\":20,\"cloves\":2,\"wedges\":0,\"lemon\":false,\"salsa\":false,\"badBatch\":false," +
            "\"kinds\":[\"Cucumber\",\"Clove\"],\"kindAmounts\":[20.0,2.0],\"peppercorns\":3,\"dill\":true," +
            "\"brine\":0.88,\"lid\":false,\"processed\":false,\"sealedUp\":false,\"labeled\":false}";

        JarContents jar;

        [UnitySetUp]
        public IEnumerator MakeJar()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            jar = Homestead.Make<JarContents>("PintJar");
            jar.transform.position = Vector3.up * 50f;
            jar.GetComponent<Carrying.Pickup>().SetResting(true, false);
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        // Judged against the card when the label goes on: a blue rosette on
        // the lid for a jar made just right, red with one thing off.
        [UnityTest]
        public IEnumerator ALabeledJarWearsItsRibbon()
        {
            string done = Pickles.Replace("\"lid\":false,\"processed\":false,\"sealedUp\":false,\"labeled\":false}",
                "\"lid\":true,\"processed\":true,\"sealedUp\":true,\"labeled\":true,\"brineVinegar\":0.5,\"brineSalt\":2.0,\"poured\":0.88}");
            jar.Restore(done);
            yield return null;
            Assert.AreEqual(Data.Ribbon.Blue, jar.Quality);
            var rosette = jar.transform.Find("Ribbon Rosette");
            Assert.IsNotNull(rosette, "A blue-ribbon jar wears a rosette.");
            Assert.IsTrue(rosette.gameObject.activeSelf);
            Assert.Greater(rosette.position.y, jar.transform.position.y + 0.12f, "It sits on the lid.");

            jar.Restore(done.Replace("\"brineSalt\":2.0", "\"brineSalt\":5.0"));
            yield return null;
            Assert.AreEqual(Data.Ribbon.Red, jar.Quality, "Too salty: red.");
            Assert.AreEqual(5f, jar.BrineSalt, 0.01f, "The brine's saltiness comes back with the jar.");

            jar.Empty();
            yield return null;
            Assert.IsFalse(rosette.gameObject.activeSelf, "Washed out, the ribbon comes off.");
        }

        // Set down, a jar glides onto the surface and sits on it, solid again
        // (its marks, like the fill line, are never counted among its parts).
        [UnityTest]
        public IEnumerator AJarSetDownSitsOnTheSurface()
        {
            var fresh = Homestead.Make<Carrying.Pickup>("PintJar");
            yield return null;
            Vector3 table = new Vector3(0f, 40f, 0f);
            fresh.transform.position = table + Vector3.up * 0.5f;
            fresh.PlaceOn(table, 0f, 0.2f);
            yield return new WaitForSeconds(0.5f);
            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(fresh.CollidersOn, "Solid once it's down.");
            float bottom = float.MaxValue;
            foreach (var r in fresh.GetComponentsInChildren<MeshRenderer>()) bottom = Mathf.Min(bottom, r.bounds.min.y);
            Assert.AreEqual(table.y, bottom, 0.01f, "It sits on the surface, not above it.");
        }

        // Eating a sealed jar tells you how it came out and what it needs, and
        // the journal keeps a tasting note for that recipe.
        [UnityTest]
        public IEnumerator EatingAJarTellsYouWhatItNeeds()
        {
            string salty = Pickles.Replace("\"lid\":false,\"processed\":false,\"sealedUp\":false,\"labeled\":false}",
                "\"lid\":true,\"processed\":true,\"sealedUp\":true,\"labeled\":true,\"brineVinegar\":0.5,\"brineSalt\":5.0,\"poured\":0.88}");
            jar.Restore(salty);
            yield return null;
            var hands = Homestead.Find<Carrying.PlayerHands>();
            jar.GetComponent<Food.JarFood>().Eaten(hands);
            yield return null;
            var caption = Homestead.Find<UI.Caption>();
            StringAssert.Contains("red ribbon", caption.Showing);
            StringAssert.Contains("salt", caption.Showing);

            var journal = Homestead.Find<UI.JournalView>();
            var entries = new System.Collections.Generic.List<UI.JournalView.Entry>();
            journal.Gather(3, entries);
            Assert.AreEqual(1, entries.Count, "One tasting note for the recipe.");
            Assert.AreEqual("Dill Pickle Chips", entries[0].title);
            StringAssert.Contains("Too salty", entries[0].body);

            // A better jar next time replaces the note.
            jar.Restore(salty.Replace("\"brineSalt\":5.0", "\"brineSalt\":2.0"));
            yield return null;
            jar.GetComponent<Food.JarFood>().Eaten(hands);
            journal.Gather(3, entries);
            Assert.AreEqual(1, entries.Count);
            StringAssert.Contains("blue ribbon", entries[0].body);
            StringAssert.Contains("Just right", entries[0].body);
        }

        static string RoundTrip(string state)
        {
            var copy = Homestead.Make<JarContents>("PintJar");
            copy.Restore(state);
            string saved = copy.Capture();
            Object.Destroy(copy.gameObject);
            return saved;
        }

        [UnityTest]
        public IEnumerator SalsaSavesAndComesBackTheSame()
        {
            jar.Restore(Salsa);
            yield return null;
            Assert.AreEqual("Salsa", jar.Contents);
            string saved = jar.Capture();
            Assert.AreEqual(saved, RoundTrip(saved));
        }

        [UnityTest]
        public IEnumerator PicklesComeBackWithEverySliceAndClove()
        {
            jar.Restore(Pickles);
            yield return null;
            Assert.AreEqual(20, jar.SliceCount);
            Assert.AreEqual(2, jar.CloveCount);
            Assert.AreEqual(3, jar.PeppercornCount);
            Assert.IsTrue(jar.HasDill);
            Assert.AreEqual("Dill Pickle Chips", jar.Contents);
            string saved = jar.Capture();
            Assert.AreEqual(saved, RoundTrip(saved));
        }

        [UnityTest]
        public IEnumerator EmptyingAJarLeavesItCleanAndOpen()
        {
            jar.Restore(Pickles);
            yield return null;
            jar.Empty();
            yield return null;
            Assert.IsFalse(jar.HasContents);
            Assert.IsFalse(jar.HasLid);
            Assert.IsNull(jar.Contents);
        }
    }
}
