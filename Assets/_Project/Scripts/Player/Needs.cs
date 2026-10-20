using System;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Player
{
    public enum HungerLevel { Full, Fed, Peckish, Hungry, Starving }
    public enum RestLevel { Rested, Tired, VeryTired, Exhausted }
    public enum Condition { Healthy, Sick, Malnourished, Dead }

    // The body's state as plain numbers, so the rules can be tested without
    // a scene: hunger and rest (100 full, 0 empty), hours spent starving, and
    // hours of sickness left. Time is passed in as in-game hours.
    [Serializable]
    public class Needs
    {
        public float hunger;
        public float rest;
        public float starvingHours;
        public float sickHours;

        public Needs(SurvivalSettings settings)
        {
            hunger = settings.startHunger;
            rest = settings.startRest;
        }

        public bool IsSick => sickHours > 0f;

        public void Pass(float hours, bool asleep, SurvivalSettings settings)
        {
            if (hours <= 0f || IsDead(settings))
            {
                return;
            }

            float hungerRate = settings.hungerPerHour * (asleep ? settings.hungerAsleep : 1f) * (IsSick ? settings.sickHungerMultiplier : 1f);
            float hungerBefore = hunger;
            hunger = hunger - hungerRate * hours;
            // Rounding can leave a crumb; that's still empty.
            if (hunger < 0.001f) hunger = 0f;
            // Only the hours spent at empty count toward starving.
            float hoursFed = hungerRate > 0f ? Mathf.Min(hours, hungerBefore / hungerRate) : hours;
            float hoursEmpty = hours - hoursFed;
            starvingHours = Mathf.Max(0f, starvingHours - settings.starvingRecoveryPerHour * hoursFed) + hoursEmpty;

            rest = asleep
                ? Mathf.Min(100f, rest + settings.restAsleepPerHour * hours)
                : Mathf.Max(0f, rest - settings.restPerHour * hours);
            sickHours = Mathf.Max(0f, sickHours - hours);
        }

        // Food that's gone bad, or a jar that wasn't safe, makes you sick
        // instead of feeding you.
        public void Eat(float nutrition, float sickFor, SurvivalSettings settings)
        {
            if (sickFor > 0f)
            {
                sickHours = Mathf.Max(sickHours, sickFor);
                hunger = Mathf.Max(0f, hunger - settings.sickHungerLoss);
                return;
            }
            hunger = Mathf.Min(100f, hunger + nutrition);
        }

        public bool IsDead(SurvivalSettings settings) => starvingHours >= settings.deathAfterHours;
        public bool IsMalnourished(SurvivalSettings settings) => starvingHours >= settings.malnourishedAfterHours;

        public HungerLevel Hunger(SurvivalSettings settings) =>
            hunger <= 0f ? HungerLevel.Starving
            : hunger < settings.hungryBelow ? HungerLevel.Hungry
            : hunger < 50f ? HungerLevel.Peckish
            : hunger < 80f ? HungerLevel.Fed
            : HungerLevel.Full;

        public RestLevel Rest(SurvivalSettings settings) =>
            rest < settings.exhaustedBelow ? RestLevel.Exhausted
            : rest < settings.tiredBelow ? RestLevel.VeryTired
            : rest < 60f ? RestLevel.Tired
            : RestLevel.Rested;

        public Condition Condition(SurvivalSettings settings) =>
            IsDead(settings) ? Player.Condition.Dead
            : IsMalnourished(settings) ? Player.Condition.Malnourished
            : IsSick ? Player.Condition.Sick
            : Player.Condition.Healthy;

        // Walking speed as a share of normal: tiredness, starving and
        // sickness each slow you, and the worst one wins.
        public float SpeedMultiplier(SurvivalSettings settings)
        {
            float tired = Mathf.Lerp(settings.exhaustedSpeed, 1f, Mathf.InverseLerp(0f, settings.tiredBelow, rest));
            float starving = IsMalnourished(settings) ? settings.malnourishedSpeed : 1f;
            float sick = IsSick ? settings.sickSpeed : 1f;
            return Mathf.Min(tired, Mathf.Min(starving, sick));
        }

        public bool CanWalkBriskly(SurvivalSettings settings) => rest >= settings.exhaustedBelow && !IsMalnourished(settings);

        // The chance each second of fumbling what you hold, from 0 at the
        // clumsy line up to the full chance at 0 rest.
        public float DropChancePerSecond(SurvivalSettings settings) =>
            rest >= settings.clumsyBelow ? 0f : Mathf.Lerp(settings.dropChancePerSecond, 0f, rest / settings.clumsyBelow);
    }
}
