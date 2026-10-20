using System;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Grandma's electric food dehydrator. Lay produce on the tray (look at
    // it holding the food), then turn it on with empty hands. Warm air dries
    // what's on the tray over half a day; dried food keeps many times
    // longer and goes straight into the soup pot. It runs on power.
    public class Dehydrator : MonoBehaviour, IInteractable, ISharedUse, ISaveable, IAfterLoad
    {
        [SerializeField] DayClock clock;
        [Tooltip("Where things lie on the tray.")]
        [SerializeField] Transform[] spots;
        [SerializeField] Transform dial;
        [SerializeField] Renderer lamp;
        [Tooltip("In-game hours to dry what's on the tray.")]
        [SerializeField] float dryHours = 12f;
        [SerializeField] string saveKey = "dehydrator";

        public bool On { get; private set; }
        public float DryHours => dryHours;

        float lastHours;
        SoundLoop hum;
        MaterialPropertyBlock block;

        void Start()
        {
            lastHours = clock.TotalHours;
            hum = Sfx.Loop(SoundId.Simmer, transform);
            block = new MaterialPropertyBlock();
            Show();
        }

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        void Update()
        {
            float now = clock.TotalHours;
            float hours = now - lastHours;
            lastHours = now;
            if (!On || hours <= 0f || !Hosting)
            {
                return;
            }
            foreach (var spot in spots)
            {
                var food = spot.GetComponentInChildren<Perishable>();
                if (food != null) food.Dry(hours, dryHours);
            }
        }

        Transform FreeSpot()
        {
            foreach (var spot in spots)
            {
                if (spot.GetComponentInChildren<Pickup>() == null) return spot;
            }
            return null;
        }

        public bool CanInteract(PlayerHands hands)
        {
            if (hands.IsEmpty) return true;
            var held = hands.Held;
            return held.GetComponent<FreshFood>() != null && held.GetComponent<Perishable>() != null && FreeSpot() != null;
        }

        public string PromptVerb(PlayerHands hands)
        {
            if (hands.IsEmpty) return On ? "Turn off the dehydrator" : "Turn on the dehydrator";
            return "Lay it on the tray";
        }

        public void Interact(PlayerHands hands)
        {
            if (hands.IsEmpty)
            {
                On = !On;
                Sfx.Play(SoundId.KnobClick, transform.position);
                Show();
                return;
            }
            Pickup item = hands.TakeHeld();
            item.AttachTo(FreeSpot(), Vector3.up * item.RestHalfHeight, item.RestTilt, 0.25f, collidersOn: true);
        }

        void Show()
        {
            if (hum != null) hum.Set(On ? 0.25f : 0f);
            if (dial != null) dial.localRotation = Quaternion.Euler(0f, 0f, On ? -120f : 0f);
            if (lamp != null && block != null)
            {
                lamp.GetPropertyBlock(block);
                block.SetColor("_BaseColor", On ? new Color(1f, 0.55f, 0.2f) : new Color(0.25f, 0.2f, 0.18f));
                lamp.SetPropertyBlock(block);
            }
        }

        [Serializable]
        class State
        {
            public bool on;
        }

        public string SaveKey => saveKey;
        public string Capture() => JsonUtility.ToJson(new State { on = On });

        public void Restore(string json)
        {
            On = JsonUtility.FromJson<State>(json).on;
            Show();
        }

        // Things on the tray stay where they were; the clock jump isn't drying time.
        public void AfterLoad() => lastHours = clock.TotalHours;
    }
}
