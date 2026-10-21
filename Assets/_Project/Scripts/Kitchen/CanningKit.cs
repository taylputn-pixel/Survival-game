using System;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Grandma's canning things: the packing towel and its stack of lids, the
    // hot pad, the cooling towel and the label box. Put away, they free the
    // counter and the table for other work and pack into a box to carry
    // off; carry the box back to the counter to set up again. Anything
    // left sitting on them stays where it was. Whether they're set up is
    // saved, and shared in a game with friends; the box is an ordinary item.
    public class CanningKit : MonoBehaviour, ISaveable
    {
        [Tooltip("Everything that packs away: the packing towel, hot pad, cooling towel and spots, label box.")]
        [SerializeField] GameObject[] parts;
        [Tooltip("The box they pack into.")]
        [SerializeField] Pickup boxPrefab;

        public bool IsSetUp { get; private set; } = true;
        public static CanningKit Instance { get; private set; }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        public static bool IsBox(Pickup item) => item != null && item.GetComponent<CanningBox>() != null;

        // Packed up into the box, which goes into your hands.
        public void PutAway(PlayerHands hands)
        {
            if (!IsSetUp) return;
            Release();
            Show(false);
            Vector3 at = hands.ViewPoint.position + hands.ViewPoint.forward * 0.5f;
            Pickup box = Instantiate(boxPrefab, at, Quaternion.Euler(0f, hands.transform.eulerAngles.y, 0f));
            box.name = boxPrefab.name;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && box.TryGetComponent(out NetworkObject shared))
            {
                shared.Spawn();
            }
            hands.Give(box);
        }

        // The box unpacked onto the counter: everything back where it goes.
        public void SetUp(PlayerHands hands)
        {
            if (IsSetUp || !IsBox(hands.Held)) return;
            Pickup box = hands.TakeHeld();
            Net.ItemNet.Remove(box.gameObject);
            Show(true);
        }

        // What's sitting on them stays put, on the counter.
        void Release()
        {
            var loose = new List<Pickup>();
            foreach (var part in parts)
            {
                if (part != null) loose.AddRange(part.GetComponentsInChildren<Pickup>());
            }
            foreach (var item in loose)
            {
                if (item.InContainer != null) continue;
                item.transform.SetParent(null, worldPositionStays: true);
                item.SetResting(true, true);
            }
        }

        void Show(bool setUp)
        {
            IsSetUp = setUp;
            foreach (var part in parts)
            {
                if (part == null) continue;
                foreach (var look in part.GetComponentsInChildren<Renderer>(true))
                {
                    if (look.GetComponentInParent<Pickup>() == null) look.enabled = setUp;
                }
                foreach (var solid in part.GetComponentsInChildren<Collider>(true))
                {
                    if (solid.GetComponentInParent<Pickup>() == null) solid.enabled = setUp;
                }
            }
        }

        [Serializable]
        class State
        {
            public bool setUp = true;
        }

        public string SaveKey => "canning-kit";
        public string Capture() => JsonUtility.ToJson(new State { setUp = IsSetUp });

        public void Restore(string json)
        {
            bool setUp = JsonUtility.FromJson<State>(json).setUp;
            if (setUp != IsSetUp)
            {
                if (!setUp) Release();
                Show(setUp);
            }
        }
    }
}
