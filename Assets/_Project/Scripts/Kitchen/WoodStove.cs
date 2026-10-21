using System;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Grandma's cast-iron wood stove, the only heat in the house. Feed it
    // firewood (look at it holding wood) and light it with empty hands. Each
    // log burns a few hours; when the last one's gone the fire dies. A lit
    // stove warms the whole house over an hour or so and keeps it warm a
    // while after it goes out. The host burns the wood.
    public class WoodStove : MonoBehaviour, IInteractable, ISharedUse, ISaveable, IAfterLoad
    {
        [SerializeField] DayClock clock;
        [SerializeField] int capacity = 4;
        [Tooltip("In-game hours one log burns.")]
        [SerializeField] float hoursPerLog = 4f;
        [Tooltip("Hours for the house to warm up fully, and to go cold after.")]
        [SerializeField] float warmsHours = 1f;
        [SerializeField] float coolsHours = 3f;
        [SerializeField] Renderer firebox;
        [SerializeField] Light glow;
        [SerializeField] Transform[] logSpots;
        [SerializeField] string saveKey = "wood-stove";

        public static readonly List<WoodStove> All = new List<WoodStove>();

        public int Logs { get; private set; }
        public bool Burning { get; private set; }
        public float FuelLeft { get; private set; }
        // How warm it's running now, 0 (cold) to 1 (roaring).
        public float Heat { get; private set; }
        public int Capacity => capacity;

        float lastHours;
        MaterialPropertyBlock block;
        GameObject[] shownLogs;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            lastHours = clock.TotalHours;
            block = new MaterialPropertyBlock();
            clock.Jumped += AfterLoad;
            Show();
        }

        void OnDestroy()
        {
            if (clock != null) clock.Jumped -= AfterLoad;
        }

        // The clock jumped to a saved (or the host's) time: that isn't burning time.
        public void AfterLoad() => lastHours = clock.TotalHours;

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        void Update()
        {
            float now = clock.TotalHours;
            float hours = now - lastHours;
            lastHours = now;
            if (hours <= 0f) return;
            if (Burning && Hosting) Burn(hours);
            // The house warms and cools on every computer the same way.
            Heat = Burning ? Mathf.Min(1f, Heat + hours / Mathf.Max(0.01f, warmsHours)) : Mathf.Max(0f, Heat - hours / Mathf.Max(0.01f, coolsHours));
            if (glow != null) glow.intensity = Burning ? Mathf.Lerp(0.6f, 1.6f, Heat) : 0f;
        }

        void Burn(float hours)
        {
            FuelLeft -= hours;
            while (FuelLeft <= 0f && Burning)
            {
                if (Logs > 0)
                {
                    Logs--;
                    FuelLeft += hoursPerLog;
                }
                else
                {
                    FuelLeft = 0f;
                    Burning = false;
                }
            }
            Show();
        }

        static bool IsWood(PlayerHands hands) => !hands.IsEmpty && hands.Held.TryGetComponent(out Firewood _);

        public bool CanInteract(PlayerHands hands)
        {
            if (IsWood(hands)) return Logs < capacity;
            return hands.IsEmpty && !Burning && Logs > 0;
        }

        public string PromptVerb(PlayerHands hands) => IsWood(hands) ? "Put wood in the stove" : "Light the stove";

        public void Interact(PlayerHands hands)
        {
            if (IsWood(hands))
            {
                Pickup log = hands.TakeHeld();
                Logs++;
                Net.ItemNet.Remove(log.gameObject);
                Sfx.Play(SoundId.SetDown, transform.position, 0.7f);
            }
            else
            {
                Light();
            }
            Show();
        }

        public void AddLog() { if (Logs < capacity) Logs++; Show(); }

        // A match to the kindling: the first log catches.
        public void Light()
        {
            if (Burning || Logs <= 0) return;
            Logs--;
            FuelLeft = hoursPerLog;
            Burning = true;
            Sfx.Play(SoundId.Paper, transform.position);
            Show();
        }

        void Show()
        {
            if (glow != null) glow.enabled = Burning;
            if (firebox != null && block != null)
            {
                firebox.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Burning ? new Color(1f, 0.45f, 0.12f) : new Color(0.12f, 0.1f, 0.09f));
                firebox.SetPropertyBlock(block);
            }
            if (logSpots != null)
            {
                for (int i = 0; i < logSpots.Length; i++) logSpots[i].gameObject.SetActive(i < Logs);
            }
        }

        [Serializable]
        class State
        {
            public int logs;
            public bool burning;
            public float fuel;
            public float heat;
        }

        public string SaveKey => saveKey;

        public string Capture() => JsonUtility.ToJson(new State
        {
            logs = Logs, burning = Burning, fuel = Mathf.Round(FuelLeft * 10f) / 10f, heat = Mathf.Round(Heat * 10f) / 10f,
        });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Logs = state.logs;
            Burning = state.burning;
            FuelLeft = state.fuel;
            // Sharing sends this often; only a load (big change) takes the saved heat.
            if (Mathf.Abs(Heat - state.heat) > 0.15f) Heat = state.heat;
            if (clock != null) lastHours = clock.TotalHours;
            Show();
        }
    }
}
