using Unity.Netcode;

namespace GrandmasHouse.Animals
{
    // A friend's request to the host to take a hen for the pot.
    public class FlockNet : NetworkBehaviour
    {
        Flock flock;

        void Awake() => flock = GetComponent<Flock>();

        public void Butcher(int hen) => ButcherRpc(hen);

        [Rpc(SendTo.Server)]
        void ButcherRpc(int hen) => flock.Butcher(hen);
    }
}
