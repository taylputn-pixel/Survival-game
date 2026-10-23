using Unity.Netcode;

namespace GrandmasHouse.Trade
{
    // A friend's handshake, sent to the host, who makes the trade (their
    // stock and the ledger travel back through the SharedState beside it).
    public class TraderNet : NetworkBehaviour
    {
        Trader trader;

        void Awake() => trader = GetComponent<Trader>();

        public void Deal(int[] counts) => DealRpc(counts);

        public void Welcome() => WelcomeRpc();

        [Rpc(SendTo.Server)]
        void WelcomeRpc() => trader.Welcome();

        [Rpc(SendTo.Server)]
        void DealRpc(int[] counts) => trader.DealHere(counts, null);
    }
}
