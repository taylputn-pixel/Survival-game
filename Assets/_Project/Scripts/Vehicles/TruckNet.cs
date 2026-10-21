using GrandmasHouse.Carrying;
using Unity.Netcode;

namespace GrandmasHouse.Vehicles
{
    // Who's driving the truck in a shared game. Asking hands it (and the
    // truck's NetworkObject) to you through the host; getting out gives it
    // back. The driver's computer moves the truck (owner NetworkTransform).
    public class TruckNet : NetworkBehaviour
    {
        const ulong NoOne = ulong.MaxValue;
        readonly NetworkVariable<ulong> driver = new NetworkVariable<ulong>(NoOne);

        Truck truck;
        PlayerHands waiting;

        public bool Taken => IsSpawned && driver.Value != NoOne;

        void Awake() => truck = GetComponent<Truck>();

        public void Ask(PlayerHands hands)
        {
            waiting = hands;
            AskRpc();
        }

        public void Release() => ReleaseRpc();

        [Rpc(SendTo.Server)]
        void AskRpc(RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if (driver.Value != NoOne && NetworkManager.ConnectedClients.ContainsKey(driver.Value)) return;
            driver.Value = who;
            if (OwnerClientId != who) NetworkObject.ChangeOwnership(who);
            GrantedRpc(RpcTarget.Single(who, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void GrantedRpc(RpcParams rpc = default)
        {
            if (waiting != null) truck.SitIn(waiting);
            waiting = null;
        }

        [Rpc(SendTo.Server)]
        void ReleaseRpc(RpcParams rpc = default)
        {
            if (driver.Value == rpc.Receive.SenderClientId) driver.Value = NoOne;
        }

        void Update()
        {
            // The driver left the game: the seat's free again.
            if (IsServer && driver.Value != NoOne && !NetworkManager.ConnectedClients.ContainsKey(driver.Value)) driver.Value = NoOne;
        }
    }
}
