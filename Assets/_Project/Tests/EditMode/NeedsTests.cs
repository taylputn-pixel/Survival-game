using GrandmasHouse.Data;
using GrandmasHouse.Food;
using GrandmasHouse.Player;
using NUnit.Framework;
using UnityEngine;

namespace GrandmasHouse.Tests
{
    // Hunger, rest, starving and sickness, with the tuning asset's defaults.
    public class NeedsTests
    {
        SurvivalSettings settings;
        Needs needs;

        [SetUp]
        public void Begin()
        {
            settings = ScriptableObject.CreateInstance<SurvivalSettings>();
            needs = new Needs(settings);
        }

        [TearDown]
        public void End() => Object.DestroyImmediate(settings);

        [Test]
        public void AFullStomachEmptiesInADayAwake()
        {
            needs.hunger = 100f;
            needs.Pass(12f, asleep: false, settings);
            Assert.AreEqual(50f, needs.hunger, 0.01f);
            needs.Pass(12f, asleep: false, settings);
            Assert.AreEqual(0f, needs.hunger, 0.01f);
            Assert.AreEqual(HungerLevel.Starving, needs.Hunger(settings));
        }

        [Test]
        public void SleepingBurnsFoodSlowerAndRestores()
        {
            needs.hunger = 100f;
            needs.rest = 0f;
            needs.Pass(8f, asleep: true, settings);
            Assert.AreEqual(100f - 8f * settings.hungerPerHour * settings.hungerAsleep, needs.hunger, 0.01f);
            Assert.AreEqual(100f, needs.rest, 0.01f);
        }

        [Test]
        public void OnlyHoursAtEmptyCountAsStarving()
        {
            needs.hunger = 100f / 24f * 2f;
            needs.Pass(5f, asleep: false, settings);
            Assert.AreEqual(3f, needs.starvingHours, 0.01f);
        }

        [Test]
        public void StarvingLongEnoughIsMalnourishedThenDeath()
        {
            needs.hunger = 0f;
            needs.Pass(settings.malnourishedAfterHours, asleep: false, settings);
            Assert.AreEqual(Condition.Malnourished, needs.Condition(settings));
            Assert.IsFalse(needs.CanWalkBriskly(settings));
            needs.Pass(settings.deathAfterHours - settings.malnourishedAfterHours, asleep: false, settings);
            Assert.IsTrue(needs.IsDead(settings));
        }

        [Test]
        public void EatingStopsStarvingAndItHealsSlowly()
        {
            needs.hunger = 0f;
            needs.starvingHours = 30f;
            needs.Eat(50f, 0f, settings);
            Assert.AreEqual(50f, needs.hunger);
            needs.Pass(4f, asleep: false, settings);
            Assert.AreEqual(30f - 4f * settings.starvingRecoveryPerHour, needs.starvingHours, 0.01f);
        }

        [Test]
        public void EatingNeverOverfills()
        {
            needs.hunger = 90f;
            needs.Eat(30f, 0f, settings);
            Assert.AreEqual(100f, needs.hunger);
        }

        [Test]
        public void BadFoodMakesYouSickInsteadOfFeedingYou()
        {
            needs.hunger = 50f;
            needs.Eat(30f, settings.spoiledSickHours, settings);
            Assert.AreEqual(50f - settings.sickHungerLoss, needs.hunger, 0.01f);
            Assert.AreEqual(Condition.Sick, needs.Condition(settings));
            Assert.Less(needs.SpeedMultiplier(settings), 1f);
            needs.Pass(settings.spoiledSickHours, asleep: false, settings);
            Assert.IsFalse(needs.IsSick);
        }

        [Test]
        public void TirednessSlowsYouAndExhaustionStopsBriskWalking()
        {
            needs.rest = 100f;
            Assert.AreEqual(1f, needs.SpeedMultiplier(settings));
            Assert.IsTrue(needs.CanWalkBriskly(settings));
            needs.rest = 0f;
            Assert.AreEqual(settings.exhaustedSpeed, needs.SpeedMultiplier(settings), 0.001f);
            Assert.IsFalse(needs.CanWalkBriskly(settings));
            Assert.AreEqual(RestLevel.Exhausted, needs.Rest(settings));
        }

        [Test]
        public void YouOnlyFumbleWhenVeryTired()
        {
            needs.rest = settings.clumsyBelow;
            Assert.AreEqual(0f, needs.DropChancePerSecond(settings));
            needs.rest = 0f;
            Assert.AreEqual(settings.dropChancePerSecond, needs.DropChancePerSecond(settings), 0.0001f);
        }

        [Test]
        public void StayingUpAllNightLeavesYouExhausted()
        {
            needs.rest = 100f;
            needs.Pass(18f, asleep: false, settings);
            Assert.AreEqual(0f, needs.rest, 0.01f);
        }

        [Test]
        public void FoodSpoilsOverItsShelfLife()
        {
            float half = Perishable.Spoil(1f, 2.5f * 24f, 5f, 1f);
            Assert.AreEqual(0.5f, half, 0.001f);
            Assert.AreEqual(0f, Perishable.Spoil(half, 10f * 24f, 5f, 1f));
        }

        [Test]
        public void TheFridgeSlowsSpoilingAndTheFreezerStopsIt()
        {
            float room = Perishable.Spoil(1f, 24f, 5f, 1f);
            float fridge = Perishable.Spoil(1f, 24f, 5f, 0.3f);
            Assert.Greater(fridge, room);
            Assert.AreEqual(1f, Perishable.Spoil(1f, 24f * 30f, 5f, 0f));
        }
    }
}
