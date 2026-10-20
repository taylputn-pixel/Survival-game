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
    // what you carry; starve long enough and you collapse where you stand.
    // Another player can feed you and bring you round; when everyone has
    // collapsed, the game is over. You're never made to sleep. The rules
    // themselves are in Needs.
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
        // Starved until you dropped; waiting for someone to feed you.
        public bool Collapsed { get; private set; }
        bool On => clock.Mode.hungerHasPenalties;

        // Something was eaten: true if it made you sick.
        public event Action<bool> Ate;
        public event Action FellDown;
        public event Action CameRound;
        // Everyone collapsed (or you, alone): the game is over.
        public event Action GameOver;

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
            if (!On || Collapsed)
            {
                return;
            }

            State.Pass(hours, Asleep, settings);
            controller.SpeedMultiplier = State.SpeedMultiplier(settings);
            controller.BriskAllowed = State.CanWalkBriskly(settings);
            Fumble();

            if (clock.Mode.gameOver && State.Collapsed(settings))
            {
                Collapse();
            }
        }

        void Collapse()
        {
            Collapsed = true;
            controller.InputEnabled = false;
            controller.LyingDown = true;
            // Whatever you held falls with you.
            while (hands.Held != null)
            {
                hands.TakeHeld().Drop();
            }
            FellDown?.Invoke();
            // In a shared game the host decides when everyone is down.
            if (!(TryGetComponent(out Net.PlayerNet net) && net.IsSpawned))
            {
                EndGame();
            }
        }

        public void EndGame() => GameOver?.Invoke();

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

        public bool CanEat(IEdible food) => On && !Collapsed && food != null && food.CanEat;

        public void Eat(IEdible food)
        {
            Fed(food.Nutrition, food.SickHours(settings));
            food.Eaten(hands);
        }

        // Food in your mouth, from your own hands or someone else's. Good
        // food brings you round if you'd collapsed.
        public void Fed(float nutrition, float sickFor)
        {
            State.Eat(nutrition, sickFor, settings);
            Sfx.Play(SoundId.Eat, hands.ViewPoint.position);
            Ate?.Invoke(sickFor > 0f);
            if (Collapsed && sickFor <= 0f && nutrition > 0f)
            {
                State.BringRound(settings);
                StandUp();
            }
        }

        void StandUp()
        {
            Collapsed = false;
            controller.LyingDown = false;
            controller.InputEnabled = true;
            CameRound?.Invoke();
        }

        // For testing from the dev menu.
        public void Set(float hunger, float rest, float starvingHours = 0f)
        {
            State.hunger = hunger;
            State.rest = rest;
            State.starvingHours = starvingHours;
            State.sickHours = 0f;
            if (Collapsed && !State.Collapsed(settings))
            {
                StandUp();
            }
        }

        // Saved with the player (see PlayerNet and SaveManager).
        public string Capture() => JsonUtility.ToJson(State);

        public void Restore(string json) => JsonUtility.FromJsonOverwrite(json, State);

        // The clock jumped to the saved time; don't count that jump.
        public void AfterLoad() => lastHours = clock.TotalHours;
    }
}
