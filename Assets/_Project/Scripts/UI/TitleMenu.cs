using GrandmasHouse.Saving;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandmasHouse.UI
{
    // The first screen: the game's name, start, options, and quit.
    // Continue appears once there's a saved game (the game saves at bedtime
    // and when you go back to the title). A new game replaces it the next
    // time it saves.
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

            menus = GetComponent<MenuStack>();
            menus.KeepRoot = true;

            RectTransform panel = UIKit.Panel("Title", transform, 640f);
            UIKit.Label(panel, "Grandma's House", 64, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.Label(panel, "Star Valley, Wyoming", 26, FontStyle.Italic, TextAnchor.MiddleCenter);
            UnityEngine.UI.Button start = null;
            if (SaveSlot.Store.Exists())
            {
                start = UIKit.Button(panel, "Continue", () => Play(continueSave: true));
            }
            var newGame = UIKit.Button(panel, "New game", () => Play(continueSave: false));
            start ??= newGame;
            UIKit.Button(panel, "Options", () => OptionsScreen.Open(menus, transform));
            UIKit.Button(panel, "Quit", Quit);
            menus.Push(panel.gameObject, start);
        }

        void Play(bool continueSave)
        {
            SaveSlot.LoadOnStart = continueSave;
            SceneManager.LoadScene(gameScene);
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
