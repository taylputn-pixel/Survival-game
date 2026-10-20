using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // Something in the house that saves its own state (the cutting board's
    // piles, a burner's knob), shared in a game with friends. Whoever changes
    // it sends its saved state; the host passes it on to everyone, who load
    // it the way a save would. A station with this is also worked by one
    // player at a time.
    public class SharedState : NetworkBehaviour
    {
        const ulong NoOne = ulong.MaxValue;
        const float CheckEvery = 0.25f;

        readonly NetworkVariable<ulong> user = new NetworkVariable<ulong>(NoOne);
        ISaveable saved;
        string lastKnown;
        float nextCheck;

        // Someone else is working here.
        public bool TakenByOther => IsSpawned && user.Value != NoOne && user.Value != NetworkManager.LocalClientId;

        void Awake() => saved = GetComponent<ISaveable>();

        public override void OnNetworkSpawn()
        {
            if (saved != null && !IsServer)
            {
                StateRequestRpc();
            }
        }

        // ---- One at a time ----

        public void Claim()
        {
            if (IsSpawned) ClaimRpc();
        }

        public void Release()
        {
            if (IsSpawned) ReleaseRpc();
        }

        [Rpc(SendTo.Server)]
        void ClaimRpc(RpcParams rpc = default)
        {
            if (user.Value == NoOne) user.Value = rpc.Receive.SenderClientId;
        }

        [Rpc(SendTo.Server)]
        void ReleaseRpc(RpcParams rpc = default)
        {
            if (user.Value == rpc.Receive.SenderClientId) user.Value = NoOne;
        }

        // ---- State ----

        void Update()
        {
            if (!IsSpawned || Time.time < nextCheck)
            {
                return;
            }
            nextCheck = Time.time + CheckEvery;
            // Someone left the game while working here.
            if (IsServer && user.Value != NoOne && !NetworkManager.ConnectedClients.ContainsKey(user.Value))
            {
                user.Value = NoOne;
            }
            if (saved == null)
            {
                return;
            }
            string now = saved.Capture();
            if (now == lastKnown)
            {
                return;
            }
            lastKnown = now;
            if (IsServer)
            {
                StateRpc(now);
            }
            else
            {
                ProposeRpc(now);
            }
        }

        [Rpc(SendTo.Server)]
        void ProposeRpc(string state)
        {
            Apply(state);
            StateRpc(state);
        }

        [Rpc(SendTo.NotServer)]
        void StateRpc(string state) => Apply(state);

        [Rpc(SendTo.Server)]
        void StateRequestRpc(RpcParams rpc = default) =>
            StateToRpc(saved.Capture(), RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));

        [Rpc(SendTo.SpecifiedInParams)]
        void StateToRpc(string state, RpcParams rpc = default) => Apply(state);

        void Apply(string state)
        {
            if (saved.Capture() != state)
            {
                saved.Restore(state);
                if (saved is IAfterLoad after)
                {
                    after.AfterLoad();
                }
            }
            // What it looks like here now, so taking in news isn't sent back.
            lastKnown = saved.Capture();
        }
    }
}
