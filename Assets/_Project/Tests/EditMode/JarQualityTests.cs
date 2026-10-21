using System.Collections.Generic;
using GrandmasHouse.Data;
using NUnit.Framework;
using UnityEditor;

namespace GrandmasHouse.Tests
{
    // A jar's fair ribbon, judged against the real recipes: blue when it's
    // made as the card says, one ribbon down for each thing that isn't.
    // Amounts come from the recipes, so retuning one doesn't break these.
    public class JarQualityTests
    {
        const string Recipes = "Assets/_Project/Data/Recipes/";
        RecipeData pickles, garlic, salsa, tomatoes;

        [OneTimeSetUp]
        public void LoadRecipes()
        {
            pickles = AssetDatabase.LoadAssetAtPath<RecipeData>(Recipes + "Recipe_DillPickleChips.asset");
            garlic = AssetDatabase.LoadAssetAtPath<RecipeData>(Recipes + "Recipe_PickledGarlic.asset");
            salsa = AssetDatabase.LoadAssetAtPath<RecipeData>(Recipes + "Recipe_Salsa.asset");
            tomatoes = AssetDatabase.LoadAssetAtPath<RecipeData>(Recipes + "Recipe_CannedTomatoes.asset");
        }

        JarMakings AsTheCardSays(RecipeData r) => new JarMakings
        {
            fullness = 1f, level = r.brineLine, cloves = r.garlicPerJar, peppercorns = r.peppercornPinches * 6,
            vinegarShare = r.usesBrine ? r.vinegarShare : -1f, saltPerPot = r.saltScoops,
        };

        [Test]
        public void MadeAsTheCardSaysIsBlue()
        {
            foreach (var r in new[] { pickles, garlic, salsa, tomatoes })
            {
                var marks = new List<string>();
                Assert.AreEqual(Ribbon.Blue, JarQuality.Grade(r, AsTheCardSays(r), marks), r.displayName + ": " + string.Join(", ", marks));
            }
        }

        [Test]
        public void EachThingOffDropsARibbon()
        {
            var jar = AsTheCardSays(pickles);
            jar.fullness = 0.6f;
            Assert.AreEqual(Ribbon.Red, JarQuality.Grade(pickles, jar), "Not packed full.");
            jar.saltPerPot = pickles.saltScoops + 2f;
            Assert.AreEqual(Ribbon.White, JarQuality.Grade(pickles, jar), "And too salty.");
            jar.vinegarShare = 0.1f;
            Assert.AreEqual(Ribbon.None, JarQuality.Grade(pickles, jar), "And weak brine: no ribbon.");
        }

        [TestCase(0.5f, "too much headspace")]
        [TestCase(1f, "filled too high")]
        public void TheBrineGoesToTheLine(float level, string why)
        {
            var jar = AsTheCardSays(pickles);
            jar.level = level;
            var marks = new List<string>();
            Assert.AreEqual(Ribbon.Red, JarQuality.Grade(pickles, jar, marks));
            CollectionAssert.Contains(marks, why);
        }

        [Test]
        public void ShortOnGarlicOrPeppercornsCounts()
        {
            var jar = AsTheCardSays(pickles);
            jar.cloves = 0;
            Assert.AreEqual(Ribbon.Red, JarQuality.Grade(pickles, jar));
            var cloves = AsTheCardSays(garlic);
            cloves.peppercorns = 0;
            Assert.AreEqual(Ribbon.Red, JarQuality.Grade(garlic, cloves));
        }

        [Test]
        public void CardsSpeakInGameAmounts()
        {
            foreach (var r in new[] { pickles, garlic, salsa, tomatoes })
            {
                string card = r.FillIn(r.cardText);
                StringAssert.DoesNotContain("{", card, r.displayName + " has a number left unfilled.");
                StringAssert.DoesNotContain("cup", card);
                StringAssert.DoesNotContain("lbs", card);
                StringAssert.Contains(r.processMinutes.ToString("0") + " minutes", card);
            }
            StringAssert.Contains(pickles.saltScoops + " scoops of salt", pickles.FillIn(pickles.cardText));
            StringAssert.Contains(pickles.garlicPerJar + " cloves of garlic", pickles.FillIn(pickles.cardText));
        }
    }
}
