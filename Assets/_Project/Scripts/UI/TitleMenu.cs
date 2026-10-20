using System;
using GrandmasHouse.Net;
using GrandmasHouse.Saving;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandmasHouse.UI
{
    // The first screen: the game's name, start, join a friend, options, and
    // quit. Continue appears once there's a saved game (the game saves when
    // the night ends and when the host goes back to the title). Starting a
    // game hosts it, so friends can join through Steam at any time.
    [RequireComponent(typeof(MenuStack))]
    public class TitleMenu : MonoBehaviour
    {
        [SerializeField] string gameScene = "Homestead";

        MenuStack menus;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Listening for Steam invites from here on.
            NetSession.Ensure();

            menus = GetComponent<MenuStack>();
            menus.KeepRoot = true;

            RectTransform panel = UIKit.Panel("Title", transform, 640f);
            UIKit.Label(panel, "Grandma's House", 64, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.Label(panel, "Star Valley, Wyoming", 26, FontStyle.Italic, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(NetSession.LastLeaveReason))
            {
                UIKit.Label(panel, NetSession.LastLeaveReason, 22, FontStyle.Italic, TextAnchor.MiddleCenter);
                NetSession.LastLeaveReason = null;
            }
            UnityEngine.UI.Button start = null;
            if (SaveSlot.Store.Exists())
            {
                start = UIKit.Button(panel, "Continue", () => Play(continueSave: true));
            }
            var newGame = UIKit.Button(panel, "New game", () => Play(continueSave: false));
            start ??= newGame;
            UIKit.Button(panel, "Join a friend", OpenJoin);
            UIKit.Button(panel, "Options", () => OptionsScreen.Open(menus, transform));
            UIKit.Button(panel, "Quit", Quit);
            menus.Push(panel.gameObject, start);

            JoinFromCommandLine();
        }

        void Play(bool continueSave)
        {
            SaveSlot.LoadOnStart = continueSave;
            SceneManager.LoadScene(gameScene);
        }

        void OpenJoin()
        {
            RectTransform panel = UIKit.Panel("Join", transform, 720f);
            UIKit.Label(panel, "Join a friend", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.Label(panel, SteamBootstrap.Ready
                ? "Ask your friend to invite you from their pause menu, or open your Steam friends list, right-click them, and choose Join Game."
                : "Steam isn't running. Start Steam to join friends.", 24, FontStyle.Normal, TextAnchor.MiddleCenter);
            UnityEngine.UI.Button first = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            first = UIKit.Button(panel, "Join a game on this computer", () => JoinAddress("127.0.0.1"));
#endif
            var back = UIKit.Button(panel, "Back", menus.Pop);
            menus.Push(panel.gameObject, first ?? back);
        }

        static void JoinAddress(string address)
        {
            if (!NetSession.Ensure().JoinAddress(address))
            {
                NetSession.LastLeaveReason = "Couldn't reach " + address + ".";
            }
        }

        // Steam starts the game with "+connect_lobby <id>" when you accept an
        // invite while it isn't running. Development builds also take
        // "-join <address>" for testing.
        static bool joinedFromCommandLine;

        static void JoinFromCommandLine()
        {
            // Only on launch, not every time you come back to the title.
            if (joinedFromCommandLine)
            {
                return;
            }
            joinedFromCommandLine = true;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong lobby) && SteamBootstrap.Ready)
                {
                    NetSession.Ensure().JoinLobby(new CSteamID(lobby));
                    return;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (args[i] == "-join")
                {
                    DevBot.StartIfAsked();
                    JoinAddress(args[i + 1]);
                    return;
                }
#endif
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
