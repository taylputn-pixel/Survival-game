using System;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Player
{
    public enum HungerLevel { Full, Fed, Peckish, Hungry, Starving }
    public enum RestLevel { Rested, Tired, VeryTired, Exhausted }
    public enum Condition { Healthy, Sick, Malnourished, Collapsed, Freezing }
    public enum WarmthLevel { Warm, Chilly, Cold, Freezing }

    // The body's state as plain numbers, so the rules can be tested without
    // a scene: hunger, rest and warmth (100 full, 0 empty), hours spent
    // starving and freezing, and hours of sickness left. Time is passed in as
    // in-game hours, with how cold it feels where you are (Fahrenheit).
    [Serializable]
    public class Needs
    {
        public float hunger;
        public float rest;
        public float starvingHours;
        public float sickHours;
        public float warmth = 100f;
        public float freezingHours;

        public Needs(SurvivalSettings settings)
        {
            hunger = settings.startHunger;
            rest = settings.startRest;
            warmth = settings.startWarmth;
        }

        public bool IsSick => sickHours > 0f;

        public void Pass(float hours, bool asleep, SurvivalSettings settings) =>
            Pass(hours, asleep, settings, settings.comfortableAbove);

        public void Pass(float hours, bool asleep, SurvivalSettings settings, float feltTemperature)
        {
            if (hours <= 0f || Collapsed(settings))
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
            Chill(hours, feltTemperature, settings);
        }

        // Warm enough and you warm back up; colder than comfortable and you
        // lose warmth, faster the colder it is. Hours at none count toward
        // freezing (and come back off once you're warming again).
        void Chill(float hours, float felt, SurvivalSettings settings)
        {
            if (felt >= settings.comfortableAbove)
            {
                warmth = Mathf.Min(100f, warmth + settings.warmUpPerHour * hours);
                freezingHours = Mathf.Max(0f, freezingHours - settings.freezingRecoveryPerHour * hours);
                return;
            }
            float rate = (settings.comfortableAbove - felt) * settings.coldPerDegreeHour;
            float before = warmth;
            warmth = Mathf.Max(0f, warmth - rate * hours);
            if (warmth < 0.001f) warmth = 0f;
            float hoursWarm = rate > 0f ? Mathf.Min(hours, before / rate) : hours;
            freezingHours += hours - hoursWarm;
        }

        // Brought in from the cold by someone: back up, still chilled.
        public void WarmUp(SurvivalSettings settings)
        {
            warmth = Mathf.Max(warmth, settings.warmedUpTo);
            freezingHours = 0f;
        }

        public bool IsFreezing => warmth <= 0f;

        public WarmthLevel Warmth(SurvivalSettings settings) =>
            warmth <= 0f ? WarmthLevel.Freezing
            : warmth < settings.coldBelow ? WarmthLevel.Cold
            : warmth < settings.chillyBelow ? WarmthLevel.Chilly
            : WarmthLevel.Warm;

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

        // Starved long enough to collapse. Nothing changes until someone
        // feeds you (BringRound); if everyone collapses, the game is over.
        public bool Collapsed(SurvivalSettings settings) =>
            starvingHours >= settings.collapseAfterHours || freezingHours >= settings.freezeCollapseHours;

        // Fed while collapsed: back on your feet, still malnourished.
        public void BringRound(SurvivalSettings settings)
        {
            starvingHours = Mathf.Min(starvingHours, settings.malnourishedAfterHours);
        }
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
            Collapsed(settings) ? Player.Condition.Collapsed
            : IsMalnourished(settings) ? Player.Condition.Malnourished
            : IsFreezing ? Player.Condition.Freezing
            : IsSick ? Player.Condition.Sick
            : Player.Condition.Healthy;

        // Walking speed as a share of normal: tiredness, starving and
        // sickness each slow you, and the worst one wins.
        public float SpeedMultiplier(SurvivalSettings settings)
        {
            float tired = Mathf.Lerp(settings.exhaustedSpeed, 1f, Mathf.InverseLerp(0f, settings.tiredBelow, rest));
            float starving = IsMalnourished(settings) ? settings.malnourishedSpeed : 1f;
            float sick = IsSick ? settings.sickSpeed : 1f;
            float cold = Mathf.Lerp(settings.coldSpeed, 1f, Mathf.InverseLerp(0f, settings.coldSlowBelow, warmth));
            return Mathf.Min(Mathf.Min(tired, cold), Mathf.Min(starving, sick));
        }

        public bool CanWalkBriskly(SurvivalSettings settings) => rest >= settings.exhaustedBelow && !IsMalnourished(settings);

        // The chance each second of fumbling what you hold, from 0 at the
        // clumsy line up to the full chance at 0 rest.
        public float DropChancePerSecond(SurvivalSettings settings) =>
            rest >= settings.clumsyBelow ? 0f : Mathf.Lerp(settings.dropChancePerSecond, 0f, rest / settings.clumsyBelow);
    }
}
