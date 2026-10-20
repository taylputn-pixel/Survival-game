using System.Collections.Generic;
using GrandmasHouse.Data;
using NUnit.Framework;
using UnityEngine;

namespace GrandmasHouse.Tests
{
    public class RecipeDataTests
    {
        RecipeData recipe;

        [SetUp]
        public void MakeRecipe() => recipe = ScriptableObject.CreateInstance<RecipeData>();

        [TearDown]
        public void DropRecipe() => Object.DestroyImmediate(recipe);

        [TestCase("Onion Slice", "Onion")]
        [TestCase("Cucumber Slice", "Cucumber")]
        [TestCase("Clove", "Clove")]
        public void APiecesKindIsTheFirstWordOfItsName(string piece, string kind)
        {
            Assert.AreEqual(kind, RecipeData.KindOf(piece));
        }

        [Test]
        public void CardTextShowsTheClovesPerJar()
        {
            recipe.clovesPerJar = 14;
            Assert.AreEqual("Pack 14 cloves.", recipe.FillIn("Pack {cloves} cloves."));
        }

        [Test]
        public void NoCardTextStaysNone()
        {
            Assert.IsNull(recipe.FillIn(null));
        }

        [Test]
        public void MissingANeededKindDoesNotMatch()
        {
            recipe.needs = new[] { "Dill" };
            Assert.IsFalse(recipe.Matches(new Dictionary<string, float> { { "Cucumber", 20f } }, 20f));
            Assert.IsFalse(recipe.Matches(new Dictionary<string, float> { { "Dill", 0f } }, 20f));
        }

        [Test]
        public void ExactlyTheMostlyShareMatches()
        {
            recipe.mostly = "Cucumber";
            recipe.mostlyShare = 0.75f;
            var amounts = new Dictionary<string, float> { { "Cucumber", 15f }, { "Onion", 5f } };
            Assert.IsTrue(recipe.Matches(amounts, 20f));
            amounts["Onion"] = 6f;
            Assert.IsFalse(recipe.Matches(amounts, 21f));
        }

        [Test]
        public void NothingMainNeverMatchesAMostly()
        {
            recipe.mostly = "Cucumber";
            Assert.IsFalse(recipe.Matches(new Dictionary<string, float> { { "Cucumber", 0f } }, 0f));
        }

        [Test]
        public void NoRulesMatchesAnything()
        {
            Assert.IsTrue(recipe.Matches(new Dictionary<string, float>(), 0f));
        }
    }
}
