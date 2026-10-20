using System;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Something that cures food so it keeps: the dehydrator, the smokehouse,
    // the salt box. Lay food in its spots; while it's working, the host
    // cures what's there over `cureHours` (Perishable.CureFor). Each kind
    // says what it takes, how it's started and what it makes.
    public abstract class Curer : MonoBehaviour, IInteractable, ISharedUse, ISaveable, IAfterLoad
    {
        [SerializeField] protected DayClock clock;
        [Tooltip("Where things go.")]
        [SerializeField] Transform[] spots;
        [Tooltip("In-game hours to cure what's in it.")]
        [UnityEngine.Serialization.FormerlySerializedAs("dryHours")]
        [SerializeField] float cureHours = 12f;
        [SerializeField] string saveKey = "curer";

        public bool On { get; protected set; }
        public float CureHours => cureHours;

        protected abstract Perishable.Cure Makes { get; }
        protected virtual bool Working => On;
        protected abstract string PutVerb { get; }

        float lastHours;

        protected virtual void Start()
        {
            lastHours = clock.TotalHours;
            Show();
        }

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        protected virtual void Update()
        {
            float now = clock.TotalHours;
            float hours = now - lastHours;
            lastHours = now;
            if (hours <= 0f || !Hosting)
            {
                return;
            }
            Tick(hours);
        }

        // Some time passing in here.
        protected virtual void Tick(float hours)
        {
            if (!Working)
            {
                return;
            }
            foreach (var spot in spots)
            {
                var food = spot.GetComponentInChildren<Perishable>();
                if (food == null || food.Cured) continue;
                food.CureFor(Makes, hours, cureHours);
                if (food.Cured) Finished(food);
            }
        }

        protected virtual void Finished(Perishable food) { }

        Transform FreeSpot()
        {
            foreach (var spot in spots)
            {
                if (spot.GetComponentInChildren<Pickup>() == null) return spot;
            }
            return null;
        }

        // What it'll take: fresh food that isn't cured yet.
        protected virtual bool Takes(Pickup held) =>
            held.GetComponent<FreshFood>() != null && held.TryGetComponent(out Perishable food) && !food.Cured;

        bool CanPut(PlayerHands hands) => !hands.IsEmpty && Takes(hands.Held) && FreeSpot() != null;

        public virtual bool CanInteract(PlayerHands hands) => CanPut(hands);
        public virtual string PromptVerb(PlayerHands hands) => PutVerb;

        public virtual void Interact(PlayerHands hands)
        {
            if (!CanPut(hands))
            {
                return;
            }
            Pickup item = hands.TakeHeld();
            item.AttachTo(FreeSpot(), Vector3.up * item.RestHalfHeight, item.RestTilt, 0.25f, collidersOn: true);
        }

        protected virtual void Show() { }

        [Serializable]
        protected class State
        {
            public bool on;
            public float left;
            public int salt;
        }

        public string SaveKey => saveKey;
        public string Capture() => JsonUtility.ToJson(Save());

        public void Restore(string json)
        {
            Load(JsonUtility.FromJson<State>(json));
            Show();
        }

        protected virtual State Save() => new State { on = On };
        protected virtual void Load(State state) => On = state.on;

        // Things in it stay where they were; the clock jump isn't curing time.
        public void AfterLoad() => lastHours = clock.TotalHours;
    }
}
