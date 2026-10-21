using Unity.Netcode;

namespace GrandmasHouse.Kitchen
{
    // A friend's last swing, sent to the host, who splits the log.
    public class ChoppingBlockNet : NetworkBehaviour
    {
        ChoppingBlock block;

        void Awake() => block = GetComponent<ChoppingBlock>();

        public void Split() => SplitRpc();

        [Rpc(SendTo.Server)]
        void SplitRpc() => block.SplitHere();
    }
}
