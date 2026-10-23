using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // A friend's requests to the host: take a hen for the pot, let a chick out.
    public class FlockNet : NetworkBehaviour
    {
        Flock flock;

        void Awake() => flock = GetComponent<Flock>();

        public void Butcher(int hen, Vector3 at) => ButcherRpc(hen, at);

        public void Adopt(Vector3 at) => AdoptRpc(at);

        [Rpc(SendTo.Server)]
        void AdoptRpc(Vector3 at) => flock.AdoptHere(at);

        [Rpc(SendTo.Server)]
        void ButcherRpc(int hen, Vector3 at) => flock.Butcher(hen, at);
    }
}
