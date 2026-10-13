using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandmasHouse.UI
{
    // The first screen: the game's name, start, options, and quit.
    // Continue appears once there's a saved game to continue.
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
            var start = UIKit.Button(panel, "New game", () => SceneManager.LoadScene(gameScene));
            UIKit.Button(panel, "Options", () => OptionsScreen.Open(menus, transform));
            UIKit.Button(panel, "Quit", Quit);
            menus.Push(panel.gameObject, start);
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
