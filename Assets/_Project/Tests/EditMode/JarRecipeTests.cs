using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GrandmasHouse.Tests
{
    // What a jar turns out to be, using the real recipes in the order the
    // pint jar lists them. Ingredient amounts come from the recipes
    // themselves, so retuning a recipe doesn't break these.
    public class JarRecipeTests
    {
        const string Recipes = "Assets/_Project/Data/Recipes/";

        RecipeData[] jarRecipes;
        RecipeData dontEat;
        RecipeData pickles, garlic, tomatoes, botulism, salsa;

        [OneTimeSetUp]
        public void LoadRecipes()
        {
            var jar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Kitchen/PintJar.prefab");
            var contents = new SerializedObject(jar.GetComponent<JarContents>());
            var list = contents.FindProperty("recipes");
            jarRecipes = new RecipeData[list.arraySize];
            for (int i = 0; i < list.arraySize; i++)
            {
                jarRecipes[i] = (RecipeData)list.GetArrayElementAtIndex(i).objectReferenceValue;
            }
            dontEat = (RecipeData)contents.FindProperty("inedible").objectReferenceValue;

            pickles = Load("Recipe_DillPickleChips");
            garlic = Load("Recipe_PickledGarlic");
            tomatoes = Load("Recipe_CannedTomatoes");
            botulism = Load("Recipe_Botulism");
            salsa = Load("Recipe_Salsa");
        }

        static RecipeData Load(string name) => AssetDatabase.LoadAssetAtPath<RecipeData>(Recipes + name + ".asset");

        RecipeData Resolve(JarFacts jar) => JarRecipe.Resolve(jarRecipes, dontEat, jar);

        JarFacts PickleJar(float cucumbers, float onions = 0f) => new JarFacts
        {
            amounts = new Dictionary<string, float> { { "Cucumber", cucumbers }, { "Onion", onions } },
            slices = Mathf.RoundToInt(cucumbers + onions),
            dill = true,
            brine = pickles.brineLine,
        };

        JarFacts TomatoJar(bool lemon) => new JarFacts
        {
            amounts = new Dictionary<string, float> { { "Tomato", tomatoes.wedgesPerJar } },
            wedges = tomatoes.wedgesPerJar,
            lemon = lemon,
            brine = 0.9f,
        };

        JarFacts SalsaJar(float tomato, float onion, float jalapeno) => new JarFacts
        {
            amounts = new Dictionary<string, float> { { "Tomato", tomato }, { "Onion", onion }, { "Jalapeño", jalapeno } },
            salsa = true,
            brine = 0.9f,
        };

        [Test]
        public void TheJarKnowsEveryRecipe()
        {
            CollectionAssert.IsSubsetOf(new[] { pickles, garlic, tomatoes, botulism, salsa }, jarRecipes);
            Assert.IsNotNull(dontEat);
        }

        [Test]
        public void CannedTomatoesAreTriedBeforeBotulism()
        {
            Assert.Less(System.Array.IndexOf(jarRecipes, tomatoes), System.Array.IndexOf(jarRecipes, botulism));
        }

        [Test]
        public void AnEmptyJarIsNothingYet()
        {
            Assert.IsNull(Resolve(new JarFacts()));
        }

        [Test]
        public void CucumbersDillAndBrineAreDillPickles()
        {
            Assert.AreSame(pickles, Resolve(PickleJar(pickles.slicesPerJar)));
        }

        [Test]
        public void PicklesWithoutDillAreNotDillPickles()
        {
            var jar = PickleJar(pickles.slicesPerJar);
            jar.dill = false;
            Assert.AreSame(dontEat, Resolve(jar));
        }

        [Test]
        public void PicklesShortOnBrineAreNotSafe()
        {
            var jar = PickleJar(pickles.slicesPerJar);
            jar.brine = pickles.brineEnough - 0.01f;
            Assert.AreSame(dontEat, Resolve(jar));
        }

        [Test]
        public void BrineJustAtTheLineIsEnough()
        {
            var jar = PickleJar(pickles.slicesPerJar);
            jar.brine = pickles.brineEnough;
            Assert.AreSame(pickles, Resolve(jar));
        }

        [Test]
        public void MostlyOnionIsNotDillPickles()
        {
            Assert.AreSame(dontEat, Resolve(PickleJar(5f, 15f)));
        }

        [Test]
        public void GarlicInWithThePicklesDoesNotCountAgainstTheCucumbers()
        {
            var jar = PickleJar(pickles.slicesPerJar);
            jar.amounts["Clove"] = pickles.garlicPerJar;
            jar.cloves = pickles.garlicPerJar;
            Assert.AreSame(pickles, Resolve(jar));
        }

        [Test]
        public void ClovesInBrineArePickledGarlic()
        {
            var jar = new JarFacts
            {
                amounts = new Dictionary<string, float> { { "Clove", garlic.clovesPerJar } },
                cloves = garlic.clovesPerJar,
                brine = garlic.brineLine,
            };
            Assert.AreSame(garlic, Resolve(jar));
        }

        [Test]
        public void TomatoesWithLemonJuiceAreCannedTomatoes()
        {
            Assert.AreSame(tomatoes, Resolve(TomatoJar(lemon: true)));
        }

        [Test]
        public void TomatoesWithoutLemonJuiceHaveBotulism()
        {
            Assert.AreSame(botulism, Resolve(TomatoJar(lemon: false)));
        }

        [Test]
        public void TomatoOnionAndJalapenoFromThePotAreSalsa()
        {
            Assert.AreSame(salsa, Resolve(SalsaJar(0.6f, 0.2f, 0.2f)));
        }

        [Test]
        public void SalsaWithoutJalapenoIsNotSalsa()
        {
            Assert.AreSame(dontEat, Resolve(SalsaJar(0.7f, 0.3f, 0f)));
        }

        [Test]
        public void SalsaShortOnTomatoIsNotSalsa()
        {
            float tomato = salsa.mostlyShare - 0.05f;
            float rest = (1f - tomato) / 2f;
            Assert.AreSame(dontEat, Resolve(SalsaJar(tomato, rest, rest)));
        }

        [Test]
        public void ABadBatchIsNeverSafe()
        {
            var jar = SalsaJar(0.6f, 0.2f, 0.2f);
            jar.badBatch = true;
            Assert.AreSame(dontEat, Resolve(jar));
        }
    }
}
