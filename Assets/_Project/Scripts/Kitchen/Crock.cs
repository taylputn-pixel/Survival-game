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
    // Grandma's stoneware crock, down in the root cellar, for sauerkraut.
    // Pack shredded cabbage in by the handful from the cutting board and
    // shake in salt; each night it ferments, and after about a week it's
    // sauerkraut, which keeps all winter in the crock. Scoop it into a bowl
    // to eat. Too little salt and it goes bad instead; scoop it too soon
    // and it's only salted cabbage.
    public class Crock : MonoBehaviour, IInteractable, ISharedUse, ISaveable
    {
        [SerializeField] DayClock clock;
        [Tooltip("Shredded cabbage the crock holds.")]
        [SerializeField] int capacity = 48;
        [Tooltip("Shreds one scoop of salt is right for.")]
        [SerializeField] int shredsPerScoop = 12;
        [SerializeField] int daysToFerment = 6;
        [Tooltip("Days without enough salt before it goes bad.")]
        [SerializeField] int daysToSpoil = 2;
        [Tooltip("Shreds in one bowl.")]
        [SerializeField] int shredsPerBowl = 8;
        [Tooltip("Hunger filled per shred: salted cabbage, and once it's sauerkraut.")]
        [SerializeField] float rawFood = 0.6f;
        [SerializeField] float krautFood = 1.4f;
        [SerializeField] Transform contents;
        [SerializeField] float fullHeight = 0.16f;
        [SerializeField] Color cabbageColor = new Color(0.82f, 0.88f, 0.62f);
        [SerializeField] Color krautColor = new Color(0.9f, 0.86f, 0.6f);
        [SerializeField] Color spoiledColor = new Color(0.4f, 0.42f, 0.3f);
        [SerializeField] string saveKey = "crock";

        public int Shreds { get; private set; }
        public int Salt { get; private set; }
        public int Days { get; private set; }
        public bool Spoiled { get; private set; }
        public bool Ready => !Spoiled && Days >= daysToFerment && Shreds > 0;
        bool Salted => Salt * shredsPerScoop >= Shreds;

        MaterialPropertyBlock block;

        void Awake() => block = new MaterialPropertyBlock();
        void Start() => Show();
        void OnEnable() => clock.NewDay += Night;
        void OnDisable() => clock.NewDay -= Night;

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        void Night(int day)
        {
            if (Hosting) Ferment();
        }

        // One night in the cool of the cellar.
        public void Ferment()
        {
            if (Shreds <= 0 || Spoiled) return;
            Days++;
            if (!Salted && Days >= daysToSpoil) Spoiled = true;
            Show();
        }

        public bool CanInteract(PlayerHands hands)
        {
            if (hands.IsEmpty) return false;
            if (Handful.Held(hands) is Handful handful) return handful.Kind.StartsWith("Cabbage") && Shreds < capacity;
            if (hands.Held.TryGetComponent(out Seasoning salt)) return salt.Kind == Seasoning.Type.Salt && !salt.IsEmpty;
            if (hands.Held.TryGetComponent(out Bowl bowl)) return bowl.IsEmpty && Shreds > 0;
            return false;
        }

        public string PromptVerb(PlayerHands hands)
        {
            if (Handful.Held(hands) != null) return "Pack into the crock";
            if (hands.Held.GetComponent<Seasoning>() != null) return "Add a scoop of salt";
            return "Scoop into the bowl";
        }

        public void Interact(PlayerHands hands)
        {
            if (Handful.Held(hands) is Handful handful)
            {
                var pieces = handful.Take(Mathf.Min(handful.Count, capacity - Shreds));
                foreach (var piece in pieces) Destroy(piece.gameObject);
                // Fresh cabbage starts the ferment over.
                Shreds += pieces.Count;
                Days = 0;
                handful.DoneWith(hands);
                Sfx.Play(SoundId.SliceDrop, transform.position, 0.8f, 0.1f);
            }
            else if (hands.Held.TryGetComponent(out Seasoning box))
            {
                if (box.Use(1f) > 0f) Salt++;
                if (box.TryGetComponent(out Net.ItemNet saltNet)) saltNet.Share();
                Sfx.Play(SoundId.SaltShake, transform.position);
            }
            else if (hands.Held.TryGetComponent(out Bowl bowl))
            {
                int taken = Mathf.Min(shredsPerBowl, Shreds);
                string dish = Spoiled ? "Spoiled cabbage" : Ready ? "Sauerkraut" : "Salted cabbage";
                bowl.Fill(dish, taken * (Ready ? krautFood : rawFood), Spoiled ? spoiledColor : Ready ? krautColor : cabbageColor, Spoiled);
                Shreds -= taken;
                Salt = Mathf.Min(Salt, Mathf.CeilToInt(Shreds / (float)shredsPerScoop));
                if (Shreds == 0) { Days = 0; Spoiled = false; Salt = 0; }
                if (bowl.TryGetComponent(out Net.ItemNet net)) net.Share();
            }
            Show();
        }

        void Show()
        {
            if (contents == null || block == null) return;
            contents.gameObject.SetActive(Shreds > 0);
            contents.localScale = new Vector3(contents.localScale.x, Mathf.Max(0.001f, fullHeight * Shreds / capacity), contents.localScale.z);
            var r = contents.GetComponentInChildren<Renderer>(true);
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Spoiled ? spoiledColor : Ready ? krautColor : cabbageColor);
            r.SetPropertyBlock(block);
        }

        [Serializable]
        class State
        {
            public int shreds;
            public int salt;
            public int days;
            public bool spoiled;
        }

        public string SaveKey => saveKey;
        public string Capture() => JsonUtility.ToJson(new State { shreds = Shreds, salt = Salt, days = Days, spoiled = Spoiled });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Shreds = state.shreds;
            Salt = state.salt;
            Days = state.days;
            Spoiled = state.spoiled;
            Show();
        }
    }
}
