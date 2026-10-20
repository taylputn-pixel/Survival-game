using System;
using System.Collections.Generic;
using System.Text;
using Steamworks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandmasHouse.Net
{
    // One game shared by up to four players. Whoever starts the game hosts
    // it (playing alone is hosting alone); friends join through Steam, or
    // by address in development builds. Lives on the NetSession prefab in
    // Resources, next to the NetworkManager and both transports.
    [RequireComponent(typeof(NetworkManager))]
    public class NetSession : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        public const ushort Port = 7777;
        const string HostKey = "host";

        [SerializeField] string titleScene = "Title";
        [SerializeField] string gameScene = "Homestead";

        public static NetSession Instance { get; private set; }
        // Why the last game ended, for the title screen to show once.
        public static string LastLeaveReason { get; set; }

        public NetworkManager Manager { get; private set; }
        public CSteamID Lobby { get; private set; } = CSteamID.Nil;
        public bool UsingSteam { get; private set; }
        public bool IsHost => Manager != null && Manager.IsHost;
        public bool IsGuest => Manager != null && Manager.IsClient && !Manager.IsServer;
        // Anyone besides the host connected right now.
        public bool HasGuests => Manager != null && Manager.IsServer && Manager.ConnectedClientsIds.Count > 1;

        readonly Dictionary<ulong, string> keys = new Dictionary<ulong, string>();
        Callback<GameLobbyJoinRequested_t> joinRequested;
        Callback<LobbyEnter_t> lobbyEntered;
        CallResult<LobbyCreated_t> lobbyCreated;
        bool leaving;

        public static NetSession Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }
            SteamBootstrap.Ensure();
            var prefab = Resources.Load<NetSession>("NetSession");
            var session = Instantiate(prefab);
            session.name = prefab.name;
            DontDestroyOnLoad(session.gameObject);
            return session;
        }

        void Awake()
        {
            Instance = this;
            Manager = GetComponent<NetworkManager>();
            Manager.NetworkConfig.ConnectionApproval = true;
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientDisconnectCallback += OnClientGone;
            if (SteamBootstrap.Ready)
            {
                joinRequested = Callback<GameLobbyJoinRequested_t>.Create(r => JoinLobby(r.m_steamIDLobby));
                lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
                lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // The key a player's saved state is kept under: their Steam ID, or
        // this computer's ID when playing without Steam.
        static string MyKey => SteamBootstrap.Ready ? SteamBootstrap.Me.m_SteamID.ToString() : SystemInfo.deviceUniqueIdentifier;

        public string KeyOf(ulong clientId) => keys.TryGetValue(clientId, out string key) ? key : HostKey;
        public static string LocalHostKey => HostKey;

        void UseTransport(bool steam)
        {
            UsingSteam = steam;
            Manager.NetworkConfig.NetworkTransport = steam
                ? (NetworkTransport)GetComponent<SteamTransport>()
                : GetComponent<UnityTransport>();
        }

        // Skip Steam and connect by address: for testing two copies of the
        // game on one computer ("-direct" on the command line, or tests).
        public static bool Direct { get; set; } = HasArg("-direct");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Instance = null;
            Direct = HasArg("-direct");
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        // Starts hosting in the scene that's already loaded (the Homestead).
        public bool Host()
        {
            UseTransport(SteamBootstrap.Ready && !Direct);
            if (!UsingSteam)
            {
                GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", Port, "0.0.0.0");
            }
            Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(MyKey);
            if (!Manager.StartHost())
            {
                if (!UsingSteam)
                {
                    return false;
                }
                // Steam wouldn't open a connection: play anyway, friends by address.
                Debug.LogWarning("Steam hosting failed; hosting without Steam.");
                UseTransport(false);
                GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", Port, "0.0.0.0");
                if (!Manager.StartHost())
                {
                    return false;
                }
            }
            if (UsingSteam)
            {
                lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers));
            }
            return true;
        }

        // Joins a friend's game over Steam (from an invite or Join Game).
        public void JoinLobby(CSteamID lobby)
        {
            if (Manager.IsListening)
            {
                return;
            }
            SteamMatchmaking.JoinLobby(lobby);
        }

        // Joins by address, for testing on one computer or a home network.
        public bool JoinAddress(string address)
        {
            UseTransport(false);
            GetComponent<UnityTransport>().SetConnectionData(address, Port);
            Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(MyKey);
            return Manager.StartClient();
        }

        void OnLobbyCreated(LobbyCreated_t result, bool failed)
        {
            if (failed || result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning("Steam: couldn't open the game to friends.");
                return;
            }
            Lobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(Lobby, "host", SteamBootstrap.Me.m_SteamID.ToString());
            SteamMatchmaking.SetLobbyData(Lobby, "game", "GrandmasHouse");
        }

        void OnLobbyEntered(LobbyEnter_t entered)
        {
            var lobby = new CSteamID(entered.m_ulSteamIDLobby);
            if (Manager.IsServer)
            {
                return;
            }
            Lobby = lobby;
            if (!ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, "host"), out ulong host))
            {
                host = SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;
            }
            UseTransport(true);
            GetComponent<SteamTransport>().HostSteamId = host;
            Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(MyKey);
            Manager.StartClient();
        }

        // Opens Steam's invite list for this game.
        public void InviteFriends()
        {
            if (UsingSteam && Lobby.IsValid())
            {
                SteamFriends.ActivateGameOverlayInviteDialog(Lobby);
            }
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (Manager.ConnectedClientsIds.Count >= MaxPlayers)
            {
                response.Approved = false;
                response.Reason = "The game is full.";
                return;
            }
            string key = request.Payload != null && request.Payload.Length > 0 ? Encoding.UTF8.GetString(request.Payload) : HostKey;
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                key = HostKey;
            }
            keys[request.ClientNetworkId] = key;
            response.Approved = true;
            response.CreatePlayerObject = true;
            Transform spot = SpawnPoints.Pick(Manager.ConnectedClientsIds.Count);
            if (spot != null)
            {
                response.Position = spot.position;
                response.Rotation = spot.rotation;
            }
        }

        void OnClientGone(ulong clientId)
        {
            if (Manager.IsServer && clientId != NetworkManager.ServerClientId)
            {
                keys.Remove(clientId);
                ItemNet.LetGoFor(clientId);
                return;
            }
            // We were the guest and the connection ended. Netcode is still
            // in the middle of handling that, so leave on the next frame.
            if (!leaving)
            {
                LastLeaveReason = string.IsNullOrEmpty(Manager.DisconnectReason) ? "Lost the connection to the host." : Manager.DisconnectReason;
                leaveNextFrame = true;
            }
        }

        bool leaveNextFrame;

        void Update()
        {
            if (leaveNextFrame)
            {
                leaveNextFrame = false;
                Leave();
            }
        }

        // Ends this game for this computer and goes back to the title. When
        // the host leaves, the game ends for everyone.
        public void Leave()
        {
            leaving = true;
            End();
            SceneManager.LoadScene(titleScene);
        }

        // Shuts the connection down without changing scenes.
        public void End()
        {
            if (Instance != this)
            {
                return;
            }
            // Shutting down reports our own disconnect; that isn't news here.
            leaving = true;
            Manager.OnClientDisconnectCallback -= OnClientGone;
            Manager.ConnectionApprovalCallback = null;
            if (UsingSteam && Lobby.IsValid())
            {
                SteamMatchmaking.LeaveLobby(Lobby);
            }
            Lobby = CSteamID.Nil;
            if (Manager != null && Manager.IsListening)
            {
                Manager.Shutdown(true);
            }
            // Gone right away, so a fresh session can start in the next scene.
            Instance = null;
            DestroyImmediate(gameObject);
        }

        public string GameScene => gameScene;
    }
}
