using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // Carries Netcode's messages over Steam's peer-to-peer sockets, so
    // friends connect through Steam with no addresses or port forwarding.
    // Steam relays the traffic when a direct route isn't possible.
    public class SteamTransport : NetworkTransport
    {
        // The host's Steam ID, set before starting as a client.
        public ulong HostSteamId { get; set; }

        struct Pending
        {
            public NetworkEvent kind;
            public ulong client;
            public ArraySegment<byte> data;
        }

        const int MaxMessagesPerPoll = 64;

        readonly Dictionary<ulong, HSteamNetConnection> connections = new Dictionary<ulong, HSteamNetConnection>();
        readonly Dictionary<uint, ulong> clientOf = new Dictionary<uint, ulong>();
        readonly Queue<Pending> pending = new Queue<Pending>();
        readonly IntPtr[] messages = new IntPtr[MaxMessagesPerPoll];
        Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;
        HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;
        bool isServer;
        ulong nextClientId = 1;

        public override ulong ServerClientId => 0;
        public override bool IsSupported => SteamBootstrap.Ready;

        public override void Initialize(NetworkManager networkManager = null)
        {
            statusChanged ??= Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
        }

        public override bool StartServer()
        {
            isServer = true;
            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
            return listenSocket != HSteamListenSocket.Invalid;
        }

        public override bool StartClient()
        {
            isServer = false;
            var host = new SteamNetworkingIdentity();
            host.SetSteamID(new CSteamID(HostSteamId));
            HSteamNetConnection connection = SteamNetworkingSockets.ConnectP2P(ref host, 0, 0, null);
            if (connection == HSteamNetConnection.Invalid)
            {
                return false;
            }
            connections[ServerClientId] = connection;
            clientOf[connection.m_HSteamNetConnection] = ServerClientId;
            return true;
        }

        void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t change)
        {
            HSteamNetConnection connection = change.m_hConn;
            switch (change.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    // Someone asking to join this host.
                    if (isServer && !clientOf.ContainsKey(connection.m_HSteamNetConnection))
                    {
                        if (SteamNetworkingSockets.AcceptConnection(connection) != EResult.k_EResultOK)
                        {
                            SteamNetworkingSockets.CloseConnection(connection, 0, "Couldn't accept", false);
                        }
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (isServer)
                    {
                        ulong client = nextClientId++;
                        connections[client] = connection;
                        clientOf[connection.m_HSteamNetConnection] = client;
                        pending.Enqueue(new Pending { kind = NetworkEvent.Connect, client = client });
                    }
                    else
                    {
                        pending.Enqueue(new Pending { kind = NetworkEvent.Connect, client = ServerClientId });
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (clientOf.TryGetValue(connection.m_HSteamNetConnection, out ulong gone))
                    {
                        clientOf.Remove(connection.m_HSteamNetConnection);
                        connections.Remove(gone);
                        pending.Enqueue(new Pending { kind = NetworkEvent.Disconnect, client = gone });
                    }
                    SteamNetworkingSockets.CloseConnection(connection, 0, "Closed", false);
                    break;
            }
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            receiveTime = Time.realtimeSinceStartup;
            if (pending.Count == 0)
            {
                ReceiveAll();
            }
            if (pending.Count > 0)
            {
                Pending next = pending.Dequeue();
                clientId = next.client;
                payload = next.data;
                return next.kind;
            }
            clientId = 0;
            payload = default;
            return NetworkEvent.Nothing;
        }

        void ReceiveAll()
        {
            foreach (var pair in connections)
            {
                int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(pair.Value, messages, MaxMessagesPerPoll);
                for (int i = 0; i < count; i++)
                {
                    var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messages[i]);
                    var bytes = new byte[message.m_cbSize];
                    Marshal.Copy(message.m_pData, bytes, 0, message.m_cbSize);
                    SteamNetworkingMessage_t.Release(messages[i]);
                    pending.Enqueue(new Pending { kind = NetworkEvent.Data, client = pair.Key, data = new ArraySegment<byte>(bytes) });
                }
            }
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery)
        {
            if (!connections.TryGetValue(clientId, out HSteamNetConnection connection))
            {
                return;
            }
            int flags = delivery switch
            {
                NetworkDelivery.Unreliable => Constants.k_nSteamNetworkingSend_Unreliable,
                NetworkDelivery.UnreliableSequenced => Constants.k_nSteamNetworkingSend_UnreliableNoNagle,
                _ => Constants.k_nSteamNetworkingSend_ReliableNoNagle,
            };
            GCHandle pinned = GCHandle.Alloc(payload.Array, GCHandleType.Pinned);
            try
            {
                IntPtr start = pinned.AddrOfPinnedObject() + payload.Offset;
                SteamNetworkingSockets.SendMessageToConnection(connection, start, (uint)payload.Count, flags, out long _);
            }
            finally
            {
                pinned.Free();
            }
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (connections.TryGetValue(clientId, out HSteamNetConnection connection))
            {
                SteamNetworkingSockets.CloseConnection(connection, 0, "Disconnected", true);
                connections.Remove(clientId);
                clientOf.Remove(connection.m_HSteamNetConnection);
            }
        }

        public override void DisconnectLocalClient() => DisconnectRemoteClient(ServerClientId);

        public override ulong GetCurrentRtt(ulong clientId) => 0;

        public override void Shutdown()
        {
            foreach (var connection in connections.Values)
            {
                SteamNetworkingSockets.CloseConnection(connection, 0, "Shutting down", true);
            }
            connections.Clear();
            clientOf.Clear();
            pending.Clear();
            if (listenSocket != HSteamListenSocket.Invalid)
            {
                SteamNetworkingSockets.CloseListenSocket(listenSocket);
                listenSocket = HSteamListenSocket.Invalid;
            }
        }
    }
}
