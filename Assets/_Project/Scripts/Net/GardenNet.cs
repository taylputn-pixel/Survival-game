using System.Collections.Generic;
using GrandmasHouse.Garden;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // One garden bed in a shared game. The host grows it: nights, weeds,
    // suckers and pests all happen there, and the host sends the bed's state
    // out whenever it changes. A guest's planting, watering, weeding and
    // pinching show at once on their own screen and go to the host to do
    // for real. Ripe produce is shared like any other item.
    [RequireComponent(typeof(GardenBed))]
    public class GardenNet : NetworkBehaviour
    {
        [Tooltip("How often the host checks the bed for changes to send, in seconds.")]
        [SerializeField] float sendEvery = 0.25f;
        [Tooltip("How often a guest's watering is sent to the host, in seconds.")]
        [SerializeField] float waterEvery = 0.2f;

        GardenBed bed;
        string lastSent;
        float nextSend;
        float nextWater;
        readonly Dictionary<int, float> waterToSend = new Dictionary<int, float>();

        // Joined someone else's game: the host grows this bed.
        public bool IsGuest => IsSpawned && !IsServer;

        void Awake()
        {
            bed = GetComponent<GardenBed>();
            bed.Shared = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsGuest)
            {
                bed.Following = true;
                StateRequestRpc();
            }
        }

        public override void OnNetworkDespawn() => bed.Following = false;

        void Update()
        {
            if (!IsSpawned)
            {
                return;
            }
            if (IsServer && Time.time >= nextSend)
            {
                nextSend = Time.time + sendEvery;
                string state = bed.Capture();
                if (state != lastSent)
                {
                    lastSent = state;
                    StateRpc(state);
                }
            }
            if (IsGuest && waterToSend.Count > 0 && Time.time >= nextWater)
            {
                nextWater = Time.time + waterEvery;
                foreach (var pair in waterToSend)
                {
                    WaterRpc(pair.Key, pair.Value);
                }
                waterToSend.Clear();
            }
        }

        [Rpc(SendTo.NotServer)]
        void StateRpc(string state) => bed.ApplyShared(state);

        [Rpc(SendTo.Server)]
        void StateRequestRpc(RpcParams rpc = default) =>
            StateToRpc(bed.Capture(), RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));

        [Rpc(SendTo.SpecifiedInParams)]
        void StateToRpc(string state, RpcParams rpc = default) => bed.ApplyShared(state);

        // ---- A guest's work, done again by the host ----

        public void SendPlant(int spot, string crop) => PlantRpc(spot, crop);

        [Rpc(SendTo.Server)]
        void PlantRpc(int spot, string crop) => bed.PlantFor(spot, crop);

        public void SendWater(int spot, float amount)
        {
            waterToSend.TryGetValue(spot, out float sum);
            waterToSend[spot] = sum + amount;
        }

        [Rpc(SendTo.Server)]
        void WaterRpc(int spot, float amount) => bed.WaterFor(spot, amount);

        public void SendPull(Vector3 localPosition) => PullRpc(localPosition);

        [Rpc(SendTo.Server)]
        void PullRpc(Vector3 localPosition) => bed.PullFor(localPosition);

        public void SendTakeOff(int spot, bool pest) => TakeOffRpc(spot, pest);

        [Rpc(SendTo.Server)]
        void TakeOffRpc(int spot, bool pest) => bed.TakeOffFor(spot, pest);
    }
}
