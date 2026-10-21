using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // A friend's request to the host to take a hen for the pot.
    public class FlockNet : NetworkBehaviour
    {
        Flock flock;

        void Awake() => flock = GetComponent<Flock>();

        public void Butcher(int hen, Vector3 at) => ButcherRpc(hen, at);

        [Rpc(SendTo.Server)]
        void ButcherRpc(int hen, Vector3 at) => flock.Butcher(hen, at);
    }
}
