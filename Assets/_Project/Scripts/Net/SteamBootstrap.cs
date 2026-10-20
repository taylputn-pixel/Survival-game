using System;
using Steamworks;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // Starts Steam once for the whole run and keeps its callbacks flowing.
    // Without the Steam client running, the game still works; friends just
    // join by address instead (development builds only).
    public class SteamBootstrap : MonoBehaviour
    {
        static SteamBootstrap instance;

        public static bool Ready { get; private set; }
        public static CSteamID Me => Ready ? SteamUser.GetSteamID() : CSteamID.Nil;
        public static string MyName => Ready ? SteamFriends.GetPersonaName() : Environment.UserName;

        // A fresh start each time Play begins, even when the editor keeps
        // static values between runs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Ready = false;
            instance = null;
        }

        public static void Ensure()
        {
            if (instance != null)
            {
                return;
            }
            var go = new GameObject("Steam");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SteamBootstrap>();
        }

        void Awake()
        {
            if (Ready)
            {
                return;
            }
            try
            {
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    Debug.LogWarning("Steam: the Steamworks library doesn't match this build.");
                    return;
                }
                Ready = SteamAPI.Init();
                if (Ready)
                {
                    SteamNetworkingUtils.InitRelayNetworkAccess();
                }
                else
                {
                    Debug.Log("Steam isn't running; playing without it.");
                }
            }
            catch (Exception e)
            {
                // No Steam client or library: fine for playing alone.
                Debug.Log("Steam unavailable: " + e.Message);
                Ready = false;
            }
        }

        void Update()
        {
            if (!Ready)
            {
                return;
            }
            try
            {
                SteamAPI.RunCallbacks();
            }
            catch (InvalidOperationException)
            {
                // Steam was shut down underneath us; carry on without it.
                Ready = false;
            }
        }

        void OnApplicationQuit()
        {
            // Steam can't start twice in one process, and in the editor
            // leaving Play mode isn't the end of it: keep Steam running there.
            if (Ready && !Application.isEditor)
            {
                SteamAPI.Shutdown();
                Ready = false;
            }
        }
    }
}
