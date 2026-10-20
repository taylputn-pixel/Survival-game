using System;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Player
{
    // One player's hunger and rest, run by the world clock. Tired, starving
    // or sick, you walk slower; exhausted you can't hurry and you fumble
    // what you carry; starve long enough and you die. You're never made to
    // sleep. The rules themselves are in Needs.
    public class PlayerNeeds : MonoBehaviour, IAfterLoad
    {
        [SerializeField] SurvivalSettings settings;
        [SerializeField] DayClock clock;
        [SerializeField] FirstPersonController controller;
        [SerializeField] PlayerHands hands;

        public Needs State { get; private set; }
        public SurvivalSettings Settings => settings;
        // Set by the bed while you sleep.
        public bool Asleep { get; set; }
        // Asleep, and the sun is up, so you can get out of bed.
        public bool CanGetUp { get; set; }
        public bool IsDead { get; private set; }
        bool On => clock.Mode.hungerHasPenalties;

        // Something was eaten: true if it made you sick.
        public event Action<bool> Ate;
        public event Action Died;

        float lastHours;
        float nextFumbleRoll;

        void Awake()
        {
            State = new Needs(settings);
            // The player is spawned into the world, so it finds the world's clock.
            if (clock == null) clock = FindAnyObjectByType<DayClock>();
        }

        // After every Awake, so the clock has set its starting hour.
        void Start()
        {
            lastHours = clock.TotalHours;
            clock.Jumped += AfterLoad;
        }

        void OnDestroy() => clock.Jumped -= AfterLoad;

        void Update()
        {
            float now = clock.TotalHours;
            float hours = now - lastHours;
            lastHours = now;
            if (!On || IsDead)
            {
                return;
            }

            State.Pass(hours, Asleep, settings);
            controller.SpeedMultiplier = State.SpeedMultiplier(settings);
            controller.BriskAllowed = State.CanWalkBriskly(settings);
            Fumble();

            if (clock.Mode.gameOver && State.IsDead(settings))
            {
                IsDead = true;
                controller.InputEnabled = false;
                Died?.Invoke();
            }
        }

        // Exhausted and on the move, now and then what you hold slips out
        // of your hand. Baskets and pots are held with both hands and stay.
        void Fumble()
        {
            if (Time.time < nextFumbleRoll)
            {
                return;
            }
            nextFumbleRoll = Time.time + 1f;
            Pickup held = hands.Held;
            if (Asleep || held == null || !controller.InputEnabled || !controller.IsMoving
                || held.NeedsBothHands || !held.CanSetDown)
            {
                return;
            }
            if (UnityEngine.Random.value < State.DropChancePerSecond(settings))
            {
                hands.Take(held).Drop();
            }
        }

        public bool CanEat(IEdible food) => On && !IsDead && food != null && food.CanEat;

        public void Eat(IEdible food)
        {
            float sickFor = food.SickHours(settings);
            State.Eat(food.Nutrition, sickFor, settings);
            Sfx.Play(SoundId.Eat, hands.ViewPoint.position);
            food.Eaten(hands);
            Ate?.Invoke(sickFor > 0f);
        }

        // For testing from the dev menu.
        public void Set(float hunger, float rest, float starvingHours = 0f)
        {
            State.hunger = hunger;
            State.rest = rest;
            State.starvingHours = starvingHours;
            State.sickHours = 0f;
        }

        // Saved with the player (see PlayerNet and SaveManager).
        public string Capture() => JsonUtility.ToJson(State);

        public void Restore(string json) => JsonUtility.FromJsonOverwrite(json, State);

        // The clock jumped to the saved time; don't count that jump.
        public void AfterLoad() => lastHours = clock.TotalHours;
    }
}
